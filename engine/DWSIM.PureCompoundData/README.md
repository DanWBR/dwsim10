# DWSIM.PureCompoundData

Online retrieval and assembly of pure-compound thermophysical constant properties
for DWSIM simulations.

Sibling to [`DWSIM.PhaseEquilibriumData`](../DWSIM.PhaseEquilibriumData/). Where the
phase-equilibrium tool retrieves mixture VLE/LLE datasets, this project retrieves
**single-compound** property data (vapor pressure, liquid density, viscosity, thermal
conductivity, ideal-gas Cp, surface tension, heat of vaporisation, critical constants,
formation energetics, …) and produces a fully-populated `ConstantProperties` object
that DWSIM can drop into a simulation.

## Phase 1 (this MVP)

Vertical slice, ThermoML-only, no extraction of shared core from phaseq yet.

- **Sources:** NIST ThermoML archive (pure-compound subset). Reuses the tar/parser
  plumbing from `DWSIM.PhaseEquilibriumData.Sources`.
- **Cache:** LiteDB, same folder conventions as phaseq
  (`%LOCALAPPDATA%/DWSIM/PureCompound` on Windows; XDG on Unix).
- **Estimation:** Joback groups, Lee-Kesler ω, Rackett ρ_L, Antoine / DIPPR 101
  curve fitting. Fills any gap the sources don't provide.
- **Builder:** Merges records across sources, fits curves, runs estimator DAG,
  emits a `PureCompoundResult` POCO with a per-field provenance map. The DWSIM
  UI layer (VB.NET) converts this into `BaseClasses.ConstantProperties`.
- **CLI:** `dwsim-purecompound download | ingest | search | show | build | stats`.

## Later phases

- NIST WebBook HTTP adapter (public online source).
- DDBST public-pages adapter.
- DIPPR local-bundle adapter (path-configurable; user provides licensed files).
- Extract shared `DWSIM.ThermoData.Core` + `DWSIM.ThermoData.Sources.ThermoML`
  from phaseq (refactor after this MVP stabilises).
- More estimators (Ambrose, Riedel, Brock-Bird, Latini, Letsou-Stiel, Chung).

## Layout

```
src/
├── Core/         domain records, IDataSource<TQuery, TRecord>, deterministic JSON
├── Sources/      ThermoML pure-subset adapter
├── Index/        LiteDB schema, fluent PureCompoundQuery, Ingestor, QueryExecutor
├── Estimation/   correlations + curve fitting
├── Builder/      ConstantPropertiesBuilder pipeline (merge → fit → estimate → emit)
└── CLI/          dwsim-purecompound console app
tests/Tests/      MSTest fixtures
cache/            download / ingest artifacts (git-ignored)
```
