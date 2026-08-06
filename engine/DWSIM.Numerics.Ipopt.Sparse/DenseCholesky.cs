// Dense Cholesky factorization (A = L L^T, lower triangle), the managed
// equivalent of LAPACK dpotrf/dpotrs. In the DWSIM profile (limited-memory BFGS,
// m = 0) Ipopt's low-rank augmented-system solver factorizes small SPD systems
// with Cholesky; this covers that need. Unblocked, but the systems are tiny.

using System;

namespace DWSIM.Numerics.Ipopt.Sparse
{
    /// <summary>Dense Cholesky factorization and solve for a symmetric positive-definite matrix.</summary>
    public sealed class DenseCholesky
    {
        private int _n;
        private double[,] _l = new double[0, 0]; // lower-triangular factor

        /// <summary>Matrix dimension.</summary>
        public int N => _n;

        /// <summary>
        /// Factorizes the SPD matrix given by the lower triangle of <paramref name="spd"/>.
        /// Returns <see cref="FactorStatus.NotPositiveDefinite"/> if a non-positive pivot is met.
        /// </summary>
        public FactorStatus Factorize(double[,] spd)
        {
            if (spd is null) throw new ArgumentNullException(nameof(spd));
            int n = spd.GetLength(0);
            if (spd.GetLength(1) != n) throw new ArgumentException("Matrix must be square.", nameof(spd));

            _n = n;
            if (_l.GetLength(0) < n) _l = new double[n, n];
            var l = _l;

            for (int i = 0; i < n; i++)
                for (int j = 0; j <= i; j++)
                    l[i, j] = spd[i, j];
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    l[i, j] = 0.0;

            for (int j = 0; j < n; j++)
            {
                double sum = l[j, j];
                for (int k = 0; k < j; k++) sum -= l[j, k] * l[j, k];
                if (sum <= 0.0)
                    return FactorStatus.NotPositiveDefinite;

                double ljj = Math.Sqrt(sum);
                l[j, j] = ljj;

                for (int i = j + 1; i < n; i++)
                {
                    double s = l[i, j];
                    for (int k = 0; k < j; k++) s -= l[i, k] * l[j, k];
                    l[i, j] = s / ljj;
                }
            }

            return FactorStatus.Success;
        }

        /// <summary>Solves A x = b in place using the most recent factorization.</summary>
        public void Solve(double[] b)
        {
            if (b is null) throw new ArgumentNullException(nameof(b));
            if (b.Length < _n) throw new ArgumentException("rhs shorter than N.", nameof(b));

            var l = _l;
            int n = _n;

            // Forward: L y = b.
            for (int i = 0; i < n; i++)
            {
                double s = b[i];
                for (int k = 0; k < i; k++) s -= l[i, k] * b[k];
                b[i] = s / l[i, i];
            }
            // Backward: L^T x = y.
            for (int i = n - 1; i >= 0; i--)
            {
                double s = b[i];
                for (int k = i + 1; k < n; k++) s -= l[k, i] * b[k];
                b[i] = s / l[i, i];
            }
        }
    }
}
