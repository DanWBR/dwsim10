# Builders.MaterialStreamBuilder

`DWSIM.Automation.FluentAPI.Builders.MaterialStreamBuilder`

Fluent wrapper for a `DWSIM.Thermodynamics.Streams.MaterialStream`.

## Methods

### `AsFlowSpec`

Specifies this stream by flow: its mass flow is held and its pressure is whatever the network resolves to. This is the usual choice for a feed.

### `AsPressureSpec`

Specifies this stream by pressure: its pressure is held and its flow is whatever the network resolves to. A network needs at least one of these or it is underdetermined.

### `At(Quantity, Quantity)`

Sets temperature and pressure.

### `CalculateBinaryDiagram_Pxy(double, bool, bool, int)`

Computes a P-x-y binary phase diagram at the given temperature. The stream must contain exactly two compounds.

**Parameters**

- `temperatureK` — System temperature in K.
- `includeVLE` — Calculate VLE curves (default true).
- `includeLLE` — Calculate LLE curves (default false).
- `steps` — Number of composition steps (default 40).

### `CalculateBinaryDiagram_Txy(double, bool, bool, bool, bool, int)`

Computes a T-x-y binary phase diagram at the given pressure. The stream must contain exactly two compounds.

**Parameters**

- `pressurePa` — System pressure in Pa.
- `includeVLE` — Calculate vapor-liquid equilibrium curves (default true).
- `includeLLE` — Calculate liquid-liquid equilibrium curves (default false).
- `includeSLE` — Calculate solid-liquid equilibrium curves (default false).
- `includeCritical` — Calculate critical locus (default false).
- `steps` — Number of composition steps (default 40).

### `CalculateCriticalPoints`

Calculates the mixture critical point(s) for the current stream composition. Returns an empty list for pure components (use compound data instead).

### `CalculatePhaseEnvelope(Action{DWSIM.Thermodynamics.PropertyPackages.PhaseEnvelopeOptions})`

Computes the phase envelope (bubble/dew curves, critical point, optional quality line, LLE, SLE, Widom line) for the current stream composition. The stream must have a property package assigned and a valid composition.

**Parameters**

- `configure` — Optional callback to customise `DWSIM.Thermodynamics.PropertyPackages.PhaseEnvelopeOptions` (quality line, hydrate, stability curve, SLE, custom initial conditions, etc.).

### `Configure(Action{DWSIM.Thermodynamics.Streams.MaterialStream})`

Escape hatch: applies an arbitrary mutation to the underlying stream.

### `FlipHorizontal(bool)`

Mirrors the stream horizontally (points its arrow the other way), as one does on a recycle return.

### `FlipVertical(bool)`

Mirrors the stream vertically.

### `OverallMassFraction(string)`

Mass fraction of  in the overall (mixture) phase.

### `OverallMoleFraction(string)`

Mole fraction of `compound` in the overall (mixture) phase.

### `PositionAt(int, int)`

Places the stream at (x, y) on the canvas.

### `Rotate(int)`

Rotates the stream on the canvas; use 0, 90, 180 or 270 degrees.

### `SetCompoundMassFlow(string, double)`

Sets overall compound mass flow (kg/s).

### `SetCompoundMolarFlow(string, double)`

Sets overall compound molar flow (mol/s).

### `WithComposition(Action{CompositionBuilder})`

Configures composition fluently. Use `.Mole` / `.Mass` inside the builder.

### `WithDynamicsSpec(DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType)`

Declares whether this stream is specified by pressure or by flow in the dynamic pressure-flow network.

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

### `DynamicsSpec`

The stream's current pressure-flow specification.

### `Flowsheet`

The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.

### `MassFlowKgPerSecond`

Read-back of `Mass Flow Kg Per Second` from the underlying object (populated after `Solve`).

### `MolarFlowMolPerSecond`

Read-back of `Molar Flow Mol Per Second` from the underlying object (populated after `Solve`).

### `Object`

The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.

### `PressurePa`

Read-back of `Pressure Pa` from the underlying object (populated after `Solve`).

### `TemperatureK`

Read-back of `Temperature K` from the underlying object (populated after `Solve`).

### `VolumetricFlowM3PerSecond`

Read-back of `Volumetric Flow M3Per Second` from the underlying object (populated after `Solve`).
