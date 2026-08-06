# Bioprocess

Bioprocess UOs ship with the free DWSIM build (no patron key required) but
are wired through the `IExternalUnitOperation` path — every typed `Add*`
method below resolves the corresponding template by display name and
returns a strongly-typed builder.

| `Flowsheet.Add*` | Display name | Builder | Notes |
|---|---|---|---|
| `AddBioReactor` | `BioReactor` | `BioReactorBuilder` | Monod / Moser / Teissier kinetics; batch / fed-batch / continuous. |
| `AddAnaerobicDigester` | `Anaerobic Digester` | `AnaerobicDigesterBuilder` | BlackBox / ADM1-Lite / ADM1 full. |
| `AddCFBFastPyrolysisReactor` | `CFB Fast Pyrolysis` | `CFBFastPyrolysisBuilder` | 1-D PFR with sand circulation. |
| `AddPretreatmentReactor` | `Pretreatment Reactor` | `PretreatmentBuilder` | Dilute acid / steam-explosion / alkaline / organosolv. |
| `AddBiogasUpgrader` | `Biogas Upgrader` | `BiogasUpgraderBuilder` | Water-scrubbing / amine / PSA / membrane. |
| `AddCellLysis` | `Cell Lysis` | `CellLysisBuilder` | Homogenizer / bead mill / chemical / enzymatic / osmotic / ultrasonic. |
| `AddCentrifuge` | `Centrifuge` | `CentrifugeBuilder` | Disk-stack / decanter / tubular. |
| `AddChromatographyColumn` | `Chromatography Column` | `ChromatographyBuilder` | Bind-elute / flow-through / Thomas-model. |
| `AddCrossflowUF` | `Crossflow UF/DF` | `CrossflowUFBuilder` | UF / DF with optional Hermia fouling. |
| `AddCrystallizer` | `Crystallizer` | `CrystallizerBuilder` | Cooling / evaporative / antisolvent. |

Every builder follows the same fluent shape:

```csharp
fs.AddAnaerobicDigester("AD-1")
  .WithModel(AnaerobicDigesterModel.ADM1Lite)
  .WithRetentionTimeDays(20)
  .WithVolume(500.CubicMeters())
  .WithTemperature(308.Kelvin())
  .ConnectFeed(slurry)
  .ConnectProduct(biogas, 0)
  .ConnectProduct(digestate, 1);
```

Use `Configure(action)` for any property without a typed setter. The
underlying object remains accessible through `Object` (typed) and
`Object` properties exposed by DWSIM's UnitOperations DLL.

See [examples 04 and 05](../../examples/04-bioprocess-train.md) for two
end-to-end bioprocess flowsheets.
