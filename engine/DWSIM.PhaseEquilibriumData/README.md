# DWSIM Phase Equilibrium Data Library — Phase 1 (MVP)

Local ThermoML archive indexer with LiteDB and `dwsim-phaseq` CLI.

- **Target:** .NET Framework 4.7.2 (SDK-style), built with MSBuild.
- **Backend:** LiteDB 5.0.21 (same version DWSIM itself uses).
- **Solution:** `DWSIM.PhaseEquilibriumData.sln` (sub-solution). Also registered in the main `DWSIM.sln`.

## Projects

| Project | Purpose |
|---|---|
| `src/Core`    | Record hierarchy (`PhaseEquilibriumDataset`, `Compound`, `Constraint`, …) + deterministic `JsonSerializerOptions`. |
| `src/Sources` | ThermoML parser, classifier, `TarGzReader`, deterministic `IdHasher`. |
| `src/Index`   | LiteDB schema, ingestor (batched, idempotent), query executor, statistics. |
| `src/CLI`     | `dwsim-phaseq` executable: `download`, `ingest`, `search`, `show`, `stats`. |
| `tests/Tests` | MSTest fixtures + parser + index tests (17 tests). |

## Build

```
msbuild /t:Restore;Build DWSIM.PhaseEquilibriumData.sln
dotnet test DWSIM.PhaseEquilibriumData.sln
```

## CLI Commands

Defaults: archive and LiteDB file live in `%LOCALAPPDATA%/DWSIM/PhaseEq` on Windows,
`$XDG_DATA_HOME/DWSIM/PhaseEq` (or `~/.local/share/DWSIM/PhaseEq`) elsewhere.

### `download` — fetch the NIST ThermoML bulk archive

```
dwsim-phaseq download
dwsim-phaseq download --url https://data.nist.gov/od/ds/mds2-2422/ThermoML.v2020-09-30.tgz --dest ./cache/ThermoML.tgz
```

### `ingest` — parse the archive and populate the LiteDB index

```
dwsim-phaseq ingest
dwsim-phaseq ingest --archive ./cache/ThermoML.tgz --db ./cache/phaseq.litedb
```

Idempotent — re-running the same archive inserts zero new rows.

### `search` — binary lookup by CAS pair

```
dwsim-phaseq search --cas1 64-17-5 --cas2 7732-18-5 --type VLE_Isobaric --limit 5
dwsim-phaseq search --cas1 64-17-5 --cas2 7732-18-5 --format json --tmin 300 --tmax 400
```

Filters (all optional): `--type`, `--tmin/--tmax` (K), `--pmin/--pmax` (kPa), `--format table|json|csv`, `--limit N`.
Deterministic ordering (`ORDER BY id ASC`) — identical invocations return identical results.

### `show` — one dataset by id

```
dwsim-phaseq show --id <hex-sha256> --format csv
```

### `stats` — summary of the index

```
dwsim-phaseq stats
```

Reports total datasets, unique compounds, DB file size, and a breakdown by `EquilibriumType`.

## NIST ThermoML JSON archive — chemical identifiers

The current NIST bulk archive (`mds2-2422`) ships each entry as JSON, not XML. The JSON schema omits CAS registry numbers — compounds are identified by `sStandardInChIKey` (27-char InChIKey). The parser stores the InChIKey in the `CasNumber` field so existing `--cas1 / --cas2` search plumbing works without a schema change. Pass InChIKeys to `search`:

```
dwsim-phaseq search --cas1 LFQSCWFLJHTTHZ-UHFFFAOYSA-N --cas2 XLYOFNOQVPJJNP-UHFFFAOYSA-N
```

XML ThermoML archives (pre-2020) still use real CAS numbers via the XML parser path.

## Exit Codes

`0` success · `1` user error · `2` data error · `3` network error.

## Scope

Phase 1 (MVP) only. Out of scope: consistency tests (Phase 2), public fluent API (Phase 2), NuGet packaging (Phase 2), DWSIM GUI integration (Phase 3), non-ThermoML sources (Phase 4).
