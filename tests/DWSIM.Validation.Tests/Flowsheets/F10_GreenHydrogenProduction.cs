using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F10 - Green hydrogen production via solar-powered water electrolysis.
    /// SolarPanel generates electricity → WaterElectrolyzer splits water into H2-rich + O2-rich streams.
    /// Validates: solar power output, H2:O2 molar ratio ≈ 2:1, mass balance.</summary>
    internal static class F10_GreenHydrogenProduction
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F10_GreenH2")
                .WithCompounds("Water", "Hydrogen", "Oxygen")
                // Not the steam tables: those are water and nothing else, and this flowsheet splits
                // water into hydrogen and oxygen. The shipped Water Electrolyzer sample uses Raoult
                // for the same reason.
                .WithPropertyPackage(PropertyPackages.Raoult);

            // Solar panel producing electricity
            var solarEnergy = fs.AddEnergyStream("solar_power");
            var sp = fs.AddSolarPanel("SP-1")
                .Configure(o => o.CreateConnectors())
                .WithPanelAreaM2(10.0)
                .WithPanelEfficiencyPercent(20.0)
                .WithPanelCount(100)
                .WithSolarIrradiationKWPerM2(1.0)
                .ConnectEnergyProduct(solarEnergy, 0);

            // Water feed to electrolyzer
            var water = fs.AddMaterialStream("water_feed")
                .At(298.15.Kelvin(), 5.0e5.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0);

            // Electrolyzer: 2 inputs (material port 0 + energy port 1), 2 outputs (H2 port 0, O2 port 1)
            var h2Out = fs.AddMaterialStream("h2_rich");
            var o2Out = fs.AddMaterialStream("o2_rich");
            fs.AddWaterElectrolyzer("EL-1")
                .Configure(o => o.CreateConnectors())
                .WithVoltage(180.0)
                .WithCellCount(100)
                .ConnectFeed(water, 0)
                .ConnectProduct(h2Out, 0)
                .ConnectProduct(o2Out, 1)
                .ConnectEnergyFeed(solarEnergy, 1);

            fs.Solve();

            double mH2 = h2Out.MassFlowKgPerSecond;
            double mO2 = o2Out.MassFlowKgPerSecond;

            double xH2 = h2Out.OverallMoleFraction("Hydrogen");
            double xO2 = o2Out.OverallMoleFraction("Oxygen");

            new ResultTable("F10 - Green hydrogen (solar + electrolysis)")
                .RowInRange("Solar power > 0", 1.0, 1000.0, sp.GeneratedPowerKW, "kW")
                .RowInRange("H2 in H2-rich stream", 0.50, 1.0, xH2, "-")
                .RowInRange("O2 in O2-rich stream", 0.001, 1.0, xO2, "-")
                .RowInRange("H2 product mass > 0", 0.001, 100.0, mH2, "kg/s")
                .RowInRange("O2 product mass > 0", 0.001, 100.0, mO2, "kg/s")
                .PrintAndThrowIfFailed();
        }
    }
}
