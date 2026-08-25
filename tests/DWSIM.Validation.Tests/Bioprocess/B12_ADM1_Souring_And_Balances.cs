using System;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors.ADM1;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>B12 - ADM1-Full behaviours that the BSM2 benchmark cannot see.
    ///
    /// B11 pins the model to a published steady state, but a healthy digester sits at pH 7.47 where
    /// the pH inhibition is saturated at 1 whatever form it takes, and BSM2's feed carries no lipid.
    /// So B11 passes just as happily with an inhibition curve that cannot sour and a hydrolysis that
    /// loses COD. These are the cases that notice.
    ///
    ///   1. Souring under organic overload - the number one way a real digester fails.
    ///   2. COD conservation with lipid in the feed.
    ///   3. Carbon conservation.</summary>
    internal static class B12_ADM1_Souring_And_Balances
    {
        public static void Run()
        {
            var rt = new ResultTable("B12 - ADM1-Full: souring, COD and carbon balances");

            Souring(rt);
            CODConservation(rt);
            CarbonConservation(rt);

            rt.PrintAndThrowIfFailed();
        }

        /// <summary>A digester fed far beyond what its methanogens can keep up with must sour.</summary>
        /// <remarks>
        /// Acidogens are fast and tolerate acid down to pH 4; acetoclastic methanogens are slow and
        /// quit below pH 6. Overload the reactor and the first group outruns the second, VFAs pile
        /// up, pH falls, and the fall shuts the methanogens down further - the runaway that kills
        /// real reactors. Reproducing it needs an inhibition curve that actually bites: the
        /// two-sided form this model used to carry left acetoclasts at 78% activity at pH 6, where
        /// the truth is nearer 3%, and the reactor cheerfully kept making methane.
        /// </remarks>
        private static void Souring(ResultTable rt)
        {
            var healthy = new ADM1Parameters();     // BSM2 defaults
            var sour = new ADM1Parameters();

            // Same reactor, ten times the feed rate: HRT drops from 19 d to under 2 d, far below the
            // acetoclasts' doubling time, so they wash out faster than they grow.
            sour.Operating.Q_in = healthy.Operating.Q_in * 10.0;

            var h = ADM1Integrator.Integrate(healthy.InitialConditions.Clone(), healthy,
                                             healthy.Operating.Q_in,
                                             healthy.Operating.ToInfluentVector(), 0.0, 200.0);
            var s = ADM1Integrator.Integrate(sour.InitialConditions.Clone(), sour,
                                             sour.Operating.Q_in,
                                             sour.Operating.ToInfluentVector(), 0.0, 200.0);

            var hf = h.FinalState;
            var sf = s.FinalState;

            rt.RowInRange("[sour] Healthy reactor sits near neutral", 7.0, 7.9, hf.pH, "-");
            rt.RowInRange("[sour] Overloaded reactor turns acid", 4.0, 6.5, sf.pH, "-");

            // VFAs are what the acid is made of.
            double vfaHealthy = hf.S_va + hf.S_bu + hf.S_pro + hf.S_ac;
            double vfaSour = sf.S_va + sf.S_bu + sf.S_pro + sf.S_ac;
            rt.RowInRange("[sour] VFAs accumulate under overload", 5.0, 1e4,
                          vfaSour / Math.Max(vfaHealthy, 1e-12), "x");

            // The acetoclasts must be gone, and with them the methane.
            rt.RowInRange("[sour] Acetoclastic methanogens wash out", 0.0, 0.2,
                          sf.X_ac / Math.Max(hf.X_ac, 1e-12), "-");
            rt.RowInRange("[sour] Methane collapses", 0.0, 0.5,
                          ADM1Equations.CH4MoleFraction(sf, sour) /
                          Math.Max(ADM1Equations.CH4MoleFraction(hf, healthy), 1e-12), "-");

            // And the inhibition itself has to be doing the work, not just dilution.
            rt.RowInRange("[sour] pH inhibition of acetoclasts bites", 0.0, 0.35,
                          ADM1Equations.I_pH(sf.pH, sour.Inhibition.pH_UL_ac, sour.Inhibition.pH_LL_ac), "-");
        }

        /// <summary>
        /// COD is conserved: what comes in leaves as effluent COD, as methane, or as hydrogen.
        /// </summary>
        /// <remarks>
        /// Lipid in the feed is the point. Hydrolysis splits it into LCFA and the glycerol backbone;
        /// dropping the glycerol - which this model used to do - loses 1 - f_fa_li = 5% of every
        /// lipid's COD, and nothing else in the suite feeds enough lipid to notice.
        /// </remarks>
        private static void CODConservation(ResultTable rt)
        {
            var p = new ADM1Parameters();
            p.Operating.UseInfluentFromFeedStream = false;
            // Lipid-heavy influent: fat is where the leak was.
            p.Operating.Xin_c = 0.0; p.Operating.Xin_ch = 2.0;
            p.Operating.Xin_pr = 2.0; p.Operating.Xin_li = 20.0; p.Operating.Xin_I = 5.0;

            var Sin = p.Operating.ToInfluentVector();
            var r = ADM1Integrator.Integrate(p.InitialConditions.Clone(), p, p.Operating.Q_in,
                                             Sin, 0.0, 400.0);
            var f = r.FinalState;
            double D = p.Operating.Q_in / p.Operating.V_liq;

            // Influent COD (the state vector is COD-based except for S_IC, S_IN, S_cat, S_an, S_IS).
            double codIn = 0.0;
            for (int i = 0; i <= 8; i++) codIn += Sin[i];      // S_su..S_ch4
            codIn += Sin[11];                                   // S_I
            for (int i = 12; i <= 23; i++) codIn += Sin[i];     // X_c..X_I

            // COD out: effluent washout + what left as gas.
            double codEff = f.TotalCOD();
            double V = p.Operating.V_liq;
            double qGas = ADM1Equations.GasOutflow(f, p);
            // Gas-phase CH4 and H2 are already on a COD basis (64 and 16 g COD/mol).
            double codGas = (f.S_ch4_gas + f.S_h2_gas) * qGas / V;

            double balance = (codEff * D + codGas) / (codIn * D);
            rt.Row("[COD] In equals out with 20 g/L of lipid", 1.0, balance, 0.01, "-");
        }

        /// <summary>Carbon is conserved: in as feed, out as effluent, CO2 and CH4.</summary>
        /// <remarks>
        /// Impossible before the carbon balance existed: S_IC had no biological source at all, so
        /// the gas came out 96% CH4 / 2% CO2 and carbon was created from nothing by the
        /// hydrogenotrophs.
        /// </remarks>
        private static void CarbonConservation(ResultTable rt)
        {
            var p = new ADM1Parameters();
            var Sin = p.Operating.ToInfluentVector();
            var r = ADM1Integrator.Integrate(p.InitialConditions.Clone(), p, p.Operating.Q_in,
                                             Sin, 0.0, 400.0);
            var f = r.FinalState;
            var st = p.Stoichiometry;
            double V = p.Operating.V_liq;
            double D = p.Operating.Q_in / V;

            // kmol C per kg COD for every COD-based state, plus S_IC which is already kmol C.
            Func<double[], double> carbonOf = v =>
                v[0] * st.C_su + v[1] * st.C_aa + v[2] * st.C_fa + v[3] * st.C_va + v[4] * st.C_bu +
                v[5] * st.C_pro + v[6] * st.C_ac + v[8] * st.C_ch4 + v[9] +
                v[11] * st.C_sI + v[12] * st.C_xc + v[13] * st.C_ch + v[14] * st.C_pr +
                v[15] * st.C_li +
                (v[16] + v[17] + v[18] + v[19] + v[20] + v[21] + v[22]) * st.C_bac +
                v[23] * st.C_xI;

            double cIn = carbonOf(Sin);
            double cEff = carbonOf(f.ToVector());
            double qGas = ADM1Equations.GasOutflow(f, p);
            // S_co2_gas is kmol/m³; S_ch4_gas is kg COD/m³ and carries C_ch4 kmol C per kg COD.
            double cGas = (f.S_co2_gas + f.S_ch4_gas * st.C_ch4) * qGas / V;

            double balance = (cEff * D + cGas) / (cIn * D);
            rt.Row("[C] In equals out", 1.0, balance, 0.01, "-");
        }
    }
}
