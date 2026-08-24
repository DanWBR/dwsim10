# Builders.DynamicsBuilder

`DWSIM.Automation.FluentAPI.Builders.DynamicsBuilder`

Configures and runs a dynamic (time-domain) integration on a flowsheet. Obtain an instance via [`RunDynamics`](dwsim-automation-fluentapi-flowsheet.md).

## Remarks

Build the schedule itself through [`Dynamics`](dwsim-automation-fluentapi-flowsheet.md); this builder only decides how the run is executed.

## Methods

### `Execute`

Runs the integration synchronously, blocking until it completes.

### `ExecuteAsync`

Runs the integration asynchronously.

### `FailIfBusy(bool)`

Fails instead of waiting when another integration is already running in this process. Integration drives global solver state, so runs are serialised.

### `FromCurrentState(bool)`

Starts the run from wherever the flowsheet is, ignoring the schedule's stored initial state.

### `OnPostStep(DWSIM.Automation.DynamicRunner.Runner.IntegratorPostStepEventHandler)`

Registers a callback invoked after each integration step completes.

### `OnPreStep(DWSIM.Automation.DynamicRunner.Runner.IntegratorPreStepEventHandler)`

Registers a callback invoked before each integration step is solved.

### `OnProgress(Action{DWSIM.Automation.DynamicRunner.IntegratorProgress})`

Reports progress once per step: simulated time, step index and a status string.

### `StopWhen(Func{DWSIM.Interfaces.IFlowsheet,double,bool})`

Stops the run cleanly when the predicate returns true, given the flowsheet and the simulated time in seconds. Checked before each step.

### `WithCancellation(Threading.CancellationToken)`

Stops the run when the token is cancelled, at the next step boundary.

### `WithHistorian(bool)`

Keeps a snapshot of the flowsheet at every step. Event transitions other than step changes interpolate from it, so ramps need it; turning it off makes long runs faster. Default is true.

### `WithMaxSteps(int)`

Stops the run after this many steps. The usual way to bound a real-time run.

### `WithMaxWallTime(TimeSpan)`

Stops the run after this much wall-clock time, whatever the simulated progress.

### `WithRealTime(bool)`

Enables or disables real-time pacing. When true, each integration step is paced to the wall clock and the run continues until stopped. Default is false (runs as fast as possible for the configured duration).

### `WithSchedule(string)`

Sets the dynamics schedule to run, by description or ID. Matching on the description ignores case. When not called, the flowsheet's current schedule is used, falling back to the first one defined.
