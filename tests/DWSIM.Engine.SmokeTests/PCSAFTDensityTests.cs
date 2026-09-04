//    PC-SAFT density-root robustness.
//
//    The reduced density (packing fraction) is found by bracketing the roots of P - Pcalc(eta) over
//    the physical range (0, ~0.74) and choosing the liquid (highest-eta) or gas (lowest-eta) root.
//    The previous simplex-on-squared-objective could slide onto a spurious low-density root or wander
//    past close packing into the NaN region - which made a high segment-number polymer's log fugacity
//    coefficient NaN for polymer-rich compositions. These guard both the small-molecule behaviour and
//    the polymer finiteness.

using System;
using System.Linq;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class PCSAFTDensityTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage Package(
            Action<DWSIM.DynamicRunner.Flowsheet> addCompounds)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            addCompounds(fs);
            var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "feed");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.SetPropertyPackage(pp);
            pp.CurrentMaterialStream = ms;
            return pp;
        }

        /// <summary>
        /// A small-molecule PC-SAFT flash stays physical: ethane/n-pentane at 350 K condenses
        /// monotonically as pressure rises and the vapour keeps getting richer in the light component.
        /// </summary>
        [Test]
        public void EthaneNPentaneVLEIsPhysical()
        {
            var pp = Package(fs => { fs.AddCompound("Ethane"); fs.AddCompound("N-pentane"); });
            var flash = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.NestedLoops();

            double prevL = -1.0, prevY = -1.0;
            foreach (var Pbar in new[] { 10.0, 20.0, 30.0 })
            {
                var r = (object[])flash.Flash_PT(new[] { 0.5, 0.5 }, Pbar * 1e5, 350.0, pp);
                double L = Convert.ToDouble(r[0]), V = Convert.ToDouble(r[1]);
                var y = (double[])r[3];
                TestContext.WriteLine($"{Pbar:F0} bar: L={L:F3} V={V:F3} y(C2)={y[0]:F3}");

                Assert.That(L + V, Is.EqualTo(1.0).Within(1e-6), "phase fractions must sum to one");
                Assert.That(L, Is.GreaterThan(prevL), "liquid fraction must grow with pressure");
                Assert.That(y[0], Is.GreaterThan(0.5), "vapour must be enriched in the lighter ethane");
                Assert.That(y[0], Is.GreaterThan(prevY), "vapour must get richer in ethane with pressure");
                prevL = L; prevY = y[0];
            }
        }

        /// <summary>
        /// A polymer's log fugacity coefficient must stay finite across the whole composition range,
        /// pure solvent to pure polymer. Its magnitude is on the order of 1e3 (proportional to the
        /// segment number), so the coefficient itself underflows to zero - the density root-finder must
        /// still return a real value, especially for polymer-rich compositions.
        /// </summary>
        [Test]
        public void PolymerLogFugacityIsFiniteAcrossComposition()
        {
            var pp = Package(fs =>
            {
                fs.AddCompound("N-pentane");
                var poly = new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
                {
                    Name = "Polypropylene",
                    CAS_Number = "9003-07-0",
                    Formula = "(C3H6)n",
                    Molar_Weight = 50400.0,
                    Critical_Temperature = 1200.0,
                    Critical_Pressure = 5.0e5,
                    Acentric_Factor = 0.5,
                    Normal_Boiling_Point = 800.0,
                    IsHYPO = 1
                };
                fs.Options.SelectedComponents.Add(poly.Name, poly);
            });

            double T = 450.15, P = 35e5;
            foreach (var xpp in new[] { 0.0, 1e-4, 1e-2, 0.1, 0.5, 0.9, 1.0 })
            {
                var w = new[] { 1.0 - xpp, xpp };
                var ln = pp.DW_CalcLnFugCoeff(w, T, P, DWSIM.Thermodynamics.PropertyPackages.State.Liquid);
                TestContext.WriteLine($"x(PP)={xpp:E2}  lnPhi = [{ln[0]:F3}, {ln[1]:F2}]");
                Assert.That(double.IsNaN(ln[0]) || double.IsNaN(ln[1]), Is.False, $"NaN log fugacity at x(PP)={xpp:E2}");
                Assert.That(ln[1], Is.LessThan(0.0).And.GreaterThan(-5000.0), $"polymer lnPhi out of range at x(PP)={xpp:E2}");
            }
        }

        /// <summary>
        /// The polymer liquid-liquid split must come out of the ordinary Simple LLE flash with no manual
        /// phase seed. Inside the miscibility gap (polypropylene in n-pentane at 460 K / 40 bar) the flash
        /// has to demix into a nearly pure solvent phase and a polymer-rich one, driven only by the EoS
        /// spinodal seed the flash builds for itself; without it the iteration collapses onto the feed and
        /// reports a single phase. The window sits at extreme dilution (polymer mole fractions ~1e-5..1e-3)
        /// and the feed is metastable, outside the spinodal, which the activity-model seeding cannot reach.
        /// </summary>
        [Test]
        public void PolymerLiquidLiquidSplitIsReachableUnseeded()
        {
            var pp = PolypropyleneInNPentane(out var z);
            var flash = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.SimpleLLE();
            var r = (object[])flash.Flash_PT(z, 40e5, 460.15, pp);

            double L1 = Convert.ToDouble(r[0]), L2 = Convert.ToDouble(r[5]);
            double w1 = MassFractionPP(((double[])r[2])[1]);
            double w2 = MassFractionPP(((double[])r[6])[1]);
            TestContext.WriteLine($"L1={L1:F3} L2={L2:F3}  w1(PP)={w1:F4} w2(PP)={w2:F4}");

            Assert.That(Math.Min(L1, L2), Is.GreaterThan(0.01), "two liquid phases must be present");
            Assert.That(Math.Max(w1, w2), Is.GreaterThan(0.15), "one phase must be polymer-rich");
            Assert.That(Math.Min(w1, w2), Is.LessThan(0.01), "the other phase must be nearly pure solvent");
        }

        /// <summary>
        /// The same split must come out of the Nested Loops (VLLE) flash that a MaterialStream uses, not only
        /// the dedicated Simple LLE flash. For a Gibbs-minimization package the VLLE flash splits the liquid
        /// first (seeded from the EoS spinodal) instead of running a vapour flash the non-volatile polymer
        /// cannot satisfy, which previously threw "Error calculating amount of the vapor phase".
        /// </summary>
        [Test]
        public void PolymerLiquidLiquidSplitIsReachableViaVLLEFlash()
        {
            var pp = PolypropyleneInNPentane(out var z);
            var flash = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.NestedLoops3PV3();
            var r = (object[])flash.Flash_PT(z, 40e5, 460.15, pp);

            double L1 = Convert.ToDouble(r[0]), V = Convert.ToDouble(r[1]), L2 = Convert.ToDouble(r[5]);
            double w1 = MassFractionPP(((double[])r[2])[1]);
            double w2 = MassFractionPP(((double[])r[6])[1]);
            TestContext.WriteLine($"L1={L1:F3} V={V:F3} L2={L2:F3}  w1(PP)={w1:F4} w2(PP)={w2:F4}");

            Assert.That(V, Is.EqualTo(0.0).Within(1e-6), "no vapour forms at this pressure");
            Assert.That(Math.Min(L1, L2), Is.GreaterThan(0.01), "two liquid phases must be present");
            Assert.That(Math.Max(w1, w2), Is.GreaterThan(0.15), "one phase must be polymer-rich");
            Assert.That(Math.Min(w1, w2), Is.LessThan(0.01), "the other phase must be nearly pure solvent");
        }

        // A polypropylene (Mn = 50.4 kg/mol) pseudo-compound dissolved in n-pentane, with a feed at 20 wt%
        // polymer - well inside the miscibility gap, though by mole the polymer is only a trace (~4e-4).
        private static DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage PolypropyleneInNPentane(out double[] feed)
        {
            var pp = Package(fs =>
            {
                fs.AddCompound("N-pentane");
                var poly = new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
                {
                    Name = "Polypropylene",
                    CAS_Number = "9003-07-0",
                    Formula = "(C3H6)n",
                    Molar_Weight = 50400.0,
                    Critical_Temperature = 1200.0,
                    Critical_Pressure = 5.0e5,
                    Acentric_Factor = 0.5,
                    Normal_Boiling_Point = 800.0,
                    IsHYPO = 1
                };
                fs.Options.SelectedComponents.Add(poly.Name, poly);
            });

            const double mwP = 50400.0, mwC5 = 72.15, wFeed = 0.20;
            double nPP = wFeed / mwP, nC5 = (1.0 - wFeed) / mwC5, tot = nPP + nC5;
            feed = new[] { nC5 / tot, nPP / tot };
            return pp;
        }

        // Mass fraction of the polymer from its mole fraction (n-pentane 72.15, polypropylene 50400 g/mol).
        private static double MassFractionPP(double xPP) => xPP * 50400.0 / (xPP * 50400.0 + (1.0 - xPP) * 72.15);
    }
}
