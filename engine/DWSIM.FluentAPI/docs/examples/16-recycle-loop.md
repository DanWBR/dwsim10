# 16 - Flowsheet with Recycle Loop

Splits an outlet, recycles a fraction back to a mixer, lets the
`FlowsheetSolver` converge the tear stream automatically.

The recycle UO doesn't yet have a typed builder; use the generic escape
hatch `AddUnitOperation(ObjectType.OT_Recycle, tag)`. The recycle block sits
between two streams: the one that leaves the splitter is its feed, and the
one that goes back to the mixer is its product.

=== "Python"

    ```python
    import clr
    clr.AddReference("DWSIM.Interfaces")
    from DWSIM.Interfaces.Enums.GraphicObjects import ObjectType
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyRecycle")
          .WithCompound("Water")
          .WithPropertyPackage(PropertyPackages.SteamTables))

    fresh      = (fs.AddMaterialStream("fresh")
                  .At(Q.Kelvin(300), Q.Atm(1)).WithMassFlow(Q.KgPerSecond(10)))
    mixed      = fs.AddMaterialStream("mixed")
    heated     = fs.AddMaterialStream("heated")
    purge      = fs.AddMaterialStream("purge")
    recycle    = fs.AddMaterialStream("recycle")       # leaves the splitter
    recycle_in = fs.AddMaterialStream("recycle-in")    # leaves the recycle block

    (fs.AddMixer("MIX-1")
       .ConnectFeed(fresh, 0).ConnectFeed(recycle_in, 1)
       .ConnectProduct(mixed))

    (fs.AddHeater("H-1")
       .WithOutletTemperature(Q.Kelvin(400))
       .ConnectFeed(mixed).ConnectProduct(heated))

    (fs.AddSplitter("SPL-1")
       .WithSplitRatios(0.2, 0.8)
       .ConnectFeed(heated)
       .ConnectProduct(purge,   0)
       .ConnectProduct(recycle, 1))

    rec = (fs.AddUnitOperation(ObjectType.OT_Recycle, "REC-1")
             .ConnectFeed(recycle).ConnectProduct(recycle_in))

    fs.AutoLayout(); fs.Solve()
    r = rec.Object.__implementation__
    print(f"Converged    = {r.Converged} after {r.IterationsTaken} iterations")
    print(f"Recycle flow = {recycle_in.MassFlowKgPerSecond:.2f} kg/s")
    print(f"Purge flow   = {purge.MassFlowKgPerSecond:.2f} kg/s")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;
    using DWSIM.Interfaces.Enums.GraphicObjects;
    using DWSIM.UnitOperations.SpecialOps;

    var fs = Flowsheet.Create("Recycle")
        .WithCompound("Water")
        .WithPropertyPackage(PropertyPackages.SteamTables);

    var fresh     = fs.AddMaterialStream("fresh")
        .At(300.Kelvin(), 1.Atm()).WithMassFlow(10.KgPerSecond());
    var mixed     = fs.AddMaterialStream("mixed");
    var heated    = fs.AddMaterialStream("heated");
    var purge     = fs.AddMaterialStream("purge");
    var recycle   = fs.AddMaterialStream("recycle");      // leaves the splitter
    var recycleIn = fs.AddMaterialStream("recycle-in");   // leaves the recycle block

    fs.AddMixer("MIX-1")
      .ConnectFeed(fresh, 0).ConnectFeed(recycleIn, 1)
      .ConnectProduct(mixed);

    fs.AddHeater("H-1")
      .WithOutletTemperature(400.Kelvin())
      .ConnectFeed(mixed).ConnectProduct(heated);

    fs.AddSplitter("SPL-1")
      .WithSplitRatios(0.2, 0.8)
      .ConnectFeed(heated)
      .ConnectProduct(purge,   0)
      .ConnectProduct(recycle, 1);

    var rec = fs.AddUnitOperation(ObjectType.OT_Recycle, "REC-1")
      .ConnectFeed(recycle).ConnectProduct(recycleIn);

    fs.AutoLayout();
    fs.Solve();
    var r = (Recycle)rec.Object;
    System.Console.WriteLine($"Converged    = {r.Converged} after {r.IterationsTaken} iterations");
    System.Console.WriteLine($"Recycle flow = {recycleIn.MassFlowKgPerSecond:F2} kg/s");
    System.Console.WriteLine($"Purge flow   = {purge.MassFlowKgPerSecond:F2} kg/s");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI
    Imports DWSIM.Interfaces.Enums.GraphicObjects
    Imports DWSIM.UnitOperations.SpecialOps

    Dim fs = Flowsheet.Create("Recycle") _
        .WithCompound("Water") _
        .WithPropertyPackage(PropertyPackages.SteamTables)

    Dim fresh     = fs.AddMaterialStream("fresh") _
        .At(300.0.Kelvin(), 1.0.Atm()).WithMassFlow(10.0.KgPerSecond())
    Dim mixed     = fs.AddMaterialStream("mixed")
    Dim heated    = fs.AddMaterialStream("heated")
    Dim purge     = fs.AddMaterialStream("purge")
    Dim recycle   = fs.AddMaterialStream("recycle")      ' leaves the splitter
    Dim recycleIn = fs.AddMaterialStream("recycle-in")   ' leaves the recycle block

    fs.AddMixer("MIX-1") _
      .ConnectFeed(fresh, 0).ConnectFeed(recycleIn, 1) _
      .ConnectProduct(mixed)

    fs.AddHeater("H-1") _
      .WithOutletTemperature(400.0.Kelvin()) _
      .ConnectFeed(mixed).ConnectProduct(heated)

    fs.AddSplitter("SPL-1") _
      .WithSplitRatios(0.2, 0.8) _
      .ConnectFeed(heated) _
      .ConnectProduct(purge,   0) _
      .ConnectProduct(recycle, 1)

    Dim rec = fs.AddUnitOperation(ObjectType.OT_Recycle, "REC-1") _
      .ConnectFeed(recycle).ConnectProduct(recycleIn)

    fs.AutoLayout()
    fs.Solve()
    Dim r = DirectCast(rec.Object, Recycle)
    Console.WriteLine($"Converged    = {r.Converged} after {r.IterationsTaken} iterations")
    Console.WriteLine($"Recycle flow = {recycleIn.MassFlowKgPerSecond:F2} kg/s")
    Console.WriteLine($"Purge flow   = {purge.MassFlowKgPerSecond:F2} kg/s")
    ```

The `FlowsheetSolver` detects the cycle, takes the recycle block as the tear
point, and iterates until the stream that leaves the splitter matches the
one that goes back to the mixer. Running the Python version prints:

```text
Converged    = True after 34 iterations
Recycle flow = 39.98 kg/s
Purge flow   = 10.00 kg/s
```

At steady state the purge takes out the 10 kg/s of fresh water and the loop
carries four times that, 40 kg/s. The recycle stops when the change between
iterations falls inside its default mass flow tolerance, 0.01 kg/s, which is
why the recycle flow reads 39.98 kg/s.
