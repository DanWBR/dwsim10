using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using DWSIM.Numerics.Ipopt.Core;

namespace DWSIM.Numerics.Ipopt.Core.Tests
{
    public class DwsimAdapterTests
    {
        private readonly ITestOutputHelper _out;
        public DwsimAdapterTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void SolveWithAnalyticGradientMatchesOptimum()
        {
            // min sum (x_i - 2)^2 over [-10,10]; optimum x = 2.
            Func<double[], double> f = x =>
            {
                double s = 0; foreach (var v in x) { double d = v - 2.0; s += d * d; } return s;
            };
            Func<double[], double[]> g = x =>
            {
                var r = new double[x.Length];
                for (int i = 0; i < x.Length; i++) r[i] = 2.0 * (x[i] - 2.0);
                return r;
            };

            var solver = new DwsimIpoptSolver();
            var vars = new double[] { 0, 0, 0 };
            var lb = new[] { -10.0, -10, -10 };
            var ub = new[] { 10.0, 10, 10 };
            var x = solver.Solve(f, g, vars, lb, ub);

            Assert.NotNull(solver.LastResult);
            Assert.Equal(SolveStatus.Solved, solver.LastResult!.Status);
            for (int i = 0; i < 3; i++) Assert.True(Math.Abs(x[i] - 2.0) < 1e-5);
        }

        [Fact]
        public void SolveWithFiniteDifferenceGradientMatchesAnalytic()
        {
            // Same problem, but no gradient delegate -> central differences (DWSIM fallback).
            Func<double[], double> f = x =>
            {
                double s = 0; foreach (var v in x) { double d = v - 2.0; s += d * d; } return s;
            };

            var solver = new DwsimIpoptSolver();
            var x = solver.Solve(f, null, new double[] { 0, 0, 0 },
                                 new[] { -10.0, -10, -10 }, new[] { 10.0, 10, 10 });

            Assert.Equal(SolveStatus.Solved, solver.LastResult!.Status);
            for (int i = 0; i < 3; i++) Assert.True(Math.Abs(x[i] - 2.0) < 1e-3, $"x[{i}]={x[i]}");
        }

        [Fact]
        public void SolvesDwsimStylePenaltyProblem()
        {
            // DWSIM folds constraints into the objective as penalties. Here:
            //   min (x0-3)^2 + (x1-3)^2  s.t.  x0 + x1 = 4   (penalty rho=1e4)
            // Analytic constrained optimum ~ (2.00005, 2.00005).
            const double rho = 1e4;
            Func<double[], double> f = x =>
            {
                double c = x[0] + x[1] - 4.0;
                double d0 = x[0] - 3.0, d1 = x[1] - 3.0;
                return d0 * d0 + d1 * d1 + rho * c * c;
            };
            Func<double[], double[]> g = x =>
            {
                double c = x[0] + x[1] - 4.0;
                return new[]
                {
                    2.0 * (x[0] - 3.0) + 2.0 * rho * c,
                    2.0 * (x[1] - 3.0) + 2.0 * rho * c
                };
            };

            var sw = new StringWriter();
            var solver = new DwsimIpoptSolver { LogWriter = sw };
            var x = solver.Solve(f, g, new double[] { 0.0, 0.0 }, new[] { 0.0, 0.0 }, new[] { 5.0, 5.0 });
            _out.WriteLine(sw.ToString());

            Assert.Equal(SolveStatus.Solved, solver.LastResult!.Status);
            double expected = (3.0 + 4.0 * rho) / (1.0 + 2.0 * rho);
            Assert.True(Math.Abs(x[0] - expected) < 1e-3, $"x0={x[0]} expected {expected}");
            Assert.True(Math.Abs(x[1] - expected) < 1e-3, $"x1={x[1]}");
            Assert.True(Math.Abs((x[0] + x[1]) - 4.0) < 1e-2, "constraint not satisfied");
        }

        [Fact]
        public void DefaultsToInfiniteBoundsWhenNull()
        {
            // No bounds passed -> unconstrained minimization of a convex quadratic.
            Func<double[], double> f = x => (x[0] - 1.5) * (x[0] - 1.5) + (x[1] + 0.5) * (x[1] + 0.5);
            Func<double[], double[]> g = x => new[] { 2.0 * (x[0] - 1.5), 2.0 * (x[1] + 0.5) };

            var solver = new DwsimIpoptSolver();
            var x = solver.Solve(f, g, new double[] { 0, 0 });

            Assert.Equal(SolveStatus.Solved, solver.LastResult!.Status);
            Assert.True(Math.Abs(x[0] - 1.5) < 1e-5);
            Assert.True(Math.Abs(x[1] + 0.5) < 1e-5);
        }

        [Fact]
        public void AdapterExposesInertiaFreeLogForComparison()
        {
            Func<double[], double> f = x => (x[0] - 2.0) * (x[0] - 2.0);
            var solver = new DwsimIpoptSolver();
            var x = solver.Solve(f, null, new double[] { 0.0 }, new[] { -5.0 }, new[] { 5.0 });

            Assert.Equal(SolveStatus.Solved, solver.LastResult!.Status);
            Assert.True(solver.LastResult.IterationLog.Count > 0);
            Assert.True(Math.Abs(x[0] - 2.0) < 1e-4);
        }
    }
}
