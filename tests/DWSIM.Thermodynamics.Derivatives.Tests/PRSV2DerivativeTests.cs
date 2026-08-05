using System;
using NUnit.Framework;
using DWSIM.Thermodynamics.PropertyPackages.Auxiliary;

namespace DWSIM.Thermodynamics.Derivatives.Tests
{
    /// <summary>
    /// Validates the closed-form temperature derivative of the PRSV2 fugacity coefficients
    /// (PRSV2.CalcLnFugDT) against central finite differences of CalcLnFug. Exercises both the
    /// constant-kappa branch (kappa1*kappa2*kappa3 = 0) and the temperature-dependent kappa(Tr) branch,
    /// with the composition-dependent Stryjek-Vera mixing rule (non-zero kij and kij2). No DB is used.
    /// </summary>
    [TestFixture]
    public class PRSV2DerivativeTests
    {
        private static readonly double[] Tc = { 190.56, 305.32, 369.83 };
        private static readonly double[] Pc = { 4599000.0, 4872000.0, 4248000.0 };
        private static readonly double[] W = { 0.011, 0.099, 0.152 };

        private static (double[,] kij, double[,] kij2) Kij()
        {
            int n = 3;
            var k = new double[n, n];
            var k2 = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (i != j) { k[i, j] = 0.01; k2[i, j] = 0.005; }
            return (k, k2);
        }

        // tipo, T, P, z, tdependent-kappa?
        [TestCase("V", 250.0, 2e6, new[] { 0.8, 0.15, 0.05 }, false)]
        [TestCase("L", 250.0, 5e6, new[] { 0.2, 0.3, 0.5 }, false)]
        [TestCase("V", 250.0, 2e6, new[] { 0.8, 0.15, 0.05 }, true)]
        [TestCase("L", 250.0, 5e6, new[] { 0.2, 0.3, 0.5 }, true)]
        [TestCase("L", 300.0, 4e6, new[] { 0.1, 0.3, 0.6 }, true)]
        public void PRSV2_dLnPhidT_MatchesFiniteDifference(string tipo, double T, double P, double[] z, bool tdep)
        {
            var m = new PRSV2();
            var (kij, kij2) = Kij();
            int n = z.Length - 1;
            double[] k1 = tdep ? new[] { 0.03, 0.02, 0.04 } : new[] { 0.0, 0.0, 0.0 };
            double[] k2 = tdep ? new[] { 0.2, 0.15, 0.25 } : new[] { 0.0, 0.0, 0.0 };
            double[] k3 = tdep ? new[] { 0.7, 0.7, 0.7 } : new[] { 0.0, 0.0, 0.0 };

            var ana = (double[])m.CalcLnFugDT(T, P, z, kij, kij2, k1, k2, k3, Tc, Pc, W, tipo);

            double eps = 0.01;
            var fp = (object[])m.CalcLnFug(T + eps, P, z, kij, kij2, k1, k2, k3, Tc, Pc, W, null, tipo);
            var fm = (object[])m.CalcLnFug(T - eps, P, z, kij, kij2, k1, k2, k3, Tc, Pc, W, null, tipo);

            for (int i = 0; i <= n; i++)
            {
                double fd = (Convert.ToDouble(fp[i]) - Convert.ToDouble(fm[i])) / (2 * eps);
                double rel = Math.Abs(ana[i] - fd) / (Math.Abs(fd) + 1e-8);
                Assert.That(rel, Is.LessThanOrEqualTo(1e-5),
                    $"dlnphi/dT comp {i}: analytical={ana[i]:E6} fd={fd:E6} rel={rel:E3}");
            }
        }
    }
}
