# 11 — Rigorous Distillation (Methanol/Water)

A 30-stage column with feed on stage 15, condenser spec by reflux ratio,
reboiler spec by molar flow, with explicit top pressure and column ΔP.

=== "Python"

    ```python
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyRigDist")
          .WithCompounds("Water", "Methanol")
          .WithPropertyPackage(PropertyPackages.NRTL))

    feed = (fs.AddMaterialStream("feed")
            .WithTemperature(Q.Kelvin(330))
            .WithPressure(Q.Atm(1.2))
            .WithMolarFlow(Q.MolPerSecond(50))
            .SetCompoundMolarFlow("Water",    25.0)
            .SetCompoundMolarFlow("Methanol", 25.0))

    dist = fs.AddMaterialStream("distillate")
    bot  = fs.AddMaterialStream("bottoms")
    cd   = fs.AddEnergyStream("cd")
    rd   = fs.AddEnergyStream("rd")

    (fs.AddDistillationColumn("T-201")
       .WithNumberOfStages(30)
       .WithFeed(feed, 15)
       .WithDistillate(dist).WithBottoms(bot)
       .WithCondenserDuty(cd).WithReboilerDuty(rd)
       .WithCondenserSpec("Reflux Ratio", 1.5)
       .WithReboilerSpec("Product Molar Flow Rate", 25.0, "mol/s")
       .WithTopPressure(Q.Atm(1.0))
       .WithColumnPressureDrop(Q.Bar(0.05)))

    fs.AutoLayout(); fs.Solve()
    print(f"x_MeOH (dist) = {dist.OverallMoleFraction('Methanol'):.4f}")
    print(f"x_H2O  (bot)  = {bot.OverallMoleFraction('Water'):.4f}")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("RigDist")
        .WithCompounds("Water", "Methanol")
        .WithPropertyPackage(PropertyPackages.NRTL);

    var feed = fs.AddMaterialStream("feed")
        .WithTemperature(330.Kelvin()).WithPressure(1.2.Atm())
        .WithMolarFlow(50.MolPerSecond())
        .SetCompoundMolarFlow("Water",    25.0)
        .SetCompoundMolarFlow("Methanol", 25.0);

    var dist = fs.AddMaterialStream("distillate");
    var bot  = fs.AddMaterialStream("bottoms");
    var cd   = fs.AddEnergyStream("cd");
    var rd   = fs.AddEnergyStream("rd");

    fs.AddDistillationColumn("T-201")
      .WithNumberOfStages(30)
      .WithFeed(feed, 15)
      .WithDistillate(dist).WithBottoms(bot)
      .WithCondenserDuty(cd).WithReboilerDuty(rd)
      .WithCondenserSpec("Reflux Ratio", 1.5)
      .WithReboilerSpec("Product Molar Flow Rate", 25.0, "mol/s")
      .WithTopPressure(1.Atm())
      .WithColumnPressureDrop(0.05.Bar());

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"x_MeOH (dist) = {dist.OverallMoleFraction("Methanol"):F4}");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    Dim fs = Flowsheet.Create("RigDist") _
        .WithCompounds("Water", "Methanol") _
        .WithPropertyPackage(PropertyPackages.NRTL)

    Dim feed = fs.AddMaterialStream("feed") _
        .WithTemperature(330.0.Kelvin()).WithPressure(1.2.Atm()) _
        .WithMolarFlow(50.0.MolPerSecond()) _
        .SetCompoundMolarFlow("Water", 25.0) _
        .SetCompoundMolarFlow("Methanol", 25.0)

    Dim dist = fs.AddMaterialStream("distillate")
    Dim bot  = fs.AddMaterialStream("bottoms")
    Dim cd   = fs.AddEnergyStream("cd")
    Dim rd   = fs.AddEnergyStream("rd")

    fs.AddDistillationColumn("T-201") _
      .WithNumberOfStages(30) _
      .WithFeed(feed, 15) _
      .WithDistillate(dist).WithBottoms(bot) _
      .WithCondenserDuty(cd).WithReboilerDuty(rd) _
      .WithCondenserSpec("Reflux Ratio", 1.5) _
      .WithReboilerSpec("Product Molar Flow Rate", 25.0, "mol/s") _
      .WithTopPressure(1.0.Atm()) _
      .WithColumnPressureDrop(0.05.Bar())

    fs.AutoLayout()
    fs.Solve()
    ```
