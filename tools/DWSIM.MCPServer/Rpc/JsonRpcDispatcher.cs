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
                        ["capabilities"] = Capabilities
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
