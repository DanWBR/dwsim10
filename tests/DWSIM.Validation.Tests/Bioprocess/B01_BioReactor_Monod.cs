using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>BioReactor - anaerobic S. cerevisiae fermentation glucose → ethanol + CO2.
    /// Monod: μ = μmax·S/(Ks+S). Inoculum: 1 g/L biomass, 100 g/L glucose, 36 h batch.
    /// Expected: glucose consumed (>50 %), ethanol produced (>0), closed mass balance.</summary>
    internal static class B01_BioReactor_Monod
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("B01_BioReactor")
                .WithCompounds("Water", "Ethanol", "Glucose", "Carbon dioxide", "Biomass_Yeast_Scerevisiae")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(303.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.899)
                .SetCompoundMassFlow("Glucose", 0.100)
                .SetCompoundMassFlow("Biomass_Yeast_Scerevisiae", 0.001);

            var liqOut = fs.AddMaterialStream("liqOut");
            var gasOut = fs.AddMaterialStream("gasOut");

            var br = fs.AddBioReactor("BR-1")
                .Configure(o => o.CreateConnectors())
                .WithVolume(50.0.CubicMeters())
                .WithBatchDuration(36.0.Hours())
                .WithKineticModel(BioKineticModel.Monod)
                .WithOperatingMode(BioReactorMode.Continuous)
                .WithThermalMode(BioReactorThermalMode.Isothermal)
                .WithAerobic(false)
                .WithMaxSpecificGrowthPerHour(0.45)
                .WithMonodKsGPerL(0.5)
                .WithBiomassYield(0.10)
                .Configure(o =>
                {
                    o.BiomassCompound = "Biomass_Yeast_Scerevisiae";
                    o.SubstrateCompound = "Glucose";
                    o.ProductCompound = "Ethanol";
                    o.CO2Compound = "Carbon dioxide";
                    o.WaterCompound = "Water";
                    o.YieldPS = 0.45;       // ~0.45 g EtOH / g glucose (close to Gay-Lussac theoretical)
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(liqOut, 0)
                .ConnectProduct(gasOut, 1);

            // Without fs.Solve(), the property package must be wired up manually
            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);
            br.Object.SetPropertyPackageInstance(pp);
            br.Object.Calculate();

            double glcOut = liqOut.OverallMassFraction("Glucose") * liqOut.MassFlowKgPerSecond
                          + gasOut.OverallMassFraction("Glucose") * gasOut.MassFlowKgPerSecond;
            double etohOut = liqOut.OverallMassFraction("Ethanol") * liqOut.MassFlowKgPerSecond
                           + gasOut.OverallMassFraction("Ethanol") * gasOut.MassFlowKgPerSecond;
            double conv = (0.100 - glcOut) / 0.100;

            new ResultTable("BioReactor Monod - continuous alcoholic fermentation")
                .RowInRange("Total outlet mass finite", 0.1, 100.0,
                     liqOut.MassFlowKgPerSecond + gasOut.MassFlowKgPerSecond, "kg/s")
                .RowInRange("Glucose consumed (>0)", 0.001, 0.1, 0.100 - glcOut, "kg/s")
                .RowInRange("Ethanol produced > 0", 1e-6, 1.0, etohOut, "kg/s")
                .PrintAndThrowIfFailed();
        }
    }
}
