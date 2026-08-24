# Builders.DynamicEventBuilder

`DWSIM.Automation.FluentAPI.Builders.DynamicEventBuilder`

Configures a single timed event. Obtain one from [`AddEvent`](dwsim-automation-fluentapi-builders-eventsetbuilder.md).

## Methods

### `And`

Returns to the event set, to add the next event.

### `At(Quantity)`

Schedules the event at this point in simulated time, counted from the start of the run. An event at t = 0 fires on the first step.

### `At(TimeSpan)`

Schedules the event at this point in simulated time, counted from the start of the run.

### `ChangeProperty(string, string, double, string)`

Sets what the event does: assign `value` to a property of an object.

**Parameters**

- `objectTag` — Tag of the object to disturb.
- `propertyId` — Property identifier; use [`Properties`](dwsim-automation-fluentapi-flowsheet.md) to discover them.
- `value` — Target value, expressed in `units`.
- `units` — Units of `value`; the object's current display units when null.

### `Enabled(bool)`

Enables or disables the event without removing it from the set.

### `WithTransition(DWSIM.Interfaces.Enums.Dynamics.DynamicsEventTransitionType, DWSIM.Interfaces.Enums.Dynamics.DynamicsEventTransitionReferenceType, string)`

Chooses how the property gets to its new value, and what the transition starts from. Anything other than a step change interpolates from a past state, which needs the historian.

## Properties

### `Object`

The underlying DWSIM event.
