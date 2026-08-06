using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.SpecialOps;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Runs a dynamic-simulation schedule. UI-agnostic port of the run loop that lives inside the
/// Eto <c>DynamicsIntegratorControl</c>: same integration strategy, controller execution order,
/// historian, monitored variables, event list and cause-and-effect matrix handling.
///
/// Progress and refresh are reported through callbacks so both the integrator panel and the
/// PID tuning tool can drive it; the tuner calls <see cref="Run"/> synchronously inside its
/// objective function, which is why the loop itself is not async.
/// </summary>
public static class DynamicsIntegratorRunner
{
    /// <summary>Flowsheet snapshot taken at the start of a run, used to resolve event values.</summary>
    private static IFlowsheet? _flowsheetClone;

    private static Dictionary<DateTime, string> _historian = new();

    public sealed class RunOptions
    {
        /// <summary>Advance in wall-clock time instead of as fast as possible.</summary>
        public bool RealTime;
        /// <summary>Restore the schedule's initial state before running.</summary>
        public bool RestoreInitialState = true;
        /// <summary>current seconds, total seconds, status text.</summary>
        public Action<int, int, string>? OnProgress;
        /// <summary>Called after each step so the host can refresh the canvas.</summary>
        public Action? OnStep;
        /// <summary>Polled every step; returning true stops the run.</summary>
        public Func<bool>? AbortRequested;
    }

    /// <summary>Reloads a stored flowsheet state (the schedule's starting point).</summary>
    public static void RestoreState(IFlowsheet flowsheet, string stateID)
    {
        if (string.IsNullOrEmpty(stateID)) return;
        if (!flowsheet.StoredSolutions.ContainsKey(stateID)) return;
        flowsheet.LoadProcessData(flowsheet.StoredSolutions[stateID]);
        flowsheet.UpdateInterface();
    }

