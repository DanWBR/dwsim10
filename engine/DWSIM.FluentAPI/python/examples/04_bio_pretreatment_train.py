"""
Lignocellulosic biomass → fermentable sugars → ethanol broth → centrifuge cake.

Builds a small biorefinery topology with the typed bioprocess builders:
    feed → Pretreatment → BioReactor → Centrifuge → Crystallizer

Bioprocess UOs are FREE (no patron key required), but they need a curated
compound database for the solver to converge — this example focuses on
exercising the typed fluent API and printing the configured parameters.
Drop in your own compound DB + solve cycle when ready.
"""
import os
import sys
import clr

DWSIM_BIN = os.environ.get(
    "DWSIM_BIN",
    r"C:\Users\danie\source\repos\DanWBR\DWSIM_Private\DWSIM\bin\x64\Debug",
)
sys.path.append(DWSIM_BIN)
clr.AddReference("DWSIM.Automation.FluentAPI")

from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q  # noqa: E402
from DWSIM.UnitOperations.Reactors import (  # noqa: E402
    PretreatmentType, BioReactorMode, BioReactorThermalMode, BioKineticModel,
)
from DWSIM.UnitOperations.UnitOperations import (  # noqa: E402
    CentrifugeType, CrystallizerMode,
)


def main():
    fs = (Flowsheet.Create("BioRefineryDemo")
          .WithCompounds("Water", "Ethanol", "Glucose", "Acetic acid")
          .WithPropertyPackage(PropertyPackages.NRTL))

    fs.AddMaterialStream("biomass-feed") \
        .At(Q.Kelvin(298.15), Q.Pascal(101325.0)) \
        .WithMassFlow(Q.KgPerSecond(10.0))

    pre = (fs.AddPretreatmentReactor("PRE-1")
           .WithTechnology(PretreatmentType.DiluteAcid)
           .WithSeverityLogR0(3.6)
           .WithResidenceTime(Q.Minutes(15.0))
           .WithSolidsLoading(0.18)
           .WithCelluloseConversion(0.10)
           .WithHemicelluloseConversion(0.92)
           .WithLigninSolubilization(0.18)
           .WithGlucoseToHMF(0.025)
           .WithXyloseToFurfural(0.06))

    fermenter = (fs.AddBioReactor("BR-1")
                 .WithVolume(Q.CubicMeters(50.0))
                 .WithBatchDuration(Q.Hours(36.0))
                 .WithKineticModel(BioKineticModel.Monod)
                 .WithOperatingMode(BioReactorMode.Batch)
                 .WithThermalMode(BioReactorThermalMode.Isothermal)
                 .WithAerobic(False)
                 .WithMaxSpecificGrowthPerHour(0.45)
                 .WithMonodKsGPerL(0.5)
                 .WithBiomassYield(0.10))

    cent = (fs.AddCentrifuge("CENT-1")
            .WithTechnology(CentrifugeType.DiskStack)
            .WithBowlSpeedRpm(8500.0)
            .WithSigmaFactorM2(1500.0)
            .WithDefaultRecoveryToHeavy(0.05)
            .WithRecoveryToHeavy("Glucose", 0.02))

    cry = (fs.AddCrystallizer("CRY-1")
           .WithMode(CrystallizerMode.Cooling)
           .WithSoluteCompound("Glucose")
           .WithSolventCompound("Water")
           .WithOperatingTemperature(Q.Kelvin(278.15))
           .WithSolubilityCoefficients(0.40, 0.005, 0.0)
           .WithEvaporationFraction(0.0))

    fs.AutoLayout()

    print("Bio refinery topology placed:")
    print(f"  Objects: {fs.Inner.SimulationObjects.Count}")
    print(f"  Pretreatment: {pre.Object.Technology}  log(R0)={pre.Object.SeverityLogR0}")
    print(f"  Fermenter:    {fermenter.Object.OperatingMode}  V={fermenter.Object.Volume} m^3")
    print(f"  Centrifuge:   {cent.Object.Technology}  ω={cent.Object.BowlSpeed_rpm} rpm")
    print(f"  Crystallizer: {cry.Object.Mode}  T={cry.Object.OperatingT_K-273.15:.1f} °C")


if __name__ == "__main__":
    main()
