# Dynamics.PidTuningResult

`DWSIM.Automation.FluentAPI.Dynamics.PidTuningResult`

Outcome of a tuning run.

## Properties

### `Aborted`

Whether the search was cut short.

### `Applied`

Whether the tuned gains were left on the controllers.

### `Controllers`

The gains found, one entry per tuned controller.

### `Error`

What stopped the search, or null.

### `Evaluations`

How many trial runs the search used.

### `FinalObjective`

The objective at the gains found.

### `ImprovementPercent`

Relative improvement in the objective, as a percentage. Negative means it got worse.

### `InitialObjective`

The objective at the starting gains.

### `Log`

One line per trial.

### `Succeeded`

True when the search completed and improved on the starting gains.
