using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Runs a compressor as a pressure-flow element in dynamic mode and checks that its discharge
    /// leaves hotter than the suction by the adiabatic rise.
    /// </summary>
    /// <remarks>
    /// The pass-through model writes the inlet temperature and the outlet enthalpy into the
    /// discharge stream and leaves the stream on the specification it had. On temperature and
    /// pressure, the default, the stream then flashes at the inlet temperature and recomputes its
    /// enthalpy, so the compression heat never reaches whatever sits downstream: a cooler holding
    /// a fixed heat removed saw gas arriving at the suction temperature. The model now puts the
    /// discharge on pressure and enthalpy, as the steady state does.
    ///
    /// The line is a fuel gas fed by flow at 25 °C and 2 bar, the compressor, and an outlet
    /// specified by pressure. The steady state fixes the rise to 2.7 bar at 62 % efficiency; the
    /// dynamic model raises the pressure by the square of the flow over its conductance, which is
    /// set here to give the same rise. Its head is the pressure rise over the suction density,
    /// a little above the isentropic head for a gas, so the dynamic outlet runs a few kelvin
    /// hotter than the steady state.
    /// </remarks>
    internal static class CompressorDischargeTemperatureTest
    {
        private const double FeedKgPerSecond = 0.6;
        private const double FeedTemperatureK = 298.15;
        private const double SuctionBar = 2.0;
        private const double DischargeBar = 2.7;
        private const double EfficiencyPercent = 62.0;
        private const double DurationSeconds = 30.0;

        public static void Run()
        {
            var fs = Flowsheet.Create("FluentCompressorDischargeTemperature")
                .WithCompound("Methane")
                .WithCompound("Ethane")
                .WithCompound("Nitrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("feed")
                .At(FeedTemperatureK.Kelvin(), SuctionBar.Bar())
                .WithMassFlow(FeedKgPerSecond.KgPerSecond())
                .WithComposition(c => c.Mass("Methane", 0.90).Mass("Ethane", 0.07).Mass("Nitrogen", 0.03))
                .AsFlowSpec();

            var product = fs.AddMaterialStream("product").AsPressureSpec();

            var compressor = fs.AddCompressor("K-01")
                .WithOutletPressure(DischargeBar.Bar())
                .WithAdiabaticEfficiencyPercent(EfficiencyPercent)
                .ConnectFeed(feed, 0)
                .ConnectProduct(product, 0);

            fs.AutoLayout();
            fs.Solve();

            var steadyOutletK = product.Object.GetTemperature();
            var steadyRiseK = steadyOutletK - FeedTemperatureK;

            Console.WriteLine("steady state: discharge at " + (product.PressurePa / 1e5).ToString("F2") +
                " bar and " + steadyOutletK.ToString("F2") + " K, rise " + steadyRiseK.ToString("F2") + " K");

            if (steadyRiseK < 20.0)
                throw new Exception("The steady state found a rise of " + steadyRiseK.ToString("F2") +
                    " K for a compression that has to heat the gas by about 33 K.");

            // The pass-through model raises the pressure by (W / conductance)^2.
            compressor.WithDynamicProperty("Flow Conductance",
                FeedKgPerSecond / Math.Sqrt((DischargeBar - SuctionBar) * 1e5));

            fs.Dynamics.DefineIntegrator("Discharge")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(DurationSeconds.Seconds())
                .Monitor("product", "PROP_MS_0", "K", "outlet temperature")
                .Monitor("product", "PROP_MS_1", "Pa", "outlet pressure")
                .Monitor("feed", "PROP_MS_0", "K", "feed temperature");

            fs.Dynamics.DefineSchedule("Discharge run")
                .WithIntegrator("Discharge")
                .MakeCurrent();

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, "Discharge run")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker)
                .ToList();

            if (blockers.Count > 0)
                throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            var result = fs.RunDynamics("Discharge run").Execute();

            if (!result.Completed)
                throw new Exception("Integration did not complete: " +
                    (result.Error == null ? "aborted" : result.Error.Message));

            Console.WriteLine(result);

            var outletT = result.GetSeries("outlet temperature");
            var outletP = result.GetSeries("outlet pressure");
            var feedT = result.GetSeries("feed temperature");

            foreach (var t in new[] { 0.0, 1.0, 2.0, 5.0, 10.0, DurationSeconds })
                Console.WriteLine("t = " + t.ToString("F0").PadLeft(3) + " s: outlet " +
                    outletT.ValueAt(t).ToString("F2") + " K at " + (outletP.ValueAt(t) / 1e5).ToString("F3") +
                    " bar, feed " + feedT.ValueAt(t).ToString("F2") + " K");

            var dynamicRiseK = outletT.Final - feedT.Final;

            if (dynamicRiseK < 0.5 * steadyRiseK)
                throw new Exception("The discharge ended at " + outletT.Final.ToString("F2") +
                    " K with the suction at " + feedT.Final.ToString("F2") +
                    " K: the compression heat is being flashed away at the inlet temperature.");

            new ResultTable("Dynamic compressor discharge temperature")
                .Row("feed held at its temperature", FeedTemperatureK, feedT.Final, 0.01, "K")
                .Row("discharge pressure from the conductance", DischargeBar * 1e5, outletP.Final, 0.01, "Pa")
                .Row("discharge temperature at the end of the run", steadyOutletK, outletT.Final, 0.02, "K")
                .Row("discharge temperature after two steps", steadyOutletK, outletT.ValueAt(2.0), 0.02, "K")
                .RowInRange("adiabatic rise over the suction", steadyRiseK - 2.0, steadyRiseK + 8.0, dynamicRiseK, "K")
                .PrintAndThrowIfFailed();
        }
    }
}
