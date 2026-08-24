# Builders.VesselBuilder

`DWSIM.Automation.FluentAPI.Builders.VesselBuilder`

Fluent builder for the Vessel unit operation. Call `AddVessel` to obtain one.

## Methods

### `InitializeFromInlet(bool)`

Fills the vessel from its inlet stream when it starts a run empty.

### `ResetContent`

Empties the vessel on the next run.

### `UseDimensionsForHeight(bool)`

Takes the height from the configured geometry instead of the entered value.

### `UseDimensionsForVolume(bool)`

Takes the volume from the configured geometry instead of the entered value.

### `WithHeight(Quantity)`

Sets the height available for liquid, which fixes the level-to-volume ratio.

### `WithLiquidLevel(Quantity)`

Sets the starting liquid level.

### `WithMinimumPressure(Quantity)`

Sets the floor below which the dynamic pressure is not allowed to fall.

### `WithOrientation(VesselOrientation)`

Sets the vessel orientation, which changes how level maps to hold-up.

### `WithVolume(Quantity)`

Sets the vessel's internal volume. A vessel with no volume has no hold-up to integrate.

## Properties

### `LiquidLevel`

The current liquid level, in metres.

### `OperatingPressure`

The current operating pressure, in pascal.
