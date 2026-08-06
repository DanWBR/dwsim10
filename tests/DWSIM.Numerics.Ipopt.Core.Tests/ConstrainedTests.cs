using System;
using DWSIM.Numerics.Ipopt.Core;
using Xunit;

namespace DWSIM.Numerics.Ipopt.Core.Tests
{
    /// <summary>
    /// The constrained path, on problems whose answer is known independently.
    /// </summary>
    public class ConstrainedTests
    {
        private sealed class Problem : INlpConstrained
        {
            public int N { get; set; }
            public int M { get; set; }
            public double[] Xl = Array.Empty<double>();
            public double[] Xu = Array.Empty<double>();
            public double[] Cl = Array.Empty<double>();
            public double[] Cu = Array.Empty<double>();
            public double[] X0 = Array.Empty<double>();
            public Func<double[], double> F = _ => 0.0;
            public Func<double[], double[]> GradF = _ => Array.Empty<double>();
            public Func<double[], double[]> G = _ => Array.Empty<double>();
            public Func<double[], double[]> JacG = _ => Array.Empty<double>();

            public void GetBounds(double[] xl, double[] xu)
            {
                Array.Copy(Xl, xl, N);
                Array.Copy(Xu, xu, N);
            }

            public void GetConstraintBounds(double[] cl, double[] cu)
            {
                Array.Copy(Cl, cl, M);
                Array.Copy(Cu, cu, M);
            }

            public void GetStartingPoint(double[] x) => Array.Copy(X0, x, N);

            public double EvalF(double[] x) => F(x);

            public void EvalGradF(double[] x, double[] gradf) => Array.Copy(GradF(x), gradf, N);

            public void EvalG(double[] x, double[] g) => Array.Copy(G(x), g, M);

            public void EvalJacG(double[] x, double[] jac) => Array.Copy(JacG(x), jac, M * N);
        }

        private const double Inf = 1e19;

        [Fact]
        public void MinimisesASumOfSquaresOnALine()
        {
            // min x1^2 + x2^2 subject to x1 + x2 = 1. The answer is (0.5, 0.5), f = 0.5.
            var p = new Problem
            {
                N = 2,
                M = 1,
                Xl = new[] { -Inf, -Inf },
                Xu = new[] { Inf, Inf },
                Cl = new[] { 1.0 },
                Cu = new[] { 1.0 },
                X0 = new[] { 3.0, -2.0 },
                F = x => x[0] * x[0] + x[1] * x[1],
                GradF = x => new[] { 2.0 * x[0], 2.0 * x[1] },
                G = x => new[] { x[0] + x[1] },
                JacG = x => new[] { 1.0, 1.0 }
            };

            var result = new ConstrainedInteriorPointSolver(
                new SolverOptions { Tolerance = 1e-10, MaxIterations = 200 }).Solve(p);

            Assert.Equal(SolveStatus.Solved, result.Status);
            Assert.Equal(0.5, result.X[0], 7);
            Assert.Equal(0.5, result.X[1], 7);
            Assert.Equal(0.5, result.ObjValue, 7);
        }

        [Fact]
        public void SolvesHockSchittkowski71()
        {
            // The problem Ipopt ships as its own tutorial:
            //   min  x1*x4*(x1+x2+x3) + x3
            //   s.t. x1*x2*x3*x4 >= 25
            //        x1^2 + x2^2 + x3^2 + x4^2 = 40
            //        1 <= xi <= 5,  x0 = (1, 5, 5, 1)
            // with the published answer f* = 17.0140173 at
            //   x* = (1, 4.74299963, 3.82114998, 1.37940829).
            // One inequality and one equality, so both branches of the slack treatment run.
            var p = new Problem
            {
                N = 4,
                M = 2,
                Xl = new[] { 1.0, 1.0, 1.0, 1.0 },
                Xu = new[] { 5.0, 5.0, 5.0, 5.0 },
                Cl = new[] { 25.0, 40.0 },
                Cu = new[] { Inf, 40.0 },
                X0 = new[] { 1.0, 5.0, 5.0, 1.0 },
                F = x => x[0] * x[3] * (x[0] + x[1] + x[2]) + x[2],
                GradF = x => new[]
                {
                    x[3] * (2.0 * x[0] + x[1] + x[2]),
                    x[0] * x[3],
                    x[0] * x[3] + 1.0,
                    x[0] * (x[0] + x[1] + x[2])
                },
                G = x => new[]
                {
                    x[0] * x[1] * x[2] * x[3],
                    x[0] * x[0] + x[1] * x[1] + x[2] * x[2] + x[3] * x[3]
                },
                JacG = x => new[]
                {
                    x[1] * x[2] * x[3], x[0] * x[2] * x[3], x[0] * x[1] * x[3], x[0] * x[1] * x[2],
                    2.0 * x[0], 2.0 * x[1], 2.0 * x[2], 2.0 * x[3]
                }
            };

            var result = new ConstrainedInteriorPointSolver(
                new SolverOptions { Tolerance = 1e-8, MaxIterations = 500 }).Solve(p);

            Assert.Equal(SolveStatus.Solved, result.Status);
            Assert.True(result.Iterations < 40, "took " + result.Iterations + " iterations");

            Assert.Equal(17.0140173, result.ObjValue, 5);
            Assert.Equal(1.0, result.X[0], 5);
            Assert.Equal(4.74299963, result.X[1], 4);
            Assert.Equal(3.82114998, result.X[2], 4);
            Assert.Equal(1.37940829, result.X[3], 4);

            // Both constraints hold at the answer.
            var g = new double[2];
            p.EvalG(result.X, g);

            Assert.True(g[0] >= 25.0 - 1e-6, "the product constraint is violated");
            Assert.Equal(40.0, g[1], 6);
        }

