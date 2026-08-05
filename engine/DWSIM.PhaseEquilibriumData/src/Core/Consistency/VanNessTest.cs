using System;
using System.Collections.Generic;
using System.Linq;

namespace DWSIM.PhaseEquilibriumData.Core.Consistency
{
    /// <summary>
    /// Van Ness modified point consistency test for binary (isothermal) VLE.
    /// Fits a two-parameter Margules activity-coefficient model to the measured
    /// ln(γ1/γ2) via linear least-squares on (A12, A21), then back-predicts y1
    /// and reports mean |Δy1|. Pass criterion: mean |Δy1| &lt; 0.01.
    /// Reference: Van Ness, Pure Appl. Chem. 67 (1995) 859.
    /// </summary>
    public static class VanNessTest
    {
        public static VanNessResult Run(
            IReadOnlyList<VlePoint> points,
            ISaturationPressureProvider psat1,
            ISaturationPressureProvider psat2)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (psat1 == null) throw new ArgumentNullException(nameof(psat1));
            if (psat2 == null) throw new ArgumentNullException(nameof(psat2));
            if (points.Count < 3)
                return new VanNessResult(false, double.NaN, double.NaN, double.NaN,
                    $"Need at least 3 points for 2-parameter Margules fit; got {points.Count}.");

            int n = points.Count;
            var x1 = new double[n];
            var y1Exp = new double[n];
            var P = new double[n];
            var ps1 = new double[n];
            var ps2 = new double[n];
            var lnRatioExp = new double[n];

            for (int i = 0; i < n; i++)
            {
                var p = points[i];
                double x = p.X1;
                double y = p.Y1;
                if (x <= 0 || x >= 1 || y <= 0 || y >= 1)
                    return new VanNessResult(false, double.NaN, double.NaN, double.NaN,
                        $"Endpoint at index {i}; model undefined.");
                ps1[i] = psat1.GetPsatKPa(p.TemperatureK);
                ps2[i] = psat2.GetPsatKPa(p.TemperatureK);
                if (ps1[i] <= 0 || ps2[i] <= 0)
                    return new VanNessResult(false, double.NaN, double.NaN, double.NaN,
                        $"Non-positive Psat at index {i}.");
                x1[i] = x;
                y1Exp[i] = y;
                P[i] = p.PressureKPa;

                double g1 = (y * p.PressureKPa) / (x * ps1[i]);
                double g2 = ((1 - y) * p.PressureKPa) / ((1 - x) * ps2[i]);
                lnRatioExp[i] = Math.Log(g1 / g2);
            }

            // Two-parameter Margules: ln(γ1/γ2) = A12*(1 - 2*x1)*... expanded:
            //   ln γ1 = x2^2 [A12 + 2*(A21 - A12)*x1]
            //   ln γ2 = x1^2 [A21 + 2*(A12 - A21)*x2]
            // ln(γ1/γ2) is linear in (A12, A21): ln(γ1/γ2) = c1(x1)*A12 + c2(x1)*A21
            // where c1(x1) = x2^2 - 2*x1*x2^2 - x1^2*(-2*x2) = derive symbolically below.
            //
            // Direct expansion:
            //   ln γ1 = A12*x2^2 + 2*(A21-A12)*x1*x2^2 = x2^2*(A12 + 2*x1*A21 - 2*x1*A12)
            //         = A12*x2^2*(1 - 2*x1) + A21*(2*x1*x2^2)
            //   ln γ2 = A21*x1^2*(1 - 2*x2) + A12*(2*x2*x1^2)
            //         = A21*x1^2*(1 - 2*x2) + A12*(2*x1^2*x2)
            // ln(γ1/γ2) = A12*[x2^2*(1 - 2*x1) - 2*x1^2*x2] + A21*[2*x1*x2^2 - x1^2*(1 - 2*x2)]
            // Solve Ax = b by normal equations.
            double s11 = 0, s12 = 0, s22 = 0, r1 = 0, r2 = 0;
            for (int i = 0; i < n; i++)
            {
                double xx1 = x1[i];
                double xx2 = 1 - xx1;
                double c1 = xx2 * xx2 * (1 - 2 * xx1) - 2 * xx1 * xx1 * xx2;
                double c2 = 2 * xx1 * xx2 * xx2 - xx1 * xx1 * (1 - 2 * xx2);
                s11 += c1 * c1;
                s12 += c1 * c2;
                s22 += c2 * c2;
                r1 += c1 * lnRatioExp[i];
                r2 += c2 * lnRatioExp[i];
            }
            double det = s11 * s22 - s12 * s12;
            if (Math.Abs(det) < 1e-18)
                return new VanNessResult(false, double.NaN, double.NaN, double.NaN,
                    "Normal-equation matrix is singular; fit failed.");
            double a12 = (s22 * r1 - s12 * r2) / det;
            double a21 = (s11 * r2 - s12 * r1) / det;

            double sumAbsDy = 0;
            for (int i = 0; i < n; i++)
            {
                double xx1 = x1[i];
                double xx2 = 1 - xx1;
                double lnG1 = xx2 * xx2 * (a12 + 2 * (a21 - a12) * xx1);
                double lnG2 = xx1 * xx1 * (a21 + 2 * (a12 - a21) * xx2);
                double g1 = Math.Exp(lnG1);
                double g2 = Math.Exp(lnG2);
                double pCalc = xx1 * g1 * ps1[i] + xx2 * g2 * ps2[i];
                double y1Calc = xx1 * g1 * ps1[i] / pCalc;
                sumAbsDy += Math.Abs(y1Exp[i] - y1Calc);
            }
            double meanAbsDy = sumAbsDy / n;
            bool passed = meanAbsDy < 0.01;
            return new VanNessResult(passed, meanAbsDy, a12, a21, null);
        }
    }

    public sealed record VanNessResult(bool Passed, double MeanAbsDeltaY1, double A12, double A21, string? Comment);
}
