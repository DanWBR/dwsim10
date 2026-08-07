using System;
using Cureos.Numerics;
using Xunit;

namespace DWSIM.Numerics.Ipopt.Core.Tests
{
    /// <summary>
    /// The Cureos.Numerics shape over the managed solver. These exercise it the way the engine
    /// does: bounds only, null arrays for g and the multipliers, an intermediate callback, and
    /// the five options DWSIM sets.
    /// </summary>
    public class CureosFacadeTests
    {
        // A separable quadratic with its minimum at (3, -2) and value -13.
        private static bool Quadratic(int n, double[] x, bool newX, ref double obj)
        {
            obj = (x[0] - 3.0) * (x[0] - 3.0) + (x[1] + 2.0) * (x[1] + 2.0) - 13.0;
            return true;
        }

        private static bool QuadraticGradient(int n, double[] x, bool newX, ref double[] grad)
        {
            grad[0] = 2.0 * (x[0] - 3.0);
            grad[1] = 2.0 * (x[1] + 2.0);
            return true;
        }

        private static Cureos.Numerics.Ipopt Unconstrained(
            double[] xL, double[] xU,
            EvaluateObjectiveDelegate f = null,
            EvaluateObjectiveGradientDelegate g = null)
        {
            return new Cureos.Numerics.Ipopt(
                2, xL, xU, 0, null, null, 0, 0,
                f ?? Quadratic, null, g ?? QuadraticGradient, null, null);
        }

        [Fact]
        public void SolvesABoundConstrainedProblemAndFillsTheCallersArray()
        {
            var x = new double[] { 0.0, 0.0 };
            double obj = 0.0;

            using (var problem = Unconstrained(
                new double[] { -10.0, -10.0 }, new double[] { 10.0, 10.0 }))
            {
                problem.AddOption("tol", 1e-8);
                problem.AddOption("print_level", 0);
                problem.AddOption("max_iter", 200);
                problem.AddOption("mu_strategy", "adaptive");
                problem.AddOption("hessian_approximation", "limited-memory");

                // Exactly what the engine passes: no g, no multipliers.
                var status = problem.SolveProblem(x, ref obj, null, null, null, null);

                Assert.Equal(IpoptReturnCode.Solve_Succeeded, status);
            }

            Assert.Equal(3.0, x[0], 6);
            Assert.Equal(-2.0, x[1], 6);
            Assert.Equal(-13.0, obj, 6);
        }

        [Fact]
        public void RespectsTheBounds()
        {
            var x = new double[] { 0.0, 0.0 };
            double obj = 0.0;

            using (var problem = Unconstrained(
                new double[] { -10.0, -10.0 }, new double[] { 1.0, 10.0 }))
            {
                problem.AddOption("tol", 1e-8);
                problem.SolveProblem(x, ref obj, null, null, null, null);
            }

            Assert.InRange(x[0], 0.99, 1.0);
            Assert.Equal(-2.0, x[1], 5);
        }

        [Fact]
        public void FallsBackToCentralDifferencesWithoutAGradient()
        {
            var x = new double[] { 0.0, 0.0 };
            double obj = 0.0;

            // Built by hand rather than through the helper, which substitutes the analytic
            // gradient, so that eval_grad_f is genuinely unset.
            using (var problem = new Cureos.Numerics.Ipopt(
                2, new double[] { -10.0, -10.0 }, new double[] { 10.0, 10.0 },
                0, null, null, 0, 0, Quadratic, null, null, null, null))
            {
                problem.AddOption("tol", 1e-8);
                var status = problem.SolveProblem(x, ref obj, null, null, null, null);

                Assert.Equal(IpoptReturnCode.Solve_Succeeded, status);
            }

            Assert.Equal(3.0, x[0], 4);
            Assert.Equal(-2.0, x[1], 4);
        }

