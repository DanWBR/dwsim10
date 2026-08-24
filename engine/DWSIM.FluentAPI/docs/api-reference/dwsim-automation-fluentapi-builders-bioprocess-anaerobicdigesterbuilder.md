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

### `WithAssumedPHForSulfide(double)`

Sets the pH assumed when splitting sulfide into volatile H2S and non-volatile HS-. Only free H2S leaves in the biogas and pKa1 is near 7, so the split is at its most pH-sensitive here. Used by BlackBox and ADM1-Lite; ADM1-Full uses its own pH.

### `WithBiomassYieldGVssPerGCOD(double)`

Sets `Biomass Yield GVss Per GCOD` and returns this builder for chaining.

### `WithCODRemoval(double)`

Sets `CODRemoval` and returns this builder for chaining.

### `WithHydraulicRetentionTime(Quantity)`

Sets `Hydraulic Retention Time` (SI) and returns this builder for chaining.

### `WithInfluentSulfateSulfurMgPerL(double)`

Sets the sulfate sulfur in the feed liquid, as S rather than as SO4 (mg S/L). Sulfate carries no COD of its own, so reducing it to sulfide draws 64 kg COD/kmol S out of the pool that would otherwise have made methane: expect a real drop in CH4.

### `WithMethaneFractionOverride(double)`

Sets `Methane Fraction Override` and returns this builder for chaining.

### `WithModel(DWSIM.UnitOperations.Reactors.DigesterModel)`

Sets `Model` and returns this builder for chaining.

### `WithSubstrateOrganicSulfurGPerKg(double)`

Sets the organic sulfur bound in the substrate (g S/kg substrate). Pass -1 to read it from the substrate compound's elemental formula, which keeps it consistent with the theoretical COD; pass >= 0 only to declare sulfur the formula omits. Unlike sulfate, this sulfur arrives already reduced and makes H2S at no cost in methane.

### `WithThermalMode(DWSIM.UnitOperations.Reactors.BioReactorThermalMode)`

Sets `Thermal Mode` and returns this builder for chaining.

### `WithVolume(Quantity)`

Sets `Volume` (SI) and returns this builder for chaining.

## Properties

### `ADM1FinalState`

Final ADM1 state after the last calculation. Null if model is not ADM1Full.

### `ADM1Trajectory`

Full ADM1 trajectory from the last Calculate call (state variables vs time). Null if not yet calculated or model is not ADM1Full.

### `ProfileSeriesNames`

Names of all available ADM1 profile series.
