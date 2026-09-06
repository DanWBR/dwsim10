using NUnit.Framework;
using DWSIM.Thermodynamics.Polymers;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// Phase 1 of the free-radical polymerization reactor: the standalone method-of-moments CSTR solver.
    /// Validated against results that do not depend on the factor-of-two termination convention: the exact
    /// theoretical polydispersity limits (1.5 for pure combination, 2.0 for pure disproportionation, in the
    /// long-chain limit), an internal mass balance (monomer units consumed equal monomer units leaving as
    /// dead polymer), and a bulk-styrene benchmark giving physically sensible conversion, Mn and PDI.
    /// </summary>
    [TestFixture]
    public class FreeRadicalCSTRTests
    {
        // A long-chain kinetics set with no transfer, so the polydispersity reaches its theoretical limit.
        // Very low initiator makes the kinetic chain length large (alpha -> 1).
        private static FreeRadicalKinetics NoTransfer(bool combination)
        {
            var k = FreeRadicalKinetics.StyreneAIBN();
            k.AtrM = 0.0; k.AtrS = 0.0;                 // remove chain transfer
            if (combination) { k.Atc = 1.255e9; k.Etc = 8000.0; k.Atd = 0.0; }
            else { k.Atc = 0.0; k.Atd = 1.255e9; k.Etd = 8000.0; }
            return k;
        }

        [Test]
        public void PureCombinationApproachesPDI_1_5()
        {
            var r = FreeRadicalCSTR.Solve(NoTransfer(combination: true), T: 333.15, ResidenceTime: 3600.0,
                                          MonomerFeed: 8.7, InitiatorFeed: 1.0e-5);
            Assert.That(r.Converged, Is.True);
            TestContext.WriteLine($"combination: X={r.Conversion:F4} Mn={r.Mn:F0} PDI={r.PDI:F4}");
            Assert.That(r.PDI, Is.EqualTo(1.5).Within(0.02),
                        "termination by combination must give a polydispersity of 3/2 for long chains");
        }

        [Test]
        public void PureDisproportionationApproachesPDI_2_0()
        {
            var r = FreeRadicalCSTR.Solve(NoTransfer(combination: false), T: 333.15, ResidenceTime: 3600.0,
                                          MonomerFeed: 8.7, InitiatorFeed: 1.0e-5);
            Assert.That(r.Converged, Is.True);
            TestContext.WriteLine($"disproportionation: X={r.Conversion:F4} Mn={r.Mn:F0} PDI={r.PDI:F4}");
            Assert.That(r.PDI, Is.EqualTo(2.0).Within(0.02),
                        "termination by disproportionation must give a polydispersity of 2 for long chains");
        }

        [Test]
        public void MonomerAndPolymerMassBalance()
        {
            // The first dead-chain moment (moles of monomer units in outgoing dead polymer per litre) must
            // equal the monomer consumed - nothing is lost, and the live-radical population is negligible.
            double Min = 8.7;
            var r = FreeRadicalCSTR.Solve(FreeRadicalKinetics.StyreneAIBN(), T: 333.15, ResidenceTime: 3600.0,
                                          MonomerFeed: Min, InitiatorFeed: 0.02);
            Assert.That(r.Converged, Is.True);
            double consumed = Min - r.MonomerConc;         // mol/L of monomer reacted
            double inPolymer = r.Lambda1;                    // mol/L of monomer units in dead chains
            TestContext.WriteLine($"consumed={consumed:F5} in polymer={inPolymer:F5} rel diff={(inPolymer - consumed) / consumed:E2}");
            Assert.That(inPolymer, Is.EqualTo(consumed).Within(0.02 * consumed),
                        "monomer units in the outgoing polymer must balance the monomer consumed");
        }

        [Test]
        public void BulkStyreneBenchmarkIsPhysical()
        {
            // AIBN-initiated bulk styrene at 60 C, 1 h residence, 0.02 M initiator.
            var r = FreeRadicalCSTR.Solve(FreeRadicalKinetics.StyreneAIBN(), T: 333.15, ResidenceTime: 3600.0,
                                          MonomerFeed: 8.7, InitiatorFeed: 0.02);
            TestContext.WriteLine($"styrene: X={r.Conversion:F4} Mn={r.Mn:F0} Mw={r.Mw:F0} PDI={r.PDI:F4} " +
                                  $"nu={r.KineticChainLength:F0} Rp={r.Rp:E3}");
            Assert.That(r.Converged, Is.True);
            Assert.That(r.Conversion, Is.InRange(0.02, 0.10), "conversion should be a few percent in one hour");
            Assert.That(r.Mn, Is.InRange(5.0e4, 3.0e5), "number-average molar mass should be of order 1e5");
            Assert.That(r.Mw, Is.GreaterThan(r.Mn), "weight-average must exceed number-average");
            // Styrene terminates mainly by combination, with some transfer to monomer, so PDI sits near 1.5-1.7.
            Assert.That(r.PDI, Is.InRange(1.45, 1.75), "styrene polydispersity should be near the combination limit");
        }

        [Test]
        public void NoInitiatorGivesNoReaction()
        {
            var r = FreeRadicalCSTR.Solve(FreeRadicalKinetics.StyreneAIBN(), T: 333.15, ResidenceTime: 3600.0,
                                          MonomerFeed: 8.7, InitiatorFeed: 0.0);
            Assert.That(r.Converged, Is.True);
            Assert.That(r.Conversion, Is.EqualTo(0.0).Within(1e-12), "no initiator means no polymerization");
        }
    }
}
