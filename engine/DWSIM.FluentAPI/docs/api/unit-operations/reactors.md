# Reactors

All reactor builders inherit `ReactorBuilder<TObject, TSelf>`, which adds:

| Method | Notes |
|---|---|
| `WithOperationMode(OperationMode mode)` | `Isothermic`, `Adiabatic`, `OutletTemperature`, `NonIsothermalNonAdiabatic`, `HeatExchange`. |
| `Isothermal()` / `Adiabatic()` | Convenience shortcuts. |
| `WithReactionSet(string id)` | Binds the reactor to a reaction set. |
| `WithReactionSet(ReactionSetBuilder set)` | Same, by builder reference. |
| `WithPressureDrop(dp)` | Inlet→outlet pressure drop. |
| `HeatDutyKW` | Read-back after `Solve` (kW). |

## Conversion reactor

```csharp
var r1 = fs.DefineConversionReaction(
    "R1",
    new Dictionary<string, double> {
        ["Methane"] = -1, ["Water"] = -2, ["Carbon dioxide"] = 1, ["Hydrogen"] = 4 },
    baseCompound: "Methane", phase: "Vapor", conversionExpression: "50");

fs.ReactionSet("Set1").Add(r1);

fs.AddConversionReactor("R-1")
  .Isothermal()
  .WithReactionSet("Set1")
  .ConnectFeed(feed)
  .ConnectProduct(gasOut, 0)
  .ConnectProduct(liqOut, 1)
  .ConnectEnergyFeed(heat, 1);
```

`Object.ComponentConversions` exposes the per-compound conversion after
`Solve`.

## Equilibrium / Gibbs reactor

`AddEquilibriumReactor`, `AddGibbsReactor`, `AddReaktoroGibbsReactor` —
inherit the common reactor surface; configure the reaction set / phase
options via the typed builder. Gibbs reactors do not need an explicit
reaction set when given a list of compounds.

## CSTR / PFR

| Builder | Setters |
|---|---|
| `CSTRBuilder` | `WithVolume(v)`, `WithHeadspaceFraction(f)`, `WithIsothermalTemperature(t)`, `WithCatalystAmountKg(kg)` |
| `PFRBuilder` | `WithVolume(v)` |

CSTR and PFR consume **kinetic** reactions (`DefineKineticReaction`) or
**heterogeneous catalytic** reactions (`DefineHetCatReaction`).

```csharp
var rxn = fs.DefineKineticReaction(
    "R-Kin",
    stoichiometry, directOrders, reverseOrders,
    baseCompound: "Methane", phase: "Vapor",
    basis: "Activity", amountUnits: "mol/L", rateUnits: "mol/[L.min]",
    aForward: 1.0e10, eForward: 80_000);
fs.ReactionSet("Kinetic").Add(rxn);

fs.AddCSTR("CSTR-1")
  .WithVolume(2.5.CubicMeters())
  .WithIsothermalTemperature(800.Kelvin())
  .WithReactionSet("Kinetic")
  .ConnectFeed(feed)
  .ConnectProduct(prod);
```

See [Reactions](../reactions.md) for the full reaction-definition reference.
