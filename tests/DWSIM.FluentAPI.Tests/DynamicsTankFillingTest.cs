using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Fills a tank through a closed valve and checks the level against a hand-computed mass balance.
    /// </summary>
    /// <remarks>
    /// This is the first test with a real dynamic unit operation in the loop, so it is the one that
    /// proves the integrator is reachable from a unit operation at all: every dynamic model looks
    /// its integrator up through the manager's current schedule, and a run that does not set that
    /// leaves them throwing.
    ///
    /// The specs matter. The tank only integrates its hold-up when its outlet is specified by
    /// pressure, and the inlet has to be specified by flow for there to be anything to accumulate.
    /// </remarks>
    internal static class DynamicsTankFillingTest
    {
        private const double FeedKgPerSecond = 1.0;
        private const double DurationSeconds = 300.0;
        private const double TankHeightM = 2.0;
        private const double TankVolumeM3 = 2.0;

        public static void Run()
        {
            var fs = Flowsheet.Create("FluentDynamicsTankFilling")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("feed")
                .At(25.Celsius(), 1.Atm())
                .WithMassFlow(FeedKgPerSecond.KgPerSecond())
                .AsFlowSpec();

            var tankOutlet = fs.AddMaterialStream("tank-outlet")
                .At(25.Celsius(), 1.Atm())
                .AsPressureSpec();

            var product = fs.AddMaterialStream("product")
                .At(25.Celsius(), 1.Atm())
                .AsPressureSpec();

            fs.AddTank("TK-01")
                .WithVolume(TankVolumeM3.CubicMeters())
                .WithHeight(TankHeightM.Meters())
                .InitializeFromInlet(false)
                .ResetContent()
                .ConnectFeed(feed, 0)
                .ConnectProduct(tankOutlet, 0);

            // A Kv calculation mode is what lets the valve compute its own flow from the pressure
            // either side of it. In the pressure-drop modes it would demand a flow specification
            // on one side instead, and a shut valve could not then hold the flow at zero.
            var valve = fs.AddValve("V-01")
                .WithCalcMode(DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode.Kv_Liquid)
                .WithKv(10.0)
                .WithOpeningKvRelationship()
                .WithOpeningPercent(100.0)
                .ConnectFeed(tankOutlet, 0)
                .ConnectProduct(product, 0);

            fs.AutoLayout();
            fs.Solve();

            // Shut the valve now that the steady state is solved: a closed valve has no steady
            // state to speak of, and this is the disturbance the run is about. Everything the feed
            // brings in from here on stays in, which is what makes the level predictable.
            valve.WithOpeningPercent(0.0).WithOpeningSetpoint(0.0);

            // The tank integrates its hold-up against whatever its outlet says, and it runs before
            // the valve does. Left at the steady-state rate, the outlet would cancel the first
            // step's accumulation exactly.
            tankOutlet.WithMassFlow(0.0.KgPerSecond());

            fs.Dynamics.DefineIntegrator("Filling")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(DurationSeconds.Seconds())
                .Monitor("TK-01", "Liquid Level", description: "tank level")
                .Monitor("feed", "PROP_MS_2", "kg/s", "feed mass flow");

            fs.Dynamics.DefineSchedule("Filling run")
                .WithIntegrator("Filling")
                .MakeCurrent();

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, "Filling run")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker)
                .ToList();

            if (blockers.Count > 0)
                throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            var result = fs.RunDynamics("Filling run").Execute();

            if (!result.Completed)
                throw new Exception("Integration did not complete: " +
                    (result.Error == null ? "aborted" : result.Error.Message));

            Console.WriteLine(result);

            var level = result.GetSeries("tank level");

            // 1 kg/s for 300 s is 300 kg of water. At about 997 kg/m3 that is 0.3009 m3, and the
            // tank's cross-section is volume over height = 1 m2, so the level is the volume.
            var density = feed.Object.Phases[0].Properties.density.GetValueOrDefault();
            var expectedVolume = FeedKgPerSecond * DurationSeconds / density;
            var area = TankVolumeM3 / TankHeightM;
            var expectedLevel = expectedVolume / area;

            Console.WriteLine("feed density = " + density.ToString("F2") + " kg/m3, expected level = " +
                expectedLevel.ToString("F4") + " m");

            new ResultTable("Dynamic tank filling")
                .Row("level at the end of the run", expectedLevel, level.Final, 0.05, "m")
                .Row("level at half the run", expectedLevel / 2.0, level.ValueAt(DurationSeconds / 2.0), 0.06, "m")
                // The first sample is taken after the first step is solved, so it already holds
                // one step's worth of feed.
                .Row("level after the first step", expectedLevel / DurationSeconds, level.Initial, 0.05, "m")
                .Row("feed held at its spec", FeedKgPerSecond,
                    result.GetSeries("feed mass flow").Final, 0.01, "kg/s")
                .PrintAndThrowIfFailed();

            // Filling a shut tank is a straight line: every sample must be at least as high as the
            // one before it, or the hold-up is not being integrated.
            for (var i = 1; i < level.Count; i++)
            {
                if (level.Values[i] >= level.Values[i - 1] - 1e-9) continue;
                throw new Exception("The level fell between t = " + level.TimeSeconds[i - 1] +
                    " s and t = " + level.TimeSeconds[i] + " s, from " + level.Values[i - 1] +
                    " m to " + level.Values[i] + " m. The tank is losing hold-up through a shut valve.");
            }

            // Reopen before saving: the case library re-solves what it stores, and a shut valve has
            // no steady state. What is worth keeping is the flowsheet with its schedule configured,
            // ready for someone to shut the valve and press play.
            valve.WithOpeningPercent(100.0).WithOpeningSetpoint(100.0);
            fs.Solve();

            CaseLibraryOutput.Emit(fs, "dynamic_tank_filling");
        }
    }
}
