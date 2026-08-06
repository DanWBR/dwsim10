# Builders.DynamicsBuilder

`DWSIM.Automation.FluentAPI.Builders.DynamicsBuilder`

Fluent builder for configuring and running a dynamic (time-domain) integration
on a DWSIM flowsheet. Obtain an instance via
[`Flowsheet.RunDynamics`](dwsim-automation-fluentapi-flowsheet.md).

## Methods

### `WithSchedule(string name)`

Selects the dynamics schedule to run by its description as configured in
DWSIM's Dynamics Manager. When not called, the first schedule in the flowsheet
is used automatically. Returns this builder for chaining.

### `WithRealTime(bool enabled = true)`

Enables or disables real-time pacing. When `true`, each integration step is
paced to the wall clock and the run continues indefinitely. When `false`
(default), the integrator runs as fast as possible for the duration configured
in the schedule. Returns this builder for chaining.

### `OnPreStep(Runner.IntegratorPreStepEventHandler handler)`

Registers a callback invoked before each integration step is solved. Multiple
calls accumulate handlers. Returns this builder for chaining.

Event args: `IntegratorPreStepEventArgs` — `tstep` (step index), `tstamp`
(simulation DateTime), `status`, `flowsheet`.

### `OnPostStep(Runner.IntegratorPostStepEventHandler handler)`

Registers a callback invoked after each integration step completes. Multiple
calls accumulate handlers. Returns this builder for chaining.

Event args: `IntegratorPostStepEventArgs` — `variables`
(`List<IDynamicsMonitoredVariable>`), `tstep`, `tstamp`, `status`,
`flowsheet`.

### `Execute()`

Runs the integration synchronously, blocking until it completes or errors.
Returns [`DynamicsResult`](dwsim-automation-fluentapi-dynamicsresult.md).

### `ExecuteAsync()`

Runs the integration asynchronously.
Returns `Task<`[`DynamicsResult`](dwsim-automation-fluentapi-dynamicsresult.md)`>`.