    /// <summary>
    /// Runs the current schedule to completion on the calling thread.
    /// Returns the exceptions raised by the flowsheet solver, empty when the run was clean.
    /// </summary>
    public static List<Exception> Run(IFlowsheet flowsheet, RunOptions options)
    {
        var exceptions = new List<Exception>();

        if (!flowsheet.DynamicsManager.ScheduleList.ContainsKey(flowsheet.DynamicsManager.CurrentSchedule))
        {
            flowsheet.ShowMessage("Please select a schedule first.", IFlowsheet.MessageType.GeneralError);
            return exceptions;
        }

        var schedule = flowsheet.DynamicsManager.ScheduleList[flowsheet.DynamicsManager.CurrentSchedule];

        if (!flowsheet.DynamicsManager.IntegratorList.ContainsKey(schedule.CurrentIntegrator))
        {
            flowsheet.ShowMessage("Please select an integrator first.", IFlowsheet.MessageType.GeneralError);
            return exceptions;
        }

        var integrator = flowsheet.DynamicsManager.IntegratorList[schedule.CurrentIntegrator];
        integrator.RealTime = options.RealTime;

        var controllers = flowsheet.SimulationObjects.Values.OfType<PIDController>()
            .OrderBy(x => x.ExecutionOrder).ToList();
        var pyControllers = flowsheet.SimulationObjects.Values.OfType<PythonController>().ToList();
        var mpcControllers = flowsheet.SimulationObjects.Values.OfType<MPCController>()
            .OrderBy(x => x.ExecutionOrder).ToList();

        if (options.RestoreInitialState && !options.RealTime && !schedule.UseCurrentStateAsInitial)
            RestoreState(flowsheet, schedule.InitialFlowsheetStateID);

        integrator.MonitoredVariableValues.Clear();

        var interval = integrator.IntegrationStep.TotalSeconds;
        if (options.RealTime) interval = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0;

        // In real-time mode the run only stops when aborted.
        double final = options.RealTime ? int.MaxValue : integrator.Duration.TotalSeconds;

        foreach (var c in controllers) c.Reset();
        foreach (var m in mpcControllers) m.Reset();
        foreach (var c in pyControllers) c.ResetRequested = true;

        if (schedule.ResetContentsOfAllObjects) ResetObjectContents(flowsheet);

        integrator.CurrentTime = new DateTime();

        double controllersCheck = 100000, streamsCheck = 100000, pfCheck = 100000;

        flowsheet.SupressMessages = true;
        _historian = new Dictionary<DateTime, string>();

        // Only the event list needs the clone. A host that cannot clone its flowsheet still
        // gets a working integrator, just without event-driven property interpolation.
        try { _flowsheetClone = flowsheet.Clone(); }
        catch { _flowsheetClone = null; }

        try
        {
            flowsheet.ProcessScripts(Scripts.EventType.IntegratorStarted, Scripts.ObjectType.Integrator, "");

            int j = 0;
            double i = 0;

            while (i <= final)
            {
                if (options.AbortRequested != null && options.AbortRequested()) break;

                var i0 = (int)i;
                var sw = Stopwatch.StartNew();

                flowsheet.ProcessScripts(Scripts.EventType.IntegratorPreStep, Scripts.ObjectType.Integrator, "");

                options.OnProgress?.Invoke(i0, (int)Math.Min(int.MaxValue, final),
                    new TimeSpan(0, 0, i0).ToString("c") + "/" + integrator.Duration.ToString("c"));

                controllersCheck += interval;
                streamsCheck += interval;
                pfCheck += interval;

                integrator.ShouldCalculateControl = controllersCheck >= integrator.CalculationRateControl * interval;
                if (integrator.ShouldCalculateControl) controllersCheck = 0.0;

                integrator.ShouldCalculateEquilibrium = streamsCheck >= integrator.CalculationRateEquilibrium * interval;
                if (integrator.ShouldCalculateEquilibrium) streamsCheck = 0.0;

                integrator.ShouldCalculatePressureFlow = pfCheck >= integrator.CalculationRatePressureFlow * interval;
                if (integrator.ShouldCalculatePressureFlow) pfCheck = 0.0;

                DWSIM.GlobalSettings.Settings.CalculatorActivated = true;
                DWSIM.GlobalSettings.Settings.CalculatorBusy = false;

                DWSIM.DynamicsManager.IntegrationStrategies.ExecuteStep(
                    flowsheet,
                    integrator,
                    () =>
                    {
                        exceptions = FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(
                            flowsheet, DWSIM.GlobalSettings.Settings.SolverMode);
                        while (DWSIM.GlobalSettings.Settings.CalculatorBusy)
                            Task.Delay(200).Wait();
                    },
                    interval);

                if (exceptions.Count > 0) break;

                _historian[integrator.CurrentTime] =
                    flowsheet.GetSnapshot(SnapshotType.ObjectData).ToString().Compress();

                StoreVariableValues(flowsheet, integrator, integrator.CurrentTime);

                flowsheet.ProcessScripts(Scripts.EventType.IntegratorStep, Scripts.ObjectType.Integrator, "");

                options.OnStep?.Invoke();

                integrator.CurrentTime = integrator.CurrentTime.AddSeconds(interval);

                if (integrator.ShouldCalculateControl)
                    SolveControllers(flowsheet, controllers, pyControllers, mpcControllers);

                var waittime = integrator.RealTimeStepMs - sw.ElapsedMilliseconds;
                if (waittime > 0 && options.RealTime) Task.Delay((int)waittime).Wait();
                sw.Stop();

                if (!options.RealTime)
                {
                    if (schedule.UsesEventList)
                        ProcessEvents(flowsheet, schedule.CurrentEventList, integrator.CurrentTime, integrator.IntegrationStep);
                    if (schedule.UsesCauseAndEffectMatrix)
                        ProcessCEMatrix(flowsheet, schedule.CurrentCauseAndEffectMatrix);
                }

                j += 1;
                i += interval;
            }

            flowsheet.ProcessScripts(
                exceptions.Count > 0 ? Scripts.EventType.IntegratorError : Scripts.EventType.IntegratorFinished,
                Scripts.ObjectType.Integrator, "");
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
            flowsheet.ProcessScripts(Scripts.EventType.IntegratorError, Scripts.ObjectType.Integrator, "");
        }
        finally
        {
            flowsheet.SupressMessages = false;
        }

        return exceptions;
    }

    // -------------------------------------------------------------------------

