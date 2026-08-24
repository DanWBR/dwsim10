# Dynamics.ControllerInfo

`DWSIM.Automation.FluentAPI.Dynamics.ControllerInfo`

A PID controller's wiring and tuning, read off the flowsheet.

## Properties

### `Active`

Whether the controller runs at all.

### `CascadeMasterId`

Internal name of the master controller in a cascade, empty when standalone.

### `ControlledObjectId`

Internal name of the object holding the process variable.

### `ControlledProperty`

Property identifier of the process variable.

### `ControlledUnits`

Display units of the process variable.

### `CumulativeError`

Accumulated error over the last run.

### `ExecutionOrder`

Position in the controller execution order, low first.

### `Id`

The controller's internal name.

### `IsWired`

True when the controller has both a process and a manipulated variable wired.

### `Kd`

Derivative gain.

### `Ki`

Integral gain.

### `Kp`

Proportional gain.

### `ManipulatedObjectId`

Internal name of the object holding the manipulated variable.

### `ManipulatedProperty`

Property identifier of the manipulated variable.

### `ManipulatedUnits`

Display units of the manipulated variable.

### `ManipulatedVariable`

Manipulated variable at the last solved step.

### `ManualOverride`

Whether the controller is in manual, holding its output fixed.

### `Output`

Controller output at the last solved step.

### `OutputMax`

Upper output clamp.

### `OutputMin`

Lower output clamp.

### `ProcessVariable`

Process variable at the last solved step.

### `ReverseActing`

Whether the control action is reversed.

### `SetPoint`

Setpoint, in the controlled property's display units.

### `Tag`

The controller's tag.
