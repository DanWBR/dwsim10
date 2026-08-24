# Dynamics.PidTuner

`DWSIM.Automation.FluentAPI.Dynamics.PidTuner`

Tunes PID controllers by simulation: a Nelder-Mead simplex over their gains, running the whole schedule once per trial and scoring the resulting transient.

## Remarks

Trials are only comparable if they all start from the same state, so the schedule needs a stored initial state. When it has none, one is taken from the flowsheet as it stands and the schedule's configuration is put back afterwards.

## Methods

### `Tune(DWSIM.Interfaces.IFlowsheet, Dynamics.PidTuningOptions)`

Runs the search.
