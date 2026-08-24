using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.DynamicsManager;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Entry point for configuring a flowsheet's dynamic simulation: integrators, schedules,
    /// event sets and cause-and-effect matrices. Obtain one from <see cref="Flowsheet.Dynamics"/>.
    /// </summary>
    /// <remarks>
    /// Every <c>Define*</c> method is idempotent by name: it creates the object on first call and
    /// returns the existing one afterwards, so a configuration script can be re-run safely.
    /// </remarks>
    /// <example>
    /// <code>
    /// fs.Dynamics
    ///   .DefineIntegrator("Fast")
    ///       .WithIntegrationStep(1.Seconds())
    ///       .WithDuration(10.Minutes())
    ///       .Monitor("TK-01", "Liquid Level", "m");
    ///
    /// fs.Dynamics.DefineSchedule("Startup").WithIntegrator("Fast").MakeCurrent();
    /// </code>
    /// </example>
    public sealed class DynamicsConfigBuilder
    {
        private readonly Flowsheet _flowsheet;

        internal DynamicsConfigBuilder(Flowsheet flowsheet)
        {
            _flowsheet = flowsheet;
        }

        internal IFlowsheet Inner => _flowsheet.Inner;

        private IDynamicsManager Manager => _flowsheet.Inner.DynamicsManager;

        // --------------------------------------------------------------- Integrators

        /// <summary>Creates an integrator with this description, or returns the existing one.</summary>
        public IntegratorBuilder DefineIntegrator(string name)
        {
            var existing = Manager.GetIntegrator(name);
            if (existing == null)
            {
                existing = new Integrator { ID = Guid.NewGuid().ToString(), Description = name };
                Manager.IntegratorList.Add(existing.ID, existing);
            }
            return new IntegratorBuilder(_flowsheet, existing);
        }

        /// <summary>Returns a builder for an existing integrator; throws when there is none by that name.</summary>
        public IntegratorBuilder Integrator(string name)
        {
            var found = Manager.GetIntegrator(name);
            if (found == null) throw NotFound("integrator", name, IntegratorNames);
            return new IntegratorBuilder(_flowsheet, found);
        }

        /// <summary>Descriptions of every integrator defined on this flowsheet.</summary>
        public IReadOnlyList<string> IntegratorNames =>
            Manager.IntegratorList.Values.Select(x => x.Description).ToList();

        // ----------------------------------------------------------------- Schedules

        /// <summary>Creates a schedule with this description, or returns the existing one.</summary>
        public ScheduleBuilder DefineSchedule(string name)
        {
            var existing = Manager.GetSchedule(name);
            if (existing == null)
            {
                existing = new Schedule { ID = Guid.NewGuid().ToString(), Description = name };
                Manager.ScheduleList.Add(existing.ID, existing);
            }
            return new ScheduleBuilder(_flowsheet, existing);
        }

        /// <summary>Returns a builder for an existing schedule; throws when there is none by that name.</summary>
        public ScheduleBuilder Schedule(string name)
        {
            var found = Manager.GetSchedule(name);
            if (found == null) throw NotFound("schedule", name, ScheduleNames);
            return new ScheduleBuilder(_flowsheet, found);
        }

        /// <summary>Descriptions of every schedule defined on this flowsheet.</summary>
        public IReadOnlyList<string> ScheduleNames =>
            Manager.ScheduleList.Values.Select(x => x.Description).ToList();

        /// <summary>Makes this schedule the current one, which is what a run defaults to.</summary>
        public DynamicsConfigBuilder UseSchedule(string name)
        {
            Schedule(name).MakeCurrent();
            return this;
        }

        // ---------------------------------------------------------------- Event sets

        /// <summary>Creates an event set with this description, or returns the existing one.</summary>
        public EventSetBuilder DefineEventSet(string name)
        {
            var existing = Manager.GetEventSet(name);
            if (existing == null)
            {
                existing = new EventSet { ID = Guid.NewGuid().ToString(), Description = name };
                Manager.EventSetList.Add(existing.ID, existing);
            }
            return new EventSetBuilder(_flowsheet, existing);
        }

        /// <summary>Returns a builder for an existing event set; throws when there is none by that name.</summary>
        public EventSetBuilder EventSet(string name)
        {
            var found = Manager.GetEventSet(name);
            if (found == null) throw NotFound("event set", name, EventSetNames);
            return new EventSetBuilder(_flowsheet, found);
        }

        /// <summary>Descriptions of every event set defined on this flowsheet.</summary>
        public IReadOnlyList<string> EventSetNames =>
            Manager.EventSetList.Values.Select(x => x.Description).ToList();

        // ------------------------------------------------------- Cause-and-effect

        /// <summary>Creates a cause-and-effect matrix with this description, or returns the existing one.</summary>
        public CauseAndEffectMatrixBuilder DefineCauseAndEffectMatrix(string name)
        {
            var existing = Manager.GetCauseAndEffectMatrix(name);
            if (existing == null)
            {
                existing = new CauseAndEffectMatrix { ID = Guid.NewGuid().ToString(), Description = name };
                Manager.CauseAndEffectMatrixList.Add(existing.ID, existing);
            }
            return new CauseAndEffectMatrixBuilder(_flowsheet, existing);
        }

        /// <summary>Returns a builder for an existing matrix; throws when there is none by that name.</summary>
        public CauseAndEffectMatrixBuilder CauseAndEffectMatrix(string name)
        {
            var found = Manager.GetCauseAndEffectMatrix(name);
            if (found == null) throw NotFound("cause-and-effect matrix", name, CauseAndEffectMatrixNames);
            return new CauseAndEffectMatrixBuilder(_flowsheet, found);
        }

        /// <summary>Descriptions of every cause-and-effect matrix defined on this flowsheet.</summary>
        public IReadOnlyList<string> CauseAndEffectMatrixNames =>
            Manager.CauseAndEffectMatrixList.Values.Select(x => x.Description).ToList();

        // -------------------------------------------------------------------- Misc

        /// <summary>
        /// Controls the state historian. It is what event transitions other than step changes
        /// interpolate from, at the cost of a compressed snapshot per integration step.
        /// </summary>
        public DynamicsConfigBuilder WithHistorian(bool enabled = true, int maxItems = 1000)
        {
            Manager.EnableHistorian = enabled;
            Manager.MaxHistorianItems = maxItems;
            return this;
        }

        /// <summary>
        /// Stores the flowsheet's current state under <paramref name="stateId"/>, so a schedule can
        /// start every run from it. See <see cref="ScheduleBuilder.WithInitialState"/>.
        /// </summary>
        public DynamicsConfigBuilder StoreCurrentStateAs(string stateId)
        {
            Inner.StoredSolutions[stateId] = Inner.GetProcessData();
            return this;
        }

        /// <summary>Turns dynamic mode on or off, and makes sure every object has its dynamic properties.</summary>
        public DynamicsConfigBuilder EnableDynamicMode(bool enabled = true)
        {
            Inner.DynamicMode = enabled;
            if (enabled)
            {
                foreach (var obj in Inner.SimulationObjects.Values)
                    PropertyCatalog.EnsureDynamicProperties(obj);
            }
            return this;
        }

        internal ISimulationObject Resolve(string tag) => _flowsheet.ResolveByTag(tag);

        internal IUnitsOfMeasure Units => Inner.FlowsheetOptions.SelectedUnitSystem;

        private static Exception NotFound(string kind, string name, IReadOnlyList<string> available)
        {
            var list = available.Count == 0
                ? "none are defined"
                : string.Join(", ", available.Select(x => "'" + x + "'"));
            return new KeyNotFoundException(
                "No " + kind + " named '" + name + "' on this flowsheet. Available: " + list + ".");
        }
    }
}
