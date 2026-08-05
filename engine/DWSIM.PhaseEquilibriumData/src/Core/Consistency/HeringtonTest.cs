using System;
using System.Collections.Generic;
using System.Linq;

namespace DWSIM.PhaseEquilibriumData.Core.Consistency
{
    /// <summary>
    /// Herington area (D-J) consistency test for binary isobaric VLE.
    /// Pass criterion (classic): |D - J| &lt; 10.
    /// Reference: Herington, J. Inst. Petrol. 37 (1951) 457.
    /// </summary>
    public static class HeringtonTest
    {
        /// <summary>
        /// Runs the test against the given sorted-by-x1 points. Returns a result with the
        /// area statistic D, the temperature-range correction J, and pass/fail.
        /// </summary>
        public static HeringtonResult Run(
            IReadOnlyList<VlePoint> points,
            ISaturationPressureProvider psat1,
            ISaturationPressureProvider psat2)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (psat1 == null) throw new ArgumentNullException(nameof(psat1));
            if (psat2 == null) throw new ArgumentNullException(nameof(psat2));
            if (points.Count < 3)
                return new HeringtonResult(false, double.NaN, double.NaN, double.NaN,
                    $"Need at least 3 points for trapezoidal integration; got {points.Count}.");

            var sorted = points.OrderBy(p => p.X1).ToArray();

            var x = new double[sorted.Length];
            var lnRatio = new double[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                var p = sorted[i];
                double x1 = p.X1;
                double x2 = 1.0 - x1;
                double y1 = p.Y1;
                double y2 = 1.0 - y1;
                if (x1 <= 0 || x2 <= 0 || y1 <= 0 || y2 <= 0)
                    return new HeringtonResult(false, double.NaN, double.NaN, double.NaN,
                        $"Endpoint or pure-component composition at index {i}; γ ratio undefined.");

                double ps1 = psat1.GetPsatKPa(p.TemperatureK);
                double ps2 = psat2.GetPsatKPa(p.TemperatureK);
                if (ps1 <= 0 || ps2 <= 0)
                    return new HeringtonResult(false, double.NaN, double.NaN, double.NaN,
                        $"Non-positive Psat at index {i} (T={p.TemperatureK:G6} K).");

                double g1 = (y1 * p.PressureKPa) / (x1 * ps1);
                double g2 = (y2 * p.PressureKPa) / (x2 * ps2);
                x[i] = x1;
                lnRatio[i] = Math.Log(g1 / g2);
            }

            double netArea = TrapezoidIntegral(x, lnRatio, absolute: false);
            double absArea = TrapezoidIntegral(x, lnRatio, absolute: true);
            if (absArea <= 0)
                return new HeringtonResult(false, double.NaN, double.NaN, double.NaN,
                    "Absolute-area integral is zero; cannot evaluate Herington D.");

            double d = 100.0 * Math.Abs(netArea) / absArea;

            double tmin = sorted.Min(p => p.TemperatureK);
            double tmax = sorted.Max(p => p.TemperatureK);
            double j = 150.0 * (tmax - tmin) / tmin;

            double dMinusJ = d - j;
            bool passed = Math.Abs(dMinusJ) < 10.0;
            return new HeringtonResult(passed, d, j, dMinusJ, null);
        }

        private static double TrapezoidIntegral(double[] x, double[] y, bool absolute)
        {
            double sum = 0.0;
            for (int i = 1; i < x.Length; i++)
            {
                double dx = x[i] - x[i - 1];
                if (dx <= 0) continue; // drop duplicate-x points
                double a = y[i - 1];
                double b = y[i];
                if (absolute)
                {
                    // Handle sign change within a segment by splitting at the zero crossing.
                    if ((a >= 0 && b >= 0) || (a <= 0 && b <= 0))
                    {
                        sum += 0.5 * (Math.Abs(a) + Math.Abs(b)) * dx;
                    }
                    else
                    {
                        double xz = dx * Math.Abs(a) / (Math.Abs(a) + Math.Abs(b));
                        sum += 0.5 * Math.Abs(a) * xz + 0.5 * Math.Abs(b) * (dx - xz);
                    }
                }
                else
                {
                    sum += 0.5 * (a + b) * dx;
                }
            }
            return sum;
        }
    }

    public sealed record HeringtonResult(bool Passed, double D, double J, double DMinusJ, string? Comment);
}
