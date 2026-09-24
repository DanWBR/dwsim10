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

## The explanation behind a code

The message says what is wrong with this object now. `FindingExplanations` says what the code
means in general, why it happens and how to fix it, in the words of someone teaching the
subject, with a page of the tutorials site to read next:

```csharp
foreach (var finding in FlowsheetDiagnostics.Check(fs.Inner))
{
    var e = FindingExplanations.For(finding);
    Console.WriteLine(e.Title);
    Console.WriteLine("  " + e.Meaning);
    Console.WriteLine("  Why: " + e.Why);
    Console.WriteLine("  Fix: " + e.HowToFix);
    Console.WriteLine("  Read: " + FindingExplanations.LearnMoreUrl(finding.Code, "pt-BR"));
}
```

`For` never returns null: a code without a written explanation gets one built from its one-line
description. `LearnMoreUrl` takes the language of the tutorials site (`en` or `pt-BR`) and maps
the page names between the two sites.

## Degrees of freedom

`DegreesOfFreedomAnalysis` lists, for every object, the specifications its calculation mode
reads and whether each one has a value. A sequential-modular solver computes each unit from its
feeds and its specifications, so the degrees of freedom of a unit are the values it still needs:

```csharp
var dof = DegreesOfFreedomAnalysis.Analyze(fs.Inner);
Console.WriteLine(dof.Remaining + " specification(s) missing");

foreach (var o in dof.Objects.Where(o => o.Remaining > 0))
{
    Console.WriteLine(o.ObjectTag + " (" + o.ObjectType + ", " + o.Mode + ")");
    foreach (var slot in o.Missing) Console.WriteLine("  needs " + slot.Name);
}
```

Each `SpecificationSlot` carries the name the editor uses, the property behind it, the SI value
when there is one, whether the mode reads it (`Required`) and whether it is usable (`IsSet`). A
feed stream needs two state variables, one flow and a composition; a heater in outlet-temperature
mode needs the outlet temperature, with the pressure drop and the efficiency listed as optional
because they have defaults; a mixer needs nothing. Types the analysis does not know come back
with `Supported == false` and no verdict.

The same holes surface in `Check` as `SPEC_MISSING`, one finding per object, naming the slots.

## Physical plausibility

`Diagnose` also reads the result the way an instructor would. A heater whose outlet came out
colder than its inlet, a pump whose outlet pressure is below its inlet, an exchanger whose cold
outlet went past the hot inlet, a shortcut column below its minimum reflux: all of these are
arithmetic the solver accepts and a process would not produce. They come back as warnings
(`HEATER_COOLED`, `PRESSURE_WRONG_DIRECTION`, `HX_TEMPERATURE_CROSS`, ...) so the numbers are
questioned before the report is handed in.

## What the rules will not do

A rule fires only when it is certain. Anything that needs a guess is left out, because a false
blocker sends whoever reads it chasing a problem that is not there, and one of those costs more
trust than ten warnings that never came.

That leaves real gaps, and they are worth knowing:

- **A new stream is born at 298.15 K, 1 atm and 1 kg/s.** Those are plausible numbers, so a feed
  nobody configured looks exactly like one deliberately set to ambient conditions. The rules
  cannot tell them apart and do not try.
- **Degrees of freedom are counted per object, not for the flowsheet as a system.** The analysis
  knows the specifications of the common unit operations; a type it does not catalogue is
  reported as such, never as missing something.
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
| `UNIT_UNCONNECTED` | A unit operation has nothing connected to it. |
| `UNIT_NO_FEED` | A unit operation has no feed, so it has nothing to process. |
| `UNIT_NO_PRODUCT` | A unit operation has no product, so its result has nowhere to go. |
| `FEED_NO_PRESSURE` | A boundary feed has no pressure. |
| `FEED_NO_TEMPERATURE` | A boundary feed has no temperature. |
| `FEED_NO_FLOW` | A boundary feed carries no flow. |
| `FEED_NO_COMPOSITION` | Every compound in a boundary feed is at zero. |
| `FEED_COMPOSITION_NOT_NORMALISED` | The mole fractions of a boundary feed do not sum to 1. |
| `VAPOR_FRACTION_OUT_OF_RANGE` | A feed is specified by a vapour fraction outside 0 to 1. |
| `SPEC_MISSING` | A unit operation lacks a specification its calculation mode needs. |
| `EFFICIENCY_OUT_OF_RANGE` | An efficiency is outside 0 to 100 %. |
| `SPLITTER_RATIOS_NOT_NORMALISED` | The split ratios of a splitter do not sum to 1. |
| `REACTOR_NO_REACTIONS` | A reactor has no active reactions to compute. |
| `RECYCLE_NO_ESTIMATE` | A recycle starts from a zero estimate. |
| `LOGICAL_TARGET_MISSING` | An adjust or specification does not name both the object it reads and the one it writes. |
| `SOLVER_EXCEPTION` | The solver raised an exception. |
| `INFINITE_LOOP` | The solver found a cycle with no recycle to tear it. |
| `NOT_CONVERGED` | A unit operation did not solve. |
| `STREAM_NOT_FINITE` | A stream carries a flow that is not a finite number. |
| `NEGATIVE_FLOW` | A stream carries a negative flow. |
| `STATE_NOT_PHYSICAL` | A solved stream has a temperature or pressure at or below zero. |
| `UNIT_HAD_NO_EFFECT` | A unit operation left its stream unchanged, so its specification is not being read. |
| `HEATER_COOLED` | A heater lowered the temperature of its stream. |
| `COOLER_HEATED` | A cooler raised the temperature of its stream. |
| `PRESSURE_WRONG_DIRECTION` | A pump, compressor, expander or valve moved the pressure the wrong way. |
| `HX_TEMPERATURE_CROSS` | A heat exchanger outlet crossed the temperature of the other side's inlet. |
| `HX_HEAT_FLOW_REVERSED` | A heat exchanger moved heat from the cold side to the hot side. |
| `TEMPERATURE_BELOW_FREEZING` | A liquid stream is below the melting point of its main compound. |
| `COLUMN_REFLUX_BELOW_MINIMUM` | A shortcut column runs below its minimum reflux ratio. |
| `MIXER_PRESSURE_MISMATCH` | The inlets of a mixer arrive at different pressures. |

Every code has a written explanation in `FindingExplanations`, rendered as the
[Diagnostic Codes](https://dwsim.org/tutorials/en/reference/diagnostics.html) page of the
tutorials site.

This table is generated from `FlowsheetCodes.All`, the same source the tools and the catalogue
read.

## Dynamic simulation

`DynamicsDiagnostics` does the same for a dynamic run — readiness before, post-mortem after —
and shares the `Finding` type. See [Dynamic Simulation](dynamics.md#diagnosis) and its own
[codes](../ai/diagnostics.md#dynamic-simulation-codes).

## Over MCP and HTTP

The same rules reach a language model as `dwsim_flowsheet_check` and `GET /api/flowsheet/check`.
See [Diagnostics for the AI Assistant](../ai/diagnostics.md).
