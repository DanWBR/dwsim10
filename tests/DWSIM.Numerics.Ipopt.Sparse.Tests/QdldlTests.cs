using System;
using Xunit;

namespace DWSIM.Numerics.Ipopt.Sparse.Tests
{
    public class QdldlTests
    {
        [Fact]
        public void SolvesSmallQuasidefiniteSaddleSystem()
        {
            // K = [ 2  0  1 ;
            //       0  3  1 ;
            //       1  1 -1 ]  -- quasidefinite: [[H, A^T],[A,-D]], H=diag(2,3), D=1.
            var k = new double[,]
            {
                { 2, 0,  1 },
                { 0, 3,  1 },
                { 1, 1, -1 },
            };

            var (ap, ai, ax) = TestMatrix.UpperCsc(k);
            var solver = new QdldlSolver();
            Assert.Equal(FactorStatus.Success, solver.AnalyzeStructure(3, ap, ai));
            Assert.Equal(FactorStatus.Success, solver.Factorize(ax));

            // Inertia must match the theorem: 2 positive, 1 negative.
            Assert.Equal(2, solver.Inertia.Positive);
            Assert.Equal(1, solver.Inertia.Negative);
            Assert.Equal(0, solver.Inertia.Zero);

            // Cross-check inertia against an independent dense eigenvalue oracle.
            var oracle = TestMatrix.InertiaOf(k);
            Assert.Equal(oracle.pos, solver.Inertia.Positive);
            Assert.Equal(oracle.neg, solver.Inertia.Negative);

            // Solve K x = b for a known x.
            var x = new double[] { 1.0, 2.0, 3.0 };
            var b = TestMatrix.MatVec(k, x);
            solver.Solve(b);
            Assert.True(TestMatrix.MaxAbsDiff(b, x) < 1e-12, $"residual too large: {TestMatrix.MaxAbsDiff(b, x)}");
        }

        [Fact]
        public void AllPositiveInertiaForSpd()
        {
            var k = new double[,]
            {
                { 4, 1, 0 },
                { 1, 3, 1 },
                { 0, 1, 2 },
            };
            var (ap, ai, ax) = TestMatrix.UpperCsc(k);
            var solver = new QdldlSolver();
            solver.AnalyzeStructure(3, ap, ai);
            Assert.Equal(FactorStatus.Success, solver.Factorize(ax));
            Assert.Equal(3, solver.Inertia.Positive);
            Assert.Equal(0, solver.Inertia.Negative);
        }

        [Fact]
        public void ReportsZeroPivotOnStructuralSaddleWithZeroBlock()
        {
            // [[0, 1],[1, 0]] -- indefinite but NOT quasidefinite. QDLDL hits the
            // zero leading pivot and must report it (the Ipopt adapter maps this to
            // SYMSOLVER_SINGULAR, which drives the perturbation handler).
            var k = new double[,]
            {
                { 0, 1 },
                { 1, 0 },
            };
            var (ap, ai, ax) = TestMatrix.UpperCsc(k);
            var solver = new QdldlSolver();
            solver.AnalyzeStructure(2, ap, ai);
            Assert.Equal(FactorStatus.ZeroPivot, solver.Factorize(ax));
        }

        [Fact]
        public void RandomQuasidefiniteInertiaAndResidual()
        {
            var rng = new Random(12345);
            const int p = 20; // positive block
            const int q = 8;  // negative block
            int n = p + q;

            // H = M^T M + I  (SPD, p x p)
            var h = SpdMatrix(rng, p);
            // Dneg = N^T N + I  (SPD, q x q); appears as -Dneg in K.
            var dneg = SpdMatrix(rng, q);

            var k = new double[n, n];
            for (int i = 0; i < p; i++)
                for (int j = 0; j < p; j++)
                    k[i, j] = h[i, j];

            for (int i = 0; i < q; i++)
                for (int j = 0; j < q; j++)
                    k[p + i, p + j] = -dneg[i, j];

            // Random coupling A (q x p) placed as A and A^T.
            for (int i = 0; i < q; i++)
                for (int j = 0; j < p; j++)
                {
                    double v = rng.NextDouble() - 0.5;
                    k[p + i, j] = v;
                    k[j, p + i] = v;
                }

            var (ap, ai, ax) = TestMatrix.UpperCsc(k);
            var solver = new QdldlSolver();
            Assert.Equal(FactorStatus.Success, solver.AnalyzeStructure(n, ap, ai));
            Assert.Equal(FactorStatus.Success, solver.Factorize(ax));

            // Quasidefinite => inertia is exactly (p, q).
            Assert.Equal(p, solver.Inertia.Positive);
            Assert.Equal(q, solver.Inertia.Negative);

            var xTrue = new double[n];
            for (int i = 0; i < n; i++) xTrue[i] = rng.NextDouble() * 2.0 - 1.0;
            var b = TestMatrix.MatVec(k, xTrue);
            solver.Solve(b);
            double res = TestMatrix.MaxAbsDiff(b, xTrue);
            Assert.True(res < 1e-9, $"residual too large: {res}");
        }

        [Fact]
        public void RefactorizeWithNewValuesReusesSymbolic()
        {
            var k1 = new double[,] { { 5, 1 }, { 1, 4 } };
            var (ap, ai, ax1) = TestMatrix.UpperCsc(k1);
            var solver = new QdldlSolver();
            solver.AnalyzeStructure(2, ap, ai);
            solver.Factorize(ax1);

            // Same pattern, different values -> factorize again without re-analyzing.
            var k2 = new double[,] { { 10, 2 }, { 2, 9 } };
            var (_, _, ax2) = TestMatrix.UpperCsc(k2);
            Assert.Equal(FactorStatus.Success, solver.Factorize(ax2));

            var x = new double[] { -3.0, 7.0 };
            var b = TestMatrix.MatVec(k2, x);
            solver.Solve(b);
            Assert.True(TestMatrix.MaxAbsDiff(b, x) < 1e-12);
        }

        private static double[,] SpdMatrix(Random rng, int m)
        {
            var mtx = new double[m, m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++)
                    mtx[i, j] = rng.NextDouble() - 0.5;

            var spd = new double[m, m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++)
                {
                    double s = 0.0;
                    for (int t = 0; t < m; t++) s += mtx[t, i] * mtx[t, j];
                    spd[i, j] = s + (i == j ? m : 0.0); // + m*I for a healthy SPD
                }
            return spd;
        }
    }
}
