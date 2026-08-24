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
    /// Configures a cause-and-effect matrix: the interlocks that fire when an indicator's alarm goes
    /// active during a run. Obtain one from <see cref="DynamicsConfigBuilder.DefineCauseAndEffectMatrix"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// fs.Dynamics.DefineCauseAndEffectMatrix("Trips")
    ///   .AddItem("close the feed valve on high level")
    ///       .WhenAlarm("LI-01", DynEnums.DynamicsAlarmType.HH)
    ///       .Then("V-01", "Opening", 0.0, "%")
    ///   .And();
    /// </code>
    /// </example>
    public sealed class CauseAndEffectMatrixBuilder
    {
        private readonly Flowsheet _flowsheet;

        internal CauseAndEffectMatrixBuilder(Flowsheet flowsheet, IDynamicsCauseAndEffectMatrix matrix)
        {
            _flowsheet = flowsheet;
            Object = matrix;
        }

        /// <summary>The underlying DWSIM matrix.</summary>
        public IDynamicsCauseAndEffectMatrix Object { get; }

        /// <summary>The matrix's description, which is how a schedule refers to it.</summary>
        public string Name => Object.Description;

        /// <summary>The matrix's internal ID.</summary>
        public string Id => Object.ID;

        internal Flowsheet Owner => _flowsheet;

        /// <summary>Adds an interlock and returns its builder. Chain <c>WhenAlarm</c> and <c>Then</c>, then <c>And()</c>.</summary>
        public CauseAndEffectItemBuilder AddItem(string description)
        {
            var item = new CauseAndEffectItem
            {
                ID = Guid.NewGuid().ToString(),
                Description = description,
                Enabled = true
            };
            Object.Items.Add(item.ID, item);
            return new CauseAndEffectItemBuilder(this, item);
        }

        /// <summary>Removes the interlock with this description; does nothing when there is none.</summary>
        public CauseAndEffectMatrixBuilder RemoveItem(string description)
        {
            var match = Object.Items.Values
                .FirstOrDefault(i => string.Equals(i.Description, description, StringComparison.OrdinalIgnoreCase));
            if (match != null) Object.Items.Remove(match.ID);
            return this;
        }

        /// <summary>Descriptions of every interlock in the matrix.</summary>
        public IReadOnlyList<string> ItemDescriptions =>
            Object.Items.Values.Select(i => i.Description).ToList();
    }

    /// <summary>
    /// Configures a single interlock: an alarm condition and the property change it triggers.
    /// Obtain one from <see cref="CauseAndEffectMatrixBuilder.AddItem"/>.
    /// </summary>
    public sealed class CauseAndEffectItemBuilder
    {
        private readonly CauseAndEffectMatrixBuilder _matrix;

        internal CauseAndEffectItemBuilder(CauseAndEffectMatrixBuilder matrix, IDynamicsCauseAndEffectItem item)
        {
            _matrix = matrix;
            Object = item;
        }

        /// <summary>The underlying DWSIM matrix item.</summary>
        public IDynamicsCauseAndEffectItem Object { get; }

        /// <summary>Sets the alarm that triggers this interlock, on an indicator object.</summary>
        public CauseAndEffectItemBuilder WhenAlarm(string indicatorTag, DynEnums.DynamicsAlarmType alarm)
        {
            var obj = _matrix.Owner.ResolveByTag(indicatorTag);
            if (!(obj is IIndicator))
                throw new ArgumentException("'" + indicatorTag + "' is not an indicator.", nameof(indicatorTag));
            Object.AssociatedIndicator = obj.Name;
            Object.AssociatedIndicatorAlarm = alarm;
            return this;
        }

        /// <summary>Sets what the interlock does when it fires: assign a value to a property.</summary>
        public CauseAndEffectItemBuilder Then(string objectTag, string propertyId, double value, string units = null)
        {
            var obj = _matrix.Owner.ResolveByTag(objectTag);
            var su = _matrix.Owner.Inner.FlowsheetOptions.SelectedUnitSystem;

            PropertyCatalog.EnsureDynamicProperties(obj);

            if (units == null)
            {
                units = obj.IsDynamicProperty(propertyId)
                    ? su.GetCurrentUnits(obj.GetDynamicPropertyUnitType(propertyId))
                    : obj.GetPropertyUnit(propertyId, su);
            }

            Object.SimulationObjectID = obj.Name;
            Object.SimulationObjectProperty = propertyId;
            Object.SimulationObjectPropertyValue = value.ToString(CultureInfo.InvariantCulture);
            Object.SimulationObjectPropertyUnits = units ?? "";
            return this;
        }

        /// <summary>Enables or disables the interlock without removing it.</summary>
        public CauseAndEffectItemBuilder Enabled(bool enabled = true)
        {
            Object.Enabled = enabled;
            return this;
        }

        /// <summary>Returns to the matrix, to add the next interlock.</summary>
        public CauseAndEffectMatrixBuilder And() => _matrix;
    }
}
