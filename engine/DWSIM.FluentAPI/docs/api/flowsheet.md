# Flowsheet

Root of the Fluent API. Wraps an `IFlowsheet` and exposes builder methods for
compounds, property packages, streams, unit operations, reactions, the solver,
and the Plus assessment kits (LCA / TEA).

## Construction

| Method | Returns | Notes |
|---|---|---|
| `Flowsheet.Create(name = null)` | `Flowsheet` | New headless flowsheet. Auto-installs the assembly resolver. |
| `Flowsheet.Wrap(IFlowsheet)` | `Flowsheet` | Wraps an existing in-memory flowsheet (open DWSIM session, extender host, AI assistant). |
| `Flowsheet.Load(filepath)` | `Flowsheet` | Loads `.dwxml` / `.dwxmz`. |
| `fs.Save(path, compressed = true)` | `Flowsheet` | Saves the flowsheet (compressed `.dwxmz` by default). |
| `Flowsheet.RegisterAssemblyResolver()` | `void` | Manual resolver bootstrap (rarely needed — `Create` calls it). |

## Compounds and property packages

```csharp
fs.WithCompound("Water")
  .WithCompounds("Methane", "Ethane", "Propane")
  .WithPropertyPackage(PropertyPackages.PengRobinson);

IReadOnlyList<string> available = fs.AvailablePropertyPackages;
```

`WithPropertyPackage` calls `License.RequirePlus()` automatically when
asked for a `PropertyPackages.Plus.*` name. To configure the package after
instantiation, see [Property Packages](property-packages.md).

## Streams

| Method | Returns |
|---|---|
| `AddMaterialStream(tag)` | `MaterialStreamBuilder` |
| `AddEnergyStream(tag)` | `EnergyStreamBuilder` |
| `MaterialStream(tag)` | `MaterialStreamBuilder` (look up by tag) |
| `EnergyStream(tag)` | `EnergyStreamBuilder` (look up by tag) |

## Unit operations — typed `AddX` methods

| Category | Methods |
|---|---|
| Core | `AddMixer`, `AddSplitter`, `AddHeater`, `AddCooler`, `AddPump`, `AddCompressor`, `AddExpander`, `AddValve`, `AddPipe`, `AddHeatExchanger`, `AddComponentSeparator`, `AddTank`, `AddSeparator`, `AddOrificePlate`, `AddFilter`, `AddSolidsSeparator` |
| Columns | `AddShortcutColumn`, `AddDistillationColumn`, `AddAbsorptionColumn` |
| Reactors | `AddConversionReactor`, `AddEquilibriumReactor`, `AddGibbsReactor`, `AddCSTR`, `AddPFR`, `AddReaktoroGibbsReactor` |
| Bioprocess (free) | `AddBioReactor`, `AddAnaerobicDigester`, `AddCFBFastPyrolysisReactor`, `AddPretreatmentReactor`, `AddBiogasUpgrader`, `AddCellLysis`, `AddCentrifuge`, `AddChromatographyColumn`, `AddCrossflowUF`, `AddCrystallizer` |
| Refining (Plus) | `AddAlkylation`, `AddAmineTreater`, `AddBlender`, `AddClausSRU`, `AddCoker`, `AddFCC`, `AddHydrocracker`, `AddHDS`, `AddIsomerization`, `AddReformer`, `AddShortcutCDU` |
| Electrolyte (Plus) | `AddIonExchange`, `AddNeutralizationReactor`, `AddPrecipitationReactor`, `AddReverseOsmosis` |
| Clean energy | `AddWindTurbine`, `AddHydroelectricTurbine`, `AddSolarPanel`, `AddWaterElectrolyzer`, `AddPEMFuelCell` |
| Plus advanced | `AddAdvancedHeatExchanger`, `AddFiredHeater`, `AddRestrictionOrifice`, `AddPipeNetwork`, `AddVaporCompressionChiller`, `AddZeoliteAdsorber`, `AddCopperBedMercuryAdsorber` |
| ExtensionPack (Plus) | `AddAirCooler2`, `AddEnergyMixer`, `AddEnergySplitter`, `AddEnergyStreamSwitch`, `AddMaterialStreamSwitch`, `AddMaterialStreamMapper`, `AddFallingFilmEvaporator`, `AddThermoPropertyEditor` |

Every typed method returns the dedicated builder (`HeaterBuilder`,
`FCCBuilder`, …) — see [Unit Operations](unit-operations/index.md).

## Generic escape hatch

```csharp
// Any ObjectType the typed surface doesn't cover yet:
fs.AddUnitOperation(ObjectType.RefluxedAbsorber, "T-201");

// Any IExternalUnitOperation by display name:
fs.AddExternalUnitOperation("Anaerobic Digester", "AD-1");

IReadOnlyList<string> external = fs.AvailableExternalUnitOperationNames;
```

`ExternalCatalog.Bioprocess`, `.Refining`, `.Electrolyte`, `.Plus` and
`.Misc` provide the canonical display-name constants — see
[External Catalog](external-catalog.md).

## Reactions

```csharp
var r1 = fs.DefineConversionReaction(
    "R1",
    new Dictionary<string, double> { ["Methane"] = -1, ["Water"] = -2,
                                     ["Carbon dioxide"] = 1, ["Hydrogen"] = 4 },
    baseCompound: "Methane",
    phase: "Vapor",
    conversionExpression: "50");

fs.ReactionSet("DefaultSet").Add(r1);
```

Other factories: `DefineEquilibriumReaction`, `DefineKineticReaction`,
`DefineHetCatReaction`. See [Reactions](reactions.md).

## Solver

```csharp
fs.Solve();                              // throws FlowsheetSolveException on error
var errors = fs.TrySolve();              // returns IReadOnlyList<Exception>

// dynamic (time-domain) integration
var result = fs.RunDynamics("Default Schedule").Execute();
var resultAsync = await fs.RunDynamics("Default Schedule").ExecuteAsync();
```

`Solve` chooses between `Automation3.CalculateFlowsheet4` (for headless
`Flowsheet2` instances) and the generic `FlowsheetSolver` so live editor
hosts and extender plugins also get correctly routed. See [Solver](solver.md)
and [Dynamics](dynamics.md).

## Layout

```csharp
fs.AutoLayout();   // re-runs DWSIM's auto-layout pass
```

Stream / UO placement otherwise advances on a 80-pixel grid set by the
internal cursor each time you add an object.

## LCA & TEA (Plus)

```csharp
var lca = fs.RunLCA();
var lca2 = fs.RunLCA(new LCASettings { ProductStreamName = "outlet",
                                       OperatingHoursPerYear = 8000 });
var tea = fs.RunTEA(new TEASettings { ProductSellingPricePerKg = 1.5 });
```

See [LCA & TEA](lca-tea.md).

## Underlying object

`fs.Inner` returns the wrapped `IFlowsheet`. Use it only when the fluent
surface does not cover a particular need.
