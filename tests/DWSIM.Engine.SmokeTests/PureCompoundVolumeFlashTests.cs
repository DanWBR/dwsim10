//    Volume-based flashes on a pure compound: the closed-vessel building block for a single-component
//    content (CO2, steam, ammonia, refrigerants).
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
    /// A pure compound has no two-phase pressure window: at a given temperature it is liquid above
    /// the vapour pressure, vapour below, and any split exactly at it. A PT flash inside a pressure
    /// search therefore jumps across the volume the vessel asks for instead of crossing it. The VT
    /// flash handles the pure case with the package's own saturation pressure and the lever rule;
    /// these cases pin that down on carbon dioxide, whose liquid is very compressible near 298 K.
    /// </summary>
    [TestFixture]
    public class PureCompoundVolumeFlashTests
    {
        private DWSIM.DynamicRunner.Flowsheet _fs = null!;
        private PengRobinsonPropertyPackage _pp = null!;
        private static readonly double[] Pure = { 1.0 };

        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();

            _fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            _fs.Init();
            _fs.AddCompound("Carbon dioxide");
            _pp = new PengRobinsonPropertyPackage { Flowsheet = _fs };
            _fs.AddPropertyPackage(_pp);
        }

        private (double V, double H, double U, double vf) At(double T, double P)
        {
            var o = _fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s" + Guid.NewGuid().ToString("N"));
            var ms = (MaterialStream)_fs.SimulationObjects[o.Name];
            ms.SetFlowsheet(_fs);
            ms.SetPropertyPackage(_pp);
            ms.SetOverallComposition(Pure);
            ms.SetTemperature(T);
            ms.SetPressure(P);
            ms.SetMassFlow(1.0);
            ms.Calculate();

            _pp.CurrentMaterialStream = ms;
            var r = _pp.FlashBase.CalculateEquilibrium(FlashSpec.P, FlashSpec.T, P, T, _pp, Pure, null, 0.0);
            var vap = r.GetVaporPhaseMoleFraction();
            double v = _pp.FlashBase.MixtureMolarVolumeAtTP(Pure, T, P, _pp);
            double h = Convert.ToDouble(r.CalculatedEnthalpy);
            return (v, h, h - P * v / _pp.AUX_MMM(Pure), vap);
        }

        [Test]
        public void VTOnTheCompressedLiquidRecoversThePressure()
        {
            var s = At(298.0, 140e5);
            TestContext.WriteLine($"liquid CO2 at 298 K, 14 MPa: v {s.V:E4} m3/mol, h {s.H:F2} kJ/kg, u {s.U:F2} kJ/kg");
            var r = _pp.FlashBase.Flash_VT(Pure, s.V, 298.0, 60e5, _pp);
            Assert.That(r.GetVaporPhaseMoleFraction(), Is.EqualTo(0.0).Within(1e-6));
            Assert.That(Convert.ToDouble(r.CalculatedPressure), Is.EqualTo(140e5).Within(0.02 * 140e5));
        }

        [Test]
        public void VTInsideTheDomeReturnsTheSaturationPressureAndTheLeverRule()
        {
            var liq = At(280.0, 60e5);      // liquid above Psat(280 K) = 41.6 bar
            var vap = At(280.0, 30e5);      // vapour below it
            double v = 0.5 * (liq.V + vap.V);
            var r = _pp.FlashBase.Flash_VT(Pure, v, 280.0, 101325.0, _pp);
            double p = Convert.ToDouble(r.CalculatedPressure);
            TestContext.WriteLine($"CO2 at 280 K, v {v:E4}: P {p / 1e5:F2} bar, vapour fraction {r.GetVaporPhaseMoleFraction():F3}");
            Assert.That(p / 1e5, Is.InRange(39.0, 44.0), "bar: the vapour pressure of CO2 at 280 K");
            Assert.That(r.GetVaporPhaseMoleFraction(), Is.InRange(0.3, 0.95), "a partly vaporized content (the vapour at 30 bar is bigger than the saturated one, so the split is vapour-rich)");
            // the volume the result stands for must be the one asked for
            double vl = _pp.AUX_MMM(Pure) / 1000.0 / _pp.AUX_LIQDENS(280.0, Pure, p);
            double vv = _pp.AUX_Z(Pure, 280.0, p, PhaseName.Vapor) * 8.314 * 280.0 / p;
            double vr = r.GetLiquidPhase1MoleFraction() * vl + r.GetVaporPhaseMoleFraction() * vv;
            Assert.That(vr, Is.EqualTo(v).Within(1e-3 * v));
        }

        [Test]
        public void VTOnTheSuperheatedVapourRecoversThePressure()
        {
            var s = At(300.0, 20e5);
            var r = _pp.FlashBase.Flash_VT(Pure, s.V, 300.0, 101325.0, _pp);
            Assert.That(r.GetVaporPhaseMoleFraction(), Is.EqualTo(1.0).Within(1e-6));
            Assert.That(Convert.ToDouble(r.CalculatedPressure), Is.EqualTo(20e5).Within(0.01 * 20e5));
        }

        [Test]
        public void VUOnTheCompressedLiquidRecoversTemperatureAndPressure()
        {
            var s = At(298.0, 140e5);
            // the residual the VU flash searches on, to see its shape around the answer
            foreach (var T in new[] { 285.0, 290.0, 295.0, 297.0, 298.0, 299.0, 301.0 })
            {
                var rt = _pp.FlashBase.Flash_VT(Pure, s.V, T, 140e5, _pp);
                double p = Convert.ToDouble(rt.CalculatedPressure);
                double u = Convert.ToDouble(rt.CalculatedEnthalpy) - p * s.V / _pp.AUX_MMM(Pure);
                TestContext.WriteLine($"T {T:F1} K: P {p / 1e5:F1} bar, vf {rt.GetVaporPhaseMoleFraction():F4}, u {u:F2} kJ/kg (target {s.U:F2})");
            }
            var r = _pp.FlashBase.Flash_VU(Pure, s.V, s.U, 140e5, 298.0, _pp);
            TestContext.WriteLine($"VU: T {Convert.ToDouble(r.CalculatedTemperature):F2} K, P {Convert.ToDouble(r.CalculatedPressure) / 1e5:F1} bar, vf {r.GetVaporPhaseMoleFraction():F4}");
            Assert.That(Convert.ToDouble(r.CalculatedTemperature), Is.EqualTo(298.0).Within(0.3));
            Assert.That(Convert.ToDouble(r.CalculatedPressure), Is.EqualTo(140e5).Within(0.03 * 140e5));
        }

        [Test]
        public void VUAfterASmallLiquidWithdrawalStaysLiquid()
        {
            // a liquid-full vessel loses 0.3 % of its mass: the pressure falls, the content stays liquid
            var s = At(298.0, 140e5);
            var r = _pp.FlashBase.Flash_VU(Pure, s.V * 1.003, s.U, 140e5, 298.0, _pp);
            double p = Convert.ToDouble(r.CalculatedPressure), t = Convert.ToDouble(r.CalculatedTemperature);
            TestContext.WriteLine($"after 0.3 % withdrawal: T {t:F2} K, P {p / 1e5:F1} bar, vf {r.GetVaporPhaseMoleFraction():F4}");
            Assert.That(r.GetVaporPhaseMoleFraction(), Is.EqualTo(0.0).Within(1e-6));
            Assert.That(p, Is.LessThan(140e5).And.GreaterThan(64e5), "bar: below the start, above the vapour pressure");
            Assert.That(t, Is.EqualTo(298.0).Within(1.0));
        }
    }
}
