using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DWSIM.Automation.DynamicRunner;
using DWSIM.Automation.FluentAPI;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;
using DWSIM.Interfaces;
using DWSIM.MCPServer.Sessions;
using Newtonsoft.Json.Linq;
using DynEnums = DWSIM.Interfaces.Enums.Dynamics;

namespace DWSIM.MCPServer.Tools.Dynamics
{
    /// <summary>
    /// Dynamic simulation over MCP: inspect what a flowsheet offers, configure an integrator and a
    /// schedule, disturb it with events, run it, and read the transient back.
    /// </summary>
    /// <remarks>
    /// Every response here is sized for a language model's context. A run can produce tens of
    /// thousands of samples; <c>run</c> and <c>status</c> never return points, <c>series</c> returns
    /// a decimated preview, and the full data only ever leaves through <c>export</c>, as a file.
    /// </remarks>
    public class DynamicsTools
    {
        private const int DefaultPreviewPoints = 40;
        private const int MaxPreviewPoints = 400;
        private const int MaxListItems = 25;

        private readonly SessionManager _sessions;
        private readonly DynamicsJobManager _jobs;

        public DynamicsTools(SessionManager sessions, DynamicsJobManager jobs)
        {
            _sessions = sessions;
            _jobs = jobs;
        }

        // ------------------------------------------------------------- Discovery

        [McpTool("dwsim_dynamics_inspect",
            "Survey a flowsheet from a dynamic-simulation point of view: which objects have dynamic models, " +
            "how the pressure-flow network is specified, how the controllers are wired, and what the Dynamics " +
            "Manager already holds. Start here before configuring anything.")]
        public JObject Inspect(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("How much to return: summary, objects, controllers, config or full", Required = false)]
            string detail = "summary")
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var inventory = DynamicsIntrospection.Inspect(fs.Inner);

            var result = new JObject
            {
                ["dynamic_mode"] = inventory.DynamicModeEnabled,
                ["object_count"] = inventory.Objects.Count,
                ["dynamic_capable_count"] = inventory.DynamicCapableObjects.Count(),
                ["controller_count"] = inventory.Controllers.Count,
                ["current_schedule"] = inventory.CurrentSchedule
            };

            var wantsAll = string.Equals(detail, "full", StringComparison.OrdinalIgnoreCase);

            if (wantsAll || string.Equals(detail, "config", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(detail, "summary", StringComparison.OrdinalIgnoreCase))
            {
                result["schedules"] = Truncate(inventory.Schedules);
                result["integrators"] = Truncate(inventory.Integrators);
                result["event_sets"] = Truncate(inventory.EventSets);
                result["cause_and_effect_matrices"] = Truncate(inventory.CauseAndEffectMatrices);
                result["stored_states"] = Truncate(inventory.StoredStates);
            }

            if (wantsAll || string.Equals(detail, "objects", StringComparison.OrdinalIgnoreCase))
            {
                var objects = new JArray();
                foreach (var o in inventory.Objects.Take(MaxListItems))
                {
                    objects.Add(new JObject
                    {
                        ["tag"] = o.Tag,
                        ["type"] = o.Type,
                        ["supports_dynamics"] = o.SupportsDynamics,
                        ["dynamics_spec"] = o.DynamicsSpec.ToString(),
                        ["dynamic_properties"] = new JArray(o.DynamicProperties.Select(p => p.Id).Cast<object>().ToArray())
                    });
                }
                result["objects"] = objects;
                if (inventory.Objects.Count > MaxListItems)
                {
                    result["objects_truncated"] = true;
                    result["objects_total"] = inventory.Objects.Count;
                }
            }

            if (wantsAll || string.Equals(detail, "controllers", StringComparison.OrdinalIgnoreCase))
            {
                result["controllers"] = new JArray(inventory.Controllers.Take(MaxListItems)
                    .Select(c => Describe(c)).Cast<object>().ToArray());
            }

            return result;
        }

        [McpTool("dwsim_dynamics_properties",
            "List the properties of one object that can be monitored, disturbed by an event, or wired to a " +
            "controller, with their ids, descriptions, units and current values. Property ids are not " +
            "guessable, so call this before dwsim_dynamics_monitor or dwsim_dynamics_event.")]
        public JObject Properties(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Tag of the object")] string tag,
            [McpParam("Case-insensitive substring to filter descriptions by", Required = false)] string filter = null)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var all = DynamicsIntrospection.AddressableProperties(fs.Inner, tag);

