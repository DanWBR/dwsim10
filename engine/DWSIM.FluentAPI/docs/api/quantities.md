# Quantities & Units

The Fluent API uses lightweight **unit-aware scalars** so call sites stay
readable without DWSIM losing its SI invariant. Two types do all the work:

- `Quantity` — a `readonly struct` holding the value already converted to SI
  plus a short dimension tag (`"T"`, `"P"`, `"Mflow"`, …).
- `Q` — a static class hosting **extension methods** on `double` and `int`
  (`300.Kelvin()`, `10.Bar()`, `100.KgPerSecond()`).

Builders consume `Quantity` directly via their `WithX` setters, which read
`q.SI` — DWSIM only ever sees SI units.

## C# / VB.NET — extension methods

```csharp
fs.AddMaterialStream("feed").At(300.Kelvin(), 10.Bar()).WithMassFlow(100.KgPerSecond());
```

In VB.NET, the same call site reads:

```vbnet
fs.AddMaterialStream("feed").At(300.0.Kelvin(), 10.0.Bar()).WithMassFlow(100.0.KgPerSecond())
```

## Python — static helpers on `Q`

pythonnet does not surface C# extension methods as instance methods, so
Python code calls them statically:

```python
from DWSIM.Automation.FluentAPI import Q
T = Q.Kelvin(300.0)
P = Q.Bar(10.0)
m = Q.KgPerSecond(100.0)
```

The returned `Quantity` is identical to what C# / VB.NET produce.

## Unit reference

| Dimension | Method | Source unit | Stored as |
|---|---|---|---|
| Temperature | `Kelvin(v)` | K | K |
| Temperature | `Celsius(v)` | °C | K (`v + 273.15`) |
| Pressure | `Pascal(v)` | Pa | Pa |
| Pressure | `KiloPascal(v)` | kPa | Pa |
| Pressure | `Bar(v)` | bar | Pa (`v × 10⁵`) |
| Pressure | `Atm(v)` | atm | Pa (`v × 101 325`) |
| Mass flow | `KgPerSecond(v)` | kg/s | kg/s |
| Mass flow | `KgPerHour(v)` | kg/h | kg/s |
| Molar flow | `MolPerSecond(v)` | mol/s | mol/s |
| Molar flow | `KmolPerSecond(v)` | kmol/s | mol/s |
| Molar flow | `KmolPerHour(v)` | kmol/h | mol/s |
| Volumetric flow | `CubicMetersPerSecond(v)` | m³/s | m³/s |
| Volumetric flow | `CubicMetersPerHour(v)` | m³/h | m³/s |
| Power | `Watts(v)` | W | kW (DWSIM's energy-stream unit) |
| Power | `Kilowatts(v)` | kW | kW |
| Power | `Megawatts(v)` | MW | kW |
| Length | `Meters(v)` | m | m |
| Length | `Centimeters(v)` | cm | m |
| Length | `Millimeters(v)` | mm | m |
| Length | `Inches(v)` | in | m |
| Volume | `CubicMeters(v)` | m³ | m³ |
| Volume | `Liters(v)` | L | m³ |
| Time | `Seconds(v)` | s | s |
| Time | `Minutes(v)` | min | s |
| Time | `Hours(v)` | h | s |
| Time | `Days(v)` | d | s |
| Fraction | `Fraction(v)` | 0–1 | 0–1 |
| Fraction | `Percent(v)` | 0–100 | 0–1 |

`int` overloads exist for the most common helpers (`Kelvin`, `Celsius`,
`Pascal`, `Bar`, `Atm`, `KgPerSecond`, `KgPerHour`, `MolPerSecond`,
`KmolPerHour`, `Kilowatts`, `Seconds`, `Minutes`, `Hours`, `Days`) so
literals like `300.Kelvin()` compile without an `.0` suffix.

## Reading values back

Builders expose plain `double` properties for read-back after `Solve` —
their names carry the unit (`TemperatureK`, `PressurePa`,
`MassFlowKgPerSecond`, `HeatDutyKW`, `DeltaPPa`, `PowerKW`, …) so there's
no ambiguity at the point of use.
