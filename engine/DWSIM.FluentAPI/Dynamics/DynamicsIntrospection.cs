using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.UnitOperations.SpecialOps;
using DynEnums = DWSIM.Interfaces.Enums.Dynamics;

namespace DWSIM.Automation.FluentAPI.Dynamics
{
    /// <summary>One object on the flowsheet, described from a dynamic-simulation point of view.</summary>
    public sealed class DynamicObjectInfo
    {
        internal DynamicObjectInfo(string tag, string id, string type, bool supportsDynamics,
            DynEnums.DynamicsSpecType spec, IReadOnlyList<PropertyEntry> dynamicProperties)
        {
            Tag = tag;
            Id = id;
            Type = type;
            SupportsDynamics = supportsDynamics;
            DynamicsSpec = spec;
            DynamicProperties = dynamicProperties;
        }

        /// <summary>The object's tag, as shown on the flowsheet.</summary>
        public string Tag { get; }

        /// <summary>The object's internal name, which events and monitored variables address it by.</summary>
        public string Id { get; }

        /// <summary>The object's display type, e.g. "Tank".</summary>
        public string Type { get; }

        /// <summary>
        /// False when the object has no dynamic model. It is still solved at every step, but at
        /// steady state — it contributes no hold-up and no lag.
        /// </summary>
        public bool SupportsDynamics { get; }

        /// <summary>Whether the object is specified by pressure or by flow in the pressure-flow network.</summary>
        public DynEnums.DynamicsSpecType DynamicsSpec { get; }

        /// <summary>The object's dynamic-mode properties, with descriptions, units and current values.</summary>
        public IReadOnlyList<PropertyEntry> DynamicProperties { get; }
    }

    /// <summary>A PID controller's wiring and tuning, read off the flowsheet.</summary>
    public sealed class ControllerInfo
    {
        internal ControllerInfo(PIDController pid, string tag)
        {
            Tag = tag;
            Id = pid.Name;
            Active = pid.Active;
            ManualOverride = pid.ManualOverride;
            ReverseActing = pid.ReverseActing;
            ExecutionOrder = pid.ExecutionOrder;
            Kp = pid.Kp;
            Ki = pid.Ki;
            Kd = pid.Kd;
            SetPoint = pid.SetPoint;
            ProcessVariable = pid.PVValue;
            ManipulatedVariable = pid.MVValue;
            Output = pid.Output;
            OutputMin = pid.OutputMin;
            OutputMax = pid.OutputMax;
            CumulativeError = pid.CumulativeError;
            CascadeMasterId = pid.CascadeMasterID;

            var controlled = pid.ControlledObjectData;
            var manipulated = pid.ManipulatedObjectData;

            ControlledObjectId = controlled == null ? "" : controlled.ID;
            ControlledProperty = controlled == null ? "" : controlled.PropertyName;
            ControlledUnits = controlled == null ? "" : controlled.Units;
            ManipulatedObjectId = manipulated == null ? "" : manipulated.ID;
            ManipulatedProperty = manipulated == null ? "" : manipulated.PropertyName;
            ManipulatedUnits = manipulated == null ? "" : manipulated.Units;
        }

        /// <summary>The controller's tag.</summary>
        public string Tag { get; }

        /// <summary>The controller's internal name.</summary>
        public string Id { get; }

        /// <summary>Whether the controller runs at all.</summary>
        public bool Active { get; }

        /// <summary>Whether the controller is in manual, holding its output fixed.</summary>
        public bool ManualOverride { get; }

        /// <summary>Whether the control action is reversed.</summary>
        public bool ReverseActing { get; }

        /// <summary>Position in the controller execution order, low first.</summary>
        public int ExecutionOrder { get; }

        /// <summary>Proportional gain.</summary>
        public double Kp { get; }

        /// <summary>Integral gain.</summary>
        public double Ki { get; }

        /// <summary>Derivative gain.</summary>
        public double Kd { get; }

        /// <summary>Setpoint, in the controlled property's display units.</summary>
        public double SetPoint { get; }

