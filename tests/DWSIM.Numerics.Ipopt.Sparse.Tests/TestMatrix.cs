using System;

namespace DWSIM.Numerics.Ipopt.Sparse.Tests
{
    /// <summary>
    /// Small dense helpers for the tests: build upper-triangular CSC from a dense
    /// symmetric matrix, multiply, and compute eigenvalues with a self-contained
    /// cyclic Jacobi solver so inertia can be cross-checked without LAPACK.
    /// </summary>
    internal static class TestMatrix
    {
        /// <summary>Builds upper-triangular CSC (diagonal always included) from a dense symmetric matrix.</summary>
        public static (int[] ap, int[] ai, double[] ax) UpperCsc(double[,] a)
        {
            int n = a.GetLength(0);
            var ap = new int[n + 1];
            var ai = new System.Collections.Generic.List<int>();
            var ax = new System.Collections.Generic.List<double>();

            for (int col = 0; col < n; col++)
            {
                ap[col] = ai.Count;
                for (int row = 0; row <= col; row++)
                {
                    double v = a[row, col];
                    if (row == col || v != 0.0)
                    {
                        ai.Add(row);
                        ax.Add(v);
                    }
                }
            }
            ap[n] = ai.Count;
            return (ap, ai.ToArray(), ax.ToArray());
        }

        public static double[] MatVec(double[,] a, double[] x)
        {
            int n = a.GetLength(0);
            var y = new double[n];
            for (int i = 0; i < n; i++)
            {
                double s = 0.0;
                for (int j = 0; j < n; j++) s += a[i, j] * x[j];
                y[i] = s;
            }
            return y;
        }

        public static double MaxAbsDiff(double[] a, double[] b)
        {
            double m = 0.0;
            for (int i = 0; i < a.Length; i++) m = Math.Max(m, Math.Abs(a[i] - b[i]));
            return m;
        }

        /// <summary>Eigenvalues of a symmetric matrix via cyclic Jacobi. Independent inertia oracle.</summary>
        public static double[] Eigenvalues(double[,] aIn)
        {
            int n = aIn.GetLength(0);
            var a = (double[,])aIn.Clone();

            for (int sweep = 0; sweep < 100; sweep++)
            {
                double off = 0.0;
                for (int p = 0; p < n; p++)
                    for (int q = p + 1; q < n; q++)
                        off += a[p, q] * a[p, q];
                if (off < 1e-28) break;

                for (int p = 0; p < n; p++)
                {
                    for (int q = p + 1; q < n; q++)
                    {
                        if (a[p, q] == 0.0) continue;

                        double theta = (a[q, q] - a[p, p]) / (2.0 * a[p, q]);
                        double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                        if (theta == 0.0) t = 1.0;
                        double c = 1.0 / Math.Sqrt(t * t + 1.0);
                        double s = t * c;

                        for (int i = 0; i < n; i++)
                        {
                            double aip = a[i, p];
                            double aiq = a[i, q];
                            a[i, p] = c * aip - s * aiq;
                            a[i, q] = s * aip + c * aiq;
                        }
                        for (int i = 0; i < n; i++)
                        {
                            double api = a[p, i];
                            double aqi = a[q, i];
                            a[p, i] = c * api - s * aqi;
                            a[q, i] = s * api + c * aqi;
                        }
                    }
                }
            }

            var eig = new double[n];
            for (int i = 0; i < n; i++) eig[i] = a[i, i];
            return eig;
        }

        public static (int pos, int neg, int zero) InertiaOf(double[,] a, double tol = 1e-9)
        {
            var eig = Eigenvalues(a);
            int pos = 0, neg = 0, zero = 0;
            foreach (var e in eig)
            {
                if (e > tol) pos++;
                else if (e < -tol) neg++;
                else zero++;
            }
            return (pos, neg, zero);
        }
    }
}
