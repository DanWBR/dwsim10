# DynamicsSeries

`DWSIM.Automation.FluentAPI.DynamicsSeries`

One monitored variable's time series, with the control-loop metrics you would otherwise compute by hand: overshoot, rise time, settling time, error integrals and oscillation.

## Remarks

Times are seconds from the start of the run; values are in the variable's display units. The metrics assume the series covers a single disturbance — a step response. Run a longer simulation with several events and they describe the whole window, not each event.

## Methods

### `FromSamples(string, double[], double[], string)`

Builds a series from samples you already have, so the same control metrics apply to data that did not come from a run — plant history, a spreadsheet, another simulator.

**Parameters**

- `name` — Name for the series.
- `times` — Sample times in seconds, ascending.
- `values` — Sample values.
- `units` — Display units, for reporting only.

### `HasConverged(double)`

True when the tail of the series is flat: the relative change over the last tenth of the run is below `relativeTolerance`.

### `IAE(double)`

Integral of the absolute error against a setpoint, by the trapezoidal rule.

### `ISE(double)`

Integral of the squared error against a setpoint.

### `IsOscillating(double@, double@)`

Detects oscillation by counting crossings of the settled value. Reports the period and the decay ratio between successive peaks — a ratio near or above 1 means it is not decaying.

### `ITAE(double)`

Time-weighted integral of the absolute error, which penalises slow settling.

### `Offset(double)`

Steady-state offset from the setpoint, in [`Units`](dwsim-automation-fluentapi-dynamicsseries.md).

### `Overshoot(double)`

Peak overshoot past the setpoint, as a percentage of the step from the initial value. Returns 0 when the response never crosses past the setpoint.

### `PeakTime(double)`

Time at which the peak used by [`Overshoot`](dwsim-automation-fluentapi-dynamicsseries.md) occurs, in seconds.

### `RiseTime(double, double)`

Time taken to travel from `lowFraction` to `highFraction` of the way from the initial value to the settled value. NaN when the response never gets there.

### `SaturationFraction(double, double)`

Fraction of samples sitting at or beyond the given bounds — a saturated actuator.

### `SettlingTime(double)`

Time after which the response stays inside `band` (as a fraction of the step) around the settled value. NaN when it never settles within the run.

### `SteadyState(double)`

The settled value, taken as the mean over the last `lastFraction` of the run. Averaging beats reading the final point, which can land on a ripple.

### `ToString`

Returns `"<name> (<units>): N points, final = X"`.

### `ValueAt(double)`

Linearly interpolates the value at a given time; clamps outside the recorded range.

## Properties

### `Count`

Number of samples.

### `Final`

The last recorded value.

### `HasDiverged`

True when the series contains a NaN, an infinity, or a value beyond 1e12.

### `Initial`

The first recorded value.

### `Max`

The largest recorded value.

### `Min`

The smallest recorded value.

### `Name`

The variable's description, as configured on the integrator.

### `ObjectId`

Internal name of the object the property belongs to.

### `ObjectTag`

Tag of the object the property belongs to.

### `PropertyId`

The property identifier being recorded.

### `TimeSeconds`

Sample times, in seconds from the start of the run.

### `Units`

Display units of the recorded values.

### `Values`

Sample values, in [`Units`](dwsim-automation-fluentapi-dynamicsseries.md).

### `VariableId`

The monitored variable's internal ID.
