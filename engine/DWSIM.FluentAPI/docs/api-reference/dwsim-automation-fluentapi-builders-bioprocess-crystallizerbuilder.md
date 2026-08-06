# Builders.Bioprocess.CrystallizerBuilder

`DWSIM.Automation.FluentAPI.Builders.Bioprocess.CrystallizerBuilder`

Fluent builder for the Crystallizer unit operation. Call [`AddCrystallizer`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithEvaporationFraction(double)`

Sets `Evaporation Fraction` and returns this builder for chaining.

### `WithMeanCrystalSizeMicrons(double)`

Sets `Mean Crystal Size Microns` and returns this builder for chaining.

### `WithMode(DWSIM.UnitOperations.UnitOperations.CrystallizerMode)`

Sets `Mode` and returns this builder for chaining.

### `WithOperatingTemperature(Quantity)`

Sets `Operating Temperature` (SI) and returns this builder for chaining.

### `WithSolubilityCoefficients(double, double, double)`

Solubility C(T) [g solute / g solvent] = A + B*(T-273.15) + C*(T-273.15)^2.

### `WithSolubilityReductionByAntisolvent(double)`

Sets `Solubility Reduction By Antisolvent` and returns this builder for chaining.

### `WithSoluteCompound(string)`

Sets `Solute Compound` and returns this builder for chaining.

### `WithSolventCompound(string)`

Sets `Solvent Compound` and returns this builder for chaining.
