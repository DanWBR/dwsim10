# WilsonConfig

`DWSIM.Automation.FluentAPI.WilsonConfig`

Wilson binary parameter setter. Wilson stores its BIPs in a CAS-keyed `Dictionary<string, Dictionary<string, double[]>>`; this helper sets `{A12, A21}` for the given CAS pair.

## Methods

### `WithBinaryByCAS(string, string, double, double)`

Sets the Wilson binary parameters {`a12`, `a21`} for the CAS-keyed pair (`cas1`, `cas2`).
