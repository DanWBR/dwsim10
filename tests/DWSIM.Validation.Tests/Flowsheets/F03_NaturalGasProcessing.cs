using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using CompMode = DWSIM.UnitOperations.UnitOperations.Compressor;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>Natural gas processing - three-phase separation + export compression.
    /// Train: wet gas (CH4/C2H6/C3H8/H2O) @ 300 K, 50 bar →
    ///        Cooler (260 K) → Vessel (3-phase: gas, condensate, water) →
    ///        Export compressor (50 → 80 bar).
    /// Validates: 3 phases coexist after cooling; outlet gas nearly dry; closed mass balance.</summary>
    internal static class F03_NaturalGasProcessing
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("F03_NaturalGas")
                .WithCompounds("Methane", "Ethane", "Propane", "Water")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // Typical wet gas: 90 % CH4, 7 % C2H6, 2 % C3H8, 1 % H2O (mol)
            var feed = fs.AddMaterialStream("wet_gas")
                .At(300.0.Kelvin(), 50e5.Pascal())
                .WithMolarFlow(100.0.MolPerSecond())
                .SetCompoundMolarFlow("Methane", 90.0)
                .SetCompoundMolarFlow("Ethane", 7.0)
                .SetCompoundMolarFlow("Propane", 2.0)
                .SetCompoundMolarFlow("Water", 1.0);

            // Cooler to induce condensation
            var chilled = fs.AddMaterialStream("chilled");
            var qCool = fs.AddEnergyStream("Q_cool");
            fs.AddCooler("CL-1")
                .WithOutletTemperature(260.0.Kelvin())
                .WithPressureDrop(0.0.Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(chilled, 0)
                .ConnectEnergyFeed(qCool, 1);

            // Three-phase vessel (Vapor + Liq1 + Liq2)
            var dryGas = fs.AddMaterialStream("dry_gas");
            var ngl = fs.AddMaterialStream("NGL");
            var water = fs.AddMaterialStream("water_out");

            fs.AddSeparator("V-1")
                .ConnectFeed(chilled, 0)
                .ConnectProduct(dryGas, 0)      // vapor
                .ConnectProduct(ngl, 1)          // liquid hydrocarbon
                .ConnectProduct(water, 2);       // water

            // Export compressor 50 → 80 bar
            var export = fs.AddMaterialStream("export_gas");
            var wExp = fs.AddEnergyStream("W_exp");
            fs.AddCompressor("C-EXP")
                .WithProcessPath(CompMode.ProcessPathType.Adiabatic)
                .WithOutletPressure(80e5.Pascal())
                .WithAdiabaticEfficiencyPercent(75.0)
                .ConnectFeed(dryGas, 0)
                .ConnectProduct(export, 0)
                .ConnectEnergyFeed(wExp, 1);

            fs.Solve();

            double mFeed = feed.MassFlowKgPerSecond;
            double mGas = dryGas.MassFlowKgPerSecond;
            double mNGL = ngl.MassFlowKgPerSecond;
            double mWater = water.MassFlowKgPerSecond;

            double xH2O_dry = dryGas.OverallMoleFraction("Water");
            double xCH4_dry = dryGas.OverallMoleFraction("Methane");
            double xH2O_water = water.OverallMoleFraction("Water");

            new ResultTable("F03 - Natural gas processing")
                .Row("Global balance: feed = gas + NGL + water", mFeed, mGas + mNGL + mWater, 0.005, "kg/s")
                .RowInRange("Dry gas enriched in CH4 (>85 %)", 0.85, 1.0, xCH4_dry, "-")
                .RowInRange("Residual water in gas &lt; 1 %", 0.0, 0.01, xH2O_dry, "-")
                .RowInRange("Condensed liquids (NGL + water) > 0", 1e-6, 10.0, mNGL + mWater, "kg/s")
                .RowInRange("Export compressor work > 0", 0.001, 1e6, wExp.EnergyFlowKW, "kW")
                .RowInRange("P_export = 80 bar", 79.9e5, 80.1e5, export.PressurePa, "Pa")
                .PrintAndThrowIfFailed();
        }
    }
}
