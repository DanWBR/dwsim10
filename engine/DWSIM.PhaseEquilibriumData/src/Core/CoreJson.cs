using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace DWSIM.PhaseEquilibriumData.Core
{
    public static class CoreJson
    {
        public static JsonSerializerSettings Options { get; } = CreateSettings();

        private static JsonSerializerSettings CreateSettings()
        {
            var s = new JsonSerializerSettings
            {
                ContractResolver = new SortedCamelCaseResolver(),
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Include,
                Culture = System.Globalization.CultureInfo.InvariantCulture,
                FloatParseHandling = FloatParseHandling.Double,
                DateParseHandling = DateParseHandling.None
            };
            s.Converters.Add(new StringEnumConverter(new CamelCaseNamingStrategy()));
            s.Converters.Add(new SortedStringDictionaryConverter());
            return s;
        }

        public static string SerializeDeterministic<T>(T value)
            => JsonConvert.SerializeObject(value, Options);

        /// <summary>
        /// Alphabetical-ordinal property ordering + camelCase names.
        /// Guarantees byte-identical output for equal inputs (AC-7 determinism).
        /// </summary>
        private sealed class SortedCamelCaseResolver : DefaultContractResolver
        {
            public SortedCamelCaseResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = false,
                    OverrideSpecifiedNames = true
                };
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization)
                    .OrderBy(p => p.PropertyName, StringComparer.Ordinal)
                    .ToList();
            }
        }

        /// <summary>
        /// Serializes any IDictionary&lt;string, T&gt; / IReadOnlyDictionary&lt;string, T&gt; with keys
        /// sorted ordinally (preserving original casing). Required for determinism on DataPoint.
        /// </summary>
        private sealed class SortedStringDictionaryConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                if (!objectType.IsGenericType) return false;
                var def = objectType.GetGenericTypeDefinition();
                if (def != typeof(Dictionary<,>)
                    && def != typeof(IReadOnlyDictionary<,>)
                    && def != typeof(IDictionary<,>))
                    return false;
                return objectType.GetGenericArguments()[0] == typeof(string);
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                if (value != null)
                {
                    var entries = new List<(string Key, object? Val)>();
                    foreach (var kv in (System.Collections.IEnumerable)value)
                    {
                        var keyProp = kv!.GetType().GetProperty("Key");
                        var valProp = kv.GetType().GetProperty("Value");
                        var k = (string?)keyProp?.GetValue(kv) ?? string.Empty;
                        var v = valProp?.GetValue(kv);
                        entries.Add((k, v));
                    }
                    foreach (var (k, v) in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(k);
                        serializer.Serialize(writer, v);
                    }
                }
                writer.WriteEndObject();
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;
                var valueType = objectType.GetGenericArguments()[1];
                var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
                var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType)!;
                var obj = JObject.Load(reader);
                foreach (var prop in obj.Properties())
                {
                    var v = prop.Value.ToObject(valueType, serializer);
                    dict[prop.Name] = v;
                }
                return dict;
            }
        }
    }
}
