using System;
using System.Collections.Generic;

namespace DWSIM.PureCompoundData.Estimation.Fitting
{
    /// Fits the three-parameter Antoine equation
    ///   log10(Psat[mmHg]) = A - B / (T[°C] + C)
    /// to a list of (T[K], Psat[Pa]) points. Uses a one-dimensional search over C
    /// (Golden-section on [-100, +100]), solving A/B linearly for each candidate.
    /// Returns null if fewer than 3 distinct points are provided.
    public static class AntoineFitter
    {
        public sealed class Result
        {
            public double A, B, C;
            public double TMinK, TMaxK;
            public double AARD; // mean absolute relative deviation of Psat
        }

        public static Result? Fit(IReadOnlyList<(double TK, double PsatPa)> points)
        {
            if (points == null || points.Count < 3) return null;

            double tMin = double.MaxValue, tMax = double.MinValue;
            foreach (var p in points)
            {
                if (p.PsatPa <= 0) return null;
                if (p.TK < tMin) tMin = p.TK;
                if (p.TK > tMax) tMax = p.TK;
            }

            double Objective(double c, out double A, out double B, out double aard)
            {
                int p = points.Count;
                var x = new double[p, 2];
                var y = new double[p];
                for (int i = 0; i < p; i++)
                {
                    double Tc = points[i].TK - 273.15;
                    x[i, 0] = 1.0;
                    x[i, 1] = -1.0 / (Tc + c);
                    y[i] = Math.Log10(points[i].PsatPa / 133.322387415); // Pa -> mmHg
                }
                var fit = LinearLeastSquares.Fit(x, y);
                if (fit == null) { A = 0; B = 0; aard = double.MaxValue; return double.MaxValue; }
                A = fit.Value.Beta[0];
                B = fit.Value.Beta[1];
                // Back-compute AARD in Psat space.
                double err = 0;
                for (int i = 0; i < p; i++)
                {
                    double log10P = A - B / ((points[i].TK - 273.15) + c);
                    double pred = Math.Pow(10, log10P) * 133.322387415;
                    err += Math.Abs((points[i].PsatPa - pred) / points[i].PsatPa);
                }
                aard = err / p;
                return aard;
            }

            double lo = -50, hi = 300;
            const double phi = 0.6180339887498949;
            double c1 = hi - phi * (hi - lo);
            double c2 = lo + phi * (hi - lo);
            double f1 = Objective(c1, out _, out _, out _);
            double f2 = Objective(c2, out _, out _, out _);
            for (int i = 0; i < 80 && (hi - lo) > 1e-4; i++)
            {
                if (f1 < f2) { hi = c2; c2 = c1; f2 = f1; c1 = hi - phi * (hi - lo); f1 = Objective(c1, out _, out _, out _); }
                else { lo = c1; c1 = c2; f1 = f2; c2 = lo + phi * (hi - lo); f2 = Objective(c2, out _, out _, out _); }
            }
            double cStar = (lo + hi) / 2;
            Objective(cStar, out double Afit, out double Bfit, out double aardFit);
            return new Result { A = Afit, B = Bfit, C = cStar, TMinK = tMin, TMaxK = tMax, AARD = aardFit };
        }
    }
}
