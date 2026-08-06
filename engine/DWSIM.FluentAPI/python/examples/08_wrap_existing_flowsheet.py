"""
Use the Fluent API on an IFlowsheet that already exists in memory — the
target scenario for an AI assistant: the host (DWSIM editor session, an
extender plugin, or a long-lived automation host) owns the IFlowsheet and
hands it to the assistant for incremental edits.

Pattern:

    fs = Flowsheet.Wrap(host_flowsheet)   # share the live document
    fs.AddHeater("H-NEW")
      .WithOutletTemperature(Q.Kelvin(350.0))
      .WithPressureDrop(Q.Bar(0.5))
    fs.Solve()

This file simulates the host with a Flowsheet.Create(), then re-wraps its
Inner IFlowsheet to prove a second caller can keep extending it.
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


def host_creates_flowsheet():
    """Stand-in for the DWSIM editing session — owns the IFlowsheet."""
    host = (Flowsheet.Create("AssistantSession")
            .WithCompound("Water")
            .WithPropertyPackage(PropertyPackages.SteamTables))
    return host.Inner   # the bare IFlowsheet, just like the host would expose


def assistant_extends(flowsheet_in_memory):
    """The AI assistant receives the IFlowsheet and adds units to it."""
    fs = Flowsheet.Wrap(flowsheet_in_memory)

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
    return outlet


def main():
    host_fs = host_creates_flowsheet()
    print(f"Host gave us a {type(host_fs).__name__} with "
          f"{host_fs.SimulationObjects.Count} objects initially")

    outlet = assistant_extends(host_fs)
    print(f"After assistant edits: {host_fs.SimulationObjects.Count} objects")
    print(f"Outlet T = {outlet.TemperatureK:.4f} K")
    print(f"Outlet m = {outlet.MassFlowKgPerSecond:.4f} kg/s")

    # Re-wrap a third time and read back — wrappers are stateless views.
    later = Flowsheet.Wrap(host_fs)
    print(f"Third wrapper sees {later.Inner.SimulationObjects.Count} objects "
          f"on the same IFlowsheet (no copy).")


if __name__ == "__main__":
    main()
