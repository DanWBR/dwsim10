# Dynamics.DynamicsIntrospection

`DWSIM.Automation.FluentAPI.Dynamics.DynamicsIntrospection`

Reads what a flowsheet offers a dynamic simulation: which objects have dynamic models, how the pressure-flow network is specified, how the controllers are wired, and what the Dynamics Manager already holds.

## Methods

### `AddressableProperties(DWSIM.Interfaces.IFlowsheet, string)`

Lists the properties of one object that make sense to monitor or disturb: the numeric regular properties plus every dynamic-mode property.

### `Inspect(DWSIM.Interfaces.IFlowsheet)`

Surveys the flowsheet.

### `Resolve(DWSIM.Interfaces.IFlowsheet, string)`

Finds an object by tag, falling back to its internal name.
