using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.UnitOperations.SpecialOps;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DynEnums = DWSIM.Interfaces.Enums.Dynamics;

namespace DWSIM.Automation.FluentAPI.Dynamics
{
    /// <summary>
    /// Checks a dynamic simulation before it runs, and explains what went wrong after it does.
    /// </summary>
    /// <remarks>
    /// The rules encode the mistakes that make a dynamic run fail or mislead: an underdetermined
    /// pressure-flow network, a valve with no Kv, a vessel with no volume, a controller wired
    /// backwards. Each finding carries a fix, so a caller can act on it without knowing the model.
    /// </remarks>
    public static class DynamicsDiagnostics
    {
        /// <summary>
        /// Answers "is this flowsheet ready to run dynamically?" — blockers first, then warnings.
        /// </summary>
        /// <param name="flowsheet">The flowsheet to check.</param>
        /// <param name="scheduleName">Schedule to check; the current or first one when null.</param>
        public static IReadOnlyList<Finding> CheckReady(IFlowsheet flowsheet, string scheduleName = null)
        {
            if (flowsheet == null) throw new ArgumentNullException(nameof(flowsheet));

            var findings = new List<Finding>();
            var manager = flowsheet.DynamicsManager;

            if (manager.ScheduleList.Count == 0)
            {
                findings.Add(new Finding("NO_SCHEDULE", DiagnosticSeverity.Blocker, "",
                    "This flowsheet has no dynamics schedule.",
                    "Define one: Dynamics.DefineSchedule(name).WithIntegrator(integrator)."));
                return findings;
            }

            IDynamicsSchedule schedule;
            try { schedule = DWSIM.Automation.DynamicRunner.IntegratorRunner.ResolveSchedule(flowsheet, scheduleName); }
            catch (Exception ex)
            {
                findings.Add(new Finding("NO_SCHEDULE", DiagnosticSeverity.Blocker, "",
                    ex.Message, "Pass one of the schedule names the message lists."));
                return findings;
            }

            if (!manager.IntegratorList.ContainsKey(schedule.CurrentIntegrator))
            {
                findings.Add(new Finding("NO_INTEGRATOR", DiagnosticSeverity.Blocker, "",
                    "Schedule '" + schedule.Description + "' has no integrator assigned.",
                    "Assign one: Dynamics.Schedule(name).WithIntegrator(integratorName)."));
                return findings;
            }

            var integrator = manager.IntegratorList[schedule.CurrentIntegrator];

            CheckSteadyState(flowsheet, findings);
            CheckIntegrator(integrator, findings);
            CheckSchedule(flowsheet, schedule, findings);
            CheckPressureFlowNetwork(flowsheet, findings);
            CheckObjects(flowsheet, findings);
            CheckControllers(flowsheet, findings);

            if (!flowsheet.DynamicMode)
            {
                findings.Add(new Finding("NO_DYNAMIC_MODE", DiagnosticSeverity.Info, "",
                    "Dynamic mode is off.",
                    "The run turns it on for its duration; nothing to do unless you are solving by hand."));
            }

            return Rank(findings);
        }

        /// <summary>
        /// Explains a finished run: what stopped it, and what the recorded series say about the
        /// model and its controllers.
        /// </summary>
        public static IReadOnlyList<Finding> Diagnose(IFlowsheet flowsheet, DynamicsResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var findings = new List<Finding>();

            foreach (var ex in result.Errors)
            {
                var baseex = ex;
                while (baseex.InnerException != null) baseex = baseex.InnerException;
                findings.Add(new Finding("SOLVER_EXCEPTION", DiagnosticSeverity.Blocker, "",
                    baseex.Message,
                    "Check the object named in the message; a smaller integration step often clears a transient failure."));
            }

            if (result.Aborted)
            {
                findings.Add(new Finding("RUN_ABORTED", DiagnosticSeverity.Warning, "",
                    "The run stopped after " + result.Steps + " steps, at " +
                    Fmt(result.FinalTimeSeconds) + " s, before the configured duration.",
                    "Raise the step or wall-time limit, or shorten the duration."));
            }

            if (result.Steps > 0 && result.WallClock.TotalSeconds / result.Steps > 1.0)
            {
                findings.Add(new Finding("SLOW_STEP", DiagnosticSeverity.Warning, "",
                    "Each step took " + Fmt(result.WallClock.TotalSeconds / result.Steps) + " s of wall time.",
                    "Raise CalculationRateEquilibrium so flashes run every few steps instead of every step."));
            }

            foreach (var series in result.Series)
            {
                DiagnoseSeries(series, findings);
            }

            if (flowsheet != null) DiagnoseControllers(flowsheet, result, findings);

            return Rank(findings);
        }

        // ------------------------------------------------------------- Readiness

