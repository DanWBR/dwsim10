using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DWSIM.Automation.DynamicRunner
{
    /// <summary>
    /// Event arguments supplied after each integrator time step has been solved.
    /// </summary>
    public class IntegratorPostStepEventArgs
    {
        /// <summary>The list of monitored variable snapshots captured at this time step.</summary>
        public List<Interfaces.IDynamicsMonitoredVariable> variables;

        /// <summary>The zero-based index of the current time step.</summary>
        public int tstep;

        /// <summary>The simulation timestamp corresponding to this time step.</summary>
        public DateTime tstamp;

        /// <summary>A string describing the solver status at this time step (e.g. "OK").</summary>
        public string status;

        /// <summary>The flowsheet being integrated.</summary>
        public IFlowsheet flowsheet;

    }

    /// <summary>
    /// Event arguments supplied before each integrator time step is solved.
    /// </summary>
    public class IntegratorPreStepEventArgs
    {
        /// <summary>The zero-based index of the upcoming time step.</summary>
        public int tstep;

        /// <summary>The simulation timestamp for the upcoming time step.</summary>
        public DateTime tstamp;

        /// <summary>A string describing the current integrator status (e.g. "READY").</summary>
        public string status;

        /// <summary>The flowsheet being integrated.</summary>
        public IFlowsheet flowsheet;

    }

    /// <summary>
    /// Static entry points for running dynamic integration on a DWSIM flowsheet.
    /// The integration loop itself lives in <see cref="IntegratorRunner"/>; this class is the
    /// process-wide event surface that COM clients and the Fluent API were written against.
    /// </summary>
    public class Runner
    {

        /// <summary>Delegate type for the <see cref="IntegratorPostStepEvent"/> event.</summary>
        public delegate void IntegratorPostStepEventHandler(object sender, IntegratorPostStepEventArgs e);

        /// <summary>Raised after each integrator time step completes successfully.</summary>
        public static event IntegratorPostStepEventHandler IntegratorPostStepEvent;

        /// <summary>Delegate type for the <see cref="IntegratorPreStepEvent"/> event.</summary>
        public delegate void IntegratorPreStepEventHandler(object sender, IntegratorPreStepEventArgs e);

        /// <summary>Raised before each integrator time step is solved.</summary>
        public static event IntegratorPreStepEventHandler IntegratorPreStepEvent;

        /// <summary>
        /// Runs the dynamic integrator for a given schedule on the specified flowsheet.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet to integrate.</param>
        /// <param name="dynschedule">
        /// The name (description) or ID of the dynamics schedule to run. Matching on the description
        /// ignores case and surrounding whitespace. When null or empty, the flowsheet's current
        /// schedule is used, falling back to the first one defined.
        /// </param>
        /// <param name="realtime">
        /// If <c>true</c>, the integrator runs in real-time mode, pacing each step to the wall clock.
        /// If <c>false</c>, it runs as fast as possible for the configured duration.
        /// </param>
        /// <param name="waittofinish">
        /// If <c>true</c>, the method blocks until integration is complete.
        /// If <c>false</c>, integration runs in a background task and the method returns immediately.
        /// </param>
        /// <returns>The <see cref="Task"/> representing the integration run.</returns>
        /// <exception cref="Exception">Thrown if the schedule is not found, or if the run fails.</exception>
        public static Task RunIntegrator(IFlowsheet Flowsheet, string dynschedule, bool realtime, bool waittofinish)
        {
            var options = new IntegratorRunOptions
            {
                Schedule = dynschedule,
                RealTime = realtime
            };

            if (waittofinish)
            {
                var result = RunAndThrow(Flowsheet, options);
                return Task.FromResult(result);
            }

            return Task.Run(() => RunAndThrow(Flowsheet, options));
        }

        /// <summary>
        /// Runs the dynamic integrator with full control over the run: schedule selection,
        /// cancellation, step and wall-time limits, historian and progress callbacks.
        /// Unlike the legacy overload, this one reports failures through
        /// <see cref="IntegratorRunResult.Exceptions"/> instead of throwing.
        /// </summary>
        public static Task<IntegratorRunResult> RunIntegrator(IFlowsheet Flowsheet, IntegratorRunOptions options)
        {
            return new IntegratorRunner(Flowsheet).RunAsync(WithStaticEvents(Flowsheet, options));
        }

        private static IntegratorRunResult RunAndThrow(IFlowsheet flowsheet, IntegratorRunOptions options)
        {
            var result = new IntegratorRunner(flowsheet).Run(WithStaticEvents(flowsheet, options));
            if (result.Exceptions.Count > 0) throw result.Exceptions[0];
            return result;
        }

        /// <summary>
        /// Chains the process-wide <see cref="IntegratorPreStepEvent"/> and
        /// <see cref="IntegratorPostStepEvent"/> onto the run's own callbacks, so subscribers keep
        /// receiving steps whichever entry point started the run.
        /// </summary>
        private static IntegratorRunOptions WithStaticEvents(IFlowsheet flowsheet, IntegratorRunOptions options)
        {
            var callerPreStep = options.PreStep;
            var callerPostStep = options.PostStep;

            options.PreStep = e =>
            {
                if (callerPreStep != null) callerPreStep(e);
                var handler = IntegratorPreStepEvent;
                if (handler != null) handler(flowsheet, e);
            };

            options.PostStep = e =>
            {
                if (callerPostStep != null) callerPostStep(e);
                var handler = IntegratorPostStepEvent;
                if (handler != null) handler(flowsheet, e);
            };

            return options;
        }

        /// <summary>
        /// Restores a previously stored flowsheet state by its ID.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet whose state will be restored.</param>
        /// <param name="stateID">The key identifying the stored solution/state to restore.</param>
        public static void RestoreState(Interfaces.IFlowsheet Flowsheet, string stateID)
        {
            IntegratorRunner.RestoreState(Flowsheet, stateID);
        }

        /// <summary>
        /// Evaluates all items in a Cause-and-Effect matrix and applies any triggered alarm effects.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet containing the simulation objects and dynamics manager.</param>
        /// <param name="cematrixID">The key of the Cause-and-Effect matrix to process.</param>
        public static void ProcessCEMatrix(Interfaces.IFlowsheet Flowsheet, string cematrixID)
        {
            IntegratorRunner.ProcessCEMatrix(Flowsheet, cematrixID);
        }

        /// <summary>
        /// Applies the property change defined by a Cause-and-Effect item to the associated simulation object.
        /// The property value is converted from the item's units to SI before being set.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet containing the target simulation object.</param>
        /// <param name="ceitem">The Cause-and-Effect item describing the object, property, and value to apply.</param>
        public static void DoAlarmEffect(Interfaces.IFlowsheet Flowsheet, Interfaces.IDynamicsCauseAndEffectItem ceitem)
        {
            IntegratorRunner.DoAlarmEffect(Flowsheet, ceitem);
        }

        /// <summary>
        /// Snapshots the current values of all monitored variables and stores them in the integrator's history.
        /// Values are converted from SI to the variable's configured display units before storage.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet containing the monitored simulation objects.</param>
        /// <param name="integrator">The integrator whose monitored variable history will be updated.</param>
        /// <param name="tstep">Unused; kept for source compatibility. History is keyed by timestamp ticks.</param>
        /// <param name="tstamp">The simulation timestamp to associate with this snapshot.</param>
        public static void StoreVariableValues(Interfaces.IFlowsheet Flowsheet, DynamicsManager.Integrator integrator, int tstep, DateTime tstamp)
        {
            IntegratorRunner.StoreVariableValues(Flowsheet, integrator, tstamp);
        }

        /// <summary>
        /// Processes all scheduled events whose timestamps fall within the current integration step window
        /// and applies their effects (e.g. property changes) to the flowsheet.
        /// Step changes only: transitions that interpolate from a past state need the historian, which
        /// only exists inside a run.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet to which event effects will be applied.</param>
        /// <param name="eventsetID">The key of the event set to process.</param>
        /// <param name="currentposition">The end of the current time window (exclusive upper bound).</param>
        /// <param name="interval">The length of the current integration step; defines the start of the window.</param>
        public static void ProcessEvents(Interfaces.IFlowsheet Flowsheet, string eventsetID, DateTime currentposition, TimeSpan interval)
        {
            if (!Flowsheet.DynamicsManager.EventSetList.ContainsKey(eventsetID)) return;

            var eventset = Flowsheet.DynamicsManager.EventSetList[eventsetID];

            var initialtime = currentposition - interval;

            foreach (var ev in eventset.Events.Values)
            {
                if (!ev.Enabled) continue;
                if (ev.TimeStamp < initialtime || ev.TimeStamp >= currentposition) continue;
                if (ev.EventType != Interfaces.Enums.Dynamics.DynamicsEventType.ChangeProperty) continue;
                if (!Flowsheet.SimulationObjects.ContainsKey(ev.SimulationObjectID)) continue;

                var value = SharedClasses.SystemsOfUnits.Converter.ConvertToSI(
                    ev.SimulationObjectPropertyUnits,
                    ev.SimulationObjectPropertyValue.ToDoubleFromInvariant());

                Flowsheet.SimulationObjects[ev.SimulationObjectID].SetPropertyValue(ev.SimulationObjectProperty, value);
            }
        }

    }
}
