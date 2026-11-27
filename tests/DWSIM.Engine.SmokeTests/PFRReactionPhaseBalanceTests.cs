//    Plug flow reactor mass balance when the reactions run on one phase of the stream.
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
using DWSIM.GlobalSettings;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.Reactors;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class PFRReactionPhaseBalanceTests
    {
        [OneTimeSetUp]
        public void RegisterPropertyPackages()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.DynamicRunner.Flowsheet Load(string filename)
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadFromXML(XDocument.Load(Path.Combine(folder, "tests", "flowsheets", filename)));
            return flowsheet;
        }

        // A sugar A and hydrogen at 98 C and 100 bar (NRTL) react to three polyols, A + H2 -> P1, P2 or P3, on the
        // liquid phase of a gas-liquid feed, isothermal, at the default flash tolerance (the file attached to issue
        // #152). Each volume step reacts the liquid's flows and puts the rest of the stream back. The flash left the
        // liquid with a hair more of the non-volatile sugar than the whole stream, the rest came out negative and
        // was clipped to zero, and every step added the difference: the products came out 8e-4 mol/s above the
        // sugar consumed (1.7e-3 of the mass flow), twice as much with twice the steps, and the duty carried the
        // excess (-92.6 instead of -85.9 kJ/mol). A product absent from the feed also started from 1e-10 mol/s
        // that the step never gave back.
        // https://github.com/DanWBR/dwsim10/issues/152
        [Test]
        public void LiquidPhaseReactionsOnAGasLiquidFeedConserveMass()
        {
            var flowsheet = Load("PFRLiquidPhaseGasLiquidFeed.dwxml");
            var pfr = flowsheet.SimulationObjects.Values.OfType<Reactor_PFR>().Single();
            var feed = (MaterialStream)flowsheet.SimulationObjects.Values.Single(o => o.GraphicObject.Tag == "feed");
            var outlet = (MaterialStream)flowsheet.SimulationObjects.Values.Single(o => o.GraphicObject.Tag == "out");

            double Flow(MaterialStream s, string compound) => s.Phases[0].Compounds[compound].MolarFlow.GetValueOrDefault();

            var duties = new double[2];
            int k = 0;
            foreach (var step in new[] { Math.Pow(2, -10), Math.Pow(2, -9) })
            {
                pfr.dV = step;
                var errors = flowsheet.SolveFlowsheet2();
                Assert.That(errors, Is.Empty, string.Join("; ", errors.Select(e => e.Message)));

                double used = Flow(feed, "Sugar A") - Flow(outlet, "Sugar A");
                double made = Flow(outlet, "Polyol P1") + Flow(outlet, "Polyol P2") + Flow(outlet, "Polyol P3");
                Assert.That(used, Is.GreaterThan(0.1), "the sugar reacts");
                Assert.That(made - used, Is.EqualTo(0.0).Within(1e-9), "every mol of product comes from a mol of sugar");

                // the residue of about 2e-7 is the file's compounds: a polyol's molar weight (182.172) is 1.2e-4 g/mol
                // above the sugar's plus hydrogen's
                double mass = (outlet.GetMassFlow() - feed.GetMassFlow()) / feed.GetMassFlow();
                Assert.That(Math.Abs(mass), Is.LessThan(1e-6), "the outlet carries the inlet's mass");

                duties[k++] = pfr.DeltaQ.GetValueOrDefault() / used;
            }

            TestContext.WriteLine("duty per mol of sugar: {0:F3} and {1:F3} kJ/mol", duties[0], duties[1]);
            Assert.That(duties[1], Is.EqualTo(duties[0]).Within(0.001).Percent, "the duty is the heat of the reactions, whatever the step");
        }
    }
}
