//    The dew curve of a heteroazeotropic binary.
//
//    This is https://github.com/DanWBR/dwsim/issues/1116. On benzene and water the dew curve of the
//    binary envelope dipped below the bubble line either side of the azeotrope, which no vapour of
//    that composition can do. A dew point is the highest temperature at which liquid appears, and
//    in a partially miscible system the dew equation has one root per liquid branch; successive
//    substitution reached the benzene-rich one, seven kelvin low. Both roots solve the equation.
//    What separates them is that the liquid of the lower one is not stable.

using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class HeteroazeotropeTests
    {
        private const double P = 101325.0;

        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.Thermodynamics.PropertyPackages.PropertyPackage BenzeneWater()
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Benzene");
            fs.AddCompound("Water");

            var pp = new DWSIM.Thermodynamics.PropertyPackages.UNIFACPropertyPackage { Flowsheet = fs };

            var obj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "feed");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.SetPropertyPackage(pp);
            pp.CurrentMaterialStream = ms;

            return pp;
        }

        /// <summary>
        /// The whole envelope: nowhere may the dew temperature fall below the bubble temperature at
        /// the same abscissa. Between the two liquid branches the bubble line is the invariant
        /// heteroazeotropic temperature, which the phase rule pins, so a dew point below it is a
        /// vapour that cannot exist.
        /// </summary>
        [Test]
        public void TheDewCurveNeverFallsBelowTheBubbleCurve()
        {
            var pp = BenzeneWater();

            object[] pars = { "T-x-y", P, 0.0, true, false, false, false, false, 0, 0, 40, 0.0, 1.0 };

            var r = (object[])pp.DW_ReturnBinaryEnvelope(pars, null);

            var x = ((ArrayList)r[0]).ToArray().Select(Convert.ToDouble).ToArray();
            var bubble = ((ArrayList)r[1]).ToArray().Select(Convert.ToDouble).ToArray();
            var dew = ((ArrayList)r[2]).ToArray().Select(Convert.ToDouble).ToArray();

            Assert.That(x, Has.Length.GreaterThan(20));

            var crossings = Enumerable.Range(0, x.Length)
                .Where(i => !double.IsNaN(dew[i]) && !double.IsNaN(bubble[i]) && dew[i] < bubble[i] - 1e-6)
                .Select(i => $"x1 = {x[i]:F4}: dew {dew[i]:F4} K below bubble {bubble[i]:F4} K")
                .ToList();

            Assert.That(crossings, Is.Empty, string.Join("; ", crossings));
        }

        /// <summary>
        /// And the shape of it on the water-rich side: approaching the azeotrope the dew
        /// temperature has to come down to it, not away from it.
        /// </summary>
        [Test]
        public void TheDewCurveDescendsTowardsTheAzeotrope()
        {
            var pp = BenzeneWater();

            var temperatures = new[] { 0.55, 0.60, 0.65, 0.70 }
                .Select(y => DewPoint(pp, y)).ToArray();

            for (int i = 0; i < temperatures.Length; i++)
            {
                TestContext.WriteLine("y(benzene) {0:F2} -> dew {1:F4} K", 0.55 + 0.05 * i, temperatures[i]);
            }

            for (int i = 1; i < temperatures.Length; i++)
            {
                Assert.That(temperatures[i], Is.LessThan(temperatures[i - 1]),
                            "the dew curve turned back up before the azeotrope");
            }

            // The heteroazeotrope of benzene and water at one atmosphere sits near 342.4 K, and
            // every one of these has to be above it.
            Assert.That(temperatures.Min(), Is.GreaterThan(342.37));
        }

        private static double DewPoint(DWSIM.Thermodynamics.PropertyPackages.PropertyPackage pp, double y)
        {
            var flash = (DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.FlashAlgorithm)
                pp.FlashBase.Clone();

            var r = (object[])flash.Flash_PV(new[] { y, 1 - y }, P, 1.0, 0.0, pp);

            return Convert.ToDouble(r[4]);
        }
    }
}
