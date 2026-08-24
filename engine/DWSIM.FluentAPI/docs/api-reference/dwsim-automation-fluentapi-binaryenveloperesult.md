# BinaryEnvelopeResult

`DWSIM.Automation.FluentAPI.BinaryEnvelopeResult`

Result of a binary phase diagram calculation. Compositions are mole fractions of the first compound (0 to 1). Temperatures in K, pressures in Pa depending on diagram type.

## Properties

### `Critical_X`

Critical locus composition. T-x-y only, may be empty.

### `Critical_Y`

Critical locus temperatures (K). T-x-y only, may be empty.

### `DiagramType`

Diagram type: "T-x-y", "P-x-y", "(T)x-y", or "(P)x-y".

### `LLE_X1`

LLE first liquid composition (mole fraction). May be empty.

### `LLE_X2`

LLE second liquid composition (mole fraction). May be empty.

### `LLE_Y`

LLE curve values (T or P). May be empty.

### `SLE_X1`

SLE first curve composition. T-x-y only, may be empty.

### `SLE_X2`

SLE second curve composition. T-x-y only, may be empty.

### `SLE_Y1`

SLE first curve temperatures (K). T-x-y only, may be empty.

### `SLE_Y2`

SLE second curve temperatures (K). T-x-y only, may be empty.

### `X`

Composition axis (mole fraction of first compound).

### `Y1`

Bubble-point curve values (T in K for T-x-y, P in Pa for P-x-y).

### `Y2`

Dew-point curve values (T in K for T-x-y, P in Pa for P-x-y).
