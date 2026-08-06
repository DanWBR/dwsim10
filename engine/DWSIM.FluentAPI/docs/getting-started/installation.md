# Installation

The Fluent API ships as a single .NET Framework 4.7.2 assembly,
`DWSIM.Automation.FluentAPI.dll`, built into the same DWSIM bin folder as the
rest of the simulator.

It does **not** allocate its own runtime — it depends on `DWSIM.exe` and the
co-located unit-operation / property-package DLLs to be present in the bin
folder. Plus components (refining, electrolyte, advanced HX, fired heater,
LCA, TEA, ThermoPack, Reaktoro) live in the `extenders/`, `unitops/` and
`ppacks/` subfolders and are auto-loaded at first use through
`Flowsheet.RegisterAssemblyResolver`.

## .NET Framework / .NET 4.x consumers

In Visual Studio (or via your `.csproj` / `.vbproj`):

1. Add a reference to `DWSIM.Automation.FluentAPI.dll` from the DWSIM bin
   folder (typically `…\DWSIM\bin\x64\Debug\` or `…\Release\`).
2. Set `Copy Local = False` if your output already runs from the DWSIM
   folder; otherwise mark every transitively-needed DWSIM DLL as `Copy Local`.
3. Make sure the runtime architecture matches DWSIM's (`x64`).

```xml
<Reference Include="DWSIM.Automation.FluentAPI">
  <HintPath>$(DWSIM_BIN)\DWSIM.Automation.FluentAPI.dll</HintPath>
  <Private>False</Private>
</Reference>
```

`using DWSIM.Automation.FluentAPI;` (C#) or
`Imports DWSIM.Automation.FluentAPI` (VB.NET) brings in `Flowsheet`,
`PropertyPackages`, `License`, the `Q` extension methods and the
`DWSIM.Automation.FluentAPI.Builders` namespace.

## Python (pythonnet)

```bash
pip install pythonnet
```

Point pythonnet at the DWSIM bin folder, then load the assembly:

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

Because pythonnet does not surface C# extension methods as instance methods,
the `Q.Kelvin(300.0)` / `Q.Bar(10.0)` / `Q.KgPerSecond(100.0)` static-helper
form is the right call from Python — see the [Python Guide](../python-guide.md).

## Verifying the install

```python
fs = Flowsheet.Create("ping").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables)
print("Available PPs:", list(fs.AvailablePropertyPackages))
print("External UOs :", list(fs.AvailableExternalUnitOperationNames))
```

If the call to `Flowsheet.Create` raises a `FileNotFoundException`, the
DWSIM bin folder is not on the resolver path — re-check `DWSIM_BIN` /
the project's `HintPath`.

## Plus components

`License.CheckLicense(yourKey)` unlocks every Plus surface (refining,
electrolyte, fired heater, LCA, TEA, ThermoPack, Reaktoro). See
[Patron Activation](patron-activation.md).
