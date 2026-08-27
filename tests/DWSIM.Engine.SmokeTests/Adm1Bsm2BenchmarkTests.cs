//    The full ADM1 (Batstone et al. 2002) has one canonical validation reference: the open-loop
//    steady state of the digester in the IWA Benchmark Simulation Model No. 2, as published by
//    Rosen & Jeppsson (2006), "Aspects on ADM1 Implementation within the BSM2 Framework" (Lund
//    University, LUTEDX/TEIE-7224), and shipped as the reference output in the BSM2 distribution
//    (github.com/wwtmodels/Benchmark-Simulation-Models, BSM2_R2019b, Results/BSM2_steady_state.pdf).
//
//    This test drives the ADM1 integrator with the exact BSM2 digester influent (post ASM2ADM
//    interface), the BSM2 operating point (V_liq = 3400 m3, Q = 178.4674 m3/d, T = 35 C, HRT 19 d),
//    and the benchmark parameter set, integrates to steady state, and checks every state against
//    the published values. Reproducing this table to well under 1 % is the accepted proof that an
//    ADM1 implementation is correct; it guards the core biochemistry, acid-base equilibria, gas
//    transfer and the van't Hoff temperature corrections against regressions.

using System;
using NUnit.Framework;
using A = DWSIM.UnitOperations.Reactors.ADM1;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class Adm1Bsm2BenchmarkTests
    {
        // BSM2 digester influent (post ASM2ADM interface), in ADM1State vector order, kg COD/m3
        // unless the state is a molar one (S_IC, S_IN, S_cat, S_an in kmol/m3).
        private static double[] Bsm2Influent()
        {
            var sin = new double[A.ADM1State.NDynamic];
            sin[1] = 0.04388;    // S_aa
            sin[9] = 0.0079326;  // S_IC
            sin[10] = 0.0019721; // S_IN
            sin[11] = 0.028067;  // S_I
            sin[13] = 3.7236;    // X_ch
            sin[14] = 15.9235;   // X_pr
            sin[15] = 8.047;     // X_li
            sin[23] = 17.0106;   // X_I
            sin[25] = 0.0052101; // S_an
            return sin;
        }

        private static A.ADM1State SolveBsm2SteadyState()
        {
            var p = new A.ADM1Parameters();
            p.ResetToBenchmark();
            p.Operating.V_liq = 3400.0;
            p.Operating.V_gas = 300.0;
            // Base acid-base/Henry constants are held at 25 C (T_base_K default) and corrected to the
            // operating temperature by van't Hoff, so only the operating temperature is set here.
            p.Physicochemical.T_op_K = 308.15;
            p.Sulfate.Enabled = false;

            var res = A.ADM1Integrator.Integrate(p.InitialConditions.Clone(), p, 178.4674, Bsm2Influent(), 0.0, 300.0);

            Assert.That(res.Converged, Is.True, "ADM1 integration did not converge on the BSM2 influent.");
            Assert.That(Math.Abs(res.SteadyStateResidual_perDay), Is.LessThan(0.05),
                        "steady-state residual too large: " + res.SteadyStateResidual_perDay);
            return res.FinalState;
        }

        [Test]
        public void Adm1_reproduces_bsm2_open_loop_steady_state()
        {
            var s = SolveBsm2SteadyState();

            // (name, got, reference, tolerance %). Reference = BSM2_steady_state.pdf effluent column.
            var checks = new (string Name, double Got, double Ref, double TolPct)[]
            {
                ("S_su",  s.S_su,  0.012394,   1.0),
                ("S_aa",  s.S_aa,  0.0055432,  1.0),
                ("S_fa",  s.S_fa,  0.10741,    1.0),
                ("S_va",  s.S_va,  0.012333,   1.0),
                ("S_bu",  s.S_bu,  0.014003,   1.0),
                ("S_pro", s.S_pro, 0.017584,   1.0),
                ("S_ac",  s.S_ac,  0.089315,   1.0),
                ("S_h2",  s.S_h2,  2.5055e-07, 2.0),
                ("S_ch4", s.S_ch4, 0.05549,    1.0),
                ("S_IC",  s.S_IC,  0.095149,   1.0),
                ("S_IN",  s.S_IN,  0.094468,   1.0),
                ("S_I",   s.S_I,   0.13087,    1.0),
                ("X_c",   s.X_c,   0.10792,    1.0),
                ("X_ch",  s.X_ch,  0.020517,   1.0),
                ("X_pr",  s.X_pr,  0.08422,    1.0),
                ("X_li",  s.X_li,  0.043629,   1.0),
                ("X_su",  s.X_su,  0.31222,    1.0),
                ("X_aa",  s.X_aa,  0.93167,    1.0),
                ("X_fa",  s.X_fa,  0.33839,    1.0),
                ("X_c4",  s.X_c4,  0.33577,    1.0),
                ("X_pro", s.X_pro, 0.10112,    1.0),
                ("X_ac",  s.X_ac,  0.67724,    1.0),
                ("X_h2",  s.X_h2,  0.28484,    1.0),
                ("X_I",   s.X_I,   17.2162,    1.0),
                ("Sgas_ch4", s.S_ch4_gas, 1.6535,     1.5),
                ("Sgas_co2", s.S_co2_gas, 0.01354,    1.5),
                ("Sgas_h2",  s.S_h2_gas,  1.1032e-05, 2.0),
                ("pH",       s.pH,        7.2631,     0.5),
                ("S_nh3",    s.S_nh3,     0.001884,   2.0),
            };

            Assert.Multiple(() =>
            {
                foreach (var c in checks)
                    Assert.That(c.Got, Is.EqualTo(c.Ref).Within(c.TolPct).Percent,
                                $"{c.Name} deviates from the BSM2 benchmark");
            });
        }

        [Test]
        public void Adm1_bsm2_biogas_is_methane_rich()
        {
            var s = SolveBsm2SteadyState();
            // Partial pressures (bar): p_i = S_gas,i * R * T / (16 for h2, 64 for ch4, 1 for co2).
            double R = 0.083145, T = 308.15;
            double pCh4 = s.S_ch4_gas * R * T / 64.0;
            double pCo2 = s.S_co2_gas * R * T;
            double ch4Frac = pCh4 / (pCh4 + pCo2);
            // Benchmark: 0.66195 / (0.66195 + 0.34691) = 0.656.
            Assert.That(ch4Frac, Is.EqualTo(0.656).Within(2.0).Percent,
                        "biogas methane fraction departs from the BSM2 benchmark (~65.6 %).");
        }
    }
}
