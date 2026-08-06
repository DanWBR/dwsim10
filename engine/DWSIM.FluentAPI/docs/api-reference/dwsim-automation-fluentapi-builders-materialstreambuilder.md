# Builders.MaterialStreamBuilder

`DWSIM.Automation.FluentAPI.Builders.MaterialStreamBuilder`

Fluent wrapper for a `DWSIM.Thermodynamics.Streams.MaterialStream`.

## Methods

### `At(Quantity, Quantity)`

Sets temperature and pressure.

### `Configure(Action{DWSIM.Thermodynamics.Streams.MaterialStream})`

Escape hatch for any property not covered by a `WithX` helper. Mutates the underlying object via the supplied delegate.

### `OverallMassFraction(string)`

Mass fraction of  in the overall (mixture) phase.

### `OverallMoleFraction(string)`

Mole fraction of `compound` in the overall (mixture) phase.

### `SetCompoundMassFlow(string, double)`

Sets overall compound mass flow (kg/s).

### `SetCompoundMolarFlow(string, double)`

Sets overall compound molar flow (mol/s).

### `WithComposition(Action{CompositionBuilder})`

Configures composition fluently. Use `.Mole` / `.Mass` inside the builder.

### `WithMassFlow(Quantity)`

Sets `Mass Flow` (SI) and returns this builder for chaining.

### `WithMolarFlow(Quantity)`

Sets `Molar Flow` (SI) and returns this builder for chaining.

### `WithPressure(Quantity)`

Sets `Pressure` (SI) and returns this builder for chaining.

### `WithTemperature(Quantity)`

Sets `Temperature` (SI) and returns this builder for chaining.

### `WithVaporFraction(double)`

Sets `Vapor Fraction` and returns this builder for chaining.

### `WithVolumetricFlow(Quantity)`

Sets `Volumetric Flow` (SI) and returns this builder for chaining.

## Properties

### `Flowsheet`

The underlying DWSIM object / owning flowsheet — escape hatch for advanced use.

### `MassFlowKgPerSecond`

Read-back of `Mass Flow Kg Per Second` from the underlying object (populated after `Solve`).

### `MolarFlowMolPerSecond`

Read-back of `Molar Flow Mol Per Second` from the underlying object (populated after `Solve`).

### `Object`

The underlying DWSIM object / owning flowsheet — escape hatch for advanced use.

### `PressurePa`

Read-back of `Pressure Pa` from the underlying object (populated after `Solve`).

### `TemperatureK`

Read-back of `Temperature K` from the underlying object (populated after `Solve`).

### `VolumetricFlowM3PerSecond`

Read-back of `Volumetric Flow M3Per Second` from the underlying object (populated after `Solve`).
