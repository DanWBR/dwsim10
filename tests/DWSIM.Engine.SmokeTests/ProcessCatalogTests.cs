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
                var problems = ProcessApplier.Validate(fs, p);
                Assert.That(problems, Is.Empty, p.Id + ": " + string.Join("; ", problems));
            }
        }

        [TestCase("methanol-synthesis", "CRUDE METHANOL")]
        [TestCase("hydrous-ethanol-distillation", "HYDROUS ETHANOL")]
        [TestCase("propane-refrigeration", "S-4")]
        public void ProcessBuildsAndSolves(string id, string productTag)
        {
            var p = ProcessCatalogLoader.LoadBuiltIn().Single(x => x.Id == id);
            var fs = NewFlowsheet();

            var setup = ProcessApplier.ApplySetup(fs, p);
            Assert.That(setup, Is.Empty, string.Join("; ", setup));
            Assert.That(fs.SelectedCompounds.Count, Is.EqualTo(p.Compounds.Count));
            Assert.That(fs.PropertyPackages.Count, Is.EqualTo(1));
            Assert.That(fs.Reactions.Count, Is.EqualTo(p.Reactions.Count));
            Assert.That(fs.FlowsheetOptions.Metadata.ProcessType.ToString(), Is.EqualTo(p.ProcessType));

            var build = ProcessApplier.BuildSkeleton(fs, p, AddExternal(fs));
            Assert.That(build, Is.Empty, string.Join("; ", build));
            Assert.That(fs.SimulationObjects.Count, Is.EqualTo(p.Skeleton.Objects.Count));

            var t0 = DateTime.Now;
            var solve = fs.SolveFlowsheet2();
            var seconds = (DateTime.Now - t0).TotalSeconds;
            TestContext.WriteLine(id + ": solved in " + seconds.ToString("0.0") + " s");
            Assert.That(solve, Is.Empty, string.Join("; ", solve.Select(e => e.Message)));
            Assert.That(seconds, Is.LessThan(60.0));

            var product = (IMaterialStream)fs.SimulationObjects.Values.Single(o => o.GraphicObject.Tag == productTag);
            var w = product.GetMassFlow();
            TestContext.WriteLine(productTag + ": " + w.ToString("0.000") + " kg/s at " + product.GetTemperature().ToString("0.0") + " K");
            Assert.That(w, Is.GreaterThan(0.0));
            foreach (var c in fs.SelectedCompounds.Keys)
                TestContext.WriteLine("  " + c + " " + product.GetOverallComposition()[fs.SelectedCompounds.Keys.ToList().IndexOf(c)].ToString("0.0000"));
        }
    }
}
