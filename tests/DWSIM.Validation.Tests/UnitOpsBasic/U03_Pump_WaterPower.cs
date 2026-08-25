using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.UnitOpsBasic
{
    /// <summary>Pump - 1 kg/s of liquid water, ΔP = 10 bar, η = 75 %.
    /// Incompressible approximation: W = (V̇·ΔP)/η = (0.001003·1e6)/0.75 ≈ 1.337 kW.
    /// (V̇ ≈ m/ρ with ρ_25°C ≈ 997.0 kg/m³.)</summary>
    internal static class U03_Pump_WaterPower
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("U03_Pump_Water")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("in")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0);

            var outS = fs.AddMaterialStream("out");

            var p = fs.AddPump("P-1")
                .WithPressureIncrease(10.0.Bar())
                .WithEfficiencyPercent(75.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(outS, 0);

            fs.Solve();

            double Wexp = (1.0 / 997.0) * 10e5 / 0.75 / 1000.0; // W in kW (DWSIM)

            new ResultTable("Pump - H2O 1 kg/s, ΔP=10 bar, η=75 %")
                .Row("ΔP", 10e5, p.DeltaPPa, 0.001, "Pa")
                .Row("Power (V̇·ΔP/η)", Wexp, p.PowerKW, 0.05, "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
