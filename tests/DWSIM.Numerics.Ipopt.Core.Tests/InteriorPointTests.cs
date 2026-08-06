using System;
using Xunit;
using DWSIM.Numerics.Ipopt.Core;

namespace DWSIM.Numerics.Ipopt.Core.Tests
{
    public class InteriorPointTests
    {
        private static double MaxAbsDiff(double[] a, double[] b)
        {
            double m = 0.0;
            for (int i = 0; i < a.Length; i++) m = Math.Max(m, Math.Abs(a[i] - b[i]));
            return m;
        }

        [Fact]
        public void InteriorOptimumOfSeparableQuadratic()
        {
            // min sum (x_i - 2)^2, bounds [-10,10]; optimum x = 2, f = 0, no bound active.
            var nlp = new QuadraticNlp(n: 5, center: 2.0, lo: -10, hi: 10);
            var res = new InteriorPointSolver().Solve(nlp);

            Assert.Equal(SolveStatus.Solved, res.Status);
            var star = Fill(5, 2.0);
            Assert.True(MaxAbsDiff(res.X, star) < 1e-5, $"x diff {MaxAbsDiff(res.X, star):E3}");
            Assert.True(res.ObjValue < 1e-8);
        }

        [Fact]
        public void ConvergesToActiveUpperBound()
        {
            // min sum (x_i - 5)^2, bounds [0,1]; optimum at x = 1 (upper bound).
            var nlp = new QuadraticNlp(n: 4, center: 5.0, lo: 0, hi: 1);
            var res = new InteriorPointSolver().Solve(nlp);

            Assert.Equal(SolveStatus.Solved, res.Status);
            var star = Fill(4, 1.0);
            Assert.True(MaxAbsDiff(res.X, star) < 1e-4, $"x diff {MaxAbsDiff(res.X, star):E3}");
        }

        [Fact]
        public void ConvergesToActiveLowerBound()
        {
            // min sum (x_i + 5)^2, bounds [0,1]; optimum at x = 0 (lower bound).
            var nlp = new QuadraticNlp(n: 4, center: -5.0, lo: 0, hi: 1);
            var res = new InteriorPointSolver().Solve(nlp);

            Assert.Equal(SolveStatus.Solved, res.Status);
            var star = Fill(4, 0.0);
            Assert.True(MaxAbsDiff(res.X, star) < 1e-4, $"x diff {MaxAbsDiff(res.X, star):E3}");
        }

        [Fact]
        public void SolvesBoundedRosenbrock()
        {
            // Classic nonconvex test; optimum (1,1), f = 0. Exercises damped BFGS + line search.
            var nlp = new RosenbrockNlp(lo: -5, hi: 5);
            var res = new InteriorPointSolver(new SolverOptions { MaxIterations = 5000 }).Solve(nlp);

            Assert.Equal(SolveStatus.Solved, res.Status);
            Assert.True(MaxAbsDiff(res.X, new[] { 1.0, 1.0 }) < 1e-4, $"x = ({res.X[0]:F6},{res.X[1]:F6})");
            Assert.True(res.ObjValue < 1e-7);
        }

        [Fact]
        public void HandlesMixedBoundedAndFreeVariables()
        {
            // x0 free, x1 in [0,1] with unconstrained optimum below 0 -> active at 0.
            // min (x0-3)^2 + (x1+2)^2 ; optimum x0=3, x1=0.
            var nlp = new MixedNlp();
            var res = new InteriorPointSolver().Solve(nlp);

            Assert.Equal(SolveStatus.Solved, res.Status);
            Assert.True(Math.Abs(res.X[0] - 3.0) < 1e-4);
            Assert.True(Math.Abs(res.X[1] - 0.0) < 1e-4);
        }

        private static double[] Fill(int n, double v)
        {
            var a = new double[n];
            for (int i = 0; i < n; i++) a[i] = v;
            return a;
        }

        // ---- test NLPs ----

        private sealed class QuadraticNlp : INlp
        {
            private readonly int _n;
            private readonly double _c, _lo, _hi;
            public QuadraticNlp(int n, double center, double lo, double hi) { _n = n; _c = center; _lo = lo; _hi = hi; }

            public int N => _n;
            public void GetBounds(double[] xl, double[] xu)
            {
                for (int i = 0; i < _n; i++) { xl[i] = _lo; xu[i] = _hi; }
            }
            public void GetStartingPoint(double[] x)
            {
                for (int i = 0; i < _n; i++) x[i] = 0.5 * (_lo + _hi);
            }
            public double EvalF(double[] x)
            {
                double s = 0.0;
                for (int i = 0; i < _n; i++) { double d = x[i] - _c; s += d * d; }
                return s;
            }
            public void EvalGradF(double[] x, double[] g)
            {
                for (int i = 0; i < _n; i++) g[i] = 2.0 * (x[i] - _c);
            }
        }

        private sealed class RosenbrockNlp : INlp
        {
            private readonly double _lo, _hi;
            public RosenbrockNlp(double lo, double hi) { _lo = lo; _hi = hi; }

            public int N => 2;
            public void GetBounds(double[] xl, double[] xu) { xl[0] = _lo; xl[1] = _lo; xu[0] = _hi; xu[1] = _hi; }
            public void GetStartingPoint(double[] x) { x[0] = -1.2; x[1] = 1.0; }
            public double EvalF(double[] x)
            {
                double a = x[1] - x[0] * x[0];
                double b = 1.0 - x[0];
                return 100.0 * a * a + b * b;
            }
            public void EvalGradF(double[] x, double[] g)
            {
                double a = x[1] - x[0] * x[0];
                g[0] = -400.0 * x[0] * a - 2.0 * (1.0 - x[0]);
                g[1] = 200.0 * a;
            }
        }

        private sealed class MixedNlp : INlp
        {
            public int N => 2;
            public void GetBounds(double[] xl, double[] xu)
            {
                xl[0] = double.NegativeInfinity; xu[0] = double.PositiveInfinity; // free
                xl[1] = 0.0; xu[1] = 1.0;
            }
            public void GetStartingPoint(double[] x) { x[0] = 0.0; x[1] = 0.5; }
            public double EvalF(double[] x)
            {
                double d0 = x[0] - 3.0, d1 = x[1] + 2.0;
                return d0 * d0 + d1 * d1;
            }
            public void EvalGradF(double[] x, double[] g)
            {
                g[0] = 2.0 * (x[0] - 3.0);
                g[1] = 2.0 * (x[1] + 2.0);
            }
        }
    }
}
