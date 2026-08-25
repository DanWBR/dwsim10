using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// One property of a simulation object, as seen from the outside: the ID you pass to
    /// <c>Monitor</c>, <c>ChangeProperty</c> or <c>SetPropertyValue</c>, plus enough metadata to
    /// decide whether it is the one you wanted.
    /// </summary>
    public sealed class PropertyEntry
    {
        internal PropertyEntry(string id, string description, string units, object value, bool isDynamic, bool writable)
        {
            Id = id;
            Description = description;
            Units = units;
            Value = value;
            IsDynamic = isDynamic;
            Writable = writable;
        }

        /// <summary>Property identifier, e.g. <c>"PROP_MS_2"</c> or a dynamic property name like <c>"Liquid Level"</c>.</summary>
        public string Id { get; }

        /// <summary>Human-readable description, as shown in DWSIM's property grid.</summary>
        public string Description { get; }

        /// <summary>Display units for the value, empty when the property is dimensionless.</summary>
        public string Units { get; }

        /// <summary>Current value in display units, or null when it could not be read.</summary>
        public object Value { get; }

        /// <summary>True for properties that only exist in dynamic mode (the object's extra properties).</summary>
        public bool IsDynamic { get; }

        /// <summary>True when the property can be written to.</summary>
        public bool Writable { get; }

        /// <summary>Returns <c>"&lt;id&gt; — &lt;description&gt; (&lt;units&gt;)"</c>.</summary>
        public override string ToString()
        {
            var unit = string.IsNullOrEmpty(Units) ? "" : " (" + Units + ")";
            return Id + " — " + Description + unit;
        }
    }

    /// <summary>
    /// Lists the properties a simulation object exposes. This is what makes monitored variables and
    /// dynamic events discoverable: both are addressed by property ID, and those IDs are not
    /// guessable from outside DWSIM.
    /// </summary>
    public static class PropertyCatalog
    {
        /// <summary>Lists the object's regular properties of the given kind.</summary>
        public static IReadOnlyList<PropertyEntry> For(ISimulationObject obj, IUnitsOfMeasure su,
            PropertyType type = PropertyType.ALL)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var writable = new HashSet<string>(SafeGetProperties(obj, PropertyType.WR), StringComparer.Ordinal);
            var list = new List<PropertyEntry>();

            foreach (var id in SafeGetProperties(obj, type))
            {
                list.Add(new PropertyEntry(
                    id,
                    Describe(obj, id),
                    Try(() => obj.GetPropertyUnit(id, su), ""),
                    Try<object>(() => obj.GetPropertyValue(id, su), null),
                    false,
                    writable.Contains(id)));
            }

            return list;
        }

        /// <summary>
        /// The most readable name a property identifier has. Most objects do not override
        /// <c>GetPropertyDescription</c> and return a placeholder, so fall back to the flowsheet's
        /// localised name — which is what DWSIM's own property grid shows — and to the identifier.
        /// </summary>
        public static string Describe(ISimulationObject obj, string propertyId)
        {
            var description = Try(() => obj.GetPropertyDescription(propertyId), null);

            // Most objects answer with the identifier itself, which describes nothing. Taking
            // it would end the search before the flowsheet is ever asked for the real name.
            if (!string.IsNullOrWhiteSpace(description) &&
                !string.Equals(description, propertyId, StringComparison.Ordinal) &&
                !description.StartsWith("No description", StringComparison.OrdinalIgnoreCase))
            {
                return description;
            }

            var flowsheet = Try<IFlowsheet>(() => obj.GetFlowsheet(), null);
            if (flowsheet != null)
            {
                var translated = Try(() => flowsheet.GetTranslatedString(propertyId), null);
                if (!string.IsNullOrWhiteSpace(translated) &&
                    !string.Equals(translated, propertyId, StringComparison.Ordinal))
                {
                    return translated;
                }
            }

            return propertyId;
        }

        /// <summary>
        /// Lists the object's dynamic-mode properties (its extra properties), creating them first
        /// when the object has not been through dynamic mode yet.
        /// </summary>
        public static IReadOnlyList<PropertyEntry> DynamicFor(ISimulationObject obj, IUnitsOfMeasure su)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            EnsureDynamicProperties(obj);

            var descriptions = (IDictionary<string, object>)obj.ExtraPropertiesDescriptions;
            var values = (IDictionary<string, object>)obj.ExtraProperties;

            var list = new List<PropertyEntry>();
            foreach (var kv in descriptions)
            {
                var units = "";
                if (su != null)
                {
                    units = Try(() => su.GetCurrentUnits(obj.GetDynamicPropertyUnitType(kv.Key)), "");
                }

                object value;
                values.TryGetValue(kv.Key, out value);

                list.Add(new PropertyEntry(kv.Key, Convert.ToString(kv.Value), units, value, true, true));
            }

            return list;
        }

        /// <summary>
        /// Lists the properties that make sense as monitored variables: the numeric ones. This is
        /// the list to show when a caller asks to monitor a property that does not exist.
        /// </summary>
        public static IReadOnlyList<PropertyEntry> Monitorable(ISimulationObject obj, IUnitsOfMeasure su)
        {
            return For(obj, su, PropertyType.ALL).Where(p => IsNumeric(p.Value)).ToList();
        }

        /// <summary>
        /// Creates the object's dynamic properties when it has none yet. Objects only get them on
        /// first use, so anything reading them outside a dynamic run has to ask first.
        /// </summary>
        public static void EnsureDynamicProperties(ISimulationObject obj)
        {
            var values = (IDictionary<string, object>)obj.ExtraProperties;
            if (values.Count > 0) return;
            try { obj.CreateDynamicProperties(); }
            catch { /* an object without dynamic properties simply stays empty */ }
        }

        private static bool IsNumeric(object value)
        {
            if (value == null) return false;
            return value is double || value is float || value is int || value is long || value is decimal;
        }

        private static string[] SafeGetProperties(ISimulationObject obj, PropertyType type)
        {
            try { return obj.GetProperties(type) ?? new string[0]; }
            catch { return new string[0]; }
        }

        private static T Try<T>(Func<T> get, T fallback)
        {
            try
            {
                var v = get();
                return v == null ? fallback : v;
            }
            catch { return fallback; }
        }
    }
}
