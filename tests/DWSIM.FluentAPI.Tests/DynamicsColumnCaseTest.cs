using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DWSIM.Automation.DynamicRunner.ColumnInternals;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;
using DWSIM.Automation.FluentAPI.Builders;
using Flowsheet = DWSIM.Automation.FluentAPI.Flowsheet;
using Valve = DWSIM.UnitOperations.UnitOperations.Valve;

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

            var fs = Flowsheet.Create("BenzeneTolueneColumnDynamics")
                .WithCompounds("Benzene", "Toluene")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // ---------------------------------------------------------------- steady state
            var feed = fs.AddMaterialStream("feed")
                .At(368.15.Kelvin(), 150000.0.Pascal())
                .WithMolarFlow(100.0.MolPerSecond())
                .SetCompoundMolarFlow("Benzene", 50.0)
                .SetCompoundMolarFlow("Toluene", 50.0)
                .AsFlowSpec();

            var distillate = fs.AddMaterialStream("distillate");
            var bottoms = fs.AddMaterialStream("bottoms");
            var condDuty = fs.AddEnergyStream("cond duty");
            var rebDuty = fs.AddEnergyStream("reb duty");

            var column = fs.AddDistillationColumn("T-01")
                .WithNumberOfStages(16)
                .WithFeed(feed, 8)
                .WithDistillate(distillate)
                .WithBottoms(bottoms)
                .WithCondenserDuty(condDuty)
                .WithReboilerDuty(rebDuty)
                .WithCondenserSpec("Reflux Ratio", 2.5, "")
                .WithReboilerSpec("Product Molar Flow Rate", 50.0, "mol/s")
                .WithTopPressure(120000.0.Pascal())
                .WithColumnPressureDrop(10000.0.Pascal());

            // Product valves: Kv mode so each valve computes its own flow from the pressure either side of
            // it, which is what the level controllers move. Downstream the products are pressure boundaries.
            var distProduct = fs.AddMaterialStream("distillate product").At(353.15.Kelvin(), 90000.0.Pascal()).AsPressureSpec();
            var botProduct = fs.AddMaterialStream("bottoms product").At(383.15.Kelvin(), 90000.0.Pascal()).AsPressureSpec();

            var lv1 = fs.AddValve("LV-01")
                .WithCalcMode(Valve.CalculationMode.Kv_Liquid)
                .WithKv(150.0)
                .WithOpeningKvRelationship()
                .WithOpeningPercent(50.0)
                .WithOpeningSetpoint(50.0)
                .ConnectFeed(distillate, 0)
                .ConnectProduct(distProduct, 0);

            var lv2 = fs.AddValve("LV-02")
                .WithCalcMode(Valve.CalculationMode.Kv_Liquid)
                .WithKv(150.0)
                .WithOpeningKvRelationship()
                .WithOpeningPercent(50.0)
                .WithOpeningSetpoint(50.0)
                .ConnectFeed(bottoms, 0)
                .ConnectProduct(botProduct, 0);

            fs.AutoLayout();
            fs.Solve();

            var col = column.Object;
            foreach (var si in col.MaterialStreams.Values)
                Console.WriteLine($"  registered material stream {si.StreamID} ({fs.Inner.SimulationObjects[si.StreamID].GraphicObject.Tag}): type {si.StreamType}, behaviour {si.StreamBehavior}, stage '{si.AssociatedStage}'");
            foreach (var si in col.EnergyStreams.Values)
                Console.WriteLine($"  registered energy stream {si.StreamID}: type {si.StreamType}, behaviour {si.StreamBehavior}, stage '{si.AssociatedStage}'");
            Console.WriteLine($"steady state: Qc = {condDuty.EnergyFlowKW:F1} kW, Qr = {rebDuty.EnergyFlowKW:F1} kW, D = {distillate.MolarFlowMolPerSecond:F2} mol/s, B = {bottoms.MolarFlowMolPerSecond:F2} mol/s");
            Console.WriteLine($"distillate benzene {distillate.OverallMoleFraction("Benzene"):F4}, bottoms toluene {bottoms.OverallMoleFraction("Toluene"):F4}");
            Console.WriteLine($"distillate P {distillate.Object.GetPressure():F0} Pa -> {distProduct.Object.GetPressure():F0} Pa; bottoms P {bottoms.Object.GetPressure():F0} Pa -> {botProduct.Object.GetPressure():F0} Pa");

            // ---------------------------------------------------------------- tray hydraulics from the rating
            var input = new ColumnInternalsInput { ColumnName = "T-01", TargetFloodFractionTrays = 0.75 };
            input.Sections.Add(new InternalsSection
            {
                Name = "Trays", FromStage = 2, ToStage = 15, Type = InternalType.SieveTray, Diameter = 0.0,
                TraySpacing = 0.5, DowncomerAreaFraction = 0.12, WeirHeight = 0.05, HoleAreaFraction = 0.10
            });
            var rating = ColumnInternalsStudy.Run(fs.Inner, input);
            var D = rating.Sections.SelectMany(sec => sec.Stages).Where(r => r.Diameter > 0).Select(r => r.Diameter).DefaultIfEmpty(0.0).Max();
            if (D <= 0) throw new Exception("the rating gave no diameter");
            var area = Math.PI * D * D / 4.0;
            var weirLength = 0.73 * D;                 // chord of a 12 % downcomer segment
            var holeArea = 0.10 * 0.76 * area;         // 10 % of the active area (two 12 % downcomers)
            Console.WriteLine($"rated diameter {D:F2} m, weir {weirLength:F2} m, hole area {holeArea:F3} m2");

            col.EstimatedDiameter = D;
            col.TraySpacing = 0.5;
            col.BottomSpacing = 2.0;   // the condenser stage doubles as the reflux drum: 2 m of vessel
            col.TopSpacing = 1.0;      // the sump: the reboiler stage height plus this
            for (int i = 0; i < col.Stages.Count; i++)
            {
                var st = col.Stages[i];
                st.DowncomerLength = weirLength;
                st.TotalHoleArea = holeArea;
                st.StageHeight = 0.5;
                st.DowncomerHeight = 0.05;
            }
            col.Stages[0].DowncomerHeight = 1.0;                    // reflux drum: the weir relation holds ~0.9 m of liquid
            col.Stages[0].DowncomerLength = 0.05;                   // a reflux line, not a weir: the reflux answers the drum level gently
            col.Stages[col.Stages.Count - 1].StageHeight = 1.0;     // reboiler stage
            col.Stages[col.Stages.Count - 1].DowncomerHeight = 0.5;
            column.WithDynamicProperty("Quasi-Steady Vapor", true)
                  .WithDynamicProperty("Calibrate Tray Coefficients", true)
                  .WithDynamicProperty("Souders-Brown Coefficient", 0.05)
                  .WithDynamicProperty("Time step discretization", substeps);

            col.InitializeDynamicsFromSteadyStateSolution();
            var level0 = col.Stages[0].LiquidLevel;
            var sump0 = col.BottomLiquidLevel;
            var pTop0 = col.Stages[0].P;
            var qc0 = condDuty.EnergyFlowKW;
            Console.WriteLine($"initial: condenser level {level0:F3} m, sump level {sump0:F3} m, top P {pTop0:F0} Pa, Qc {qc0:F1} kW");
            for (int i = 0; i < col.Stages.Count; i++)
                Console.WriteLine($"  stage {i + 1}: level {col.Stages[i].LiquidLevel:F4} m, K {col.Stages[i].DryTrayPressureDropCoefficient:E2}, P {col.Stages[i].P:F0} Pa, T {col.Stages[i].T - 273.15:F1} C");

            // ---------------------------------------------------------------- controllers
            fs.AddPIDController("LC-01")
                .Controls("T-01", "Stage_LiquidLevel_1", "m")
                .Manipulates("LV-01", "PROP_VA_5", "")
                .WithSetPoint(level0)
                .WithTuning(1.5, 0.01, 0.0)
                .WithOutputLimits(0.0, 100.0)
                .WithOffset(50.0)
                .ReverseActing(true)
                .Configure(o => o.ManipulatedVariableSpan = 100.0);

            fs.AddPIDController("LC-02")
                .Controls("T-01", "Sump_LiquidLevel", "m")
                .Manipulates("LV-02", "PROP_VA_5", "")
                .WithSetPoint(sump0)
                .WithTuning(2.0, 0.02, 0.0)
                .WithOutputLimits(0.0, 100.0)
                .WithOffset(50.0)
                .ReverseActing(true)
                .Configure(o => o.ManipulatedVariableSpan = 100.0);

            fs.AddPIDController("PC-01")
                .Controls("T-01", "Stage_Pressure_1", "Pa")
                .Manipulates("cond duty", "PROP_ES_0", "kW")
                .WithSetPoint(pTop0)
                .WithTuning(2.0, 0.02, 0.0)
                .WithOutputLimits(0.0, 3.0 * qc0)   // the duty stream carries the heat removed, positive
                .WithOffset(qc0)
                .ReverseActing(true)                // pressure up: remove more heat
                .Configure(o => o.ManipulatedVariableSpan = qc0);

            fs.AddIndicator("LI-01", IndicatorKind.Level).Reads("T-01", "Stage_LiquidLevel_1").WithRange(0.0, 2.0);
            fs.AddIndicator("LI-02", IndicatorKind.Level).Reads("T-01", "Sump_LiquidLevel").WithRange(0.0, 2.0);
            fs.AddIndicator("PI-01", IndicatorKind.Analog).Reads("T-01", "Stage_Pressure_1").WithRange(100000.0, 150000.0);
            fs.AddIndicator("TI-08", IndicatorKind.Analog).Reads("T-01", "Stage_Temperature_8").WithRange(350.0, 400.0);

            // ---------------------------------------------------------------- schedule
            var feedKgs = feed.Object.GetMassFlow();
            fs.Dynamics.DefineIntegrator("Feed step")
                .WithIntegrationStep(step.Seconds())
                .WithDuration(duration.Seconds())
                .Monitor("T-01", "Stage_LiquidLevel_1", "m", "condenser level")
                .Monitor("T-01", "Sump_LiquidLevel", "m", "sump level")
                .Monitor("T-01", "Stage_LiquidLevel_8", "m", "feed tray level")
                .Monitor("T-01", "Stage_Pressure_1", "Pa", "top pressure")
                .Monitor("T-01", "Stage_Pressure_16", "Pa", "bottom pressure")
                .Monitor("T-01", "Stage_Temperature_1", "K", "condenser temperature")
                .Monitor("T-01", "Stage_Temperature_8", "K", "feed tray temperature")
                .Monitor("T-01", "Stage_Temperature_16", "K", "reboiler temperature")
                .Monitor("LV-01", "PROP_VA_5", "", "distillate valve opening")
                .Monitor("LV-02", "PROP_VA_5", "", "bottoms valve opening")
                .Monitor("cond duty", "PROP_ES_0", "kW", "condenser duty")
                .Monitor("feed", "PROP_MS_2", "kg/s", "feed mass flow")
                .Monitor("distillate product", "PROP_MS_2", "kg/s", "distillate mass flow")
                .Monitor("bottoms product", "PROP_MS_2", "kg/s", "bottoms mass flow")
                .Monitor("distillate product", "PROP_MS_102/Benzene", "", "distillate benzene fraction")
                .Monitor("bottoms product", "PROP_MS_102/Toluene", "", "bottoms toluene fraction");

            fs.Dynamics.DefineEventSet("Feed step")
                .AddStepChange("feed", "PROP_MS_2", 1.10 * feedKgs, at: stepUpAt.Seconds(), units: "kg/s")
                .AddStepChange("feed", "PROP_MS_2", feedKgs, at: stepBackAt.Seconds(), units: "kg/s");

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
            var names = new[] { "condenser level", "sump level", "feed tray level", "top pressure", "bottom pressure",
                "condenser temperature", "feed tray temperature", "reboiler temperature", "distillate valve opening",
                "bottoms valve opening", "condenser duty", "feed mass flow", "distillate mass flow", "bottoms mass flow",
                "distillate benzene fraction", "bottoms toluene fraction" };
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
                .Row("condenser level back at setpoint", level0, series["condenser level"].Final, 0.05, "m")
                .Row("sump level back at setpoint", sump0, series["sump level"].Final, 0.05, "m")
                .Row("top pressure back at setpoint", pTop0 / 1000.0, series["top pressure"].Final / 1000.0, 0.02, "kPa")
                .Row("feed back at its base rate", feedKgs, series["feed mass flow"].Final, 0.01, "kg/s")
                .PrintAndThrowIfFailed();

            // ---------------------------------------------------------------- case library
            // the file stored is the steady state with the schedule configured, ready to press play
            lv1.WithOpeningPercent(50.0).WithOpeningSetpoint(50.0);
            lv2.WithOpeningPercent(50.0).WithOpeningSetpoint(50.0);
            fs.Solve();
            CaseLibraryOutput.Emit(fs, CaseName);
        }
    }
}
