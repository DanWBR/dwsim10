// Dense symmetric-indefinite factorization via the Bunch-Kaufman diagonal
// pivoting method (LAPACK dsytf2/dsytrs equivalent, lower triangle), producing
// A = P L D L^T P^T with 1x1 and 2x2 diagonal blocks. Unlike the static LDL of
// QDLDL, the 2x2 pivots make this robust on genuinely indefinite matrices and
// give a *correct* inertia with no regularization -- so it serves two roles:
//   1. a validation oracle for the sparse QDLDL path, and
//   2. a robust fast-path solver for small KKT systems (N up to a couple thousand).
//
// The pivoting/update/solve steps mirror the reference LAPACK routines closely
// so they can be checked against them.

using System;

namespace DWSIM.Numerics.Ipopt.Sparse
{
    /// <summary>
    /// Dense Bunch-Kaufman LDL^T factorization of a symmetric indefinite matrix,
    /// with correct inertia (via 1x1/2x2 diagonal blocks) and solve.
    /// </summary>
    public sealed class BunchKaufman
    {
        // (1 + sqrt(17)) / 8, the Bunch-Kaufman pivoting threshold.
        private static readonly double Alpha = (1.0 + Math.Sqrt(17.0)) / 8.0;

        private int _n;
        private double[,] _a = new double[0, 0]; // working lower triangle, factorized in place
        private int[] _ipiv = Array.Empty<int>(); // >=0: 1x1 pivot index; <0: 2x2 block, decode -(v)-1

        /// <summary>Matrix dimension.</summary>
        public int N => _n;

        /// <summary>Inertia from the most recent factorization.</summary>
        public Inertia Inertia { get; private set; }

        /// <summary>
        /// Factorizes the symmetric matrix given by the lower triangle of
        /// <paramref name="symmetric"/> (an n x n array; the strict upper triangle is
        /// ignored). The input is copied, not modified.
        /// </summary>
        public FactorStatus Factorize(double[,] symmetric)
        {
            if (symmetric is null) throw new ArgumentNullException(nameof(symmetric));
            int n = symmetric.GetLength(0);
            if (symmetric.GetLength(1) != n) throw new ArgumentException("Matrix must be square.", nameof(symmetric));

            _n = n;
            if (_a.GetLength(0) < n) _a = new double[n, n];
            if (_ipiv.Length < n) _ipiv = new int[n];

            double[,] a = _a;
            for (int j = 0; j < n; j++)
                for (int i = j; i < n; i++)
                    a[i, j] = symmetric[i, j];

            bool singular = false;
            int[] ipiv = _ipiv;

            int k = 0;
            while (k < n)
            {
                int kstep = 1;
                int kp;

                double absakk = Math.Abs(a[k, k]);

                // Largest off-diagonal magnitude in column k below the diagonal.
                int imax = -1;
                double colmax = 0.0;
                for (int i = k + 1; i < n; i++)
                {
                    double v = Math.Abs(a[i, k]);
                    if (v > colmax) { colmax = v; imax = i; }
                }

                if (Math.Max(absakk, colmax) == 0.0)
                {
                    // Null column: zero pivot, nothing to eliminate.
                    kp = k;
                    singular = true;
                }
                else
                {
                    if (absakk >= Alpha * colmax)
                    {
                        kp = k; // 1x1, no interchange
                    }
                    else
                    {
                        // rowmax: largest off-diagonal magnitude in "row" imax.
                        double rowmax = 0.0;
                        for (int j = k; j < imax; j++)
                        {
                            double v = Math.Abs(a[imax, j]);
                            if (v > rowmax) rowmax = v;
                        }
                        for (int i = imax + 1; i < n; i++)
                        {
                            double v = Math.Abs(a[i, imax]);
                            if (v > rowmax) rowmax = v;
                        }

                        if (absakk >= Alpha * colmax * (colmax / rowmax))
                        {
                            kp = k; // 1x1, no interchange
                        }
                        else if (Math.Abs(a[imax, imax]) >= Alpha * rowmax)
                        {
                            kp = imax; // 1x1 with interchange k <-> imax
                        }
                        else
                        {
                            kp = imax; // 2x2 pivot
                            kstep = 2;
                        }
                    }
                }

                int kk = k + kstep - 1; // column that kp gets swapped with

                if (kp != kk)
                {
                    // Symmetric interchange of rows/columns kk and kp in the trailing block.
                    for (int i = kp + 1; i < n; i++)
                    {
                        (a[i, kk], a[i, kp]) = (a[i, kp], a[i, kk]);
                    }
                    for (int j = kk + 1; j < kp; j++)
                    {
                        (a[j, kk], a[kp, j]) = (a[kp, j], a[j, kk]);
                    }
                    (a[kk, kk], a[kp, kp]) = (a[kp, kp], a[kk, kk]);
                    if (kstep == 2)
                    {
                        (a[k + 1, k], a[kp, k]) = (a[kp, k], a[k + 1, k]);
                    }
                }

                if (kstep == 1)
                {
                    double akk = a[k, k];
                    if (akk != 0.0 && k < n - 1)
                    {
                        double d11inv = 1.0 / akk;
                        // Rank-1 update of the trailing block using the unscaled column, then scale.
                        for (int i = k + 1; i < n; i++)
                        {
                            double wi = a[i, k];
                            if (wi != 0.0)
                            {
                                double f = d11inv * wi;
                                for (int j = k + 1; j <= i; j++)
                                {
                                    a[i, j] -= f * a[j, k];
                                }
                            }
                        }
                        for (int i = k + 1; i < n; i++)
                        {
                            a[i, k] *= d11inv;
                        }
                    }
                    ipiv[k] = kp; // >= 0 marks a 1x1 pivot
                }
                else
                {
                    if (k < n - 2)
                    {
                        double d21 = a[k + 1, k];
                        double d11 = a[k + 1, k + 1] / d21;
                        double d22 = a[k, k] / d21;
                        double t = 1.0 / (d11 * d22 - 1.0);
                        d21 = t / d21;

                        for (int j = k + 2; j < n; j++)
                        {
                            double wk = d21 * (d11 * a[j, k] - a[j, k + 1]);
                            double wkp1 = d21 * (d22 * a[j, k + 1] - a[j, k]);
                            for (int i = j; i < n; i++)
                            {
                                a[i, j] -= a[i, k] * wk + a[i, k + 1] * wkp1;
                            }
                            a[j, k] = wk;
                            a[j, k + 1] = wkp1;
                        }
                    }
                    int code = -(kp + 1); // < 0 marks a 2x2 block
                    ipiv[k] = code;
                    ipiv[k + 1] = code;
                }

                k += kstep;
            }

            Inertia = ComputeInertia();
            return singular ? FactorStatus.ZeroPivot : FactorStatus.Success;
        }

