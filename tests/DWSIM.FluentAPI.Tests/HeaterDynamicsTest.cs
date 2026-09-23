using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Runs a heater with a fixed heat added in dynamic mode and checks that its outlet warms.
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="CoolerDynamicsTest"/>: the heater's duty is heat added, so a
    /// positive duty has to warm the holdup. The steady state fixes the duty that takes the feed
    /// from 20 to 40 °C, and with a holdup of about a second of feed the outlet has to sit at the
    /// steady-state temperature once the run is under way.
    /// </remarks>
    internal static class HeaterDynamicsTest
    {
        private const double FeedKgPerSecond = 0.3;
        private const double FeedTemperatureK = 293.15;
        private const double OutletTemperatureK = 313.15;
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

            var fs = Flowsheet.Create("FluentDynamicsHeaterHeatAdded" + (int)efficiencyPercent)
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

            var heater = fs.AddHeater("E-01")
                .WithOutletTemperature(OutletTemperatureK.Kelvin())
                .WithEfficiencyPercent(efficiencyPercent)
                .WithDynamicProperty("Volume", 0.05.CubicMeters())
                .WithDynamicProperty("Flow Conductance", 0.003)
                .WithDynamicProperty("Initialize using Inlet Stream", true)
                .ConnectFeed(feed, 0)
                .ConnectProduct(product, 0);

            fs.AutoLayout();
            fs.Solve();

            var dutyKW = heater.HeatDutyKW;
            heater.WithHeatAdded(dutyKW.Kilowatts());
            fs.Solve();

            var steadyOutletK = product.Object.GetTemperature();

            Console.WriteLine("heat added = " + dutyKW.ToString("F3") + " kW, steady-state outlet = " +
                steadyOutletK.ToString("F2") + " K");

            if (dutyKW <= 0.0)
                throw new Exception("The steady state found a heat added of " + dutyKW +
                    " kW for a feed that has to be warmed.");

            fs.Dynamics.DefineIntegrator("Heating")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(DurationSeconds.Seconds())
                .Monitor("product", "PROP_MS_0", "K", "outlet temperature")
                .Monitor("feed", "PROP_MS_0", "K", "feed temperature")
                .Monitor("product", "PROP_MS_2", "kg/s", "outlet mass flow");

            fs.Dynamics.DefineSchedule("Heating run")
                .WithIntegrator("Heating")
                .MakeCurrent();

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, "Heating run")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker)
                .ToList();

            if (blockers.Count > 0)
                throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            var result = fs.RunDynamics("Heating run").Execute();

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

            if (outletT.Final <= feedT.Final)
                throw new Exception("The heater outlet ended at " + outletT.Final.ToString("F2") +
                    " K with the feed at " + feedT.Final.ToString("F2") +
                    " K: a positive heat added is not warming the holdup.");

            new ResultTable("Dynamic heater on a fixed heat added at " + efficiencyPercent.ToString("F0") + " % efficiency")
                .Row("outlet temperature at the end of the run", steadyOutletK, outletT.Final, 1.5, "K")
                .Row("outlet temperature after one minute", steadyOutletK, outletT.ValueAt(60.0), 1.5, "K")
                .Row("outlet temperature at half the run", steadyOutletK, outletT.ValueAt(DurationSeconds / 2.0), 1.5, "K")
                .Row("feed held at its temperature", FeedTemperatureK, feedT.Final, 0.01, "K")
                .Row("outlet flow held at the feed flow", FeedKgPerSecond, outletFlow.Final, 0.001, "kg/s")
                .PrintAndThrowIfFailed();
        }
    }
}
