# Builders.EnergyStreamBuilder

`DWSIM.Automation.FluentAPI.Builders.EnergyStreamBuilder`

Fluent wrapper for an `DWSIM.UnitOperations.Streams.EnergyStream`. Energy in DWSIM is in kW.

## Methods

### `Configure(Action{DWSIM.UnitOperations.Streams.EnergyStream})`

Escape hatch for any property not covered by a `WithX` helper. Mutates the underlying object via the supplied delegate.

### `WithEnergyFlow(Quantity)`

Sets the energy flow (kW). Pass via `10.Kilowatts()`.

## Properties

### `EnergyFlowKW`

Read-back of `Energy Flow KW` from the underlying object (populated after `Solve`).

### `Flowsheet`

The underlying DWSIM object / owning flowsheet — escape hatch for advanced use.

### `Object`

The underlying DWSIM object / owning flowsheet — escape hatch for advanced use.