        /// <summary>Process variable at the last solved step.</summary>
        public double ProcessVariable { get; }

        /// <summary>Manipulated variable at the last solved step.</summary>
        public double ManipulatedVariable { get; }

        /// <summary>Controller output at the last solved step.</summary>
        public double Output { get; }

        /// <summary>Lower output clamp.</summary>
        public double OutputMin { get; }

        /// <summary>Upper output clamp.</summary>
        public double OutputMax { get; }

        /// <summary>Accumulated error over the last run.</summary>
        public double CumulativeError { get; }

        /// <summary>Internal name of the master controller in a cascade, empty when standalone.</summary>
        public string CascadeMasterId { get; }

        /// <summary>Internal name of the object holding the process variable.</summary>
        public string ControlledObjectId { get; }

        /// <summary>Property identifier of the process variable.</summary>
        public string ControlledProperty { get; }

        /// <summary>Display units of the process variable.</summary>
        public string ControlledUnits { get; }

        /// <summary>Internal name of the object holding the manipulated variable.</summary>
        public string ManipulatedObjectId { get; }

        /// <summary>Property identifier of the manipulated variable.</summary>
        public string ManipulatedProperty { get; }

        /// <summary>Display units of the manipulated variable.</summary>
        public string ManipulatedUnits { get; }

        /// <summary>True when the controller has both a process and a manipulated variable wired.</summary>
        public bool IsWired =>
            !string.IsNullOrEmpty(ControlledObjectId) && !string.IsNullOrEmpty(ControlledProperty) &&
            !string.IsNullOrEmpty(ManipulatedObjectId) && !string.IsNullOrEmpty(ManipulatedProperty);
    }

    /// <summary>What a flowsheet holds that matters to a dynamic simulation.</summary>
    public sealed class DynamicsInventory
    {
        internal DynamicsInventory(bool dynamicMode, IReadOnlyList<DynamicObjectInfo> objects,
            IReadOnlyList<ControllerInfo> controllers, IReadOnlyList<string> indicators,
            IReadOnlyList<string> schedules, IReadOnlyList<string> integrators,
            IReadOnlyList<string> eventSets, IReadOnlyList<string> matrices,
            IReadOnlyList<string> storedStates, string currentSchedule)
        {
            DynamicModeEnabled = dynamicMode;
            Objects = objects;
            Controllers = controllers;
            Indicators = indicators;
            Schedules = schedules;
            Integrators = integrators;
            EventSets = eventSets;
            CauseAndEffectMatrices = matrices;
            StoredStates = storedStates;
            CurrentSchedule = currentSchedule;
        }

        /// <summary>Whether the flowsheet is currently in dynamic mode.</summary>
        public bool DynamicModeEnabled { get; }

        /// <summary>Every simulation object, with its dynamic capabilities.</summary>
        public IReadOnlyList<DynamicObjectInfo> Objects { get; }

        /// <summary>Every PID controller, with its wiring and tuning.</summary>
        public IReadOnlyList<ControllerInfo> Controllers { get; }

        /// <summary>Tags of every indicator, which is what a cause-and-effect matrix reacts to.</summary>
        public IReadOnlyList<string> Indicators { get; }

        /// <summary>Descriptions of the defined schedules.</summary>
        public IReadOnlyList<string> Schedules { get; }

        /// <summary>Descriptions of the defined integrators.</summary>
        public IReadOnlyList<string> Integrators { get; }

        /// <summary>Descriptions of the defined event sets.</summary>
        public IReadOnlyList<string> EventSets { get; }

        /// <summary>Descriptions of the defined cause-and-effect matrices.</summary>
        public IReadOnlyList<string> CauseAndEffectMatrices { get; }

        /// <summary>Names of the stored flowsheet states a schedule can start from.</summary>
        public IReadOnlyList<string> StoredStates { get; }

        /// <summary>Description of the current schedule, empty when none is selected.</summary>
        public string CurrentSchedule { get; }

        /// <summary>Objects that carry a dynamic model, and so contribute hold-up and lag.</summary>
        public IEnumerable<DynamicObjectInfo> DynamicCapableObjects => Objects.Where(o => o.SupportsDynamics);
    }

