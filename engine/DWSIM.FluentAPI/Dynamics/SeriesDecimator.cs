using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DWSIM.Automation.FluentAPI.Dynamics
{
    /// <summary>
    /// Reduces a time series to a handful of points that still look like the original.
    /// </summary>
    /// <remarks>
    /// A dynamic run easily produces thousands of steps, which no chat interface — and no language
    /// model's context — wants in full. Averaging would flatten exactly what matters in a transient:
    /// the overshoot peak and any oscillation. This uses largest-triangle-three-buckets, which
    /// selects real samples by visual significance, and then forces the extremes back in.
    /// </remarks>
    public static class SeriesDecimator
    {
        /// <summary>
        /// Picks at most <paramref name="maxPoints"/> samples from the series, preserving its shape,
        /// its first and last points and its minimum and maximum.
        /// </summary>
        /// <returns>The indices of the selected samples, in ascending order.</returns>
        public static int[] SelectIndices(IReadOnlyList<double> times, IReadOnlyList<double> values, int maxPoints)
        {
            if (times == null) throw new ArgumentNullException(nameof(times));
            if (values == null) throw new ArgumentNullException(nameof(values));

            var n = Math.Min(times.Count, values.Count);
            if (maxPoints < 3) maxPoints = 3;
            if (n <= maxPoints) return Enumerable.Range(0, n).ToArray();

            var selected = new List<int> { 0 };

            // Three buckets: the fixed first and last points, and maxPoints-2 in between.
            var bucketSize = (double)(n - 2) / (maxPoints - 2);
            var previous = 0;

            for (var i = 0; i < maxPoints - 2; i++)
            {
                var start = (int)Math.Floor((i + 1) * bucketSize) + 1;
                var end = Math.Min((int)Math.Floor((i + 2) * bucketSize) + 1, n);

                var nextStart = end;
                var nextEnd = Math.Min((int)Math.Floor((i + 3) * bucketSize) + 1, n);
                if (nextStart >= nextEnd) { nextStart = n - 1; nextEnd = n; }

                double avgT = 0, avgY = 0;
                var count = nextEnd - nextStart;
                for (var j = nextStart; j < nextEnd; j++) { avgT += times[j]; avgY += values[j]; }
                if (count > 0) { avgT /= count; avgY /= count; }

                var bestArea = -1.0;
                var best = start;
                for (var j = start; j < end; j++)
                {
                    var area = Math.Abs(
                        (times[previous] - avgT) * (values[j] - values[previous]) -
                        (times[previous] - times[j]) * (avgY - values[previous])) * 0.5;
                    if (area <= bestArea) continue;
                    bestArea = area;
                    best = j;
                }

                selected.Add(best);
                previous = best;
            }

            selected.Add(n - 1);

            // LTTB optimises for visual area, which can still miss a single-sample spike. The peak
            // of a transient is the whole point of looking, so put the extremes back.
            var minIndex = 0;
            var maxIndex = 0;
            for (var i = 1; i < n; i++)
            {
                if (values[i] < values[minIndex]) minIndex = i;
                if (values[i] > values[maxIndex]) maxIndex = i;
            }
            if (!selected.Contains(minIndex)) selected.Add(minIndex);
            if (!selected.Contains(maxIndex)) selected.Add(maxIndex);

            return selected.Distinct().OrderBy(i => i).ToArray();
        }

        /// <summary>Decimates a series and returns the selected (time, value) pairs.</summary>
        public static (double[] Times, double[] Values) Decimate(
            IReadOnlyList<double> times, IReadOnlyList<double> values, int maxPoints)
        {
            var indices = SelectIndices(times, values, maxPoints);
            var t = new double[indices.Length];
            var y = new double[indices.Length];
            for (var i = 0; i < indices.Length; i++)
            {
                t[i] = times[indices[i]];
                y[i] = values[indices[i]];
            }
            return (t, y);
        }

        /// <summary>
        /// Decimates a series to a preview, honouring an optional time window.
        /// </summary>
        /// <param name="series">The series to sample.</param>
        /// <param name="maxPoints">Point budget; clamped to at least 3 and at most 400.</param>
        /// <param name="startSeconds">Lower time bound, inclusive; null for the start of the run.</param>
        /// <param name="endSeconds">Upper time bound, inclusive; null for the end of the run.</param>
        public static (double[] Times, double[] Values) Preview(DynamicsSeries series, int maxPoints = 40,
            double? startSeconds = null, double? endSeconds = null)
        {
            if (series == null) throw new ArgumentNullException(nameof(series));
            if (maxPoints > 400) maxPoints = 400;

            var times = series.TimeSeconds;
            var values = series.Values;

            if (startSeconds.HasValue || endSeconds.HasValue)
            {
                var lo = startSeconds ?? double.MinValue;
                var hi = endSeconds ?? double.MaxValue;
                var keptT = new List<double>();
                var keptY = new List<double>();
                for (var i = 0; i < series.Count; i++)
                {
                    if (times[i] < lo || times[i] > hi) continue;
                    keptT.Add(times[i]);
                    keptY.Add(values[i]);
                }
                times = keptT;
                values = keptY;
            }

            return Decimate(times, values, maxPoints);
        }

        /// <summary>
        /// Formats a number for transport: six significant digits, invariant culture. Enough
        /// precision to reason about, short enough not to waste a context window.
        /// </summary>
        public static string Format(double value)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsPositiveInfinity(value)) return "Infinity";
            if (double.IsNegativeInfinity(value)) return "-Infinity";
            return value.ToString("G6", CultureInfo.InvariantCulture);
        }
    }
}
