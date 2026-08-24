# Builders.TankBuilder

`DWSIM.Automation.FluentAPI.Builders.TankBuilder`

Fluent builder for the Tank unit operation. Call [`AddTank`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `AsClosedTank(bool)`

Models the tank closed, computing vapour-space pressure instead of assuming atmospheric.

### `InitializeFromInlet(bool)`

Fills the tank from its inlet stream when it starts a run empty.

### `ResetContent`

Empties the tank on the next run.

### `WithAmbientHeatLoss(Quantity, double)`

Sets ambient heat loss: the surrounding temperature and the UA product. UA = 0 disables it.

### `WithHeight(Quantity)`

Sets the liquid height available for accumulation, which fixes the level-to-volume ratio.

### `WithLiquidLevel(Quantity)`

Sets the starting liquid level.

### `WithMinimumPressure(Quantity)`

Sets the floor below which the dynamic pressure is not allowed to fall.

### `WithVolume(Quantity)`

Sets the tank's internal volume.

## Properties

### `LiquidLevel`

The current liquid level, in metres.

### `OperatingPressure`

The current operating pressure, in pascal.
