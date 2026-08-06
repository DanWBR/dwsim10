using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Pretreatment unit operation. Call <see cref="Flowsheet.AddPretreatment"/> to obtain one.</summary>
    public sealed class PretreatmentBuilder : UnitOpBuilder<Reactor_Pretreatment, PretreatmentBuilder>
    {
        internal PretreatmentBuilder(Flowsheet f, Reactor_Pretreatment o) : base(f, o) { }

        /// <summary>Sets <c>Technology</c> and returns this builder for chaining.</summary>
        public PretreatmentBuilder WithTechnology(PretreatmentType t) { Object.Technology = t; return this; }
        /// <summary>Sets <c>Severity Log R0</c> and returns this builder for chaining.</summary>
        public PretreatmentBuilder WithSeverityLogR0(double logR0) { Object.SeverityLogR0 = logR0; return this; }
        /// <summary>Sets <c>Residence Time</c> (SI) and returns this builder for chaining.</summary>
        public PretreatmentBuilder WithResidenceTime(Quantity t) { Object.ResidenceTime_s = t.SI; return this; }
        /// <summary>Sets <c>Solids Loading</c> and returns this builder for chaining.</summary>
        public PretreatmentBuilder WithSolidsLoading(double wfrac) { Object.SolidsLoading_wfrac = wfrac; return this; }
        /// <summary>Sets <c>Cellulose Conversion</c> and returns this builder for chaining.</summary>
        public PretreatmentBuilder WithCelluloseConversion(double frac) { Object.CelluloseConversion = frac; return this; }
        /// <summary>Sets <c>Hemicellulose Conversion</c> and returns this builder for chaining.</summary>
        public PretreatmentBuilder WithHemicelluloseConversion(double frac) { Object.HemicelluloseConversion = frac; return this; }
        /// <summary>Sets <c>Lignin Solubilization</c> and returns this builder for chaining.</summary>
        public PretreatmentBuilder WithLigninSolubilization(double frac) { Object.LigninSolubilization = frac; return this; }
        /// <summary>Sets <c>Glucose To HMF</c> and returns this builder for chaining.</summary>
        public PretreatmentBuilder WithGlucoseToHMF(double frac) { Object.GlucoseToHMF = frac; return this; }
        /// <summary>Sets <c>Xylose To Furfural</c> and returns this builder for chaining.</summary>
        public PretreatmentBuilder WithXyloseToFurfural(double frac) { Object.XyloseToFurfural = frac; return this; }
    }
}
