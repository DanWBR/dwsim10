using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Vertical or horizontal vessel orientation.</summary>
    public enum VesselOrientation
    {
        /// <summary>Vertical.</summary>
        Vertical = 0,
        /// <summary>Horizontal.</summary>
        Horizontal = 1
    }

    /// <summary>Fluent builder for the Vessel unit operation. Call <see cref="Flowsheet.AddVessel"/> to obtain one.</summary>
    public sealed class VesselBuilder : UnitOpBuilder<Vessel, VesselBuilder>
    {
        internal VesselBuilder(Flowsheet f, Vessel o) : base(f, o) { }

        // ------------------------------------------------------ Dynamic mode

        /// <summary>Sets the vessel's internal volume. A vessel with no volume has no hold-up to integrate.</summary>
        public VesselBuilder WithVolume(Quantity volume) => WithDynamicProperty("Volume", volume);

        /// <summary>Sets the height available for liquid, which fixes the level-to-volume ratio.</summary>
        public VesselBuilder WithHeight(Quantity height) => WithDynamicProperty("Height", height);

        /// <summary>Sets the starting liquid level.</summary>
        public VesselBuilder WithLiquidLevel(Quantity level) => WithDynamicProperty("Liquid Level", level);

        /// <summary>Sets the vessel orientation, which changes how level maps to hold-up.</summary>
        public VesselBuilder WithOrientation(VesselOrientation orientation) =>
            WithDynamicProperty("Vessel Orientation", (double)(int)orientation);

        /// <summary>Takes the volume from the configured geometry instead of the entered value.</summary>
        public VesselBuilder UseDimensionsForVolume(bool use = true) =>
            WithDynamicProperty("Get Volume from Dimensions", use);

        /// <summary>Takes the height from the configured geometry instead of the entered value.</summary>
        public VesselBuilder UseDimensionsForHeight(bool use = true) =>
            WithDynamicProperty("Get Height from Dimensions", use);

        /// <summary>Sets the floor below which the dynamic pressure is not allowed to fall.</summary>
        public VesselBuilder WithMinimumPressure(Quantity pressure) =>
            WithDynamicProperty("Minimum Pressure", pressure);

        /// <summary>Fills the vessel from its inlet stream when it starts a run empty.</summary>
        public VesselBuilder InitializeFromInlet(bool initialize = true) =>
            WithDynamicProperty("Initialize using Inlet Stream", initialize);

        /// <summary>Empties the vessel on the next run.</summary>
        public VesselBuilder ResetContent() => WithDynamicProperty("Reset Content", true);

        /// <summary>The current liquid level, in metres.</summary>
        public double LiquidLevel => GetDynamicValue("Liquid Level");

        /// <summary>The current operating pressure, in pascal.</summary>
        public double OperatingPressure => GetDynamicValue("Operating Pressure");
    }
}
