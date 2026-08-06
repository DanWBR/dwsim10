# NRTLConfig

`DWSIM.Automation.FluentAPI.NRTLConfig`

NRTL binary parameter setter. Sets A12/A21 (cal/mol), alpha12 (non-randomness, ~0.3 typical) and optional B12/B21 (T-dependent terms).

## Methods

### `WithBinary(string, string, double, double, double, double, double)`

Sets the NRTL binary parameters for the (`compound1`, `compound2`) pair: `a12`/`a21` in cal/mol, non-randomness `alpha12`, optional T-dependent `b12`/`b21`.
