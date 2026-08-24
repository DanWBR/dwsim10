# PropertyPackageBuilder

`DWSIM.Automation.FluentAPI.PropertyPackageBuilder`

Fluent surface for tweaking a property package after `WithPropertyPackage` has instantiated it. Exposes flash-algorithm choice, generic flash settings, and typed sub-builders that surface the model-specific interaction parameters (PR/SRK kij, NRTL Aij/alpha, UNIQUAC Aij, Wilson Aij). For anything not covered by a typed setter, use [`Configure`](dwsim-automation-fluentapi-propertypackagebuilder.md) to mutate the underlying object directly.

## Methods

### `Configure(Action{DWSIM.Interfaces.IPropertyPackage})`

Escape hatch: applies an arbitrary mutation to the underlying property package.

### `ConfigureNRTL(Action{NRTLConfig})`

Configures NRTL binary parameters (A12, A21, alpha; optionally B12/B21 for T-dependent).

### `ConfigurePR(Action{PRConfig})`

Configures Peng-Robinson (PR / PR78 / PRSV2) interaction parameters via a typed sub-builder.

### `ConfigureSRK(Action{SRKConfig})`

Configures Soave-Redlich-Kwong interaction parameters.

### `ConfigureUNIQUAC(Action{UNIQUACConfig})`

Configures UNIQUAC binary parameters (A12, A21; optionally B12/B21).

### `ConfigureWilson(Action{WilsonConfig})`

Configures Wilson binary parameters by CAS number (the underlying model is keyed by CAS).

### `WithFlashApproach(DWSIM.Thermodynamics.PropertyPackages.PropertyPackage.FlashCalculationApproachType)`

Switches the high-level flash strategy: NestedLoops (default), InsideOut or Gibbs minimization.

### `WithFlashSetting(DWSIM.Interfaces.Enums.FlashSetting, string)`

Sets a single entry on the property package's `FlashSettings` dictionary. Values are stored as strings (DWSIM convention).

### `WithFlashSetting(DWSIM.Interfaces.Enums.FlashSetting, double)`

Convenience overload that formats `value` using invariant culture.

### `WithFlashSetting(DWSIM.Interfaces.Enums.FlashSetting, bool)`

Convenience overload for boolean settings.

## Properties

### `Flowsheet`

The owning flowsheet.

### `Inner`

The underlying DWSIM property package - escape hatch for advanced settings.
