using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using DWSIM.UnitOperations.Reactors;

namespace DWSIM.Validation.Tests.Flowsheets
{
    /// <summary>F20 - CFB fast pyrolysis plant with two biomass compositions.
    /// (A) Pine wood (42/25/33 cellulose/hemicellulose/lignin) - External sand, high S/B
    /// (B) Agricultural residue (35/30/20 + 15% ash) - Internal char combustor, lower S/B
    /// Both feed at 1 kg/s → CFBFastPyrolysis → product stream.
    /// Validates: yield closure, outlet temperature, vapor residence time, product fractions,
    /// and char combustor coupling in mode B.</summary>
    internal static class F20_CFBPyrolysisPlant
    {
        public static void Run()
        {
            var rt = new ResultTable("F20 - CFB fast pyrolysis: 2 feedstocks × 2 sand modes");

            RunPineExternal(rt);
            RunResidueInternalCombustor(rt);

            rt.PrintAndThrowIfFailed();
        }

        private static void RunPineExternal(ResultTable rt)
        {
            var fs = Flowsheet.Create("F20A_Pine")
                .WithCompounds("Water", "Carbon dioxide", "Carbon monoxide", "Methane",
                               "Biomass_Generic")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("biomass")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Biomass_Generic", 1.0);

            var prod = fs.AddMaterialStream("prod");

            var py = fs.AddCFBFastPyrolysisReactor("CFB-A")
                .Configure(o => o.CreateConnectors())
                .WithRiserHeight(10.0.Meters())
                .WithRiserDiameter(0.8.Meters())
                .WithAxialCells(40)
                .WithCarrierGasVelocityMPerS(5.0)
                .WithSolidsHoldup(0.05)
                .WithSandMode(CFBSandMode.External)
                .WithSandInletTemperature(850.0.Kelvin())
                .WithSandToBiomassRatio(20.0)
                .WithHeatLossFraction(0.03)
                .WithBiomassComposition(0.42, 0.25, 0.33)
                .Configure(o => o.BiomassCompound = "Biomass_Generic")
                .ConnectFeed(feed, 0)
                .ConnectProduct(prod, 0);

            py.Object.Calculate();

            double yieldSum = py.Object.Result_OilYield_wfrac
                            + py.Object.Result_GasYield_wfrac
                            + py.Object.Result_CharYield_wfrac
                            + py.Object.Result_UnreactedSolid_wfrac;

            rt.RowInRange("[A] Pine yields ≈ 1", 0.95, 1.05, yieldSum, "-");
            rt.RowInRange("[A] Bio-oil yield > 0", 0.01, 0.95,
                py.Object.Result_OilYield_wfrac, "-");
            rt.RowInRange("[A] Gas yield > 0", 0.005, 0.50,
                py.Object.Result_GasYield_wfrac, "-");
            rt.RowInRange("[A] Char yield > 0", 0.005, 0.50,
                py.Object.Result_CharYield_wfrac, "-");
            rt.RowInRange("[A] T_out 650-850 K", 650.0, 850.0,
                py.Object.Result_OutletTemperature_K, "K");
            rt.RowInRange("[A] Vapor residence < 3 s", 0.01, 3.0,
                py.Object.Result_VaporResidenceTime_s, "s");
            rt.RowInRange("[A] Pyrolysis duty > 0", 1.0, 1e6,
                py.Object.Result_PyrolysisDuty_kW, "kW");

            // ---- Axial profile validation via Fluent API ----
            var traj = py.Trajectory;
            if (traj != null)
            {
                var series = py.ProfileSeriesNames;
                rt.RowInRange("[A] Profile series count > 0", 1, 50, series?.Length ?? 0, "-");

                var tempProfile = py.GetProfileSeries("T_K");
                rt.RowInRange("[A] Profile T_K points > 0", 2, 1000, tempProfile?.Length ?? 0, "pts");

                var csv = py.ProfileToCSV();
                rt.RowInRange("[A] Profile CSV length > 0", 10, 1e8, csv?.Length ?? 0, "chars");
            }
        }

        private static void RunResidueInternalCombustor(ResultTable rt)
        {
            var fs = Flowsheet.Create("F20B_Residue")
                .WithCompounds("Water", "Carbon dioxide", "Carbon monoxide", "Methane",
                               "Oxygen", "Nitrogen", "Biomass_Generic")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("biomass")
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow("Biomass_Generic", 1.0);

            var prod = fs.AddMaterialStream("prod");

            var py = fs.AddCFBFastPyrolysisReactor("CFB-B")
                .Configure(o => o.CreateConnectors())
                .WithRiserHeight(12.0.Meters())
                .WithRiserDiameter(1.0.Meters())
                .WithAxialCells(50)
                .WithCarrierGasVelocityMPerS(6.0)
                .WithSolidsHoldup(0.04)
                .WithSandMode(CFBSandMode.InternalCharCombustor)
                .WithSandToBiomassRatio(12.0)
                .WithHeatLossFraction(0.04)
                .WithBiomassComposition(0.35, 0.30, 0.20)
                .Configure(o =>
                {
                    o.BiomassCompound = "Biomass_Generic";
                    o.OxygenCompound = "Oxygen";
                    o.CO2Compound = "Carbon dioxide";
                    o.NitrogenCompound = "Nitrogen";
                    o.CharLHV_Jkg = 30e6;
                    o.CharCombustorExcessAir = 0.20;
                    o.CharCombustorHeatLoss = 0.03;
                })
                .ConnectFeed(feed, 0)
                .ConnectProduct(prod, 0);

            py.Object.Calculate();

            double yieldSum = py.Object.Result_OilYield_wfrac
                            + py.Object.Result_GasYield_wfrac
                            + py.Object.Result_CharYield_wfrac
                            + py.Object.Result_UnreactedSolid_wfrac;

            rt.RowInRange("[B] Residue yields ≈ 1", 0.95, 1.05, yieldSum, "-");
            rt.RowInRange("[B] Bio-oil yield > 0", 0.01, 0.95,
                py.Object.Result_OilYield_wfrac, "-");
            rt.RowInRange("[B] Gas yield > 0", 0.005, 0.50,
                py.Object.Result_GasYield_wfrac, "-");
            rt.RowInRange("[B] Char yield > 0", 0.005, 0.50,
                py.Object.Result_CharYield_wfrac, "-");
            rt.RowInRange("[B] T_out 600-900 K", 600.0, 900.0,
                py.Object.Result_OutletTemperature_K, "K");
            rt.RowInRange("[B] Combustor duty > 0", 1.0, 1e6,
                py.Object.Result_CombustorDuty_kW, "kW");
            rt.RowInRange("[B] Sand circulation > 0", 0.1, 100.0,
                py.Object.Result_SandCirculation_kgps, "kg/s");

            // ---- Axial profile: DataTable export ----
            var dt = py.ProfileToDataTable();
            if (dt != null)
                rt.RowInRange("[B] Profile DataTable rows > 0", 2, 1000, dt.Rows.Count, "rows");
        }
    }
}
