using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders.CleanEnergy
{
    /// <summary>Fluent builder for the Solar Panel unit operation. Call <see cref="Flowsheet.AddSolarPanel"/> to obtain one.</summary>
    public sealed class SolarPanelBuilder : UnitOpBuilder<SolarPanel, SolarPanelBuilder>
    {
        internal SolarPanelBuilder(Flowsheet f, SolarPanel o) : base(f, o) { }

        /// <summary>Sets <c>Panel Area M2</c> and returns this builder for chaining.</summary>
        public SolarPanelBuilder WithPanelAreaM2(double m2) { Object.PanelArea = m2; return this; }
        /// <summary>Sets <c>Panel Efficiency Percent</c> and returns this builder for chaining.</summary>
        public SolarPanelBuilder WithPanelEfficiencyPercent(double pct) { Object.PanelEfficiency = pct; return this; }
        /// <summary>Sets <c>Panel Count</c> and returns this builder for chaining.</summary>
        public SolarPanelBuilder WithPanelCount(int n) { Object.NumberOfPanels = n; return this; }
        /// <summary>Sets <c>Solar Irradiation KWPer M2</c> and returns this builder for chaining.</summary>
        public SolarPanelBuilder WithSolarIrradiationKWPerM2(double kw) { Object.SolarIrradiation_kW_m2 = kw; return this; }

        /// <summary>Read-back of <c>Generated Power KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double GeneratedPowerKW => Object.GeneratedPower;
        /// <summary>Read-back of <c>Actual Solar Irradiation KWPer M2</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double ActualSolarIrradiationKWPerM2 => Object.ActualSolarIrradiation_kW_m2;
    }
}
