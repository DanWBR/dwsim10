using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using ChromatographyMode = DWSIM.UnitOperations.UnitOperations.ChromatographyMode;
using ChromatographyChemistry = DWSIM.UnitOperations.UnitOperations.ChromatographyChemistry;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>Bind&Elute chromatography - ethanol capture (product surrogate) on IEX.
    /// 95 % recovery for product, 5 % default for impurities. Column 100 L, DBC 50 g/L.
    /// Verifies: product is mainly directed to the "product" stream.</summary>
    internal static class B04_Chromatography_BindElute
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("B04_Chrom")
                .WithCompounds("Water", "Glucose", "Ethanol")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.85)
                .SetCompoundMassFlow("Glucose", 0.05)
                .SetCompoundMassFlow("Ethanol", 0.10);

            var product = fs.AddMaterialStream("product");
            var waste = fs.AddMaterialStream("waste");

            var ch = fs.AddChromatographyColumn("CHR-1")
                .Configure(o => o.CreateConnectors())
                .WithMode(ChromatographyMode.BindElute)
                .WithChemistry(ChromatographyChemistry.IonExchange)
                .WithColumnVolumeLiters(100.0)
                .WithDynamicBindingCapacityGPerL(50.0)
                .WithDefaultRecoveryToProduct(0.05)
                .WithRecoveryToProduct("Ethanol", 0.95)
                .WithResinDensityGPerL(700.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(product, 0)
                .ConnectProduct(waste, 1);

            ch.Object.Calculate();

            new ResultTable("Chromatography Bind&Elute - IEX, ethanol capture")
                .Row("Feed mass", 1.0, ch.Object.Result_FeedMass_kgs, 0.001, "kg/s")
                .Row("Balance F = P + W", 1.0, ch.Object.Result_ProductMass_kgs + ch.Object.Result_WasteMass_kgs, 0.001, "kg/s")
                // Result_TargetRecovery is only populated when TargetCompound is set explicitly; check the stream directly:
                .RowInRange("Ethanol in product ≈ 0.095 kg/s (95 % recovery)", 0.09, 0.10001,
                    product.OverallMassFraction("Ethanol") * product.MassFlowKgPerSecond, "kg/s")
                .PrintAndThrowIfFailed();
        }
    }
}
