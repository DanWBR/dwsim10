# Dynamic Simulation

A dynamic simulation integrates the flowsheet forward in time: hold-up accumulates, valves
open and close, controllers act, and every monitored variable leaves a time series behind.

Everything the Dynamics Manager holds is reachable from code. A schedule can be built from
nothing, run, and scored without the flowsheet ever being opened in DWSIM.

```csharp
var fs = Flowsheet.Create("Tank level")
    .WithCompound("Water")
    .WithPropertyPackage(PropertyPackages.SteamTables);

// ... build the flowsheet and solve it at steady state ...

fs.Dynamics.DefineIntegrator("Fast")
    .WithIntegrationStep(1.Seconds())
    .WithDuration(10.Minutes())
    .Monitor("TK-01", "Liquid Level", "m");

fs.Dynamics.DefineEventSet("Upsets")
    .AddStepChange("feed", "PROP_MS_2", 2.5, at: 60.Seconds(), units: "kg/s");

fs.Dynamics.DefineSchedule("Startup")
    .WithIntegrator("Fast")
    .WithEventSet("Upsets")
    .MakeCurrent();

var result = fs.RunDynamics().Execute();

var level = result["TK-01 Liquid Level"];
Console.WriteLine($"settled at {level.SteadyState():F3} m, " +
                  $"{level.Overshoot(1.0):F1} % overshoot");
```

## Before a run

A dynamic run integrates forward from wherever the flowsheet is, so it needs a solved steady
state to start from, at least one monitored variable, and a well-posed pressure-flow network.

`DynamicsDiagnostics.CheckReady` answers all of that in one call, and every finding carries a
fix:

```csharp
foreach (var finding in DynamicsDiagnostics.CheckReady(fs.Inner))
    Console.WriteLine(finding);

// [BLOCKER] NO_MONITORED_VARS: Integrator 'Fast' records no variables, so the run will
//           produce no series. Fix: Add some: Dynamics.Integrator(name).Monitor(tag, propertyId).
```

### The pressure-flow network

Each material stream is specified either by pressure or by flow, and the network needs both
kinds. A feed is normally specified by flow and a boundary by pressure:

```csharp
feed.AsFlowSpec();
product.AsPressureSpec();
```

Get this wrong and unit operations either refuse to run — a valve will not accept pressure on
both sides unless it is in a Kv calculation mode — or quietly hold nothing up.

### Property identifiers

Monitored variables, events and controllers all address properties by identifier, and those
are not guessable. Ask:

```csharp
foreach (var p in fs.MonitorableProperties("feed"))
    Console.WriteLine($"{p.Id}  {p.Description}  ({p.Units})");

// PROP_MS_2  Mass Flow  (kg/h)
```

`fs.DynamicProperties(tag)` lists the dynamic-mode ones — a vessel's `"Volume"`, a valve's
`"Opening Setpoint"` — which are named in words rather than by identifier.

## Configuration

### `Flowsheet.Dynamics`

| Method | Notes |
|---|---|
| `DefineIntegrator(name)` | Creates or returns an integrator. Idempotent by name. |
| `DefineSchedule(name)` | Creates or returns a schedule. |
| `DefineEventSet(name)` | Creates or returns an event set. |
| `DefineCauseAndEffectMatrix(name)` | Creates or returns an interlock matrix. |
| `UseSchedule(name)` | Makes a schedule current, which is what a run defaults to. |
| `StoreCurrentStateAs(id)` | Stores the current state, for a schedule to start from. |
| `WithHistorian(enabled, maxItems)` | Controls the state history that event transitions interpolate from. |
| `EnableDynamicMode(enabled)` | Turns dynamic mode on and creates the objects' dynamic properties. |

Every `Define*` is idempotent, so a configuration script can be re-run safely.

### `IntegratorBuilder`

