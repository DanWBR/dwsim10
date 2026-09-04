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
            Assert.That(res.Length, Is.EqualTo(input.LengthToDiameter * res.Diameter).Within(1e-6));
            Assert.That(res.InletNozzle, Is.GreaterThan(0.0));
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
