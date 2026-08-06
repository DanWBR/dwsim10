# Builders.ReactorBuilder`2

`DWSIM.Automation.FluentAPI.Builders.ReactorBuilder`2`

Common reactor configuration. Shared by all reactor builders via inheritance.

## Constructors

### `(ctor)(Flowsheet, `0)`

Initialises the reactor builder with its owning flowsheet and the underlying DWSIM reactor.

## Methods

### `Adiabatic`

Shortcut for [`WithOperationMode`](dwsim-automation-fluentapi-builders-reactorbuilder-2.md) with `Adiabatic`.

### `Isothermal`

Shortcut for [`WithOperationMode`](dwsim-automation-fluentapi-builders-reactorbuilder-2.md) with `Isothermic`.

### `WithOperationMode(DWSIM.UnitOperations.Reactors.OperationMode)`

Sets the thermal operation mode (Isothermic, Adiabatic, OutletTemperature, NonIsothermalNonAdiabatic, HeatExchange).

### `WithPressureDrop(Quantity)`

Sets the inlet-to-outlet pressure drop across the reactor.

### `WithReactionSet(string)`

Binds the reactor to the reaction set identified by `id`.

### `WithReactionSet(ReactionSetBuilder)`

Binds the reactor to the reaction set described by `set`.

## Properties

### `HeatDutyKW`

Heat duty exchanged with the surroundings (kW). Available after Solve().
