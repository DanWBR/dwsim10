using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces;

namespace DWSIM.MCPServer.Tools
{
    /// <summary>
    /// Sets a property on a simulation object by whichever name the caller knows it by.
    /// </summary>
    /// <remarks>
    /// A DWSIM model carries its settings in three places that do not overlap: the property
    /// system (<c>PROP_CO_1</c> and friends), the dynamic-property bag (<c>"Liquid Level"</c>),
    /// and plain .NET properties the other two never mention — a tank's <c>Volume</c>, a valve's
    /// <c>Kv</c>, a compressor's <c>POut</c>.
    ///
    /// A caller has no way to know which of the three holds the setting it wants, so this tries
    /// all of them and reports what is actually available when none matches.
    /// </remarks>
    public static class PropertySetter
    {
        /// <summary>Names past this many are counted rather than listed.</summary>
        private const int MaxListed = 40;

        /// <summary>
        /// Applies each entry to the object, returning what was set.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// When a name matches nothing, carrying the names that would have worked.
        /// </exception>
        public static JArray Apply(ISimulationObject obj, JObject properties, IUnitsOfMeasure units)
        {
            var applied = new JArray();
            if (properties == null) return applied;

            foreach (var entry in properties)
            {
                if (TrySet(obj, entry.Key, entry.Value, units))
                {
                    applied.Add(entry.Key + " = " + entry.Value);
                    continue;
                }

                throw new ArgumentException(Describe(obj, entry.Key, units));
            }

            return applied;
        }

        /// <summary>Sets one property, returning false when there is no such setting.</summary>
        public static bool TrySet(ISimulationObject obj, string name, JToken value, IUnitsOfMeasure units)
        {
            if (obj.IsDynamicProperty(name))
            {
                object dynamicValue = value.Type == JTokenType.Boolean
                    ? (object)value.Value<bool>()
                    : value.Value<double>();

                obj.AddDynamicProperty(name, dynamicValue);
                return true;
            }

            var writable = obj.GetProperties(Interfaces.Enums.PropertyType.WR) ?? new string[0];
            if (writable.Contains(name))
            {
                obj.SetPropertyValue(name, value.Value<double>(), units);
                return true;
            }

            return TrySetClr(obj, name, value);
        }

        /// <summary>
        /// Sets a plain .NET property, including an enum given by name — a compressor's process
        /// path or a valve's calculation mode are set no other way.
        /// </summary>
        private static bool TrySetClr(ISimulationObject obj, string name, JToken value)
        {
            var property = obj.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null || !property.CanWrite || property.GetIndexParameters().Length > 0)
                return false;

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            object converted;
            if (type.IsEnum)
            {
                var text = value.Type == JTokenType.String ? value.Value<string>() : value.ToString();
                try { converted = Enum.Parse(type, text, true); }
                catch (Exception)
                {
                    throw new ArgumentException("'" + text + "' is not a valid " + type.Name +
                        ". Use one of: " + string.Join(", ", Enum.GetNames(type)) + ".");
                }
            }
            else if (type == typeof(bool)) converted = value.Value<bool>();
            else if (type == typeof(int)) converted = value.Value<int>();
            else if (type == typeof(double)) converted = value.Value<double>();
            else if (type == typeof(string)) converted = value.Value<string>();
            else return false;

            property.SetValue(obj, converted);
            return true;
        }

        /// <summary>The message for a name that matched nothing: what it was, and what would work.</summary>
        private static string Describe(ISimulationObject obj, string name, IUnitsOfMeasure units)
        {
            var tag = obj.GraphicObject != null && !string.IsNullOrEmpty(obj.GraphicObject.Tag)
                ? obj.GraphicObject.Tag
                : obj.Name;

            var known = PropertyCatalog.DynamicFor(obj, units).Select(p => p.Id)
                .Concat(obj.GetProperties(Interfaces.Enums.PropertyType.WR) ?? new string[0])
                .Concat(SettableClrProperties(obj))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            var listed = string.Join(", ", known.Take(MaxListed).Select(p => "'" + p + "'"));
            var more = known.Count > MaxListed ? " (and " + (known.Count - MaxListed) + " more)" : "";

            return "'" + tag + "' has no settable property '" + name + "'. Available: " + listed + more + ".";
        }

        /// <summary>The plain .NET properties a caller could set.</summary>
        public static IEnumerable<string> SettableClrProperties(ISimulationObject obj)
        {
            return obj.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .Where(p =>
                {
                    var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                    return t.IsEnum || t == typeof(double) || t == typeof(int) || t == typeof(bool);
                })
                .Select(p => p.Name);
        }
    }
}
