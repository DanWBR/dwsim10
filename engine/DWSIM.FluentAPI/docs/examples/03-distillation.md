# 03 — Distillation (Rigorous, Ethanol/Water)

Rigorous distillation column under NRTL. Demonstrates the
`DistillationColumnBuilder` surface — feed/product wiring, condenser /
reboiler specs, top pressure, column ΔP — and reading back the duties.

=== "Python"

    ```python
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyDist")
          .WithCompounds("Water", "Ethanol")
          .WithPropertyPackage(PropertyPackages.NRTL))

    feed = (fs.AddMaterialStream("feed")
            .WithTemperature(Q.Kelvin(300))
            .WithMolarFlow(Q.MolPerSecond(100))
            .SetCompoundMolarFlow("Water",   50.0)
            .SetCompoundMolarFlow("Ethanol", 50.0))

    dist = fs.AddMaterialStream("distillate")
    bot  = fs.AddMaterialStream("bottoms")
    cd   = fs.AddEnergyStream("cd")
    rd   = fs.AddEnergyStream("rd")

    (fs.AddDistillationColumn("T-101")
       .WithNumberOfStages(20)
       .WithFeed(feed, 10)
       .WithDistillate(dist)
       .WithBottoms(bot)
       .WithCondenserDuty(cd)
       .WithReboilerDuty(rd)
       .WithCondenserSpec("Reflux Ratio", 2.0, "")
       .WithReboilerSpec("Product Molar Flow Rate", 75.0, "mol/s")
       .WithTopPressure(Q.Pascal(101325.0))
       .WithColumnPressureDrop(Q.Pascal(0.0)))

    fs.AutoLayout(); fs.Solve()

    print(f"Distillate: {dist.MolarFlowMolPerSecond:.2f} mol/s")
    print(f"Bottoms:    {bot.MolarFlowMolPerSecond:.2f} mol/s")
    print(f"Cond duty:  {cd.EnergyFlowKW:.2f} kW   Reb duty: {rd.EnergyFlowKW:.2f} kW")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("Dist")
        .WithCompounds("Water", "Ethanol")
        .WithPropertyPackage(PropertyPackages.NRTL);

    var feed = fs.AddMaterialStream("feed")
        .WithTemperature(300.Kelvin())
        .WithMolarFlow(100.MolPerSecond())
        .SetCompoundMolarFlow("Water",   50.0)
        .SetCompoundMolarFlow("Ethanol", 50.0);

    var dist = fs.AddMaterialStream("distillate");
    var bot  = fs.AddMaterialStream("bottoms");
    var cd   = fs.AddEnergyStream("cd");
    var rd   = fs.AddEnergyStream("rd");

    fs.AddDistillationColumn("T-101")
      .WithNumberOfStages(20)
      .WithFeed(feed, 10)
      .WithDistillate(dist)
      .WithBottoms(bot)
      .WithCondenserDuty(cd)
      .WithReboilerDuty(rd)
      .WithCondenserSpec("Reflux Ratio", 2.0)
      .WithReboilerSpec("Product Molar Flow Rate", 75.0, "mol/s")
      .WithTopPressure(1.Atm())
      .WithColumnPressureDrop(0.Pascal());

    fs.AutoLayout();
    fs.Solve();

    System.Console.WriteLine($"Cond duty = {cd.EnergyFlowKW:F2} kW");
    System.Console.WriteLine($"Reb duty  = {rd.EnergyFlowKW:F2} kW");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    Module Program
        Sub Main()
            Dim fs = Flowsheet.Create("Dist") _
                .WithCompounds("Water", "Ethanol") _
                .WithPropertyPackage(PropertyPackages.NRTL)

            Dim feed = fs.AddMaterialStream("feed") _
                .WithTemperature(300.0.Kelvin()) _
                .WithMolarFlow(100.0.MolPerSecond()) _
                .SetCompoundMolarFlow("Water",   50.0) _
                .SetCompoundMolarFlow("Ethanol", 50.0)

            Dim dist = fs.AddMaterialStream("distillate")
            Dim bot  = fs.AddMaterialStream("bottoms")
            Dim cd   = fs.AddEnergyStream("cd")
            Dim rd   = fs.AddEnergyStream("rd")

            fs.AddDistillationColumn("T-101") _
              .WithNumberOfStages(20) _
              .WithFeed(feed, 10) _
              .WithDistillate(dist) _
              .WithBottoms(bot) _
              .WithCondenserDuty(cd) _
              .WithReboilerDuty(rd) _
              .WithCondenserSpec("Reflux Ratio", 2.0) _
              .WithReboilerSpec("Product Molar Flow Rate", 75.0, "mol/s") _
              .WithTopPressure(1.0.Atm()) _
              .WithColumnPressureDrop(0.0.Pascal())

            fs.AutoLayout()
            fs.Solve()

            Console.WriteLine($"Cond duty = {cd.EnergyFlowKW:F2} kW")
            Console.WriteLine($"Reb duty  = {rd.EnergyFlowKW:F2} kW")
        End Sub
    End Module
    ```
