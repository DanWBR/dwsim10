using NUnit.Framework;
using DWSIM.Thermodynamics.Polymers;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The standalone binary free-radical copolymer CSTR solver (terminal model + method of moments). Validated
    /// against results that do not depend on the numerical details: the instantaneous copolymer composition
    /// equals the Mayo-Lewis equation (ideal copolymerization tracks the feed, an azeotropic system holds its
    /// composition), the polydispersity reaches its combination limit, and a styrene/MMA benchmark gives a
    /// physically sensible composition and molar mass.
    /// </summary>
    [TestFixture]
    public class CopolymerCSTRTests
    {
        [Test]
        public void IdealCopolymerizationTracksTheMonomerComposition()
        {
            // rA = rB = 1 (ideal): the copolymer composition equals the monomer composition, F_A = f_A.
            var k = CopolymerKinetics.StyreneMMA();
            k.ReactivityA = 1.0; k.ReactivityB = 1.0;
            var r = CopolymerCSTR.Solve(k, T: 333.15, ResidenceTime: 3600.0, MonomerAFeed: 4.0, MonomerBFeed: 4.0, InitiatorFeed: 0.02);
            Assert.That(r.Converged, Is.True);
            double fA = r.MonomerAConc / (r.MonomerAConc + r.MonomerBConc);
            TestContext.WriteLine($"ideal: f_A={fA:F4} F_A={r.CopolymerCompositionA:F4}");
            Assert.That(r.CopolymerCompositionA, Is.EqualTo(fA).Within(1e-6), "an ideal copolymer follows the monomer composition");
        }

        [Test]
        public void AzeotropicCopolymerizationHoldsItsComposition()
        {
            // Symmetric reactivity ratios (rA = rB = 0.5) and equal propagation, equal feed: the azeotrope is at
            // f_A = 0.5, so the copolymer comes out at F_A = 0.5.
            var k = CopolymerKinetics.StyreneMMA();
            k.ReactivityA = 0.5; k.ReactivityB = 0.5;
            k.ApBB = k.ApAA; k.EpBB = k.EpAA;              // make B propagate like A (symmetric)
            var r = CopolymerCSTR.Solve(k, T: 333.15, ResidenceTime: 3600.0, MonomerAFeed: 4.0, MonomerBFeed: 4.0, InitiatorFeed: 0.02);
            Assert.That(r.Converged, Is.True);
            TestContext.WriteLine($"azeotrope: F_A={r.CopolymerCompositionA:F4}");
            Assert.That(r.CopolymerCompositionA, Is.EqualTo(0.5).Within(0.005), "an azeotropic system copolymerizes at F_A = 0.5");
        }

        [Test]
        public void CompositionEqualsTheMayoLewisEquation()
        {
            var k = CopolymerKinetics.StyreneMMA();
            var r = CopolymerCSTR.Solve(k, T: 333.15, ResidenceTime: 3600.0, MonomerAFeed: 5.0, MonomerBFeed: 3.0, InitiatorFeed: 0.02);
            Assert.That(r.Converged, Is.True);
            double fA = r.MonomerAConc / (r.MonomerAConc + r.MonomerBConc);
            double ml = CopolymerCSTR.MayoLewis(fA, k.ReactivityA, k.ReactivityB);
            TestContext.WriteLine($"mayo-lewis: f_A={fA:F4} F_A={r.CopolymerCompositionA:F4} ML={ml:F4}");
            Assert.That(r.CopolymerCompositionA, Is.EqualTo(ml).Within(1e-6), "the solved composition must equal Mayo-Lewis");
        }

        [Test]
        public void PureCombinationApproachesPDI_1_5()
        {
            var k = CopolymerKinetics.StyreneMMA();     // termination all by combination (Atd = 0)
            var r = CopolymerCSTR.Solve(k, T: 333.15, ResidenceTime: 3600.0, MonomerAFeed: 4.0, MonomerBFeed: 4.0, InitiatorFeed: 1.0e-5);
            Assert.That(r.Converged, Is.True);
            TestContext.WriteLine($"combination: X={r.OverallConversion:F4} Mn={r.Mn:F0} PDI={r.PDI:F4}");
            Assert.That(r.PDI, Is.EqualTo(1.5).Within(0.02), "combination termination gives a polydispersity of 3/2 for long chains");
        }

        [Test]
        public void GelEffectAcceleratesTheCopolymerization()
        {
            // The gel effect drives auto-acceleration in the copolymer too: an inactive model reproduces the
            // baseline, and diffusion-limited termination raises both conversion and molar mass.
            var k = CopolymerKinetics.StyreneMMA();
            var baseline = CopolymerCSTR.Solve(k, 333.15, 7200.0, 4.0, 4.0, 0.05);
            var none = CopolymerCSTR.Solve(k, 333.15, 7200.0, 4.0, 4.0, 0.05, new GelEffect());
            var gelled = CopolymerCSTR.Solve(k, 333.15, 7200.0, 4.0, 4.0, 0.05,
                                             new GelEffect { ModelType = GelModelType.Exponential, GtC1 = 2.0, GtC2 = 4.0 });
            TestContext.WriteLine($"copo no gel: X={baseline.OverallConversion:F4} Mn={baseline.Mn:F0}");
            TestContext.WriteLine($"copo gel:    X={gelled.OverallConversion:F4} Mn={gelled.Mn:F0} F_A={gelled.CopolymerCompositionA:F4}");
            Assert.Multiple(() =>
            {
                Assert.That(none.OverallConversion, Is.EqualTo(baseline.OverallConversion).Within(1e-12), "inactive gel = baseline");
                Assert.That(none.Mn, Is.EqualTo(baseline.Mn).Within(1e-6), "inactive gel = baseline");
                Assert.That(gelled.Converged, Is.True);
                Assert.That(gelled.OverallConversion, Is.GreaterThan(baseline.OverallConversion), "the gel effect auto-accelerates");
                Assert.That(gelled.Mn, Is.GreaterThan(baseline.Mn), "slower termination lengthens the chains");
            });
        }

        [Test]
        public void StyreneMMABenchmarkIsPhysical()
        {
            var k = CopolymerKinetics.StyreneMMA();
            var r = CopolymerCSTR.Solve(k, T: 333.15, ResidenceTime: 3600.0, MonomerAFeed: 4.0, MonomerBFeed: 4.0, InitiatorFeed: 0.02);
            TestContext.WriteLine($"styrene/MMA: X={r.OverallConversion:F4} F_A(styrene)={r.CopolymerCompositionA:F4} " +
                                  $"Mn={r.Mn:F0} Mw={r.Mw:F0} PDI={r.PDI:F4}");
            Assert.Multiple(() =>
            {
                Assert.That(r.Converged, Is.True);
                Assert.That(r.OverallConversion, Is.InRange(0.01, 0.20), "a few percent conversion in one hour");
                // rA, rB both < 1: an alternating tendency keeps the copolymer near equimolar for a 50/50 feed.
                Assert.That(r.CopolymerCompositionA, Is.InRange(0.4, 0.6), "styrene fraction near equimolar for a 50/50 feed");
                Assert.That(r.Mn, Is.InRange(1.0e4, 5.0e5), "number-average molar mass of order 1e5");
                Assert.That(r.Mw, Is.GreaterThan(r.Mn), "weight-average exceeds number-average");
            });
        }
    }
}
