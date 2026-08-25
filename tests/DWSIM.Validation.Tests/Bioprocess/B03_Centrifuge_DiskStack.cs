using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using CentrifugeType = DWSIM.UnitOperations.UnitOperations.CentrifugeType;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>DiskStack centrifuge - separates biomass from fermented broth.
    /// Recovery of Biomass_Generic (macromolecule MW>10000) to the heavy phase = 95 %,
    /// dissolved glucose goes mainly to the clarified stream (recovery=0.02).
    /// Verifies overall mass balance (feed = heavy + light) after Solve.</summary>
    internal static class B03_Centrifuge_DiskStack
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("B03_Centrifuge")
                .WithCompounds("Water", "Glucose", "Biomass_Generic")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(10.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 9.0)
                .SetCompoundMassFlow("Glucose", 0.5)
                .SetCompoundMassFlow("Biomass_Generic", 0.5);

            var heavy = fs.AddMaterialStream("heavy");
            var light = fs.AddMaterialStream("light");

            var c = fs.AddCentrifuge("CENT-1")
                .Configure(o => o.CreateConnectors())          // headless: install 1 in + 2 out
                .WithTechnology(CentrifugeType.DiskStack)
                .WithBowlSpeedRpm(8500.0)
                .WithSigmaFactorM2(1500.0)
                .WithDefaultRecoveryToHeavy(0.95)
                .WithRecoveryToHeavy("Water", 0.05)
                .WithRecoveryToHeavy("Glucose", 0.02)
                .ConnectFeed(feed, 0)
                .ConnectProduct(heavy, 0)
                .ConnectProduct(light, 1);

            // External UOs in headless mode: call Calculate directly (FlowsheetSolver does not enqueue them)
            c.Object.Calculate();

            double mFeed = feed.MassFlowKgPerSecond;
            double mHeavy = heavy.MassFlowKgPerSecond;
            double mLight = light.MassFlowKgPerSecond;

            new ResultTable("Centrifuge - DiskStack solve, 10 kg/s")
                .Row("Mass balance F = H + L", mFeed, mHeavy + mLight, 0.001, "kg/s")
                .Row("FeedMass result", 10.0, c.Object.Result_FeedMass_kgs, 0.001, "kg/s")
                .Row("HeavyMass result", c.Object.Result_HeavyMass_kgs, mHeavy, 0.001, "kg/s")
                // Solids recovery is only populated for macromolecules (MW > 10000); Biomass_Generic is 246 g/mol.
                .RowInRange("Heavy enriched in biomass (>40 %)", 0.4, 1.0,
                    heavy.OverallMassFraction("Biomass_Generic"), "-")
                .RowInRange("Light enriched in water (>90 %)", 0.9, 1.0,
                    light.OverallMassFraction("Water"), "-")
                .PrintAndThrowIfFailed();
        }
    }
}