    /// <summary>
    /// Reads what a flowsheet offers a dynamic simulation: which objects have dynamic models, how
    /// the pressure-flow network is specified, how the controllers are wired, and what the Dynamics
    /// Manager already holds.
    /// </summary>
    public static class DynamicsIntrospection
    {
        /// <summary>Surveys the flowsheet.</summary>
        public static DynamicsInventory Inspect(IFlowsheet flowsheet)
        {
            if (flowsheet == null) throw new ArgumentNullException(nameof(flowsheet));

            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var objects = new List<DynamicObjectInfo>();
            var controllers = new List<ControllerInfo>();
            var indicators = new List<string>();

            foreach (var obj in flowsheet.SimulationObjects.Values)
            {
                var tag = obj.GraphicObject != null ? obj.GraphicObject.Tag : obj.Name;

                IReadOnlyList<PropertyEntry> dynamicProperties;
                try { dynamicProperties = PropertyCatalog.DynamicFor(obj, su); }
                catch { dynamicProperties = new PropertyEntry[0]; }

                objects.Add(new DynamicObjectInfo(tag, obj.Name, SafeTypeName(obj),
                    obj.SupportsDynamicMode, obj.DynamicsSpec, dynamicProperties));

                var pid = obj as PIDController;
                if (pid != null) controllers.Add(new ControllerInfo(pid, tag));

                if (obj is IIndicator) indicators.Add(tag);
            }

            var manager = flowsheet.DynamicsManager;

            var currentSchedule = "";
            if (!string.IsNullOrEmpty(manager.CurrentSchedule) &&
                manager.ScheduleList.ContainsKey(manager.CurrentSchedule))
            {
                currentSchedule = manager.ScheduleList[manager.CurrentSchedule].Description;
            }

            return new DynamicsInventory(
                flowsheet.DynamicMode,
                objects,
                controllers,
                indicators,
                manager.ScheduleList.Values.Select(x => x.Description).ToList(),
                manager.IntegratorList.Values.Select(x => x.Description).ToList(),
                manager.EventSetList.Values.Select(x => x.Description).ToList(),
                manager.CauseAndEffectMatrixList.Values.Select(x => x.Description).ToList(),
                flowsheet.StoredSolutions.Keys.ToList(),
                currentSchedule);
        }

        /// <summary>
        /// Lists the properties of one object that make sense to monitor or disturb: the numeric
        /// regular properties plus every dynamic-mode property.
        /// </summary>
        public static IReadOnlyList<PropertyEntry> AddressableProperties(IFlowsheet flowsheet, string tag)
        {
            var obj = Resolve(flowsheet, tag);
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;

            // Dynamic properties also show up in GetProperties once they exist, so the two lists
            // overlap. The dynamic entry is the better one — it carries the real unit type.
            var dynamic = PropertyCatalog.DynamicFor(obj, su);
            var seen = new HashSet<string>(dynamic.Select(p => p.Id), StringComparer.Ordinal);

            var list = new List<PropertyEntry>(dynamic);
            list.AddRange(PropertyCatalog.Monitorable(obj, su).Where(p => !seen.Contains(p.Id)));
            return list;
        }

        /// <summary>Finds an object by tag, falling back to its internal name.</summary>
        public static ISimulationObject Resolve(IFlowsheet flowsheet, string tagOrId)
        {
            var match = flowsheet.SimulationObjects.Values.FirstOrDefault(
                o => o.GraphicObject != null &&
                     string.Equals(o.GraphicObject.Tag, tagOrId, StringComparison.Ordinal));

            if (match == null && flowsheet.SimulationObjects.ContainsKey(tagOrId))
                match = flowsheet.SimulationObjects[tagOrId];

            if (match == null)
                throw new KeyNotFoundException("No simulation object with tag or id '" + tagOrId + "'.");

            return match;
        }

        private static string SafeTypeName(ISimulationObject obj)
        {
            try { return obj.GetDisplayName(); }
            catch { return obj.GetType().Name; }
        }
    }
}
