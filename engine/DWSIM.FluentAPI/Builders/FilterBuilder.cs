using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for the Filter unit operation (rotary vacuum drum). Call <see cref="Flowsheet.AddFilter"/> to obtain one.
    /// The filter takes the solids from the feed's solid phase, so the solid compounds must flash
    /// as solids (see <see cref="PropertyPackageBuilder.WithForcedSolids"/>). In Simulation mode
    /// (<see cref="WithFilterArea"/>) it computes the pressure drop; in Design mode
    /// (<see cref="WithPressureDrop"/>) it computes the area.
    /// </summary>
    public sealed class FilterBuilder : UnitOpBuilder<Filter, FilterBuilder>
    {
        internal FilterBuilder(Flowsheet f, Filter o) : base(f, o) { }

        /// <summary>Sets <c>Calculation Mode</c> and returns this builder for chaining.</summary>
        public FilterBuilder WithCalculationMode(Filter.CalculationMode mode) { Object.CalcMode = mode; return this; }

        /// <summary>Sets the total filter area, in m2, and switches the filter to Simulation mode, which computes the pressure drop.</summary>
        public FilterBuilder WithFilterArea(double m2)
        {
            Object.TotalFilterArea = m2;
            Object.CalcMode = Filter.CalculationMode.Simulation;
            return this;
        }

        /// <summary>Sets the pressure drop across the filter and switches it to Design mode, which computes the area.</summary>
        public FilterBuilder WithPressureDrop(Quantity dp)
        {
            Object.PressureDrop = dp.SI;
            Object.CalcMode = Filter.CalculationMode.Design;
            return this;
        }

        /// <summary>Sets the fraction (0 to 1) of the drum area submerged in the slurry.</summary>
        public FilterBuilder WithSubmergedAreaFraction(double fraction) { Object.SubmergedAreaFraction = fraction; return this; }

        /// <summary>Sets the time of one drum revolution.</summary>
        public FilterBuilder WithCycleTime(Quantity time) { Object.FilterCycleTime = time.SI; return this; }

        /// <summary>Sets the specific cake resistance, in m/kg.</summary>
        public FilterBuilder WithSpecificCakeResistance(double mPerKg) { Object.SpecificCakeResistance = mPerKg; return this; }

        /// <summary>Sets the filter medium resistance, in 1/m.</summary>
        public FilterBuilder WithMediumResistance(double perMeter) { Object.FilterMediumResistance = perMeter; return this; }

        /// <summary>Sets the liquid content of the discharged cake, in percent of the cake mass.</summary>
        public FilterBuilder WithCakeMoisturePercent(double percent) { Object.CakeRelativeHumidity = percent; return this; }

        /// <summary>Read-back of the pressure drop, in Pa (computed in Simulation mode, populated after <c>Solve</c>).</summary>
        public double PressureDropPa => Object.PressureDrop;
        /// <summary>Read-back of the total filter area, in m2 (computed in Design mode, populated after <c>Solve</c>).</summary>
        public double FilterAreaM2 => Object.TotalFilterArea;
    }
}
