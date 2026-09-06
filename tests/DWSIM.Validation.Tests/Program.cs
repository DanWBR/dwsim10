using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DWSIM.Validation.Tests
{
    internal static class Program
    {
        // Cases that need a Plus unit operation - refining, CCUS, the GPU column solver and
        // the electrolyte ops - live in DWSIM.Validation.Tests.Plus, in the Patreon repository,
        // which carries the projects they depend on. The gaps in the numbering here are those.
        private static readonly Dictionary<string, Action> Cases = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
        {
            // Thermodynamics
            { "t01", Thermodynamics.T01_SteamTables_Density_Water25C.Run },
            { "t02", Thermodynamics.T02_SteamTables_SaturationT_1atm.Run },
            { "t03", Thermodynamics.T03_NRTL_EthanolWaterAzeotrope.Run },
            { "t04", Thermodynamics.T04_PR_MethaneIdealGasLimit.Run },
            { "t05", Thermodynamics.T05_SRK_PropaneVaporPressure.Run },
            { "t06", Thermodynamics.T06_PR78_NHeptaneDensity.Run },
            { "t07", Thermodynamics.T07_LKP_MethaneCompressibility.Run },
            { "t08", Thermodynamics.T08_UNIQUAC_EthanolWaterAzeotrope.Run },
            { "t09", Thermodynamics.T09_Wilson_MethanolWater.Run },
            { "t10", Thermodynamics.T10_UNIFAC_EthanolWaterAzeotrope.Run },
            { "t11", Thermodynamics.T11_ModUNIFAC_AcetoneWater.Run },
            { "t12", Thermodynamics.T12_Raoult_BenzeneToluene.Run },
            { "t13", Thermodynamics.T13_Seawater_Density.Run },
            { "t14", Thermodynamics.T14_PRSV2_MethanolVaporPressure.Run },
            { "t15", Thermodynamics.T15_GraysonStreed_HydrocarbonMix.Run },
            { "t16", Thermodynamics.T16_UNIFAC_LL_NHexaneWater.Run },
            { "t17", Thermodynamics.T17_PR_vs_SRK_NaturalGas.Run },
            { "t18", Thermodynamics.T18_ChaoSeader_HydrocarbonFlash.Run },
            { "t19", Thermodynamics.T19_ModUNIFAC_NIST_Toluene.Run },
            // Unit ops basic
            { "u01", UnitOpsBasic.U01_Heater_WaterDuty.Run },
            { "u02", UnitOpsBasic.U02_Cooler_WaterDuty.Run },
            { "u03", UnitOpsBasic.U03_Pump_WaterPower.Run },
            { "u04", UnitOpsBasic.U04_HeatExchanger_DutyBalance.Run },
            { "u05", UnitOpsBasic.U05_Compressor_Isentropic.Run },
            { "u06", UnitOpsBasic.U06_Expander_Isentropic.Run },
            { "u07", UnitOpsBasic.U07_Valve_JouleThomson.Run },
            { "u08", UnitOpsBasic.U08_Splitter_Ratios.Run },
            // Unit ops advanced
            { "a01", UnitOpsAdvanced.A01_Distillation_EthanolWater.Run },
            { "a02", UnitOpsAdvanced.A02_ConvReactor_Stoichiometry.Run },
            { "a03", UnitOpsAdvanced.A03_EquilibriumReactor_WGS.Run },
            { "a04", UnitOpsAdvanced.A04_Mixer_EnergyBalance.Run },
            { "a05", UnitOpsAdvanced.A05_Gibbs_SteamReforming.Run },
            // Bioprocess
            { "b01", Bioprocess.B01_BioReactor_Monod.Run },
            { "b02", Bioprocess.B02_AnaerobicDigester_ADM1.Run },
            { "b03", Bioprocess.B03_Centrifuge_DiskStack.Run },
            { "b04", Bioprocess.B04_Chromatography_BindElute.Run },
            { "b05", Bioprocess.B05_CellLysis_HighPressure.Run },
            { "b06", Bioprocess.B06_CrossflowUF_Concentration.Run },
            { "b07", Bioprocess.B07_Crystallizer_Cooling.Run },
            { "b08", Bioprocess.B08_BiogasUpgrader_Amine.Run },
            { "b09", Bioprocess.B09_Pretreatment_DiluteAcid.Run },
            { "b10", Bioprocess.B10_CFBFastPyrolysis.Run },
            { "b11", Bioprocess.B11_ADM1_BSM2_Benchmark.Run },
            { "b12", Bioprocess.B12_ADM1_Souring_And_Balances.Run },
            { "b13", Bioprocess.B13_BioReactor_SulfurBalance.Run },
            { "b14", Bioprocess.B14_ADM1S_SulfateReduction.Run },
            { "z99", Bioprocess.Z99_AnaerobicDigesterBugfixCheck.Run },
            // Compounds
            { "c01", Compounds.C01_PseudoComponent_HeavyCut.Run },
            { "c02", Compounds.C02_CompoundFromJson.Run },
            // Refining (Plus, gracefully skipped without patron key)
            // Property package configuration
            { "p01", PropertyPackageConfig.P01_PR_KijEffect.Run },
            { "p02", PropertyPackageConfig.P02_NRTL_BinaryEffect.Run },
            { "p03", PropertyPackageConfig.P03_FlashSettings.Run },
            // Full process flowsheets
            { "f01", Flowsheets.F01_RefrigerationCycle.Run },
            { "f02", Flowsheets.F02_EthanolPlant.Run },
            { "f03", Flowsheets.F03_NaturalGasProcessing.Run },
            { "f04", Flowsheets.F04_AmmoniaSynthesisLoop.Run },
            { "f06", Flowsheets.F06_LignocelluosicFermentation.Run },
            { "f07", Flowsheets.F07_DownstreamProcessing.Run },
            { "f08", Flowsheets.F08_BiogasUpgradingPlant.Run },
            { "f09", Flowsheets.F09_MicroalgaeCultivation.Run },
            { "f10", Flowsheets.F10_GreenHydrogenProduction.Run },
            { "f11", Flowsheets.F11_BenzeneTolueneDistillation.Run },
            { "f16", Flowsheets.F16_HydroelectricPower.Run },
            { "f18", Flowsheets.F18_AnaerobicDigesterADM1.Run },
            { "f19", Flowsheets.F19_BioReactorModes.Run },
            { "f20", Flowsheets.F20_CFBPyrolysisPlant.Run },
            { "f21", Flowsheets.F21_AnaerobicDigesterSulfur.Run },
            // CCUS (Plus, gracefully skipped without patron key)
            // Sample suite (runs last - loads every shipped flowsheet sample)
            { "s01", Samples.S01_AllSamples.Run },
        };

        [STAThread]
        public static int Main(string[] args)
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            DWSIM.Automation.FluentAPI.Flowsheet.RegisterAssemblyResolver();

            var names = args.Length > 0 ? args : new List<string>(Cases.Keys).ToArray();

            int failed = 0;
            foreach (var name in names)
            {
                Console.WriteLine();
                Console.WriteLine("=== Running: " + name);
                if (!Cases.TryGetValue(name, out var action))
                {
                    Console.WriteLine("Unknown test: " + name);
                    failed++;
                    continue;
                }
                try
                {
                    action();
                    Console.WriteLine("--- " + name + ": OK");
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("--- " + name + ": FAIL");
                    Console.WriteLine(ex.Message);
                    if (ex is AggregateException agg)
                    {
                        foreach (var inner in agg.InnerExceptions)
                        {
                            Console.WriteLine("  inner: " + inner.GetType().Name + ": " + inner.Message);
                            if (Environment.GetEnvironmentVariable("VTEST_VERBOSE") == "1")
                                Console.WriteLine(inner.StackTrace);
                        }
                    }
                    else if (ex.InnerException != null)
                    {
                        Console.WriteLine("  inner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message);
                        if (Environment.GetEnvironmentVariable("VTEST_VERBOSE") == "1")
                            Console.WriteLine(ex.InnerException.StackTrace);
                    }
                    if (Environment.GetEnvironmentVariable("VTEST_VERBOSE") == "1")
                        Console.WriteLine(ex.StackTrace);
                }
                Console.Out.Flush();
            }

            Console.WriteLine();
            Console.WriteLine(failed == 0 ? "ALL TESTS PASSED" : (failed + " TEST(S) FAILED"));
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("Press any key to close...");
                Console.ReadKey();
            }
            Console.Out.Flush();
            Environment.Exit(failed);
            return failed;
        }
    }
}
