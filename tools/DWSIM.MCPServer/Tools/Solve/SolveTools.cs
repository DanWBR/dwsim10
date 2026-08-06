using System;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.MCPServer.Sessions;

namespace DWSIM.MCPServer.Tools.Solve
{
    public class SolveTools
    {
        private readonly SessionManager _sessions;

        public SolveTools(SessionManager sessions) { _sessions = sessions; }

        [McpTool("dwsim_solve_run", "Solve/calculate the flowsheet. Returns success status and any errors per object.")]
        public JObject Run(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Solver timeout in seconds", Required = false, JsonType = "integer")] int timeout_s = 300)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);

            // Use FlowsheetSolver2 (parallel-safe) if McpFlowsheet is available,
            // otherwise fall back to FluentAPI's TrySolve.
            var mcpFs = _sessions.GetMcpFlowsheet(flowsheet_id);
            System.Collections.Generic.IReadOnlyList<Exception> errors;

            if (mcpFs != null)
            {
                Console.Error.WriteLine($"[dwsim-mcp] Solving flowsheet {flowsheet_id} with FlowsheetSolver2 (timeout={timeout_s}s)...");
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout_s)))
                {
                    errors = mcpFs.SolveFlowsheet(cts.Token, timeout_s);
                }
            }
            else
            {
                Console.Error.WriteLine($"[dwsim-mcp] Solving flowsheet {flowsheet_id} with FluentAPI TrySolve...");
                errors = fs.TrySolve();
            }

            Console.Error.WriteLine($"[dwsim-mcp] Solve complete. Errors: {errors.Count}");

            var objectStatuses = new JArray();
            foreach (var obj in fs.Inner.SimulationObjects.Values)
            {
                var go = obj.GraphicObject;
                if (go == null) continue;
                objectStatuses.Add(new JObject
                {
                    ["name"] = go.Tag,
                    ["type"] = go.ObjectType.ToString(),
                    ["calculated"] = obj.Calculated,
                    ["error"] = obj.ErrorMessage ?? ""
                });
            }

            var errorMessages = new JArray();
            foreach (var ex in errors)
                errorMessages.Add(ex.Message);

            return new JObject
            {
                ["ok"] = errors.Count == 0,
                ["error_count"] = errors.Count,
                ["errors"] = errorMessages,
                ["objects"] = objectStatuses
            };
        }

        [McpTool("dwsim_solve_diagnostics", "Get diagnostic information about unconverged/unsolved objects in the flowsheet.")]
        public JObject Diagnostics(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var inner = fs.Inner;

            var unsolved = new JArray();
            var warnings = new JArray();

            foreach (var obj in inner.SimulationObjects.Values)
            {
                var go = obj.GraphicObject;
                if (go == null) continue;

                if (!obj.Calculated)
                {
                    unsolved.Add(new JObject
                    {
                        ["name"] = go.Tag,
                        ["type"] = go.ObjectType.ToString(),
                        ["error"] = obj.ErrorMessage ?? "Not calculated"
                    });
                }
                else if (!string.IsNullOrEmpty(obj.ErrorMessage))
                {
                    warnings.Add(new JObject
                    {
                        ["name"] = go.Tag,
                        ["type"] = go.ObjectType.ToString(),
                        ["warning"] = obj.ErrorMessage
                    });
                }
            }

            return new JObject
            {
                ["unsolved"] = unsolved,
                ["warnings"] = warnings,
                ["unsolved_count"] = unsolved.Count,
                ["warning_count"] = warnings.Count
            };
        }
    }
}
