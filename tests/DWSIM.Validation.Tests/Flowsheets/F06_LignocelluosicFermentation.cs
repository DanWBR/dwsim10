using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors;
using BioMode = DWSIM.UnitOperations.Reactors.BioReactorMode;
using BioThermal = DWSIM.UnitOperations.Reactors.BioReactorThermalMode;
using BioKinetic = DWSIM.UnitOperations.Reactors.BioKineticModel;
using PretreatType = DWSIM.UnitOperations.Reactors.PretreatmentType;
using CentrifugeType = DWSIM.UnitOperations.UnitOperations.CentrifugeType;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F06 - Lignocellulosic (2G) fermentation train.
    /// feed (sugary mash + seed biomass) → Pretreatment → BioReactor (Monod) →
    /// Centrifuge (DiskStack) → clarified broth + yeast cream.
    /// Bio UOs are invoked manually in topological order (FlowsheetSolver does not
    /// enqueue IExternalUnitOperation in headless mode).</summary>
    internal static class F06_LignocelluosicFermentation
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F06_LignoFerm")
                .WithCompounds("Water", "Glucose", "Ethanol", "Carbon dioxide", "Biomass_Yeast_Scerevisiae")
                .WithPropertyPackage(PropertyPackages.NRTL);

            // Feed: 10 kg/s pre-hydrolysed mash (water + glucose + inoculum)
            var feed = fs.AddMaterialStream("hydrolyzate")
                .At(303.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(10.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 8.5)
                .SetCompoundMassFlow("Glucose", 1.4)
                .SetCompoundMassFlow("Biomass_Yeast_Scerevisiae", 0.1);

            // Pretreatment (mild severity, simulating post-steam-explosion neutralization)
            var pretreated = fs.AddMaterialStream("pretreated");
            var pre = fs.AddPretreatmentReactor("PRE-1")
                .Configure(o => o.CreateConnectors())
                .WithTechnology(PretreatType.SteamExplosion)
                .WithSeverityLogR0(3.2)
                .WithResidenceTime(10.0.Minutes())
                .WithSolidsLoading(0.20)
                .ConnectFeed(feed, 0)
                .ConnectProduct(pretreated, 0);

            // Anaerobic Monod fermenter
            var brothLiq = fs.AddMaterialStream("broth_liq");
            var brothGas = fs.AddMaterialStream("broth_gas");
            var br = fs.AddBioReactor("BR-1")
                .Configure(o => o.CreateConnectors())
                .WithVolume(80.0.CubicMeters())
                .WithBatchDuration(36.0.Hours())
                .WithKineticModel(BioKinetic.Monod)
                .WithOperatingMode(BioMode.Continuous)
                .WithThermalMode(BioThermal.Isothermal)
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
                    o.YieldPS = 0.45;
                })
                .ConnectFeed(pretreated, 0)
                .ConnectProduct(brothLiq, 0)
                .ConnectProduct(brothGas, 1);

            // Centrifuge: biomass to the heavy phase, clarified broth to the light phase
            var heavy = fs.AddMaterialStream("yeast_cream");
            var light = fs.AddMaterialStream("clarified_broth");
            var cent = fs.AddCentrifuge("CENT-1")
                .Configure(o => o.CreateConnectors())
                .WithTechnology(CentrifugeType.DiskStack)
                .WithBowlSpeedRpm(8500.0)
                .WithSigmaFactorM2(1500.0)
                .WithDefaultRecoveryToHeavy(0.05)
                .WithRecoveryToHeavy("Biomass_Yeast_Scerevisiae", 0.95)
                .WithRecoveryToHeavy("Water", 0.05)
                .WithRecoveryToHeavy("Glucose", 0.02)
                .WithRecoveryToHeavy("Ethanol", 0.05)
                .ConnectFeed(brothLiq, 0)
                .ConnectProduct(heavy, 0)
                .ConnectProduct(light, 1);

            // Manual calculation in topological order (PP must be wired up on each UO/stream)
            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);

            pre.Object.PropertyPackage = pp;
            pre.Object.Calculate();

            // Flash pretreated stream before the BioReactor: Pretreatment writes masses but does not trigger the flash.
            pretreated.Object.SetPropertyPackageInstance(pp);
            pretreated.Object.Calculate(true, true);

            br.Object.SetPropertyPackageInstance(pp);
            br.Object.Calculate();

            // Flash brothLiq before the centrifuge.
            brothLiq.Object.SetPropertyPackageInstance(pp);
            brothLiq.Object.Calculate(true, true);
            cent.Object.Calculate();

            // Assertions
            double mFeed = feed.MassFlowKgPerSecond;
            double mLiq = brothLiq.MassFlowKgPerSecond;
            double mGas = brothGas.MassFlowKgPerSecond;
            double mHeavy = heavy.MassFlowKgPerSecond;
            double mLight = light.MassFlowKgPerSecond;

            double yEtOH_light = light.OverallMassFraction("Ethanol");
            double yBio_heavy = heavy.OverallMassFraction("Biomass_Yeast_Scerevisiae");

            new ResultTable("F06 - 2G fermentation train")
                .RowInRange("Pretreatment passes mass (>0)", 1.0, 100.0, pretreated.MassFlowKgPerSecond, "kg/s")
                .RowInRange("BioReactor produces ethanol", 1e-6, 10.0,
                    (light.OverallMassFraction("Ethanol") * mLight + heavy.OverallMassFraction("Ethanol") * mHeavy), "kg/s")
                .Row("Centrifuge balance F = H + L", mLiq, mHeavy + mLight, 0.005, "kg/s")
                // Biomass concentration: feed ~1 %, cream reaches ~15 % (>10× enrichment).
                .RowInRange("Cream enriched in biomass (>10 %)", 0.10, 1.0, yBio_heavy, "-")
                .RowInRange("Ethanol predominant in clarified broth", 0.005, 0.20, yEtOH_light, "-")
                .PrintAndThrowIfFailed();
        }
    }
}
