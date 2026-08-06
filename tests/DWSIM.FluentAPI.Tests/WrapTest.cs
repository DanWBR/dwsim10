using System;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Verifies <see cref="Flowsheet.Wrap"/> on an existing <see cref="IFlowsheet"/>:
    /// build a flowsheet with one wrapper, hand the inner IFlowsheet to a fresh
    /// wrapper, add more objects + solve through the second wrapper. Mirrors the
    /// AI-assistant scenario where the host owns the IFlowsheet and the API is
    /// invoked iteratively to extend it.
    /// </summary>
    internal static class WrapTest
    {
        public static void Run()
        {
            // Pretend the host (DWSIM session / extender) gave us this IFlowsheet.
            var host = Flowsheet.Create("WrapHost")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            IFlowsheet hostFlowsheet = host.Inner;

            // ---- Wrap the existing IFlowsheet from a separate caller ----
            var wrapped = Flowsheet.Wrap(hostFlowsheet);

            // It really IS the same IFlowsheet - not a copy.
            if (!ReferenceEquals(wrapped.Inner, hostFlowsheet))
                throw new Exception("Wrap returned a different IFlowsheet instance");

            // Add streams + a Mixer through the wrapper.
            var inlet1 = wrapped.AddMaterialStream("inlet1")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(100.KgPerSecond());
            var inlet2 = wrapped.AddMaterialStream("inlet2")
                .At(348.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(50.KgPerSecond());
            var outlet = wrapped.AddMaterialStream("outlet");

            wrapped.AddMixer("MIX-1")
                .ConnectFeed(inlet1, 0)
                .ConnectFeed(inlet2, 1)
                .ConnectProduct(outlet, 0);

            wrapped.AutoLayout();
            wrapped.Solve();

            var T = outlet.TemperatureK;
            var m = outlet.MassFlowKgPerSecond;
            Console.WriteLine($"Wrap solve: outlet T = {T:F4} K, m = {m:F4} kg/s");
            if (Math.Abs(m - 150.0) > 1e-6)
                throw new Exception($"Mixer mass balance via wrapped flowsheet failed: {m}");

            // The host wrapper should see the same data - wrappers share state.
            int hostCount = host.Inner.SimulationObjects.Count;
            int wrappedCount = wrapped.Inner.SimulationObjects.Count;
            if (hostCount != wrappedCount)
                throw new Exception($"Host/wrapped count mismatch: {hostCount} vs {wrappedCount}");
            Console.WriteLine($"Both wrappers see {hostCount} objects on the shared IFlowsheet.");

            // Null guard.
            try { Flowsheet.Wrap(null); throw new Exception("Wrap(null) should throw"); }
            catch (ArgumentNullException) { Console.WriteLine("Wrap(null) → ArgumentNullException ✓"); }
        }
    }
}
