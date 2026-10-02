# 05 - Anaerobic Digester

Acetic acid wastewater → ADM1-Lite digester → digestate + biogas.

The digester converts the compound named in `SubstrateCompound`, here acetic
acid. Its retention time follows from the working volume and the feed flow:
500 kg/h of wastewater is about 12 m³/d, so a 240 m³ digester runs at the
20 days given to `WithHydraulicRetentionTime` (the reactor logs a warning when
the two disagree). Outlet 0 is the digestate and outlet 1 the biogas.

=== "Python"

    ```python
    import clr
    clr.AddReference("DWSIM.UnitOperations")
    from System import Action
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q
    from DWSIM.Automation.FluentAPI.Builders import CompositionBuilder
    from DWSIM.UnitOperations.Reactors import DigesterModel

    fs = (Flowsheet.Create("PyAD")
          .WithCompounds("Water", "Acetic acid", "Methane", "Carbon dioxide")
          .WithPropertyPackage(PropertyPackages.NRTL))

    sludge = (fs.AddMaterialStream("sludge")
              .At(Q.Kelvin(308.15), Q.Atm(1))
              .WithMassFlow(Q.KgPerHour(500))
              .WithComposition(Action[CompositionBuilder](lambda c: c
                  .Mass("Water",       0.95)
                  .Mass("Acetic acid", 0.05))))

    digestate = fs.AddMaterialStream("digestate")
    biogas    = fs.AddMaterialStream("biogas")

    ad = (fs.AddAnaerobicDigester("AD-1")
            .WithModel(DigesterModel.ADM1Lite)
            .WithVolume(Q.CubicMeters(240))
            .WithHydraulicRetentionTime(Q.Days(20))
            .ConnectFeed(sludge)
            .ConnectProduct(digestate, 0)
            .ConnectProduct(biogas, 1))
    ad.Object.SubstrateCompound = "Acetic acid"

    fs.AutoLayout(); fs.Solve()
    d = ad.Object
    print(f"COD removed = {d.Result_CODremoved_kgs / d.Result_CODin_kgs * 100:.1f} %")
    print(f"Biogas flow = {biogas.MassFlowKgPerSecond*3600:.2f} kg/h")
    print(f"Methane     = {biogas.OverallMoleFraction('Methane')*100:.1f} mol %")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;
    using DWSIM.UnitOperations.Reactors;

    var fs = Flowsheet.Create("AD")
        .WithCompounds("Water", "Acetic acid", "Methane", "Carbon dioxide")
        .WithPropertyPackage(PropertyPackages.NRTL);

    var sludge = fs.AddMaterialStream("sludge")
        .At(308.15.Kelvin(), 1.Atm())
        .WithMassFlow(500.KgPerHour())
        .WithComposition(c => c
            .Mass("Water",       0.95)
            .Mass("Acetic acid", 0.05));

    var digestate = fs.AddMaterialStream("digestate");
    var biogas    = fs.AddMaterialStream("biogas");

    var ad = fs.AddAnaerobicDigester("AD-1")
        .WithModel(DigesterModel.ADM1Lite)
        .WithVolume(240.0.CubicMeters())
        .WithHydraulicRetentionTime(20.Days())
        .ConnectFeed(sludge)
        .ConnectProduct(digestate, 0)
        .ConnectProduct(biogas, 1);
    ad.Object.SubstrateCompound = "Acetic acid";

    fs.AutoLayout();
    fs.Solve();
    var d = ad.Object;
    System.Console.WriteLine($"COD removed = {d.Result_CODremoved_kgs / d.Result_CODin_kgs * 100:F1} %");
    System.Console.WriteLine($"Biogas flow = {biogas.MassFlowKgPerSecond*3600:F2} kg/h");
    System.Console.WriteLine($"Methane     = {biogas.OverallMoleFraction("Methane")*100:F1} mol %");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI
    Imports DWSIM.UnitOperations.Reactors

    Dim fs = Flowsheet.Create("AD") _
        .WithCompounds("Water", "Acetic acid", "Methane", "Carbon dioxide") _
        .WithPropertyPackage(PropertyPackages.NRTL)

    Dim sludge = fs.AddMaterialStream("sludge") _
        .At(308.15.Kelvin(), 1.0.Atm()) _
        .WithMassFlow(500.0.KgPerHour()) _
        .WithComposition(Sub(c) c _
            .Mass("Water", 0.95) _
            .Mass("Acetic acid", 0.05))

    Dim digestate = fs.AddMaterialStream("digestate")
    Dim biogas    = fs.AddMaterialStream("biogas")

    Dim ad = fs.AddAnaerobicDigester("AD-1") _
      .WithModel(DigesterModel.ADM1Lite) _
      .WithVolume(240.0.CubicMeters()) _
      .WithHydraulicRetentionTime(20.0.Days()) _
      .ConnectFeed(sludge) _
      .ConnectProduct(digestate, 0) _
      .ConnectProduct(biogas, 1)
    ad.Object.SubstrateCompound = "Acetic acid"

    fs.AutoLayout()
    fs.Solve()
    Dim d = ad.Object
    Console.WriteLine($"COD removed = {d.Result_CODremoved_kgs / d.Result_CODin_kgs * 100:F1} %")
    Console.WriteLine($"Biogas flow = {biogas.MassFlowKgPerSecond * 3600:F2} kg/h")
    Console.WriteLine($"Methane     = {biogas.OverallMoleFraction("Methane") * 100:F1} mol %")
    ```

Running the Python version prints:

```text
COD removed = 99.9 %
Biogas flow = 15.72 kg/h
Methane     = 66.8 mol %
```

Almost all of the 25 kg/h of acetic acid is digested. The biogas is about
two thirds methane and one third carbon dioxide, and the digestate leaves
with the other 484.28 kg/h of the feed. The sludge grown in the digester is
reported only when a biomass compound is named in `BiomassCompound`.
