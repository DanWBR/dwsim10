using System;
using DWSIM.Interfaces.Enums;

namespace DWSIM.Automation.DynamicRunner.Setup
{
    /// <summary>How much a readiness issue matters.</summary>
    public enum DynamicsIssueSeverity
    {
        /// <summary>Worth knowing, but the run will proceed.</summary>
        Info,
        /// <summary>The run will proceed but the result is likely to disappoint.</summary>
        Warning,
        /// <summary>The run cannot produce a meaningful result until this is fixed.</summary>
        Blocker
    }

    /// <summary>
    /// One thing standing between a flowsheet and a dynamic run — and, when the fix is mechanical,
    /// the value that would settle it and the means to apply it.
    /// </summary>
    /// <remarks>
    /// The diagnostics engine only ever needed to describe a problem. A wizard has to offer to fix
    /// it, so this carries a suggested value the user can edit and an <see cref="Apply"/> that
    /// writes it. Issues with <see cref="CanAutoFix"/> false are advisory: the user has to open the
    /// editor and decide.
    /// </remarks>
    public sealed class DynamicsIssue
    {
        /// <summary>Stable identifier, e.g. <c>VALVE_NO_KV</c>.</summary>
        public string Code { get; set; }

        /// <summary>How much this matters.</summary>
        public DynamicsIssueSeverity Severity { get; set; }

        /// <summary>Name of the object concerned — the stable key, empty for flowsheet-wide issues.</summary>
        public string ObjectId { get; set; }

        /// <summary>Tag of the object concerned — what the user sees on the canvas.</summary>
        public string ObjectTag { get; set; }

        /// <summary>What is wrong, in one sentence.</summary>
        public string Message { get; set; }

        /// <summary>What to do about it, in one sentence.</summary>
        public string Fix { get; set; }

        /// <summary>Which wizard page this belongs on.</summary>
        public DynamicsIssueCategory Category { get; set; }

        /// <summary>True when <see cref="Apply"/> can settle this without the user opening an editor.</summary>
        public bool CanAutoFix { get; set; }

        /// <summary>Label for the editable value, e.g. "Kv" or "Volume". Empty when there is nothing to edit.</summary>
        public string ValueLabel { get; set; }

        /// <summary>The value the wizard proposes: a double, a bool, or an enum member.</summary>
        public object SuggestedValue { get; set; }

        /// <summary>Unit type of <see cref="SuggestedValue"/>, so the UI can show it in the user's units.</summary>
        public UnitOfMeasure UnitType { get; set; }

        /// <summary>
        /// Applies the confirmed value. Null when the issue is advisory. Implementations are
        /// idempotent: running the wizard twice over the same flowsheet changes nothing the second
        /// time.
        /// </summary>
        public Action<object> Apply { get; set; }

        internal DynamicsIssue() { UnitType = UnitOfMeasure.none; }

        internal DynamicsIssue(string code, DynamicsIssueSeverity severity, string message, string fix)
        {
            Code = code;
            Severity = severity;
            Message = message;
            Fix = fix ?? "";
            ObjectId = "";
            ObjectTag = "";
            UnitType = UnitOfMeasure.none;
            Category = DynamicsIssueCategory.Overview;
        }

        /// <summary>Returns <c>"[SEVERITY] CODE (tag): message Fix: ..."</c>.</summary>
        public override string ToString()
        {
            var where = string.IsNullOrEmpty(ObjectTag) ? "" : " (" + ObjectTag + ")";
            var fix = string.IsNullOrEmpty(Fix) ? "" : " Fix: " + Fix;
            return "[" + Severity.ToString().ToUpperInvariant() + "] " + Code + where + ": " + Message + fix;
        }
    }

    /// <summary>Which step of the conversion an issue belongs to.</summary>
    public enum DynamicsIssueCategory
    {
        /// <summary>Flowsheet-wide: compounds, property package, steady state, unsupported objects.</summary>
        Overview,
        /// <summary>Vessels, tanks and reactors: volume, height, level.</summary>
        Holdup,
        /// <summary>Valves, pumps, compressors and pipes.</summary>
        Hydraulics,
        /// <summary>Material stream pressure/flow specifications.</summary>
        BoundarySpecs,
        /// <summary>Controllers.</summary>
        Control,
        /// <summary>Integrator and schedule.</summary>
        Integrator
    }
}
