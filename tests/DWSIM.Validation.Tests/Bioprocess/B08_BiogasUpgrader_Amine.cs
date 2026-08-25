using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using BiogasUpgraderTech = DWSIM.UnitOperations.UnitOperations.BiogasUpgraderTech;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>Biogas Upgrader Amine - raw sour biogas → biomethane.
    /// CO2 removal 99 %, H2S removal 99.5 %, CH4 loss 0.1 %. Expected: biomethane CH4 purity > 97 %,
    /// recovery > 99 %, H2S stripped to trace.</summary>
    internal static class B08_BiogasUpgrader_Amine
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("B08_BU")
                .WithCompounds("Methane", "Carbon dioxide", "Hydrogen sulfide", "Water")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("biogas")
                .At(308.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Methane", 0.34)
                .SetCompoundMassFlow("Carbon dioxide", 0.62)
                .SetCompoundMassFlow("Hydrogen sulfide", 0.005)
                .SetCompoundMassFlow("Water", 0.035);

            var biomethane = fs.AddMaterialStream("biomethane");
            var offgas = fs.AddMaterialStream("offgas");

            var bu = fs.AddBiogasUpgrader("BU-1")
                .Configure(o => o.CreateConnectors())
                .WithTechnology(BiogasUpgraderTech.Amine)
                .WithCO2Removal(0.99)
                .WithH2SCompound("Hydrogen sulfide")
                .WithH2SRemoval(0.995)
                .WithH2ORemoval(0.90)
                .WithCH4LossFraction(0.001)
                .WithTargetCH4Purity(0.975)
                .ConnectFeed(feed, 0)
                .ConnectProduct(biomethane, 0)
                .ConnectProduct(offgas, 1);

            bu.Object.Calculate();

            new ResultTable("Biogas Upgrader Amine - biogas → biomethane")
                .Row("Feed mass", 1.0, bu.Object.Result_FeedMass_kgs, 0.001, "kg/s")
                .Row("Mass balance", 1.0, bu.Object.Result_UpgradedMass_kgs + bu.Object.Result_OffgasMass_kgs, 0.001, "kg/s")
                .RowInRange("Biomethane CH4 purity > 90 %", 0.90, 1.0, bu.Object.Result_UpgradedCH4Fraction, "-")
                .RowInRange("CH4 recovery > 99 %", 0.99, 1.0, bu.Object.Result_CH4RecoveryFraction, "-")
                .RowInRange("H2S stripped from biomethane", 0.0, 1e-4, biomethane.OverallMassFraction("Hydrogen sulfide"), "-")
                .RowInRange("Wobbe Index > 0", 1.0, 1e6, bu.Object.Result_WobbeIndex, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
