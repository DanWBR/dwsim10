using System;
using System.Collections.Generic;
using NUnit.Framework;
using DWSIM.Thermodynamics.PropertyPackages.ThermoPlugs;

namespace DWSIM.Thermodynamics.Derivatives.Tests
{
    /// <summary>
    /// Validates the closed-form temperature and composition derivatives of the cubic-EOS fugacity
    /// coefficients (CubicEOSDerivatives.Calc) against high-quality central finite differences of the
    /// production fugacity routine (ThermoPlugs.PR/SRK.CalcLnFug), plus the Maxwell symmetry relation
    /// d(ln phi_i)/dn_j = d(ln phi_j)/dn_i. No thermo database is required: Tc/Pc/omega are supplied
    /// explicitly so the pure EOS math is exercised in isolation.
    /// </summary>
    [TestFixture]
    public class CubicEOSDerivativeTests
    {
        // ---- component constants: methane, ethane, propane, n-butane, CO2 ----
        private static readonly Dictionary<string, double[]> Comp = new Dictionary<string, double[]>
        {
            // name -> { Tc[K], Pc[Pa], omega }
            { "C1", new[] { 190.56, 4599000.0, 0.011 } },
            { "C2", new[] { 305.32, 4872000.0, 0.099 } },
            { "C3", new[] { 369.83, 4248000.0, 0.152 } },
            { "nC4", new[] { 425.12, 3796000.0, 0.200 } },
            { "CO2", new[] { 304.21, 7383000.0, 0.2236 } },
            { "nC10", new[] { 617.70, 2110000.0, 0.4923 } }, // omega > 0.491 (PR78 high-w kappa branch)
        };

        public class Case
        {
            public string Name;
            public int Eos;              // 0 = PR, 1 = SRK
            public string[] Comps;
            public double[] X;
            public double T, P;
            public int Phase;            // 0 = liquid, 1 = vapor
            public override string ToString() => Name;
        }

        private static IEnumerable<Case> Cases()
        {
            foreach (int eos in new[] { 0, 1, 2 })
            {
                string tag = eos == 0 ? "PR" : eos == 1 ? "SRK" : "PR78";
                var c123 = new[] { "C1", "C2", "C3" };
                yield return new Case { Name = $"{tag} C1C2C3 liquid 250K 5MPa", Eos = eos, Comps = c123, X = new[] { 0.2, 0.3, 0.5 }, T = 250.0, P = 5e6, Phase = 0 };
                yield return new Case { Name = $"{tag} C1C2C3 vapour 250K 1MPa", Eos = eos, Comps = c123, X = new[] { 0.8, 0.15, 0.05 }, T = 250.0, P = 1e6, Phase = 1 };
                yield return new Case { Name = $"{tag} C1C2C3 liquid 320K 3MPa", Eos = eos, Comps = c123, X = new[] { 0.2, 0.3, 0.5 }, T = 320.0, P = 3e6, Phase = 0 };
                // wide-K with a trace component
                yield return new Case { Name = $"{tag} C1C2C3nC4 trace-nC4 liquid", Eos = eos, Comps = new[] { "C1", "C2", "C3", "nC4" }, X = new[] { 0.3, 0.3, 0.399999, 1e-6 }, T = 280.0, P = 4e6, Phase = 0 };
                // CO2-rich, moderately high pressure
                yield return new Case { Name = $"{tag} CO2C1 vapour 260K 3MPa", Eos = eos, Comps = new[] { "CO2", "C1" }, X = new[] { 0.7, 0.3 }, T = 260.0, P = 3e6, Phase = 1 };
                // heavy component (omega > 0.491) to exercise the PR78 high-acentric-factor kappa branch,
                // away from any critical point (C1 supercritical, nC10 well into the liquid region)
                yield return new Case { Name = $"{tag} C1nC10 liquid 320K 3MPa", Eos = eos, Comps = new[] { "C1", "nC10" }, X = new[] { 0.2, 0.8 }, T = 320.0, P = 3e6, Phase = 0 };
            }
        }

