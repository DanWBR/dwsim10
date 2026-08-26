using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DWSIM.DynamicsManager;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.SpecialOps;
using DWSIM.UnitOperations.SpecialOps.Helpers;
using DWSIM.UnitOperations.UnitOperations;
using DynEnums = DWSIM.Interfaces.Enums.Dynamics;

namespace DWSIM.Automation.DynamicRunner.Setup
{
    /// <summary>Choices a conversion makes on the user's behalf, all of them overridable.</summary>
    public sealed class DynamicsSetupOptions
    {
        /// <summary>Schedule to check against; the current or first one when null.</summary>
        public string ScheduleName { get; set; }

        /// <summary>How long a holdup vessel should take to turn over, when its volume has to be invented. Seconds.</summary>
        public double TargetResidenceTimeSeconds { get; set; }

        /// <summary>Where a sized valve should sit at the design point, in percent. Half open leaves authority both ways.</summary>
        public double DesignValveOpeningPct { get; set; }

        /// <summary>Initial liquid level, as a fraction of the available height.</summary>
        public double InitialLevelFraction { get; set; }

        /// <summary>Integration step for a newly created integrator.</summary>
        public TimeSpan IntegrationStep { get; set; }

        /// <summary>Duration for a newly created integrator.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>Display name for a newly created integrator.</summary>
        public string IntegratorName { get; set; }

        /// <summary>Display name for a newly created schedule.</summary>
        public string ScheduleDisplayName { get; set; }

        /// <summary>The defaults: five minutes of holdup, a half-open valve, a five-second step over ten minutes.</summary>
        public DynamicsSetupOptions()
        {
            TargetResidenceTimeSeconds = 300.0;
            DesignValveOpeningPct = 50.0;
            InitialLevelFraction = 0.5;
            IntegrationStep = TimeSpan.FromSeconds(5);
            Duration = TimeSpan.FromMinutes(10);
            IntegratorName = "Dynamics Wizard";
            ScheduleDisplayName = "Dynamics Wizard";
        }
    }

    /// <summary>
    /// Turns a solved steady-state flowsheet into one that can be integrated: works out the values
    /// the dynamic models need but the steady state never had to supply, and offers them as issues
    /// the caller can apply one by one.
    /// </summary>
    /// <remarks>
    /// <see cref="DynamicsReadiness"/> says what is wrong. This says what to do about it, in numbers
    /// taken from the converged operating point: the Kv a valve needs to pass the flow it is already
    /// passing, the volume that gives a vessel a sensible residence time, the level loop that keeps
    /// it from running dry.
    ///
    /// Every <see cref="DynamicsIssue.Apply"/> here is idempotent. Running the whole plan twice over
    /// the same flowsheet leaves it exactly as the first pass did.
    /// </remarks>
    public static class DynamicsSetupPlan
    {
        /// <summary>
        /// Everything standing between this flowsheet and a dynamic run, with a suggested value
        /// wherever one can be computed.
        /// </summary>
        /// <remarks>
        /// Calls <see cref="EnsureDynamicProperties"/> first: volumes and levels live in the dynamic
        /// property bag, and it cannot be read before it exists. That is additive and is what the
        /// engine itself does when it loads an object, so it is not treated as a change.
        /// </remarks>
        public static IReadOnlyList<DynamicsIssue> Propose(IFlowsheet flowsheet, DynamicsSetupOptions options = null)
        {
            if (flowsheet == null) throw new ArgumentNullException("flowsheet");
            if (options == null) options = new DynamicsSetupOptions();

            EnsureDynamicProperties(flowsheet);

            var issues = DynamicsReadiness.Check(flowsheet, options.ScheduleName).ToList();

            EnrichValveSizing(flowsheet, issues, options);
            EnrichHoldup(flowsheet, issues, options);
            AddStreamSpecs(flowsheet, issues);
            AddControlLoops(flowsheet, issues, options);
            AddIntegratorAndSchedule(flowsheet, issues, options);

            return issues
                .OrderByDescending(i => (int)i.Severity)
                .ThenBy(i => i.Code, StringComparer.Ordinal)
                .ThenBy(i => i.ObjectTag, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Creates the dynamic property bag on every object that has one and has not filled it yet.
        /// Objects that have no dynamic properties are left alone.
        /// </summary>
        public static void EnsureDynamicProperties(IFlowsheet flowsheet)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                if (!obj.HasPropertiesForDynamicMode) continue;

                var values = (IDictionary<string, object>)obj.ExtraProperties;
                if (values.Count > 0) continue;

                try { obj.CreateDynamicProperties(); }
                catch { /* an object that cannot describe itself simply stays empty */ }
            }
        }

