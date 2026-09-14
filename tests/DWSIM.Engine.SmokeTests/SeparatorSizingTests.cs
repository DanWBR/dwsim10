//    Gas-liquid separator sizing: reading the stream conditions off the flowsheet.
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using DWSIM.GlobalSettings;
using DWSIM.Thermodynamics.Streams;
using DWSIM.Thermodynamics.Utilities.Sizing;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class SeparatorSizingTests
    {
        [OneTimeSetUp]
        public void RegisterPropertyPackages()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.DynamicRunner.Flowsheet Load()
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
            {
                folder = Path.GetDirectoryName(folder);
            }

            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadFromXML(XDocument.Load(Path.Combine(folder, "tests", "flowsheets", "ThreePhaseSeparator.dwxml")));

            var errors = flowsheet.SolveFlowsheet2();
            Assert.That(errors, Is.Empty, "the sample did not solve");

            return flowsheet;
        }

        // The separator has six inlet ports and the drawing decides which one the feed lands on.
        // Reading the feed off the first port only left the utility with nothing to size whenever
        // the stream was drawn into another port. https://github.com/DanWBR/dwsim10/issues/62
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void TheInletIsFoundOnAnyPort(int port)
        {
            var flowsheet = Load();
            var vessel = flowsheet.SimulationObjects.Values.OfType<Vessel>().Single();
            var feed = vessel.GraphicObject.InputConnectors[0].AttachedConnector.AttachedFrom;

            if (port != 0)
            {
                flowsheet.DisconnectObjects(feed, vessel.GraphicObject);
                flowsheet.ConnectObjects(feed, vessel.GraphicObject, 0, port);
            }

            Assert.That(vessel.GraphicObject.InputConnectors[port].IsAttached, Is.True,
                        $"the feed was not moved to port {port}");

            var input = new SeparatorSizingInput();

            Assert.That(SeparatorSizing.ReadStreams(flowsheet, vessel, input), Is.True,
                        "the streams were not read from the separator");

            Assert.That(input.InletDensity, Is.GreaterThan(0.0));
            Assert.That(input.VaporDensity, Is.GreaterThan(0.0));
            Assert.That(input.LiquidDensity, Is.GreaterThan(0.0));
            Assert.That(input.VaporVolumetricFlow, Is.GreaterThan(0.0));
            Assert.That(input.LiquidVolumetricFlow, Is.GreaterThan(0.0));

            var res = SeparatorSizing.SizeVertical(input);

            Assert.That(res.Diameter, Is.GreaterThan(0.0));
            // the height is the larger of the aspect ratio and the liquid-holdup height, so it is at
            // least the length-to-diameter multiple of the diameter
            Assert.That(res.Length, Is.GreaterThanOrEqualTo(input.LengthToDiameter * res.Diameter - 1e-6));
            Assert.That(res.InletNozzle, Is.GreaterThan(0.0));
        }

        // A longer liquid residence time needs a bigger vertical vessel. The diameter is the larger of
        // the one the gas velocity asks for and the one that holds the liquid at the requested aspect
        // ratio, so both dimensions grow and the height stays at L/D times the diameter. The residence
        // time used to be ignored entirely for a vertical separator. issue #66
        [Test]
        public void TheVerticalVesselGrowsWithTheResidenceTime()
        {
            var flowsheet = Load();
            var vessel = flowsheet.SimulationObjects.Values.OfType<Vessel>().Single();

            var input = new SeparatorSizingInput();
            Assert.That(SeparatorSizing.ReadStreams(flowsheet, vessel, input), Is.True);

            input.ResidenceTime = 1.0;
            var shortHold = SeparatorSizing.SizeVertical(input);

            input.ResidenceTime = 120.0;
            var longHold = SeparatorSizing.SizeVertical(input);

            Assert.That(longHold.Diameter, Is.GreaterThan(shortHold.Diameter),
                        "a longer residence time must make the vertical vessel wider");
            Assert.That(longHold.Length, Is.GreaterThan(shortHold.Length),
                        "a longer residence time must make the vertical vessel taller");
            Assert.That(longHold.Length / longHold.Diameter, Is.EqualTo(input.LengthToDiameter).Within(1e-9),
                        "the height follows the requested aspect ratio");
        }

        private static DWSIM.DynamicRunner.Flowsheet LoadZipped(string filename)
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null);

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", filename));
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty, "the sample did not solve");
            return flowsheet;
        }

        // The file from issue #80: 9.8 m3/h of liquid and 16.9 m3/h of gas. The gas velocity alone asks
        // for a 100 mm diameter, and stacking five minutes of liquid on that gave a 104 m tall vessel on
        // which the aspect ratio had no effect. The diameter now comes from the liquid holdup at the
        // requested L/D when that is the larger, and every design parameter moves the result.
        // https://github.com/DanWBR/dwsim10/issues/80
        [Test]
        public void TheVerticalVesselRespectsTheAspectRatioWhenTheLiquidSetsTheDiameter()
        {
            var flowsheet = LoadZipped("GasLiquidSeparatorSizing.dwxmz");
            var vessel = flowsheet.SimulationObjects.Values.OfType<Vessel>().Single();

            var input = new SeparatorSizingInput();
            Assert.That(SeparatorSizing.ReadStreams(flowsheet, vessel, input), Is.True);

            var res = SeparatorSizing.SizeVertical(input);
            Assert.That(res.DiameterSetByLiquid, Is.True);
            Assert.That(res.Diameter, Is.EqualTo(804.0).Within(5.0), "mm");
            Assert.That(res.Length, Is.EqualTo(3.0 * res.Diameter).Within(1e-6), "height = L/D x diameter");

            // the liquid holdup for the residence time fits under one diameter of vapour space
            var holdup = input.LiquidVolumetricFlow * input.ResidenceTime * 60.0;
            var d = res.Diameter / 1000.0;
            Assert.That(holdup + System.Math.PI * d * d / 4.0 * d, Is.EqualTo(System.Math.PI * d * d / 4.0 * res.Length / 1000.0).Within(1e-6));

            input.LengthToDiameter = 5.0;
            var slender = SeparatorSizing.SizeVertical(input);
            Assert.That(slender.Diameter, Is.LessThan(res.Diameter), "a higher L/D gives a narrower vessel");
            Assert.That(slender.Length, Is.GreaterThan(res.Length), "and a taller one");
            input.LengthToDiameter = 3.0;

            input.NozzleConstant = 50.0;
            var slowNozzles = SeparatorSizing.SizeVertical(input);
            Assert.That(slowNozzles.InletNozzle, Is.GreaterThan(res.InletNozzle), "a lower nozzle constant means a lower nozzle velocity and a bigger nozzle");
            Assert.That(slowNozzles.GasNozzle, Is.GreaterThan(res.GasNozzle));
            Assert.That(slowNozzles.LiquidNozzle, Is.EqualTo(res.LiquidNozzle).Within(1e-9), "the liquid nozzle follows the liquid velocity, not the constant");
            Assert.That(slowNozzles.Diameter, Is.EqualTo(res.Diameter).Within(1e-9), "the nozzle constant does not size the vessel");
        }

        // When the gas load dominates, the gas velocity still sets the diameter and the height is L/D
        // times that: a small liquid holdup does not shrink the vessel below what the gas needs.
        [Test]
        public void TheGasVelocitySetsTheDiameterWhenTheLiquidLoadIsSmall()
        {
            var flowsheet = LoadZipped("GasLiquidSeparatorSizing.dwxmz");
            var vessel = flowsheet.SimulationObjects.Values.OfType<Vessel>().Single();

            var input = new SeparatorSizingInput();
            Assert.That(SeparatorSizing.ReadStreams(flowsheet, vessel, input), Is.True);
            input.LiquidVolumetricFlow /= 1e6;

            var res = SeparatorSizing.SizeVertical(input);
            Assert.That(res.DiameterSetByLiquid, Is.False);
            Assert.That(res.Diameter, Is.EqualTo(100.2).Within(0.5), "mm, from the gas velocity");
            Assert.That(res.Length, Is.EqualTo(3.0 * res.Diameter).Within(1e-6));
        }

        // Both liquid outlets carry flow in this sample, so the liquid side is the two of them.
        [Test]
        public void TheSecondLiquidOutletCountsTowardsTheLiquidLoad()
        {
            var flowsheet = Load();
            var vessel = flowsheet.SimulationObjects.Values.OfType<Vessel>().Single();

            var input = new SeparatorSizingInput();
            Assert.That(SeparatorSizing.ReadStreams(flowsheet, vessel, input), Is.True);

            var liquids = new[] { 1, 2 }
                .Select(i => (MaterialStream)flowsheet.SimulationObjects[
                    vessel.GraphicObject.OutputConnectors[i].AttachedConnector.AttachedTo.Name])
                .ToList();

            var flow = liquids.Sum(s => s.Phases[0].Properties.volumetric_flow.GetValueOrDefault());

            Assert.That(input.LiquidVolumetricFlow, Is.EqualTo(flow).Within(1e-9));
        }

        [Test]
        public void AnUnconnectedSeparatorIsReported()
        {
            var flowsheet = Load();
            var vessel = flowsheet.SimulationObjects.Values.OfType<Vessel>().Single();
            var feed = vessel.GraphicObject.InputConnectors[0].AttachedConnector.AttachedFrom;

            flowsheet.DisconnectObjects(feed, vessel.GraphicObject);

            Assert.That(SeparatorSizing.ReadStreams(flowsheet, vessel, new SeparatorSizingInput()), Is.False,
                        "a separator with no feed cannot be sized");
        }
    }
}
