# 04 — Bioprocess Pretreatment Train

Lignocellulosic biomass → pretreatment → centrifugal solid/liquid split.
Demonstrates two free `IExternalUnitOperation`-backed bioprocess UOs
chained through plain material streams.

=== "Python"

    ```python
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyBioPretreat")
          .WithCompounds("Water", "Cellulose", "Hemicellulose", "Lignin", "Glucose", "Xylose")
          .WithPropertyPackage(PropertyPackages.NRTL))

    biomass = (fs.AddMaterialStream("biomass")
               .WithTemperature(Q.Kelvin(298.15))
               .WithMassFlow(Q.KgPerHour(1000))
               .WithComposition(lambda c: c
                   .Mass("Water",         0.30)
                   .Mass("Cellulose",     0.40)
                   .Mass("Hemicellulose", 0.20)
                   .Mass("Lignin",        0.10)))

    pretreated = fs.AddMaterialStream("pretreated")
    solids     = fs.AddMaterialStream("solids")
    liquor     = fs.AddMaterialStream("liquor")

    (fs.AddPretreatmentReactor("PT-1")
       .WithMode("DiluteAcid")
       .WithTemperature(Q.Kelvin(443.15))
       .WithResidenceTime(Q.Minutes(15))
       .WithCelluloseConversion(0.10)
       .WithHemicelluloseConversion(0.85)
       .ConnectFeed(biomass)
       .ConnectProduct(pretreated))

    (fs.AddCentrifuge("CF-1")
       .WithSolidsRecovery(0.95)
       .ConnectFeed(pretreated)
       .ConnectProduct(solids, 0)
       .ConnectProduct(liquor, 1))

    fs.AutoLayout(); fs.Solve()
    print(f"Liquor flow = {liquor.MassFlowKgPerSecond*3600:.1f} kg/h")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("BioPretreat")
        .WithCompounds("Water", "Cellulose", "Hemicellulose", "Lignin", "Glucose", "Xylose")
        .WithPropertyPackage(PropertyPackages.NRTL);

    var biomass = fs.AddMaterialStream("biomass")
        .WithTemperature(298.15.Kelvin())
        .WithMassFlow(1000.KgPerHour())
        .WithComposition(c => c
            .Mass("Water",         0.30)
            .Mass("Cellulose",     0.40)
            .Mass("Hemicellulose", 0.20)
            .Mass("Lignin",        0.10));

    var pretreated = fs.AddMaterialStream("pretreated");
    var solids     = fs.AddMaterialStream("solids");
    var liquor     = fs.AddMaterialStream("liquor");

    fs.AddPretreatmentReactor("PT-1")
      .WithMode("DiluteAcid")
      .WithTemperature(443.15.Kelvin())
      .WithResidenceTime(15.Minutes())
      .WithCelluloseConversion(0.10)
      .WithHemicelluloseConversion(0.85)
      .ConnectFeed(biomass)
      .ConnectProduct(pretreated);

    fs.AddCentrifuge("CF-1")
      .WithSolidsRecovery(0.95)
      .ConnectFeed(pretreated)
      .ConnectProduct(solids, 0)
      .ConnectProduct(liquor, 1);

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"Liquor flow = {liquor.MassFlowKgPerSecond*3600:F1} kg/h");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    Module Program
        Sub Main()
            Dim fs = Flowsheet.Create("BioPretreat") _
                .WithCompounds("Water", "Cellulose", "Hemicellulose", "Lignin", "Glucose", "Xylose") _
                .WithPropertyPackage(PropertyPackages.NRTL)

            Dim biomass = fs.AddMaterialStream("biomass") _
                .WithTemperature(298.15.Kelvin()) _
                .WithMassFlow(1000.0.KgPerHour()) _
                .WithComposition(Sub(c) c _
                    .Mass("Water", 0.3) _
                    .Mass("Cellulose", 0.4) _
                    .Mass("Hemicellulose", 0.2) _
                    .Mass("Lignin", 0.1))

            Dim pretreated = fs.AddMaterialStream("pretreated")
            Dim solids     = fs.AddMaterialStream("solids")
            Dim liquor     = fs.AddMaterialStream("liquor")

            fs.AddPretreatmentReactor("PT-1") _
              .WithMode("DiluteAcid") _
              .WithTemperature(443.15.Kelvin()) _
              .WithResidenceTime(15.0.Minutes()) _
              .WithCelluloseConversion(0.1) _
              .WithHemicelluloseConversion(0.85) _
              .ConnectFeed(biomass) _
              .ConnectProduct(pretreated)

            fs.AddCentrifuge("CF-1") _
              .WithSolidsRecovery(0.95) _
              .ConnectFeed(pretreated) _
              .ConnectProduct(solids, 0) _
              .ConnectProduct(liquor, 1)

            fs.AutoLayout()
            fs.Solve()
        End Sub
    End Module
    ```
