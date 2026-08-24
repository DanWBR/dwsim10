# Diagnostics.Finding

`DWSIM.Automation.FluentAPI.Diagnostics.Finding`

One thing wrong, or suspicious, about a simulation.

## Methods

### `ToString`

Returns `"[SEVERITY] CODE (tag): message Fix: ..."`.

## Properties

### `Code`

Stable identifier, e.g. `VALVE_NO_KV`. See [`Diagnostics.DiagnosticCodes`](dwsim-automation-fluentapi-diagnostics-diagnosticcodes.md).

### `Fix`

What to do about it, in one sentence.

### `Message`

What is wrong, in one sentence.

### `ObjectTag`

Tag of the object the finding is about, empty when it concerns the flowsheet as a whole.

### `Severity`

How much this matters.
