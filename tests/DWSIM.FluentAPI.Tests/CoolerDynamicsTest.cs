using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Runs a cooler with a fixed heat removed in dynamic mode and checks that its outlet cools.
    /// </summary>
    /// <remarks>
    /// The cooler's dynamic model shares its energy balance with the heater's, where the duty is
    /// heat added to the holdup. The cooler's duty is the heat removed, positive when it cools, and
    /// it used to be fed into that balance with the heater's sign: a cooler with a positive duty
    /// warmed its outlet, and a temperature controller on it ran away.
    ///
    /// The steady state fixes the duty that takes the feed from 40 to 20 °C. The holdup is small
    /// against the flow (about a second of feed), so once the run is under way the outlet has to
    /// sit at the steady-state temperature, below the feed.
    /// </remarks>
    internal static class CoolerDynamicsTest
    {
        private const double FeedKgPerSecond = 0.3;
        private const double FeedTemperatureK = 313.15;
        private const double OutletTemperatureK = 293.15;
        private const double DurationSeconds = 300.0;

        public static void Run()
        {
            // At the default efficiency the run checks the sign of the duty. At 80 % it checks that the
            // dynamic model scales the duty by the efficiency as the steady state does: left out, the
            // outlet would overshoot the steady-state temperature by a quarter of the temperature change.
            RunAt(100.0);
            RunAt(80.0);
        }

        private static void RunAt(double efficiencyPercent)
        {
            Console.WriteLine("efficiency = " + efficiencyPercent.ToString("F0") + " %");

            var fs = Flowsheet.Create("FluentDynamicsCoolerHeatRemoved" + (int)efficiencyPercent)
                .WithCompound("Methane")
                .WithCompound("Ethane")
                .WithCompound("Propane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("feed")
                .At(FeedTemperatureK.Kelvin(), 10.Bar())
                .WithMassFlow(FeedKgPerSecond.KgPerSecond())
                .WithComposition(c => c.Mole("Methane", 0.85).Mole("Ethane", 0.10).Mole("Propane", 0.05))
                .AsFlowSpec();

            var product = fs.AddMaterialStream("product")
                .At(OutletTemperatureK.Kelvin(), 10.Bar())
                .AsPressureSpec();

            // Solve once on an outlet temperature to find the duty, then run on that duty: the
            // dynamic model only accepts the heat removed and energy stream modes.
            var cooler = fs.AddCooler("E-01")
                .WithOutletTemperature(OutletTemperatureK.Kelvin())
                .WithEfficiencyPercent(efficiencyPercent)
                .WithDynamicProperty("Volume", 0.05.CubicMeters())
                .WithDynamicProperty("Flow Conductance", 0.003)
                .WithDynamicProperty("Initialize using Inlet Stream", true)
                .ConnectFeed(feed, 0)
                .ConnectProduct(product, 0);

            fs.AutoLayout();
            fs.Solve();

            var dutyKW = cooler.HeatRemovedKW;
            cooler.WithHeatRemoved(dutyKW.Kilowatts());
            fs.Solve();

            var steadyOutletK = product.Object.GetTemperature();

            Console.WriteLine("heat removed = " + dutyKW.ToString("F3") + " kW, steady-state outlet = " +
                steadyOutletK.ToString("F2") + " K");

            if (dutyKW <= 0.0)
                throw new Exception("The steady state found a heat removed of " + dutyKW +
                    " kW for a feed that has to be cooled.");

            fs.Dynamics.DefineIntegrator("Cooling")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(DurationSeconds.Seconds())
                .Monitor("product", "PROP_MS_0", "K", "outlet temperature")
                .Monitor("feed", "PROP_MS_0", "K", "feed temperature")
                .Monitor("product", "PROP_MS_2", "kg/s", "outlet mass flow");

            fs.Dynamics.DefineSchedule("Cooling run")
                .WithIntegrator("Cooling")
                .MakeCurrent();

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, "Cooling run")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker)
                .ToList();

            if (blockers.Count > 0)
                throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            var result = fs.RunDynamics("Cooling run").Execute();

            if (!result.Completed)
                throw new Exception("Integration did not complete: " +
                    (result.Error == null ? "aborted" : result.Error.Message));

            Console.WriteLine(result);

            var outletT = result.GetSeries("outlet temperature");
            var feedT = result.GetSeries("feed temperature");
            var outletFlow = result.GetSeries("outlet mass flow");

            foreach (var t in new[] { 0.0, 5.0, 10.0, 30.0, 60.0, 120.0, DurationSeconds })
                Console.WriteLine("t = " + t.ToString("F0").PadLeft(4) + " s: outlet " +
                    outletT.ValueAt(t).ToString("F2") + " K, feed " + feedT.ValueAt(t).ToString("F2") +
                    " K, outlet flow " + outletFlow.ValueAt(t).ToString("F4") + " kg/s");

            if (outletT.Final >= feedT.Final)
                throw new Exception("The cooler outlet ended at " + outletT.Final.ToString("F2") +
                    " K with the feed at " + feedT.Final.ToString("F2") +
                    " K: a positive heat removed is heating the holdup.");

            // The holdup turns over in about a second, so the outlet reaches the steady-state
            // temperature long before the run ends. The tolerance covers the flash of the same
            // enthalpy at the holdup pressure, which the constant-volume cooling lowers a little.
            new ResultTable("Dynamic cooler on a fixed heat removed at " + efficiencyPercent.ToString("F0") + " % efficiency")
                .Row("outlet temperature at the end of the run", steadyOutletK, outletT.Final, 1.5, "K")
                .Row("outlet temperature after one minute", steadyOutletK, outletT.ValueAt(60.0), 1.5, "K")
                .Row("outlet temperature at half the run", steadyOutletK, outletT.ValueAt(DurationSeconds / 2.0), 1.5, "K")
                .Row("feed held at its temperature", FeedTemperatureK, feedT.Final, 0.01, "K")
                .Row("outlet flow held at the feed flow", FeedKgPerSecond, outletFlow.Final, 0.001, "kg/s")
                .PrintAndThrowIfFailed();
        }
    }
}
