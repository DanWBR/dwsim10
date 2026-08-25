using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using CrossflowUFMode = DWSIM.UnitOperations.UnitOperations.CrossflowUFMode;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>Crossflow UF - protein concentration in water, VCF=10.
    /// Protein 100 % retained (sieving=0), water passes freely (sieving=1) → retentate holds all product
    /// in 1/10 of the feed volume; permeate is water + salts.</summary>
    internal static class B06_CrossflowUF_Concentration
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("B06_UF")
                .WithCompounds("Water", "Glucose", "Ethanol")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(10.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 9.7)
                .SetCompoundMassFlow("Glucose", 0.2)
                .SetCompoundMassFlow("Ethanol", 0.1);

            var retentate = fs.AddMaterialStream("retentate");
            var permeate = fs.AddMaterialStream("permeate");

            var uf = fs.AddCrossflowUF("UF-1")
                .Configure(o => o.CreateConnectors())
                .WithOperatingMode(CrossflowUFMode.Concentration)
                .WithVCF(10.0)
                .WithDefaultSievingCoefficient(1.0)
                .WithSievingCoefficient("Ethanol", 0.0)        // product fully retained
                .WithMembraneFluxKgPerM2S(20.0 / 3600.0)
                .WithTransmembranePressure(1.5.Bar())
                .Configure(o => o.MembraneArea_m2 = 50.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(retentate, 0)
                .ConnectProduct(permeate, 1);

            uf.Object.Calculate();

            new ResultTable("Crossflow UF - Concentration VCF=10")
                .Row("Feed mass", 10.0, uf.Object.Result_FeedMass_kgs, 0.001, "kg/s")
                .Row("Mass balance F = R + P", 10.0, uf.Object.Result_Retentate_kgs + uf.Object.Result_Permeate_kgs, 0.001, "kg/s")
                .RowInRange("Effective VCF ≈ 10", 9.0, 11.0, uf.Object.Result_EffectiveVCF, "-")
                .RowInRange("Ethanol in retentate > 90 %", 0.09, 0.10001,
                    retentate.OverallMassFraction("Ethanol") * retentate.MassFlowKgPerSecond, "kg/s")
                .RowInRange("Permeate nearly pure water", 0.95, 1.0,
                    permeate.OverallMassFraction("Water"), "-")
                .PrintAndThrowIfFailed();
        }
    }
}
