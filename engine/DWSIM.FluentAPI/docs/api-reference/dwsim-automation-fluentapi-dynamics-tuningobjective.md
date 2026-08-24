# Dynamics.TuningObjective

`DWSIM.Automation.FluentAPI.Dynamics.TuningObjective`

What the tuner minimises.

## Fields

### `CumulativeError`

Sum of the controllers' own accumulated error. What the GUI tuning tool uses.

### `IAE`

Integral of the absolute error. The usual default: balanced and readable.

### `ISE`

Integral of the squared error. Punishes large excursions harder.

### `ITAE`

Time-weighted absolute error. Punishes slow settling hardest.
