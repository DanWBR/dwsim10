using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsAdvanced
{
    /// <summary>Conversion Reactor - CH4 + 2 H2O → CO2 + 4 H2 (steam reforming).
    /// Specified conversion = 50 % of CH4 → closed stoichiometry should give:
    /// CH4_out = 0.5·CH4_in, H2O_out = H2O_in - 2·(0.5·CH4_in), CO2_out = 0.5·CH4_in, H2_out = 4·0.5·CH4_in.</summary>
    internal static class A02_ConvReactor_Stoichiometry
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("A02_ConvReactor")
                .WithCompounds("Carbon dioxide", "Water", "Hydrogen", "Methane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var r1 = fs.DefineConversionReaction("R1",
                new Dictionary<string, double> { { "Methane", -1 }, { "Water", -2 }, { "Carbon dioxide", 1 }, { "Hydrogen", 4 } },
                "Methane", "Vapor", "50");

            fs.ReactionSet("Set").Add(r1);

            const double nCH4 = 2.0;
            const double nH2O = 6.0;

            var feed = fs.AddMaterialStream("feed")
                .WithTemperature(1000.Kelvin())
                .WithMolarFlow((nCH4 + nH2O).MolPerSecond())
                .SetCompoundMolarFlow("Methane", nCH4)
                .SetCompoundMolarFlow("Water", nH2O)
                .SetCompoundMolarFlow("Carbon dioxide", 0.0)
                .SetCompoundMolarFlow("Hydrogen", 0.0);

            var gasOut = fs.AddMaterialStream("gasOut");
            var liqOut = fs.AddMaterialStream("liqOut");
            var heat = fs.AddEnergyStream("heat");

            fs.AddConversionReactor("R-1")
                .Isothermal()
                .WithReactionSet("Set")
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(feed, 0)
                .ConnectProduct(gasOut, 0)
                .ConnectProduct(liqOut, 1)
                .ConnectEnergyFeed(heat, 1);

            fs.Solve();

            // Sum of molar flows for each component across both products
            double Sum(string c) =>
                gasOut.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault()
                + liqOut.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault();

            double nCH4out = Sum("Methane");
            double nH2Oout = Sum("Water");
            double nCO2out = Sum("Carbon dioxide");
            double nH2out = Sum("Hydrogen");

            const double X = 0.5;     // 50 % CH4 conversion
            double extent = X * nCH4; // ξ
            double nCH4exp = nCH4 - extent;
            double nH2Oexp = nH2O - 2 * extent;
            double nCO2exp = extent;
            double nH2exp = 4 * extent;

            new ResultTable("Conv Reactor - CH4+2H2O→CO2+4H2 at 50% (stoichiometry)")
                .Row("CH4 out", nCH4exp, nCH4out, 0.005, "mol/s")
                .Row("H2O out", nH2Oexp, nH2Oout, 0.005, "mol/s")
                .Row("CO2 out", nCO2exp, nCO2out, 0.005, "mol/s")
                .Row("H2  out", nH2exp, nH2out, 0.005, "mol/s")
                .PrintAndThrowIfFailed();
        }
    }
}
