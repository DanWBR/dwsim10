using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.SpecialOps;
using DWSIM.UnitOperations.UnitOperations;
using DynEnums = DWSIM.Interfaces.Enums.Dynamics;

namespace DWSIM.Automation.DynamicRunner.Setup
{
    /// <summary>
    /// Answers "is this flowsheet ready to run dynamically?".
    /// </summary>
    /// <remarks>
    /// The rules encode the mistakes that make a dynamic run fail or mislead: an underdetermined
    /// pressure-flow network, a valve with no Kv, a vessel with no volume, a controller wired
    /// backwards, a calculation mode the dynamic model refuses. Each issue carries a fix, so a
    /// caller can act on it without knowing the model.
    ///
    /// This is the single source of the readiness rules. The Fluent API's DynamicsDiagnostics maps
    /// these onto its own Finding type, and the wizards in both user interfaces drive them
    /// directly, using the suggested values to offer a fix.
    /// </remarks>
    public static class DynamicsReadiness
    {
        /// <summary>
        /// Checks a flowsheet, blockers first. Nothing here changes the flowsheet.
        /// </summary>
        /// <param name="flowsheet">The flowsheet to check.</param>
        /// <param name="scheduleName">Schedule to check; the current or first one when null.</param>
        public static IReadOnlyList<DynamicsIssue> Check(IFlowsheet flowsheet, string scheduleName = null)
        {
            if (flowsheet == null) throw new ArgumentNullException("flowsheet");

            var issues = new List<DynamicsIssue>();
            var manager = flowsheet.DynamicsManager;

            if (manager.ScheduleList.Count == 0)
            {
                issues.Add(new DynamicsIssue("NO_SCHEDULE", DynamicsIssueSeverity.Blocker,
                    "This flowsheet has no dynamics schedule.",
                    "Create one, together with an integrator, on the integrator step.")
                { Category = DynamicsIssueCategory.Integrator });

                // Everything downstream of a schedule is unknowable, but the flowsheet itself can
                // still be checked, and a wizard needs those pages populated on the first pass.
                CheckSteadyState(flowsheet, issues);
                CheckPressureFlowNetwork(flowsheet, issues);
                CheckObjects(flowsheet, issues);
                CheckControllers(flowsheet, issues);
                return Rank(issues);
            }

            IDynamicsSchedule schedule;
            try { schedule = IntegratorRunner.ResolveSchedule(flowsheet, scheduleName); }
            catch (Exception ex)
            {
                issues.Add(new DynamicsIssue("NO_SCHEDULE", DynamicsIssueSeverity.Blocker,
                    ex.Message, "Pass one of the schedule names the message lists.")
                { Category = DynamicsIssueCategory.Integrator });
                return Rank(issues);
            }

            if (!manager.IntegratorList.ContainsKey(schedule.CurrentIntegrator))
            {
                issues.Add(new DynamicsIssue("NO_INTEGRATOR", DynamicsIssueSeverity.Blocker,
                    "Schedule '" + schedule.Description + "' has no integrator assigned.",
                    "Assign one on the integrator step.")
                { Category = DynamicsIssueCategory.Integrator });
                return Rank(issues);
            }

            var integrator = manager.IntegratorList[schedule.CurrentIntegrator];

            CheckSteadyState(flowsheet, issues);
            CheckIntegrator(integrator, issues);
            CheckSchedule(flowsheet, schedule, issues);
            CheckPressureFlowNetwork(flowsheet, issues);
            CheckObjects(flowsheet, issues);
            CheckControllers(flowsheet, issues);

            if (!flowsheet.DynamicMode)
            {
                issues.Add(new DynamicsIssue("NO_DYNAMIC_MODE", DynamicsIssueSeverity.Info,
                    "Dynamic mode is off.",
                    "The run turns it on for its duration; nothing to do unless you are solving by hand.")
                { Category = DynamicsIssueCategory.Overview });
            }

            return Rank(issues);
        }

