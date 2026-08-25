using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using HXMode = DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F11 - Benzene/toluene binary distillation with feed preheat.
    /// Feed (50/50 Bz/Tol) → HeatExchanger (cold side, preheated by hot water) →
    /// DistillationColumn (30 stages) → distillate (Bz-rich) + bottoms (Tol-rich).
    /// Validates: separation achieved, mass balance, HX transfers heat.</summary>
    internal static class F11_BenzeneTolueneDistillation
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F11_BzTol_Dist")
                .WithCompounds("Benzene", "Toluene", "Water")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // Process feed: equimolar Bz/Tol
            var rawFeed = fs.AddMaterialStream("raw_feed")
                .At(300.0.Kelvin(), 101325.0.Pascal())
                .WithMolarFlow(100.0.MolPerSecond())
                .SetCompoundMolarFlow("Benzene", 50.0)
                .SetCompoundMolarFlow("Toluene", 50.0)
                // Water is on the flowsheet for the hot utility, and a new stream starts with its
                // compounds split evenly, so leaving it unset put 33.3 mol/s of water in the feed:
                // it left over the top and read as a distillate only 60 % benzene.
                .SetCompoundMolarFlow("Water", 0.0);

            // Hot utility (water)
            var hotUtil = fs.AddMaterialStream("hot_utility")
                .At(373.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(2.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 2.0);

            // HX outputs
            var preheated = fs.AddMaterialStream("preheated_feed");
            var utilOut = fs.AddMaterialStream("utility_out");

            fs.AddHeatExchanger("HX-1")
                .WithCalculationMode(HXMode.CalcBothTemp_UA)
                .WithGlobalUA(8000.0)
                .WithHotSidePressureDrop(0.0.Pascal())
                .WithColdSidePressureDrop(0.0.Pascal())
                .ConnectFeed(hotUtil, 0)
                .ConnectFeed(rawFeed, 1)
                .ConnectProduct(utilOut, 0)
                .ConnectProduct(preheated, 1);

            // Distillation column
            var distillate = fs.AddMaterialStream("distillate");
            var bottoms = fs.AddMaterialStream("bottoms");
            var condDuty = fs.AddEnergyStream("cond_duty");
            var rebDuty = fs.AddEnergyStream("reb_duty");

            fs.AddDistillationColumn("T-101")
                .WithNumberOfStages(30)
                .WithFeed(preheated, 15)
                .WithDistillate(distillate)
                .WithBottoms(bottoms)
                .WithCondenserDuty(condDuty)
                .WithReboilerDuty(rebDuty)
                .WithCondenserSpec("Reflux Ratio", 3.0, "")
                .WithReboilerSpec("Product Molar Flow Rate", 50.0, "mol/s")
                .WithTopPressure(101325.0.Pascal())
                .WithColumnPressureDrop(0.0.Pascal())
                .Configure(c =>
                {
                    c.MaxIterations = 200;
                    c.ExternalLoopTolerance = 1e-3;
                    c.InternalLoopTolerance = 1e-3;
                });

            fs.Solve();

            double F = rawFeed.MolarFlowMolPerSecond;
            double D = distillate.MolarFlowMolPerSecond;
            double B = bottoms.MolarFlowMolPerSecond;
            double yBz_dist = distillate.OverallMoleFraction("Benzene");
            double yTol_bot = bottoms.OverallMoleFraction("Toluene");
            double Tpreheated = preheated.TemperatureK;

            new ResultTable("F11 - Bz/Tol distillation with feed preheat")
                .Row("Molar balance F = D + B", F, D + B, 0.001, "mol/s")
                .Row("Bottoms flow B = 50", 50.0, B, 0.001, "mol/s")
                .RowInRange("Distillate rich in Bz (>85%)", 0.85, 1.0, yBz_dist, "-")
                .RowInRange("Bottoms rich in Tol (>85%)", 0.85, 1.0, yTol_bot, "-")
                .RowInRange("Feed preheated above 300 K", 300.1, 400.0, Tpreheated, "K")
                .RowInRange("Condenser duty > 0", 0.0, 1e9, condDuty.EnergyFlowKW, "kW")
                .RowInRange("Reboiler duty > 0", 0.0, 1e9, rebDuty.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
