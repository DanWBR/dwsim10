//    The IAPWS-IF97 steam tables outside the range they cover, and what a heat exchanger does when
//    the stream on one of its sides is on them.
//
//    This is https://github.com/DanWBR/dwsim/issues/1106. Water streams on the steam tables made a
//    heat exchanger stop matching its specified outlet temperature. The exchanger bounds its duty
//    by asking what the cold stream would hold at the hot inlet temperature, which for a combustion
//    gas is well over 2000 K; the correlations there returned an enthalpy of -43000 kJ/kg, the
//    bound came out negative, and the guard rejected a duty that was perfectly reachable.

using System;
using System.Linq;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class SteamTableRangeTests
    {
        private const double P = 19350000.0;

        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.DynamicRunner.Flowsheet WaterFlowsheet()
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Water");
            return fs;
        }

        private static DWSIM.Thermodynamics.Streams.MaterialStream Stream(
            DWSIM.DynamicRunner.Flowsheet fs,
            DWSIM.Thermodynamics.PropertyPackages.PropertyPackage pp, string tag)
        {
            var obj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, tag);

            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.PropertyPackage = pp;
            ms.AssignSelfToPP();
            return ms;
        }

        [TestCase(400.0)]
        [TestCase(814.0)]
        [TestCase(1073.0)]
        public void TheSteamTablesAnswerInsideTheirRange(double temperature)
        {
            var fs = WaterFlowsheet();
            var ms = Stream(fs, new DWSIM.Thermodynamics.PropertyPackages.SteamTablesPropertyPackage
            {
                Flowsheet = fs
            }, "s");

            ms.SetMassFlow(1.0);
            ms.SetPressure(P);
            ms.SetTemperature(temperature);
            ms.SetFlashSpec("PT");
            ms.Calculate();

            var h = ms.GetMassEnthalpy();

            // Water at these conditions is between a few hundred and a few thousand kJ/kg, and
            // rising with temperature. The number that started this was -43000.
            Assert.That(h, Is.GreaterThan(0.0).And.LessThan(10000.0),
                        $"enthalpy at {temperature} K came back as {h} kJ/kg");
        }

        [TestCase(1500.0)]
        [TestCase(2000.0)]
        [TestCase(2352.0)]
        public void TheSteamTablesRefuseAboveTheirRange(double temperature)
        {
            var fs = WaterFlowsheet();
            var ms = Stream(fs, new DWSIM.Thermodynamics.PropertyPackages.SteamTablesPropertyPackage
            {
                Flowsheet = fs
            }, "s");

            ms.SetMassFlow(1.0);
            ms.SetPressure(P);
            ms.SetTemperature(temperature);
            ms.SetFlashSpec("PT");

            // Refusing is the point: these used to return a number, and a wrong one.
            Assert.That(() => ms.Calculate(), Throws.Exception);
        }

        /// <summary>
        /// A pressure-enthalpy flash on water: the state is well inside the correlations' range,
        /// but the flash brackets the temperature to find it. It must bracket only inside the range,
        /// or the bracket's far end lands where the correlations refuse and the whole flash throws.
        /// This is the path every heater, cooler and valve downstream of a water stream takes.
        /// </summary>
        [TestCase(560.0)]
        [TestCase(650.0)]
        [TestCase(900.0)]
        public void ThePressureEnthalpyFlashSolvesInsideTheRange(double temperature)
        {
            var fs = WaterFlowsheet();
            var ms = Stream(fs, new DWSIM.Thermodynamics.PropertyPackages.SteamTablesPropertyPackage
            {
                Flowsheet = fs
            }, "s");

            ms.SetMassFlow(1.0);
            ms.SetPressure(P);
            ms.SetTemperature(temperature);
            ms.SetFlashSpec("PT");
            ms.Calculate();
            var h = ms.GetMassEnthalpy();

            // Same state, now given by pressure and enthalpy.
            ms.SetFlashSpec("PH");
            ms.SetMassEnthalpy(h);

            Assert.That(() => ms.Calculate(), Throws.Nothing,
                        $"the PH flash threw finding {temperature} K");
            Assert.That(ms.GetTemperature(), Is.EqualTo(temperature).Within(1.0),
                        "the PH flash did not recover the temperature it was given");
        }

        /// <summary>
        /// A combustion gas at 2300 K heating water on the steam tables, with the cold outlet
        /// specified. The exchanger has to reach that outlet even though the cold side's property
        /// package cannot be evaluated anywhere near the hot inlet temperature.
        /// </summary>
        [Test]
        public void AnExchangerReachesItsSpecifiedOutletWithWaterOnTheSteamTables()
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Water");
            fs.AddCompound("Nitrogen");

            var pr = new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage { Flowsheet = fs };
            var steam = new DWSIM.Thermodynamics.PropertyPackages.SteamTablesPropertyPackage { Flowsheet = fs };
            fs.AddPropertyPackage(pr);
            fs.AddPropertyPackage(steam);

            var hotIn = Stream(fs, pr, "hot in");
            hotIn.SetMassFlow(100.0);
            hotIn.SetPressure(101325.0);
            hotIn.SetTemperature(2300.0);
            hotIn.SetOverallComposition(new[] { 0.0, 1.0 });

            var coldIn = Stream(fs, steam, "cold in");
            coldIn.SetMassFlow(20.0);
            coldIn.SetPressure(P);
            coldIn.SetTemperature(560.0);
            coldIn.SetOverallComposition(new[] { 1.0, 0.0 });

            var hotOut = Stream(fs, pr, "hot out");
            var coldOut = Stream(fs, steam, "cold out");

            var hxObj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.HeatExchanger, 0, 0, "HX");
            var hx = (DWSIM.UnitOperations.UnitOperations.HeatExchanger)fs.SimulationObjects[hxObj.Name];
            hx.SetFlowsheet(fs);
            hx.PropertyPackage = pr;

            fs.ConnectObjects(hotIn.GraphicObject, hx.GraphicObject, 0, 0);
            fs.ConnectObjects(coldIn.GraphicObject, hx.GraphicObject, 0, 1);
            fs.ConnectObjects(hx.GraphicObject, hotOut.GraphicObject, 0, 0);
            fs.ConnectObjects(hx.GraphicObject, coldOut.GraphicObject, 1, 0);

            hx.CalculationMode = DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode.CalcTempHotOut;
            hx.ColdSideOutletTemperature = 800.0;
            hx.Area = 100.0;

            var errors = fs.SolveFlowsheet2();

            Assert.That(errors, Is.Empty,
                        "the solver reported: " + string.Join("; ", errors.Select(e => e.Message)));

            Assert.That(coldOut.GetTemperature(), Is.EqualTo(800.0).Within(0.01),
                        "the cold outlet did not reach the temperature it was given");

            // The bound the guard uses has to be a real one, not a negative number produced by
            // evaluating water at 2300 K.
            Assert.That(hx.MaxHeatExchange, Is.GreaterThan(hx.Q));
        }
    }
}
