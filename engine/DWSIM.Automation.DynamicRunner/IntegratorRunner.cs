using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.SpecialOps;

namespace DWSIM.Automation.DynamicRunner
{
    /// <summary>
    /// Thrown when a run is requested with <see cref="IntegratorRunOptions.FailIfBusy"/> set
    /// while another integration already holds <see cref="IntegratorRunner.Gate"/>.
    /// </summary>
    public class IntegratorBusyException : Exception
    {
        public IntegratorBusyException()
            : base("Another dynamic integration is already running in this process.") { }
    }

    /// <summary>Options for a single dynamic integration run.</summary>
    public sealed class IntegratorRunOptions
    {
        /// <summary>
        /// Schedule ID or description. When null, the flowsheet's current schedule is used,
        /// falling back to the first one defined.
        /// </summary>
        public string Schedule;

        /// <summary>
        /// Advance in wall-clock time instead of as fast as possible. Real-time runs only stop
        /// when aborted or when a step/time limit is reached.
        /// </summary>
        public bool RealTime;

        /// <summary>Restore the schedule's initial stored state before running.</summary>
        public bool RestoreInitialState = true;

        /// <summary>
        /// Carry on from where the last run stopped instead of starting over: keep the recorded
        /// history, the controllers' state and the integrator's clock, and pick the simulated time
        /// up where it was left. This is what a pause and a single step are made of.
        /// </summary>
        public bool Resume;

        /// <summary>
        /// Pace each step to one second of wall clock, whatever the integration step. Lets a run
        /// be watched against a real clock rather than as fast as the solver goes.
        /// </summary>
        public bool ClockSync;

        /// <summary>
        /// Solves the flowsheet at each step. Defaults to the standard flowsheet solver; a host
        /// with its own solver passes it here.
        /// </summary>
        public IFlowsheetSolver Solver;

        /// <summary>
        /// Keep a snapshot of the flowsheet at every step. Required by event transitions other
        /// than step changes; costs a snapshot plus compression per step.
        /// </summary>
        public bool EnableHistorian = true;

        /// <summary>Cancels the run at the next step boundary.</summary>
        public CancellationToken CancellationToken = CancellationToken.None;

        /// <summary>Polled every step; returning true stops the run.</summary>
        public Func<bool> AbortRequested;

        /// <summary>Stops the run after this many steps.</summary>
        public int? MaxSteps;

        /// <summary>Stops the run after this much wall-clock time has elapsed.</summary>
        public TimeSpan? MaxWallTime;

        /// <summary>
        /// Throw <see cref="IntegratorBusyException"/> instead of waiting when another
        /// integration is already running.
        /// </summary>
        public bool FailIfBusy;

        /// <summary>Reported once per step, before the step is solved.</summary>
        public Action<IntegratorProgress> OnProgress;

        /// <summary>Called after each step so the host can refresh its canvas.</summary>
        public Action OnStep;

        /// <summary>Called before each step is solved.</summary>
        public Action<IntegratorPreStepEventArgs> PreStep;

        /// <summary>Called after each step completes, with the monitored variable snapshot.</summary>
        public Action<IntegratorPostStepEventArgs> PostStep;
    }

    /// <summary>Progress of a running integration.</summary>
    public readonly struct IntegratorProgress
    {
        public IntegratorProgress(double currentSeconds, double totalSeconds, int step, string status)
        {
            CurrentSeconds = currentSeconds;
            TotalSeconds = totalSeconds;
            Step = step;
            Status = status;
        }

        /// <summary>Simulation time elapsed, in seconds from t = 0.</summary>
        public double CurrentSeconds { get; }

        /// <summary>Configured duration in seconds; <see cref="double.MaxValue"/> in real-time mode.</summary>
        public double TotalSeconds { get; }

        /// <summary>Zero-based step index.</summary>
        public int Step { get; }

        /// <summary>Human-readable status, e.g. "00:01:30/00:10:00".</summary>
        public string Status { get; }
    }

    /// <summary>Outcome of a dynamic integration run.</summary>
    public sealed class IntegratorRunResult
    {
        internal IntegratorRunResult(IDynamicsSchedule schedule, IDynamicsIntegrator integrator,
            int steps, double finalTimeSeconds, TimeSpan wallClock, bool aborted, List<Exception> exceptions)
        {
            Schedule = schedule;
            Integrator = integrator;
            Steps = steps;
            FinalTimeSeconds = finalTimeSeconds;
            WallClock = wallClock;
            Aborted = aborted;
            Exceptions = exceptions.AsReadOnly();
        }

