# Builders.CauseAndEffectItemBuilder

`DWSIM.Automation.FluentAPI.Builders.CauseAndEffectItemBuilder`

Configures a single interlock: an alarm condition and the property change it triggers. Obtain one from [`AddItem`](dwsim-automation-fluentapi-builders-causeandeffectmatrixbuilder.md).

## Methods

### `And`

Returns to the matrix, to add the next interlock.

### `Enabled(bool)`

Enables or disables the interlock without removing it.

### `Then(string, string, double, string)`

Sets what the interlock does when it fires: assign a value to a property.

### `WhenAlarm(string, DWSIM.Interfaces.Enums.Dynamics.DynamicsAlarmType)`

Sets the alarm that triggers this interlock, on an indicator object.

## Properties

### `Object`

The underlying DWSIM matrix item.
