# Builders.ScheduleBuilder

`DWSIM.Automation.FluentAPI.Builders.ScheduleBuilder`

Configures a dynamics schedule: which integrator runs, which event set and cause-and-effect matrix are active, and what state the run starts from. Obtain one from [`DefineSchedule`](dwsim-automation-fluentapi-builders-dynamicsconfigbuilder.md).

## Methods

### `Configure(Action{DWSIM.Interfaces.IDynamicsSchedule})`

Escape hatch: applies an arbitrary mutation to the underlying schedule.

### `MakeCurrent`

Makes this the flowsheet's current schedule, which is what a run defaults to.

### `ResetContentsOfAllObjects(bool)`

Empties the hold-up of every object with dynamic content at the start of each run.

### `Run`

Returns a [`Builders.DynamicsBuilder`](dwsim-automation-fluentapi-builders-dynamicsbuilder.md) that will run this schedule.

### `UseCurrentStateAsInitial(bool)`

Starts each run from wherever the flowsheet happens to be, instead of from a stored state. This is the default, and the only option when no state has been stored.

### `WithCauseAndEffectMatrix(string)`

Activates a cause-and-effect matrix on this schedule, by its description.

### `WithEventSet(string)`

Activates an event set on this schedule, by its description.

### `WithInitialState(string)`

Starts each run from a stored state, which is what makes repeated runs comparable — PID tuning depends on it. Store one with [`StoreCurrentStateAs`](dwsim-automation-fluentapi-builders-dynamicsconfigbuilder.md).

### `WithIntegrator(string)`

Assigns the integrator this schedule runs, by its description.

### `WithoutCauseAndEffectMatrix`

Stops evaluating the cause-and-effect matrix, without deleting it.

### `WithoutEventSet`

Stops running the event set, without deleting it.

## Properties

### `Id`

The schedule's internal ID.

### `Name`

The schedule's description, which is how a run refers to it.

### `Object`

The underlying DWSIM schedule.
