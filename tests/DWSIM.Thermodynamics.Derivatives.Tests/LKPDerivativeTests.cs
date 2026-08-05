using System;
using NUnit.Framework;
using DWSIM.Thermodynamics.PropertyPackages.Auxiliary;

namespace DWSIM.Thermodynamics.Derivatives.Tests
{
    /// <summary>
    /// Validates the closed-form temperature derivative of the Lee-Kesler-Plocker fugacity coefficients
    /// (LeeKeslerPlocker.CalcLnFugDT) against central finite differences of CalcLnFugCPU. The fugacity part
    /// is fully analytical (BWR implicit differentiation, two reference fluids); the enthalpy-departure
    /// T-derivative uses a cheap finite difference. Critical constants are supplied explicitly (no DB).
    /// </summary>
    [TestFixture]
    public class LKPDerivativeTests
    {
        private static readonly double[] Tc = { 190.56, 305.32, 369.83 };     // C1, C2, C3
        private static readonly double[] Pc = { 4599000.0, 4872000.0, 4248000.0 };
        private static readonly double[] W = { 0.011, 0.099, 0.152 };
        private static readonly double[] Vc = { 0.0986, 0.1455, 0.200 };
        private static readonly double[] MM = { 16.04, 30.07, 44.1 };

        private static double[,] Kij()
        {
            int n = 3;
            var k = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    k[i, j] = 1.0; // tcjk uses VKij as a multiplier on sqrt(Tc_i Tc_j)
            return k;
        }

        [TestCase("V", 250.0, 1e6, new[] { 0.8, 0.15, 0.05 })]
        [TestCase("L", 250.0, 5e6, new[] { 0.2, 0.3, 0.5 })]
        [TestCase("V", 320.0, 2e6, new[] { 0.5, 0.3, 0.2 })]
        [TestCase("L", 300.0, 4e6, new[] { 0.1, 0.3, 0.6 })]
        public void LKP_dLnPhidT_MatchesFiniteDifference(string tipo, double T, double P, double[] z)
        {
            var m = new LeeKeslerPlocker();
            var kij = Kij();
            int n = z.Length - 1;

            var ana = (double[])m.CalcLnFugDT(tipo, T, P, z, kij, Tc, Pc, W, MM, Vc);

            double eps = 0.01;
            var fp = (double[])m.CalcLnFugCPU(tipo, T + eps, P, z, kij, Tc, Pc, W, MM, Vc, 0.0);
            var fm = (double[])m.CalcLnFugCPU(tipo, T - eps, P, z, kij, Tc, Pc, W, MM, Vc, 0.0);

            for (int i = 0; i <= n; i++)
            {
                double fd = (fp[i] - fm[i]) / (2 * eps);
                double rel = Math.Abs(ana[i] - fd) / (Math.Abs(fd) + 1e-8);
                Assert.That(rel, Is.LessThanOrEqualTo(1e-5),
                    $"dlnphi/dT comp {i}: analytical={ana[i]:E6} fd={fd:E6} rel={rel:E3}");
            }
        }

        [TestCase("V", 250.0, 1e6, new[] { 0.8, 0.15, 0.05 })]
        [TestCase("L", 250.0, 5e6, new[] { 0.2, 0.3, 0.5 })]
        [TestCase("V", 320.0, 2e6, new[] { 0.5, 0.3, 0.2 })]
        public void LKP_dLnPhidn_MatchesFiniteDifference(string tipo, double T, double P, double[] z)
        {
            var m = new LeeKeslerPlocker();
            var kij = Kij();
            int n = z.Length - 1;

            var ana = (double[,])m.CalcLnFugDN(tipo, T, P, z, kij, Tc, Pc, W, MM, Vc);

            double step = 1e-6;
            for (int k = 0; k <= n; k++)
            {
                var fp = (double[])m.CalcLnFugCPU(tipo, T, P, Renorm(z, k, +step), kij, Tc, Pc, W, MM, Vc, 0.0);
                var fm = (double[])m.CalcLnFugCPU(tipo, T, P, Renorm(z, k, -step), kij, Tc, Pc, W, MM, Vc, 0.0);
                for (int i = 0; i <= n; i++)
                {
                    double fd = (fp[i] - fm[i]) / (2 * step);
                    double rel = Math.Abs(ana[i, k] - fd) / (Math.Abs(fd) + 1e-5);
                    Assert.That(rel, Is.LessThanOrEqualTo(1e-3),
                        $"dlnphi/dn i={i} k={k}: analytical={ana[i, k]:E6} fd={fd:E6} rel={rel:E3}");
                }
            }
        }

        private static double[] Renorm(double[] x, int k, double step)
        {
            int nn = x.Length;
            var y = new double[nn];
            double s = 0;
            for (int i = 0; i < nn; i++) y[i] = x[i];
            y[k] += step;
            for (int i = 0; i < nn; i++) s += y[i];
            for (int i = 0; i < nn; i++) y[i] /= s;
            return y;
        }
    }
}
