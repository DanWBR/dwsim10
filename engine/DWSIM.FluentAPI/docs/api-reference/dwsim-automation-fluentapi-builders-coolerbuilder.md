# Builders.CoolerBuilder

`DWSIM.Automation.FluentAPI.Builders.CoolerBuilder`

Fluent builder for the Cooler unit operation. Call [`AddCooler`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithCalcMode(DWSIM.UnitOperations.UnitOperations.Cooler.CalculationMode)`

Sets `Calc Mode` and returns this builder for chaining.

### `WithEfficiencyPercent(double)`

Sets `Efficiency Percent` and returns this builder for chaining.

### `WithHeatRemoved(Quantity)`

Sets `Heat Removed` (SI) and returns this builder for chaining.

### `WithOutletTemperature(Quantity)`

Sets `Outlet Temperature` (SI) and returns this builder for chaining.

### `WithOutletVaporFraction(double)`

Sets `Outlet Vapor Fraction` and returns this builder for chaining.

### `WithPressureDrop(Quantity)`

Sets `Pressure Drop` (SI) and returns this builder for chaining.

### `WithTemperatureChange(Quantity)`

Sets `Temperature Change` (SI) and returns this builder for chaining.

## Properties

### `HeatRemovedKW`

Read-back of `Heat Removed KW` from the underlying object (populated after `Solve`).
