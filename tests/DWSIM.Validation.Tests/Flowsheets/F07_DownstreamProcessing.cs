using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using LysisTech = DWSIM.UnitOperations.UnitOperations.LysisTechnology;
using CentrifugeType = DWSIM.UnitOperations.UnitOperations.CentrifugeType;
using ChromatographyMode = DWSIM.UnitOperations.UnitOperations.ChromatographyMode;
using ChromatographyChemistry = DWSIM.UnitOperations.UnitOperations.ChromatographyChemistry;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F07 - Downstream Processing (DSP) of recombinant protein.
    /// E. coli cell paste → CellLysis (HPH 80 MPa) → Centrifuge (debris vs. clarified lysate) →
    /// Chromatography (Bind-Elute IEX, product capture).
    /// Ethanol used as a "soluble product" surrogate (no protein name in the standard DB).</summary>
    internal static class F07_DownstreamProcessing
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F07_DSP")
                .WithCompounds("Water", "Biomass_Ecoli", "Ethanol", "Glucose")
                .WithPropertyPackage(PropertyPackages.NRTL);

            // Cell paste (10 % biomass, 1 % product, balance water + salts)
            var feed = fs.AddMaterialStream("cell_paste")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.85)
                .SetCompoundMassFlow("Biomass_Ecoli", 0.10)
                .SetCompoundMassFlow("Ethanol", 0.01)
                .SetCompoundMassFlow("Glucose", 0.04);

            // 1) Cell lysis (HPH 80 MPa, 2 passes)
            var lysate = fs.AddMaterialStream("lysate");
            var debris = fs.AddMaterialStream("debris");
            var cl = fs.AddCellLysis("CL-1")
                .Configure(o => o.CreateConnectors())
                .WithTechnology(LysisTech.HighPressureHomogenizer)
                .WithPasses(2)
                .WithPressureMPa(80.0)
                .WithBiomassCompound("Biomass_Ecoli")
                .WithDefaultReleaseFraction(0.30)
                .WithReleaseFraction("Ethanol", 0.95)        // product released
                .WithReleaseFraction("Glucose", 0.95)
                .ConnectFeed(feed, 0)
                .ConnectProduct(lysate, 0)
                .ConnectProduct(debris, 1);

            // 2) Clarifying centrifuge (removes debris)
            var clarified = fs.AddMaterialStream("clarified");
            var cake = fs.AddMaterialStream("cake");
            var cent = fs.AddCentrifuge("CENT-1")
                .Configure(o => o.CreateConnectors())
                .WithTechnology(CentrifugeType.DiskStack)
                .WithBowlSpeedRpm(10000.0)
                .WithSigmaFactorM2(2000.0)
                .WithDefaultRecoveryToHeavy(0.95)            // solids to cake
                .WithRecoveryToHeavy("Water", 0.05)          // supernatant = clarified
                .WithRecoveryToHeavy("Ethanol", 0.05)        // product stays in supernatant
                .WithRecoveryToHeavy("Glucose", 0.05)
                .ConnectFeed(lysate, 0)
                .ConnectProduct(cake, 0)                     // port 0 = heavy
                .ConnectProduct(clarified, 1);               // port 1 = light

            // 3) Bind-Elute Chromatography (IEX) - product capture
            var product = fs.AddMaterialStream("product_pool");
            var waste = fs.AddMaterialStream("waste");
            var ch = fs.AddChromatographyColumn("CHR-1")
                .Configure(o => o.CreateConnectors())
                .WithMode(ChromatographyMode.BindElute)
                .WithChemistry(ChromatographyChemistry.IonExchange)
                .WithColumnVolumeLiters(50.0)
                .WithDynamicBindingCapacityGPerL(40.0)
                .WithDefaultRecoveryToProduct(0.05)
                .WithRecoveryToProduct("Ethanol", 0.92)      // 92 % product recovery
                .ConnectFeed(clarified, 0)
                .ConnectProduct(product, 0)
                .ConnectProduct(waste, 1);

            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);

            cl.Object.Calculate();
            cent.Object.Calculate();
            ch.Object.Calculate();

            double prodIn = 0.01;            // kg/s ethanol in the feed
            double prodLysate = lysate.OverallMassFraction("Ethanol") * lysate.MassFlowKgPerSecond;
            double prodClarified = clarified.OverallMassFraction("Ethanol") * clarified.MassFlowKgPerSecond;
            double prodFinal = product.OverallMassFraction("Ethanol") * product.MassFlowKgPerSecond;

            double overallRec = prodFinal / prodIn;

            new ResultTable("F07 - Full DSP (Lysis → Centrifuge → Chromatography)")
                .Row("Lysis balance F = lysate + debris", 1.0,
                    lysate.MassFlowKgPerSecond + debris.MassFlowKgPerSecond, 0.005, "kg/s")
                .Row("Centrifuge balance F = cake + clarified", lysate.MassFlowKgPerSecond,
                    cake.MassFlowKgPerSecond + clarified.MassFlowKgPerSecond, 0.005, "kg/s")
                .Row("Chromatography balance F = prod + waste", clarified.MassFlowKgPerSecond,
                    product.MassFlowKgPerSecond + waste.MassFlowKgPerSecond, 0.005, "kg/s")
                .RowInRange("Product > 0 at each step", 1e-6, 1.0, prodLysate, "kg/s")
                .RowInRange("Product retained after centrifuge", 1e-6, 1.0, prodClarified, "kg/s")
                .RowInRange("Overall product recovery > 70 %", 0.70, 1.0, overallRec, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
