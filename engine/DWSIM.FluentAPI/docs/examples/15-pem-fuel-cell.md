# 15 - PEM Fuel Cell

Hydrogen + air → electrical power + water vapour.

The fuel cell runs the Amphlett static model of the OPEM library, so DWSIM
needs a Python distribution for it. The desktop application takes its folder
from General Settings; a script does not read those settings and sets
`Settings.PythonPath` itself before solving. The builder has no typed setters
yet; the model inputs live in `InputParameters`, under the OPEM names: `N`
is the number of cells, `A` the active area in cm² and `i-stop` the current
in A at which the stack runs. The stack temperature is the mean of the two
inlet temperatures. The electric power leaves through outlet port 1.

=== "Python"

    ```python
    import sys
    import clr
    clr.AddReference("DWSIM.GlobalSettings")
    from DWSIM.GlobalSettings import Settings
    from System import Action
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q
    from DWSIM.Automation.FluentAPI.Builders import CompositionBuilder

    Settings.PythonPath = sys.base_prefix    # a Python 3 distribution for OPEM

    fs = (Flowsheet.Create("PyPEMFC")
          .WithCompounds("Hydrogen", "Oxygen", "Nitrogen", "Water")
          .WithPropertyPackage(PropertyPackages.PengRobinson))

    h2  = (fs.AddMaterialStream("H2-feed").At(Q.Kelvin(343), Q.Bar(2))
             .WithMassFlow(Q.KgPerHour(2))
             .WithComposition(Action[CompositionBuilder](lambda c: c.Mole("Hydrogen", 1.0))))
    air = (fs.AddMaterialStream("air").At(Q.Kelvin(343), Q.Bar(2))
             .WithMassFlow(Q.KgPerHour(20))
             .WithComposition(Action[CompositionBuilder](lambda c: c
                 .Mole("Oxygen", 0.21).Mole("Nitrogen", 0.79))))
    exhaust = fs.AddMaterialStream("exhaust")
    power   = fs.AddEnergyStream("DC-power")

    fc = fs.AddPEMFuelCell("FC-1")
    fc.Object.CreateConnectors()    # the canvas creates the ports on first draw
    (fc.ConnectFeed(h2,  0)
       .ConnectFeed(air, 1)
       .ConnectProduct(exhaust, 0)
       .ConnectEnergyProduct(power, 1))
    p = fc.Object.InputParameters
    p["N"].Value      = 120     # cells in the stack
    p["A"].Value      = 50.6    # active area, cm2
    p["i-stop"].Value = 40.0    # stack current, A

    fs.AutoLayout(); fs.Solve()
    r = fc.Object.OutputParameters
    print(f"Stack voltage = {r['V'].Value:.2f} V")
    print(f"DC power      = {power.EnergyFlowKW:.2f} kW")
    print(f"Heat released = {r['Ph'].Value / 1000:.2f} kW")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    DWSIM.GlobalSettings.Settings.PythonPath = @"C:\Python312";    // a Python 3 distribution for OPEM

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

    var fc = fs.AddPEMFuelCell("FC-1");
    fc.Object.CreateConnectors();    // the canvas creates the ports on first draw
    fc.ConnectFeed(h2,  0)
      .ConnectFeed(air, 1)
      .ConnectProduct(exhaust, 0)
      .ConnectEnergyProduct(power, 1);
    var p = fc.Object.InputParameters;
    p["N"].Value      = 120;     // cells in the stack
    p["A"].Value      = 50.6;    // active area, cm2
    p["i-stop"].Value = 40.0;    // stack current, A

    fs.AutoLayout();
    fs.Solve();
    var r = fc.Object.OutputParameters;
    System.Console.WriteLine($"Stack voltage = {r["V"].Value:F2} V");
    System.Console.WriteLine($"DC power      = {power.EnergyFlowKW:F2} kW");
    System.Console.WriteLine($"Heat released = {r["Ph"].Value / 1000:F2} kW");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    DWSIM.GlobalSettings.Settings.PythonPath = "C:\Python312"    ' a Python 3 distribution for OPEM

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

    Dim fc = fs.AddPEMFuelCell("FC-1")
    fc.Object.CreateConnectors()    ' the canvas creates the ports on first draw
    fc.ConnectFeed(h2,  0) _
      .ConnectFeed(air, 1) _
      .ConnectProduct(exhaust, 0) _
      .ConnectEnergyProduct(power, 1)
    Dim p = fc.Object.InputParameters
    p("N").Value = 120          ' cells in the stack
    p("A").Value = 50.6         ' active area, cm2
    p("i-stop").Value = 40.0    ' stack current, A

    fs.AutoLayout()
    fs.Solve()
    Dim r = fc.Object.OutputParameters
    Console.WriteLine($"Stack voltage = {r("V").Value:F2} V")
    Console.WriteLine($"DC power      = {power.EnergyFlowKW:F2} kW")
    Console.WriteLine($"Heat released = {r("Ph").Value / 1000:F2} kW")
    ```

Running the example prints:

```text
Stack voltage = 63.20 V
DC power      = 2.52 kW
Heat released = 3.37 kW
```

At 40 A each of the 120 cells gives 0.53 V, so the stack delivers 2.52 kW of
electric power and releases 3.37 kW of heat into the exhaust.
