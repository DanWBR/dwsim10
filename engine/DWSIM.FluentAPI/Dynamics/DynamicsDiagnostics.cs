using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.UnitOperations.SpecialOps;
using DWSIM.Automation.FluentAPI.Diagnostics;

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
        /// <remarks>
        /// The rules themselves live in <see cref="DWSIM.Automation.DynamicRunner.Setup.DynamicsReadiness"/>,
        /// one layer down, where the wizards in both user interfaces can reach them too. This maps
        /// what they report onto the finding type the rest of the Fluent API speaks.
        /// </remarks>
        public static IReadOnlyList<Finding> CheckReady(IFlowsheet flowsheet, string scheduleName = null)
        {
            if (flowsheet == null) throw new ArgumentNullException(nameof(flowsheet));

            return DWSIM.Automation.DynamicRunner.Setup.DynamicsReadiness
                .Check(flowsheet, scheduleName)
                .Select(ToFinding)
                .ToList();
        }

        private static Finding ToFinding(DWSIM.Automation.DynamicRunner.Setup.DynamicsIssue issue)
        {
            return new Finding(issue.Code, ToSeverity(issue.Severity), issue.ObjectTag, issue.Message, issue.Fix);
        }

        private static DiagnosticSeverity ToSeverity(DWSIM.Automation.DynamicRunner.Setup.DynamicsIssueSeverity severity)
        {
            switch (severity)
            {
                case DWSIM.Automation.DynamicRunner.Setup.DynamicsIssueSeverity.Blocker:
                    return DiagnosticSeverity.Blocker;
                case DWSIM.Automation.DynamicRunner.Setup.DynamicsIssueSeverity.Warning:
                    return DiagnosticSeverity.Warning;
                default:
                    return DiagnosticSeverity.Info;
            }
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



        private static string Fmt(double value) => SeriesDecimator.Format(value);
    }
}
