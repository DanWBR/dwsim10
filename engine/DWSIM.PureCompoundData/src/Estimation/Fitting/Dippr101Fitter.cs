using System;
using System.Collections.Generic;

namespace DWSIM.PureCompoundData.Estimation.Fitting
{
    /// Fits the (reduced) DIPPR 101 equation
    ///   ln(Psat[Pa]) = A + B/T + C * ln(T)          (D = 0, E = 0)
    /// to a list of (T[K], Psat[Pa]) points via linear least squares. The D*T^E term is
    /// out of scope for the minimal fitter - it requires non-linear optimization and
    /// is typically only fit when a wide T-range is available.
    public static class Dippr101Fitter
    {
        public sealed class Result
        {
            public double A, B, C;
            public double D = 0, E = 0;
            public double TMinK, TMaxK;
            public double AARD;
        }

        public static Result? Fit(IReadOnlyList<(double TK, double PsatPa)> points)
        {
            if (points == null || points.Count < 3) return null;

            double tMin = double.MaxValue, tMax = double.MinValue;
            foreach (var p in points)
            {
                if (p.PsatPa <= 0 || p.TK <= 0) return null;
                if (p.TK < tMin) tMin = p.TK;
                if (p.TK > tMax) tMax = p.TK;
            }

            int n = points.Count;
            var x = new double[n, 3];
            var y = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i, 0] = 1.0;
                x[i, 1] = 1.0 / points[i].TK;
                x[i, 2] = Math.Log(points[i].TK);
                y[i] = Math.Log(points[i].PsatPa);
            }
            var fit = LinearLeastSquares.Fit(x, y);
            if (fit == null) return null;

            double err = 0;
            for (int i = 0; i < n; i++)
            {
                double ln = fit.Value.Beta[0] + fit.Value.Beta[1] / points[i].TK
                             + fit.Value.Beta[2] * Math.Log(points[i].TK);
                double pred = Math.Exp(ln);
                err += Math.Abs((points[i].PsatPa - pred) / points[i].PsatPa);
            }

            return new Result
            {
                A = fit.Value.Beta[0],
                B = fit.Value.Beta[1],
                C = fit.Value.Beta[2],
                TMinK = tMin,
                TMaxK = tMax,
                AARD = err / n,
            };
        }
    }
}
