# External Catalog

`ExternalCatalog` holds canonical display-name constants for every
`IExternalUnitOperation` registered through `IFlowsheet.AvailableSimulationObjects`.
Use these constants with `Flowsheet.AddExternalUnitOperation` or with the
typed `Add*` methods (which already reference them internally).

```csharp
fs.AddExternalUnitOperation(ExternalCatalog.Bioprocess.AnaerobicDigester, "AD-1");
```

`ExternalCatalog.RequiresPlus(displayName)` answers whether a name needs an
active patron key.

## Bioprocess (free)

| Constant | Display name |
|---|---|
| `Bioprocess.AnaerobicDigester` | Anaerobic Digester |
| `Bioprocess.BioReactor` | BioReactor |
| `Bioprocess.CFBFastPyrolysis` | CFB Fast Pyrolysis |
| `Bioprocess.PretreatmentReactor` | Pretreatment Reactor |
| `Bioprocess.BiogasUpgrader` | Biogas Upgrader |
| `Bioprocess.CellLysis` | Cell Lysis |
| `Bioprocess.Centrifuge` | Centrifuge |
| `Bioprocess.ChromatographyColumn` | Chromatography Column |
| `Bioprocess.CrossflowUFDF` | Crossflow UF/DF |
| `Bioprocess.Crystallizer` | Crystallizer |

`Bioprocess.All` returns the flat list.

## Refining (Plus)

| Constant | Display name |
|---|---|
| `Refining.Alkylation` | Shortcut Alkylation |
| `Refining.AmineTreater` | Shortcut Amine Treater |
| `Refining.Blender` | Shortcut Blender |
| `Refining.ClausSRU` | Shortcut Claus SRU |
| `Refining.Coker` | Shortcut Coker |
| `Refining.FCC` | Shortcut FCC |
| `Refining.Hydrocracker` | Shortcut Hydrocracker |
| `Refining.HDS` | Shortcut HDS |
| `Refining.Isomerization` | Shortcut Isomerization |
| `Refining.Reformer` | Shortcut Reformer |
| `Refining.CDU` | Shortcut CDU |

## Electrolyte (Plus)

| Constant | Display name |
|---|---|
| `Electrolyte.IonExchangeUnit` | Ion Exchange Unit |
| `Electrolyte.NeutralizationReactor` | Neutralization Reactor |
| `Electrolyte.PrecipitationReactor` | Precipitation Reactor |
| `Electrolyte.ReverseOsmosisUnit` | Reverse Osmosis Unit |

## Plus advanced & ExtensionPack

| Constant | Display name |
|---|---|
| `Plus.AdvancedHeatExchanger` | Advanced Heat Exchanger |
| `Plus.FiredHeater` | Fired Heater |
| `Plus.PipeNetwork` | Pipe Network Unit Operation |
| `Plus.VaporCompressionChiller` | Vapor Compression Chiller |
| `Plus.ZeoliteAdsorber` | Zeolite Adsorber |
| `Plus.CopperBedHgAdsorber` | Copper Bed Hg Adsorber |
| `Plus.AirCooler2` | Air Cooler 2 |
| `Plus.EnergyMixer` | Energy Mixer |
| `Plus.EnergySplitter` | Energy Splitter |
| `Plus.EnergyStreamSwitch` | Energy Stream Switch |
| `Plus.MaterialStreamSwitch` | Material Stream Switch |
| `Plus.MaterialStreamMapper` | Material Stream Mapper |
| `Plus.FallingFilmEvaporator` | Falling Film Evaporator |
| `Plus.ThermoPropertyEditor` | Thermo Property Editor |

## Misc (free)

| Constant | Display name |
|---|---|
| `Misc.ReliefValve` | Relief Valve |

## Discovering at runtime

```csharp
foreach (var name in fs.AvailableExternalUnitOperationNames)
    Console.WriteLine(name);
```

Names match each UO's `GetDisplayName()` exactly and round-trip with this
list — so any constant in `ExternalCatalog` is guaranteed to resolve
against the running build (provided the corresponding DLL is present).
