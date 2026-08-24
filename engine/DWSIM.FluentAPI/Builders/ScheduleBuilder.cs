using System;
using System.Collections.Generic;
using DWSIM.Interfaces;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Configures a dynamics schedule: which integrator runs, which event set and cause-and-effect
    /// matrix are active, and what state the run starts from.
    /// Obtain one from <see cref="DynamicsConfigBuilder.DefineSchedule"/>.
    /// </summary>
    public sealed class ScheduleBuilder
    {
        private readonly Flowsheet _flowsheet;

        internal ScheduleBuilder(Flowsheet flowsheet, IDynamicsSchedule schedule)
        {
            _flowsheet = flowsheet;
            Object = schedule;
        }

        /// <summary>The underlying DWSIM schedule.</summary>
        public IDynamicsSchedule Object { get; }

        /// <summary>The schedule's description, which is how a run refers to it.</summary>
        public string Name => Object.Description;

        /// <summary>The schedule's internal ID.</summary>
        public string Id => Object.ID;

        /// <summary>Assigns the integrator this schedule runs, by its description.</summary>
        public ScheduleBuilder WithIntegrator(string integratorName)
        {
            var integrator = _flowsheet.Inner.DynamicsManager.GetIntegrator(integratorName);
            if (integrator == null)
                throw new KeyNotFoundException("No integrator named '" + integratorName + "' on this flowsheet.");
            Object.CurrentIntegrator = integrator.ID;
            return this;
        }

        /// <summary>Activates an event set on this schedule, by its description.</summary>
        public ScheduleBuilder WithEventSet(string eventSetName)
        {
            var set = _flowsheet.Inner.DynamicsManager.GetEventSet(eventSetName);
            if (set == null)
                throw new KeyNotFoundException("No event set named '" + eventSetName + "' on this flowsheet.");
            Object.CurrentEventList = set.ID;
            Object.UsesEventList = true;
            return this;
        }

        /// <summary>Stops running the event set, without deleting it.</summary>
        public ScheduleBuilder WithoutEventSet()
        {
            Object.UsesEventList = false;
            return this;
        }

        /// <summary>Activates a cause-and-effect matrix on this schedule, by its description.</summary>
        public ScheduleBuilder WithCauseAndEffectMatrix(string matrixName)
        {
            var matrix = _flowsheet.Inner.DynamicsManager.GetCauseAndEffectMatrix(matrixName);
            if (matrix == null)
                throw new KeyNotFoundException("No cause-and-effect matrix named '" + matrixName + "' on this flowsheet.");
            Object.CurrentCauseAndEffectMatrix = matrix.ID;
            Object.UsesCauseAndEffectMatrix = true;
            return this;
        }

        /// <summary>Stops evaluating the cause-and-effect matrix, without deleting it.</summary>
        public ScheduleBuilder WithoutCauseAndEffectMatrix()
        {
            Object.UsesCauseAndEffectMatrix = false;
            return this;
        }

        /// <summary>
        /// Starts each run from wherever the flowsheet happens to be, instead of from a stored state.
        /// This is the default, and the only option when no state has been stored.
        /// </summary>
        public ScheduleBuilder UseCurrentStateAsInitial(bool use = true)
        {
            Object.UseCurrentStateAsInitial = use;
            return this;
        }

        /// <summary>
        /// Starts each run from a stored state, which is what makes repeated runs comparable —
        /// PID tuning depends on it. Store one with <see cref="DynamicsConfigBuilder.StoreCurrentStateAs"/>.
        /// </summary>
        public ScheduleBuilder WithInitialState(string storedStateId)
        {
            if (!_flowsheet.Inner.StoredSolutions.ContainsKey(storedStateId))
                throw new KeyNotFoundException(
                    "No stored state named '" + storedStateId + "'. Call Dynamics.StoreCurrentStateAs first.");
            Object.InitialFlowsheetStateID = storedStateId;
            Object.UseCurrentStateAsInitial = false;
            return this;
        }

        /// <summary>Empties the hold-up of every object with dynamic content at the start of each run.</summary>
        public ScheduleBuilder ResetContentsOfAllObjects(bool reset = true)
        {
            Object.ResetContentsOfAllObjects = reset;
            return this;
        }

        /// <summary>Makes this the flowsheet's current schedule, which is what a run defaults to.</summary>
        public ScheduleBuilder MakeCurrent()
        {
            _flowsheet.Inner.DynamicsManager.CurrentSchedule = Object.ID;
            return this;
        }

        /// <summary>Returns a <see cref="DynamicsBuilder"/> that will run this schedule.</summary>
        public DynamicsBuilder Run() => _flowsheet.RunDynamics(Object.ID);

        /// <summary>Escape hatch: applies an arbitrary mutation to the underlying schedule.</summary>
        public ScheduleBuilder Configure(Action<IDynamicsSchedule> action)
        {
            if (action != null) action(Object);
            return this;
        }
    }
}
