# DynamicsResult

`DWSIM.Automation.FluentAPI.DynamicsResult`

Result of a dynamic integration run: the time series of every monitored variable, plus what the run itself did — how many steps, how long, and what went wrong if anything did.

## Methods

### `GetSeries(string)`

Looks up a series by description, by monitored-variable ID, or by `"tag.PropertyId"`.

### `ReadSeries(DWSIM.Interfaces.IFlowsheet, DWSIM.Interfaces.IDynamicsIntegrator)`

Reads the series out of an integrator's recorded history.

**Remarks**

The history is keyed by timestamp ticks. Files written by older builds keyed it by step index instead, so a history whose largest key is too small to be a tick count is read as step indices and scaled by the integration step.

### `ToCsv(string)`

Writes the run to a CSV file: one row per step, one column per monitored variable, plus the time column. Values are invariant-culture, in display units.

### `ToCsv`

Renders the run as CSV text.

### `ToString`

Returns a one-line summary of the run.

### `TryGetSeries(string, DynamicsSeries@)`

Looks up a series without throwing.

## Properties

### `Aborted`

True when the run stopped on a cancellation, an abort request or a step/time limit.

### `Completed`

True if integration ran to completion without error and without being aborted.

### `Error`

The first exception that stopped integration, or null.

### `Errors`

Every exception raised during the run.

### `FinalTimeSeconds`

Simulated time reached, in seconds.

### `IntegratorName`

Description of the integrator that ran.

### `Item`

Looks up a series by description, by monitored-variable ID, or by `"tag.PropertyId"`.

### `MonitoredVariables`

Time-series data for each monitored variable, keyed by description. Kept for compatibility; [`Series`](dwsim-automation-fluentapi-dynamicsresult.md) carries the units and the metrics.

### `ScheduleName`

Description of the schedule that ran.

### `Series`

Every monitored variable's series, in the order the integrator holds them.

### `Steps`

Number of integration steps solved.

### `WallClock`

Wall-clock time the run took.
