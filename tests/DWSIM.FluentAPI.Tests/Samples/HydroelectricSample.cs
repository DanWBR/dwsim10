using DWSIM.Automation.FluentAPI;
using HXMode = DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Small hydroelectric turbine with downstream heat recovery.
    /// Reservoir water (50 m head) → hydroelectric turbine → tailrace water →
    /// heat exchanger (cold side) against hot process water.
    /// Checks: turbine power in the head-based range, HX temperatures coherent.</summary>
    internal static class HydroelectricSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("HydroelectricPower")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var reservoir = fs.AddMaterialStream("reservoir")
                .At(288.15.Kelvin(), 6.0e5.Pascal())
                .WithMassFlow(50.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 50.0);

            var turbineOut = fs.AddMaterialStream("tailrace");
            var turbineEnergy = fs.AddEnergyStream("W turbine");
            var ht = fs.AddHydroelectricTurbine("HT-1")
                .Configure(o => o.CreateConnectors())
                .WithStaticHeadM(50.0)
                .WithEfficiencyPercent(85.0)
                .WithInletVelocityMPerS(3.0)
                .WithOutletVelocityMPerS(3.0)
                .ConnectFeed(reservoir, 0)
                .ConnectProduct(turbineOut, 0);
            // The turbine's power output sits at OutputConnectors(1).
            fs.Inner.ConnectObjects(
                ht.Object.GraphicObject,
                turbineEnergy.Object.GraphicObject, 1, 0);

            var hotIn = fs.AddMaterialStream("hot process in")
                .At(353.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(5.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 5.0);

            var hotOut = fs.AddMaterialStream("hot process out");
            var coldOut = fs.AddMaterialStream("warmed river water");

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

            new ResultTable("Hydroelectric turbine + heat recovery")
                .RowInRange("Turbine power 10-40 kW", 10.0, 40.0, ht.GeneratedPowerKW, "kW")
                .RowInRange("Hot side cools (< 353 K)", 280.0, 352.0, hotOut.TemperatureK, "K")
                .RowInRange("Cold side warms (> 288 K)", 289.0, 360.0, coldOut.TemperatureK, "K")
                .Row("Cold side mass conservation", 50.0, coldOut.MassFlowKgPerSecond, 0.01, "kg/s")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "hydroelectric-heat-recovery");
        }
    }
}
