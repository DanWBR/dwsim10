using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.UnitOperations.Reactors;
using BioOps = DWSIM.UnitOperations.UnitOperations;
using PretreatmentType = DWSIM.UnitOperations.Reactors.PretreatmentType;
using BioReactorMode = DWSIM.UnitOperations.Reactors.BioReactorMode;
using BioReactorThermalMode = DWSIM.UnitOperations.Reactors.BioReactorThermalMode;
using BioKineticModel = DWSIM.UnitOperations.Reactors.BioKineticModel;
using CentrifugeType = DWSIM.UnitOperations.UnitOperations.CentrifugeType;
using CrystallizerMode = DWSIM.UnitOperations.UnitOperations.CrystallizerMode;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Builds a small lignocellulosic-bioethanol-style train using the typed
    /// bioprocess builders: Pretreatment → BioReactor → Centrifuge → Crystallizer.
    /// Asserts each stage's typed config landed on the underlying object - does
    /// not invoke the solver (bio UOs need a curated compound DB to converge).
    /// </summary>
    internal static class BioTrainTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("BioTrain")
                .WithCompounds("Water", "Ethanol", "Glucose", "Acetic acid")
                .WithPropertyPackage(PropertyPackages.NRTL);

            var feed = fs.AddMaterialStream("biomass-feed")
                .WithTemperature(298.15.Kelvin())
                .WithPressure(101325.0.Pascal())
                .WithMassFlow(10.0.KgPerSecond());

            var pre = fs.AddPretreatmentReactor("PRE-1")
                .WithTechnology(PretreatmentType.DiluteAcid)
                .WithSeverityLogR0(3.6)
                .WithResidenceTime(15.0.Minutes())
                .WithSolidsLoading(0.18)
                .WithCelluloseConversion(0.10)
                .WithHemicelluloseConversion(0.92)
                .WithLigninSolubilization(0.18)
                .WithGlucoseToHMF(0.025)
                .WithXyloseToFurfural(0.06);

            var fermenter = fs.AddBioReactor("BR-1")
                .WithVolume(50.0.CubicMeters())
                .WithBatchDuration(36.0.Hours())
                .WithKineticModel(BioKineticModel.Monod)
                .WithOperatingMode(BioReactorMode.Batch)
                .WithThermalMode(BioReactorThermalMode.Isothermal)
                .WithAerobic(false)
                .WithMaxSpecificGrowthPerHour(0.45)
                .WithMonodKsGPerL(0.5)
                .WithBiomassYield(0.10);

            var sep = fs.AddCentrifuge("CENT-1")
                .WithTechnology(CentrifugeType.DiskStack)
                .WithBowlSpeedRpm(8500.0)
                .WithSigmaFactorM2(1500.0)
                .WithDefaultRecoveryToHeavy(0.05)
                .WithRecoveryToHeavy("Glucose", 0.02);

            var cry = fs.AddCrystallizer("CRY-1")
                .WithMode(CrystallizerMode.Cooling)
                .WithSoluteCompound("Glucose")
                .WithSolventCompound("Water")
                .WithOperatingTemperature(278.15.Kelvin())
                .WithSolubilityCoefficients(0.40, 0.005, 0.0)
                .WithEvaporationFraction(0.0);

            // ----- Assert configurations actually landed on the underlying objects.
            if (pre.Object.Technology != PretreatmentType.DiluteAcid)
                throw new Exception("Pretreatment.Technology not applied");
            if (Math.Abs(pre.Object.HemicelluloseConversion - 0.92) > 1e-9)
                throw new Exception("Pretreatment.HemicelluloseConversion not applied");
            if (fermenter.Object.OperatingMode != BioReactorMode.Batch)
                throw new Exception("BioReactor.OperatingMode not applied");
            if (Math.Abs(fermenter.Object.Volume - 50.0) > 1e-9)
                throw new Exception("BioReactor.Volume not applied");
            if (sep.Object.Technology != CentrifugeType.DiskStack)
                throw new Exception("Centrifuge.Technology not applied");
            if (Math.Abs(sep.Object.BowlSpeed_rpm - 8500.0) > 1e-9)
                throw new Exception("Centrifuge.BowlSpeed_rpm not applied");
            if (cry.Object.Mode != CrystallizerMode.Cooling)
                throw new Exception("Crystallizer.Mode not applied");
            if (cry.Object.SoluteCompound != "Glucose")
                throw new Exception("Crystallizer.SoluteCompound not applied");

            int n = fs.Inner.SimulationObjects.Count;
            Console.WriteLine("Bio train objects placed: " + n + " (1 stream + 4 UOs expected)");
            if (n != 5) throw new Exception("Expected 5 placed objects, got " + n);

            // Pretreatment severity sanity: log R0 = 3.6 → R0 ≈ 4000 (Overend & Chornet).
            double r0 = Math.Pow(10.0, pre.Object.SeverityLogR0);
            Console.WriteLine($"Pretreatment severity factor R0 ≈ {r0:F0}");
        }
    }
}
