using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsBasic
{
    /// <summary>Cooler - cooling 1 kg/s of liquid water from 80 °C → 25 °C.
    /// Expected behavior: symmetric to Heater (same duty magnitude).</summary>
    internal static class U02_Cooler_WaterDuty
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("U02_Cooler_Water")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("in")
                .At(353.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0);

            var outS = fs.AddMaterialStream("out");

            var C = fs.AddCooler("CL-1")
                .WithOutletTemperature(298.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(outS, 0);

            fs.Solve();

            new ResultTable("Cooler - H2O 1 kg/s, 80 → 25 °C")
                .Row("T_out", 298.15, outS.TemperatureK, 0.001, "K")
                .RowInRange("Heat removed (NIST 230±5 kW)", 225.0, 235.0, System.Math.Abs(C.HeatRemovedKW), "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