        private static (double[] Tc, double[] Pc, double[] w) Constants(string[] comps)
        {
            int n = comps.Length;
            var Tc = new double[n]; var Pc = new double[n]; var w = new double[n];
            for (int i = 0; i < n; i++) { Tc[i] = Comp[comps[i]][0]; Pc[i] = Comp[comps[i]][1]; w[i] = Comp[comps[i]][2]; }
            return (Tc, Pc, w);
        }

        private static double[] CalcLnFug(int eos, double T, double P, double[] Vx, double[,] kij, double[] Tc, double[] Pc, double[] w, int phase)
        {
            if (eos == 0)
                return (double[])new PR().CalcLnFug(T, P, Vx, kij, Tc, Pc, w, null, phase);
            if (eos == 1)
                return (double[])new SRK().CalcLnFug(T, P, Vx, kij, Tc, Pc, w, null, phase);
            return (double[])new PR78().CalcLnFug(T, P, Vx, kij, Tc, Pc, w, null, phase);
        }

        [TestCaseSource(nameof(Cases))]
        public void MatchesFiniteDifference(Case c)
        {
            int n = c.X.Length - 1;
            var (Tc, Pc, w) = Constants(c.Comps);
            var kij = new double[n + 1, n + 1];

            var res = (object[])CubicEOSDerivatives.Calc(c.Eos, c.T, c.P, c.X, kij, Tc, Pc, w, c.Phase);
            var lnphi = (double[])res[0];
            var dT = (double[])res[1];
            var dn = (double[,])res[2];

            // (0) analytical ln(phi) reproduces the production routine
            var refLnphi = CalcLnFug(c.Eos, c.T, c.P, c.X, kij, Tc, Pc, w, c.Phase);
            for (int i = 0; i <= n; i++)
                AssertClose(lnphi[i], refLnphi[i], 1e-6, 1e-7, $"ln(phi) comp {i}");

            // (1) d(ln phi)/dT vs central FD
            double epsT = 1e-3;
            var fp = CalcLnFug(c.Eos, c.T + epsT, c.P, c.X, kij, Tc, Pc, w, c.Phase);
            var fm = CalcLnFug(c.Eos, c.T - epsT, c.P, c.X, kij, Tc, Pc, w, c.Phase);
            for (int i = 0; i <= n; i++)
            {
                double fd = (fp[i] - fm[i]) / (2 * epsT);
                AssertClose(dT[i], fd, 1e-4, 1e-7, $"dlnphi/dT comp {i}");
            }

            // (2) d(ln phi)/dn_j vs central FD (total-moles = 1 basis)
            double dnStep = 1e-6;
            for (int j = 0; j <= n; j++)
            {
                var gp = PerturbAndCalc(c, kij, Tc, Pc, w, j, +dnStep);
                var gm = PerturbAndCalc(c, kij, Tc, Pc, w, j, -dnStep);
                for (int i = 0; i <= n; i++)
                {
                    double fd = (gp[i] - gm[i]) / (2 * dnStep);
                    AssertClose(dn[i, j], fd, 1e-3, 1e-5, $"dlnphi/dn i={i} j={j}");
                }
            }

            // (3) Maxwell symmetry: d(ln phi_i)/dn_j == d(ln phi_j)/dn_i
            for (int i = 0; i <= n; i++)
                for (int j = i + 1; j <= n; j++)
                    AssertClose(dn[i, j], dn[j, i], 1e-6, 1e-8, $"symmetry i={i} j={j}");
        }

        private static double[] PerturbAndCalc(Case c, double[,] kij, double[] Tc, double[] Pc, double[] w, int j, double step)
        {
            int n = c.X.Length - 1;
            var x = new double[n + 1];
            double s = 0.0;
            for (int k = 0; k <= n; k++) { x[k] = c.X[k]; }
            x[j] += step;
            for (int k = 0; k <= n; k++) s += x[k];
            for (int k = 0; k <= n; k++) x[k] /= s;
            return CalcLnFug(c.Eos, c.T, c.P, x, kij, Tc, Pc, w, c.Phase);
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
