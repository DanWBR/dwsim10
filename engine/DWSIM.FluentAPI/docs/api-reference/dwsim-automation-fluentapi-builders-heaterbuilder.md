# Builders.HeaterBuilder

`DWSIM.Automation.FluentAPI.Builders.HeaterBuilder`

Fluent builder for the Heater unit operation. Call [`AddHeater`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithCalcMode(DWSIM.UnitOperations.UnitOperations.Heater.CalculationMode)`

Sets `Calc Mode` and returns this builder for chaining.

### `WithEfficiencyPercent(double)`

Sets `Efficiency Percent` and returns this builder for chaining.

### `WithHeatAdded(Quantity)`

Sets `Heat Added` (SI) and returns this builder for chaining.

### `WithOutletTemperature(Quantity)`

Sets `Outlet Temperature` (SI) and returns this builder for chaining.

### `WithOutletVaporFraction(double)`

Sets `Outlet Vapor Fraction` and returns this builder for chaining.

### `WithPressureDrop(Quantity)`

Sets `Pressure Drop` (SI) and returns this builder for chaining.

### `WithTemperatureChange(Quantity)`

Sets `Temperature Change` (SI) and returns this builder for chaining.

## Properties

### `HeatDutyKW`

Read-back of `Heat Duty KW` from the underlying object (populated after `Solve`).

### `OutletTemperatureK`

Read-back of `Outlet Temperature K` from the underlying object (populated after `Solve`).
