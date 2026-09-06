using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Polystyrene devolatilization: strip the solvent from a polymer solution under vacuum.
    /// A 25 wt% polystyrene / 75 wt% ethylbenzene solution (the effluent of a solution-polymerization
    /// reactor) is heated to 470 K and flashed at 0.15 bar. Ethylbenzene, the only volatile species,
    /// leaves as vapour; the polystyrene is non-volatile and stays behind as a concentrated melt.
    /// Property package: PC-SAFT (the polymer is a segment chain, m = (m/M).Mn).
    /// Checks: mass balance, the vapour is essentially pure solvent (no polymer), and the melt is a
    /// high-purity polystyrene product.</summary>
    internal static class PolymerDevolatilizationSample
    {
        private static string AddcompsDir([CallerFilePath] string sourceFile = "")
        {
            // <repo>/tests/DWSIM.FluentAPI.Tests/Samples/<this file> -> <repo>/content/addcomps
            var dir = Path.GetDirectoryName(sourceFile);
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "content", "addcomps"));
        }

        public static void Run()
        {
            var polystyrene = Path.Combine(AddcompsDir(), "Polystyrene.json");

            var fs = Flowsheet.Create("PolymerDevolatilization")
                .WithCompounds("Ethylbenzene")
                .WithCompoundFromJson(polystyrene)
                .WithPropertyPackage(PropertyPackages.PCSAFT);

            // Reactor effluent: a 25 wt% polystyrene solution, still a single liquid at 1 atm.
            var feed = fs.AddMaterialStream("reactor effluent")
                .At(320.0.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Ethylbenzene", 0.75)
                .SetCompoundMassFlow("Polystyrene", 0.25);

            // Devolatilizer: heat to 470 K and drop to 0.15 bar so the ethylbenzene flashes off.
            var hotFeed = fs.AddMaterialStream("heated feed");
            fs.AddHeater("H-1")
                .WithOutletTemperature(470.0.Kelvin())
                .WithPressureDrop((101325.0 - 15000.0).Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(hotFeed, 0);

            var solventVapor = fs.AddMaterialStream("solvent vapour");
            var polymerMelt = fs.AddMaterialStream("polymer melt");
            fs.AddSeparator("V-1")
                .ConnectFeed(hotFeed, 0)
                .ConnectProduct(solventVapor, 0)
                .ConnectProduct(polymerMelt, 1);

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("Solver reported: " +
                    string.Join("; ", errors.Select(e => e.Message)));

            double meltPS = polymerMelt.OverallMassFraction("Polystyrene");
            double vaporPS = solventVapor.OverallMassFraction("Polystyrene");
            double vaporEB = solventVapor.OverallMassFraction("Ethylbenzene");
            double solventRecovered = solventVapor.MassFlowKgPerSecond /
                                      feed.MassFlowKgPerSecond / 0.75; // fraction of fed EB recovered as vapour

            new ResultTable("Polystyrene devolatilization (flash off ethylbenzene, PC-SAFT)")
                .Row("Mass balance F = vapour + melt", feed.MassFlowKgPerSecond,
                     solventVapor.MassFlowKgPerSecond + polymerMelt.MassFlowKgPerSecond, 0.005, "kg/s")
                .RowInRange("Solvent vapour is essentially pure ethylbenzene (>99 wt%)", 0.99, 1.0, vaporEB, "-")
                .RowInRange("No polymer in the vapour (<1 ppm)", 0.0, 1e-6, vaporPS, "-")
                .RowInRange("Melt is a concentrated polystyrene product (>90 wt%)", 0.90, 1.0, meltPS, "-")
                .RowInRange("Most of the fed solvent is recovered (>90 %)", 0.90, 1.0, solventRecovered, "-")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "polymer-devolatilization");
        }
    }
}
