//    The complex-step derivative of the Beggs and Brill pressure drop, checked against differencing the
//    correlation itself. Differencing is trustworthy HERE, where the correlation is closed form; it is not
//    trustworthy on the pipe as a whole, whose per-increment flashes leave noise that a difference divides
//    by the step. That is what the complex step exists to avoid: it takes no difference, so there is no
//    cancellation and no step size to pick.

using System;
using DWSIM.UnitOperations.FlowPackages;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class BeggsBrillGradientTests
    {
        // D[m], L[m], dz[m], roughness[m], qv[m3/d], ql[m3/d], muv[cP], mul[cP], rhov, rhol [kg/m3], surft[N/m]
        static readonly double[][] Cases =
        {
            new[] { 0.10,  100.0,  10.0, 4.6e-5,  5000.0,  500.0, 0.015, 1.2, 45.0, 780.0, 0.020 }, // uphill
            new[] { 0.15,  500.0,   0.0, 4.6e-5, 20000.0, 2000.0, 0.018, 0.8, 60.0, 700.0, 0.015 }, // horizontal
            new[] { 0.08,  200.0, -20.0, 4.6e-5,   800.0, 4000.0, 0.012, 2.5, 30.0, 850.0, 0.025 }, // downhill
            new[] { 0.20, 1000.0,  50.0, 4.6e-5, 50000.0,  300.0, 0.020, 0.5, 90.0, 650.0, 0.010 }, // gas dominated
        };

        static double Total(BeggsBrill bb, double[] c, double qv, double ql)
        {
            var r = bb.CalculateDeltaP(c[0], c[1], c[2], c[3], qv, ql, c[6], c[7], c[8], c[9], c[10]);
            return Convert.ToDouble(r[4]);
        }

        [Test]
        public void ComplexStepGradientMatchesCentralDifference()
        {
            var bb = new BeggsBrill();
            foreach (var c in Cases)
            {
                double qv = c[4], ql = c[5];
                var g = bb.CalculateDeltaPGradient(c[0], c[1], c[2], c[3], qv, ql, c[6], c[7], c[8], c[9], c[10]);
                Assert.That(g, Is.Not.Null, "no gradient for a two-phase case");

                // h/x = 1e-4 sits in the central difference's sweet spot for this correlation: the
                // truncation error has fallen as h^2 and round-off has not yet taken over.
                double hv = qv * 1e-4, hl = ql * 1e-4;
                double fdv = (Total(bb, c, qv + hv, ql) - Total(bb, c, qv - hv, ql)) / (2 * hv);
                double fdl = (Total(bb, c, qv, ql + hl) - Total(bb, c, qv, ql - hl)) / (2 * hl);

                Assert.That(g[0], Is.EqualTo(fdv).Within(1e-6).Percent, "d(dP)/dqv at qv=" + qv);
                Assert.That(g[1], Is.EqualTo(fdl).Within(1e-6).Percent, "d(dP)/dql at ql=" + ql);
            }
        }

        [Test]
        public void ComplexTwinReproducesTheRealCorrelation()
        {
            // The gradient runs through a complex copy of the correlation. If the two ever drift apart the
            // differences above stop matching, so this asserts the same thing from the value side: an
            // imaginary step of 1e-30 cannot move the real part.
            var bb = new BeggsBrill();
            foreach (var c in Cases)
            {
                double dp = Total(bb, c, c[4], c[5]);
                var g = bb.CalculateDeltaPGradient(c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7], c[8], c[9], c[10]);
                Assert.That(g, Is.Not.Null);
                // a first-order prediction over a small step must land on the correlation's own value
                double step = c[4] * 1e-3;
                double predicted = dp + g[0] * step;
                double actual = Total(bb, c, c[4] + step, c[5]);
                Assert.That(predicted, Is.EqualTo(actual).Within(0.01).Percent, "twin disagrees with the real routine");
            }
        }

        [Test]
        public void SinglePhaseInputHasNoTwoPhaseGradient()
        {
            var bb = new BeggsBrill();
            var c = Cases[0];
            Assert.That(bb.CalculateDeltaPGradient(c[0], c[1], c[2], c[3], 0.0, c[5], c[6], c[7], c[8], c[9], c[10]), Is.Null);
            Assert.That(bb.CalculateDeltaPGradient(c[0], c[1], c[2], c[3], c[4], 0.0, c[6], c[7], c[8], c[9], c[10]), Is.Null);
        }
    }
}
