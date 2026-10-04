//    Fluent API builders: ports on creation, builders that set the inputs the calculations read,
//    and the typed builders of the logical blocks, controllers and separators.
//    Copyright 2026 Daniel Wagner Oliveira de Medeiros
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
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;
using FluentFlowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class FluentBuilderTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
        }

        // ------------------------------------------------------------------ ports on creation

        /// <summary>
        /// The clean-energy blocks and the Reaktoro reactor build their ports through the graphic's
        /// owner, which used to be assigned only after the ports were asked for: a headless Add left
        /// them with none. They have ports now, and asking again does not add more.
        /// </summary>
        [Test]
        public void TheCleanEnergyBlocksAndTheReaktoroReactorHavePortsWhenAdded()
        {
            var fs = FluentFlowsheet.Create("PortsProbe").WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var objects = new ISimulationObject[]
            {
                fs.AddWindTurbine("WT").Object,
                fs.AddHydroelectricTurbine("HYT").Object,
                fs.AddSolarPanel("SP").Object,
                fs.AddWaterElectrolyzer("WE").Object,
                fs.AddPEMFuelCell("FC").Object,
                fs.AddReaktoroGibbsReactor("RG").Object,
            };

            foreach (var o in objects)
            {
                var g = o.GraphicObject;
                int ins = g.InputConnectors.Count, outs = g.OutputConnectors.Count;
                TestContext.WriteLine($"{g.Tag}: {ins} inlet(s), {outs} outlet(s)");
                Assert.That(ins + outs, Is.GreaterThan(0), g.Tag + " has no ports");

                ((IExternalUnitOperation)o).CreateConnectors();
                Assert.That(g.InputConnectors.Count, Is.EqualTo(ins), g.Tag + " inlets duplicated");
                Assert.That(g.OutputConnectors.Count, Is.EqualTo(outs), g.Tag + " outlets duplicated");
            }

            Assert.That(objects[0].GraphicObject.OutputConnectors.Count, Is.EqualTo(1), "wind turbine power outlet");
            Assert.That(objects[3].GraphicObject.InputConnectors.Count, Is.EqualTo(2), "electrolyzer water and power inlets");
            Assert.That(objects[3].GraphicObject.OutputConnectors.Count, Is.EqualTo(2), "electrolyzer two gas outlets");
            Assert.That(objects[5].GraphicObject.InputConnectors.Count, Is.GreaterThanOrEqualTo(1), "Reaktoro reactor inlet");
            Assert.That(objects[5].GraphicObject.OutputConnectors.Count, Is.GreaterThanOrEqualTo(2), "Reaktoro reactor outlets");
        }

        // ------------------------------------------------------------------ heat exchanger UA

        /// <summary>
        /// WithGlobalUA gives the exchanger a UA, so the same UA gives the same duty whatever the
        /// area and whichever of the two is set first.
        /// </summary>
        [Test]
        public void TheGlobalUAIsKeptWhateverTheArea()
        {
            var fs = FluentFlowsheet.Create("HXUA").WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            HeatExchangerBuilder Exchanger(string tag)
            {
                var hot = fs.AddMaterialStream(tag + "-hot").At(90.0.Celsius(), 3.0.Bar()).WithMassFlow(1.0.KgPerSecond());
                var cold = fs.AddMaterialStream(tag + "-cold").At(20.0.Celsius(), 3.0.Bar()).WithMassFlow(1.0.KgPerSecond());
                return fs.AddHeatExchanger(tag)
                    .ConnectFeed(hot, 0).ConnectProduct(fs.AddMaterialStream(tag + "-hot-out"), 0)
                    .ConnectFeed(cold, 1).ConnectProduct(fs.AddMaterialStream(tag + "-cold-out"), 1);
            }

            var a = Exchanger("E-A").WithGlobalUA(2000.0);
            var b = Exchanger("E-B").WithGlobalUA(2000.0).WithExchangeArea(25.0);
            var c = Exchanger("E-C").WithExchangeArea(10.0).WithGlobalUA(2000.0);

            Assert.That(b.Object.OverallCoefficient.Value, Is.EqualTo(80.0).Within(1e-9), "U = UA / A");
            Assert.That(c.GlobalUA, Is.EqualTo(2000.0).Within(1e-9));

            fs.Solve();

            double qa = a.Object.Q.GetValueOrDefault(), qb = b.Object.Q.GetValueOrDefault(), qc = c.Object.Q.GetValueOrDefault();
            TestContext.WriteLine($"duty: A {qa:F3} kW, B {qb:F3} kW, C {qc:F3} kW; LMTD {a.Object.LMTD:F3} K");

            Assert.That(qa, Is.GreaterThan(10.0));
            Assert.That(qb, Is.EqualTo(qa).Within(1e-4 * qa));
            Assert.That(qc, Is.EqualTo(qa).Within(1e-4 * qa));
            Assert.That(qa, Is.EqualTo(2000.0 * a.Object.LMTD / 1000.0).Within(0.01 * qa), "Q = UA LMTD");
        }

        // ------------------------------------------------------------------ water electrolyzer

        /// <summary>
        /// WithEfficiencyPercent sets the efficiency the electrolyzer runs at: the share of the power
        /// that splits water, and with it the hydrogen made.
        /// </summary>
        [Test]
        public void TheElectrolyzerRunsAtTheEfficiencyGiven()
        {
            (double efficiency, double hydrogen) Run(double percent)
            {
                var fs = FluentFlowsheet.Create("WE").WithCompounds("Water", "Hydrogen", "Oxygen")
                    .WithPropertyPackage(PropertyPackages.PengRobinson);
                var water = fs.AddMaterialStream("Water").At(60.0.Celsius(), 1.0.Bar()).SetCompoundMolarFlow("Water", 20.0);
                var h2 = fs.AddMaterialStream("H2");
                var o2 = fs.AddMaterialStream("O2");
                var power = fs.AddEnergyStream("Power").WithEnergyFlow(100.0.Kilowatts());
                var el = fs.AddWaterElectrolyzer("EL").WithEfficiencyPercent(percent)
                    .ConnectFeed(water, 0).ConnectEnergyFeed(power, 1)
                    .ConnectProduct(h2, 0).ConnectProduct(o2, 1);
                fs.Solve();
                return (el.Efficiency, h2.Object.Phases[0].Compounds["Hydrogen"].MolarFlow.GetValueOrDefault());
            }

            var at70 = Run(70.0);
            var at50 = Run(50.0);
            TestContext.WriteLine($"70 %: efficiency {at70.efficiency:F4}, H2 {at70.hydrogen:F5} mol/s");
            TestContext.WriteLine($"50 %: efficiency {at50.efficiency:F4}, H2 {at50.hydrogen:F5} mol/s");

            Assert.That(at70.efficiency, Is.EqualTo(0.70).Within(1e-6));
            Assert.That(at50.efficiency, Is.EqualTo(0.50).Within(1e-6));
            Assert.That(at70.hydrogen, Is.GreaterThan(0.0));
            Assert.That(at50.hydrogen / at70.hydrogen, Is.EqualTo(50.0 / 70.0).Within(1e-6));
        }

        // ------------------------------------------------------------------ wind turbine

        /// <summary>
        /// WithAirDensityKgPerM3 replaces the density the turbine calculates; the power is
        /// proportional to it.
        /// </summary>
        [Test]
        public void TheWindTurbineUsesTheAirDensityGiven()
        {
            var fs = FluentFlowsheet.Create("WT").WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var calc = WindTurbineFor(fs, "WT-CALC", null);
            var given = WindTurbineFor(fs, "WT-GIVEN", 1.0);

            fs.Solve();

            TestContext.WriteLine($"calculated density {calc.AirDensityKgPerM3:F4} kg/m3, {calc.GeneratedPowerKW:F2} kW; " +
                                  $"given 1.0 kg/m3, {given.GeneratedPowerKW:F2} kW");

            Assert.That(calc.AirDensityKgPerM3, Is.InRange(1.1, 1.3), "air at 15 C and 1 atm");
            Assert.That(given.AirDensityKgPerM3, Is.EqualTo(1.0));
            Assert.That(given.GeneratedPowerKW / calc.GeneratedPowerKW,
                        Is.EqualTo(1.0 / calc.AirDensityKgPerM3).Within(1e-9));
        }

        private static DWSIM.Automation.FluentAPI.Builders.CleanEnergy.WindTurbineBuilder WindTurbineFor(
            FluentFlowsheet fs, string tag, double? density)
        {
            tag = tag + "-" + fs.Inner.SimulationObjects.Count;
            var power = fs.AddEnergyStream(tag + " power");
            var wt = fs.AddWindTurbine(tag)
                .WithRotorDiameterM(80.0)
                .WithEfficiencyPercent(75.0)
                .WithUserDefinedWeather(12.0, 15.0.Celsius(), 1.0.Atm(), 60.0)
                .ConnectEnergyProduct(power, 0);
            if (density.HasValue) wt.WithAirDensityKgPerM3(density.Value);
            return wt;
        }

        // ------------------------------------------------------------------ biogas upgrader, pretreatment

        /// <summary>WithTechnology loads the technology's removal defaults, as the editor does.</summary>
        [Test]
        public void TheBiogasUpgraderTechnologySetsItsDefaults()
        {
            double CO2Slip(DWSIM.UnitOperations.UnitOperations.BiogasUpgraderTech tech)
            {
                var fs = FluentFlowsheet.Create("BGU").WithCompounds("Methane", "Carbon dioxide", "Water")
                    .WithPropertyPackage(PropertyPackages.PengRobinson);
                var feed = fs.AddMaterialStream("Biogas").At(30.0.Celsius(), 8.0.Bar())
                    .SetCompoundMolarFlow("Methane", 3.7).SetCompoundMolarFlow("Carbon dioxide", 2.3);
                var gas = fs.AddMaterialStream("Biomethane");
                var off = fs.AddMaterialStream("Offgas");
                var up = fs.AddBiogasUpgrader("U-1").WithTechnology(tech)
                    .ConnectFeed(feed, 0).ConnectProduct(gas, 0).ConnectProduct(off, 1);
                TestContext.WriteLine($"{tech}: CO2 removal {up.Object.CO2RemovalEfficiency}, CH4 loss {up.Object.CH4LossFraction}");
                fs.Solve();
                return gas.Object.Phases[0].Compounds["Carbon dioxide"].MassFlow.GetValueOrDefault() /
                       feed.Object.Phases[0].Compounds["Carbon dioxide"].MassFlow.GetValueOrDefault();
            }

            Assert.That(CO2Slip(DWSIM.UnitOperations.UnitOperations.BiogasUpgraderTech.Amine), Is.EqualTo(0.01).Within(1e-6));
            Assert.That(CO2Slip(DWSIM.UnitOperations.UnitOperations.BiogasUpgraderTech.WaterScrubbing), Is.EqualTo(0.08).Within(1e-6));

            var pre = FluentFlowsheet.Create("PRE").AddPretreatmentReactor("PRE-1")
                .WithTechnology(DWSIM.UnitOperations.Reactors.PretreatmentType.Alkaline);
            Assert.That(pre.Object.LigninSolubilization, Is.EqualTo(0.70), "pretreatment technology defaults");
        }

        // ------------------------------------------------------------------ vapor fraction spec

        /// <summary>Water at 1 atm specified by vapor fraction sits at its normal boiling point, for 0 and for 1.</summary>
        [Test]
        public void AVaporFractionSpecGivesTheBoilingPointOfWater()
        {
            var fs = FluentFlowsheet.Create("VF").WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var liquid = fs.AddMaterialStream("Sat liquid").WithPressure(1.0.Atm()).WithVaporFraction(0.0).WithMassFlow(1.0.KgPerSecond());
            var vapor = fs.AddMaterialStream("Sat vapor").WithMassFlow(1.0.KgPerSecond()).WithVaporFraction(1.0).WithPressure(1.0.Atm());

            Assert.That(liquid.Object.SpecType, Is.EqualTo(StreamSpec.Pressure_and_VaporFraction));
            Assert.That(vapor.Object.SpecType, Is.EqualTo(StreamSpec.Pressure_and_VaporFraction));

            fs.Solve();

            TestContext.WriteLine($"VF 0: {liquid.TemperatureK:F3} K; VF 1: {vapor.TemperatureK:F3} K");
            Assert.That(liquid.TemperatureK, Is.EqualTo(373.12).Within(0.1));
            Assert.That(vapor.TemperatureK, Is.EqualTo(373.12).Within(0.1));
            Assert.That(liquid.VaporFraction, Is.EqualTo(0.0).Within(1e-6));
            Assert.That(vapor.VaporFraction, Is.EqualTo(1.0).Within(1e-6));
        }

        /// <summary>
        /// A propane-butane mixture specified by pressure boils below its dew point; specified by
        /// temperature (no pressure given) its bubble pressure is above its dew pressure.
        /// </summary>
        [Test]
        public void AVaporFractionSpecSeparatesBubbleAndDewPoints()
        {
            var fs = FluentFlowsheet.Create("VF2").WithCompounds("Propane", "N-butane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            MaterialStreamBuilder Lpg(string tag) => fs.AddMaterialStream(tag).WithMolarFlow(1.0.MolPerSecond())
                .WithComposition(c => c.Mole("Propane", 0.5).Mole("N-butane", 0.5));

            var bubbleT = Lpg("Bubble T").WithPressure(5.0.Bar()).WithVaporFraction(0.0);
            var dewT = Lpg("Dew T").WithPressure(5.0.Bar()).WithVaporFraction(1.0);
            var bubbleP = Lpg("Bubble P").WithTemperature(300.0.Kelvin()).WithVaporFraction(0.0);
            var dewP = Lpg("Dew P").WithVaporFraction(1.0).WithTemperature(300.0.Kelvin());

            Assert.That(bubbleP.Object.SpecType, Is.EqualTo(StreamSpec.Temperature_and_VaporFraction));
            Assert.That(dewP.Object.SpecType, Is.EqualTo(StreamSpec.Temperature_and_VaporFraction));

            fs.Solve();

            TestContext.WriteLine($"5 bar: bubble {bubbleT.TemperatureK:F2} K, dew {dewT.TemperatureK:F2} K");
            TestContext.WriteLine($"300 K: bubble {bubbleP.PressurePa / 1e5:F3} bar, dew {dewP.PressurePa / 1e5:F3} bar");

            Assert.That(dewT.TemperatureK - bubbleT.TemperatureK, Is.GreaterThan(5.0));
            Assert.That(bubbleP.PressurePa - dewP.PressurePa, Is.GreaterThan(0.5e5));
            Assert.That(bubbleP.TemperatureK, Is.EqualTo(300.0).Within(1e-6));
        }

        // ------------------------------------------------------------------ logical blocks

        /// <summary>The adjust finds the heater duty that brings water to 80 C.</summary>
        [Test]
        public void TheAdjustBuilderFindsTheHeaterDuty()
        {
            var fs = FluentFlowsheet.Create("ADJ").WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);
            var feed = fs.AddMaterialStream("Cold water").At(25.0.Celsius(), 2.0.Bar()).WithMassFlow(2.0.KgPerSecond());
            var product = fs.AddMaterialStream("Hot water");
            var duty = fs.AddEnergyStream("Q-1");
            var heater = fs.AddHeater("H-1").WithHeatAdded(100.0.Kilowatts())
                .ConnectFeed(feed).ConnectProduct(product).ConnectEnergyFeed(duty, 1);

            var adj = fs.AddAdjust("ADJ-1")
                .Manipulates("H-1", "PROP_HT_3")
                .Controls("Hot water", "PROP_MS_0")
                .WithTargetValue(80.0.Celsius())
                .WithTolerance(0.001);

            fs.Solve();

            var h1 = feed.Object.Phases[0].Properties.enthalpy.GetValueOrDefault();
            var h2 = product.Object.Phases[0].Properties.enthalpy.GetValueOrDefault();
            TestContext.WriteLine($"T out {product.TemperatureK - 273.15:F4} C, duty {heater.HeatDutyKW:F3} kW, m.dh {2.0 * (h2 - h1):F3} kW");

            Assert.That(adj.Object.SimultaneousAdjust, Is.True);
            Assert.That(product.TemperatureK, Is.EqualTo(353.15).Within(0.01));
            Assert.That(adj.ControlledValue, Is.EqualTo(353.15).Within(0.01));
            Assert.That(heater.HeatDutyKW, Is.EqualTo(2.0 * (h2 - h1)).Within(0.5));
        }

        /// <summary>The spec sets the cooler outlet 10 K above the cooling water supply.</summary>
        [Test]
        public void TheSpecBuilderWritesTheTarget()
        {
            var fs = FluentFlowsheet.Create("SPEC").WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);
            var cw = fs.AddMaterialStream("CW supply").At(28.0.Celsius(), 3.0.Bar()).WithMassFlow(20.0.KgPerSecond());
            var hot = fs.AddMaterialStream("Hot water").At(90.0.Celsius(), 2.0.Bar()).WithMassFlow(3.0.KgPerSecond());
            var cooled = fs.AddMaterialStream("Cooled water");
            var duty = fs.AddEnergyStream("C-1 duty");
            fs.AddCooler("C-1").WithOutletTemperature(50.0.Celsius())
                .ConnectFeed(hot).ConnectProduct(cooled).ConnectEnergyProduct(duty);

            var spec = fs.AddSpec("SPEC-1")
                .WithSource("CW supply", "PROP_MS_0")
                .WithTarget("C-1", "PROP_CL_2")
                .WithExpression("X + 10")
                .WithFlowsheetCalculationMode(SpecCalcMode.BeforeTargetObject);

            fs.Solve();

            TestContext.WriteLine($"CW {cw.TemperatureK:F3} K, cooled {cooled.TemperatureK:F3} K");
            Assert.That(cw.Object.IsSpecAttached && cw.Object.SpecVarType == SpecVarType.Source, Is.True);
            Assert.That(cooled.TemperatureK, Is.EqualTo(cw.TemperatureK + 10.0).Within(0.01));
            Assert.That(spec.Object.Expression, Is.EqualTo("X + 10"));
        }

        /// <summary>The information carrier copies one heater's outlet temperature to two others.</summary>
        [Test]
        public void TheInformationCarrierBuilderCopiesTheValue()
        {
            var fs = FluentFlowsheet.Create("IC").WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);
            var outs = new MaterialStreamBuilder[3];
            var temperatures = new[] { 75.0, 40.0, 40.0 };
            for (int i = 0; i < 3; i++)
            {
                var feed = fs.AddMaterialStream("Feed-" + (i + 1)).At(20.0.Celsius(), 2.0.Bar()).WithMassFlow(1.0.KgPerSecond());
                outs[i] = fs.AddMaterialStream("Out-" + (i + 1));
                fs.AddHeater("H-" + (i + 1)).WithOutletTemperature(temperatures[i].Celsius())
                    .ConnectFeed(feed).ConnectProduct(outs[i]);
            }

            fs.AddInformationCarrier("IC-1")
                .WithSource("H-1", "PROP_HT_2")
                .WithTarget("H-2", "PROP_HT_2")
                .WithTarget("H-3", "PROP_HT_2");

            fs.Solve();

            TestContext.WriteLine(string.Join(", ", outs.Select(o => (o.TemperatureK - 273.15).ToString("F3") + " C")));
            Assert.That(outs[0].TemperatureK, Is.EqualTo(348.15).Within(0.01));
            Assert.That(outs[1].TemperatureK, Is.EqualTo(outs[0].TemperatureK).Within(0.01));
            Assert.That(outs[2].TemperatureK, Is.EqualTo(outs[0].TemperatureK).Within(0.01));
        }

        // ------------------------------------------------------------------ controllers

        /// <summary>The level loop the controller tests run on: feed valve, tank, outlet valve.</summary>
        private static (FluentFlowsheet fs, ValveBuilder outletValve) LevelLoop(string name)
        {
            var fs = FluentFlowsheet.Create(name).WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);
            var inlet = fs.AddMaterialStream("Inlet").At(25.0.Celsius(), 130000.0.Pascal())
                .WithMassFlow(10.0.KgPerSecond()).AsFlowSpec();
            var s1 = fs.AddMaterialStream("S-1").AsPressureSpec();
            var s2 = fs.AddMaterialStream("S-2").AsPressureSpec();
            var outlet = fs.AddMaterialStream("Outlet").AsPressureSpec();
            fs.AddValve("V-01").WithCalcMode(Valve.CalculationMode.Kv_Liquid).WithKv(100.0)
                .WithOpeningPercent(50.0).ConnectFeed(inlet).ConnectProduct(s1);
            fs.AddTank("T-01").WithVolume(2.0.CubicMeters()).WithHeight(2.0.Meters())
                .ConnectFeed(s1).ConnectProduct(s2);
            var valve = fs.AddValve("V-02").WithCalcMode(Valve.CalculationMode.Kv_Liquid).WithKv(400.0)
                .WithOpeningKvRelationship().WithOpeningPercent(29.0)
                .ConnectFeed(s2).ConnectProduct(outlet);
            fs.Solve();
            outlet.Object.Phases[0].Properties.pressure = 101325.0;

            fs.Dynamics.DefineIntegrator("Int1")
                .WithIntegrationStep(5.0.Seconds()).WithDuration(60.0.Seconds())
                .Monitor("T-01", "Liquid Level", "m", "level")
                .Monitor("V-02", "PROP_VA_5", "", "opening");
            fs.Dynamics.DefineSchedule("Run").WithIntegrator("Int1").UseCurrentStateAsInitial(true).MakeCurrent();
            return (fs, valve);
        }

        /// <summary>The Python controller reads the level and writes the valve opening its script computes.</summary>
        [Test]
        public void ThePythonControllerBuilderWiresTheScript()
        {
            var (fs, valve) = LevelLoop("PYC");

            var lc = fs.AddPythonController("LC-PY")
                .Controls("T-01", "Liquid Level", "m")
                .Manipulates("V-02", "PROP_VA_5", "")
                .WithInitialOutput(29.0)
                .WithScript("SP = 1.0\nMV = 37.5");

            var result = fs.RunDynamics("Run").Execute();
            Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors.Select(e => e.Message)));

            var opening = result.GetSeries("opening");
            TestContext.WriteLine($"{result.Steps} steps; PV {lc.ProcessVariable:F4} m; opening {opening.Initial:F2} -> {opening.Final:F2} %");

            Assert.That(lc.ProcessVariable, Is.GreaterThan(0.0), "the controller read the level");
            Assert.That(lc.SetPoint, Is.EqualTo(1.0));
            Assert.That(lc.Output, Is.EqualTo(37.5));
            Assert.That(valve.Object.OpeningPct, Is.EqualTo(37.5).Within(1e-9), "the controller wrote the opening");
        }

        /// <summary>The MPC controller runs on the level loop and keeps the opening inside its limits.</summary>
        [Test]
        public void TheMPCControllerBuilderRunsTheController()
        {
            var (fs, valve) = LevelLoop("MPC");

            var mpc = fs.AddMPCController("LC-MPC")
                .Controls("T-01", "Liquid Level", "m", 0.9, 1.1)
                .Manipulates("V-02", "PROP_VA_5", "", 0.0, 100.0)
                .WithIntegratingModel(0, 0, -0.0004)
                .WithSampleTime(5.0.Seconds())
                .WithHorizons(30, 5)
                .WithMoveSuppression(0.1);

            var result = fs.RunDynamics("Run").Execute();
            Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors.Select(e => e.Message)));

            var level = result.GetSeries("level");
            var opening = result.GetSeries("opening");
            TestContext.WriteLine($"{result.Steps} steps; level {level.Initial:F4} -> {level.Final:F4} m; opening {opening.Initial:F2} -> {opening.Final:F2} %");

            Assert.That(mpc.Object.CVHistory.Count, Is.GreaterThan(0), "the controller ran");
            Assert.That(mpc.ManipulatedValue(0), Is.InRange(0.0, 100.0));
            Assert.That(mpc.ControlledValue(0), Is.EqualTo(level.Final).Within(1e-6));
            Assert.That(opening.Values.Any(v => Math.Abs(v - 29.0) > 1e-6), Is.True, "the controller moved the valve");
        }

        /// <summary>
        /// The MPC builder adds a measured disturbance with its models, and the controller records the
        /// disturbance at each control step.
        /// </summary>
        [Test]
        public void TheMPCControllerBuilderAddsDisturbanceModels()
        {
            var (fs, _) = LevelLoop("MPCDV");

            var mpc = fs.AddMPCController("LC-MPC")
                .Controls("T-01", "Liquid Level", "m", 0.9, 1.1)
                .Manipulates("V-02", "PROP_VA_5", "", 0.0, 100.0)
                .Measures("Inlet", "PROP_MS_2", "kg/s")
                .WithIntegratingModel(0, 0, -0.0004)
                .WithIntegratingDisturbanceModel(0, 0, 0.001, 10.0.Seconds())
                .WithFirstOrderDisturbanceModel(0, 0, 0.0005, 60.0.Seconds(), 5.0.Seconds())
                .WithSampleTime(5.0.Seconds())
                .WithHorizons(30, 5);

            var models = mpc.Object.DisturbanceModels;
            Assert.That(models.Count, Is.EqualTo(2));
            Assert.That(models[0].DVIndex, Is.EqualTo(0));
            Assert.That(models[0].Integrating, Is.True);
            Assert.That(models[0].Gain, Is.EqualTo(0.001));
            Assert.That(models[0].TimeConstant, Is.EqualTo(10.0));
            Assert.That(models[1].Integrating, Is.False);
            Assert.That(models[1].TimeConstant, Is.EqualTo(60.0));
            Assert.That(models[1].DeadTime, Is.EqualTo(5.0));
            Assert.That(mpc.Object.StepResponseModels.Count, Is.EqualTo(1), "disturbance models stay out of the step response models");
            Assert.That(mpc.DisturbanceValue(0), Is.EqualTo(10.0).Within(1e-6));
            Assert.That(mpc.Object.GetPropertyValue("Use Measured Disturbances"), Is.EqualTo(true));
            Assert.That(mpc.Object.GetChartModelNames(), Does.Contain("DV Trends"));

            var result = fs.RunDynamics("Run").Execute();
            Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors.Select(e => e.Message)));

            Assert.That(mpc.Object.DVHistory.Count, Is.EqualTo(mpc.Object.CVHistory.Count));
            Assert.That(mpc.Object.DVHistory.Last()[0], Is.EqualTo(10.0).Within(1e-6));
            Assert.That(mpc.ManipulatedValue(0), Is.InRange(0.0, 100.0));
        }

        // ------------------------------------------------------------------ separators, column, tank

        /// <summary>The component separator sends the specified shares to the specified outlet.</summary>
        [Test]
        public void TheComponentSeparatorBuilderSplitsByTheSpecs()
        {
            var fs = FluentFlowsheet.Create("CS").WithCompounds("Methane", "Ethane", "Carbon dioxide")
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            var feed = fs.AddMaterialStream("Sour gas").At(35.0.Celsius(), 50.0.Bar())
                .SetCompoundMolarFlow("Methane", 85.0).SetCompoundMolarFlow("Ethane", 7.0).SetCompoundMolarFlow("Carbon dioxide", 8.0);
            var sweet = fs.AddMaterialStream("Sweet gas");
            var co2 = fs.AddMaterialStream("CO2 rich");
            var heat = fs.AddEnergyStream("X-1 heat");

            fs.AddComponentSeparator("X-1")
                .ConnectFeed(feed).ConnectProduct(sweet, 0).ConnectProduct(co2, 1).ConnectEnergyProduct(heat, 0)
                .WithSpecifiedOutlet(0)
                .WithMolarPercent("Methane", 99.0)
                .WithMolarPercent("Ethane", 98.0)
                .WithMolarFlow("Carbon dioxide", 0.16.MolPerSecond());

            fs.Solve();

            double N(MaterialStreamBuilder s, string c) => s.Object.Phases[0].Compounds[c].MolarFlow.GetValueOrDefault();
            TestContext.WriteLine($"sweet: CH4 {N(sweet, "Methane"):F3}, CO2 {N(sweet, "Carbon dioxide"):F4} mol/s; rich: CO2 {N(co2, "Carbon dioxide"):F4} mol/s");

            Assert.That(N(sweet, "Methane"), Is.EqualTo(84.15).Within(1e-6));
            Assert.That(N(sweet, "Ethane"), Is.EqualTo(6.86).Within(1e-6));
            Assert.That(N(sweet, "Carbon dioxide"), Is.EqualTo(0.16).Within(1e-6));
            Assert.That(N(co2, "Carbon dioxide"), Is.EqualTo(7.84).Within(1e-6));
            Assert.That(sweet.MassFlowKgPerSecond + co2.MassFlowKgPerSecond, Is.EqualTo(feed.MassFlowKgPerSecond).Within(1e-6));
        }

        /// <summary>
        /// Forced solids put benzoic acid in the solid phase, which the solids separator splits by its
        /// two efficiencies.
        /// </summary>
        [Test]
        public void TheSolidsSeparatorBuilderSplitsForcedSolids()
        {
            var fs = FluentFlowsheet.Create("SS").WithCompounds("Water", "Benzoic acid")
                .WithPropertyPackage(PropertyPackages.Raoult, pp => pp.WithForcedSolids("Benzoic acid", "Benzoic acid"));
            var pp0 = (DWSIM.Thermodynamics.PropertyPackages.PropertyPackage)fs.Inner.PropertyPackages.Values.First();
            Assert.That(pp0.ForcedSolids, Is.EquivalentTo(new[] { "Benzoic acid" }));

            var slurry = fs.AddMaterialStream("Slurry").At(25.0.Celsius(), 1.5.Bar())
                .SetCompoundMassFlow("Water", 5.0).SetCompoundMassFlow("Benzoic acid", 0.5);
            var liquor = fs.AddMaterialStream("Mother liquor");
            var solids = fs.AddMaterialStream("Wet solids");
            fs.AddSolidsSeparator("SS-1")
                .ConnectFeed(slurry).ConnectProduct(liquor, 0).ConnectProduct(solids, 1)
                .WithSolidsSeparationEfficiency(98.0)
                .WithLiquidSeparationEfficiency(95.0);

            fs.Solve();

            double W(MaterialStreamBuilder s, string c) => s.Object.Phases[0].Compounds[c].MassFlow.GetValueOrDefault();
            TestContext.WriteLine($"solids: acid {W(solids, "Benzoic acid"):F4}, water {W(solids, "Water"):F4} kg/s");

            Assert.That(W(solids, "Benzoic acid"), Is.EqualTo(0.49).Within(1e-6));
            Assert.That(W(solids, "Water"), Is.EqualTo(0.25).Within(1e-6));
            Assert.That(W(liquor, "Water"), Is.EqualTo(4.75).Within(1e-6));
        }

        /// <summary>
        /// The filter computes the pressure drop for an area in Simulation mode, and the same pressure
        /// drop gives back the area in Design mode.
        /// </summary>
        [Test]
        public void TheFilterBuilderSimulatesAndDesigns()
        {
            double Run(Action<FilterBuilder> spec, out FilterBuilder filter, out double balance)
            {
                var fs = FluentFlowsheet.Create("F").WithCompounds("Water", "Benzoic acid")
                    .WithPropertyPackage(PropertyPackages.Raoult, pp => pp.WithForcedSolids("Benzoic acid"));
                var slurry = fs.AddMaterialStream("Slurry").At(25.0.Celsius(), 1.0.Bar())
                    .SetCompoundMassFlow("Water", 5.0).SetCompoundMassFlow("Benzoic acid", 0.5);
                var filtrate = fs.AddMaterialStream("Filtrate");
                var cake = fs.AddMaterialStream("Cake");
                filter = fs.AddFilter("F-1")
                    .ConnectFeed(slurry).ConnectProduct(filtrate, 0).ConnectProduct(cake, 1)
                    .WithSubmergedAreaFraction(0.35)
                    .WithCycleTime(90.0.Seconds())
                    .WithSpecificCakeResistance(3.0e9)
                    .WithMediumResistance(1.0e9)
                    .WithCakeMoisturePercent(15.0);
                spec(filter);
                fs.Solve();
                balance = slurry.MassFlowKgPerSecond - filtrate.MassFlowKgPerSecond - cake.MassFlowKgPerSecond;
                return filter.Object.CalcMode == Filter.CalculationMode.Simulation ? filter.PressureDropPa : filter.FilterAreaM2;
            }

            var dp = Run(f => f.WithFilterArea(8.0), out var sim, out var b1);
            var area = Run(f => f.WithPressureDrop(dp.Pascal()), out var design, out var b2);
            TestContext.WriteLine($"8 m2 -> {dp:F1} Pa -> {area:F4} m2");

            Assert.That(sim.Object.CalcMode, Is.EqualTo(Filter.CalculationMode.Simulation));
            Assert.That(design.Object.CalcMode, Is.EqualTo(Filter.CalculationMode.Design));
            Assert.That(dp, Is.GreaterThan(0.0));
            Assert.That(area, Is.EqualTo(8.0).Within(1e-3));
            Assert.That(Math.Abs(b1) + Math.Abs(b2), Is.LessThan(1e-6), "mass balance");
        }

        /// <summary>The shortcut column separates benzene from toluene with the keys and reflux given.</summary>
        [Test]
        public void TheShortcutColumnBuilderSetsKeysAndSpecs()
        {
            var fs = FluentFlowsheet.Create("SC").WithCompounds("Benzene", "Toluene")
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            var feed = fs.AddMaterialStream("Feed").At(90.0.Celsius(), 1.0.Atm())
                .SetCompoundMolarFlow("Benzene", 50.0).SetCompoundMolarFlow("Toluene", 50.0);
            var distillate = fs.AddMaterialStream("Distillate");
            var bottoms = fs.AddMaterialStream("Bottoms");
            var qc = fs.AddEnergyStream("Q-cond");
            var qb = fs.AddEnergyStream("Q-reb");

            var col = fs.AddShortcutColumn("T-1")
                .ConnectFeed(feed, 0).ConnectProduct(distillate, 0).ConnectProduct(bottoms, 1)
                .ConnectEnergyProduct(qc).ConnectEnergyFeed(qb, 1)
                .WithKeys("Benzene", "Toluene")
                .WithLightKeyInBottoms(0.01)
                .WithHeavyKeyInDistillate(0.01)
                .WithRefluxRatio(2.0)
                .WithPressures(1.0.Atm(), 1.0.Atm());

            fs.Solve();

            TestContext.WriteLine($"Rmin {col.MinimumRefluxRatio:F3}, Nmin {col.MinimumStages:F2}, N {col.TheoreticalStages:F2}, feed stage {col.OptimumFeedStage:F1}");
            TestContext.WriteLine($"distillate benzene {distillate.OverallMoleFraction("Benzene"):F4}; Qc {col.CondenserDutyKW:F1} kW, Qb {col.ReboilerDutyKW:F1} kW");

            Assert.That(col.MinimumRefluxRatio, Is.InRange(0.5, 2.0));
            Assert.That(col.MinimumStages, Is.GreaterThan(5.0));
            Assert.That(col.TheoreticalStages, Is.GreaterThan(col.MinimumStages));
            Assert.That(distillate.OverallMoleFraction("Benzene"), Is.EqualTo(0.99).Within(1e-3));
            Assert.That(bottoms.OverallMoleFraction("Benzene"), Is.EqualTo(0.01).Within(1e-3));
        }

        /// <summary>The tank takes the pressure drop given.</summary>
        [Test]
        public void TheTankBuilderSetsThePressureDrop()
        {
            var fs = FluentFlowsheet.Create("TK").WithCompound("N-decane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            var feed = fs.AddMaterialStream("In").At(30.0.Celsius(), 2.0.Bar()).WithMassFlow(2.0.KgPerSecond());
            var product = fs.AddMaterialStream("Out");
            var tank = fs.AddTank("TK-1").WithVolume(50.0.CubicMeters()).WithPressureDrop(0.2.Bar())
                .ConnectFeed(feed).ConnectProduct(product);

            fs.Solve();

            var q = feed.Object.Phases[0].Properties.volumetric_flow.GetValueOrDefault();
            TestContext.WriteLine($"outlet {product.PressurePa / 1e5:F4} bar; residence {tank.ResidenceTimeSeconds / 3600.0:F3} h");
            Assert.That(product.PressurePa, Is.EqualTo(1.8e5).Within(1.0));
            Assert.That(tank.ResidenceTimeSeconds, Is.EqualTo(50.0 / q).Within(1e-3 * 50.0 / q));
        }
    }
}