        /// <summary>
        /// A dynamic run integrates forward from wherever the flowsheet is. Starting it from a
        /// state that was never solved means integrating from nothing, and the first step fails on
        /// whatever the steady state would have failed on.
        /// </summary>
        private static void CheckSteadyState(IFlowsheet flowsheet, List<Finding> findings)
        {
            if (flowsheet.PropertyPackages.Count == 0)
            {
                findings.Add(new Finding("NO_PROPERTY_PACKAGE", DiagnosticSeverity.Blocker, "",
                    "The flowsheet has no property package, so nothing can be flashed.",
                    "Add one before anything else: WithPropertyPackage(name)."));
                return;
            }

            if (flowsheet.SelectedCompounds.Count == 0)
            {
                findings.Add(new Finding("NO_COMPOUNDS", DiagnosticSeverity.Blocker, "",
                    "The flowsheet has no compounds.",
                    "Add them before anything else: WithCompounds(names)."));
                return;
            }

            var unsolved = flowsheet.SimulationObjects.Values
                .Where(o => !o.Calculated && o.GraphicObject != null && o.GraphicObject.Active)
                .Select(o => o.GraphicObject.Tag)
                .Take(5)
                .ToList();

            if (unsolved.Count > 0)
            {
                findings.Add(new Finding("NOT_SOLVED_STEADY_STATE", DiagnosticSeverity.Warning, "",
                    "These objects have not been solved: " + string.Join(", ", unsolved) + ".",
                    "Solve the flowsheet at steady state first; dynamics integrates forward from that state."));
            }
        }

        private static void CheckIntegrator(IDynamicsIntegrator integrator, List<Finding> findings)
        {
            if (integrator.MonitoredVariables.Count == 0)
            {
                findings.Add(new Finding("NO_MONITORED_VARS", DiagnosticSeverity.Warning, "",
                    "Integrator '" + integrator.Description + "' records no variables, so the run will produce no series.",
                    "Add some: Dynamics.Integrator(name).Monitor(tag, propertyId)."));
            }

            var step = integrator.IntegrationStep.TotalSeconds;
            if (step > 0)
            {
                var steps = integrator.Duration.TotalSeconds / step;
                if (steps > 100000)
                {
                    findings.Add(new Finding("TOO_MANY_STEPS", DiagnosticSeverity.Blocker, "",
                        "The duration and step give " + Fmt(steps) + " integration steps.",
                        "Raise the integration step, or shorten the duration."));
                }
            }
        }

        private static void CheckSchedule(IFlowsheet flowsheet, IDynamicsSchedule schedule, List<Finding> findings)
        {
            if (schedule.UseCurrentStateAsInitial) return;

            if (string.IsNullOrEmpty(schedule.InitialFlowsheetStateID) ||
                !flowsheet.StoredSolutions.ContainsKey(schedule.InitialFlowsheetStateID))
            {
                findings.Add(new Finding("MISSING_INITIAL_STATE", DiagnosticSeverity.Blocker, "",
                    "Schedule '" + schedule.Description + "' starts from stored state '" +
                    schedule.InitialFlowsheetStateID + "', which does not exist.",
                    "Store one with Dynamics.StoreCurrentStateAs(id), or call UseCurrentStateAsInitial()."));
            }
        }

        private static void CheckPressureFlowNetwork(IFlowsheet flowsheet, List<Finding> findings)
        {
            var streams = flowsheet.SimulationObjects.Values
                .Where(o => o is IMaterialStream)
                .ToList();

            if (streams.Count == 0) return;

            var pressureSpecs = streams.Count(s => s.DynamicsSpec == DynEnums.DynamicsSpecType.Pressure);

            if (pressureSpecs == 0)
            {
                findings.Add(new Finding("NO_PRESSURE_SPEC", DiagnosticSeverity.Warning, "",
                    "No material stream is specified by pressure, so the pressure-flow network has nothing to resolve against.",
                    "Mark the boundary streams: stream.AsPressureSpec() on the product side, AsFlowSpec() on the feed."));
            }
            else if (pressureSpecs == streams.Count && streams.Count > 1)
            {
                findings.Add(new Finding("ALL_FLOW_SPECS", DiagnosticSeverity.Info, "",
                    "Every material stream is specified by pressure; no flow is being held.",
                    "Mark the feed with AsFlowSpec() if you meant to fix its flow rate."));
            }
        }