        [Fact]
        public void HonoursAnInequalityThatIsNotActive()
        {
            // min (x1-1)^2 + (x2-1)^2 subject to x1 + x2 <= 10: the constraint is slack at the
            // answer, so the slack has to stay interior without dragging the point.
            var p = new Problem
            {
                N = 2,
                M = 1,
                Xl = new[] { -Inf, -Inf },
                Xu = new[] { Inf, Inf },
                Cl = new[] { -Inf },
                Cu = new[] { 10.0 },
                X0 = new[] { 4.0, 4.0 },
                F = x => (x[0] - 1.0) * (x[0] - 1.0) + (x[1] - 1.0) * (x[1] - 1.0),
                GradF = x => new[] { 2.0 * (x[0] - 1.0), 2.0 * (x[1] - 1.0) },
                G = x => new[] { x[0] + x[1] },
                JacG = x => new[] { 1.0, 1.0 }
            };

            var result = new ConstrainedInteriorPointSolver(
                new SolverOptions { Tolerance = 1e-10, MaxIterations = 200 }).Solve(p);

            Assert.Equal(SolveStatus.Solved, result.Status);
            Assert.Equal(1.0, result.X[0], 6);
            Assert.Equal(1.0, result.X[1], 6);
        }

        [Fact]
        public void SolvesTheShapeTheGibbsThreePhaseFlashPoses()
        {
            // n = 2 * nc variables, one per compound per liquid phase, and nc + 1 constraints
            //   g_i = z_i * F - x_i - x_{i+m}  in [0, 1000]
            // whose Jacobian is constant at -1 in two places per row. That is exactly what
            // GibbsMinimization3P builds; the objective here stands in for the Gibbs energy with
            // a strictly convex function whose unconstrained minimum breaks the constraints, so
            // they have to bite.
            const int nc = 4;
            int m = nc + 1;
            int n = 2 * m;

            var feed = new[] { 3.0, 2.0, 4.0, 1.0, 2.5 };
            var target = new[] { 2.0, 2.0, 3.0, 1.0, 2.0, 2.0, 1.0, 2.0, 0.5, 1.0 };

            var jac = new double[m * n];
            for (int r = 0; r < m; r++)
            {
                jac[r * n + r] = -1.0;
                jac[r * n + r + m] = -1.0;
            }

            var p = new Problem
            {
                N = n,
                M = m,
                Xl = new double[n],
                Xu = new double[n],
                Cl = new double[m],
                Cu = new double[m],
                X0 = new double[n],
                F = x =>
                {
                    double s = 0.0;
                    for (int i = 0; i < n; i++) s += (x[i] - target[i]) * (x[i] - target[i]);
                    return s;
                },
                GradF = x =>
                {
                    var gr = new double[n];
                    for (int i = 0; i < n; i++) gr[i] = 2.0 * (x[i] - target[i]);
                    return gr;
                },
                G = x =>
                {
                    var gv = new double[m];
                    for (int r = 0; r < m; r++) gv[r] = feed[r] - x[r] - x[r + m];
                    return gv;
                },
                JacG = _ => jac
            };

            for (int i = 0; i < n; i++)
            {
                p.Xl[i] = 0.0;
                p.Xu[i] = feed[i % m];
                p.X0[i] = 0.25 * feed[i % m];
            }

            for (int r = 0; r < m; r++)
            {
                p.Cl[r] = 0.0;
                p.Cu[r] = 1000.0;
            }

            var result = new ConstrainedInteriorPointSolver(
                new SolverOptions { Tolerance = 1e-8, MaxIterations = 500 }).Solve(p);

            Assert.Equal(SolveStatus.Solved, result.Status);

            // Every phase split is non-negative and the two phases together never exceed the feed.
            for (int i = 0; i < n; i++)
            {
                Assert.True(result.X[i] >= -1e-8, "a phase amount went negative");
            }

            for (int r = 0; r < m; r++)
            {
                Assert.True(result.X[r] + result.X[r + m] <= feed[r] + 1e-6,
                            "the two phases together exceed what was fed of compound " + r);
            }

            // Compound 3 is the one whose two targets, 1.0 and 0.5, cannot both be met: their
            // sum is 1.5 against a feed of 1.0. Minimising the squared distance under an equal
            // penalty splits the shortfall evenly, so the answer is 0.75 and 0.25.
            Assert.Equal(0.75, result.X[3], 5);
            Assert.Equal(0.25, result.X[3 + m], 5);
        }

        [Fact]
        public void RefusesAProblemWithoutConstraints()
        {
            var p = new Problem
            {
                N = 1,
                M = 0,
                Xl = new[] { -Inf },
                Xu = new[] { Inf },
                X0 = new[] { 0.0 },
                F = x => x[0] * x[0],
                GradF = x => new[] { 2.0 * x[0] }
            };

            var result = new ConstrainedInteriorPointSolver().Solve(p);

            Assert.Equal(SolveStatus.InvalidInput, result.Status);
        }
    }
}