| Method | Notes |
|---|---|
| `WithIntegrationStep(t)` | The time step. |
| `WithDuration(t)` | How much simulated time a run covers. |
| `WithMethod(m)` | `ExplicitEuler`, `RungeKutta4`, `ImplicitEuler` or `AdaptiveRK45`. |
| `WithAdaptiveStep(enabled, min, max)` | Step bounds for the adaptive method. |
| `WithErrorTolerance(tol)` | Relative error tolerance. |
| `WithConvergence(tol, maxIterations)` | Inner-loop criteria for the implicit method. |
| `WithCalculationRates(equilibrium, pressureFlow, control)` | How often each subsystem is recalculated, in steps. Raising the equilibrium rate is the usual way to speed up a flash-bound run. |
| `Monitor(tag, propertyId, units, description)` | Records a property's time series. |
| `MonitorAll(tag, params propertyIds)` | Records several at once. |
| `ClearMonitoredVariables()` | Starts the list over. |

### `ScheduleBuilder`

`WithIntegrator(name)`, `WithEventSet(name)`, `WithCauseAndEffectMatrix(name)`,
`UseCurrentStateAsInitial(bool)`, `WithInitialState(storedStateId)`,
`ResetContentsOfAllObjects(bool)`, `MakeCurrent()`, `Run()`.

A schedule that starts from a stored state gives repeatable runs, which is what makes two runs
comparable — and what PID tuning depends on.

### Events

```csharp
fs.Dynamics.DefineEventSet("Upsets")
    .AddStepChange("feed", MassFlow, 25.0, at: 60.Seconds(), units: "kg/s")
    .AddEvent("ramp the feed back down")
        .At(300.Seconds())
        .ChangeProperty("feed", MassFlow, 10.0, "kg/s")
        .WithTransition(DynEnums.DynamicsEventTransitionType.LinearChange)
    .And();
```

A **step change** holds the old value until its instant, then jumps.

A **ramp** interpolates from the state recorded at its reference event — by default the
previous event in the set, or the start of the run when there is none — up to its own instant.
That recorded state is the one from *before* the reference event was applied, so a ramp placed
after a step change on the same property ramps from the value the step replaced, not from the
step's value. Ramps read the historian, so leave it enabled.

### Controllers

```csharp
fs.AddPIDController("LIC-01")
  .Controls("TK-01", "Liquid Level", "m")
  .Manipulates("V-01", "Opening", "%")
  .WithSetPoint(1.0)
  .WithTuning(kp: 5.0, ki: 0.5, kd: 0.0)
  .WithOutputLimits(0.0, 100.0);
```

A controller that manipulates a valve opening needs the valve's flow coefficient to follow that
opening, or closing it changes nothing:

```csharp
fs.AddValve("V-01")
  .WithCalcMode(Valve.CalculationMode.Kv_Liquid)
  .WithKv(10.0)
  .WithOpeningKvRelationship();
```

## Running

`Flowsheet.RunDynamics(scheduleName)` returns a `DynamicsBuilder`. The schedule is matched by
description — ignoring case and surrounding whitespace — or by ID; omit it and the flowsheet's
current schedule is used.

| Method | Notes |
|---|---|
| `WithSchedule(name)` | Select the schedule. |
| `WithRealTime(enabled)` | Pace each step to the wall clock. Real-time runs continue until stopped. |
| `WithHistorian(enabled)` | Snapshot every step. Ramps need it; turning it off makes long runs faster. |
| `WithCancellation(token)` | Stop at the next step boundary. |
| `WithMaxSteps(n)`, `WithMaxWallTime(t)` | Bound the run. |
| `StopWhen((fs, t) => …)` | Stop cleanly on a condition. |
| `FailIfBusy()` | Fail rather than wait when another integration is running. |
| `OnProgress(handler)`, `OnPreStep(handler)`, `OnPostStep(handler)` | Callbacks. |
| `Execute()` / `ExecuteAsync()` | Run, returning a `DynamicsResult`. |

Integration drives process-wide solver state, so runs are serialised: a second one waits for the
first, or fails immediately with `FailIfBusy()`.

## Results

`DynamicsResult` carries `Series`, `Completed`, `Aborted`, `Errors`, `Steps`,
`FinalTimeSeconds`, `WallClock`, and `ToCsv(path)`. Look a series up by description, by
monitored-variable ID, or by `"tag.PropertyId"`.

`DynamicsSeries` carries the samples and the metrics a control engineer would compute by hand:

