# 14 — Water Electrolyzer

PEM electrolyzer producing hydrogen at 30 bar.

=== "Python"

    ```python
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyEL")
          .WithCompounds("Water", "Hydrogen", "Oxygen")
          .WithPropertyPackage(PropertyPackages.PengRobinson))

    water  = (fs.AddMaterialStream("DI-water")
              .At(Q.Kelvin(298), Q.Bar(2)).WithMassFlow(Q.KgPerSecond(0.5)))
    power  = fs.AddEnergyStream("PV-power").WithEnergyFlow(Q.Megawatts(2))
    h2_out = fs.AddMaterialStream("H2")
    o2_out = fs.AddMaterialStream("O2")

    el = (fs.AddWaterElectrolyzer("EL-1")
            .WithStackPower(Q.Megawatts(2))
            .WithFaradayEfficiency(0.65)
            .WithCellVoltage(1.85)
            .WithOperatingPressure(Q.Bar(30))
            .ConnectEnergyFeed(power)
            .ConnectFeed(water)
            .ConnectProduct(h2_out, 0)
            .ConnectProduct(o2_out, 1))

    fs.AutoLayout(); fs.Solve()
    print(f"H2 flow = {h2_out.MassFlowKgPerSecond*3600:.2f} kg/h")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("EL")
        .WithCompounds("Water", "Hydrogen", "Oxygen")
        .WithPropertyPackage(PropertyPackages.PengRobinson);

    var water = fs.AddMaterialStream("DI-water")
        .At(298.Kelvin(), 2.Bar()).WithMassFlow(0.5.KgPerSecond());
    var power = fs.AddEnergyStream("PV-power").WithEnergyFlow(2.Megawatts());
    var h2 = fs.AddMaterialStream("H2");
    var o2 = fs.AddMaterialStream("O2");

    fs.AddWaterElectrolyzer("EL-1")
      .WithStackPower(2.Megawatts())
      .WithFaradayEfficiency(0.65)
      .WithCellVoltage(1.85)
      .WithOperatingPressure(30.Bar())
      .ConnectEnergyFeed(power)
      .ConnectFeed(water)
      .ConnectProduct(h2, 0)
      .ConnectProduct(o2, 1);

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"H2 = {h2.MassFlowKgPerSecond*3600:F2} kg/h");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    Dim fs = Flowsheet.Create("EL") _
        .WithCompounds("Water", "Hydrogen", "Oxygen") _
        .WithPropertyPackage(PropertyPackages.PengRobinson)

    Dim water = fs.AddMaterialStream("DI-water") _
        .At(298.0.Kelvin(), 2.0.Bar()).WithMassFlow(0.5.KgPerSecond())
    Dim power = fs.AddEnergyStream("PV-power").WithEnergyFlow(2.0.Megawatts())
    Dim h2 = fs.AddMaterialStream("H2")
    Dim o2 = fs.AddMaterialStream("O2")

    fs.AddWaterElectrolyzer("EL-1") _
      .WithStackPower(2.0.Megawatts()) _
      .WithFaradayEfficiency(0.65) _
      .WithCellVoltage(1.85) _
      .WithOperatingPressure(30.0.Bar()) _
      .ConnectEnergyFeed(power) _
      .ConnectFeed(water) _
      .ConnectProduct(h2, 0) _
      .ConnectProduct(o2, 1)

    fs.AutoLayout()
    fs.Solve()
    ```
