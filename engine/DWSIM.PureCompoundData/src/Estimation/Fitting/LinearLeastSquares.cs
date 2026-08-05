using System;

namespace DWSIM.PureCompoundData.Estimation.Fitting
{
    /// Small in-place Gaussian-elimination solver for dense linear systems,
    /// used by the Antoine / DIPPR fitters to solve the normal equations.
    internal static class LinearLeastSquares
    {
        /// Solves A * x = b for x, n in [1..8]. Returns null if singular.
        internal static double[]? Solve(double[,] a, double[] b)
        {
            int n = b.Length;
            var m = new double[n, n + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) m[i, j] = a[i, j];
                m[i, n] = b[i];
            }
            for (int k = 0; k < n; k++)
            {
                int piv = k;
                double best = Math.Abs(m[k, k]);
                for (int i = k + 1; i < n; i++)
                    if (Math.Abs(m[i, k]) > best) { best = Math.Abs(m[i, k]); piv = i; }
                if (best < 1e-14) return null;
                if (piv != k)
                    for (int j = 0; j <= n; j++)
                    { var tmp = m[k, j]; m[k, j] = m[piv, j]; m[piv, j] = tmp; }
                for (int i = k + 1; i < n; i++)
                {
                    double f = m[i, k] / m[k, k];
                    for (int j = k; j <= n; j++) m[i, j] -= f * m[k, j];
                }
            }
            var x = new double[n];
            for (int i = n - 1; i >= 0; i--)
            {
                double s = m[i, n];
                for (int j = i + 1; j < n; j++) s -= m[i, j] * x[j];
                x[i] = s / m[i, i];
            }
            return x;
        }

        /// Linear least-squares fit of y = X*beta with k parameters, p points.
        /// Returns beta[k] and residual AARD (mean absolute relative deviation of y vs fit).
        internal static (double[] Beta, double AARD)? Fit(double[,] x, double[] y)
        {
            int p = y.Length;
            int k = x.GetLength(1);
            var xtx = new double[k, k];
            var xty = new double[k];
            for (int i = 0; i < p; i++)
            {
                for (int a = 0; a < k; a++)
                {
                    xty[a] += x[i, a] * y[i];
                    for (int b = 0; b < k; b++)
                        xtx[a, b] += x[i, a] * x[i, b];
                }
            }
            var beta = Solve(xtx, xty);
            if (beta == null) return null;

            double aard = 0;
            int counted = 0;
            for (int i = 0; i < p; i++)
            {
                double fit = 0;
                for (int a = 0; a < k; a++) fit += x[i, a] * beta[a];
                if (Math.Abs(y[i]) > 1e-12) { aard += Math.Abs((y[i] - fit) / y[i]); counted++; }
            }
            return (beta, counted > 0 ? aard / counted : 0);
        }
    }
}
