//    The waste heat a water electrolyser puts into its outlet streams.
//
//    This is https://github.com/DanWBR/dwsim/issues/1119. WasteHeat is a power, in kW, and it was
//    added straight to a specific enthalpy, in kJ/kg, weighted only by a dimensionless mass
//    fraction. The size of the error was the numerical value of the mass flow, so a PEM stack
//    circulating water well past the stoichiometry to cool itself, which is how they are actually
//    run, came out with a temperature rise orders of magnitude too large.

using System;
using System.Linq;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class WaterElectrolyzerWasteHeatTests
    {
        private const double FeedTemperature = 298.15;

        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        /// <summary>
        /// The same stack, the same power, and ten times the water through it. The waste heat is
        /// unchanged, so it is spread over ten times the mass and the outlet has to warm by a
        /// tenth as much. That is the statement the dimensional error destroyed: it made the rise
        /// very nearly independent of the flow.
        /// </summary>
        [Test]
        public void TheTemperatureRiseFallsWithTheWaterFlow()
        {
            var small = Run(10.0);
            var large = Run(100.0);

            TestContext.WriteLine("10 kg/s : waste heat {0:F3} kW, oxygen outlet rises {1:F4} K",
                                  small.WasteHeat, small.Rise);
            TestContext.WriteLine("100 kg/s: waste heat {0:F3} kW, oxygen outlet rises {1:F4} K",
                                  large.WasteHeat, large.Rise);

            Assert.That(large.WasteHeat, Is.EqualTo(small.WasteHeat).Within(1e-9),
                        "the two runs have to differ only in the water flow");

            Assert.That(small.Rise / large.Rise, Is.EqualTo(10.0).Within(0.5),
                        "ten times the water did not give a tenth of the temperature rise");
        }

        /// <summary>
        /// And the size of it: three hundred kilowatts into ten kilograms a second of water is
        /// thirty kilojoules a kilogram, which for water is a little under seven kelvin.
        /// </summary>
        [Test]
        public void TheTemperatureRiseIsTheOneTheHeatCapacityGives()
        {
            var run = Run(10.0);

            var expected = run.WasteHeat / 10.0 / 4.18;

            Assert.That(run.Rise, Is.EqualTo(expected).Within(0.15 * expected),
                        $"{run.WasteHeat} kW over 10 kg/s should raise water by about {expected} K");
        }

        private readonly record struct Result(double WasteHeat, double Rise);

        private static Result Run(double waterFlow)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Water");
            fs.AddCompound("Hydrogen");
            fs.AddCompound("Oxygen");

            var pp = new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage { Flowsheet = fs };
            fs.AddPropertyPackage(pp);

            var water = Stream(fs, pp, "water");
            water.SetMassFlow(waterFlow);
            water.SetPressure(101325.0);
            water.SetTemperature(FeedTemperature);
            water.SetOverallComposition(new[] { 1.0, 0.0, 0.0 });

            var h2 = Stream(fs, pp, "hydrogen");
            var o2 = Stream(fs, pp, "oxygen");

            var powerObj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.EnergyStream, 0, 0, "power");
            var power = (DWSIM.UnitOperations.Streams.EnergyStream)fs.SimulationObjects[powerObj.Name];
            power.SetFlowsheet(fs);
            power.EnergyFlow = 1000.0;

            var weObj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.WaterElectrolyzer, 0, 0, "WE");
            var we = (DWSIM.UnitOperations.UnitOperations.WaterElectrolyzer)fs.SimulationObjects[weObj.Name];
            we.SetFlowsheet(fs);
            we.PropertyPackage = pp;
            we.InputEfficiency = 0.7;
            we.CreateConnectors();

            fs.ConnectObjects(water.GraphicObject, we.GraphicObject, 0, 0);
            fs.ConnectObjects(power.GraphicObject, we.GraphicObject, 0, 1);
            fs.ConnectObjects(we.GraphicObject, h2.GraphicObject, 0, 0);
            fs.ConnectObjects(we.GraphicObject, o2.GraphicObject, 1, 0);

            var errors = fs.SolveFlowsheet2();
            Assert.That(errors, Is.Empty, string.Join("; ", errors.Select(e => e.Message)));

            // The oxygen-rich outlet is where the unreacted water leaves, so it carries almost all
            // of the mass and shows the rise plainly.
            return new Result(we.WasteHeat, o2.GetTemperature() - FeedTemperature);
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
    }
}
