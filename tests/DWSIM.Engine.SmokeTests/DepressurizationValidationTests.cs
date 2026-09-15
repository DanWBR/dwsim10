//    Depressurization validation: analytical ideal-gas blowdown, the isentropic path of a real gas,
//    and the Imperial College nitrogen blowdown of Haque et al. (1992).
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
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class DepressurizationValidationTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static (DWSIM.DynamicRunner.Flowsheet fs, MaterialStream source, PengRobinsonPropertyPackage pp) Host(string[] compounds, double[] z, double T, double P)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            foreach (var c in compounds) fs.AddCompound(c);
            var pp = new PengRobinsonPropertyPackage { Flowsheet = fs };
            fs.AddPropertyPackage(pp);
            var o = fs.AddObject(ObjectType.MaterialStream, 0, 0, "src");
            var ms = (MaterialStream)fs.SimulationObjects[o.Name];
            ms.SetFlowsheet(fs);
            ms.SetPropertyPackage(pp);
            ms.SetOverallComposition(z);
            ms.SetTemperature(T);
            ms.SetPressure(P);
            ms.SetMassFlow(1.0);
            ms.Calculate();
            return (fs, ms, pp);
        }

        // Adiabatic blowdown of an ideal gas through a choked orifice has a closed form: with
        // a = (Cd A / V) c0 psi, psi = (2/(k+1))^((k+1)/(2(k-1))), c0 = sqrt(k R T0 / M),
        //   P/P0 = [1 + (k-1)/2 a t]^(-2k/(k-1)),   T/T0 = [1 + (k-1)/2 a t]^(-2)
        // as long as the orifice stays choked. Methane at 10 bar is within 2 % of ideal, so the
        // Peng-Robinson run must follow the curve to a few percent.
        [Test]
        public void MethaneAtTenBarFollowsTheIdealGasSolution()
        {
            var (fs, src, pp) = Host(new[] { "Methane" }, new[] { 1.0 }, 300.0, 10e5);
            var input = new DepressurizationInput
            {
                SourceStreamName = src.Name, InitialPressure = 10e5, InitialTemperature = 300.0, InitialLiquidVolumeFraction = 0.0,
                Diameter = 1.5, Length = 6.0, HeadType = "Flat", OrificeDiameter = 0.020, DischargeCoefficient = 0.62,
                BackPressure = 101325.0, IncludeWallHeatTransfer = false, TimeStep = 0.5, Duration = 400.0, StopAtPressure = 2.0e5
            };
            var r = DepressurizationStudy.Run(fs, input);
            Assert.That(r.Warnings.Where(w => w.StartsWith("The integration stopped")), Is.Empty);

            pp.CurrentMaterialStream = src;
            double M = pp.AUX_MMM(new[] { 1.0 }) / 1000.0;                       // kg/mol
            double cpig = pp.AUX_CPm(Phase.Vapor, 300.0) * M * 1000.0;           // J/(mol K)
            double k = cpig / (cpig - 8.314);
            double V = r.VesselVolume, A = Math.PI * 0.02 * 0.02 / 4.0;
            double c0 = Math.Sqrt(k * 8.314 * 300.0 / M);
            double psi = Math.Pow(2.0 / (k + 1.0), (k + 1.0) / (2.0 * (k - 1.0)));
            double a = 0.62 * A / V * c0 * psi;
            double rc = Math.Pow(2.0 / (k + 1.0), k / (k - 1.0));

            TestContext.WriteLine($"k = {k:F3}, V = {V:F3} m3, a = {a:G4} 1/s; points {r.Points.Count}");
            TestContext.WriteLine("   t(s)   P model   P ideal   dP%    T model  T ideal");
            double worstP = 0, worstT = 0;
            foreach (var pt in r.Points.Where(x => x.Time > 0 && x.Pressure * rc > 101325.0 * 1.05))
            {
                double f = 1.0 + (k - 1.0) / 2.0 * a * pt.Time;
                double pIdeal = 10e5 * Math.Pow(f, -2.0 * k / (k - 1.0));
                double tIdeal = 300.0 * Math.Pow(f, -2.0);
                double dp = (pt.Pressure - pIdeal) / pIdeal * 100.0;
                worstP = Math.Max(worstP, Math.Abs(dp));
                worstT = Math.Max(worstT, Math.Abs(pt.Temperature - tIdeal));
                if (Math.Abs(pt.Time % 20.0) < 1e-9 || pt.Time < 3)
                    TestContext.WriteLine($"{pt.Time,7:F1}  {pt.Pressure / 1e5,7:F3}  {pIdeal / 1e5,7:F3}  {dp,5:F1}  {pt.Temperature,7:F1}  {tIdeal,7:F1}");
            }
            TestContext.WriteLine($"worst: pressure {worstP:F1} %, temperature {worstT:F1} K");
            Assert.That(worstP, Is.LessThan(4.0), "% pressure deviation from the ideal-gas solution");
            // the closed form keeps k constant; methane's Cp falls as it cools, so the real isentrope runs
            // a few K below it by the end (the real-gas isentrope itself is checked in the next test)
            Assert.That(worstT, Is.LessThan(6.0), "K temperature deviation from the constant-k ideal-gas solution");
        }

        // Without a wall the content of a blowdown expands reversibly: the gas that stays behind
        // follows a constant-entropy path, whatever the orifice does. So T at each pressure must equal
        // the PS flash of the fluid from the initial state, here a real gas mixture at 60 bar where the
        // departure from ideal is far from negligible. This checks the UV balance and the VU flash.
        [Test]
        public void AWallLessBlowdownOfARealGasStaysOnTheIsentrope()
        {
            var z = new[] { 0.85, 0.10, 0.05 };
            var (fs, src, pp) = Host(new[] { "Methane", "Ethane", "Propane" }, z, 310.0, 60e5);
            var input = new DepressurizationInput
            {
                SourceStreamName = src.Name, InitialPressure = 60e5, InitialTemperature = 310.0, InitialLiquidVolumeFraction = 0.0,
                Diameter = 1.0, Length = 3.0, OrificeDiameter = 0.015, DischargeCoefficient = 0.62,
                BackPressure = 101325.0, IncludeWallHeatTransfer = false, TimeStep = 0.5, Duration = 600.0, StopAtPressure = 5e5
            };
            var r = DepressurizationStudy.Run(fs, input);
            Assert.That(r.Warnings.Where(w => w.StartsWith("The integration stopped")), Is.Empty);
            Assert.That(r.TimeToStopPressure, Is.Not.Null);

            pp.CurrentMaterialStream = src;
            var start = pp.FlashBase.CalculateEquilibrium(FlashSpec.P, FlashSpec.T, 60e5, 310.0, pp, z, null, 0.0);
            double s0 = Convert.ToDouble(start.CalculatedEntropy);

            TestContext.WriteLine("   t(s)    P(bar)   T model   T isentrope");
            double worst = 0;
            foreach (var pt in r.Points.Where(x => x.Time > 0))
            {
                var iso = pp.FlashBase.CalculateEquilibrium(FlashSpec.P, FlashSpec.S, pt.Pressure, s0, pp, z, null, pt.Temperature);
                double tIso = Convert.ToDouble(iso.CalculatedTemperature);
                worst = Math.Max(worst, Math.Abs(pt.Temperature - tIso));
                if (Math.Abs(pt.Time % 30.0) < 1e-9)
                    TestContext.WriteLine($"{pt.Time,7:F1}  {pt.Pressure / 1e5,8:F2}  {pt.Temperature,8:F2}  {tIso,8:F2}");
            }
            TestContext.WriteLine($"worst deviation from the isentrope: {worst:F2} K; final {r.FinalPressure / 1e5:F1} bar, {r.FinalTemperature:F1} K");
            Assert.That(worst, Is.LessThan(2.0), "K: explicit Euler drift over a 60 to 5 bar blowdown");
        }

        // Haque, Richardson, Saville, Chamberlain and Shirvill (1992), "Blowdown of pressure vessels
        // II: experimental validation", Trans IChemE 70B, as redrawn in Shafiq et al. (2020), Process
        // Safety and Environmental Protection 133, Fig. 5b: nitrogen from 150 bar in a 0.273 m ID x
        // 1.524 m vertical vessel (0.086 m3, 25 mm carbon steel wall) through a 6.35 mm top orifice.
        // Read from the figure, as changes from the initial temperature: the bulk gas drops 108 K by
        // about 40 s and recovers to -60 K by 100 s; the inner wall drops only 5 to 6 K and stays there;
        // the pressure reaches atmospheric in about 100 s. The run is compared with those figures and
        // the numbers are printed for the report. Factor 1 is the natural-convection correlation as is;
        // 0.5 shows the sensitivity of the gas minimum to the film coefficient; 0 is the adiabatic bound.
        [TestCase(1.0)]
        [TestCase(0.5)]
        [TestCase(0.0)]
        public void HaqueNitrogenBlowdownIsReproducedInOutline(double htcFactor)
        {
            var (fs, src, pp) = Host(new[] { "Nitrogen" }, new[] { 1.0 }, 293.15, 150e5);
            var input = new DepressurizationInput
            {
                SourceStreamName = src.Name, InitialPressure = 150e5, InitialTemperature = 293.15, InitialLiquidVolumeFraction = 0.0,
                Diameter = 0.273, Length = 1.524, HeadType = "Flat", WallThickness = 0.025, WallMaterial = "Carbon Steel",
                OrificeDiameter = 0.00635, DischargeCoefficient = 0.8, BackPressure = 101325.0, AmbientTemperature = 293.15,
                IncludeWallHeatTransfer = htcFactor > 0.0, InternalHeatTransferFactor = htcFactor, TimeStep = 0.5, Duration = 200.0
            };
            var r = DepressurizationStudy.Run(fs, input);
            Assert.That(r.Warnings.Where(w => w.StartsWith("The integration stopped")), Is.Empty);

            var tMinPoint = r.Points.OrderBy(p => p.Temperature).First();
            var t2bar = r.Points.FirstOrDefault(p => p.Pressure <= 2e5);
            TestContext.WriteLine($"V {r.VesselVolume:F4} m3, m0 {r.InitialMass:F2} kg");
            TestContext.WriteLine("   t(s)    P(bar)   T gas   T wall(dry)  W(kg/s)   h(W/m2K)");
            foreach (var pt in r.Points.Where(p => Math.Abs(p.Time % 10.0) < 1e-9))
                TestContext.WriteLine($"{pt.Time,7:F0}  {pt.Pressure / 1e5,8:F1}  {pt.Temperature,7:F1}  {pt.DryWallTemperature,10:F1}  {pt.MassFlow,8:F4}  {pt.DryWallHeatTransferCoefficient,8:F0}");
            TestContext.WriteLine($"gas minimum {tMinPoint.Temperature:F1} K at {tMinPoint.Time:F0} s; wall minimum {r.MinimumDryWallTemperature:F1} K; 2 bar at {(t2bar != null ? t2bar.Time.ToString("F0") : "never")} s; final {r.FinalPressure / 1e5:F2} bar at {r.FinalTemperature:F1} K");

            if (htcFactor != 1.0) return; // the other factors are printed for the sensitivity table only
            Assert.That(t2bar, Is.Not.Null, "the vessel blows down to 2 bar within 200 s");
            Assert.That(t2bar!.Time, Is.InRange(60.0, 130.0), "s: the published blowdown reaches atmospheric in about 100 s");
            Assert.That(293.15 - tMinPoint.Temperature, Is.InRange(80.0, 120.0), "K: the published gas drop is 108 K; the model runs 15 to 20 K warm");
            Assert.That(tMinPoint.Time, Is.InRange(25.0, 60.0), "s: the published minimum is at about 40 s");
            Assert.That(293.15 - r.MinimumDryWallTemperature, Is.InRange(2.0, 10.0), "K: the published inner-wall drop is 5 to 6 K");
            var t100 = r.Points.First(p => p.Time >= 100.0);
            Assert.That(293.15 - t100.Temperature, Is.InRange(30.0, 80.0), "K: the published gas is 60 K below the start at 100 s");
        }
        // Fredenhagen and Eggers (2001), CO2 with N2 blown down from a top-vented, liquid-full 0.05 m3
        // vessel (0.242 m ID) through a 17 mm2 orifice, as redrawn in Shafiq et al. (2020) Figs. 10 and
        // 11. Measured: 14 MPa falls to the bubble point near 8 MPa in about 3 s, then 5.6 MPa at 20 s,
        // 3.9 at 40, 2.8 at 60, 2.05 at 80; the liquid goes 298 K, 277 K at 20 s, 270 at 40, 262 at 60,
        // 255 at 80, 246 at 120, 236 at 150. The N2 content is not given in the review; it is inferred
        // from the bubble point at the kink (8 MPa near 290 K) and printed. Both runs are printed for the
        // report; the assertions take the inferred composition.
        [TestCase(0.0)]
        [TestCase(-1.0)]
        public void FredenhagenEggersCarbonDioxideBlowdownIsReproducedInOutline(double nitrogen)
        {
            bool inferred = nitrogen < 0;   // the pure CO2 run is printed as a reference only
            if (inferred)
            {
                // bubble pressure of CO2 + N2 at 286 K (the liquid temperature at the end of the liquid-full stage): pick the N2 fraction that puts it at 8 MPa
                var (fs0, s0, pp0) = Host(new[] { "Carbon dioxide", "Nitrogen" }, new[] { 0.97, 0.03 }, 286.0, 80e5);
                pp0.CurrentMaterialStream = s0;
                double best = 0.0, bestErr = double.MaxValue;
                foreach (var x in new[] { 0.02, 0.04, 0.06 })
                {
                    // past the mixture critical locus (between 7 and 8 % N2 for Peng-Robinson at 286 K)
                    // the bubble point no longer exists and the flash fails or returns junk; 7 % is so close
                    // to it that the flashes crawl, so the scan stops at 6 %
                    double pb;
                    try { pb = Convert.ToDouble(((object[])pp0.FlashBase.Flash_TV(new[] { 1 - x, x }, 286.0, 0.0, 60e5, pp0))[4]); }
                    catch { pb = double.NaN; }
                    TestContext.WriteLine($"x N2 {x:F2}: bubble P at 286 K = {pb / 1e6:F2} MPa");
                    if (pb > 5e6 && pb < 12e6 && Math.Abs(pb - 8e6) < bestErr) { bestErr = Math.Abs(pb - 8e6); best = x; }
                }
                nitrogen = best;
                TestContext.WriteLine($"inferred N2 fraction {nitrogen:F2}");
            }
            var (fs, src, pp) = Host(new[] { "Carbon dioxide", "Nitrogen" }, new[] { 1 - nitrogen, nitrogen }, 298.0, 140e5);
            double length = 0.05 / (Math.PI / 4 * 0.242 * 0.242);
            TestContext.WriteLine($"initial state 298 K / 14 MPa: vapour fraction {Convert.ToDouble(src.Phases[2].Properties.molarfraction):F4}, density {Convert.ToDouble(src.Phases[0].Properties.density):F1} kg/m3, liquid density {Convert.ToDouble(src.Phases[1].Properties.density):F1}");
            var input = new DepressurizationInput
            {
                SourceStreamName = src.Name, InitialPressure = 140e5, InitialTemperature = 298.0, InitialLiquidVolumeFraction = 1.0,
                Diameter = 0.242, Length = length, HeadType = "Flat", WallThickness = 0.025, WallMaterial = "Carbon Steel",
                OrificeDiameter = Math.Sqrt(4 * 17e-6 / Math.PI), DischargeCoefficient = 0.8, BackPressure = 101325.0, AmbientTemperature = 298.0,
                IncludeWallHeatTransfer = true, TimeStep = 0.2, Duration = 150.0
            };
            var r = DepressurizationStudy.Run(fs, input);
            foreach (var w in r.Warnings) TestContext.WriteLine("warning: " + w);
            Assert.That(r.Warnings.Where(w => w.StartsWith("The integration stopped")), Is.Empty);

            TestContext.WriteLine($"N2 {nitrogen:F2}, V {r.VesselVolume:F4} m3, m0 {r.InitialMass:F2} kg");
            TestContext.WriteLine("   t(s)   P(MPa)   T(K)   T wall(wet)  W(kg/s)  liq frac  liq in vent  m out(kg)");
            foreach (var pt in r.Points.Where(p => p.Time <= 3.0 ? true : p.Time <= 10.0 ? Math.Round(p.Time, 3) % 1.0 == 0.0 : Math.Round(p.Time, 3) % 10.0 == 0.0))
                TestContext.WriteLine($"{pt.Time,7:F1}  {pt.Pressure / 1e6,7:F2}  {pt.Temperature,6:F1}  {pt.WettedWallTemperature,10:F1}  {pt.MassFlow,8:F3}  {pt.LiquidVolumeFraction,8:F3}  {1 - pt.VapourFractionOut,8:F2}  {pt.CumulativeMass,8:F3}");

            DepressurizationPoint At(double t) => r.Points.First(p => p.Time >= t - 1e-6);
            if (!inferred) return;
            Assert.That(1 - At(0.2).VapourFractionOut, Is.GreaterThan(0.9), "the top vent carries liquid while the vessel is liquid-full");
            Assert.That(At(5.0).Pressure / 1e6, Is.InRange(6.0, 9.5), "MPa: the liquid-full stage ends at the bubble point (measured near 8 MPa) within a few seconds");
            Assert.That(At(20.0).Pressure / 1e6, Is.InRange(4.5, 6.8), "MPa at 20 s (measured 5.6)");
            Assert.That(At(80.0).Pressure / 1e6, Is.InRange(1.5, 2.8), "MPa at 80 s (measured 2.05)");
            Assert.That(At(20.0).Temperature, Is.InRange(270.0, 285.0), "K at 20 s (measured 277)");
            Assert.That(At(80.0).Temperature, Is.InRange(247.0, 263.0), "K at 80 s (measured 255)");
            Assert.That(At(150.0).Temperature, Is.InRange(225.0, 250.0), "K at 150 s (measured 236)");
        }
        // Saville, Richardson and Barker (2004), "Leakage in ethylene pipelines", Trans IChemE 82B:
        // 61-68, Tables 2, 4, 6 and 8: BLOWDOWN's homogeneous-equilibrium flow of ethylene at 10 C from
        // a pipeline through a 10 mm hole to the atmosphere, the fluid supercritical (90, 79, 69, 59.6
        // bar) or gaseous (44, 28 bar), and the 50 mm hole at 90 and 44 bar. The paper found the hole
        // choked even for the all-liquid dense phase, at a throat pressure around 41 to 45 bar, far
        // above the ideal-gas critical ratio. Compared with the valve's orifice model at Cd = 1.
        [Test]
        public void SavilleEthyleneHoleFlowsAreReproduced()
        {
            var cases = new[]
            {
                (P0: 90.0, d: 0.010, W: 4.4, Pt: 41.4), (P0: 79.0, d: 0.010, W: 3.8, Pt: 43.1), (P0: 69.0, d: 0.010, W: 3.2, Pt: 45.4),
                (P0: 59.6, d: 0.010, W: 2.3, Pt: 0.0), (P0: 44.0, d: 0.010, W: 0.95, Pt: 15.5), (P0: 28.0, d: 0.010, W: 0.55, Pt: 0.0),
                (P0: 90.0, d: 0.050, W: 114.0, Pt: 45.4), (P0: 44.0, d: 0.050, W: 24.0, Pt: 17.7)
            };
            var (fs, src, pp) = Host(new[] { "Ethylene" }, new[] { 1.0 }, 283.15, 90e5);
            var valve = new DWSIM.UnitOperations.UnitOperations.Valve { OrificeDischargeCoefficient = 1.0 };
            TestContext.WriteLine("  P0(bar)  hole(mm)  rho(kg/m3)  vf  |  W paper  W model  dev(%)  |  Pthroat paper  model (bar)");
            double worst = 0.0;
            foreach (var c in cases)
            {
                src.SetPressure(c.P0 * 1e5);
                src.SetTemperature(283.15);
                src.Calculate();
                valve.OrificeDiameter = c.d;
                double w = valve.OrificeMassFlow(src, c.P0 * 1e5, 101325.0, 283.15, 1.0);
                double dev = (w / c.W - 1) * 100;
                worst = Math.Max(worst, Math.Abs(dev));
                TestContext.WriteLine($"{c.P0,7:F1}  {c.d * 1000,7:F0}  {Convert.ToDouble(src.Phases[0].Properties.density),10:F1}  {Convert.ToDouble(src.Phases[2].Properties.molarfraction),4:F2}  |  {c.W,7:F2}  {w,7:F2}  {dev,6:F1}  |  {(c.Pt > 0 ? c.Pt.ToString("F1") : "  n/a"),13}  {valve.OrificeThroatPressure / 1e5,6:F1}");
                Assert.That(w, Is.EqualTo(c.W).Within(0.15 * c.W), $"kg/s through a {c.d * 1000:F0} mm hole at {c.P0} bar");
                // the gas cases enter the dome on the isentrope and the flux curve goes flat between the
                // gas-side maximum and the two-phase one, so only the dense-phase throat is checked
                if (c.Pt > 0 && c.P0 > 50) Assert.That(valve.OrificeThroatPressure / 1e5, Is.EqualTo(c.Pt).Within(8.0), $"bar: choke pressure at {c.P0} bar (the paper itself gives 41.4 and 45.4 for the two holes)");
            }
            TestContext.WriteLine($"worst deviation {worst:F1} %");
        }
        // Richardson and Saville (1996), "Blowdown of LPG pipelines", Trans IChemE 74B: 235-244, Isle of
        // Grain test P47: a 100 m x 154 mm ID (7.3 mm wall) horizontal line full of LPG (95 % propane,
        // 5 % butane) at 21.3 bar and 14.6 C, blown down through a 50 mm nominal orifice at its end
        // (adopted equivalent 70.4 mm, Cd 0.80) to 1 bar, ambient 15.4 C. Read from Fig. 4 (measured,
        // closed end): pressure 7.4, 7.0, 6.3, 5.0, 3.3, 2.0, 1.3 bar at 0, 20, 40, 60, 80, 100, 120 s;
        // temperature 14.6, 11, 5, -3, -13, -24, -32 C; inventory 0.95, 0.60, 0.37, 0.22, 0.15, 0.11,
        // 0.08 t. The orifice is small against the bore, so the line behaves as a vessel (the paper
        // notes the open- and closed-end pressures nearly coincide in this test). The hole sits at the
        // end of the pipe, centred on the axis. BLOWDOWN treats the flow along the line as homogeneous
        // two-phase, and so does the asserted run (the outlet takes the bulk quality); the stratified run,
        // in which the hole passes the phase at its level, is printed for the comparison and shows why:
        // it holds the liquid back and the pressure falls too fast. The paper notes that the measured
        // inventory keeps a residual (the hole acts as a dam) while the prediction goes to zero.
        [TestCase(true)]
        [TestCase(false)]
        public void RichardsonSavilleLpgPipelineP47IsReproducedInOutline(bool homogeneous)
        {
            var (fs, src, pp) = Host(new[] { "Propane", "N-butane" }, new[] { 0.95, 0.05 }, 287.75, 21.3e5);
            var input = new DepressurizationInput
            {
                SourceStreamName = src.Name, InitialPressure = 21.3e5, InitialTemperature = 287.75, InitialLiquidVolumeFraction = 1.0,
                Horizontal = true, Diameter = 0.154, Length = 100.0, HeadType = "Flat", WallThickness = 0.0073, WallMaterial = "Carbon Steel",
                OutletNozzleElevation = 0.077 + 0.0704 / 2,   // top edge of the hole, centred on the pipe axis
                OutletHomogeneous = homogeneous,
                OrificeDiameter = 0.0704, DischargeCoefficient = 0.80, BackPressure = 1.0e5, AmbientTemperature = 288.55,
                IncludeWallHeatTransfer = true, TimeStep = 0.2, Duration = 140.0
            };
            var r = DepressurizationStudy.Run(fs, input);
            foreach (var w in r.Warnings) TestContext.WriteLine("warning: " + w);
            Assert.That(r.Warnings.Where(w => w.StartsWith("The integration stopped")), Is.Empty);

            TestContext.WriteLine($"{(homogeneous ? "homogeneous outlet" : "stratified outlet")}: V {r.VesselVolume:F3} m3, m0 {r.InitialMass:F0} kg");
            TestContext.WriteLine("   t(s)   P(bar)   T(C)   T wall(wet)  W(kg/s)  liq frac  liq in vent  inventory(t)");
            foreach (var pt in r.Points.Where(p => p.Time <= 2.0 ? true : Math.Round(p.Time, 3) % 10.0 == 0.0))
                TestContext.WriteLine($"{pt.Time,7:F1}  {pt.Pressure / 1e5,7:F2}  {pt.Temperature - 273.15,6:F1}  {pt.WettedWallTemperature - 273.15,10:F1}  {pt.MassFlow,8:F2}  {pt.LiquidVolumeFraction,8:F3}  {1 - pt.VapourFractionOut,8:F2}  {(r.InitialMass - pt.CumulativeMass) / 1000.0,8:F3}");

            DepressurizationPoint At(double t) => r.Points.First(p => p.Time >= t - 1e-6);
            double Inv(double t) => (r.InitialMass - At(t).CumulativeMass) / 1000.0;
            if (!homogeneous) return;   // the stratified run is printed for the comparison only
            Assert.That(r.InitialMass / 1000.0, Is.EqualTo(0.95).Within(0.08), "t: the measured initial inventory");
            Assert.That(At(2.0).Pressure / 1e5, Is.InRange(6.0, 8.5), "bar: the compressed liquid expands to the bubble point at once (measured 7.4)");
            Assert.That(At(40.0).Pressure / 1e5, Is.InRange(4.8, 7.3), "bar at 40 s (measured 6.3)");
            Assert.That(At(80.0).Pressure / 1e5, Is.InRange(2.0, 4.6), "bar at 80 s (measured 3.3)");
            Assert.That(At(40.0).Temperature - 273.15, Is.InRange(-2.0, 10.0), "C at 40 s (measured 5)");
            Assert.That(At(60.0).Temperature - 273.15, Is.InRange(-9.0, 5.0), "C at 60 s (measured -3)");
            Assert.That(r.MinimumFluidTemperature - 273.15, Is.InRange(-46.0, -28.0), "C: the measured minimum is -33 C; the model empties the line, as BLOWDOWN did, and runs a few degrees colder");
            Assert.That(Inv(20.0), Is.InRange(0.45, 0.75), "t at 20 s (measured 0.60)");
            Assert.That(Inv(60.0), Is.InRange(0.10, 0.34), "t at 60 s (measured 0.22)");
        }
    }
}
