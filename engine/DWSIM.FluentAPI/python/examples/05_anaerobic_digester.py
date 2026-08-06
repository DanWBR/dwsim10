"""
Anaerobic digester for biogas production from a wastewater feed.

The Anaerobic Digester FREE bioprocess UO supports BlackBox / ADM1-Lite /
full-ADM1 kinetic modes. This example wires it together with a BiogasUpgrader
(membrane separation) to model a small biogas-to-biomethane plant.

Sulfur: standard ADM1 (Batstone et al. 2002) excludes sulfate reduction, so the
digester carries a stoichiometric sulfur balance on top of it. Sulfate-S and
organic-S are declared separately because they behave differently: sulfate has no
COD of its own, so reducing it to sulfide draws 64 kg COD/kmol S out of the pool
that would have made methane, while organic sulfur arrives already reduced inside
the substrate and costs no methane at all. The sulfide is then split between the
biogas (as H2S) and the effluent by Henry's law and the H2S/HS- equilibrium.

Both ends have to be set for the H2S to go anywhere: H2SCompound on the digester,
so the H2S is written into the biogas stream, and H2SCompound on the upgrader, so
its H2S removal actually strips it. Leave either unassigned and the sulfur is
still computed and reported, but silently stays out of the streams.
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
    DigesterModel, BioReactorThermalMode,
)
from DWSIM.UnitOperations.UnitOperations import BiogasUpgraderTech  # noqa: E402


def main():
    fs = (Flowsheet.Create("BiogasPlant")
          .WithCompounds("Water", "Methane", "Carbon dioxide", "Hydrogen sulfide", "Acetic acid")
          .WithPropertyPackage(PropertyPackages.PengRobinson))

    fs.AddMaterialStream("ww-feed") \
        .At(Q.Kelvin(308.15), Q.Pascal(101325.0)) \
        .WithMassFlow(Q.KgPerSecond(2.5))

    digester = (fs.AddAnaerobicDigester("AD-1")
                .WithVolume(Q.CubicMeters(150.0))
                .WithHydraulicRetentionTime(Q.Days(20.0))
                .WithCODRemoval(0.85)
                .WithBiomassYieldGVssPerGCOD(0.08)
                .WithThermalMode(BioReactorThermalMode.Isothermal)
                .WithModel(DigesterModel.ADM1Lite)
                .WithADM1HydrolysisRatePerDay(10.0)
                .WithADM1SugarUptakePerDay(30.0)
                .WithADM1AcetateUptakePerDay(8.0)
                # 400 mg S/L of sulfate is a moderate agricultural feed; pig slurry runs higher.
                # Organic S at -1 means "read it from the substrate compound's formula".
                .WithInfluentSulfateSulfurMgPerL(400.0)
                .WithSubstrateOrganicSulfurGPerKg(-1.0)
                .Configure(lambda o: setattr(o, "H2SCompound", "Hydrogen sulfide")))

    upgrader = (fs.AddBiogasUpgrader("UPG-1")
                .WithTechnology(BiogasUpgraderTech.MembraneSeparation)
                .WithCO2Removal(0.96)
                .WithH2SCompound("Hydrogen sulfide")
                .WithH2SRemoval(0.99)
                .WithH2ORemoval(0.95)
                .WithCH4LossFraction(0.02)
                .WithTargetCH4Purity(0.97))

    fs.AutoLayout()

    print(f"Digester:  V={digester.Object.Volume:.0f} m^3  HRT={digester.Object.HRT_s/86400:.0f} d  COD removal={digester.Object.CODRemovalEfficiency:.0%}")
    print(f"           model={digester.Object.Model}  k_hyd={digester.Object.ADM1_k_hyd_d}/d  k_ac={digester.Object.ADM1_km_ac_d}/d")
    print(f"           sulfate-S={digester.Object.InfluentSulfateS_mgL:.0f} mg/L  organic-S={digester.Object.SubstrateOrganicS_gPerKg:.0f} g/kg (-1 = from formula)")
    print(f"Upgrader:  {upgrader.Object.Technology}  → CH4 target {upgrader.Object.TargetCH4Purity:.0%}  CH4 loss {upgrader.Object.CH4LossFraction:.1%}")
    print(f"           H2S removal {upgrader.Object.H2SRemovalEfficiency:.0%} on '{upgrader.Object.H2SCompound}'")
    print(f"Total flowsheet objects: {fs.Inner.SimulationObjects.Count}")


if __name__ == "__main__":
    main()
