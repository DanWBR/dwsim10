using NUnit.Framework;

namespace DWSIM.Validation.Tests
{
    /// <summary>
    /// Runs every validation case under NUnit, so `dotnet test` on the solution covers them.
    /// </summary>
    /// <remarks>
    /// The cases are the console runner's: each sets up a small simulation and throws when the
    /// answer moves away from its published reference. This shell only names them for the test
    /// host, and is generated from Program.cs so the two cannot drift apart.
    ///
    /// To see the comparison table for one case, run it through the runner instead:
    ///     dotnet run --project tests/DWSIM.Validation.Tests -- t01
    /// </remarks>
    [TestFixture]
    public class ValidationCases
    {
        /// <summary>
        /// What the console runner does in Main before any case runs.
        /// </summary>
        /// <remarks>
        /// The compound and property-package databases are found relative to the assembly, and
        /// the external unit operations are loaded by an assembly resolver. Under the test host
        /// neither is set up, and the cases fail looking for compounds that are on disk.
        /// </remarks>
        [OneTimeSetUp]
        public void Setup()
        {
            System.IO.Directory.SetCurrentDirectory(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location));

            DWSIM.Automation.FluentAPI.Flowsheet.RegisterAssemblyResolver();
        }

        // Bioprocess
        [Test] public void B01_BioReactor_Monod() => Bioprocess.B01_BioReactor_Monod.Run();
        [Test] public void B02_AnaerobicDigester_ADM1() => Bioprocess.B02_AnaerobicDigester_ADM1.Run();
        [Test] public void B03_Centrifuge_DiskStack() => Bioprocess.B03_Centrifuge_DiskStack.Run();
        [Test] public void B04_Chromatography_BindElute() => Bioprocess.B04_Chromatography_BindElute.Run();
        [Test] public void B05_CellLysis_HighPressure() => Bioprocess.B05_CellLysis_HighPressure.Run();
        [Test] public void B06_CrossflowUF_Concentration() => Bioprocess.B06_CrossflowUF_Concentration.Run();
        [Test] public void B07_Crystallizer_Cooling() => Bioprocess.B07_Crystallizer_Cooling.Run();
        [Test] public void B08_BiogasUpgrader_Amine() => Bioprocess.B08_BiogasUpgrader_Amine.Run();
        [Test] public void B09_Pretreatment_DiluteAcid() => Bioprocess.B09_Pretreatment_DiluteAcid.Run();
        [Test] public void B10_CFBFastPyrolysis() => Bioprocess.B10_CFBFastPyrolysis.Run();
        [Test] public void B11_ADM1_BSM2_Benchmark() => Bioprocess.B11_ADM1_BSM2_Benchmark.Run();
        [Test] public void B12_ADM1_Souring_And_Balances() => Bioprocess.B12_ADM1_Souring_And_Balances.Run();
        [Test] public void B13_BioReactor_SulfurBalance() => Bioprocess.B13_BioReactor_SulfurBalance.Run();
        [Test] public void B14_ADM1S_SulfateReduction() => Bioprocess.B14_ADM1S_SulfateReduction.Run();
        [Test] public void Z99_AnaerobicDigesterBugfixCheck() => Bioprocess.Z99_AnaerobicDigesterBugfixCheck.Run();

        // Compounds
        [Test] public void C01_PseudoComponent_HeavyCut() => Compounds.C01_PseudoComponent_HeavyCut.Run();
        [Test] public void C02_CompoundFromJson() => Compounds.C02_CompoundFromJson.Run();

