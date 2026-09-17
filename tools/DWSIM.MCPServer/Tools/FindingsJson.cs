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
                    ["fix"] = finding.Fix,
                    ["learn_more"] = FindingExplanations.LearnMoreUrl(finding.Code)
                });
            }
            return array;
        }

        /// <summary>The written explanation of a code: meaning, why it happens, how to fix it, where to read.</summary>
        public static JObject Explanation(string code)
        {
            var explanation = FindingExplanations.For(code);
            return new JObject
            {
                ["code"] = explanation.Code,
                ["title"] = explanation.Title,
                ["meaning"] = explanation.Meaning,
                ["why"] = explanation.Why,
                ["how_to_fix"] = explanation.HowToFix,
                ["learn_more"] = FindingExplanations.LearnMoreUrl(explanation.Code)
            };
        }

        /// <summary>
        /// The explanations of every distinct code among the findings, so a caller that meets a
        /// code for the first time can read it up without a second call.
        /// </summary>
        public static JArray Explanations(IEnumerable<Finding> findings)
        {
            var array = new JArray();
            foreach (var code in findings.Select(f => f.Code).Distinct())
                array.Add(Explanation(code));
            return array;
        }

        /// <summary>The degrees of freedom of one object: its specification slots and what is missing.</summary>
        public static JObject DegreesOfFreedom(ObjectDegreesOfFreedom dof)
        {
            var slots = new JArray();
            foreach (var slot in dof.Slots)
            {
                var item = new JObject
                {
                    ["name"] = slot.Name,
                    ["property"] = slot.Property,
                    ["required"] = slot.Required,
                    ["set"] = slot.IsSet
                };
                if (slot.Value.HasValue) item["value"] = slot.Value.Value;
                if (!string.IsNullOrEmpty(slot.Units)) item["units"] = slot.Units;
                if (!string.IsNullOrEmpty(slot.Note)) item["note"] = slot.Note;
                slots.Add(item);
            }

            var result = new JObject
            {
                ["object"] = dof.ObjectTag,
                ["type"] = dof.ObjectType,
                ["mode"] = dof.Mode,
                ["supported"] = dof.Supported,
                ["required"] = dof.Required,
                ["specified"] = dof.Specified,
                ["remaining"] = dof.Remaining,
                ["missing"] = new JArray(dof.Missing.Select(s => s.Name)),
                ["slots"] = slots
            };
            if (!string.IsNullOrEmpty(dof.Note)) result["note"] = dof.Note;
            return result;
        }

        /// <summary>The degrees of freedom of the whole flowsheet, objects with holes first.</summary>
        public static JObject DegreesOfFreedom(FlowsheetDegreesOfFreedom dof)
        {
            return new JObject
            {
                ["fully_specified"] = dof.IsFullySpecified,
                ["remaining"] = dof.Remaining,
                ["unsupported"] = dof.Unsupported,
                ["objects"] = new JArray(dof.Objects.Select(DegreesOfFreedom))
            };
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
                ["findings"] = From(findings),
                ["explanations"] = Explanations(findings.Take(MaxItems))
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
