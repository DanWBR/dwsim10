"""Steam-tables 2-stream Mixer — Python equivalent of MixerTest.cs."""
import sys, clr, os

DWSIM_BIN = os.environ.get(
    "DWSIM_BIN",
    r"C:\Users\danie\source\repos\DanWBR\DWSIM_Private\DWSIM\bin\x64\Debug",
)
sys.path.append(DWSIM_BIN)
clr.AddReference("DWSIM.Automation.FluentAPI")

from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q

fs = (Flowsheet.Create("PyMixer")
      .WithCompound("Water")
      .WithPropertyPackage(PropertyPackages.SteamTables))

inlet1 = (fs.AddMaterialStream("inlet1")
          .At(Q.Kelvin(300.0), Q.Pascal(101325.0))
          .WithMassFlow(Q.KgPerSecond(100.0)))

inlet2 = (fs.AddMaterialStream("inlet2")
          .At(Q.Kelvin(348.0), Q.Pascal(101325.0))
          .WithMassFlow(Q.KgPerSecond(50.0)))

outlet = fs.AddMaterialStream("outlet")

(fs.AddMixer("MIX-1")
   .ConnectFeed(inlet1, 0)
   .ConnectFeed(inlet2, 1)
   .ConnectProduct(outlet, 0))

fs.AutoLayout()
fs.Solve()

print(f"Outlet T  = {outlet.TemperatureK:.4f} K")
print(f"Mass flow = {outlet.MassFlowKgPerSecond:.4f} kg/s")
