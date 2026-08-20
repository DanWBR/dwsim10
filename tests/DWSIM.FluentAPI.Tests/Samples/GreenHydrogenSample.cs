using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Green hydrogen production: solar-powered water electrolysis.
    /// Solar panel array (100 × 10 m², 20 %) generates electricity →
    /// water electrolyzer (180 V, 100 cells) splits water into H2-rich and O2-rich streams.
    /// Checks: solar power output, product purities, both product flows above zero.</summary>
    internal static class GreenHydrogenSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("GreenHydrogen")
                .WithCompounds("Water", "Hydrogen", "Oxygen")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var solarEnergy = fs.AddEnergyStream("solar power");
            var sp = fs.AddSolarPanel("SP-1")
                .Configure(o => o.CreateConnectors())
                .WithPanelAreaM2(10.0)
                .WithPanelEfficiencyPercent(20.0)
                .WithPanelCount(100)
                .WithSolarIrradiationKWPerM2(1.0)
                .ConnectEnergyProduct(solarEnergy, 0);

            var water = fs.AddMaterialStream("water feed")
                .At(298.15.Kelvin(), 5.0e5.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0);

            var h2Out = fs.AddMaterialStream("h2 product");
            var o2Out = fs.AddMaterialStream("o2 product");
            fs.AddWaterElectrolyzer("EL-1")
                .Configure(o => o.CreateConnectors())
                .WithVoltage(180.0)
                .WithCellCount(100)
                .ConnectFeed(water, 0)
                .ConnectProduct(h2Out, 0)
                .ConnectProduct(o2Out, 1)
                .ConnectEnergyFeed(solarEnergy, 1);

            fs.Solve();

            new ResultTable("Green hydrogen (solar + electrolysis)")
                .RowInRange("Solar power > 0", 1.0, 1000.0, sp.GeneratedPowerKW, "kW")
                .RowInRange("H2 in H2-rich stream", 0.50, 1.0, h2Out.OverallMoleFraction("Hydrogen"), "-")
                .RowInRange("O2 in O2-rich stream", 0.001, 1.0, o2Out.OverallMoleFraction("Oxygen"), "-")
                .RowInRange("H2 product mass > 0", 0.001, 100.0, h2Out.MassFlowKgPerSecond, "kg/s")
                .RowInRange("O2 product mass > 0", 0.001, 100.0, o2Out.MassFlowKgPerSecond, "kg/s")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "green-hydrogen-solar-electrolysis");
        }
    }
}
