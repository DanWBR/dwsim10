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
        [Test] public void AMixerBalancesMassAndEnergy() => MixerTest.Run();

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

        [Test] public void TheGreenHydrogenSampleSolvesAndSaves() => GreenHydrogenSample.Run();

        [Test] public void TheBiogasToGridSampleSolvesAndSaves() => BiogasToGridSample.Run();
    }
}
