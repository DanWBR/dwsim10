//    Gas-liquid separator in dynamic mode: wetted/dry wall split, API 521 fire case, temperature tracking.
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
using DWSIM.Automation.DynamicRunner;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.Pipe;
using NUnit.Framework;
using DynamicsSpecType = DWSIM.Interfaces.Enums.Dynamics.DynamicsSpecType;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The vessel wall as two metal segments, one under the liquid and one in the vapour space, each
    /// with its own temperature; the API 521 pool-fire heat on the wetted area; and the lowest fluid
    /// and metal temperatures of a run, which is what a depressurization study reads off the model.
    /// </summary>
    [TestFixture]
    public class DynamicsVesselWallTests
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
            public MaterialStream Feed = null!, GasOut = null!, LiquidOut = null!, Sink = null!;
            public Valve? Bdv;
        }

        // A 1.5 m x 6 m vertical vessel, 10 mm carbon steel wall, half full of n-octane under methane at
        // 20 bar and 320 K. Optionally a blowdown valve on the gas outlet to a 2 bar sink.
        private static Case Build(bool withBdv, double segmentSeconds, bool split, bool fire)
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
            c.Vessel = (Vessel)fs.SimulationObjects[fs.AddObject(ObjectType.Vessel, 0, 0, "V-1").Name];
            c.Vessel.SetFlowsheet(fs);
            c.Vessel.PropertyPackage = pp;

            fs.ConnectObjects(c.Feed.GraphicObject, c.Vessel.GraphicObject, 0, 0);
            fs.ConnectObjects(c.Vessel.GraphicObject, c.GasOut.GraphicObject, 0, 0);
            fs.ConnectObjects(c.Vessel.GraphicObject, c.LiquidOut.GraphicObject, 1, 0);
            if (withBdv)
            {
                c.Sink = Stream("Flare");
                c.Bdv = (Valve)fs.SimulationObjects[fs.AddObject(ObjectType.Valve, 0, 0, "BDV").Name];
                c.Bdv.SetFlowsheet(fs);
                c.Bdv.PropertyPackage = pp;
                fs.ConnectObjects(c.GasOut.GraphicObject, c.Bdv.GraphicObject, 0, 0);
                fs.ConnectObjects(c.Bdv.GraphicObject, c.Sink.GraphicObject, 0, 0);
                c.Bdv.CalcMode = Valve.CalculationMode.Kv_General;
                c.Bdv.Kv = Valve.KvFromOrifice(0.015, 0.62);   // a 15 mm blowdown orifice
                c.Bdv.OpeningPct = 100.0;
                c.Sink.DynamicsSpec = DynamicsSpecType.Pressure;
            }

            c.Feed.SetTemperature(320.0);
            c.Feed.SetPressure(20e5);
            c.Feed.SetMassFlow(1e-4);
            c.Feed.SetOverallComposition(new[] { 0.5, 0.5 });
            c.Feed.DynamicsSpec = DynamicsSpecType.Flow;
            c.GasOut.DynamicsSpec = withBdv ? DynamicsSpecType.Pressure : DynamicsSpecType.Flow;
            c.LiquidOut.DynamicsSpec = DynamicsSpecType.Flow;

            var v = c.Vessel;
            v.SelectedEquipmentType = "Vertical";
            v.HeadType = "Ellipsoidal (2:1)";
            v.CreateDimensionsList();
            v.Dimensions[0].Value = 1500.0;   // mm
            v.Dimensions[1].Value = 6.0;      // m
            v.WallThickness = 0.010;
            v.WallMaterial = "Carbon Steel";
            v.CalculateRigorousHeatBalance = true;
            v.ThermalProperties.TipoPerfil = ThermalEditorDefinitions.ThermalProfileType.Estimar_CGTC;
            v.ThermalProperties.Temp_amb_estimar = 298.15;
            v.CreateDynamicProperties();
            v.SetDynamicProperty("Volume", Math.PI / 4.0 * 1.5 * 1.5 * 6.0);
            v.SetDynamicProperty("Height", 6.0);
            v.SetDynamicProperty("Split Wall (Wetted/Dry)", split);
            v.SetDynamicProperty("Rigorous Energy Balance (UV)", split || fire);
            v.SetDynamicProperty("Fire Case (API 521)", fire);

            var errors = fs.SolveFlowsheet2();
            Assert.That(errors, Is.Empty, "steady state: " + string.Join("; ", errors.Select(e => e.Message)));
            // the steady-state solve rewrites the dimensions from its own sizing; the test geometry goes back in
            v.Dimensions[0].Value = 1500.0;
            v.Dimensions[1].Value = 6.0;
            if (withBdv) c.Sink.SetPressure(2e5);
            c.GasOut.SetMassFlow(withBdv ? c.GasOut.GetMassFlow() : 0.0);
            c.LiquidOut.SetMassFlow(0.0);

            SetContent(c, 0.5);

            var integrator = new DWSIM.DynamicsManager.Integrator
            {
                ID = "wall", Description = "wall",
                IntegrationStep = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromSeconds(segmentSeconds),
                ShouldCalculateEquilibrium = true, ShouldCalculatePressureFlow = true, ShouldCalculateControl = true
            };
            var schedule = new DWSIM.DynamicsManager.Schedule { ID = "wall", Description = "wall", CurrentIntegrator = "wall", UseCurrentStateAsInitial = true };
            fs.DynamicsManager.IntegratorList["wall"] = integrator;
            fs.DynamicsManager.ScheduleList["wall"] = schedule;
            fs.DynamicsManager.CurrentSchedule = "wall";
            return c;
        }

        /// <summary>Half liquid, half gas by volume, from the steady-state vessel outlets.</summary>
        private static void SetContent(Case c, double liquidVolumeFraction)
        {
            var liq = c.LiquidOut; var gas = c.GasOut;
            double vol = Convert.ToDouble(c.Vessel.GetDynamicProperty("Volume"));
            double rl = liq.Phases[0].Properties.density.GetValueOrDefault(), rg = gas.Phases[0].Properties.density.GetValueOrDefault();
            double ml = liquidVolumeFraction * vol * rl, mg = (1 - liquidVolumeFraction) * vol * rg;
            var wl = liq.Phases[0].Compounds.Values.Select(x => x.MassFraction.GetValueOrDefault()).ToArray();
            var wg = gas.Phases[0].Compounds.Values.Select(x => x.MassFraction.GetValueOrDefault()).ToArray();
            var w = wl.Select((x, i) => (ml * x + mg * wg[i]) / (ml + mg)).ToArray();
            var acc = (MaterialStream)c.Feed.Clone();
            acc.SetFlowsheet(c.Flowsheet); acc.SetPropertyPackage(c.Feed.PropertyPackage);
            acc.SetOverallMassComposition(w);
            acc.SetMassFlow(ml + mg);
            acc.SetTemperature(320.0); acc.SetPressure(20e5);
            acc.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Temperature_and_Pressure;
            acc.PropertyPackage.CurrentMaterialStream = acc;
            acc.Calculate();
            c.Vessel.AccumulationStream = acc;
            c.Vessel.WallTemperature = 320.0;
            c.Vessel.WallTemperatureWetted = 320.0;
            c.Vessel.WallTemperatureDry = 320.0;
        }

        private static double Dyn(Vessel v, string name) => Convert.ToDouble(v.GetDynamicProperty(name));

        private static void Run(Case c)
        {
            var r = new IntegratorRunner(c.Flowsheet).Run(new IntegratorRunOptions { Schedule = "wall", RestoreInitialState = false });
            Assert.That(r.Exceptions, Is.Empty, string.Join(Environment.NewLine, r.Exceptions.Select(e => e.ToString())));
        }

        [Test]
        public void TheGeometryHelpersFollowTheVesselShape()
        {
            var c = Build(false, 1.0, false, false);
            var v = c.Vessel;
            const double D = 1.5, DE = 1.51, L = 6.0;

            v.SelectedEquipmentType = "Vertical";
            Assert.That(v.LiquidHeightFromFraction(0.5, D, L), Is.EqualTo(3.0).Within(1e-9));
            var head = v.HeadArea(DE);
            Assert.That(head, Is.EqualTo(1.084 * DE * DE).Within(1e-9), "2:1 ellipsoidal head");
            Assert.That(v.WettedArea(3.0, D, DE, L), Is.EqualTo(Math.PI * DE * 3.0 + head).Within(1e-9));
            Assert.That(v.WettedArea(L, D, DE, L), Is.EqualTo(v.TotalWallArea(DE, L)).Within(1e-9), "full: every surface is wetted");
            Assert.That(v.WettedArea(0.0, D, DE, L), Is.EqualTo(0.0));

            v.SelectedEquipmentType = "Horizontal";
            Assert.That(v.LiquidHeightFromFraction(0.5, D, L), Is.EqualTo(D / 2.0).Within(1e-6), "half full: level at the centre line");
            Assert.That(v.LiquidHeightFromFraction(1.0, D, L), Is.EqualTo(D).Within(1e-6));
            Assert.That(v.WettedArea(D / 2.0, D, DE, L), Is.EqualTo(Math.PI / 2.0 * DE * L + head).Within(1e-6), "half the shell and half of each head");

            Assert.That(Vessel.FireHeatInput(10.0, 1.0, true), Is.EqualTo(43200.0 * Math.Pow(10.0, 0.82)).Within(1e-6));
            Assert.That(Vessel.FireHeatInput(10.0, 0.3, false), Is.EqualTo(70900.0 * 0.3 * Math.Pow(10.0, 0.82)).Within(1e-6));
            Assert.That(Vessel.FireHeatInput(0.0, 1.0, true), Is.EqualTo(0.0));
        }

        // A pool fire on the closed vessel: the API 521 heat goes into the liquid, the pressure and the
        // content temperature rise, the dry metal (given a flux) runs hotter than the fluid, and the
        // run keeps the highest dry-wall temperature.
        [Test]
        public void TheFireCaseHeatsTheContentAndTheDryWall()
        {
            var c = Build(false, 60.0, true, true);
            var v = c.Vessel;
            v.SetDynamicProperty("Fire Dry Wall Heat Flux", 30000.0);

            double p0 = 20e5, t0 = 320.0;
            Run(c);

            double p1 = Dyn(v, "Operating Pressure"), t1 = v.AccumulationStream.GetTemperature();
            double aWet = Dyn(v, "Wetted Area"), qFire = Dyn(v, "Fire Heat Input");
            TestContext.WriteLine($"after 60 s: P {p1 / 1e5:F2} bar, T {t1:F1} K, wetted {aWet:F2} m2, fire {qFire:F0} kW, wall wet {Dyn(v, "Wetted Wall Temperature"):F1} K, dry {Dyn(v, "Dry Wall Temperature"):F1} K");

            Assert.That(aWet, Is.GreaterThan(7.0).And.LessThan(20.0), "m2: half of a 1.5 x 6 m shell plus the bottom head");
            Assert.That(qFire, Is.EqualTo(43200.0 * Math.Pow(aWet, 0.82) / 1000.0).Within(1.0), "kW, API 521 with adequate drainage");
            Assert.That(p1, Is.GreaterThan(p0), "the fire raises the pressure");
            Assert.That(t1, Is.GreaterThan(t0), "and the content temperature");
            Assert.That(Dyn(v, "Wetted Wall Temperature"), Is.EqualTo(t1).Within(0.5), "the wetted metal stays with the liquid (one step behind)");
            Assert.That(Dyn(v, "Dry Wall Temperature"), Is.GreaterThan(t1 + 5.0), "the dry metal runs hotter than the fluid");
            Assert.That(Dyn(v, "Maximum Dry Wall Temperature"), Is.GreaterThanOrEqualTo(Dyn(v, "Dry Wall Temperature")), "the outer face of the dry metal is the hottest point");
        }

        // Blowdown through a 15 mm orifice: the content cools on expansion and the metal lags behind
        // it, so the dry wall is warmer than the gas and the lowest fluid temperature is recorded.
        [Test]
        public void ABlowdownRecordsTheLowestFluidAndWallTemperatures()
        {
            var c = Build(true, 120.0, true, false);
            var v = c.Vessel;
            Run(c);

            double p = Dyn(v, "Operating Pressure"), t = v.AccumulationStream.GetTemperature();
            double tMin = Dyn(v, "Minimum Fluid Temperature"), wallDry = Dyn(v, "Dry Wall Temperature"), wallWet = Dyn(v, "Wetted Wall Temperature");
            TestContext.WriteLine($"after 120 s: P {p / 1e5:F2} bar, T {t:F1} K, min T {tMin:F1} K, wall wet {wallWet:F1} K, dry {wallDry:F1} K, BDV {c.GasOut.GetMassFlow() * 3600:F0} kg/h");

            Assert.That(p, Is.LessThan(15e5), "the vessel blew down");
            Assert.That(t, Is.LessThan(320.0), "expansion cooled the content");
            Assert.That(tMin, Is.LessThanOrEqualTo(t + 1e-9));
            Assert.That(tMin, Is.LessThan(320.0));
            Assert.That(wallDry, Is.GreaterThan(t), "the metal lags the gas");
            Assert.That(Dyn(v, "Minimum Dry Wall Temperature"), Is.LessThanOrEqualTo(wallDry + 1e-9));
            Assert.That(v.WallTemperature, Is.InRange(Math.Min(wallWet, wallDry) - 1e-9, Math.Max(wallWet, wallDry) + 1e-9), "the lumped wall is the area-weighted mean");
            Assert.That(c.GasOut.GetMassFlow(), Is.GreaterThan(0.0), "gas leaves through the orifice");
        }

        // With the split and the UV balance off, the lumped isothermal model of before still runs:
        // the content keeps its temperature and the lumped wall drifts towards the ambient.
        [Test]
        public void TheLumpedIsothermalModelIsStillTheDefault()
        {
            var c = Build(true, 30.0, false, false);
            var v = c.Vessel;
            v.ThermalProperties.TipoPerfil = ThermalEditorDefinitions.ThermalProfileType.Definir_CGTC;
            v.ThermalProperties.Temp_amb_definir = 298.15;
            v.ThermalProperties.CGTC_Definido = 50.0;
            Run(c);
            Assert.That(Dyn(v, "Operating Pressure"), Is.LessThan(20e5));
            Assert.That(v.AccumulationStream.GetTemperature(), Is.EqualTo(320.0).Within(1.0), "legacy: no expansion cooling");
            Assert.That(v.WallTemperature, Is.EqualTo(320.0).Within(1.0), "legacy with a fixed internal coefficient: the wall only exchanges with the content, which did not move");
            Assert.That(Dyn(v, "Wetted Area"), Is.EqualTo(0.0), "the split geometry is not evaluated");
        }
    }
}