        /// <summary>
        /// A dynamic run integrates forward from wherever the flowsheet is. Starting it from a
        /// state that was never solved means integrating from nothing, and the first step fails on
        /// whatever the steady state would have failed on.
        /// </summary>
        private static void CheckSteadyState(IFlowsheet flowsheet, List<DynamicsIssue> issues)
        {
            if (flowsheet.PropertyPackages.Count == 0)
            {
                issues.Add(new DynamicsIssue("NO_PROPERTY_PACKAGE", DynamicsIssueSeverity.Blocker,
                    "The flowsheet has no property package, so nothing can be flashed.",
                    "Add one before anything else.")
                { Category = DynamicsIssueCategory.Overview });
                return;
            }

            if (flowsheet.SelectedCompounds.Count == 0)
            {
                issues.Add(new DynamicsIssue("NO_COMPOUNDS", DynamicsIssueSeverity.Blocker,
                    "The flowsheet has no compounds.",
                    "Add them before anything else.")
                { Category = DynamicsIssueCategory.Overview });
                return;
            }

            var unsolved = flowsheet.SimulationObjects.Values
                .Where(o => !o.Calculated && o.GraphicObject != null && o.GraphicObject.Active)
                .Select(o => o.GraphicObject.Tag)
                .Take(5)
                .ToList();

            if (unsolved.Count > 0)
            {
                issues.Add(new DynamicsIssue("NOT_SOLVED_STEADY_STATE", DynamicsIssueSeverity.Warning,
                    "These objects have not been solved: " + string.Join(", ", unsolved) + ".",
                    "Solve the flowsheet at steady state first; dynamics integrates forward from that state.")
                { Category = DynamicsIssueCategory.Overview });
            }
        }

        private static void CheckIntegrator(IDynamicsIntegrator integrator, List<DynamicsIssue> issues)
        {
            if (integrator.MonitoredVariables.Count == 0)
            {
                issues.Add(new DynamicsIssue("NO_MONITORED_VARS", DynamicsIssueSeverity.Warning,
                    "Integrator '" + integrator.Description + "' records no variables, so the run will produce no series.",
                    "Add some on the integrator step.")
                { Category = DynamicsIssueCategory.Integrator });
            }

            var step = integrator.IntegrationStep.TotalSeconds;
            if (step > 0)
            {
                var steps = integrator.Duration.TotalSeconds / step;
                if (steps > 100000)
                {
                    issues.Add(new DynamicsIssue("TOO_MANY_STEPS", DynamicsIssueSeverity.Blocker,
                        "The duration and step give " + Fmt(steps) + " integration steps.",
                        "Raise the integration step, or shorten the duration.")
                    { Category = DynamicsIssueCategory.Integrator });
                }
            }
        }

        private static void CheckSchedule(IFlowsheet flowsheet, IDynamicsSchedule schedule, List<DynamicsIssue> issues)
        {
            if (schedule.UseCurrentStateAsInitial) return;

            if (string.IsNullOrEmpty(schedule.InitialFlowsheetStateID) ||
                !flowsheet.StoredSolutions.ContainsKey(schedule.InitialFlowsheetStateID))
            {
                var target = schedule;
                issues.Add(new DynamicsIssue
                {
                    Code = "MISSING_INITIAL_STATE",
                    Severity = DynamicsIssueSeverity.Blocker,
                    Category = DynamicsIssueCategory.Integrator,
                    Message = "Schedule '" + schedule.Description + "' starts from stored state '" +
                              schedule.InitialFlowsheetStateID + "', which does not exist.",
                    Fix = "Start from the current state instead, or store the state it names.",
                    CanAutoFix = true,
                    ValueLabel = "Use current state",
                    SuggestedValue = true,
                    Apply = v => { target.UseCurrentStateAsInitial = Convert.ToBoolean(v); }
                });
            }
        }

