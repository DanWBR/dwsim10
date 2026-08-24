# Dynamics.PidTuningOptions

`DWSIM.Automation.FluentAPI.Dynamics.PidTuningOptions`

How to tune, and what to tune.

## Fields

### `AbortRequested`

Polled between trials; returning true stops the search.

### `Apply`

Leave the tuned gains on the controllers. When false, the originals are restored.

### `ControllerTags`

Tags of the controllers to tune. All of them when null or empty.

### `KdMax`

Upper bound on Kd.

### `KiMax`

Upper bound on Ki.

### `KpMaxFactor`

Upper bound on Kp, as a multiple of its starting value.

### `MaxEvaluations`

Simplex function-evaluation budget. Each evaluation runs the whole schedule once.

### `MaxWallTimePerRun`

Wall-clock limit for a single trial run.

### `Objective`

What to minimise.

### `OnProgress`

Receives one line per trial.

### `ScheduleName`

Schedule to run for each trial; the current or first one when null.
