# Builders.CleanEnergy.WindTurbineBuilder

`DWSIM.Automation.FluentAPI.Builders.CleanEnergy.WindTurbineBuilder`

Fluent builder for the Wind Turbine unit operation. Call [`AddWindTurbine`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithAirDensityKgPerM3(double)`

Sets `Air Density Kg Per M3` and returns this builder for chaining.

### `WithDiskAreaM2(double)`

Sets `Disk Area M2` and returns this builder for chaining.

### `WithEfficiencyPercent(double)`

Sets `Efficiency Percent` and returns this builder for chaining.

### `WithRotorDiameterM(double)`

Sets `Rotor Diameter M` and returns this builder for chaining.

### `WithTurbineCount(int)`

Sets `Turbine Count` and returns this builder for chaining.

## Properties

### `GeneratedPowerKW`

Read-back of `Generated Power KW` from the underlying object (populated after `Solve`).

### `MaxTheoreticalPowerKW`

Read-back of `Max Theoretical Power KW` from the underlying object (populated after `Solve`).
