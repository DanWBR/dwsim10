using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Interfaces;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Fluent builder for configuring and running a dynamic (time-domain) integration on a DWSIM flowsheet.
    /// Obtain an instance via <see cref="Flowsheet.RunDynamics"/>.
    /// </summary>
    public sealed class DynamicsBuilder
    {
        private readonly IFlowsheet _flowsheet;
        private string _scheduleName;
        private bool _realTime;
        private Runner.IntegratorPreStepEventHandler _preStep;
        private Runner.IntegratorPostStepEventHandler _postStep;

        internal DynamicsBuilder(IFlowsheet flowsheet, string scheduleName)
        {
            _flowsheet = flowsheet;
            _scheduleName = scheduleName;
        }

        /// <summary>
        /// Sets the dynamics schedule to run, identified by its description as configured in DWSIM.
        /// When not called, the first schedule in the flowsheet is used.
        /// </summary>
        public DynamicsBuilder WithSchedule(string name) { _scheduleName = name; return this; }

        /// <summary>
        /// Enables or disables real-time pacing. When true, each integration step is paced to the
        /// wall clock and the run continues indefinitely. Default is false (runs as fast as possible
        /// for the configured duration).
        /// </summary>
        public DynamicsBuilder WithRealTime(bool enabled = true) { _realTime = enabled; return this; }

        /// <summary>Registers a callback invoked before each integration step is solved.</summary>
        public DynamicsBuilder OnPreStep(Runner.IntegratorPreStepEventHandler handler)
        { _preStep += handler; return this; }

        /// <summary>Registers a callback invoked after each integration step completes.</summary>
        public DynamicsBuilder OnPostStep(Runner.IntegratorPostStepEventHandler handler)
        { _postStep += handler; return this; }

        /// <summary>
        /// Runs the integration asynchronously. Returns a <see cref="DynamicsResult"/> containing
        /// the monitored-variable time series once integration completes.
        /// </summary>
        public async Task<DynamicsResult> ExecuteAsync()
        {
            var data = new Dictionary<string, List<(double, double)>>();
            var epoch = new DateTime();

            Runner.IntegratorPostStepEventHandler collector = (s, e) =>
            {
                foreach (var v in e.variables)
                {
                    if (!data.TryGetValue(v.Description, out var series))
                        data[v.Description] = series = new List<(double, double)>();
                    double t = (e.tstamp - epoch).TotalSeconds;
                    double.TryParse(v.PropertyValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double val);
                    series.Add((t, val));
                }
            };

            if (_preStep != null) Runner.IntegratorPreStepEvent += _preStep;
            Runner.IntegratorPostStepEvent += collector;
            if (_postStep != null) Runner.IntegratorPostStepEvent += _postStep;

            Exception runError = null;
            try
            {
                var task = Runner.RunIntegrator(_flowsheet, ResolveScheduleName(), _realTime, waittofinish: false);
                await task.ConfigureAwait(false);
            }
            catch (Exception ex) { runError = ex; }
            finally
            {
                if (_preStep != null) Runner.IntegratorPreStepEvent -= _preStep;
                Runner.IntegratorPostStepEvent -= collector;
                if (_postStep != null) Runner.IntegratorPostStepEvent -= _postStep;
            }

            return new DynamicsResult(data, runError == null, runError);
        }

        /// <summary>
        /// Runs the integration synchronously, blocking until it completes.
        /// Returns a <see cref="DynamicsResult"/> containing the monitored-variable time series.
        /// </summary>
        public DynamicsResult Execute() => ExecuteAsync().GetAwaiter().GetResult();

        private string ResolveScheduleName()
        {
            if (_scheduleName != null) return _scheduleName;
            foreach (var s in _flowsheet.DynamicsManager.ScheduleList.Values)
                return s.Description;
            throw new InvalidOperationException("No dynamics schedule found in the flowsheet. Configure one in DWSIM or call WithSchedule().");
        }
    }
}
