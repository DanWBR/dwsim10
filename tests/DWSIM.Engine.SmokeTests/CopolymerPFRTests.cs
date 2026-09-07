using NUnit.Framework;
using DWSIM.Thermodynamics.Polymers;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The standalone copolymer plug-flow / batch solver. Its defining behaviour, absent from the well-mixed
    /// CSTR, is composition drift: the more reactive monomer depletes first, so the instantaneous copolymer
    /// composition moves along the reactor and the product carries a spread of compositions. Validated by an
    /// exact composition mass balance, the absence of drift at an azeotrope, the presence and direction of
    /// drift for a skewed feed, and the polydispersity (the combination limit at low conversion, broadening as
    /// conversion builds).
    /// </summary>
    [TestFixture]
    public class CopolymerPFRTests
    {
        [Test]
        public void CumulativeCompositionMatchesTheConsumedMonomer()
        {
            // With no transfer, every monomer consumed is propagated into a chain, so the cumulative copolymer
            // composition must equal the overall consumed-monomer ratio exactly (a conservation check).
            var k = CopolymerKinetics.StyreneMMA();
            k.AtrMA = 0.0; k.AtrMB = 0.0;
            var r = CopolymerPFR.Solve(k, 333.15, 10000.0, 3.0, 5.0, 0.05);
            Assert.That(r.Converged, Is.True);
            double consumedA = 3.0 - r.MonomerAConc, consumedB = 5.0 - r.MonomerBConc;
            double fromBalance = consumedA / (consumedA + consumedB);
            TestContext.WriteLine($"balance: X={r.OverallConversion:F4} cumF_A={r.CumulativeCompositionA:F5} consumed={fromBalance:F5}");
            Assert.That(r.CumulativeCompositionA, Is.EqualTo(fromBalance).Within(1e-4), "cumulative composition = consumed-monomer ratio");
        }

        [Test]
        public void AzeotropicFeedDoesNotDrift()
        {
            // Symmetric reactivity and propagation with an equal feed sit at the azeotrope: no drift, so the
            // instantaneous and cumulative compositions both stay at 0.5 even at high conversion.
            var k = CopolymerKinetics.StyreneMMA();
            k.ReactivityA = 0.5; k.ReactivityB = 0.5;
            k.ApBB = k.ApAA; k.EpBB = k.EpAA;
            k.AtrMA = 0.0; k.AtrMB = 0.0;
            var r = CopolymerPFR.Solve(k, 333.15, 20000.0, 4.0, 4.0, 0.05);
            TestContext.WriteLine($"azeotrope: X={r.OverallConversion:F4} inst={r.InstantaneousCompositionA:F5} cum={r.CumulativeCompositionA:F5}");
            Assert.Multiple(() =>
            {
                Assert.That(r.OverallConversion, Is.GreaterThan(0.4), "the run must reach appreciable conversion");
                Assert.That(r.InstantaneousCompositionA, Is.EqualTo(0.5).Within(0.005), "no instantaneous drift at the azeotrope");
                Assert.That(r.CumulativeCompositionA, Is.EqualTo(0.5).Within(0.005), "no cumulative drift at the azeotrope");
            });
        }

        [Test]
        public void SkewedFeedDriftsWithConversion()
        {
            // A skewed feed of a non-azeotropic pair drifts: at low conversion the instantaneous and cumulative
            // compositions agree (both Mayo-Lewis at the feed), but by high conversion they separate.
            var k = CopolymerKinetics.StyreneMMA();     // rA = 0.52, rB = 0.46
            double fA0 = 2.0 / 8.0;
            double mayoFeed = CopolymerPFRMayo(fA0, k.ReactivityA, k.ReactivityB);

            var lo = CopolymerPFR.Solve(k, 333.15, 200.0, 2.0, 6.0, 0.05);      // low conversion
            var hi = CopolymerPFR.Solve(k, 333.15, 40000.0, 2.0, 6.0, 0.05);    // high conversion
            TestContext.WriteLine($"drift lo: X={lo.OverallConversion:F4} inst={lo.InstantaneousCompositionA:F4} cum={lo.CumulativeCompositionA:F4} mayoFeed={mayoFeed:F4}");
            TestContext.WriteLine($"drift hi: X={hi.OverallConversion:F4} inst={hi.InstantaneousCompositionA:F4} cum={hi.CumulativeCompositionA:F4}");
            Assert.Multiple(() =>
            {
                Assert.That(lo.InstantaneousCompositionA, Is.EqualTo(mayoFeed).Within(0.01), "low conversion tracks the feed Mayo-Lewis");
                Assert.That(lo.InstantaneousCompositionA, Is.EqualTo(lo.CumulativeCompositionA).Within(0.01), "no drift yet at low conversion");
                Assert.That(hi.OverallConversion, Is.GreaterThan(0.6), "the high-conversion run must convert most monomer");
                Assert.That(System.Math.Abs(hi.InstantaneousCompositionA - hi.CumulativeCompositionA), Is.GreaterThan(0.02),
                            "the composition must drift by high conversion");
            });
        }

        [Test]
        public void PolydispersityStartsAtTheCombinationLimitAndBroadens()
        {
            // Instantaneously the combination limit is 1.5; the batch accumulates chains formed under changing
            // conditions, so the cumulative polydispersity rises above 1.5 as conversion builds.
            var k = CopolymerKinetics.StyreneMMA();     // termination all by combination
            k.AtrMA = 0.0; k.AtrMB = 0.0;
            var lo = CopolymerPFR.Solve(k, 333.15, 50.0, 4.0, 4.0, 0.02);
            var hi = CopolymerPFR.Solve(k, 333.15, 30000.0, 4.0, 4.0, 0.02);
            TestContext.WriteLine($"pdi lo: X={lo.OverallConversion:F4} PDI={lo.PDI:F4}");
            TestContext.WriteLine($"pdi hi: X={hi.OverallConversion:F4} PDI={hi.PDI:F4}");
            Assert.Multiple(() =>
            {
                Assert.That(lo.PDI, Is.EqualTo(1.5).Within(0.03), "low conversion sits at the combination limit");
                Assert.That(hi.PDI, Is.GreaterThan(lo.PDI + 0.05), "the cumulative distribution broadens with conversion");
            });
        }

        private static double CopolymerPFRMayo(double fA, double rA, double rB)
        {
            double fB = 1.0 - fA;
            return (rA * fA * fA + fA * fB) / (rA * fA * fA + 2.0 * fA * fB + rB * fB * fB);
        }
    }
}
