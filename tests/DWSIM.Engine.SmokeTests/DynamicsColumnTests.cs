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

using System;
using System.IO;
using System.Linq;
using DWSIM.Automation.DynamicRunner;
using DWSIM.GlobalSettings;
using DWSIM.UnitOperations.UnitOperations;
using IntegrationMethod = DWSIM.Interfaces.Enums.Dynamics.IntegrationMethod;
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

        private static DWSIM.DynamicRunner.Flowsheet Load()
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets");

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", "BenzeneTolueneColumnDynamics.dwxmz"));
            return flowsheet;
        }

        // Total liquid and vapor the stages hold after a run of the given method and number of steps.
        private static double StageHoldup(IntegrationMethod method, int steps)
        {
            var flowsheet = Load();
            var schedule = IntegratorRunner.ResolveSchedule(flowsheet, null);
            flowsheet.DynamicsManager.IntegratorList[schedule.CurrentIntegrator].IntegrationMethod = method;
            var result = new IntegratorRunner(flowsheet).Run(new IntegratorRunOptions { MaxSteps = steps, EnableHistorian = false });
            Assert.That(result.Exceptions, Is.Empty, method + ": " + string.Join("; ", result.Exceptions.Select(e => e.Message)));
            var column = flowsheet.SimulationObjects.Values.OfType<DistillationColumn>().Single();
            return column.Stages.Sum(s => s.AccumulationStream.GetMassFlow());
        }

        // The benzene-toluene column of the case library, saved solved and in dynamic mode. The first dynamic step
        // seeds the holdup from the last steady-state solution, which used to exist only after a solve in the same
        // session: opened from the file and run straight away, the column threw a null reference.
        [Test]
        public void AColumnOpenedFromAFileSeedsItsDynamicsFromTheSavedProfiles()
        {
            var flowsheet = Load();
            var column = flowsheet.SimulationObjects.Values.OfType<DistillationColumn>().Single();
            var solution = column.GetLastSolution();
            Assert.That(solution, Is.Not.Null);
            Assert.That(solution.StageTemps.Count, Is.EqualTo(column.Stages.Count));
            Assert.That(solution.StageTemps.Select(t => t.Value), Is.All.GreaterThan(300.0));

            Assert.That(column.GetDynamicContents(), Is.NaN, "nothing is held before the holdups are seeded");

            var result = new IntegratorRunner(flowsheet).Run(new IntegratorRunOptions { MaxSteps = 3, EnableHistorian = false });

            Assert.That(result.Exceptions, Is.Empty, string.Join("; ", result.Exceptions.Select(e => e.Message)));
            Assert.That(result.Steps, Is.EqualTo(3));
            Assert.That(column.Stages.All(s => s.AccumulationStream != null), Is.True);

            // the dynamic contents are the stages and the sump, so the implicit Euler step sees the column settle
            double held = column.Stages.Sum(s => s.AccumulationStream.GetMassFlow()) + column.BottomsAccumulationStream.GetMassFlow();
            Assert.That(column.GetDynamicContents(), Is.EqualTo(held).Within(1e-9 * held));
            Assert.That(held, Is.GreaterThan(1000.0));
        }

        // Implicit Euler solves a step more than once and restores the holdups before each solve. The column keeps
        // its holdups on the stages, which were not restored, so it advanced once per solve: after 15 implicit steps
        // it was where 30 explicit steps take it.
        [Test]
        public void AnImplicitStepAdvancesTheColumnByOneStep()
        {
            double explicit15 = StageHoldup(IntegrationMethod.ExplicitEuler, 15);
            double explicit30 = StageHoldup(IntegrationMethod.ExplicitEuler, 30);
            double implicit15 = StageHoldup(IntegrationMethod.ImplicitEuler, 15);

            Assert.That(Math.Abs(explicit30 - explicit15), Is.GreaterThan(1.0), "the holdup must move over the run for the test to tell");
            Assert.That(Math.Abs(implicit15 - explicit15), Is.LessThan(0.2 * Math.Abs(explicit30 - explicit15)),
                $"implicit {implicit15:G6} kg, explicit {explicit15:G6} kg at the same time, {explicit30:G6} kg at twice the time");
        }
    }
}
