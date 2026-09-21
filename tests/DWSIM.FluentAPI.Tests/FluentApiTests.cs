//    Runs the fluent API tests under NUnit, so they sit in the same test run as the rest.
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Each case builds a flowsheet through the fluent API, solves it and checks the answer.
    /// The test bodies throw on failure, which is what NUnit reads as a failing test.
    /// </summary>
    [TestFixture]
    public class FluentApiTests
    {
        /// <summary>
        /// The cases that take minutes rather than seconds: the polymer models, which run PC-SAFT
        /// flashes over long compound lists, and the dynamic column runs, which integrate hundreds of
        /// steps. Together they are around 90 % of the time this suite takes, so they are left out of
        /// an ordinary run and are meant to be run before cutting a release:
        ///
        ///     dotnet test tests\DWSIM.FluentAPI.Tests --filter TestCategory=ReleaseOnly
        ///
        /// tests\tests.runsettings is what keeps them out of a plain <c>dotnet test</c>.
        /// </summary>
        public const string Slow = "ReleaseOnly";

        [Test] public void AMixerBalancesMassAndEnergy() => MixerTest.Run();

        [Test] public void ABroydenRecycleConvergesLikeSubstitution() => RecycleBroydenTest.Run();

        [Test] public void ABroydenRecycleLeavesASubstitutionRecycleToConverge() => RecycleBroydenTest.RunBesideSubstitution();

        [Test] public void AConvergedBroydenRecycleHoldsWhileASlowerRecycleConverges() => RecycleBroydenTest.RunBesideSlowerSubstitution();

        [Test] public void APythonScriptUnitOperationImportsTheStandardLibrary() => PythonScriptUOTest.Run();

        [Test] public void AConversionReactorConsumesItsReagents() => ConvReactorTest.Run();

        [Test] public void ADistillationColumnSeparates() => DistillationTest.Run();

        [Test] public void AnAbsorberRunsToTheEnd() => AbsorberTest.Run();

        [Test] public void TheCleanEnergyUnitOperationsCalculate() => CleanEnergyTest.Run();

        [Test] public void TheExternalCatalogNamesResolve() => ExternalCatalogTest.Run();

        [Test] public void TheTypedBuildersInstantiate() => TypedBuildersTest.Run();

        [Test] public void ABioprocessTrainSolves() => BioTrainTest.Run();

        [Test] public void AnExistingFlowsheetCanBeWrapped() => WrapTest.Run();

        [Test] public void ThePhaseDiagramsAreBuilt() => PhaseDiagramTest.Run();

        [Test] public void APumpFollowsItsPerformanceCurves() => PumpCurvesTest.Run();

        [Test] public void APumpReadsItsCurvesMeasuredAtSeveralSpeeds() => PumpMultiSpeedCurvesTest.Run();

        [Test] public void ADisplacementPumpDeliversWhatItDisplaces() => PositiveDisplacementPumpTest.Run();

        [Test] public void PropertyIdentifiersHaveReadableNames() => PropertyCatalogTest.Run();
        [Test] public void TheAssistantApiAnswersOverHttp() => AssistantHttpTest.Run();
        [Test] public void TheDiagnosticsNameAFlowsheetsFaults() => FlowsheetDiagnosticsTest.Run();
        [Test] public void ADynamicRunFollowsItsScheduledEvents() => DynamicsEventProfileTest.Run();
        [Test] public void AnEventWritesItsValueInTheRightUnit() => EventUnitConversionTest.Run();

        [Test] public void ATankFillsAtTheRateItIsFed() => DynamicsTankFillingTest.Run();
        [Test, Category(Slow)] public void ADynamicColumnRidesAFeedStep() => DynamicsColumnCaseTest.Run();
        [Test, Category(Slow)] public void AColumnStartsUpFromEmpty() => DynamicsColumnStartupCaseTest.Run();
        [Test, Category(Slow)] public void AColumnShutsDown() => DynamicsColumnShutdownCaseTest.Run();

        [Test] public void NaturalLayoutLaysRecyclesOutAsARectangle() => RecycleLayoutTest.Run();

        // ----- Industrial sample flowsheets: each one solves, is checked for physical
        // ----- sense, and is saved (.dwxmz + PFD screenshot) for the dwsim-case-library.

        [Test] public void ThePropaneRefrigerationSampleSolvesAndSaves() => PropaneRefrigerationSample.Run();

        [Test] public void TheSteamMethaneReformerSampleSolvesAndSaves() => SteamMethaneReformerSample.Run();

        [Test] public void TheAmmoniaSynthesisSampleSolvesAndSaves() => AmmoniaSynthesisSample.Run();

        [Test] public void TheMethanolSynthesisSampleSolvesAndSaves() => MethanolSynthesisSample.Run();

        [Test] public void TheBenzeneTolueneSampleSolvesAndSaves() => BenzeneTolueneSample.Run();

        [Test] public void TheEthanolDistillerySampleSolvesAndSaves() => EthanolDistillerySample.Run();

        [Test] public void TheNaturalGasSampleSolvesAndSaves() => NaturalGasProcessingSample.Run();

        [Test] public void TheHydroelectricSampleSolvesAndSaves() => HydroelectricSample.Run();

        [Test] public void ThePumpCurvesSampleSolvesAndSaves() => PumpCurvesSample.Run();

        [Test] public void TheCompressorCurvesSampleSolvesAndSaves() => CompressorCurvesSample.Run();

        [Test] public void TheExpanderCurvesSampleSolvesAndSaves() => ExpanderCurvesSample.Run();

        [Test] public void TheDosingPumpDynamicsSampleSolvesAndSaves() => DosingPumpDynamicsSample.Run();

        [Test] public void TheGreenHydrogenSampleSolvesAndSaves() => GreenHydrogenSample.Run();

        [Test] public void TheBiogasToGridSampleSolvesAndSaves() => BiogasToGridSample.Run();

        [Test, Category(Slow)] public void ThePolymerDevolatilizationSampleSolvesAndSaves() => PolymerDevolatilizationSample.Run();

        [Test, Category(Slow)] public void ThePolymerCloudPointSampleSolvesAndSaves() => PolymerCloudPointSample.Run();

        [Test, Category(Slow)] public void TheCopolymerDevolatilizationSampleSolvesAndSaves() => CopolymerDevolatilizationSample.Run();

        [Test, Category(Slow)] public void ThePegDewateringSampleSolvesAndSaves() => PegDewateringSample.Run();

        [Test, Category(Slow)] public void TheStyrenePolymerizationSampleSolvesAndSaves() => StyrenePolymerizationSample.Run();
    }
}
