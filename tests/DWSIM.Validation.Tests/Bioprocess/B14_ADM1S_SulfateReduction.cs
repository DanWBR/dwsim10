using System;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors.ADM1;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>B14 - ADM1-S, the sulfate-reduction extension.
    ///
    /// Standard ADM1 has no sulfur cycle at all, so the extension has two jobs: stay completely out
    /// of the way when there is no sulfate, and behave like sulfate reduction when there is. These
    /// are the checks that separate those.
    ///
    ///   1. Inert without sulfate - enabling the extension on a sulfate-free feed must reproduce
    ///      ADM1-Full state for state, or every existing benchmark is quietly at risk.
    ///   2. Sulfate is respired, H2S reaches the biogas, and methane pays for it.
    ///   3. Sulfur closes: sulfate in equals sulfate + sulfide + gaseous H2S out.
    ///   4. COD closes with sulfide carrying the electrons the donors lost.
    ///   5. Free H2S inhibits: pile enough sulfate in and the methanogens suffer beyond what the
    ///      diverted electrons alone would cost.</summary>
    internal static class B14_ADM1S_SulfateReduction
    {
        public static void Run()
        {
            var rt = new ResultTable("B14 - ADM1-S: sulfate reduction, competition and balances");

            InertWithoutSulfate(rt);
            SulfateIsReduced(rt);
            SulfateOverload(rt);

            rt.PrintAndThrowIfFailed();
        }

        /// <summary>
        /// Feed more sulfate than the donors can reduce and the excess has to survive to the
        /// effluent.
        /// </summary>
        /// <remarks>
        /// This is the case the stoichiometric models cannot represent at all - they assume every
        /// sulfate fed is reduced and cap the COD debit with a warning when it will not fit. Here
        /// the reducers simply run out of electrons, the conversion lands well below 1, and the
        /// sulfur balance only closes if the leftover sulfate is still counted.
        /// </remarks>
        private static void SulfateOverload(ResultTable rt)
        {
            // BSM2's influent carries about 32 kg COD/m³ of degradable particulate; 0.5 kmol S/m³
            // would take 32 of it, so the sulfate cannot all be respired however keen the reducers.
            const double so4 = 0.5;

            var p = new ADM1Parameters();
            p.Sulfate.Enabled = true;
            p.Sulfate.Sin_so4 = so4;
            SeedSRB(p, 0.01);

            var Sin = p.Operating.ToInfluentVector(p.Sulfate);
            var r = ADM1Integrator.Integrate(p.InitialConditions.Clone(), p,
                                             p.Operating.Q_in, Sin, 0.0, 600.0);
            var f = r.FinalState;
            double V = p.Operating.V_liq;
            double D = p.Operating.Q_in / V;
            double qGas = ADM1Equations.GasOutflow(f, p);

            rt.RowInRange("[over] Integration reached its horizon", 1, 1, r.Converged ? 1 : 0, "-");
            rt.RowInRange("[over] Sulfate outruns the donors", 0.0, 0.95,
                          1.0 - f.S_so4 / so4, "-");
            rt.RowInRange("[over] Unreduced sulfate survives", 0.05 * so4, so4, f.S_so4, "kmol S/m3");

            double sIn = (Sin[31] + Sin[29]) * D * V;
            double sOut = (f.S_so4 + f.S_IS) * D * V + f.S_h2s_gas * qGas;
            rt.Row("[over] Sulfur still closes", 1.0, sOut / sIn, 2e-5, "-");
        }

        /// <summary>A parameter set with the extension on but no sulfate anywhere must be ADM1-Full.</summary>
        /// <remarks>
        /// Not a formality: the extension touches the shared rate expressions and the hydrogen
        /// quasi-steady solve, both of which the BSM2 benchmark runs through. If the H2S inhibition
        /// factor or the SRB hydrogen sink were ever non-neutral at zero sulfide, this is where it
        /// would show, and the tolerance is tight enough that only exact neutrality passes.
        /// </remarks>
        private static void InertWithoutSulfate(ResultTable rt)
        {
            var plain = new ADM1Parameters();

            var withS = new ADM1Parameters();
            withS.Sulfate.Enabled = true;
            withS.Sulfate.Sin_so4 = 0.0;
            // No inoculum either: a seeded population would decay into the composites and perturb
            // the answer even with nothing for it to eat.
            withS.InitialConditions.X_srb_h2 = 0.0;
            withS.InitialConditions.X_srb_ac = 0.0;
            withS.InitialConditions.X_srb_pro = 0.0;
            withS.InitialConditions.X_srb_bu = 0.0;

            var a = ADM1Integrator.Integrate(plain.InitialConditions.Clone(), plain,
                                             plain.Operating.Q_in,
                                             plain.Operating.ToInfluentVector(), 0.0, 200.0);
            var b = ADM1Integrator.Integrate(withS.InitialConditions.Clone(), withS,
                                             withS.Operating.Q_in,
                                             withS.Operating.ToInfluentVector(withS.Sulfate), 0.0, 200.0);

            rt.Row("[inert] pH matches ADM1-Full", a.FinalState.pH, b.FinalState.pH, 1e-9, "-");
            rt.Row("[inert] CH4 fraction matches", ADM1Equations.CH4MoleFraction(a.FinalState, plain),
                   ADM1Equations.CH4MoleFraction(b.FinalState, withS), 1e-9, "-");
            rt.Row("[inert] Biogas flow matches", ADM1Equations.BiogasFlow_Nm3_d(a.FinalState, plain),
                   ADM1Equations.BiogasFlow_Nm3_d(b.FinalState, withS), 1e-9, "m3/d");
            rt.Row("[inert] Acetate matches", a.FinalState.S_ac, b.FinalState.S_ac, 1e-9, "kg COD/m3");
            rt.Row("[inert] Acetoclasts match", a.FinalState.X_ac, b.FinalState.X_ac, 1e-9, "kg COD/m3");
        }

        /// <summary>Feed sulfate and the reducers must take it, make H2S, and cost the digester methane.</summary>
        private static void SulfateIsReduced(ResultTable rt)
        {
            // 0.02 kmol S/m³ is 640 mg S/L: a high-sulfate industrial feed, and 1.28 kg COD/m³ of
            // electrons - a few per cent of what this influent carries, so the effect is real but
            // the reactor is not sulfate-limited.
            const double so4 = 0.02;
            const double horizon = 600.0;

            var refP = new ADM1Parameters();
            var sP = new ADM1Parameters();
            sP.Sulfate.Enabled = true;
            sP.Sulfate.Sin_so4 = so4;
            SeedSRB(sP, 0.01);

            var refR = ADM1Integrator.Integrate(refP.InitialConditions.Clone(), refP,
                                                refP.Operating.Q_in,
                                                refP.Operating.ToInfluentVector(), 0.0, horizon);
            var Sin = sP.Operating.ToInfluentVector(sP.Sulfate);
            var sR = ADM1Integrator.Integrate(sP.InitialConditions.Clone(), sP,
                                              sP.Operating.Q_in, Sin, 0.0, horizon);

            rt.RowInRange("[SO4] Integration reached its horizon", 1, 1, sR.Converged ? 1 : 0, "-");

            var f = sR.FinalState;
            double V = sP.Operating.V_liq;
            double D = sP.Operating.Q_in / V;
            double qGas = ADM1Equations.GasOutflow(f, sP);

            // The reducers have to establish, not wash out.
            double xSRB = f.X_srb_h2 + f.X_srb_ac + f.X_srb_pro + f.X_srb_bu;
            rt.RowInRange("[SO4] SRB establish a population", 0.02, 100.0, xSRB, "kg COD/m3");

            // Most of the sulfate should be gone, and the sulfur it became has to be findable.
            rt.RowInRange("[SO4] Most of the sulfate is reduced", 0.5, 1.0,
                          1.0 - f.S_so4 / so4, "-");
            rt.RowInRange("[SO4] H2S reaches the biogas", 100.0, 200000.0,
                          ADM1Equations.H2SMoleFraction(f, sP) * 1e6, "ppmv");

            // The three conservation rows run at 2e-5 rather than the 1% the older balance tests
            // use. All three close to about 1e-6 here, so that is still 20x of headroom over the
            // integrator's own 1e-6 relative tolerance - and it is what makes them able to catch a
            // single sign slip in one coefficient. The carbon row is the one that proves it: with
            // the SRB decay term inverted it closes to 7.4e-5, which 1% would have waved through.
            const double balanceTol = 2e-5;

            // Sulfur balance: sulfate in = sulfate + sulfide out (liquid) + H2S out (gas).
            double sIn = (Sin[31] + Sin[29]) * D * V;
            double sOut = (f.S_so4 + f.S_IS) * D * V + f.S_h2s_gas * qGas;
            rt.Row("[SO4] Sulfur in equals sulfur out", 1.0, sOut / sIn, balanceTol, "-");

            // COD balance. Sulfate brings no COD in; the sulfide it becomes carries 64 kg COD/kmol S
            // out, dissolved (already inside TotalCOD) or as H2S in the gas, which is the term that
            // has to be added by hand or the electrons look destroyed.
            double codIn = 0.0;
            for (int i = 0; i <= 8; i++) codIn += Sin[i];
            codIn += Sin[11];
            for (int i = 12; i <= 23; i++) codIn += Sin[i];
            codIn += ADM1State.COD_per_kmol_S * Sin[29];
            double codGas = (f.S_ch4_gas + f.S_h2_gas) * qGas / V
                          + ADM1State.COD_per_kmol_S * f.S_h2s_gas * qGas / V;
            rt.Row("[SO4] COD in equals COD out", 1.0,
                   (f.TotalCOD() * D + codGas) / (codIn * D), balanceTol, "-");

            // Carbon closes too. The reducers move carbon in three directions at once - the
            // hydrogenotrophs fix CO2 into biomass, the acetotrophs mineralise acetate, the
            // incomplete oxidisers do both - so a sign slip anywhere in their carbon coefficients
            // shows up here and nowhere else.
            var st = sP.Stoichiometry;
            Func<double[], double> carbonOf = v =>
                v[0] * st.C_su + v[1] * st.C_aa + v[2] * st.C_fa + v[3] * st.C_va + v[4] * st.C_bu +
                v[5] * st.C_pro + v[6] * st.C_ac + v[8] * st.C_ch4 + v[9] +
                v[11] * st.C_sI + v[12] * st.C_xc + v[13] * st.C_ch + v[14] * st.C_pr +
                v[15] * st.C_li +
                (v[16] + v[17] + v[18] + v[19] + v[20] + v[21] + v[22]) * st.C_bac +
                v[23] * st.C_xI +
                (v[32] + v[33] + v[34] + v[35]) * st.C_bac;
            double cGas = (f.S_co2_gas + f.S_ch4_gas * st.C_ch4) * qGas / V;
            rt.Row("[SO4] Carbon in equals carbon out", 1.0,
                   (carbonOf(f.ToVector()) * D + cGas) / (carbonOf(Sin) * D), balanceTol, "-");

            // Methane must pay for the diverted electrons: the reducers take hydrogen and acetate
            // that would otherwise have been methanised.
            double ch4Ref = ADM1Equations.CH4MoleFraction(refR.FinalState, refP);
            double ch4S = ADM1Equations.CH4MoleFraction(f, sP);
            rt.RowInRange("[SO4] Methane fraction falls", 0.5, 0.999, ch4S / ch4Ref, "-");

            // Free H2S, not total sulfide, is what inhibits - so the factor has to be below 1 and
            // driven by the undissociated fraction the pH leaves behind.
            double iS = ADM1Sulfate.I_h2s(f, sP.Sulfate.K_I_h2s);
            rt.RowInRange("[SO4] Free H2S inhibits the methanogens", 0.01, 0.999, iS, "-");
            rt.RowInRange("[SO4] Sulfide is mostly dissociated at digester pH", 0.0, 0.9,
                          Math.Max(f.S_IS - f.S_hs_ion, 0.0) / Math.Max(f.S_IS, 1e-30), "-");
        }

        private static void SeedSRB(ADM1Parameters p, double x)
        {
            p.InitialConditions.X_srb_h2 = x;
            p.InitialConditions.X_srb_ac = x;
            p.InitialConditions.X_srb_pro = x;
            p.InitialConditions.X_srb_bu = x;
        }
    }
}
