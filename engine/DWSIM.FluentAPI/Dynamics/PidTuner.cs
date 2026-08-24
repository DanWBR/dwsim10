using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DotNumerics.Optimization;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Interfaces;
using DWSIM.UnitOperations.SpecialOps;

namespace DWSIM.Automation.FluentAPI.Dynamics
{
    /// <summary>What the tuner minimises.</summary>
    public enum TuningObjective
    {
        /// <summary>Integral of the absolute error. The usual default: balanced and readable.</summary>
        IAE,
        /// <summary>Integral of the squared error. Punishes large excursions harder.</summary>
        ISE,
        /// <summary>Time-weighted absolute error. Punishes slow settling hardest.</summary>
        ITAE,
        /// <summary>Sum of the controllers' own accumulated error. What the GUI tuning tool uses.</summary>
        CumulativeError
    }

    /// <summary>How to tune, and what to tune.</summary>
    public sealed class PidTuningOptions
    {
        /// <summary>Schedule to run for each trial; the current or first one when null.</summary>
        public string ScheduleName;

        /// <summary>Tags of the controllers to tune. All of them when null or empty.</summary>
        public IReadOnlyList<string> ControllerTags;

        /// <summary>Simplex function-evaluation budget. Each evaluation runs the whole schedule once.</summary>
        public int MaxEvaluations = 30;

        /// <summary>What to minimise.</summary>
        public TuningObjective Objective = TuningObjective.IAE;

        /// <summary>Upper bound on Kp, as a multiple of its starting value.</summary>
        public double KpMaxFactor = 10.0;

        /// <summary>Upper bound on Ki.</summary>
        public double KiMax = 100.0;

        /// <summary>Upper bound on Kd.</summary>
        public double KdMax = 100.0;

        /// <summary>Leave the tuned gains on the controllers. When false, the originals are restored.</summary>
        public bool Apply = true;

        /// <summary>Polled between trials; returning true stops the search.</summary>
        public Func<bool> AbortRequested;

        /// <summary>Receives one line per trial.</summary>
        public Action<string> OnProgress;

        /// <summary>Wall-clock limit for a single trial run.</summary>
        public TimeSpan? MaxWallTimePerRun;
    }

    /// <summary>The gains found for one controller.</summary>
    public sealed class TunedController
    {
        internal TunedController(string tag, double kp, double ki, double kd,
            double originalKp, double originalKi, double originalKd)
        {
            Tag = tag;
            Kp = kp;
            Ki = ki;
            Kd = kd;
            OriginalKp = originalKp;
            OriginalKi = originalKi;
            OriginalKd = originalKd;
        }

        /// <summary>The controller's tag.</summary>
        public string Tag { get; }

        /// <summary>Tuned proportional gain.</summary>
        public double Kp { get; }

        /// <summary>Tuned integral gain.</summary>
        public double Ki { get; }

        /// <summary>Tuned derivative gain.</summary>
        public double Kd { get; }

        /// <summary>Proportional gain before tuning.</summary>
        public double OriginalKp { get; }

        /// <summary>Integral gain before tuning.</summary>
        public double OriginalKi { get; }

        /// <summary>Derivative gain before tuning.</summary>
        public double OriginalKd { get; }

        /// <summary>Returns <c>"tag: Kp = ..., Ki = ..., Kd = ..."</c>.</summary>
        public override string ToString()
        {
            return Tag + ": Kp = " + F(Kp) + ", Ki = " + F(Ki) + ", Kd = " + F(Kd);
        }

        private static string F(double v) => v.ToString("G6", CultureInfo.InvariantCulture);
    }

    /// <summary>Outcome of a tuning run.</summary>
    public sealed class PidTuningResult
    {
        internal PidTuningResult(IReadOnlyList<TunedController> controllers, double initialObjective,
            double finalObjective, int evaluations, bool applied, bool aborted, IReadOnlyList<string> log,
            Exception error)
        {
            Controllers = controllers;
            InitialObjective = initialObjective;
            FinalObjective = finalObjective;
            Evaluations = evaluations;
            Applied = applied;
            Aborted = aborted;
            Log = log;
            Error = error;
        }

        /// <summary>The gains found, one entry per tuned controller.</summary>
        public IReadOnlyList<TunedController> Controllers { get; }

        /// <summary>The objective at the starting gains.</summary>
        public double InitialObjective { get; }

        /// <summary>The objective at the gains found.</summary>
        public double FinalObjective { get; }

        /// <summary>How many trial runs the search used.</summary>
        public int Evaluations { get; }

        /// <summary>Whether the tuned gains were left on the controllers.</summary>
        public bool Applied { get; }

        /// <summary>Whether the search was cut short.</summary>
        public bool Aborted { get; }

        /// <summary>One line per trial.</summary>
        public IReadOnlyList<string> Log { get; }

        /// <summary>What stopped the search, or null.</summary>
        public Exception Error { get; }

