# Unit Operations — Overview

Every unit operation has a typed builder. You obtain one through the
matching `Flowsheet.Add<UO>(tag)` method, configure it with `WithX` setters,
and connect ports via the inherited `ConnectFeed` / `ConnectProduct` /
`ConnectEnergyFeed` / `ConnectEnergyProduct`.

## Common base — `UnitOpBuilder<TObject, TSelf>`

Every typed builder inherits these methods (CRTP keeps the return type
correctly typed across chains):

| Method | Purpose |
|---|---|
| `ConnectFeed(stream, port = 0)` | Connect a material stream as a feed. |
| `ConnectProduct(stream, port = 0)` | Connect a material stream as a product. |
| `ConnectEnergyFeed(stream, port = 0)` | Energy stream as a feed (e.g. heat in). |
| `ConnectEnergyProduct(stream, port = 0)` | Energy stream as a product (e.g. heat out). |
| `ConnectNewProduct(newTag, port = 0)` | Allocate a new outlet stream and connect it. |
| `Configure(action)` | Escape hatch — applies an arbitrary mutation to the underlying DWSIM object. |
| `Object` | The underlying DWSIM object. |
| `Flowsheet` | The owning `Flowsheet`. |

## Categories

- **[Core](core.md)** — mixers, splitters, heaters, coolers, pumps,
  compressors, expanders, valves, pipes, heat exchangers, separators,
  tanks, filters.
- **[Reactors](reactors.md)** — conversion, equilibrium, Gibbs, CSTR, PFR,
  Reaktoro Gibbs.
- **[Columns](columns.md)** — shortcut, rigorous distillation, absorption.
- **[Bioprocess](bioprocess.md)** — bioreactor, anaerobic digester,
  pretreatment, centrifuge, crystallizer, …
- **[Refining (Plus)](refining.md)** — alkylation, FCC, reformer,
  hydrocracker, HDS, isomerization, CDU, …
- **[Electrolyte (Plus)](electrolyte.md)** — ion exchange, neutralization,
  precipitation, reverse osmosis.
- **[Clean Energy](clean-energy.md)** — wind / hydro turbines, solar
  panels, water electrolyzer, PEM fuel cell.

## Generic escape hatch

For unit operations that do not yet have a typed builder (e.g.
`RefluxedAbsorber`, `ReboiledAbsorber`):

```csharp
fs.AddUnitOperation(ObjectType.RefluxedAbsorber, "T-201")
  .ConnectFeed(feed)
  .ConnectProduct(top, 0)
  .ConnectProduct(bot, 1);
```

For any `IExternalUnitOperation` by its display name:

```csharp
fs.AddExternalUnitOperation("Anaerobic Digester", "AD-1");
```

`fs.AvailableExternalUnitOperationNames` enumerates every
`IExternalUnitOperation` registered in the running DWSIM build.
