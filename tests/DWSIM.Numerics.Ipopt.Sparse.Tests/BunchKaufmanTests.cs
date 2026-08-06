using System;
using Xunit;

namespace DWSIM.Numerics.Ipopt.Sparse.Tests
{
    public class BunchKaufmanTests
    {
        [Fact]
        public void HandlesZeroDiagonalSaddleThatQdldlCannot()
        {
            // [[0, 1],[1, 0]] : indefinite, NOT quasidefinite. QDLDL fails here;
            // Bunch-Kaufman uses a 2x2 pivot and returns inertia (1, 1).
            var k = new double[,] { { 0, 1 }, { 1, 0 } };
            var bk = new BunchKaufman();
            Assert.Equal(FactorStatus.Success, bk.Factorize(k));
            Assert.Equal(1, bk.Inertia.Positive);
            Assert.Equal(1, bk.Inertia.Negative);

            var x = new double[] { 3.0, -5.0 };
            var b = TestMatrix.MatVec(k, x);
            bk.Solve(b);
            Assert.True(TestMatrix.MaxAbsDiff(b, x) < 1e-12);
        }

        [Fact]
        public void MatchesInertiaAndSolvesQuasidefiniteSaddle()
        {
            var k = new double[,]
            {
                { 2, 0,  1 },
                { 0, 3,  1 },
                { 1, 1, -1 },
            };
            var bk = new BunchKaufman();
            Assert.Equal(FactorStatus.Success, bk.Factorize(k));

            var oracle = TestMatrix.InertiaOf(k);
            Assert.Equal(oracle.pos, bk.Inertia.Positive);
            Assert.Equal(oracle.neg, bk.Inertia.Negative);

            var x = new double[] { 1.0, 2.0, 3.0 };
            var b = TestMatrix.MatVec(k, x);
            bk.Solve(b);
            Assert.True(TestMatrix.MaxAbsDiff(b, x) < 1e-12);
        }

        [Fact]
        public void AllPositiveForSpd()
        {
            var k = new double[,]
            {
                { 4, 1, 0 },
                { 1, 3, 1 },
                { 0, 1, 2 },
            };
            var bk = new BunchKaufman();
            bk.Factorize(k);
            Assert.Equal(3, bk.Inertia.Positive);
            Assert.Equal(0, bk.Inertia.Negative);
        }

        [Fact]
        public void ForcesTwoByTwoPivotWithTinyDiagonal()
        {
            // Near-zero diagonal entries force 2x2 pivots on the (1,2) block.
            var k = new double[,]
            {
                { 5.0,   0.0,   0.0 },
                { 0.0,   1e-14, 2.0 },
                { 0.0,   2.0,   1e-14 },
            };
            var bk = new BunchKaufman();
            Assert.Equal(FactorStatus.Success, bk.Factorize(k));

            var oracle = TestMatrix.InertiaOf(k);
            Assert.Equal(oracle.pos, bk.Inertia.Positive);
            Assert.Equal(oracle.neg, bk.Inertia.Negative);

            var x = new double[] { -1.0, 2.5, 4.0 };
            var b = TestMatrix.MatVec(k, x);
            bk.Solve(b);
            Assert.True(TestMatrix.MaxAbsDiff(b, x) < 1e-9);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(7)]
        [InlineData(23)]
        public void RandomSymmetricIndefinite_InertiaAndResidual(int seed)
        {
            var rng = new Random(seed);
            for (int trial = 0; trial < 40; trial++)
            {
                int n = 1 + rng.Next(30);
                var k = RandomSymmetric(rng, n);

                var bk = new BunchKaufman();
                var status = bk.Factorize(k);
                Assert.Equal(FactorStatus.Success, status);

                // Inertia must match the independent dense eigenvalue oracle.
                var oracle = TestMatrix.InertiaOf(k, tol: 1e-8);
                Assert.Equal(oracle.pos, bk.Inertia.Positive);
                Assert.Equal(oracle.neg, bk.Inertia.Negative);
                Assert.Equal(n, bk.Inertia.Positive + bk.Inertia.Negative + bk.Inertia.Zero);

                // Solve accuracy.
                var xTrue = new double[n];
                for (int i = 0; i < n; i++) xTrue[i] = rng.NextDouble() * 4.0 - 2.0;
                var b = TestMatrix.MatVec(k, xTrue);
                bk.Solve(b);
                double res = TestMatrix.MaxAbsDiff(b, xTrue);
                Assert.True(res < 1e-6, $"n={n} trial={trial} residual={res}");
            }
        }

        [Fact]
        public void AgreesWithQdldlOnRandomQuasidefinite()
        {
            var rng = new Random(999);
            const int p = 15, q = 6;
            int n = p + q;

            var k = new double[n, n];
            var h = Spd(rng, p);
            var dn = Spd(rng, q);
            for (int i = 0; i < p; i++)
                for (int j = 0; j < p; j++) k[i, j] = h[i, j];
            for (int i = 0; i < q; i++)
                for (int j = 0; j < q; j++) k[p + i, p + j] = -dn[i, j];
            for (int i = 0; i < q; i++)
                for (int j = 0; j < p; j++)
                {
                    double v = rng.NextDouble() - 0.5;
                    k[p + i, j] = v; k[j, p + i] = v;
                }

            var bk = new BunchKaufman();
            bk.Factorize(k);

            var (ap, ai, ax) = TestMatrix.UpperCsc(k);
            var q2 = new QdldlSolver();
            q2.AnalyzeStructure(n, ap, ai);
            q2.Factorize(ax);

            Assert.Equal(q2.Inertia.Positive, bk.Inertia.Positive);
            Assert.Equal(q2.Inertia.Negative, bk.Inertia.Negative);

            var xTrue = new double[n];
            for (int i = 0; i < n; i++) xTrue[i] = rng.NextDouble() - 0.5;
            var b1 = TestMatrix.MatVec(k, xTrue);
            var b2 = (double[])b1.Clone();
            bk.Solve(b1);
            q2.Solve(b2);
            Assert.True(TestMatrix.MaxAbsDiff(b1, xTrue) < 1e-9);
            Assert.True(TestMatrix.MaxAbsDiff(b2, xTrue) < 1e-9);
            Assert.True(TestMatrix.MaxAbsDiff(b1, b2) < 1e-9);
        }

        private static double[,] RandomSymmetric(Random rng, int n)
        {
            var k = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j <= i; j++)
                {
                    double v = rng.NextDouble() * 2.0 - 1.0;
                    k[i, j] = v;
                    k[j, i] = v;
                }
            return k;
        }

        private static double[,] Spd(Random rng, int m)
        {
            var mtx = new double[m, m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++) mtx[i, j] = rng.NextDouble() - 0.5;
            var spd = new double[m, m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++)
                {
                    double s = 0.0;
                    for (int t = 0; t < m; t++) s += mtx[t, i] * mtx[t, j];
                    spd[i, j] = s + (i == j ? m : 0.0);
                }
            return spd;
        }
    }
}
