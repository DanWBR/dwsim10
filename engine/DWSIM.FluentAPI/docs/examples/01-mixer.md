# 01 — Mixer

Two water streams at different temperatures, mixed under IAPWS-IF97 steam
tables. Demonstrates: flowsheet creation, compounds, property packages,
material streams with `At(T, P)`, mixer port-by-port connection, solver,
read-back.

=== "Python"

    ```python
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    fs = (Flowsheet.Create("PyMixer")
          .WithCompound("Water")
          .WithPropertyPackage(PropertyPackages.SteamTables))

    inlet1 = (fs.AddMaterialStream("inlet1")
              .At(Q.Kelvin(300.0), Q.Pascal(101325.0))
              .WithMassFlow(Q.KgPerSecond(100.0)))

    inlet2 = (fs.AddMaterialStream("inlet2")
              .At(Q.Kelvin(348.0), Q.Pascal(101325.0))
              .WithMassFlow(Q.KgPerSecond(50.0)))

    outlet = fs.AddMaterialStream("outlet")

    (fs.AddMixer("MIX-1")
       .ConnectFeed(inlet1, 0)
       .ConnectFeed(inlet2, 1)
       .ConnectProduct(outlet, 0))

    fs.AutoLayout()
    fs.Solve()

    print(f"Outlet T  = {outlet.TemperatureK:.4f} K")
    print(f"Mass flow = {outlet.MassFlowKgPerSecond:.4f} kg/s")
    ```

=== "C#"

    ```csharp
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("CSharpMixer")
        .WithCompound("Water")
        .WithPropertyPackage(PropertyPackages.SteamTables);

    var inlet1 = fs.AddMaterialStream("inlet1")
        .At(300.Kelvin(), 1.Atm())
        .WithMassFlow(100.KgPerSecond());

    var inlet2 = fs.AddMaterialStream("inlet2")
        .At(348.Kelvin(), 1.Atm())
        .WithMassFlow(50.KgPerSecond());

    var outlet = fs.AddMaterialStream("outlet");

    fs.AddMixer("MIX-1")
      .ConnectFeed(inlet1, 0)
      .ConnectFeed(inlet2, 1)
      .ConnectProduct(outlet, 0);

    fs.AutoLayout();
    fs.Solve();

    System.Console.WriteLine($"Outlet T  = {outlet.TemperatureK:F4} K");
    System.Console.WriteLine($"Mass flow = {outlet.MassFlowKgPerSecond:F4} kg/s");
    ```

=== "VB.NET"

    ```vbnet
    Imports DWSIM.Automation.FluentAPI

    Module Program
        Sub Main()
            Dim fs = Flowsheet.Create("VBMixer") _
                .WithCompound("Water") _
                .WithPropertyPackage(PropertyPackages.SteamTables)

            Dim inlet1 = fs.AddMaterialStream("inlet1") _
                .At(300.0.Kelvin(), 1.0.Atm()) _
                .WithMassFlow(100.0.KgPerSecond())

            Dim inlet2 = fs.AddMaterialStream("inlet2") _
                .At(348.0.Kelvin(), 1.0.Atm()) _
                .WithMassFlow(50.0.KgPerSecond())

            Dim outlet = fs.AddMaterialStream("outlet")

            fs.AddMixer("MIX-1") _
              .ConnectFeed(inlet1, 0) _
              .ConnectFeed(inlet2, 1) _
              .ConnectProduct(outlet, 0)

            fs.AutoLayout()
            fs.Solve()

            Console.WriteLine($"Outlet T  = {outlet.TemperatureK:F4} K")
            Console.WriteLine($"Mass flow = {outlet.MassFlowKgPerSecond:F4} kg/s")
        End Sub
    End Module
    ```
