//    Spec calculation modes in a full flowsheet solve: the flowsheet option, the per-spec mode and
//    the reference object of BeforeObject/AfterObject, in both flowsheet solvers.
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
using System.Threading.Tasks;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;
using NUnit.Framework;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// A heater H1 takes S1 (water, 25 C, 2 bar, 1 kg/s) to S2 at 40 C, with E1 as its energy
    /// stream; a spec writes the heater's outlet temperature (PROP_HT_2). "Y + 10" counts the times
    /// the spec ran (50 C after exactly one run). Reading X from S2, which starts at 10 C and gets
    /// 40 C from the heater, tells when the spec ran: before the heater (20 C everywhere), after S2
    /// (S2 40 C, heater set to 50 C) or after the flowsheet, which solves it again (50 C everywhere).
    /// Every case runs in both solvers, FlowsheetSolver and FlowsheetSolver2.
    /// </summary>
    [TestFixture]
    public class SpecCalculationModeTests
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
            public MaterialStreamBuilder S1, S2;
            public EnergyStreamBuilder E1;
            public HeaterBuilder H1;
            public SpecBuilder Spec;

            public double S2C => S2.TemperatureK - 273.15;
            public double H1C => H1.Object.OutletTemperature.GetValueOrDefault() - 273.15;
        }

        private static Case HeaterCase(string name)
        {
            var c = new Case();
            c.Fs = FluentFlowsheet.Create(name).WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            c.S1 = c.Fs.AddMaterialStream("S1").At(25.0.Celsius(), 2.0.Bar()).WithMassFlow(1.0.KgPerSecond());
            c.S2 = c.Fs.AddMaterialStream("S2").At(10.0.Celsius(), 2.0.Bar()).WithMassFlow(1.0.KgPerSecond());
            c.E1 = c.Fs.AddEnergyStream("E1");
            c.H1 = c.Fs.AddHeater("H1").WithOutletTemperature(40.0.Celsius())
                .ConnectFeed(c.S1).ConnectProduct(c.S2).ConnectEnergyFeed(c.E1, 1);
            return c;
        }

        /// <summary>The heater case with a spec on the heater's outlet temperature, X read from <paramref name="sourceTag"/>.</summary>
        private static Case SpecCase(string sourceTag, string expression, SpecCalcMode global)
        {
            var c = HeaterCase("SpecMode");
            c.Spec = c.Fs.AddSpec("SPEC-1").WithSource(sourceTag, "PROP_MS_0").WithTarget("H1", "PROP_HT_2")
                .WithExpression(expression).WithFlowsheetCalculationMode(global);
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

        /// <summary>
        /// A spec left at GlobalSetting runs once, at the point the flowsheet option names.
        /// </summary>
        [TestCase(SpecCalcMode.AfterSourceObject, "S2", "X + 10", 40.0, 50.0, false)]
        [TestCase(SpecCalcMode.AfterSourceObject, "S2", "X + 10", 40.0, 50.0, true)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "S2", "X + 10", 20.0, 20.0, false)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "S2", "X + 10", 20.0, 20.0, true)]
        [TestCase(SpecCalcMode.BeforeFlowsheet, "S2", "X + 10", 20.0, 20.0, false)]
        [TestCase(SpecCalcMode.BeforeFlowsheet, "S2", "X + 10", 20.0, 20.0, true)]
        [TestCase(SpecCalcMode.AfterFlowsheet, "S2", "X + 10", 50.0, 50.0, false)]
        [TestCase(SpecCalcMode.AfterFlowsheet, "S2", "X + 10", 50.0, 50.0, true)]
        [TestCase(SpecCalcMode.AfterSourceObject, "S1", "Y + 10", 50.0, 50.0, false)]
        [TestCase(SpecCalcMode.AfterSourceObject, "S1", "Y + 10", 50.0, 50.0, true)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "S1", "Y + 10", 50.0, 50.0, false)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "S1", "Y + 10", 50.0, 50.0, true)]
        [TestCase(SpecCalcMode.BeforeFlowsheet, "S1", "Y + 10", 50.0, 50.0, false)]
        [TestCase(SpecCalcMode.BeforeFlowsheet, "S1", "Y + 10", 50.0, 50.0, true)]
        [TestCase(SpecCalcMode.AfterFlowsheet, "S1", "Y + 10", 50.0, 50.0, false)]
        [TestCase(SpecCalcMode.AfterFlowsheet, "S1", "Y + 10", 50.0, 50.0, true)]
        [TestCase(SpecCalcMode.AfterSourceObject, "S1", "X + 30", 55.0, 55.0, false)]
        [TestCase(SpecCalcMode.AfterSourceObject, "S1", "X + 30", 55.0, 55.0, true)]
        public void AGlobalSettingSpecFollowsTheFlowsheetOption(SpecCalcMode global, string source, string expression,
            double s2, double h1, bool solver2)
        {
            var c = SpecCase(source, expression, global);
            SolveClean(c.Fs, solver2);
            Check(c, s2, h1, $"GlobalSetting under {global}, X from {source}, '{expression}'");
        }

        // ------------------------------------------------------------------ BeforeObject / AfterObject

        /// <summary>
        /// BeforeObject and AfterObject run the spec once, right before or after the reference
        /// object: a unit operation, a material stream or an energy stream. The flowsheet option
        /// (AfterSourceObject) does not run it a second time.
        /// </summary>
        [TestCase(SpecCalcMode2.BeforeObject, "H1", "Y + 10", 50.0, 50.0, false)]
        [TestCase(SpecCalcMode2.BeforeObject, "H1", "Y + 10", 50.0, 50.0, true)]
        [TestCase(SpecCalcMode2.AfterObject, "H1", "Y + 10", 40.0, 50.0, false)]
        [TestCase(SpecCalcMode2.AfterObject, "H1", "Y + 10", 40.0, 50.0, true)]
        [TestCase(SpecCalcMode2.BeforeObject, "H1", "X + 30", 55.0, 55.0, false)]
        [TestCase(SpecCalcMode2.AfterObject, "H1", "X + 30", 40.0, 55.0, false)]
        [TestCase(SpecCalcMode2.AfterObject, "H1", "X + 30", 40.0, 55.0, true)]
        [TestCase(SpecCalcMode2.BeforeObject, "S1", "Y + 10", 50.0, 50.0, false)]
        [TestCase(SpecCalcMode2.BeforeObject, "S1", "Y + 10", 50.0, 50.0, true)]
        [TestCase(SpecCalcMode2.AfterObject, "S1", "Y + 10", 50.0, 50.0, false)]
        [TestCase(SpecCalcMode2.AfterObject, "S1", "Y + 10", 50.0, 50.0, true)]
        [TestCase(SpecCalcMode2.BeforeObject, "S2", "Y + 10", 40.0, 50.0, false)]
        [TestCase(SpecCalcMode2.BeforeObject, "S2", "Y + 10", 40.0, 50.0, true)]
        [TestCase(SpecCalcMode2.AfterObject, "S2", "Y + 10", 40.0, 50.0, false)]
        [TestCase(SpecCalcMode2.AfterObject, "S2", "Y + 10", 40.0, 50.0, true)]
        public void BeforeAndAfterObjectRunOnceAroundTheReferenceObject(SpecCalcMode2 mode, string reference,
            string expression, double s2, double h1, bool solver2)
        {
            var c = SpecCase("S1", expression, SpecCalcMode.AfterSourceObject);
            c.Spec.WithCalculationMode(mode, reference);
            SolveClean(c.Fs, solver2);
            Check(c, s2, h1, $"{mode} {reference}, '{expression}'");
        }

        /// <summary>
        /// An energy stream as the reference object: the spec runs once, in either mode. The heater's
        /// energy feed E1 is calculated ahead of the heater, so the heater runs at 50 C both ways.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void BeforeAndAfterObjectRunOnceAroundAnEnergyStream(bool solver2)
        {
            foreach (var mode in new[] { SpecCalcMode2.BeforeObject, SpecCalcMode2.AfterObject })
            {
                var c = SpecCase("S1", "Y + 10", SpecCalcMode.AfterSourceObject);
                c.Spec.WithCalculationMode(mode, "E1");
                SolveClean(c.Fs, solver2);
                TestContext.WriteLine($"{mode} E1: S2 {c.S2C:F3} C, H1 outlet temperature {c.H1C:F3} C");
                Assert.That(c.H1C, Is.EqualTo(50.0).Within(Tol), mode + " E1: the spec ran once");
                Assert.That(c.S2C, Is.EqualTo(50.0).Within(Tol), mode + " E1: ahead of the heater");
            }
        }

        /// <summary>
        /// Around a material stream, BeforeObject writes ahead of the stream's flash and AfterObject
        /// after it. The spec raises S1's own temperature by 10 C: written before the flash the
        /// heater takes the feed from 35 C, written after it the feed's enthalpy is still that of 25 C,
        /// so the heater duty is three times larger.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void AroundAMaterialStreamBeforeObjectPrecedesTheFlashAndAfterObjectFollowsIt(bool solver2)
        {
            double Duty(SpecCalcMode2 mode)
            {
                var c = HeaterCase("SpecStream");
                c.Spec = c.Fs.AddSpec("SPEC-1").WithSource("S1", "PROP_MS_0").WithTarget("S1", "PROP_MS_0")
                    .WithExpression("Y + 10").WithCalculationMode(mode, "S1");
                SolveClean(c.Fs, solver2);
                double q = c.E1.Object.EnergyFlow.GetValueOrDefault();
                TestContext.WriteLine($"{mode} S1: S1 {c.S1.TemperatureK - 273.15:F3} C, duty {q:F3} kW");
                Assert.That(c.S1.TemperatureK - 273.15, Is.EqualTo(35.0).Within(Tol), mode + ": S1 written once");
                return q;
            }

            double before = Duty(SpecCalcMode2.BeforeObject), after = Duty(SpecCalcMode2.AfterObject);
            Assert.That(before / after, Is.EqualTo(1.0 / 3.0).Within(0.01), "duty from 35 C over duty from 25 C");
        }

        // ------------------------------------------------------------------ explicit modes against a contrary option

        /// <summary>
        /// A spec with its own mode runs there whatever the flowsheet option says, and only there.
        /// (Under the AfterFlowsheet option the solver goes through the flowsheet twice, so an
        /// object-bound spec runs once per pass; the cases below avoid that option for them.)
        /// </summary>
        [TestCase(SpecCalcMode2.AfterSourceObject, SpecCalcMode.BeforeTargetObject, 40.0, 50.0, false)]
        [TestCase(SpecCalcMode2.AfterSourceObject, SpecCalcMode.BeforeTargetObject, 40.0, 50.0, true)]
        [TestCase(SpecCalcMode2.AfterSourceObject, SpecCalcMode.BeforeFlowsheet, 40.0, 50.0, false)]
        [TestCase(SpecCalcMode2.AfterSourceObject, SpecCalcMode.BeforeFlowsheet, 40.0, 50.0, true)]
        [TestCase(SpecCalcMode2.BeforeTargetObject, SpecCalcMode.AfterSourceObject, 20.0, 20.0, false)]
        [TestCase(SpecCalcMode2.BeforeTargetObject, SpecCalcMode.AfterSourceObject, 20.0, 20.0, true)]
        [TestCase(SpecCalcMode2.BeforeTargetObject, SpecCalcMode.BeforeFlowsheet, 20.0, 20.0, false)]
        [TestCase(SpecCalcMode2.BeforeTargetObject, SpecCalcMode.BeforeFlowsheet, 20.0, 20.0, true)]
        [TestCase(SpecCalcMode2.BeforeFlowsheet, SpecCalcMode.AfterFlowsheet, 20.0, 20.0, false)]
        [TestCase(SpecCalcMode2.BeforeFlowsheet, SpecCalcMode.AfterFlowsheet, 20.0, 20.0, true)]
        [TestCase(SpecCalcMode2.BeforeFlowsheet, SpecCalcMode.AfterSourceObject, 20.0, 20.0, false)]
        [TestCase(SpecCalcMode2.BeforeFlowsheet, SpecCalcMode.AfterSourceObject, 20.0, 20.0, true)]
        [TestCase(SpecCalcMode2.AfterFlowsheet, SpecCalcMode.BeforeFlowsheet, 50.0, 50.0, false)]
        [TestCase(SpecCalcMode2.AfterFlowsheet, SpecCalcMode.BeforeFlowsheet, 50.0, 50.0, true)]
        [TestCase(SpecCalcMode2.AfterFlowsheet, SpecCalcMode.AfterSourceObject, 50.0, 50.0, false)]
        [TestCase(SpecCalcMode2.AfterFlowsheet, SpecCalcMode.AfterSourceObject, 50.0, 50.0, true)]
        public void AnExplicitModeOverridesAContraryFlowsheetOption(SpecCalcMode2 mode, SpecCalcMode global,
            double s2, double h1, bool solver2)
        {
            var c = SpecCase("S2", "X + 10", global);
            c.Spec.WithCalculationMode(mode);
            SolveClean(c.Fs, solver2);
            Check(c, s2, h1, $"{mode} under {global}");
        }

        // ------------------------------------------------------------------ recycle and adjust

        /// <summary>
        /// A spec on a heater inside a recycle loop: S1 and a recycled half of the product mix,
        /// the heater runs at S1 + 30 C, and the loop converges with the product at that temperature.
        /// </summary>
        [TestCase(SpecCalcMode2.GlobalSetting, null, false)]
        [TestCase(SpecCalcMode2.GlobalSetting, null, true)]
        [TestCase(SpecCalcMode2.BeforeObject, "H1", false)]
        [TestCase(SpecCalcMode2.BeforeObject, "H1", true)]
        [TestCase(SpecCalcMode2.AfterObject, "MIX", false)]
        [TestCase(SpecCalcMode2.AfterObject, "MIX", true)]
        [TestCase(SpecCalcMode2.BeforeTargetObject, null, false)]
        [TestCase(SpecCalcMode2.BeforeFlowsheet, null, true)]
        public void ASpecInsideARecycleLoopConverges(SpecCalcMode2 mode, string reference, bool solver2)
        {
            var fs = FluentFlowsheet.Create("SpecRecycle").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var feed = fs.AddMaterialStream("S1").At(25.0.Celsius(), 2.0.Bar()).WithMassFlow(1.0.KgPerSecond());
            var ret = fs.AddMaterialStream("S7").At(25.0.Celsius(), 2.0.Bar()).WithMassFlow(0.5.KgPerSecond());
            var mixed = fs.AddMaterialStream("S3");
            var hot = fs.AddMaterialStream("S4");
            var prod = fs.AddMaterialStream("S5");
            var tear = fs.AddMaterialStream("S6");
            var e = fs.AddEnergyStream("E1");
            fs.AddMixer("MIX").ConnectFeed(feed, 0).ConnectFeed(ret, 1).ConnectProduct(mixed, 0);
            var h1 = fs.AddHeater("H1").WithOutletTemperature(40.0.Celsius()).ConnectFeed(mixed).ConnectProduct(hot)
                .ConnectEnergyFeed(e, 1);
            fs.AddSplitter("SPL").ConnectFeed(hot, 0).ConnectProduct(prod, 0).ConnectProduct(tear, 1)
                .Configure(s => { s.Ratios.Clear(); s.Ratios.Add(0.5); s.Ratios.Add(0.5); s.Ratios.Add(0.0); });
            fs.AddUnitOperation(ObjectType.OT_Recycle, "REC").ConnectFeed(tear, 0).ConnectProduct(ret, 0);

            fs.AddSpec("SPEC-1").WithSource("S1", "PROP_MS_0").WithTarget("H1", "PROP_HT_2")
                .WithExpression("X + 30").WithCalculationMode(mode, reference);

            SolveClean(fs, solver2);

            double tProd = prod.TemperatureK - 273.15, h1C = h1.Object.OutletTemperature.GetValueOrDefault() - 273.15;
            double wProd = prod.MassFlowKgPerSecond, wRet = ret.MassFlowKgPerSecond;
            TestContext.WriteLine($"{mode} {reference}: S5 {tProd:F3} C, {wProd:F5} kg/s; recycle {wRet:F5} kg/s; H1 {h1C:F3} C");
            Assert.That(h1C, Is.EqualTo(55.0).Within(Tol), "H1 outlet temperature");
            Assert.That(tProd, Is.EqualTo(55.0).Within(0.01), "product temperature");
            Assert.That(wProd, Is.EqualTo(1.0).Within(1e-3), "product flow, loop closed");
            Assert.That(wRet, Is.EqualTo(1.0).Within(1e-3), "recycled flow, loop closed");
        }

        /// <summary>
        /// A spec next to an adjust: the adjust sets H1's duty for S2 at 60 C; the spec sets heater
        /// H2 to S2 + 10 C, so S3 leaves at 70 C.
        /// </summary>
        [TestCase(SpecCalcMode2.GlobalSetting, null, false)]
        [TestCase(SpecCalcMode2.GlobalSetting, null, true)]
        [TestCase(SpecCalcMode2.AfterObject, "S2", false)]
        [TestCase(SpecCalcMode2.AfterObject, "S2", true)]
        [TestCase(SpecCalcMode2.BeforeObject, "H2", false)]
        [TestCase(SpecCalcMode2.BeforeObject, "H2", true)]
        public void ASpecNextToAnAdjust(SpecCalcMode2 mode, string reference, bool solver2)
        {
            var fs = FluentFlowsheet.Create("SpecAdjust").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var s1 = fs.AddMaterialStream("S1").At(25.0.Celsius(), 2.0.Bar()).WithMassFlow(1.0.KgPerSecond());
            var s2 = fs.AddMaterialStream("S2");
            var s3 = fs.AddMaterialStream("S3");
            fs.AddHeater("H1").WithHeatAdded(50.0.Kilowatts()).ConnectFeed(s1).ConnectProduct(s2)
                .ConnectEnergyFeed(fs.AddEnergyStream("E1"), 1);
            fs.AddHeater("H2").WithOutletTemperature(40.0.Celsius()).ConnectFeed(s2).ConnectProduct(s3)
                .ConnectEnergyFeed(fs.AddEnergyStream("E2"), 1);
            fs.AddAdjust("ADJ").Manipulates("H1", "PROP_HT_3").Controls("S2", "PROP_MS_0")
                .WithTargetValue(60.0.Celsius()).WithTolerance(0.001);
            fs.AddSpec("SPEC-1").WithSource("S2", "PROP_MS_0").WithTarget("H2", "PROP_HT_2")
                .WithExpression("X + 10").WithCalculationMode(mode, reference);

            SolveClean(fs, solver2);

            double t2 = s2.TemperatureK - 273.15, t3 = s3.TemperatureK - 273.15;
            TestContext.WriteLine($"{mode} {reference}: S2 {t2:F4} C, S3 {t3:F4} C");
            Assert.That(t2, Is.EqualTo(60.0).Within(0.05), "S2 at the adjust target");
            Assert.That(t3 - t2, Is.EqualTo(10.0).Within(Tol), "S3 = S2 + 10 C");
        }

        // ------------------------------------------------------------------ attachment without a spec

        /// <summary>
        /// An object marked as attached to a spec with an empty or unknown spec id (an API-built
        /// flowsheet can leave it so) solves without a KeyNotFoundException; nothing runs for it.
        /// </summary>
        [TestCase(SpecCalcMode.AfterSourceObject, "", false)]
        [TestCase(SpecCalcMode.AfterSourceObject, "", true)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "", false)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "", true)]
        [TestCase(SpecCalcMode.AfterSourceObject, "NO-SUCH-SPEC", false)]
        [TestCase(SpecCalcMode.BeforeTargetObject, "NO-SUCH-SPEC", true)]
        public void AnEmptyOrUnknownAttachedSpecIdIsSkipped(SpecCalcMode global, string specId, bool solver2)
        {
            var c = HeaterCase("SpecEmptyId");
            c.Fs.Inner.FlowsheetOptions.SpecCalculationMode = global;
            foreach (var o in new DWSIM.Interfaces.ISimulationObject[] { c.S1.Object, c.S2.Object, c.E1.Object, c.H1.Object })
            {
                o.IsSpecAttached = true;
                o.AttachedSpecId = specId;
            }
            c.H1.Object.SpecVarType = SpecVarType.Target;
            c.S2.Object.SpecVarType = SpecVarType.Target;

            SolveClean(c.Fs, solver2);
            Check(c, 40.0, 40.0, $"attached spec id '{specId}' under {global}");
        }
    }
}
