# Builders.Bioprocess.BiogasUpgraderBuilder

`DWSIM.Automation.FluentAPI.Builders.Bioprocess.BiogasUpgraderBuilder`

Fluent builder for the Biogas Upgrader unit operation. Call [`AddBiogasUpgrader`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithCH4LossFraction(double)`

Sets `CH4Loss Fraction` and returns this builder for chaining.

### `WithCO2Removal(double)`

Sets `CO2Removal` and returns this builder for chaining.

### `WithH2ORemoval(double)`

Sets `H2ORemoval` and returns this builder for chaining.

### `WithH2SCompound(string)`

Names the compound treated as H2S, enabling [`WithH2SRemoval`](dwsim-automation-fluentapi-builders-bioprocess-biogasupgraderbuilder.md), and returns this builder for chaining. Unassigned by default (feed assumed already desulfurized).

### `WithH2SRemoval(double)`

Sets `H2SRemoval` and returns this builder for chaining. Has no effect unless [`WithH2SCompound`](dwsim-automation-fluentapi-builders-bioprocess-biogasupgraderbuilder.md) assigns the compound to strip; the upgrader logs a warning if the feed carries H2S with no compound assigned.

### `WithTargetCH4Purity(double)`

Sets `Target CH4Purity` and returns this builder for chaining.

### `WithTechnology(DWSIM.UnitOperations.UnitOperations.BiogasUpgraderTech)`

Sets `Technology` and returns this builder for chaining.
