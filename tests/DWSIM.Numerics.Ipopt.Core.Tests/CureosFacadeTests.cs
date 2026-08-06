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
        public void RefusesConstraintsAndSaysHowMany()
        {
            var x = new double[] { 0.0, 0.0 };
            double obj = 0.0;

            using (var problem = new Cureos.Numerics.Ipopt(
                2, new double[] { -10.0, -10.0 }, new double[] { 10.0, 10.0 },
                3, new double[3], new double[3], 6, 0,
                Quadratic, null, QuadraticGradient, null, null))
            {
                var error = Assert.Throws<NotSupportedException>(
                    () => problem.SolveProblem(x, ref obj, new double[3], new double[3], null, null));

                Assert.Contains("3 constraint", error.Message);
            }
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