        public IDynamicsSchedule Schedule { get; }

        public IDynamicsIntegrator Integrator { get; }

        /// <summary>Number of steps actually solved.</summary>
        public int Steps { get; }

        /// <summary>Simulation time reached, in seconds from t = 0.</summary>
        public double FinalTimeSeconds { get; }

        /// <summary>Wall-clock time the run took.</summary>
        public TimeSpan WallClock { get; }

        /// <summary>True when the run stopped on a cancellation, an abort request or a step/time limit.</summary>
        public bool Aborted { get; }

        /// <summary>Exceptions raised by the solver or by the loop; empty when the run was clean.</summary>
        public IReadOnlyList<Exception> Exceptions { get; }

        /// <summary>True when the run finished without exceptions and without being aborted.</summary>
        public bool Completed => Exceptions.Count == 0 && !Aborted;
    }

    /// <summary>
    /// Runs a dynamic-simulation schedule: integration strategy, controller execution order,
    /// historian, monitored variables, event list and cause-and-effect matrix.
    ///
    /// This is the single implementation of the integration loop. The GUI integrator panel, the
    /// PID tuning tool, the Fluent API and the automation servers all drive it from here.
    /// Progress and refresh are reported through callbacks so the loop itself stays synchronous —
    /// the PID tuner calls <see cref="Run"/> inside its objective function.
    /// </summary>
    public sealed class IntegratorRunner
    {
        /// <summary>
        /// Process-wide gate. Integration drives <c>GlobalSettings.Settings.CalculatorActivated</c>,
        /// <c>CalculatorBusy</c> and <c>SolverMode</c> — all global — and mutates the integrator's
        /// <c>CurrentTime</c> and <c>MonitoredVariableValues</c>. Concurrent runs corrupt each other,
        /// so every caller goes through here.
        /// </summary>
        public static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        private static int _running;

        /// <summary>True while an integration holds <see cref="Gate"/>.</summary>
        public static bool IsRunning => Volatile.Read(ref _running) > 0;

        private readonly IFlowsheet _flowsheet;

        /// <summary>Flowsheet snapshot taken at the start of a run, used to resolve event values.</summary>
        private IFlowsheet _flowsheetClone;

        private Dictionary<DateTime, string> _historian = new Dictionary<DateTime, string>();

        public IntegratorRunner(IFlowsheet flowsheet)
        {
            if (flowsheet == null) throw new ArgumentNullException(nameof(flowsheet));
            _flowsheet = flowsheet;
        }

        /// <summary>
        /// The flowsheet states recorded during the run, by simulation time. A host can step back
        /// through them; they survive between resumed runs and are dropped when a fresh one starts.
        /// </summary>
        public IReadOnlyDictionary<DateTime, string> History => _historian;

        /// <summary>Total size of the recorded states, in bytes.</summary>
        public long HistoryBytes
        {
            get
            {
                long total = 0;
                foreach (var state in _historian.Values) total += 22 + (long)state.Length * 2;
                return total;
            }
        }

        /// <summary>Drops the recorded states.</summary>
        public void ClearHistory()
        {
            _historian.Clear();
        }

        /// <summary>
        /// Puts the flowsheet back into the state recorded at <paramref name="timestamp"/>.
        /// </summary>
        /// <returns>False when no state was recorded at that time.</returns>
        public bool RestoreHistoryState(DateTime timestamp)
        {
            string state;
            if (!_historian.TryGetValue(timestamp, out state)) return false;

            _flowsheet.RestoreSnapshot(XDocument.Parse(state.Decompress()), SnapshotType.ObjectData);
            _flowsheet.UpdateInterface();
            return true;
        }

        /// <summary>The recorded time closest to, and not after, the one asked for.</summary>
        public DateTime? NearestRecordedTime(DateTime timestamp)
        {
            var candidates = _historian.Keys.Where(k => k <= timestamp).ToList();
            if (candidates.Count == 0) return null;
            return candidates.Max();
        }

        /// <summary>Runs the schedule to completion on the calling thread.</summary>
        public IntegratorRunResult Run(IntegratorRunOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (options.FailIfBusy)
            {
                if (!Gate.Wait(0)) throw new IntegratorBusyException();
            }
            else
            {
                Gate.Wait(options.CancellationToken);
            }

            Interlocked.Increment(ref _running);
            try
            {
                return RunCore(options);
            }
            finally
            {
                Interlocked.Decrement(ref _running);
                Gate.Release();
            }
        }

        /// <summary>Runs the schedule on a background thread.</summary>
        public Task<IntegratorRunResult> RunAsync(IntegratorRunOptions options)
        {
            return Task.Run(() => Run(options), CancellationToken.None);
        }

