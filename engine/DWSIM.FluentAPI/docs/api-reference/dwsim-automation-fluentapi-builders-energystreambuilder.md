# Builders.EnergyStreamBuilder

`DWSIM.Automation.FluentAPI.Builders.EnergyStreamBuilder`

Fluent wrapper for an `DWSIM.UnitOperations.Streams.EnergyStream`. Energy in DWSIM is in kW.

## Methods

### `Configure(Action{DWSIM.UnitOperations.Streams.EnergyStream})`

Escape hatch for any property not covered by a `WithX` helper. Mutates the underlying object via the supplied delegate.

### `FlipHorizontal(bool)`

Mirrors the stream horizontally.

### `FlipVertical(bool)`

Mirrors the stream vertically.

### `PositionAt(int, int)`

Places the stream at (x, y) on the canvas.

### `Rotate(int)`

Rotates the stream on the canvas; use 0, 90, 180 or 270 degrees.

### `WithEnergyFlow(Quantity)`

Sets the energy flow (kW). Pass via `10.Kilowatts()`.

## Properties

### `EnergyFlowKW`

Read-back of `Energy Flow KW` from the underlying object (populated after `Solve`).

### `Flowsheet`

The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.

### `Object`

The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.
