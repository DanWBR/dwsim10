"""Phase envelope, binary phase diagram, and critical points for a binary mixture."""
import sys, clr, os

DWSIM_BIN = os.environ.get(
    "DWSIM_BIN",
    r"C:\Users\danie\source\repos\DanWBR\DWSIM_Private\DWSIM\bin\x64\Debug",
)
sys.path.append(DWSIM_BIN)
clr.AddReference("DWSIM.Automation.FluentAPI")

from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages

fs = (Flowsheet.Create("PhaseDiagrams")
      .WithCompound("Methane")
      .WithCompound("Ethane")
      .WithPropertyPackage(PropertyPackages.PengRobinson))

stream = (fs.AddMaterialStream("feed")
          .WithComposition(lambda c: c.Mole("Methane", 0.7).Mole("Ethane", 0.3)))

# --- Critical point(s) of the mixture
print("=== Critical points ===")
cps = stream.CalculateCriticalPoints()
for i, cp in enumerate(cps):
    print(f"CP {i+1}: T = {cp.TemperatureK:.2f} K, "
          f"P = {cp.PressurePa/1e5:.2f} bar, "
          f"V = {cp.MolarVolumeM3PerMol:.6f} m3/mol")

# --- Phase envelope (full)
print("\n=== Phase envelope ===")
env = stream.CalculatePhaseEnvelope()
print(f"Bubble curve: {len(env.BubbleTemperaturesK)} points")
print(f"Dew curve:    {len(env.DewTemperaturesK)} points")
print(f"Critical points on envelope: {len(env.CriticalPoints)}")
if len(env.BubbleTemperaturesK) > 0:
    print(f"  Bubble T range: {min(env.BubbleTemperaturesK):.2f} - "
          f"{max(env.BubbleTemperaturesK):.2f} K")
    print(f"  Bubble P range: {min(env.BubblePressuresPa)/1e5:.2f} - "
          f"{max(env.BubblePressuresPa)/1e5:.2f} bar")

# --- Binary T-x-y diagram at 10 bar
print("\n=== Binary T-x-y at 10 bar ===")
txy = stream.CalculateBinaryDiagram_Txy(pressurePa=10e5, steps=20)
print(f"Diagram type: {txy.DiagramType}")
print(f"Composition points: {len(txy.X)}")
for i in range(0, len(txy.X), max(1, len(txy.X) // 5)):
    print(f"  x = {txy.X[i]:.3f}  Tbubble = {txy.Y1[i]:.2f} K  "
          f"Tdew = {txy.Y2[i]:.2f} K")

# --- Binary P-x-y diagram at 200 K
print("\n=== Binary P-x-y at 200 K ===")
pxy = stream.CalculateBinaryDiagram_Pxy(temperatureK=200.0, steps=20)
print(f"Composition points: {len(pxy.X)}")
for i in range(0, len(pxy.X), max(1, len(pxy.X) // 5)):
    print(f"  x = {pxy.X[i]:.3f}  Pbubble = {pxy.Y1[i]/1e5:.2f} bar  "
          f"Pdew = {pxy.Y2[i]/1e5:.2f} bar")
