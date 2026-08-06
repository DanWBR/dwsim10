using System;
using Xunit;

namespace DWSIM.Numerics.Ipopt.Sparse.Tests
{
    public class DenseCholeskyTests
    {
        [Fact]
        public void SolvesSpdSystem()
        {
            var a = new double[,]
            {
                { 4, 1, 0 },
                { 1, 3, 1 },
                { 0, 1, 2 },
            };
            var chol = new DenseCholesky();
            Assert.Equal(FactorStatus.Success, chol.Factorize(a));

            var x = new double[] { 1.0, -2.0, 3.0 };
            var b = TestMatrix.MatVec(a, x);
            chol.Solve(b);
            Assert.True(TestMatrix.MaxAbsDiff(b, x) < 1e-12);
        }

        [Fact]
        public void RejectsNonPositiveDefinite()
        {
            // Indefinite (one negative eigenvalue).
            var a = new double[,] { { 1, 2 }, { 2, 1 } };
            var chol = new DenseCholesky();
            Assert.Equal(FactorStatus.NotPositiveDefinite, chol.Factorize(a));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(11)]
        [InlineData(40)]
        public void RandomSpdMatchesBunchKaufmanSolution(int n)
        {
            var rng = new Random(100 + n);
            var a = Spd(rng, n);

            var xTrue = new double[n];
            for (int i = 0; i < n; i++) xTrue[i] = rng.NextDouble() * 2.0 - 1.0;
            var b0 = TestMatrix.MatVec(a, xTrue);

            var chol = new DenseCholesky();
            Assert.Equal(FactorStatus.Success, chol.Factorize(a));
            var bc = (double[])b0.Clone();
            chol.Solve(bc);
            Assert.True(TestMatrix.MaxAbsDiff(bc, xTrue) < 1e-9);

            // SPD => all-positive inertia; solution agrees with Bunch-Kaufman.
            var bk = new BunchKaufman();
            bk.Factorize(a);
            Assert.Equal(n, bk.Inertia.Positive);
            var bb = (double[])b0.Clone();
            bk.Solve(bb);
            Assert.True(TestMatrix.MaxAbsDiff(bc, bb) < 1e-9);
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
