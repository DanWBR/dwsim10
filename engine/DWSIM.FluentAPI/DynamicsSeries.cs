using System;
using System.Collections.Generic;
using System.Linq;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// One monitored variable's time series, with the control-loop metrics you would otherwise
    /// compute by hand: overshoot, rise time, settling time, error integrals and oscillation.
    /// </summary>
    /// <remarks>
    /// Times are seconds from the start of the run; values are in the variable's display units.
    /// The metrics assume the series covers a single disturbance — a step response. Run a longer
    /// simulation with several events and they describe the whole window, not each event.
    /// </remarks>
    public sealed class DynamicsSeries
    {
        private readonly double[] _t;
        private readonly double[] _y;

        internal DynamicsSeries(string name, string variableId, string objectTag, string objectId,
            string propertyId, string units, double[] times, double[] values)
        {
            Name = name;
            VariableId = variableId;
            ObjectTag = objectTag;
            ObjectId = objectId;
            PropertyId = propertyId;
            Units = units;
            _t = times;
            _y = values;
        }

        /// <summary>
        /// Builds a series from samples you already have, so the same control metrics apply to data
        /// that did not come from a run — plant history, a spreadsheet, another simulator.
        /// </summary>
        /// <param name="name">Name for the series.</param>
        /// <param name="times">Sample times in seconds, ascending.</param>
        /// <param name="values">Sample values.</param>
        /// <param name="units">Display units, for reporting only.</param>
        public static DynamicsSeries FromSamples(string name, double[] times, double[] values, string units = "")
        {
            if (times == null) throw new ArgumentNullException(nameof(times));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (times.Length != values.Length)
                throw new ArgumentException("times and values must be the same length.", nameof(values));

            return new DynamicsSeries(name, "", "", "", "", units,
                (double[])times.Clone(), (double[])values.Clone());
        }

        /// <summary>The variable's description, as configured on the integrator.</summary>
        public string Name { get; }

        /// <summary>The monitored variable's internal ID.</summary>
        public string VariableId { get; }

        /// <summary>Tag of the object the property belongs to.</summary>
        public string ObjectTag { get; }

        /// <summary>Internal name of the object the property belongs to.</summary>
        public string ObjectId { get; }

        /// <summary>The property identifier being recorded.</summary>
        public string PropertyId { get; }

        /// <summary>Display units of the recorded values.</summary>
        public string Units { get; }

        /// <summary>Sample times, in seconds from the start of the run.</summary>
        public IReadOnlyList<double> TimeSeconds => _t;

        /// <summary>Sample values, in <see cref="Units"/>.</summary>
        public IReadOnlyList<double> Values => _y;

        /// <summary>Number of samples.</summary>
        public int Count => _y.Length;

        /// <summary>The first recorded value.</summary>
        public double Initial => _y.Length == 0 ? double.NaN : _y[0];

        /// <summary>The last recorded value.</summary>
        public double Final => _y.Length == 0 ? double.NaN : _y[_y.Length - 1];

        /// <summary>The smallest recorded value.</summary>
        public double Min => _y.Length == 0 ? double.NaN : _y.Min();

        /// <summary>The largest recorded value.</summary>
        public double Max => _y.Length == 0 ? double.NaN : _y.Max();

        /// <summary>True when the series contains a NaN, an infinity, or a value beyond 1e12.</summary>
        public bool HasDiverged =>
            _y.Any(v => double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12);

        /// <summary>Linearly interpolates the value at a given time; clamps outside the recorded range.</summary>
        public double ValueAt(double timeSeconds)
        {
            if (_y.Length == 0) return double.NaN;
            if (timeSeconds <= _t[0]) return _y[0];
            if (timeSeconds >= _t[_t.Length - 1]) return _y[_y.Length - 1];

            for (var i = 1; i < _t.Length; i++)
            {
                if (_t[i] < timeSeconds) continue;
                var span = _t[i] - _t[i - 1];
                if (span <= 0.0) return _y[i];
                var f = (timeSeconds - _t[i - 1]) / span;
                return _y[i - 1] + f * (_y[i] - _y[i - 1]);
            }
            return _y[_y.Length - 1];
        }

        /// <summary>
        /// The settled value, taken as the mean over the last <paramref name="lastFraction"/> of the
        /// run. Averaging beats reading the final point, which can land on a ripple.
        /// </summary>
        public double SteadyState(double lastFraction = 0.10)
        {
            if (_y.Length == 0) return double.NaN;
            var take = Math.Max(1, (int)Math.Round(_y.Length * Math.Min(Math.Max(lastFraction, 0.0), 1.0)));
            return _y.Skip(_y.Length - take).Average();
        }

        /// <summary>
        /// Peak overshoot past the setpoint, as a percentage of the step from the initial value.
        /// Returns 0 when the response never crosses past the setpoint.
        /// </summary>
        public double Overshoot(double setpoint)
        {
            if (_y.Length == 0) return double.NaN;

            var step = setpoint - Initial;
            if (Math.Abs(step) < 1e-30) return 0.0;

            // Overshoot is on the side the response is travelling towards.
            var peak = step > 0 ? Max : Min;
            var excess = (peak - setpoint) / step * 100.0;
            return excess > 0.0 ? excess : 0.0;
        }

        /// <summary>Time at which the peak used by <see cref="Overshoot"/> occurs, in seconds.</summary>
        public double PeakTime(double setpoint)
        {
            if (_y.Length == 0) return double.NaN;
            var step = setpoint - Initial;
            var index = 0;
            for (var i = 1; i < _y.Length; i++)
            {
                if (step > 0 ? _y[i] > _y[index] : _y[i] < _y[index]) index = i;
            }
            return _t[index];
        }

        /// <summary>
        /// Time taken to travel from <paramref name="lowFraction"/> to <paramref name="highFraction"/>
        /// of the way from the initial value to the settled value. NaN when the response never gets there.
        /// </summary>
        public double RiseTime(double lowFraction = 0.10, double highFraction = 0.90)
        {
            if (_y.Length < 2) return double.NaN;

            var start = Initial;
            var end = SteadyState();
            var span = end - start;
            if (Math.Abs(span) < 1e-30) return double.NaN;

            var lowValue = start + lowFraction * span;
            var highValue = start + highFraction * span;

            var tLow = FirstCrossing(lowValue, span > 0);
            var tHigh = FirstCrossing(highValue, span > 0);

            if (double.IsNaN(tLow) || double.IsNaN(tHigh)) return double.NaN;
            return tHigh - tLow;
        }

        /// <summary>
        /// Time after which the response stays inside <paramref name="band"/> (as a fraction of the
        /// step) around the settled value. NaN when it never settles within the run.
        /// </summary>
        public double SettlingTime(double band = 0.02)
        {
            if (_y.Length < 2) return double.NaN;

            var end = SteadyState();
            var span = Math.Abs(end - Initial);
            // A response that never moves has no step to scale the band by; fall back on the value.
            var tolerance = band * (span > 1e-30 ? span : Math.Max(Math.Abs(end), 1e-30));

            for (var i = _y.Length - 1; i >= 0; i--)
            {
                if (Math.Abs(_y[i] - end) <= tolerance) continue;
                return i + 1 < _t.Length ? _t[i + 1] : double.NaN;
            }
            return _t[0];
        }

        /// <summary>Steady-state offset from the setpoint, in <see cref="Units"/>.</summary>
        public double Offset(double setpoint) => SteadyState() - setpoint;

        /// <summary>Integral of the absolute error against a setpoint, by the trapezoidal rule.</summary>
        public double IAE(double setpoint) => Integrate(e => Math.Abs(e), setpoint);

        /// <summary>Integral of the squared error against a setpoint.</summary>
        public double ISE(double setpoint) => Integrate(e => e * e, setpoint);

        /// <summary>Time-weighted integral of the absolute error, which penalises slow settling.</summary>
        public double ITAE(double setpoint)
        {
            if (_y.Length < 2) return 0.0;
            var total = 0.0;
            for (var i = 1; i < _y.Length; i++)
            {
                var dt = _t[i] - _t[i - 1];
                var a = _t[i - 1] * Math.Abs(_y[i - 1] - setpoint);
                var b = _t[i] * Math.Abs(_y[i] - setpoint);
                total += 0.5 * (a + b) * dt;
            }
            return total;
        }

        /// <summary>
        /// Detects oscillation by counting crossings of the settled value. Reports the period and
        /// the decay ratio between successive peaks — a ratio near or above 1 means it is not decaying.
        /// </summary>
        public bool IsOscillating(out double periodSeconds, out double decayRatio)
        {
            periodSeconds = double.NaN;
            decayRatio = double.NaN;

            if (_y.Length < 5) return false;

            var mean = SteadyState();
            var amplitude = Math.Max(Math.Abs(Max - mean), Math.Abs(mean - Min));
            // Ripple below a thousandth of the span is numerical noise, not oscillation.
            var threshold = Math.Max(1e-12, 1e-3 * Math.Max(Math.Abs(Max - Min), 1e-30));
            if (amplitude < threshold) return false;

            var crossings = new List<double>();
            for (var i = 1; i < _y.Length; i++)
            {
                var a = _y[i - 1] - mean;
                var b = _y[i] - mean;
                if (a == 0.0 || Math.Sign(a) == Math.Sign(b)) continue;
                var span = b - a;
                var f = span == 0.0 ? 0.0 : -a / span;
                crossings.Add(_t[i - 1] + f * (_t[i] - _t[i - 1]));
            }

            if (crossings.Count < 3) return false;

            // Two crossings make a half-cycle.
            var halfPeriods = new List<double>();
            for (var i = 1; i < crossings.Count; i++) halfPeriods.Add(crossings[i] - crossings[i - 1]);
            periodSeconds = 2.0 * halfPeriods.Average();

            var peaks = FindPeaks(mean);
            if (peaks.Count >= 2 && Math.Abs(peaks[0]) > 1e-30)
                decayRatio = Math.Abs(peaks[peaks.Count - 1]) / Math.Abs(peaks[0]);

            return true;
        }

        /// <summary>
        /// True when the tail of the series is flat: the relative change over the last tenth of the
        /// run is below <paramref name="relativeTolerance"/>.
        /// </summary>
        public bool HasConverged(double relativeTolerance = 1e-4)
        {
            if (_y.Length < 3) return false;

            var take = Math.Max(2, _y.Length / 10);
            var tail = _y.Skip(_y.Length - take).ToArray();
            var scale = Math.Max(Math.Abs(tail.Average()), 1e-30);
            return (tail.Max() - tail.Min()) / scale <= relativeTolerance;
        }

        /// <summary>Fraction of samples sitting at or beyond the given bounds — a saturated actuator.</summary>
        public double SaturationFraction(double minimum, double maximum)
        {
            if (_y.Length == 0) return 0.0;
            var tolerance = 1e-6 * Math.Max(Math.Abs(maximum - minimum), 1e-30);
            var hits = _y.Count(v => v <= minimum + tolerance || v >= maximum - tolerance);
            return (double)hits / _y.Length;
        }

        /// <summary>Returns <c>"&lt;name&gt; (&lt;units&gt;): N points, final = X"</c>.</summary>
        public override string ToString()
        {
            var unit = string.IsNullOrEmpty(Units) ? "" : " (" + Units + ")";
            return Name + unit + ": " + Count + " points, final = " + Final.ToString("G6");
        }

        // -------------------------------------------------------------------------

        private double Integrate(Func<double, double> f, double setpoint)
        {
            if (_y.Length < 2) return 0.0;
            var total = 0.0;
            for (var i = 1; i < _y.Length; i++)
            {
                var dt = _t[i] - _t[i - 1];
                total += 0.5 * (f(_y[i - 1] - setpoint) + f(_y[i] - setpoint)) * dt;
            }
            return total;
        }

        private double FirstCrossing(double level, bool rising)
        {
            for (var i = 1; i < _y.Length; i++)
            {
                var crossed = rising ? _y[i] >= level : _y[i] <= level;
                if (!crossed) continue;

                var span = _y[i] - _y[i - 1];
                if (Math.Abs(span) < 1e-30) return _t[i];
                var f = (level - _y[i - 1]) / span;
                if (f < 0.0) f = 0.0;
                if (f > 1.0) f = 1.0;
                return _t[i - 1] + f * (_t[i] - _t[i - 1]);
            }
            return double.NaN;
        }

        private List<double> FindPeaks(double mean)
        {
            var peaks = new List<double>();
            for (var i = 1; i < _y.Length - 1; i++)
            {
                var previous = _y[i] - _y[i - 1];
                var next = _y[i + 1] - _y[i];
                if (previous > 0 && next <= 0) peaks.Add(_y[i] - mean);
                else if (previous < 0 && next >= 0) peaks.Add(_y[i] - mean);
            }
            return peaks;
        }
    }
}
