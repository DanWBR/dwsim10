using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Poly(ethylene glycol) dewatering: concentrating an aqueous PEG solution by flashing off water.
    /// PEG is a hydrogen-bonding polymer (its chain-end hydroxyls and the ether oxygens along the chain all
    /// associate with water), so it holds water strongly and is itself non-volatile. A 20 wt% PEG solution
    /// (Mn = 10 000 g/mol) is heated to 355 K under a mild vacuum (0.4 bar): the water flashes off as pure
    /// vapour and the PEG is concentrated to about 70 wt%.
    /// Property package: PC-SAFT with association (PEG modelled as a 4C + ether-site associating polymer),
    /// solved by the PC-SAFT flash that recomputes the solvent K-value at each trial composition - a plain
    /// vapour-liquid flash oscillates here because water's activity in PEG is strongly, steeply non-ideal.
    /// Checks: mass balance, the vapour is pure water (no polymer), and the solution is concentrated.</summary>
    internal static class PegDewateringSample
    {
        private static string AddcompsDir([CallerFilePath] string sourceFile = "")
        {
            var dir = Path.GetDirectoryName(sourceFile);
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "content", "addcomps"));
        }

        public static void Run()
        {
            var cp = Newtonsoft.Json.JsonConvert.DeserializeObject<DWSIM.Thermodynamics.BaseClasses.ConstantProperties>(
                File.ReadAllText(Path.Combine(AddcompsDir(), "Poly_ethylene_glycol.json")));
            cp.CurrentDB = "User"; cp.OriginalDB = "User"; cp.Molar_Weight = 10000.0;

            var fs = Flowsheet.Create("PegDewatering")
                .WithCompounds("Water")
                .WithCompound(cp)
                .WithPropertyPackage(PropertyPackages.PCSAFT);

            const string pegName = "Poly(ethylene glycol)";

            var feed = fs.AddMaterialStream("aqueous PEG")
                .At(320.0.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.80)
                .SetCompoundMassFlow(pegName, 0.20);

            var hotFeed = fs.AddMaterialStream("heated solution");
            fs.AddHeater("H-1")
                .WithOutletTemperature(355.0.Kelvin())
                .WithPressureDrop((101325.0 - 40000.0).Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(hotFeed, 0);

            var waterVapor = fs.AddMaterialStream("water vapour");
            var concentrate = fs.AddMaterialStream("concentrated PEG");
            fs.AddSeparator("V-1")
                .ConnectFeed(hotFeed, 0)
                .ConnectProduct(waterVapor, 0)
                .ConnectProduct(concentrate, 1);

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("Solver reported: " +
                    string.Join("; ", errors.Select(e => e.Message)));

            double feedPeg = feed.OverallMassFraction(pegName);
            double concPeg = concentrate.OverallMassFraction(pegName);
            double vaporWater = waterVapor.OverallMassFraction("Water");
            double vaporPeg = waterVapor.OverallMassFraction(pegName);

            new ResultTable("PEG dewatering (flash off water, PC-SAFT with association)")
                .Row("Mass balance F = vapour + concentrate", feed.MassFlowKgPerSecond,
                     waterVapor.MassFlowKgPerSecond + concentrate.MassFlowKgPerSecond, 0.005, "kg/s")
                .RowInRange("Vapour is pure water (>99.9 wt%)", 0.999, 1.0, vaporWater, "-")
                .RowInRange("No polymer in the vapour (<1 ppm)", 0.0, 1e-6, vaporPeg, "-")
                .RowInRange("The solution is concentrated to > 50 wt% PEG", 0.50, 1.0, concPeg, "-")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "peg-dewatering");
        }
    }
}
