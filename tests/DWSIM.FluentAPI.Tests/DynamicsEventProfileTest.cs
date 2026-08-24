using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Builds a dynamic simulation from scratch — integrator, schedule, event set and monitored
    /// variable — and checks that the recorded series follows the events.
    /// </summary>
    /// <remarks>
    /// Deliberately has no unit operation: a lone material stream driven by events exercises the
    /// integration loop, the schedule lookup, the tick-keyed history and the ramp interpolation
    /// without any process model that could fail for its own reasons. If this one fails, nothing
    /// downstream of it is worth reading.
    /// </remarks>
    internal static class DynamicsEventProfileTest
    {
        private const string MassFlow = "PROP_MS_2";
        private const double InitialFlow = 10.0;

        public static void Run()
        {
            RunStepProfile();
            RunRampProfile();
            RunGuards();
            RunResume();
        }

        /// <summary>
        /// A paused run resumed, and single steps taken from it, have to land exactly where an
        /// uninterrupted run would. This is what the GUI's play, pause and step buttons are.
        /// </summary>
        private static void RunResume()
        {
            var whole = RunProfile(runner => runner.Run(Options(null)));

            var piecewise = RunProfile(runner =>
            {
                // Ten steps, as a pause would leave it, then carry on to the end.
                runner.Run(Options(o => o.MaxSteps = 10));

                var resumed = Options(o => o.Resume = true);
                var result = runner.Run(resumed);

                return result;
            });

            new ResultTable("Resuming a paused run")
                .Row("steps, uninterrupted", 121, whole.Steps, 0.0)
                .Row("steps, paused and resumed", 121, piecewise.Steps + 10, 0.0)
                .Row("final time, uninterrupted", 121.0, whole.FinalTimeSeconds, 0.01, "s")
                .Row("final time, resumed", 121.0, piecewise.FinalTimeSeconds, 0.01, "s")
                .PrintAndThrowIfFailed();

            // The history a resumed run records has to continue the paused one's, not replace it.
            if (piecewise.Integrator.MonitoredVariableValues.Count != 121)
            {
                throw new Exception("A resumed run recorded " +
                    piecewise.Integrator.MonitoredVariableValues.Count +
                    " points; the uninterrupted one recorded 121. Resuming restarted the history.");
            }
        }

        private static IntegratorRunOptions Options(Action<IntegratorRunOptions> configure)
        {
            var options = new IntegratorRunOptions { Schedule = "Resume run" };
            if (configure != null) configure(options);
            return options;
        }

        private static IntegratorRunResult RunProfile(Func<IntegratorRunner, IntegratorRunResult> drive)
        {
            var fs = NewFlowsheet("FluentDynamicsResume");

            fs.Dynamics.DefineIntegrator("Resume")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(120.Seconds())
                .Monitor("feed", MassFlow, "kg/s", "feed mass flow");

            fs.Dynamics.DefineSchedule("Resume run").WithIntegrator("Resume").MakeCurrent();

            var runner = new IntegratorRunner(fs.Inner);
            var result = drive(runner);

            if (result.Exceptions.Count > 0) throw result.Exceptions[0];
            return result;
        }

        /// <summary>A step change holds the old value until its instant, then jumps.</summary>
        private static void RunStepProfile()
        {
            var fs = NewFlowsheet("FluentDynamicsStepProfile");

            fs.Dynamics.DefineIntegrator("Steps")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(120.Seconds())
                .Monitor("feed", MassFlow, "kg/s", "feed mass flow");

            fs.Dynamics.DefineEventSet("A step")
                .AddStepChange("feed", MassFlow, 25.0, at: 60.Seconds(), units: "kg/s");

            fs.Dynamics.DefineSchedule("Step run")
                .WithIntegrator("Steps")
                .WithEventSet("A step")
                .MakeCurrent();

            RequireReady(fs, "Step run");

            var result = fs.RunDynamics("step run").Execute();
            Require(result);

            Console.WriteLine(result);
            var series = result.GetSeries("feed mass flow");

            new ResultTable("Dynamics step profile")
                .Row("recorded points", 121, series.Count, 0.0)
                .Row("flow before the step, t = 30 s", InitialFlow, series.ValueAt(30.0), 0.02, "kg/s")
                .Row("flow after the step, t = 90 s", 25.0, series.ValueAt(90.0), 0.02, "kg/s")
                .Row("final flow", 25.0, series.Final, 0.02, "kg/s")
                .Row("first sample time", 0.0, series.TimeSeconds[0], 0.0, "s")
                .Row("last sample time", 120.0, series.TimeSeconds[series.Count - 1], 0.01, "s")
                .PrintAndThrowIfFailed();

            // A history keyed by anything other than ticks puts the chart and the spreadsheet on
            // the wrong x axis. Reading the series back is what proves the key was right.
            var spacing = series.TimeSeconds[1] - series.TimeSeconds[0];
            if (Math.Abs(spacing - 1.0) > 0.01)
                throw new Exception("The time axis is wrong: samples are " + spacing +
                    " s apart, expected 1 s.");
        }

        /// <summary>
        /// A linear transition interpolates from the state recorded at its reference event, which
        /// here is the start of the run, up to its own instant.
        /// </summary>
        private static void RunRampProfile()
        {
            var fs = NewFlowsheet("FluentDynamicsRampProfile");

            fs.Dynamics.DefineIntegrator("Ramps")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(120.Seconds())
                .Monitor("feed", MassFlow, "kg/s", "feed mass flow");

            fs.Dynamics.DefineEventSet("A ramp")
                .AddRamp("feed", MassFlow, 50.0, at: 90.Seconds(), units: "kg/s");

            fs.Dynamics.DefineSchedule("Ramp run")
                .WithIntegrator("Ramps")
                .WithEventSet("A ramp")
                .MakeCurrent();

            RequireReady(fs, "Ramp run");

            var result = fs.RunDynamics("Ramp run").Execute();
            Require(result);

            Console.WriteLine(result);
            var series = result.GetSeries("feed mass flow");

            // Ramps from 10 kg/s at t = 0 to 50 kg/s at t = 90, then holds.
            new ResultTable("Dynamics ramp profile")
                .Row("flow a third of the way up, t = 30 s", 23.33, series.ValueAt(30.0), 0.05, "kg/s")
                .Row("flow two thirds up, t = 60 s", 36.67, series.ValueAt(60.0), 0.05, "kg/s")
                .Row("flow at the top of the ramp, t = 90 s", 50.0, series.ValueAt(90.0), 0.02, "kg/s")
                .Row("flow after the ramp, t = 120 s", 50.0, series.Final, 0.02, "kg/s")
                .PrintAndThrowIfFailed();

            // A ramp that jumped instead of ramping would still hit the endpoints above.
            var rising = 0;
            for (var i = 1; i < series.Count && series.TimeSeconds[i] <= 90.0; i++)
                if (series.Values[i] > series.Values[i - 1]) rising += 1;

            if (rising < 80)
                throw new Exception("The ramp is not gradual: only " + rising +
                    " of the ~90 samples before t = 90 s rose. It behaved like a step change.");
        }

        /// <summary>Schedule lookup, run limits, and leaving the flowsheet as we found it.</summary>
        private static void RunGuards()
        {
            var fs = NewFlowsheet("FluentDynamicsGuards");

            fs.Dynamics.DefineIntegrator("Guarded")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(120.Seconds())
                .Monitor("feed", MassFlow, "kg/s", "feed mass flow");

            fs.Dynamics.DefineSchedule("Guarded Run").WithIntegrator("Guarded").MakeCurrent();

            // Case and surrounding whitespace must not decide whether a schedule is found.
            var result = fs.RunDynamics("  gUaRdEd RuN  ").WithMaxSteps(5).Execute();

            if (result.Series.Count == 0)
                throw new Exception("A schedule name in a different case did not resolve.");
            if (!result.Aborted)
                throw new Exception("WithMaxSteps did not stop the run.");
            if (result.Steps != 5)
                throw new Exception("WithMaxSteps stopped after " + result.Steps + " steps, expected 5.");

            if (fs.Inner.DynamicMode)
                throw new Exception("Dynamic mode was left on after the run.");

            // An unknown schedule has to say so, and say what is on offer.
            var missing = fs.RunDynamics("no such schedule").Execute();
            if (missing.Completed)
                throw new Exception("Running an undefined schedule did not fail.");
            if (missing.Error == null || !missing.Error.Message.Contains("Guarded Run"))
                throw new Exception("The error for an undefined schedule does not list the available ones: " +
                    (missing.Error == null ? "no error" : missing.Error.Message));

            // A stop condition ends the run cleanly, without an exception.
            var stopped = fs.RunDynamics("Guarded Run").StopWhen((f, t) => t >= 10.0).Execute();
            if (!stopped.Aborted) throw new Exception("StopWhen did not stop the run.");
            if (stopped.FinalTimeSeconds > 12.0)
                throw new Exception("StopWhen stopped at t = " + stopped.FinalTimeSeconds + " s, expected about 10 s.");
        }

        // -------------------------------------------------------------------------

        private static Flowsheet NewFlowsheet(string name)
        {
            var fs = Flowsheet.Create(name)
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            fs.AddMaterialStream("feed")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(InitialFlow.KgPerSecond())
                .AsFlowSpec();

            fs.Solve();
            return fs;
        }

        private static void RequireReady(Flowsheet fs, string schedule)
        {
            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, schedule)
                .Where(f => f.Severity == DiagnosticSeverity.Blocker)
                .ToList();

            if (blockers.Count > 0)
                throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));
        }

        private static void Require(DynamicsResult result)
        {
            if (result.Completed) return;
            throw new Exception("Integration did not complete: " +
                (result.Error == null ? "aborted" : result.Error.Message));
        }
    }
}
