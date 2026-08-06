# DWSIM

Open source chemical process simulator: the calculation engine and a desktop application, on
.NET 10, for Windows, Linux and macOS.

DWSIM models steady-state and dynamic processes. It ships around thirty property packages, from
the cubic equations of state and the activity coefficient models to the steam tables, the
electrolyte models and CoolProp; a library of unit operations that covers separators, heat
exchangers, reactors, rigorous and shortcut columns, pipes, bioprocess and clean energy
equipment; and rigorous flash algorithms for vapour, liquid and solid phases. Flowsheets are
read and written in DWSIM's own `.dwxml` / `.dwxmz` format, the same one the Windows edition
uses, so a simulation moves between the two without conversion.

## Building

```
git clone --recursive https://github.com/DanWBR/DWSIMCore
```

```
dotnet build DWSIM.slnx
```

The `--recursive` matters: the spreadsheet is [ReoGrid](https://github.com/DanWBR/ReoGrid), a
submodule under `external/`. In a tree that is already cloned, `git submodule update --init`
does the same job.

```
dotnet run --project ui/DWSIM.UI.Desktop.Avalonia
```

A flowsheet path on the command line opens as the first document.

## Layout

| Folder | What is in it |
|---|---|
| `engine/` | The simulator: interfaces, thermodynamics, unit operations, the flowsheet and its solver, the automation and fluent APIs |
| `ui/` | The desktop application, written in Avalonia |
| `external/` | Third-party source, as submodules |
| `lib/` | Managed assemblies that are not on NuGet, with a note on each |
| `tests/` | The test suites and the sample flowsheets they run |
| `tools/` | Build-time helpers |

## Testing

```
dotnet test DWSIM.slnx
```

The suites load the compound databases, check the analytical thermodynamic derivatives against
numerical ones, build flowsheets through the fluent API, and load and solve the fourteen sample
flowsheets under `tests/flowsheets`.

Seven of those fourteen samples do not solve. They do not solve on the .NET Framework build of
the engine either, with the same object reporting the same message, so they are pinned as such:
four are columns that miss the tolerance, one is a column that breaks its own mass balance, and
two carry scripts written for Python 2.

## What this repository is not

This is the open source edition. The AI assistant, the convergence enhancer, and the unit
operations and property packages of the Patreon edition (refining, electrolyte operations, the
extension pack, life cycle assessment, techno-economic analysis) live elsewhere. Their entry
points in the fluent API are still here and say so when called.

Some numerical back-ends are Windows-only native libraries and have no binary in this
repository: IPOPT, which the Gibbs energy minimisation and the binary interaction parameter
regression use; `lpsolve55`, which seeds the Gibbs reactor; and `steam67`. Everything that
reaches them fails with a message that says which one is missing.

Extensions written against DWSIM 9 need a few edits; [BREAKING.md](BREAKING.md) lists them.

## Licence

GNU General Public License version 3 or later. See [LICENSE](LICENSE).

The third-party components carry their own licences, listed in the About box of the application
and in [lib/README.md](lib/README.md).
