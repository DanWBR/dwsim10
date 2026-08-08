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
| `tools/` | Programs that are not the application: the MCP server, and build-time helpers |
| `docs/` | Notes that outlive a commit message |

Sixty-three projects. Every assembly is versioned `10.2.0.0` from `Directory.Build.props`;
third-party source under `external/` keeps the version its author gave it.

## Testing

```
dotnet test DWSIM.slnx
```

Seven suites, two hundred and eleven tests: the linear programming solver, the settings file,
the analytical thermodynamic derivatives against numerical ones, the managed IPOPT solver, its
linear algebra and its façade, the fluent API, and the engine smoke tests, which load the
compound databases, register the property packages, and load and solve the fourteen sample
flowsheets under `tests/flowsheets`.

Seven of those fourteen samples do not solve. They do not solve on the .NET Framework build of
the engine either, with the same object reporting the same message, so they are pinned as such:
four are columns that miss the tolerance, one is a column that breaks its own mass balance, and
two carry scripts written for Python 2.

## The MCP server

`tools/DWSIM.MCPServer` exposes the flowsheet, the streams, the unit operations, the
thermodynamics and the solver as twenty-seven Model Context Protocol tools, over stdio or over
HTTP with server-sent events, so an assistant can build and solve a simulation.

```
dotnet run --project tools/DWSIM.MCPServer -- --stdio
```

`--http --port 5000` serves the same tools over SSE instead. It sits on the automation and fluent
APIs and on nothing else.

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

IPOPT is managed code here. `engine/DWSIM.Numerics.Ipopt.Sparse` holds the linear algebra
(QDLDL, Bunch-Kaufman, dense Cholesky), `engine/DWSIM.Numerics.Ipopt.Core` the primal-dual
interior-point solver (limited-memory BFGS, adaptive mu, Ipopt-format iteration log), and
`engine/DWSIM.Numerics.Ipopt` presents them under the `Cureos.Numerics` shape the engine was
written against, so no call site changed.

The bound-constrained case, which is seven of the engine's eight call sites, agrees with the
native library: over five thousand problems shaped like the engine's regressions the objective
matches to a relative 1e-6 in 94 per cent of them and never differs by more than 9.1e-5, below
the tolerance both are asked for, and the Gibbs reactor of the sample flowsheet reaches the same
minimum to 2.7e-7. The eighth call site, the Gibbs three-phase flash, poses `m = n + 1`
constraints; the constrained path reaches the native vapour fraction of 0.42217598 on it, and is
right on independent benchmarks too, including Hock-Schittkowski 71 to six digits.
[docs/ipopt-contract.md](docs/ipopt-contract.md) records the surface and the measurements.

The two other Windows-only native libraries are gone rather than missing: `lpsolve55`, which
seeded the Gibbs reactor, was replaced by a managed two-phase simplex validated against it over
twenty thousand element-balance problems, and `steam67` had no caller.

CoolProp is the one native library left, and it now ships for all six runtimes. It is bound to
its flat C API rather than through the SWIG-generated C# wrapper, which needed a build made with
`-DCOOLPROP_CSHARP_MODULE=ON` and so existed for x64 Windows alone.

Extensions written against DWSIM 9 need a few edits; [BREAKING.md](BREAKING.md) lists them.

## Licence

GNU General Public License version 3 or later. See [LICENSE](LICENSE).

The third-party components carry their own licences, listed in the About box of the application
and in [lib/README.md](lib/README.md).
