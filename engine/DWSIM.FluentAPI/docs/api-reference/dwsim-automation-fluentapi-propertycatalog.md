# PropertyCatalog

`DWSIM.Automation.FluentAPI.PropertyCatalog`

Lists the properties a simulation object exposes. This is what makes monitored variables and dynamic events discoverable: both are addressed by property ID, and those IDs are not guessable from outside DWSIM.

## Methods

### `Describe(DWSIM.Interfaces.ISimulationObject, string)`

The most readable name a property identifier has. Most objects do not override `GetPropertyDescription` and return a placeholder, so fall back to the flowsheet's localised name — which is what DWSIM's own property grid shows — and to the identifier.

### `DynamicFor(DWSIM.Interfaces.ISimulationObject, DWSIM.Interfaces.IUnitsOfMeasure)`

Lists the object's dynamic-mode properties (its extra properties), creating them first when the object has not been through dynamic mode yet.

### `EnsureDynamicProperties(DWSIM.Interfaces.ISimulationObject)`

Creates the object's dynamic properties when it has none yet. Objects only get them on first use, so anything reading them outside a dynamic run has to ask first.

### `For(DWSIM.Interfaces.ISimulationObject, DWSIM.Interfaces.IUnitsOfMeasure, DWSIM.Interfaces.Enums.PropertyType)`

Lists the object's regular properties of the given kind.

### `Monitorable(DWSIM.Interfaces.ISimulationObject, DWSIM.Interfaces.IUnitsOfMeasure)`

Lists the properties that make sense as monitored variables: the numeric ones. This is the list to show when a caller asks to monitor a property that does not exist.