        [Fact]
        public void SolvesAConstrainedProblemThroughTheFacade()
        {
            // Hock-Schittkowski 71, the problem Ipopt ships as its tutorial, posed the way the
            // Gibbs three-phase flash poses its own: constraint bounds, a triplet Jacobian asked
            // for its structure first and its values after.
            var x = new double[] { 1.0, 5.0, 5.0, 1.0 };
            double obj = 0.0;
            var g = new double[2];

            EvaluateObjectiveDelegate f = (int n, double[] xx, bool newX, ref double value) =>
            {
                value = xx[0] * xx[3] * (xx[0] + xx[1] + xx[2]) + xx[2];
                return true;
            };

            EvaluateObjectiveGradientDelegate gradF = (int n, double[] xx, bool newX, ref double[] grad) =>
            {
                grad[0] = xx[3] * (2.0 * xx[0] + xx[1] + xx[2]);
                grad[1] = xx[0] * xx[3];
                grad[2] = xx[0] * xx[3] + 1.0;
                grad[3] = xx[0] * (xx[0] + xx[1] + xx[2]);
                return true;
            };

            EvaluateConstraintsDelegate evalG = (int n, double[] xx, bool newX, int m, ref double[] gg) =>
            {
                gg[0] = xx[0] * xx[1] * xx[2] * xx[3];
                gg[1] = xx[0] * xx[0] + xx[1] * xx[1] + xx[2] * xx[2] + xx[3] * xx[3];
                return true;
            };

            EvaluateJacobianDelegate jacG = (int n, double[] xx, bool newX, int m, int nele,
                                             ref int[] iRow, ref int[] jCol, ref double[] values) =>
            {
                if (values == null)
                {
                    var rows = new int[nele];
                    var cols = new int[nele];

                    for (int r = 0; r < 2; r++)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            rows[r * 4 + c] = r;
                            cols[r * 4 + c] = c;
                        }
                    }

                    iRow = rows;
                    jCol = cols;
                }
                else
                {
                    values[0] = xx[1] * xx[2] * xx[3];
                    values[1] = xx[0] * xx[2] * xx[3];
                    values[2] = xx[0] * xx[1] * xx[3];
                    values[3] = xx[0] * xx[1] * xx[2];
                    values[4] = 2.0 * xx[0];
                    values[5] = 2.0 * xx[1];
                    values[6] = 2.0 * xx[2];
                    values[7] = 2.0 * xx[3];
                }

                return true;
            };

            using (var problem = new Cureos.Numerics.Ipopt(
                4, new[] { 1.0, 1.0, 1.0, 1.0 }, new[] { 5.0, 5.0, 5.0, 5.0 },
                2, new[] { 25.0, 40.0 }, new[] { 2e19, 40.0 },
                8, 0, f, evalG, gradF, jacG, null))
            {
                problem.AddOption("tol", 1e-8);
                problem.AddOption("max_iter", 500);
                problem.AddOption("mu_strategy", "adaptive");
                problem.AddOption("hessian_approximation", "limited-memory");

                var status = problem.SolveProblem(x, ref obj, g, null, null, null);

                Assert.Equal(IpoptReturnCode.Solve_Succeeded, status);
            }

            Assert.Equal(17.0140173, obj, 5);
            Assert.Equal(1.0, x[0], 5);
            Assert.Equal(4.74299963, x[1], 4);

