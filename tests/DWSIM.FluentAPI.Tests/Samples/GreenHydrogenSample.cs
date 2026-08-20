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

            // Unset compounds do not default to zero; leaving H2/O2 alone puts phantom
            // product into the feed and the outlet flows stop making sense.
            var water = fs.AddMaterialStream("water feed")
                .At(298.15.Kelvin(), 5.0e5.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 1.0)
                .SetCompoundMassFlow("Hydrogen", 0.0)
                .SetCompoundMassFlow("Oxygen", 0.0);

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

            // Faraday anchor: n(H2) = P / V * cells / 2F = 200000/180*100/(2*96485).
            double nH2 = h2Out.Object.Phases[0].Compounds["Hydrogen"].MolarFlow.GetValueOrDefault();
            double nO2 = o2Out.Object.Phases[0].Compounds["Oxygen"].MolarFlow.GetValueOrDefault();
            double nH2_faraday = 200000.0 / 180.0 * 100.0 / (2.0 * 96485.3365);

            new ResultTable("Green hydrogen (solar + electrolysis)")
                .RowInRange("Solar power > 0", 1.0, 1000.0, sp.GeneratedPowerKW, "kW")
                .RowInRange("H2 in H2-rich stream", 0.50, 1.0, h2Out.OverallMoleFraction("Hydrogen"), "-")
                .Row("H2 production follows Faraday's law", nH2_faraday, nH2, 0.005, "mol/s")
                .Row("O2 production = half the H2", nH2_faraday / 2.0, nO2, 0.005, "mol/s")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "green-hydrogen-solar-electrolysis");
        }
    }
}
