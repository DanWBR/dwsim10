using System.Collections.Generic;
using NUnit.Framework;
using DWSIM.Thermodynamics.Polymers;
using DWSIM.Thermodynamics.BaseClasses;

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
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

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
        public void ReactorDistributionFlowsIntoPCSAFT()
        {
            // Phase 2 end to end: reactor result -> Mn/PDI -> BuildCuts -> outlet stream -> PC-SAFT flash.
            // 1. Solve a bulk styrene CSTR at a residence time giving a substantial polymer fraction.
            var res = FreeRadicalCSTR.Solve(FreeRadicalKinetics.StyreneAIBN(), T: 333.15, ResidenceTime: 21600.0,
                                            MonomerFeed: 8.7, InitiatorFeed: 0.02);
            Assert.That(res.Converged, Is.True);
            TestContext.WriteLine($"reactor: X={res.Conversion:F3} Mn={res.Mn:F0} PDI={res.PDI:F3}");

            // 2. Base polystyrene: CAS matches pcsaft.dat so PC-SAFT reuses its segment parameters.
            var basePS = new ConstantProperties
            {
                Name = "Polystyrene", CAS_Number = "9003-53-6", Formula = "(C8H8)n", Molar_Weight = res.Mn,
                Critical_Temperature = 1200.0, Critical_Pressure = 5.0e5, Acentric_Factor = 0.5,
                Normal_Boiling_Point = 800.0, IsHYPO = 1, CurrentDB = "User", OriginalDB = "User"
            };
            List<ConstantProperties> cuts = null;
            double[] x = FreeRadicalCSTR.ExpandOutlet(res, basePS, 5, PolymerDistribution.SchulzZimm, ref cuts);

            // The expansion must preserve the reactor's number-average molar mass.
            double num = 0.0, den = 0.0;
            for (int k = 0; k < cuts.Count; k++) { num += x[k + 1] * cuts[k].Molar_Weight; den += x[k + 1]; }
            Assert.That(num / den, Is.EqualTo(res.Mn).Within(res.Mn * 1e-3), "the cuts must preserve the reactor Mn");

            // 3. Flowsheet: ethylbenzene stands in for the residual styrene monomer (styrene has no shipped
            //    PC-SAFT parameters; ethylbenzene is its saturated analogue and flashes cleanly), plus the cuts.
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Ethylbenzene");
            foreach (var c in cuts) fs.Options.SelectedComponents.Add(c.Name, c);
            var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
            var o = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "outlet");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[o.Name];
            ms.SetFlowsheet(fs); ms.PropertyPackage = pp; ms.AssignSelfToPP(); pp.CurrentMaterialStream = ms;
            ms.SetMassFlow(1.0);
            ms.SetOverallComposition(x);

            // 4. Devolatilize the outlet (470 K, 0.15 bar): strip residual monomer, keep the polymer liquid.
            ms.SetTemperature(470.0); ms.SetPressure(15000.0); ms.SetFlashSpec("PT");
            ms.Calculate();

            double vf = ms.Phases[2].Properties.molarfraction.GetValueOrDefault();
            TestContext.WriteLine($"devol: VF={vf:F5}");

            // Cut-weighted number-average of the polymer left in the liquid.
            double lnum = 0.0, lden = 0.0, vmassPoly = 0.0;
            foreach (var c in cuts)
            {
                double xl = ms.Phases[3].Compounds[c.Name].MoleFraction.GetValueOrDefault();
                lnum += xl * c.Molar_Weight; lden += xl;
                vmassPoly += ms.Phases[2].Compounds[c.Name].MassFraction.GetValueOrDefault();
            }
            double MnLiquid = lden > 0 ? lnum / lden : 0.0;

            Assert.Multiple(() =>
            {
                Assert.That(vf, Is.GreaterThan(0.5).And.LessThan(1.0), "residual monomer devolatilizes, but not to all-vapour");
                Assert.That(vmassPoly, Is.LessThan(1.0e-6), "the polymer distribution must stay out of the vapour");
                Assert.That(MnLiquid, Is.EqualTo(res.Mn).Within(res.Mn * 0.05),
                            "PC-SAFT resolves the distribution and preserves its Mn in the liquid");
            });
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
