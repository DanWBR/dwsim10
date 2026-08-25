using System;
using System.IO;
using System.Reflection;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors.ADM1;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>B11 - ADM1-Full against the published BSM2 steady state.
    ///
    /// This is the case that says whether the model is right rather than merely self-consistent.
    /// Rosen &amp; Jeppsson 2006 publish the steady state of the BSM2 digester (3400 m³ liquid /
    /// 300 m³ gas, 178.4674 m³/d, 35 °C): pH 7.47, 2955 m³/d of biogas at 65 % CH4. Those numbers
    /// come out of an implementation the whole field has checked; matching them is the closest a
    /// unit test gets to matching reality.
    ///
    /// Runs ADM1Integrator directly. It is a Public Module, so no flowsheet, property package or
    /// compound database is involved and a failure here is the ADM1 equations and nothing else.
    ///
    /// Two cases, and the second is the one with teeth. Starting from the published steady state
    /// only shows the model does not drift away from it. Starting from a perturbed state and
    /// arriving there shows the steady state is genuinely the model's own.</summary>
    internal static class B11_ADM1_BSM2_Benchmark
    {
        // Rosen & Jeppsson 2006, BSM2 digester at steady state.
        private const double BSM2_pH = 7.47;
        private const double BSM2_xCH4 = 0.650;

        // Compared against GasOutflow, not BiogasFlow_Nm3_d. R&J report q_gas as the model computes
        // it - m³/d at the operating temperature - and it is widely quoted as "Nm³/d", including in
        // this repo's own sample comment. BiogasFlow_Nm3_d additionally normalises to 273.15 K,
        // which is a different and equally valid quantity, 11.4% smaller here. Both functions are
        // right; comparing the normalised one against the un-normalised reference is not.
        private const double BSM2_QGas_m3d_at_Top = 2955.0;

        public static void Run()
        {
            var rt = new ResultTable("B11 - ADM1-Full vs published BSM2 steady state");

            var p = LoadBenchmark();

            // ---- From the published steady state: must stay there ----
            var atSS = ADM1Integrator.Integrate(p.InitialConditions.Clone(), p, p.Operating.Q_in,
                                                p.Operating.ToInfluentVector(), 0.0, 200.0);

            rt.RowInRange("Integrator reached its horizon", 1, 1, atSS.Converged ? 1 : 0, "-");
            if (!string.IsNullOrEmpty(atSS.StopReason))
                Console.WriteLine("  note: StopReason = " + atSS.StopReason);

            var f = atSS.FinalState;
            rt.Row("pH holds at the BSM2 value", BSM2_pH, f.pH, 0.01, "-");
            rt.Row("Biogas flow holds", BSM2_QGas_m3d_at_Top, ADM1Equations.GasOutflow(f, p), 0.02, "m³/d");
            rt.Row("CH4 fraction holds", BSM2_xCH4, ADM1Equations.CH4MoleFraction(f, p), 0.03, "-");

            // A digester makes CO2 as well as methane; the balance of the dry gas is essentially it.
            // Before the carbon balance existed this came out at 0.02 and nothing noticed.
            rt.RowInRange("CO2 is the rest of the gas", 0.25, 0.45,
                          ADM1Equations.CO2MoleFraction(f, p), "-");

            // ---- From a perturbed state: must converge back ----
            // Halve every biomass population and drain the buffer. The reactor has to regrow its
            // community and re-establish alkalinity on its own; landing back on the published
            // numbers is what shows they are the model's steady state and not just its input.
            var ic = p.InitialConditions.Clone();
            ic.X_su *= 0.5; ic.X_aa *= 0.5; ic.X_fa *= 0.5; ic.X_c4 *= 0.5;
            ic.X_pro *= 0.5; ic.X_ac *= 0.5; ic.X_h2 *= 0.5;
            ic.S_IC *= 0.5;

            var fromPerturbed = ADM1Integrator.Integrate(ic, p, p.Operating.Q_in,
                                                         p.Operating.ToInfluentVector(), 0.0, 600.0);
            var g = fromPerturbed.FinalState;

            rt.RowInRange("Recovers from a halved community", 1, 1, fromPerturbed.Converged ? 1 : 0, "-");
            rt.Row("pH converges to BSM2", BSM2_pH, g.pH, 0.02, "-");
            rt.Row("Biogas flow converges to BSM2", BSM2_QGas_m3d_at_Top,
                   ADM1Equations.GasOutflow(g, p), 0.04, "m³/d");
            rt.Row("CH4 fraction converges to BSM2", BSM2_xCH4,
                   ADM1Equations.CH4MoleFraction(g, p), 0.05, "-");

            rt.PrintAndThrowIfFailed();
        }

        private static string SamplePath(string name)
        {
            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var path = Path.Combine(baseDir, "Reactors", "ADM1", "Samples", name);
            if (!File.Exists(path)) throw new FileNotFoundException("ADM1 sample not found: " + path);
            return path;
        }

        /// <summary>
        /// Loads the BSM2 sample, having first proved the loader actually reads files.
        /// </summary>
        /// <remarks>
        /// ADM1Parameters.FromJSON swallows every exception and hands back a default parameter set.
        /// The defaults ARE the BSM2 benchmark, so a missing, corrupt or silently-ignored BSM2 file
        /// would let this whole test pass while measuring nothing - and no value in that file
        /// differs from the defaults, so nothing in it can be used to tell the two apart.
        ///
        /// The thermophilic sample can: it runs at 328.15 K against a 308.15 K default. Loading it
        /// first and checking the temperature came through proves FromJSON is really parsing, which
        /// is what makes the BSM2 load below trustworthy.
        /// </remarks>
        private static ADM1Parameters LoadBenchmark()
        {
            var control = ADM1Parameters.FromJSON(File.ReadAllText(SamplePath("ADM1_Thermophilic_55C.json")));
            if (Math.Abs(control.Physicochemical.T_op_K - 328.15) > 1e-6)
                throw new Exception("ADM1Parameters.FromJSON is not reading the sample files: the " +
                                    "thermophilic control came back at T_op_K=" +
                                    control.Physicochemical.T_op_K + " instead of 328.15, which is " +
                                    "what a silent fallback to defaults looks like.");

            var p = ADM1Parameters.FromJSON(File.ReadAllText(SamplePath("ADM1_BSM2_Mesophilic.json")));
            if (Math.Abs(p.Operating.V_liq - 3400.0) > 1e-6 || Math.Abs(p.Operating.Q_in - 178.4674) > 1e-4)
                throw new Exception("Sample is not the BSM2 digester: V_liq=" + p.Operating.V_liq +
                                    " Q_in=" + p.Operating.Q_in);
            return p;
        }
    }
}
