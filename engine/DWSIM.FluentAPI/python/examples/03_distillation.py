"""Water/ethanol Distillation Column — Python port of DistillationTest.cs."""
import sys, clr, os

DWSIM_BIN = os.environ.get(
    "DWSIM_BIN",
    r"C:\Users\danie\source\repos\DanWBR\DWSIM_Private\DWSIM\bin\x64\Debug",
)
sys.path.append(DWSIM_BIN)
clr.AddReference("DWSIM.Automation.FluentAPI")

from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

fs = (Flowsheet.Create("PyDist")
      .WithCompounds("Water", "Ethanol")
      .WithPropertyPackage(PropertyPackages.NRTL))

feed = (fs.AddMaterialStream("feed")
        .WithTemperature(Q.Kelvin(300.0))
        .WithMolarFlow(Q.MolPerSecond(100.0))
        .SetCompoundMolarFlow("Water", 50.0)
        .SetCompoundMolarFlow("Ethanol", 50.0))

distillate = fs.AddMaterialStream("distillate")
bottoms = fs.AddMaterialStream("bottoms")
cond_duty = fs.AddEnergyStream("cond duty")
reb_duty = fs.AddEnergyStream("reb duty")

(fs.AddDistillationColumn("T-101")
   .WithNumberOfStages(20)
   .WithFeed(feed, 10)
   .WithDistillate(distillate)
   .WithBottoms(bottoms)
   .WithCondenserDuty(cond_duty)
   .WithReboilerDuty(reb_duty)
   .WithCondenserSpec("Reflux Ratio", 2.0, "")
   .WithReboilerSpec("Product Molar Flow Rate", 75.0, "mol/s")
   .WithTopPressure(Q.Pascal(101325.0))
   .WithColumnPressureDrop(Q.Pascal(0.0)))

fs.AutoLayout()
fs.Solve()

print(f"Condenser duty = {cond_duty.EnergyFlowKW:.4f} kW")
print(f"Reboiler  duty = {reb_duty.EnergyFlowKW:.4f} kW")
print(f"Distillate flow = {distillate.MolarFlowMolPerSecond:.4f} mol/s")
print(f"Bottoms flow    = {bottoms.MolarFlowMolPerSecond:.4f} mol/s")
print(f"Distillate: H2O={distillate.OverallMoleFraction('Water'):.4f} EtOH={distillate.OverallMoleFraction('Ethanol'):.4f}")
print(f"Bottoms   : H2O={bottoms.OverallMoleFraction('Water'):.4f} EtOH={bottoms.OverallMoleFraction('Ethanol'):.4f}")
