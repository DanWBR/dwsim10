using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsAdvanced
{
    /// <summary>Gibbs Reactor - free-energy minimization for steam reforming.
    /// CH4 + 2H2O ⇌ CO2 + 4H2 ; CH4 + H2O ⇌ CO + 3H2 (and internal WGS).
    /// Feed 1:3 CH4:H2O @ 1100 K, 1 bar. Expected: high CH4 conversion (>95 %),
    /// substantial H2 and CO + CO2 production.</summary>
    internal static class A05_Gibbs_SteamReforming
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("A05_Gibbs_SR")
                .WithCompounds("Methane", "Water", "Carbon dioxide", "Carbon monoxide", "Hydrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("feed")
                .WithTemperature(1100.Kelvin())
                .WithPressure(1e5.Pascal())
                .WithMolarFlow(4.0.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 1.0)
                .SetCompoundMolarFlow("Water", 3.0)
                .SetCompoundMolarFlow("Carbon dioxide", 0.0)
                .SetCompoundMolarFlow("Carbon monoxide", 0.0)
                .SetCompoundMolarFlow("Hydrogen", 0.0);

            var gasOut = fs.AddMaterialStream("gasOut");
            var liqOut = fs.AddMaterialStream("liqOut");
            var heat = fs.AddEnergyStream("heat");

            var rxBuilder = fs.AddGibbsReactor("R-G")
                .Isothermal()
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(feed, 0)
                .ConnectProduct(gasOut, 0)
                .ConnectProduct(liqOut, 1)
                .ConnectEnergyFeed(heat, 1);

            // CreateElementMatrix requires the feed to be connected (it reads the input stream).
            rxBuilder.Object.ComponentIDs = new List<string> { "Methane", "Water", "Carbon dioxide", "Carbon monoxide", "Hydrogen" };
            rxBuilder.Object.CreateElementMatrix();
            rxBuilder.Object.InitializeFromPreviousSolution = false;

            fs.Solve();

            double Sum(string c) =>
                gasOut.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault()
                + liqOut.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault();

            double nCH4_out = Sum("Methane");
            double nH2_out = Sum("Hydrogen");
            double nCO_out = Sum("Carbon monoxide");
            double nCO2_out = Sum("Carbon dioxide");

            double conv = (1.0 - nCH4_out) / 1.0;

            new ResultTable("Gibbs Reactor - steam reforming CH4 @ 1100 K, 1 bar")
                .RowInRange("CH4 conversion > 95 %", 0.95, 1.0, conv, "-")
                .RowInRange("H2 produced > 2 mol/s", 2.0, 4.0, nH2_out, "mol/s")
                .RowInRange("(CO + CO2) ≈ CH4 reacted", 0.95, 1.05, (nCO_out + nCO2_out) / (1.0 - nCH4_out), "-")
                .PrintAndThrowIfFailed();
        }
    }
}
