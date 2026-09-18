//    McCabe-Thiele diagram and equation of state explorer: the two teaching tools against textbook numbers.
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
using System.IO;
using System.Linq;
using DWSIM.Automation.DynamicRunner.EosExplorer;
using DWSIM.Automation.DynamicRunner.McCabeThiele;
using DWSIM.Automation.DynamicRunner.PackageComparison;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.GlobalSettings;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class TeachingToolsTests
    {
        private DWSIM.DynamicRunner.Flowsheet _host = null!;

        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();

            _host = new DWSIM.DynamicRunner.Flowsheet(null, null);
            _host.Init();
            _host.AddCompound("Benzene");
            _host.AddCompound("Toluene");
            _host.AddCompound("Propane");
            _host.AddCompound("Water");
            _host.AddCompound("Ethanol");
            var pp = new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage { Flowsheet = _host };
            _host.AddPropertyPackage(pp);
            var nrtl = new DWSIM.Thermodynamics.PropertyPackages.NRTLPropertyPackage { Flowsheet = _host, Tag = "NRTL" };
            _host.AddPropertyPackage(nrtl);
            var raoult = new DWSIM.Thermodynamics.PropertyPackages.RaoultPropertyPackage { Flowsheet = _host, Tag = "Raoult" };
            _host.AddPropertyPackage(raoult);
        }

        // ------------------------------------------------------------------ McCabe-Thiele

        /// <summary>
        /// Benzene-toluene at 1 atm, xF 0.5, saturated liquid feed, xD 0.95, xB 0.05, R = 1.5 Rmin: the
        /// classic textbook case (McCabe, Smith and Harriott, Example 21.1 family) gives Rmin near 1.1
        /// and about 11 to 13 theoretical stages.
        /// </summary>
        [Test]
        public void BenzeneTolueneDiagramMatchesTheTextbook()
        {
            var input = new McCabeThieleInput
            {
                LightKey = "Benzene", HeavyKey = "Toluene", Pressure = 101325,
                FeedComposition = 0.5, FeedQuality = 1.0, DistillateComposition = 0.95, BottomsComposition = 0.05,
                RefluxAsMultipleOfMinimum = true, RefluxMultiplier = 1.5
            };
            var r = McCabeThieleStudy.Run(_host, input);
            Console.WriteLine(r.TextReport);

            Assert.That(r.Feasible, Is.True, string.Join(" | ", r.Warnings));
            Assert.That(r.EquilibriumX.Length, Is.GreaterThan(40));
            // the curve sits above the diagonal everywhere, benzene being the more volatile
            for (int i = 1; i < r.EquilibriumX.Length - 1; i++)
                Assert.That(r.EquilibriumY[i], Is.GreaterThan(r.EquilibriumX[i]), "y > x at x = " + r.EquilibriumX[i]);
            Assert.That(r.EquilibriumT.First(), Is.EqualTo(383.8).Within(1.5), "toluene boils near 383.8 K");
            Assert.That(r.EquilibriumT.Last(), Is.EqualTo(353.2).Within(1.5), "benzene boils near 353.2 K");

            Assert.That(r.MinimumReflux, Is.EqualTo(1.1).Within(0.2));
            Assert.That(r.RefluxRatio, Is.EqualTo(1.5 * r.MinimumReflux).Within(1e-9));
            Assert.That(r.TheoreticalStages, Is.InRange(10, 14));
            Assert.That(r.FeedStage, Is.InRange(4, 8));
            Assert.That(r.MinimumStages, Is.InRange(6, 9));
            Assert.That(r.Stages.Last().X, Is.LessThanOrEqualTo(0.05));
            Assert.That(r.Stages.First().Y, Is.EqualTo(0.95).Within(1e-9));
            Assert.That(double.IsNaN(r.Azeotrope), Is.True);

            // the case file round-trips
            var path = Path.Combine(Path.GetTempPath(), "mct-test" + McCabeThieleInput.FileExtension);
            input.SaveToFile(path);
            var back = McCabeThieleInput.LoadFromFile(path);
            Assert.That(back.LightKey, Is.EqualTo("Benzene"));
            Assert.That(back.RefluxMultiplier, Is.EqualTo(1.5));
            Assert.That(back.RefluxAsMultipleOfMinimum, Is.True);
        }

        /// <summary>Ethanol-water on NRTL has the azeotrope near x = 0.89; a distillate beyond it is reported as unreachable.</summary>
        [Test]
        public void EthanolWaterAzeotropeIsReported()
        {
            var input = new McCabeThieleInput
            {
                PropertyPackageTag = "NRTL",
                LightKey = "Ethanol", HeavyKey = "Water", Pressure = 101325,
                FeedComposition = 0.3, FeedQuality = 1.0, DistillateComposition = 0.95, BottomsComposition = 0.02,
                RefluxRatio = 3.0
            };
            var r = McCabeThieleStudy.Run(_host, input);
            Console.WriteLine(r.TextReport);
            Assert.That(r.Azeotrope, Is.EqualTo(0.89).Within(0.04));
            Assert.That(r.Feasible, Is.False);
            Assert.That(r.Warnings.Any(w => w.Contains("azeotrope")), Is.True);
        }

        /// <summary>Below the minimum reflux the stages pinch and the construction says so instead of looping.</summary>
        [Test]
        public void RefluxBelowMinimumPinches()
        {
            var input = new McCabeThieleInput
            {
                LightKey = "Benzene", HeavyKey = "Toluene", Pressure = 101325,
                FeedComposition = 0.5, FeedQuality = 1.0, DistillateComposition = 0.95, BottomsComposition = 0.05,
                RefluxRatio = 0.5
            };
            var r = McCabeThieleStudy.Run(_host, input);
            Assert.That(r.Feasible, Is.False);
            Assert.That(r.Warnings.Any(w => w.Contains("below the minimum") || w.Contains("pinched")), Is.True, string.Join(" | ", r.Warnings));
        }

        // ------------------------------------------------------------------ EOS explorer

        /// <summary>
        /// Propane on Peng-Robinson: Zc of the equation is 0.3074, the loop shows below Tc, the
        /// Maxwell pressure at 300 K is within a few percent of the database vapour pressure
        /// (about 9.98 bar), and there is no condensation above Tc.
        /// </summary>
        [Test]
        public void PropaneIsothermsAndMaxwellConstruction()
        {
            var input = new EosExplorerInput { CompoundName = "Propane", Equation = CubicEos.PengRobinson, Temperatures = "300; 340; 369.83; 400" };
            var r = EosExplorerStudy.Run(_host, input);
            Console.WriteLine(r.TextReport);

            Assert.That(r.EquationZc, Is.EqualTo(0.3074).Within(1e-4));
            Assert.That(r.Tc, Is.EqualTo(369.8).Within(1.0));
            Assert.That(r.Isotherms.Count, Is.EqualTo(4));

            var t300 = r.Isotherms[0];
            Assert.That(t300.Subcritical, Is.True);
            Assert.That(t300.SaturationPressure / 1e5, Is.EqualTo(9.98).Within(0.6));
            Assert.That(t300.DatabaseSaturationPressure / 1e5, Is.EqualTo(9.98).Within(0.5));
            Assert.That(t300.LiquidVolume, Is.LessThan(t300.VaporVolume));
            Assert.That(t300.LiquidZ, Is.LessThan(0.1));
            Assert.That(t300.VaporZ, Is.InRange(0.6, 0.95));
            // the loop: the isotherm dips below the saturation pressure between the liquid and vapour volumes
            var loop = t300.Volume.Select((v, i) => new { v, p = t300.Pressure[i] })
                .Where(x => x.v > t300.LiquidVolume && x.v < t300.VaporVolume).Select(x => x.p);
            Assert.That(loop.Min(), Is.LessThan(t300.SaturationPressure));
            Assert.That(loop.Max(), Is.GreaterThan(t300.SaturationPressure));

            var t400 = r.Isotherms[3];
            Assert.That(t400.Subcritical, Is.False);
            Assert.That(double.IsNaN(t400.SaturationPressure), Is.True);
            // a supercritical isotherm falls monotonically with volume
            for (int i = 1; i < t400.Pressure.Length; i++)
                Assert.That(t400.Pressure[i], Is.LessThanOrEqualTo(t400.Pressure[i - 1] + 1e-6));

            Assert.That(r.SaturationT.Length, Is.GreaterThan(30));
            Assert.That(r.SaturationP.Last(), Is.EqualTo(r.Pc).Within(1e-6));
        }

        /// <summary>van der Waals overpredicts the vapour pressure badly and gives Zc = 3/8; the point of showing it beside Peng-Robinson.</summary>
        [Test]
        public void VanDerWaalsShowsItsAge()
        {
            var vdw = EosExplorerStudy.Run(_host, new EosExplorerInput { CompoundName = "Propane", Equation = CubicEos.VanDerWaals, Temperatures = "300" });
            var pr = EosExplorerStudy.Run(_host, new EosExplorerInput { CompoundName = "Propane", Equation = CubicEos.PengRobinson, Temperatures = "300" });
            Assert.That(vdw.EquationZc, Is.EqualTo(0.375).Within(1e-9));
            var devVdw = Math.Abs(vdw.Isotherms[0].SaturationPressure / vdw.Isotherms[0].DatabaseSaturationPressure - 1);
            var devPr = Math.Abs(pr.Isotherms[0].SaturationPressure / pr.Isotherms[0].DatabaseSaturationPressure - 1);
            Console.WriteLine("vdW deviation " + devVdw + ", PR deviation " + devPr);
            Assert.That(devPr, Is.LessThan(devVdw));
            Assert.That(devPr, Is.LessThan(0.05));
        }
    
        // ------------------------------------------------------------------ property package comparison

        /// <summary>
        /// Ethanol-water at 1 atm against five literature points: NRTL (fitted parameters, an
        /// azeotrope) beats Raoult's law (ideal liquid, no azeotrope), and the verdict says so.
        /// </summary>
        [Test]
        public void EthanolWaterComparisonRanksNrtlAboveRaoult()
        {
            var input = new PackageComparisonInput { Compound1 = "Ethanol", Compound2 = "Water", Kind = DiagramKind.Txy, Pressure = 101325, Points = 41, DatasetLabel = "literature" };
            input.SetPackages(new[] { "NRTL", "Raoult" });
            input.SetData(new[]
            {
                new ExperimentalPoint { X1 = 0.10, Y1 = 0.437, T = 359.8, P = 101325 },
                new ExperimentalPoint { X1 = 0.30, Y1 = 0.575, T = 354.8, P = 101325 },
                new ExperimentalPoint { X1 = 0.50, Y1 = 0.652, T = 352.9, P = 101325 },
                new ExperimentalPoint { X1 = 0.70, Y1 = 0.755, T = 351.5, P = 101325 },
                new ExperimentalPoint { X1 = 0.90, Y1 = 0.898, T = 351.3, P = 101325 },
            });
            var r = PackageComparisonStudy.Run(_host, input);
            Console.WriteLine(r.TextReport);

            Assert.That(r.Curves.Count, Is.EqualTo(2));
            var nrtl = r.Curves.First(c => c.Tag == "NRTL");
            var raoult = r.Curves.First(c => c.Tag == "Raoult");
            Assert.That(nrtl.Failed, Is.False, string.Join(" | ", nrtl.Warnings));
            Assert.That(raoult.Failed, Is.False, string.Join(" | ", raoult.Warnings));
            Assert.That(nrtl.PointsCompared, Is.EqualTo(5));
            Assert.That(nrtl.AadBubble, Is.LessThan(1.5), "NRTL within 1.5 K of the data");
            Assert.That(raoult.AadBubble, Is.GreaterThan(nrtl.AadBubble), "Raoult's law worse than NRTL");
            Assert.That(nrtl.Azeotrope, Is.EqualTo(0.89).Within(0.05), "NRTL sees the azeotrope");
            Assert.That(double.IsNaN(raoult.Azeotrope), "Raoult's law cannot see one");
            Assert.That(r.Ranking[0], Is.EqualTo("NRTL"));
            Assert.That(r.Verdict.Any(v => v.Contains("fits best")), string.Join(Environment.NewLine, r.Verdict));
            Assert.That(r.Verdict.Any(v => v.Contains("azeotrope")), string.Join(Environment.NewLine, r.Verdict));

            // the case file round-trips the points and the package list
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "compare_test" + PackageComparisonInput.FileExtension);
            input.SaveToFile(path);
            var back = PackageComparisonInput.LoadFromFile(path);
            Assert.That(back.Data().Count, Is.EqualTo(5));
            Assert.That(back.PackageList(), Is.EqualTo(new[] { "NRTL", "Raoult" }));
            Assert.That(back.Data()[2].Y1, Is.EqualTo(0.652).Within(1e-12));
        }
    
        // ------------------------------------------------------------------ contextual help

        /// <summary>F1 links: every object type has a page, the Portuguese site gets its own page names, unknown tools land on the track index.</summary>
        [Test]
        public void ContextualHelpLinksResolveInBothLanguages()
        {
            Assert.That(ContextualHelp.UrlFor(ObjectType.Heater), Is.EqualTo("https://dwsim.org/tutorials/en/beginner/03-heater-cooler.html"));
            Assert.That(ContextualHelp.UrlFor(ObjectType.Heater, "pt-BR"), Is.EqualTo("https://dwsim.org/tutorials/pt-BR/iniciante/03-aquecedor-resfriador.html"));
            Assert.That(ContextualHelp.UrlFor(ObjectType.OT_Recycle, "pt-BR"), Does.EndWith("fundamentos/03-reciclos-e-convergencia.html"));
            Assert.That(ContextualHelp.UrlFor(ObjectType.GO_Text), Does.EndWith("en/fundamentals/index.html"), "a decoration falls back to the track index");
            Assert.That(ContextualHelp.UrlForTool("mccabe-thiele", "pt-BR"), Does.EndWith("fundamentos/07-as-ferramentas-de-aprendizado.html#mccabe-thiele-diagram"));
            Assert.That(ContextualHelp.UrlForTool("no-such-tool"), Does.EndWith("en/fundamentals/index.html"));
            foreach (ObjectType t in Enum.GetValues(typeof(ObjectType)))
                Assert.That(ContextualHelp.UrlFor(t, "pt-BR"), Does.StartWith("https://dwsim.org/tutorials/"), t.ToString());
        }
    }
}
