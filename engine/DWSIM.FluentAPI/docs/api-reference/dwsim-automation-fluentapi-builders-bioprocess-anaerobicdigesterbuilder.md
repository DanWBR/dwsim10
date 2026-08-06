# Builders.Bioprocess.AnaerobicDigesterBuilder

`DWSIM.Automation.FluentAPI.Builders.Bioprocess.AnaerobicDigesterBuilder`

Fluent builder for the Anaerobic Digester unit operation. Call [`AddAnaerobicDigester`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `GetProfileSeries(string)`

Returns a named ADM1 profile series as an array of doubles.

### `ProfileToCSV`

Exports the full ADM1 trajectory to CSV text.

### `ProfileToDataTable`

Exports the full ADM1 trajectory to a DataTable for charting or tabular display.

### `WithADM1AcetateUptakePerDay(double)`

Sets `ADM1Acetate Uptake Per Day` and returns this builder for chaining.

### `WithADM1HydrolysisRatePerDay(double)`

Sets the ADM1 first-order hydrolysis rate constant (per day).

### `WithADM1SugarUptakePerDay(double)`

Sets `ADM1Sugar Uptake Per Day` and returns this builder for chaining.

### `WithBiomassYieldGVssPerGCOD(double)`

Sets `Biomass Yield GVss Per GCOD` and returns this builder for chaining.

### `WithCODRemoval(double)`

Sets `CODRemoval` and returns this builder for chaining.

### `WithHydraulicRetentionTime(Quantity)`

Sets `Hydraulic Retention Time` (SI) and returns this builder for chaining.

### `WithMethaneFractionOverride(double)`

Sets `Methane Fraction Override` and returns this builder for chaining.

### `WithModel(DWSIM.UnitOperations.Reactors.DigesterModel)`

Sets `Model` and returns this builder for chaining.

### `WithThermalMode(DWSIM.UnitOperations.Reactors.BioReactorThermalMode)`

Sets `Thermal Mode` and returns this builder for chaining.

### `WithVolume(Quantity)`

Sets `Volume` (SI) and returns this builder for chaining.

## Properties

### `ADM1FinalState`

Final ADM1 state after the last calculation. Null if model is not ADM1Full.

### `ADM1Trajectory`

Full ADM1 trajectory from the last Calculate call (29 state variables vs time). Null if not yet calculated or model is not ADM1Full.

### `ProfileSeriesNames`

Names of all available ADM1 profile series.
