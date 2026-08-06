using System;
using System.Globalization;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using DWSIM.Numerics.Ipopt.Core;

namespace DWSIM.Numerics.Ipopt.Core.Tests
{
    public class LogAndOptionsTests
    {
        private readonly ITestOutputHelper _out;
        public LogAndOptionsTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void ProducesIpoptStyleLogAndConverges()
        {
            var sw = new StringWriter();
            var opt = new SolverOptions { LogWriter = sw };
            var res = new InteriorPointSolver(opt).Solve(new BoundedRosenbrock());

            Assert.Equal(SolveStatus.Solved, res.Status);

            string text = sw.ToString();
            _out.WriteLine(text); // eyeball vs a native Ipopt run

            Assert.Contains("iter", text);
            Assert.Contains("inf_du", text);
            // Invariant formatting: scientific notation, never a locale decimal comma.
            Assert.Contains("e", text);
            Assert.DoesNotContain(",", text);

            // Log collected, and the last recorded row is the converged point.
            Assert.NotEmpty(res.IterationLog);
            var last = res.IterationLog[res.IterationLog.Count - 1];
            Assert.True(last.InfDu <= 1e-6, $"final inf_du {last.InfDu:E3}");
        }

        [Fact]
        public void DualInfeasibilityDecreasesToConvergence()
        {
            var res = new InteriorPointSolver().Solve(new BoundedRosenbrock());
            Assert.Equal(SolveStatus.Solved, res.Status);

            double firstInfDu = res.IterationLog[0].InfDu;
            double lastInfDu = res.IterationLog[res.IterationLog.Count - 1].InfDu;
            Assert.True(lastInfDu < firstInfDu);
            Assert.True(lastInfDu <= 1e-6);
        }

        [Fact]
        public void RespectsCustomMuInitAndStillConverges()
        {
            var res = new InteriorPointSolver(new SolverOptions { MuInit = 1.0 })
                .Solve(new QuadraticBounded());
            Assert.Equal(SolveStatus.Solved, res.Status);
            Assert.True(Math.Abs(res.X[0] - 2.0) < 1e-5);
        }

        [Fact]
        public void HeaderRowFormatsWithInvariantCulture()
        {
            var info = new IterationInfo(3, -1.234567e2, 0.0, 4.5e-3, 1e-4, 2.2e-2, 1e-8, 1.0, 0.5, 2);
            string row = IpoptLog.Row(info);
            Assert.DoesNotContain(",", row);
            Assert.Contains("e", row);
        }

        private sealed class BoundedRosenbrock : INlp
        {
            public int N => 2;
            public void GetBounds(double[] xl, double[] xu) { xl[0] = -5; xl[1] = -5; xu[0] = 5; xu[1] = 5; }
            public void GetStartingPoint(double[] x) { x[0] = -1.2; x[1] = 1.0; }
            public double EvalF(double[] x)
            {
                double a = x[1] - x[0] * x[0], b = 1.0 - x[0];
                return 100.0 * a * a + b * b;
            }
            public void EvalGradF(double[] x, double[] g)
            {
                double a = x[1] - x[0] * x[0];
                g[0] = -400.0 * x[0] * a - 2.0 * (1.0 - x[0]);
                g[1] = 200.0 * a;
            }
        }

        private sealed class QuadraticBounded : INlp
        {
            public int N => 3;
            public void GetBounds(double[] xl, double[] xu)
            {
                for (int i = 0; i < 3; i++) { xl[i] = -10; xu[i] = 10; }
            }
            public void GetStartingPoint(double[] x) { for (int i = 0; i < 3; i++) x[i] = 0.0; }
            public double EvalF(double[] x)
            {
                double s = 0; for (int i = 0; i < 3; i++) { double d = x[i] - 2.0; s += d * d; } return s;
            }
            public void EvalGradF(double[] x, double[] g)
            {
                for (int i = 0; i < 3; i++) g[i] = 2.0 * (x[i] - 2.0);
            }
        }
    }
}