    private static void SolveControllers(IFlowsheet flowsheet,
        List<PIDController> controllers, List<PythonController> pyControllers, List<MPCController> mpcControllers)
    {
        foreach (var controller in controllers)
        {
            if (!controller.Active) continue;
            flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationStarted, Scripts.ObjectType.FlowsheetObject, controller.Name);
            try
            {
                controller.Solve();
                flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationFinished, Scripts.ObjectType.FlowsheetObject, controller.Name);
            }
            catch
            {
                flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationError, Scripts.ObjectType.FlowsheetObject, controller.Name);
                throw;
            }
        }
        foreach (var controller in pyControllers)
        {
            if (!controller.Active) continue;
            flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationStarted, Scripts.ObjectType.FlowsheetObject, controller.Name);
            try
            {
                controller.Solve();
                flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationFinished, Scripts.ObjectType.FlowsheetObject, controller.Name);
            }
            catch
            {
                flowsheet.ProcessScripts(Scripts.EventType.ObjectCalculationError, Scripts.ObjectType.FlowsheetObject, controller.Name);
                throw;
            }
        }
        foreach (var mpc in mpcControllers)
        {
            if (mpc.Active) mpc.Solve();
        }
    }

    private static void ResetObjectContents(IFlowsheet flowsheet)
    {
        foreach (var obj in flowsheet.SimulationObjects.Values)
        {
            if (!obj.HasPropertiesForDynamicMode) continue;
            if (obj is not DWSIM.SharedClasses.UnitOperations.BaseClass bobj) continue;
            foreach (var prop in new[]
                     {
                         "Reset Content", "Reset Contents",
                         "Initialize using Inlet Stream", "Initialize using Inlet Streams"
                     })
            {
                if (bobj.GetDynamicProperty(prop) != null) bobj.SetDynamicProperty(prop, 1);
            }
        }
    }

    private static void StoreVariableValues(IFlowsheet flowsheet, IDynamicsIntegrator integrator, DateTime tstamp)
    {
        var list = new List<IDynamicsMonitoredVariable>();
        foreach (DWSIM.DynamicsManager.MonitoredVariable v in integrator.MonitoredVariables)
        {
            var vnew = (DWSIM.DynamicsManager.MonitoredVariable)v.Clone();
            if (!flowsheet.SimulationObjects.ContainsKey(vnew.ObjectID)) continue;
            var sobj = flowsheet.SimulationObjects[vnew.ObjectID];
            vnew.PropertyValue = DWSIM.SharedClasses.SystemsOfUnits.Converter
                .ConvertFromSI(vnew.PropertyUnits, Convert.ToDouble(sobj.GetPropertyValue(vnew.PropertyID)))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            vnew.TimeStamp = tstamp;
            list.Add(vnew);
        }
        integrator.MonitoredVariableValues.Add(tstamp.Ticks, list);
    }

    private static void ProcessEvents(IFlowsheet flowsheet, string eventsetID, DateTime currentposition, TimeSpan interval)
    {
        if (!flowsheet.DynamicsManager.EventSetList.ContainsKey(eventsetID)) return;
        var eventset = flowsheet.DynamicsManager.EventSetList[eventsetID];

        var initialtime = currentposition - interval;
        var events = eventset.Events.Values
            .Where(x => x.TimeStamp >= initialtime && x.TimeStamp < currentposition).ToList();

        if (_flowsheetClone != null)
        {
            var props = flowsheet.DynamicsManager.GetPropertyValuesFromEvents(
                _flowsheetClone, currentposition, _historian, eventset);

            foreach (var p in props)
            {
                if (!flowsheet.SimulationObjects.ContainsKey(p.Item1)) continue;
                flowsheet.SimulationObjects[p.Item1].SetPropertyValue(p.Item2, p.Item3);
            }
        }

        foreach (var ev in events)
        {
            if (!ev.Enabled) continue;
            if (ev.EventType != Interfaces.Enums.Dynamics.DynamicsEventType.ChangeProperty) continue;
            if (!flowsheet.SimulationObjects.ContainsKey(ev.SimulationObjectID)) continue;
            var value = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(
                ev.SimulationObjectPropertyUnits, ev.SimulationObjectPropertyValue.ToDoubleFromInvariant());
            flowsheet.SimulationObjects[ev.SimulationObjectID].SetPropertyValue(ev.SimulationObjectProperty, value);
        }
    }

    private static void ProcessCEMatrix(IFlowsheet flowsheet, string cematrixID)
    {
        if (!flowsheet.DynamicsManager.CauseAndEffectMatrixList.ContainsKey(cematrixID)) return;
        var matrix = flowsheet.DynamicsManager.CauseAndEffectMatrixList[cematrixID];

        foreach (var item in matrix.Items.Values)
        {
            if (!item.Enabled) continue;
            if (!flowsheet.SimulationObjects.ContainsKey(item.AssociatedIndicator)) continue;
            var indicator = (IIndicator)flowsheet.SimulationObjects[item.AssociatedIndicator];

            var fire = item.AssociatedIndicatorAlarm switch
            {
                Interfaces.Enums.Dynamics.DynamicsAlarmType.LL => indicator.VeryLowAlarmActive,
                Interfaces.Enums.Dynamics.DynamicsAlarmType.L => indicator.LowAlarmActive,
                Interfaces.Enums.Dynamics.DynamicsAlarmType.H => indicator.HighAlarmActive,
                Interfaces.Enums.Dynamics.DynamicsAlarmType.HH => indicator.VeryHighAlarmActive,
                _ => false
            };
            if (fire) DoAlarmEffect(flowsheet, item);
        }
    }

    private static void DoAlarmEffect(IFlowsheet flowsheet, IDynamicsCauseAndEffectItem ceitem)
    {
        if (!flowsheet.SimulationObjects.ContainsKey(ceitem.SimulationObjectID)) return;
        var value = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(
            ceitem.SimulationObjectPropertyUnits, ceitem.SimulationObjectPropertyValue.ToDoubleFromInvariant());
        flowsheet.SimulationObjects[ceitem.SimulationObjectID].SetPropertyValue(ceitem.SimulationObjectProperty, value);
    }
}
