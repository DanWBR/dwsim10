//    Compound editing layer: property descriptors, equation catalog, in-place editing.
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces;
using DWSIM.SharedClasses.SystemsOfUnits;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.Thermodynamics.CompoundEditing;
using DWSIM.Thermodynamics.PropertyPackages;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class CompoundEditingTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        // A User compound with water's DIPPR 101 vapor-pressure coefficients and enough constants
        // for the estimation fallbacks. Built by hand so the tests need no database.
        private static ConstantProperties Water()
        {
            var cp = new ConstantProperties
            {
                Name = "Water",
                CAS_Number = "7732-18-5",
                Formula = "H2O",
                Molar_Weight = 18.015,
                Critical_Temperature = 647.13,
                Critical_Pressure = 22055000.0,
                Critical_Volume = 0.05595,
                Critical_Compressibility = 0.229,
                Acentric_Factor = 0.3449,
                Normal_Boiling_Point = 373.15,
                TemperatureOfFusion = 273.15,
                OriginalDB = "User",
                CurrentDB = "User",
                VaporPressureEquation = "101",
                Vapor_Pressure_Constant_A = 73.649,
                Vapor_Pressure_Constant_B = -7258.2,
                Vapor_Pressure_Constant_C = -7.3037,
                Vapor_Pressure_Constant_D = 4.1653e-6,
                Vapor_Pressure_Constant_E = 2.0,
                Vapor_Pressure_TMIN = 273.16,
                Vapor_Pressure_TMAX = 647.13
            };
            return cp;
        }

        // ------------------------------------------------------------------ descriptor table

        // The table is the single source of truth for both editors, so every property of the interface
        // must be covered by exactly one descriptor, one block, or the explicit exclusion list. A new
        // interface member without a home fails here, not silently in the editor.
        [Test]
        public void EveryInterfacePropertyIsDescribedExactlyOnce()
        {
            var interfaceKeys = typeof(ICompoundConstantProperties).GetProperties().Select(p => p.Name).OrderBy(n => n).ToList();
            var tableKeys = CompoundPropertyDescriptors.AllKeys();

            var duplicates = tableKeys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.That(duplicates, Is.Empty, "keys listed more than once");

            var missing = interfaceKeys.Except(tableKeys).ToList();
            var unknown = tableKeys.Except(interfaceKeys).ToList();
            Assert.That(missing, Is.Empty, "interface properties with no descriptor, block or exclusion");
            Assert.That(unknown, Is.Empty, "table keys that are not interface properties");
        }

        [Test]
        public void EveryDescriptorHasHelpText()
        {
            foreach (var d in CompoundPropertyDescriptors.Scalars)
                Assert.That(d.Help.Length, Is.GreaterThan(20), d.Key + " needs a real explanation");
            foreach (var b in CompoundPropertyDescriptors.Blocks)
            {
                Assert.That(b.Help.Length, Is.GreaterThan(40), b.Key + " needs a real explanation");
                Assert.That(b.NoEquationBehaviour.Length, Is.GreaterThan(10), b.Key + " must say what happens without an equation");
            }
            foreach (var g in CompoundPropertyDescriptors.Groups)
                Assert.That(g.Intro.Length, Is.GreaterThan(20), g.DisplayName + " needs an intro");
        }

        [Test]
        public void ScalarDescriptorsRoundTrip()
        {
            var cp = Water();
            foreach (var d in CompoundPropertyDescriptors.Scalars)
            {
                object value;
                switch (d.Kind)
                {
                    case CompoundPropertyKind.Double: value = 2.5; break;
                    case CompoundPropertyKind.NullableDouble: value = 1.25; break;
                    case CompoundPropertyKind.Integer: value = 3; break;
                    case CompoundPropertyKind.Boolean: value = true; break;
                    case CompoundPropertyKind.Text: value = "x" + d.Key; break;
                    case CompoundPropertyKind.Elements:
                    case CompoundPropertyKind.Groups: value = new SortedList { { "1", 2.0 } }; break;
                    default: continue;
                }
                d.SetValue(cp, value);
                var back = d.GetValue(cp);
                if (value is SortedList list)
                {
                    Assert.That(back, Is.InstanceOf<SortedList>(), d.Key);
                    Assert.That(((SortedList)back)["1"], Is.EqualTo(2.0), d.Key);
                    Assert.That(ReferenceEquals(back, list), Is.False, d.Key + " must copy the list");
                }
                else if (value is int i)
                {
                    Assert.That(Convert.ToInt32(back), Is.EqualTo(i), d.Key);
                }
                else
                {
                    Assert.That(back, Is.EqualTo(value), d.Key);
                }
            }

            // a string with the other decimal separator is accepted for numbers
            var mw = CompoundPropertyDescriptors.ByKey("Molar_Weight");
            mw.SetValue(cp, "18,015");
            Assert.That(cp.Molar_Weight, Is.EqualTo(18.015).Within(1e-12));
            mw.SetValue(cp, "18.015");
            Assert.That(cp.Molar_Weight, Is.EqualTo(18.015).Within(1e-12));
            Assert.Throws<FormatException>(() => mw.SetValue(cp, "abc"));

            // a blank clears a nullable, and the boiling point mirrors into the historical NBP field
            var wk = CompoundPropertyDescriptors.ByKey("PF_Watson_K");
            wk.SetValue(cp, "");
            Assert.That(cp.PF_Watson_K, Is.Null);
            CompoundPropertyDescriptors.ByKey("Normal_Boiling_Point").SetValue(cp, 400.0);
            Assert.That(cp.NBP, Is.EqualTo(400.0));
        }

        [Test]
        public void DisplayUnitConversionRoundTrips()
        {
            var cp = Water();
            var si = new SI();
            var eng = new English();
            foreach (var d in CompoundPropertyDescriptors.Scalars.Where(x => x.UnitSelector != null && x.Kind == CompoundPropertyKind.Double))
            {
                d.SetValue(cp, 300.0);
                Assert.That((double)d.GetDisplayValue(cp, si), Is.EqualTo(300.0).Within(1e-9), d.Key + " under SI");

                var shown = (double)d.GetDisplayValue(cp, eng);
                d.SetFromDisplay(cp, eng, shown);
                Assert.That((double)d.GetValue(cp), Is.EqualTo(300.0).Within(1e-6), d.Key + " under English units");
                Assert.That(d.DisplayUnit(eng), Is.Not.Empty, d.Key);
            }
        }

        // ------------------------------------------------------------------ equation catalog

        // The catalog lists ids; the formulas come from the engine. Probe the engine so an id added to
        // GetEquationString without being listed here, or listed here without an engine case, fails.
        [Test]
        public void EquationCatalogMatchesEngine()
        {
            foreach (var id in CompoundEquationCatalog.KnownIds)
            {
                var info = CompoundEquationCatalog.Describe(id);
                Assert.That(info.Formula, Is.Not.EqualTo("Not Defined"), "id " + id + " is listed but the engine has no formula");
                Assert.That(info.PlainDescription.Length, Is.GreaterThan(10), "id " + id + " needs an explanation");
                // generic coefficients can overflow some forms (a division by B - C, a negative base to a
                // fractional power), so only the dispatch is checked here: a listed id must not throw
                Assert.DoesNotThrow(() => PropertyPackage.CalcCSTDepProp(id, 1, 1, 1, 1, 1, 300.0, 500.0), "id " + id + " does not evaluate");
            }

            var known = new HashSet<string>(CompoundEquationCatalog.KnownIds);
            for (int i = 1; i <= 1000; i++)
            {
                var id = i.ToString();
                if (PropertyPackage.GetEquationString(id) != "Not Defined")
                    Assert.That(known.Contains(id), Is.True, "engine knows equation " + id + " but the catalog does not list it");
            }

            Assert.That(CompoundEquationCatalog.Describe("101").CoefficientCount, Is.EqualTo(5));
            Assert.That(CompoundEquationCatalog.Describe("2").CoefficientCount, Is.EqualTo(2));
            Assert.That(CompoundEquationCatalog.Describe("4").UsesCoefficient('E'), Is.False);
            Assert.That(CompoundEquationCatalog.Describe("106").UsesReducedTemperature, Is.True);
            Assert.That(CompoundEquationCatalog.Describe("").IsNotDefined, Is.True);
            Assert.That(CompoundEquationCatalog.Describe("0").IsNotDefined, Is.True);

            var expr = CompoundEquationCatalog.Describe("y = A + B * T");
            Assert.That(expr.IsExpression, Is.True);
            Assert.That(CompoundEquationCatalog.Evaluate("y = A + B * T", 1.0, 2.0, 0, 0, 0, 300.0, 0), Is.EqualTo(601.0).Within(1e-9));
            string msg = "";
            Assert.That(CompoundEquationCatalog.TryValidateExpression("y = A + B * T", ref msg), Is.True, msg);
        }

        [Test]
        public void FormulaWithValuesSubstitutesTheCoefficients()
        {
            var text = CompoundEquationCatalog.FormulaWithValues(CompoundEquationCatalog.Describe("101"), 73.649, -7258.2, -7.3037, 4.1653e-6, 2.0);
            Assert.That(text, Does.Contain("73.649"));
            Assert.That(text, Does.Contain("(-7258.2)"));
            Assert.That(text, Does.Not.Contain(" A "));
        }

        // ------------------------------------------------------------------ blocks

        // The units the raw equation must return for a User (JSON) compound, as the engine reads them.
        // This is what the explainer shows, so it is pinned to the engine's own conversions.
        [Test]
        public void RawEquationUnitsForAUserCompoundMatchTheEngine()
        {
            var cp = Water();
            string Unit(string block) => CompoundPropertyDescriptors.BlockByKey(block).RawEquationUnit(cp);
            Assert.That(Unit("VaporPressure"), Is.EqualTo("Pa"));
            Assert.That(Unit("IdealGasHeatCapacity"), Is.EqualTo("J/(kmol.K)"));
            Assert.That(Unit("EnthalpyOfVaporization"), Is.EqualTo("J/kmol"));
            Assert.That(Unit("LiquidViscosity"), Is.EqualTo("Pa.s"));
            Assert.That(Unit("LiquidDensity"), Is.EqualTo("kg/m3"));
            Assert.That(Unit("LiquidHeatCapacity"), Is.EqualTo("J/(kmol.K)"));
            Assert.That(Unit("SolidDensity"), Does.StartWith("kmol/m3"));
            Assert.That(Unit("SolidHeatCapacity"), Is.EqualTo("J/(kmol.K)"));
            Assert.That(Unit("SurfaceTension"), Is.EqualTo("N/m"));

            foreach (var b in CompoundPropertyDescriptors.Blocks)
                Assert.That(b.EquationIsHonoured(cp), Is.True, b.Key + " must honour the equation for a User compound");

            cp.OriginalDB = "DWSIM";
            Assert.That(CompoundPropertyDescriptors.BlockByKey("VaporPressure").EquationIsHonoured(cp), Is.False,
                        "the built-in database uses a fixed vapor-pressure form");
        }

        [Test]
        public void BlockEvaluatorFollowsTheEngine()
        {
            var cp = Water();
            var pvap = CompoundPropertyDescriptors.BlockByKey("VaporPressure");

            // the DIPPR 101 coefficients of water give 1 atm at the normal boiling point
            var direct = CompoundEquationCatalog.Evaluate("101", 73.649, -7258.2, -7.3037, 4.1653e-6, 2.0, 373.15, 647.13);
            Assert.That(direct, Is.EqualTo(101325.0).Within(2.0).Percent);
            Assert.That(pvap.Evaluator(cp, 373.15), Is.EqualTo(direct).Within(1e-6).Percent, "the block evaluates through the compound");
            Assert.That(pvap.EvaluatorMessage(cp, 373.15), Is.Not.Empty);

            var range = pvap.DefaultRange(cp);
            Assert.That(range.Tmin, Is.EqualTo(273.16));
            Assert.That(range.Tmax, Is.EqualTo(647.13));
            var pts = pvap.Sample(cp, range.Tmin, range.Tmax, 21);
            Assert.That(pts.Count, Is.EqualTo(21));
            Assert.That(pts.Last().Y, Is.GreaterThan(pts.First().Y), "vapor pressure rises with temperature");

            // no equation: the estimate still produces a curve, and the block says which estimate
            cp.VaporPressureEquation = "";
            Assert.That(pvap.Evaluator(cp, 373.15), Is.GreaterThan(0.0));
            Assert.That(pvap.NoEquationBehaviour, Does.Contain("Lee-Kesler"));

            // a block with no range properties still gets a usable default span
            var cpig = CompoundPropertyDescriptors.BlockByKey("IdealGasHeatCapacity");
            var r2 = cpig.DefaultRange(cp);
            Assert.That(r2.Tmax, Is.GreaterThan(r2.Tmin));
            Assert.That(cpig.Keys(), Does.Not.Contain(null));
        }

        // ------------------------------------------------------------------ round-trip fixes

        [Test]
        public void LoadDataDoesNotAccumulateNistGroupsOrElements()
        {
            var cp = Water();
            cp.NISTMODFACGroups.Add("1", 2);
            cp.UNIFACGroups.Add("16", 1);
            var elementsBefore = cp.Elements.Count;

            cp.LoadData(cp.SaveData());
            cp.LoadData(cp.SaveData());

            Assert.That(cp.NISTMODFACGroups.Count, Is.EqualTo(1));
            Assert.That(cp.UNIFACGroups.Count, Is.EqualTo(1));
            Assert.That(cp.Elements.Count, Is.EqualTo(elementsBefore));
        }

        // ------------------------------------------------------------------ editing in place

        private static (DWSIM.DynamicRunner.Flowsheet fs, DWSIM.Thermodynamics.Streams.MaterialStream ms) FlowsheetWithWater()
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Water");
            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            return (fs, ms);
        }

        [Test]
        public void CopyIntoIsExactAndKeepsIdentity()
        {
            var a = Water();
            a.PF_Watson_K = null;
            a.Vapor_Pressure_Tabular_Data.XData.Add(300.0);
            a.Vapor_Pressure_Tabular_Data.YData.Add(3500.0);
            a.UNIFACGroups.Add("16", 1);
            ((IDictionary<string, object>)a.ExtraProperties)["Source"] = "test";
            a.LinkedJsonFile = @"C:\x\water.json";

            var b = new ConstantProperties { Name = "Other", ID = 42, OriginalDB = "ChemSep", PF_Watson_K = 12.0 };
            CompoundEditor.CopyInto(a, b);

            Assert.That(b.Name, Is.EqualTo("Other"), "identity is not copied by default");
            Assert.That(b.ID, Is.EqualTo(42));
            Assert.That(b.OriginalDB, Is.EqualTo("ChemSep"));
            Assert.That(b.Critical_Temperature, Is.EqualTo(a.Critical_Temperature));
            Assert.That(b.Vapor_Pressure_Constant_A, Is.EqualTo(73.649));
            Assert.That(b.PF_Watson_K, Is.Null, "a nullable that is Nothing stays Nothing");
            Assert.That(b.Elements.Count, Is.EqualTo(a.Elements.Count));
            Assert.That(b.UNIFACGroups["16"], Is.EqualTo(1));
            Assert.That(ReferenceEquals(b.UNIFACGroups, a.UNIFACGroups), Is.False);
            Assert.That(b.Vapor_Pressure_Tabular_Data.XData, Is.EqualTo(new[] { 300.0 }));
            Assert.That(ReferenceEquals(b.Vapor_Pressure_Tabular_Data, a.Vapor_Pressure_Tabular_Data), Is.False);
            Assert.That(((IDictionary<string, object>)b.ExtraProperties)["Source"], Is.EqualTo("test"));
            Assert.That(b.LinkedJsonFile, Is.EqualTo(@"C:\x\water.json"));
        }

        // The whole point of the editor: the stream keeps pointing at the same compound object after OK,
        // and an undo restores the old values on that same object.
        [Test]
        public void ApplyInPlaceKeepsIdentityAndUndoRestoresOnTheSameInstance()
        {
            var (fs, ms) = FlowsheetWithWater();
            // a non-default ordering makes IFlowsheet.SelectedCompounds return a copy: the trap the service must avoid
            fs.FlowsheetOptions.CompoundOrderingMode = Enum.GetValues(typeof(DWSIM.Interfaces.Enums.CompoundOrdering))
                .Cast<DWSIM.Interfaces.Enums.CompoundOrdering>().First(v => v != DWSIM.Interfaces.Enums.CompoundOrdering.AsAdded);

            var live = CompoundEditor.FindLive(fs, "Water");
            Assert.That(live, Is.Not.Null);
            Assert.That(ReferenceEquals(ms.Phases[0].Compounds["Water"].ConstantProperties, live), Is.True, "the stream holds the live instance");
            var originalTc = live.Critical_Temperature;

            var snapshot = fs.GetSnapshot(DWSIM.Interfaces.Enums.SnapshotType.Compounds);

            var edited = CompoundEditor.BeginEdit(live);
            edited.Critical_Temperature = 700.0;
            edited.Comments = "edited";
            ms.Calculated = true;

            CompoundEditor.ApplyInPlace(fs, "Water", edited);

            Assert.That(ReferenceEquals(ms.Phases[0].Compounds["Water"].ConstantProperties, live), Is.True, "still the same object after OK");
            Assert.That(live.Critical_Temperature, Is.EqualTo(700.0));
            Assert.That(live.Comments, Is.EqualTo("edited"));
            Assert.That(live.IsModified, Is.True);
            Assert.That(ms.Calculated, Is.False, "results are invalidated");
            Assert.That(ReferenceEquals(edited, live), Is.False, "the clone is not swapped in");

            fs.RestoreSnapshot(snapshot, DWSIM.Interfaces.Enums.SnapshotType.Compounds);

            Assert.That(ReferenceEquals(ms.Phases[0].Compounds["Water"].ConstantProperties, live), Is.True, "undo keeps the same object");
            Assert.That(live.Critical_Temperature, Is.EqualTo(originalTc), "undo restores the old value");
        }

        [Test]
        public void ImportJsonCompoundUpdatesASelectedCompoundInPlace()
        {
            var (fs, ms) = FlowsheetWithWater();
            var live = CompoundEditor.FindLive(fs, "Water");

            var edited = CompoundEditor.BeginEdit(live);
            edited.Critical_Temperature = 650.0;
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dwsim-water-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                CompoundEditor.SaveToJson(edited, path);
                Assert.That(System.IO.File.ReadAllText(path), Does.Not.Contain("LinkedJsonFile"), "the link is not written into the JSON");

                var returned = CompoundEditor.ImportJsonCompound(fs, path);

                Assert.That(ReferenceEquals(returned, live), Is.True);
                Assert.That(ReferenceEquals(ms.Phases[0].Compounds["Water"].ConstantProperties, live), Is.True);
                Assert.That(live.Critical_Temperature, Is.EqualTo(650.0));
                Assert.That(live.LinkedJsonFile, Is.EqualTo(System.IO.Path.GetFullPath(path)));
                Assert.That(CompoundEditor.ResolveLinkedJson(fs, live), Is.EqualTo(System.IO.Path.GetFullPath(path)));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Test]
        public void ImportJsonCompoundAddsANewCompoundToEveryStream()
        {
            var (fs, ms) = FlowsheetWithWater();
            var cp = Water();
            cp.Name = "Purin Test";
            cp.CAS_Number = "";
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dwsim-purin-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                System.IO.File.WriteAllText(path, cp.ExportToJSON());

                var added = CompoundEditor.ImportJsonCompound(fs, path);

                Assert.That(fs.SelectedCompounds.ContainsKey("Purin Test"), Is.True);
                Assert.That(ReferenceEquals(fs.SelectedCompounds["Purin Test"], added), Is.True);
                Assert.That(ReferenceEquals(ms.Phases[0].Compounds["Purin Test"].ConstantProperties, added), Is.True);
                Assert.That(added.OriginalDB, Is.EqualTo("User"));
                Assert.That(added.LinkedJsonFile, Is.EqualTo(System.IO.Path.GetFullPath(path)));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Test]
        public void LinkedJsonFileSurvivesTheSimulationRoundTrip()
        {
            var cp = Water();
            cp.LinkedJsonFile = @"C:\somewhere\water.json";
            var copy = new ConstantProperties();
            copy.LoadData(cp.SaveData());
            Assert.That(copy.LinkedJsonFile, Is.EqualTo(@"C:\somewhere\water.json"));
            Assert.That(cp.ExportToJSON(), Does.Not.Contain("LinkedJsonFile"));
        }

        // ------------------------------------------------------------------ diff and validator

        // The case from the field: a pseudo-compound whose formula, element list and molecular weight
        // disagree, with an acentric factor beyond the Lee-Kesler range and no vapor-pressure equation.
        [Test]
        public void DiffAndValidatorOnAnInconsistentPseudoCompound()
        {
            var original = Water();
            var edited = CompoundEditor.BeginEdit(original);
            edited.Formula = "C14.08H27.75O11.67N1S0.33";
            edited.Molar_Weight = 424.58;
            edited.Acentric_Factor = 0.9;
            edited.VaporPressureEquation = "";

            var issues = CompoundValidator.Validate(edited);
            var codes = issues.Select(i => i.Code).ToList();
            Assert.That(codes, Does.Contain("MW_FORMULA_MISMATCH"));
            var mw = issues.First(i => i.Code == "MW_FORMULA_MISMATCH");
            Assert.That(mw.Message, Does.Contain("408."));
            Assert.That(mw.Message, Does.Contain("424.58"));
            Assert.That(mw.PropertyKey, Is.EqualTo("Molar_Weight"));
            Assert.That(codes, Does.Contain("OMEGA_HIGH_WITH_LK_PVAP"));
            Assert.That(codes, Does.Contain("PVAP_ESTIMATED"));
            Assert.That(codes, Does.Contain("CP_IG_MISSING"));
            Assert.That(codes, Does.Not.Contain("TC_PC_SUSPICIOUS"), "not water's constants copied: it is water");

            var diff = CompoundDiff.Compare(original, edited);
            var expectedCount = CompoundPropertyDescriptors.Scalars.Count
                                + CompoundPropertyDescriptors.Blocks.Sum(b => b.Keys().Count)
                                + CompoundPropertyDescriptors.Blocks.Count;
            Assert.That(diff.Count, Is.EqualTo(expectedCount), "one entry per property plus one curve per block");
            var differing = diff.Where(e => e.Differs).Select(e => e.Key).ToList();
            Assert.That(differing, Does.Contain("Formula"));
            Assert.That(differing, Does.Contain("Molar_Weight"));
            Assert.That(differing, Does.Contain("Elements"));
            Assert.That(differing, Does.Contain("Acentric_Factor"));
            Assert.That(differing, Does.Contain("VaporPressureEquation"));
            Assert.That(differing, Does.Contain("VaporPressure.Curve"), "the estimated curve differs from the DIPPR one");
            Assert.That(diff.First(e => e.Key == "Elements").Detail, Does.Contain("added").Or.Contain("->"));
            Assert.That(differing, Does.Not.Contain("Critical_Temperature"));
        }

        [Test]
        public void DiffIgnoresFloatingNoiseAndEquivalentSentinels()
        {
            var a = Water();
            a.VaporPressureEquation = "";
            var b = CompoundEditor.BeginEdit(a);
            b.Critical_Temperature += 1e-10;
            b.VaporPressureEquation = "0";

            var diff = CompoundDiff.Compare(a, b);
            Assert.That(diff.Where(e => e.Differs).Select(e => e.Key), Is.Empty);
        }

        // The inert from the field whose critical constants were copied from water: it would evaporate
        // like water in any flash. And a solid flagged without a density.
        [Test]
        public void ValidatorFlagsWaterConstantsCopiedOntoAnInertAndSolidsWithoutDensity()
        {
            var cp = Water();
            cp.Name = "Cenizas";
            cp.CAS_Number = "100000";
            cp.Formula = "";
            cp.Molar_Weight = 200.0;
            cp.Critical_Temperature = 647.34;
            cp.Critical_Pressure = 22120000.0;
            cp.IsSolid = true;
            cp.SolidDensityEquation = "";
            cp.SolidDensityAtTs = 0.0;

            var codes = CompoundValidator.Validate(cp).Select(i => i.Code).ToList();
            Assert.That(codes, Does.Contain("TC_PC_SUSPICIOUS"));
            Assert.That(codes, Does.Contain("SOLID_WITHOUT_DENSITY"));
            Assert.That(codes, Does.Not.Contain("MW_FORMULA_MISMATCH"), "no formula, nothing to compare");
        }

        [Test]
        public void ValidatorExplainsThatBuiltInCompoundsIgnoreTheEquationNumber()
        {
            var cp = Water();
            cp.OriginalDB = "DWSIM";
            var issues = CompoundValidator.Validate(cp);
            var ignored = issues.Where(i => i.Code == "EQUATION_IGNORED").ToList();
            Assert.That(ignored.Select(i => i.PropertyKey), Does.Contain("VaporPressureEquation"));
            Assert.That(ignored.First().Message, Does.Contain("User compound"));

            cp.OriginalDB = "User";
            cp.VaporPressureEquation = "999";
            Assert.That(CompoundValidator.Validate(cp).Select(i => i.Code), Does.Contain("EQUATION_UNKNOWN_ID"));

            cp.VaporPressureEquation = "y = A +";
            Assert.That(CompoundValidator.Validate(cp).Select(i => i.Code), Does.Contain("EXPRESSION_INVALID"));

            cp.VaporPressureEquation = "4";
            cp.Vapor_Pressure_Constant_E = 2.0;
            Assert.That(CompoundValidator.Validate(cp).Select(i => i.Code), Does.Contain("COEFF_UNUSED"), "equation 4 does not use E");
        }

        [Test]
        public void ValidatorNeverThrowsOverTheWholeDatabase()
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            Assert.That(fs.AvailableCompounds.Count, Is.GreaterThan(100));
            foreach (var cp in fs.AvailableCompounds.Values)
                Assert.DoesNotThrow(() => CompoundValidator.Validate(cp), cp.Name);
        }

        [Test]
        public void CloneCopiesExtraPropertiesAndIsIndependent()
        {
            var cp = Water();
            ((IDictionary<string, object>)cp.ExtraProperties)["Source"] = "test";
            var copy = (ConstantProperties)cp.Clone();

            Assert.That(((IDictionary<string, object>)copy.ExtraProperties)["Source"], Is.EqualTo("test"));
            Assert.That(ReferenceEquals(copy.ExtraProperties, cp.ExtraProperties), Is.False);
            Assert.That(ReferenceEquals(copy.Elements, cp.Elements), Is.False);

            copy.Critical_Temperature = 700.0;
            Assert.That(cp.Critical_Temperature, Is.EqualTo(647.13));
        }
    }
}
