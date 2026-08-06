using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Heater unit operation. Call <see cref="Flowsheet.AddHeater"/> to obtain one.</summary>
    public sealed class HeaterBuilder : UnitOpBuilder<Heater, HeaterBuilder>
    {
        internal HeaterBuilder(Flowsheet f, Heater o) : base(f, o) { }

        /// <summary>Sets <c>Calc Mode</c> and returns this builder for chaining.</summary>
        public HeaterBuilder WithCalcMode(Heater.CalculationMode mode) { Object.CalcMode = mode; return this; }
        /// <summary>Sets <c>Outlet Temperature</c> (SI) and returns this builder for chaining.</summary>
        public HeaterBuilder WithOutletTemperature(Quantity t) { Object.OutletTemperature = t.SI; Object.CalcMode = Heater.CalculationMode.OutletTemperature; return this; }
        /// <summary>Sets <c>Outlet Vapor Fraction</c> and returns this builder for chaining.</summary>
        public HeaterBuilder WithOutletVaporFraction(double frac) { Object.OutletVaporFraction = frac; Object.CalcMode = Heater.CalculationMode.OutletVaporFraction; return this; }
        /// <summary>Sets <c>Heat Added</c> (SI) and returns this builder for chaining.</summary>
        public HeaterBuilder WithHeatAdded(Quantity power) { Object.DeltaQ = power.SI; Object.CalcMode = Heater.CalculationMode.HeatAdded; return this; }
        /// <summary>Sets <c>Temperature Change</c> (SI) and returns this builder for chaining.</summary>
        public HeaterBuilder WithTemperatureChange(Quantity dT) { Object.DeltaT = dT.SI; Object.CalcMode = Heater.CalculationMode.TemperatureChange; return this; }
        /// <summary>Sets <c>Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public HeaterBuilder WithPressureDrop(Quantity dp) { Object.DeltaP = dp.SI; return this; }
        /// <summary>Sets <c>Efficiency Percent</c> and returns this builder for chaining.</summary>
        public HeaterBuilder WithEfficiencyPercent(double pct) { Object.Eficiencia = pct; return this; }

        /// <summary>Read-back of <c>Heat Duty KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double HeatDutyKW => Object.DeltaQ.GetValueOrDefault();
        /// <summary>Read-back of <c>Outlet Temperature K</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double OutletTemperatureK => Object.OutletTemperature.GetValueOrDefault();
    }
}
