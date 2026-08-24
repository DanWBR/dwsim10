# Dynamics.DynamicObjectInfo

`DWSIM.Automation.FluentAPI.Dynamics.DynamicObjectInfo`

One object on the flowsheet, described from a dynamic-simulation point of view.

## Properties

### `DynamicProperties`

The object's dynamic-mode properties, with descriptions, units and current values.

### `DynamicsSpec`

Whether the object is specified by pressure or by flow in the pressure-flow network.

### `Id`

The object's internal name, which events and monitored variables address it by.

### `SupportsDynamics`

False when the object has no dynamic model. It is still solved at every step, but at steady state — it contributes no hold-up and no lag.

### `Tag`

The object's tag, as shown on the flowsheet.

### `Type`

The object's display type, e.g. "Tank".
