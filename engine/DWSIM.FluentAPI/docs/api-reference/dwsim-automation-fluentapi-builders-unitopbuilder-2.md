# Builders.UnitOpBuilder`2

`DWSIM.Automation.FluentAPI.Builders.UnitOpBuilder`2`

Base class for all fluent unit-operation builders. Provides port-based connection helpers (feed/product material and energy streams) shared by every `DWSIM.Interfaces.ISimulationObject`.

**Type parameters**

- `TObject` — Concrete DWSIM unit-operation class.
- `TSelf` — CRTP self type so chained calls return the derived builder.

## Constructors

### `(ctor)(Flowsheet, `0)`

Initialises the builder with its owning flowsheet and the underlying DWSIM object.

## Methods

### `Configure(Action{`0})`

Escape hatch: applies an arbitrary mutation to the underlying DWSIM object.

### `ConnectEnergyFeed(EnergyStreamBuilder, int)`

Connects an energy stream as a feed at the given port.

### `ConnectEnergyProduct(EnergyStreamBuilder, int)`

Connects an energy stream as a product at the given port.

### `ConnectFeed(MaterialStreamBuilder, int)`

Connects a material stream as a feed at the given port (default 0).

### `ConnectNewProduct(string, int)`

Creates a new material stream with `newTag` and connects it as a product at the given port. Returns the new stream's builder for further chaining.

### `ConnectProduct(MaterialStreamBuilder, int)`

Connects a material stream as a product at the given port (default 0).

## Properties

### `Flowsheet`

The owning flowsheet.

### `Object`

The underlying DWSIM object.

### `Self`

Returns this cast to the derived builder type, for chaining.
