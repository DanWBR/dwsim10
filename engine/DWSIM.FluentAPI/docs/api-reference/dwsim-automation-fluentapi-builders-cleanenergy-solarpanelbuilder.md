# Builders.CleanEnergy.SolarPanelBuilder

`DWSIM.Automation.FluentAPI.Builders.CleanEnergy.SolarPanelBuilder`

Fluent builder for the Solar Panel unit operation. Call [`AddSolarPanel`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithPanelAreaM2(double)`

Sets `Panel Area M2` and returns this builder for chaining.

### `WithPanelCount(int)`

Sets `Panel Count` and returns this builder for chaining.

### `WithPanelEfficiencyPercent(double)`

Sets `Panel Efficiency Percent` and returns this builder for chaining.

### `WithSolarIrradiationKWPerM2(double)`

Sets `Solar Irradiation KWPer M2` and returns this builder for chaining.

## Properties

### `ActualSolarIrradiationKWPerM2`

Read-back of `Actual Solar Irradiation KWPer M2` from the underlying object (populated after `Solve`).

### `GeneratedPowerKW`

Read-back of `Generated Power KW` from the underlying object (populated after `Solve`).
