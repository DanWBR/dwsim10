using NUnit.Framework;
using DWSIM.Thermodynamics.Polymers;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The dynamic (transient) reaction step used by the polymerization reactor in DWSIM's dynamic mode. Stepping
    /// a well-mixed holdup with no feed must reproduce the batch (PFR) trajectory, and adding feed each step must
    /// reproduce the semibatch trajectory - the dynamic mode is the same physics driven by the integrator.
    /// </summary>
    [TestFixture]
    public class CopolymerDynamicsTests
    {
        [Test]
        public void SteppingWithoutFeedReproducesTheBatchTrajectory()
        {
            var k = CopolymerKinetics.StyreneMMA();
            double V = 2.0, tTotal = 20000.0;
            double cA0 = 3.0, cB0 = 5.0, cI0 = 0.05;   // mol/L

            var batch = CopolymerPFR.Solve(k, 333.15, tTotal, cA0, cB0, cI0);

            var st = new CopolymerDynState { MonomerA = cA0 * V, MonomerB = cB0 * V, Initiator = cI0 * V, Volume = V };
            int steps = 400;
            double dt = tTotal / steps;
            for (int i = 0; i < steps; i++) CopolymerDynamics.Advance(k, 333.15, null, st, dt, 5);

            double convDyn = 1.0 - (st.MonomerA + st.MonomerB) / ((cA0 + cB0) * V);
            TestContext.WriteLine($"batch:   X={batch.OverallConversion:F4} cum={batch.CumulativeCompositionA:F4} Mn={batch.Mn:F0}");
            TestContext.WriteLine($"dynamic: X={convDyn:F4} cum={st.CumulativeCompositionA():F4} Mn={st.NumberAverageMW(k):F0}");
            Assert.Multiple(() =>
            {
                Assert.That(convDyn, Is.EqualTo(batch.OverallConversion).Within(0.01), "stepwise conversion matches the batch");
                Assert.That(st.CumulativeCompositionA(), Is.EqualTo(batch.CumulativeCompositionA).Within(0.01), "stepwise composition matches the batch");
                Assert.That(st.NumberAverageMW(k), Is.EqualTo(batch.Mn).Within(batch.Mn * 0.03), "stepwise Mn matches the batch");
            });
        }

        [Test]
        public void FeedingEachStepReproducesTheSemibatchTrajectory()
        {
            var k = CopolymerKinetics.StyreneMMA();
            var feed = new SemibatchFeed
            {
                ChargeA = 0.2, ChargeB = 0.6, ChargeI = 0.5, ChargeVolume = 0.5,
                FeedA = 1.8 / 30000.0, FeedB = 5.4 / 30000.0, FeedI = 0.0,
                FeedVolumetric = 1.5 / 30000.0, FeedDuration = 30000.0, TotalTime = 30000.0
            };
            var reference = SemibatchCopolymer.Solve(k, 333.15, feed);

            // Reproduce it by operator splitting: add feed, react, each step.
            var st = new CopolymerDynState { MonomerA = feed.ChargeA, MonomerB = feed.ChargeB, Initiator = feed.ChargeI, Volume = feed.ChargeVolume };
            int steps = 600;
            double dt = feed.TotalTime / steps;
            for (int i = 0; i < steps; i++)
            {
                st.MonomerA += feed.FeedA * dt;
                st.MonomerB += feed.FeedB * dt;
                st.Volume += feed.FeedVolumetric * dt;
                CopolymerDynamics.Advance(k, 333.15, null, st, dt, 5);
            }
            TestContext.WriteLine($"semibatch ref: cum={reference.CumulativeCompositionA:F4} Mn={reference.Mn:F0}");
            TestContext.WriteLine($"stepped:       cum={st.CumulativeCompositionA():F4} Mn={st.NumberAverageMW(k):F0}");
            Assert.Multiple(() =>
            {
                Assert.That(st.CumulativeCompositionA(), Is.EqualTo(reference.CumulativeCompositionA).Within(0.02), "stepwise semibatch composition matches");
                Assert.That(st.NumberAverageMW(k), Is.EqualTo(reference.Mn).Within(reference.Mn * 0.05), "stepwise semibatch Mn matches");
            });
        }
    }
}
