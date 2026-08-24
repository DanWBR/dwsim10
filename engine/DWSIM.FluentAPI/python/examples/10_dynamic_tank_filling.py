"""Dynamic tank filling - Python equivalent of DynamicsTankFillingTest.cs.

Builds the schedule from nothing, shuts the valve, integrates for five minutes, and
checks the level against a mass balance you can do on paper.
"""
import sys, clr, os

DWSIM_BIN = os.environ.get(
    "DWSIM_BIN",
    r"C:\Users\danie\source\repos\DanWBR\DWSIM_Private\DWSIM\bin\x64\Debug",
)
sys.path.append(DWSIM_BIN)
clr.AddReference("DWSIM.Automation.FluentAPI")
clr.AddReference("DWSIM.UnitOperations")

from System import TimeSpan
from DWSIM.Automation.FluentAPI import Flowsheet, PropertyPackages, Q
from DWSIM.Automation.FluentAPI.Dynamics import DynamicsDiagnostics, DiagnosticSeverity
from DWSIM.UnitOperations.UnitOperations import Valve

FEED_KG_S = 1.0
DURATION_S = 300.0

Flowsheet.RegisterAssemblyResolver()

fs = (Flowsheet.Create("PyDynamicTankFilling")
      .WithCompound("Water")
      .WithPropertyPackage(PropertyPackages.SteamTables))

# A feed is specified by flow; the tank only accumulates when its outlet is specified
# by pressure.
feed = (fs.AddMaterialStream("feed")
        .At(Q.Celsius(25.0), Q.Atm(1.0))
        .WithMassFlow(Q.KgPerSecond(FEED_KG_S))
        .AsFlowSpec())

tank_outlet = (fs.AddMaterialStream("tank-outlet")
               .At(Q.Celsius(25.0), Q.Atm(1.0))
               .AsPressureSpec())

product = (fs.AddMaterialStream("product")
           .At(Q.Celsius(25.0), Q.Atm(1.0))
           .AsPressureSpec())

(fs.AddTank("TK-01")
   .WithVolume(Q.CubicMeters(2.0))
   .WithHeight(Q.Meters(2.0))
   .InitializeFromInlet(False)
   .ResetContent()
   .ConnectFeed(feed, 0)
   .ConnectProduct(tank_outlet, 0))

# A Kv mode is what lets the valve compute its own flow from the pressure either side of
# it; the opening characteristic is what makes closing it actually stop the flow.
valve = (fs.AddValve("V-01")
         .WithCalcMode(Valve.CalculationMode.Kv_Liquid)
         .WithKv(10.0)
         .WithOpeningKvRelationship()
         .WithOpeningPercent(100.0)
         .ConnectFeed(tank_outlet, 0)
         .ConnectProduct(product, 0))

fs.AutoLayout()
fs.Solve()

# Shut the valve now that the steady state is solved: a closed valve has no steady state,
# and this is the disturbance the run is about.
valve.WithOpeningPercent(0.0).WithOpeningSetpoint(0.0)
tank_outlet.WithMassFlow(Q.KgPerSecond(0.0))

(fs.Dynamics.DefineIntegrator("Filling")
   .WithIntegrationStep(TimeSpan.FromSeconds(1))
   .WithDuration(TimeSpan.FromSeconds(DURATION_S))
   .Monitor("TK-01", "Liquid Level")
   .Monitor("feed", "PROP_MS_2", "kg/s"))

fs.Dynamics.DefineSchedule("Filling run").WithIntegrator("Filling").MakeCurrent()

blockers = [f for f in DynamicsDiagnostics.CheckReady(fs.Inner, "Filling run")
            if f.Severity == DiagnosticSeverity.Blocker]
if blockers:
    for f in blockers:
        print(f)
    raise SystemExit("the flowsheet is not ready to run dynamically")

result = fs.RunDynamics("Filling run").Execute()

if not result.Completed:
    for f in DynamicsDiagnostics.Diagnose(fs.Inner, result):
        print(f)
    raise SystemExit("integration failed")

print(result)

level = result.GetSeries("TK-01 Liquid Level")

# 1 kg/s for 300 s is 300 kg. At about 997 kg/m3 that is 0.3009 m3, and the tank's
# cross-section is volume over height = 1 m2, so the level is the volume.
density = feed.Object.Phases[0].Properties.density.GetValueOrDefault()
expected = FEED_KG_S * DURATION_S / density / (2.0 / 2.0)

print(f"expected {expected:.4f} m, got {level.Final:.4f} m "
      f"({abs(level.Final - expected) / expected * 100:.2f} % off)")

result.ToCsv("filling.csv")
print(f"wrote {level.Count} points to filling.csv")
