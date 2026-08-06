using System;
using DWSIM.Automation.FluentAPI;
using System.Collections.Generic;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Fluent equivalent of convReactor.cs (steam reforming, 2 conversion reactions).</summary>
    internal static class ConvReactorTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("FluentConvReactorTest")
                .WithCompounds("Carbon dioxide", "Carbon monoxide", "Water", "Hydrogen", "Methane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var r1 = fs.DefineConversionReaction("R1",
                new Dictionary<string, double> { { "Methane", -1 }, { "Water", -2 }, { "Carbon dioxide", 1 }, { "Hydrogen", 4 } },
                "Methane", "Vapor", "50");

            var r2 = fs.DefineConversionReaction("R2",
                new Dictionary<string, double> { { "Methane", -1 }, { "Water", -1 }, { "Carbon monoxide", 1 }, { "Hydrogen", 3 } },
                "Water", "Vapor", "50");

            fs.ReactionSet("DefaultSet")
                .Add(r1)
                .Add(r2);

            var feed = fs.AddMaterialStream("inlet")
                .WithTemperature(1000.Kelvin())
                .WithMolarFlow(5.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 2.0)
                .SetCompoundMolarFlow("Water", 3.0)
                .SetCompoundMolarFlow("Carbon dioxide", 0.0)
                .SetCompoundMolarFlow("Carbon monoxide", 0.0)
                .SetCompoundMolarFlow("Hydrogen", 0.0);

            var gasOut = fs.AddMaterialStream("gas outlet");
            var liqOut = fs.AddMaterialStream("liquid outlet");
            var heat = fs.AddEnergyStream("heat");

            var reactor = fs.AddConversionReactor("R-1")
                .Isothermal()
                .WithReactionSet("DefaultSet")
                .WithPressureDrop(0.0.Pascal())
                .ConnectFeed(feed, 0)
                .ConnectProduct(gasOut, 0)
                .ConnectProduct(liqOut, 1)
                .ConnectEnergyFeed(heat, 1);

            fs.AutoLayout();
            fs.Solve();

            Console.WriteLine($"Reactor heat duty = {reactor.HeatDutyKW:F4} kW");
            foreach (var c in reactor.Object.ComponentConversions)
            {
                if (c.Value > 0) Console.WriteLine($"  {c.Key}: {c.Value:P2}");
            }
        }
    }
}
