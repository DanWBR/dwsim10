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

        /// <summary>
        /// Makes the effective flow coefficient follow the stem opening along a characteristic curve.
        /// Without this the valve passes its full Kv at any opening, so a shut valve still flows —
        /// which matters as soon as a controller starts moving the opening.
        /// </summary>
        /// <param name="characteristic">The curve relating opening to flow coefficient.</param>
        /// <param name="rangeability">Rangeability used by the equal-percentage curve.</param>
        public ValveBuilder WithOpeningKvRelationship(
            Valve.OpeningKvRelationshipType characteristic = Valve.OpeningKvRelationshipType.Linear,
            double rangeability = 50.0)
        {
            Object.EnableOpeningKvRelationship = true;
            Object.DefinedOpeningKvRelationShipType = characteristic;
            Object.CharacteristicParameter = rangeability;
            return this;
        }

        // ------------------------------------------------------ Dynamic mode

        /// <summary>
        /// Sets the opening the actuator drives towards. This is what a level or flow controller
        /// manipulates; the actual opening follows it through the delay and time constant below.
        /// </summary>
        public ValveBuilder WithOpeningSetpoint(double percent) => WithDynamicProperty("Opening Setpoint", percent);

        /// <summary>Sets the actuator dead time. Zero means the opening tracks the setpoint immediately.</summary>
        public ValveBuilder WithActuatorDelay(Quantity delay) => WithDynamicProperty("Actuator Delay", delay);

        /// <summary>Sets the first-order lag of the actuator response.</summary>
        public ValveBuilder WithActuatorTimeConstant(Quantity tau) =>
            WithDynamicProperty("Actuator Time Constant", tau);

        /// <summary>Sets the liquid pressure recovery factor FL, which fixes the cavitation threshold.</summary>
        public ValveBuilder WithPressureRecoveryFactor(double fl) =>
            WithDynamicProperty("Liquid Pressure Recovery Factor FL", fl);

        /// <summary>True when the last dynamic step detected cavitation across the valve.</summary>
        public bool CavitationAlarm
        {
            get
            {
                var value = GetDynamicProperty("Cavitation Alarm");
                return value != null && System.Convert.ToBoolean(value);
            }
        }
    }
}
