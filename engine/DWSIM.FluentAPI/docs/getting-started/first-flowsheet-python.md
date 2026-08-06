# First Flowsheet — Python

The same mixer as the [C# guide](first-flowsheet-csharp.md), driven from
Python via pythonnet.

```python
import sys, clr, os

DWSIM_BIN = os.environ.get(
    "DWSIM_BIN",
    r"C:\Users\you\source\repos\DWSIM\bin\x64\Debug",
)
sys.path.append(DWSIM_BIN)
clr.AddReference("DWSIM.Automation.FluentAPI")

from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

fs = (Flowsheet.Create("PyMixer")
      .WithCompound("Water")
      .WithPropertyPackage(PropertyPackages.SteamTables))

inlet1 = (fs.AddMaterialStream("inlet1")
          .At(Q.Kelvin(300.0), Q.Pascal(101325.0))
          .WithMassFlow(Q.KgPerSecond(100.0)))

inlet2 = (fs.AddMaterialStream("inlet2")
          .At(Q.Kelvin(348.0), Q.Pascal(101325.0))
          .WithMassFlow(Q.KgPerSecond(50.0)))

outlet = fs.AddMaterialStream("outlet")

(fs.AddMixer("MIX-1")
   .ConnectFeed(inlet1, 0)
   .ConnectFeed(inlet2, 1)
   .ConnectProduct(outlet, 0))

fs.AutoLayout()
fs.Solve()

print(f"Outlet T  = {outlet.TemperatureK:.4f} K")
print(f"Mass flow = {outlet.MassFlowKgPerSecond:.4f} kg/s")
```

## What changes vs C#

- **Quantities are static-helper calls.** pythonnet does not surface C#
  extension methods as instance methods, so you write `Q.Kelvin(300.0)` —
  not `(300.0).Kelvin()`. The returned `Quantity` is identical.
- **Imports.** `from DWSIM.Automation.FluentAPI import Flowsheet,
  PropertyPackages, Q` is enough for the basics; pull `License` for patron
  unlocking, and `import` from `DWSIM.Automation.FluentAPI.Builders` only
  when you need a builder type by name.
- **Generic dictionaries.** When a method takes `Dictionary<string, double>`
  (e.g. `DefineConversionReaction` for stoichiometry), build it via
  `from System.Collections.Generic import Dictionary` and
  `Dictionary[String, Double]()`. See
  [02 — Conversion Reactor](../examples/02-conversion-reactor.md).
- **No assembly-startup work needed.** `clr.AddReference` is the equivalent
  of a `<Reference>` in the .csproj — DWSIM is loaded the moment you first
  touch `Flowsheet`.

## Iterating from a notebook

The Fluent API is safe to drive from a Jupyter notebook. Re-running cells
that allocate a fresh `Flowsheet` is fine; pythonnet keeps the loaded
assembly alive across cell executions, so the second `Flowsheet.Create`
reuses everything.

For long-lived hosts (a running DWSIM session), prefer
[`Flowsheet.Wrap`](../examples/08-wrap-existing-flowsheet.md) to script
the live document instead of allocating a new headless flowsheet.
