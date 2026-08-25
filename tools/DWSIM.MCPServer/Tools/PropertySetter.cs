using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
            // The calculation mode decides which of a unit's specifications it actually reads, so
            // it is the setting most worth getting right — and the one whose name a caller is least
            // likely to know. Every unit that has one exposes SetCalculationMode with the names to
            // go with it, which beats reaching for the CalcMode property by reflection.
            if (IsCalculationMode(name)) return TrySetCalculationMode(obj, value);

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

        private static bool IsCalculationMode(string name)
        {
            return string.Equals(name, "CalcMode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "CalculationMode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "calculation_mode", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sets the calculation mode from a name or an id.</summary>
        private static bool TrySetCalculationMode(ISimulationObject obj, JToken value)
        {
            var modes = CalculationModes(obj);
            if (modes.Count == 0) return false;

            int id;
            if (value.Type == JTokenType.Integer)
            {
                id = value.Value<int>();
                if (!modes.Values.Contains(id))
                {
                    throw new ArgumentException(id + " is not a calculation mode of this unit. " +
                        DescribeModes(modes));
                }
            }
            else
            {
                var wanted = value.ToString();
                if (!modes.TryGetValue(wanted, out id))
                {
                    throw new ArgumentException("'" + wanted + "' is not a calculation mode of this " +
                        "unit. " + DescribeModes(modes));
                }
            }

            Invoke(obj, "SetCalculationMode", id);
            return true;
        }

        /// <summary>
        /// The unit's calculation modes, by name. <c>GetCalculationModes</c> reports them as
        /// "Name: OutletTemperature  ID: 1", which is meant for a person to read.
        /// </summary>
        public static IDictionary<string, int> CalculationModes(ISimulationObject obj)
        {
            var modes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var described = Invoke(obj, "GetCalculationModes") as string[];
            if (described == null) return modes;

            foreach (var line in described)
            {
                var match = Regex.Match(line ?? "", @"Name:\s*(?<name>\S+)\s+ID:\s*(?<id>-?\d+)");
                if (match.Success)
                    modes[match.Groups["name"].Value] = int.Parse(match.Groups["id"].Value);
            }

            return modes;
        }

        private static string DescribeModes(IDictionary<string, int> modes)
        {
            return "Use one of: " + string.Join(", ", modes.Keys.Select(k => "'" + k + "'")) + ".";
        }

        /// <summary>
        /// Calls a method the base unit-operation class declares. Reflection because the MCP
        /// server sees objects as ISimulationObject, which does not carry these.
        /// </summary>
        private static object Invoke(ISimulationObject obj, string method, params object[] args)
        {
            var info = obj.GetType().GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance);

            return info == null ? null : info.Invoke(obj, args);
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
