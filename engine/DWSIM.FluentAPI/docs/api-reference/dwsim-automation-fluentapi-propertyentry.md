# PropertyEntry

`DWSIM.Automation.FluentAPI.PropertyEntry`

One property of a simulation object, as seen from the outside: the ID you pass to `Monitor`, `ChangeProperty` or `SetPropertyValue`, plus enough metadata to decide whether it is the one you wanted.

## Methods

### `ToString`

Returns `"<id> — <description> (<units>)"`.

## Properties

### `Description`

Human-readable description, as shown in DWSIM's property grid.

### `Id`

Property identifier, e.g. `"PROP_MS_2"` or a dynamic property name like `"Liquid Level"`.

### `IsDynamic`

True for properties that only exist in dynamic mode (the object's extra properties).

### `Units`

Display units for the value, empty when the property is dimensionless.

### `Value`

Current value in display units, or null when it could not be read.

### `Writable`

True when the property can be written to.
