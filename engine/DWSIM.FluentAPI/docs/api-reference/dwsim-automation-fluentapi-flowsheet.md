# Flowsheet

`DWSIM.Automation.FluentAPI.Flowsheet`

Root of the Fluent API. Wraps an `DWSIM.Interfaces.IFlowsheet` and exposes builder methods for compounds, property packages, streams, unit operations, reactions, and the solver.

## Methods

### `AddAbsorptionColumn(string)`

Adds a Absorption Column unit operation tagged `tag` and returns its fluent builder.

### `AddAdvancedHeatExchanger(string)`

Adds a Advanced Heat Exchanger unit operation tagged `tag` and returns its fluent builder.

### `AddAirCooler2(string)`

Adds a Air Cooler2 unit operation tagged `tag` and returns its fluent builder.

### `AddAlkylation(string)`

Adds a Alkylation unit operation tagged `tag` and returns its fluent builder.

### `AddAmineTreater(string)`

Adds a Amine Treater unit operation tagged `tag` and returns its fluent builder.

### `AddAnaerobicDigester(string)`

Adds a Anaerobic Digester unit operation tagged `tag` and returns its fluent builder.

### `AddBiogasUpgrader(string)`

Adds a Biogas Upgrader unit operation tagged `tag` and returns its fluent builder.

### `AddBioReactor(string)`

Adds a Bio Reactor unit operation tagged `tag` and returns its fluent builder.

### `AddBlender(string)`

Adds a Blender unit operation tagged `tag` and returns its fluent builder.

### `AddCellLysis(string)`

Adds a Cell Lysis unit operation tagged `tag` and returns its fluent builder.

### `AddCentrifuge(string)`

Adds a Centrifuge unit operation tagged `tag` and returns its fluent builder.

### `AddCFBFastPyrolysisReactor(string)`

Adds a CFBFast Pyrolysis Reactor unit operation tagged `tag` and returns its fluent builder.

### `AddChromatographyColumn(string)`

Adds a Chromatography Column unit operation tagged `tag` and returns its fluent builder.

### `AddClausSRU(string)`

Adds a Claus SRU unit operation tagged `tag` and returns its fluent builder.

### `AddCoker(string)`

Adds a Coker unit operation tagged `tag` and returns its fluent builder.

### `AddComponentSeparator(string)`

Adds a Component Separator unit operation tagged `tag` and returns its fluent builder.

### `AddCompressor(string)`

Adds a Compressor unit operation tagged `tag` and returns its fluent builder.

### `AddConversionReactor(string)`

Adds a Conversion Reactor unit operation tagged `tag` and returns its fluent builder.

### `AddCooler(string)`

Adds a Cooler unit operation tagged `tag` and returns its fluent builder.

### `AddCopperBedMercuryAdsorber(string)`

Adds a Copper Bed Mercury Adsorber unit operation tagged `tag` and returns its fluent builder.

### `AddCrossflowUF(string)`

Adds a Crossflow UF unit operation tagged `tag` and returns its fluent builder.

### `AddCrystallizer(string)`

Adds a Crystallizer unit operation tagged `tag` and returns its fluent builder.

### `AddCSTR(string)`

Adds a CSTR unit operation tagged `tag` and returns its fluent builder.

### `AddDistillationColumn(string)`

Adds a Distillation Column unit operation tagged `tag` and returns its fluent builder.

### `AddEnergyMixer(string)`

Adds a Energy Mixer unit operation tagged `tag` and returns its fluent builder.

### `AddEnergySplitter(string)`

Adds a Energy Splitter unit operation tagged `tag` and returns its fluent builder.

### `AddEnergyStream(string)`

Creates a new [`EnergyStream`](dwsim-automation-fluentapi-flowsheet.md) tagged `tag` and returns its fluent builder.

### `AddEnergyStreamSwitch(string)`

Adds a Energy Stream Switch unit operation tagged `tag` and returns its fluent builder.

### `AddEquilibriumReactor(string)`

Adds a Equilibrium Reactor unit operation tagged `tag` and returns its fluent builder.

### `AddExpander(string)`

Adds a Expander unit operation tagged `tag` and returns its fluent builder.

### `AddExternalToSurface(int, int, string, DWSIM.Interfaces.IExternalUnitOperation)`

Routes `AddObjectToSurface(External, ..., uoobj)` to whichever flavour the wrapped `DWSIM.Interfaces.IFlowsheet` exposes. Reflection avoids a hard reference on DWSIM.exe (which would drag the WinForms editor into headless consumers) while still supporting the classic `FormFlowsheet` path.