            var matching = string.IsNullOrEmpty(filter)
                ? all
                : all.Where(p =>
                    (p.Description ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (p.Id ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var items = new JArray();
            foreach (var p in matching.Take(MaxListItems))
            {
                items.Add(new JObject
                {
                    ["id"] = p.Id,
                    ["description"] = p.Description,
                    ["units"] = p.Units,
                    ["value"] = p.Value == null ? null : SeriesDecimator.Format(ToDouble(p.Value)),
                    ["dynamic"] = p.IsDynamic
                });
            }

            var result = new JObject { ["tag"] = tag, ["properties"] = items, ["total"] = matching.Count };
            if (matching.Count > MaxListItems) result["truncated"] = true;
            return result;
        }

        [McpTool("dwsim_dynamics_check",
            "Answer whether a flowsheet is ready to run dynamically. Returns blockers that must be fixed, " +
            "warnings worth reading, and an estimate of how many steps the run will take. Each finding " +
            "carries a concrete fix. Call this before dwsim_dynamics_run.")]
        public JObject Check(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Schedule name; the current or first one when omitted", Required = false)] string schedule = null)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var findings = DynamicsDiagnostics.CheckReady(fs.Inner, schedule);

            var blockers = findings.Where(f => f.Severity == DiagnosticSeverity.Blocker).ToList();

            var result = new JObject
            {
                ["ready"] = blockers.Count == 0,
                ["blockers"] = Findings(blockers),
                ["warnings"] = Findings(findings.Where(f => f.Severity == DiagnosticSeverity.Warning)),
                ["notes"] = Findings(findings.Where(f => f.Severity == DiagnosticSeverity.Info))
            };

            try
            {
                var resolved = IntegratorRunner.ResolveSchedule(fs.Inner, schedule);
                var integrator = fs.Inner.DynamicsManager.IntegratorList[resolved.CurrentIntegrator];
                result["schedule"] = resolved.Description;
                result["integrator"] = integrator.Description;
                result["step_s"] = integrator.IntegrationStep.TotalSeconds;
                result["duration_s"] = integrator.Duration.TotalSeconds;
                result["estimated_steps"] = (int)(integrator.Duration.TotalSeconds / integrator.IntegrationStep.TotalSeconds);
                result["monitored_variables"] = new JArray(
                    integrator.MonitoredVariables.Select(v => (object)v.Description).ToArray());
            }
            catch (Exception)
            {
                // The findings already say why the schedule could not be resolved.
            }

            return result;
        }

        // --------------------------------------------------------- Configuration

        [McpTool("dwsim_dynamics_setup",
            "Create or update an integrator and a schedule in one call, and turn dynamic mode on. " +
            "Idempotent by name, so it is safe to call again to adjust settings.")]
        public JObject Setup(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Schedule name to create or update")] string schedule,
            [McpParam("Integration step in seconds", JsonType = "number")] double step_s,
            [McpParam("How much simulated time the run covers, in seconds", JsonType = "number")] double duration_s,
            [McpParam("Integrator name; defaults to the schedule name", Required = false)] string integrator = null,
            [McpParam("ExplicitEuler, RungeKutta4, ImplicitEuler or AdaptiveRK45", Required = false)] string method = null,
            [McpParam("Recalculate equilibrium every N steps", Required = false, JsonType = "integer")] int rate_equilibrium = 1,
            [McpParam("Recalculate the pressure-flow network every N steps", Required = false, JsonType = "integer")] int rate_pressure_flow = 1,
            [McpParam("Run the controllers every N steps", Required = false, JsonType = "integer")] int rate_control = 1,
            [McpParam("Relative error tolerance for the adaptive method", Required = false, JsonType = "number")] double error_tolerance = 0,
            [McpParam("Make this the current schedule", Required = false, JsonType = "boolean")] bool make_current = true,
            [McpParam("Turn dynamic mode on", Required = false, JsonType = "boolean")] bool enable_dynamic_mode = true)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var integratorName = string.IsNullOrEmpty(integrator) ? schedule : integrator;

            var builder = fs.Dynamics.DefineIntegrator(integratorName)
                .WithIntegrationStep(TimeSpan.FromSeconds(step_s))
                .WithDuration(TimeSpan.FromSeconds(duration_s))
                .WithCalculationRates(rate_equilibrium, rate_pressure_flow, rate_control);

            if (!string.IsNullOrEmpty(method))
            {
                DynEnums.IntegrationMethod parsed;
                if (!Enum.TryParse(method, true, out parsed))
                {
                    throw new ArgumentException("Unknown integration method '" + method + "'. Use one of: " +
                        string.Join(", ", Enum.GetNames(typeof(DynEnums.IntegrationMethod))) + ".");
                }
                builder.WithMethod(parsed);
                if (parsed == DynEnums.IntegrationMethod.AdaptiveRK45) builder.WithAdaptiveStep(true);
            }

            if (error_tolerance > 0) builder.WithErrorTolerance(error_tolerance);

            var scheduleBuilder = fs.Dynamics.DefineSchedule(schedule).WithIntegrator(integratorName);
            if (make_current) scheduleBuilder.MakeCurrent();

            if (enable_dynamic_mode) fs.Dynamics.EnableDynamicMode();

            return new JObject
            {
                ["schedule"] = schedule,
                ["integrator"] = integratorName,
                ["step_s"] = step_s,
                ["duration_s"] = duration_s,
                ["estimated_steps"] = (int)(duration_s / step_s),
                ["monitored_variables"] = new JArray(builder.MonitoredVariableNames.Cast<object>().ToArray())
            };
        }

        [McpTool("dwsim_dynamics_monitor",
            "Manage the variables an integrator records. Only monitored variables appear in the results, so " +
            "a run with none produces no series. Variables are given as \"TAG.PropertyId\"; find the ids " +
            "with dwsim_dynamics_properties.")]
        public JObject Monitor(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("set, add, list or clear", Required = false)] string action = "list",
            [McpParam("Integrator name; the current schedule's when omitted", Required = false)] string integrator = null,
            [McpParam("Variables as \"TAG.PropertyId\"", Required = false)] string[] variables = null)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var builder = fs.Dynamics.Integrator(ResolveIntegratorName(fs, integrator));

            switch ((action ?? "list").ToLowerInvariant())
            {
                case "clear":
                    builder.ClearMonitoredVariables();
                    break;

                case "set":
                case "add":
                    if (variables == null || variables.Length == 0)
                        throw new ArgumentException("Pass at least one variable as \"TAG.PropertyId\".");
                    if (action.ToLowerInvariant() == "set") builder.ClearMonitoredVariables();
                    foreach (var spec in variables)
                    {
                        var split = spec.LastIndexOf('.');
                        if (split <= 0)
                            throw new ArgumentException("'" + spec + "' is not in the form \"TAG.PropertyId\".");
                        builder.Monitor(spec.Substring(0, split), spec.Substring(split + 1));
                    }
                    break;

                case "list":
                    break;

                default:
                    throw new ArgumentException("Unknown action '" + action + "'. Use set, add, list or clear.");
            }

