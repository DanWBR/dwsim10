//    Checks the wrapper the engine actually calls, end to end over the managed IPOPT.
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using NUnit.Framework;

namespace DWSIM.MathOps.Tests
{
    /// <summary>
    /// Seven of the engine's eight IPOPT call sites go through
    /// <see cref="MathEx.Optimization.IPOPTSolver"/>: the two nested-loops fallbacks, the binary
    /// interaction parameter regression of three property packages, the flowsheet optimiser and
    /// the natural gas dew point. These exercise that whole chain, from the wrapper down through
    /// the Cureos.Numerics shape into the managed interior-point solver.
    /// </summary>
    [TestFixture]
    public class IpoptSolverTests
    {
        /// <summary>Rosenbrock, minimum 0 at (1, 1). The classic hard case for a quasi-Newton method.</summary>
        private static double Rosenbrock(double[] x)
        {
            double a = 1.0 - x[0];
            double b = x[1] - x[0] * x[0];
            return a * a + 100.0 * b * b;
        }

        private static double[] RosenbrockGradient(double[] x)
        {
            double b = x[1] - x[0] * x[0];
            return new[]
            {
                -2.0 * (1.0 - x[0]) - 400.0 * x[0] * b,
                200.0 * b
            };
        }

        /// <summary>The shape a binary interaction parameter regression has: a sum of squares.</summary>
        private static double SumOfSquares(double[] x)
        {
            double s = 0.0;
            var target = new[] { 0.35, -1.2, 4.0 };

            for (int i = 0; i < x.Length; i++)
            {
                double d = x[i] - target[i];
                s += d * d;
            }

            return s;
        }

        [Test]
        public void SolvesRosenbrockWithAnAnalyticGradient()
        {
            var solver = new MathEx.Optimization.IPOPTSolver
            {
                Tolerance = 1e-10,
                MaxIterations = 2000
            };

            var x = solver.Solve(Rosenbrock, RosenbrockGradient, new[] { -1.2, 1.0 });

            Assert.That(x[0], Is.EqualTo(1.0).Within(1e-4));
            Assert.That(x[1], Is.EqualTo(1.0).Within(1e-4));
            Assert.That(Rosenbrock(x), Is.LessThan(1e-8));
        }

        [Test]
        public void SolvesWithoutAGradient()
        {
            // No gradient supplied: the wrapper builds one by central differences, which is the
            // path the property package regressions take.
            var solver = new MathEx.Optimization.IPOPTSolver
            {
                Tolerance = 1e-8,
                MaxIterations = 2000
            };

            var x = solver.Solve(SumOfSquares, null, new[] { 0.0, 0.0, 0.0 });

            Assert.That(x[0], Is.EqualTo(0.35).Within(1e-3));
            Assert.That(x[1], Is.EqualTo(-1.2).Within(1e-3));
            Assert.That(x[2], Is.EqualTo(4.0).Within(1e-3));
        }

        [Test]
        public void HonoursTheBounds()
        {
            var solver = new MathEx.Optimization.IPOPTSolver
            {
                Tolerance = 1e-8,
                MaxIterations = 2000
            };

            var x = solver.Solve(SumOfSquares, null,
                                 new[] { 0.0, 0.0, 0.0 },
                                 new[] { -10.0, -10.0, -10.0 },
                                 new[] { 10.0, 10.0, 1.0 });

            Assert.That(x[0], Is.EqualTo(0.35).Within(1e-3));
            Assert.That(x[2], Is.LessThanOrEqualTo(1.0 + 1e-9));
            Assert.That(x[2], Is.GreaterThan(0.9));
        }

        [Test]
        public void CountsItsIterations()
        {
            var solver = new MathEx.Optimization.IPOPTSolver
            {
                Tolerance = 1e-10,
                MaxIterations = 2000
            };

            solver.Solve(Rosenbrock, RosenbrockGradient, new[] { -1.2, 1.0 });

            Assert.That(solver.Iterations, Is.GreaterThan(0));
        }

        [Test]
        public void ReturnsTheLowestObjectiveItSawByDefault()
        {
            var solver = new MathEx.Optimization.IPOPTSolver
            {
                Tolerance = 1e-10,
                MaxIterations = 2000
            };

            Assert.That(solver.ReturnLowestObjFuncValue, Is.True);

            var x = solver.Solve(Rosenbrock, RosenbrockGradient, new[] { -1.2, 1.0 });

            // Whatever the solver's own last point was, the wrapper hands back the best one it
            // evaluated, so the objective can never be worse than at the starting point.
            Assert.That(Rosenbrock(x), Is.LessThan(Rosenbrock(new[] { -1.2, 1.0 })));
        }
    }
}
