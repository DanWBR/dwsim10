//    Gas-liquid separator in dynamic mode: gas blow-by through the liquid outlet.
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
using DWSIM.Automation.DynamicRunner;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;
using DynamicsSpecType = DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// A separator whose level control valve is stuck open. Once the liquid is gone, the gas in
    /// the vessel has to leave through the liquid outlet at the vessel pressure: that is the
    /// blow-by relief case, and the liquid outlet used to dry up instead of passing gas.
    /// </summary>
    [TestFixture]
    public class DynamicsBlowByTests
    {
        [OneTimeSetUp]
        public void RegisterPropertyPackages()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private sealed class Case
        {
            public DWSIM.DynamicRunner.Flowsheet Flowsheet = null!;
            public Vessel Vessel = null!;
            public Valve Lcv = null!;
            public MaterialStream Feed = null!, GasOut = null!, LiquidOut = null!, Sink = null!;
        }

        private sealed class Snapshot
        {
            public double Time, Level, GasFraction, Pressure, LcvFlow, LcvInletVapourFraction, LcvInletPressure;
            public override string ToString() =>
                $"t={Time,5:F0} s  level={Level:F4} m  gas fraction={GasFraction:F3}  P={Pressure / 1e5:F2} bar  " +
                $"LCV={LcvFlow:F3} kg/s  LCV inlet vapour={LcvInletVapourFraction:F3}  LCV inlet P={LcvInletPressure / 1e5:F2} bar";
        }

        // Methane + n-octane at 20 bar and 320 K into a 2 m3, 2 m tall separator. The liquid leaves
        // through a wide-open Kv = 10 valve to a 5 bar sink, so the vessel drains in about 20 s at
        // 1 kg/s of feed. The gas outlet keeps the flow of the steady state.
        private static Case Build(double segmentSeconds)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Methane");
            fs.AddCompound("N-octane");

            var pp = new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage { Flowsheet = fs };
            fs.AddPropertyPackage(pp);

            MaterialStream Stream(string tag)
            {
                var o = fs.AddObject(ObjectType.MaterialStream, 0, 0, tag);
                var ms = (MaterialStream)fs.SimulationObjects[o.Name];
                ms.SetFlowsheet(fs);
                ms.SetPropertyPackage(pp);
                return ms;
            }

            var c = new Case { Flowsheet = fs };
            c.Feed = Stream("Feed");
            c.GasOut = Stream("Gas");
            c.LiquidOut = Stream("Liquid");
            c.Sink = Stream("Sink");

            c.Vessel = (Vessel)fs.SimulationObjects[fs.AddObject(ObjectType.Vessel, 0, 0, "V-1").Name];
            c.Vessel.SetFlowsheet(fs);
            c.Vessel.PropertyPackage = pp;
            c.Lcv = (Valve)fs.SimulationObjects[fs.AddObject(ObjectType.Valve, 0, 0, "LCV").Name];
            c.Lcv.SetFlowsheet(fs);
            c.Lcv.PropertyPackage = pp;

            fs.ConnectObjects(c.Feed.GraphicObject, c.Vessel.GraphicObject, 0, 0);
            fs.ConnectObjects(c.Vessel.GraphicObject, c.GasOut.GraphicObject, 0, 0);
            fs.ConnectObjects(c.Vessel.GraphicObject, c.LiquidOut.GraphicObject, 1, 0);
            fs.ConnectObjects(c.LiquidOut.GraphicObject, c.Lcv.GraphicObject, 0, 0);
            fs.ConnectObjects(c.Lcv.GraphicObject, c.Sink.GraphicObject, 0, 0);

            c.Feed.SetTemperature(320.0);
            c.Feed.SetPressure(20e5);
            c.Feed.SetMassFlow(1.0);
            c.Feed.SetOverallComposition(new[] { 0.5, 0.5 });
            c.Feed.DynamicsSpec = DynamicsSpecType.Flow;
            c.GasOut.DynamicsSpec = DynamicsSpecType.Pressure;
            c.LiquidOut.DynamicsSpec = DynamicsSpecType.Pressure;
            c.Sink.DynamicsSpec = DynamicsSpecType.Pressure;

            c.Lcv.CalcMode = Valve.CalculationMode.Kv_General;
            c.Lcv.Kv = 10.0;
            c.Lcv.OpeningPct = 100.0;

            c.Vessel.CreateDynamicProperties();
            c.Vessel.SetDynamicProperty("Volume", 2.0);
            c.Vessel.SetDynamicProperty("Height", 2.0);

            var errors = fs.SolveFlowsheet2();
            Assert.That(errors, Is.Empty, "steady state: " + string.Join("; ", errors.Select(e => e.Message)));
            Assert.That(c.LiquidOut.GetMassFlow(), Is.GreaterThan(0.3), "the feed must be two-phase for the case to mean anything");

            c.Sink.SetPressure(5e5);

            var integrator = new DWSIM.DynamicsManager.Integrator
            {
                ID = "blowby", Description = "blowby",
                IntegrationStep = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromSeconds(segmentSeconds),
                ShouldCalculateEquilibrium = true,
                ShouldCalculatePressureFlow = true,
                ShouldCalculateControl = true
            };
            var schedule = new DWSIM.DynamicsManager.Schedule
            {
                ID = "blowby", Description = "blowby", CurrentIntegrator = "blowby", UseCurrentStateAsInitial = true
            };
            fs.DynamicsManager.IntegratorList["blowby"] = integrator;
            fs.DynamicsManager.ScheduleList["blowby"] = schedule;
            fs.DynamicsManager.CurrentSchedule = "blowby";
            return c;
        }

        private static double Dyn(Vessel v, string name) => Convert.ToDouble(v.GetDynamicProperty(name));

        /// <summary>Runs one integrator segment from the current state.</summary>
        private static IntegratorRunResult Run(Case c)
        {
            var result = new IntegratorRunner(c.Flowsheet).Run(new IntegratorRunOptions { Schedule = "blowby", RestoreInitialState = false });
            Assert.That(result.Exceptions, Is.Empty, string.Join(Environment.NewLine, result.Exceptions.Select(e => e.ToString())));
            return result;
        }

        private static Snapshot Snap(Case c, double time) => new()
        {
            Time = time,
            Level = Dyn(c.Vessel, "Liquid Level"),
            GasFraction = Dyn(c.Vessel, "Liquid Outlet Gas Fraction"),
            Pressure = Dyn(c.Vessel, "Operating Pressure"),
            LcvFlow = c.LiquidOut.GetMassFlow(),
            LcvInletVapourFraction = c.LiquidOut.Phases[2].Properties.molarfraction.GetValueOrDefault(),
            LcvInletPressure = c.LiquidOut.GetPressure()
        };

        [Test]
        public void TheLiquidOutletPassesGasOnceTheVesselHasDrained()
        {
            const double segment = 5.0;
            var c = Build(segment);

            var history = new List<Snapshot>();
            for (int i = 1; i <= 24; i++)
            {
                Run(c);
                history.Add(Snap(c, i * segment));
                TestContext.WriteLine(history.Last());
            }

            // phase 1: liquid through the LCV, the level falling
            var draining = history.First();
            Assert.That(draining.GasFraction, Is.EqualTo(0.0), "still draining liquid after 5 s");
            Assert.That(draining.LcvInletVapourFraction, Is.LessThan(0.01));
            Assert.That(draining.LcvFlow, Is.GreaterThan(2.0), "kg/s of liquid through a wide-open Kv 10 valve");

            // phase 2: the liquid is gone, the outlet switched to gas and the LCV passes gas at the
            // vessel pressure: the blow-by
            var blowBy = history.Where(s => s.GasFraction >= 1.0 - 1e-9).ToList();
            Assert.That(blowBy, Is.Not.Empty, "the liquid outlet never switched to gas");
            var first = blowBy.First();
            Assert.That(first.Level, Is.LessThan(0.02), "m: the level is at the bottom when the blow-by starts");
            Assert.That(first.LcvFlow, Is.GreaterThan(0.05), "kg/s of gas through the LCV");
            Assert.That(first.LcvInletVapourFraction, Is.GreaterThan(0.99), "the LCV inlet is gas");
            Assert.That(first.LcvInletPressure, Is.EqualTo(first.Pressure).Within(1.0), "no static head once the level is below the nozzle");
            Assert.That(first.Pressure, Is.GreaterThan(6e5), "the vessel is still well above the sink when the gas starts to pass");

            // the vessel blows down towards the sink through the level valve
            Assert.That(history.Last().Pressure, Is.LessThan(first.Pressure));
            Assert.That(history.Last().Pressure, Is.LessThan(8e5), "Pa: two minutes in, the vessel is most of the way down to the 5 bar sink");
            for (int i = 1; i < history.Count; i++)
                Assert.That(history[i].Pressure, Is.LessThanOrEqualTo(history[i - 1].Pressure + 0.1e5), $"the pressure never rises (t = {history[i].Time} s)");
        }

        [Test]
        public void ALiquidOutletNozzleAboveTheLevelPassesGasFromTheFirstStep()
        {
            var c = Build(1.0);
            c.Vessel.SetDynamicProperty("Liquid Outlet Nozzle Elevation", 1.9);

            Run(c);

            Assert.That(Dyn(c.Vessel, "Liquid Level"), Is.GreaterThan(0.1), "the vessel still holds liquid");
            Assert.That(Dyn(c.Vessel, "Liquid Outlet Gas Fraction"), Is.EqualTo(1.0).Within(1e-9));

            // the liquid outlet carries the vessel's gas composition
            var content = c.Vessel.AccumulationStream;
            foreach (var comp in c.LiquidOut.Phases[0].Compounds.Values)
            {
                var y = content.Phases[2].Compounds[comp.Name].MoleFraction.GetValueOrDefault();
                Assert.That(comp.MoleFraction.GetValueOrDefault(), Is.EqualTo(y).Within(1e-6), comp.Name);
            }
        }

        [Test]
        public void TheOutletBlendsAcrossTheTransitionBand()
        {
            var c = Build(1.0);
            Run(c);

            var level = Dyn(c.Vessel, "Liquid Level");
            Assert.That(level, Is.GreaterThan(0.2));

            // nozzle at the bottom with the level well above it: liquid
            Assert.That(c.Vessel.LiquidOutletGasFraction(level, 0.0, 0.01), Is.EqualTo(0.0));
            // level at the nozzle: gas
            Assert.That(c.Vessel.LiquidOutletGasFraction(level, level, 0.01), Is.EqualTo(1.0));
            // level halfway up the band: half and half
            Assert.That(c.Vessel.LiquidOutletGasFraction(level, level - 0.05, 0.1), Is.EqualTo(0.5).Within(1e-9));
            // no band: a switch at the nozzle
            Assert.That(c.Vessel.LiquidOutletGasFraction(level, level + 1e-6, 0.0), Is.EqualTo(1.0));
            Assert.That(c.Vessel.LiquidOutletGasFraction(level, level - 1e-6, 0.0), Is.EqualTo(0.0));
        }

        // The Kv-mode valve's two-phase branch in dynamic mode returned kg/h where the liquid and
        // gas branches return kg/s: a blend through the valve drained a vessel 3600 times too fast.
        [Test]
        public void TheTwoPhaseValveFlowIsInKilogramsPerSecond()
        {
            var c = Build(1.0);
            // a blend on the liquid outlet: nozzle just below the level so the band is active
            Run(c);
            var level = Dyn(c.Vessel, "Liquid Level");
            c.Vessel.SetDynamicProperty("Liquid Outlet Nozzle Elevation", level - 0.05);
            c.Vessel.SetDynamicProperty("Liquid Outlet Transition Height", 0.1);
            Run(c);

            var g = Dyn(c.Vessel, "Liquid Outlet Gas Fraction");
            Assert.That(g, Is.GreaterThan(0.05).And.LessThan(0.95), "the outlet is a blend");
            // a liquid-only Kv 10 valve passes about 9 kg/s here and gas alone under 1 kg/s; the
            // blend sits between them, not in the thousands
            Assert.That(c.LiquidOut.GetMassFlow(), Is.GreaterThan(0.3).And.LessThan(20.0), "kg/s");
        }
    }
}