        private static void CheckPressureFlowNetwork(IFlowsheet flowsheet, List<DynamicsIssue> issues)
        {
            var streams = flowsheet.SimulationObjects.Values
                .Where(o => o is IMaterialStream)
                .ToList();

            if (streams.Count == 0) return;

            var pressureSpecs = streams.Count(s => s.DynamicsSpec == DynEnums.DynamicsSpecType.Pressure);

            if (pressureSpecs == 0)
            {
                issues.Add(new DynamicsIssue("NO_PRESSURE_SPEC", DynamicsIssueSeverity.Warning,
                    "No material stream is specified by pressure, so the pressure-flow network has nothing to resolve against.",
                    "Mark the boundary streams: pressure on the product side, flow on the feed.")
                { Category = DynamicsIssueCategory.BoundarySpecs });
            }
            else if (pressureSpecs == streams.Count && streams.Count > 1)
            {
                issues.Add(new DynamicsIssue("ALL_FLOW_SPECS", DynamicsIssueSeverity.Info,
                    "Every material stream is specified by pressure; no flow is being held.",
                    "Mark the feed by flow if you meant to fix its flow rate.")
                { Category = DynamicsIssueCategory.BoundarySpecs });
            }
        }

        private static void CheckObjects(IFlowsheet flowsheet, List<DynamicsIssue> issues)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var tag = obj.GraphicObject != null ? obj.GraphicObject.Tag : obj.Name;

                if (!obj.SupportsDynamicMode && !(obj is IMaterialStream))
                {
                    issues.Add(new DynamicsIssue("UNSUPPORTED_OBJECT", DynamicsIssueSeverity.Info,
                        SafeType(obj) + " has no dynamic model; it is solved at steady state on every step.",
                        "Nothing to do, unless you expected it to hold up material.")
                    { ObjectId = obj.Name, ObjectTag = tag, Category = DynamicsIssueCategory.Overview });
                }

