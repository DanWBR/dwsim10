//    Volume-based flashes (VT, VP, VH, VS, VU): the building blocks of a closed-vessel model.
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
using DWSIM.GlobalSettings;
using DWSIM.Interfaces.Enums;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// A closed vessel knows its volume and either T, H, S or U; the flash has to find the pressure
    /// (and temperature) from that. These used to be Simplex searches boxed to 0.7 to 1.4 times the
    /// pressure estimate, so a vessel that blew down by more than that could not be flashed. They are
    /// now bracketed root searches: every case here starts from a deliberately poor estimate.
    /// </summary>
    [TestFixture]
    public class VolumeFlashTests
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
            _fs.AddCompound("Methane");
            _fs.AddCompound("N-octane");
            _pp = new PengRobinsonPropertyPackage { Flowsheet = _fs };
            _fs.AddPropertyPackage(_pp);
        }

        private sealed class State
        {
            public double[] Z = null!;
            public double T, P, V, H, S, U;   // V in m3/mol; H, S, U per kg
        }

        /// <summary>A PT state of the mixture, with the volume defined the way the flashes define it.</summary>
        private State At(double[] z, double T, double P)
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

            _pp.CurrentMaterialStream = ms;
            var r = _pp.FlashBase.CalculateEquilibrium(FlashSpec.P, FlashSpec.T, P, T, _pp, z, null, 0.0);
            double v = 0.0;
            var l1 = r.GetLiquidPhase1MoleFraction();
            var vap = r.GetVaporPhaseMoleFraction();
            if (l1 > 0) v += l1 / (_pp.AUX_LIQDENS(T, r.GetLiquidPhase1MoleFractions(), P) / _pp.AUX_MMM(r.GetLiquidPhase1MoleFractions()) * 1000.0);
            if (vap > 0) v += vap * _pp.AUX_Z(r.GetVaporPhaseMoleFractions(), T, P, PhaseName.Vapor) * 8.314 * T / P;

            var mw = _pp.AUX_MMM(z);
            return new State
            {
                Z = z, T = T, P = P, V = v,
                H = Convert.ToDouble(r.CalculatedEnthalpy),
                S = Convert.ToDouble(r.CalculatedEntropy),
                U = Convert.ToDouble(r.CalculatedEnthalpy) - P * v / mw
            };
        }

        private static readonly double[] TwoPhase = { 0.5, 0.5 };
        private static readonly double[] Gas = { 1.0, 0.0 };

        [TestCase(320.0, 20e5, 101325.0)]      // two-phase, estimate 20 times too low
        [TestCase(320.0, 20e5, 200e5)]         // estimate 10 times too high
        [TestCase(250.0, 2e5, 50e5)]
        public void VTFindsThePressureFromAPoorEstimate(double T, double P, double pRef)
        {
            var s = At(TwoPhase, T, P);
            var r = _pp.FlashBase.Flash_VT(s.Z, s.V, s.T, pRef, _pp);
            Assert.That(Convert.ToDouble(r.CalculatedPressure), Is.EqualTo(P).Within(0.002 * P));
        }

        [Test]
        public void VTOnASinglePhaseGas()
        {
            var s = At(Gas, 300.0, 60e5);
            var r = _pp.FlashBase.Flash_VT(s.Z, s.V, s.T, 5e5, _pp);
            Assert.That(Convert.ToDouble(r.CalculatedPressure), Is.EqualTo(60e5).Within(0.002 * 60e5));
        }

        [TestCase(320.0, 20e5, 200.0)]
        [TestCase(320.0, 20e5, 500.0)]
        public void VPFindsTheTemperatureFromAPoorEstimate(double T, double P, double tRef)
        {
            var s = At(TwoPhase, T, P);
            var r = _pp.FlashBase.Flash_VP(s.Z, s.V, s.P, tRef, _pp);
            Assert.That(Convert.ToDouble(r.CalculatedTemperature), Is.EqualTo(T).Within(0.2));
        }

        [Test]
        public void VHRecoversTemperatureAndPressure()
        {
            var s = At(TwoPhase, 320.0, 20e5);
            var r = _pp.FlashBase.Flash_VH(s.Z, s.V, s.H, 101325.0, 250.0, _pp);
            Assert.That(Convert.ToDouble(r.CalculatedTemperature), Is.EqualTo(320.0).Within(0.3));
            Assert.That(Convert.ToDouble(r.CalculatedPressure), Is.EqualTo(20e5).Within(0.005 * 20e5));
        }

        // The VS flash summed enthalpies where it meant entropies, so it never solved what it was asked.
        [Test]
        public void VSRecoversTemperatureAndPressure()
        {
            var s = At(TwoPhase, 320.0, 20e5);
            var r = _pp.FlashBase.Flash_VS(s.Z, s.V, s.S, 101325.0, 250.0, _pp);
            Assert.That(Convert.ToDouble(r.CalculatedTemperature), Is.EqualTo(320.0).Within(0.3));
            Assert.That(Convert.ToDouble(r.CalculatedPressure), Is.EqualTo(20e5).Within(0.005 * 20e5));
        }

        [Test]
        public void VURecoversTemperatureAndPressure()
        {
            var s = At(TwoPhase, 320.0, 20e5);
            var r = _pp.FlashBase.Flash_VU(s.Z, s.V, s.U, 101325.0, 250.0, _pp);
            Assert.That(Convert.ToDouble(r.CalculatedTemperature), Is.EqualTo(320.0).Within(0.3));
            Assert.That(Convert.ToDouble(r.CalculatedPressure), Is.EqualTo(20e5).Within(0.005 * 20e5));
        }

        // An adiabatic blowdown step: the same gas, same U, five times the volume. Pressure has to fall
        // well below a fifth of the start (the gas cools), and the flash must get there from the old state.
        [Test]
        public void VUFollowsAnExpansionFarOutsideTheOldBounds()
        {
            var s = At(Gas, 300.0, 60e5);
            var r = _pp.FlashBase.Flash_VU(s.Z, s.V * 5.0, s.U, s.P, s.T, _pp);
            var p = Convert.ToDouble(r.CalculatedPressure);
            var t = Convert.ToDouble(r.CalculatedTemperature);
            Assert.That(p, Is.LessThan(60e5 / 5.0));
            Assert.That(t, Is.LessThan(300.0));
            // and the state found really has that volume and energy
            var check = At(Gas, t, p);
            Assert.That(check.V, Is.EqualTo(s.V * 5.0).Within(0.005 * s.V * 5.0));
            Assert.That(check.U, Is.EqualTo(s.U).Within(0.5));
        }

        // The VolumeEntropy flash type was dispatched under a duplicated PressureEntropy case and never ran.
        [Test]
        public void VolumeEntropyIsReachableThroughThePropertyPackage()
        {
            var s = At(TwoPhase, 320.0, 20e5);
            var r = _pp.CalculateEquilibrium(FlashCalculationType.VolumeEntropy, s.V, s.S, s.Z, null, 250.0);
            Assert.That(Convert.ToDouble(r.CalculatedTemperature), Is.EqualTo(320.0).Within(0.3));
        }
    }
}
