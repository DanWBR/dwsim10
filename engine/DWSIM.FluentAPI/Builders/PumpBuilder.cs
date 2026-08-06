using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Pump unit operation. Call <see cref="Flowsheet.AddPump"/> to obtain one.</summary>
    public sealed class PumpBuilder : UnitOpBuilder<Pump, PumpBuilder>
    {
        internal PumpBuilder(Flowsheet f, Pump o) : base(f, o) { }

        /// <summary>Sets <c>Calc Mode</c> and returns this builder for chaining.</summary>
        public PumpBuilder WithCalcMode(Pump.CalculationMode mode) { Object.CalcMode = mode; return this; }
        /// <summary>Sets <c>Pressure Increase</c> (SI) and returns this builder for chaining.</summary>
        public PumpBuilder WithPressureIncrease(Quantity dp) { Object.DeltaP = dp.SI; Object.CalcMode = Pump.CalculationMode.Delta_P; return this; }
        /// <summary>Sets <c>Outlet Pressure</c> (SI) and returns this builder for chaining.</summary>
        public PumpBuilder WithOutletPressure(Quantity p) { Object.Pout = p.SI; Object.CalcMode = Pump.CalculationMode.OutletPressure; return this; }
        /// <summary>Sets <c>Power</c> (SI) and returns this builder for chaining.</summary>
        public PumpBuilder WithPower(Quantity power) { Object.DeltaQ = power.SI; Object.CalcMode = Pump.CalculationMode.Power; return this; }
        /// <summary>Sets <c>Efficiency Percent</c> and returns this builder for chaining.</summary>
        public PumpBuilder WithEfficiencyPercent(double pct) { Object.Eficiencia = pct; return this; }

        /// <summary>Read-back of <c>Delta PPa</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double DeltaPPa => Object.DeltaP.GetValueOrDefault();
        /// <summary>Read-back of <c>Power KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double PowerKW => Object.DeltaQ.GetValueOrDefault();
    }
}