        /// <summary>Applies one issue with the value the user confirmed, or its suggestion when none was given.</summary>
        public static bool Apply(DynamicsIssue issue, object value = null)
        {
            if (issue == null || issue.Apply == null) return false;
            issue.Apply(value ?? issue.SuggestedValue);
            return true;
        }

        // ------------------------------------------------------------- Hydraulics

        /// <summary>
        /// Fills in the Kv a valve with none would need. Sizing runs against the converged operating
        /// point, so it only works once the flowsheet has been solved and the valve has a pressure
        /// drop across it.
        /// </summary>
        private static void EnrichValveSizing(IFlowsheet flowsheet, List<DynamicsIssue> issues, DynamicsSetupOptions options)
        {
            foreach (var valve in flowsheet.SimulationObjects.Values.OfType<Valve>())
            {
                var issue = issues.FirstOrDefault(i => i.Code == "VALVE_NO_KV" && i.ObjectId == valve.Name);
                if (issue == null) continue;

                double sized;
                if (!TrySizeValve(valve, options.DesignValveOpeningPct, out sized)) continue;

                var target = valve;
                var opening = options.DesignValveOpeningPct;

                issue.CanAutoFix = true;
                issue.ValueLabel = "Kv";
                issue.SuggestedValue = sized;
                issue.Fix = "Size it at " + DynamicsReadiness.Fmt(sized) + ", which passes the current flow with the " +
                            "valve " + DynamicsReadiness.Fmt(opening) + " % open, leaving authority in both directions.";
                issue.Apply = v =>
                {
                    if (target.CalcMode == Valve.CalculationMode.DeltaP ||
                        target.CalcMode == Valve.CalculationMode.OutletPressure)
                    {
                        target.CalcMode = Valve.CalculationMode.Kv_General;
                    }
                    target.EnableOpeningKvRelationship = true;
                    target.OpeningPct = opening;
                    target.Kv = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                };
            }
        }

        /// <summary>
        /// Asks the valve to size itself against its own streams, then puts it back exactly as it
        /// was. CalculateKv writes straight to the valve, so the state has to be saved and restored
        /// around it — proposing a value must not change anything.
        /// </summary>
        private static bool TrySizeValve(Valve valve, double designOpeningPct, out double sized)
        {
            sized = 0.0;

            var oldKv = valve.Kv;
            var oldOpening = valve.OpeningPct;
            var oldEnable = valve.EnableOpeningKvRelationship;
            var oldMode = valve.CalcMode;

            try
            {
                if (valve.CalcMode == Valve.CalculationMode.DeltaP ||
                    valve.CalcMode == Valve.CalculationMode.OutletPressure)
                {
                    valve.CalcMode = Valve.CalculationMode.Kv_General;
                }

                // CalculateKv divides the required Kv by the characteristic at the current opening,
                // so setting the design opening first is what makes the result the rated Kv.
                valve.OpeningPct = designOpeningPct;
                valve.EnableOpeningKvRelationship = true;

                valve.CalculateKv();

                sized = valve.Kv;
            }
            catch
            {
                sized = 0.0;
            }
            finally
            {
                valve.Kv = oldKv;
                valve.OpeningPct = oldOpening;
                valve.EnableOpeningKvRelationship = oldEnable;
                valve.CalcMode = oldMode;
            }

            return sized > 0.0 && !double.IsNaN(sized) && !double.IsInfinity(sized);
        }

