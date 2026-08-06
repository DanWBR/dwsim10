# Builders.CleanEnergy.HydroelectricTurbineBuilder

`DWSIM.Automation.FluentAPI.Builders.CleanEnergy.HydroelectricTurbineBuilder`

Fluent builder for the Hydroelectric Turbine unit operation. Call [`AddHydroelectricTurbine`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithEfficiencyPercent(double)`

Sets `Efficiency Percent` and returns this builder for chaining.

### `WithInletVelocityMPerS(double)`

Sets `Inlet Velocity MPer S` and returns this builder for chaining.

### `WithOutletVelocityMPerS(double)`

Sets `Outlet Velocity MPer S` and returns this builder for chaining.

### `WithStaticHeadM(double)`

Sets `Static Head M` and returns this builder for chaining.

### `WithVelocityHeadM(double)`

Sets `Velocity Head M` and returns this builder for chaining.

## Properties

### `GeneratedPowerKW`

Read-back of `Generated Power KW` from the underlying object (populated after `Solve`).

### `TotalHeadM`

Read-back of `Total Head M` from the underlying object (populated after `Solve`).
