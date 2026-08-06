using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.Reactors.ADM1;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Anaerobic Digester unit operation. Call <see cref="Flowsheet.AddAnaerobicDigester"/> to obtain one.</summary>
    public sealed class AnaerobicDigesterBuilder : UnitOpBuilder<Reactor_AnaerobicDigester, AnaerobicDigesterBuilder>
    {
        internal AnaerobicDigesterBuilder(Flowsheet f, Reactor_AnaerobicDigester o) : base(f, o) { }

        /// <summary>Sets <c>Volume</c> (SI) and returns this builder for chaining.</summary>
        public AnaerobicDigesterBuilder WithVolume(Quantity v) { Object.Volume = v.SI; return this; }
        /// <summary>Sets <c>Hydraulic Retention Time</c> (SI) and returns this builder for chaining.</summary>
        public AnaerobicDigesterBuilder WithHydraulicRetentionTime(Quantity t) { Object.HRT_s = t.SI; return this; }
        /// <summary>Sets <c>CODRemoval</c> and returns this builder for chaining.</summary>
        public AnaerobicDigesterBuilder WithCODRemoval(double fraction) { Object.CODRemovalEfficiency = fraction; return this; }
        /// <summary>Sets <c>Biomass Yield GVss Per GCOD</c> and returns this builder for chaining.</summary>
        public AnaerobicDigesterBuilder WithBiomassYieldGVssPerGCOD(double y) { Object.BiomassYield_gVSSpergCOD = y; return this; }
        /// <summary>Sets <c>Methane Fraction Override</c> and returns this builder for chaining.</summary>
        public AnaerobicDigesterBuilder WithMethaneFractionOverride(double frac) { Object.MethaneFractionOverride = frac; return this; }
        /// <summary>Sets <c>Thermal Mode</c> and returns this builder for chaining.</summary>
        public AnaerobicDigesterBuilder WithThermalMode(BioReactorThermalMode m) { Object.ThermalMode = m; return this; }
        /// <summary>Sets <c>Model</c> and returns this builder for chaining.</summary>
        public AnaerobicDigesterBuilder WithModel(DigesterModel m) { Object.Model = m; return this; }

        // ---- Sulfur balance (all three models) ----

        /// <summary>Sets the sulfate sulfur in the feed liquid, as S rather than as SO4 (mg S/L).
        /// Sulfate carries no COD of its own, so reducing it to sulfide draws 64 kg COD/kmol S out
        /// of the pool that would otherwise have made methane: expect a real drop in CH4.</summary>
        public AnaerobicDigesterBuilder WithInfluentSulfateSulfurMgPerL(double mgL) { Object.InfluentSulfateS_mgL = mgL; return this; }

        /// <summary>Sets the organic sulfur bound in the substrate (g S/kg substrate). Pass -1 to
        /// read it from the substrate compound's elemental formula, which keeps it consistent with
        /// the theoretical COD; pass >= 0 only to declare sulfur the formula omits. Unlike sulfate,
        /// this sulfur arrives already reduced and makes H2S at no cost in methane.</summary>
        public AnaerobicDigesterBuilder WithSubstrateOrganicSulfurGPerKg(double gPerKg) { Object.SubstrateOrganicS_gPerKg = gPerKg; return this; }

        /// <summary>Sets the pH assumed when splitting sulfide into volatile H2S and non-volatile
        /// HS-. Only free H2S leaves in the biogas and pKa1 is near 7, so the split is at its most
        /// pH-sensitive here. Used by BlackBox and ADM1-Lite; ADM1-Full uses its own pH.</summary>
        public AnaerobicDigesterBuilder WithAssumedPHForSulfide(double pH) { Object.AssumedPH_ForSulfide = pH; return this; }

        // ADM1 simplified parameters
        /// <summary>Sets the ADM1 first-order hydrolysis rate constant (per day).</summary>
        public AnaerobicDigesterBuilder WithADM1HydrolysisRatePerDay(double k) { Object.ADM1_k_hyd_d = k; return this; }
        /// <summary>Sets <c>ADM1Sugar Uptake Per Day</c> and returns this builder for chaining.</summary>
        public AnaerobicDigesterBuilder WithADM1SugarUptakePerDay(double k) { Object.ADM1_km_su_d = k; return this; }
        /// <summary>Sets <c>ADM1Acetate Uptake Per Day</c> and returns this builder for chaining.</summary>
        public AnaerobicDigesterBuilder WithADM1AcetateUptakePerDay(double k) { Object.ADM1_km_ac_d = k; return this; }

        // ---- Property profile access (populated after Calculate with ADM1Full model) ----

        /// <summary>Full ADM1 trajectory from the last Calculate call (state variables vs time). Null if not yet calculated or model is not ADM1Full.</summary>
        public ADM1TrajectoryResult ADM1Trajectory => Object.ADM1LastTrajectory;

        /// <summary>Final ADM1 state after the last calculation. Null if model is not ADM1Full.</summary>
        public ADM1State ADM1FinalState => Object.ADM1LastState;

        /// <summary>Names of all available ADM1 profile series.</summary>
        public string[] ProfileSeriesNames => Object.ADM1LastTrajectory?.AvailableSeries();

        /// <summary>Returns a named ADM1 profile series as an array of doubles.</summary>
        public double[] GetProfileSeries(string name) => Object.ADM1LastTrajectory?.GetSeries(name);

        /// <summary>Exports the full ADM1 trajectory to CSV text.</summary>
        public string ProfileToCSV() => Object.ADM1LastTrajectory?.ToCSV();

        /// <summary>Exports the full ADM1 trajectory to a DataTable for charting or tabular display.</summary>
        public System.Data.DataTable ProfileToDataTable() => Object.ADM1LastTrajectory?.ToDataTable();
    }
}
