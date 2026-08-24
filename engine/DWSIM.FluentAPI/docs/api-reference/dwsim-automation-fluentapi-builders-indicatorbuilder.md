# Builders.IndicatorBuilder

`DWSIM.Automation.FluentAPI.Builders.IndicatorBuilder`

Fluent builder for an indicator: it reads one property and raises alarms on it. Alarms are what a cause-and-effect matrix reacts to, so an interlock needs an indicator first.

**Example**

```csharp
fs.AddIndicator("LI-01", IndicatorKind.Level)
  .Reads("TK-01", "Liquid Level", "m")
  .WithRange(0.0, 2.0)
  .WithAlarms(veryLow: 0.1, low: 0.3, high: 1.7, veryHigh: 1.9);
```

## Methods

### `Reads(string, string, string)`

Points the indicator at a property of an object.

### `WithAlarms(Nullable{double}, Nullable{double}, Nullable{double}, Nullable{double})`

Sets the alarm thresholds. Passing null leaves a level disabled; each supplied value enables its level.

### `WithDigits(int, int)`

Sets how many digits the readout shows either side of the decimal point.

### `WithRange(double, double)`

Sets the indicator's scale, in the read property's display units.

## Properties

### `CurrentValue`

The value read at the last solved step, in display units.

### `Indicator`

The underlying indicator.
