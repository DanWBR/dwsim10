using System.Collections.Generic;

namespace DWSIM.Automation.FluentAPI.Diagnostics
{
    /// <summary>How much a finding matters.</summary>
    public enum DiagnosticSeverity
    {
        /// <summary>Worth knowing, but the run will proceed.</summary>
        Info,
        /// <summary>The run will proceed but the result is likely to disappoint.</summary>
        Warning,
        /// <summary>The run cannot produce a meaningful result until this is fixed.</summary>
        Blocker
    }

    /// <summary>One thing wrong, or suspicious, about a simulation.</summary>
    public sealed class Finding
    {
        internal Finding(string code, DiagnosticSeverity severity, string objectTag, string message, string fix)
        {
            Code = code;
            Severity = severity;
            ObjectTag = objectTag ?? "";
            Message = message;
            Fix = fix ?? "";
        }

        /// <summary>Stable identifier, e.g. <c>VALVE_NO_KV</c>. See <see cref="DiagnosticCodes"/>.</summary>
        public string Code { get; }

        /// <summary>How much this matters.</summary>
        public DiagnosticSeverity Severity { get; }

        /// <summary>Tag of the object the finding is about, empty when it concerns the flowsheet as a whole.</summary>
        public string ObjectTag { get; }

        /// <summary>What is wrong, in one sentence.</summary>
        public string Message { get; }

        /// <summary>What to do about it, in one sentence.</summary>
        public string Fix { get; }

        /// <summary>Returns <c>"[SEVERITY] CODE (tag): message Fix: ..."</c>.</summary>
        public override string ToString()
        {
            var where = string.IsNullOrEmpty(ObjectTag) ? "" : " (" + ObjectTag + ")";
            var fix = string.IsNullOrEmpty(Fix) ? "" : " Fix: " + Fix;
            return "[" + Severity.ToString().ToUpperInvariant() + "] " + Code + where + ": " + Message + fix;
        }
    }

    /// <summary>
    /// The diagnostic codes this engine emits. Kept here so the tool catalogue, the documentation
    /// and the findings themselves cannot drift apart.
    /// </summary>
    public static class DiagnosticCodes
    {
        /// <summary>Every code, mapped to a one-line explanation.</summary>
        public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
        {
            // Readiness
            { "NO_SCHEDULE", "The flowsheet has no dynamics schedule." },
            { "NO_INTEGRATOR", "The schedule has no integrator assigned." },
            { "NO_DYNAMIC_MODE", "Dynamic mode is off, so unit operations solve at steady state." },
            { "NO_MONITORED_VARS", "The integrator records no variables, so the run produces no series." },
            { "NOT_SOLVED_STEADY_STATE", "Some objects have never been solved; dynamics starts from an undefined state." },
            { "NO_PROPERTY_PACKAGE", "The flowsheet has no property package, so nothing can be flashed." },
            { "NO_COMPOUNDS", "The flowsheet has no compounds." },
            { "MISSING_INITIAL_STATE", "The schedule starts from a stored state that does not exist." },
            { "TOO_MANY_STEPS", "Duration divided by step gives an impractical number of steps." },
            { "NO_PRESSURE_SPEC", "No stream is specified by pressure, leaving the pressure-flow network underdetermined." },
            { "ALL_FLOW_SPECS", "Every stream is specified by flow, so pressure has nothing to resolve against." },
            { "VALVE_NO_KV", "A valve has no flow coefficient, so it cannot pass a computed flow." },
            { "VALVE_PRESSURE_DROP_MODE", "A valve is in a pressure-drop mode, so it cannot compute its own flow." },
            { "VALVE_OPENING_IGNORED", "A valve passes its full Kv at any opening, so closing it does nothing." },
            { "VESSEL_NO_VOLUME", "A vessel or tank has no volume, so it holds nothing up and adds no lag." },
            { "PID_UNBOUND", "A controller is missing its process or manipulated variable." },
            { "PID_LIMITS_INVALID", "A controller's output minimum is not below its maximum." },
            { "PID_INACTIVE", "A controller is switched off or in manual, so the loop is open." },
            { "UNSUPPORTED_OBJECT", "An object has no dynamic model and is solved at steady state every step." },

            // Post-run
            { "SOLVER_EXCEPTION", "The solver raised an exception and the run stopped early." },
            { "NAN_IN_SERIES", "A recorded series contains NaN or infinity." },
            { "DIVERGENT", "A recorded series grew without bound." },
            { "SUSTAINED_OSCILLATION", "A series oscillates without decaying." },
            { "MV_SATURATED", "A controller sat at its output limit for most of the run." },
            { "STEP_TOO_LARGE_TRANSIENT", "A series jumps by more than half its range between adjacent steps." },
            { "SLOW_STEP", "Each step took more than a second of wall time." },
            { "PID_ACTION_INVERTED", "A controller consistently moved its output in the direction that increases the error." },
            { "RUN_ABORTED", "The run stopped before reaching the configured duration." },
            { "NOT_SETTLED", "A series had not settled by the end of the run." }
        };
    }

}