### `AddExternalUnitOperation(string, string)`

Adds an external unit operation (bioprocess, refining, advanced heat exchanger, fired heater, pipe network, etc.) by its `GetDisplayName` string. The flowsheet's `AvailableSimulationObjects` registry is searched for a template whose display name matches; that template's `IExternalUnitOperation.ReturnInstance` is called to create a fresh instance, which is then placed on the surface. Plus / DWSIMPlus components (refining, advanced HX, fired heater, etc.) require an active patron key — call [`Activate`](dwsim-automation-fluentapi-license.md) first or [`RequiresPlus`](dwsim-automation-fluentapi-externalcatalog.md) will throw.

**Parameters**

- `displayName` — Display name of the UO, e.g. `"Anaerobic Digester"`, `"Shortcut FCC"`. See [`ExternalCatalog`](dwsim-automation-fluentapi-externalcatalog.md) for the canonical constants.
- `tag` — User-visible tag for the new instance.

### `AddFallingFilmEvaporator(string)`

Adds a Falling Film Evaporator unit operation tagged `tag` and returns its fluent builder.

### `AddFCC(string)`

Adds a FCC unit operation tagged `tag` and returns its fluent builder.

### `AddFilter(string)`

Adds a Filter unit operation tagged `tag` and returns its fluent builder.

### `AddFiredHeater(string)`

Adds a Fired Heater unit operation tagged `tag` and returns its fluent builder.

### `AddGibbsReactor(string)`

Adds a Gibbs Reactor unit operation tagged `tag` and returns its fluent builder.

### `AddHDS(string)`

Adds a HDS unit operation tagged `tag` and returns its fluent builder.

### `AddHeater(string)`

Adds a Heater unit operation tagged `tag` and returns its fluent builder.

### `AddHeatExchanger(string)`

Adds a Heat Exchanger unit operation tagged `tag` and returns its fluent builder.

### `AddHydrocracker(string)`

Adds a Hydrocracker unit operation tagged `tag` and returns its fluent builder.

### `AddHydroelectricTurbine(string)`

Adds a Hydroelectric Turbine unit operation tagged `tag` and returns its fluent builder.

### `AddIonExchange(string)`

Adds a Ion Exchange unit operation tagged `tag` and returns its fluent builder.

### `AddIsomerization(string)`

Adds a Isomerization unit operation tagged `tag` and returns its fluent builder.

### `AddMaterialStream(string)`

Creates a new [`MaterialStream`](dwsim-automation-fluentapi-flowsheet.md) tagged `tag` and returns its fluent builder.

### `AddMaterialStreamMapper(string)`

Adds a Material Stream Mapper unit operation tagged `tag` and returns its fluent builder.

### `AddMaterialStreamSwitch(string)`

Adds a Material Stream Switch unit operation tagged `tag` and returns its fluent builder.

### `AddMixer(string)`

Adds a Mixer unit operation tagged `tag` and returns its fluent builder.

### `AddNeutralizationReactor(string)`

Adds a Neutralization Reactor unit operation tagged `tag` and returns its fluent builder.

### `AddOrificePlate(string)`

Adds a Orifice Plate unit operation tagged `tag` and returns its fluent builder.

### `AddPEMFuelCell(string)`

Adds a PEMFuel Cell unit operation tagged `tag` and returns its fluent builder.

### `AddPFR(string)`

Adds a PFR unit operation tagged `tag` and returns its fluent builder.

### `AddPipe(string)`

Adds a Pipe unit operation tagged `tag` and returns its fluent builder.

### `AddPipeNetwork(string)`

Adds a Pipe Network unit operation tagged `tag` and returns its fluent builder.

### `AddPrecipitationReactor(string)`

Adds a Precipitation Reactor unit operation tagged `tag` and returns its fluent builder.

### `AddPretreatmentReactor(string)`

Adds a Pretreatment Reactor unit operation tagged `tag` and returns its fluent builder.

### `AddPump(string)`

Adds a Pump unit operation tagged `tag` and returns its fluent builder.

### `AddReaktoroGibbsReactor(string)`

Adds a Reaktoro Gibbs Reactor unit operation tagged `tag` and returns its fluent builder.

### `AddReformer(string)`

Adds a Reformer unit operation tagged `tag` and returns its fluent builder.

### `AddRestrictionOrifice(string)`

Adds a Restriction Orifice unit operation tagged `tag` and returns its fluent builder.

### `AddReverseOsmosis(string)`

Adds a Reverse Osmosis unit operation tagged `tag` and returns its fluent builder.

### `AddSeparator(string)`

