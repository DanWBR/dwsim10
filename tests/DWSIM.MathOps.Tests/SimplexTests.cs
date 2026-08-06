//    Checks the two-phase simplex against problems whose answer is known by hand.
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
using System.Linq;
using DWSIM.MathOps.MathEx.LinearProgramming;
using NUnit.Framework;

namespace DWSIM.MathOps.Tests
{
    [TestFixture]
    public class SimplexTests
    {
        private static double[] Solve(double[,] a, double[] b, double[] c)
        {
            var x = new double[0];

            Assert.That(Simplex.Minimize(a, b, c, ref x), Is.EqualTo(SimplexStatus.Optimal));

            return x;
        }

        [Test]
        public void ItFindsTheVertexOfATwoVariableProblem()
        {
            // x1 + x2 = 4 and x1 = 1 leave a single point
            var a = new double[,] { { 1, 1 }, { 1, 0 } };
            var b = new double[] { 4, 1 };
            var c = new double[] { -1, -1 };

            var x = Solve(a, b, c);

            Assert.That(x[0], Is.EqualTo(1.0).Within(1e-12));
            Assert.That(x[1], Is.EqualTo(3.0).Within(1e-12));
        }

        [Test]
        public void ItPicksTheCheaperOfTwoVertices()
        {
            // x1 + x2 = 10, and the second variable is the cheaper one
            var a = new double[,] { { 1, 1 } };
            var b = new double[] { 10 };
            var c = new double[] { 3, -2 };

            var x = Solve(a, b, c);

            Assert.That(x[0], Is.EqualTo(0.0).Within(1e-12));
            Assert.That(x[1], Is.EqualTo(10.0).Within(1e-12));
        }

        [Test]
        public void ItSolvesAnElementBalance()
        {
            // Compounds CH4, H2O, CO, H2, CO2 over the elements C, H and O, fed one mole of
            // methane and one of water. Carbon monoxide and hydrogen are the cheap products, and
            // there is not enough oxygen to make carbon dioxide, so the answer is CO + 3 H2.
            var a = new double[,]
            {
                { 1, 0, 1, 0, 1 },
                { 4, 2, 0, 2, 0 },
                { 0, 1, 1, 0, 2 }
            };
            var b = new double[] { 1, 6, 1 };
            var c = new double[] { 0, 0, -100, -50, -10 };

            var x = Solve(a, b, c);

            Assert.That(x[2], Is.EqualTo(1.0).Within(1e-10), "carbon monoxide");
            Assert.That(x[3], Is.EqualTo(3.0).Within(1e-10), "hydrogen");
            Assert.That(x[0] + x[1] + x[4], Is.EqualTo(0.0).Within(1e-10), "nothing else is left");

            var objective = c.Select((g, i) => g * x[i]).Sum();

            Assert.That(objective, Is.EqualTo(-250.0).Within(1e-9));
        }

        [Test]
        public void ARedundantConstraintDoesNotStopIt()
        {
            // the second row repeats the first, so one artificial variable stays basic at zero
            var a = new double[,] { { 1, 1, 1 }, { 1, 1, 1 }, { 0, 1, 0 } };
            var b = new double[] { 5, 5, 2 };
            var c = new double[] { 1, 1, -1 };

            var x = Solve(a, b, c);

            Assert.That(x[0] + x[1] + x[2], Is.EqualTo(5.0).Within(1e-10));
            Assert.That(x[1], Is.EqualTo(2.0).Within(1e-10));
            Assert.That(x[2], Is.EqualTo(3.0).Within(1e-10), "the only variable worth having");
        }

        [Test]
        public void ADegenerateVertexDoesNotMakeItCycle()
        {
            // every right-hand side is zero, so every basic variable sits at zero
            var a = new double[,] { { 1, 1, 0 }, { 0, 1, 1 } };
            var b = new double[] { 0, 0 };
            var c = new double[] { -1, 2, -3 };

            var x = Solve(a, b, c);

            Assert.That(x.All(v => Math.Abs(v) < 1e-12), "the origin is the only feasible point");
        }

        [Test]
        public void ItReportsAnImpossibleBalance()
        {
            // a negative total cannot be reached with non-negative amounts of anything
            var a = new double[,] { { 1, 1 } };
            var b = new double[] { -3 };
            var c = new double[] { 1, 1 };
            var x = new double[0];

            Assert.That(Simplex.Minimize(a, b, c, ref x), Is.EqualTo(SimplexStatus.Infeasible));
        }

        [Test]
        public void ItReportsAnObjectiveWithNoFloor()
        {
            // x1 = x2 can grow together for ever, and both are worth having
            var a = new double[,] { { 1, -1 } };
            var b = new double[] { 0 };
            var c = new double[] { -1, -1 };
            var x = new double[0];

            Assert.That(Simplex.Minimize(a, b, c, ref x), Is.EqualTo(SimplexStatus.Unbounded));
        }

        [Test]
        public void ItRefusesMismatchedShapes()
        {
            var a = new double[,] { { 1, 1 } };

            Assert.Throws<ArgumentException>(() =>
            {
                var x = new double[0];
                Simplex.Minimize(a, new double[] { 1, 2 }, new double[] { 1, 1 }, ref x);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var x = new double[0];
                Simplex.Minimize(a, new double[] { 1 }, new double[] { 1, 1, 1 }, ref x);
            });
        }

        /// <summary>
        /// The answers of a thousand element-balance problems have to satisfy the balance, stay
        /// non-negative, and sit on a vertex: no more non-zero amounts than there are elements.
        /// </summary>
        [Test]
        public void ItAnswersElementBalancesOfEveryShape()
        {
            var random = new Random(20260806);

            for (var round = 0; round < 1000; round++)
            {
                var m = 1 + random.Next(6);
                var n = m + random.Next(30);

                var a = new double[m, n];

                for (var j = 0; j < n; j++)
                {
                    var atoms = 0;
                    for (var i = 0; i < m; i++)
                    {
                        var v = random.NextDouble() < 0.45 ? random.Next(1, 5) : 0;
                        a[i, j] = v;
                        atoms += v;
                    }
                    if (atoms == 0) a[random.Next(m), j] = 1;
                }

                var feed = new double[n];
                for (var j = 0; j < n; j++)
                    feed[j] = random.NextDouble() < 0.3 ? 0.0 : random.NextDouble() * 100.0;

                var b = new double[m];
                for (var i = 0; i < m; i++)
                    for (var j = 0; j < n; j++) b[i] += a[i, j] * feed[j];

                var c = new double[n];
                for (var j = 0; j < n; j++) c[j] = (random.NextDouble() - 0.5) * 1000.0;

                var x = new double[0];

                Assert.That(Simplex.Minimize(a, b, c, ref x), Is.EqualTo(SimplexStatus.Optimal),
                            $"round {round}: m={m} n={n}");

                for (var i = 0; i < m; i++)
                {
                    var row = 0.0;
                    for (var j = 0; j < n; j++) row += a[i, j] * x[j];

                    Assert.That(row, Is.EqualTo(b[i]).Within(1e-8).Percent,
                                $"round {round}: element {i} is out of balance");
                }

                Assert.That(x.Min(), Is.GreaterThanOrEqualTo(0.0), $"round {round}: a negative amount");

                Assert.That(x.Count(v => v > 0.0), Is.LessThanOrEqualTo(m),
                            $"round {round}: the answer is not a vertex");
            }
        }
    }
}
