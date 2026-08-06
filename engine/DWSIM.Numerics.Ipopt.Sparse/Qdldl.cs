// Faithful C# port of QDLDL (https://github.com/osqp/qdldl), the LDL^T
// factorization of a quasi-definite matrix used by the OSQP project.
//
// Original C source:
//   Copyright 2018, Paul Goulart, Bartolomeo Stellato, Goran Banjac, The OSQP developers
//   Licensed under the Apache License, Version 2.0.
//   SPDX-License-Identifier: Apache-2.0
//
// This port keeps the original algorithm and variable roles intact so it can be
// checked line-by-line against qdldl.c. See QdldlSolver for the allocating,
// higher-level wrapper meant to be consumed by the Ipopt linear-solver adapter.

using System;

namespace DWSIM.Numerics.Ipopt.Sparse
{
    /// <summary>
    /// Low-level, allocation-free LDL^T factorization routines ported from QDLDL.
    /// The matrix must be supplied in compressed-sparse-column (CSC) form holding
    /// the <b>upper triangle</b> (including the diagonal) of a symmetric matrix,
    /// with the diagonal present in every column. All buffers are caller-owned.
    /// </summary>
    public static class Qdldl
    {
        private const int Unknown = -1;
        private const bool Used = true;
        private const bool Unused = false;

        /// <summary>
        /// Computes the elimination tree and the per-column nonzero counts of L.
        /// </summary>
        /// <param name="n">Matrix dimension.</param>
        /// <param name="ap">Column pointers, length n+1.</param>
        /// <param name="ai">Row indices (upper triangle), length ap[n].</param>
        /// <param name="work">Scratch, length n.</param>
        /// <param name="lnz">Output: nonzeros per column of L, length n.</param>
        /// <param name="etree">Output: elimination tree, length n.</param>
        /// <returns>
        /// Total nonzeros in L (space needed for Li/Lx); -1 if the input is not
        /// valid upper-triangular CSC with a full diagonal; -2 on integer overflow.
        /// </returns>
        public static int Etree(int n, int[] ap, int[] ai, int[] work, int[] lnz, int[] etree)
        {
            int i, j, p;

            for (i = 0; i < n; i++)
            {
                work[i] = 0;
                lnz[i] = 0;
                etree[i] = Unknown;

                // Every column must have at least one entry.
                if (ap[i] == ap[i + 1])
                {
                    return -1;
                }
            }

            for (j = 0; j < n; j++)
            {
                work[j] = j;

                for (p = ap[j]; p < ap[j + 1]; p++)
                {
                    i = ai[p];

                    // Reject entries below the diagonal.
                    if (i > j)
                    {
                        return -1;
                    }

                    while (work[i] != j)
                    {
                        if (etree[i] == Unknown)
                        {
                            etree[i] = j;
                        }
                        lnz[i]++;
                        work[i] = j;
                        i = etree[i];
                    }
                }
            }

            long sumLnz = 0;
            for (i = 0; i < n; i++)
            {
                if (sumLnz > int.MaxValue - lnz[i])
                {
                    return -2;
                }
                sumLnz += lnz[i];
            }

            return (int)sumLnz;
        }

