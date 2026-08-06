using System;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Fluent equivalent of distColumn.cs (water/ethanol, NRTL, 20 stages).</summary>
    internal static class DistillationTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("FluentDistTest")
                .WithCompounds("Water", "Ethanol")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("feed")
                .WithTemperature(300.Kelvin())
                .WithMolarFlow(100.MolPerSecond())
                .SetCompoundMolarFlow("Water", 50.0)
                .SetCompoundMolarFlow("Ethanol", 50.0);

            var distillate = fs.AddMaterialStream("distillate");
            var bottoms = fs.AddMaterialStream("bottoms");
            var condDuty = fs.AddEnergyStream("cond duty");
            var rebDuty = fs.AddEnergyStream("reb duty");

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

            fs.AutoLayout();
            fs.Solve();

            Console.WriteLine($"Condenser duty = {condDuty.EnergyFlowKW:F4} kW");
            Console.WriteLine($"Reboiler  duty = {rebDuty.EnergyFlowKW:F4} kW");
            Console.WriteLine($"Distillate flow = {distillate.MolarFlowMolPerSecond:F4} mol/s");
            Console.WriteLine($"Bottoms flow    = {bottoms.MolarFlowMolPerSecond:F4} mol/s");
            Console.WriteLine($"Distillate composition: H2O={distillate.OverallMoleFraction("Water"):F4} EtOH={distillate.OverallMoleFraction("Ethanol"):F4}");
            Console.WriteLine($"Bottoms    composition: H2O={bottoms.OverallMoleFraction("Water"):F4} EtOH={bottoms.OverallMoleFraction("Ethanol"):F4}");
        }
    }
}
