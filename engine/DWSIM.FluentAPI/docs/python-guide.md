# Python Guide

`DWSIM.Automation.FluentAPI.dll` is a regular .NET assembly, so it works
under any pythonnet installation pointed at the DWSIM bin folder.

## Setup

```bash
pip install pythonnet
```

```python
import sys, clr, os

DWSIM_BIN = os.environ.get(
    "DWSIM_BIN",
    r"C:\Users\you\source\repos\DWSIM\bin\x64\Debug",
)
sys.path.append(DWSIM_BIN)
clr.AddReference("DWSIM.Automation.FluentAPI")

from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, License, Q
```

`clr.AddReference` is the equivalent of a `<Reference>` in a .csproj —
DWSIM is loaded the moment you first touch `Flowsheet`.

## Quantities — call `Q` statically

pythonnet does not surface C# extension methods as instance methods, so
`(300.0).Kelvin()` does not work. Use the static-helper form:

```python
T = Q.Kelvin(300.0)
P = Q.Bar(10.0)
m = Q.KgPerSecond(100.0)
```

The returned `Quantity` is identical to what C# / VB.NET produce. See the
full [unit table](api/quantities.md).

## Generic dictionaries

When a method takes `Dictionary<string, double>` (e.g.
`DefineConversionReaction` for stoichiometry), build it explicitly:

```python
from System.Collections.Generic import Dictionary
from System import String, Double

def stoich(d):
    out = Dictionary[String, Double]()
    for k, v in d.items():
        out[k] = float(v)
    return out

r1 = fs.DefineConversionReaction(
    "R1",
    stoich({"Methane": -1, "Water": -2, "Carbon dioxide": 1, "Hydrogen": 4}),
    "Methane", "Vapor", "50")
```

## Patron-key activation

```python
import os
from DWSIM.Automation.FluentAPI import License

key = os.environ["DWSIM_PATRON_KEY"]
if not License.CheckLicense(key):
    raise SystemExit("License.CheckLicense returned False.")
print(f"Activated, access level {License.AccessLevel}")
```

## Headless or live

- **Headless**: `Flowsheet.Create("name")` — fresh in-memory flowsheet.
  Best for batch / CI / unit tests.
- **Live**: `Flowsheet.Wrap(existingIFlowsheet)` — script the flowsheet of
  an open DWSIM editing session, an extender plugin or the AI-assistant
  host. The same `IFlowsheet` instance is reused.
- **From disk**: `Flowsheet.Load(path)`.

## IntelliSense / Pylance stubs

The DWSIM build script can generate `.pyi` stub files from the assembly's
XML doc comments (`DWSIM.Automation.FluentAPI.xml`). See
`python/intellisense/README.md` in the source tree. With those stubs on
your `PYTHONPATH`, Pylance / PyCharm will surface every method, parameter
and XML doc comment — making the Fluent API as discoverable from Python
as it is from C#.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `FileNotFoundException` on `Flowsheet.Create` | DWSIM bin folder not on `sys.path`. |
| `TypeError: 'method-wrapper' object is not callable` on `.Kelvin()` | Used the extension-method form; switch to `Q.Kelvin(...)`. |
| `KeyNotFoundException` on `AddExternalUnitOperation` | Plus DLL not present in `unitops/`. |
| `InvalidOperationException("requires an active Patron key")` | Call `License.CheckLicense(...)` before any Plus surface. |
| Solver throws but the flowsheet looks correct | Use `fs.TrySolve()` to inspect the per-UO error list. |