        /// <summary>
        /// Numeric LDL^T factorization. Returns the number of positive entries of D
        /// (from which the inertia follows: negatives = n - positives when no pivot
        /// is zero), or -1 if a diagonal pivot evaluates exactly to zero.
        /// </summary>
        /// <param name="n">Matrix dimension.</param>
        /// <param name="ap">Column pointers, length n+1.</param>
        /// <param name="ai">Row indices (upper triangle), length ap[n].</param>
        /// <param name="ax">Values matching <paramref name="ai"/>, length ap[n].</param>
        /// <param name="lp">Output: L column pointers, length n+1.</param>
        /// <param name="li">Output: L row indices, length sumLnz.</param>
        /// <param name="lx">Output: L values, length sumLnz.</param>
        /// <param name="d">Output: diagonal D, length n.</param>
        /// <param name="dinv">Output: reciprocal of D, length n.</param>
        /// <param name="lnz">Per-column nonzero counts from <see cref="Etree"/>, length n.</param>
        /// <param name="etree">Elimination tree from <see cref="Etree"/>, length n.</param>
        /// <param name="boolWork">Scratch, length n.</param>
        /// <param name="intWork">Scratch, length 3*n.</param>
        /// <param name="floatWork">Scratch, length n.</param>
        public static int Factor(
            int n, int[] ap, int[] ai, double[] ax,
            int[] lp, int[] li, double[] lx, double[] d, double[] dinv,
            int[] lnz, int[] etree,
            bool[] boolWork, int[] intWork, double[] floatWork)
        {
            int i, j, k, nnzY, bidx, cidx, nextIdx, nnzE, tmpIdx;
            double yValsCidx;
            int positiveValuesInD = 0;

            // Partition the shared integer scratch, mirroring the C pointer arithmetic.
            bool[] yMarkers = boolWork;
            int[] yIdx = intWork;         // uses [0, n)
            int elimBase = n;             // elimBuffer      -> intWork[n .. 2n)
            int nextBase = 2 * n;         // LNextSpaceInCol -> intWork[2n .. 3n)
            double[] yVals = floatWork;

            lp[0] = 0;

            for (i = 0; i < n; i++)
            {
                lp[i + 1] = lp[i] + lnz[i];
                yMarkers[i] = Unused;
                yVals[i] = 0.0;
                d[i] = 0.0;
                intWork[nextBase + i] = lp[i];
            }

            d[0] = ax[0];
            if (d[0] == 0.0)
            {
                return -1;
            }
            if (d[0] > 0.0)
            {
                positiveValuesInD++;
            }
            dinv[0] = 1.0 / d[0];

            for (k = 1; k < n; k++)
            {
                nnzY = 0;
                tmpIdx = ap[k + 1];

                for (i = ap[k]; i < tmpIdx; i++)
                {
                    bidx = ai[i];

                    if (bidx == k)
                    {
                        d[k] = ax[i];
                        continue;
                    }

                    yVals[bidx] = ax[i];

                    nextIdx = bidx;

                    if (yMarkers[nextIdx] == Unused)
                    {
                        yMarkers[nextIdx] = Used;
                        intWork[elimBase + 0] = nextIdx;
                        nnzE = 1;

                        nextIdx = etree[bidx];

                        while (nextIdx != Unknown && nextIdx < k)
                        {
                            if (yMarkers[nextIdx] == Used)
                            {
                                break;
                            }

                            yMarkers[nextIdx] = Used;
                            intWork[elimBase + nnzE] = nextIdx;
                            nnzE++;
                            nextIdx = etree[nextIdx];
                        }

                        while (nnzE > 0)
                        {
                            yIdx[nnzY++] = intWork[elimBase + (--nnzE)];
                        }
                    }
                }

                for (i = nnzY - 1; i >= 0; i--)
                {
                    cidx = yIdx[i];

                    tmpIdx = intWork[nextBase + cidx];
                    yValsCidx = yVals[cidx];

                    for (j = lp[cidx]; j < tmpIdx; j++)
                    {
                        yVals[li[j]] -= lx[j] * yValsCidx;
                    }

                    li[tmpIdx] = k;
                    lx[tmpIdx] = yValsCidx * dinv[cidx];

                    d[k] -= yValsCidx * lx[tmpIdx];
                    intWork[nextBase + cidx]++;

                    yVals[cidx] = 0.0;
                    yMarkers[cidx] = Unused;
                }

                if (d[k] == 0.0)
                {
                    return -1;
                }
                if (d[k] > 0.0)
                {
                    positiveValuesInD++;
                }
                dinv[k] = 1.0 / d[k];
            }

            return positiveValuesInD;
        }

        /// <summary>Solves (L+I) x = b in place.</summary>
        public static void Lsolve(int n, int[] lp, int[] li, double[] lx, double[] x)
        {
            for (int i = 0; i < n; i++)
            {
                double val = x[i];
                for (int j = lp[i]; j < lp[i + 1]; j++)
                {
                    x[li[j]] -= lx[j] * val;
                }
            }
        }

        /// <summary>Solves (L+I)^T x = b in place.</summary>
        public static void Ltsolve(int n, int[] lp, int[] li, double[] lx, double[] x)
        {
            for (int i = n - 1; i >= 0; i--)
            {
                double val = x[i];
                for (int j = lp[i]; j < lp[i + 1]; j++)
                {
                    val -= lx[j] * x[li[j]];
                }
                x[i] = val;
            }
        }

        /// <summary>Solves A x = b in place given the LDL^T factors.</summary>
        public static void Solve(int n, int[] lp, int[] li, double[] lx, double[] dinv, double[] x)
        {
            Lsolve(n, lp, li, lx, x);
            for (int i = 0; i < n; i++)
            {
                x[i] *= dinv[i];
            }
            Ltsolve(n, lp, li, lx, x);
        }
    }
}
