# Builders.Bioprocess.BioReactorBuilder

`DWSIM.Automation.FluentAPI.Builders.Bioprocess.BioReactorBuilder`

Fluent builder for the Bio Reactor unit operation. Call [`AddBioReactor`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `GetProfileSeries(string)`

Returns a named profile series as an array of doubles.

### `ProfileToCSV`

Exports the full trajectory to CSV text.

### `ProfileToDataTable`

Exports the full trajectory to a DataTable for charting or tabular display.

### `WithAerobic(bool)`

Sets `Aerobic` and returns this builder for chaining.

### `WithBatchDuration(Quantity)`

Sets `Batch Duration` (SI) and returns this builder for chaining.

### `WithBiomassYield(double)`

Sets `Biomass Yield` and returns this builder for chaining.

### `WithKineticModel(DWSIM.UnitOperations.Reactors.BioKineticModel)`

Sets `Kinetic Model` and returns this builder for chaining.

### `WithKLaPerHour(double)`

Sets `KLa Per Hour` and returns this builder for chaining.

### `WithMaxSpecificGrowthPerHour(double)`

Sets `Max Specific Growth Per Hour` and returns this builder for chaining.

### `WithMonodKsGPerL(double)`

Sets `Monod Ks GPer L` and returns this builder for chaining.

### `WithOperatingMode(DWSIM.UnitOperations.Reactors.BioReactorMode)`

Sets `Operating Mode` and returns this builder for chaining.

### `WithThermalMode(DWSIM.UnitOperations.Reactors.BioReactorThermalMode)`

Sets `Thermal Mode` and returns this builder for chaining.

### `WithVolume(Quantity)`

Sets `Volume` (SI) and returns this builder for chaining.

## Properties

### `ProfileSeriesNames`

Names of all available profile series (e.g. "X", "S", "P", "Mu").

### `Trajectory`

Dynamic trajectory from the last Calculate call (biomass, substrate, product vs time). Null if not yet calculated.
