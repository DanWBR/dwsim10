# Dynamics.DynamicsDiagnostics

`DWSIM.Automation.FluentAPI.Dynamics.DynamicsDiagnostics`

Checks a dynamic simulation before it runs, and explains what went wrong after it does.

## Remarks

The rules encode the mistakes that make a dynamic run fail or mislead: an underdetermined pressure-flow network, a valve with no Kv, a vessel with no volume, a controller wired backwards. Each finding carries a fix, so a caller can act on it without knowing the model.

## Methods

### `CheckReady(DWSIM.Interfaces.IFlowsheet, string)`

Answers "is this flowsheet ready to run dynamically?" — blockers first, then warnings.

**Parameters**

- `flowsheet` — The flowsheet to check.
- `scheduleName` — Schedule to check; the current or first one when null.

### `CheckSteadyState(DWSIM.Interfaces.IFlowsheet, Collections.Generic.List{Diagnostics.Finding})`

A dynamic run integrates forward from wherever the flowsheet is. Starting it from a state that was never solved means integrating from nothing, and the first step fails on whatever the steady state would have failed on.

### `Diagnose(DWSIM.Interfaces.IFlowsheet, DynamicsResult)`

Explains a finished run: what stopped it, and what the recorded series say about the model and its controllers.
