using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Validation.Tests.Bioprocess
{
    /// <summary>CFB Fast Pyrolysis Reactor - pine biomass (cell 42 % / hemi 25 % / lig 33 %).
    /// Riser 8 m × 0.6 m, T_sand=833 K, S/B ratio=15. Expected: yields oil+gas+char ≈ 1,
    /// pyrolysis duty > 0 (endothermic), T_out within fast-pyrolysis range (700-820 K).</summary>
    internal static class B10_CFBFastPyrolysis
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("B10_CFB")
                .WithCompounds("Water", "Carbon dioxide", "Carbon monoxide", "Methane", "Biomass_Generic")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("biomass")
                .At(308.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Biomass_Generic", 1.0);

            var prod = fs.AddMaterialStream("prod");

            var py = fs.AddCFBFastPyrolysisReactor("CFB-1")
                .Configure(o => o.CreateConnectors())
                .WithRiserHeight(8.0.Meters())
                .WithRiserDiameter(0.6.Meters())
                .WithAxialCells(20)
                .WithCarrierGasVelocityMPerS(5.0)
                .WithSolidsHoldup(0.05)
                .WithSandMode(CFBSandMode.External)
                .WithSandInletTemperature(833.0.Kelvin())
                .WithSandToBiomassRatio(15.0)
                .WithHeatLossFraction(0.05)
                .WithBiomassComposition(0.42, 0.25, 0.33)
                .Configure(o => o.BiomassCompound = "Biomass_Generic")
                .ConnectFeed(feed, 0)
                .ConnectProduct(prod, 0);

            py.Object.Calculate();

            double yieldsTotal = py.Object.Result_OilYield_wfrac
                                + py.Object.Result_GasYield_wfrac
                                + py.Object.Result_CharYield_wfrac
                                + py.Object.Result_UnreactedSolid_wfrac;

            new ResultTable("CFB Fast Pyrolysis - pine, T_sand=833 K")
                .RowInRange("Yields sum ≈ 1", 0.95, 1.05, yieldsTotal, "-")
                .RowInRange("Oil yield > 0", 1e-3, 1.0, py.Object.Result_OilYield_wfrac, "-")
                .RowInRange("Gas yield > 0", 1e-3, 1.0, py.Object.Result_GasYield_wfrac, "-")
                .RowInRange("Char yield > 0", 1e-3, 1.0, py.Object.Result_CharYield_wfrac, "-")
                .RowInRange("T_out within pyrolysis range (700-820 K)", 700.0, 820.0, py.Object.Result_OutletTemperature_K, "K")
                .RowInRange("Pyrolysis duty > 0 (endothermic)", 1.0, 1e6, py.Object.Result_PyrolysisDuty_kW, "kW")
                .PrintAndThrowIfFailed();
        }
    }
}
