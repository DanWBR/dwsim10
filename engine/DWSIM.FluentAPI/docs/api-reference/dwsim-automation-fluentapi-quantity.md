# Quantity

`DWSIM.Automation.FluentAPI.Quantity`

Lightweight unit-aware scalar. The numeric value is stored in SI units; conversion happens at construction via the extension methods on [`Q`](dwsim-automation-fluentapi-q.md). A [`Quantity`](dwsim-automation-fluentapi-quantity.md) is consumed by builder `WithX` setters which call [`SI`](dwsim-automation-fluentapi-quantity.md) directly, so DWSIM always sees SI internally.

**Example**

```csharp
fs.AddMaterialStream("feed").At(300.Kelvin(), 10.Bar()).WithMassFlow(100.KgPerSecond());
```

## Constructors

### `(ctor)(double, string)`

Constructs a [`Quantity`](dwsim-automation-fluentapi-quantity.md) from an SI value and a dimension tag.

## Methods

### `ToString`

Returns `"<value> (<dimension>, SI)"`.

## Properties

### `Dimension`

Short tag identifying the physical dimension (e.g. `"T"`, `"P"`, `"Mflow"`). Informational.

### `SI`

Numeric value in the canonical SI unit for this dimension (K, Pa, kg/s, mol/s, m, m³, m³/s, kW).
