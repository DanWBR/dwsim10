# 17 — Dynamic Simulation

Fills a tank through a shut valve and checks the level against a mass balance you can do on
paper. It is the smallest example with a real dynamic unit operation in the loop, so it shows
the three things a dynamic run needs that a steady-state one does not: pressure-flow
specifications, a hold-up with somewhere to put it, and monitored variables.

## The flowsheet

```csharp
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Dynamics;
using Valve = DWSIM.UnitOperations.UnitOperations.Valve;

var fs = Flowsheet.Create("Tank filling")
    .WithCompound("Water")
    .WithPropertyPackage(PropertyPackages.SteamTables);

// A feed is specified by flow: its rate is held, and the network resolves its pressure.
var feed = fs.AddMaterialStream("feed")
    .At(25.Celsius(), 1.Atm())
    .WithMassFlow(1.KgPerSecond())
    .AsFlowSpec();

// The tank only integrates its hold-up when its outlet is specified by pressure.
var tankOutlet = fs.AddMaterialStream("tank-outlet").At(25.Celsius(), 1.Atm()).AsPressureSpec();
var product = fs.AddMaterialStream("product").At(25.Celsius(), 1.Atm()).AsPressureSpec();

fs.AddTank("TK-01")
    .WithVolume(2.CubicMeters())
    .WithHeight(2.Meters())
    .InitializeFromInlet(false)
    .ResetContent()
    .ConnectFeed(feed, 0)
    .ConnectProduct(tankOutlet, 0);

// A Kv calculation mode is what lets the valve compute its own flow from the pressure either
// side of it. In the pressure-drop modes it would demand a flow specification on one side, and
// a shut valve could not then hold the flow at zero.
var valve = fs.AddValve("V-01")
    .WithCalcMode(Valve.CalculationMode.Kv_Liquid)
    .WithKv(10.0)
    .WithOpeningKvRelationship()   // without this the valve passes full Kv at any opening
    .WithOpeningPercent(100.0)
    .ConnectFeed(tankOutlet, 0)
    .ConnectProduct(product, 0);

fs.AutoLayout();
fs.Solve();
```

The valve is open for the steady-state solve, because a shut valve has no steady state to speak
of. Shutting it is the disturbance the run is about:

```csharp
valve.WithOpeningPercent(0.0).WithOpeningSetpoint(0.0);

// The tank integrates against whatever its outlet says, and it runs before the valve does.
// Left at the steady-state rate, the outlet would cancel the first step's accumulation exactly.
tankOutlet.WithMassFlow(0.0.KgPerSecond());
```

## The schedule

```csharp
fs.Dynamics.DefineIntegrator("Filling")
    .WithIntegrationStep(1.Seconds())
    .WithDuration(300.Seconds())
    .Monitor("TK-01", "Liquid Level")
    .Monitor("feed", "PROP_MS_2", "kg/s");

fs.Dynamics.DefineSchedule("Filling run").WithIntegrator("Filling").MakeCurrent();
```

Check before running. It costs nothing and every finding carries a fix:

```csharp
foreach (var f in DynamicsDiagnostics.CheckReady(fs.Inner, "Filling run"))
    Console.WriteLine(f);
```

## The run

```csharp
var result = fs.RunDynamics("Filling run").Execute();

if (!result.Completed)
{
    foreach (var f in DynamicsDiagnostics.Diagnose(fs.Inner, result)) Console.WriteLine(f);
    return;
}

Console.WriteLine(result);
// Dynamics run on 'Filling run' completed: 301 steps, 301 s simulated in 4.2 s,
// 2 monitored variable(s).
```

## Checking the answer

One kilogram a second for 300 seconds is 300 kg of water. At about 997 kg/m³ that is 0.3009 m³,
and the tank's cross-section is volume over height — 1 m² — so the level is the volume:

```csharp
var level = result["TK-01 Liquid Level"];

var density = feed.Object.Phases[0].Properties.density.GetValueOrDefault();
var expected = 1.0 * 300.0 / density / (2.0 / 2.0);

Console.WriteLine($"expected {expected:F4} m, got {level.Final:F4} m");
// expected 0.3009 m, got 0.3019 m

result.ToCsv("filling.csv");
```

Within a third of a percent, which is the integration error of a 1 s explicit Euler step over
five minutes. Shorten the step and it closes further.

## What the series knows

`DynamicsSeries` carries the control metrics, so a level under control needs no extra
arithmetic:

```csharp
Console.WriteLine($"settled at {level.SteadyState():F3} m");
Console.WriteLine($"overshoot  {level.Overshoot(1.0):F1} %");
Console.WriteLine($"settling   {level.SettlingTime(0.02):F0} s");
Console.WriteLine($"IAE        {level.IAE(1.0):G4}");

if (level.IsOscillating(out var period, out var decay))
    Console.WriteLine($"oscillating, period {period:F1} s, decay ratio {decay:F2}");
```

Add a controller and those numbers are what tells you whether it is any good — and
`PidTuner.Tune` minimises exactly them.

## See also

- [Dynamic Simulation](../api/dynamics.md) — the full API
- [Dynamic Simulation for the AI Assistant](../ai/dynamics.md) — the same capability over MCP and HTTP
