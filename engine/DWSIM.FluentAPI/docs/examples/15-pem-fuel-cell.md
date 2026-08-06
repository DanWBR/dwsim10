# 15 — PEM Fuel Cell

Hydrogen + air → electrical power + water vapour.

=== "Python"

    ```python
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyPEMFC")
          .WithCompounds("Hydrogen", "Oxygen", "Nitrogen", "Water")
          .WithPropertyPackage(PropertyPackages.PengRobinson))

    h2  = fs.AddMaterialStream("H2-feed").At(Q.Kelvin(343), Q.Bar(2)) \
            .WithMassFlow(Q.KgPerHour(2)).WithComposition(lambda c: c.Mole("Hydrogen", 1.0))
    air = fs.AddMaterialStream("air").At(Q.Kelvin(343), Q.Bar(2)) \
            .WithMassFlow(Q.KgPerHour(20)).WithComposition(lambda c: c
                .Mole("Oxygen",   0.21).Mole("Nitrogen", 0.79))
    exhaust = fs.AddMaterialStream("exhaust")
    power   = fs.AddEnergyStream("DC-power")

    fc = (fs.AddPEMFuelCell("FC-1")
            .WithStackArea(0.5)
            .WithNumberOfCells(120)
            .WithOperatingTemperature(Q.Kelvin(343))
            .WithStoichiometricRatioAir(2.0)
            .ConnectFeed(h2,  0)
            .ConnectFeed(air, 1)
            .ConnectProduct(exhaust)
            .ConnectEnergyProduct(power))

    fs.AutoLayout(); fs.Solve()
    print(f"DC power = {power.EnergyFlowKW:.2f} kW")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("PEMFC")
        .WithCompounds("Hydrogen", "Oxygen", "Nitrogen", "Water")
        .WithPropertyPackage(PropertyPackages.PengRobinson);

    var h2 = fs.AddMaterialStream("H2-feed")
        .At(343.Kelvin(), 2.Bar()).WithMassFlow(2.KgPerHour())
        .WithComposition(c => c.Mole("Hydrogen", 1.0));

    var air = fs.AddMaterialStream("air")
        .At(343.Kelvin(), 2.Bar()).WithMassFlow(20.KgPerHour())
        .WithComposition(c => c.Mole("Oxygen", 0.21).Mole("Nitrogen", 0.79));

    var exhaust = fs.AddMaterialStream("exhaust");
    var power   = fs.AddEnergyStream("DC-power");

    fs.AddPEMFuelCell("FC-1")
      .WithStackArea(0.5)
      .WithNumberOfCells(120)
      .WithOperatingTemperature(343.Kelvin())
      .WithStoichiometricRatioAir(2.0)
      .ConnectFeed(h2,  0)
      .ConnectFeed(air, 1)
      .ConnectProduct(exhaust)
      .ConnectEnergyProduct(power);

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"DC power = {power.EnergyFlowKW:F2} kW");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    Dim fs = Flowsheet.Create("PEMFC") _
        .WithCompounds("Hydrogen", "Oxygen", "Nitrogen", "Water") _
        .WithPropertyPackage(PropertyPackages.PengRobinson)

    Dim h2 = fs.AddMaterialStream("H2-feed") _
        .At(343.0.Kelvin(), 2.0.Bar()).WithMassFlow(2.0.KgPerHour()) _
        .WithComposition(Sub(c) c.Mole("Hydrogen", 1.0))

    Dim air = fs.AddMaterialStream("air") _
        .At(343.0.Kelvin(), 2.0.Bar()).WithMassFlow(20.0.KgPerHour()) _
        .WithComposition(Sub(c) c.Mole("Oxygen", 0.21).Mole("Nitrogen", 0.79))

    Dim exhaust = fs.AddMaterialStream("exhaust")
    Dim power   = fs.AddEnergyStream("DC-power")

    fs.AddPEMFuelCell("FC-1") _
      .WithStackArea(0.5) _
      .WithNumberOfCells(120) _
      .WithOperatingTemperature(343.0.Kelvin()) _
      .WithStoichiometricRatioAir(2.0) _
      .ConnectFeed(h2,  0) _
      .ConnectFeed(air, 1) _
      .ConnectProduct(exhaust) _
      .ConnectEnergyProduct(power)

    fs.AutoLayout()
    fs.Solve()
    ```