Adds a Separator unit operation tagged `tag` and returns its fluent builder.

### `AddShortcutCDU(string)`

Adds a Shortcut CDU unit operation tagged `tag` and returns its fluent builder.

### `AddShortcutColumn(string)`

Adds a Shortcut Column unit operation tagged `tag` and returns its fluent builder.

### `AddSolarPanel(string)`

Adds a Solar Panel unit operation tagged `tag` and returns its fluent builder.

### `AddSolidsSeparator(string)`

Adds a Solids Separator unit operation tagged `tag` and returns its fluent builder.

### `AddSplitter(string)`

Adds a Splitter unit operation tagged `tag` and returns its fluent builder.

### `AddTank(string)`

Adds a Tank unit operation tagged `tag` and returns its fluent builder.

### `AddThermoPropertyEditor(string)`

Adds a Thermo Property Editor unit operation tagged `tag` and returns its fluent builder.

### `AddUnitOperation(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType, string)`

Generic escape hatch for any unit operation in the `DWSIM.Interfaces.Enums.GraphicObjects.ObjectType` enum that does not have a dedicated builder yet (e.g. RefluxedAbsorber, ReboiledAbsorber, Tank, etc.).

### `AddValve(string)`

Adds a Valve unit operation tagged `tag` and returns its fluent builder.

### `AddVaporCompressionChiller(string)`

Adds a Vapor Compression Chiller unit operation tagged `tag` and returns its fluent builder.

### `AddWaterElectrolyzer(string)`

Adds a Water Electrolyzer unit operation tagged `tag` and returns its fluent builder.

### `AddWindTurbine(string)`

Adds a Wind Turbine unit operation tagged `tag` and returns its fluent builder.

### `AddZeoliteAdsorber(string)`

Adds a Zeolite Adsorber unit operation tagged `tag` and returns its fluent builder.

### `AutoLayout`

Triggers the built-in auto-layout pass.

### `Create(string)`

Creates a new headless flowsheet.

### `DefineConversionReaction(string, Collections.Generic.Dictionary{string,double}, string, string, string, string)`

Defines a fractional-conversion reaction.

### `DefineEquilibriumReaction(string, Collections.Generic.Dictionary{string,double}, string, string, string, string, string, double, string)`

Defines an equilibrium reaction with a ln(Keq) expression.

### `DefineHetCatReaction(string, Collections.Generic.Dictionary{string,double}, string, string, string, string, string, string, string, string)`

Defines a heterogeneous catalytic (Langmuir-Hinshelwood) reaction.

### `DefineKineticReaction(string, Collections.Generic.Dictionary{string,double}, Collections.Generic.Dictionary{string,double}, Collections.Generic.Dictionary{string,double}, string, string, string, string, string, double, double, double, double, string, string, string)`

Defines a kinetic (Arrhenius) reaction.

### `EnergyStream(string)`

Looks up an existing [`EnergyStream`](dwsim-automation-fluentapi-flowsheet.md) by its tag and wraps it in a builder for further configuration / read-back.

### `Load(string)`

Loads a flowsheet from .dwxml or .dwxmz.

### `MakeExternal``2(string, string, Func{Flowsheet,``0,``1}, bool)`

Instantiates an external (IExternalUnitOperation) UO by display name and wraps the fresh instance in a typed builder. Used by the bioprocess, refining, electrolyte and other Plus typed builder methods.

**Remarks**

Dispatches to whichever `AddObjectToSurface` overload the wrapped host exposes: 
- `FlowsheetBase.AddObjectToSurface(type, x, y, tag, id, uoobj, createConnected)` — used by Automation3 / DWSIM.UI.Desktop.Shared / DynamicRunner.
- `FormFlowsheet.FormSurface.AddObjectToSurface(type, x, y, chemsep, tag, id, uoobj, createConnected)` — used by the classic WinForms editor (FormFlowsheet implements IFlowsheet directly, not via FlowsheetBase).

### `MaterialStream(string)`

Looks up an existing [`MaterialStream`](dwsim-automation-fluentapi-flowsheet.md) by its tag and wraps it in a builder for further configuration / read-back.

### `ReactionSet(string, string)`

Returns a builder for a reaction set; creates it if it does not exist.

### `RegisterAssemblyResolver`

Installs the assembly resolver that probes `extenders`, `unitops` and `ppacks` next to the running assembly. Call this once before any method that statically references Plus assemblies (LCA, TEA, refining UOs, electrolyte / ThermoPack PPs) is JITted — typically in your `Main` / process startup. [`Create`](dwsim-automation-fluentapi-flowsheet.md) calls it implicitly.

