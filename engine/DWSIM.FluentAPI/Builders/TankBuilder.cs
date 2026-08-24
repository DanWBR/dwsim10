using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for the Tank unit operation. Call <see cref="Flowsheet.AddTank"/> to obtain one.</summary>
    public sealed class TankBuilder : UnitOpBuilder<Tank, TankBuilder>
    {
        internal TankBuilder(Flowsheet f, Tank o) : base(f, o) { }

        /// <summary>Sets the tank's internal volume.</summary>
        public TankBuilder WithVolume(Quantity volume) { Object.Volume = volume.SI; return this; }

        // ------------------------------------------------------ Dynamic mode

        /// <summary>Sets the liquid height available for accumulation, which fixes the level-to-volume ratio.</summary>
        public TankBuilder WithHeight(Quantity height) => WithDynamicProperty("Height", height);

        /// <summary>Sets the starting liquid level.</summary>
        public TankBuilder WithLiquidLevel(Quantity level) => WithDynamicProperty("Liquid Level", level);

        /// <summary>Models the tank closed, computing vapour-space pressure instead of assuming atmospheric.</summary>
        public TankBuilder AsClosedTank(bool closed = true) => WithDynamicProperty("Closed Tank", closed);

        /// <summary>Sets ambient heat loss: the surrounding temperature and the UA product. UA = 0 disables it.</summary>
        public TankBuilder WithAmbientHeatLoss(Quantity ambientTemperature, double uaProduct)
        {
            WithDynamicProperty("Ambient Temperature", ambientTemperature);
            return WithDynamicProperty("Ambient UA Product", uaProduct);
        }

        /// <summary>Sets the floor below which the dynamic pressure is not allowed to fall.</summary>
        public TankBuilder WithMinimumPressure(Quantity pressure) => WithDynamicProperty("Minimum Pressure", pressure);

        /// <summary>Fills the tank from its inlet stream when it starts a run empty.</summary>
        public TankBuilder InitializeFromInlet(bool initialize = true) =>
            WithDynamicProperty("Initialize using Inlet Stream", initialize);

        /// <summary>Empties the tank on the next run.</summary>
        public TankBuilder ResetContent() => WithDynamicProperty("Reset Content", true);

        /// <summary>The current liquid level, in metres.</summary>
        public double LiquidLevel => GetDynamicValue("Liquid Level");

        /// <summary>The current operating pressure, in pascal.</summary>
        public double OperatingPressure => GetDynamicValue("Operating Pressure");
    }
}
