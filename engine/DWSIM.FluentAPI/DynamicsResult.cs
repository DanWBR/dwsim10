using System;
using System.Collections.Generic;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Result of a dynamic integration run, containing per-variable time-series data.
    /// </summary>
    public sealed class DynamicsResult
    {
        /// <summary>
        /// Time-series data for each monitored variable.
        /// Keys are the variable descriptions as configured in DWSIM.
        /// Values are ordered (simulation time in seconds from t=0, value in display units) pairs.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<(double TimeSeconds, double Value)>> MonitoredVariables { get; }

        /// <summary>True if integration ran to completion without error.</summary>
        public bool Completed { get; }

        /// <summary>The exception that caused integration to stop, or null when <see cref="Completed"/> is true.</summary>
        public Exception Error { get; }

        internal DynamicsResult(
            Dictionary<string, List<(double, double)>> data,
            bool completed,
            Exception error)
        {
            var ro = new Dictionary<string, IReadOnlyList<(double TimeSeconds, double Value)>>(data.Count);
            foreach (var kv in data)
                ro[kv.Key] = kv.Value.AsReadOnly();
            MonitoredVariables = ro;
            Completed = completed;
            Error = error;
        }
    }
}
