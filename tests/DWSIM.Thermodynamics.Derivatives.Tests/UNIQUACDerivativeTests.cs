using System;
using System.Collections.Generic;
using NUnit.Framework;
using DWSIM.Thermodynamics.PropertyPackages.Auxiliary;

namespace DWSIM.Thermodynamics.Derivatives.Tests
{
    /// <summary>
    /// Validates the closed-form UNIQUAC activity-coefficient derivatives (UNIQUAC.GAMMA_DERIVS) against
    /// central finite differences of GAMMA_MR: d(ln gamma)/dT and d(ln gamma)/dn (total-moles = 1 basis).
    /// Interaction parameters (with non-zero B and C) and r/q surface/volume parameters are supplied
    /// synthetically; no thermo database lookup is required.
    /// </summary>
    [TestFixture]
    public class UNIQUACDerivativeTests
    {
        private static readonly double[] VR = { 0.92, 2.1055, 1.4311 }; // water, ethanol, methanol
        private static readonly double[] VQ = { 1.40, 1.9720, 1.4320 };

        private static UNIQUAC BuildModel()
        {
            var m = new UNIQUAC();
            m.InteractionParameters.Clear();
            AddPair(m.InteractionParameters, "0", "1", a12: 258.0, a21: -110.0, b12: 0.5, b21: -0.2, c12: 0.001, c21: -0.0006);
            AddPair(m.InteractionParameters, "0", "2", a12: -30.0, a21: 140.0, b12: -0.3, b21: 0.2, c12: 0.0008, c21: 0.0003);
            AddPair(m.InteractionParameters, "1", "2", a12: 90.0, a21: -55.0, b12: 0.15, b21: -0.25, c12: -0.0004, c21: 0.0009);
            return m;
        }

        private static void AddPair(Dictionary<string, Dictionary<string, UNIQUAC_IPData>> ip, string i, string j,
            double a12, double a21, double b12, double b21, double c12, double c21)
        {
            var d = new UNIQUAC_IPData { A12 = a12, A21 = a21, B12 = b12, B21 = b21, C12 = c12, C21 = c21 };
            if (!ip.ContainsKey(i)) ip[i] = new Dictionary<string, UNIQUAC_IPData>();
            ip[i][j] = d;
        }

        private static double[] Lng(UNIQUAC m, double T, double[] x, string[] ids)
        {
            var g = (double[])m.GAMMA_MR(T, x, ids, VQ, VR);
            var r = new double[g.Length];
            for (int i = 0; i < g.Length; i++) r[i] = Math.Log(g[i]);
            return r;
        }

        [Test]
        public void UNIQUAC_MatchesFiniteDifference()
        {
            var m = BuildModel();
            var ids = new[] { "0", "1", "2" };
            var x = new[] { 0.3, 0.25, 0.45 };
            double T = 340.0;
            int n = x.Length - 1;

            var res = (object[])m.GAMMA_DERIVS(T, x, ids, VQ, VR);
            var dT = (double[])res[1];
            var dn = (double[,])res[2];

            double epsT = 1e-3;
            var lp = Lng(m, T + epsT, x, ids);
            var lm = Lng(m, T - epsT, x, ids);
            for (int i = 0; i <= n; i++)
            {
                double fd = (lp[i] - lm[i]) / (2 * epsT);
                AssertClose(dT[i], fd, 1e-4, 1e-8, $"dlngamma/dT comp {i}");
            }

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
