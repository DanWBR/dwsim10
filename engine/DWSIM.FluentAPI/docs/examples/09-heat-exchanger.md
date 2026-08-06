# 09 — Heat Exchanger (Pinch Point)

Counter-current heat exchanger: hot oil cooled against cold water.

=== "Python"

    ```python
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q
    from DWSIM.UnitOperations.UnitOperations import HeatExchangerCalcMode

    fs = (Flowsheet.Create("PyHX")
          .WithCompound("Water")
          .WithPropertyPackage(PropertyPackages.SteamTables))

    hot_in = (fs.AddMaterialStream("hot-in")
              .At(Q.Kelvin(450), Q.Atm(2)).WithMassFlow(Q.KgPerSecond(5)))
    cold_in = (fs.AddMaterialStream("cold-in")
               .At(Q.Kelvin(298), Q.Atm(2)).WithMassFlow(Q.KgPerSecond(8)))
    hot_out  = fs.AddMaterialStream("hot-out")
    cold_out = fs.AddMaterialStream("cold-out")

    hx = (fs.AddHeatExchanger("E-1")
            .WithCalculationMode(HeatExchangerCalcMode.PinchPoint)
            .WithGlobalUA(2500.0)
            .WithHotSidePressureDrop(Q.Bar(0.2))
            .WithColdSidePressureDrop(Q.Bar(0.1))
            .ConnectFeed(hot_in,  0).ConnectProduct(hot_out,  0)
            .ConnectFeed(cold_in, 1).ConnectProduct(cold_out, 1))

    fs.AutoLayout(); fs.Solve()
    print(f"Hot out  T = {hot_out.TemperatureK:.2f} K")
    print(f"Cold out T = {cold_out.TemperatureK:.2f} K")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;
    using DWSIM.UnitOperations.UnitOperations;

    var fs = Flowsheet.Create("HX")
        .WithCompound("Water")
        .WithPropertyPackage(PropertyPackages.SteamTables);

    var hotIn  = fs.AddMaterialStream("hot-in").At(450.Kelvin(),  2.Atm()).WithMassFlow(5.KgPerSecond());
    var coldIn = fs.AddMaterialStream("cold-in").At(298.Kelvin(), 2.Atm()).WithMassFlow(8.KgPerSecond());
    var hotOut  = fs.AddMaterialStream("hot-out");
    var coldOut = fs.AddMaterialStream("cold-out");

    fs.AddHeatExchanger("E-1")
      .WithCalculationMode(HeatExchangerCalcMode.PinchPoint)
      .WithGlobalUA(2500.0)
      .WithHotSidePressureDrop(0.2.Bar())
      .WithColdSidePressureDrop(0.1.Bar())
      .ConnectFeed(hotIn,  0).ConnectProduct(hotOut,  0)
      .ConnectFeed(coldIn, 1).ConnectProduct(coldOut, 1);

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"Hot out  T = {hotOut.TemperatureK:F2} K");
    System.Console.WriteLine($"Cold out T = {coldOut.TemperatureK:F2} K");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI
    Imports DWSIM.UnitOperations.UnitOperations

    Dim fs = Flowsheet.Create("HX") _
        .WithCompound("Water") _
        .WithPropertyPackage(PropertyPackages.SteamTables)

    Dim hotIn  = fs.AddMaterialStream("hot-in").At(450.0.Kelvin(),  2.0.Atm()).WithMassFlow(5.0.KgPerSecond())
    Dim coldIn = fs.AddMaterialStream("cold-in").At(298.0.Kelvin(), 2.0.Atm()).WithMassFlow(8.0.KgPerSecond())
    Dim hotOut  = fs.AddMaterialStream("hot-out")
    Dim coldOut = fs.AddMaterialStream("cold-out")

    fs.AddHeatExchanger("E-1") _
      .WithCalculationMode(HeatExchangerCalcMode.PinchPoint) _
      .WithGlobalUA(2500.0) _
      .WithHotSidePressureDrop(0.2.Bar()) _
      .WithColdSidePressureDrop(0.1.Bar()) _
      .ConnectFeed(hotIn,  0).ConnectProduct(hotOut,  0) _
      .ConnectFeed(coldIn, 1).ConnectProduct(coldOut, 1)

    fs.AutoLayout()
    fs.Solve()
    ```
