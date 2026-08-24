using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using DWSIM.Automation.FluentAPI.Diagnostics;

namespace DWSIM.MCPServer.Tools
{
    /// <summary>
    /// Renders diagnostic findings for a tool response.
    /// </summary>
    /// <remarks>
    /// Every tool that reports findings renders them the same way, so a caller learns the shape
    /// once: a code to branch on, a severity to rank by, the object to look at, what is wrong and
    /// what to do about it.
    /// </remarks>
    public static class FindingsJson
    {
        /// <summary>Findings past this many are counted rather than listed.</summary>
        public const int MaxItems = 25;

        /// <summary>Renders findings, worst first, capped at <see cref="MaxItems"/>.</summary>
        public static JArray From(IEnumerable<Finding> findings)
        {
            var array = new JArray();
            foreach (var finding in findings.Take(MaxItems))
            {
                array.Add(new JObject
                {
                    ["code"] = finding.Code,
                    ["severity"] = finding.Severity.ToString().ToLowerInvariant(),
                    ["object"] = finding.ObjectTag,
                    ["message"] = finding.Message,
                    ["fix"] = finding.Fix
                });
            }
            return array;
        }

        /// <summary>
        /// A full report: the findings, how many there are of each severity, and whether anything
        /// blocks progress.
        /// </summary>
        public static JObject Report(IReadOnlyList<Finding> findings)
        {
            var blockers = findings.Count(f => f.Severity == DiagnosticSeverity.Blocker);
            var warnings = findings.Count(f => f.Severity == DiagnosticSeverity.Warning);

            var report = new JObject
            {
                ["ready"] = blockers == 0,
                ["blockers"] = blockers,
                ["warnings"] = warnings,
                ["findings"] = From(findings)
            };

            if (findings.Count > MaxItems)
            {
                report["truncated"] = true;
                report["total"] = findings.Count;
            }

            return report;
        }
    }
}
