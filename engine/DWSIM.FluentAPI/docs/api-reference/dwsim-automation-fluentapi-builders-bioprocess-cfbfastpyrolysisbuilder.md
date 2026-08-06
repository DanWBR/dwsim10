# Builders.Bioprocess.CFBFastPyrolysisBuilder

`DWSIM.Automation.FluentAPI.Builders.Bioprocess.CFBFastPyrolysisBuilder`

Fluent builder for the CFBFast Pyrolysis unit operation. Call `AddCFBFastPyrolysis` to obtain one.

## Methods

### `GetProfileSeries(string)`

Returns a named axial profile series as an array of doubles.

### `ProfileToCSV`

Exports the full axial trajectory to CSV text.

### `ProfileToDataTable`

Exports the full axial trajectory to a DataTable for charting or tabular display.

### `WithAxialCells(int)`

Sets `Axial Cells` and returns this builder for chaining.

### `WithBiomassComposition(double, double, double)`

Sets `Biomass Composition` and returns this builder for chaining.

### `WithCarrierGasVelocityMPerS(double)`

Sets `Carrier Gas Velocity MPer S` and returns this builder for chaining.

### `WithHeatLossFraction(double)`

Sets `Heat Loss Fraction` and returns this builder for chaining.

### `WithRiserDiameter(Quantity)`

Sets `Riser Diameter` (SI) and returns this builder for chaining.

### `WithRiserHeight(Quantity)`

Sets `Riser Height` (SI) and returns this builder for chaining.

### `WithSandInletTemperature(Quantity)`

Sets `Sand Inlet Temperature` (SI) and returns this builder for chaining.

### `WithSandMode(DWSIM.UnitOperations.Reactors.CFBSandMode)`

Sets `Sand Mode` and returns this builder for chaining.

### `WithSandToBiomassRatio(double)`

Sets `Sand To Biomass Ratio` and returns this builder for chaining.

### `WithSolidsHoldup(double)`

Sets `Solids Holdup` and returns this builder for chaining.

## Properties

### `ProfileSeriesNames`

Names of all available axial profile series (e.g. "T_K", "SolidVelocity_ms").

### `Trajectory`

Axial trajectory from the last Calculate call (temperature, yields, species vs riser height). Null if not yet calculated.
