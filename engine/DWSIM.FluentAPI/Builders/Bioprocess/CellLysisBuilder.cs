using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.Bioprocess
{
    /// <summary>Fluent builder for the Cell Lysis unit operation. Call <see cref="Flowsheet.AddCellLysis"/> to obtain one.</summary>
    public sealed class CellLysisBuilder : UnitOpBuilder<UnitOp_CellLysis, CellLysisBuilder>
    {
        internal CellLysisBuilder(Flowsheet f, UnitOp_CellLysis o) : base(f, o) { }

        /// <summary>Sets <c>Technology</c> and returns this builder for chaining.</summary>
        public CellLysisBuilder WithTechnology(LysisTechnology t) { Object.Technology = t; return this; }
        /// <summary>Sets <c>Passes</c> and returns this builder for chaining.</summary>
        public CellLysisBuilder WithPasses(int n) { Object.Passes = n; return this; }
        /// <summary>Sets <c>Pressure MPa</c> and returns this builder for chaining.</summary>
        public CellLysisBuilder WithPressureMPa(double p) { Object.Pressure_MPa = p; return this; }
        /// <summary>Sets <c>Biomass Compound</c> and returns this builder for chaining.</summary>
        public CellLysisBuilder WithBiomassCompound(string name) { Object.BiomassCompound = name; return this; }
        /// <summary>Sets <c>Default Release Fraction</c> and returns this builder for chaining.</summary>
        public CellLysisBuilder WithDefaultReleaseFraction(double frac) { Object.DefaultReleaseFraction = frac; return this; }
        /// <summary>Sets <c>Release Fraction</c> and returns this builder for chaining.</summary>
        public CellLysisBuilder WithReleaseFraction(string compound, double frac)
        {
            if (Object.ReleaseFraction == null)
                Object.ReleaseFraction = new System.Collections.Generic.Dictionary<string, double>();
            Object.ReleaseFraction[compound] = frac;
            return this;
        }
        /// <summary>Sets <c>Ultrasound Power Density WPer ML</c> and returns this builder for chaining.</summary>
        public CellLysisBuilder WithUltrasoundPowerDensityWPerML(double wml) { Object.Ultrasound_PowerDensity_WmL = wml; return this; }
        /// <summary>Sets <c>Ultrasound Time</c> (SI) and returns this builder for chaining.</summary>
        public CellLysisBuilder WithUltrasoundTime(Quantity t) { Object.Ultrasound_Time_s = t.SI; return this; }
    }
}
