# 16 — Flowsheet with Recycle Loop

Splits an outlet, recycles a fraction back to a mixer, lets the
`FlowsheetSolver` converge the tear stream automatically.

The recycle UO doesn't yet have a typed builder; use the generic escape
hatch `AddUnitOperation(ObjectType.Recycle, tag)`.

=== "Python"

    ```python
    from DWSIM.Interfaces.Enums.GraphicObjects import ObjectType
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyRecycle")
          .WithCompound("Water")
          .WithPropertyPackage(PropertyPackages.SteamTables))

    fresh   = (fs.AddMaterialStream("fresh")
               .At(Q.Kelvin(300), Q.Atm(1)).WithMassFlow(Q.KgPerSecond(10)))
    mixed   = fs.AddMaterialStream("mixed")
    heated  = fs.AddMaterialStream("heated")
    purge   = fs.AddMaterialStream("purge")
    recycle = fs.AddMaterialStream("recycle")

    (fs.AddMixer("MIX-1")
       .ConnectFeed(fresh,   0).ConnectFeed(recycle, 1)
       .ConnectProduct(mixed))

    (fs.AddHeater("H-1")
       .WithOutletTemperature(Q.Kelvin(400))
       .ConnectFeed(mixed).ConnectProduct(heated))

    (fs.AddSplitter("SPL-1")
       .WithSplitRatios(0.2, 0.8)
       .ConnectFeed(heated)
       .ConnectProduct(purge,   0)
       .ConnectProduct(recycle, 1))

    (fs.AddUnitOperation(ObjectType.Recycle, "REC-1")
       .ConnectFeed(recycle).ConnectProduct(recycle))

    fs.AutoLayout(); fs.Solve()
    print(f"Purge flow = {purge.MassFlowKgPerSecond:.4f} kg/s")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;
    using DWSIM.Interfaces.Enums.GraphicObjects;

    var fs = Flowsheet.Create("Recycle")
        .WithCompound("Water")
        .WithPropertyPackage(PropertyPackages.SteamTables);

    var fresh   = fs.AddMaterialStream("fresh")
        .At(300.Kelvin(), 1.Atm()).WithMassFlow(10.KgPerSecond());
    var mixed   = fs.AddMaterialStream("mixed");
    var heated  = fs.AddMaterialStream("heated");
    var purge   = fs.AddMaterialStream("purge");
    var recycle = fs.AddMaterialStream("recycle");

    fs.AddMixer("MIX-1")
      .ConnectFeed(fresh, 0).ConnectFeed(recycle, 1)
      .ConnectProduct(mixed);

    fs.AddHeater("H-1")
      .WithOutletTemperature(400.Kelvin())
      .ConnectFeed(mixed).ConnectProduct(heated);

    fs.AddSplitter("SPL-1")
      .WithSplitRatios(0.2, 0.8)
      .ConnectFeed(heated)
      .ConnectProduct(purge,   0)
      .ConnectProduct(recycle, 1);

    fs.AddUnitOperation(ObjectType.Recycle, "REC-1")
      .ConnectFeed(recycle).ConnectProduct(recycle);

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"Purge = {purge.MassFlowKgPerSecond:F4} kg/s");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI
    Imports DWSIM.Interfaces.Enums.GraphicObjects

    Dim fs = Flowsheet.Create("Recycle") _
        .WithCompound("Water") _
        .WithPropertyPackage(PropertyPackages.SteamTables)

    Dim fresh   = fs.AddMaterialStream("fresh") _
        .At(300.0.Kelvin(), 1.0.Atm()).WithMassFlow(10.0.KgPerSecond())
    Dim mixed   = fs.AddMaterialStream("mixed")
    Dim heated  = fs.AddMaterialStream("heated")
    Dim purge   = fs.AddMaterialStream("purge")
    Dim recycle = fs.AddMaterialStream("recycle")

    fs.AddMixer("MIX-1") _
      .ConnectFeed(fresh, 0).ConnectFeed(recycle, 1) _
      .ConnectProduct(mixed)

    fs.AddHeater("H-1") _
      .WithOutletTemperature(400.0.Kelvin()) _
      .ConnectFeed(mixed).ConnectProduct(heated)

    fs.AddSplitter("SPL-1") _
      .WithSplitRatios(0.2, 0.8) _
      .ConnectFeed(heated) _
      .ConnectProduct(purge,   0) _
      .ConnectProduct(recycle, 1)

    fs.AddUnitOperation(ObjectType.Recycle, "REC-1") _
      .ConnectFeed(recycle).ConnectProduct(recycle)

    fs.AutoLayout()
    fs.Solve()
    ```

The `FlowsheetSolver` detects the cycle, picks a tear stream, and iterates
until convergence — no extra wiring on the Fluent API side.