        /// <summary>Relative improvement in the objective, as a percentage. Negative means it got worse.</summary>
        public double ImprovementPercent =>
            Math.Abs(InitialObjective) < 1e-30
                ? 0.0
                : (InitialObjective - FinalObjective) / Math.Abs(InitialObjective) * 100.0;

        /// <summary>True when the search completed and improved on the starting gains.</summary>
        public bool Succeeded => Error == null && FinalObjective <= InitialObjective;
    }

    /// <summary>
    /// Tunes PID controllers by simulation: a Nelder-Mead simplex over their gains, running the
    /// whole schedule once per trial and scoring the resulting transient.
    /// </summary>
    /// <remarks>
    /// Trials are only comparable if they all start from the same state, so the schedule needs a
    /// stored initial state. When it has none, one is taken from the flowsheet as it stands and the
    /// schedule's configuration is put back afterwards.
    /// </remarks>
    public static class PidTuner
    {
        private const string BaselineStateId = "__pid_tuning_baseline";

        /// <summary>Runs the search.</summary>
        public static PidTuningResult Tune(IFlowsheet flowsheet, PidTuningOptions options)
        {
            if (flowsheet == null) throw new ArgumentNullException(nameof(flowsheet));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var log = new List<string>();
            var controllers = SelectControllers(flowsheet, options.ControllerTags);

            if (controllers.Count == 0)
            {
                return new PidTuningResult(new TunedController[0], 0, 0, 0, false, false, log,
                    new InvalidOperationException("No PID controller to tune on this flowsheet."));
            }

            var unwired = controllers
                .Where(c => c.ControlledObjectData == null || c.ManipulatedObjectData == null ||
                            string.IsNullOrEmpty(c.ControlledObjectData.ID) ||
                            string.IsNullOrEmpty(c.ManipulatedObjectData.ID))
                .Select(Tag)
                .ToList();

            if (unwired.Count > 0)
            {
                return new PidTuningResult(new TunedController[0], 0, 0, 0, false, false, log,
                    new InvalidOperationException(
                        "These controllers have no process or manipulated variable wired: " +
                        string.Join(", ", unwired) + "."));
            }

            var schedule = IntegratorRunner.ResolveSchedule(flowsheet, options.ScheduleName);
            var originals = controllers.Select(c => new[] { c.Kp, c.Ki, c.Kd }).ToList();

            // Trials must all start from the same place or their scores are not comparable.
            var previousStateId = schedule.InitialFlowsheetStateID;
            var previousUseCurrent = schedule.UseCurrentStateAsInitial;
            var addedBaseline = false;

            if (schedule.UseCurrentStateAsInitial || string.IsNullOrEmpty(schedule.InitialFlowsheetStateID) ||
                !flowsheet.StoredSolutions.ContainsKey(schedule.InitialFlowsheetStateID))
            {
                flowsheet.StoredSolutions[BaselineStateId] = flowsheet.GetProcessData();
                schedule.InitialFlowsheetStateID = BaselineStateId;
                schedule.UseCurrentStateAsInitial = false;
                addedBaseline = true;
                Report(options, log, "No initial state on schedule '" + schedule.Description +
                                     "'; captured the current one as the tuning baseline.");
            }

            var evaluations = 0;
            var aborted = false;
            Exception error = null;
            double[] best = null;
            var initialObjective = double.NaN;

            try
            {
                OptMultivariateFunction objective = x =>
                {
                    if (options.AbortRequested != null && options.AbortRequested())
                    {
                        aborted = true;
                        return double.MaxValue;
                    }

                    evaluations += 1;
                    var score = Evaluate(flowsheet, schedule, controllers, x, options, log, evaluations);
                    if (double.IsNaN(initialObjective)) initialObjective = score;
                    return score;
                };

                var variables = new List<OptSimplexBoundVariable>();
                foreach (var c in controllers)
                {
                    variables.Add(new OptSimplexBoundVariable(c.Kp, 0.0, Math.Max(c.Kp * options.KpMaxFactor, 1e-6)));
                    variables.Add(new OptSimplexBoundVariable(c.Ki, 0.0, options.KiMax));
                    variables.Add(new OptSimplexBoundVariable(c.Kd, 0.0, options.KdMax));
                }

                var simplex = new Simplex { MaxFunEvaluations = options.MaxEvaluations };
                best = simplex.ComputeMin(objective, variables.ToArray());
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                if (addedBaseline)
                {
                    schedule.InitialFlowsheetStateID = previousStateId;
                    schedule.UseCurrentStateAsInitial = previousUseCurrent;
                    flowsheet.StoredSolutions.Remove(BaselineStateId);
                }
            }

            var finalObjective = double.NaN;
            var tuned = new List<TunedController>();

            if (best != null && error == null)
            {
                finalObjective = Score(flowsheet, controllers, options);

                for (var i = 0; i < controllers.Count; i++)
                {
                    tuned.Add(new TunedController(Tag(controllers[i]),
                        best[i * 3], best[i * 3 + 1], best[i * 3 + 2],
                        originals[i][0], originals[i][1], originals[i][2]));
                }

                Apply(controllers, options.Apply ? best : Flatten(originals));
            }
            else
            {
                Apply(controllers, Flatten(originals));
            }

            return new PidTuningResult(tuned, initialObjective, finalObjective, evaluations,
                options.Apply && error == null, aborted, log, error);
        }

