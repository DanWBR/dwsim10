using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Heat Exchanger unit operation. Call <see cref="Flowsheet.AddHeatExchanger"/> to obtain one.</summary>
    public sealed class HeatExchangerBuilder : UnitOpBuilder<HeatExchanger, HeatExchangerBuilder>
    {
        internal HeatExchangerBuilder(Flowsheet f, HeatExchanger o) : base(f, o) { }

        /// <summary>Sets <c>Calculation Mode</c> and returns this builder for chaining.</summary>
        public HeatExchangerBuilder WithCalculationMode(HeatExchangerCalcMode mode)
        { Object.CalculationMode = mode; return this; }

        /// <summary>Sets <c>Hot Side Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public HeatExchangerBuilder WithHotSidePressureDrop(Quantity dp) { Object.HotSidePressureDrop = dp.SI; return this; }
        /// <summary>Sets <c>Cold Side Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public HeatExchangerBuilder WithColdSidePressureDrop(Quantity dp) { Object.ColdSidePressureDrop = dp.SI; return this; }
        /// <summary>Sets <c>Global UA</c> and returns this builder for chaining.</summary>
        public HeatExchangerBuilder WithGlobalUA(double ua) { Object.OverallCoefficient = ua; return this; }
        /// <summary>Sets <c>Exchange Area</c> and returns this builder for chaining.</summary>
        public HeatExchangerBuilder WithExchangeArea(double m2) { Object.Area = m2; return this; }
    }
}