        private static void CheckObjects(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var tag = obj.GraphicObject != null ? obj.GraphicObject.Tag : obj.Name;

                if (!obj.SupportsDynamicMode && !(obj is IMaterialStream))
                {
                    findings.Add(new Finding("UNSUPPORTED_OBJECT", DiagnosticSeverity.Info, tag,
                        SafeType(obj) + " has no dynamic model; it is solved at steady state on every step.",
                        "Nothing to do, unless you expected it to hold up material."));
                }

                var valve = obj as Valve;
                if (valve != null)
                {
                    if (valve.Kv <= 0.0)
                    {
                        findings.Add(new Finding("VALVE_NO_KV", DiagnosticSeverity.Warning, tag,
                            "The valve has no flow coefficient (Kv = " + Fmt(valve.Kv) + ").",
                            "Set one: valve.WithKv(kv). Without it the valve cannot pass a computed flow."));
                    }

                    if (valve.CalcMode == Valve.CalculationMode.DeltaP ||
                        valve.CalcMode == Valve.CalculationMode.OutletPressure)
                    {
                        findings.Add(new Finding("VALVE_PRESSURE_DROP_MODE", DiagnosticSeverity.Warning, tag,
                            "The valve is in " + valve.CalcMode +
                            " mode, so it cannot compute its own flow and demands a flow specification on one side.",
                            "Switch to a Kv mode: valve.WithCalcMode(Valve.CalculationMode.Kv_Liquid)."));
                    }

                    if (!valve.EnableOpeningKvRelationship)
                    {
                        findings.Add(new Finding("VALVE_OPENING_IGNORED", DiagnosticSeverity.Warning, tag,
                            "The valve passes its full Kv at any opening, so closing it does not stop the flow.",
                            "Enable the characteristic: valve.WithOpeningKvRelationship(). A controller " +
                            "manipulating the opening has no effect without it."));
                    }
                }

                var vessel = obj as Vessel;
                if (vessel != null && DynamicValue(obj, "Volume") <= 0.0)
                {
                    findings.Add(new Finding("VESSEL_NO_VOLUME", DiagnosticSeverity.Warning, tag,
                        "The vessel has no volume, so it holds nothing up and adds no lag.",
                        "Set one: vessel.WithVolume(2.CubicMetres()) — or use the geometry."));
                }

                var tank = obj as Tank;
                if (tank != null && tank.Volume <= 0.0)
                {
                    findings.Add(new Finding("VESSEL_NO_VOLUME", DiagnosticSeverity.Warning, tag,
                        "The tank has no volume, so it holds nothing up and adds no lag.",
                        "Set one: tank.WithVolume(...) and tank.WithHeight(...)."));
                }
            }
        }

        private static void CheckControllers(IFlowsheet flowsheet, List<Finding> findings)
        {
            foreach (var pid in flowsheet.SimulationObjects.Values.OfType<PIDController>())
            {
                var tag = pid.GraphicObject != null ? pid.GraphicObject.Tag : pid.Name;
                var info = new ControllerInfo(pid, tag);

                if (!info.IsWired)
                {
                    findings.Add(new Finding("PID_UNBOUND", DiagnosticSeverity.Blocker, tag,
                        "The controller is missing its process or manipulated variable.",
                        "Wire it: Controls(tag, propertyId) and Manipulates(tag, propertyId)."));
                }

                if (pid.OutputMin >= pid.OutputMax)
                {
                    findings.Add(new Finding("PID_LIMITS_INVALID", DiagnosticSeverity.Blocker, tag,
                        "The output minimum (" + Fmt(pid.OutputMin) + ") is not below the maximum (" +
                        Fmt(pid.OutputMax) + ").",
                        "Set them to the manipulated variable's physical range, e.g. WithOutputLimits(0, 100)."));
                }

                if (!pid.Active || pid.ManualOverride)
                {
                    findings.Add(new Finding("PID_INACTIVE", DiagnosticSeverity.Warning, tag,
                        pid.Active ? "The controller is in manual." : "The controller is switched off.",
                        "Put it in automatic: Active(true) and ManualOverride(false)."));
                }
            }
        }

        // ------------------------------------------------------------- Post-run

