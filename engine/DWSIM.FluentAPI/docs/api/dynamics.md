# Dynamic Simulation

The Fluent API exposes DWSIM's time-domain integration engine through a single
entry point on `Flowsheet`:

```csharp
DynamicsBuilder RunDynamics(string scheduleName = null)
```

The flowsheet must be loaded from a `.dwxmz` / `.dwxml` file that already has
at least one dynamics schedule configured in DWSIM's Dynamics Manager. Dynamic
simulations cannot be built from scratch with `Flowsheet.Create()` — the
schedule, integrator settings, monitored variables, events and cause-and-effect
matrices are all configured inside DWSIM and saved in the file.

## `DynamicsBuilder` methods

| Method | Notes |
|---|---|
| `WithSchedule(string name)` | Select the schedule by its description. Defaults to the first schedule when omitted. |
| `WithRealTime(bool enabled = true)` | Pace each step to the wall clock when `true`; run as fast as possible when `false` (default). Real-time mode runs indefinitely — stop it by cancelling from a callback. |
| `OnPreStep(handler)` | Register a callback invoked **before** each step is solved. |
| `OnPostStep(handler)` | Register a callback invoked **after** each step completes. |
| `Execute()` | Run synchronously — blocks until the integration finishes and returns `DynamicsResult`. |
| `ExecuteAsync()` | Run asynchronously — returns `Task<DynamicsResult>`. |

Multiple `OnPreStep` / `OnPostStep` calls accumulate handlers (they are added,
not replaced).

## Callback signatures

```csharp
// Pre-step — modify flowsheet properties before the solver runs
void PreStepHandler(object sender, IntegratorPreStepEventArgs e)
// e.tstamp   — simulation DateTime (starts at new DateTime(), advances each step)
// e.tstep    — zero-based step index
// e.flowsheet — the IFlowsheet being integrated

// Post-step — read outputs or log data after the solver runs
void PostStepHandler(object sender, IntegratorPostStepEventArgs e)
// e.variables — List<IDynamicsMonitoredVariable> snapshots at this step
// e.tstamp, e.tstep, e.flowsheet — same as above
```

Both types live in `DWSIM.Automation.DynamicRunner`.

## `DynamicsResult`

| Member | Type | Description |
|---|---|---|
| `MonitoredVariables` | `IReadOnlyDictionary<string, IReadOnlyList<(double TimeSeconds, double Value)>>` | Time-series per variable. Key = variable description; value = ordered (simulation time in seconds from t=0, value in display units) pairs. |
| `Completed` | `bool` | `true` if integration ran to the end without error. |
| `Error` | `Exception` | The exception that stopped integration, or `null`. |

The monitored variables and their display units are configured in the DWSIM
Dynamics Manager inside the saved flowsheet file.

## Full example (C#)

```csharp
using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Interfaces;

Flowsheet.RegisterAssemblyResolver();
var fs = Flowsheet.Load(@"C:\simulations\tank_control.dwxmz");

var result = fs.RunDynamics("Default Schedule")
    .WithRealTime(false)
    .OnPreStep((s, e) =>
    {
        // step-change on inlet flow at t = 60 s
        double t = (e.tstamp - new DateTime()).TotalSeconds;
        if (t >= 60 && t < 61)
        {
            var inlet = (IMaterialStream)e.flowsheet.GetObject("Inlet");
            inlet.SetMassFlow(2.5);   // kg/s SI
        }
    })
    .OnPostStep((s, e) =>
    {
        double t = (e.tstamp - new DateTime()).TotalSeconds;
        Console.Write($"\rt = {t,6:F1} s");
    })
    .Execute();

Console.WriteLine();

if (!result.Completed)
{
    Console.WriteLine($"Integration failed: {result.Error!.Message}");
    return;
}

// print last-value summary
foreach (var (name, series) in result.MonitoredVariables)
{
    var last = series.Last();
    Console.WriteLine($"{name,-30} final = {last.Value:G6}  ({series.Count} pts)");
}

// export time series to CSV
using var csv = new System.IO.StreamWriter("dynamics_out.csv");
var headers = result.MonitoredVariables.Keys.ToList();
csv.WriteLine("t_s," + string.Join(",", headers));
int n = result.MonitoredVariables.Values.First().Count;
for (int i = 0; i < n; i++)
{
    double t = result.MonitoredVariables.Values.First()[i].TimeSeconds;
    var vals = headers.Select(h => result.MonitoredVariables[h][i].Value.ToString("G6"));
    csv.WriteLine($"{t:F2},{string.Join(",", vals)}");
}
```

## Python example

```python
import sys, clr
sys.path.append(r"C:\path\to\DWSIM\bin\x64\Debug")
clr.AddReference("DWSIM.Automation.FluentAPI")
clr.AddReference("DWSIM.Automation.DynamicRunner")

from System import DateTime
from DWSIM.Automation.FluentAPI import Flowsheet
from DWSIM.Automation.DynamicRunner import Runner

Flowsheet.RegisterAssemblyResolver()
fs = Flowsheet.Load(r"C:\simulations\tank_control.dwxmz")

epoch = DateTime()

def on_post(sender, e):
    t = (e.tstamp - epoch).TotalSeconds
    for v in e.variables:
        print(f"t={t:.1f}s  {v.Description} = {v.PropertyValue} {v.PropertyUnits}")

builder = (fs.RunDynamics("Default Schedule")
             .WithRealTime(False)
             .OnPostStep(Runner.IntegratorPostStepEventHandler(on_post)))

result = builder.Execute()

if result.Completed:
    for name, series in result.MonitoredVariables:
        print(f"{name}: {series.Count} points")
else:
    print(f"Error: {result.Error.Message}")
```

## Notes

- The `Runner` static events are shared process-wide. `DynamicsBuilder`
  attaches handlers before the run and detaches them in a `finally` block, so
  concurrent sequential calls are safe. Parallel concurrent calls on the same
  process are not supported by the underlying engine.
- Real-time mode (`WithRealTime(true)`) runs indefinitely. Use the `OnPreStep`
  callback to break out by raising an exception when a stop condition is met —
  `result.Error` will capture it and `result.Completed` will be `false`.
- `MonitoredVariables` values are already in the display units configured in
  DWSIM's Dynamics Manager (not necessarily SI).