        // Flowsheets
        [Test] public void F01_RefrigerationCycle() => Flowsheets.F01_RefrigerationCycle.Run();
        [Test] public void F02_EthanolPlant() => Flowsheets.F02_EthanolPlant.Run();
        [Test] public void F03_NaturalGasProcessing() => Flowsheets.F03_NaturalGasProcessing.Run();
        [Test] public void F04_AmmoniaSynthesisLoop() => Flowsheets.F04_AmmoniaSynthesisLoop.Run();
        [Test] public void F06_LignocelluosicFermentation() => Flowsheets.F06_LignocelluosicFermentation.Run();
        [Test] public void F07_DownstreamProcessing() => Flowsheets.F07_DownstreamProcessing.Run();
        [Test] public void F08_BiogasUpgradingPlant() => Flowsheets.F08_BiogasUpgradingPlant.Run();
        [Test] public void F09_MicroalgaeCultivation() => Flowsheets.F09_MicroalgaeCultivation.Run();
        [Test] public void F10_GreenHydrogenProduction() => Flowsheets.F10_GreenHydrogenProduction.Run();
        [Test] public void F11_BenzeneTolueneDistillation() => Flowsheets.F11_BenzeneTolueneDistillation.Run();
        [Test] public void F15_MethanolSynthesis() => Flowsheets.F15_MethanolSynthesis.Run();
        [Test] public void F16_HydroelectricPower() => Flowsheets.F16_HydroelectricPower.Run();
        [Test] public void F18_AnaerobicDigesterADM1() => Flowsheets.F18_AnaerobicDigesterADM1.Run();
        [Test] public void F19_BioReactorModes() => Flowsheets.F19_BioReactorModes.Run();
        [Test] public void F20_CFBPyrolysisPlant() => Flowsheets.F20_CFBPyrolysisPlant.Run();
        [Test] public void F21_AnaerobicDigesterSulfur() => Flowsheets.F21_AnaerobicDigesterSulfur.Run();

        // PropertyPackageConfig
        [Test] public void P01_PR_KijEffect() => PropertyPackageConfig.P01_PR_KijEffect.Run();
        [Test] public void P02_NRTL_BinaryEffect() => PropertyPackageConfig.P02_NRTL_BinaryEffect.Run();
        [Test] public void P03_FlashSettings() => PropertyPackageConfig.P03_FlashSettings.Run();

        // Samples
        [Test] public void S01_AllSamples() => Samples.S01_AllSamples.Run();

