using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Dynamics;
using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Unit tests for the analysis layer: the metrics and the decimator work on plain arrays, so
    /// they can be checked against responses whose answers are known in closed form.
    /// </summary>
    [TestFixture]
    public class DynamicsAnalysisTests
    {
        /// <summary>
        /// A second-order step response with a known damping ratio has a textbook overshoot, so the
        /// metric can be checked against the formula rather than against itself.
        /// </summary>
        [Test]
        public void SecondOrderMetricsMatchTheClosedForm()
        {
            // zeta = 0.5, wn = 1 rad/s. Overshoot = exp(-pi*zeta/sqrt(1-zeta^2)) = 16.3 %,
            // and the first peak lands at pi / (wn*sqrt(1-zeta^2)) = 3.628 s.
            const double zeta = 0.5;
            const double wn = 1.0;
            const double setpoint = 1.0;

            var n = 4001;
            var t = new double[n];
            var y = new double[n];
            var wd = wn * Math.Sqrt(1 - zeta * zeta);

            for (var i = 0; i < n; i++)
            {
                t[i] = i * 0.01;
                y[i] = setpoint * (1 - Math.Exp(-zeta * wn * t[i]) *
                    (Math.Cos(wd * t[i]) + zeta / Math.Sqrt(1 - zeta * zeta) * Math.Sin(wd * t[i])));
            }

            var series = Series(t, y);

            var expectedOvershoot = Math.Exp(-Math.PI * zeta / Math.Sqrt(1 - zeta * zeta)) * 100.0;
            var expectedPeakTime = Math.PI / wd;

            new ResultTable("Second-order step response")
                .Row("overshoot", expectedOvershoot, series.Overshoot(setpoint), 0.02, "%")
                .Row("peak time", expectedPeakTime, series.PeakTime(setpoint), 0.02, "s")
                .Row("steady state", setpoint, series.SteadyState(), 0.01)
                .RowInRange("settling time within 2 %", 5.0, 9.0, series.SettlingTime(0.02), "s")
                .RowInRange("rise time 10-90 %", 1.0, 3.0, series.RiseTime(), "s")
                .PrintAndThrowIfFailed();

            // The steady state is a tail average, so the offset carries the tail's residual ripple
            // rather than being exactly zero.
            Assert.That(Math.Abs(series.Offset(setpoint)), Is.LessThan(1e-6));
            Assert.That(series.HasConverged(), Is.True, "A settled response should read as converged.");
            Assert.That(series.HasDiverged, Is.False);
        }

        /// <summary>An undamped sine is the clearest case of sustained oscillation there is.</summary>
        [Test]
        public void AnUndampedOscillationIsDetectedWithItsPeriod()
        {
            const double period = 4.0;
            var n = 2001;
            var t = new double[n];
            var y = new double[n];
            for (var i = 0; i < n; i++)
            {
                t[i] = i * 0.01;
                y[i] = 1.0 + 0.3 * Math.Sin(2 * Math.PI * t[i] / period);
            }

            var series = Series(t, y);

            double detectedPeriod, decay;
            Assert.That(series.IsOscillating(out detectedPeriod, out decay), Is.True,
                "A pure sine should read as oscillating.");
            Assert.That(detectedPeriod, Is.EqualTo(period).Within(0.05 * period));
            Assert.That(series.HasConverged(), Is.False, "An oscillation has not converged.");
        }

        /// <summary>A flat line must not be reported as an oscillation just because of rounding noise.</summary>
        [Test]
        public void AFlatSeriesIsNotReportedAsOscillating()
        {
            var n = 500;
            var t = new double[n];
            var y = new double[n];
            for (var i = 0; i < n; i++)
            {
                t[i] = i * 0.1;
                y[i] = 5.0 + (i % 2 == 0 ? 1e-14 : -1e-14);
            }

            var series = Series(t, y);

            double period, decay;
            Assert.That(series.IsOscillating(out period, out decay), Is.False);
            Assert.That(series.HasConverged(), Is.True);
        }

        /// <summary>Error integrals against a constant offset are exact, so they can be checked by hand.</summary>
        [Test]
        public void TheErrorIntegralsMatchAHandComputedOffset()
        {
            var n = 101;
            var t = new double[n];
            var y = new double[n];
            for (var i = 0; i < n; i++) { t[i] = i * 1.0; y[i] = 3.0; }

            var series = Series(t, y);

            // A constant error of 2 over 100 s: IAE = 200, ISE = 400, ITAE = 2 * 100^2 / 2 = 10000.
            new ResultTable("Error integrals at a constant offset")
                .Row("IAE", 200.0, series.IAE(1.0), 1e-9)
                .Row("ISE", 400.0, series.ISE(1.0), 1e-9)
                .Row("ITAE", 10000.0, series.ITAE(1.0), 1e-9)
                .PrintAndThrowIfFailed();
        }

        /// <summary>
        /// The whole point of the decimator is that a reader sees the peak. A single-sample spike
        /// in a long flat series is the case that averaging would erase.
        /// </summary>
        [Test]
        public void DecimationKeepsTheExtremesAndTheEndpoints()
        {
            var n = 5000;
            var t = new double[n];
            var y = new double[n];
            for (var i = 0; i < n; i++) { t[i] = i * 0.1; y[i] = 1.0; }
            y[3123] = 42.0;
            y[1500] = -7.0;

            var decimated = SeriesDecimator.Decimate(t, y, 40);

            Assert.That(decimated.Values.Length, Is.LessThanOrEqualTo(42),
                "The budget is 40 points plus the two forced extremes.");
            Assert.That(decimated.Values.Max(), Is.EqualTo(42.0), "The spike was dropped.");
            Assert.That(decimated.Values.Min(), Is.EqualTo(-7.0), "The dip was dropped.");
            Assert.That(decimated.Times[0], Is.EqualTo(t[0]));
            Assert.That(decimated.Times[decimated.Times.Length - 1], Is.EqualTo(t[n - 1]));

            // Times must stay ordered, or a chart drawn from them zigzags.
            for (var i = 1; i < decimated.Times.Length; i++)
                Assert.That(decimated.Times[i], Is.GreaterThan(decimated.Times[i - 1]));
        }

        /// <summary>A series shorter than the budget comes back whole.</summary>
        [Test]
        public void DecimationLeavesShortSeriesAlone()
        {
            var t = new double[] { 0, 1, 2, 3, 4 };
            var y = new double[] { 5, 6, 7, 8, 9 };

            var decimated = SeriesDecimator.Decimate(t, y, 40);

            Assert.That(decimated.Values, Is.EqualTo(y));
            Assert.That(decimated.Times, Is.EqualTo(t));
        }

        /// <summary>A saturated actuator is a fraction of samples pinned at a limit.</summary>
        [Test]
        public void SaturationIsMeasuredAgainstTheOutputLimits()
        {
            var n = 100;
            var t = new double[n];
            var y = new double[n];
            for (var i = 0; i < n; i++) { t[i] = i; y[i] = i < 80 ? 100.0 : 50.0; }

            var series = Series(t, y);

            Assert.That(series.SaturationFraction(0.0, 100.0), Is.EqualTo(0.8).Within(1e-9));
            Assert.That(series.SaturationFraction(0.0, 200.0), Is.EqualTo(0.0).Within(1e-9));
        }

        /// <summary>Divergence has to be caught even when no value is NaN.</summary>
        [Test]
        public void DivergenceIsDetected()
        {
            var t = new double[] { 0, 1, 2, 3 };
            Assert.That(Series(t, new double[] { 1, 10, 1e6, 1e14 }).HasDiverged, Is.True);
            Assert.That(Series(t, new double[] { 1, 2, double.NaN, 4 }).HasDiverged, Is.True);
            Assert.That(Series(t, new double[] { 1, 2, 3, 4 }).HasDiverged, Is.False);
        }

        /// <summary>Every code a finding can carry has to be documented, or the catalogue lies.</summary>
        [Test]
        public void EveryDiagnosticCodeIsDocumented()
        {
            Assert.That(DiagnosticCodes.All, Is.Not.Empty);
            foreach (var entry in DiagnosticCodes.All)
            {
                Assert.That(entry.Key, Does.Match("^[A-Z][A-Z0-9_]+$"),
                    "Diagnostic codes are upper snake case so they stay stable across languages.");
                Assert.That(entry.Value, Is.Not.Empty);
            }
        }

        private static DynamicsSeries Series(double[] t, double[] y)
        {
            return DynamicsSeries.FromSamples("test", t, y);
        }
    }
}
