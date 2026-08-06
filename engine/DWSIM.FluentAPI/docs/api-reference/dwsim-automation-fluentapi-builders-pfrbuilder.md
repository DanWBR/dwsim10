# Builders.PFRBuilder

`DWSIM.Automation.FluentAPI.Builders.PFRBuilder`

Fluent builder for the PFR unit operation. Call [`AddPFR`](dwsim-automation-fluentapi-flowsheet.md) to obtain one.

## Methods

### `WithVolume(Quantity)`

Sets `Volume` (SI) and returns this builder for chaining.

## Properties

### `Profile`

Composition/temperature/pressure profile along the reactor length. Each element is (Position_m, Temperature_K, Pressure_Pa, List<ProfileItem>). Null or empty if not yet calculated.

### `ProfilePointCount`

Number of axial points in the profile (0 if not yet calculated).
