using System;
using System.IO;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The benzene-toluene column shut down from its design steady state: the feed is cut, the reboiler duty
    /// ramps to zero while the reflux keeps running, the pressure controller takes the condenser duty to zero
    /// as the boilup dies, and the two level controllers are given low setpoints to drain the sump and the drum.
    /// The flowsheet is emitted for the case library before the run, at the design steady state, with that state
    /// stored as "Design" and selected as the schedule's initial state; the run is then checked for the drained
    /// levels and the dead duties.
    /// </summary>
    internal static class DynamicsColumnShutdownCaseTest
    {
        public const string CaseName = "benzene-toluene-column-shutdown";
        static DWSIM.Automation.FluentAPI.DynamicsResult RunResult;

        static double Env(string name, double fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : fallback;
        }

        public static void Run()
        {
            // The published case is the hour-long run (DWSIM_COL_DURATION=3600); the suite runs a short one.
            var duration = Env("DWSIM_COL_DURATION", 1200.0);
            var step = Env("DWSIM_COL_STEP", 2.0);
            var substeps = Env("DWSIM_COL_SUBSTEPS", 2.0);
            var feedOffAt = Env("DWSIM_COL_FEED_OFF_AT", 60.0);        // feed cut
            var heatEnd = Env("DWSIM_COL_HEAT_END", 1260.0);           // reboiler duty reaches zero
            var drainSumpAt = Env("DWSIM_COL_DRAIN_SUMP_AT", 1500.0);  // sump level setpoint lowered
            var drainDrumAt = Env("DWSIM_COL_DRAIN_DRUM_AT", 1800.0);  // drum level setpoint lowered
            Console.WriteLine($"shutdown: {duration} s, step {step} s, {substeps} sub-steps, feed off at {feedOffAt} s, reboiler ramp to zero {feedOffAt}-{heatEnd} s, sump drained from {drainSumpAt} s, drum from {drainDrumAt} s");

            var c = BenzeneTolueneDynamicColumn.Build(substeps);
            var fs = c.Fs;
            var col = c.Col;

            // the column sits at atmospheric pressure once it is cold: the same blanket as the startup
            c.Column.WithDynamicProperty("Minimum Pressure", 101325.0)
                    .WithDynamicProperty("Coolant Temperature", 298.15)
                    .WithDynamicProperty("Tray Weeping", true);

            // ---------------------------------------------------------------- the procedure
            c.DefineIntegrator("Shutdown", step, duration);

            fs.Dynamics.DefineEventSet("Shutdown")
                .AddStepChange("feed", "PROP_MS_2", 0.0, at: feedOffAt.Seconds(), units: "kg/s", description: "feed cut")
                .AddEvent("reboiler steam ramped down to zero")
                    .At(heatEnd.Seconds())
                    .ChangeProperty("reb duty", "PROP_ES_0", 0.0, "kW")
                    .WithTransition(DWSIM.Interfaces.Enums.Dynamics.DynamicsEventTransitionType.LinearChange, referenceEventDescription: "feed cut")
                    .And()
                .AddStepChange("LC-02", "SetPointAbs", 0.10, at: drainSumpAt.Seconds(), units: "m", description: "drain the sump: level setpoint to 0.10 m")
                .AddStepChange("LC-01", "SetPointAbs", 0.05, at: drainDrumAt.Seconds(), units: "m", description: "drain the reflux drum: level setpoint to 0.05 m");

            // the design steady state, seeded, is the state every run starts from: stored with the file
            fs.Dynamics.WithHistorian(true, 5000);
            fs.Dynamics.StoreCurrentStateAs("Design");
            fs.Dynamics.DefineSchedule("Shutdown")
                .WithIntegrator("Shutdown")
                .WithEventSet("Shutdown")
                .WithInitialState("Design")
                .MakeCurrent();

            // ---------------------------------------------------------------- what the PFD shows
            fs.AddText("note", "Benzene-toluene column shut down from steady state: feed cut at 1 min, reboiler steam ramped to zero over 20 min under total reflux, then the sump and the drum are drained through their level controllers. Press play on the 'Shutdown' schedule.", 40, 20).WithFontSize(13);
            fs.AddChart("trend", 1150, 40, 620, 420).ForIntegrator("Shutdown");
            fs.AddChart("temperature profile", 1150, 480, 620, 380).ForObject("T-01", "Temperature Profile");
            fs.AddPropertyTable("column table", 40, 620)
                .WithHeader("T-01")
                .Show("T-01", "PROP_DC_5", "PROP_DC_6", "Stage_Pressure_1", "Stage_Pressure_16", "Stage_Temperature_1", "Stage_Temperature_8", "Stage_Temperature_16", "Stage_LiquidLevel_1", "Sump_LiquidLevel");
            fs.AddMasterTable("streams", 40, 860)
                .OfFamily(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream)
                .Including()
                .WithProperties("PROP_MS_0", "PROP_MS_1", "PROP_MS_2", "PROP_MS_3", "PROP_MS_102/Benzene")
                .WithHeader("Material streams");

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, "Shutdown")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker).ToList();
            if (blockers.Count > 0) throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            // ---------------------------------------------------------------- case library
            // the file stored is the design steady state with the schedule configured, ready to press play:
            // saved before the run, so it carries the design levels, setpoints and pressures
            var dir = CaseLibraryOutput.DirFor(CaseName);
            if (duration >= 3000) CaseLibraryOutput.Emit(fs, CaseName);
            var tracePath = Path.Combine(dir, "trace.csv");
            using (var trace = new StreamWriter(tracePath))
            {
                var wroteHeader = false;
                RunResult = fs.RunDynamics("Shutdown").OnPostStep((sender, e) =>
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
            using (var w = new StreamWriter(Path.Combine(dir, "shutdown-run.csv")))
            {
                w.WriteLine("time_s," + string.Join(",", names.Select(n => n.Replace(' ', '_'))));
                var t = series["condenser level"].TimeSeconds;
                for (int k = 0; k < t.Count; k++)
                    w.WriteLine(t[k].ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        string.Join(",", names.Select(n => k < series[n].Values.Count ? series[n].Values[k].ToString("G6", System.Globalization.CultureInfo.InvariantCulture) : "")));
            }
            foreach (var n in names)
                Console.WriteLine($"  {n}: initial {series[n].Initial:G5}, at 600 s {series[n].ValueAt(600):G5}, at 1200 s {series[n].ValueAt(1200):G5}, at 2400 s {series[n].ValueAt(2400):G5}, final {series[n].Final:G5}");

            if (duration < 3000) return;   // a short exploratory run: no closure check
            new ResultTable("Dynamic column, shutdown")
                .RowInRange("feed cut", -1e-6, 1e-6, series["feed mass flow"].Final, "kg/s")
                .RowInRange("reboiler duty at zero", -1e-6, 1e-6, series["reboiler duty"].Final, "kW")
                .RowInRange("condenser duty at its minimum", -1.0, 0.2 * c.Qc0 + 1.0, series["condenser duty"].Final, "kW")
                .RowInRange("sump drained to its low setpoint", 0.0, 0.20, series["sump level"].Final, "m")
                .RowInRange("drum drained to its low setpoint", 0.0, 0.15, series["condenser level"].Final, "m")
                .RowInRange("pressure back at the blanket", 101.0, 105.0, series["top pressure"].Final / 1000.0, "kPa")
                .PrintAndThrowIfFailed();
        }
    }
}
