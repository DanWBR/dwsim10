using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.MCPServer.Sessions;

namespace DWSIM.MCPServer.Tools.Solve
{
    public class SolveTools
    {
        private readonly SessionManager _sessions;

        public SolveTools(SessionManager sessions) { _sessions = sessions; }

        [McpTool("dwsim_flowsheet_check",
            "Check the flowsheet for the faults that stop it solving - dangling streams, unconnected " +
            "unit operations, feeds with no flow, a loop with no recycle - without solving it. " +
            "Cheap, so call it before dwsim_solve_run. Each finding carries the fix for it.")]
        public JObject Check(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var findings = FlowsheetDiagnostics.Check(fs.Inner);

            var report = FindingsJson.Report(findings);
            report["object_count"] = fs.Inner.SimulationObjects.Count;
            report["compound_count"] = fs.Inner.SelectedCompounds.Count;
            return report;
        }

        [McpTool("dwsim_solve_run",
            "Solve the flowsheet. On failure the response carries diagnostic findings naming the " +
            "object at fault and what to do about it; call dwsim_flowsheet_check first to catch " +
            "the same faults without paying for a solve.")]
        public JObject Run(
            [McpParam("Flowsheet handle")] string flowsheet_id,
            [McpParam("Solver timeout in seconds", Required = false, JsonType = "integer")] int timeout_s = 300)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);

            // Use FlowsheetSolver2 (parallel-safe) if McpFlowsheet is available,
            // otherwise fall back to FluentAPI's TrySolve.
            var mcpFs = _sessions.GetMcpFlowsheet(flowsheet_id);
            IReadOnlyList<Exception> errors;

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

            var result = new JObject
            {
                ["ok"] = errors.Count == 0,
                ["error_count"] = errors.Count,
                ["errors"] = errorMessages,
                ["objects"] = objectStatuses
            };

            // A raw exception message tells a caller what threw, not what to do. Diagnosing on the
            // way out costs nothing next to the solve and turns the failure into a next step.
            var findings = FlowsheetDiagnostics.Diagnose(fs.Inner, errors);
            if (findings.Count > 0)
            {
                result["findings"] = FindingsJson.From(findings);
                result["blockers"] = findings.Count(f => f.Severity == DiagnosticSeverity.Blocker);
            }

            return result;
        }

        [McpTool("dwsim_solve_diagnostics",
            "Explain a flowsheet that did not solve: which object failed and why, plus the setup " +
            "faults behind it. Call after dwsim_solve_run reports errors.")]
        public JObject Diagnostics(
            [McpParam("Flowsheet handle")] string flowsheet_id)
        {
            var fs = _sessions.GetFlowsheet(flowsheet_id);
            var inner = fs.Inner;

            // The solver's own exceptions are gone by now; what survives is each object's state,
            // which is what Diagnose reads when it is given no exceptions.
            var findings = FlowsheetDiagnostics.Diagnose(inner, null);

            var report = FindingsJson.Report(findings);

            var unsolved = new JArray();
            foreach (var obj in inner.SimulationObjects.Values)
            {
                var go = obj.GraphicObject;
                if (go == null || obj.Calculated) continue;
                unsolved.Add(new JObject
                {
                    ["name"] = go.Tag,
                    ["type"] = go.ObjectType.ToString(),
                    ["error"] = string.IsNullOrEmpty(obj.ErrorMessage) ? "Not calculated" : obj.ErrorMessage
                });
            }

            report["unsolved"] = unsolved;
            report["unsolved_count"] = unsolved.Count;
            return report;
        }
    }
}