        // Thermodynamics
        [Test] public void T01_SteamTables_Density_Water25C() => Thermodynamics.T01_SteamTables_Density_Water25C.Run();
        [Test] public void T02_SteamTables_SaturationT_1atm() => Thermodynamics.T02_SteamTables_SaturationT_1atm.Run();
        [Test] public void T03_NRTL_EthanolWaterAzeotrope() => Thermodynamics.T03_NRTL_EthanolWaterAzeotrope.Run();
        [Test] public void T04_PR_MethaneIdealGasLimit() => Thermodynamics.T04_PR_MethaneIdealGasLimit.Run();
        [Test] public void T05_SRK_PropaneVaporPressure() => Thermodynamics.T05_SRK_PropaneVaporPressure.Run();
        [Test] public void T06_PR78_NHeptaneDensity() => Thermodynamics.T06_PR78_NHeptaneDensity.Run();
        [Test] public void T07_LKP_MethaneCompressibility() => Thermodynamics.T07_LKP_MethaneCompressibility.Run();
        [Test] public void T08_UNIQUAC_EthanolWaterAzeotrope() => Thermodynamics.T08_UNIQUAC_EthanolWaterAzeotrope.Run();
        [Test] public void T09_Wilson_MethanolWater() => Thermodynamics.T09_Wilson_MethanolWater.Run();
        [Test] public void T10_UNIFAC_EthanolWaterAzeotrope() => Thermodynamics.T10_UNIFAC_EthanolWaterAzeotrope.Run();
        [Test] public void T11_ModUNIFAC_AcetoneWater() => Thermodynamics.T11_ModUNIFAC_AcetoneWater.Run();
        [Test] public void T12_Raoult_BenzeneToluene() => Thermodynamics.T12_Raoult_BenzeneToluene.Run();
        [Test] public void T13_Seawater_Density() => Thermodynamics.T13_Seawater_Density.Run();
        [Test] public void T14_PRSV2_MethanolVaporPressure() => Thermodynamics.T14_PRSV2_MethanolVaporPressure.Run();
        [Test] public void T15_GraysonStreed_HydrocarbonMix() => Thermodynamics.T15_GraysonStreed_HydrocarbonMix.Run();
        [Test] public void T16_UNIFAC_LL_NHexaneWater() => Thermodynamics.T16_UNIFAC_LL_NHexaneWater.Run();
        [Test] public void T17_PR_vs_SRK_NaturalGas() => Thermodynamics.T17_PR_vs_SRK_NaturalGas.Run();
        [Test] public void T18_ChaoSeader_HydrocarbonFlash() => Thermodynamics.T18_ChaoSeader_HydrocarbonFlash.Run();
        [Test] public void T19_ModUNIFAC_NIST_Toluene() => Thermodynamics.T19_ModUNIFAC_NIST_Toluene.Run();
        [Test] public void T20_PR_NaturalGas_VLE5Comp() => Thermodynamics.T20_PR_NaturalGas_VLE5Comp.Run();
        [Test] public void T21_SRK_AcidGas_VLE4Comp() => Thermodynamics.T21_SRK_AcidGas_VLE4Comp.Run();
        [Test] public void T22_PR_WaterHydrocarbon_VLLE() => Thermodynamics.T22_PR_WaterHydrocarbon_VLLE.Run();
        [Test] public void T23_PR_WaterPentaneOctane_VLLE() => Thermodynamics.T23_PR_WaterPentaneOctane_VLLE.Run();
        [Test] public void T24_NRTL_WaterBenzeneAcetone_LLE() => Thermodynamics.T24_NRTL_WaterBenzeneAcetone_LLE.Run();
        [Test] public void T25_UNIQUAC_WaterTolueneAcetone_LLE() => Thermodynamics.T25_UNIQUAC_WaterTolueneAcetone_LLE.Run();
        [Test] public void T26_NRTL_XyleneCrystallization_SLE() => Thermodynamics.T26_NRTL_XyleneCrystallization_SLE.Run();

        // UnitOpsAdvanced
        [Test] public void A01_Distillation_EthanolWater() => UnitOpsAdvanced.A01_Distillation_EthanolWater.Run();
        [Test] public void A02_ConvReactor_Stoichiometry() => UnitOpsAdvanced.A02_ConvReactor_Stoichiometry.Run();
        [Test] public void A03_EquilibriumReactor_WGS() => UnitOpsAdvanced.A03_EquilibriumReactor_WGS.Run();
        [Test] public void A04_Mixer_EnergyBalance() => UnitOpsAdvanced.A04_Mixer_EnergyBalance.Run();
        [Test] public void A05_Gibbs_SteamReforming() => UnitOpsAdvanced.A05_Gibbs_SteamReforming.Run();

        // UnitOpsBasic
        [Test] public void U01_Heater_WaterDuty() => UnitOpsBasic.U01_Heater_WaterDuty.Run();
        [Test] public void U02_Cooler_WaterDuty() => UnitOpsBasic.U02_Cooler_WaterDuty.Run();
        [Test] public void U03_Pump_WaterPower() => UnitOpsBasic.U03_Pump_WaterPower.Run();
        [Test] public void U04_HeatExchanger_DutyBalance() => UnitOpsBasic.U04_HeatExchanger_DutyBalance.Run();
        [Test] public void U05_Compressor_Isentropic() => UnitOpsBasic.U05_Compressor_Isentropic.Run();
        [Test] public void U06_Expander_Isentropic() => UnitOpsBasic.U06_Expander_Isentropic.Run();
        [Test] public void U07_Valve_JouleThomson() => UnitOpsBasic.U07_Valve_JouleThomson.Run();
        [Test] public void U08_Splitter_Ratios() => UnitOpsBasic.U08_Splitter_Ratios.Run();
    }
}
