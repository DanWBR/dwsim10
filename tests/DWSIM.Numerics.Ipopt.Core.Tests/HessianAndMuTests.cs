using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using DWSIM.Numerics.Ipopt.Core;

namespace DWSIM.Numerics.Ipopt.Core.Tests
{
    public class HessianAndMuTests
    {
        private readonly ITestOutputHelper _out;
        public HessianAndMuTests(ITestOutputHelper output) { _out = output; }

        public static TheoryData<MuStrategy, HessianApproximation> Combos()
        {
            var d = new TheoryData<MuStrategy, HessianApproximation>();
            foreach (var mu in new[] { MuStrategy.Monotone, MuStrategy.Adaptive })
                foreach (var h in new[] { HessianApproximation.DenseBfgs, HessianApproximation.LimitedMemoryBfgs })
                    d.Add(mu, h);
            return d;
        }

        [Theory]
        [MemberData(nameof(Combos))]
        public void AllStrategyCombosSolveRosenbrock(MuStrategy mu, HessianApproximation hess)
        {
            var opt = new SolverOptions { MuStrategy = mu, HessianApproximation = hess, MaxIterations = 5000 };
            var res = new InteriorPointSolver(opt).Solve(new Rosenbrock());

            Assert.Equal(SolveStatus.Solved, res.Status);
            Assert.True(Math.Abs(res.X[0] - 1.0) < 1e-4 && Math.Abs(res.X[1] - 1.0) < 1e-4,
                $"{mu}/{hess}: x=({res.X[0]:F6},{res.X[1]:F6})");
        }

        [Theory]
        [MemberData(nameof(Combos))]
        public void AllStrategyCombosSolveBoundedQuadratic(MuStrategy mu, HessianApproximation hess)
        {
            // Optimum pulled to the upper bound at 1.
            var opt = new SolverOptions { MuStrategy = mu, HessianApproximation = hess };
            var res = new InteriorPointSolver(opt).Solve(new Quadratic(center: 5.0, lo: 0, hi: 1, n: 5));

            Assert.Equal(SolveStatus.Solved, res.Status);
            for (int i = 0; i < 5; i++) Assert.True(Math.Abs(res.X[i] - 1.0) < 1e-4);
        }

        [Fact]
        public void DwsimDefaultsAreAdaptiveLimitedMemory()
        {
            var opt = new SolverOptions();
            Assert.Equal(MuStrategy.Adaptive, opt.MuStrategy);
            Assert.Equal(HessianApproximation.LimitedMemoryBfgs, opt.HessianApproximation);
            Assert.Equal(6, opt.LimitedMemoryMaxHistory);
        }

        [Fact]
        public void AdaptiveMuLogVariesAndConverges()
        {
            var sw = new StringWriter();
            var opt = new SolverOptions
            {
                MuStrategy = MuStrategy.Adaptive,
                HessianApproximation = HessianApproximation.LimitedMemoryBfgs,
                LogWriter = sw
            };
            var res = new InteriorPointSolver(opt).Solve(new Rosenbrock());
            Assert.Equal(SolveStatus.Solved, res.Status);
            _out.WriteLine(sw.ToString());

            // Adaptive mu is not a monotone staircase: lg(mu) should take several distinct
            // values across iterations (unlike the monotone 0.1 -> 0.02 -> ... schedule).
            int distinctMu = 0;
            double prev = double.NaN;
            foreach (var it in res.IterationLog)
            {
                double lg = Math.Round(Math.Log10(it.Mu), 3);
                if (lg != prev) distinctMu++;
                prev = lg;
            }
            Assert.True(distinctMu >= 5, $"expected varied mu, got {distinctMu} distinct values");
        }

        private sealed class Rosenbrock : INlp
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

        private sealed class Quadratic : INlp
        {
            private readonly int _n; private readonly double _c, _lo, _hi;
            public Quadratic(double center, double lo, double hi, int n) { _c = center; _lo = lo; _hi = hi; _n = n; }
            public int N => _n;
            public void GetBounds(double[] xl, double[] xu) { for (int i = 0; i < _n; i++) { xl[i] = _lo; xu[i] = _hi; } }
            public void GetStartingPoint(double[] x) { for (int i = 0; i < _n; i++) x[i] = 0.5 * (_lo + _hi); }
            public double EvalF(double[] x) { double s = 0; for (int i = 0; i < _n; i++) { double d = x[i] - _c; s += d * d; } return s; }
            public void EvalGradF(double[] x, double[] g) { for (int i = 0; i < _n; i++) g[i] = 2.0 * (x[i] - _c); }
        }
    }
}
