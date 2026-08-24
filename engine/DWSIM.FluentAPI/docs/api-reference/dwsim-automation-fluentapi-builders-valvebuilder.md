# Builders.ValveBuilder

`DWSIM.Automation.FluentAPI.Builders.ValveBuilder`

Fluent builder for the Valve unit operation. Call [`AddValve`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithActuatorDelay(Quantity)`

Sets the actuator dead time. Zero means the opening tracks the setpoint immediately.

### `WithActuatorTimeConstant(Quantity)`

Sets the first-order lag of the actuator response.

### `WithCalcMode(DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode)`

Sets `Calc Mode` and returns this builder for chaining.

### `WithKv(double)`

Sets `Kv` and returns this builder for chaining.

### `WithOpeningKvRelationship(DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType, double)`

Makes the effective flow coefficient follow the stem opening along a characteristic curve. Without this the valve passes its full Kv at any opening, so a shut valve still flows — which matters as soon as a controller starts moving the opening.

**Parameters**

- `characteristic` — The curve relating opening to flow coefficient.
- `rangeability` — Rangeability used by the equal-percentage curve.

### `WithOpeningPercent(double)`

Sets `Opening Percent` and returns this builder for chaining.

### `WithOpeningSetpoint(double)`

Sets the opening the actuator drives towards. This is what a level or flow controller manipulates; the actual opening follows it through the delay and time constant below.

### `WithOutletPressure(Quantity)`

Sets `Outlet Pressure` (SI) and returns this builder for chaining.

### `WithPressureDrop(Quantity)`

Sets `Pressure Drop` (SI) and returns this builder for chaining.

### `WithPressureRecoveryFactor(double)`

Sets the liquid pressure recovery factor FL, which fixes the cavitation threshold.

## Properties

### `CavitationAlarm`

True when the last dynamic step detected cavitation across the valve.
