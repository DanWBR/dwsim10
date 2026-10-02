# 04 - Bioprocess Pretreatment Train

Lignocellulosic biomass → dilute-acid pretreatment → decanter centrifuge
solid/liquid split. Demonstrates two free `IExternalUnitOperation`-backed
bioprocess UOs chained through plain material streams.

The compound database has no cellulose, hemicellulose or lignin. The
pretreatment reactor works on mass and takes the name of the compound that
carries each fraction, so sucrose stands in for cellulose, lactose for
hemicellulose and coal for lignin. Furfural, HMF, acetic acid and dissolved
lignin only form when their compounds are assigned as well; this example
leaves them out, so the reactor releases glucose and xylose only.

=== "Python"

    ```python
    import clr
    clr.AddReference("DWSIM.UnitOperations")
    from System import Action
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q
    from DWSIM.Automation.FluentAPI.Builders import CompositionBuilder
    from DWSIM.UnitOperations.Reactors import PretreatmentType
    from DWSIM.UnitOperations.UnitOperations import CentrifugeType

    fs = (Flowsheet.Create("PyBioPretreat")
          .WithCompounds("Water", "Sucrose", "Lactose", "Coal", "Glucose", "Xylose")
          .WithPropertyPackage(PropertyPackages.NRTL))

    biomass = (fs.AddMaterialStream("biomass")
               .At(Q.Kelvin(443.15), Q.Bar(10))
               .WithMassFlow(Q.KgPerHour(1000))
               .WithComposition(Action[CompositionBuilder](lambda c: c
                   .Mass("Water",   0.30)
                   .Mass("Sucrose", 0.40)     # cellulose
                   .Mass("Lactose", 0.20)     # hemicellulose
                   .Mass("Coal",    0.10))))  # lignin

    pretreated = fs.AddMaterialStream("pretreated")
    solids     = fs.AddMaterialStream("solids")
    liquor     = fs.AddMaterialStream("liquor")

    pt = (fs.AddPretreatmentReactor("PT-1")
            .WithTechnology(PretreatmentType.DiluteAcid)
            .WithResidenceTime(Q.Minutes(15))
            .WithCelluloseConversion(0.10)
            .WithHemicelluloseConversion(0.85)
            .ConnectFeed(biomass)
            .ConnectProduct(pretreated))
    r = pt.Object
    r.CelluloseCompound     = "Sucrose"
    r.HemicelluloseCompound = "Lactose"
    r.LigninCompound        = "Coal"
    r.GlucoseCompound       = "Glucose"
    r.XyloseCompound        = "Xylose"

    (fs.AddCentrifuge("CF-1")
       .WithTechnology(CentrifugeType.Decanter)
       .WithDefaultRecoveryToHeavy(0.10)          # share of the liquid that leaves with the cake
       .WithRecoveryToHeavy("Sucrose", 0.95)
       .WithRecoveryToHeavy("Lactose", 0.95)
       .WithRecoveryToHeavy("Coal",    0.95)
       .ConnectFeed(pretreated)
       .ConnectProduct(solids, 0)                 # heavy phase
       .ConnectProduct(liquor, 1))                # light phase

    fs.AutoLayout(); fs.Solve()
    print(f"Glucose released = {r.Result_GlucoseProduced_kgs*3600:.1f} kg/h")
    print(f"Xylose released  = {r.Result_XyloseProduced_kgs*3600:.1f} kg/h")
    print(f"Solids flow      = {solids.MassFlowKgPerSecond*3600:.1f} kg/h")
    print(f"Liquor flow      = {liquor.MassFlowKgPerSecond*3600:.1f} kg/h")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;
    using DWSIM.UnitOperations.Reactors;
    using CentrifugeType = DWSIM.UnitOperations.UnitOperations.CentrifugeType;

    var fs = Flowsheet.Create("BioPretreat")
        .WithCompounds("Water", "Sucrose", "Lactose", "Coal", "Glucose", "Xylose")
        .WithPropertyPackage(PropertyPackages.NRTL);

    var biomass = fs.AddMaterialStream("biomass")
        .At(443.15.Kelvin(), 10.Bar())
        .WithMassFlow(1000.KgPerHour())
        .WithComposition(c => c
            .Mass("Water",   0.30)
            .Mass("Sucrose", 0.40)     // cellulose
            .Mass("Lactose", 0.20)     // hemicellulose
            .Mass("Coal",    0.10));   // lignin

    var pretreated = fs.AddMaterialStream("pretreated");
    var solids     = fs.AddMaterialStream("solids");
    var liquor     = fs.AddMaterialStream("liquor");

    var pt = fs.AddPretreatmentReactor("PT-1")
        .WithTechnology(PretreatmentType.DiluteAcid)
        .WithResidenceTime(15.Minutes())
        .WithCelluloseConversion(0.10)
        .WithHemicelluloseConversion(0.85)
        .ConnectFeed(biomass)
        .ConnectProduct(pretreated);
    var r = pt.Object;
    r.CelluloseCompound     = "Sucrose";
    r.HemicelluloseCompound = "Lactose";
    r.LigninCompound        = "Coal";
    r.GlucoseCompound       = "Glucose";
    r.XyloseCompound        = "Xylose";

    fs.AddCentrifuge("CF-1")
      .WithTechnology(CentrifugeType.Decanter)
      .WithDefaultRecoveryToHeavy(0.10)
      .WithRecoveryToHeavy("Sucrose", 0.95)
      .WithRecoveryToHeavy("Lactose", 0.95)
      .WithRecoveryToHeavy("Coal",    0.95)
      .ConnectFeed(pretreated)
      .ConnectProduct(solids, 0)
      .ConnectProduct(liquor, 1);

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"Glucose released = {r.Result_GlucoseProduced_kgs*3600:F1} kg/h");
    System.Console.WriteLine($"Xylose released  = {r.Result_XyloseProduced_kgs*3600:F1} kg/h");
    System.Console.WriteLine($"Solids flow      = {solids.MassFlowKgPerSecond*3600:F1} kg/h");
    System.Console.WriteLine($"Liquor flow      = {liquor.MassFlowKgPerSecond*3600:F1} kg/h");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI
    Imports DWSIM.UnitOperations.Reactors
    Imports CentrifugeType = DWSIM.UnitOperations.UnitOperations.CentrifugeType

    Module Program
        Sub Main()
            Dim fs = Flowsheet.Create("BioPretreat") _
                .WithCompounds("Water", "Sucrose", "Lactose", "Coal", "Glucose", "Xylose") _
                .WithPropertyPackage(PropertyPackages.NRTL)

            Dim biomass = fs.AddMaterialStream("biomass") _
                .At(443.15.Kelvin(), 10.0.Bar()) _
                .WithMassFlow(1000.0.KgPerHour()) _
                .WithComposition(Sub(c) c _
                    .Mass("Water", 0.3) _
                    .Mass("Sucrose", 0.4) _
                    .Mass("Lactose", 0.2) _
                    .Mass("Coal", 0.1))

            Dim pretreated = fs.AddMaterialStream("pretreated")
            Dim solids     = fs.AddMaterialStream("solids")
            Dim liquor     = fs.AddMaterialStream("liquor")

            Dim pt = fs.AddPretreatmentReactor("PT-1") _
                .WithTechnology(PretreatmentType.DiluteAcid) _
                .WithResidenceTime(15.0.Minutes()) _
                .WithCelluloseConversion(0.1) _
                .WithHemicelluloseConversion(0.85) _
                .ConnectFeed(biomass) _
                .ConnectProduct(pretreated)
            Dim r = pt.Object
            r.CelluloseCompound = "Sucrose"
            r.HemicelluloseCompound = "Lactose"
            r.LigninCompound = "Coal"
            r.GlucoseCompound = "Glucose"
            r.XyloseCompound = "Xylose"

            fs.AddCentrifuge("CF-1") _
              .WithTechnology(CentrifugeType.Decanter) _
              .WithDefaultRecoveryToHeavy(0.1) _
              .WithRecoveryToHeavy("Sucrose", 0.95) _
              .WithRecoveryToHeavy("Lactose", 0.95) _
              .WithRecoveryToHeavy("Coal", 0.95) _
              .ConnectFeed(pretreated) _
              .ConnectProduct(solids, 0) _
              .ConnectProduct(liquor, 1)

            fs.AutoLayout()
            fs.Solve()
            Console.WriteLine($"Glucose released = {r.Result_GlucoseProduced_kgs * 3600:F1} kg/h")
            Console.WriteLine($"Xylose released  = {r.Result_XyloseProduced_kgs * 3600:F1} kg/h")
            Console.WriteLine($"Solids flow      = {solids.MassFlowKgPerSecond * 3600:F1} kg/h")
            Console.WriteLine($"Liquor flow      = {liquor.MassFlowKgPerSecond * 3600:F1} kg/h")
        End Sub
    End Module
    ```

Running the Python version prints:

```text
Glucose released = 44.4 kg/h
Xylose released  = 193.2 kg/h
Solids flow      = 516.5 kg/h
Liquor flow      = 483.5 kg/h
```

The reactor converts 10 % of the cellulose and 85 % of the hemicellulose.
Hydrolysis takes up water, so the 40 kg/h of cellulose converted gives
44.4 kg/h of glucose and the 170 kg/h of hemicellulose gives 193.2 kg/h of
xylose. The decanter sends 95 % of the remaining solids and 10 % of the rest
to the cake, and the 1000 kg/h feed leaves as 516.5 kg/h of solids and
483.5 kg/h of sugar liquor.
