# Solver

```csharp
fs.Solve();                                  // throws on error
IReadOnlyList<Exception> errors = fs.TrySolve();   // returns the error list
```

## `Solve` semantics

- Walks the flowsheet in topological order, calculating each unit
  operation in turn.
- Routes through `Automation3.CalculateFlowsheet4` when the wrapped
  `IFlowsheet` is the headless `Automation3.Flowsheet2`. Otherwise falls
  through to the universal `DWSIM.FlowsheetSolver.FlowsheetSolver` — that
  's the path used when wrapping a live editor session, an extender host,
  or the AI-assistant flowsheet.
- On error, every solver exception (one per failing UO) is collected into a
  `FlowsheetSolveException : AggregateException`.

## `TrySolve`

When you'd rather inspect errors than catch:

```csharp
var errors = fs.TrySolve();
foreach (var ex in errors) Console.WriteLine(ex.Message);
```

`errors.Count == 0` means the run succeeded.

## Reading results

After `Solve`, every stream / UO read-back property is populated:

```csharp
var dist = fs.MaterialStream("distillate");
Console.WriteLine($"T = {dist.TemperatureK:F2} K");
Console.WriteLine($"n = {dist.MolarFlowMolPerSecond:F4} mol/s");
Console.WriteLine($"x(EtOH) = {dist.OverallMoleFraction("Ethanol"):F4}");

var heater = fs.AddHeater("H-1");   // re-fetched, same instance
Console.WriteLine($"duty = {heater.HeatDutyKW:F2} kW");
```

## Saving / loading solved flowsheets

```csharp
fs.Save(@"C:\runs\out.dwxmz");                   // compressed .dwxmz
fs.Save(@"C:\runs\out.dwxml", compressed: false); // plain .dwxml

var fs2 = Flowsheet.Load(@"C:\runs\out.dwxmz");
fs2.Solve();
```

## Dynamic simulation

`RunDynamics` returns a `DynamicsBuilder` that configures and runs a
time-domain integration on the flowsheet. The flowsheet must be loaded from a
`.dwxmz` file that contains at least one dynamics schedule created in DWSIM's
Dynamics Manager.

```csharp
var fs = Flowsheet.Load("plant.dwxmz");

// synchronous — blocks until integration finishes
var result = fs.RunDynamics("Default Schedule")
    .WithRealTime(false)
    .OnPreStep((s, e) =>
    {
        // perturb an input before each step
        var feed = (IMaterialStream)e.flowsheet.GetObject("Feed");
        feed.SetMassFlow(newFlow);
    })
    .OnPostStep((s, e) =>
    {
        Console.WriteLine($"t = {(e.tstamp - new DateTime()).TotalSeconds:F1} s");
    })
    .Execute();

// read the time-series data
foreach (var (varName, series) in result.MonitoredVariables)
{
    Console.WriteLine($"{varName}: {series.Count} points");
    Console.WriteLine($"  final value = {series[series.Count - 1].Value}");
}
```

`ExecuteAsync` is available for non-blocking use:

```csharp
var result = await fs.RunDynamics("Schedule A").ExecuteAsync();
```

`result.Completed` is `false` and `result.Error` is set when the integrator
throws. See [Dynamics](dynamics.md) for the full API.

## Recycle loops & convergence

The Fluent API does not introduce its own convergence machinery — the
underlying `FlowsheetSolver` handles tear streams, recycle blocks and
controllers exactly like the DWSIM editor. To wire a recycle, instantiate
the recycle object via the generic escape hatch (a typed builder is on the
roadmap) and connect it like any other UO:

```csharp
var rec = fs.AddUnitOperation(ObjectType.Recycle, "REC-1");
rec.ConnectFeed(loopBack).ConnectProduct(loopFwd);
```

See [example 16](../examples/16-recycle-loop.md).
