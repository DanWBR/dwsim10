# The Reaktoro contract

What DWSIM asks Reaktoro for, how it asks now, and how it used to ask. Written down because the two
sides live in different repositories: the C API is in
[DanWBR/reaktoro](https://github.com/DanWBR/reaktoro) under `ReaktoroC/`, and changing it means
changing `engine/DWSIM.Thermodynamics.ReaktoroPropertyPackage/Reaktoro.vb` in step.

## What the engine actually needs

Four call sites, and between them a surface small enough to fit on a page:

| Call site | What it wants |
|---|---|
| `ReaktoroFlash.Flash_PT` | equilibrium at T and P; species amounts, activity coefficients, amount per phase |
| `ReaktoroFlash.Flash_PV` (the bubble-temperature loop) | the same, once per temperature it tries |
| `ActivityCoefficients.Calculate` | activity coefficients at a composition that is given, not solved for |
| `Reactor_ReaktoroGibbs` | equilibrium over phases built from a list of elements, plus a listing of a database's species |

Nothing asks for kinetics, transport, surfaces, or sensitivity derivatives.

## The C surface

```c
int  reaktoro_version(char* buffer, int size);
int  reaktoro_last_error(char* buffer, int size);

ReaktoroSystem* reaktoro_create(const char* database, const char* aqueous_species,
                                const char* gaseous_species, const char* gaseous_model);

ReaktoroSystem* reaktoro_create_speciated(const char* database_kind, const char* database,
                                          const char* elements, int aqueous, int gaseous,
                                          int liquid, int mineral, const char* gaseous_model);

void reaktoro_destroy(ReaktoroSystem* system);

int  reaktoro_species_count(const ReaktoroSystem* system);
int  reaktoro_species_names(const ReaktoroSystem* system, char* buffer, int size);

int  reaktoro_equilibrate(ReaktoroSystem* system, double T, double P,
                          const char* substances, const double* amounts, int amounts_size,
                          double* species_amounts, double* ln_activity_coefficients,
                          double* aqueous_amount, double* gaseous_amount);

int  reaktoro_properties(ReaktoroSystem* system, double T, double P,
                         const double* species_amounts, int species_amounts_size,
                         double* ln_activity_coefficients);

int  reaktoro_database_species(const char* database_kind, const char* database,
                               char* buffer, int size);
```

Conventions, all of them deliberate and none of them negotiable from one side alone:

- **Nothing throws across the boundary.** Every entry point catches, returns a value that says it
  failed, and leaves the reason where `reaktoro_last_error` reads it. The error string is per
  thread, because the flowsheet solver calls this from whatever thread it happens to be on.
- **The caller owns every buffer** except the system handle, which `reaktoro_destroy` releases. A
  string function writes into the buffer and returns the length it needs, terminator aside, so a
  caller that guessed too small can size one and ask again.
- **Species order is the system's order**, the one `reaktoro_species_names` reports, and every array
  in and out of an equilibrium has `reaktoro_species_count` entries in it.
- **Lists of names** are separated by spaces, tabs or semicolons, whichever the caller finds easier
  to build.

## Reaktoro 1 to Reaktoro 2

The engine used to reach version 1 through its Python package. Version 2 removed the classes that
path was written against, which is why staying on version 1 was for a long time the price of
staying on Python at all.

| Reaktoro 1, through Python | Reaktoro 2, in C++ |
|---|---|
| `Database("supcrt07-organics.xml")` | `SupcrtDatabase("supcrt07-organics")`, embedded in the library |
| `ChemicalEditor(db).addAqueousPhase(names)` | `AqueousPhase(StringList(names))` |
| `.setChemicalModelHKF()` + `.setActivityModelDrummondCO2()` | `.set(chain(ActivityModelHKF(), ActivityModelDrummond("CO2")))` |
| `.addAqueousPhaseWithElements(elements)` | `AqueousPhase(speciate(elements))` |
| `.addGaseousPhase(names)` | `GaseousPhase(StringList(names))`, ideal gas unless told otherwise |
| `ChemicalSystem(editor)` | `ChemicalSystem(Phases(db))` |
| `EquilibriumProblem(system)` + `add(formula, n, "mol")` | `Material(system).add(formula, n, "mol")` |
| `equilibrate(problem)` | `material.equilibrate(T, "K", P, "Pa")` |
| `ChemicalProperties(system).update(T, P, n)` | `ChemicalState` + `ChemicalProps` |
| `state.phaseAmount("Aqueous")` | `props.phaseProps(index).amount()` |
| `state.speciesAmounts()` | unchanged |
| `properties.lnActivityCoefficients().val` | `props.speciesActivityCoefficientsLn()` |

Two differences reach the user rather than the code:

- **An external database has to be YAML or JSON.** Version 1's XML is not readable. The databases the
  library carries are all still there, and a `DatabaseName` stored with a file extension has it
  dropped when the database is opened.
- **Doubly charged ions changed spelling**: `SO4--` became `SO4-2`, `CO3--` became `CO3-2`. The
  compound map ships corrected, and a test fails if one drifts back.

## The runtime

`.github/workflows/runtimes.yml` in the fork builds the C API and walks its dependency closure -
Reaktoro pulls in Optima, phreeqc4rkt, ThermoFun, yaml-cpp, spdlog and the rest, all shared - then
strips them, repoints their RPATHs at the folder they are in, loads the result with the build
environment out of the picture, and solves one equilibrium through it before publishing. A folder
that cannot answer never reaches a release.

Five runtime identifiers: `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. Not
`win-arm64`: conda-forge, where the dependencies come from, does not build for it, so getting that
one means building the whole chain from source.

The archives are around fifty megabytes each, so they are downloaded at build time from a release
pinned by `ReaktoroRuntimeTag` rather than carried in this repository, and cached under
`engine/DWSIM.Thermodynamics.ReaktoroPropertyPackage/native/`. Moving to a new Reaktoro means
publishing a new release in the fork and changing that one property.
