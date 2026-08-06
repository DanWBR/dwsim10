// Offline validation: replay captured KKT systems through the managed solver and
// report (1) whether its inertia matches what the native solver reported, and
// (2) the size distribution -- which decides dense-vs-sparse for the real port.

using System;
using System.Collections.Generic;

namespace DWSIM.Numerics.Ipopt.Sparse
{
    /// <summary>Options controlling a replay run.</summary>
    public sealed class ReplayOptions
    {
        public LinearSolverKind Kind = LinearSolverKind.Auto;
        public int DenseThreshold = 1000;
        public double GrowthLimit = 1e10;
    }

    /// <summary>Aggregated result of replaying a set of KKT records.</summary>
    public sealed class ReplayReport
    {
        public int Records;
        public int Comparable;          // records with a known native inertia
        public int InertiaAgreements;   // managed neg == native neg
        public int ManagedSingular;     // managed reported Singular
        public int ManagedWrong;        // comparable, not singular, but neg mismatched native
        public int MinN = int.MaxValue;
        public int MaxN;
        public long TotalNnz;
        public double MaxSolveResidual;
        public int WithinDenseThreshold;
        public readonly int[] SizeBuckets = new int[6]; // <=100,<=500,<=1000,<=2000,<=5000,>5000

        public double AgreementRate => Comparable == 0 ? double.NaN : (double)InertiaAgreements / Comparable;
        public double DenseFraction => Records == 0 ? double.NaN : (double)WithinDenseThreshold / Records;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"records                : {Records}");
            sb.AppendLine($"N range                : [{(Records == 0 ? 0 : MinN)}, {MaxN}]");
            sb.AppendLine($"total nnz              : {TotalNnz}");
            sb.AppendLine($"inertia comparable     : {Comparable}");
            sb.AppendLine($"inertia agreements     : {InertiaAgreements}" +
                          (Comparable > 0 ? $"  ({AgreementRate:P2})" : ""));
            sb.AppendLine($"managed singular       : {ManagedSingular}");
            sb.AppendLine($"managed wrong inertia  : {ManagedWrong}");
            sb.AppendLine($"max solve residual     : {MaxSolveResidual:E3}");
            sb.AppendLine($"within dense threshold : {WithinDenseThreshold}" +
                          (Records > 0 ? $"  ({DenseFraction:P2})" : ""));
            sb.AppendLine($"size buckets N<= [100,500,1000,2000,5000,inf] : " +
                          $"[{SizeBuckets[0]},{SizeBuckets[1]},{SizeBuckets[2]},{SizeBuckets[3]},{SizeBuckets[4]},{SizeBuckets[5]}]");
            return sb.ToString();
        }
    }

    /// <summary>Runs the managed solver over captured records and aggregates a report.</summary>
    public static class ReplayEngine
    {
        public static ReplayReport Run(IReadOnlyList<KktRecord> records, ReplayOptions? options = null)
        {
            options ??= new ReplayOptions();
            var report = new ReplayReport { Records = records.Count };

            foreach (var r in records)
            {
                if (r.N < report.MinN) report.MinN = r.N;
                if (r.N > report.MaxN) report.MaxN = r.N;
                report.TotalNnz += r.Nnz;
                if (r.N <= options.DenseThreshold) report.WithinDenseThreshold++;
                Bucket(report.SizeBuckets, r.N);

                var solver = new IpoptLdlSolver
                {
                    Kind = options.Kind,
                    DenseThreshold = options.DenseThreshold,
                    GrowthLimit = options.GrowthLimit
                };

                var init = solver.InitializeStructure(r.N, r.Nnz, r.Irn, r.Jcn);
                if (init != SymSolverStatus.Success)
                {
                    report.ManagedSingular++; // treat structural failure like a factorization failure
                    continue;
                }

                Array.Copy(r.Values, solver.GetValuesArray(), r.Nnz);

                // Factorize only (nrhs = 0), without asking Ipopt's inertia check, so we
                // can compare the managed inertia directly against the native one.
                var status = solver.MultiSolve(newMatrix: true, nrhs: 0, rhs: Array.Empty<double>(),
                                               checkNegEVals: false, numberOfNegEVals: 0);

                bool comparable = r.NativeNegEVals >= 0;
                if (comparable) report.Comparable++;

                if (status == SymSolverStatus.Singular)
                {
                    report.ManagedSingular++;
                }
                else if (comparable)
                {
                    if (solver.NumberOfNegEVals == r.NativeNegEVals) report.InertiaAgreements++;
                    else report.ManagedWrong++;
                }

                // If a right-hand side was captured and the factorization was usable,
                // solve and measure the true residual on the original matrix.
                if (status == SymSolverStatus.Success && r.Nrhs >= 1 && r.Rhs.Length >= r.N)
                {
                    var rhs = new double[r.N];
                    Array.Copy(r.Rhs, 0, rhs, 0, r.N);
                    var b0 = (double[])rhs.Clone();

                    var solveStatus = solver.MultiSolve(false, 1, rhs, false, 0);
                    if (solveStatus == SymSolverStatus.Success)
                    {
                        double res = Residual(r, rhs, b0);
                        if (res > report.MaxSolveResidual) report.MaxSolveResidual = res;
                    }
                }
            }

            if (records.Count == 0) report.MinN = 0;
            return report;
        }

        /// <summary>||A x - b||_inf using the symmetric lower-triangle triplets.</summary>
        private static double Residual(KktRecord r, double[] x, double[] b)
        {
            var y = new double[r.N];
            for (int k = 0; k < r.Nnz; k++)
            {
                int i = r.Irn[k];
                int j = r.Jcn[k];
                double v = r.Values[k];
                y[i] += v * x[j];
                if (i != j) y[j] += v * x[i];
            }
            double m = 0.0;
            for (int i = 0; i < r.N; i++)
            {
                double e = Math.Abs(y[i] - b[i]);
                if (e > m) m = e;
            }
            return m;
        }

        private static void Bucket(int[] buckets, int n)
        {
            if (n <= 100) buckets[0]++;
            else if (n <= 500) buckets[1]++;
            else if (n <= 1000) buckets[2]++;
            else if (n <= 2000) buckets[3]++;
            else if (n <= 5000) buckets[4]++;
            else buckets[5]++;
        }
    }
}
