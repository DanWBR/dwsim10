using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DWSIM.DynamicsManager;
using DWSIM.Interfaces;
using DynEnums = DWSIM.Interfaces.Enums.Dynamics;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>
    /// Configures a set of timed disturbances applied to the flowsheet during a run: the step
    /// changes and ramps that make a dynamic simulation worth running.
    /// Obtain one from <see cref="DynamicsConfigBuilder.DefineEventSet"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.Dynamics.DefineEventSet("Upsets")
    ///   .AddStepChange("feed", "PROP_MS_2", 2.5, at: 60.Seconds(), units: "kg/s")
    ///   .AddEvent("ramp the feed up")
    ///       .At(120.Seconds())
    ///       .ChangeProperty("feed", "PROP_MS_2", 5.0, "kg/s")
    ///       .WithTransition(DynEnums.DynamicsEventTransitionType.LinearChange)
    ///   .And();
    /// </code>
    /// </example>
    public sealed class EventSetBuilder
    {
        private readonly Flowsheet _flowsheet;

        internal EventSetBuilder(Flowsheet flowsheet, IDynamicsEventSet eventSet)
        {
            _flowsheet = flowsheet;
            Object = eventSet;
        }

        /// <summary>The underlying DWSIM event set.</summary>
        public IDynamicsEventSet Object { get; }

        /// <summary>The event set's description, which is how a schedule refers to it.</summary>
        public string Name => Object.Description;

        /// <summary>The event set's internal ID.</summary>
        public string Id => Object.ID;

        internal Flowsheet Owner => _flowsheet;

        /// <summary>
        /// Adds an event and returns its builder. Chain <c>At</c> and <c>ChangeProperty</c> on it,
        /// then <c>And()</c> to come back here.
        /// </summary>
        public DynamicEventBuilder AddEvent(string description)
        {
            var ev = new DynamicEvent
            {
                ID = Guid.NewGuid().ToString(),
                Description = description,
                Enabled = true,
                TimeStamp = new DateTime(),
                EventType = DynEnums.DynamicsEventType.ChangeProperty,
                TransitionType = DynEnums.DynamicsEventTransitionType.StepChange,
                TransitionReference = DynEnums.DynamicsEventTransitionReferenceType.PreviousEvent
            };
            Object.Events.Add(ev.ID, ev);
            return new DynamicEventBuilder(this, ev);
        }

        /// <summary>Sets a property to a new value the instant <paramref name="at"/> is reached.</summary>
        public EventSetBuilder AddStepChange(string objectTag, string propertyId, double value, Quantity at,
            string units = null, string description = null)
        {
            return AddEvent(description ?? DefaultDescription(objectTag, propertyId, "step"))
                .At(at)
                .ChangeProperty(objectTag, propertyId, value, units)
                .WithTransition(DynEnums.DynamicsEventTransitionType.StepChange)
                .And();
        }

        /// <summary>
        /// Ramps a property linearly up to <paramref name="value"/>, arriving at <paramref name="at"/>.
        /// </summary>
        /// <remarks>
        /// The ramp interpolates from the state recorded at its reference event — by default the
        /// previous event in the set, or the start of the run when there is none — up to its own
        /// instant. That recorded state is the one from *before* the reference event was applied,
        /// so a ramp placed after a step change on the same property ramps from the value the step
        /// replaced, not from the step's value. Ramps read the historian, so leave it enabled.
        /// </remarks>
        public EventSetBuilder AddRamp(string objectTag, string propertyId, double value, Quantity at,
            string units = null, string description = null)
        {
            return AddEvent(description ?? DefaultDescription(objectTag, propertyId, "ramp"))
                .At(at)
                .ChangeProperty(objectTag, propertyId, value, units)
                .WithTransition(DynEnums.DynamicsEventTransitionType.LinearChange)
                .And();
        }

        /// <summary>Removes the event with this description; does nothing when there is none.</summary>
        public EventSetBuilder RemoveEvent(string description)
        {
            var match = Object.Events.Values
                .FirstOrDefault(e => string.Equals(e.Description, description, StringComparison.OrdinalIgnoreCase));
            if (match != null) Object.Events.Remove(match.ID);
            return this;
        }

        /// <summary>Removes every event from the set.</summary>
        public EventSetBuilder ClearEvents()
        {
            Object.Events.Clear();
            return this;
        }

        /// <summary>The events in this set, ordered by time, as "t = 12 s: description".</summary>
        public IReadOnlyList<string> EventDescriptions =>
            Object.Events.Values
                .OrderBy(e => e.TimeStamp)
                .Select(e => "t = " + (e.TimeStamp - new DateTime()).TotalSeconds.ToString("G6", CultureInfo.InvariantCulture) +
                             " s: " + e.Description)
                .ToList();

        private static string DefaultDescription(string objectTag, string propertyId, string kind)
        {
            return kind + " " + objectTag + "." + propertyId;
        }
    }

    /// <summary>
    /// Configures a single timed event. Obtain one from <see cref="EventSetBuilder.AddEvent"/>.
    /// </summary>
    public sealed class DynamicEventBuilder
    {
        private readonly EventSetBuilder _set;

        internal DynamicEventBuilder(EventSetBuilder set, IDynamicsEvent ev)
        {
            _set = set;
            Object = ev;
        }

        /// <summary>The underlying DWSIM event.</summary>
        public IDynamicsEvent Object { get; }

        /// <summary>
        /// Schedules the event at this point in simulated time, counted from the start of the run.
        /// An event at t = 0 fires on the first step.
        /// </summary>
        public DynamicEventBuilder At(Quantity time) => At(TimeSpan.FromSeconds(time.SI));

        /// <summary>Schedules the event at this point in simulated time, counted from the start of the run.</summary>
        public DynamicEventBuilder At(TimeSpan time)
        {
            Object.TimeStamp = new DateTime().Add(time);
            return this;
        }

        /// <summary>
        /// Sets what the event does: assign <paramref name="value"/> to a property of an object.
        /// </summary>
        /// <param name="objectTag">Tag of the object to disturb.</param>
        /// <param name="propertyId">Property identifier; use <see cref="Flowsheet.Properties"/> to discover them.</param>
        /// <param name="value">Target value, expressed in <paramref name="units"/>.</param>
        /// <param name="units">Units of <paramref name="value"/>; the object's current display units when null.</param>
        public DynamicEventBuilder ChangeProperty(string objectTag, string propertyId, double value, string units = null)
        {
            var obj = _set.Owner.ResolveByTag(objectTag);
            var su = _set.Owner.Inner.FlowsheetOptions.SelectedUnitSystem;

            PropertyCatalog.EnsureDynamicProperties(obj);

            if (units == null)
            {
                units = obj.IsDynamicProperty(propertyId)
                    ? su.GetCurrentUnits(obj.GetDynamicPropertyUnitType(propertyId))
                    : obj.GetPropertyUnit(propertyId, su);
            }

            Object.EventType = DynEnums.DynamicsEventType.ChangeProperty;
            Object.SimulationObjectID = obj.Name;
            Object.SimulationObjectProperty = propertyId;
            Object.SimulationObjectPropertyValue = value.ToString(CultureInfo.InvariantCulture);
            Object.SimulationObjectPropertyUnits = units ?? "";
            return this;
        }

        /// <summary>
        /// Chooses how the property gets to its new value, and what the transition starts from.
        /// Anything other than a step change interpolates from a past state, which needs the historian.
        /// </summary>
        public DynamicEventBuilder WithTransition(
            DynEnums.DynamicsEventTransitionType type,
            DynEnums.DynamicsEventTransitionReferenceType reference = DynEnums.DynamicsEventTransitionReferenceType.PreviousEvent,
            string referenceEventDescription = null)
        {
            Object.TransitionType = type;
            Object.TransitionReference = reference;

            if (referenceEventDescription != null)
            {
                var match = _set.Object.Events.Values.FirstOrDefault(
                    e => string.Equals(e.Description, referenceEventDescription, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    throw new KeyNotFoundException(
                        "No event named '" + referenceEventDescription + "' in event set '" + _set.Name + "'.");
                Object.TransitionReferenceEventID = match.ID;
                Object.TransitionReference = DynEnums.DynamicsEventTransitionReferenceType.SpecificEvent;
            }

            return this;
        }

        /// <summary>Enables or disables the event without removing it from the set.</summary>
        public DynamicEventBuilder Enabled(bool enabled = true)
        {
            Object.Enabled = enabled;
            return this;
        }

        /// <summary>Returns to the event set, to add the next event.</summary>
        public EventSetBuilder And() => _set;
    }
}
