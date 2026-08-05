using System;
using System.Collections.Generic;
using NUnit.Framework;
using DWSIM.Thermodynamics.PropertyPackages.Auxiliary;

namespace DWSIM.Thermodynamics.Derivatives.Tests
{
    /// <summary>
    /// Validates the closed-form NRTL activity-coefficient derivatives (NRTL.GAMMA_DERIVS) against
    /// central finite differences of GAMMA_MR: d(ln gamma)/dT and d(ln gamma)/dn (total-moles = 1 basis).
    /// Interaction parameters are supplied synthetically (with non-zero B and C terms so dtau/dT is fully
    /// exercised), so no thermo database lookup is required for the assertions.
    /// </summary>
    [TestFixture]
    public class NRTLDerivativeTests
    {
        private static NRTL BuildModel()
        {
            var m = new NRTL();
            var ip = new Dictionary<string, Dictionary<string, NRTL_IPData>>();
            AddPair(ip, "0", "1", a12: 350.0, a21: -120.0, b12: 0.6, b21: -0.25, c12: 0.0012, c21: -0.0007, alpha: 0.3);
            AddPair(ip, "0", "2", a12: 210.0, a21: 95.0, b12: -0.4, b21: 0.15, c12: 0.0009, c21: 0.0004, alpha: 0.47);
            AddPair(ip, "1", "2", a12: -60.0, a21: 180.0, b12: 0.2, b21: -0.3, c12: -0.0005, c21: 0.0011, alpha: 0.3);
            m.InteractionParameters = ip;
            return m;
        }

        private static void AddPair(Dictionary<string, Dictionary<string, NRTL_IPData>> ip, string i, string j,
            double a12, double a21, double b12, double b21, double c12, double c21, double alpha)
        {
            var d = new NRTL_IPData { ID1 = i, ID2 = j, A12 = a12, A21 = a21, B12 = b12, B21 = b21, C12 = c12, C21 = c21, alpha12 = alpha };
            if (!ip.ContainsKey(i)) ip[i] = new Dictionary<string, NRTL_IPData>();
            ip[i][j] = d;
        }

        private static double[] Lng(NRTL m, double T, double[] x, string[] ids)
        {
            var g = (double[])m.GAMMA_MR(T, x, ids);
            var r = new double[g.Length];
            for (int i = 0; i < g.Length; i++) r[i] = Math.Log(g[i]);
            return r;
        }

        [Test]
        public void NRTL_MatchesFiniteDifference()
        {
            var m = BuildModel();
            var ids = new[] { "0", "1", "2" };
            var x = new[] { 0.3, 0.25, 0.45 };
            double T = 340.0;
            int n = x.Length - 1;

            var res = (object[])m.GAMMA_DERIVS(T, x, ids);
            var dT = (double[])res[1];
            var dn = (double[,])res[2];

            // d(ln gamma)/dT central FD
            double epsT = 1e-3;
            var lp = Lng(m, T + epsT, x, ids);
            var lm = Lng(m, T - epsT, x, ids);
            for (int i = 0; i <= n; i++)
            {
                double fd = (lp[i] - lm[i]) / (2 * epsT);
                AssertClose(dT[i], fd, 1e-4, 1e-8, $"dlngamma/dT comp {i}");
            }

            // d(ln gamma)/dn_k central FD (total-moles = 1 basis)
            double step = 1e-6;
            for (int k = 0; k <= n; k++)
            {
                var lpj = Lng(m, T, Renorm(x, k, +step), ids);
                var lmj = Lng(m, T, Renorm(x, k, -step), ids);
                for (int i = 0; i <= n; i++)
                {
                    double fd = (lpj[i] - lmj[i]) / (2 * step);
                    AssertClose(dn[i, k], fd, 1e-3, 1e-6, $"dlngamma/dn i={i} k={k}");
                }
            }
        }

        private static double[] Renorm(double[] x, int k, double step)
        {
            int n = x.Length;
            var y = new double[n];
            double s = 0;
            for (int i = 0; i < n; i++) y[i] = x[i];
            y[k] += step;
            for (int i = 0; i < n; i++) s += y[i];
            for (int i = 0; i < n; i++) y[i] /= s;
            return y;
        }

        private static void AssertClose(double got, double expected, double relTol, double absTol, string msg)
        {
            double diff = Math.Abs(got - expected);
            if (diff <= absTol) return;
            double rel = diff / (Math.Abs(expected) + 1e-30);
            Assert.That(rel, Is.LessThanOrEqualTo(relTol),
                $"{msg}: analytical={got:E6} fd={expected:E6} rel={rel:E3} abs={diff:E3}");
        }
    }
}
