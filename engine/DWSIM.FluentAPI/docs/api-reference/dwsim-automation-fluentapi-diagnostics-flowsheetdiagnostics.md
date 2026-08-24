# Diagnostics.FlowsheetDiagnostics

`DWSIM.Automation.FluentAPI.Diagnostics.FlowsheetDiagnostics`

Checks a flowsheet before it is solved, and explains what went wrong after it is.

## Remarks

The rules encode the mistakes that keep a freshly assembled flowsheet from solving: a feed with no composition, a port left dangling, a recycle with nothing to start from, a loop with nothing to tear it. Each finding carries a fix, so a caller can act on it without having read the model. Checking is cheap and solving is not, so [`Check`](dwsim-automation-fluentapi-diagnostics-flowsheetdiagnostics.md) is worth running first every time. What it cannot know it does not guess: a rule fires only when it is certain, because a false blocker sends a caller chasing a problem that is not there.

## Methods

### `Check(DWSIM.Interfaces.IFlowsheet)`

Everything wrong with the flowsheet as it stands, worst first.

**Parameters**

- `flowsheet` — The flowsheet to check.

**Returns**: Findings ordered blockers first. An empty list means nothing known to be wrong; it is not a promise that the solve will converge.

### `Diagnose(DWSIM.Interfaces.IFlowsheet, Collections.Generic.IEnumerable{Exception})`

Explains a solve that failed, or that finished with objects left unconverged.

**Parameters**

- `flowsheet` — The flowsheet that was solved.
- `errors` — The exceptions the solver returned; may be null or empty.

### `OwnerOf(DWSIM.Interfaces.IFlowsheet, Exception)`

The tag of the object an exception names, empty when it names none.

## Fields

### `PortlessTypes`

Objects that legitimately have no material ports.
