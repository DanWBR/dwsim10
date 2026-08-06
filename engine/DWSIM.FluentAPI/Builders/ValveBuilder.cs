using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Valve unit operation. Call <see cref="Flowsheet.AddValve"/> to obtain one.</summary>
    public sealed class ValveBuilder : UnitOpBuilder<Valve, ValveBuilder>
    {
        internal ValveBuilder(Flowsheet f, Valve o) : base(f, o) { }

        /// <summary>Sets <c>Calc Mode</c> and returns this builder for chaining.</summary>
        public ValveBuilder WithCalcMode(Valve.CalculationMode mode) { Object.CalcMode = mode; return this; }
        /// <summary>Sets <c>Pressure Drop</c> (SI) and returns this builder for chaining.</summary>
        public ValveBuilder WithPressureDrop(Quantity dp) { Object.DeltaP = dp.SI; Object.CalcMode = Valve.CalculationMode.DeltaP; return this; }
        /// <summary>Sets <c>Outlet Pressure</c> (SI) and returns this builder for chaining.</summary>
        public ValveBuilder WithOutletPressure(Quantity p) { Object.OutletPressure = p.SI; Object.CalcMode = Valve.CalculationMode.OutletPressure; return this; }
        /// <summary>Sets <c>Kv</c> and returns this builder for chaining.</summary>
        public ValveBuilder WithKv(double kv) { Object.Kv = kv; return this; }
        /// <summary>Sets <c>Opening Percent</c> and returns this builder for chaining.</summary>
        public ValveBuilder WithOpeningPercent(double pct) { Object.OpeningPct = pct; return this; }
    }
}
