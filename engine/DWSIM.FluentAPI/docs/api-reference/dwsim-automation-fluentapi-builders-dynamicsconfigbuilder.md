# Builders.DynamicsConfigBuilder

`DWSIM.Automation.FluentAPI.Builders.DynamicsConfigBuilder`

Entry point for configuring a flowsheet's dynamic simulation: integrators, schedules, event sets and cause-and-effect matrices. Obtain one from [`Dynamics`](dwsim-automation-fluentapi-flowsheet.md).

## Remarks

Every `Define*` method is idempotent by name: it creates the object on first call and returns the existing one afterwards, so a configuration script can be re-run safely.

**Example**

```csharp
fs.Dynamics
  .DefineIntegrator("Fast")
      .WithIntegrationStep(1.Seconds())
      .WithDuration(10.Minutes())
      .Monitor("TK-01", "Liquid Level", "m");

fs.Dynamics.DefineSchedule("Startup").WithIntegrator("Fast").MakeCurrent();
```

## Methods

### `CauseAndEffectMatrix(string)`

Returns a builder for an existing matrix; throws when there is none by that name.

### `DefineCauseAndEffectMatrix(string)`

Creates a cause-and-effect matrix with this description, or returns the existing one.

### `DefineEventSet(string)`

Creates an event set with this description, or returns the existing one.

### `DefineIntegrator(string)`

Creates an integrator with this description, or returns the existing one.

### `DefineSchedule(string)`

Creates a schedule with this description, or returns the existing one.

### `EnableDynamicMode(bool)`

Turns dynamic mode on or off, and makes sure every object has its dynamic properties.

### `EventSet(string)`

Returns a builder for an existing event set; throws when there is none by that name.

### `Integrator(string)`

Returns a builder for an existing integrator; throws when there is none by that name.

### `Schedule(string)`

Returns a builder for an existing schedule; throws when there is none by that name.

### `StoreCurrentStateAs(string)`

Stores the flowsheet's current state under `stateId`, so a schedule can start every run from it. See [`WithInitialState`](dwsim-automation-fluentapi-builders-schedulebuilder.md).

### `UseSchedule(string)`

Makes this schedule the current one, which is what a run defaults to.

### `WithHistorian(bool, int)`

Controls the state historian. It is what event transitions other than step changes interpolate from, at the cost of a compressed snapshot per integration step.

## Properties

### `CauseAndEffectMatrixNames`

Descriptions of every cause-and-effect matrix defined on this flowsheet.

### `EventSetNames`

Descriptions of every event set defined on this flowsheet.

### `IntegratorNames`

Descriptions of every integrator defined on this flowsheet.

### `ScheduleNames`

Descriptions of every schedule defined on this flowsheet.
