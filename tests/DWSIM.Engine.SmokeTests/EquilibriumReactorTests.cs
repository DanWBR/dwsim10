//    Equilibrium reactor regressions.
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DWSIM.GlobalSettings;
using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class EquilibriumReactorTests
    {
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
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", filename));
            return flowsheet;
        }

        // SO2 + 1/2 O2 <-> SO3 in an isothermal equilibrium reactor fed by a heater, NRTL, Keq from the
        // Gibbs energies (the file attached to issue #79). The file carries InternalLoopTolerance = 1 and
        // ExternalLoopTolerance = 1: "0,001" and "0,01" as shown on a German locale, read back by an earlier
        // 10.2 build with the thousands separator allowed. With a tolerance of 1 on a sum of squared
        // residuals the Newton solver accepted its starting point, so the conversion only moved when the
        // starting point drifted far enough, and a temperature sweep came out as a staircase
        // (96.0 % from 411 to 444 C, then 92.0 % to 500 C, then 84.2 % ...). The reactor now resets such
        // tolerances on load, and the sweep is smooth and monotonic.
        // https://github.com/DanWBR/dwsim10/issues/79
        [Test]
        public void TheSulfurDioxideConversionFallsSmoothlyWithTemperature()
        {
            var flowsheet = Load("SO2OxidationEquilibrium.dwxmz");
            var heater = flowsheet.SimulationObjects.Values.OfType<Heater>().Single(h => h.GraphicObject.Tag == "HT-1");
            var reactor = flowsheet.SimulationObjects.Values.OfType<Reactor_Equilibrium>().Single();

            Assert.That(reactor.InternalLoopTolerance, Is.EqualTo(0.001), "the file's tolerance of 1 is reset on load");
            Assert.That(reactor.ExternalLoopTolerance, Is.EqualTo(0.01));

            var conversions = new List<double>();
            for (double t = 400.0; t <= 700.01; t += 25.0)
            {
                heater.OutletTemperature = t + 273.15;
                var errors = flowsheet.SolveFlowsheet2();
                Assert.That(errors, Is.Empty, "at " + t + " C the solver reported: " + string.Join("; ", errors.Select(e => e.Message)));
                conversions.Add(reactor.ComponentConversions["Sulfur dioxide"] * 100.0);
            }
            TestContext.WriteLine(string.Join(", ", conversions.Select(c => c.ToString("F2"))));

            // exothermic: conversion falls with temperature, and every 25 C step moves it
            for (int i = 1; i < conversions.Count; i++)
                Assert.That(conversions[i], Is.LessThan(conversions[i - 1] - 0.2), "step " + i + ": " + conversions[i - 1].ToString("F2") + " -> " + conversions[i].ToString("F2") + " %");

            // the values the file gives with its tolerances at 0.001/0.01, which also match the V9 curve
            Assert.That(conversions.First(), Is.EqualTo(98.16).Within(0.3), "400 C");
            Assert.That(conversions[8], Is.EqualTo(76.33).Within(0.5), "600 C");
            Assert.That(conversions.Last(), Is.EqualTo(52.14).Within(0.5), "700 C");
        }

        [Test]
        public void ALooseToleranceIsResetWhenTheReactorCalculates()
        {
            var flowsheet = Load("SO2OxidationEquilibrium.dwxmz");
            var reactor = flowsheet.SimulationObjects.Values.OfType<Reactor_Equilibrium>().Single();
            reactor.InternalLoopTolerance = 1.0;
            reactor.ExternalLoopTolerance = 0.5;
            reactor.Calculate();
            Assert.That(reactor.InternalLoopTolerance, Is.EqualTo(0.001));
            Assert.That(reactor.ExternalLoopTolerance, Is.EqualTo(0.01));

            // a deliberately tight setting is left alone
            reactor.InternalLoopTolerance = 1e-6;
            reactor.ExternalLoopTolerance = 0.05;
            reactor.Calculate();
            Assert.That(reactor.InternalLoopTolerance, Is.EqualTo(1e-6));
            Assert.That(reactor.ExternalLoopTolerance, Is.EqualTo(0.05));
        }
    }
}
