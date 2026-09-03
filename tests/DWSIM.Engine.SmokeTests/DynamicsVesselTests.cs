//    Gas-liquid separator in dynamic mode: pressure seeding and property units.
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

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DWSIM.Automation.DynamicRunner;
using DWSIM.GlobalSettings;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class DynamicsVesselTests
    {
        [OneTimeSetUp]
        public void RegisterPropertyPackages()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        // Feed at 37.9 bar and 1 kg/s into a 3 m3 separator whose vapour leaves through a Kv = 10
        // valve to a vent at 1 atm. The file was saved after a run, so the vessel carries its
        // content stream. https://github.com/DanWBR/dwsim10/issues/57, /58, /59
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
            flowsheet.LoadFromXML(XDocument.Load(Path.Combine(folder, "tests", "flowsheets", "VesselDepressurization.dwxml")));

            return flowsheet;
        }

        [Test]
        public void ADynamicPropertyReadsAndWritesInSIUnlessAUnitSystemIsGiven()
        {
            var flowsheet = Load();
            var vessel = flowsheet.SimulationObjects.Values.OfType<Vessel>().Single();
            var display = flowsheet.FlowsheetOptions.SelectedUnitSystem;

            Assert.That(display.pressure, Is.EqualTo("bar"), "the sample is meant to display pressures in bar");

            // stored value is SI, and so is what comes back with no unit system
            Assert.That(Convert.ToDouble(vessel.GetPropertyValue("Operating Pressure")),
                        Is.EqualTo(Convert.ToDouble(vessel.GetDynamicProperty("Operating Pressure"))).Within(1e-9));

            // a unit system converts both ways
            vessel.SetPropertyValue("Operating Pressure", 38.245, display);
            Assert.That(Convert.ToDouble(vessel.GetDynamicProperty("Operating Pressure")), Is.EqualTo(3824500.0).Within(1e-6));
            Assert.That(Convert.ToDouble(vessel.GetPropertyValue("Operating Pressure", display)), Is.EqualTo(38.245).Within(1e-9));

            // no unit system: the number is taken as SI, as for every other property
            vessel.SetPropertyValue("Operating Pressure", 3000000.0);
            Assert.That(Convert.ToDouble(vessel.GetDynamicProperty("Operating Pressure")), Is.EqualTo(3000000.0).Within(1e-6));
        }

        [Test]
        public void TheVesselStartsAtTheFeedPressureAndTheMonitoredPressureIsInBar()
        {
            var flowsheet = Load();
            var vessel = flowsheet.SimulationObjects.Values.OfType<Vessel>().Single();
            var feed = flowsheet.SimulationObjects.Values.OfType<MaterialStream>().Single(s => s.GraphicObject.Tag == "Feed1");

            var errors = flowsheet.SolveFlowsheet2();
            Assert.That(errors, Is.Empty, "steady state: " + string.Join("; ", errors.Select(e => e.Message)));

            double feedPressure = feed.GetPressure();
            Assert.That(feedPressure, Is.EqualTo(3789464.7).Within(1.0));

            vessel.SetDynamicProperty("Reset Content", 1);

            var runner = new IntegratorRunner(flowsheet);
            var result = runner.Run(new IntegratorRunOptions { Schedule = "REPRO", RestoreInitialState = false });

            Assert.That(result.Exceptions, Is.Empty, string.Join("; ", result.Exceptions.Select(e => e.Message)));
            Assert.That(result.Steps, Is.GreaterThanOrEqualTo(100));

            var ticks = result.Integrator.MonitoredVariableValues.Keys.OrderBy(k => k).ToList();
            var first = result.Integrator.MonitoredVariableValues[ticks.First()]
                .Single(v => v.PropertyID == "Operating Pressure");
            var last = result.Integrator.MonitoredVariableValues[ticks.Last()]
                .Single(v => v.PropertyID == "Operating Pressure");

            Assert.That(first.PropertyUnits, Is.EqualTo("bar"));

            // the content is built from the inlet, so the vessel starts at the feed pressure,
            // and the monitored value is that pressure in bar, not in bar divided by 1e5 again
            double firstBar = double.Parse(first.PropertyValue, System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(firstBar, Is.EqualTo(feedPressure / 1e5).Within(0.5));

            // the vessel itself keeps working in Pa, and with the feed replenishing what the
            // valve vents the pressure stays close to the feed pressure
            double finalPa = Convert.ToDouble(vessel.GetDynamicProperty("Operating Pressure"));
            Assert.That(finalPa, Is.EqualTo(feedPressure).Within(0.05 * feedPressure));
            double lastBar = double.Parse(last.PropertyValue, System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(lastBar, Is.EqualTo(finalPa / 1e5).Within(1e-6));
        }
    }
}
