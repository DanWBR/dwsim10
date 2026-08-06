# Streams

## Material streams

`fs.AddMaterialStream(tag)` returns a `MaterialStreamBuilder`.
`fs.MaterialStream(tag)` looks up an existing stream by its tag.

### Setters

| Method | Effect |
|---|---|
| `At(t, p)` | Shorthand for `WithTemperature(t).WithPressure(p)`. |
| `WithTemperature(t)` | Sets stream temperature. |
| `WithPressure(p)` | Sets stream pressure. |
| `WithMassFlow(m)` | Sets total mass flow. |
| `WithMolarFlow(n)` | Sets total molar flow. |
| `WithVolumetricFlow(q)` | Sets total volumetric flow. |
| `WithVaporFraction(frac)` | Sets molar vapor fraction. |
| `SetCompoundMolarFlow(name, mol/s)` | Per-compound molar flow override. |
| `SetCompoundMassFlow(name, kg/s)` | Per-compound mass flow override. |
| `WithComposition(c => …)` | Composition builder — see below. |
| `Configure(action)` | Escape hatch for the underlying `MaterialStream`. |

### Composition builder

```csharp
fs.AddMaterialStream("feed")
  .At(300.Kelvin(), 1.Atm())
  .WithMolarFlow(100.MolPerSecond())
  .WithComposition(c => c
      .Mole("Water",   0.50)
      .Mole("Ethanol", 0.50));
```

`Mole` and `Mass` entries are normalized when applied; mole takes precedence
when both are populated. The total flow set on the stream defines the basis.

### Read-back (after `Solve`)

| Property | Unit |
|---|---|
| `TemperatureK` | K |
| `PressurePa` | Pa |
| `MassFlowKgPerSecond` | kg/s |
| `MolarFlowMolPerSecond` | mol/s |
| `VolumetricFlowM3PerSecond` | m³/s |
| `OverallMoleFraction(compound)` | – |
| `OverallMassFraction(compound)` | – |

The underlying DWSIM object remains accessible through `Object` — useful for
phase-by-phase results and other detailed queries.

## Energy streams

`fs.AddEnergyStream(tag)` returns an `EnergyStreamBuilder`.

| Method / property | Notes |
|---|---|
| `WithEnergyFlow(power)` | Sets the energy flow (kW under the hood). |
| `EnergyFlowKW` | Read-back in kW after `Solve`. |
| `Object` | Underlying `EnergyStream`. |

Energy streams attach to unit operations through each builder's
`ConnectEnergyFeed` / `ConnectEnergyProduct` (inherited from
`UnitOpBuilder<,>`).

```csharp
var rd = fs.AddEnergyStream("reboiler-duty");
fs.AddDistillationColumn("T-101")
  .WithReboilerDuty(rd)
  // …
;
fs.Solve();
Console.WriteLine($"Reboiler duty = {rd.EnergyFlowKW:F2} kW");
```
