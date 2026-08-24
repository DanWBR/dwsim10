# ExternalCatalog.Plus

`DWSIM.Automation.FluentAPI.ExternalCatalog.Plus`

Other Plus components (advanced HX, fired heater, networking, energy-stream ops, etc.).

## Properties

### `All`

Every advanced-Plus display name as a flat list.

## Fields

### `AdvancedHeatExchanger`

Shell-and-tube heat exchanger with rating / design / simulation modes (Bell-Delaware).

### `AirCooler2`

Detailed air-cooler with fan curves and global weather hookup.

### `CopperBedHgAdsorber`

Copper-bed mercury removal (capacity-based or Wheeler-Jonas).

### `EnergyMixer`

Energy-stream mixer (sum or selectable inputs).

### `EnergySplitter`

Energy-stream splitter (split-ratio or fixed flow per output).

### `EnergyStreamSwitch`

Energy-stream switch - routes by an evaluated boolean expression.

### `FallingFilmEvaporator`

Falling-film evaporator with stage-wise vapour-fraction profile.

### `FiredHeater`

Fired-heater (radiant + convection sections, draft + emissions models).

### `MaterialStreamMapper`

Material-stream mapper / overrider (compounds, T, P, flow with custom units).

### `MaterialStreamSwitch`

Material-stream switch - routes by an evaluated boolean expression.

### `PipeNetwork`

Pipe-network solver (Simplex / Nelder-Mead) over connected Pipe / Pump / Valve / Node blocks.

### `ThermoPropertyEditor`

Thermodynamic property editor - overrides PP interaction parameters within a simulation.

### `VaporCompressionChiller`

Multi-stage vapor-compression chiller (1-3 stages, economizers, equipment sizing).

### `ZeoliteAdsorber`

Zeolite molecular-sieve adsorber (PSA or equilibrium).
