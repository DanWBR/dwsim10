# Clean Energy

Free unit operations targeting hybrid energy / power-to-X flowsheets.

| `Flowsheet.Add*` | Builder | Inputs / outputs |
|---|---|---|
| `AddWindTurbine` | `WindTurbineBuilder` | Wind speed → electrical power. |
| `AddHydroelectricTurbine` | `HydroelectricTurbineBuilder` | Water flow + head → power. |
| `AddSolarPanel` | `SolarPanelBuilder` | Irradiance + array geometry → power. |
| `AddWaterElectrolyzer` | `WaterElectrolyzerBuilder` | Power + water → H₂ + O₂. |
| `AddPEMFuelCell` | `PEMFuelCellBuilder` | H₂ + O₂ → electrical power. |

```csharp
fs.AddWaterElectrolyzer("EL-1")
  .WithStackPower(2.Megawatts())
  .WithFaradayEfficiency(0.65)
  .WithCellVoltage(1.85)
  .WithOperatingPressure(30.Bar())
  .ConnectEnergyFeed(power)
  .ConnectFeed(water)
  .ConnectProduct(h2, 0)
  .ConnectProduct(o2, 1);
```

See examples [14](../../examples/14-water-electrolyzer.md) and
[15](../../examples/15-pem-fuel-cell.md).
