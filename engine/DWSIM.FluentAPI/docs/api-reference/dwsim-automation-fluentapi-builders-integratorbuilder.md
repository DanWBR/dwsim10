# Builders.IntegratorBuilder

`DWSIM.Automation.FluentAPI.Builders.IntegratorBuilder`

Configures a dynamic integrator: time step, duration, numerical method, calculation rates and the variables whose time series the run records. Obtain one from [`DefineIntegrator`](dwsim-automation-fluentapi-builders-dynamicsconfigbuilder.md).

## Methods

### `ClearMonitoredVariables`

Drops every monitored variable, so a reconfiguration starts from a clean list.

### `Configure(Action{DWSIM.Interfaces.IDynamicsIntegrator})`

Escape hatch: applies an arbitrary mutation to the underlying integrator.

### `FriendlyName(DWSIM.Interfaces.ISimulationObject, string)`

The most readable name a property has. Dynamic properties are already named in words; regular ones carry a description, except when the object never wrote one, in which case the identifier beats the placeholder text.

### `Monitor(string, string, string, string, double, double)`

Records a property's time series over the run. Only monitored variables end up in [`DynamicsResult`](dwsim-automation-fluentapi-dynamicsresult.md), in the chart and in the spreadsheet.

**Parameters**

- `objectTag` — Tag of the object holding the property.
- `propertyId` — Property identifier, e.g. `"PROP_MS_2"` or a dynamic property name like `"Liquid Level"`. Use [`Properties`](dwsim-automation-fluentapi-flowsheet.md) to discover them.
- `units` — Display units; taken from the object's current unit system when null.
- `description` — Series name; defaults to `"tag - property description"`.
- `axisMin` — Lower chart axis bound; leave both bounds at 0 to autoscale.
- `axisMax` — Upper chart axis bound.

### `MonitorAll(string, string[])`

Records several properties of the same object, using default units and names.

### `WithAdaptiveStep(bool, Nullable{Quantity}, Nullable{Quantity})`

Enables adaptive step sizing, with optional bounds. Only `AdaptiveRK45` varies the step; the bounds are what it varies between.

### `WithCalculationRates(int, int, int)`

Sets how often each subsystem is recalculated, in integration steps. Raising the equilibrium rate is the usual way to speed up a run whose flashes dominate the cost.

### `WithConvergence(double, int)`

Sets the inner-loop convergence criteria used by the implicit method.

### `WithDuration(Quantity)`

Sets how much simulated time a run covers.

### `WithDuration(TimeSpan)`

Sets how much simulated time a run covers.

### `WithErrorTolerance(double)`

Sets the relative error tolerance used by the adaptive and step-doubling methods.

### `WithIntegrationStep(Quantity)`

Sets the integration time step.

### `WithIntegrationStep(TimeSpan)`

Sets the integration time step.

### `WithMethod(DWSIM.Interfaces.Enums.Dynamics.IntegrationMethod)`

Selects the numerical integration method.

### `WithRealTimeStep(int)`

Sets the wall-clock pace of a real-time run, in milliseconds per step.

## Properties

### `Id`

The integrator's internal ID.

### `MonitoredVariableNames`

The monitored variables currently configured, as "description (units)".

### `Name`

The integrator's description, which is how schedules and runs refer to it.

### `Object`

The underlying DWSIM integrator.
