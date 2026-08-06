# Columns

## Shortcut column

`AddShortcutColumn(tag)` returns a `ShortcutColumnBuilder` for the
Fenske-Underwood-Gilliland shortcut (FUG). Useful for quick screening
before committing to the rigorous solver.

```csharp
fs.AddShortcutColumn("T-1")
  .WithLightKey("Ethanol", recovery: 0.99)
  .WithHeavyKey("Water",   recovery: 0.01)
  .WithCondenserPressure(1.Atm())
  .WithReboilerPressure(1.2.Atm())
  .WithRefluxRatio(2.0)
  .ConnectFeed(feed)
  .ConnectProduct(top, 0)
  .ConnectProduct(bot, 1);
```

## Distillation column (rigorous)

`AddDistillationColumn(tag)` returns `DistillationColumnBuilder` for the
stage-by-stage rigorous solver.

| Method | Purpose |
|---|---|
| `WithNumberOfStages(n)` | Total stages (incl. condenser & reboiler). |
| `WithFeed(stream, stageNumber)` | Feed location. |
| `WithDistillate(stream)` | Top product. |
| `WithBottoms(stream)` | Bottom product. |
| `WithVaporProduct(stream)` | Optional second-phase top product. |
| `WithCondenserDuty(energy)` | Energy stream for condenser. |
| `WithReboilerDuty(energy)` | Energy stream for reboiler. |
| `WithCondenserSpec(specType, value, units, compound = "")` | E.g. `"Reflux Ratio"`. |
| `WithReboilerSpec(specType, value, units, compound = "")` | E.g. `"Product Molar Flow Rate"`. |
| `WithTopPressure(p)` | Top-stage pressure. |
| `WithColumnPressureDrop(dp)` | Total drop across the column. |

```csharp
fs.AddDistillationColumn("T-101")
  .WithNumberOfStages(20)
  .WithFeed(feed, 10)
  .WithDistillate(dist)
  .WithBottoms(bot)
  .WithCondenserDuty(cd)
  .WithReboilerDuty(rd)
  .WithCondenserSpec("Reflux Ratio", 2.0)
  .WithReboilerSpec("Product Molar Flow Rate", 75.0, "mol/s")
  .WithTopPressure(1.Atm())
  .WithColumnPressureDrop(0.Pascal());
```

`specType` and `units` follow DWSIM's existing rigorous-column convention —
the strings are the same as the editor combo-box entries.

## Absorption column

`AddAbsorptionColumn(tag)` returns `AbsorptionColumnBuilder` for the
rigorous absorber. Surface mirrors the distillation builder minus condenser
/ reboiler.

```csharp
fs.AddAbsorptionColumn("ABS-1")
  .WithNumberOfStages(15)
  .WithLeanSolvent(solvent, stage: 1)
  .WithGasFeed(feed, stage: 15)
  .WithRichSolvent(richOut)
  .WithTreatedGas(gasOut)
  .WithTopPressure(1.Atm())
  .WithColumnPressureDrop(0.1.Bar());
```
