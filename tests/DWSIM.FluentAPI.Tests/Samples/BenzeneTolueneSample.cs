using DWSIM.Automation.FluentAPI;
using HXMode = DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Benzene/toluene binary distillation with feed preheat.
    /// Equimolar feed → heat exchanger (preheated by hot water, UA mode) →
    /// distillation column (30 stages, RR = 3, B = 50 mol/s).
    /// Checks: molar balance, both products above 85 % purity, HX transfers heat.</summary>
    internal static class BenzeneTolueneSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("BenzeneTolueneDistillation")
                .WithCompounds("Benzene", "Toluene", "Water")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var rawFeed = fs.AddMaterialStream("raw feed")
                .At(300.0.Kelvin(), 101325.0.Pascal())
                .WithMolarFlow(100.0.MolPerSecond())
                .SetCompoundMolarFlow("Benzene", 50.0)
                .SetCompoundMolarFlow("Toluene", 50.0);

            var hotUtil = fs.AddMaterialStream("hot utility")
                .At(373.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(2.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 2.0);

            var preheated = fs.AddMaterialStream("preheated feed");
            var utilOut = fs.AddMaterialStream("utility out");

            fs.AddHeatExchanger("HX-1")
                .WithCalculationMode(HXMode.CalcBothTemp_UA)
                .WithGlobalUA(8000.0)
                .WithHotSidePressureDrop(0.0.Pascal())
                .WithColdSidePressureDrop(0.0.Pascal())
                .ConnectFeed(hotUtil, 0)
                .ConnectFeed(rawFeed, 1)
                .ConnectProduct(utilOut, 0)
                .ConnectProduct(preheated, 1);

            var distillate = fs.AddMaterialStream("benzene product");
            var bottoms = fs.AddMaterialStream("toluene product");
            var condDuty = fs.AddEnergyStream("cond duty");
            var rebDuty = fs.AddEnergyStream("reb duty");

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

            new ResultTable("Benzene/toluene distillation with feed preheat")
                .Row("Molar balance F = D + B", F, D + B, 0.001, "mol/s")
                .Row("Bottoms flow B = 50", 50.0, B, 0.001, "mol/s")
                .RowInRange("Distillate rich in benzene (>85 %)", 0.85, 1.0, distillate.OverallMoleFraction("Benzene"), "-")
                .RowInRange("Bottoms rich in toluene (>85 %)", 0.85, 1.0, bottoms.OverallMoleFraction("Toluene"), "-")
                .RowInRange("Feed preheated above 300 K", 300.1, 400.0, preheated.TemperatureK, "K")
                .RowInRange("Condenser duty > 0", 0.0, 1e9, condDuty.EnergyFlowKW, "kW")
                .RowInRange("Reboiler duty > 0", 0.0, 1e9, rebDuty.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "benzene-toluene-distillation");
        }
    }
}
