using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsAdvanced
{
    /// <summary>Binary distillation column EtOH/H2O (NRTL, 20 stages, 1 atm).
    /// Verifies: (1) global mass balance, (2) distillate rich in EtOH (separation occurred),
    /// (3) reboiler duty &gt; 0, (4) condenser duty &gt; 0.</summary>
    internal static class A01_Distillation_EthanolWater
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("A01_Dist_EtOH_H2O")
                .WithCompounds("Water", "Ethanol")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .WithTemperature(300.Kelvin())
                .WithMolarFlow(100.MolPerSecond())
                .SetCompoundMolarFlow("Water", 50.0)
                .SetCompoundMolarFlow("Ethanol", 50.0);

            var distillate = fs.AddMaterialStream("distillate");
            var bottoms = fs.AddMaterialStream("bottoms");
            var condDuty = fs.AddEnergyStream("condDuty");
            var rebDuty = fs.AddEnergyStream("rebDuty");

            fs.AddDistillationColumn("T-101")
                .WithNumberOfStages(20)
                .WithFeed(feed, 10)
                .WithDistillate(distillate)
                .WithBottoms(bottoms)
                .WithCondenserDuty(condDuty)
                .WithReboilerDuty(rebDuty)
                .WithCondenserSpec("Reflux Ratio", 2.0, "")
                .WithReboilerSpec("Product Molar Flow Rate", 75.0, "mol/s")
                .WithTopPressure(101325.0.Pascal())
                .WithColumnPressureDrop(0.0.Pascal());

            fs.Solve();

            double F = feed.MolarFlowMolPerSecond;
            double D = distillate.MolarFlowMolPerSecond;
            double B = bottoms.MolarFlowMolPerSecond;
            double yEtOH = distillate.OverallMoleFraction("Ethanol");
            double xEtOH = bottoms.OverallMoleFraction("Ethanol");

            new ResultTable("EtOH/H2O distillation - NRTL, 20 stages, RR=2, B=75 mol/s")
                .Row("Global balance F = D + B", F, D + B, 0.001, "mol/s")
                .Row("B (specified)", 75.0, B, 0.001, "mol/s")
                .RowInRange("Distillate rich in EtOH (>50%)", 0.5, 1.0, yEtOH, "-")
                .RowInRange("Bottoms lean in EtOH (<50%)", 0.0, 0.5, xEtOH, "-")
                // DWSIM reports both duties as positive magnitudes (kW of energy exchanged).
                .RowInRange("Condenser duty > 0", 0.0, 1e9, condDuty.EnergyFlowKW, "kW")
                .RowInRange("Reboiler duty > 0", 0.0, 1e9, rebDuty.EnergyFlowKW, "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
