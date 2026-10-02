using System;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Verifies the typed bioprocess builders compile, instantiate through the
    /// IExternalUnitOperation path, and accept fluent setter calls; and that the typed builders
    /// of the logical blocks, controllers, separators and the shortcut column do the same.
    /// </summary>
    internal static class TypedBuildersTest
    {
        public static void Run()
        {
            ProbeBioprocess();
            ProbeLogicalBlocksAndSeparators();
        }

        private static void ProbeLogicalBlocksAndSeparators()
        {
            var fs = Flowsheet.Create("LogicalBuildersProbe")
                .WithCompounds("Water", "Benzoic acid")
                .WithPropertyPackage(PropertyPackages.Raoult, pp => pp.WithForcedSolids("Benzoic acid"));

            fs.AddMaterialStream("S-1").At(300.0.Kelvin(), 1.0.Bar()).WithVaporFraction(0.0);
            fs.AddMaterialStream("S-2");
            fs.AddHeater("H-1").WithOutletTemperature(320.0.Kelvin());
            fs.AddHeater("H-2").WithOutletTemperature(320.0.Kelvin());
            fs.AddTank("T-1").WithVolume(1.0.CubicMeters()).WithPressureDrop(0.1.Bar());
            fs.AddValve("V-1");

            fs.AddAdjust("ADJ-1").Manipulates("H-1", "PROP_HT_3").Controls("S-2", "PROP_MS_0")
                .WithTargetValue(330.0.Kelvin()).WithTolerance(0.01);
            fs.AddSpec("SPEC-1").WithSource("S-1", "PROP_MS_0").WithTarget("H-2", "PROP_HT_2")
                .WithExpression("X + 5");
            fs.AddInformationCarrier("IC-1").WithSource("H-1", "PROP_HT_2").WithTarget("H-2", "PROP_HT_2");
            fs.AddPythonController("PC-1").Controls("T-1", "Liquid Level", "m").Manipulates("V-1", "PROP_VA_5", "")
                .WithScript("SP = 1.0\nMV = 50.0");
            fs.AddMPCController("MPC-1").Controls("T-1", "Liquid Level", "m", 0.9, 1.1)
                .Manipulates("V-1", "PROP_VA_5", "", 0.0, 100.0).WithIntegratingModel(0, 0, -0.0004)
                .WithSampleTime(5.0.Seconds()).WithHorizons(30, 5);
            fs.AddComponentSeparator("CS-1").WithSpecifiedOutlet(1).WithMassPercent("Water", 10.0);
            fs.AddSolidsSeparator("SS-1").WithSolidsSeparationEfficiency(99.0).WithLiquidSeparationEfficiency(90.0);
            fs.AddFilter("F-1").WithFilterArea(5.0).WithCycleTime(60.0.Seconds());
            fs.AddShortcutColumn("SC-1").WithKeys("Water", "Benzoic acid").WithRefluxRatio(2.0)
                .WithPressures(1.0.Atm(), 1.0.Atm());

            int n = fs.Inner.SimulationObjects.Count;
            Console.WriteLine("Logical blocks, controllers and separators instantiated: " + n);
            if (n < 15) throw new Exception("Expected 15 objects, got " + n);
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
