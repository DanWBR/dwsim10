//    Pressure-enthalpy flash of an LPG vapour at its dew point expanded across a valve, and the
//    Brent scan that hung the nested-loops flash during the Isle of Grain P47 blowdown.
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
using System.Threading;
using System.Threading.Tasks;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces.Enums;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class LpgVapourPHFlashTests
    {
        private DWSIM.DynamicRunner.Flowsheet _fs = null!;
        private PengRobinsonPropertyPackage _pp = null!;

        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
            _fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            _fs.Init();
            _fs.AddCompound("Propane");
            _fs.AddCompound("N-butane");
            _pp = new PengRobinsonPropertyPackage { Flowsheet = _fs };
            _fs.AddPropertyPackage(_pp);
        }

        private MaterialStream Stream(double[] z, double T, double P)
        {
            var o = _fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s" + Guid.NewGuid().ToString("N"));
            var ms = (MaterialStream)_fs.SimulationObjects[o.Name];
            ms.SetFlowsheet(_fs);
            ms.SetPropertyPackage(_pp);
            ms.SetOverallComposition(z);
            ms.SetTemperature(T);
            ms.SetPressure(P);
            ms.SetMassFlow(1.0);
            ms.Calculate();
            return ms;
        }

        // the vessel content at 57 s of the P47 run: LPG 95/5 at 2.46 bar and -18.4 C, two-phase; the
        // vapour leaves through the valve and is expanded to 1 bar at constant enthalpy
        [TestCase(2.46e5, 254.75, 1.0e5)]
        [TestCase(2.46e5, 254.75, 1.5e5)]
        [TestCase(2.0e5, 250.0, 1.0e5)]
        public void TheValveOutletPHFlashOfTheVapourConverges(double P, double T, double P2)
        {
            var vessel = Stream(new[] { 0.95, 0.05 }, T, P);
            var y = new[] { Convert.ToDouble(vessel.Phases[2].Compounds["Propane"].MoleFraction), Convert.ToDouble(vessel.Phases[2].Compounds["N-butane"].MoleFraction) };
            double hv = Convert.ToDouble(vessel.Phases[2].Properties.enthalpy);
            TestContext.WriteLine($"vapour y = {y[0]:F4}/{y[1]:F4}, h = {hv:F2} kJ/kg, vapour fraction {Convert.ToDouble(vessel.Phases[2].Properties.molarfraction):F3}");

            var outlet = Stream(y, T, P);
            outlet.SetPressure(P2);
            outlet.SetMassEnthalpy(hv);
            outlet.SpecType = StreamSpec.Pressure_and_Enthalpy;
            var task = Task.Run(() => outlet.Calculate());
            Assert.That(task.Wait(TimeSpan.FromSeconds(60)), Is.True, "the PH flash did not return within 60 s");
            TestContext.WriteLine($"outlet: T {outlet.GetTemperature():F2} K, vapour fraction {Convert.ToDouble(outlet.Phases[2].Properties.molarfraction):F4}");
            Assert.That(outlet.GetTemperature(), Is.InRange(200.0, 280.0));
        }
        // the exact "Gas to BDV" stream states before the step that hung: a vapour at its dew point,
        // PH spec, swept over the pressures and enthalpies of the next few steps
        [Test]
        public void TheDewPointVapourPHFlashConverges()
        {
            var y = new[] { 0.98645, 0.01355 };
            foreach (var P in new[] { 2.4576e5, 2.450e5, 2.444e5, 2.438e5, 2.430e5, 2.420e5, 2.400e5 })
                foreach (var h in new[] { -77.48, -77.60, -77.67, -77.75, -77.90, -78.10 })
                {
                    var ms = Stream(y, 254.7, P);
                    ms.SetMassEnthalpy(h);
                    ms.SpecType = StreamSpec.Pressure_and_Enthalpy;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var task = Task.Run(() => ms.Calculate());
                    bool ok = task.Wait(TimeSpan.FromSeconds(20));
                    TestContext.WriteLine($"P {P / 1e5:F4} bar, h {h:F2}: {(ok ? $"T {ms.GetTemperature():F3} K, vf {Convert.ToDouble(ms.Phases[2].Properties.molarfraction):F6}" : "HUNG")} in {sw.ElapsedMilliseconds} ms");
                    Assert.That(ok, Is.True, $"PH flash hung at P {P} h {h}");
                }
        }
        // BrentOpt2 scanned its interval with a step of (max - min) / n and looped until a sign change
        // or the end of the interval: with equal bounds the step is zero and the scan never ends. The
        // PH flash hands it such a bracket when its bisection has collapsed onto a dew point.
        [Test]
        public void BrentOpt2ReturnsOnADegenerateInterval()
        {
            var brent = new DWSIM.MathOps.MathEx.BrentOpt.Brent();
            var task = Task.Run(() => brent.BrentOpt2(254.6, 254.6, 100, 1e-6, 100, x => x - 254.0));
            Assert.That(task.Wait(TimeSpan.FromSeconds(10)), Is.True, "the scan must return on a zero-width interval");
            Assert.That(task.Result, Is.EqualTo(254.6).Within(1e-9));
        }
    }
}
