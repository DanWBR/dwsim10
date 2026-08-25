using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders.Bioprocess;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F19 - BioReactor in all three operating modes with different kinetic models.
    /// (A) Continuous + Monod: glucose → ethanol (S. cerevisiae)
    /// (B) Batch + Haldane: substrate-inhibited fermentation
    /// (C) FedBatch + Contois: biomass-dependent saturation
    /// Validates: substrate consumption, product formation, mode-specific behavior,
    /// and property profile access via Fluent API.</summary>
    internal static class F19_BioReactorModes
    {
        public static void Run()
        {
            var rt = new ResultTable("F19 - BioReactor: 3 modes × 3 kinetics + profiles");

            RunContinuousMonod(rt);
            RunBatchHaldane(rt);
            RunFedBatchContois(rt);

            rt.PrintAndThrowIfFailed();
        }

        private static void RunContinuousMonod(ResultTable rt)
        {
            var fs = Flowsheet.Create("F19A_Continuous")
                .WithCompounds("Water", "Ethanol", "Glucose", "Carbon dioxide",
                               "Biomass_Yeast_Scerevisiae")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(303.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.889)
                .SetCompoundMassFlow("Glucose", 0.110)
                .SetCompoundMassFlow("Biomass_Yeast_Scerevisiae", 0.001);

            var liqOut = fs.AddMaterialStream("liqOut");
            var gasOut = fs.AddMaterialStream("gasOut");

            var brBuilder = fs.AddBioReactor("BR-A")
                .Configure(o => o.CreateConnectors())
                .WithVolume(50.0.CubicMeters())
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
                    o.YieldPS = 0.45;
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(liqOut, 0)
                .ConnectProduct(gasOut, 1);

            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);
            brBuilder.Object.SetPropertyPackageInstance(pp);
            brBuilder.Object.Calculate();

            liqOut.Object.SetPropertyPackageInstance(pp);
            liqOut.Object.Calculate(true, true);
            gasOut.Object.SetPropertyPackageInstance(pp);
            gasOut.Object.Calculate(true, true);

            double mOut = liqOut.MassFlowKgPerSecond + gasOut.MassFlowKgPerSecond;
            double glcOut = liqOut.OverallMassFraction("Glucose") * liqOut.MassFlowKgPerSecond;
            double etohOut = liqOut.OverallMassFraction("Ethanol") * liqOut.MassFlowKgPerSecond;

            rt.RowInRange("[A] Continuous mass > 0", 0.1, 10.0, mOut, "kg/s");
            rt.RowInRange("[A] Glucose consumed", 0.001, 0.11, 0.110 - glcOut, "kg/s");
            rt.RowInRange("[A] Ethanol produced", 1e-6, 1.0, etohOut, "kg/s");

            // ---- Property profile validation via Fluent API ----
            var traj = brBuilder.Trajectory;
            if (traj != null)
            {
                var series = brBuilder.ProfileSeriesNames;
                rt.RowInRange("[A] Profile series count > 0", 1, 20, series?.Length ?? 0, "-");

                var csv = brBuilder.ProfileToCSV();
                rt.RowInRange("[A] Profile CSV length > 0", 10, 1e8, csv?.Length ?? 0, "chars");

                var dt = brBuilder.ProfileToDataTable();
                rt.RowInRange("[A] Profile DataTable rows > 0", 1, 1e6, dt?.Rows.Count ?? 0, "rows");
            }
            else
            {
                rt.RowInRange("[A] Profile available", 1, 1, 0, "-");
            }
        }

        private static void RunBatchHaldane(ResultTable rt)
        {
            var fs = Flowsheet.Create("F19B_Batch")
                .WithCompounds("Water", "Ethanol", "Glucose", "Carbon dioxide",
                               "Biomass_Yeast_Scerevisiae")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(303.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.699)
                .SetCompoundMassFlow("Glucose", 0.300)
                .SetCompoundMassFlow("Biomass_Yeast_Scerevisiae", 0.001);

            var liqOut = fs.AddMaterialStream("liqOut");
            var gasOut = fs.AddMaterialStream("gasOut");

            var brBuilder = fs.AddBioReactor("BR-B")
                .Configure(o => o.CreateConnectors())
                .WithVolume(10.0.CubicMeters())
                .WithBatchDuration(48.0.Hours())
                .WithKineticModel(BioKineticModel.Haldane)
                .WithOperatingMode(BioReactorMode.Batch)
                .WithThermalMode(BioReactorThermalMode.Adiabatic)
                .WithAerobic(false)
                .WithMaxSpecificGrowthPerHour(0.40)
                .WithMonodKsGPerL(0.5)
                .WithBiomassYield(0.10)
                .Configure(o =>
                {
                    o.BiomassCompound = "Biomass_Yeast_Scerevisiae";
                    o.SubstrateCompound = "Glucose";
                    o.ProductCompound = "Ethanol";
                    o.CO2Compound = "Carbon dioxide";
                    o.WaterCompound = "Water";
                    o.Ki_gL = 80.0;
                    o.YieldPS = 0.45;
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(liqOut, 0)
                .ConnectProduct(gasOut, 1);

            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);
            brBuilder.Object.SetPropertyPackageInstance(pp);
            brBuilder.Object.Calculate();

            liqOut.Object.SetPropertyPackageInstance(pp);
            liqOut.Object.Calculate(true, true);
            gasOut.Object.SetPropertyPackageInstance(pp);
            gasOut.Object.Calculate(true, true);

            double mOut = liqOut.MassFlowKgPerSecond + gasOut.MassFlowKgPerSecond;
            double glcOut = liqOut.OverallMassFraction("Glucose") * liqOut.MassFlowKgPerSecond;
            double etohOut = liqOut.OverallMassFraction("Ethanol") * liqOut.MassFlowKgPerSecond;

            // Batch mode reports on a cycle-averaged basis: the reactor treats the inlet as one
            // V-sized charge processed every BatchDuration, so the outlet flow is the feed scaled
            // by (V / BatchDuration) / Q_feed, not the feed itself. The feed here is deliberately
            // mismatched to that ratio, which is what makes the scaling visible.
            // Q is the broth's liquid-phase flow, which is what the reactor sizes the cycle on.
            double qFeed = feed.Object.Phases[1].Properties.volumetric_flow.GetValueOrDefault();
            double cycleInlet = 1.0 * (10.0 / (48.0 * 3600.0)) / qFeed;
            rt.RowInRange("[B] Batch mass = cycle inlet", cycleInlet * 0.95, cycleInlet * 1.10,
                mOut, "kg/s");
            rt.RowInRange("[B] Glucose consumed (Haldane)", 0.001, 0.30, 0.300 - glcOut, "kg/s");
            rt.RowInRange("[B] Ethanol produced (Haldane)", 1e-6, 1.0, etohOut, "kg/s");
            rt.RowInRange("[B] Outlet T >= feed (adiabatic)", 303.0, 500.0,
                brBuilder.Object.Result_OutletTemperature_K, "K");

            // ---- Profile: check substrate depletion curve ----
            var substrateSeries = brBuilder.GetProfileSeries("S");
            if (substrateSeries != null && substrateSeries.Length > 1)
            {
                rt.RowInRange("[B] Profile S series points > 10", 10, 1e6, substrateSeries.Length, "pts");
                rt.RowInRange("[B] Profile S[0] > 0", 1.0, 1e7, substrateSeries[0], "-");
            }
        }

        private static void RunFedBatchContois(ResultTable rt)
        {
            var fs = Flowsheet.Create("F19C_FedBatch")
                .WithCompounds("Water", "Ethanol", "Glucose", "Carbon dioxide",
                               "Biomass_Yeast_Scerevisiae")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .At(303.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(0.5.KgPerSecond())
                .SetCompoundMassFlow("Water", 0.445)
                .SetCompoundMassFlow("Glucose", 0.050)
                .SetCompoundMassFlow("Biomass_Yeast_Scerevisiae", 0.005);

            var liqOut = fs.AddMaterialStream("liqOut");
            var gasOut = fs.AddMaterialStream("gasOut");

            var brBuilder = fs.AddBioReactor("BR-C")
                .Configure(o => o.CreateConnectors())
                .WithVolume(20.0.CubicMeters())
                .WithBatchDuration(24.0.Hours())
                .WithKineticModel(BioKineticModel.Contois)
                .WithOperatingMode(BioReactorMode.FedBatch)
                .WithThermalMode(BioReactorThermalMode.Isothermal)
                .WithAerobic(false)
                .WithMaxSpecificGrowthPerHour(0.35)
                .WithMonodKsGPerL(0.8)
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
                .ConnectFeed(feed, 0)
                .ConnectProduct(liqOut, 0)
                .ConnectProduct(gasOut, 1);

            var pp = System.Linq.Enumerable.First(fs.Inner.PropertyPackages).Value;
            feed.Object.SetPropertyPackageInstance(pp);
            feed.Object.Calculate(true, true);
            brBuilder.Object.SetPropertyPackageInstance(pp);
            brBuilder.Object.Calculate();

            liqOut.Object.SetPropertyPackageInstance(pp);
            liqOut.Object.Calculate(true, true);
            gasOut.Object.SetPropertyPackageInstance(pp);
            gasOut.Object.Calculate(true, true);

            double mOut = liqOut.MassFlowKgPerSecond + gasOut.MassFlowKgPerSecond;
            double glcOut = liqOut.OverallMassFraction("Glucose") * liqOut.MassFlowKgPerSecond;
            double etohOut = liqOut.OverallMassFraction("Ethanol") * liqOut.MassFlowKgPerSecond;

            rt.RowInRange("[C] FedBatch mass > 0", 0.1, 10.0, mOut, "kg/s");
            rt.RowInRange("[C] Glucose consumed (Contois)", 0.001, 0.05, 0.050 - glcOut, "kg/s");
            rt.RowInRange("[C] Ethanol produced (Contois)", 1e-6, 1.0, etohOut, "kg/s");

            // ---- Profile: check biomass growth curve ----
            var biomassSeries = brBuilder.GetProfileSeries("X");
            if (biomassSeries != null && biomassSeries.Length > 1)
            {
                double xFinal = biomassSeries[biomassSeries.Length - 1];
                rt.RowInRange("[C] Profile X final > 0", 1.0, 1e8, xFinal, "-");
                rt.RowInRange("[C] Profile X points > 10", 10, 1e6, biomassSeries.Length, "pts");
            }
        }
    }
}
