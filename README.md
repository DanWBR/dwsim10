# DWSIM

Open source chemical process simulator: the calculation engine and a desktop application, on
.NET 10, for Windows, Linux and macOS, on x64 and arm64.

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
| `packaging/` | The scripts that turn a publish into a `.deb`, an `.app` and a DMG |
| `tools/` | Build-time helpers |
| `docs/` | Notes that outlive a commit message |

Sixty-two projects. Every assembly is versioned `10.2.0.0` from `Directory.Build.props`;
third-party source under `external/` keeps the version its author gave it.

## Testing

```
dotnet test DWSIM.slnx
```

Seven suites, a hundred and fifty tests: the linear programming solver, the settings file, the
analytical thermodynamic derivatives against numerical ones, the managed IPOPT solver and its
linear algebra, the fluent API, and the engine smoke tests, which load the compound databases,
register the property packages, and load and solve the fourteen sample flowsheets under
`tests/flowsheets`.

Seven of those fourteen samples do not solve. They do not solve on the .NET Framework build of
the engine either, with the same object reporting the same message, so they are pinned as such:
four are columns that miss the tolerance, one is a column that breaks its own mass balance, and
two carry scripts written for Python 2.

## Releases

`.github/workflows/release.yml` publishes self-contained builds for `win-x64`, `win-arm64`,
`linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`, around 220 MB each, and packages them as
a zip, a `.tar.gz`, a `.deb` and a signed and notarized DMG. The Apple signing steps are
conditional on the secrets being present: without them the DMG is still built, unsigned, and the
log says so. [packaging/README.md](packaging/README.md) lists the secrets to create.

## What this repository is not

This is the open source edition. The AI assistant, the convergence enhancer, and the unit
operations and property packages of the Patreon edition (refining, electrolyte operations, the
extension pack, life cycle assessment, techno-economic analysis) live elsewhere. Their entry
points in the fluent API are still here and say so when called.

One numerical back-end is still not wired in: IPOPT, which the Gibbs energy minimisation and the
binary interaction parameter regression use. `engine/DWSIM.Numerics.Ipopt` is the façade the
engine calls and it still throws, but the solver behind it now exists in managed code:
`DWSIM.Numerics.Ipopt.Sparse` (QDLDL, Bunch-Kaufman, Cholesky) and `DWSIM.Numerics.Ipopt.Core`
(primal-dual interior point, limited-memory BFGS, adaptive mu), 58 tests. It covers the
bound-constrained case, which is seven of the engine's eight call sites; the eighth, the Gibbs
three-phase flash, poses `m = n + 1` equality constraints and needs the constrained path first.
[docs/ipopt-contract.md](docs/ipopt-contract.md) records the surface the façade has to present.

The two other Windows-only native libraries are gone rather than missing: `lpsolve55`, which
seeded the Gibbs reactor, was replaced by a managed two-phase simplex validated against it over
twenty thousand element-balance problems, and `steam67` had no caller.

Extensions written against DWSIM 9 need a few edits; [BREAKING.md](BREAKING.md) lists them.

## Licence

GNU General Public License version 3 or later. See [LICENSE](LICENSE).

The third-party components carry their own licences, listed in the About box of the application
and in [lib/README.md](lib/README.md).
