# 05 — Anaerobic Digester

Sludge → ADM1-Lite digester → biogas + digestate.

=== "Python"

    ```python
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyAD")
          .WithCompounds("Water", "Acetic acid", "Methane", "Carbon dioxide")
          .WithPropertyPackage(PropertyPackages.NRTL))

    sludge = (fs.AddMaterialStream("sludge")
              .WithTemperature(Q.Kelvin(308.15))
              .WithMassFlow(Q.KgPerHour(500))
              .WithComposition(lambda c: c
                  .Mass("Water",       0.95)
                  .Mass("Acetic acid", 0.05)))

    biogas    = fs.AddMaterialStream("biogas")
    digestate = fs.AddMaterialStream("digestate")

    (fs.AddAnaerobicDigester("AD-1")
       .WithModel("ADM1Lite")
       .WithVolume(Q.CubicMeters(500))
       .WithRetentionTime(Q.Days(20))
       .WithTemperature(Q.Kelvin(308.15))
       .ConnectFeed(sludge)
       .ConnectProduct(biogas, 0)
       .ConnectProduct(digestate, 1))

    fs.AutoLayout(); fs.Solve()
    print(f"Biogas flow = {biogas.MassFlowKgPerSecond*3600:.2f} kg/h")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("AD")
        .WithCompounds("Water", "Acetic acid", "Methane", "Carbon dioxide")
        .WithPropertyPackage(PropertyPackages.NRTL);

    var sludge = fs.AddMaterialStream("sludge")
        .WithTemperature(308.15.Kelvin())
        .WithMassFlow(500.KgPerHour())
        .WithComposition(c => c
            .Mass("Water",       0.95)
            .Mass("Acetic acid", 0.05));

    var biogas    = fs.AddMaterialStream("biogas");
    var digestate = fs.AddMaterialStream("digestate");

    fs.AddAnaerobicDigester("AD-1")
      .WithModel("ADM1Lite")
      .WithVolume(500.CubicMeters())
      .WithRetentionTime(20.Days())
      .WithTemperature(308.15.Kelvin())
      .ConnectFeed(sludge)
      .ConnectProduct(biogas, 0)
      .ConnectProduct(digestate, 1);

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"Biogas flow = {biogas.MassFlowKgPerSecond*3600:F2} kg/h");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    Dim fs = Flowsheet.Create("AD") _
        .WithCompounds("Water", "Acetic acid", "Methane", "Carbon dioxide") _
        .WithPropertyPackage(PropertyPackages.NRTL)

    Dim sludge = fs.AddMaterialStream("sludge") _
        .WithTemperature(308.15.Kelvin()) _
        .WithMassFlow(500.0.KgPerHour()) _
        .WithComposition(Sub(c) c _
            .Mass("Water", 0.95) _
            .Mass("Acetic acid", 0.05))

    Dim biogas    = fs.AddMaterialStream("biogas")
    Dim digestate = fs.AddMaterialStream("digestate")

    fs.AddAnaerobicDigester("AD-1") _
      .WithModel("ADM1Lite") _
      .WithVolume(500.0.CubicMeters()) _
      .WithRetentionTime(20.0.Days()) _
      .WithTemperature(308.15.Kelvin()) _
      .ConnectFeed(sludge) _
      .ConnectProduct(biogas, 0) _
      .ConnectProduct(digestate, 1)

    fs.AutoLayout()
    fs.Solve()
    ```
