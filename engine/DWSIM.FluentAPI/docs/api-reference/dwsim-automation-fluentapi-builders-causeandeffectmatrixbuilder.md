# Builders.CauseAndEffectMatrixBuilder

`DWSIM.Automation.FluentAPI.Builders.CauseAndEffectMatrixBuilder`

Configures a cause-and-effect matrix: the interlocks that fire when an indicator's alarm goes active during a run. Obtain one from [`DefineCauseAndEffectMatrix`](dwsim-automation-fluentapi-builders-dynamicsconfigbuilder.md).

**Example**

```csharp
fs.Dynamics.DefineCauseAndEffectMatrix("Trips")
  .AddItem("close the feed valve on high level")
      .WhenAlarm("LI-01", DynEnums.DynamicsAlarmType.HH)
      .Then("V-01", "Opening Setpoint", 0.0, "%")
  .And();
```

## Methods

### `AddItem(string)`

Adds an interlock and returns its builder. Chain `WhenAlarm` and `Then`, then `And()`.

### `RemoveItem(string)`

Removes the interlock with this description; does nothing when there is none.

## Properties

### `Id`

The matrix's internal ID.

### `ItemDescriptions`

Descriptions of every interlock in the matrix.

### `Name`

The matrix's description, which is how a schedule refers to it.

### `Object`

The underlying DWSIM matrix.
