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

**Remarks**

The rules themselves live in `DWSIM.Automation.DynamicRunner.Setup.DynamicsReadiness`, one layer down, where the wizards in both user interfaces can reach them too. This maps what they report onto the finding type the rest of the Fluent API speaks.

### `Diagnose(DWSIM.Interfaces.IFlowsheet, DynamicsResult)`

Explains a finished run: what stopped it, and what the recorded series say about the model and its controllers.
