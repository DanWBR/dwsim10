using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsBasic
{
    /// <summary>Heater - heating 1 kg/s of liquid water from 25 °C → 80 °C.
    /// Q = m·∫cp·dT (Steam Tables IAPWS). Expected range: 230 ± 5 kW.
    /// (Using avg cp ≈ 4.18 kJ/kg·K → Q ≈ 1·4.18·55 = 229.9 kW.)</summary>
    internal static class U01_Heater_WaterDuty
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("U01_Heater_Water")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("in")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0);

            var outS = fs.AddMaterialStream("out");

            var H = fs.AddHeater("HX-1")
                .WithOutletTemperature(353.15.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(outS, 0);

            fs.Solve();

            new ResultTable("Heater - H2O 1 kg/s, 25 → 80 °C")
                .Row("T_out", 353.15, outS.TemperatureK, 0.001, "K")
                .RowInRange("Heat duty (NIST 230±5 kW)", 225.0, 235.0, H.HeatDutyKW, "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