        // ------------------------------------------------------------- Holdup

        /// <summary>
        /// Gives a vessel or tank a volume, from the throughput it already carries. A unit with no
        /// volume integrates nothing, so this is what turns a steady-state block into a dynamic one.
        /// </summary>
        private static void EnrichHoldup(IFlowsheet flowsheet, List<DynamicsIssue> issues, DynamicsSetupOptions options)
        {
            foreach (var issue in issues.Where(i => i.Code == "VESSEL_NO_VOLUME").ToList())
            {
                ISimulationObject obj;
                if (!flowsheet.SimulationObjects.TryGetValue(issue.ObjectId, out obj)) continue;

                var throughput = InletVolumetricFlow(flowsheet, obj);
                if (throughput <= 0.0 || double.IsNaN(throughput) || double.IsInfinity(throughput)) continue;

                var volume = throughput * options.TargetResidenceTimeSeconds;
                if (volume <= 0.0) continue;

                // A vertical vessel roughly three diameters tall is the usual starting shape, so
                // height follows from the volume rather than being invented separately.
                var height = Math.Pow(12.0 * volume / Math.PI, 1.0 / 3.0);
                var level = height * options.InitialLevelFraction;

                var tank = obj as Tank;
                var target = obj;

                issue.CanAutoFix = true;
                issue.ValueLabel = "Volume";
                issue.SuggestedValue = volume;
                issue.UnitType = UnitOfMeasure.volume;
                issue.Fix = "Give it " + DynamicsReadiness.Fmt(volume) + " m³, which is " +
                            DynamicsReadiness.Fmt(options.TargetResidenceTimeSeconds / 60.0) +
                            " minutes of the flow it carries now.";
                issue.Apply = v =>
                {
                    var chosen = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                    var h = Math.Pow(12.0 * chosen / Math.PI, 1.0 / 3.0);

                    if (tank != null)
                    {
                        tank.Volume = chosen;
                    }
                    else
                    {
                        target.AddDynamicProperty("Volume", chosen);
                        target.AddDynamicProperty("Get Volume from Dimensions", false);
                        target.AddDynamicProperty("Get Height from Dimensions", false);
                    }

                    target.AddDynamicProperty("Height", h);
                    target.AddDynamicProperty("Liquid Level", h * options.InitialLevelFraction);
                };
            }
        }

        // ------------------------------------------------------------- Boundary specs

        /// <summary>
        /// Marks the edges of the pressure-flow network. A stream with nothing upstream is a feed and
        /// holds its flow; a stream with nothing downstream is a product and holds its pressure. The
        /// interior is left alone: those streams are whatever the network resolves them to.
        /// </summary>
        private static void AddStreamSpecs(IFlowsheet flowsheet, List<DynamicsIssue> issues)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                if (!(obj is IMaterialStream)) continue;
                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;

                var hasSource = graphic.InputConnectors.Any(c => c.IsAttached);
                var hasSink = graphic.OutputConnectors.Any(c => c.IsAttached);

                // An orphan stream is not a boundary, it is an oversight; and an interior stream is
                // not ours to specify.
                if (hasSource == hasSink) continue;

                var wanted = hasSource
                    ? DynEnums.DynamicsSpecType.Pressure
                    : DynEnums.DynamicsSpecType.Flow;

                if (obj.DynamicsSpec == wanted) continue;

                var target = obj;
                var role = hasSource ? "product" : "feed";
                var holds = hasSource ? "pressure" : "mass flow";
                var resolves = hasSource ? "flow" : "pressure";

