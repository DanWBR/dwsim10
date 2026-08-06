using System;
using Xunit;

namespace DWSIM.Numerics.Ipopt.Sparse.Tests
{
    public class IpoptLdlSolverTests
    {
        // Lower-triangular triplets for the quasidefinite saddle
        // K = [[2,0,1],[0,3,1],[1,1,-1]], inertia (2,1).
        private static (int n, int[] irn, int[] jcn, double[] val) SaddleTriplets() =>
            (3,
             new[] { 0, 1, 2, 2, 2 },
             new[] { 0, 1, 0, 1, 2 },
             new[] { 2.0, 3.0, 1.0, 1.0, -1.0 });

        private static double[,] SaddleDense() => new double[,]
        {
            { 2, 0,  1 },
            { 0, 3,  1 },
            { 1, 1, -1 },
        };

        [Theory]
        [InlineData(LinearSolverKind.Sparse)]
        [InlineData(LinearSolverKind.Dense)]
        [InlineData(LinearSolverKind.Auto)]
        public void FactorizesAndSolvesFromTriplets(LinearSolverKind kind)
        {
            var (n, irn, jcn, val) = SaddleTriplets();
            var solver = new IpoptLdlSolver { Kind = kind };
            Assert.Equal(SymSolverStatus.Success, solver.InitializeStructure(n, val.Length, irn, jcn));

            var buf = solver.GetValuesArray();
            Array.Copy(val, buf, val.Length);

            var x = new double[] { 1.0, 2.0, 3.0 };
            var rhs = TestMatrix.MatVec(SaddleDense(), x);

            var status = solver.MultiSolve(newMatrix: true, nrhs: 1, rhs: rhs,
                                           checkNegEVals: true, numberOfNegEVals: 1);
            Assert.Equal(SymSolverStatus.Success, status);
            Assert.Equal(2, solver.Inertia.Positive);
            Assert.Equal(1, solver.Inertia.Negative);
            Assert.Equal(1, solver.NumberOfNegEVals);
            Assert.True(TestMatrix.MaxAbsDiff(rhs, x) < 1e-10);
        }

        [Fact]
        public void DetectsWrongInertia()
        {
            var (n, irn, jcn, val) = SaddleTriplets();
            var solver = new IpoptLdlSolver { Kind = LinearSolverKind.Sparse };
            solver.InitializeStructure(n, val.Length, irn, jcn);
            Array.Copy(val, solver.GetValuesArray(), val.Length);

            var rhs = new double[n];
            // Actual negatives = 1; ask for 2 -> WrongInertia (no solve performed).
            var status = solver.MultiSolve(true, 1, rhs, checkNegEVals: true, numberOfNegEVals: 2);
            Assert.Equal(SymSolverStatus.WrongInertia, status);
        }

        [Fact]
        public void SumsDuplicateTriplets()
        {
            // Same matrix, but (2,2) = -1 is split across two triplets (-0.4) + (-0.6).
            int n = 3;
            var irn = new[] { 0, 1, 2, 2, 2, 2 };
            var jcn = new[] { 0, 1, 0, 1, 2, 2 };
            var val = new[] { 2.0, 3.0, 1.0, 1.0, -0.4, -0.6 };

            var solver = new IpoptLdlSolver { Kind = LinearSolverKind.Sparse };
            solver.InitializeStructure(n, val.Length, irn, jcn);
            Array.Copy(val, solver.GetValuesArray(), val.Length);

            var x = new double[] { 1.0, 2.0, 3.0 };
            var rhs = TestMatrix.MatVec(SaddleDense(), x);
            var status = solver.MultiSolve(true, 1, rhs, false, 0);

            Assert.Equal(SymSolverStatus.Success, status);
            Assert.Equal(1, solver.Inertia.Negative);
            Assert.True(TestMatrix.MaxAbsDiff(rhs, x) < 1e-10);
        }

        [Fact]
        public void SolvesMultipleRightHandSides()
        {
            var (n, irn, jcn, val) = SaddleTriplets();
            var solver = new IpoptLdlSolver { Kind = LinearSolverKind.Dense };
            solver.InitializeStructure(n, val.Length, irn, jcn);
            Array.Copy(val, solver.GetValuesArray(), val.Length);

            var k = SaddleDense();
            var x1 = new double[] { 1.0, 2.0, 3.0 };
            var x2 = new double[] { -2.0, 0.5, 4.0 };
            var b1 = TestMatrix.MatVec(k, x1);
            var b2 = TestMatrix.MatVec(k, x2);

            var rhs = new double[2 * n];
            Array.Copy(b1, 0, rhs, 0, n);
            Array.Copy(b2, 0, rhs, n, n);

            Assert.Equal(SymSolverStatus.Success, solver.MultiSolve(true, 2, rhs, false, 0));

            var s1 = new double[n];
            var s2 = new double[n];
            Array.Copy(rhs, 0, s1, 0, n);
            Array.Copy(rhs, n, s2, 0, n);
            Assert.True(TestMatrix.MaxAbsDiff(s1, x1) < 1e-10);
            Assert.True(TestMatrix.MaxAbsDiff(s2, x2) < 1e-10);
        }