        // -------------------------------------------------------------------------

        private IntegratorRunResult RunCore(IntegratorRunOptions options)
        {
            var flowsheet = _flowsheet;
            var exceptions = new List<Exception>();

            var schedule = ResolveSchedule(flowsheet, options.Schedule);

            if (!flowsheet.DynamicsManager.IntegratorList.ContainsKey(schedule.CurrentIntegrator))
            {
                throw new Exception("Schedule " + Quote(schedule.Description) +
                    " has no integrator assigned. Assign one in the Dynamics Manager.");
            }

            var integrator = flowsheet.DynamicsManager.IntegratorList[schedule.CurrentIntegrator];
            integrator.RealTime = options.RealTime;

            var controllers = flowsheet.SimulationObjects.Values.OfType<PIDController>()
                .OrderBy(x => x.ExecutionOrder).ToList();
            var pyControllers = flowsheet.SimulationObjects.Values.OfType<PythonController>().ToList();
            var mpcControllers = flowsheet.SimulationObjects.Values.OfType<MPCController>()
                .OrderBy(x => x.ExecutionOrder).ToList();

            // A resumed run picks up the clock, the recorded history and the controllers' state
            // exactly as the paused one left them; only a fresh run starts any of that over.
            if (!options.Resume)
            {
                if (options.RestoreInitialState && !options.RealTime && !schedule.UseCurrentStateAsInitial)
                    RestoreState(flowsheet, schedule.InitialFlowsheetStateID);

                integrator.MonitoredVariableValues.Clear();
            }

            var interval = integrator.IntegrationStep.TotalSeconds;
            if (options.RealTime) interval = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0;
            if (interval <= 0.0) throw new Exception("The integration step must be greater than zero.");

            // In real-time mode the run only stops when aborted or limited.
            double final = options.RealTime ? double.MaxValue : integrator.Duration.TotalSeconds;

            if (!options.Resume)
            {
                foreach (var c in controllers) c.Reset();
                foreach (var m in mpcControllers) m.Reset();
                foreach (var c in pyControllers) c.ResetRequested = true;

                if (schedule.ResetContentsOfAllObjects) ResetObjectContents(flowsheet);

                integrator.CurrentTime = new DateTime();
            }

            double controllersCheck = 100000, streamsCheck = 100000, pfCheck = 100000;

            // Every global we touch is restored on the way out: a run must not leave the flowsheet
            // in a different mode, or pointing at a different schedule, than it found it.
            var previousDynamicMode = flowsheet.DynamicMode;
            var previousSchedule = flowsheet.DynamicsManager.CurrentSchedule;
            var previousSupress = flowsheet.SupressMessages;

            // Unit operations resolve their integrator through DynamicsManager.CurrentSchedule,
            // so it has to point at the schedule being run before the first step is solved.
            flowsheet.DynamicsManager.CurrentSchedule = schedule.ID;
            flowsheet.DynamicMode = true;
            flowsheet.SupressMessages = true;

            if (!options.Resume) _historian = new Dictionary<DateTime, string>();
            _flowsheetClone = null;

            // Only the event list needs the clone. A host that cannot clone its flowsheet still
            // gets a working integrator, just without event-driven property interpolation.
            if (options.EnableHistorian)
            {
                try { _flowsheetClone = flowsheet.Clone(); }
                catch { _flowsheetClone = null; }
            }

            var aborted = false;
            var steps = 0;

            // Simulated time already covered, which a resumed run carries on from.
            double i = options.Resume ? (integrator.CurrentTime - new DateTime()).TotalSeconds : 0;

            var runClock = Stopwatch.StartNew();

            try
            {
                flowsheet.ProcessScripts(Scripts.EventType.IntegratorStarted, Scripts.ObjectType.Integrator, "");

                while (i <= final)
                {
                    if (ShouldStop(options, steps, runClock)) { aborted = true; break; }

                    var i0 = (int)i;
                    var sw = Stopwatch.StartNew();

                    flowsheet.ProcessScripts(Scripts.EventType.IntegratorPreStep, Scripts.ObjectType.Integrator, "");

                    if (options.OnProgress != null)
                    {
                        options.OnProgress(new IntegratorProgress(i, final, steps,
                            new TimeSpan(0, 0, i0).ToString("c") + "/" + integrator.Duration.ToString("c")));
                    }

                    if (options.PreStep != null)
                    {
                        options.PreStep(new IntegratorPreStepEventArgs
                        {
                            status = "READY",
                            tstamp = integrator.CurrentTime,
                            tstep = steps,
                            flowsheet = flowsheet
                        });
                    }

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

                    var stepErrors = new List<Exception>();

                    DWSIM.DynamicsManager.IntegrationStrategies.ExecuteStep(
                        flowsheet,
                        integrator,
                        () =>
                        {
                            stepErrors = options.Solver != null
                                ? options.Solver.SolveFlowsheet(flowsheet)
                                : FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(
                                    flowsheet, DWSIM.GlobalSettings.Settings.SolverMode);
                            while (DWSIM.GlobalSettings.Settings.CalculatorBusy)
                                Task.Delay(200).Wait();
                        },
                        interval);

                    if (stepErrors != null && stepErrors.Count > 0)
                    {
                        exceptions.AddRange(stepErrors);
                        break;
                    }

                    if (options.EnableHistorian) StoreSnapshot(flowsheet, integrator.CurrentTime);

                    var variables = StoreVariableValues(flowsheet, integrator, integrator.CurrentTime);

                    flowsheet.ProcessScripts(Scripts.EventType.IntegratorStep, Scripts.ObjectType.Integrator, "");

                    if (options.PostStep != null)
                    {
                        options.PostStep(new IntegratorPostStepEventArgs
                        {
                            status = "OK",
                            tstamp = integrator.CurrentTime,
                            tstep = steps,
                            flowsheet = flowsheet,
                            variables = variables
                        });
                    }

                    if (options.OnStep != null) options.OnStep();

                    steps += 1;
                    integrator.CurrentTime = integrator.CurrentTime.AddSeconds(interval);

                    if (integrator.ShouldCalculateControl)
                        SolveControllers(flowsheet, controllers, pyControllers, mpcControllers);

                    var waittime = integrator.RealTimeStepMs - sw.ElapsedMilliseconds;
                    if (waittime > 0 && options.RealTime) Pace(waittime, options.CancellationToken);

                    // Clock sync paces to the wall clock rather than to the integrator's real-time
                    // step, so a run can be watched second by second whatever the step size.
                    var synctime = 1000 - sw.ElapsedMilliseconds;
                    if (synctime > 0 && options.ClockSync) Pace(synctime, options.CancellationToken);

                    sw.Stop();

                    if (!options.RealTime)
                    {
                        if (schedule.UsesEventList)
                            ProcessEvents(flowsheet, schedule.CurrentEventList, integrator.CurrentTime, integrator.IntegrationStep);
                        if (schedule.UsesCauseAndEffectMatrix)
                            ProcessCEMatrix(flowsheet, schedule.CurrentCauseAndEffectMatrix);
                    }

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
                runClock.Stop();
                flowsheet.SupressMessages = previousSupress;
                flowsheet.DynamicMode = previousDynamicMode;
                flowsheet.DynamicsManager.CurrentSchedule = previousSchedule;

                // On a windowed host the clone is a form, and it holds handles until disposed.
                var disposable = _flowsheetClone as IDisposable;
                if (disposable != null)
                {
                    try { disposable.Dispose(); }
                    catch { /* a host that cannot dispose its clone is no reason to fail the run */ }
                }
                _flowsheetClone = null;
            }

            return new IntegratorRunResult(schedule, integrator, steps, i, runClock.Elapsed, aborted, exceptions);
        }

        private static void Pace(long milliseconds, CancellationToken token)
        {
            try { Task.Delay((int)milliseconds, token).Wait(); }
            catch (Exception) when (token.IsCancellationRequested) { }
        }

        private static bool ShouldStop(IntegratorRunOptions options, int steps, Stopwatch clock)
        {
            if (options.CancellationToken.IsCancellationRequested) return true;
            if (options.AbortRequested != null && options.AbortRequested()) return true;
            if (options.MaxSteps.HasValue && steps >= options.MaxSteps.Value) return true;
            if (options.MaxWallTime.HasValue && clock.Elapsed >= options.MaxWallTime.Value) return true;
            return false;
        }

        private static string Quote(string s)
        {
            return "'" + s + "'";
        }

        /// <summary>
        /// Finds a schedule by ID, or by description ignoring case and surrounding whitespace.
        /// With no name given, falls back to the flowsheet's current schedule and then to the first one.
        /// </summary>
        public static IDynamicsSchedule ResolveSchedule(IFlowsheet flowsheet, string nameOrId)
        {
            var manager = flowsheet.DynamicsManager;

            if (manager.ScheduleList.Count == 0)
            {
                throw new Exception(
                    "This flowsheet has no dynamics schedule. Create one in the Dynamics Manager, or with the Fluent API.");
            }

            if (!string.IsNullOrWhiteSpace(nameOrId))
            {
                if (manager.ScheduleList.ContainsKey(nameOrId)) return manager.ScheduleList[nameOrId];

                var wanted = nameOrId.Trim();
                var match = manager.ScheduleList.Values.FirstOrDefault(
                    s => string.Equals((s.Description ?? "").Trim(), wanted, StringComparison.OrdinalIgnoreCase));

                if (match != null) return match;

                var available = string.Join(", ", manager.ScheduleList.Values.Select(s => Quote(s.Description)));
                throw new Exception("Schedule " + Quote(nameOrId) + " not found. Available schedules: " + available + ".");
            }

            if (!string.IsNullOrEmpty(manager.CurrentSchedule) && manager.ScheduleList.ContainsKey(manager.CurrentSchedule))
                return manager.ScheduleList[manager.CurrentSchedule];

            return manager.ScheduleList.Values.First();
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
        /// Snapshots the monitored variables and appends them to the integrator's history,
        /// keyed by timestamp ticks. Values are converted from SI to each variable's display units.
        /// </summary>
        public static List<IDynamicsMonitoredVariable> StoreVariableValues(
            IFlowsheet flowsheet, IDynamicsIntegrator integrator, DateTime tstamp)
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
            integrator.MonitoredVariableValues[tstamp.Ticks] = list;
            return list;
        }

        private void StoreSnapshot(IFlowsheet flowsheet, DateTime tstamp)
        {
            if (!flowsheet.DynamicsManager.EnableHistorian) return;

            _historian[tstamp] = flowsheet.GetSnapshot(SnapshotType.ObjectData).ToString().Compress();

            var max = flowsheet.DynamicsManager.MaxHistorianItems;
            if (max <= 0 || _historian.Count <= max) return;

            var stale = _historian.Keys.OrderBy(k => k).Take(_historian.Count - max).ToList();
            foreach (var key in stale) _historian.Remove(key);
        }

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
                var bobj = obj as DWSIM.SharedClasses.UnitOperations.BaseClass;
                if (bobj == null) continue;
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

        private void ProcessEvents(IFlowsheet flowsheet, string eventsetID, DateTime currentposition, TimeSpan interval)
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
                if (ev.EventType != Dynamics.DynamicsEventType.ChangeProperty) continue;
                if (!flowsheet.SimulationObjects.ContainsKey(ev.SimulationObjectID)) continue;
                var value = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(
                    ev.SimulationObjectPropertyUnits, ev.SimulationObjectPropertyValue.ToDoubleFromInvariant());
                flowsheet.SimulationObjects[ev.SimulationObjectID].SetPropertyValue(ev.SimulationObjectProperty, value);
            }
        }

