//    The shell-and-tube exchanger's fouling-factor mode, which takes the outlet temperatures as
//    given and works back to the fouling that would produce them.
//
//    This is the second report on https://github.com/DanWBR/dwsim/issues/1106: rate an exchanger,
//    switch the mode to fouling factor without touching anything else, and the outlets jump to
//    values that have nothing to do with the specification. The mode never computed the outlet
//    enthalpies, so they stayed at zero, and the pressure-enthalpy flash that closes the routine
//    read that zero back as a temperature. It never computed the duty either: Q kept whatever the
//    previous run had left on the object.

using System;
using System.Linq;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class ShellAndTubeFoulingTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        [Test]
        public void SwitchingToFoulingFactorKeepsTheOutletsItWasGiven()
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Water");

            var pp = new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage { Flowsheet = fs };
            fs.AddPropertyPackage(pp);

            var hotIn = Stream(fs, pp, "hot in", 10.0, 500000.0, 418.15);
            var coldIn = Stream(fs, pp, "cold in", 10.0, 500000.0, 303.15);
            var hotOut = Stream(fs, pp, "hot out", 0.0, 0.0, 0.0);
            var coldOut = Stream(fs, pp, "cold out", 0.0, 0.0, 0.0);

            var hxObj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.HeatExchanger, 0, 0, "HX");
            var hx = (DWSIM.UnitOperations.UnitOperations.HeatExchanger)fs.SimulationObjects[hxObj.Name];
            hx.SetFlowsheet(fs);
            hx.PropertyPackage = pp;

            fs.ConnectObjects(hotIn.GraphicObject, hx.GraphicObject, 0, 0);
            fs.ConnectObjects(coldIn.GraphicObject, hx.GraphicObject, 0, 1);
            fs.ConnectObjects(hx.GraphicObject, hotOut.GraphicObject, 0, 0);
            fs.ConnectObjects(hx.GraphicObject, coldOut.GraphicObject, 1, 0);

            hx.CalculationMode =
                DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode.ShellandTube_Rating;

            var errors = fs.SolveFlowsheet2();
            Assert.That(errors, Is.Empty,
                        "rating: " + string.Join("; ", errors.Select(e => e.Message)));

            var ratedCold = coldOut.GetTemperature();
            var ratedHot = hotOut.GetTemperature();
            var ratedQ = hx.Q.GetValueOrDefault();

            TestContext.WriteLine("rating   cold out {0:F4} K   hot out {1:F4} K   Q {2:F3} kW",
                                  ratedCold, ratedHot, ratedQ);

            Assert.That(ratedCold, Is.GreaterThan(303.15).And.LessThan(418.15));
            Assert.That(ratedHot, Is.GreaterThan(303.15).And.LessThan(418.15));

            // The switch the reporter made: same inputs, different mode. The outlets the rating
            // produced are now the specification, so the answer has to stay where it was.
            hx.CalculationMode =
                DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor;

            errors = fs.SolveFlowsheet2();
            Assert.That(errors, Is.Empty,
                        "fouling: " + string.Join("; ", errors.Select(e => e.Message)));

            TestContext.WriteLine("fouling  cold out {0:F4} K   hot out {1:F4} K   Q {2:F3} kW   Rf {3:G6}",
                                  coldOut.GetTemperature(), hotOut.GetTemperature(),
                                  hx.Q.GetValueOrDefault(), hx.STProperties.OverallFoulingFactor);

            Assert.That(coldOut.GetTemperature(), Is.EqualTo(ratedCold).Within(0.5),
                        "the cold outlet moved when only the calculation mode changed");
            Assert.That(hotOut.GetTemperature(), Is.EqualTo(ratedHot).Within(0.5),
                        "the hot outlet moved when only the calculation mode changed");

            Assert.That(hx.Q.GetValueOrDefault(), Is.EqualTo(ratedQ).Within(0.01 * ratedQ),
                        "the duty was not recomputed from the specified outlets");

            // The rating run carried no fouling, so working backwards from its own answer has to
            // give none either.
            Assert.That(hx.STProperties.OverallFoulingFactor, Is.EqualTo(0.0).Within(1e-5));
        }

        private static DWSIM.Thermodynamics.Streams.MaterialStream Stream(
            DWSIM.DynamicRunner.Flowsheet fs,
            DWSIM.Thermodynamics.PropertyPackages.PropertyPackage pp,
            string tag, double w, double p, double t)
        {
            var obj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, tag);

            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.PropertyPackage = pp;
            ms.AssignSelfToPP();

            if (w > 0.0)
            {
                ms.SetMassFlow(w);
                ms.SetPressure(p);
                ms.SetTemperature(t);
                ms.SetOverallComposition(new[] { 1.0 });
            }

            return ms;
        }
    }
}
