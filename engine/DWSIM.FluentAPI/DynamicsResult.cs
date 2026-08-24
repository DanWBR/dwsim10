using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DWSIM.Interfaces;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Result of a dynamic integration run: the time series of every monitored variable, plus what
    /// the run itself did — how many steps, how long, and what went wrong if anything did.
    /// </summary>
    public sealed class DynamicsResult
    {
        private readonly Dictionary<string, DynamicsSeries> _byName;

        internal DynamicsResult(
            IReadOnlyList<DynamicsSeries> series,
            string scheduleName, string integratorName,
            int steps, double finalTimeSeconds, TimeSpan wallClock,
            bool aborted, IReadOnlyList<Exception> errors)
        {
            Series = series;
            ScheduleName = scheduleName;
            IntegratorName = integratorName;
            Steps = steps;
            FinalTimeSeconds = finalTimeSeconds;
            WallClock = wallClock;
            Aborted = aborted;
            Errors = errors ?? new Exception[0];

            _byName = new Dictionary<string, DynamicsSeries>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in series)
            {
                // First one wins: two variables can carry the same description, and the ID
                // lookups below still reach the others.
                if (!_byName.ContainsKey(s.Name)) _byName[s.Name] = s;
                if (!_byName.ContainsKey(s.VariableId)) _byName[s.VariableId] = s;
                var qualified = s.ObjectTag + "." + s.PropertyId;
                if (!_byName.ContainsKey(qualified)) _byName[qualified] = s;
            }

            var legacy = new Dictionary<string, IReadOnlyList<(double TimeSeconds, double Value)>>();
            foreach (var s in series)
            {
                if (legacy.ContainsKey(s.Name)) continue;
                var points = new List<(double, double)>(s.Count);
                for (var i = 0; i < s.Count; i++) points.Add((s.TimeSeconds[i], s.Values[i]));
                legacy[s.Name] = points.AsReadOnly();
            }
            MonitoredVariables = legacy;
        }

        /// <summary>
        /// Time-series data for each monitored variable, keyed by description.
        /// Kept for compatibility; <see cref="Series"/> carries the units and the metrics.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<(double TimeSeconds, double Value)>> MonitoredVariables { get; }

        /// <summary>Every monitored variable's series, in the order the integrator holds them.</summary>
        public IReadOnlyList<DynamicsSeries> Series { get; }

        /// <summary>True if integration ran to completion without error and without being aborted.</summary>
        public bool Completed => Errors.Count == 0 && !Aborted;

        /// <summary>The first exception that stopped integration, or null.</summary>
        public Exception Error => Errors.Count > 0 ? Errors[0] : null;

        /// <summary>Every exception raised during the run.</summary>
        public IReadOnlyList<Exception> Errors { get; }

        /// <summary>True when the run stopped on a cancellation, an abort request or a step/time limit.</summary>
        public bool Aborted { get; }

        /// <summary>Description of the schedule that ran.</summary>
        public string ScheduleName { get; }

        /// <summary>Description of the integrator that ran.</summary>
        public string IntegratorName { get; }

        /// <summary>Number of integration steps solved.</summary>
        public int Steps { get; }

        /// <summary>Simulated time reached, in seconds.</summary>
        public double FinalTimeSeconds { get; }

        /// <summary>Wall-clock time the run took.</summary>
        public TimeSpan WallClock { get; }

        /// <summary>
        /// Looks up a series by description, by monitored-variable ID, or by <c>"tag.PropertyId"</c>.
        /// </summary>
        public DynamicsSeries this[string name] => GetSeries(name);

        /// <summary>
        /// Looks up a series by description, by monitored-variable ID, or by <c>"tag.PropertyId"</c>.
        /// </summary>
        /// <exception cref="KeyNotFoundException">No series answers to that name.</exception>
        public DynamicsSeries GetSeries(string name)
        {
            DynamicsSeries found;
            if (_byName.TryGetValue(name, out found)) return found;

            var available = Series.Count == 0
                ? "the integrator recorded none — add monitored variables before running"
                : string.Join(", ", Series.Select(s => "'" + s.Name + "'"));
            throw new KeyNotFoundException("No monitored variable named '" + name + "'. Available: " + available + ".");
        }

        /// <summary>Looks up a series without throwing.</summary>
        public bool TryGetSeries(string name, out DynamicsSeries series) => _byName.TryGetValue(name, out series);

        /// <summary>
        /// Writes the run to a CSV file: one row per step, one column per monitored variable, plus
        /// the time column. Values are invariant-culture, in display units.
        /// </summary>
        public void ToCsv(string path)
        {
            File.WriteAllText(path, ToCsv(), Encoding.UTF8);
        }

        /// <summary>Renders the run as CSV text.</summary>
        public string ToCsv()
        {
            var sb = new StringBuilder();

            sb.Append("t_s");
            foreach (var s in Series)
            {
                sb.Append(',');
                sb.Append(Escape(s.Name + (string.IsNullOrEmpty(s.Units) ? "" : " (" + s.Units + ")")));
            }
            sb.AppendLine();

            var rows = Series.Count == 0 ? 0 : Series.Max(s => s.Count);
            for (var i = 0; i < rows; i++)
            {
                var time = Series.Where(s => i < s.Count).Select(s => s.TimeSeconds[i]).FirstOrDefault();
                sb.Append(time.ToString("G9", CultureInfo.InvariantCulture));
                foreach (var s in Series)
                {
                    sb.Append(',');
                    if (i < s.Count) sb.Append(s.Values[i].ToString("G9", CultureInfo.InvariantCulture));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>Returns a one-line summary of the run.</summary>
        public override string ToString()
        {
            var state = Completed ? "completed" : Aborted ? "aborted" : "failed";
            return "Dynamics run on '" + ScheduleName + "' " + state + ": " + Steps + " steps, " +
                   FinalTimeSeconds.ToString("G6", CultureInfo.InvariantCulture) + " s simulated in " +
                   WallClock.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s, " +
                   Series.Count + " monitored variable(s).";
        }

        private static string Escape(string field)
        {
            if (field.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return field;
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        // -------------------------------------------------------------------------

        /// <summary>
        /// Reads the series out of an integrator's recorded history.
        /// </summary>
        /// <remarks>
        /// The history is keyed by timestamp ticks. Files written by older builds keyed it by step
        /// index instead, so a history whose largest key is too small to be a tick count is read as
        /// step indices and scaled by the integration step.
        /// </remarks>
        internal static IReadOnlyList<DynamicsSeries> ReadSeries(IFlowsheet flowsheet, IDynamicsIntegrator integrator)
        {
            var history = integrator.MonitoredVariableValues;
            if (history == null || history.Count == 0) return new DynamicsSeries[0];

            var keys = history.Keys.OrderBy(k => k).ToList();

            // One second is 10 million ticks, so anything below that cannot be a real timestamp.
            var keysAreStepIndices = keys[keys.Count - 1] < TimeSpan.TicksPerSecond;
            var stepSeconds = integrator.IntegrationStep.TotalSeconds;

            var times = new double[keys.Count];
            for (var i = 0; i < keys.Count; i++)
            {
                times[i] = keysAreStepIndices
                    ? keys[i] * stepSeconds
                    : (double)keys[i] / TimeSpan.TicksPerSecond;
            }

            var tagsById = flowsheet.SimulationObjects.Values
                .Where(o => o.GraphicObject != null)
                .GroupBy(o => o.Name)
                .ToDictionary(g => g.Key, g => g.First().GraphicObject.Tag, StringComparer.Ordinal);

            var result = new List<DynamicsSeries>();

            for (var v = 0; v < integrator.MonitoredVariables.Count; v++)
            {
                var meta = integrator.MonitoredVariables[v];
                var values = new double[keys.Count];

                for (var i = 0; i < keys.Count; i++)
                {
                    var snapshot = history[keys[i]];
                    double value;
                    if (snapshot != null && v < snapshot.Count)
                        double.TryParse(snapshot[v].PropertyValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
                    else
                        value = double.NaN;
                    values[i] = value;
                }

                string tag;
                if (!tagsById.TryGetValue(meta.ObjectID ?? "", out tag)) tag = meta.ObjectID;

                result.Add(new DynamicsSeries(meta.Description, meta.ID, tag, meta.ObjectID,
                    meta.PropertyID, meta.PropertyUnits, times, values));
            }

            return result;
        }
    }
}
