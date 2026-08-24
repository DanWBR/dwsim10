using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DWSIM.MCPServer.Tools;

namespace DWSIM.MCPServer.Rpc
{
    public class JsonRpcDispatcher
    {
        private readonly ToolRegistry _registry;

        private static readonly JObject ServerInfo = new JObject
        {
            ["name"] = "dwsim-mcp-server",
            ["version"] = "1.0.0"
        };

        private static readonly JObject Capabilities = new JObject
        {
            ["tools"] = new JObject { ["listChanged"] = false }
        };

        /// <summary>
        /// Sent with the handshake, so a client learns the workflows without anyone having to write
        /// them into its own prompt. This is the only channel the server itself controls.
        /// </summary>
        private const string Instructions =
            "DWSIM process simulation over MCP. Two workflows:\n\n" +
            "STEADY STATE: dwsim_flowsheet_create (or _load) -> dwsim_thermo_add_compounds -> " +
            "dwsim_thermo_set_property_package -> dwsim_stream_add_material -> dwsim_unitop_add -> " +
            "dwsim_unitop_connect -> dwsim_solve_run -> dwsim_stream_get_results. " +
            "Compounds and a property package must come before any stream, or nothing can be flashed.\n\n" +
            "DYNAMICS (time domain): dwsim_dynamics_inspect to see what the flowsheet offers -> " +
            "dwsim_dynamics_properties to find property ids, which are never guessable -> " +
            "dwsim_dynamics_setup (integrator step and duration, schedule) -> dwsim_dynamics_monitor " +
            "(nothing is recorded otherwise) -> dwsim_dynamics_event for step changes and ramps -> " +
            "dwsim_dynamics_check -> dwsim_dynamics_run -> poll dwsim_dynamics_status -> " +
            "dwsim_dynamics_series or dwsim_dynamics_analyze. When a run misbehaves, dwsim_dynamics_diagnose " +
            "names the cause and the fix; for a sluggish or oscillating control loop, dwsim_dynamics_tune_pid " +
            "searches the gains.\n\n" +
            "A dynamic run needs the flowsheet solved at steady state first, at least one monitored variable, " +
            "and a pressure-flow network with both kinds of specification: feeds by flow, boundaries by pressure.\n\n" +
            "TIME SERIES ARE LARGE. dwsim_dynamics_run and _status never return points. dwsim_dynamics_series " +
            "returns a decimated preview, about 40 points by default, capped at 400. For the complete data " +
            "call dwsim_dynamics_export, which writes a CSV file and returns only its path.\n\n" +
            "Only one integration runs in the process at a time; a second request is refused rather than queued.";

        public JsonRpcDispatcher(ToolRegistry registry)
        {
            _registry = registry;
        }

        public string HandleMessage(string line)
        {
            JsonRpcRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<JsonRpcRequest>(line);
            }
            catch (Exception ex)
            {
                return Serialize(JsonRpcResponse.Fail(null, McpErrorCodes.ParseError, "Parse error: " + ex.Message));
            }

            if (request == null || string.IsNullOrEmpty(request.Method))
                return Serialize(JsonRpcResponse.Fail(request?.Id, McpErrorCodes.InvalidRequest, "Invalid request"));

            try
            {
                var result = Dispatch(request);
                return Serialize(result);
            }
            catch (Exception ex)
            {
                return Serialize(JsonRpcResponse.Fail(request.Id, McpErrorCodes.InternalError, ex.Message));
            }
        }

        private JsonRpcResponse Dispatch(JsonRpcRequest request)
        {
            switch (request.Method)
            {
                case "initialize":
                    return JsonRpcResponse.Success(request.Id, new JObject
                    {
                        ["protocolVersion"] = "2024-11-05",
                        ["serverInfo"] = ServerInfo,
                        ["capabilities"] = Capabilities,
                        ["instructions"] = Instructions
                    });

                case "notifications/initialized":
                    return null;

                case "tools/list":
                    return JsonRpcResponse.Success(request.Id, new JObject
                    {
                        ["tools"] = _registry.ListTools()
                    });

                case "tools/call":
                    return HandleToolCall(request);

                case "ping":
                    return JsonRpcResponse.Success(request.Id, new JObject());

                default:
                    return JsonRpcResponse.Fail(request.Id, McpErrorCodes.MethodNotFound,
                        $"Method not found: {request.Method}");
            }
        }

        private JsonRpcResponse HandleToolCall(JsonRpcRequest request)
        {
            var toolName = request.Params?["name"]?.ToString();
            if (string.IsNullOrEmpty(toolName))
                return JsonRpcResponse.Fail(request.Id, McpErrorCodes.InvalidParams, "Missing tool name");

            var arguments = request.Params["arguments"] as JObject ?? new JObject();

            try
            {
                var result = _registry.Invoke(toolName, arguments);
                return JsonRpcResponse.Success(request.Id, new JObject
                {
                    ["content"] = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "text",
                            ["text"] = result.ToString(Formatting.None)
                        }
                    }
                });
            }
            catch (ArgumentException ex)
            {
                return JsonRpcResponse.Fail(request.Id, McpErrorCodes.InvalidParams, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return JsonRpcResponse.Fail(request.Id, McpErrorCodes.MethodNotFound, ex.Message);
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                return JsonRpcResponse.Success(request.Id, new JObject
                {
                    ["content"] = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "text",
                            ["text"] = JsonConvert.SerializeObject(new { error = inner.Message, type = inner.GetType().Name })
                        }
                    },
                    ["isError"] = true
                });
            }
        }

        private static string Serialize(JsonRpcResponse response)
        {
            if (response == null) return null;
            return JsonConvert.SerializeObject(response, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }
    }
}
