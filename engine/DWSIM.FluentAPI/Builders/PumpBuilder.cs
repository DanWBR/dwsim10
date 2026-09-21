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

        /// <summary>
        /// Sets the speed (rpm) the pump runs at in Curves mode, which is what a variable-frequency
        /// drive changes. Zero runs it at the speed its reference curves were measured at.
        /// </summary>
        public PumpBuilder WithOperatingSpeed(double rpm) { Object.OperatingSpeed = rpm; return this; }

        /// <summary>
        /// Configures the reference curve set, the one the pump has always had, and records the speed
        /// it was measured at.
        /// </summary>
        public PumpBuilder WithCurves(double measuredAtRpm, System.Action<DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps.CurveSet> configure)
        {
            Object.PumpCurveSet.ImpellerSpeed = measuredAtRpm;
            configure?.Invoke(Object.PumpCurveSet);
            Object.CalcMode = Pump.CalculationMode.Curves;
            return this;
        }

        /// <summary>
        /// Adds a curve set measured at another speed, as a manufacturer publishes for a pump on a
        /// variable-frequency drive. Between two measured speeds the pump reads both sets and blends
        /// them; outside their range it scales the nearest one with the affinity laws.
        /// </summary>
        public PumpBuilder WithCurvesAtSpeed(int rpm, System.Action<DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps.CurveSet> configure)
        {
            var set = new DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps.CurveSet { ImpellerSpeed = rpm };
            configure?.Invoke(set);
            Object.CurveSets[rpm] = set;
            Object.CalcMode = Pump.CalculationMode.Curves;
            return this;
        }

        /// <summary>Read-back of <c>Delta PPa</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double DeltaPPa => Object.DeltaP.GetValueOrDefault();
        /// <summary>Read-back of <c>Power KW</c> from the underlying object (populated after <c>Solve</c>).</summary>
        public double PowerKW => Object.DeltaQ.GetValueOrDefault();
    }
}
