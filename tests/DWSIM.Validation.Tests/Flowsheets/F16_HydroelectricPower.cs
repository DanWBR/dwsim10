using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using HXMode = DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F16 - Small hydroelectric turbine with downstream heat recovery.
    /// Reservoir water (50 m head) → HydroelectricTurbine → outlet water →
    /// HeatExchanger (cold side) against hot process water.
    /// Validates: turbine power ≈ 21 kW, heat transfer occurs, temperatures coherent.</summary>
    internal static class F16_HydroelectricPower
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F16_Hydro")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            // Reservoir water
            var reservoir = fs.AddMaterialStream("reservoir")
                .At(288.15.Kelvin(), 6.0e5.Pascal())
                .WithMassFlow(50.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 50.0);

            // Turbine with 50 m static head (energy output at port 1 - connect manually)
            var turbineOut = fs.AddMaterialStream("turbine_out");
            var turbineEnergy = fs.AddEnergyStream("W_turbine");
            var ht = fs.AddHydroelectricTurbine("HT-1")
                .Configure(o => o.CreateConnectors())
                .WithStaticHeadM(50.0)
                .WithEfficiencyPercent(85.0)
                .WithInletVelocityMPerS(3.0)
                .WithOutletVelocityMPerS(3.0)
                .ConnectFeed(reservoir, 0)
                .ConnectProduct(turbineOut, 0);
            // Energy output is at OutputConnectors(1) - connect via flowsheet API
            fs.Inner.ConnectObjects(
                ht.Object.GraphicObject,
                turbineEnergy.Object.GraphicObject, 1, 0);

            // Hot process water for the HX
            var hotIn = fs.AddMaterialStream("hot_process_in")
                .At(353.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(5.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 5.0);

            var hotOut = fs.AddMaterialStream("hot_process_out");
            var coldOut = fs.AddMaterialStream("cold_river_out");

            fs.AddHeatExchanger("HX-1")
                .WithCalculationMode(HXMode.CalcBothTemp_UA)
                .WithGlobalUA(15000.0)
                .WithHotSidePressureDrop(0.0.Pascal())
                .WithColdSidePressureDrop(0.0.Pascal())
                .ConnectFeed(hotIn, 0)
                .ConnectFeed(turbineOut, 1)
                .ConnectProduct(hotOut, 0)
                .ConnectProduct(coldOut, 1);

            fs.Solve();

            double power = ht.GeneratedPowerKW;
            double Thot_out = hotOut.TemperatureK;
            double Tcold_out = coldOut.TemperatureK;
            double mColdOut = coldOut.MassFlowKgPerSecond;

            new ResultTable("F16 - Hydroelectric turbine + heat recovery")
                .RowInRange("Turbine power 10-40 kW", 10.0, 40.0, power, "kW")
                .RowInRange("Hot side cools (< 353 K)", 280.0, 352.0, Thot_out, "K")
                .RowInRange("Cold side warms (> 288 K)", 289.0, 360.0, Tcold_out, "K")
                .Row("Cold side mass conservation", 50.0, mColdOut, 0.01, "kg/s")
                .PrintAndThrowIfFailed();
        }
    }
}
