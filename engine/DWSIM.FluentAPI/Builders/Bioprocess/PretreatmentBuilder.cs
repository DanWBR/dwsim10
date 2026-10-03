using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Pretreatment unit operation. Call <see cref="Flowsheet.AddPretreatment"/> to obtain one.</summary>
    public sealed class PretreatmentBuilder : UnitOpBuilder<Reactor_Pretreatment, PretreatmentBuilder>
    {
        internal PretreatmentBuilder(Flowsheet f, Reactor_Pretreatment o) : base(f, o) { }

        /// <summary>
        /// Sets <c>Technology</c> and loads its default conversions, as the editor does when the
        /// technology is picked. Set any conversion afterwards to override its default.
        /// </summary>
        public PretreatmentBuilder WithTechnology(PretreatmentType t)
        {
            Object.Technology = t;
            Object.ApplyTechnologyDefaults();
            return this;
        }
        /// <summary>
        /// Sets <c>Conversion Mode</c>: <see cref="PretreatmentConversionMode.UserFractions"/> (default) applies the
        /// conversion fractions as set; <see cref="PretreatmentConversionMode.Severity"/> computes them from the severity.
        /// </summary>
        public PretreatmentBuilder WithConversionMode(PretreatmentConversionMode mode) { Object.ConversionMode = mode; return this; }
        /// <summary>
        /// Switches to the severity model: the cellulose and hemicellulose conversions and the sugar degradation follow
        /// from log R0 (or from the combined severity log R0 - pH for dilute acid), with rate constants calibrated so that
        /// log R0 = 3.5 reproduces the technology defaults. Set the technology first.
        /// </summary>
        public PretreatmentBuilder WithSeverityModel() { Object.ConversionMode = PretreatmentConversionMode.Severity; return this; }
        /// <summary>
        /// Sets <c>Severity Log R0</c>. The reactor reads it only when the residence time is zero
        /// (<c>WithResidenceTime(Q.Seconds(0))</c>); with a positive residence time it computes log R0 itself.
        /// </summary>
        public PretreatmentBuilder WithSeverityLogR0(double logR0) { Object.SeverityLogR0 = logR0; return this; }
        /// <summary>
        /// Sets <c>Residence Time</c> (SI). A positive value makes the reactor compute log R0 from it and the temperature;
        /// zero hands the severity to <see cref="WithSeverityLogR0"/>.
        /// </summary>
        public PretreatmentBuilder WithResidenceTime(Quantity t) { Object.ResidenceTime_s = t.SI; return this; }
        /// <summary>
        /// Sets the expected <c>Solids Loading</c> (w/w). The reactor computes the loading from the feed and, in Severity
        /// mode, warns when the two differ; it never adds water.
        /// </summary>
        public PretreatmentBuilder WithSolidsLoading(double wfrac) { Object.SolidsLoading_wfrac = wfrac; return this; }
        /// <summary>
        /// Sets <c>Acid Compound</c>, the mineral acid in the feed whose concentration in the liquid gives the pH of the
        /// combined severity, and its second dissociation constant (1.99 for sulfuric acid; use 99 for HCl).
        /// </summary>
        public PretreatmentBuilder WithAcidCompound(string compound, double secondPKa = 1.99)
        {
            Object.AcidCompound = compound;
            Object.AcidSecondPKa = secondPKa;
            return this;
        }
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
