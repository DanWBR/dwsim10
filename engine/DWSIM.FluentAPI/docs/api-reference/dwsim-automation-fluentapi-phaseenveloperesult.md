# PhaseEnvelopeResult

`DWSIM.Automation.FluentAPI.PhaseEnvelopeResult`

Result of a phase envelope calculation for a multicomponent mixture. All temperatures in K, pressures in Pa, enthalpies in kJ/kg, entropies in kJ/(kg*K), volumes in m3/kg.

## Properties

### `BubbleEnthalpies`

Bubble curve enthalpies.

### `BubbleEnthalpies_L2`

Second liquid phase (L2) bubble enthalpies.

### `BubbleEnthalpies_L3`

Third liquid phase (L3) bubble enthalpies.

### `BubbleEntropies`

Bubble curve entropies.

### `BubbleEntropies_L2`

Second liquid phase (L2) bubble entropies.

### `BubbleEntropies_L3`

Third liquid phase (L3) bubble entropies.

### `BubblePressuresPa`

Bubble curve pressures (Pa).

### `BubblePressuresPa_L2`

Second liquid phase (L2) bubble pressures (Pa).

### `BubblePressuresPa_L3`

Third liquid phase (L3) bubble pressures (Pa).

### `BubbleTemperaturesK`

Bubble curve temperatures (K).

### `BubbleTemperaturesK_L2`

Second liquid phase (L2) bubble temperatures (K). Populated when liquid instability is detected.

### `BubbleTemperaturesK_L3`

Third liquid phase (L3) bubble temperatures (K).

### `BubbleVolumes`

Bubble curve specific volumes.

### `BubbleVolumes_L2`

Second liquid phase (L2) bubble volumes.

### `BubbleVolumes_L3`

Third liquid phase (L3) bubble volumes.

### `CriticalPoints`

Critical point(s) identified on the envelope.

### `DewEnthalpies`

Dew curve enthalpies.

### `DewEntropies`

Dew curve entropies.

### `DewPressuresPa`

Dew curve pressures (Pa).

### `DewTemperaturesK`

Dew curve temperatures (K).

### `DewVolumes`

Dew curve specific volumes.

### `QualityPressuresPa`

Quality line pressures (Pa). Populated when `QualityLine = true`.

### `QualityTemperaturesK`

Quality line temperatures (K). Populated when `QualityLine = true`.

### `SLE_PressuresPa_1`

Solid-liquid equilibrium pressures, first curve (Pa).

### `SLE_PressuresPa_2`

Solid-liquid equilibrium pressures, second curve (Pa).

### `SLE_TemperaturesK_1`

Solid-liquid equilibrium temperatures, first curve (K).

### `SLE_TemperaturesK_2`

Solid-liquid equilibrium temperatures, second curve (K).

### `StabilityPressuresPa`

Phase stability/instability curve pressures (Pa).

### `StabilityTemperaturesK`

Phase stability/instability curve temperatures (K).

### `WidomBetaT_PressuresPa`

Widom line (isothermal compressibility-based) pressures (Pa).

### `WidomBetaT_TemperaturesK`

Widom line (isothermal compressibility-based) temperatures (K).

### `WidomCp_PressuresPa`

Widom line (Cp-based) pressures (Pa).

### `WidomCp_TemperaturesK`

Widom line (Cp-based) temperatures (K).