                CheckValve(obj as Valve, tag, issues);
                CheckHoldup(obj, tag, issues);
                CheckCalculationMode(obj, tag, issues);
            }
        }

        private static void CheckValve(Valve valve, string tag, List<DynamicsIssue> issues)
        {
            if (valve == null) return;

            if (valve.Kv <= 0.0)
            {
                issues.Add(new DynamicsIssue
                {
                    Code = "VALVE_NO_KV",
                    Severity = DynamicsIssueSeverity.Warning,
                    ObjectId = valve.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Hydraulics,
                    Message = "The valve has no flow coefficient (Kv = " + Fmt(valve.Kv) + ").",
                    Fix = "Size it from the converged operating point. Without a Kv the valve cannot pass a computed flow."
                });
            }

            if (valve.CalcMode == Valve.CalculationMode.DeltaP ||
                valve.CalcMode == Valve.CalculationMode.OutletPressure)
            {
                var target = valve;
                issues.Add(new DynamicsIssue
                {
                    Code = "VALVE_PRESSURE_DROP_MODE",
                    Severity = DynamicsIssueSeverity.Warning,
                    ObjectId = valve.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Hydraulics,
                    Message = "The valve is in " + valve.CalcMode +
                              " mode, so it cannot compute its own flow and demands a flow specification on one side.",
                    Fix = "Switch to a Kv mode so the valve resolves flow from the pressures on either side.",
                    CanAutoFix = true,
                    ValueLabel = "Calculation mode",
                    SuggestedValue = Valve.CalculationMode.Kv_General,
                    Apply = v => { target.CalcMode = (Valve.CalculationMode)v; }
                });
            }

            if (!valve.EnableOpeningKvRelationship)
            {
                var target = valve;
                issues.Add(new DynamicsIssue
                {
                    Code = "VALVE_OPENING_IGNORED",
                    Severity = DynamicsIssueSeverity.Warning,
                    ObjectId = valve.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Hydraulics,
                    Message = "The valve passes its full Kv at any opening, so closing it does not stop the flow.",
                    Fix = "Enable the opening-Kv characteristic. A controller manipulating the opening has no effect without it.",
                    CanAutoFix = true,
                    ValueLabel = "Opening affects Kv",
                    SuggestedValue = true,
                    Apply = v => { target.EnableOpeningKvRelationship = Convert.ToBoolean(v); }
                });
            }
        }

        private static void CheckHoldup(ISimulationObject obj, string tag, List<DynamicsIssue> issues)
        {
            var vessel = obj as Vessel;
            if (vessel != null && DynamicValue(obj, "Volume") <= 0.0)
            {
                issues.Add(new DynamicsIssue
                {
                    Code = "VESSEL_NO_VOLUME",
                    Severity = DynamicsIssueSeverity.Warning,
                    ObjectId = obj.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Holdup,
                    Message = "The vessel has no volume, so it holds nothing up and adds no lag.",
                    Fix = "Set a volume, or take it from the vessel dimensions."
                });
            }

            var tank = obj as Tank;
            if (tank != null && tank.Volume <= 0.0)
            {
                issues.Add(new DynamicsIssue
                {
                    Code = "VESSEL_NO_VOLUME",
                    Severity = DynamicsIssueSeverity.Warning,
                    ObjectId = obj.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Holdup,
                    Message = "The tank has no volume, so it holds nothing up and adds no lag.",
                    Fix = "Set a volume and a height."
                });
            }
        }

        /// <summary>
        /// Calculation modes the dynamic models refuse outright. Each of these throws on the first
        /// integration step, from inside RunDynamicModel, with a message that does not name the
        /// object — so they are worth catching before the run rather than after.
        /// </summary>
        private static void CheckCalculationMode(ISimulationObject obj, string tag, List<DynamicsIssue> issues)
        {
            var vessel = obj as Vessel;
            if (vessel != null && (int)vessel.CalculationMode > 1)
            {
                var target = vessel;
                issues.Add(new DynamicsIssue
                {
                    Code = "MODE_NOT_SUPPORTED_DYNAMIC",
                    Severity = DynamicsIssueSeverity.Blocker,
                    ObjectId = obj.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Holdup,
                    Message = "The separator is in " + vessel.CalculationMode +
                              " mode; only Adiabatic and Legacy run in dynamic mode.",
                    Fix = "Switch it to Adiabatic.",
                    CanAutoFix = true,
                    ValueLabel = "Calculation mode",
                    SuggestedValue = Vessel.CalculationModes.Adiabatic,
                    Apply = v => { target.CalculationMode = (Vessel.CalculationModes)v; }
                });
            }

            var heater = obj as Heater;
            if (heater != null && IsRejectedHeaterMode(heater.CalcMode))
            {
                var target = heater;
                issues.Add(new DynamicsIssue
                {
                    Code = "MODE_NOT_SUPPORTED_DYNAMIC",
                    Severity = DynamicsIssueSeverity.Blocker,
                    ObjectId = obj.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Hydraulics,
                    Message = "The heater is in " + heater.CalcMode + " mode, which the dynamic model refuses.",
                    Fix = "Switch to a duty-based mode, which the dynamic model integrates.",
                    CanAutoFix = true,
                    ValueLabel = "Calculation mode",
                    SuggestedValue = Heater.CalculationMode.HeatAdded,
                    Apply = v => { target.CalcMode = (Heater.CalculationMode)v; }
                });
            }

            var cooler = obj as Cooler;
            if (cooler != null && IsRejectedCoolerMode(cooler.CalcMode))
            {
                var target = cooler;
                issues.Add(new DynamicsIssue
                {
                    Code = "MODE_NOT_SUPPORTED_DYNAMIC",
                    Severity = DynamicsIssueSeverity.Blocker,
                    ObjectId = obj.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Hydraulics,
                    Message = "The cooler is in " + cooler.CalcMode + " mode, which the dynamic model refuses.",
                    Fix = "Switch to a duty-based mode, which the dynamic model integrates.",
                    CanAutoFix = true,
                    ValueLabel = "Calculation mode",
                    SuggestedValue = Cooler.CalculationMode.HeatRemoved,
                    Apply = v => { target.CalcMode = (Cooler.CalculationMode)v; }
                });
            }

            var pipe = obj as Pipe;
            if (pipe != null && (pipe.Specification == Pipe.Specmode.OutletPressure ||
                                 pipe.Specification == Pipe.Specmode.OutletTemperature))
            {
                var target = pipe;
                issues.Add(new DynamicsIssue
                {
                    Code = "MODE_NOT_SUPPORTED_DYNAMIC",
                    Severity = DynamicsIssueSeverity.Blocker,
                    ObjectId = obj.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Hydraulics,
                    Message = "The pipe is in " + pipe.Specification + " mode, which the dynamic model refuses.",
                    Fix = "Switch it to Length, so the hydraulic profile sets the pressure drop.",
                    CanAutoFix = true,
                    ValueLabel = "Specification",
                    SuggestedValue = Pipe.Specmode.Length,
                    Apply = v => { target.Specification = (Pipe.Specmode)v; }
                });
            }

            var cstr = obj as Reactor_CSTR;
            if (cstr != null && IsRejectedReactorMode(cstr.ReactorOperationMode))
            {
                var target = cstr;
                issues.Add(new DynamicsIssue
                {
                    Code = "MODE_NOT_SUPPORTED_DYNAMIC",
                    Severity = DynamicsIssueSeverity.Blocker,
                    ObjectId = obj.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Holdup,
                    Message = "The reactor is in " + cstr.ReactorOperationMode + " mode, which the dynamic model refuses.",
                    Fix = "Switch it to Adiabatic.",
                    CanAutoFix = true,
                    ValueLabel = "Operation mode",
                    SuggestedValue = OperationMode.Adiabatic,
                    Apply = v => { target.ReactorOperationMode = (OperationMode)v; }
                });
            }

            var pfr = obj as Reactor_PFR;
            if (pfr != null && IsRejectedReactorMode(pfr.ReactorOperationMode))
            {
                var target = pfr;
                issues.Add(new DynamicsIssue
                {
                    Code = "MODE_NOT_SUPPORTED_DYNAMIC",
                    Severity = DynamicsIssueSeverity.Blocker,
                    ObjectId = obj.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Holdup,
                    Message = "The reactor is in " + pfr.ReactorOperationMode + " mode, which the dynamic model refuses.",
                    Fix = "Switch it to Adiabatic.",
                    CanAutoFix = true,
                    ValueLabel = "Operation mode",
                    SuggestedValue = OperationMode.Adiabatic,
                    Apply = v => { target.ReactorOperationMode = (OperationMode)v; }
                });
            }
        }

        private static bool IsRejectedHeaterMode(Heater.CalculationMode mode)
        {
            return mode == Heater.CalculationMode.OutletVaporFraction ||
                   mode == Heater.CalculationMode.TemperatureChange ||
                   mode == Heater.CalculationMode.OutletTemperature;
        }

        private static bool IsRejectedCoolerMode(Cooler.CalculationMode mode)
        {
            return mode == Cooler.CalculationMode.OutletVaporFraction ||
                   mode == Cooler.CalculationMode.TemperatureChange ||
                   mode == Cooler.CalculationMode.OutletTemperature;
        }

        private static bool IsRejectedReactorMode(OperationMode mode)
        {
            return mode == OperationMode.OutletTemperature ||
                   mode == OperationMode.Isothermic;
        }

        private static void CheckControllers(IFlowsheet flowsheet, List<DynamicsIssue> issues)
        {
            foreach (var pid in flowsheet.SimulationObjects.Values.OfType<PIDController>())
            {
                var tag = pid.GraphicObject != null ? pid.GraphicObject.Tag : pid.Name;

                if (!IsWired(pid))
                {
                    issues.Add(new DynamicsIssue("PID_UNBOUND", DynamicsIssueSeverity.Blocker,
                        "The controller is missing its process or manipulated variable.",
                        "Wire both on the control step.")
                    { ObjectId = pid.Name, ObjectTag = tag, Category = DynamicsIssueCategory.Control });
                }

                if (pid.OutputMin >= pid.OutputMax)
                {
                    var target = pid;
                    issues.Add(new DynamicsIssue
                    {
                        Code = "PID_LIMITS_INVALID",
                        Severity = DynamicsIssueSeverity.Blocker,
                        ObjectId = pid.Name,
                        ObjectTag = tag,
                        Category = DynamicsIssueCategory.Control,
                        Message = "The output minimum (" + Fmt(pid.OutputMin) + ") is not below the maximum (" +
                                  Fmt(pid.OutputMax) + ").",
                        Fix = "Set them to the manipulated variable's physical range, e.g. 0 to 100 for a valve opening.",
                        CanAutoFix = true,
                        ValueLabel = "Output maximum",
                        SuggestedValue = 100.0,
                        Apply = v =>
                        {
                            target.OutputMin = 0.0;
                            target.OutputMax = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                        }
                    });
                }

                // Everything the controller writes is scaled by BaseSP = Abs(SetPoint), so a
                // setpoint of zero collapses the output and the loop silently does nothing.
                if (Math.Abs(pid.SetPoint) < 1e-12)
                {
                    issues.Add(new DynamicsIssue("PID_ZERO_SETPOINT", DynamicsIssueSeverity.Warning,
                        "The setpoint is zero. The controller scales its output by the magnitude of the setpoint, " +
                        "so the loop has no authority at all.",
                        "Set a non-zero setpoint, in the controlled variable's units.")
                    { ObjectId = pid.Name, ObjectTag = tag, Category = DynamicsIssueCategory.Control });
                }

                if (!pid.Active || pid.ManualOverride)
                {
                    var target = pid;
                    issues.Add(new DynamicsIssue
                    {
                        Code = "PID_INACTIVE",
                        Severity = DynamicsIssueSeverity.Warning,
                        ObjectId = pid.Name,
                        ObjectTag = tag,
                        Category = DynamicsIssueCategory.Control,
                        Message = pid.Active ? "The controller is in manual." : "The controller is switched off.",
                        Fix = "Put it in automatic.",
                        CanAutoFix = true,
                        ValueLabel = "Automatic",
                        SuggestedValue = true,
                        Apply = v =>
                        {
                            var auto = Convert.ToBoolean(v);
                            target.Active = auto;
                            target.ManualOverride = !auto;
                        }
                    });
                }
            }
        }

        /// <summary>True when the controller has both a process and a manipulated variable wired.</summary>
        internal static bool IsWired(PIDController pid)
        {
            var pv = pid.ControlledObjectData;
            var mv = pid.ManipulatedObjectData;
            return pv != null && !string.IsNullOrEmpty(pv.ID) && !string.IsNullOrEmpty(pv.PropertyName) &&
                   mv != null && !string.IsNullOrEmpty(mv.ID) && !string.IsNullOrEmpty(mv.PropertyName);
        }

        private static IReadOnlyList<DynamicsIssue> Rank(List<DynamicsIssue> issues)
        {
            return issues
                .OrderByDescending(f => (int)f.Severity)
                .ThenBy(f => f.Code, StringComparer.Ordinal)
                .ThenBy(f => f.ObjectTag, StringComparer.Ordinal)
                .ToList();
        }

        internal static double DynamicValue(ISimulationObject obj, string name)
        {
            try
            {
                var value = obj.GetDynamicProperty(name);
                return value == null ? 0.0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch { return 0.0; }
        }

        internal static string SafeType(ISimulationObject obj)
        {
            try { return obj.GetDisplayName(); }
            catch { return obj.GetType().Name; }
        }

        internal static string Fmt(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return value.ToString(CultureInfo.InvariantCulture);
            var abs = Math.Abs(value);
            if (abs != 0.0 && (abs < 1e-3 || abs >= 1e6)) return value.ToString("G4", CultureInfo.InvariantCulture);
            return Math.Round(value, 4).ToString(CultureInfo.InvariantCulture);
        }
    }
}