        private static void DiagnoseSeries(DynamicsSeries series, List<Finding> findings)
        {
            if (series.Count == 0) return;

            if (series.Values.Any(double.IsNaN) || series.Values.Any(double.IsInfinity))
            {
                findings.Add(new Finding("NAN_IN_SERIES", DiagnosticSeverity.Blocker, series.ObjectTag,
                    "'" + series.Name + "' contains NaN or infinity.",
                    "Reduce the integration step, or check that the object feeding it is initialised."));
                return;
            }

            if (series.HasDiverged)
            {
                findings.Add(new Finding("DIVERGENT", DiagnosticSeverity.Blocker, series.ObjectTag,
                    "'" + series.Name + "' grew without bound (max = " + Fmt(series.Max) + ").",
                    "Reduce the integration step; if a controller drives it, check ReverseActing and the gains."));
                return;
            }

            double period, decay;
            if (series.IsOscillating(out period, out decay) && (double.IsNaN(decay) || decay > 0.7))
            {
                findings.Add(new Finding("SUSTAINED_OSCILLATION", DiagnosticSeverity.Warning, series.ObjectTag,
                    "'" + series.Name + "' oscillates with a period of " + Fmt(period) +
                    " s and is not decaying" + (double.IsNaN(decay) ? "" : " (decay ratio " + Fmt(decay) + ")") + ".",
                    "Lower the proportional gain or raise the integral time; tune_pid does this automatically."));
            }

            var range = series.Max - series.Min;
            if (range > 0)
            {
                for (var i = 1; i < series.Count; i++)
                {
                    if (Math.Abs(series.Values[i] - series.Values[i - 1]) <= 0.5 * range) continue;
                    findings.Add(new Finding("STEP_TOO_LARGE_TRANSIENT", DiagnosticSeverity.Warning,
                        series.ObjectTag,
                        "'" + series.Name + "' jumps by more than half its range between adjacent steps, at t = " +
                        Fmt(series.TimeSeconds[i]) + " s.",
                        "Reduce the integration step; the transient is faster than the integrator resolves. " +
                        "A scheduled step change at that time is expected and can be ignored."));
                    break;
                }
            }

            if (!series.HasConverged() && !series.IsOscillating(out period, out decay))
            {
                findings.Add(new Finding("NOT_SETTLED", DiagnosticSeverity.Info, series.ObjectTag,
                    "'" + series.Name + "' had not settled by the end of the run.",
                    "Extend the duration if you need the steady-state value."));
            }
        }

        private static void DiagnoseControllers(IFlowsheet flowsheet, DynamicsResult result, List<Finding> findings)
        {
            foreach (var pid in flowsheet.SimulationObjects.Values.OfType<PIDController>())
            {
                var tag = pid.GraphicObject != null ? pid.GraphicObject.Tag : pid.Name;

                DynamicsSeries pv, mv;
                result.TryGetSeries(tag + "." + SafeProperty(pid.ControlledObjectData), out pv);
                result.TryGetSeries(FindManipulatedKey(result, pid), out mv);

                if (mv != null)
                {
                    var saturated = mv.SaturationFraction(pid.OutputMin, pid.OutputMax);
                    if (saturated > 0.9)
                    {
                        findings.Add(new Finding("MV_SATURATED", DiagnosticSeverity.Warning, tag,
                            "The manipulated variable sat at its limit for " +
                            Fmt(saturated * 100.0) + " % of the run.",
                            "The loop has no authority left: widen the output limits, or resize the final control element."));
                    }
                }

                if (pv != null && mv != null && pv.Count == mv.Count && pv.Count > 3)
                {
                    var wrongWay = 0;
                    var moves = 0;
                    for (var i = 1; i < pv.Count; i++)
                    {
                        var error = pv.Values[i - 1] - pid.SetPoint;
                        var move = mv.Values[i] - mv.Values[i - 1];
                        if (Math.Abs(move) < 1e-12 || Math.Abs(error) < 1e-12) continue;
                        moves += 1;
                        // Correct action moves the MV so that the error shrinks. Which sign that is
                        // depends on the loop, so this only flags a consistent one-way bias.
                        if (Math.Sign(error) == Math.Sign(move) * (pid.ReverseActing ? -1 : 1)) wrongWay += 1;
                    }

                    if (moves > 10 && (double)wrongWay / moves > 0.9)
                    {
                        findings.Add(new Finding("PID_ACTION_INVERTED", DiagnosticSeverity.Warning, tag,
                            "The controller moved its output in the direction that increases the error on " +
                            Fmt((double)wrongWay / moves * 100.0) + " % of its moves.",
                            "Flip ReverseActing on this controller."));
                    }
                }
            }
        }

        // -------------------------------------------------------------------------

        private static IReadOnlyList<Finding> Rank(List<Finding> findings)
        {
            return findings
                .OrderByDescending(f => (int)f.Severity)
                .ThenBy(f => f.Code, StringComparer.Ordinal)
                .ToList();
        }

        private static string FindManipulatedKey(DynamicsResult result, PIDController pid)
        {
            var data = pid.ManipulatedObjectData;
            if (data == null) return "";

            var series = result.Series.FirstOrDefault(
                s => string.Equals(s.ObjectId, data.ID, StringComparison.Ordinal) &&
                     string.Equals(s.PropertyId, data.PropertyName, StringComparison.Ordinal));

            return series == null ? "" : series.Name;
        }

        private static string SafeProperty(ISpecialOpObjectInfo info)
        {
            return info == null ? "" : info.PropertyName;
        }

        private static double DynamicValue(ISimulationObject obj, string name)
        {
            try
            {
                var value = obj.GetDynamicProperty(name);
                return value == null ? 0.0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch { return 0.0; }
        }

        private static string SafeType(ISimulationObject obj)
        {
            try { return obj.GetDisplayName(); }
            catch { return obj.GetType().Name; }
        }

        private static string Fmt(double value) => SeriesDecimator.Format(value);
    }
}
