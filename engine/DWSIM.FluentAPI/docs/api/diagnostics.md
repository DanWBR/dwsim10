# Diagnostics

Building a flowsheet that solves is mostly a matter of not making one of a dozen mistakes, and
the solver is a poor teacher: it reports the exception it hit, not the fault that caused it.

`FlowsheetDiagnostics` names the fault instead. Every finding carries a code to branch on, the
object to look at, one sentence on what is wrong, and one on what to do:

```csharp
using DWSIM.Automation.FluentAPI.Diagnostics;

foreach (var finding in FlowsheetDiagnostics.Check(fs.Inner))
    Console.WriteLine(finding);

// [BLOCKER] UNIT_NO_PRODUCT (H-1): This unit operation has no product, so its result
//           has nowhere to go. Fix: Connect a material stream to one of its outlet ports.
```

## Check before you solve

`Check` reads the flowsheet as it stands and never solves anything, so it costs microseconds
against a solve that costs seconds. Run it before every `Solve()` and most failures turn into a
fix applied beforehand.

```csharp
var blockers = FlowsheetDiagnostics.Check(fs.Inner)
    .Where(f => f.Severity == DiagnosticSeverity.Blocker)
    .ToList();

if (blockers.Count > 0)
{
    foreach (var b in blockers) Console.WriteLine(b);
    return;
}

fs.Solve();
```

An empty list means nothing *known* to be wrong. It is not a promise that the solve converges —
no static check can make that promise about a flash.

## Explain what did go wrong

`Diagnose` takes the exceptions a solve returned and explains them: which object raised it, and
what setup fault sits behind it.

```csharp
var errors = fs.TrySolve();

if (errors.Count > 0)
{
    foreach (var finding in FlowsheetDiagnostics.Diagnose(fs.Inner, errors))
        Console.WriteLine(finding);
}
```

It reports the exception together with the flowsheet's own blockers, because the exception is
usually the symptom and the setup fault the cause. A pump that threw on a bad specification and
a feed with no flow are the same story told from two ends.

Passing `null` for the exceptions still works, and reads the objects' own state — useful when
the solve happened somewhere you did not catch its return.

## A finding

| Member | Description |
|---|---|
| `Code` | Stable identifier, e.g. `UNIT_NO_FEED`. Branch on this, not on the message. |
| `Severity` | `Blocker`, `Warning` or `Info`. |
| `ObjectTag` | The object concerned, empty when it is about the flowsheet as a whole. |
| `Message` | What is wrong, in one sentence. |
| `Fix` | What to do about it, in one sentence. |

Findings come back worst first, so a caller working top-down fixes what matters soonest.

## What the rules will not do

A rule fires only when it is certain. Anything that needs a guess is left out, because a false
blocker sends whoever reads it chasing a problem that is not there, and one of those costs more
trust than ten warnings that never came.

That leaves real gaps, and they are worth knowing:

- **A new stream is born at 298.15 K, 1 atm and 1 kg/s.** Those are plausible numbers, so a feed
  nobody configured looks exactly like one deliberately set to ambient conditions. The rules
  cannot tell them apart and do not try.
- **Degrees of freedom are not counted.** Whether a unit operation has enough specifications is a
  question about that model, not about the topology.
- **Thermodynamics is not judged.** Picking Steam Tables for a hydrocarbon train is a mistake no
  static rule can see.

## Codes

| Code | Meaning |
|---|---|
| `EMPTY_FLOWSHEET` | The flowsheet has no objects. |
| `NO_COMPOUNDS` | The flowsheet has no compounds, so no stream can carry anything. |
| `NO_PROPERTY_PACKAGE` | The flowsheet has no property package, so nothing can be flashed. |
| `DUPLICATE_TAG` | Two or more objects share a tag, so addressing one by tag is ambiguous. |
| `STREAM_DANGLING` | A stream is connected to nothing at either end. |
| `ENERGY_STREAM_HALF_CONNECTED` | An energy stream is attached at one end only. |
| `UNIT_UNCONNECTED` | A unit operation has nothing connected to it. |
| `UNIT_NO_FEED` | A unit operation has no feed, so it has nothing to process. |
| `UNIT_NO_PRODUCT` | A unit operation has no product, so its result has nowhere to go. |
| `FEED_NO_PRESSURE` | A boundary feed has no pressure. |
| `FEED_NO_TEMPERATURE` | A boundary feed has no temperature. |
| `FEED_NO_FLOW` | A boundary feed carries no flow. |
| `FEED_NO_COMPOSITION` | Every compound in a boundary feed is at zero. |
| `FEED_COMPOSITION_NOT_NORMALISED` | The mole fractions of a boundary feed do not sum to 1. |
| `RECYCLE_NO_ESTIMATE` | A recycle starts from a zero estimate. |
| `LOGICAL_TARGET_MISSING` | An adjust or specification does not name both the object it reads and the one it writes. |
| `SOLVER_EXCEPTION` | The solver raised an exception. |
| `INFINITE_LOOP` | The solver found a cycle with no recycle to tear it. |
| `NOT_CONVERGED` | A unit operation did not solve. |
| `STREAM_NOT_FINITE` | A stream carries a flow that is not a finite number. |
| `NEGATIVE_FLOW` | A stream carries a negative flow. |
This table is generated from `FlowsheetCodes.All`, the same source the tools and the catalogue
read.

## Dynamic simulation

`DynamicsDiagnostics` does the same for a dynamic run — readiness before, post-mortem after —
and shares the `Finding` type. See [Dynamic Simulation](dynamics.md#diagnosis) and its own
[codes](../ai/diagnostics.md#dynamic-simulation-codes).

## Over MCP and HTTP

The same rules reach a language model as `dwsim_flowsheet_check` and `GET /api/flowsheet/check`.
See [Diagnostics for the AI Assistant](../ai/diagnostics.md).
