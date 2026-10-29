//    The Seawater IAPWS-08 package against IAPWS-08 / IAPWS-95, and seawater that boils
//    (https://github.com/DanWBR/dwsim10/issues/122). The references are those of the issue, computed with the
//    package's own SIA library: IAPWS R13-08 for the saline part, IAPWS-95 for pure water and its vapour.

using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.GlobalSettings;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class SeawaterIapws08Tests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
        }

        private static Flowsheet NewFlowsheet() =>
            Flowsheet.Create("Seawater").WithCompounds("Water", "Salt").WithPropertyPackage(PropertyPackages.Seawater);

        // a stream of seawater at salinity S (kg of salt per kg of seawater)
        private static MaterialStreamBuilder Seawater(Flowsheet fs, string tag, double S, double massFlow = 1.0) =>
            fs.AddMaterialStream(tag).WithMassFlow(massFlow.KgPerSecond())
              .WithComposition(cb => cb.Mass("Water", 1.0 - S).Mass("Salt", S));

        private static double Salinity(MaterialStream ms, int phase)
        {
            var c = ms.Phases[phase].Compounds;
            double w = c["Water"].MassFraction.GetValueOrDefault(), s = c["Salt"].MassFraction.GetValueOrDefault();
            return s / (s + w);
        }

        // the package and a stream's mole fractions at salinity S
        private static (SeawaterPropertyPackage pp, double[] x) Package(double S)
        {
            var fs = NewFlowsheet();
            var ms = Seawater(fs, "SW", S).At(298.15.Kelvin(), 101325.0.Pascal());
            Assert.That(fs.TrySolve(), Is.Empty);
            var pp = (SeawaterPropertyPackage)ms.Object.PropertyPackage;
            pp.CurrentMaterialStream = ms.Object;
            return (pp, ms.Object.Phases[0].Compounds.Values.Select(c => c.MoleFraction.GetValueOrDefault()).ToArray());
        }

        // the boiling point elevation of IAPWS-08 (the issue's table): the saturation temperature of the solution less
        // that of pure water at the same pressure
        [TestCase(7385.0, 0.035, 0.3540)]
        [TestCase(7385.0, 0.070, 0.7624)]
        [TestCase(7385.0, 0.100, 1.1774)]
        [TestCase(101325.0, 0.035, 0.5259)]
        [TestCase(101325.0, 0.070, 1.1371)]
        [TestCase(101325.0, 0.100, 1.7641)]
        public void TheBoilingPointElevationIsIapws08s(double p, double S, double bpe)
        {
            var (pp, x) = Package(S);
            double t0 = pp.SaturationTemperature(new[] { 1.0, 0.0 }, p);
            double t = pp.SaturationTemperature(x, p);
            TestContext.Out.WriteLine($"{p} Pa, {S * 1000} g/kg: BPE {t - t0:F4} K (IAPWS-08 {bpe})");
            Assert.That(t - t0, Is.EqualTo(bpe).Within(0.002));
        }

        // the absolute salinity is kg of salt per kg of seawater: the density at 25 C, 1 atm and 35 g/kg is IAPWS-08's
        [Test]
        public void TheDensityIsIapws08sAtTheAbsoluteSalinity()
        {
            var fs = NewFlowsheet();
            var ms = Seawater(fs, "SW", 0.035).At(298.15.Kelvin(), 101325.0.Pascal());
            Assert.That(fs.TrySolve(), Is.Empty);
            double rho = ms.Object.Phases[3].Properties.density.GetValueOrDefault();
            TestContext.Out.WriteLine($"density {rho:F3} kg/m3");
            Assert.That(rho, Is.EqualTo(1023.220).Within(0.01));
        }

        // the vapour is IAPWS-95's: saturated vapour at 70 C and its latent heat
        [Test]
        public void TheVapourIsIapws95s()
        {
            var (pp, x) = Package(0.035);
            double T = 343.15, p = pp.VaporPressure(new[] { 1.0, 0.0 }, T);
            double hv = pp.DW_CalcEnthalpy(new[] { 1.0, 0.0 }, T, p, State.Vapor);
            double hl = pp.DW_CalcEnthalpy(new[] { 1.0, 0.0 }, T, p, State.Liquid);
            TestContext.Out.WriteLine($"vapour {hv:F3} kJ/kg, latent heat {hv - hl:F3} kJ/kg");
            Assert.That(hv, Is.EqualTo(2626.10).Within(0.05));
            Assert.That(hv - hl, Is.EqualTo(2333.03).Within(0.05));
        }

        // seawater at 35 g/kg freezes below 0 C
        [Test]
        public void SeawaterFreezesBelowZero()
        {
            var (pp, x) = Package(0.035);
            double tf = pp.TemperatureOfFusion(x, 273.15) - 273.15;
            TestContext.Out.WriteLine($"freezing point {tf:F3} C");
            Assert.That(tf, Is.EqualTo(-1.92).Within(0.02));
            var fs = NewFlowsheet();
            var ms = Seawater(fs, "SW", 0.035).At(274.15.Kelvin(), 101325.0.Pascal());
            Assert.That(fs.TrySolve(), Is.Empty);
            Assert.That(ms.Object.Phases[7].Properties.molarfraction.GetValueOrDefault(), Is.EqualTo(0.0).Within(1e-9), "solid at 1 C");
        }

        // a heater adding 500 kW to 1 kg/s at 35 g/kg, 25 C and 1 atm boils part of it
        [Test]
        public void AHeaterBoilsSeawater()
        {
            var fs = NewFlowsheet();
            var feed = Seawater(fs, "IN", 0.035).At(298.15.Kelvin(), 101325.0.Pascal());
            var outlet = fs.AddMaterialStream("OUT");
            fs.AddHeater("H-1").ConnectFeed(feed, 0).ConnectProduct(outlet, 0).WithHeatAdded(500.0.Kilowatts()).WithPressureDrop(0.0.Pascal());
            Assert.That(fs.TrySolve(), Is.Empty);
            Check(outlet.Object, 100.554, 0.0871, 0.03834);
        }

        // brine at 70 g/kg, 90 C and 1 atm let down to 40 kPa flashes
        [Test]
        public void AValveFlashesBrine()
        {
            var fs = NewFlowsheet();
            var feed = Seawater(fs, "IN", 0.070).At(363.15.Kelvin(), 101325.0.Pascal());
            var outlet = fs.AddMaterialStream("OUT");
            fs.AddValve("V-1").ConnectFeed(feed, 0).ConnectProduct(outlet, 0).WithOutletPressure(40000.0.Pascal());
            Assert.That(fs.TrySolve(), Is.Empty);
            Check(outlet.Object, 76.860, 0.0220, 0.07157);
        }

        // seawater at 35 g/kg, 60 C and 25 kPa into a vessel heated by 500 kW, as an evaporator effect
        [Test]
        public void AHeatedVesselEvaporatesSeawater()
        {
            var fs = NewFlowsheet();
            var feed = Seawater(fs, "IN", 0.035).At(333.15.Kelvin(), 25000.0.Pascal());
            var v = fs.AddMaterialStream("VAPOUR");
            var l = fs.AddMaterialStream("BRINE");
            var ev = fs.AddSeparator("EV-1").ConnectFeed(feed, 0).ConnectProduct(v, 0).ConnectProduct(l, 1);
            ev.ConnectEnergyFeed(fs.AddEnergyStream("Q").WithEnergyFlow(500.0.Kilowatts()), 6);
            Assert.That(fs.TrySolve(), Is.Empty);
            double wv = v.Object.GetMassFlow(), wl = l.Object.GetMassFlow();
            TestContext.Out.WriteLine($"T {l.Object.GetTemperature() - 273.15:F3} C, vapour {wv / (wv + wl):P2}, brine {Salinity(l.Object, 0) * 1000:F2} g/kg");
            Assert.That(l.Object.GetTemperature() - 273.15, Is.EqualTo(65.502).Within(0.01));
            Assert.That(wv / (wv + wl), Is.EqualTo(0.2038).Within(0.0005));
            Assert.That(Salinity(l.Object, 0), Is.EqualTo(0.04396).Within(0.00005));
        }

        // a stream at 30 kPa and a vapour fraction: the temperature is the bubble point of the brine left, not the feed's
        [TestCase(0.0, 69.528)]
        [TestCase(0.4, 69.840)]
        public void APressureVapourFractionFlashTakesTheBrinesBubblePoint(double vf, double tC)
        {
            var fs = NewFlowsheet();
            var ms = Seawater(fs, "SW", 0.035).WithPressure(30000.0.Pascal()).WithVaporFraction(vf);
            Assert.That(fs.TrySolve(), Is.Empty);
            TestContext.Out.WriteLine($"VF {vf}: {ms.Object.GetTemperature() - 273.15:F3} C");
            Assert.That(ms.Object.GetTemperature() - 273.15, Is.EqualTo(tC).Within(0.01));
        }

        private static void Check(MaterialStream ms, double tC, double vapourMassFraction, double brine)
        {
            double t = ms.GetTemperature() - 273.15, wv = ms.Phases[2].Properties.massfraction.GetValueOrDefault();
            double sb = Salinity(ms, 1);
            TestContext.Out.WriteLine($"T {t:F3} C, vapour {wv:P2}, brine {sb * 1000:F2} g/kg");
            Assert.That(t, Is.EqualTo(tC).Within(0.01));
            Assert.That(wv, Is.EqualTo(vapourMassFraction).Within(0.0005));
            Assert.That(sb, Is.EqualTo(brine).Within(0.00005));
            // the salt stays in the liquid
            double salt = ms.Phases[0].Compounds["Salt"].MassFlow.GetValueOrDefault();
            double saltL = ms.Phases[1].Compounds["Salt"].MassFlow.GetValueOrDefault();
            Assert.That(saltL, Is.EqualTo(salt).Within(1e-9 + 1e-6 * salt), "salt balance");
        }
    }
}