        [Fact]
        public void ReportsSingularOnZeroColumn()
        {
            // K = [[0,0],[0,5]] : column 0 is null -> singular in both paths.
            int n = 2;
            var irn = new[] { 0, 1 };
            var jcn = new[] { 0, 1 };
            var val = new[] { 0.0, 5.0 };

            foreach (var kind in new[] { LinearSolverKind.Sparse, LinearSolverKind.Dense })
            {
                var solver = new IpoptLdlSolver { Kind = kind };
                solver.InitializeStructure(n, val.Length, irn, jcn);
                Array.Copy(val, solver.GetValuesArray(), val.Length);
                var rhs = new double[n];
                Assert.Equal(SymSolverStatus.Singular, solver.MultiSolve(true, 1, rhs, false, 0));
            }
        }

        [Fact]
        public void GrowthGuardRejectsTinyPivotFactorization()
        {
            // K = [[1e-14, 1],[1, 0]] : QDLDL factorizes without an exact zero pivot,
            // but L blows up (~1e14). The growth guard must flag it as singular so
            // Ipopt perturbs instead of trusting an inertia read from noise.
            int n = 2;
            var irn = new[] { 0, 1, 1 };
            var jcn = new[] { 0, 0, 1 };
            var val = new[] { 1e-14, 1.0, 0.0 };

            var solver = new IpoptLdlSolver { Kind = LinearSolverKind.Sparse, GrowthLimit = 1e10 };
            solver.InitializeStructure(n, val.Length, irn, jcn);
            Array.Copy(val, solver.GetValuesArray(), val.Length);
            var rhs = new double[n];
            Assert.Equal(SymSolverStatus.Singular, solver.MultiSolve(true, 1, rhs, false, 0));
        }

        [Fact]
        public void MissingDiagonalIsFatal()
        {
            // Column 1 has no diagonal entry.
            int n = 2;
            var irn = new[] { 0, 1 };
            var jcn = new[] { 0, 0 };
            var val = new[] { 2.0, 1.0 };

            var solver = new IpoptLdlSolver { Kind = LinearSolverKind.Sparse };
            Assert.Equal(SymSolverStatus.FatalError, solver.InitializeStructure(n, val.Length, irn, jcn));
        }

        [Fact]
        public void DenseAndSparseAgreeOnRandomQuasidefinite()
        {
            var rng = new Random(2024);
            const int p = 12, q = 5;
            int n = p + q;
            var k = new double[n, n];

            for (int i = 0; i < p; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double v = (i == j) ? p + 1.0 : (rng.NextDouble() - 0.5) * 0.1;
                    k[i, j] = v; k[j, i] = v;
                }
            }
            for (int i = 0; i < q; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double v = (i == j) ? -(q + 1.0) : (rng.NextDouble() - 0.5) * 0.1;
                    k[p + i, p + j] = v; k[p + j, p + i] = v;
                }
            }
            for (int i = 0; i < q; i++)
                for (int j = 0; j < p; j++)
                {
                    double v = rng.NextDouble() - 0.5;
                    k[p + i, j] = v; k[j, p + i] = v;
                }

            var (irn, jcn, val) = LowerTriplets(k);
            var xTrue = new double[n];
            for (int i = 0; i < n; i++) xTrue[i] = rng.NextDouble() - 0.5;
            var b0 = TestMatrix.MatVec(k, xTrue);

            var results = new (int neg, double res)[2];
            var kinds = new[] { LinearSolverKind.Sparse, LinearSolverKind.Dense };
            for (int t = 0; t < 2; t++)
            {
                var solver = new IpoptLdlSolver { Kind = kinds[t] };
                Assert.Equal(SymSolverStatus.Success, solver.InitializeStructure(n, val.Length, irn, jcn));
                Array.Copy(val, solver.GetValuesArray(), val.Length);
                var rhs = (double[])b0.Clone();
                Assert.Equal(SymSolverStatus.Success, solver.MultiSolve(true, 1, rhs, false, 0));
                results[t] = (solver.Inertia.Negative, TestMatrix.MaxAbsDiff(rhs, xTrue));
            }

            Assert.Equal(results[0].neg, results[1].neg);
            Assert.True(results[0].res < 1e-8);
            Assert.True(results[1].res < 1e-8);
        }

        private static (int[] irn, int[] jcn, double[] val) LowerTriplets(double[,] a)
        {
            int n = a.GetLength(0);
            var irn = new System.Collections.Generic.List<int>();
            var jcn = new System.Collections.Generic.List<int>();
            var val = new System.Collections.Generic.List<double>();
            for (int j = 0; j < n; j++)
                for (int i = j; i < n; i++)
                    if (i == j || a[i, j] != 0.0)
                    {
                        irn.Add(i); jcn.Add(j); val.Add(a[i, j]);
                    }
            return (irn.ToArray(), jcn.ToArray(), val.ToArray());
        }
    }
}