        private Inertia ComputeInertia()
        {
            int pos = 0, neg = 0, zero = 0;
            double[,] a = _a;
            int n = _n;

            int k = 0;
            while (k < n)
            {
                if (_ipiv[k] >= 0)
                {
                    double d = a[k, k];
                    if (d > 0.0) pos++;
                    else if (d < 0.0) neg++;
                    else zero++;
                    k += 1;
                }
                else
                {
                    // 2x2 block at (k, k+1): eigenvalues of [[a11, a21],[a21, a22]].
                    double a11 = a[k, k];
                    double a21 = a[k + 1, k];
                    double a22 = a[k + 1, k + 1];
                    double tr = a11 + a22;
                    double det = a11 * a22 - a21 * a21;
                    double disc = Math.Sqrt(Math.Max(0.0, tr * tr - 4.0 * det));
                    double l1 = 0.5 * (tr + disc);
                    double l2 = 0.5 * (tr - disc);
                    CountSign(l1, ref pos, ref neg, ref zero);
                    CountSign(l2, ref pos, ref neg, ref zero);
                    k += 2;
                }
            }
            return new Inertia(pos, neg, zero);
        }

        private static void CountSign(double v, ref int pos, ref int neg, ref int zero)
        {
            if (v > 0.0) pos++;
            else if (v < 0.0) neg++;
            else zero++;
        }

        /// <summary>Solves A x = b in place using the most recent factorization.</summary>
        public void Solve(double[] b)
        {
            if (b is null) throw new ArgumentNullException(nameof(b));
            if (b.Length < _n) throw new ArgumentException("rhs shorter than N.", nameof(b));

            double[,] a = _a;
            int n = _n;
            int[] ipiv = _ipiv;

            // Phase 1: solve L D y = b.
            int k = 0;
            while (k < n)
            {
                if (ipiv[k] >= 0)
                {
                    int kp = ipiv[k];
                    if (kp != k) (b[k], b[kp]) = (b[kp], b[k]);
                    double bk = b[k];
                    for (int i = k + 1; i < n; i++) b[i] -= a[i, k] * bk;
                    b[k] = bk / a[k, k];
                    k += 1;
                }
                else
                {
                    int kp = -ipiv[k] - 1;
                    if (kp != k + 1) (b[k + 1], b[kp]) = (b[kp], b[k + 1]);
                    double bk = b[k];
                    double bk1 = b[k + 1];
                    for (int i = k + 2; i < n; i++) b[i] -= a[i, k] * bk + a[i, k + 1] * bk1;

                    double akm1k = a[k + 1, k];
                    double akm1 = a[k, k] / akm1k;
                    double ak = a[k + 1, k + 1] / akm1k;
                    double denom = akm1 * ak - 1.0;
                    double bkm1s = bk / akm1k;
                    double bks = bk1 / akm1k;
                    b[k] = (ak * bkm1s - bks) / denom;
                    b[k + 1] = (akm1 * bks - bkm1s) / denom;
                    k += 2;
                }
            }

            // Phase 2: solve L^T x = y (with the interchanges undone in reverse).
            k = n - 1;
            while (k >= 0)
            {
                if (ipiv[k] >= 0)
                {
                    if (k < n - 1)
                    {
                        double s = 0.0;
                        for (int i = k + 1; i < n; i++) s += a[i, k] * b[i];
                        b[k] -= s;
                    }
                    int kp = ipiv[k];
                    if (kp != k) (b[k], b[kp]) = (b[kp], b[k]);
                    k -= 1;
                }
                else
                {
                    if (k < n - 1)
                    {
                        double s = 0.0, s2 = 0.0;
                        for (int i = k + 1; i < n; i++)
                        {
                            s += a[i, k] * b[i];
                            s2 += a[i, k - 1] * b[i];
                        }
                        b[k] -= s;
                        b[k - 1] -= s2;
                    }
                    int kp = -ipiv[k] - 1;
                    if (kp != k) (b[k], b[kp]) = (b[kp], b[k]);
                    k -= 2;
                }
            }
        }
    }
}
