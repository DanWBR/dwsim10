# Builders.EventSetBuilder

`DWSIM.Automation.FluentAPI.Builders.EventSetBuilder`

Configures a set of timed disturbances applied to the flowsheet during a run: the step changes and ramps that make a dynamic simulation worth running. Obtain one from [`DefineEventSet`](dwsim-automation-fluentapi-builders-dynamicsconfigbuilder.md).

**Example**

```csharp
fs.Dynamics.DefineEventSet("Upsets")
  .AddStepChange("feed", "PROP_MS_2", 2.5, at: 60.Seconds(), units: "kg/s")
  .AddEvent("ramp the feed up")
      .At(120.Seconds())
      .ChangeProperty("feed", "PROP_MS_2", 5.0, "kg/s")
      .WithTransition(DynEnums.DynamicsEventTransitionType.LinearChange)
  .And();
```

## Methods

### `AddEvent(string)`

Adds an event and returns its builder. Chain `At` and `ChangeProperty` on it, then `And()` to come back here.

### `AddRamp(string, string, double, Quantity, string, string)`

Ramps a property linearly up to `value`, arriving at `at`.

**Remarks**

The ramp interpolates from the state recorded at its reference event — by default the previous event in the set, or the start of the run when there is none — up to its own instant. That recorded state is the one from *before* the reference event was applied, so a ramp placed after a step change on the same property ramps from the value the step replaced, not from the step's value. Ramps read the historian, so leave it enabled.

### `AddStepChange(string, string, double, Quantity, string, string)`

Sets a property to a new value the instant `at` is reached.

### `ClearEvents`

Removes every event from the set.

### `RemoveEvent(string)`

Removes the event with this description; does nothing when there is none.

## Properties

### `EventDescriptions`

The events in this set, ordered by time, as "t = 12 s: description".

### `Id`

The event set's internal ID.

### `Name`

The event set's description, which is how a schedule refers to it.

### `Object`

The underlying DWSIM event set.
