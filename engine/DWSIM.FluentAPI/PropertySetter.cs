using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DWSIM.Interfaces;

namespace DWSIM.Automation.FluentAPI
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
        public static IReadOnlyList<string> Apply(ISimulationObject obj,
            IEnumerable<KeyValuePair<string, object>> properties, IUnitsOfMeasure units)
        {
            var applied = new List<string>();
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
        public static bool TrySet(ISimulationObject obj, string name, object value, IUnitsOfMeasure units)
        {
            // The calculation mode decides which of a unit's specifications it actually reads, so
            // it is the setting most worth getting right — and the one whose name a caller is least
            // likely to know. Every unit that has one exposes SetCalculationMode with the names to
            // go with it, which beats reaching for the CalcMode property by reflection.
            if (IsCalculationMode(name)) return TrySetCalculationMode(obj, value);

            if (obj.IsDynamicProperty(name))
            {
                object dynamicValue = AsBoolean(value) ?? (object)AsDouble(value);

                obj.AddDynamicProperty(name, dynamicValue);
                return true;
            }

            var writable = obj.GetProperties(Interfaces.Enums.PropertyType.WR) ?? new string[0];
            if (writable.Contains(name))
            {
                obj.SetPropertyValue(name, AsDouble(value), units);
                return true;
            }

            return TrySetClr(obj, name, value);
        }

        /// <summary>
        /// Sets a plain .NET property, including an enum given by name — a compressor's process
        /// path or a valve's calculation mode are set no other way.
        /// </summary>
        private static bool TrySetClr(ISimulationObject obj, string name, object value)
        {
            var property = obj.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null || !property.CanWrite || property.GetIndexParameters().Length > 0)
                return false;

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            object converted;
            if (type.IsEnum)
            {
                var text = Convert.ToString(value);
                try { converted = Enum.Parse(type, text, true); }
                catch (Exception)
                {
                    throw new ArgumentException("'" + text + "' is not a valid " + type.Name +
                        ". Use one of: " + string.Join(", ", Enum.GetNames(type)) + ".");
                }
            }
            else if (type == typeof(bool)) converted = AsBoolean(value) ?? false;
            else if (type == typeof(int)) converted = (int)AsDouble(value);
            else if (type == typeof(double)) converted = AsDouble(value);
            else if (type == typeof(string)) converted = Convert.ToString(value);
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
        private static bool TrySetCalculationMode(ISimulationObject obj, object value)
        {
            var modes = CalculationModes(obj);
            if (modes.Count == 0) return false;

            int id;
            if (IsWholeNumber(value))
            {
                id = (int)AsDouble(value);
                if (!modes.Values.Contains(id))
                {
                    throw new ArgumentException(id + " is not a calculation mode of this unit. " +
                        DescribeModes(modes));
                }
            }
            else
            {
                var wanted = Convert.ToString(value);
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

        /// <summary>The value as a number, whatever a transport wrapped it in.</summary>
        private static double AsDouble(object value)
        {
            if (value == null) return 0.0;
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        /// <summary>The value as a boolean, or null when it is not one.</summary>
        private static bool? AsBoolean(object value)
        {
            if (value is bool b) return b;

            var text = Convert.ToString(value);
            bool parsed;
            if (bool.TryParse(text, out parsed)) return parsed;
            return null;
        }

        /// <summary>Whether the value is a whole number, and so could be a mode id.</summary>
        private static bool IsWholeNumber(object value)
        {
            if (value is int || value is long || value is short) return true;

            double number;
            if (!double.TryParse(Convert.ToString(value), NumberStyles.Any,
                                 CultureInfo.InvariantCulture, out number)) return false;

            return Math.Abs(number - Math.Round(number)) < 1e-9;
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
