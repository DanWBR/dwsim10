# Builders.PumpBuilder

`DWSIM.Automation.FluentAPI.Builders.PumpBuilder`

Fluent builder for the Pump unit operation. Call [`AddPump`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithCalcMode(DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode)`

Sets `Calc Mode` and returns this builder for chaining.

### `WithEfficiencyPercent(double)`

Sets `Efficiency Percent` and returns this builder for chaining.

### `WithOutletPressure(Quantity)`

Sets `Outlet Pressure` (SI) and returns this builder for chaining.

### `WithPower(Quantity)`

Sets `Power` (SI) and returns this builder for chaining.

### `WithPressureIncrease(Quantity)`

Sets `Pressure Increase` (SI) and returns this builder for chaining.

## Properties

### `DeltaPPa`

Read-back of `Delta PPa` from the underlying object (populated after `Solve`).

### `PowerKW`

Read-back of `Power KW` from the underlying object (populated after `Solve`).