        // -------------------------------------------------------------------------

        private static double Evaluate(IFlowsheet flowsheet, IDynamicsSchedule schedule,
            List<PIDController> controllers, double[] gains, PidTuningOptions options,
            List<string> log, int evaluation)
        {
            IntegratorRunner.RestoreState(flowsheet, schedule.InitialFlowsheetStateID);
            Apply(controllers, gains);

            var run = new IntegratorRunner(flowsheet).Run(new IntegratorRunOptions
            {
                Schedule = schedule.ID,
                RealTime = false,
                // The state was just restored; restoring again would undo the gains we just applied.
                RestoreInitialState = false,
                // Hot path: a snapshot plus compression per step would dominate the cost, and
                // nothing here interpolates event transitions.
                EnableHistorian = false,
                MaxWallTime = options.MaxWallTimePerRun,
                AbortRequested = options.AbortRequested
            });

            double score;
            if (run.Exceptions.Count > 0)
            {
                // A gain set that breaks the solver is simply a bad one.
                score = double.MaxValue;
            }
            else
            {
                score = Score(flowsheet, controllers, options, run.Integrator);
            }

            var gainText = string.Join(", ", controllers.Select((c, i) =>
                Tag(c) + " Kp=" + F(gains[i * 3]) + " Ki=" + F(gains[i * 3 + 1]) + " Kd=" + F(gains[i * 3 + 2])));

            Report(options, log, "#" + evaluation + ": " + gainText + " -> " +
                                 (score == double.MaxValue ? "failed" : F(score)));

            return score;
        }

        private static double Score(IFlowsheet flowsheet, List<PIDController> controllers,
            PidTuningOptions options, IDynamicsIntegrator integrator = null)
        {
            if (options.Objective == TuningObjective.CumulativeError || integrator == null)
                return controllers.Sum(c => Math.Abs(c.CumulativeError));

            var series = DynamicsResult.ReadSeries(flowsheet, integrator);
            if (series.Count == 0) return controllers.Sum(c => Math.Abs(c.CumulativeError));

            var total = 0.0;
            foreach (var controller in controllers)
            {
                var data = controller.ControlledObjectData;
                if (data == null) continue;

                var pv = series.FirstOrDefault(
                    s => string.Equals(s.ObjectId, data.ID, StringComparison.Ordinal) &&
                         string.Equals(s.PropertyId, data.PropertyName, StringComparison.Ordinal));

                // Without the process variable monitored there is nothing to integrate; the
                // controller's own accumulated error is the next best thing.
                if (pv == null) { total += Math.Abs(controller.CumulativeError); continue; }

                switch (options.Objective)
                {
                    case TuningObjective.ISE: total += pv.ISE(controller.SetPoint); break;
                    case TuningObjective.ITAE: total += pv.ITAE(controller.SetPoint); break;
                    default: total += pv.IAE(controller.SetPoint); break;
                }
            }

            return total;
        }

        private static List<PIDController> SelectControllers(IFlowsheet flowsheet, IReadOnlyList<string> tags)
        {
            var all = flowsheet.SimulationObjects.Values.OfType<PIDController>()
                .OrderBy(c => c.ExecutionOrder)
                .ToList();

            if (tags == null || tags.Count == 0) return all;

            var wanted = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            var selected = all.Where(c => wanted.Contains(Tag(c)) || wanted.Contains(c.Name)).ToList();

            var missing = wanted.Where(t =>
                !all.Any(c => string.Equals(Tag(c), t, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(c.Name, t, StringComparison.OrdinalIgnoreCase))).ToList();

            if (missing.Count > 0)
                throw new KeyNotFoundException("No PID controller tagged " +
                    string.Join(", ", missing.Select(m => "'" + m + "'")) + " on this flowsheet.");

            return selected;
        }

        private static void Apply(List<PIDController> controllers, double[] gains)
        {
            for (var i = 0; i < controllers.Count; i++)
            {
                controllers[i].Kp = gains[i * 3];
                controllers[i].Ki = gains[i * 3 + 1];
                controllers[i].Kd = gains[i * 3 + 2];
            }
        }

        private static double[] Flatten(List<double[]> gains)
        {
            return gains.SelectMany(g => g).ToArray();
        }

        private static void Report(PidTuningOptions options, List<string> log, string line)
        {
            log.Add(line);
            if (options.OnProgress != null) options.OnProgress(line);
        }

        private static string Tag(PIDController c)
        {
            return c.GraphicObject != null ? c.GraphicObject.Tag : c.Name;
        }

        private static string F(double v) => v.ToString("G6", CultureInfo.InvariantCulture);
    }
}
