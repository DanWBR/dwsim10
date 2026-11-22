//    Process catalog of the simulation wizard: loading, validation, setup, skeleton and solve.
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
using System.Collections.Generic;
using System.Linq;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.SharedClasses.ProcessCatalog;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class ProcessCatalogTests
    {
        [OneTimeSetUp]
        public void RegisterPropertyPackages()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();

            // a local run can check the processes that need Patreon components: copy those components
            // into unitops and ppacks next to the tests, with DWSIM.Support, and set DWSIM_TEST_ACCESS_LEVEL
            if (int.TryParse(Environment.GetEnvironmentVariable("DWSIM_TEST_ACCESS_LEVEL"), out var level))
            {
                var init = Type.GetType("DWSIM.Support.Initialization, DWSIM.Support");
                init?.GetProperty("AccessLevelProvider", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.SetValue(null, new Func<int>(() => level));
            }
        }

        private static DWSIM.DynamicRunner.Flowsheet NewFlowsheet()
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            return fs;
        }

        // the headless host adds an external unit operation the way the cross-platform one does
        private static ProcessApplier.AddExternalHandler AddExternal(DWSIM.DynamicRunner.Flowsheet fs) =>
            (template, x, y, tag) =>
            {
                var fresh = (IExternalUnitOperation)template.ReturnInstance(template.GetType().AssemblyQualifiedName);
                var id = fs.AddObjectToSurface(ObjectType.External, x, y, tag, "", fresh, false);
                return fs.SimulationObjects[id];
            };

        [Test]
        public void BuiltInCatalogLoadsAndValidates()
        {
            var errors = new List<string>();
            var all = ProcessCatalogLoader.LoadBuiltIn(errors);
            Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
            Assert.That(all.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(all.Select(p => p.Id).Distinct().Count(), Is.EqualTo(all.Count), "duplicate ids");

            var fs = NewFlowsheet();
            foreach (var p in all)
            {
                Assert.That(p.Industry, Is.Not.Empty, p.Id);
                // a Patreon unit operation or package that this build doesn't have isn't a fault of the catalog
                var problems = ProcessApplier.Validate(fs, p)
                    .Where(x => !x.StartsWith("Unit operation not available") && !x.StartsWith("Property package not available")).ToList();
                Assert.That(problems, Is.Empty, p.Id + ": " + string.Join("; ", problems));
                // a ready-made simulation has a title and points to a sample file or a Case Library case
                foreach (var ex in p.Examples)
                {
                    Assert.That(ex.Title, Is.Not.Empty, p.Id);
                    Assert.That(string.IsNullOrWhiteSpace(ex.Sample) != string.IsNullOrWhiteSpace(ex.Case), Is.True, p.Id + ": " + ex.Title);
                }
                if (p.IsExampleOnly) Assert.That(p.Examples, Is.Not.Empty, p.Id);
            }
        }

        public static IEnumerable<string> CatalogIds() => ProcessCatalogLoader.LoadBuiltIn().Select(p => p.Id);

        // every process of the catalog that this installation can apply builds, solves within a minute and
        // leaves every object calculated; the processes that need Patreon components are checked in the
        // Patreon build, where those components are present
        [TestCaseSource(nameof(CatalogIds))]
        public void ProcessBuildsAndSolves(string id)
        {
            var p = ProcessCatalogLoader.LoadBuiltIn().Single(x => x.Id == id);
            if (p.IsExampleOnly) Assert.Ignore(id + " only points to ready-made simulations");
            var fs = NewFlowsheet();
            var problems = ProcessApplier.Validate(fs, p);
            if (problems.Count > 0) Assert.Ignore(id + " needs: " + string.Join("; ", problems));

            var setup = ProcessApplier.ApplySetup(fs, p);
            Assert.That(setup.Where(m => !m.StartsWith("Using ")), Is.Empty, string.Join("; ", setup));
            Assert.That(fs.SelectedCompounds.Count, Is.EqualTo(p.Compounds.Count));
            Assert.That(fs.PropertyPackages.Count, Is.EqualTo(1));
            Assert.That(fs.Reactions.Count, Is.EqualTo(p.Reactions.Count));

            var build = ProcessApplier.BuildSkeleton(fs, p, AddExternal(fs));
            Assert.That(build, Is.Empty, string.Join("; ", build));
            Assert.That(fs.SimulationObjects.Count, Is.EqualTo(p.Skeleton.Objects.Count));

            var t0 = DateTime.Now;
            var solve = fs.SolveFlowsheet2();
            var seconds = (DateTime.Now - t0).TotalSeconds;
            TestContext.WriteLine(id + ": solved in " + seconds.ToString("0.0") + " s");
            Assert.That(solve, Is.Empty, string.Join("; ", solve.Select(e => e.Message)));
            Assert.That(seconds, Is.LessThan(60.0));

            var notCalculated = fs.SimulationObjects.Values.Where(o => !o.Calculated).Select(o => o.GraphicObject.Tag).ToList();
            Assert.That(notCalculated, Is.Empty, "not calculated: " + string.Join(", ", notCalculated));

            var names = fs.SelectedCompounds.Keys.ToList();
            foreach (var so in fs.SimulationObjects.Values)
            {
                if (!(so is IMaterialStream ms) || so.GraphicObject.OutputConnectors[0].IsAttached) continue;
                var x = ms.GetOverallComposition();
                var main = Enumerable.Range(0, names.Count).OrderByDescending(i => x[i]).Take(3)
                    .Select(i => names[i] + " " + x[i].ToString("0.000"));
                TestContext.WriteLine("  " + so.GraphicObject.Tag + ": " + ms.GetMassFlow().ToString("0.000") + " kg/s, " +
                                      (ms.GetTemperature() - 273.15).ToString("0.0") + " C, " + (ms.GetPressure() / 1e5).ToString("0.00") + " bar, " +
                                      string.Join(", ", main));
            }
            foreach (var so in fs.SimulationObjects.Values.Where(o => o is IEnergyStream))
                TestContext.WriteLine("  " + so.GraphicObject.Tag + ": " + ((IEnergyStream)so).GetEnergyFlow().ToString("0.0") + " kW");
        }
    }
}
