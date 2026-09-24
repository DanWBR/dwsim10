using System;
using System.IO;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The benzene-toluene column started from empty: cold, at atmospheric pressure, product valves closed and
    /// the level controllers in manual. The schedule is an operating procedure with times: the feed fills the
    /// column, the reboiler duty ramps once its stage is covered, the vapor climbs and fills the drum, the reflux
    /// starts on the drum level, and the level controllers are put in automatic when their levels exist. The run
    /// is checked against the steady state the column was designed for and emitted for the case library.
    /// </summary>
    internal static class DynamicsColumnStartupCaseTest
    {
        public const string CaseName = "benzene-toluene-column-startup";
        static DWSIM.Automation.FluentAPI.DynamicsResult RunResult;

        static double Env(string name, double fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : fallback;
        }

        public static void Run()
        {
            // The published case is the two-hour run (DWSIM_COL_DURATION=7200); the suite runs a short one.
            var duration = Env("DWSIM_COL_DURATION", 1500.0);
            var step = Env("DWSIM_COL_STEP", 2.0);
            var substeps = Env("DWSIM_COL_SUBSTEPS", 2.0);
            var heatStart = Env("DWSIM_COL_HEAT_START", 300.0);     // reboiler duty ramp begins (its stage is covered)
            var heatEnd = Env("DWSIM_COL_HEAT_END", 1500.0);        // ramp reaches the design duty
            var lc2At = Env("DWSIM_COL_LC2_AT", 550.0);             // sump level controller to automatic (its level reaches the setpoint)
            var lc1At = Env("DWSIM_COL_LC1_AT", 1700.0);            // drum level controller to automatic
            Console.WriteLine($"startup: {duration} s, step {step} s, {substeps} sub-steps, reboiler ramp {heatStart}-{heatEnd} s, LC-02 auto at {lc2At} s, LC-01 auto at {lc1At} s");

            var c = BenzeneTolueneDynamicColumn.Build(substeps);
            var fs = c.Fs;
            var col = c.Col;

            // ---------------------------------------------------------------- the empty column
            c.Column.WithDynamicProperty("Start Empty", true)
                    .WithDynamicProperty("Initial Pressure", 101325.0)
                    .WithDynamicProperty("Initial Temperature", 298.15)
                    .WithDynamicProperty("Initial Sump Level", 0.0)
                    .WithDynamicProperty("Minimum Pressure", 101325.0)
                    .WithDynamicProperty("Coolant Temperature", 298.15)
                    .WithDynamicProperty("Tray Weeping", true);

            // product valves closed, level loops in manual (the controller writes its manual output, zero)
            c.Lv1.WithOpeningPercent(0.0).WithOpeningSetpoint(0.0);
            c.Lv2.WithOpeningPercent(0.0).WithOpeningSetpoint(0.0);
            c.Lc1.Object.ManualOverride = true; c.Lc1.Object.MVValue = 0.0;
            c.Lc2.Object.ManualOverride = true; c.Lc2.Object.MVValue = 0.0;

            // ---------------------------------------------------------------- the procedure
            c.DefineIntegrator("Startup", step, duration);

            fs.Dynamics.DefineEventSet("Startup")
                .AddStepChange("reb duty", "PROP_ES_0", 0.0, at: 0.0.Seconds(), units: "kW", description: "reboiler steam off")
                .AddStepChange("reb duty", "PROP_ES_0", 0.0, at: heatStart.Seconds(), units: "kW", description: "reboiler stage covered: start the steam")
                .AddEvent("reboiler duty ramp to design")
                    .At(heatEnd.Seconds())
                    .ChangeProperty("reb duty", "PROP_ES_0", c.Qr0, "kW")
                    .WithTransition(DWSIM.Interfaces.Enums.Dynamics.DynamicsEventTransitionType.LinearChange, referenceEventDescription: "reboiler stage covered: start the steam")
                    .And()
                .AddStepChange("LC-02", "ManualOverride", 0.0, at: lc2At.Seconds(), units: "", description: "sump level controller to automatic")
                .AddStepChange("LC-01", "ManualOverride", 0.0, at: lc1At.Seconds(), units: "", description: "drum level controller to automatic");

            fs.Dynamics.WithHistorian(true, 5000);
            fs.Dynamics.DefineSchedule("Startup")
                .WithIntegrator("Startup")
                .WithEventSet("Startup")
                .MakeCurrent();

            // ---------------------------------------------------------------- what the PFD shows
            fs.AddText("note", "Benzene-toluene column started from empty: cold, at atmospheric pressure, valves closed. The feed fills the column, the reboiler ramps, the vapor fills the drum, the reflux starts, the level loops go to automatic. Press play on the 'Startup' schedule.", 40, 20).WithFontSize(13);
            fs.AddChart("trend", 1150, 40, 620, 420).ForIntegrator("Startup");
            fs.AddChart("temperature profile", 1150, 480, 620, 380).ForObject("T-01", "Temperature Profile");
            fs.AddPropertyTable("column table", 40, 620)
                .WithHeader("T-01")
                .Show("T-01", "PROP_DC_5", "PROP_DC_6", "Stage_Pressure_1", "Stage_Pressure_16", "Stage_Temperature_1", "Stage_Temperature_8", "Stage_Temperature_16", "Stage_LiquidLevel_1", "Sump_LiquidLevel");
            fs.AddMasterTable("streams", 40, 860)
                .OfFamily(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream)
                .Including()
                .WithProperties("PROP_MS_0", "PROP_MS_1", "PROP_MS_2", "PROP_MS_3", "PROP_MS_102/Benzene")
                .WithHeader("Material streams");

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, "Startup")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker).ToList();
            if (blockers.Count > 0) throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            // the empty state, seeded here so the first step does not have to
            col.InitializeDynamicsEmpty();
            Console.WriteLine($"empty column: drum level {col.Stages[0].LiquidLevel:F4} m, sump level {col.BottomLiquidLevel:F4} m, P {col.Stages[0].P:F0} Pa, T {col.Stages[0].T - 273.15:F1} C");

            var dir = CaseLibraryOutput.DirFor(CaseName);
            var tracePath = Path.Combine(dir, "trace.csv");
            using (var trace = new StreamWriter(tracePath))
            {
                var wroteHeader = false;
                var debugValve = Environment.GetEnvironmentVariable("DWSIM_COL_DEBUG_VALVE") == "1";
                RunResult = fs.RunDynamics("Startup").OnPostStep((sender, e) =>
                {
                    if (!wroteHeader) { trace.WriteLine("t," + string.Join(",", e.variables.Select(v => v.Description.Replace(' ', '_')))); wroteHeader = true; }
                    var ts = e.tstamp.TimeOfDay.TotalSeconds;
                    trace.WriteLine(ts.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + string.Join(",", e.variables.Select(v => v.PropertyValue)));
                    trace.Flush();
                    if (debugValve && Math.Abs(ts % 100.0) < 1e-6)
                    {
                        var b = c.Bottoms.Object; var bp = c.BotProduct.Object;
                        Console.WriteLine($"  [valve] t {ts:F0}: bottoms T {b.GetTemperature():F2} K, P {b.GetPressure():F0} Pa, W {b.GetMassFlow():G4} kg/s, rho {b.Phases[0].Properties.density.GetValueOrDefault():G4}, VF {b.Phases[2].Properties.molarfraction.GetValueOrDefault():G3}, calc {b.Calculated}; product P {bp.GetPressure():F0} Pa, W {bp.GetMassFlow():G4}; LV-02 opening {c.Lv2.Object.OpeningPct:F1} %");
                    }
                }).Execute();
            }
            var result = RunResult;
            if (!result.Completed)
                throw new Exception("Integration did not complete: " + (result.Error == null ? "aborted" : result.Error.Message));
            Console.WriteLine(result);

            // ---------------------------------------------------------------- series out
            var names = BenzeneTolueneDynamicColumn.MonitorNames;
            var series = names.ToDictionary(n => n, n => result.GetSeries(n));
            using (var w = new StreamWriter(Path.Combine(dir, "startup-run.csv")))
            {
                w.WriteLine("time_s," + string.Join(",", names.Select(n => n.Replace(' ', '_'))));
                var t = series["condenser level"].TimeSeconds;
                for (int k = 0; k < t.Count; k++)
                    w.WriteLine(t[k].ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        string.Join(",", names.Select(n => k < series[n].Values.Count ? series[n].Values[k].ToString("G6", System.Globalization.CultureInfo.InvariantCulture) : "")));
            }
            foreach (var n in names)
                Console.WriteLine($"  {n}: initial {series[n].Initial:G5}, at 600 s {series[n].ValueAt(600):G5}, at 1200 s {series[n].ValueAt(1200):G5}, at 2400 s {series[n].ValueAt(2400):G5}, at 3600 s {series[n].ValueAt(3600):G5}, final {series[n].Final:G5}");

            if (duration < 5000) return;   // a short exploratory run: no closure check, nothing emitted
            new ResultTable("Dynamic column, startup from empty")
                .Row("condenser level at setpoint", c.Level0, series["condenser level"].Final, 0.05, "m")
                .Row("sump level at setpoint", c.Sump0, series["sump level"].Final, 0.05, "m")
                .Row("top pressure at setpoint", c.PTop0 / 1000.0, series["top pressure"].Final / 1000.0, 0.02, "kPa")
                .Row("distillate benzene purity reached", c.XdBenzene0, series["distillate benzene fraction"].Final, 0.03, "")
                .Row("bottoms toluene purity reached", c.XbToluene0, series["bottoms toluene fraction"].Final, 0.03, "")
                .PrintAndThrowIfFailed();

            // ---------------------------------------------------------------- case library
            // the file stored is the steady state with the schedule configured: pressing play starts empty.
            // The valves are half open for the steady state (closed, the Kv valve cannot pass the design flow);
            // the level controllers in manual close them on the first dynamic step.
            c.Lv1.WithOpeningPercent(50.0).WithOpeningSetpoint(50.0);
            c.Lv2.WithOpeningPercent(50.0).WithOpeningSetpoint(50.0);
            c.Feed.Object.SetMassFlow(c.FeedKgs);
            BenzeneTolueneDynamicColumn.RestoreFeedPressure(c);
            fs.Solve();
            BenzeneTolueneDynamicColumn.RestoreRatedDiameter(c);
            BenzeneTolueneDynamicColumn.SetProductBoundaries(c);
            c.Lc1.Object.ManualOverride = true; c.Lc1.Object.MVValue = 0.0;
            c.Lc2.Object.ManualOverride = true; c.Lc2.Object.MVValue = 0.0;
            CaseLibraryOutput.Emit(fs, CaseName);
        }
    }
}
