# DynamicsResult

`DWSIM.Automation.FluentAPI.DynamicsResult`

Immutable result returned by
[`DynamicsBuilder.Execute`](dwsim-automation-fluentapi-builders-dynamicsbuilder.md) /
`ExecuteAsync`. Contains the monitored-variable time series and a
success/failure indicator.

## Properties

### `MonitoredVariables`

`IReadOnlyDictionary<string, IReadOnlyList<(double TimeSeconds, double Value)>>`

Time-series data for each monitored variable. Keys are the variable
descriptions as configured in DWSIM's Dynamics Manager. Values are
chronologically ordered `(TimeSeconds, Value)` pairs where `TimeSeconds` is
the simulation time in seconds from t = 0 and `Value` is the reading in the
display units configured in DWSIM (not necessarily SI).

An empty dictionary is returned when no monitored variables were defined in the
schedule's integrator, or when the integration failed before any steps
completed.

### `Completed`

`bool`

`true` when integration ran to the configured end time without exception.
`false` when it was stopped early by an error.

### `Error`

`Exception`

The exception that caused integration to stop, or `null` when `Completed` is
`true`.
