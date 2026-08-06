# 10 — Pump + Compressor Train

Liquid pump → vaporizer → gas compressor.

=== "Python"

    ```python
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyPumpComp")
          .WithCompound("Methane")
          .WithPropertyPackage(PropertyPackages.PengRobinson))

    feed = (fs.AddMaterialStream("LNG")
            .At(Q.Kelvin(120), Q.Bar(1)).WithMassFlow(Q.KgPerSecond(5)))
    pumped = fs.AddMaterialStream("pumped")
    vap    = fs.AddMaterialStream("vapor")
    out    = fs.AddMaterialStream("HP-gas")

    pump = (fs.AddPump("P-1")
              .WithOutletPressure(Q.Bar(20))
              .WithEfficiencyPercent(75)
              .ConnectFeed(feed).ConnectProduct(pumped))

    (fs.AddHeater("H-1")
       .WithOutletTemperature(Q.Kelvin(280))
       .ConnectFeed(pumped).ConnectProduct(vap))

    comp = (fs.AddCompressor("C-1")
              .WithOutletPressure(Q.Bar(80))
              .WithEfficiencyPercent(78)
              .ConnectFeed(vap).ConnectProduct(out))

    fs.AutoLayout(); fs.Solve()
    print(f"Pump power = {pump.PowerKW:.2f} kW")
    print(f"Comp power = {comp.PowerKW:.2f} kW")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("PumpComp")
        .WithCompound("Methane")
        .WithPropertyPackage(PropertyPackages.PengRobinson);

    var feed = fs.AddMaterialStream("LNG").At(120.Kelvin(), 1.Bar()).WithMassFlow(5.KgPerSecond());
    var pumped = fs.AddMaterialStream("pumped");
    var vap    = fs.AddMaterialStream("vapor");
    var hp     = fs.AddMaterialStream("HP-gas");

    var pump = fs.AddPump("P-1")
        .WithOutletPressure(20.Bar()).WithEfficiencyPercent(75)
        .ConnectFeed(feed).ConnectProduct(pumped);

    fs.AddHeater("H-1")
      .WithOutletTemperature(280.Kelvin())
      .ConnectFeed(pumped).ConnectProduct(vap);

    var comp = fs.AddCompressor("C-1")
        .WithOutletPressure(80.Bar()).WithEfficiencyPercent(78)
        .ConnectFeed(vap).ConnectProduct(hp);

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"Pump power = {pump.PowerKW:F2} kW");
    System.Console.WriteLine($"Comp power = {comp.PowerKW:F2} kW");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    Dim fs = Flowsheet.Create("PumpComp") _
        .WithCompound("Methane") _
        .WithPropertyPackage(PropertyPackages.PengRobinson)

    Dim feed = fs.AddMaterialStream("LNG").At(120.0.Kelvin(), 1.0.Bar()).WithMassFlow(5.0.KgPerSecond())
    Dim pumped = fs.AddMaterialStream("pumped")
    Dim vap    = fs.AddMaterialStream("vapor")
    Dim hp     = fs.AddMaterialStream("HP-gas")

    Dim pump = fs.AddPump("P-1") _
        .WithOutletPressure(20.0.Bar()).WithEfficiencyPercent(75) _
        .ConnectFeed(feed).ConnectProduct(pumped)

    fs.AddHeater("H-1") _
      .WithOutletTemperature(280.0.Kelvin()) _
      .ConnectFeed(pumped).ConnectProduct(vap)

    Dim comp = fs.AddCompressor("C-1") _
        .WithOutletPressure(80.0.Bar()).WithEfficiencyPercent(78) _
        .ConnectFeed(vap).ConnectProduct(hp)

    fs.AutoLayout()
    fs.Solve()
    ```
