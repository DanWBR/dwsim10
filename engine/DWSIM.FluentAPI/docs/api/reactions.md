# Reactions

Reactions are defined on the `Flowsheet` and grouped into reaction sets;
reactor builders bind to a set by id.

## Defining reactions

### Conversion (fractional)

```csharp
var r1 = fs.DefineConversionReaction(
    name: "R1",
    stoichiometry: new Dictionary<string, double> {
        ["Methane"] = -1, ["Water"] = -2,
        ["Carbon dioxide"] = 1, ["Hydrogen"] = 4 },
    baseCompound: "Methane",
    phase: "Vapor",
    conversionExpression: "50",   // % of base compound converted
    description: "");
```

`conversionExpression` is a DWSIM expression — usually a literal percent,
but any expression evaluable by the calculator engine works (e.g.
`"50 + 0.1*T"`).

### Equilibrium

```csharp
var r2 = fs.DefineEquilibriumReaction(
    name: "WGS",
    stoichiometry: new Dictionary<string, double> {
        ["Carbon monoxide"] = -1, ["Water"] = -1,
        ["Carbon dioxide"] = 1, ["Hydrogen"] = 1 },
    baseCompound: "Carbon monoxide",
    phase: "Vapor", basis: "Activity", units: "",
    lnKeqExpression: "4577.8/T - 4.33",
    approachT: 0.0);
```

### Kinetic (Arrhenius)

```csharp
var r3 = fs.DefineKineticReaction(
    name: "R-Kin",
    stoichiometry: stoich,
    directOrders: directOrders,
    reverseOrders: reverseOrders,
    baseCompound: "A", phase: "Vapor",
    basis: "Activity", amountUnits: "mol/L", rateUnits: "mol/[L.min]",
    aForward: 1.0e10, eForward: 80_000,
    aReverse: 0.0,    eReverse: 0.0);
```

Use `forwardExpression` / `reverseExpression` (string) to override the rate
law if the Arrhenius form is not enough.

### Heterogeneous catalytic (Langmuir-Hinshelwood)

```csharp
var r4 = fs.DefineHetCatReaction(
    name: "HetCat",
    stoichiometry: stoich,
    baseCompound: "A", phase: "Vapor",
    basis: "Activity", amountUnits: "mol/L", rateUnits: "mol/[kgcat.s]",
    numeratorExpression: "k * P_A * P_B",
    denominatorExpression: "(1 + K_A*P_A + K_B*P_B)^2");
```

## Reaction sets

```csharp
fs.ReactionSet("Methane Reforming", "Steam reforming reactions")
  .Add(r1)
  .Add(r2);
```

`ReactionSet(id, description)` creates the set if it doesn't exist and
returns a builder. `Add(reaction, rank = 0, enabled = true)` binds an
existing reaction; `rank` controls evaluation order in the equilibrium
solver.

Bind a reactor to a set with `WithReactionSet("Set Id")` or
`WithReactionSet(setBuilder)`.

## Python — passing dictionaries

Build the .NET `Dictionary<string, double>` from Python via pythonnet:

```python
from System.Collections.Generic import Dictionary
from System import String, Double

def stoich(d):
    out = Dictionary[String, Double]()
    for k, v in d.items():
        out[k] = float(v)
    return out

r1 = fs.DefineConversionReaction(
    "R1",
    stoich({"Methane": -1, "Water": -2, "Carbon dioxide": 1, "Hydrogen": 4}),
    "Methane", "Vapor", "50")
```