            // The caller asked for the constraint values, so they came back.
            Assert.True(g[0] >= 25.0 - 1e-6);
            Assert.Equal(40.0, g[1], 6);
        }

        [Fact]
        public void AcceptsAnExactHessianDeclaredTheWayTheEngineDeclaresOne()
        {
            // Every eval_h in the engine is written the same way: nele_hess is passed as zero and
            // the callback replaces the value array with a full n by n block instead of filling
            // the triplets it was asked for. The native library never calls such a callback, so
            // nothing caught the shape; hessian_approximation=exact does call it, and has to
            // recognise what comes back.
            var x = new double[] { 1.0, 5.0, 5.0, 1.0 };
            double obj = 0.0;
            var g = new double[2];
            int hessianCalls = 0;

            EvaluateObjectiveDelegate f = (int n, double[] xx, bool newX, ref double value) =>
            {
                value = xx[0] * xx[3] * (xx[0] + xx[1] + xx[2]) + xx[2];
                return true;
            };

            EvaluateObjectiveGradientDelegate gradF = (int n, double[] xx, bool newX, ref double[] grad) =>
            {
                grad[0] = xx[3] * (2.0 * xx[0] + xx[1] + xx[2]);
                grad[1] = xx[0] * xx[3];
                grad[2] = xx[0] * xx[3] + 1.0;
                grad[3] = xx[0] * (xx[0] + xx[1] + xx[2]);
                return true;
            };

            EvaluateConstraintsDelegate evalG = (int n, double[] xx, bool newX, int m, ref double[] gg) =>
            {
                gg[0] = xx[0] * xx[1] * xx[2] * xx[3];
                gg[1] = xx[0] * xx[0] + xx[1] * xx[1] + xx[2] * xx[2] + xx[3] * xx[3];
                return true;
            };

            EvaluateJacobianDelegate jacG = (int n, double[] xx, bool newX, int m, int nele,
                                             ref int[] iRow, ref int[] jCol, ref double[] values) =>
            {
                if (values == null)
                {
                    var rows = new int[nele];
                    var cols = new int[nele];

                    for (int r = 0; r < 2; r++)
                        for (int c = 0; c < 4; c++)
                        {
                            rows[r * 4 + c] = r;
                            cols[r * 4 + c] = c;
                        }

                    iRow = rows;
                    jCol = cols;
                }
                else
                {
                    values[0] = xx[1] * xx[2] * xx[3];
                    values[1] = xx[0] * xx[2] * xx[3];
                    values[2] = xx[0] * xx[1] * xx[3];
                    values[3] = xx[0] * xx[1] * xx[2];
                    values[4] = 2.0 * xx[0];
                    values[5] = 2.0 * xx[1];
                    values[6] = 2.0 * xx[2];
                    values[7] = 2.0 * xx[3];
                }

                return true;
            };

            EvaluateHessianDelegate evalH = (int n, double[] xx, bool newX, double sigma, int m,
                                             double[] lambda, bool newLambda, int nele,
                                             ref int[] iRow, ref int[] jCol, ref double[] values) =>
            {
                if (values == null) return true;

                hessianCalls++;

                var h = new double[16];

                void Put(int i, int j, double v) { h[i * 4 + j] += v; if (i != j) h[j * 4 + i] += v; }

                Put(0, 0, sigma * 2.0 * xx[3]);
                Put(0, 1, sigma * xx[3]);
                Put(0, 2, sigma * xx[3]);
                Put(0, 3, sigma * (2.0 * xx[0] + xx[1] + xx[2]));
                Put(1, 3, sigma * xx[0]);
                Put(2, 3, sigma * xx[0]);

                Put(0, 1, lambda[0] * xx[2] * xx[3]);
                Put(0, 2, lambda[0] * xx[1] * xx[3]);
                Put(0, 3, lambda[0] * xx[1] * xx[2]);
                Put(1, 2, lambda[0] * xx[0] * xx[3]);
                Put(1, 3, lambda[0] * xx[0] * xx[2]);
                Put(2, 3, lambda[0] * xx[0] * xx[1]);

                for (int i = 0; i < 4; i++) Put(i, i, lambda[1] * 2.0);

                values = h;
                return true;
            };

            using (var problem = new Cureos.Numerics.Ipopt(
                4, new[] { 1.0, 1.0, 1.0, 1.0 }, new[] { 5.0, 5.0, 5.0, 5.0 },
                2, new[] { 25.0, 40.0 }, new[] { 2e19, 40.0 },
                8, 0, f, evalG, gradF, jacG, evalH))
            {
                problem.AddOption("tol", 1e-8);
                problem.AddOption("max_iter", 500);
                problem.AddOption("mu_strategy", "adaptive");
                problem.AddOption("hessian_approximation", "exact");

                var status = problem.SolveProblem(x, ref obj, g, null, null, null);

                Assert.Equal(IpoptReturnCode.Solve_Succeeded, status);
            }

            Assert.True(hessianCalls > 0, "the option was accepted but eval_h was never called");

            Assert.Equal(17.0140173, obj, 5);
            Assert.Equal(1.0, x[0], 5);
            Assert.Equal(4.74299963, x[1], 4);
        }

        [Fact]
        public void AnIntermediateCallbackThatReturnsFalseStopsTheSolve()
        {
            var x = new double[] { 0.0, 0.0 };
            double obj = 0.0;
            int seen = 0;

            using (var problem = Unconstrained(
                new double[] { -10.0, -10.0 }, new double[] { 10.0, 10.0 }))
            {
                problem.SetIntermediateCallback(
                    (mode, iter, objValue, infPr, infDu, mu, dNorm, reg, alphaDu, alphaPr, ls) =>
                    {
                        seen++;
                        return seen < 2;
                    });

                var status = problem.SolveProblem(x, ref obj, null, null, null, null);

                Assert.Equal(IpoptReturnCode.User_Requested_Stop, status);
            }

            Assert.Equal(2, seen);
        }

        [Fact]
        public void RunningOutOfIterationsIsReportedAsSuch()
        {
            var x = new double[] { 0.0, 0.0 };
            double obj = 0.0;

            using (var problem = Unconstrained(
                new double[] { -10.0, -10.0 }, new double[] { 10.0, 10.0 }))
            {
                problem.AddOption("tol", 1e-14);
                problem.AddOption("max_iter", 1);

                var status = problem.SolveProblem(x, ref obj, null, null, null, null);

                Assert.Equal(IpoptReturnCode.Maximum_Iterations_Exceeded, status);
            }
        }

        [Fact]
        public void AnObjectiveThatReportsFailureComesBackAsAnInvalidNumber()
        {
            var x = new double[] { 0.0, 0.0 };
            double obj = 0.0;

            EvaluateObjectiveDelegate refuses = (int n, double[] xx, bool newX, ref double value) =>
            {
                value = 0.0;
                return false;
            };

            using (var problem = Unconstrained(
                new double[] { -10.0, -10.0 }, new double[] { 10.0, 10.0 }, f: refuses))
            {
                var status = problem.SolveProblem(x, ref obj, null, null, null, null);

                Assert.Equal(IpoptReturnCode.Invalid_Number_Detected, status);
            }
        }

        [Fact]
        public void NullBoundsMeanUnbounded()
        {
            var x = new double[] { 0.0, 0.0 };
            double obj = 0.0;

            using (var problem = Unconstrained(null, null))
            {
                problem.AddOption("tol", 1e-8);
                var status = problem.SolveProblem(x, ref obj, null, null, null, null);

                Assert.Equal(IpoptReturnCode.Solve_Succeeded, status);
            }

            Assert.Equal(3.0, x[0], 6);
            Assert.Equal(-2.0, x[1], 6);
        }
    }
}