| Member | Notes |
|---|---|
| `Initial`, `Final`, `Min`, `Max`, `Count` | The basics. |
| `ValueAt(t)` | Linear interpolation. |
| `SteadyState(lastFraction)` | Mean over the tail, which beats reading the last point. |
| `Overshoot(sp)`, `PeakTime(sp)` | Peak past the setpoint, as a percentage of the step. |
| `RiseTime(low, high)` | 10 % to 90 % by default. |
| `SettlingTime(band)` | When it last leaves the band around the settled value. |
| `Offset(sp)` | Steady-state error. |
| `IAE(sp)`, `ISE(sp)`, `ITAE(sp)` | Error integrals. |
| `IsOscillating(out period, out decay)` | Period and decay ratio. |
| `HasConverged(tol)`, `HasDiverged` | Whether it settled, or ran away. |
| `SaturationFraction(min, max)` | How much of the run an actuator sat at its limit. |

`DynamicsSeries.FromSamples` applies the same metrics to data that did not come from a run —
plant history, a spreadsheet, another simulator.

Values are in the display units configured for each monitored variable, which are not
necessarily SI.

## Diagnosis

`DynamicsDiagnostics.Diagnose(flowsheet, result)` explains a run that misbehaved: solver
failures mapped to the object that raised them, NaNs, divergence, sustained oscillation,
saturated controllers, integration steps too large for the transient, and controllers whose
action is reversed.

That last one is worth knowing about. A controller wired backwards moves its output in the
direction that increases the error, which on a chart looks exactly like a diverging model:

```
[WARNING] PID_ACTION_INVERTED (LIC-01): The controller moved its output in the direction
          that increases the error on 97.3 % of its moves. Fix: Flip ReverseActing on this
          controller.
```

`DiagnosticCodes.All` lists every code with a one-line explanation.

## Tuning

`PidTuner.Tune` searches the controllers' gains with a Nelder-Mead simplex, running the whole
schedule once per trial and scoring the transient:

```csharp
var tuning = PidTuner.Tune(fs.Inner, new PidTuningOptions
{
    ControllerTags = new[] { "LIC-01" },
    Objective = TuningObjective.IAE,
    MaxEvaluations = 30
});

Console.WriteLine($"{tuning.ImprovementPercent:F1} % better");
foreach (var c in tuning.Controllers) Console.WriteLine(c);
```

Trials are only comparable if they all start from the same state, so the schedule needs a stored
initial state. When it has none, the tuner captures the flowsheet as it stands, uses that, and
puts the schedule's configuration back afterwards.

## Reading a run in a chat or a script

A run of any length produces far more samples than a person — or a language model — wants at
once. `SeriesDecimator` reduces a series to a few dozen points that still look like the
original, using largest-triangle-three-buckets plus the forced extremes, so the overshoot peak
survives:

```csharp
var (times, values) = SeriesDecimator.Preview(series, maxPoints: 40);
```

## Python

```python
import sys, clr
sys.path.append(r"C:\path\to\DWSIM\bin\x64\Debug")
clr.AddReference("DWSIM.Automation.FluentAPI")

from DWSIM.Automation.FluentAPI import Flowsheet, Q
from System import TimeSpan

Flowsheet.RegisterAssemblyResolver()
fs = Flowsheet.Load(r"C:\simulations\tank_control.dwxmz")

fs.Dynamics.DefineIntegrator("Fast") \
    .WithIntegrationStep(TimeSpan.FromSeconds(1)) \
    .WithDuration(TimeSpan.FromMinutes(10)) \
    .Monitor("TK-01", "Liquid Level", "m")

fs.Dynamics.DefineSchedule("Startup").WithIntegrator("Fast").MakeCurrent()

result = fs.RunDynamics().Execute()

if result.Completed:
    level = result.GetSeries("TK-01 Liquid Level")
    print(f"settled at {level.SteadyState():.3f} m over {level.Count} points")
    result.ToCsv(r"C:\simulations\level.csv")
else:
    print(f"failed: {result.Error.Message}")
```

pythonnet does not surface C# extension methods, so call the unit helpers as statics —
`Q.Seconds(60.0)` — or pass a `TimeSpan` directly.
