# Core Unit Operations

These cover the bulk of any flowsheet — mixers, splitters, heat / pressure
equipment, separators and storage.

## Mixer / Splitter

```csharp
fs.AddMixer("MIX-1")
  .ConnectFeed(stream1, 0)
  .ConnectFeed(stream2, 1)
  .ConnectProduct(outlet, 0);

fs.AddSplitter("SPL-1")
  .ConnectFeed(feed)
  .WithSplitRatios(0.7, 0.3)
  .ConnectProduct(top)
  .ConnectProduct(bot, 1);
```

`MixerBuilder` and `SplitterBuilder` expose the connection helpers from the
common base; splitters add `WithSplitRatios(params double[])` /
`WithFlowSpecs(params double[])`.

## Heater / Cooler

`HeaterBuilder` and `CoolerBuilder` share the `Heater` / `Cooler` calc-mode
machinery:

| Method | Calc mode set |
|---|---|
| `WithOutletTemperature(t)` | `OutletTemperature` |
| `WithOutletVaporFraction(frac)` | `OutletVaporFraction` |
| `WithHeatAdded(power)` (Heater) / `WithHeatRemoved(power)` (Cooler) | `HeatAdded` |
| `WithTemperatureChange(dT)` | `TemperatureChange` |
| `WithPressureDrop(dp)` | (independent) |
| `WithEfficiencyPercent(pct)` | (independent) |

Read-back: `HeatDutyKW`, `OutletTemperatureK`.

```csharp
fs.AddHeater("H-1")
  .WithOutletTemperature(450.Kelvin())
  .WithPressureDrop(0.2.Bar())
  .ConnectFeed(feed)
  .ConnectProduct(hot);
```

## Pump / Compressor / Expander / Valve

| Builder | Setters | Read-back |
|---|---|---|
| `PumpBuilder` | `WithPressureIncrease(dp)`, `WithOutletPressure(p)`, `WithPower(power)`, `WithEfficiencyPercent(pct)` | `DeltaPPa`, `PowerKW` |
| `CompressorBuilder` | `WithPressureIncrease(dp)`, `WithOutletPressure(p)`, `WithPower(power)`, `WithEfficiencyPercent(pct)` | `PowerKW` |
| `ExpanderBuilder` | `WithOutletPressure(p)`, `WithEfficiencyPercent(pct)` | `PowerKW` |
| `ValveBuilder` | `WithOutletPressure(p)`, `WithPressureDrop(dp)` | – |

```csharp
fs.AddPump("P-1")
  .WithOutletPressure(10.Bar())
  .WithEfficiencyPercent(75)
  .ConnectFeed(feed)
  .ConnectProduct(pumped);
```

## Heat exchanger

```csharp
fs.AddHeatExchanger("E-1")
  .WithCalculationMode(HeatExchangerCalcMode.PinchPoint)
  .WithGlobalUA(2500.0)
  .WithExchangeArea(50.0)
  .WithHotSidePressureDrop(0.2.Bar())
  .WithColdSidePressureDrop(0.1.Bar())
  .ConnectFeed(hotIn,  0).ConnectProduct(hotOut, 0)
  .ConnectFeed(coldIn, 1).ConnectProduct(coldOut, 1);
```

`HeatExchangerCalcMode` covers `CalcBothTemp_UA`, `CalcHotOut_HeatExchanged`,
`CalcColdOut_HeatExchanged`, `PinchPoint`, etc. — the same enum used by
DWSIM internally.

## Pipe / Orifice / Restriction

`PipeBuilder` and `OrificePlateBuilder` follow the same pattern; for the
restriction-orifice (Plus) UO use `AddRestrictionOrifice` and
`RestrictionOrificeBuilder`.

## Separation & storage

| Method | Builder | Setters |
|---|---|---|
| `AddSeparator(tag)` | `VesselBuilder` | `WithFlashPressure`, `WithFlashTemperature`, `WithPressureDrop` |
| `AddTank(tag)` | `TankBuilder` | `WithVolume`, `WithPressureDrop` |
| `AddComponentSeparator(tag)` | `ComponentSeparatorBuilder` | `WithSeparationFraction(compound, frac, port)` |
| `AddFilter(tag)` | `FilterBuilder` | `WithFilterMedium`, `WithCakeProperties`, `WithPressureDrop` |
| `AddSolidsSeparator(tag)` | `SolidsSeparatorBuilder` | `WithSolidsRecovery`, `WithSeparationEfficiency` |

Read each builder's source (under `Builders/` in the FluentAPI project) for
the full list — every method has an XML doc comment.
