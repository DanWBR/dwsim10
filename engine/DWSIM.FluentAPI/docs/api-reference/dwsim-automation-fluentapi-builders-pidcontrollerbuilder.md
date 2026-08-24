# Builders.PIDControllerBuilder

`DWSIM.Automation.FluentAPI.Builders.PIDControllerBuilder`

Fluent builder for a PID controller. A controller needs three things before it can run: the variable it reads ([`Controls`](dwsim-automation-fluentapi-builders-pidcontrollerbuilder.md)), the one it writes ([`Manipulates`](dwsim-automation-fluentapi-builders-pidcontrollerbuilder.md)), and a setpoint.

**Example**

```csharp
fs.AddPIDController("LIC-01")
  .Controls("TK-01", "Liquid Level", "m")
  .Manipulates("V-01", "Opening", "%")
  .WithSetPoint(1.0)
  .WithTuning(kp: 5.0, ki: 0.5, kd: 0.0)
  .WithOutputLimits(0.0, 100.0);
```

## Methods

### `Active(bool)`

Takes the controller in or out of service.

### `CascadeFrom(string)`

Takes the setpoint from a master controller's output, forming a cascade.

### `Controls(string, string, string)`

Sets the process variable: what the controller reads and tries to hold at setpoint.

### `Manipulates(string, string, string)`

Sets the manipulated variable: what the controller writes to.

### `ManualOverride(bool, double)`

Puts the controller in manual, holding its output at a fixed value.

### `References(string, string, string)`

Sets the disturbance variable read by the feedforward term.

### `ReverseActing(bool)`

Reverses the control action. Get this backwards and the controller drives the error up instead of down, which looks exactly like a diverging simulation.

### `WithDerivativeFilter(double, bool)`

Filters the derivative term, and optionally takes it on the PV instead of the error.

### `WithExecutionOrder(int)`

Sets the order in which this controller runs relative to the others, low first.

### `WithFeedforward(double, Quantity, Quantity)`

Configures the feedforward term acting on the disturbance set by [`References`](dwsim-automation-fluentapi-builders-pidcontrollerbuilder.md).

### `WithOffset(double)`

Sets the bias added to the controller output.

### `WithOutputLimits(double, double)`

Clamps the controller output. These are the manipulated variable's physical bounds — a valve opening cannot leave 0-100 % — and they also bound the anti-windup.

### `WithSetPoint(double)`

Sets the setpoint, in the controlled property's display units.

### `WithSetpointWeights(double, double)`

Sets the setpoint weights of the proportional and derivative terms.

### `WithTuning(double, double, double)`

Sets the proportional, integral and derivative gains.

### `WithWindupGuard(double)`

Sets the integral anti-windup guard.

## Properties

### `CumulativeError`

The accumulated error over the run; the objective the PID tuner minimises.

### `ManipulatedVariable`

The manipulated variable at the last solved step, in display units.

### `Output`

The controller output at the last solved step.

### `ProcessVariable`

The process variable at the last solved step, in display units.

### `SetPoint`

The setpoint, in display units.