        internal static void ProcessCEMatrix(IFlowsheet flowsheet, string cematrixID)
        {
            if (!flowsheet.DynamicsManager.CauseAndEffectMatrixList.ContainsKey(cematrixID)) return;
            var matrix = flowsheet.DynamicsManager.CauseAndEffectMatrixList[cematrixID];

            foreach (var item in matrix.Items.Values)
            {
                if (!item.Enabled) continue;
                if (!flowsheet.SimulationObjects.ContainsKey(item.AssociatedIndicator)) continue;
                var indicator = (IIndicator)flowsheet.SimulationObjects[item.AssociatedIndicator];

                bool fire;
                switch (item.AssociatedIndicatorAlarm)
                {
                    case Dynamics.DynamicsAlarmType.LL: fire = indicator.VeryLowAlarmActive; break;
                    case Dynamics.DynamicsAlarmType.L: fire = indicator.LowAlarmActive; break;
                    case Dynamics.DynamicsAlarmType.H: fire = indicator.HighAlarmActive; break;
                    case Dynamics.DynamicsAlarmType.HH: fire = indicator.VeryHighAlarmActive; break;
                    default: fire = false; break;
                }
                if (fire) DoAlarmEffect(flowsheet, item);
            }
        }

        internal static void DoAlarmEffect(IFlowsheet flowsheet, IDynamicsCauseAndEffectItem ceitem)
        {
            if (!flowsheet.SimulationObjects.ContainsKey(ceitem.SimulationObjectID)) return;
            var value = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(
                ceitem.SimulationObjectPropertyUnits, ceitem.SimulationObjectPropertyValue.ToDoubleFromInvariant());
            flowsheet.SimulationObjects[ceitem.SimulationObjectID].SetPropertyValue(ceitem.SimulationObjectProperty, value);
        }
    }
}
