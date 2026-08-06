using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.SpecialOps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
    /// Provides static methods for running dynamic integration on a DWSIM flowsheet.
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
        /// The name (description) of the dynamics schedule to run. Must match a schedule defined in the flowsheet.
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
        /// <exception cref="Exception">Thrown if the specified schedule name is not found in the flowsheet.</exception>
        public static Task RunIntegrator(IFlowsheet Flowsheet, string dynschedule, bool realtime, bool waittofinish)
        {

            Flowsheet.DynamicMode = true;

            var schedule = Flowsheet.DynamicsManager.ScheduleList.Values.Where(s => s.Description.ToLower().Equals(dynschedule)).FirstOrDefault();

            if (schedule == null) throw new Exception("Specified Schedule not found");

            var integrator = Flowsheet.DynamicsManager.IntegratorList[schedule.CurrentIntegrator];

            integrator.RealTime = realtime;

            var Controllers = Flowsheet.SimulationObjects.Values.Where(x => x is PIDController)
                .Cast<PIDController>().OrderBy(x => x.ExecutionOrder).Cast<ISimulationObject>().ToList();
            var Controllers2 = Flowsheet.SimulationObjects.Values.Where(x => x is PythonController).ToList();
            var MPCControllers = Flowsheet.SimulationObjects.Values.Where(x => x is MPCController)
                .Cast<MPCController>().OrderBy(x => x.ExecutionOrder).ToList();

            if (!waittofinish)
                if (!realtime)
                    if (!schedule.UseCurrentStateAsInitial)
                        RestoreState(Flowsheet, schedule.InitialFlowsheetStateID);

            integrator.MonitoredVariableValues.Clear();

            var interval = integrator.IntegrationStep.TotalSeconds;

            if (realtime)
                interval = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0;

            double final;

            if (realtime)
                final = double.MaxValue;
            else
                final = integrator.Duration.TotalSeconds;

            foreach (PIDController controller in Controllers)
                controller.Reset();

            foreach (MPCController mpc in MPCControllers)
                mpc.Reset();

            foreach (PythonController controller in Controllers2)
                controller.ResetRequested = true;

            if (schedule.ResetContentsOfAllObjects)
            {
                foreach (var obj in Flowsheet.SimulationObjects.Values)
                {
                    if (obj.HasPropertiesForDynamicMode)
                    {
                        if (obj is DWSIM.SharedClasses.UnitOperations.BaseClass)
                        {
                            var bobj = (DWSIM.SharedClasses.UnitOperations.BaseClass)obj;
                            if (bobj.GetDynamicProperty("Reset Content") != null)
                                bobj.SetDynamicProperty("Reset Content", 1);
                            if (bobj.GetDynamicProperty("Reset Contents") != null)
                                bobj.SetDynamicProperty("Reset Contents", 1);
                            if (bobj.GetDynamicProperty("Initialize using Inlet Stream") != null)
                                bobj.SetDynamicProperty("Initialize using Inlet Stream", 1);
                            if (bobj.GetDynamicProperty("Initialize using Inlet Streams") != null)
                                bobj.SetDynamicProperty("Initialize using Inlet Streams", 1);
                        }
                    }
                }
            }

            integrator.CurrentTime = new DateTime();

            integrator.MonitoredVariableValues.Clear();

            double controllers_check = 100000;
            double streams_check = 100000;
            double pf_check = 100000;

            Flowsheet.SupressMessages = true;

            var exceptions = new List<Exception>();

            var maintask = new Task(() =>
            {
                int j = 0;

                double i = 0;

                Flowsheet.ProcessScripts(Scripts.EventType.IntegratorStarted, Scripts.ObjectType.Integrator, "");

                while (i <= final)
                {

                    int i0 = (int)i;

                    var sw = new Stopwatch();

                    sw.Start();

                    Flowsheet.ProcessScripts(Scripts.EventType.IntegratorPreStep, Scripts.ObjectType.FlowsheetObject, "");

                    var preargs = new IntegratorPreStepEventArgs
                    {
                        status = "READY",
                        tstamp = integrator.CurrentTime,
                        tstep = j,
                        flowsheet = Flowsheet
                    };

                    IntegratorPreStepEvent?.Invoke(Flowsheet, preargs);

                    controllers_check += interval;
                    streams_check += interval;
                    pf_check += interval;

                    if (controllers_check >= integrator.CalculationRateControl * interval)
                    {
                        controllers_check = 0.0;
                        integrator.ShouldCalculateControl = true;
                    }
                    else
                        integrator.ShouldCalculateControl = false;

                    if (streams_check >= integrator.CalculationRateEquilibrium * interval)
                    {
                        streams_check = 0.0;
                        integrator.ShouldCalculateEquilibrium = true;
                    }
                    else
                        integrator.ShouldCalculateEquilibrium = false;

                    if (pf_check >= integrator.CalculationRatePressureFlow * interval)
                    {
                        pf_check = 0.0;
                        integrator.ShouldCalculatePressureFlow = true;
                    }
                    else
                        integrator.ShouldCalculatePressureFlow = false;

                    GlobalSettings.Settings.CalculatorActivated = true;
                    GlobalSettings.Settings.CalculatorBusy = false;

                    DynamicsManager.IntegrationStrategies.ExecuteStep(
                        Flowsheet,
                        integrator,
                        () => {
                            exceptions = FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(Flowsheet, GlobalSettings.Settings.SolverMode);
                            while (GlobalSettings.Settings.CalculatorBusy)
                                Task.Delay(200).Wait();
                        },
                        interval);

                    if (exceptions.Count > 0) break;

                    StoreVariableValues(Flowsheet, (DynamicsManager.Integrator)integrator, j, integrator.CurrentTime);

                    Flowsheet.ProcessScripts(Scripts.EventType.IntegratorStep, Scripts.ObjectType.FlowsheetObject, "");

                    var postargs = new IntegratorPostStepEventArgs {
                        status = "OK",
                        tstamp = integrator.CurrentTime,
                        tstep = j,
                        flowsheet = Flowsheet,
                        variables = integrator.MonitoredVariableValues.Values.Last()
                    };

                    IntegratorPostStepEvent?.Invoke(Flowsheet, postargs);

                    integrator.CurrentTime = integrator.CurrentTime.AddSeconds(interval);

                    if (integrator.ShouldCalculateControl)
                    {
                        foreach (PIDController controller in Controllers)
                        {
                            if (controller.Active)
                            {
                                Flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationStarted, Scripts.ObjectType.FlowsheetObject, controller.Name);
                                try
                                {
                                    controller.Solve();
                                    Flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationFinished, Scripts.ObjectType.FlowsheetObject, controller.Name);
                                }
                                catch (Exception ex)
                                {
                                    Flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationError, Scripts.ObjectType.FlowsheetObject, controller.Name);
                                    throw ex;
                                }
                            }
                        }
                        foreach (PythonController controller in Controllers2)
                        {
                            if (controller.Active)
                            {
                                Flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationStarted, Scripts.ObjectType.FlowsheetObject, controller.Name);
                                try
                                {
                                    controller.Solve();
                                    Flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationFinished, Scripts.ObjectType.FlowsheetObject, controller.Name);
                                }
                                catch (Exception ex)
                                {
                                    Flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationError, Scripts.ObjectType.FlowsheetObject, controller.Name);
                                    throw ex;
                                }
                            }
                        }
                        foreach (MPCController mpc in MPCControllers)
                        {
                            if (mpc.Active)
                            {
                                try
                                {
                                    mpc.Solve();
                                }
                                catch (Exception ex)
                                {
                                    throw ex;
                                }
                            }
                        }
                    }

                    var waittime = integrator.RealTimeStepMs - sw.ElapsedMilliseconds;

                    if (waittime > 0 && realtime)
                        Task.Delay((int)waittime).Wait();

                    sw.Stop();

                    if (!realtime)
                    {
                        if (schedule.UsesEventList)
                            ProcessEvents(Flowsheet, schedule.CurrentEventList, integrator.CurrentTime, integrator.IntegrationStep);

                        if (schedule.UsesCauseAndEffectMatrix)
                            ProcessCEMatrix(Flowsheet, schedule.CurrentCauseAndEffectMatrix);
                    }

                    j += 1;

                    i += interval;

                }

                if (exceptions.Count > 0) throw exceptions[0];

            });

            maintask.ContinueWith(t =>
            {
                if (t.Exception != null)
                    Flowsheet.ProcessScripts(Scripts.EventType.IntegratorError, Scripts.ObjectType.Integrator, "");
                else
                    Flowsheet.ProcessScripts(Scripts.EventType.IntegratorFinished, Scripts.ObjectType.Integrator, "");

                Flowsheet.SupressMessages = false;
                Flowsheet.UpdateOpenEditForms();
                if (t.Exception != null)
                {
                    Exception baseexception;
                    foreach (var ex in t.Exception.Flatten().InnerExceptions)
                    {
                        string euid = Guid.NewGuid().ToString();
                        SharedClasses.ExceptionProcessing.ExceptionList.Exceptions.Add(euid, ex);
                        if (ex is AggregateException)
                        {
                            baseexception = ex.InnerException;
                            foreach (var iex in ((AggregateException)ex).Flatten().InnerExceptions)
                            {
                                while (iex.InnerException != null)
                                    baseexception = iex.InnerException;
                                Flowsheet.ShowMessage(baseexception.Message.ToString(), Interfaces.IFlowsheet.MessageType.GeneralError, euid);
                            }
                        }
                        else
                        {
                            baseexception = ex;
                            if (baseexception.InnerException != null)
                            {
                                while (baseexception.InnerException.InnerException != null)
                                {
                                    baseexception = baseexception.InnerException;
                                    if ((baseexception == null))
                                        break;
                                    if ((baseexception.InnerException == null))
                                        break;
                                }
                                Flowsheet.ShowMessage(baseexception.Message.ToString(), Interfaces.IFlowsheet.MessageType.GeneralError, euid);
                            }
                        }
                    }
                }

            });

            if (waittofinish)
                maintask.RunSynchronously(TaskScheduler.Default);
            else
                maintask.Start(TaskScheduler.Default);

            return maintask;
        }

        /// <summary>
        /// Restores a previously stored flowsheet state by its ID.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet whose state will be restored.</param>
        /// <param name="stateID">The key identifying the stored solution/state to restore.</param>
        public static void RestoreState(Interfaces.IFlowsheet Flowsheet, string stateID)
        {
            try
            {
                var initialstate = Flowsheet.StoredSolutions[stateID];
                Flowsheet.LoadProcessData(initialstate);
                Flowsheet.UpdateInterface();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Error Restoring State {0}: {1}", stateID, ex.Message));
            }
        }

        /// <summary>
        /// Evaluates all items in a Cause-and-Effect matrix and applies any triggered alarm effects.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet containing the simulation objects and dynamics manager.</param>
        /// <param name="cematrixID">The key of the Cause-and-Effect matrix to process.</param>
        public static void ProcessCEMatrix(Interfaces.IFlowsheet Flowsheet, string cematrixID)
        {
            var matrix = Flowsheet.DynamicsManager.CauseAndEffectMatrixList[cematrixID];

            foreach (var item in matrix.Items.Values)
            {
                if (item.Enabled)
                {
                    var indicator = (Interfaces.IIndicator)Flowsheet.SimulationObjects[item.AssociatedIndicator];
                    switch (item.AssociatedIndicatorAlarm)
                    {
                        case Interfaces.Enums.Dynamics.DynamicsAlarmType.LL:
                            if (indicator.VeryLowAlarmActive)
                                DoAlarmEffect(Flowsheet, item);
                            break;
                        case Interfaces.Enums.Dynamics.DynamicsAlarmType.L:
                            if (indicator.LowAlarmActive)
                                DoAlarmEffect(Flowsheet, item);
                            break;
                        case Interfaces.Enums.Dynamics.DynamicsAlarmType.H:
                            if (indicator.HighAlarmActive)
                                DoAlarmEffect(Flowsheet, item);
                            break;
                        case Interfaces.Enums.Dynamics.DynamicsAlarmType.HH:
                            if (indicator.VeryHighAlarmActive)
                                DoAlarmEffect(Flowsheet, item);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Applies the property change defined by a Cause-and-Effect item to the associated simulation object.
        /// The property value is converted from the item's units to SI before being set.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet containing the target simulation object.</param>
        /// <param name="ceitem">The Cause-and-Effect item describing the object, property, and value to apply.</param>
        public static void DoAlarmEffect(Interfaces.IFlowsheet Flowsheet, Interfaces.IDynamicsCauseAndEffectItem ceitem)
        {
            var obj = Flowsheet.SimulationObjects[ceitem.SimulationObjectID];
            var value = SharedClasses.SystemsOfUnits.Converter.ConvertToSI(ceitem.SimulationObjectPropertyUnits, ceitem.SimulationObjectPropertyValue.ToDoubleFromInvariant());
            obj.SetPropertyValue(ceitem.SimulationObjectProperty, value);
        }

        /// <summary>
        /// Snapshots the current values of all monitored variables and stores them in the integrator's history.
        /// Values are converted from SI to the variable's configured display units before storage.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet containing the monitored simulation objects.</param>
        /// <param name="integrator">The integrator whose monitored variable history will be updated.</param>
        /// <param name="tstep">The zero-based time step index used as the history key.</param>
        /// <param name="tstamp">The simulation timestamp to associate with this snapshot.</param>
        public static void StoreVariableValues(Interfaces.IFlowsheet Flowsheet, DynamicsManager.Integrator integrator, int tstep, DateTime tstamp)
        {
            List<Interfaces.IDynamicsMonitoredVariable> list = new List<Interfaces.IDynamicsMonitoredVariable>();

            foreach (DynamicsManager.MonitoredVariable v in integrator.MonitoredVariables)
            {
                var vnew = (DynamicsManager.MonitoredVariable)v.Clone();
                var sobj = Flowsheet.SimulationObjects[vnew.ObjectID];
                var cval = Convert.ToDouble(sobj.GetPropertyValue(vnew.PropertyID));
                vnew.PropertyValue = SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(vnew.PropertyUnits, cval).ToString(System.Globalization.CultureInfo.InvariantCulture);
                vnew.TimeStamp = tstamp;
                list.Add(vnew);
            }

            integrator.MonitoredVariableValues.Add(tstep, list);
        }

        /// <summary>
        /// Processes all scheduled events whose timestamps fall within the current integration step window
        /// and applies their effects (e.g. property changes) to the flowsheet.
        /// </summary>
        /// <param name="Flowsheet">The flowsheet to which event effects will be applied.</param>
        /// <param name="eventsetID">The key of the event set to process.</param>
        /// <param name="currentposition">The end of the current time window (exclusive upper bound).</param>
        /// <param name="interval">The length of the current integration step; defines the start of the window.</param>
        public static void ProcessEvents(Interfaces.IFlowsheet Flowsheet, string eventsetID, DateTime currentposition, TimeSpan interval)
        {
            var eventset = Flowsheet.DynamicsManager.EventSetList[eventsetID];

            var initialtime = currentposition - interval;

            var finaltime = currentposition;

            var events = eventset.Events.Values.Where(x => x.TimeStamp >= initialtime & x.TimeStamp < finaltime).ToList();

            foreach (var ev in events)
            {
                if (ev.Enabled)
                {
                    switch (ev.EventType)
                    {
                        case Interfaces.Enums.Dynamics.DynamicsEventType.ChangeProperty:
                            var obj = Flowsheet.SimulationObjects[ev.SimulationObjectID];
                            var value = SharedClasses.SystemsOfUnits.Converter.ConvertToSI(ev.SimulationObjectPropertyUnits, ev.SimulationObjectPropertyValue.ToDoubleFromInvariant());
                            obj.SetPropertyValue(ev.SimulationObjectProperty, value);
                            break;
                        case Interfaces.Enums.Dynamics.DynamicsEventType.RunScript:
                            break;
                    }
                }
            }
        }

    }
}
