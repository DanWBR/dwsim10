using System;
using System.Threading;
using System.Threading.Tasks;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Interfaces;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Configures and runs a dynamic (time-domain) integration on a flowsheet.
    /// Obtain an instance via <see cref="Flowsheet.RunDynamics"/>.
    /// </summary>
    /// <remarks>
    /// Build the schedule itself through <see cref="Flowsheet.Dynamics"/>; this builder only
    /// decides how the run is executed.
    /// </remarks>
    public sealed class DynamicsBuilder
    {
        private readonly Flowsheet _flowsheet;
        private readonly IntegratorRunOptions _options = new IntegratorRunOptions();
        private Runner.IntegratorPreStepEventHandler _preStep;
        private Runner.IntegratorPostStepEventHandler _postStep;
        private Func<IFlowsheet, double, bool> _stopCondition;

        internal DynamicsBuilder(Flowsheet flowsheet, string scheduleName)
        {
            _flowsheet = flowsheet;
            _options.Schedule = scheduleName;
        }

        /// <summary>
        /// Sets the dynamics schedule to run, by description or ID. Matching on the description
        /// ignores case. When not called, the flowsheet's current schedule is used, falling back to
        /// the first one defined.
        /// </summary>
        public DynamicsBuilder WithSchedule(string name) { _options.Schedule = name; return this; }

        /// <summary>
        /// Enables or disables real-time pacing. When true, each integration step is paced to the
        /// wall clock and the run continues until stopped. Default is false (runs as fast as
        /// possible for the configured duration).
        /// </summary>
        public DynamicsBuilder WithRealTime(bool enabled = true) { _options.RealTime = enabled; return this; }

        /// <summary>
        /// Keeps a snapshot of the flowsheet at every step. Event transitions other than step
        /// changes interpolate from it, so ramps need it; turning it off makes long runs faster.
        /// Default is true.
        /// </summary>
        public DynamicsBuilder WithHistorian(bool enabled = true) { _options.EnableHistorian = enabled; return this; }

        /// <summary>Starts the run from wherever the flowsheet is, ignoring the schedule's stored initial state.</summary>
        public DynamicsBuilder FromCurrentState(bool fromCurrent = true)
        { _options.RestoreInitialState = !fromCurrent; return this; }

        /// <summary>Stops the run when the token is cancelled, at the next step boundary.</summary>
        public DynamicsBuilder WithCancellation(CancellationToken token)
        { _options.CancellationToken = token; return this; }

        /// <summary>Stops the run after this many steps. The usual way to bound a real-time run.</summary>
        public DynamicsBuilder WithMaxSteps(int steps) { _options.MaxSteps = steps; return this; }

        /// <summary>Stops the run after this much wall-clock time, whatever the simulated progress.</summary>
        public DynamicsBuilder WithMaxWallTime(TimeSpan limit) { _options.MaxWallTime = limit; return this; }

        /// <summary>
        /// Fails instead of waiting when another integration is already running in this process.
        /// Integration drives global solver state, so runs are serialised.
        /// </summary>
        public DynamicsBuilder FailIfBusy(bool fail = true) { _options.FailIfBusy = fail; return this; }

        /// <summary>Registers a callback invoked before each integration step is solved.</summary>
        public DynamicsBuilder OnPreStep(Runner.IntegratorPreStepEventHandler handler)
        { _preStep += handler; return this; }

        /// <summary>Registers a callback invoked after each integration step completes.</summary>
        public DynamicsBuilder OnPostStep(Runner.IntegratorPostStepEventHandler handler)
        { _postStep += handler; return this; }

        /// <summary>Reports progress once per step: simulated time, step index and a status string.</summary>
        public DynamicsBuilder OnProgress(Action<IntegratorProgress> handler)
        { _options.OnProgress += handler; return this; }

        /// <summary>
        /// Stops the run cleanly when the predicate returns true, given the flowsheet and the
        /// simulated time in seconds. Checked before each step.
        /// </summary>
        public DynamicsBuilder StopWhen(Func<IFlowsheet, double, bool> predicate)
        { _stopCondition = predicate; return this; }

        /// <summary>Runs the integration asynchronously.</summary>
        public async Task<DynamicsResult> ExecuteAsync()
        {
            return await Task.Run(() => Execute()).ConfigureAwait(false);
        }

        /// <summary>Runs the integration synchronously, blocking until it completes.</summary>
        public DynamicsResult Execute()
        {
            var inner = _flowsheet.Inner;

            if (_preStep != null)
            {
                var handler = _preStep;
                _options.PreStep += e => handler(inner, e);
            }

            if (_postStep != null)
            {
                var handler = _postStep;
                _options.PostStep += e => handler(inner, e);
            }

            if (_stopCondition != null)
            {
                var predicate = _stopCondition;
                var integrator = new StopWatcher(inner, predicate);
                _options.PreStep += integrator.Observe;
                _options.AbortRequested = integrator.ShouldStop;
            }

            IntegratorRunResult run;
            try
            {
                run = new IntegratorRunner(inner).Run(_options);
            }
            catch (Exception ex)
            {
                // Configuration failures — no schedule, no integrator, zero step — throw before the
                // loop starts, and callers read them off the result like any other failure.
                return new DynamicsResult(new DynamicsSeries[0], _options.Schedule ?? "", "",
                    0, 0.0, TimeSpan.Zero, false, new[] { ex });
            }

            var series = DynamicsResult.ReadSeries(inner, run.Integrator);

            return new DynamicsResult(series,
                run.Schedule.Description, run.Integrator.Description,
                run.Steps, run.FinalTimeSeconds, run.WallClock, run.Aborted, run.Exceptions);
        }

        /// <summary>
        /// Bridges <see cref="StopWhen"/> to the runner's abort flag: the predicate needs the
        /// simulated time, which only the step callback carries.
        /// </summary>
        private sealed class StopWatcher
        {
            private readonly IFlowsheet _flowsheet;
            private readonly Func<IFlowsheet, double, bool> _predicate;
            private bool _stop;

            public StopWatcher(IFlowsheet flowsheet, Func<IFlowsheet, double, bool> predicate)
            {
                _flowsheet = flowsheet;
                _predicate = predicate;
            }

            public void Observe(IntegratorPreStepEventArgs e)
            {
                var seconds = (e.tstamp - new DateTime()).TotalSeconds;
                if (_predicate(_flowsheet, seconds)) _stop = true;
            }

            public bool ShouldStop() => _stop;
        }
    }
}
