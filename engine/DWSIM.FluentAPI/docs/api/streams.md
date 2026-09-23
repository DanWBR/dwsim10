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
| `SetCompoundMolarFlow(name, mol/s)` | Molar flow of one compound; on a new stream, the compounds never named are set to zero. |
| `SetCompoundMassFlow(name, kg/s)` | Mass flow of one compound; on a new stream, the compounds never named are set to zero. |
| `WithComposition(c => …)` | Composition builder — see below. Compounds not named are set to zero. |
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
Compounds not named are set to zero.

### Per-compound flows

A new stream starts with every compound at the flowsheet's default share. On a
stream created by `AddMaterialStream`, the first `SetCompoundMolarFlow` or
`SetCompoundMassFlow` call zeroes every compound not named through the builder,
so a feed defined one compound at a time carries only the compounds named:

```csharp
fs.AddMaterialStream("syngas")
  .At(300.Kelvin(), 30.Bar())
  .SetCompoundMolarFlow("Hydrogen", 2.0)
  .SetCompoundMolarFlow("Carbon monoxide", 1.0);   // Methanol ends at zero
```

On a stream obtained with `fs.MaterialStream(tag)` that the builder did not
create (one loaded from a file, for instance), each call changes only the
compound named and the others keep their flows.

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
