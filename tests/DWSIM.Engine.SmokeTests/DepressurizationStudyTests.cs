//    Vessel depressurization study: the engine behind the Depressurization utility.
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
using System.Linq;
using DWSIM.Automation.DynamicRunner.Depressurization;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.Streams;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class DepressurizationStudyTests
    {
        private DWSIM.DynamicRunner.Flowsheet _host = null!;
        private MaterialStream _source = null!;

        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();

            // the host flowsheet only lends its compounds, property package and a stream's composition
            _host = new DWSIM.DynamicRunner.Flowsheet(null, null);
            _host.Init();
            _host.AddCompound("Methane");
            _host.AddCompound("Ethane");
            _host.AddCompound("N-octane");
            var pp = new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage { Flowsheet = _host };
            _host.AddPropertyPackage(pp);
            var o = _host.AddObject(ObjectType.MaterialStream, 0, 0, "Feed gas");
            _source = (MaterialStream)_host.SimulationObjects[o.Name];
            _source.SetFlowsheet(_host);
            _source.SetPropertyPackage(pp);
            _source.SetOverallComposition(new[] { 0.6, 0.1, 0.3 });
            _source.SetTemperature(300.0);
            _source.SetPressure(40e5);
            _source.SetMassFlow(1.0);
            _source.Calculate();
        }

        private DepressurizationInput Input() => new DepressurizationInput
        {
            SourceStreamName = _source.Name,
            InitialPressure = 40e5,
            InitialTemperature = 300.0,
            InitialLiquidVolumeFraction = 0.3,
            Diameter = 1.5,
            Length = 6.0,
            OrificeDiameter = 0.025,
            DischargeCoefficient = 0.62,
            BackPressure = 2e5,
            TimeStep = 2.0,
            Duration = 600.0
        };

        [Test]
        public void AnAdiabaticBlowdownCoolsAndDepressurizesTheVessel()
        {
            var input = Input();
            input.StopAtPressure = 7e5;
            var r = DepressurizationStudy.Run(_host, input);

            TestContext.WriteLine($"{r.Points.Count} points in {r.Elapsed.TotalSeconds:F1} s; V {r.VesselVolume:F2} m3, wetted {r.WettedAreaAtStart:F2} m2, m0 {r.InitialMass:F0} kg");
            TestContext.WriteLine($"final P {r.FinalPressure / 1e5:F2} bar at {r.Points.Last().Time:F0} s; T min {r.MinimumFluidTemperature:F1} K, wall min wet {r.MinimumWettedWallTemperature:F1} dry {r.MinimumDryWallTemperature:F1}; peak {r.PeakMassFlow:F2} kg/s, released {r.TotalMassReleased:F0} kg; t50 {r.TimeToHalfPressure}; tstop {r.TimeToStopPressure}");
            foreach (var w in r.Warnings) TestContext.WriteLine("warning: " + w);

            Assert.That(r.Warnings.Where(w => w.StartsWith("The integration stopped")), Is.Empty);
            Assert.That(r.VesselVolume, Is.EqualTo(Math.PI / 4 * 1.5 * 1.5 * 6 + 2 * Math.PI * Math.Pow(1.5, 3) / 24).Within(1e-6), "cylinder plus two 2:1 heads");
            Assert.That(r.Points.First().Pressure, Is.EqualTo(40e5).Within(0.02 * 40e5));
            Assert.That(r.TimeToStopPressure, Is.Not.Null, "7 bar reached");
            Assert.That(r.FinalPressure, Is.LessThanOrEqualTo(7e5 + 1e4));
            Assert.That(r.TimeToHalfPressure, Is.LessThan(r.TimeToStopPressure));

            // pressure falls monotonically and the content cools on expansion
            for (int i = 1; i < r.Points.Count; i++)
                Assert.That(r.Points[i].Pressure, Is.LessThanOrEqualTo(r.Points[i - 1].Pressure + 1e3), $"t = {r.Points[i].Time}");
            Assert.That(r.MinimumFluidTemperature, Is.LessThan(300.0 - 5.0), "K");
            Assert.That(r.MinimumDryWallTemperature, Is.GreaterThan(r.MinimumFluidTemperature), "the metal lags the gas");
            Assert.That(r.PeakMassFlow, Is.GreaterThan(0.0));
            Assert.That(r.TotalMassReleased, Is.GreaterThan(0.0).And.LessThan(r.InitialMass));
            Assert.That(r.Points.Last().CumulativeMass, Is.EqualTo(r.TotalMassReleased).Within(1e-9));
        }

        [Test]
        public void TheFireCaseRaisesThePressureBeforeTheValveCanCatchUp()
        {
            var input = Input();
            input.Mode = DepressurizationMode.Fire;
            input.OrificeDiameter = 0.004;      // a small orifice: the fire wins at first
            input.FireDryWallHeatFlux = 20000.0;
            input.Duration = 120.0;
            var r = DepressurizationStudy.Run(_host, input);

            TestContext.WriteLine($"fire: P {r.Points.First().Pressure / 1e5:F2} -> {r.FinalPressure / 1e5:F2} bar, T {r.Points.First().Temperature:F1} -> {r.FinalTemperature:F1} K, dry wall max {r.MaximumDryWallTemperature:F1} K, fire heat {r.Points.Last().FireHeat:F0} kW");
            Assert.That(r.Points.Last().FireHeat, Is.GreaterThan(100.0), "kW of pool-fire heat on the wetted wall");
            Assert.That(r.FinalTemperature, Is.GreaterThan(300.0));
            Assert.That(r.MaximumDryWallTemperature, Is.GreaterThan(r.FinalTemperature + 20.0), "the unwetted metal heats up faster than the content");
        }

        [Test]
        public void TheIsothermalModeKeepsTheTemperature()
        {
            var input = Input();
            input.Mode = DepressurizationMode.Isothermal;
            input.Duration = 120.0;
            var r = DepressurizationStudy.Run(_host, input);
            Assert.That(r.FinalPressure, Is.LessThan(40e5));
            Assert.That(r.MinimumFluidTemperature, Is.EqualTo(300.0).Within(1.5));
            Assert.That(r.Warnings.Any(w => w.Contains("Isothermal")), Is.True);
        }

        [Test]
        public void AValveThatOpensSlowlyStartsClosed()
        {
            var input = Input();
            input.ValveOpeningTime = 30.0;
            input.Duration = 40.0;
            var r = DepressurizationStudy.Run(_host, input);
            var at10 = r.Points.First(p => p.Time >= 10.0);
            var at40 = r.Points.Last();
            Assert.That(at10.ValveOpening, Is.EqualTo(100.0 * 10.0 / 30.0).Within(1e-6));
            Assert.That(at40.ValveOpening, Is.EqualTo(100.0).Within(1e-9));
            Assert.That(at10.MassFlow, Is.LessThan(at40.MassFlow), "the flow grows as the valve opens");
        }

        [Test]
        public void AGasFilledVesselIsAcceptedWithAWarning()
        {
            var input = Input();
            input.InitialTemperature = 650.0;   // above the critical temperature of n-octane: all vapour
            input.Duration = 60.0;
            var r = DepressurizationStudy.Run(_host, input);
            Assert.That(r.Warnings.Any(w => w.Contains("all vapour")), Is.True);
            Assert.That(r.WettedAreaAtStart, Is.EqualTo(0.0));
            Assert.That(r.FinalPressure, Is.LessThan(40e5));
        }
    }
}
