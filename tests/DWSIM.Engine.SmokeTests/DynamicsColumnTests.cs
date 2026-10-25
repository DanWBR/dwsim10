//    Distillation column in dynamic mode, opened from a file.
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
using DWSIM.Automation.DynamicRunner;
using DWSIM.GlobalSettings;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class DynamicsColumnTests
    {
        [OneTimeSetUp]
        public void RegisterPropertyPackages()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        // The benzene-toluene column of the case library, saved solved and in dynamic mode. The first dynamic step
        // seeds the holdup from the last steady-state solution, which used to exist only after a solve in the same
        // session: opened from the file and run straight away, the column threw a null reference.
        [Test]
        public void AColumnOpenedFromAFileSeedsItsDynamicsFromTheSavedProfiles()
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets");

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", "BenzeneTolueneColumnDynamics.dwxmz"));

            var column = flowsheet.SimulationObjects.Values.OfType<DistillationColumn>().Single();
            var solution = column.GetLastSolution();
            Assert.That(solution, Is.Not.Null);
            Assert.That(solution.StageTemps.Count, Is.EqualTo(column.Stages.Count));
            Assert.That(solution.StageTemps.Select(t => t.Value), Is.All.GreaterThan(300.0));

            var result = new IntegratorRunner(flowsheet).Run(new IntegratorRunOptions { MaxSteps = 3, EnableHistorian = false });

            Assert.That(result.Exceptions, Is.Empty, string.Join("; ", result.Exceptions.Select(e => e.Message)));
            Assert.That(result.Steps, Is.EqualTo(3));
            Assert.That(column.Stages.All(s => s.AccumulationStream != null), Is.True);
        }
    }
}
