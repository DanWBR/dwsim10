//    Rigorous column solver regressions.
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
using DWSIM.GlobalSettings;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class RigorousColumnTests
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
            {
                folder = Path.GetDirectoryName(folder);
            }

            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", filename));

            return flowsheet;
        }

        // A 12-stage reboiled stripper (full reflux, reflux ratio 0, bottoms rate spec) on a
        // Fischer-Tropsch naphtha of 69 database compounds carrying trace dissolved gases
        // (methane 2e-4, H2 1e-4, CO2 4e-3, ...), Peng-Robinson. The feed is a subcooled liquid,
        // and the single-phase PT flash used for the stage composition estimates hands back
        // its input array, so every subcooled stage used to share one composition vector and
        // the solver overwrote stage after stage with the last one: the lightest component
        // vanished from the column and the balance check reported
        // "Failed to fulfill mass balance for Methane: Relative Error = 0.99999".
        // The stage estimates saved in the file are a converged solution of the same column
        // from an independent stage-by-stage flash cascade (166 degF top, 252.6 degF bottoms).
        // https://github.com/DanWBR/dwsim10/issues/60
        [TestCase(true)]
        [TestCase(false)]
        public void TheNaphthaStripperKeepsItsTraceMethane(bool useSavedEstimates)
        {
            var flowsheet = Load("TraceMethaneStripper.dwxmz");

            var column = flowsheet.SimulationObjects.Values.OfType<DistillationColumn>().Single();
            column.UseTemperatureEstimates = useSavedEstimates;
            column.UseVaporFlowEstimates = useSavedEstimates;
            column.UseLiquidFlowEstimates = useSavedEstimates;

            var errors = flowsheet.SolveFlowsheet2();

            Assert.That(errors, Is.Empty,
                        "the solver reported: " + string.Join("; ", errors.Select(e => e.Message)));

            var streams = flowsheet.SimulationObjects.Values.OfType<MaterialStream>().ToList();
            var feed = streams.Single(s => s.GraphicObject.Tag == "FEED");
            var overhead = streams.Single(s => s.GraphicObject.Tag == "OVERHEAD");
            var bottoms = streams.Single(s => s.GraphicObject.Tag == "BOTTOMS");

            foreach (var name in new[] { "Methane", "Hydrogen", "Carbon dioxide", "Ethane", "Propane", "N-heptane" })
            {
                double inFlow = feed.Phases[0].Compounds[name].MolarFlow.GetValueOrDefault();
                double outFlow = overhead.Phases[0].Compounds[name].MolarFlow.GetValueOrDefault()
                               + bottoms.Phases[0].Compounds[name].MolarFlow.GetValueOrDefault();

                Assert.That(outFlow, Is.EqualTo(inFlow).Within(0.1).Percent, name + " does not balance");
            }

            // the dissolved gases all leave overhead
            Assert.That(overhead.Phases[0].Compounds["Methane"].MolarFlow.GetValueOrDefault(),
                        Is.EqualTo(feed.Phases[0].Compounds["Methane"].MolarFlow.GetValueOrDefault()).Within(0.1).Percent);

            // the independent solution: overhead at 166 degF, bottoms at 252.6 degF
            Assert.That(overhead.Phases[0].Properties.temperature, Is.EqualTo(347.6).Within(2.0));
            Assert.That(bottoms.Phases[0].Properties.temperature, Is.EqualTo(395.7).Within(2.0));
        }
    }
}
