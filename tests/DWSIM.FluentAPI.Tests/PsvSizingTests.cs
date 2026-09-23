using System;
using DWSIM.Automation.FluentAPI;
using NUnit.Framework;
using PSV = DWSIM.Thermodynamics.Utilities.PSV;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Pressure relief valve sizing against the worked examples of API RP 520 Part I (7th edition,
    /// January 2000): gas in critical flow (3.6.2.2), gas in subcritical flow (3.6.3.2), liquid with
    /// the viscosity correction (3.8.2) and two-phase flow by the omega method (D.2.1.2).
    /// </summary>
    /// <remarks>
    /// The SI methods take absolute pressures in Pa, with P1 = set pressure (gauge) + overpressure +
    /// atmospheric pressure. The older PSV_* methods take kgf/cm2 gauge and must give the same area.
    /// </remarks>
    [TestFixture]
    public class PsvSizingTests
    {
        private const double Atm = 101325.0;
        private const double PsiToPa = 6894.757293;
        private const double Ft3PerLbToM3PerKg = 1.0 / 16.018463;
        private const double InchSquaredToMm2 = 645.16;

        // 3.6.2.2 and 3.6.3.2: butane/pentane vapour, 53,500 lb/h [24,260 kg/h], M = 65, 348 K,
        // set at 75 psig [517 kPag], 10 % accumulation, Z = 0.84, k = 1.09, Kd = 0.975.
        private const double GasW = 24260.0 / 3600.0;
        private const double GasSet = 517.0e3 + Atm;
        private const double GasT = 348.0, GasZ = 0.84, GasM = 65.0, GasK = 1.09, GasKd = 0.975;

        [Test]
        public void TheRelievingPressureAddsTheOverpressureToTheGaugeSetPressure()
        {
            // API: P1 = 75 x 1.1 + 14.7 = 97.2 psia [670 kPa]
            var p1 = PSV.Sizing.RelievingPressure(GasSet, 10.0);
            Assert.That(p1, Is.EqualTo(670.0e3).Within(0.1).Percent);
            Assert.That(p1 / PsiToPa, Is.EqualTo(97.2).Within(0.2).Percent);
        }

        [Test]
        public void GasInCriticalFlowMatchesTheApi520Example()
        {
            var p1 = PSV.Sizing.RelievingPressure(GasSet, 10.0);
            var a = PSV.Sizing.GasArea(p1, Atm, GasT, GasW, GasZ, GasM, GasK, GasKd, 1.0, 1.0);

            Console.WriteLine($"gas, critical flow: A = {a:F3} in2 = {a * InchSquaredToMm2:F0} mm2 (API 520: 4.93 in2, 3179 mm2)");

            Assert.That(a, Is.EqualTo(4.93).Within(2).Percent);
            Assert.That(PSV.Sizing.StandardOrifice(a).Item1, Is.EqualTo("P"));
        }

        [Test]
        public void GasInSubcriticalFlowMatchesTheApi520Example()
        {
            // total back pressure 55 + 7.5 psig = 77.2 psia [532 kPaa], above the critical 57.3 psia
            var p1 = PSV.Sizing.RelievingPressure(GasSet, 10.0);
            var p2 = 532.0e3;
            Assert.That(p2, Is.GreaterThan(PSV.Sizing.CriticalFlowPressure(p1, GasK)));

            var a = PSV.Sizing.GasArea(p1, p2, GasT, GasW, GasZ, GasM, GasK, GasKd, 1.0, 1.0);

            Console.WriteLine($"gas, subcritical flow: A = {a:F3} in2 = {a * InchSquaredToMm2:F0} mm2 (API 520: 5.6 in2, 3610 mm2, F2 read off the chart)");

            Assert.That(a * InchSquaredToMm2, Is.EqualTo(3610.0).Within(2).Percent);
        }

        [Test]
        public void TheGaugeInterfaceGivesTheSameGasAreaAsTheAbsoluteOne()
        {
            const double paPerKgfCm2 = Atm / 1.033;

            var p1 = PSV.Sizing.RelievingPressure(GasSet, 10.0);
            var si = PSV.Sizing.GasArea(p1, Atm, GasT, GasW, GasZ, GasM, GasK, GasKd, 1.0, 1.0);

            var gauge = Convert.ToDouble(new PSV.Sizing().PSV_G_D((p1 - Atm) / paPerKgfCm2, 0.0, GasT, GasW * 3600.0,
                GasZ, GasM, GasK, GasKd, 1.0, 1.0));

            Assert.That(gauge, Is.EqualTo(si).Within(1e-9).Percent);

            // an absolute pressure handed to the gauge interface gains one more atmosphere
            var doubleCounted = Convert.ToDouble(new PSV.Sizing().PSV_G_D(p1 / paPerKgfCm2, Atm / paPerKgfCm2, GasT, GasW * 3600.0,
                GasZ, GasM, GasK, GasKd, 1.0, 1.0));
            Assert.That(doubleCounted, Is.LessThan(0.9 * si));
        }

        [Test]
        public void LiquidWithTheViscosityCorrectionMatchesTheApi520Example()
        {
            // 3.8.2: 1800 gpm, G = 0.90, 2000 SSU, set at 250 psig [1724 kPag], P1 = 275 psig [1896 kPag],
            // back pressure 50 psig [345 kPag], Kd = 0.65, Kw = 0.97. The example takes the viscosity in
            // SSU (R = 12,700 Q / (U sqrt A)); the cP value with the same Reynolds number is 2800 G U / 12,700.
            var q = 1800.0 * 3.785411784e-3 / 60.0;
            var rho = 900.0;
            var muCp = 2800.0 * 0.9 * 2000.0 / 12700.0;
            var p1 = 1896.0e3 + Atm;
            var p2 = 345.0e3 + Atm;

            var ar = PSV.Sizing.LiquidArea(q, p1, p2, rho, 0.0, 0.65, 0.97, 1.0);
            var a = PSV.Sizing.LiquidArea(q, p1, p2, rho, muCp / 1000.0, 0.65, 0.97, 1.0);

            Console.WriteLine($"liquid: AR = {ar:F3} in2 (API 520: 4.752), A = {a:F3} in2 (API 520: 4.930 with Kv = 0.964)");

            Assert.That(ar, Is.EqualTo(4.752).Within(1).Percent);
            Assert.That(a, Is.EqualTo(4.930).Within(1).Percent);
        }

        [Test]
        public void TheLiquidViscosityCorrectionEndsBeyondTheLargestOrifice()
        {
            // about 50 in2, past the T orifice (26 in2); the older loop never ended there
            var a = PSV.Sizing.LiquidArea(1.0, 11.0e5, 1.0e5, 900.0, 0.3, 0.65, 1.0, 1.0);
            Assert.That(a, Is.GreaterThan(26.0));
            Assert.That(double.IsNaN(a), Is.False);
        }

        [Test]
        public void TwoPhaseByTheOmegaMethodMatchesTheApi520Example()
        {
            // D.2.1.2: 477,430 lb/h, set at 60 psig, P0 = 80.7 psia, back pressure 29.7 psia,
            // v0 = 0.3116 ft3/lb, v9 = 0.3629 ft3/lb (isenthalpic flash), Kd = 0.85, Kb = Kc = 1.
            var w = 477430.0 * 0.45359237 / 3600.0;
            var p0 = PSV.Sizing.RelievingPressure(60.0 * PsiToPa + Atm, 10.0);
            var res = PSV.Sizing.TwoPhaseArea(0.3116 * Ft3PerLbToM3PerKg, 0.3629 * Ft3PerLbToM3PerKg,
                p0, 29.7 * PsiToPa, w, 0.85, 1.0, 1.0);

            var gLbSFt2 = res[3] * 0.204816;
            Console.WriteLine($"two-phase: omega = {res[1]:F3} (1.482), eta_c = {res[2]:F3} (0.66 off the chart), " +
                              $"G = {gLbSFt2:F1} lb/s ft2 (594.1), A = {res[0]:F2} in2 (37.8)");

            Assert.That(p0 / PsiToPa, Is.EqualTo(80.7).Within(0.5).Percent);
            Assert.That(res[1], Is.EqualTo(1.482).Within(0.2).Percent);
            Assert.That(res[4], Is.EqualTo(1.0), "the example is in critical flow");
            Assert.That(gLbSFt2, Is.EqualTo(594.1).Within(2).Percent);
            Assert.That(res[0], Is.EqualTo(37.8).Within(2).Percent);
        }

        [Test]
        public void TheOmegaFluxIsContinuousAtTheCriticalPressure()
        {
            // at omega = 1 the omega method is an isothermal ideal gas: eta_c = exp(-1/2)
            Assert.That(PSV.Sizing.OmegaCriticalPressureRatio(1.0), Is.EqualTo(Math.Exp(-0.5)).Within(1e-6).Percent);

            foreach (var omega in new[] { 0.3, 1.482, 5.0, 20.0 })
            {
                var etac = PSV.Sizing.OmegaCriticalPressureRatio(omega);
                var critical = PSV.Sizing.TwoPhaseArea(1.0, 1.0 + omega / 9.0, 1.0e6, 0.999 * etac * 1.0e6, 1.0, 1.0, 1.0, 1.0);
                var subcritical = PSV.Sizing.TwoPhaseArea(1.0, 1.0 + omega / 9.0, 1.0e6, 1.001 * etac * 1.0e6, 1.0, 1.0, 1.0, 1.0);
                Assert.That(critical[4], Is.EqualTo(1.0));
                Assert.That(subcritical[4], Is.EqualTo(0.0));
                Assert.That(subcritical[3], Is.EqualTo(critical[3]).Within(0.1).Percent, $"omega = {omega}");
            }
        }

        [Test]
        public void ANearCriticalNaturalGasIsFlaggedForTheIdealGasAssumption()
        {
            var fs = Flowsheet.Create("PsvIdealGasWarning")
                .WithCompounds("Methane", "Ethane", "Propane", "Nitrogen", "Carbon dioxide")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var dense = fs.AddMaterialStream("sales gas")
                .At(240.Kelvin(), 70.Bar())
                .WithMassFlow(1.0.KgPerSecond())
                .WithComposition(c => c.Mole("Methane", 0.88).Mole("Ethane", 0.07).Mole("Propane", 0.03)
                                       .Mole("Nitrogen", 0.01).Mole("Carbon dioxide", 0.01));

            var light = fs.AddMaterialStream("low pressure gas")
                .At(300.Kelvin(), 5.Bar())
                .WithMassFlow(1.0.KgPerSecond())
                .WithComposition(c => c.Mole("Methane", 0.88).Mole("Ethane", 0.07).Mole("Propane", 0.03)
                                       .Mole("Nitrogen", 0.01).Mole("Carbon dioxide", 0.01));

            fs.Solve();

            (double z, double k) Gas(DWSIM.Thermodynamics.Streams.MaterialStream ms)
            {
                var p = ms.Phases[2].Properties;
                return (p.compressibilityFactor.GetValueOrDefault(),
                        p.heatCapacityCp.GetValueOrDefault() / p.heatCapacityCv.GetValueOrDefault());
            }

            var d = Gas(dense.Object);
            var l = Gas(light.Object);
            Console.WriteLine($"240 K, 70 bar: Z = {d.z:F3}, Cp/Cv = {d.k:F3}");
            Console.WriteLine($"300 K, 5 bar: Z = {l.z:F3}, Cp/Cv = {l.k:F3}");

            Assert.That(PSV.Sizing.IdealGasWarning(d.z, d.k), Is.Not.Null);
            Assert.That(PSV.Sizing.IdealGasWarning(l.z, l.k), Is.Null);
        }

        [Test]
        public void TheOmegaVolumesComeFromAnIsentropicFlashOfTheStream()
        {
            var fs = Flowsheet.Create("PsvOmegaVolumes")
                .WithCompounds("Methane", "N-hexane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("two-phase")
                .At(320.Kelvin(), 20.Bar())
                .WithMassFlow(10.0.KgPerSecond())
                .WithComposition(c => c.Mole("Methane", 0.3).Mole("N-hexane", 0.7));

            fs.Solve();

            var ms = feed.Object;
            var pBefore = ms.Phases[0].Properties.pressure.GetValueOrDefault();
            var currentBefore = ms.PropertyPackage.CurrentMaterialStream;
            var vf = ms.Phases[2].Properties.molarfraction.GetValueOrDefault();
            Assert.That(vf, Is.GreaterThan(0.0).And.LessThan(1.0), "the test stream must be two-phase");

            var v = PSV.Sizing.OmegaSpecificVolumes(ms);
            var omega = 9.0 * (v[1] / v[0] - 1.0);
            Console.WriteLine($"vapour fraction {vf:F3}, v0 = {v[0]:G5} m3/kg, v9 = {v[1]:G5} m3/kg, omega = {omega:F3}");

            Assert.That(v[0], Is.EqualTo(1.0 / ms.Phases[0].Properties.density.GetValueOrDefault()).Within(1e-9).Percent);
            Assert.That(v[1], Is.GreaterThan(v[0]));
            Assert.That(ms.Phases[0].Properties.pressure.GetValueOrDefault(), Is.EqualTo(pBefore), "the stream must be left untouched");
            Assert.That(ms.PropertyPackage.CurrentMaterialStream, Is.SameAs(currentBefore));

            var p0 = PSV.Sizing.RelievingPressure(pBefore, 10.0);
            var res = PSV.Sizing.TwoPhaseArea(v[0], v[1], p0, Atm, 10.0, 0.85, 1.0, 1.0);
            Assert.That(res[0], Is.GreaterThan(0.0));
        }
    }
}