                issues.Add(new DynamicsIssue
                {
                    Code = "STREAM_BOUNDARY_SPEC",
                    Severity = DynamicsIssueSeverity.Warning,
                    ObjectId = obj.Name,
                    ObjectTag = graphic.Tag,
                    Category = DynamicsIssueCategory.BoundarySpecs,
                    Message = "This is a " + role + " stream, but it is specified by " +
                              obj.DynamicsSpec.ToString().ToLowerInvariant() + ".",
                    Fix = "Specify it by " + holds + "; its " + resolves + " is then whatever the network resolves to.",
                    CanAutoFix = true,
                    ValueLabel = "Specification",
                    SuggestedValue = wanted,
                    Apply = v => { target.DynamicsSpec = (DynEnums.DynamicsSpecType)v; }
                });
            }
        }

        // ------------------------------------------------------------- Control

        /// <summary>
        /// Offers a level loop wherever a holdup unit drains through a valve. Without one, a vessel
        /// either fills until it floods or empties until it runs dry — which is the usual reason a
        /// first dynamic run ends in an exception.
        /// </summary>
        private static void AddControlLoops(IFlowsheet flowsheet, List<DynamicsIssue> issues, DynamicsSetupOptions options)
        {
            foreach (var obj in flowsheet.SimulationObjects.Values.ToList())
            {
                var isVessel = obj is Vessel;
                var isTank = obj is Tank;
                if (!isVessel && !isTank) continue;

                var graphic = obj.GraphicObject;
                if (graphic == null || !graphic.Active) continue;

                // Outlet 0 of a separator is the vapour draw; the liquid leaves through outlet 1.
                var liquidOutlet = isVessel ? 1 : 0;
                var valve = DownstreamValve(flowsheet, obj, liquidOutlet);
                if (valve == null) continue;

                if (AlreadyControlled(flowsheet, obj.Name, "Liquid Level")) continue;

                var level = DynamicsReadiness.DynamicValue(obj, "Liquid Level");
                var height = DynamicsReadiness.DynamicValue(obj, "Height");

                // The level the wizard would set up if the holdup fix is applied too, so the
                // proposal still makes sense on a vessel that has no geometry yet.
                if (level <= 0.0) level = height > 0.0 ? height * options.InitialLevelFraction : 1.0;

                var vessel = obj;
                var finalElement = valve;
                var setpoint = level;
                var tag = graphic.Tag;

                issues.Add(new DynamicsIssue
                {
                    Code = "NO_LEVEL_CONTROL",
                    Severity = DynamicsIssueSeverity.Warning,
                    ObjectId = obj.Name,
                    ObjectTag = tag,
                    Category = DynamicsIssueCategory.Control,
                    Message = "Nothing controls the level in " + tag + ", which drains through " +
                              finalElement.GraphicObject.Tag + ". It will fill or empty without limit.",
                    Fix = "Add a level controller holding " + DynamicsReadiness.Fmt(setpoint) +
                          " m by moving the opening of " + finalElement.GraphicObject.Tag + ".",
                    CanAutoFix = true,
                    ValueLabel = "Level setpoint",
                    SuggestedValue = setpoint,
                    UnitType = UnitOfMeasure.distance,
                    Apply = v => CreateLevelController(flowsheet, vessel, finalElement,
                                                       Convert.ToDouble(v, CultureInfo.InvariantCulture))
                });
            }
        }

        /// <summary>
        /// Builds the level loop: the controller reads the vessel level and writes the valve's
        /// opening setpoint, which the actuator model then moves towards.
        /// </summary>
        private static void CreateLevelController(IFlowsheet flowsheet, ISimulationObject vessel,
                                                  ISimulationObject valve, double setpoint)
        {
            var vesselTag = vessel.GraphicObject != null ? vessel.GraphicObject.Tag : vessel.Name;
            var tag = UniqueTag(flowsheet, "LIC-" + vesselTag);

            var x = vessel.GraphicObject != null ? (int)vessel.GraphicObject.X + 40 : 100;
            var y = vessel.GraphicObject != null ? (int)vessel.GraphicObject.Y - 60 : 100;

            var created = flowsheet.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.Controller_PID, x, y, tag);

            var pid = created as PIDController;
            if (pid == null) return;

            pid.ControlledObjectData = Describe(flowsheet, vessel, "Liquid Level");
            pid.ManipulatedObjectData = Describe(flowsheet, valve, "Opening Setpoint");

            pid.SetPoint = setpoint;
            pid.OutputMin = 0.0;
            pid.OutputMax = 100.0;
            pid.Active = true;
            pid.ManualOverride = false;

            // A level loop is an integrating process: mostly proportional, a little reset, no
            // derivative. These are a starting point for the tuner, not a tuning.
            pid.Kp = 1.0;
            pid.Ki = 0.1;
            pid.Kd = 0.0;

            flowsheet.UpdateInterface();
        }

        /// <summary>
        /// Describes an object-property pair the way the controller expects it, resolving the display
        /// units from whichever catalogue the property belongs to.
        /// </summary>
        private static SpecialOpObjectInfo Describe(IFlowsheet flowsheet, ISimulationObject obj, string propertyId)
        {
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;

            var isDynamic = false;
            try { isDynamic = obj.IsDynamicProperty(propertyId); }
            catch { }

            var units = "";
            var unitsType = UnitOfMeasure.none;

            try
            {
                if (isDynamic)
                {
                    unitsType = obj.GetDynamicPropertyUnitType(propertyId);
                    units = su.GetCurrentUnits(unitsType);
                }
                else
                {
                    units = obj.GetPropertyUnit(propertyId, su);
                }
            }
            catch { }

            return new SpecialOpObjectInfo
            {
                ID = obj.Name,
                Name = obj.GraphicObject != null ? obj.GraphicObject.Tag : obj.Name,
                PropertyName = propertyId,
                ObjectType = DynamicsReadiness.SafeType(obj),
                Units = units ?? "",
                UnitsType = unitsType
            };
        }

        private static bool AlreadyControlled(IFlowsheet flowsheet, string objectId, string propertyId)
        {
            return flowsheet.SimulationObjects.Values.OfType<PIDController>().Any(pid =>
            {
                var pv = pid.ControlledObjectData;
                return pv != null &&
                       string.Equals(pv.ID, objectId, StringComparison.Ordinal) &&
                       string.Equals(pv.PropertyName, propertyId, StringComparison.Ordinal);
            });
        }

        private static string UniqueTag(IFlowsheet flowsheet, string wanted)
        {
            var taken = new HashSet<string>(flowsheet.SimulationObjects.Values
                .Where(o => o.GraphicObject != null)
                .Select(o => o.GraphicObject.Tag));

            if (!taken.Contains(wanted)) return wanted;

            for (var i = 2; i < 1000; i++)
            {
                var candidate = wanted + "-" + i.ToString(CultureInfo.InvariantCulture);
                if (!taken.Contains(candidate)) return candidate;
            }

            return wanted + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
        }

        // ------------------------------------------------------------- Integrator and schedule

        /// <summary>
        /// Creates the integrator and schedule when the flowsheet has none, and gives the integrator
        /// something to record — an integrator that monitors nothing runs perfectly and reports
        /// nothing at all.
        /// </summary>
        private static void AddIntegratorAndSchedule(IFlowsheet flowsheet, List<DynamicsIssue> issues, DynamicsSetupOptions options)
        {
            var manager = flowsheet.DynamicsManager;

            var missing = issues.FirstOrDefault(i => i.Code == "NO_SCHEDULE" || i.Code == "NO_INTEGRATOR");
            if (missing != null)
            {
                missing.CanAutoFix = true;
                missing.ValueLabel = "Integration step (s)";
                missing.SuggestedValue = options.IntegrationStep.TotalSeconds;
                missing.Fix = "Create an integrator stepping every " +
                              DynamicsReadiness.Fmt(options.IntegrationStep.TotalSeconds) + " s over " +
                              DynamicsReadiness.Fmt(options.Duration.TotalMinutes) + " minutes, and a schedule to run it.";
                missing.Apply = v =>
                {
                    var step = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                    EnsureIntegratorAndSchedule(flowsheet, options, step);
                };
                return;
            }

            var monitors = issues.FirstOrDefault(i => i.Code == "NO_MONITORED_VARS");
            if (monitors == null) return;

            IDynamicsSchedule schedule;
            try { schedule = IntegratorRunner.ResolveSchedule(flowsheet, options.ScheduleName); }
            catch { return; }

            if (!manager.IntegratorList.ContainsKey(schedule.CurrentIntegrator)) return;
            var integrator = manager.IntegratorList[schedule.CurrentIntegrator];

            var candidates = InterestingVariables(flowsheet);
            if (candidates.Count == 0) return;

            monitors.CanAutoFix = true;
            monitors.ValueLabel = "Record";
            monitors.SuggestedValue = candidates.Count;
            monitors.Fix = "Record the " + candidates.Count.ToString(CultureInfo.InvariantCulture) +
                           " variables that say whether the run behaved: vessel levels, valve openings and controller outputs.";
            monitors.Apply = v => AddMonitoredVariables(flowsheet, integrator, candidates);
        }

        /// <summary>
        /// Creates an integrator and a schedule bound to it, and makes that schedule current. Does
        /// nothing beyond filling gaps: an existing integrator or schedule is reused as it stands.
        /// </summary>
        public static IDynamicsIntegrator EnsureIntegratorAndSchedule(IFlowsheet flowsheet,
                                                                     DynamicsSetupOptions options = null,
                                                                     double integrationStepSeconds = 0.0)
        {
            if (options == null) options = new DynamicsSetupOptions();
            var manager = flowsheet.DynamicsManager;

            var step = integrationStepSeconds > 0.0
                ? TimeSpan.FromSeconds(integrationStepSeconds)
                : options.IntegrationStep;

            Integrator integrator = null;

            if (manager.IntegratorList.Count == 0)
            {
                integrator = new Integrator
                {
                    ID = Guid.NewGuid().ToString(),
                    Description = options.IntegratorName,
                    IntegrationStep = step,
                    Duration = options.Duration,
                    ShouldCalculatePressureFlow = true,
                    ShouldCalculateControl = true,
                    ShouldCalculateEquilibrium = true
                };
                manager.IntegratorList.Add(integrator.ID, integrator);
            }

            var integratorId = integrator != null
                ? integrator.ID
                : manager.IntegratorList.Keys.First();

            IDynamicsSchedule schedule;

            if (manager.ScheduleList.Count == 0)
            {
                var created = new Schedule
                {
                    ID = Guid.NewGuid().ToString(),
                    Description = options.ScheduleDisplayName,
                    CurrentIntegrator = integratorId,
                    UseCurrentStateAsInitial = true
                };
                manager.ScheduleList.Add(created.ID, created);
                schedule = created;
            }
            else
            {
                schedule = manager.ScheduleList[manager.ScheduleList.Keys.First()];
                if (string.IsNullOrEmpty(schedule.CurrentIntegrator) ||
                    !manager.IntegratorList.ContainsKey(schedule.CurrentIntegrator))
                {
                    schedule.CurrentIntegrator = integratorId;
                }
            }

            if (string.IsNullOrEmpty(manager.CurrentSchedule) ||
                !manager.ScheduleList.ContainsKey(manager.CurrentSchedule))
            {
                manager.CurrentSchedule = schedule.ID;
            }

            // Ramps and transitions read past states out of the historian, so a schedule that may
            // carry events needs it switched on.
            manager.EnableHistorian = true;

            return manager.IntegratorList[schedule.CurrentIntegrator];
        }

        /// <summary>
        /// The variables worth recording by default: what a holdup unit is doing, what the final
        /// control elements are doing, and what the controllers are asking for.
        /// </summary>
        private static List<KeyValuePair<ISimulationObject, string>> InterestingVariables(IFlowsheet flowsheet)
        {
            var wanted = new List<KeyValuePair<ISimulationObject, string>>();

            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                if (obj is Vessel || obj is Tank)
                {
                    wanted.Add(new KeyValuePair<ISimulationObject, string>(obj, "Liquid Level"));
                    wanted.Add(new KeyValuePair<ISimulationObject, string>(obj, "Operating Pressure"));
                }
                else if (obj is Valve)
                {
                    wanted.Add(new KeyValuePair<ISimulationObject, string>(obj, "Opening Setpoint"));
                }
                else if (obj is PIDController)
                {
                    wanted.Add(new KeyValuePair<ISimulationObject, string>(obj, "OutputAbs"));
                }
            }

            return wanted;
        }

        private static void AddMonitoredVariables(IFlowsheet flowsheet, IDynamicsIntegrator integrator,
                                                  List<KeyValuePair<ISimulationObject, string>> variables)
        {
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;

            foreach (var pair in variables)
            {
                var obj = pair.Key;
                var propertyId = pair.Value;

                var already = integrator.MonitoredVariables.Any(
                    v => string.Equals(v.ObjectID, obj.Name, StringComparison.Ordinal) &&
                         string.Equals(v.PropertyID, propertyId, StringComparison.Ordinal));

                if (already) continue;

                var units = "";
                try
                {
                    units = obj.IsDynamicProperty(propertyId)
                        ? su.GetCurrentUnits(obj.GetDynamicPropertyUnitType(propertyId))
                        : obj.GetPropertyUnit(propertyId, su);
                }
                catch { }

                var tag = obj.GraphicObject != null ? obj.GraphicObject.Tag : obj.Name;

                integrator.MonitoredVariables.Add(new MonitoredVariable
                {
                    ID = Guid.NewGuid().ToString(),
                    Description = tag + " - " + propertyId,
                    ObjectID = obj.Name,
                    PropertyID = propertyId,
                    PropertyUnits = units ?? ""
                });
            }
        }

        // ------------------------------------------------------------- Topology helpers

        /// <summary>The valve, if any, that the given outlet of this object eventually feeds.</summary>
        private static ISimulationObject DownstreamValve(IFlowsheet flowsheet, ISimulationObject obj, int outletIndex)
        {
            var stream = OutletStream(flowsheet, obj, outletIndex);
            if (stream == null || stream.GraphicObject == null) return null;

            var sink = stream.GraphicObject.OutputConnectors.FirstOrDefault(c => c.IsAttached);
            if (sink == null || sink.AttachedConnector == null || sink.AttachedConnector.AttachedTo == null) return null;

            ISimulationObject downstream;
            if (!flowsheet.SimulationObjects.TryGetValue(sink.AttachedConnector.AttachedTo.Name, out downstream)) return null;

            return downstream is Valve ? downstream : null;
        }

        private static ISimulationObject OutletStream(IFlowsheet flowsheet, ISimulationObject obj, int index)
        {
            var graphic = obj.GraphicObject;
            if (graphic == null || index >= graphic.OutputConnectors.Count) return null;

            var connector = graphic.OutputConnectors[index];
            if (!connector.IsAttached || connector.AttachedConnector == null ||
                connector.AttachedConnector.AttachedTo == null) return null;

            ISimulationObject stream;
            return flowsheet.SimulationObjects.TryGetValue(connector.AttachedConnector.AttachedTo.Name, out stream)
                ? stream
                : null;
        }

        /// <summary>Total volumetric flow entering an object, in m³/s.</summary>
        private static double InletVolumetricFlow(IFlowsheet flowsheet, ISimulationObject obj)
        {
            var graphic = obj.GraphicObject;
            if (graphic == null) return 0.0;

            var total = 0.0;

            foreach (var connector in graphic.InputConnectors)
            {
                if (!connector.IsAttached || connector.AttachedConnector == null ||
                    connector.AttachedConnector.AttachedFrom == null) continue;

                ISimulationObject upstream;
                if (!flowsheet.SimulationObjects.TryGetValue(connector.AttachedConnector.AttachedFrom.Name, out upstream)) continue;

                var stream = upstream as IMaterialStream;
                if (stream == null) continue;

                try
                {
                    var flow = stream.GetVolumetricFlow();
                    if (!double.IsNaN(flow) && !double.IsInfinity(flow) && flow > 0.0) total += flow;
                }
                catch { }
            }

            return total;
        }
    }
}
