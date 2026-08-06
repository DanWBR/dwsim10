using System;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Fluent equivalent of newAPI.cs (steam-tables 2-stream Mixer).</summary>
    internal static class MixerTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("FluentMixerTest")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var inlet1 = fs.AddMaterialStream("inlet1")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(100.KgPerSecond());

            var inlet2 = fs.AddMaterialStream("inlet2")
                .At(348.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(50.KgPerSecond());

            var outlet = fs.AddMaterialStream("outlet");

            fs.AddMixer("MIX-1")
                .ConnectFeed(inlet1, 0)
                .ConnectFeed(inlet2, 1)
                .ConnectProduct(outlet, 0);

            fs.AutoLayout();
            fs.Solve();

            var T = outlet.TemperatureK;
            var m = outlet.MassFlowKgPerSecond;
            Console.WriteLine($"Outlet T = {T:F4} K, mass flow = {m:F4} kg/s");

            if (Math.Abs(m - 150.0) > 1e-6) throw new Exception($"Mass balance failed: expected 150, got {m}");
            if (T < 295 || T > 360) throw new Exception($"Outlet T out of range: {T}");
        }
    }
}
