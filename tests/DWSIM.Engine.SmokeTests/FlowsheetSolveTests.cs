//    Loads the sample flowsheets and solves them, which is what proves the ported engine runs.
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
using DWSIM.Interfaces;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class FlowsheetSolveTests
    {
        private static string FlowsheetFolder
        {
            get
            {
                var folder = TestContext.CurrentContext.TestDirectory;
                while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                {
                    folder = Path.GetDirectoryName(folder);
                }

                Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");

                return Path.Combine(folder, "tests", "flowsheets");
            }
        }

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
            var path = Path.Combine(FlowsheetFolder, filename);

            Assert.That(File.Exists(path), $"{filename} is not in tests/flowsheets");

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();

            if (Path.GetExtension(path).ToLowerInvariant().EndsWith("z"))
            {
                flowsheet.LoadZippedXML(path);
            }
            else
            {
                flowsheet.LoadFromXML(XDocument.Load(path));
            }

            return flowsheet;
        }

        [Test]
        public void TheCompoundDatabasesLoad()
        {
            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();

            Assert.That(flowsheet.AvailableCompounds.Count, Is.GreaterThan(1000),
                        "the compound databases did not load");
        }

        [Test]
        public void ThePropertyPackagesAreRegistered()
        {
            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();

            Assert.That(flowsheet.AvailablePropertyPackages.Count, Is.GreaterThan(10),
                        "the property packages did not register");
        }

        [TestCase("BiodieselProduction.dwxmz")]
        [TestCase("CavettProblem.dwxml")]
        [TestCase("ExtractiveDistillation.dwxmz")]
        [TestCase("GibbsAndEquilibriumReactors.dwxml")]
        [TestCase("HeatExchangerSizingAndDesign.dwxml")]
        [TestCase("HydrocycloneCustomUnitOperation.dwxml")]
        [TestCase("HydrogenproductionthroughMethaneCatalyticSteamReforming.dwxml")]
        [TestCase("LiquidLiquidExtraction.dwxmz")]
        [TestCase("MembraneCustomUnitOperation.dwxml")]
        [TestCase("NaturalGasProcessingUnit.dwxml")]
        [TestCase("PetroleumDistillation.dwxml")]
        [TestCase("SimpleAbsorberSample.dwxml")]
        [TestCase("SimpleLNGExchangerCustomUnitOperation.dwxml")]
        [TestCase("ThreePhaseSeparator.dwxml")]
        public void AFlowsheetLoads(string filename)
        {
            var flowsheet = Load(filename);

            Assert.That(flowsheet.SimulationObjects.Count, Is.GreaterThan(0), "no objects were read");
            Assert.That(flowsheet.SelectedCompounds.Count, Is.GreaterThan(0), "no compounds were read");
            Assert.That(flowsheet.PropertyPackages.Count, Is.GreaterThan(0), "no property package was read");
        }

        [TestCase("CavettProblem.dwxml")]
        [TestCase("GibbsAndEquilibriumReactors.dwxml")]
        [TestCase("HeatExchangerSizingAndDesign.dwxml")]
        [TestCase("HydrogenproductionthroughMethaneCatalyticSteamReforming.dwxml")]
        [TestCase("MembraneCustomUnitOperation.dwxml")]
        [TestCase("PetroleumDistillation.dwxml")]
        [TestCase("ThreePhaseSeparator.dwxml")]
        public void AFlowsheetSolves(string filename)
        {
            var flowsheet = Load(filename);

            var errors = flowsheet.SolveFlowsheet2();

            Assert.That(errors, Is.Empty,
                        "the solver reported: " + string.Join("; ", errors.Select(e => e.Message)));

            var streams = flowsheet.SimulationObjects.Values
                .Where(o => o.GraphicObject != null &&
                            o.GraphicObject.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream)
                .ToList();

            Assert.That(streams, Is.Not.Empty, "the flowsheet has no material streams");
            Assert.That(streams.All(s => s.Calculated), "some material stream was left uncalculated");
        }

        // The seven samples below do not solve, and do not solve on the .NET Framework build of the
        // engine either: the same object reports the same message there. Four are columns that miss
        // the tolerance, one is a column that breaks its own mass balance, and two carry scripts
        // written for Python 2 that no longer parse. They are pinned here so that the day one of
        // them starts behaving differently, the suite says so.
        [TestCase("BiodieselProduction.dwxmz", "Biodiesel Purification: DCErrorStillHigh")]
        [TestCase("ExtractiveDistillation.dwxmz", "Acetone Column (6 atm): DCErrorStillHigh")]
        [TestCase("HydrocycloneCustomUnitOperation.dwxml", "SyntaxError: invalid syntax")]
        [TestCase("LiquidLiquidExtraction.dwxmz", "ABS-002: DCErrorStillHigh")]
        [TestCase("NaturalGasProcessingUnit.dwxml", "DEBUTANIZER: Failed to fulfill mass balance")]
        [TestCase("SimpleAbsorberSample.dwxml", "ABS-000: DCErrorStillHigh")]
        [TestCase("SimpleLNGExchangerCustomUnitOperation.dwxml", "SyntaxError: invalid syntax")]
        public void AFlowsheetFailsTheWayItAlreadyDid(string filename, string expected)
        {
            var flowsheet = Load(filename);

            var errors = flowsheet.SolveFlowsheet2();

            Assert.That(errors.Count, Is.EqualTo(1),
                        "the solver reported: " + string.Join("; ", errors.Select(e => e.Message)));
            Assert.That(errors[0].Message, Does.Contain(expected));
        }
    }
}
