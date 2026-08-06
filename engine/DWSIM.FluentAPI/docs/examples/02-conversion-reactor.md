# 02 — Conversion Reactor

Steam-methane reforming with two parallel conversion reactions, isothermal
mode at 1000 K, Peng-Robinson EOS.

=== "Python"

    ```python
    from System.Collections.Generic import Dictionary
    from System import String, Double
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    def stoich(d):
        out = Dictionary[String, Double]()
        for k, v in d.items(): out[k] = float(v)
        return out

    fs = (Flowsheet.Create("PyConvReactor")
          .WithCompounds("Carbon dioxide", "Carbon monoxide", "Water", "Hydrogen", "Methane")
          .WithPropertyPackage(PropertyPackages.PengRobinson))

    r1 = fs.DefineConversionReaction(
        "R1", stoich({"Methane": -1, "Water": -2, "Carbon dioxide": 1, "Hydrogen": 4}),
        "Methane", "Vapor", "50")
    r2 = fs.DefineConversionReaction(
        "R2", stoich({"Methane": -1, "Water": -1, "Carbon monoxide": 1, "Hydrogen": 3}),
        "Water", "Vapor", "50")

    fs.ReactionSet("DefaultSet").Add(r1).Add(r2)

    feed = (fs.AddMaterialStream("inlet")
            .WithTemperature(Q.Kelvin(1000.0))
            .WithMolarFlow(Q.MolPerSecond(5.0))
            .SetCompoundMolarFlow("Methane", 2.0)
            .SetCompoundMolarFlow("Water",   3.0))

    gas_out = fs.AddMaterialStream("gas outlet")
    liq_out = fs.AddMaterialStream("liquid outlet")
    heat    = fs.AddEnergyStream("heat")

    reactor = (fs.AddConversionReactor("R-1")
               .Isothermal()
               .WithReactionSet("DefaultSet")
               .WithPressureDrop(Q.Pascal(0.0))
               .ConnectFeed(feed, 0)
               .ConnectProduct(gas_out, 0)
               .ConnectProduct(liq_out, 1)
               .ConnectEnergyFeed(heat, 1))

    fs.AutoLayout(); fs.Solve()

    print(f"Reactor heat duty = {reactor.HeatDutyKW:.4f} kW")
    for kv in reactor.Object.ComponentConversions:
        if kv.Value > 0:
            print(f"  {kv.Key}: {kv.Value*100:.2f}%")
    ```

=== "C#"

    ```csharp
    using System.Collections.Generic;
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("ConvReactor")
        .WithCompounds("Carbon dioxide", "Carbon monoxide", "Water", "Hydrogen", "Methane")
        .WithPropertyPackage(PropertyPackages.PengRobinson);

    var r1 = fs.DefineConversionReaction("R1",
        new Dictionary<string, double> {
            ["Methane"] = -1, ["Water"] = -2,
            ["Carbon dioxide"] = 1, ["Hydrogen"] = 4 },
        "Methane", "Vapor", "50");

    var r2 = fs.DefineConversionReaction("R2",
        new Dictionary<string, double> {
            ["Methane"] = -1, ["Water"] = -1,
            ["Carbon monoxide"] = 1, ["Hydrogen"] = 3 },
        "Water", "Vapor", "50");

    fs.ReactionSet("DefaultSet").Add(r1).Add(r2);

    var feed = fs.AddMaterialStream("inlet")
        .WithTemperature(1000.Kelvin())
        .WithMolarFlow(5.MolPerSecond())
        .SetCompoundMolarFlow("Methane", 2.0)
        .SetCompoundMolarFlow("Water",   3.0);

    var gasOut = fs.AddMaterialStream("gas outlet");
    var liqOut = fs.AddMaterialStream("liquid outlet");
    var heat   = fs.AddEnergyStream("heat");

    var reactor = fs.AddConversionReactor("R-1")
        .Isothermal()
        .WithReactionSet("DefaultSet")
        .WithPressureDrop(0.Pascal())
        .ConnectFeed(feed, 0)
        .ConnectProduct(gasOut, 0)
        .ConnectProduct(liqOut, 1)
        .ConnectEnergyFeed(heat, 1);

    fs.AutoLayout();
    fs.Solve();

    System.Console.WriteLine($"Reactor heat duty = {reactor.HeatDutyKW:F4} kW");
    foreach (var kv in reactor.Object.ComponentConversions)
        if (kv.Value > 0)
            System.Console.WriteLine($"  {kv.Key}: {kv.Value*100:F2}%");
    ```

=== "VB.NET"

    ```vbnet
    Imports System.Collections.Generic
    Imports DWSIM.Automation.FluentAPI

    Module Program
        Sub Main()
            Dim fs = Flowsheet.Create("ConvReactor") _
                .WithCompounds("Carbon dioxide", "Carbon monoxide", "Water", "Hydrogen", "Methane") _
                .WithPropertyPackage(PropertyPackages.PengRobinson)

            Dim s1 As New Dictionary(Of String, Double) From {
                {"Methane", -1}, {"Water", -2},
                {"Carbon dioxide", 1}, {"Hydrogen", 4}}

            Dim s2 As New Dictionary(Of String, Double) From {
                {"Methane", -1}, {"Water", -1},
                {"Carbon monoxide", 1}, {"Hydrogen", 3}}

            Dim r1 = fs.DefineConversionReaction("R1", s1, "Methane", "Vapor", "50")
            Dim r2 = fs.DefineConversionReaction("R2", s2, "Water",   "Vapor", "50")

            fs.ReactionSet("DefaultSet").Add(r1).Add(r2)

            Dim feed = fs.AddMaterialStream("inlet") _
                .WithTemperature(1000.0.Kelvin()) _
                .WithMolarFlow(5.0.MolPerSecond()) _
                .SetCompoundMolarFlow("Methane", 2.0) _
                .SetCompoundMolarFlow("Water",   3.0)

            Dim gasOut = fs.AddMaterialStream("gas outlet")
            Dim liqOut = fs.AddMaterialStream("liquid outlet")
            Dim heat   = fs.AddEnergyStream("heat")

            Dim reactor = fs.AddConversionReactor("R-1") _
                .Isothermal() _
                .WithReactionSet("DefaultSet") _
                .WithPressureDrop(0.0.Pascal()) _
                .ConnectFeed(feed, 0) _
                .ConnectProduct(gasOut, 0) _
                .ConnectProduct(liqOut, 1) _
                .ConnectEnergyFeed(heat, 1)

            fs.AutoLayout()
            fs.Solve()

            Console.WriteLine($"Reactor heat duty = {reactor.HeatDutyKW:F4} kW")
        End Sub
    End Module
    ```
