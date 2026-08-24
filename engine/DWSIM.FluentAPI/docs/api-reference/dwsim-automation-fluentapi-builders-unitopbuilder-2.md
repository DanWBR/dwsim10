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

### `FlipHorizontal(bool)`

Mirrors the object horizontally (swaps its inlet and outlet sides), as one does on a recycle return.

### `FlipVertical(bool)`

Mirrors the object vertically (swaps its top and bottom).

### `GetDynamicProperty(string)`

Reads a dynamic-mode property, or null when the object has none by that name.

### `GetDynamicValue(string)`

Reads a numeric dynamic-mode property in SI units, or 0 when it is unset.

### `PositionAt(int, int)`

Places the object at (x, y) on the canvas.

### `Rotate(int)`

Rotates the object on the canvas; use 0, 90, 180 or 270 degrees.

### `WithDynamicProperty(string, double)`

Sets a dynamic-mode property by name, e.g. `"Liquid Level"` or `"Volume"`. The value is in SI units, matching what DWSIM stores internally.

### `WithDynamicProperty(string, Quantity)`

Sets a dynamic-mode property from a unit-aware quantity.

### `WithDynamicProperty(string, bool)`

Sets a boolean dynamic-mode property, e.g. `"Reset Content"`.

### `WithDynamicsSpec(DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType)`

Declares whether the object is specified by pressure or by flow in the dynamic pressure-flow network. A network with no pressure specification anywhere is underdetermined.

## Properties

### `DynamicProperties`

Every dynamic-mode property this object exposes, with descriptions and units.

### `Flowsheet`

The owning flowsheet.

### `Object`

The underlying DWSIM object.

### `Self`

Returns this cast to the derived builder type, for chaining.
