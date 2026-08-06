using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Cooler unit operation. Call <see cref="Flowsheet.AddCooler"/> to obtain one.</summary>
    public sealed class CoolerBuilder : UnitOpBuilder<Cooler, CoolerBuilder>
    {
        internal CoolerBuilder(Flowsheet f, Cooler o) : base(f, o) { }

        /// <summary>Sets <c>Calc Mode</c> and returns this builder for chaining.</summary>
        public CoolerBuilder WithCalcMode(Cooler.CalculationMode mode) { Object.CalcMode = mode; return this; }
        /// <summary>Sets <c>Outlet Temperature</c> (SI) and returns this builder for chaining.</summary>
        public CoolerBuilder WithOutletTemperature(Quantity t) { Object.OutletTemperature = t.SI; Object.CalcMode = Cooler.CalculationMode.OutletTemperature; return this; }
        /// <summary>Sets <c>Outlet Vapor Fraction</c> and returns this builder for chaining.</summary>
        public CoolerBuilder WithOutletVaporFraction(double frac) { Object.OutletVaporFraction = frac; Object.CalcMode = Cooler.CalculationMode.OutletVaporFraction; return this; }
        /// <summary>Sets <c>Heat Removed</c> (SI) and returns this builder for chaining.</summary>
        public CoolerBuilder WithHeatRemoved(Quantity power) { Object.DeltaQ = power.SI; Object.CalcMode = Cooler.CalculationMode.HeatRemoved; return this; }
        /// <summary>Sets <c>Temperature Change</c> (SI) and returns this builder for chaining.</summary>
        public CoolerBuilder WithTemperatureChange(Quantity dT) { Object.DeltaT = dT.SI; Object.CalcMode = Cooler.CalculationMode.TemperatureChange; return this; }
        /// <summary>Sets <c>Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public CoolerBuilder WithPressureDrop(Quantity dp) { Object.DeltaP = dp.SI; return this; }
        /// <summary>Sets <c>Efficiency Percent</c> and returns this builder for chaining.</summary>
        public CoolerBuilder WithEfficiencyPercent(double pct) { Object.Eficiencia = pct; return this; }

        /// <summary>Read-back of <c>Heat Removed KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double HeatRemovedKW => Object.DeltaQ.GetValueOrDefault();
    }
}