            return new JObject
            {
                ["integrator"] = builder.Name,
                ["monitored_variables"] = new JArray(builder.MonitoredVariableNames.Cast<object>().ToArray())
            };
        }

        [McpTool("dwsim_dynamics_event",
            "Manage the timed disturbances applied during a run: step changes and ramps on any property. " +
            "A step holds the old value until its instant then jumps; a ramp interpolates from the previous " +
            "event's recorded state up to its own instant. The event set is attached to the schedule automatically.")]
        public JObject Event(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("add, list, remove or clear", Required = false)] string action = "list",
            [McpParam("Event set name; defaults to one named after the schedule", Required = false)] string event_set = null,
            [McpParam("Schedule to attach the event set to; the current one when omitted", Required = false)] string schedule = null,
            [McpParam("Tag of the object to disturb", Required = false)] string tag = null,
            [McpParam("Property id to change", Required = false)] string property = null,
            [McpParam("Target value, in the property's display units", Required = false, JsonType = "number")] double value = 0,
            [McpParam("Units of the value; the display units when omitted", Required = false)] string units = null,
            [McpParam("When the event fires, in seconds from the start of the run", Required = false, JsonType = "number")] double at_s = 0,
            [McpParam("step, linear, log or inverse_log", Required = false)] string transition = "step",
            [McpParam("Description of the event to add or remove", Required = false)] string description = null)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);

            var scheduleName = string.IsNullOrEmpty(schedule)
                ? IntegratorRunner.ResolveSchedule(fs.Inner, null).Description
                : schedule;

            var setName = string.IsNullOrEmpty(event_set) ? scheduleName + " events" : event_set;
            var set = fs.Dynamics.DefineEventSet(setName);

            switch ((action ?? "list").ToLowerInvariant())
            {
                case "add":
                    if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(property))
                        throw new ArgumentException("An event needs a tag and a property.");

                    var kind = (transition ?? "step").ToLowerInvariant();
                    if (kind == "step")
                    {
                        set.AddStepChange(tag, property, value, at_s.Seconds(), units, description);
                    }
                    else
                    {
                        var type = ParseTransition(kind);
                        set.AddEvent(description ?? (kind + " " + tag + "." + property))
                            .At(at_s.Seconds())
                            .ChangeProperty(tag, property, value, units)
                            .WithTransition(type)
                            .And();
                    }

                    // An event set nothing runs is an event set that does nothing.
                    fs.Dynamics.Schedule(scheduleName).WithEventSet(setName);
                    break;

                case "remove":
                    if (string.IsNullOrEmpty(description))
                        throw new ArgumentException("Pass the description of the event to remove.");
                    set.RemoveEvent(description);
                    break;

                case "clear":
                    set.ClearEvents();
                    break;

                case "list":
                    break;

                default:
                    throw new ArgumentException("Unknown action '" + action + "'. Use add, list, remove or clear.");
            }

            return new JObject
            {
                ["event_set"] = setName,
                ["schedule"] = scheduleName,
                ["events"] = new JArray(set.EventDescriptions.Cast<object>().ToArray())
            };
        }

        [McpTool("dwsim_dynamics_controller",
            "Read or set up the PID controllers on a flowsheet: what each one reads and writes, its setpoint, " +
            "gains, output limits and action. A controller without both a process and a manipulated variable " +
            "does nothing during a run.")]
        public JObject Controller(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("list or set", Required = false)] string action = "list",
            [McpParam("Controller tag", Required = false)] string tag = null,
            [McpParam("Setpoint, in the controlled property's units", Required = false, JsonType = "number")] double? sp = null,
            [McpParam("Proportional gain", Required = false, JsonType = "number")] double? kp = null,
            [McpParam("Integral gain", Required = false, JsonType = "number")] double? ki = null,
            [McpParam("Derivative gain", Required = false, JsonType = "number")] double? kd = null,
            [McpParam("Lower output clamp", Required = false, JsonType = "number")] double? out_min = null,
            [McpParam("Upper output clamp", Required = false, JsonType = "number")] double? out_max = null,
            [McpParam("Reverse the control action", Required = false, JsonType = "boolean")] bool? reverse_acting = null,
            [McpParam("Put the controller in or out of service", Required = false, JsonType = "boolean")] bool? active = null,
            [McpParam("Hold the output at a fixed value", Required = false, JsonType = "boolean")] bool? manual_override = null,
            [McpParam("Position in the controller execution order, low first", Required = false, JsonType = "integer")] int? execution_order = null)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);

            if (string.Equals(action, "set", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(tag)) throw new ArgumentException("Pass the controller's tag.");

                var obj = DynamicsIntrospection.Resolve(fs.Inner, tag);
                var pid = obj as DWSIM.UnitOperations.SpecialOps.PIDController;
                if (pid == null) throw new ArgumentException("'" + tag + "' is not a PID controller.");

                if (sp.HasValue) pid.SetPoint = sp.Value;
                if (kp.HasValue) pid.Kp = kp.Value;
                if (ki.HasValue) pid.Ki = ki.Value;
                if (kd.HasValue) pid.Kd = kd.Value;
                if (out_min.HasValue) pid.OutputMin = out_min.Value;
                if (out_max.HasValue) pid.OutputMax = out_max.Value;
                if (reverse_acting.HasValue) pid.ReverseActing = reverse_acting.Value;
                if (active.HasValue) pid.Active = active.Value;
                if (manual_override.HasValue) pid.ManualOverride = manual_override.Value;
                if (execution_order.HasValue) pid.ExecutionOrder = execution_order.Value;

                if (pid.OutputMin >= pid.OutputMax)
                {
                    throw new ArgumentException("The output minimum (" + pid.OutputMin +
                        ") must be below the maximum (" + pid.OutputMax + ").");
                }
            }

            var inventory = DynamicsIntrospection.Inspect(fs.Inner);
            var wanted = string.IsNullOrEmpty(tag)
                ? inventory.Controllers
                : inventory.Controllers.Where(c => string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase)).ToList();

            return new JObject
            {
                ["controllers"] = new JArray(wanted.Take(MaxListItems).Select(c => Describe(c)).Cast<object>().ToArray())
            };
        }

        [McpTool("dwsim_dynamics_object",
            "Configure an object for dynamic simulation: its pressure-flow specification, its dynamic " +
            "properties such as a vessel's volume or a valve's opening, and — for valves — the calculation " +
            "mode and opening characteristic. This is what the fixes from dwsim_dynamics_check are asking for.")]
        public JObject Object(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Tag of the object")] string tag,
            [McpParam("pressure or flow, for the pressure-flow network", Required = false)] string dynamics_spec = null,
            [McpParam("Dynamic properties to set, as {name: value} in SI units", Required = false, JsonType = "object")]
            JObject properties = null,
            [McpParam("Valve calculation mode: Kv_Liquid, Kv_Gas, Kv_General, Kv_Steam, DeltaP or OutletPressure", Required = false)]
            string valve_calc_mode = null,
            [McpParam("Make a valve's flow coefficient follow its opening: Linear, EqualPercentage or QuickOpening", Required = false)]
            string valve_opening_characteristic = null)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var obj = DynamicsIntrospection.Resolve(fs.Inner, tag);
            var applied = new JArray();

            PropertyCatalog.EnsureDynamicProperties(obj);

            if (!string.IsNullOrEmpty(dynamics_spec))
            {
                DynEnums.DynamicsSpecType spec;
                if (!Enum.TryParse(dynamics_spec, true, out spec))
                    throw new ArgumentException("Unknown dynamics spec '" + dynamics_spec + "'. Use pressure or flow.");
                obj.DynamicsSpec = spec;
                applied.Add("dynamics_spec = " + spec);
            }

            if (properties != null)
            {
                var units = fs.Inner.FlowsheetOptions.SelectedUnitSystem;

                foreach (var entry in properties)
                {
                    if (obj.IsDynamicProperty(entry.Key))
                    {
                        object value = entry.Value.Type == JTokenType.Boolean
                            ? (object)entry.Value.Value<bool>()
                            : entry.Value.Value<double>();

                        obj.AddDynamicProperty(entry.Key, value);
                        applied.Add(entry.Key + " = " + value);
                        continue;
                    }

                    // Some settings a dynamic run depends on are ordinary properties — a tank's
                    // volume, for one — so fall through to those rather than refusing.
                    var writable = obj.GetProperties(Interfaces.Enums.PropertyType.WR) ?? new string[0];
                    if (writable.Contains(entry.Key))
                    {
                        obj.SetPropertyValue(entry.Key, entry.Value.Value<double>(), units);
                        applied.Add(entry.Key + " = " + entry.Value);
                        continue;
                    }

                    var known = PropertyCatalog.DynamicFor(obj, units).Select(p => p.Id)
                        .Concat(writable)
                        .Take(MaxListItems);

                    throw new ArgumentException("'" + tag + "' has no settable property '" + entry.Key +
                        "'. Available: " + string.Join(", ", known.Select(p => "'" + p + "'")) + ".");
                }
            }

            var valve = obj as DWSIM.UnitOperations.UnitOperations.Valve;

            if (!string.IsNullOrEmpty(valve_calc_mode))
            {
                if (valve == null) throw new ArgumentException("'" + tag + "' is not a valve.");
                DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode mode;
                if (!Enum.TryParse(valve_calc_mode, true, out mode))
                {
                    throw new ArgumentException("Unknown valve calculation mode '" + valve_calc_mode + "'. Use one of: " +
                        string.Join(", ", Enum.GetNames(typeof(DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode))) + ".");
                }
                valve.CalcMode = mode;
                applied.Add("calc_mode = " + mode);
            }

            if (!string.IsNullOrEmpty(valve_opening_characteristic))
            {
                if (valve == null) throw new ArgumentException("'" + tag + "' is not a valve.");
                DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType characteristic;
                if (!Enum.TryParse(valve_opening_characteristic, true, out characteristic))
                {
                    throw new ArgumentException("Unknown opening characteristic '" + valve_opening_characteristic +
                        "'. Use Linear, EqualPercentage or QuickOpening.");
                }
                valve.EnableOpeningKvRelationship = true;
                valve.DefinedOpeningKvRelationShipType = characteristic;
                applied.Add("opening_characteristic = " + characteristic);
            }

            var su = fs.Inner.FlowsheetOptions.SelectedUnitSystem;
            return new JObject
            {
                ["tag"] = tag,
                ["applied"] = applied,
                ["dynamics_spec"] = obj.DynamicsSpec.ToString(),
                ["dynamic_properties"] = new JArray(PropertyCatalog.DynamicFor(obj, su)
                    .Take(MaxListItems)
                    .Select(p => (object)new JObject
                    {
                        ["id"] = p.Id,
                        ["value"] = p.Value == null ? null : SeriesDecimator.Format(ToDouble(p.Value)),
                        ["units"] = p.Units
                    }).ToArray())
            };
        }

        [McpTool("dwsim_dynamics_state",
            "Manage stored flowsheet states. A schedule that starts from a stored state gives repeatable runs, " +
            "which is what makes two runs comparable and what PID tuning needs.")]
        public JObject State(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("save, restore, list, delete or attach", Required = false)] string action = "list",
            [McpParam("Name of the state", Required = false)] string name = null,
            [McpParam("Schedule to attach the state to, for the attach action", Required = false)] string schedule = null)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);

            switch ((action ?? "list").ToLowerInvariant())
            {
                case "save":
                    Require(name, "a state name");
                    fs.Dynamics.StoreCurrentStateAs(name);
                    break;

                case "restore":
                    Require(name, "a state name");
                    IntegratorRunner.RestoreState(fs.Inner, name);
                    break;

                case "delete":
                    Require(name, "a state name");
                    fs.Inner.StoredSolutions.Remove(name);
                    break;

                case "attach":
                    Require(name, "a state name");
                    var scheduleName = string.IsNullOrEmpty(schedule)
                        ? IntegratorRunner.ResolveSchedule(fs.Inner, null).Description
                        : schedule;
                    fs.Dynamics.Schedule(scheduleName).WithInitialState(name);
                    break;

                case "list":
                    break;

                default:
                    throw new ArgumentException("Unknown action '" + action + "'. Use save, restore, list, delete or attach.");
            }

            return new JObject
            {
                ["stored_states"] = new JArray(fs.Inner.StoredSolutions.Keys.Take(MaxListItems).Cast<object>().ToArray()),
                ["total"] = fs.Inner.StoredSolutions.Count
            };
        }

        // ------------------------------------------------------------------ Run

        [McpTool("dwsim_dynamics_run",
            "Start a dynamic integration. Returns a run_id immediately; poll dwsim_dynamics_status for progress " +
            "and results. Never returns time-series points — read those with dwsim_dynamics_series once the run " +
            "has finished. Only one integration runs in the process at a time.")]
        public JObject Run(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Schedule to run; the current or first one when omitted", Required = false)] string schedule = null,
            [McpParam("Override the integrator's duration, in seconds", Required = false, JsonType = "number")] double duration_s = 0,
            [McpParam("Give up after this much wall-clock time, in seconds", Required = false, JsonType = "integer")] int max_wall_time_s = 300,
            [McpParam("Stop after this many steps", Required = false, JsonType = "integer")] int max_steps = 0,
            [McpParam("Block until the run finishes. Only for short runs.", Required = false, JsonType = "boolean")] bool wait = false)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, schedule)
                .Where(f => f.Severity == DiagnosticSeverity.Blocker).ToList();

            if (blockers.Count > 0)
            {
                return new JObject
                {
                    ["started"] = false,
                    ["reason"] = "not_ready",
                    ["blockers"] = Findings(blockers)
                };
            }

            var resolved = IntegratorRunner.ResolveSchedule(fs.Inner, schedule);
            var integrator = fs.Inner.DynamicsManager.IntegratorList[resolved.CurrentIntegrator];

            if (duration_s > 0) integrator.Duration = TimeSpan.FromSeconds(duration_s);

            var job = _jobs.Start(flowsheet_id, "run", record =>
            {
                record.TotalSeconds = integrator.Duration.TotalSeconds;

                var builder = fs.RunDynamics(resolved.ID)
                    .WithCancellation(record.Cancellation.Token)
                    .WithMaxWallTime(TimeSpan.FromSeconds(max_wall_time_s))
                    .OnProgress(p =>
                    {
                        record.CurrentSeconds = p.CurrentSeconds;
                        record.Steps = p.Step;
                    });

                if (max_steps > 0) builder.WithMaxSteps(max_steps);

                record.Result = builder.Execute();
                if (record.Result.Error != null) record.Error = record.Result.Error;
            });

            if (wait) WaitFor(job, max_wall_time_s + 30);

            var result = new JObject
            {
                ["started"] = true,
                ["run_id"] = job.Id,
                ["schedule"] = resolved.Description,
                ["integrator"] = integrator.Description,
                ["estimated_steps"] = (int)(integrator.Duration.TotalSeconds / integrator.IntegrationStep.TotalSeconds),
                ["state"] = job.State.ToString().ToLowerInvariant()
            };

            if (wait) result["summary"] = Summarise(job);
            return result;
        }

        [McpTool("dwsim_dynamics_status",
            "Check on a run. While it runs, returns progress; once it has finished, returns a per-variable " +
            "summary — first, last, minimum, maximum and whether the variable settled. Returns no time-series " +
            "points; call dwsim_dynamics_series for those.")]
        public JObject Status(
            [McpParam("Run handle from dwsim_dynamics_run")] string run_id,
            [McpParam("Include the per-variable summary once finished", Required = false, JsonType = "boolean")]
            bool include_summary = true)
        {
            var job = _jobs.Get(run_id);

            var result = new JObject
            {
                ["run_id"] = job.Id,
                ["kind"] = job.Kind,
                ["state"] = job.State.ToString().ToLowerInvariant(),
                ["steps"] = job.Steps,
                ["simulated_s"] = SeriesDecimator.Format(job.CurrentSeconds),
                ["elapsed_s"] = SeriesDecimator.Format(job.Elapsed.TotalSeconds)
            };

            if (job.Progress.HasValue) result["progress"] = SeriesDecimator.Format(job.Progress.Value);
            if (job.Error != null) result["error"] = BaseMessage(job.Error);

            if (job.IsFinished && include_summary)
            {
                if (job.Result != null) result["summary"] = Summarise(job);
                if (job.Tuning != null) result["tuning"] = Describe(job.Tuning);
            }

            if (job.Kind == "tune")
            {
                result["log"] = new JArray(job.RecentLog().Cast<object>().ToArray());
            }

            return result;
        }

        [McpTool("dwsim_dynamics_abort", "Stop a running integration or tuning search at the next step boundary.")]
        public JObject Abort([McpParam("Run handle")] string run_id)
        {
            var job = _jobs.Abort(run_id);
            return new JObject
            {
                ["run_id"] = job.Id,
                ["state"] = job.State.ToString().ToLowerInvariant(),
                ["steps"] = job.Steps
            };
        }

        // -------------------------------------------------------------- Results

        [McpTool("dwsim_dynamics_series",
            "Read the recorded transient as a decimated preview: a few dozen points chosen to preserve the " +
            "shape, including the peaks. This is the only tool that returns points, and it caps them on " +
            "purpose. For the complete series use dwsim_dynamics_export, which writes a file.")]
        public JObject Series(
            [McpParam("Run handle")] string run_id,
            [McpParam("Variable names to include; all of them when omitted", Required = false)] string[] variables = null,
            [McpParam("Lower time bound in seconds", Required = false, JsonType = "number")] double t_start_s = 0,
            [McpParam("Upper time bound in seconds; the end of the run when omitted", Required = false, JsonType = "number")] double t_end_s = 0,
            [McpParam("Point budget, capped at 400", Required = false, JsonType = "integer")] int max_points = DefaultPreviewPoints,
            [McpParam("columns or csv", Required = false)] string format = "columns")
        {
            var result = RequireResult(run_id);

            var selected = SelectSeries(result, variables);
            if (selected.Count == 0)
            {
                return new JObject
                {
                    ["run_id"] = run_id,
                    ["series"] = new JObject(),
                    ["note"] = "The integrator recorded no variables. Add some with dwsim_dynamics_monitor and run again."
                };
            }

            if (max_points < 3) max_points = 3;
            if (max_points > MaxPreviewPoints) max_points = MaxPreviewPoints;

            double? lo = t_start_s > 0 ? t_start_s : (double?)null;
            double? hi = t_end_s > 0 ? t_end_s : (double?)null;

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var lines = new List<string>();
                lines.Add("t_s," + string.Join(",", selected.Select(s => s.Name.Replace(",", " "))));
                var reference = SeriesDecimator.Preview(selected[0], max_points, lo, hi);
                for (var i = 0; i < reference.Times.Length; i++)
                {
                    var row = new List<string> { SeriesDecimator.Format(reference.Times[i]) };
                    foreach (var s in selected)
                        row.Add(SeriesDecimator.Format(s.ValueAt(reference.Times[i])));
                    lines.Add(string.Join(",", row));
                }
                return new JObject
                {
                    ["run_id"] = run_id,
                    ["csv"] = string.Join("\n", lines),
                    ["points"] = reference.Times.Length,
                    ["decimated_from"] = selected[0].Count
                };
            }

            var body = new JObject();
            var timeline = SeriesDecimator.Preview(selected[0], max_points, lo, hi);

            foreach (var s in selected)
            {
                var values = new JArray();
                foreach (var t in timeline.Times) values.Add(SeriesDecimator.Format(s.ValueAt(t)));
                body[s.Name] = new JObject
                {
                    ["units"] = s.Units,
                    ["values"] = values
                };
            }

            return new JObject
            {
                ["run_id"] = run_id,
                ["t_s"] = new JArray(timeline.Times.Select(t => (object)SeriesDecimator.Format(t)).ToArray()),
                ["series"] = body,
                ["points"] = timeline.Times.Length,
                ["decimated_from"] = selected[0].Count,
                ["note"] = "Decimated preview. Call dwsim_dynamics_export for the full series."
            };
        }

        [McpTool("dwsim_dynamics_analyze",
            "Score the transient the way a control engineer would: overshoot, rise time, settling time, " +
            "steady-state offset and the error integrals, plus a verdict on whether the response is stable, " +
            "oscillating or diverging.")]
        public JObject Analyze(
            [McpParam("Run handle")] string run_id,
            [McpParam("Variable to analyse; every recorded one when omitted", Required = false)] string variable = null,
            [McpParam("Setpoint to score against; taken from the controller when omitted", Required = false, JsonType = "number")]
            double setpoint = double.NaN,
            [McpParam("Settling band as a percentage of the step", Required = false, JsonType = "number")]
            double settling_band_pct = 2.0)
        {
            var result = RequireResult(run_id);
            var selected = SelectSeries(result, variable == null ? null : new[] { variable });

            var band = settling_band_pct / 100.0;
            var analyses = new JArray();

            foreach (var s in selected.Take(MaxListItems))
            {
                var target = double.IsNaN(setpoint) ? s.SteadyState() : setpoint;

                double period, decay;
                var oscillating = s.IsOscillating(out period, out decay);

                var entry = new JObject
                {
                    ["variable"] = s.Name,
                    ["units"] = s.Units,
                    ["initial"] = SeriesDecimator.Format(s.Initial),
                    ["final"] = SeriesDecimator.Format(s.Final),
                    ["min"] = SeriesDecimator.Format(s.Min),
                    ["max"] = SeriesDecimator.Format(s.Max),
                    ["steady_state"] = SeriesDecimator.Format(s.SteadyState()),
                    ["setpoint"] = SeriesDecimator.Format(target),
                    ["offset"] = SeriesDecimator.Format(s.Offset(target)),
                    ["overshoot_pct"] = SeriesDecimator.Format(s.Overshoot(target)),
                    ["peak_time_s"] = SeriesDecimator.Format(s.PeakTime(target)),
                    ["rise_time_s"] = SeriesDecimator.Format(s.RiseTime()),
                    ["settling_time_s"] = SeriesDecimator.Format(s.SettlingTime(band)),
                    ["iae"] = SeriesDecimator.Format(s.IAE(target)),
                    ["ise"] = SeriesDecimator.Format(s.ISE(target)),
                    ["itae"] = SeriesDecimator.Format(s.ITAE(target)),
                    ["verdict"] = Verdict(s, oscillating, decay)
                };

                if (oscillating)
                {
                    entry["oscillation_period_s"] = SeriesDecimator.Format(period);
                    if (!double.IsNaN(decay)) entry["decay_ratio"] = SeriesDecimator.Format(decay);
                }

                analyses.Add(entry);
            }

            return new JObject { ["run_id"] = run_id, ["analysis"] = analyses };
        }

        [McpTool("dwsim_dynamics_export",
            "Write the complete time series to a CSV file on disk. This is how to get at the full data " +
            "without spending context on it.")]
        public JObject Export(
            [McpParam("Run handle")] string run_id,
            [McpParam("Path of the file to write")] string file_path)
        {
            var result = RequireResult(run_id);
            result.ToCsv(file_path);

            return new JObject
            {
                ["run_id"] = run_id,
                ["file_path"] = file_path,
                ["rows"] = result.Series.Count == 0 ? 0 : result.Series.Max(s => s.Count),
                ["variables"] = result.Series.Count
            };
        }

        [McpTool("dwsim_dynamics_diagnose",
            "Explain a run that misbehaved: solver failures mapped to the object that raised them, NaNs, " +
            "divergence, sustained oscillation, saturated controllers, integration steps too large for the " +
            "transient, and controllers whose action is reversed. Each finding carries a fix.")]
        public JObject Diagnose(
            [McpParam("Run handle; omit to check the flowsheet instead", Required = false)] string run_id = null,
            [McpParam("Flowsheet handle, when no run is given", Required = false)] string flowsheet_id = null)
        {
            if (!string.IsNullOrEmpty(run_id))
            {
                var job = _jobs.Get(run_id);
                var fs = _sessions.GetFlowsheet(job.FlowsheetId);

                if (job.Result == null)
                {
                    return new JObject
                    {
                        ["run_id"] = run_id,
                        ["state"] = job.State.ToString().ToLowerInvariant(),
                        ["findings"] = new JArray(),
                        ["note"] = "The run has produced no results yet."
                    };
                }

                var findings = DynamicsDiagnostics.Diagnose(fs.Inner, job.Result);
                return new JObject
                {
                    ["run_id"] = run_id,
                    ["state"] = job.State.ToString().ToLowerInvariant(),
                    ["findings"] = Findings(findings)
                };
            }

            if (string.IsNullOrEmpty(flowsheet_id))
                throw new ArgumentException("Pass either a run_id or a flowsheet_id.");

            var flowsheet = _sessions.GetFlowsheet(flowsheet_id);
            return new JObject
            {
                ["flowsheet_id"] = flowsheet_id,
                ["findings"] = Findings(DynamicsDiagnostics.CheckReady(flowsheet.Inner))
            };
        }

        // --------------------------------------------------------------- Tuning

        [McpTool("dwsim_dynamics_tune_pid",
            "Tune PID controllers by simulation: a Nelder-Mead search over their gains, running the schedule " +
            "once per trial and scoring the transient. Returns a run_id; poll dwsim_dynamics_status. Needs a " +
            "stored initial state to make trials comparable, and captures one when the schedule has none.")]
        public JObject TunePid(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Controller tags to tune; all of them when omitted", Required = false)] string[] controllers = null,
            [McpParam("Schedule to run for each trial", Required = false)] string schedule = null,
            [McpParam("IAE, ISE, ITAE or CumulativeError", Required = false)] string objective = "IAE",
            [McpParam("Trial budget", Required = false, JsonType = "integer")] int max_evaluations = 30,
            [McpParam("Leave the tuned gains on the controllers", Required = false, JsonType = "boolean")] bool apply = true,
            [McpParam("Give up on a single trial after this many seconds", Required = false, JsonType = "integer")] int max_wall_time_per_run_s = 120,
            [McpParam("Block until tuning finishes", Required = false, JsonType = "boolean")] bool wait = false)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);

            TuningObjective parsedObjective;
            if (!Enum.TryParse(objective, true, out parsedObjective))
            {
                throw new ArgumentException("Unknown objective '" + objective + "'. Use one of: " +
                    string.Join(", ", Enum.GetNames(typeof(TuningObjective))) + ".");
            }

            var job = _jobs.Start(flowsheet_id, "tune", record =>
            {
                record.Tuning = PidTuner.Tune(fs.Inner, new PidTuningOptions
                {
                    ScheduleName = schedule,
                    ControllerTags = controllers,
                    Objective = parsedObjective,
                    MaxEvaluations = max_evaluations,
                    Apply = apply,
                    MaxWallTimePerRun = TimeSpan.FromSeconds(max_wall_time_per_run_s),
                    AbortRequested = () => record.Cancellation.IsCancellationRequested,
                    OnProgress = line =>
                    {
                        record.Log(line);
                        record.Steps += 1;
                    }
                });

                if (record.Tuning.Error != null) record.Error = record.Tuning.Error;
            });

            if (wait) WaitFor(job, max_evaluations * max_wall_time_per_run_s + 30);

            var result = new JObject
            {
                ["started"] = true,
                ["run_id"] = job.Id,
                ["objective"] = parsedObjective.ToString(),
                ["max_evaluations"] = max_evaluations,
                ["state"] = job.State.ToString().ToLowerInvariant()
            };

            if (wait && job.Tuning != null) result["tuning"] = Describe(job.Tuning);
            return result;
        }

        // -------------------------------------------------------------------------

        private static void Require(string value, string what)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("This action needs " + what + ".");
        }

        private string ResolveIntegratorName(FluentFlowsheet fs, string integrator)
        {
            if (!string.IsNullOrEmpty(integrator)) return integrator;

            var schedule = IntegratorRunner.ResolveSchedule(fs.Inner, null);
            if (!fs.Inner.DynamicsManager.IntegratorList.ContainsKey(schedule.CurrentIntegrator))
            {
                throw new InvalidOperationException("Schedule '" + schedule.Description +
                    "' has no integrator. Call dwsim_dynamics_setup first.");
            }
            return fs.Inner.DynamicsManager.IntegratorList[schedule.CurrentIntegrator].Description;
        }

        private static DynEnums.DynamicsEventTransitionType ParseTransition(string kind)
        {
            switch (kind)
            {
                case "linear": return DynEnums.DynamicsEventTransitionType.LinearChange;
                case "log": return DynEnums.DynamicsEventTransitionType.LogChange;
                case "inverse_log": return DynEnums.DynamicsEventTransitionType.InverseLogChange;
                case "random": return DynEnums.DynamicsEventTransitionType.RandomChange;
                case "step": return DynEnums.DynamicsEventTransitionType.StepChange;
                default:
                    throw new ArgumentException("Unknown transition '" + kind +
                        "'. Use step, linear, log, inverse_log or random.");
            }
        }

        private DynamicsResult RequireResult(string runId)
        {
            var job = _jobs.Get(runId);

            if (job.Result == null)
            {
                throw new InvalidOperationException("Run " + runId + " is " +
                    job.State.ToString().ToLowerInvariant() + " and has no results yet." +
                    (job.Error == null ? "" : " It failed: " + BaseMessage(job.Error)));
            }

            return job.Result;
        }

        private static List<DynamicsSeries> SelectSeries(DynamicsResult result, string[] wanted)
        {
            if (wanted == null || wanted.Length == 0) return result.Series.ToList();

            var selected = new List<DynamicsSeries>();
            foreach (var name in wanted)
            {
                DynamicsSeries series;
                if (!result.TryGetSeries(name, out series))
                {
                    throw new ArgumentException("No monitored variable named '" + name + "'. Available: " +
                        string.Join(", ", result.Series.Select(s => "'" + s.Name + "'")) + ".");
                }
                selected.Add(series);
            }
            return selected;
        }

        private static JObject Summarise(DynamicsJob job)
        {
            var result = job.Result;

            var variables = new JArray();
            foreach (var s in result.Series.Take(MaxListItems))
            {
                variables.Add(new JObject
                {
                    ["variable"] = s.Name,
                    ["units"] = s.Units,
                    ["first"] = SeriesDecimator.Format(s.Initial),
                    ["last"] = SeriesDecimator.Format(s.Final),
                    ["min"] = SeriesDecimator.Format(s.Min),
                    ["max"] = SeriesDecimator.Format(s.Max),
                    ["settled"] = s.HasConverged(),
                    ["diverged"] = s.HasDiverged
                });
            }

            return new JObject
            {
                ["schedule"] = result.ScheduleName,
                ["integrator"] = result.IntegratorName,
                ["completed"] = result.Completed,
                ["aborted"] = result.Aborted,
                ["steps"] = result.Steps,
                ["simulated_s"] = SeriesDecimator.Format(result.FinalTimeSeconds),
                ["wall_clock_s"] = SeriesDecimator.Format(result.WallClock.TotalSeconds),
                ["variables"] = variables,
                ["errors"] = new JArray(result.Errors.Take(5).Select(e => (object)BaseMessage(e)).ToArray())
            };
        }

        private static JObject Describe(PidTuningResult tuning)
        {
            return new JObject
            {
                ["succeeded"] = tuning.Succeeded,
                ["applied"] = tuning.Applied,
                ["aborted"] = tuning.Aborted,
                ["evaluations"] = tuning.Evaluations,
                ["initial_objective"] = SeriesDecimator.Format(tuning.InitialObjective),
                ["final_objective"] = SeriesDecimator.Format(tuning.FinalObjective),
                ["improvement_pct"] = SeriesDecimator.Format(tuning.ImprovementPercent),
                ["error"] = tuning.Error == null ? null : BaseMessage(tuning.Error),
                ["controllers"] = new JArray(tuning.Controllers.Select(c => (object)new JObject
                {
                    ["tag"] = c.Tag,
                    ["kp"] = SeriesDecimator.Format(c.Kp),
                    ["ki"] = SeriesDecimator.Format(c.Ki),
                    ["kd"] = SeriesDecimator.Format(c.Kd),
                    ["original_kp"] = SeriesDecimator.Format(c.OriginalKp),
                    ["original_ki"] = SeriesDecimator.Format(c.OriginalKi),
                    ["original_kd"] = SeriesDecimator.Format(c.OriginalKd)
                }).ToArray())
            };
        }

        private static JObject Describe(ControllerInfo c)
        {
            return new JObject
            {
                ["tag"] = c.Tag,
                ["wired"] = c.IsWired,
                ["active"] = c.Active,
                ["manual"] = c.ManualOverride,
                ["reverse_acting"] = c.ReverseActing,
                ["execution_order"] = c.ExecutionOrder,
                ["kp"] = SeriesDecimator.Format(c.Kp),
                ["ki"] = SeriesDecimator.Format(c.Ki),
                ["kd"] = SeriesDecimator.Format(c.Kd),
                ["sp"] = SeriesDecimator.Format(c.SetPoint),
                ["pv"] = SeriesDecimator.Format(c.ProcessVariable),
                ["mv"] = SeriesDecimator.Format(c.ManipulatedVariable),
                ["out_min"] = SeriesDecimator.Format(c.OutputMin),
                ["out_max"] = SeriesDecimator.Format(c.OutputMax),
                ["controls"] = c.ControlledObjectId + "." + c.ControlledProperty,
                ["manipulates"] = c.ManipulatedObjectId + "." + c.ManipulatedProperty
            };
        }

        private static string Verdict(DynamicsSeries s, bool oscillating, double decay)
        {
            if (s.HasDiverged) return "divergent";
            if (oscillating && (double.IsNaN(decay) || decay > 0.9)) return "sustained_oscillation";
            if (oscillating) return "damped_oscillation";
            if (!s.HasConverged()) return "still_moving";
            return "stable";
        }

        private static JArray Findings(IEnumerable<Finding> findings)
        {
            return FindingsJson.From(findings);
        }

        private static JArray Truncate(IReadOnlyList<string> items)
        {
            return new JArray(items.Take(MaxListItems).Cast<object>().ToArray());
        }

        private static void WaitFor(DynamicsJob job, int timeoutSeconds)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (!job.IsFinished && DateTime.UtcNow < deadline)
                System.Threading.Thread.Sleep(100);
        }

        private static string BaseMessage(Exception ex)
        {
            var baseex = ex;
            while (baseex.InnerException != null) baseex = baseex.InnerException;
            return baseex.Message;
        }

        private static double ToDouble(object value)
        {
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return double.NaN; }
        }
    }
}