### `RunLCA`

Runs a complete Life Cycle Assessment on this flowsheet using `Analyze`. Returns impact categories (GWP, AP, EP, ODP, POCP, HTP, ADP), per-UO contribution, hotspot list, inventory summary and a Markdown report. Requires an active patron key (DWSIMPlus extension).

### `RunLCA(DWSIM.Extensions.LifeCycleAssessment.Models.LCASettings)`

Runs a complete Life Cycle Assessment with custom `settings` (functional unit, operating hours, system boundary, allocation method, grid mix region, carbon tax, etc.). Requires an active patron key (DWSIMPlus extension).

### `RunTEA`

Runs a complete Techno-Economic Analysis on this flowsheet using `Analyze`. Returns NPV, IRR, payback period, ROI, production cost, CAPEX/OPEX breakdown, equipment / materials / utilities tables, cash-flow projection and a Markdown report. Requires an active patron key (DWSIMPlus extension).

### `RunTEA(DWSIM.Extensions.TechnoEconomicAnalysis.Models.TEASettings)`

Runs a complete Techno-Economic Analysis with custom `settings` (discount rate, plant life, tax rate, working capital, operating hours, product price, depreciation, etc.). Requires an active patron key (DWSIMPlus extension).

### `Save(string, bool)`

Saves the flowsheet (compressed .dwxmz when `compressed` is true).

### `Solve`

Solves the flowsheet synchronously. Throws [`FlowsheetSolveException`](dwsim-automation-fluentapi-flowsheetsolveexception.md) containing all solver exceptions when one or more occur.

### `SolveCore`

Routes to the right solver entry point depending on whether the wrapped flowsheet is the headless `Flowsheet2` (use `Automation3`'s fast path) or any other `DWSIM.Interfaces.IFlowsheet` (FormFlowsheet, Eto UI.Forms.Flowsheet, extender host, …) — for those, fall through to the universal `DWSIM.FlowsheetSolver.FlowsheetSolver`.

### `TrySolve`

Solves the flowsheet without throwing; returns solver exceptions (empty when OK).

### `WithCompound(string)`

Adds a single compound by its DWSIM database name (e.g. `"Water"`, `"Methane"`).

### `WithCompounds(string[])`

Adds multiple compounds in one call. Equivalent to calling [`WithCompound`](dwsim-automation-fluentapi-flowsheet.md) for each.

### `WithPropertyPackage(string)`

Adds a property package by name (see [`PropertyPackages`](dwsim-automation-fluentapi-propertypackages.md)).

**Remarks**

Plus / DWSIMPlus PPs (electrolyte, ThermoPack, Reaktoro) require an active patron key.

### `Wrap(DWSIM.Interfaces.IFlowsheet)`

Wraps an `DWSIM.Interfaces.IFlowsheet` already living in memory — for example, the flowsheet of an open DWSIM editing session, an extender plugin, or the AI assistant host — and exposes the full Fluent surface (compounds, property packages, typed UO builders, reactions, solver, LCA / TEA) on top of it.

**Parameters**

- `existing` — The flowsheet to wrap (typically obtained from `Automation.GetMainWindow()`, an extender callback, or DWSIM's UI host).

**Remarks**

Use this when you don't want to allocate a new headless flowsheet but want to script edits on an existing one. The same `DWSIM.Interfaces.IFlowsheet` instance is reused, so subsequent calls (graphic placement, solver, save) happen on the live document the user sees. 

 

 Adding new `DWSIM.Interfaces.IExternalUnitOperation` instances (bioprocess, refining, electrolyte, advanced Plus) requires the underlying type to be either `FlowsheetBase` (used by `Automation3.Flowsheet2`, classic `FormFlowsheet` and Eto `UI.Forms.Flowsheet`) or any subclass — every standard DWSIM flowsheet host already qualifies.

**Example**

```csharp
// Inside a DWSIM extender plugin:
public void Run(IFlowsheet flowsheet) {
    var fs = Flowsheet.Wrap(flowsheet);
    fs.AddHeater("H-NEW")
      .WithOutletTemperature(350.Kelvin())
      .WithPressureDrop(0.5.Bar());
    fs.Solve();
}
```

## Properties

### `AvailableExternalUnitOperationNames`

Returns the display names of every loaded `DWSIM.Interfaces.IExternalUnitOperation` template.

### `AvailablePropertyPackages`

Returns the names of every property package registered in the flowsheet (free + Plus that loaded successfully).

### `Inner`

The underlying DWSIM flowsheet. Use this only when the Fluent surface is insufficient.
