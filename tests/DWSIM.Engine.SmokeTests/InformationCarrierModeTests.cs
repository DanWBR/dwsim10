//    Information carrier: calculation modes in a full flowsheet solve, in both flowsheet solvers,
//    and the links surviving a save and load.
//    Copyright 2026 Daniel Wagner Oliveira de Medeiros
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.SpecialOps;
using NUnit.Framework;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// Water at 25 C goes through H1 (40 C) to S2 and through H2 (80 C) to S3, which starts at
    /// 10 C. An information carrier copies S3's temperature to H1's outlet temperature, so when it
    /// runs shows in S2 and in H1's setting: before H1 (10 C everywhere), after S3 (S2 at 40 C,
    /// H1 set to 80 C) or after the flowsheet, which solves it again (80 C everywhere).
    /// Every case runs in both solvers, FlowsheetSolver and FlowsheetSolver2.
    /// </summary>
    [TestFixture]
    public class InformationCarrierModeTests
    {
        private const double Tol = 1e-3;

        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
        }

        // ------------------------------------------------------------------ helpers

        private sealed class Case
        {
            public FluentFlowsheet Fs;
            public MaterialStreamBuilder S1, S2, S3;
            public HeaterBuilder H1, H2;
            public InformationCarrierBuilder IC;

            public double S2C => S2.TemperatureK - 273.15;
            public double H1C => H1.Object.OutletTemperature.GetValueOrDefault() - 273.15;
        }

        private static Case Build(SpecCalcMode global, bool withCarrier = true)
        {
            var c = new Case();
            c.Fs = FluentFlowsheet.Create("ICMode").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            c.S1 = c.Fs.AddMaterialStream("S1").At(25.0.Celsius(), 2.0.Bar()).WithMassFlow(1.0.KgPerSecond());
            c.S2 = c.Fs.AddMaterialStream("S2");
            c.S3 = c.Fs.AddMaterialStream("S3").At(10.0.Celsius(), 2.0.Bar()).WithMassFlow(1.0.KgPerSecond());
            c.H1 = c.Fs.AddHeater("H1").WithOutletTemperature(40.0.Celsius())
                .ConnectFeed(c.S1).ConnectProduct(c.S2).ConnectEnergyFeed(c.Fs.AddEnergyStream("E1"), 1);
            c.H2 = c.Fs.AddHeater("H2").WithOutletTemperature(80.0.Celsius())
                .ConnectFeed(c.S2).ConnectProduct(c.S3).ConnectEnergyFeed(c.Fs.AddEnergyStream("E2"), 1);
            if (withCarrier)
                c.IC = c.Fs.AddInformationCarrier("IC-1").WithSource("S3", "PROP_MS_0").WithTarget("H1", "PROP_HT_2")
                    .WithFlowsheetCalculationMode(global);
            else
                c.Fs.Inner.FlowsheetOptions.InformationCarrierCalculationMode = global;
            return c;
        }

        private static IReadOnlyList<Exception> Solve(FluentFlowsheet fs, bool solver2)
        {
            if (!solver2) return fs.TrySolve();
            DWSIM.GlobalSettings.Settings.CalculatorActivated = true;
            var solver = new DWSIM.FlowsheetSolver.FlowsheetSolver2();
            return Task.Run(() => solver.SolveFlowsheet(fs.Inner)).Result ?? new List<Exception>();
        }

        private static void SolveClean(FluentFlowsheet fs, bool solver2)
        {
            var errors = Solve(fs, solver2);
            foreach (var e in errors) TestContext.WriteLine("solver error: " + e);
            Assert.That(errors, Is.Empty, "solver errors");
        }

        private static void Check(Case c, double s2, double h1, string what)
        {
            TestContext.WriteLine($"{what}: S2 {c.S2C:F3} C, H1 outlet temperature {c.H1C:F3} C");
            Assert.That(c.S2C, Is.EqualTo(s2).Within(Tol), what + ": S2 temperature");
            Assert.That(c.H1C, Is.EqualTo(h1).Within(Tol), what + ": H1 outlet temperature");
        }

        // ------------------------------------------------------------------ GlobalSetting

        /// <summary>A carrier left at GlobalSetting runs where the flowsheet option says, as it always did.</summary>
        [TestCase(SpecCalcMode.AfterSourceObject, 40.0, 80.0, false)]
        [TestCase(SpecCalcMode.AfterSourceObject, 40.0, 80.0, true)]
        [TestCase(SpecCalcMode.BeforeTargetObject, 10.0, 10.0, false)]
        [TestCase(SpecCalcMode.BeforeTargetObject, 10.0, 10.0, true)]
        [TestCase(SpecCalcMode.BeforeFlowsheet, 10.0, 10.0, false)]
        [TestCase(SpecCalcMode.BeforeFlowsheet, 10.0, 10.0, true)]
        [TestCase(SpecCalcMode.AfterFlowsheet, 80.0, 80.0, false)]
        [TestCase(SpecCalcMode.AfterFlowsheet, 80.0, 80.0, true)]
        public void AGlobalSettingCarrierFollowsTheFlowsheetOption(SpecCalcMode global, double s2, double h1, bool solver2)
        {
            var c = Build(global);
            SolveClean(c.Fs, solver2);
            Check(c, s2, h1, $"GlobalSetting under {global}");
        }

        // ------------------------------------------------------------------ explicit modes

        /// <summary>
        /// A carrier with its own mode runs there whatever the flowsheet option says, and only there.
        /// AfterObject and BeforeObject have no reference object in a carrier and follow the option.
        /// </summary>
        [TestCase(SpecCalcMode2.AfterSourceObject, SpecCalcMode.BeforeTargetObject, 40.0, 80.0, false)]
        [TestCase(SpecCalcMode2.AfterSourceObject, SpecCalcMode.BeforeTargetObject, 40.0, 80.0, true)]
        [TestCase(SpecCalcMode2.AfterSourceObject, SpecCalcMode.BeforeFlowsheet, 40.0, 80.0, false)]
        [TestCase(SpecCalcMode2.AfterSourceObject, SpecCalcMode.BeforeFlowsheet, 40.0, 80.0, true)]
        [TestCase(SpecCalcMode2.BeforeTargetObject, SpecCalcMode.AfterSourceObject, 10.0, 10.0, false)]
        [TestCase(SpecCalcMode2.BeforeTargetObject, SpecCalcMode.AfterSourceObject, 10.0, 10.0, true)]
        [TestCase(SpecCalcMode2.BeforeTargetObject, SpecCalcMode.BeforeFlowsheet, 10.0, 10.0, false)]
        [TestCase(SpecCalcMode2.BeforeFlowsheet, SpecCalcMode.AfterSourceObject, 10.0, 10.0, false)]
        [TestCase(SpecCalcMode2.BeforeFlowsheet, SpecCalcMode.AfterSourceObject, 10.0, 10.0, true)]
        [TestCase(SpecCalcMode2.BeforeFlowsheet, SpecCalcMode.AfterFlowsheet, 10.0, 10.0, false)]
        [TestCase(SpecCalcMode2.BeforeFlowsheet, SpecCalcMode.AfterFlowsheet, 10.0, 10.0, true)]
        [TestCase(SpecCalcMode2.AfterFlowsheet, SpecCalcMode.AfterSourceObject, 80.0, 80.0, false)]
        [TestCase(SpecCalcMode2.AfterFlowsheet, SpecCalcMode.AfterSourceObject, 80.0, 80.0, true)]
        [TestCase(SpecCalcMode2.AfterFlowsheet, SpecCalcMode.BeforeFlowsheet, 80.0, 80.0, false)]
        [TestCase(SpecCalcMode2.AfterFlowsheet, SpecCalcMode.BeforeFlowsheet, 80.0, 80.0, true)]
        [TestCase(SpecCalcMode2.AfterObject, SpecCalcMode.BeforeTargetObject, 10.0, 10.0, false)]
        [TestCase(SpecCalcMode2.BeforeObject, SpecCalcMode.AfterSourceObject, 40.0, 80.0, true)]
        public void AnExplicitModeOverridesAContraryFlowsheetOption(SpecCalcMode2 mode, SpecCalcMode global,
            double s2, double h1, bool solver2)
        {
            var c = Build(global);
            c.IC.WithCalculationMode(mode);
            SolveClean(c.Fs, solver2);
            Check(c, s2, h1, $"{mode} under {global}");
        }

        // ------------------------------------------------------------------ attachment without a carrier

        /// <summary>
        /// Objects marked as attached to an information carrier with an empty or unknown id solve
        /// without a KeyNotFoundException; nothing runs for them.
        /// </summary>
        [TestCase(SpecCalcMode.AfterSourceObject, "", false)]
        [TestCase(SpecCalcMode.AfterSourceObject, "", true)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "", false)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "", true)]
        [TestCase(SpecCalcMode.AfterSourceObject, "NO-SUCH-CARRIER", true)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "NO-SUCH-CARRIER", false)]
        public void AnEmptyOrUnknownAttachedCarrierIdIsSkipped(SpecCalcMode global, string id, bool solver2)
        {
            var c = Build(global, withCarrier: false);
            foreach (var o in c.Fs.Inner.SimulationObjects.Values)
            {
                o.IsInfoCarrierAttached = true;
                o.AttachedInfoCarrierId = id;
                o.InfoCarrierVarType = o.GraphicObject.InputConnectors.Count > 0 && o.GraphicObject.InputConnectors[0].IsAttached
                    ? SpecVarType.Target : SpecVarType.Source;
            }
            SolveClean(c.Fs, solver2);
            Check(c, 40.0, 40.0, $"carrier id '{id}' under {global}");
        }

        // ------------------------------------------------------------------ save and load

        /// <summary>
        /// The object type of the source and of each target survives SaveData and LoadData: it is
        /// written as the Type attribute and read back from it (files with the older ObjectType
        /// attribute still load).
        /// </summary>
        [Test]
        public void TheLinkedObjectTypesSurviveSaveDataAndLoadData()
        {
            var c = Build(SpecCalcMode.AfterSourceObject);
            c.IC.WithTarget("H2", "PROP_HT_2");
            var ic = c.IC.Object;
            Assert.That(ic.SourceObjectData.ObjectType, Is.Not.Empty, "source type set by the builder");

            var data = ic.SaveData();
            var copy = new InformationCarrier();
            copy.LoadData(data);

            TestContext.WriteLine($"source {copy.SourceObjectData.ObjectType}; targets {copy.TargetObjectData.ObjectType}, {copy.TargetObjectData2.ObjectType}");
            Assert.That(copy.SourceObjectData.ObjectType, Is.EqualTo(ic.SourceObjectData.ObjectType), "source");
            Assert.That(copy.TargetObjectData.ObjectType, Is.EqualTo(ic.TargetObjectData.ObjectType), "target 1");
            Assert.That(copy.TargetObjectData2.ObjectType, Is.EqualTo(ic.TargetObjectData2.ObjectType), "target 2");
            Assert.That(copy.TargetObjectData.PropertyName, Is.EqualTo("PROP_HT_2"));

            // the older attribute name
            foreach (var x in data.Where(e => e.Name.LocalName.StartsWith("SourceObjectData") || e.Name.LocalName.StartsWith("TargetObjectData")))
            {
                var t = x.Attribute("Type");
                if (t == null) continue;
                x.SetAttributeValue("ObjectType", t.Value);
                t.Remove();
            }
            var old = new InformationCarrier();
            old.LoadData(data);
            Assert.That(old.SourceObjectData.ObjectType, Is.EqualTo(ic.SourceObjectData.ObjectType), "source, ObjectType attribute");
            Assert.That(old.TargetObjectData2.ObjectType, Is.EqualTo(ic.TargetObjectData2.ObjectType), "target 2, ObjectType attribute");
        }

        /// <summary>
        /// Saved to a file and opened again through the Fluent API, the carrier keeps its links, the
        /// object types and its own calculation mode, and solves to the same result.
        /// </summary>
        [Test]
        public void ACarrierSavedAndOpenedKeepsItsLinksAndMode()
        {
            var c = Build(SpecCalcMode.BeforeTargetObject);
            c.IC.WithCalculationMode(SpecCalcMode2.AfterSourceObject);
            var path = Path.Combine(Path.GetTempPath(), "ic-roundtrip-" + Guid.NewGuid().ToString("N") + ".dwxmz");
            try
            {
                c.Fs.Save(path);
                var loaded = FluentFlowsheet.Load(path);
                var ic = loaded.Inner.SimulationObjects.Values.OfType<InformationCarrier>().Single();

                TestContext.WriteLine($"loaded: mode {ic.CalculationMode}; source {ic.SourceObjectData.Name} {ic.SourceObjectData.PropertyName} ({ic.SourceObjectData.ObjectType}); " +
                                      $"target {ic.TargetObjectData.Name} {ic.TargetObjectData.PropertyName} ({ic.TargetObjectData.ObjectType})");
                Assert.That(ic.CalculationMode, Is.EqualTo(SpecCalcMode2.AfterSourceObject));
                Assert.That(ic.SourceObjectData.ObjectType, Is.EqualTo(c.IC.Object.SourceObjectData.ObjectType).And.Not.Empty);
                Assert.That(ic.TargetObjectData.ObjectType, Is.EqualTo(c.IC.Object.TargetObjectData.ObjectType).And.Not.Empty);
                Assert.That(ic.TargetObjectData.PropertyName, Is.EqualTo("PROP_HT_2"));

                SolveClean(loaded, false);
                var s2 = loaded.Inner.SimulationObjects.Values.Single(o => o.GraphicObject.Tag == "S2") as IMaterialStream;
                var h1 = loaded.Inner.SimulationObjects.Values.Single(o => o.GraphicObject.Tag == "H1")
                    as DWSIM.UnitOperations.UnitOperations.Heater;
                Assert.That(s2.GetTemperature() - 273.15, Is.EqualTo(40.0).Within(Tol), "S2 after the reload");
                Assert.That(h1.OutletTemperature.GetValueOrDefault() - 273.15, Is.EqualTo(80.0).Within(Tol), "H1 after the reload");
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
