# Builders.DistillationColumnBuilder

`DWSIM.Automation.FluentAPI.Builders.DistillationColumnBuilder`

Fluent builder for the rigorous distillation column.

## Methods

### `WithBottoms(MaterialStreamBuilder)`

Sets `Bottoms` and returns this builder for chaining.

### `WithColumnPressureDrop(Quantity)`

Sets `Column Pressure Drop` (SI) and returns this builder for chaining.

### `WithCondenserDuty(EnergyStreamBuilder)`

Sets `Condenser Duty` and returns this builder for chaining.

### `WithCondenserSpec(string, double, string, string)`

Sets the condenser specification (e.g. "Reflux Ratio", value, "" for unitless). The unit can also travel inside the spec type: `"Product Flow Rate (mol/s)"`.

### `WithCondenserSpec(string, Quantity, string)`

Sets the condenser specification from a [`Quantity`](dwsim-automation-fluentapi-quantity.md) (e.g. `"Product Molar Flow Rate", 75.0.MolPerSecond()`).

### `WithDistillate(MaterialStreamBuilder)`

Sets `Distillate` and returns this builder for chaining.

### `WithFeed(MaterialStreamBuilder, int)`

Sets `Feed` and returns this builder for chaining.

### `WithNumberOfStages(int)`

Sets `Number Of Stages` and returns this builder for chaining.

### `WithReboilerDuty(EnergyStreamBuilder)`

Sets `Reboiler Duty` and returns this builder for chaining.

### `WithReboilerSpec(string, double, string, string)`

Sets the reboiler specification (e.g. "Product Molar Flow Rate", 75, "mol/s"). The unit can also travel inside the spec type: `"Product Flow Rate (mol/s)"`.

### `WithReboilerSpec(string, Quantity, string)`

Sets the reboiler specification from a [`Quantity`](dwsim-automation-fluentapi-quantity.md) (e.g. `"Product Molar Flow Rate", 25.0.MolPerSecond()`).

### `WithTopPressure(Quantity)`

Sets `Top Pressure` (SI) and returns this builder for chaining.

### `WithVaporProduct(MaterialStreamBuilder)`

Sets `Vapor Product` and returns this builder for chaining.
