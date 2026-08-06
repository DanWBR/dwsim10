"""Steam-reforming Conversion Reactor — Python port of ConvReactorTest.cs."""
import sys, clr, os
from System.Collections.Generic import Dictionary
from System import String, Double

DWSIM_BIN = os.environ.get(
    "DWSIM_BIN",
    r"C:\Users\danie\source\repos\DanWBR\DWSIM_Private\DWSIM\bin\x64\Debug",
)
sys.path.append(DWSIM_BIN)
clr.AddReference("DWSIM.Automation.FluentAPI")

from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q


def stoich(d):
    out = Dictionary[String, Double]()
    for k, v in d.items():
        out[k] = float(v)
    return out


fs = (Flowsheet.Create("PyConvReactor")
      .WithCompounds("Carbon dioxide", "Carbon monoxide", "Water", "Hydrogen", "Methane")
      .WithPropertyPackage(PropertyPackages.PengRobinson))

r1 = fs.DefineConversionReaction(
    "R1", stoich({"Methane": -1, "Water": -2, "Carbon dioxide": 1, "Hydrogen": 4}),
    "Methane", "Vapor", "50")

r2 = fs.DefineConversionReaction(
    "R2", stoich({"Methane": -1, "Water": -1, "Carbon monoxide": 1, "Hydrogen": 3}),
    "Water", "Vapor", "50")

fs.ReactionSet("DefaultSet").Add(r1).Add(r2)

feed = (fs.AddMaterialStream("inlet")
        .WithTemperature(Q.Kelvin(1000.0))
        .WithMolarFlow(Q.MolPerSecond(5.0))
        .SetCompoundMolarFlow("Methane", 2.0)
        .SetCompoundMolarFlow("Water", 3.0)
        .SetCompoundMolarFlow("Carbon dioxide", 0.0)
        .SetCompoundMolarFlow("Carbon monoxide", 0.0)
        .SetCompoundMolarFlow("Hydrogen", 0.0))

gas_out = fs.AddMaterialStream("gas outlet")
liq_out = fs.AddMaterialStream("liquid outlet")
heat = fs.AddEnergyStream("heat")

reactor = (fs.AddConversionReactor("R-1")
           .Isothermal()
           .WithReactionSet("DefaultSet")
           .WithPressureDrop(Q.Pascal(0.0))
           .ConnectFeed(feed, 0)
           .ConnectProduct(gas_out, 0)
           .ConnectProduct(liq_out, 1)
           .ConnectEnergyFeed(heat, 1))

fs.AutoLayout()
fs.Solve()

print(f"Reactor heat duty = {reactor.HeatDutyKW:.4f} kW")
for kv in reactor.Object.ComponentConversions:
    if kv.Value > 0:
        print(f"  {kv.Key}: {kv.Value*100:.2f}%")
