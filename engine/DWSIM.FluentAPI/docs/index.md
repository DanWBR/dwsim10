# DWSIM Fluent API

A **fluent, strongly-typed** automation surface for [DWSIM](https://dwsim.org) — the
open-source chemical process simulator. The same `DWSIM.Automation.FluentAPI.dll`
drives flowsheets from **C#**, **VB.NET** and **Python** (via
[pythonnet](https://pythonnet.github.io)), so you can move between languages
without rewriting your simulation logic.

## Why use it?

- **Readable**: `fs.AddPump("P-1").WithOutletPressure(10.Bar()).WithEfficiencyPercent(75)` reads top-to-bottom like the P&ID it represents.
- **SI inside**: numeric quantities are converted at the call site
  (`300.Kelvin()`, `100.KgPerSecond()`, `10.Bar()`); DWSIM only ever sees SI.
- **Builder per UO**: every unit operation (heater, pump, distillation, FCC,
  bioreactor, RO, electrolyzer, …) has a typed builder with `WithX` setters and
  read-back properties populated after `Solve`.
- **Patron-aware**: Plus components (refining shortcuts, electrolyte ops,
  fired heater, advanced HX, LCA, TEA, ThermoPack, Reaktoro) are gated by
  [`License`](api/license.md) — you call `License.CheckLicense(key)` once and
  the rest of the API stops throwing.
- **Headless or live**: `Flowsheet.Create()` allocates a fresh headless
  flowsheet for batch / CI / unit tests; `Flowsheet.Wrap(existing)` scripts an
  open DWSIM editing session, an extender plugin or the AI-assistant host.

## Hello, flowsheet

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("Hello")
        .WithCompound("Water")
        .WithPropertyPackage(PropertyPackages.SteamTables);

    var feed = fs.AddMaterialStream("feed")
        .At(300.Kelvin(), 1.Atm())
        .WithMassFlow(10.KgPerSecond());

    var hot = fs.AddMaterialStream("hot");

    fs.AddHeater("H-1")
      .WithOutletTemperature(400.Kelvin())
      .ConnectFeed(feed)
      .ConnectProduct(hot);

    fs.Solve();
    System.Console.WriteLine($"Heat duty = {fs.AddHeater("H-1").HeatDutyKW:F2} kW");
    ```

=== "Python"

    ```python
    import sys, clr
    sys.path.append(r"C:\path\to\DWSIM\bin\x64\Debug")
    clr.AddReference("DWSIM.Automation.FluentAPI")

    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("Hello")
          .WithCompound("Water")
          .WithPropertyPackage(PropertyPackages.SteamTables))

    feed = (fs.AddMaterialStream("feed")
            .At(Q.Kelvin(300), Q.Atm(1))
            .WithMassFlow(Q.KgPerSecond(10)))

    hot = fs.AddMaterialStream("hot")

    h1 = (fs.AddHeater("H-1")
            .WithOutletTemperature(Q.Kelvin(400))
            .ConnectFeed(feed)
            .ConnectProduct(hot))

    fs.Solve()
    print(f"Heat duty = {h1.HeatDutyKW:.2f} kW")
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    Dim fs = Flowsheet.Create("Hello") _
        .WithCompound("Water") _
        .WithPropertyPackage(PropertyPackages.SteamTables)

    Dim feed = fs.AddMaterialStream("feed") _
        .At(300.0.Kelvin(), 1.0.Atm()) _
        .WithMassFlow(10.0.KgPerSecond())

    Dim hot = fs.AddMaterialStream("hot")

    Dim h1 = fs.AddHeater("H-1") _
        .WithOutletTemperature(400.0.Kelvin()) _
        .ConnectFeed(feed) _
        .ConnectProduct(hot)

    fs.Solve()
    Console.WriteLine($"Heat duty = {h1.HeatDutyKW:F2} kW")
    ```

## Where to next?

- **[Installation](getting-started/installation.md)** — reference the DLL from a
  .NET project or a Python script.
- **[First Flowsheet](getting-started/first-flowsheet-csharp.md)** — a guided
  walk-through of the mixer example.
- **[API Reference](api/flowsheet.md)** — every public type, organized by topic.
- **[Examples](examples/index.md)** — 17 end-to-end runnable flowsheets, each
  shown in Python, C# and VB.NET.
