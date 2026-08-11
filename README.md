## DWSIM - Open Source Process Simulator
Copyright 2008-2026 Daniel Wagner Oliveira de Medeiros and contributors

DWSIM is a software for modeling, simulating, and optimizing steady-state and dynamic chemical processes.

### License

DWSIM is licensed under the GNU General Public License (GPL) Version 3.

See COPYING for more information.

### Supported Operating Systems

- Windows (x64/arm64)
- Linux (x64/arm64)
- macOS (x64/arm64)

## Building

```
git clone --recursive https://github.com/DanWBR/dwsim10
```

```
dotnet build DWSIM.slnx
```

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

### Donations

- Patreon: https://patreon.com/dwsim
- GitHub Sponsors: https://github.com/sponsors/DanWBR
- Buy-me-a-coffee: https://www.buymeacoffee.com/dwsim
- Bitcoin tips are welcome at bc1qf37y47vfk5wzxqpyh39y7th32x6lja0h0gc383