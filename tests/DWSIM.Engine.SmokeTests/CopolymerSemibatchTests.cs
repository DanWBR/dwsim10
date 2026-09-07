using NUnit.Framework;
using DWSIM.Thermodynamics.Polymers;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The standalone semibatch copolymer solver. Metering the more reactive monomer in during the run is the
    /// industrial way to hold the copolymer composition constant; the reactor holdup grows as feed is added, so
    /// the balances are on total amounts. Validated by conservation (incorporated monomer and reactor volume),
    /// and by the defining result: a semibatch feed suppresses the composition drift that the same monomers,
    /// charged all at once as a batch, would show.
    /// </summary>
    [TestFixture]
    public class CopolymerSemibatchTests
    {
        [Test]
        public void MassBalanceAndVolumeGrowth()
        {
            var k = CopolymerKinetics.StyreneMMA();
            k.AtrMA = 0.0; k.AtrMB = 0.0;   // no transfer, so every consumed monomer is propagated into a chain
            var feed = new SemibatchFeed
            {
                ChargeA = 0.2, ChargeB = 0.6, ChargeI = 0.2, ChargeVolume = 0.5,
                FeedA = 1.8 / 20000.0, FeedB = 5.4 / 20000.0, FeedI = 0.0,
                FeedVolumetric = 1.5 / 20000.0, FeedDuration = 20000.0, TotalTime = 24000.0
            };
            var r = SemibatchCopolymer.Solve(k, 333.15, feed);
            Assert.That(r.Converged, Is.True);

            double consumedA = (0.2 + 1.8) - r.MonomerAConc * r.FinalVolume;
            double consumedB = (0.6 + 5.4) - r.MonomerBConc * r.FinalVolume;
            TestContext.WriteLine($"balance: X={r.OverallConversion:F4} Vf={r.FinalVolume:F3} " +
                                  $"PsiA={r.MonomerAIncorporated:F5} consumedA={consumedA:F5}");
            Assert.Multiple(() =>
            {
                Assert.That(r.FinalVolume, Is.EqualTo(0.5 + 1.5).Within(0.01), "volume = initial charge + fed volume");
                Assert.That(r.MonomerAIncorporated, Is.EqualTo(consumedA).Within(1e-3 * consumedA + 1e-9), "incorporated A = consumed A");
                Assert.That(r.MonomerBIncorporated, Is.EqualTo(consumedB).Within(1e-3 * consumedB + 1e-9), "incorporated B = consumed B");
            });
        }

        [Test]
        public void SemibatchFeedSuppressesTheCompositionDrift()
        {
            // Same total monomer (A:B = 2:6) and initiator, reacted for the same time. The batch charges it all
            // at the start and drifts; the semibatch keeps a small heel and meters the rest in at the 1:3 ratio,
            // so the more reactive monomer is continually replenished and the composition barely moves.
            var k = CopolymerKinetics.StyreneMMA();

            var batch = new SemibatchFeed
            {
                ChargeA = 2.0, ChargeB = 6.0, ChargeI = 0.5, ChargeVolume = 2.0,
                FeedA = 0.0, FeedB = 0.0, FeedI = 0.0, FeedVolumetric = 0.0,
                FeedDuration = 0.0, TotalTime = 40000.0
            };
            var semi = new SemibatchFeed
            {
                ChargeA = 0.2, ChargeB = 0.6, ChargeI = 0.5, ChargeVolume = 0.5,
                FeedA = 1.8 / 30000.0, FeedB = 5.4 / 30000.0, FeedI = 0.0,
                FeedVolumetric = 1.5 / 30000.0, FeedDuration = 30000.0, TotalTime = 40000.0
            };

            var rb = SemibatchCopolymer.Solve(k, 333.15, batch);
            var rs = SemibatchCopolymer.Solve(k, 333.15, semi);

            double batchDrift = System.Math.Abs(rb.InstantaneousCompositionA - rb.CumulativeCompositionA);
            double semiDrift = System.Math.Abs(rs.InstantaneousCompositionA - rs.CumulativeCompositionA);
            TestContext.WriteLine($"batch: X={rb.OverallConversion:F3} inst={rb.InstantaneousCompositionA:F4} cum={rb.CumulativeCompositionA:F4} drift={batchDrift:F4}");
            TestContext.WriteLine($"semi:  X={rs.OverallConversion:F3} inst={rs.InstantaneousCompositionA:F4} cum={rs.CumulativeCompositionA:F4} drift={semiDrift:F4}");
            Assert.Multiple(() =>
            {
                Assert.That(rb.Converged, Is.True);
                Assert.That(rs.Converged, Is.True);
                Assert.That(rb.OverallConversion, Is.GreaterThan(0.4), "the batch must react appreciably");
                Assert.That(rs.OverallConversion, Is.GreaterThan(0.4), "the semibatch must react appreciably");
                Assert.That(semiDrift, Is.LessThan(0.6 * batchDrift), "the semibatch feed must suppress the composition drift");
            });
        }

        [Test]
        public void StarvedFeedSetsCompositionByFeedRatio()
        {
            // In the monomer-starved limit the monomers react as fast as they are fed, so the copolymer
            // composition equals the feed molar composition (here A:B = 1:3, so F_A -> 0.25), not the Mayo-Lewis
            // value of that ratio. A high radical flux and a slow feed keep the reactor starved.
            var k = CopolymerKinetics.StyreneMMA();
            // End the run when the feed ends (no batch finish), so the reactor stays starved to the last instant
            // rather than draining its heel and drifting at the tail.
            var feed = new SemibatchFeed
            {
                ChargeA = 0.02, ChargeB = 0.06, ChargeI = 2.0, ChargeVolume = 1.0,
                FeedA = 1.0 / 60000.0, FeedB = 3.0 / 60000.0, FeedI = 0.0,
                FeedVolumetric = 0.5 / 60000.0, FeedDuration = 60000.0, TotalTime = 60000.0
            };
            var r = SemibatchCopolymer.Solve(k, 333.15, feed);
            TestContext.WriteLine($"starved: X={r.OverallConversion:F3} inst={r.InstantaneousCompositionA:F4} cum={r.CumulativeCompositionA:F4}");
            Assert.Multiple(() =>
            {
                Assert.That(r.Converged, Is.True);
                Assert.That(r.CumulativeCompositionA, Is.EqualTo(0.25).Within(0.04), "starved feed sets composition by the feed ratio");
                Assert.That(System.Math.Abs(r.InstantaneousCompositionA - r.CumulativeCompositionA), Is.LessThan(0.05), "little drift under starvation");
            });
        }
    }
}
