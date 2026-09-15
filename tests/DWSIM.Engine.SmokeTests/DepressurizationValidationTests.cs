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
        // II: experimental validation", Trans IChemE 70B: nitrogen from 150 bar and about 20 C in a
        // 0.273 m ID x 1.524 m vertical vessel (0.0892 m3, 25 mm carbon steel wall) through a 6.35 mm
        // top orifice. The published record shows the pressure reaching atmospheric in roughly 100 s,
        // the bulk gas bottoming out around 200 K near 50 to 60 s, and the inner wall staying far
        // warmer, close to 270 K. The run is compared with those figures and the numbers are printed
        // for the report.
        [Test]
        public void HaqueNitrogenBlowdownIsReproducedInOutline()
        {
            var (fs, src, pp) = Host(new[] { "Nitrogen" }, new[] { 1.0 }, 293.15, 150e5);
            var input = new DepressurizationInput
            {
                SourceStreamName = src.Name, InitialPressure = 150e5, InitialTemperature = 293.15, InitialLiquidVolumeFraction = 0.0,
                Diameter = 0.273, Length = 1.524, HeadType = "Flat", WallThickness = 0.025, WallMaterial = "Carbon Steel",
                OrificeDiameter = 0.00635, DischargeCoefficient = 0.8, BackPressure = 101325.0, AmbientTemperature = 293.15,
                IncludeWallHeatTransfer = true, TimeStep = 0.5, Duration = 200.0
            };
            var r = DepressurizationStudy.Run(fs, input);
            Assert.That(r.Warnings.Where(w => w.StartsWith("The integration stopped")), Is.Empty);

            var tMinPoint = r.Points.OrderBy(p => p.Temperature).First();
            var t2bar = r.Points.FirstOrDefault(p => p.Pressure <= 2e5);
            TestContext.WriteLine($"V {r.VesselVolume:F4} m3, m0 {r.InitialMass:F2} kg");
            TestContext.WriteLine("   t(s)    P(bar)   T gas   T wall(dry)  W(kg/s)");
            foreach (var pt in r.Points.Where(p => Math.Abs(p.Time % 10.0) < 1e-9))
                TestContext.WriteLine($"{pt.Time,7:F0}  {pt.Pressure / 1e5,8:F1}  {pt.Temperature,7:F1}  {pt.DryWallTemperature,10:F1}  {pt.MassFlow,8:F4}");
            TestContext.WriteLine($"gas minimum {tMinPoint.Temperature:F1} K at {tMinPoint.Time:F0} s; wall minimum {r.MinimumDryWallTemperature:F1} K; 2 bar at {(t2bar != null ? t2bar.Time.ToString("F0") : "never")} s; final {r.FinalPressure / 1e5:F2} bar at {r.FinalTemperature:F1} K");

            Assert.That(t2bar, Is.Not.Null, "the vessel blows down to 2 bar within 200 s");
            Assert.That(t2bar!.Time, Is.InRange(60.0, 160.0), "s: the published blowdown takes about 100 s");
            Assert.That(tMinPoint.Temperature, Is.InRange(180.0, 225.0), "K: the bulk gas bottoms out around 200 K");
            Assert.That(tMinPoint.Time, Is.InRange(30.0, 90.0), "s: the minimum comes past the middle of the blowdown");
            Assert.That(r.MinimumDryWallTemperature, Is.InRange(250.0, 290.0), "K: the wall lags far behind the gas");
        }
    }
}
