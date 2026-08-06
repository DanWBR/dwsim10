using System;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Verifies the typed bioprocess builders compile, instantiate through the
    /// IExternalUnitOperation path, and accept fluent setter calls.
    /// </summary>
    internal static class TypedBuildersTest
    {
        public static void Run()
        {
            ProbeBioprocess();
        }

        private static void ProbeBioprocess()
        {
            var fs = Flowsheet.Create("BioBuildersProbe")
                .WithCompound("Water");

            var br = fs.AddBioReactor("BR-1")
                .WithVolume((2.5).CubicMeters())
                .WithKineticModel(DWSIM.UnitOperations.Reactors.BioKineticModel.Monod)
                .WithOperatingMode(DWSIM.UnitOperations.Reactors.BioReactorMode.Batch)
                .WithMaxSpecificGrowthPerHour(0.6)
                .WithBiomassYield(0.45);

            var ad = fs.AddAnaerobicDigester("AD-1")
                .WithVolume(50.0.CubicMeters())
                .WithCODRemoval(0.88)
                .WithModel(DWSIM.UnitOperations.Reactors.DigesterModel.ADM1Lite);

            var pyr = fs.AddCFBFastPyrolysisReactor("PYR-1")
                .WithRiserHeight(10.0.Meters())
                .WithSandToBiomassRatio(20.0)
                .WithBiomassComposition(0.42, 0.28, 0.30);

            var pre = fs.AddPretreatmentReactor("PRE-1")
                .WithTechnology(DWSIM.UnitOperations.Reactors.PretreatmentType.SteamExplosion)
                .WithSeverityLogR0(3.8)
                .WithCelluloseConversion(0.12);

            var bgu = fs.AddBiogasUpgrader("BGU-1")
                .WithTechnology(DWSIM.UnitOperations.UnitOperations.BiogasUpgraderTech.MembraneSeparation)
                .WithCO2Removal(0.96)
                .WithTargetCH4Purity(0.97);

            var lys = fs.AddCellLysis("LYS-1")
                .WithTechnology(DWSIM.UnitOperations.UnitOperations.LysisTechnology.HighPressureHomogenizer)
                .WithPressureMPa(100.0)
                .WithPasses(3);

            var cnt = fs.AddCentrifuge("CENT-1")
                .WithTechnology(DWSIM.UnitOperations.UnitOperations.CentrifugeType.DiskStack)
                .WithBowlSpeedRpm(8000);

            var chr = fs.AddChromatographyColumn("CHR-1")
                .WithMode(DWSIM.UnitOperations.UnitOperations.ChromatographyMode.BindElute)
                .WithChemistry(DWSIM.UnitOperations.UnitOperations.ChromatographyChemistry.Affinity)
                .WithColumnVolumeLiters(20);

            var uf = fs.AddCrossflowUF("UF-1")
                .WithOperatingMode(DWSIM.UnitOperations.UnitOperations.CrossflowUFMode.DiafiltrationConstantVolume)
                .WithDiavolumes(7)
                .WithMembraneArea(15.0.CubicMeters());

            var cry = fs.AddCrystallizer("CRY-1")
                .WithMode(DWSIM.UnitOperations.UnitOperations.CrystallizerMode.Cooling)
                .WithSolventCompound("Water")
                .WithOperatingTemperature(280.Kelvin())
                .WithSolubilityCoefficients(0.40, 0.006, 0.0);

            int n = fs.Inner.SimulationObjects.Count;
            Console.WriteLine("Bio UOs instantiated: " + n);
            if (n < 10) throw new Exception("Expected 10 bio UOs, got " + n);
        }
    }
}
