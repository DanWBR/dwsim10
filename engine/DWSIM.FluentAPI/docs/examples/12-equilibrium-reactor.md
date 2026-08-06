# 12 — Equilibrium Reactor (Water-Gas Shift)

WGS reaction at 600 K, isothermal, with a `ln(Keq)` expression.

=== "Python"

    ```python
    from System.Collections.Generic import Dictionary
    from System import String, Double
    from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

    def stoich(d):
        out = Dictionary[String, Double]()
        for k, v in d.items(): out[k] = float(v)
        return out

    fs = (Flowsheet.Create("PyEqReact")
          .WithCompounds("Carbon monoxide", "Water", "Carbon dioxide", "Hydrogen")
          .WithPropertyPackage(PropertyPackages.PengRobinson))

    wgs = fs.DefineEquilibriumReaction(
        "WGS",
        stoich({"Carbon monoxide": -1, "Water": -1, "Carbon dioxide": 1, "Hydrogen": 1}),
        "Carbon monoxide", "Vapor", "Activity", "",
        "4577.8/T - 4.33", 0.0)

    fs.ReactionSet("WGSset").Add(wgs)

    feed = (fs.AddMaterialStream("syngas")
            .WithTemperature(Q.Kelvin(600))
            .WithMolarFlow(Q.MolPerSecond(10))
            .SetCompoundMolarFlow("Carbon monoxide", 5.0)
            .SetCompoundMolarFlow("Water",           5.0))

    out  = fs.AddMaterialStream("shifted")
    heat = fs.AddEnergyStream("Q")

    rxn = (fs.AddEquilibriumReactor("R-WGS")
             .Isothermal()
             .WithReactionSet("WGSset")
             .ConnectFeed(feed)
             .ConnectProduct(out)
             .ConnectEnergyFeed(heat))

    fs.AutoLayout(); fs.Solve()
    print(f"H2 mole frac = {out.OverallMoleFraction('Hydrogen'):.4f}")
    print(f"Heat duty    = {rxn.HeatDutyKW:.2f} kW")
    ```

=== "C#"

    ```csharp
    using System.Collections.Generic;
    using DWSIM.Automation.FluentAPI;

    var fs = Flowsheet.Create("EqReact")
        .WithCompounds("Carbon monoxide", "Water", "Carbon dioxide", "Hydrogen")
        .WithPropertyPackage(PropertyPackages.PengRobinson);

    var wgs = fs.DefineEquilibriumReaction("WGS",
        new Dictionary<string, double> {
            ["Carbon monoxide"] = -1, ["Water"] = -1,
            ["Carbon dioxide"]  =  1, ["Hydrogen"] = 1 },
        "Carbon monoxide", "Vapor", "Activity", "",
        "4577.8/T - 4.33", 0.0);

    fs.ReactionSet("WGSset").Add(wgs);

    var feed = fs.AddMaterialStream("syngas")
        .WithTemperature(600.Kelvin()).WithMolarFlow(10.MolPerSecond())
        .SetCompoundMolarFlow("Carbon monoxide", 5.0)
        .SetCompoundMolarFlow("Water",           5.0);

    var prod = fs.AddMaterialStream("shifted");
    var q    = fs.AddEnergyStream("Q");

    var rxn = fs.AddEquilibriumReactor("R-WGS")
        .Isothermal()
        .WithReactionSet("WGSset")
        .ConnectFeed(feed).ConnectProduct(prod)
        .ConnectEnergyFeed(q);

    fs.AutoLayout();
    fs.Solve();
    System.Console.WriteLine($"H2 = {prod.OverallMoleFraction("Hydrogen"):F4}");
    System.Console.WriteLine($"Q  = {rxn.HeatDutyKW:F2} kW");
    ```

=== "VB.NET"

    ```vbnet
    Imports System.Collections.Generic
    Imports DWSIM.Automation.FluentAPI

    Dim fs = Flowsheet.Create("EqReact") _
        .WithCompounds("Carbon monoxide", "Water", "Carbon dioxide", "Hydrogen") _
        .WithPropertyPackage(PropertyPackages.PengRobinson)

    Dim s As New Dictionary(Of String, Double) From {
        {"Carbon monoxide", -1}, {"Water", -1},
        {"Carbon dioxide",   1}, {"Hydrogen", 1}}

    Dim wgs = fs.DefineEquilibriumReaction("WGS", s,
        "Carbon monoxide", "Vapor", "Activity", "", "4577.8/T - 4.33", 0.0)
    fs.ReactionSet("WGSset").Add(wgs)

    Dim feed = fs.AddMaterialStream("syngas") _
        .WithTemperature(600.0.Kelvin()).WithMolarFlow(10.0.MolPerSecond()) _
        .SetCompoundMolarFlow("Carbon monoxide", 5.0) _
        .SetCompoundMolarFlow("Water",           5.0)

    Dim prod = fs.AddMaterialStream("shifted")
    Dim q    = fs.AddEnergyStream("Q")

    Dim rxn = fs.AddEquilibriumReactor("R-WGS") _
        .Isothermal() _
        .WithReactionSet("WGSset") _
        .ConnectFeed(feed).ConnectProduct(prod) _
        .ConnectEnergyFeed(q)

    fs.AutoLayout()
    fs.Solve()
    ```
