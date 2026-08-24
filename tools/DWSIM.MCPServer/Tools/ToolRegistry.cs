using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace DWSIM.MCPServer.Tools
{
    public class ToolRegistration
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject InputSchema { get; set; }
        public MethodInfo Method { get; set; }
        public object Instance { get; set; }
        public ParameterInfo[] Parameters { get; set; }
    }

    public class ToolRegistry
    {
        private readonly Dictionary<string, ToolRegistration> _tools = new Dictionary<string, ToolRegistration>();

        public void RegisterToolsFrom(object instance)
        {
            var type = instance.GetType();
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = method.GetCustomAttribute<McpToolAttribute>();
                if (attr == null) continue;

                var parameters = method.GetParameters();
                var schema = BuildInputSchema(parameters);

                // GetMethods does not promise an order, so two methods sharing a tool name would
                // leave which one wins up to chance, and the loser silently unreachable.
                if (_tools.TryGetValue(attr.Name, out var clash))
                {
                    throw new InvalidOperationException(
                        $"Two tools are registered as '{attr.Name}': {clash.Instance.GetType().Name}.{clash.Method.Name} " +
                        $"and {type.Name}.{method.Name}. Tool names must be unique.");
                }

                _tools[attr.Name] = new ToolRegistration
                {
                    Name = attr.Name,
                    Description = attr.Description,
                    InputSchema = schema,
                    Method = method,
                    Instance = instance,
                    Parameters = parameters
                };
            }
        }

        public JArray ListTools()
        {
            var arr = new JArray();
            foreach (var tool in _tools.Values)
            {
                arr.Add(new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["inputSchema"] = tool.InputSchema
                });
            }
            return arr;
        }

        public ToolRegistration GetTool(string name)
        {
            _tools.TryGetValue(name, out var reg);
            return reg;
        }

        public JObject Invoke(string name, JObject arguments)
        {
            var tool = GetTool(name);
            if (tool == null)
                throw new InvalidOperationException($"Unknown tool: {name}");

            var args = new object[tool.Parameters.Length];
            for (int i = 0; i < tool.Parameters.Length; i++)
            {
                var p = tool.Parameters[i];
                JToken val;
                if (arguments != null && arguments.TryGetValue(p.Name, out val) && val.Type != JTokenType.Null)
                {
                    args[i] = val.ToObject(p.ParameterType);
                }
                else if (p.HasDefaultValue)
                {
                    args[i] = p.DefaultValue;
                }
                else
                {
                    var paramAttr = p.GetCustomAttribute<McpParamAttribute>();
                    if (paramAttr != null && !paramAttr.Required)
                        args[i] = GetDefault(p.ParameterType);
                    else
                        throw new ArgumentException($"Missing required parameter: {p.Name}");
                }
            }

            var result = tool.Method.Invoke(tool.Instance, args);
            if (result is JObject jo) return jo;
            return JObject.FromObject(result);
        }

        private static JObject BuildInputSchema(ParameterInfo[] parameters)
        {
            var props = new JObject();
            var required = new JArray();

            foreach (var p in parameters)
            {
                var paramAttr = p.GetCustomAttribute<McpParamAttribute>();
                var prop = new JObject { ["type"] = MapClrTypeToJson(p.ParameterType, paramAttr) };

                if (paramAttr != null)
                    prop["description"] = paramAttr.Description;

                if (p.ParameterType == typeof(string[]) || p.ParameterType == typeof(List<string>))
                {
                    prop["type"] = "array";
                    prop["items"] = new JObject { ["type"] = "string" };
                }

                props[p.Name] = prop;

                bool isRequired = paramAttr != null ? paramAttr.Required : !p.HasDefaultValue;
                if (isRequired)
                    required.Add(p.Name);
            }

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = props,
                ["required"] = required
            };
        }

        private static string MapClrTypeToJson(Type t, McpParamAttribute attr)
        {
            if (attr?.JsonType != null) return attr.JsonType;
            if (t == typeof(string)) return "string";
            if (t == typeof(int) || t == typeof(long)) return "integer";
            if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return "number";
            if (t == typeof(bool)) return "boolean";
            if (t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))) return "array";
            if (t == typeof(JObject) || t == typeof(Dictionary<string, double>) || t == typeof(Dictionary<string, object>)) return "object";
            return "string";
        }

        private static object GetDefault(Type t)
        {
            return t.IsValueType ? Activator.CreateInstance(t) : null;
        }
    }
}
