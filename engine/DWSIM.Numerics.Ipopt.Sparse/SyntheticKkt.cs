// Generates synthetic quasidefinite KKT systems with a *known* inertia, so the
// serialization + replay pipeline can be exercised and tested before any real
// capture from native Ipopt exists. Each record is [[H, A^T],[A, -D]] with H, D
// SPD, whose inertia is exactly (p positive, q negative); NativeNegEVals is set
// to q as the ground truth a real MA57 capture would provide.

using System;
using System.Collections.Generic;

namespace DWSIM.Numerics.Ipopt.Sparse
{
    /// <summary>Builds synthetic quasidefinite KKT records with a known inertia.</summary>
    public static class SyntheticKkt
    {
        public static List<KktRecord> Generate(int count, int seed = 1, int maxP = 25, int maxQ = 12)
        {
            var rng = new Random(seed);
            var list = new List<KktRecord>(count);
            for (int c = 0; c < count; c++)
            {
                int p = 1 + rng.Next(maxP);
                int q = rng.Next(maxQ + 1);
                list.Add(One(rng, p, q));
            }
            return list;
        }

        private static KktRecord One(Random rng, int p, int q)
        {
            int n = p + q;
            var k = new double[n, n];

            // H (SPD, diagonally dominant) in the top-left block.
            for (int i = 0; i < p; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double v = (i == j) ? p + 1.0 + rng.NextDouble() : (rng.NextDouble() - 0.5) * 0.2;
                    k[i, j] = v; k[j, i] = v;
                }
            }
            // -D (D SPD) in the bottom-right block.
            for (int i = 0; i < q; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double v = (i == j) ? -(q + 1.0 + rng.NextDouble()) : (rng.NextDouble() - 0.5) * 0.2;
                    k[p + i, p + j] = v; k[p + j, p + i] = v;
                }
            }
            // Coupling A / A^T.
            for (int i = 0; i < q; i++)
            {
                for (int j = 0; j < p; j++)
                {
                    double v = rng.NextDouble() - 0.5;
                    k[p + i, j] = v; k[j, p + i] = v;
                }
            }

            // Lower-triangle triplets.
            var irn = new List<int>();
            var jcn = new List<int>();
            var val = new List<double>();
            for (int col = 0; col < n; col++)
                for (int row = col; row < n; row++)
                    if (row == col || k[row, col] != 0.0)
                    {
                        irn.Add(row); jcn.Add(col); val.Add(k[row, col]);
                    }

            // rhs = K * xTrue for a random xTrue.
            var xTrue = new double[n];
            for (int i = 0; i < n; i++) xTrue[i] = rng.NextDouble() * 2.0 - 1.0;
            var rhs = new double[n];
            for (int i = 0; i < n; i++)
            {
                double s = 0.0;
                for (int j = 0; j < n; j++) s += k[i, j] * xTrue[j];
                rhs[i] = s;
            }

            return new KktRecord
            {
                N = n,
                Irn = irn.ToArray(),
                Jcn = jcn.ToArray(),
                Values = val.ToArray(),
                Nrhs = 1,
                Rhs = rhs,
                RequestedNegEVals = q,
                NativeNegEVals = q,
                CheckNegEVals = true
            };
        }
    }
}
