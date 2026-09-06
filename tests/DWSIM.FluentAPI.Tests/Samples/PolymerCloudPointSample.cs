using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Polymer solution cloud point (liquid-liquid demixing). A 20 wt% polypropylene solution in
    /// n-pentane is a single liquid at low temperature but demixes into a polymer-rich and a solvent-rich
    /// liquid as it is heated toward the solvent's critical region (lower critical solution behaviour). At
    /// 460 K and 40 bar the solution is inside the miscibility gap, so a three-phase separator splits it into
    /// a concentrated polymer phase and an almost pure n-pentane phase.
    /// Property package: PC-SAFT, whose segment model reproduces the Tumakaka et al. (2002) cloud curve.
    /// Checks: two liquid phases form, one polymer-rich and one nearly pure solvent, and the split balances.</summary>
    internal static class PolymerCloudPointSample
    {
        private static string AddcompsDir([CallerFilePath] string sourceFile = "")
        {
            var dir = Path.GetDirectoryName(sourceFile);
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "content", "addcomps"));
        }

        public static void Run()
        {
            var polypropylene = Path.Combine(AddcompsDir(), "Polypropylene.json");

            var fs = Flowsheet.Create("PolymerCloudPoint")
                .WithCompounds("N-pentane")
                .WithCompoundFromJson(polypropylene)
                .WithPropertyPackage(PropertyPackages.PCSAFT);

            // 20 wt% polypropylene solution, inside the miscibility gap at 460 K / 40 bar.
            var feed = fs.AddMaterialStream("polymer solution")
                .At(460.15.Kelvin(), 4.0e6.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("N-pentane", 0.80)
                .SetCompoundMassFlow("Polypropylene", 0.20);

            var vapor = fs.AddMaterialStream("vapour (none)");
            var solventPhase = fs.AddMaterialStream("solvent-rich liquid");
            var polymerPhase = fs.AddMaterialStream("polymer-rich liquid");
            fs.AddSeparator("V-1")
                .ConnectFeed(feed, 0)
                .ConnectProduct(vapor, 0)
                .ConnectProduct(solventPhase, 1)
                .ConnectProduct(polymerPhase, 2);

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("Solver reported: " +
                    string.Join("; ", errors.Select(e => e.Message)));

            double wSolvent = solventPhase.OverallMassFraction("Polypropylene");
            double wPolymer = polymerPhase.OverallMassFraction("Polypropylene");
            // Order the two liquid outlets by polymer content (the separator's light/heavy assignment
            // depends on density, which for a polymer solution is not the discriminator we care about).
            double wLean = Math.Min(wSolvent, wPolymer);
            double wRich = Math.Max(wSolvent, wPolymer);
            double mLiquids = solventPhase.MassFlowKgPerSecond + polymerPhase.MassFlowKgPerSecond;

            new ResultTable("Polypropylene / n-pentane cloud point (LLE, PC-SAFT)")
                .Row("Mass balance F = liquid1 + liquid2", feed.MassFlowKgPerSecond, mLiquids, 0.005, "kg/s")
                .RowInRange("A polymer-rich liquid forms (>15 wt% PP)", 0.15, 1.0, wRich, "-")
                .RowInRange("A nearly pure n-pentane liquid forms (<2 wt% PP)", 0.0, 0.02, wLean, "-")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "polymer-cloud-point");
        }
    }
}
