using System;
using System.IO;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// A benzene-toluene column run in dynamics: tray hydraulics from the column internals rating, level
    /// control on the condenser and the sump, pressure control on the condenser duty, and a feed step that
    /// the controllers have to ride out. The run is checked for closure (levels and pressure return to
    /// setpoint) and the flowsheet is emitted for the case library with the schedule configured.
    /// </summary>
    internal static class DynamicsColumnCaseTest
    {
        public const string CaseName = "benzene-toluene-column-dynamics";
        static DWSIM.Automation.FluentAPI.DynamicsResult RunResult;

        static double Env(string name, double fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : fallback;
        }

        public static void Run()
        {
            // The published case is the hour-long run: DWSIM_COL_DURATION=3600 DWSIM_COL_STEP_AT=600
            // DWSIM_COL_STEP_BACK_AT=2400 (about ten minutes of wall time). The suite runs twenty minutes.
            var duration = Env("DWSIM_COL_DURATION", 1200.0);
            var step = Env("DWSIM_COL_STEP", 2.0);
            var substeps = Env("DWSIM_COL_SUBSTEPS", 2.0);
            var stepUpAt = Env("DWSIM_COL_STEP_AT", 300.0);
            var stepBackAt = Env("DWSIM_COL_STEP_BACK_AT", 900.0);
            Console.WriteLine($"run: {duration} s, step {step} s, {substeps} sub-steps, feed +10 % at {stepUpAt} s, back at {stepBackAt} s");

            var c = BenzeneTolueneDynamicColumn.Build(substeps);
            var fs = c.Fs;

            // ---------------------------------------------------------------- schedule
            c.DefineIntegrator("Feed step", step, duration);

            fs.Dynamics.DefineEventSet("Feed step")
                .AddStepChange("feed", "PROP_MS_2", 1.10 * c.FeedKgs, at: stepUpAt.Seconds(), units: "kg/s")
                .AddStepChange("feed", "PROP_MS_2", c.FeedKgs, at: stepBackAt.Seconds(), units: "kg/s");

            fs.Dynamics.WithHistorian(true, 5000);
            fs.Dynamics.DefineSchedule("Feed step run")
                .WithIntegrator("Feed step")
                .WithEventSet("Feed step")
                .MakeCurrent();

            // ---------------------------------------------------------------- what the PFD shows
            fs.AddText("note", "Benzene-toluene column in dynamics: levels, pressure and temperatures follow a +10 % feed step at 10 min (back at 40 min). Press play on the 'Feed step run' schedule.", 40, 20).WithFontSize(13);
            fs.AddChart("trend", 1150, 40, 620, 420).ForIntegrator("Feed step");
            fs.AddChart("temperature profile", 1150, 480, 620, 380).ForObject("T-01", "Temperature Profile");
            fs.AddPropertyTable("column table", 40, 620)
                .WithHeader("T-01")
                .Show("T-01", "PROP_DC_5", "PROP_DC_6", "Stage_Pressure_1", "Stage_Pressure_16", "Stage_Temperature_1", "Stage_Temperature_8", "Stage_Temperature_16", "Stage_LiquidLevel_1", "Sump_LiquidLevel");
            fs.AddMasterTable("streams", 40, 860)
                .OfFamily(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream)
                .Including()
                .WithProperties("PROP_MS_0", "PROP_MS_1", "PROP_MS_2", "PROP_MS_3", "PROP_MS_102/Benzene")
                .WithHeader("Material streams");
            Console.WriteLine("annotations: " + string.Join(", ", fs.Annotations.Select(a => a.Tag + " (" + a.ObjectType + ")")));

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, "Feed step run")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker).ToList();
            if (blockers.Count > 0) throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            var tracePath = Path.Combine(CaseLibraryOutput.DirFor(CaseName), "trace.csv");
            using (var trace = new StreamWriter(tracePath))
            {
                var wroteHeader = false;
                RunResult = fs.RunDynamics("Feed step run").OnPostStep((sender, e) =>
                {
                    if (!wroteHeader) { trace.WriteLine("t," + string.Join(",", e.variables.Select(v => v.Description.Replace(' ', '_')))); wroteHeader = true; }
                    trace.WriteLine(e.tstamp.TimeOfDay.TotalSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + string.Join(",", e.variables.Select(v => v.PropertyValue)));
                    trace.Flush();
                }).Execute();
            }
            var result = RunResult;
            if (!result.Completed)
                throw new Exception("Integration did not complete: " + (result.Error == null ? "aborted" : result.Error.Message));
            Console.WriteLine(result);

            // ---------------------------------------------------------------- series out
            var names = BenzeneTolueneDynamicColumn.MonitorNames;
            var series = names.ToDictionary(n => n, n => result.GetSeries(n));
            var dir = CaseLibraryOutput.DirFor(CaseName);
            using (var w = new StreamWriter(Path.Combine(dir, "feed-step-run.csv")))
            {
                w.WriteLine("time_s," + string.Join(",", names.Select(n => n.Replace(' ', '_'))));
                var t = series["condenser level"].TimeSeconds;
                for (int k = 0; k < t.Count; k++)
                    w.WriteLine(t[k].ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        string.Join(",", names.Select(n => k < series[n].Values.Count ? series[n].Values[k].ToString("G6", System.Globalization.CultureInfo.InvariantCulture) : "")));
            }
            foreach (var n in names)
                Console.WriteLine($"  {n}: initial {series[n].Initial:G5}, at 600 s {series[n].ValueAt(600):G5}, at 1200 s {series[n].ValueAt(1200):G5}, at 2400 s {series[n].ValueAt(2400):G5}, final {series[n].Final:G5}");

            if (duration < 1000) return;   // a short exploratory run: no closure check, nothing emitted
            new ResultTable("Dynamic column, feed step")
                .Row("condenser level back at setpoint", c.Level0, series["condenser level"].Final, 0.05, "m")
                .Row("sump level back at setpoint", c.Sump0, series["sump level"].Final, 0.05, "m")
                .Row("top pressure back at setpoint", c.PTop0 / 1000.0, series["top pressure"].Final / 1000.0, 0.02, "kPa")
                .Row("feed back at its base rate", c.FeedKgs, series["feed mass flow"].Final, 0.01, "kg/s")
                .PrintAndThrowIfFailed();

            // ---------------------------------------------------------------- case library
            // the file stored is the steady state with the schedule configured, ready to press play
            c.Lv1.WithOpeningPercent(50.0).WithOpeningSetpoint(50.0);
            c.Lv2.WithOpeningPercent(50.0).WithOpeningSetpoint(50.0);
            BenzeneTolueneDynamicColumn.RestoreFeedPressure(c);
            fs.Solve();
            BenzeneTolueneDynamicColumn.RestoreRatedDiameter(c);
            BenzeneTolueneDynamicColumn.SetProductBoundaries(c);
            Console.WriteLine($"file: feed P {c.Feed.Object.GetPressure():F0} Pa, column diameter {c.Col.EstimatedDiameter:F3} m, LV-01 {c.Lv1.Object.OpeningPct:F0} %, LV-02 {c.Lv2.Object.OpeningPct:F0} %");
            CaseLibraryOutput.Emit(fs, CaseName);
        }
    }
}
