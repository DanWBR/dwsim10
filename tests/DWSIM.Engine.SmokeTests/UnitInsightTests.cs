//    Unit operation insight: the written explanations against hand-built flowsheets.
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
using System.Linq;
using DWSIM.Automation.DynamicRunner.Insight;
using DWSIM.Automation.DynamicRunner.LiveSliders;
using DWSIM.Automation.DynamicRunner.Scenarios;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class UnitInsightTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.DynamicRunner.Flowsheet NewFlowsheet(params string[] compounds)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            foreach (var c in compounds) fs.AddCompound(c);
            var pp = new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage { Flowsheet = fs };
            fs.AddPropertyPackage(pp);
            return fs;
        }

        private static MaterialStream Stream(DWSIM.DynamicRunner.Flowsheet fs, string tag)
        {
            var o = fs.AddObject(ObjectType.MaterialStream, 0, 0, tag);
            var ms = (MaterialStream)fs.SimulationObjects[o.Name];
            ms.SetFlowsheet(fs);
            ms.SetPropertyPackage(fs.PropertyPackages.Values.First());
            return ms;
        }

        private static T Unit<T>(DWSIM.DynamicRunner.Flowsheet fs, ObjectType type, string tag) where T : DWSIM.UnitOperations.UnitOperations.UnitOpBaseClass
        {
            var o = (T)fs.SimulationObjects[fs.AddObject(type, 0, 0, tag).Name];
            o.SetFlowsheet(fs);
            o.PropertyPackage = (DWSIM.Thermodynamics.PropertyPackages.PropertyPackage)fs.PropertyPackages.Values.First();
            return o;
        }

        private static void Solve(DWSIM.DynamicRunner.Flowsheet fs)
        {
            var errors = fs.SolveFlowsheet2();
            Assert.That(errors, Is.Empty, string.Join("; ", errors.Select(e => e.Message)));
        }

        private static InsightResult Explain(DWSIM.DynamicRunner.Flowsheet fs, string tag)
        {
            var obj = fs.SimulationObjects.Values.First(o => o.GraphicObject.Tag == tag);
            var r = UnitInsightStudy.Explain(fs, obj);
            Console.WriteLine(r.TextReport);
            Console.WriteLine();
            Assert.That(r.Warnings.Where(w => w.StartsWith("The explanation stopped early")), Is.Empty, string.Join(" | ", r.Warnings));
            return r;
        }

        /// <summary>
        /// Benzene-toluene at 1 atm and 368 K is two-phase: the stream explanation writes the
        /// Rachford-Rice function, whose value at the solved vapour fraction is zero, and the
        /// separator fed by it closes its balances and repeats the same split.
        /// </summary>
        [Test]
        public void FlashAndSeparatorAreExplainedWithRachfordRice()
        {
            var fs = NewFlowsheet("Benzene", "Toluene");
            var feed = Stream(fs, "Feed");
            var vap = Stream(fs, "Vapour");
            var liq = Stream(fs, "Liquid");
            var vessel = Unit<Vessel>(fs, ObjectType.Vessel, "V-1");
            fs.ConnectObjects(feed.GraphicObject, vessel.GraphicObject, 0, 0);
            fs.ConnectObjects(vessel.GraphicObject, vap.GraphicObject, 0, 0);
            fs.ConnectObjects(vessel.GraphicObject, liq.GraphicObject, 1, 0);
            feed.SetTemperature(368.0);
            feed.SetPressure(101325.0);
            feed.SetMolarFlow(10.0);
            feed.SetOverallComposition(new[] { 0.5, 0.5 });
            Solve(fs);

            var r = Explain(fs, "Feed");
            Assert.That(r.Title, Does.Contain("how its state was found"));
            Assert.That(r.Charts.Any(c => c.Title.Contains("Rachford-Rice")), "the stream gets a Rachford-Rice chart");
            var rr = r.Charts.First(c => c.Title.Contains("Rachford-Rice"));
            var solution = rr.Series.First(s => s.Title == "solution");
            Assert.That(solution.Y[0], Is.EqualTo(0).Within(1e-3), "f(beta) vanishes at the flash's own vapour fraction");
            Assert.That(solution.X[0], Is.GreaterThan(0.05).And.LessThan(0.95));
            Assert.That(r.Lines.Any(l => l.Contains("between the bubble and dew points")), string.Join("\n", r.Lines));
            Assert.That(r.Tables.First(t => t.Title.StartsWith("Composition")).Rows.Count, Is.EqualTo(2));

            var v = Explain(fs, "V-1");
            Assert.That(v.Tables.Any(t => t.Title.StartsWith("Molar flows")));
            Assert.That(v.Lines.Any(l => l.Contains("which closes")), "the energy balance closes: " + string.Join("\n", v.Lines));
            Assert.That(v.Charts.Any(c => c.Title.Contains("Rachford-Rice")));
        }

        /// <summary>
        /// A heater, a valve and a compressor on propane: the heater's energy balance reproduces its
        /// duty and draws a heating curve that crosses the boiling plateau, the valve is isenthalpic
        /// and the compressor's isentropic efficiency comes back as the 75 % it was given.
        /// </summary>
        [Test]
        public void HeaterValveAndCompressorAreExplained()
        {
            var fs = NewFlowsheet("Propane");
            var s1 = Stream(fs, "S1"); var s2 = Stream(fs, "S2"); var s3 = Stream(fs, "S3"); var s4 = Stream(fs, "S4");
            var heater = Unit<Heater>(fs, ObjectType.Heater, "H-1");
            var valve = Unit<Valve>(fs, ObjectType.Valve, "VLV-1");
            var comp = Unit<Compressor>(fs, ObjectType.Compressor, "C-1");
            var e1 = fs.AddObject(ObjectType.EnergyStream, 0, 0, "E1");
            var e2 = fs.AddObject(ObjectType.EnergyStream, 0, 0, "E2");
            fs.ConnectObjects(s1.GraphicObject, heater.GraphicObject, 0, 0);
            fs.ConnectObjects(heater.GraphicObject, s2.GraphicObject, 0, 0);
            fs.ConnectObjects(e1.GraphicObject, heater.GraphicObject, 0, 1);
            fs.ConnectObjects(s2.GraphicObject, valve.GraphicObject, 0, 0);
            fs.ConnectObjects(valve.GraphicObject, s3.GraphicObject, 0, 0);
            fs.ConnectObjects(s3.GraphicObject, comp.GraphicObject, 0, 0);
            fs.ConnectObjects(comp.GraphicObject, s4.GraphicObject, 0, 0);
            fs.ConnectObjects(e2.GraphicObject, comp.GraphicObject, 0, 1);
            s1.SetTemperature(280.0);          // subcooled liquid at 15 bar
            s1.SetPressure(15e5);
            s1.SetMassFlow(1.0);
            s1.SetOverallComposition(new[] { 1.0 });
            heater.CalcMode = Heater.CalculationMode.OutletTemperature;
            heater.OutletTemperature = 330.0;  // superheated vapour at 15 bar (Tsat ~ 316 K)
            valve.CalcMode = Valve.CalculationMode.OutletPressure;
            valve.OutletPressure = 5e5;
            comp.CalcMode = Compressor.CalculationMode.OutletPressure;
            comp.POut = 15e5;
            comp.AdiabaticEfficiency = 75.0;
            Solve(fs);

            var h = Explain(fs, "H-1");
            Assert.That(h.Lines.Any(l => l.Contains("is calculated in the mode")));
            Assert.That(h.Charts.Any(c => c.Title.StartsWith("Heating curve")), "heating curve drawn");
            var vf = h.Charts.First(c => c.Title.Contains("Vapour fraction")).Series[0];
            Assert.That(vf.Y.First(), Is.EqualTo(0).Within(1e-6), "starts liquid");
            Assert.That(vf.Y.Last(), Is.EqualTo(1).Within(1e-6), "ends vapour");

            var v = Explain(fs, "VLV-1");
            Assert.That(v.Lines.Any(l => l.Contains("H_out = H_in")));
            Assert.That(v.Charts.Any(c => c.Title == "Isenthalpic path"));

            var c = Explain(fs, "C-1");
            var eff = c.Lines.First(l => l.StartsWith("Isentropic efficiency"));
            Console.WriteLine(eff);
            Assert.That(eff, Does.Contain("= 75.0 %").Or.Contain("= 74.9 %").Or.Contain("= 75.1 %"), eff);
            Assert.That(c.Charts.Any(ch => ch.Title.StartsWith("Isentropic path")));
        }

        /// <summary>
        /// A counter-current exchanger cooling hot water with cold water: the T-Q diagram has two
        /// straight lines, the pinch is at an end and equals the smaller terminal difference, and
        /// U A = Q / LMTD.
        /// </summary>
        [Test]
        public void HeatExchangerGetsATQDiagram()
        {
            var fs = NewFlowsheet("Water");
            var hin = Stream(fs, "HotIn"); var hout = Stream(fs, "HotOut");
            var cin = Stream(fs, "ColdIn"); var cout = Stream(fs, "ColdOut");
            var hx = Unit<HeatExchanger>(fs, ObjectType.HeatExchanger, "HX-1");
            fs.ConnectObjects(hin.GraphicObject, hx.GraphicObject, 0, 0);
            fs.ConnectObjects(cin.GraphicObject, hx.GraphicObject, 0, 1);
            fs.ConnectObjects(hx.GraphicObject, hout.GraphicObject, 0, 0);
            fs.ConnectObjects(hx.GraphicObject, cout.GraphicObject, 1, 0);
            foreach (var s in new[] { hin, cin }) { s.SetPressure(3e5); s.SetMassFlow(1.0); s.SetOverallComposition(new[] { 1.0 }); }
            hin.SetTemperature(360.0);
            cin.SetTemperature(300.0);
            hx.CalculationMode = HeatExchangerCalcMode.CalcBothTemp_UA;
            hx.OverallCoefficient = 1000.0;
            hx.Area = 5.0;
            hx.FlowDir = FlowDirection.CounterCurrent;
            Solve(fs);

            var r = Explain(fs, "HX-1");
            var tq = r.Charts.FirstOrDefault(c => c.Title == "T-Q diagram");
            Assert.That(tq, Is.Not.Null, "T-Q chart drawn");
            Assert.That(tq.Series.Count, Is.EqualTo(3));
            var pinchLine = r.Lines.First(l => l.StartsWith("The two lines come closest"));
            Console.WriteLine(pinchLine);
            Assert.That(r.Lines.Any(l => l.Contains("LMTD = (dT1 - dT2)")), string.Join("\n", r.Lines));
            Assert.That(r.Lines.Any(l => l.Contains("U A = Q / (F LMTD)")));
            // equal flows of the same fluid: parallel lines, the approach is the same at both ends and equals the pinch
            double dt1 = hin.GetTemperature() - cout.GetTemperature();
            var pinchValue = double.Parse(pinchLine.Split(new[] { "a gap of " }, StringSplitOptions.None)[1].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(pinchValue, Is.EqualTo(dt1).Within(0.5));
        }

        /// <summary>
        /// A liquid-phase CSTR with first-order Arrhenius kinetics: the Levenspiel plot's CSTR
        /// rectangle at the reactor's conversion reproduces the reactor volume, since the rate the
        /// plot evaluates at the outlet is the rate the reactor ran at.
        /// </summary>
        [Test]
        public void KineticCSTRGetsALevenspielPlotThatReproducesItsVolume()
        {
            var fs = NewFlowsheet("Ethyl acetate", "Water", "Acetic acid", "Ethanol");
            var feed = Stream(fs, "Feed");
            var prod = Stream(fs, "Product");
            var vent = Stream(fs, "Vent");
            var cstr = Unit<Reactor_CSTR>(fs, ObjectType.RCT_CSTR, "R-1");
            var e = fs.AddObject(ObjectType.EnergyStream, 0, 0, "Q");
            fs.ConnectObjects(feed.GraphicObject, cstr.GraphicObject, 0, 0);
            fs.ConnectObjects(cstr.GraphicObject, vent.GraphicObject, 0, 0);
            fs.ConnectObjects(cstr.GraphicObject, prod.GraphicObject, 1, 0);
            fs.ConnectObjects(e.GraphicObject, cstr.GraphicObject, 0, 1);
            // hydrolysis of ethyl acetate in a large excess of water, first order in the ester
            var rxn = fs.CreateKineticReaction("Hydrolysis", "", new Dictionary<string, double> { { "Ethyl acetate", -1 }, { "Water", -1 }, { "Acetic acid", 1 }, { "Ethanol", 1 } },
                new Dictionary<string, double> { { "Ethyl acetate", 1 }, { "Water", 0 }, { "Acetic acid", 0 }, { "Ethanol", 0 } },
                new Dictionary<string, double> { { "Ethyl acetate", 0 }, { "Water", 0 }, { "Acetic acid", 0 }, { "Ethanol", 0 } },
                "Ethyl acetate", "Liquid", "Molar Concentration", "mol/m3", "mol/[m3.s]", 0.5, 30000.0, 0.0, 0.0, "", "");
            fs.AddReaction(rxn);
            var set = fs.CreateReactionSet("Set", "");
            fs.AddReactionSet(set);
            fs.AddReactionToSet(rxn.ID, set.ID, true, 0);
            cstr.ReactionSetID = set.ID;
            cstr.ReactorOperationMode = OperationMode.Isothermic;
            cstr.Volume = 2.0;
            cstr.Headspace = 0.0;
            feed.SetTemperature(330.0);
            feed.SetPressure(101325.0);
            feed.SetMolarFlow(20.0);
            feed.SetOverallComposition(new[] { 0.05, 0.95, 0.0, 0.0 });
            Solve(fs);

            var r = Explain(fs, "R-1");
            var lev = r.Charts.FirstOrDefault(c => c.Title.StartsWith("Levenspiel"));
            Assert.That(lev, Is.Not.Null, "Levenspiel chart drawn: " + string.Join("\n", r.Lines));
            var line = r.Lines.First(l => l.Contains("V_CSTR = "));
            Console.WriteLine(line);
            var vc = double.Parse(line.Split(new[] { "V_CSTR = " }, StringSplitOptions.None)[1].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(vc, Is.EqualTo(2.0).Within(0.3), "the rectangle at the outlet rate gives the reactor volume back");
            var vp = double.Parse(line.Split(new[] { "V_PFR = " }, StringSplitOptions.None)[1].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(vp, Is.LessThan(vc), "a PFR needs less volume for a rate that falls with conversion");
        }

        /// <summary>
        /// The extractive distillation sample: every rigorous column gets its balances, its
        /// reflux, the key pair with Fenske and Underwood, and the three stage profiles.
        /// </summary>
        [Test]
        public void RigorousColumnsAreExplainedWithProfilesAndTheKeyPair()
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !System.IO.Directory.Exists(System.IO.Path.Combine(folder, "tests", "flowsheets")))
                folder = System.IO.Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null);
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.LoadZippedXML(System.IO.Path.Combine(folder, "tests", "flowsheets", "ExtractiveDistillation.dwxmz"));
            Solve(fs);

            var columns = fs.SimulationObjects.Values.Where(o => o.GraphicObject.ObjectType == ObjectType.DistillationColumn).ToList();
            Assert.That(columns, Is.Not.Empty);
            foreach (var col in columns)
            {
                var r = Explain(fs, col.GraphicObject.Tag);
                Assert.That(r.Charts.Select(ch => ch.Title), Is.EquivalentTo(new[] { "Temperature profile", "Internal flows", "Liquid composition profile" }));
                Assert.That(r.Tables.Any(t => t.Title.StartsWith("Molar flows")));
                Assert.That(r.Lines.Any(l => l.Contains("R = L / D")), string.Join(Environment.NewLine, r.Lines));
                Assert.That(r.Lines.Any(l => l.StartsWith("The feed ") && l.Contains("enters on stage")), string.Join(Environment.NewLine, r.Lines));
                Assert.That(r.Lines.Any(l => l.StartsWith("Fenske: N_min")), "Fenske line: " + string.Join(Environment.NewLine, r.Lines));
            }
        }

        /// <summary>
        /// Scenario comparison on the propane train: raising the heater outlet temperature is the
        /// one specification that changed, the downstream stream and compressor results move, the
        /// feed stream does not, and the snapshot survives a file round trip.
        /// </summary>
        [Test]
        public void ScenarioComparisonFindsTheChangedSpecAndItsEffects()
        {
            var fs = NewFlowsheet("Propane");
            var s1 = Stream(fs, "S1"); var s2 = Stream(fs, "S2"); var s3 = Stream(fs, "S3");
            var heater = Unit<Heater>(fs, ObjectType.Heater, "H-1");
            var comp = Unit<Compressor>(fs, ObjectType.Compressor, "C-1");
            var e1 = fs.AddObject(ObjectType.EnergyStream, 0, 0, "E1");
            var e2 = fs.AddObject(ObjectType.EnergyStream, 0, 0, "E2");
            fs.ConnectObjects(s1.GraphicObject, heater.GraphicObject, 0, 0);
            fs.ConnectObjects(heater.GraphicObject, s2.GraphicObject, 0, 0);
            fs.ConnectObjects(e1.GraphicObject, heater.GraphicObject, 0, 1);
            fs.ConnectObjects(s2.GraphicObject, comp.GraphicObject, 0, 0);
            fs.ConnectObjects(comp.GraphicObject, s3.GraphicObject, 0, 0);
            fs.ConnectObjects(e2.GraphicObject, comp.GraphicObject, 0, 1);
            s1.SetTemperature(300.0); s1.SetPressure(5e5); s1.SetMassFlow(1.0); s1.SetOverallComposition(new[] { 1.0 });
            heater.CalcMode = Heater.CalculationMode.OutletTemperature;
            heater.OutletTemperature = 320.0;
            comp.CalcMode = Compressor.CalculationMode.OutletPressure;
            comp.POut = 15e5;
            Solve(fs);
            var a = ScenarioComparison.Snapshot(fs, "base");
            Assert.That(a.Values.Count, Is.GreaterThan(20));

            heater.OutletTemperature = 340.0;
            Solve(fs);
            var b = ScenarioComparison.Snapshot(fs, "hotter");
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scenario_test" + ScenarioSnapshot.FileExtension);
            b.SaveToFile(path);
            var b2 = ScenarioSnapshot.LoadFromFile(path);
            Assert.That(b2.Values.Count, Is.EqualTo(b.Values.Count));

            var r = ScenarioComparison.Compare(a, b2);
            Console.WriteLine(r.TextReport);
            var inputs = r.Differences.Where(d => d.Changed && d.IsInput).ToList();
            Assert.That(inputs.Any(d => d.ObjectTag == "H-1" && d.Property.Contains("PROP_HT") ), "the heater outlet temperature is the changed spec: " + string.Join(", ", inputs.Select(d => d.ObjectTag + " " + d.Property)));
            Assert.That(r.Differences.Any(d => d.Changed && d.ObjectTag == "S3" && !d.IsInput), "the compressor outlet moved");
            Assert.That(r.Differences.Any(d => d.Changed && d.ObjectTag == "C-1" && !d.IsInput), "the compressor results moved");
            Assert.That(r.Differences.Where(d => d.ObjectTag == "S1" && !d.IsInput).All(d => !d.Changed), "the feed did not move");
            Assert.That(r.Summary.Any(l => l.StartsWith("1 specification(s) changed")), string.Join(Environment.NewLine, r.Summary));
            Assert.That(r.Differences[0].IsInput && r.Differences[0].Changed, "changed specifications come first");
        }

        /// <summary>
        /// Live sliders on the same train: the heater offers its outlet temperature as a
        /// specification (and the feed its T, P and flow), the compressor offers none of its results;
        /// applying two slider positions re-solves and the watched compressor power follows.
        /// </summary>
        [Test]
        public void LiveSlidersOfferSpecificationsAndResolveOnApply()
        {
            var fs = NewFlowsheet("Propane");
            var s1 = Stream(fs, "S1"); var s2 = Stream(fs, "S2"); var s3 = Stream(fs, "S3");
            var heater = Unit<Heater>(fs, ObjectType.Heater, "H-1");
            var comp = Unit<Compressor>(fs, ObjectType.Compressor, "C-1");
            var e1 = fs.AddObject(ObjectType.EnergyStream, 0, 0, "E1");
            var e2 = fs.AddObject(ObjectType.EnergyStream, 0, 0, "E2");
            fs.ConnectObjects(s1.GraphicObject, heater.GraphicObject, 0, 0);
            fs.ConnectObjects(heater.GraphicObject, s2.GraphicObject, 0, 0);
            fs.ConnectObjects(e1.GraphicObject, heater.GraphicObject, 0, 1);
            fs.ConnectObjects(s2.GraphicObject, comp.GraphicObject, 0, 0);
            fs.ConnectObjects(comp.GraphicObject, s3.GraphicObject, 0, 0);
            fs.ConnectObjects(e2.GraphicObject, comp.GraphicObject, 0, 1);
            s1.SetTemperature(300.0); s1.SetPressure(5e5); s1.SetMassFlow(1.0); s1.SetOverallComposition(new[] { 1.0 });
            heater.CalcMode = Heater.CalculationMode.OutletTemperature;
            heater.OutletTemperature = 320.0;
            comp.CalcMode = Compressor.CalculationMode.OutletPressure;
            comp.POut = 15e5;
            Solve(fs);

            var specObjects = LiveSliderStudy.ObjectsWithSpecifications(fs).Select(o => o.GraphicObject.Tag).ToList();
            Console.WriteLine("objects with specifications: " + string.Join(", ", specObjects));
            Assert.That(specObjects, Does.Contain("H-1").And.Contain("S1").And.Contain("C-1"));
            Assert.That(specObjects, Does.Not.Contain("S2").And.Not.Contain("E1"));
            var heaterSpecs = LiveSliderStudy.Specifications(fs, heater);
            Console.WriteLine("heater specs: " + string.Join(", ", heaterSpecs.Select(c => c.Label + " = " + c.Value)));
            Assert.That(heaterSpecs.Count, Is.EqualTo(1), "one spec in outlet-temperature mode");
            Assert.That(heaterSpecs[0].Name, Does.Contain("Outlet Temperature"));
            var compResults = LiveSliderStudy.Results(fs, comp);
            Assert.That(compResults.Any(c => c.Name.Contains("Power")), "the compressor power is a result");

            var slider = new SliderDefinition { ObjectName = heater.Name, Property = heaterSpecs[0].Property };
            double lo, hi;
            LiveSliderStudy.DefaultRange(heaterSpecs[0].Value, heaterSpecs[0].Unit, out lo, out hi);
            slider.Min = lo; slider.Max = hi;
            var watch = new WatchDefinition { ObjectName = comp.Name, Property = compResults.First(c => c.Name.Contains("Power")).Property };
            var t0 = LiveSliderStudy.GetValue(fs, heater.Name, slider.Property);
            var a = LiveSliderStudy.Apply(fs, new[] { slider }, new[] { t0 }, new[] { watch });
            var b = LiveSliderStudy.Apply(fs, new[] { slider }, new[] { t0 + 20 }, new[] { watch });
            Console.WriteLine("power at T0: " + a.Outputs[0] + ", at T0 + 20: " + b.Outputs[0] + " (" + b.Seconds.ToString("F2") + " s)");
            Assert.That(a.Solved && b.Solved, a.Error + " " + b.Error);
            Assert.That(b.Outputs[0], Is.GreaterThan(a.Outputs[0] * 1.02), "a hotter suction takes more power");

            var input = new LiveSliderInput();
            input.SetSliders(new[] { slider }); input.SetWatches(new[] { watch });
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sliders_test" + LiveSliderInput.FileExtension);
            input.SaveToFile(path);
            var back = LiveSliderInput.LoadFromFile(path);
            Assert.That(back.Sliders().Count, Is.EqualTo(1));
            Assert.That(back.Sliders()[0].Max, Is.EqualTo(hi).Within(1e-9));
            Assert.That(back.Watches()[0].Property, Is.EqualTo(watch.Property));
        }

        /// <summary>Mixer and splitter: the balance tables and the pressure rule.</summary>
        [Test]
        public void MixerAndSplitterAreExplained()
        {
            var fs = NewFlowsheet("Benzene", "Toluene");
            var a = Stream(fs, "A"); var b = Stream(fs, "B"); var m = Stream(fs, "M"); var s1 = Stream(fs, "S1"); var s2 = Stream(fs, "S2");
            var mixer = Unit<Mixer>(fs, ObjectType.NodeIn, "MIX-1");
            var splitter = Unit<Splitter>(fs, ObjectType.NodeOut, "SPL-1");
            fs.ConnectObjects(a.GraphicObject, mixer.GraphicObject, 0, 0);
            fs.ConnectObjects(b.GraphicObject, mixer.GraphicObject, 0, 1);
            fs.ConnectObjects(mixer.GraphicObject, m.GraphicObject, 0, 0);
            fs.ConnectObjects(m.GraphicObject, splitter.GraphicObject, 0, 0);
            fs.ConnectObjects(splitter.GraphicObject, s1.GraphicObject, 0, 0);
            fs.ConnectObjects(splitter.GraphicObject, s2.GraphicObject, 1, 0);
            a.SetTemperature(300); a.SetPressure(2e5); a.SetMassFlow(1.0); a.SetOverallComposition(new[] { 1.0, 0.0 });
            b.SetTemperature(340); b.SetPressure(3e5); b.SetMassFlow(2.0); b.SetOverallComposition(new[] { 0.0, 1.0 });
            splitter.Ratios.Clear(); splitter.Ratios.Add(0.3); splitter.Ratios.Add(0.7); splitter.Ratios.Add(0.0);
            Solve(fs);

            var r = Explain(fs, "MIX-1");
            Assert.That(r.Tables[0].Rows.Count, Is.EqualTo(2));
            Assert.That(r.Lines.Any(l => l.Contains("follows the rule \"Minimum")), string.Join("\n", r.Lines));
            var sp = Explain(fs, "SPL-1");
            Assert.That(sp.Lines.Any(l => l.Contains("0.3000 of")), string.Join("\n", sp.Lines));
        }
    }
}
