using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Automation.FluentAPI.Dynamics;
using DWSIM.UnitOperations.Reactors;
using Flowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Solves an adiabatic CSTR with an exothermic kinetic reaction to its steady state and runs it in
    /// dynamics for five minutes with nothing changed. The reactor has to stay where the steady state put it.
    /// </summary>
    /// <remarks>
    /// Methanol carbonylation with the kinetics of the "Dynamic Simulation - CSTR" sample. The dynamic model
    /// used to add the reaction extent in mol to a holdup inventory kept in kmol, so the composition moved a
    /// thousand times too fast, and it applied the reaction heat once per step whatever the step length. From
    /// its steady state the sample ignited to 547 K, sooner the shorter the step.
    ///
    /// Two runs, at 1 s and 0.1 s steps: the answer must not depend on the step. After the runs the flowsheet
    /// is solved again in steady state, and the energy stream of the adiabatic reactor has to read zero (it used
    /// to carry whatever the last dynamic run had left in the reactor's heat duty).
    ///
    /// <see cref="RunJacket"/> does the same with a cooling jacket (Heat Exchange mode), whose duty the dynamic
    /// model used to count twice: once from the jacket and once more from the energy stream that reports it.
    /// <see cref="RunDrain"/> cuts the feed and lets the product draw the reactor empty: the empty reactor has to
    /// sit at its minimum pressure with its temperature where it was, where it used to heat without bound.
    /// </remarks>
    internal static class CstrDynamicsTest
    {
        private const double DurationSeconds = 300.0;

        public static void Run()
        {
            var fs = Build();

            var reactor = Reactor(fs);
            var product = fs.MaterialStream("product").Object;
            var feed = fs.MaterialStream("feed").Object;

            var steadyT = product.GetTemperature();
            var steadyConversion = reactor.ComponentConversions["Methanol"];
            var steadyHeat = -reactor.DHRi.Values.Sum();

            Console.WriteLine("steady state: feed " + feed.GetTemperature().ToString("F2") + " K, reactor " +
                steadyT.ToString("F2") + " K, methanol conversion " + (steadyConversion * 100).ToString("F3") +
                " %, heat of reaction " + steadyHeat.ToString("F1") + " kW");

            if (steadyT - feed.GetTemperature() < 1.0 || steadyHeat < 10.0)
                throw new Exception("The steady state is meant to carry a reaction heat that warms the reactor above its feed.");

            fs.Dynamics.StoreCurrentStateAs("Steady state");

            var table = new ResultTable("Adiabatic CSTR left alone from its steady state")
                .Row("energy stream of the adiabatic reactor after the steady solve", 0.0, fs.EnergyStream("e1").Object.EnergyFlow.GetValueOrDefault(), 1e-9, "kW");

            foreach (var step in new[] { 1.0, 0.1 })
            {
                var result = Integrate(fs, step);
                var temperature = result.GetSeries("reactor temperature");
                var conversion = result.GetSeries("methanol conversion");

                foreach (var t in new[] { 0.0, 10.0, 30.0, 60.0, 120.0, 180.0, 240.0, DurationSeconds })
                    Console.WriteLine("step " + step.ToString("F1") + " s, t = " + t.ToString("F0").PadLeft(3) + " s: " +
                        temperature.ValueAt(t).ToString("F3") + " K, methanol conversion " +
                        conversion.ValueAt(t).ToString("F3") + " %");

                var label = "step " + step.ToString("F1") + " s: ";
                table.RowInRange(label + "highest reactor temperature", steadyT - 1.0, steadyT + 1.0, temperature.Max, "K")
                     .RowInRange(label + "lowest reactor temperature", steadyT - 1.0, steadyT + 1.0, temperature.Min, "K")
                     .Row(label + "methanol conversion at 5 min", steadyConversion * 100, conversion.Final, 0.1, "%");
            }

            fs.Inner.LoadProcessData(fs.Inner.StoredSolutions["Steady state"]);
            fs.Solve();

            table.Row("energy stream of the adiabatic reactor after a dynamic run and a steady solve", 0.0, fs.EnergyStream("e1").Object.EnergyFlow.GetValueOrDefault(), 1e-9, "kW")
                 .PrintAndThrowIfFailed();
        }

        public static void RunJacket()
        {
            var fs = Build();

            var reactor = Reactor(fs);
            reactor.ReactorOperationMode = OperationMode.HeatExchange;
            reactor.Diameter = 2.0;                        // jacket area 4 V / D = 20 m2
            reactor.OverallHeatTransferCoefficient = 500.0; // W/m2.K
            reactor.CoolantInletTemperature = 355.0;
            fs.Solve();

            var steadyT = reactor.OutletTemperature;
            var steadyDuty = fs.EnergyStream("e1").Object.EnergyFlow.GetValueOrDefault();

            Console.WriteLine("steady state with the jacket: reactor " + steadyT.ToString("F2") + " K, jacket duty " +
                steadyDuty.ToString("F1") + " kW");

            if (steadyDuty > -10.0)
                throw new Exception("The jacket is meant to take heat out of the reactor in this case.");

            fs.Dynamics.StoreCurrentStateAs("Steady state");

            var result = Integrate(fs, 1.0, extra: i => i.Monitor("e1", "PROP_ES_0", "kW", "jacket duty"));
            var temperature = result.GetSeries("reactor temperature");
            var jacketDuty = result.GetSeries("jacket duty");

            new ResultTable("Jacketed CSTR left alone from its steady state")
                .RowInRange("highest reactor temperature", steadyT - 1.0, steadyT + 1.0, temperature.Max, "K")
                .RowInRange("lowest reactor temperature", steadyT - 1.0, steadyT + 1.0, temperature.Min, "K")
                .Row("jacket duty at 5 min", steadyDuty, jacketDuty.Final, 0.02, "kW")
                .PrintAndThrowIfFailed();
        }

        public static void RunDrain()
        {
            const double cutAtSeconds = 10.0;
            const double drainSeconds = 150.0;

            var fs = Build();
            var steadyT = fs.MaterialStream("product").Object.GetTemperature();
            fs.Dynamics.StoreCurrentStateAs("Steady state");

            fs.Dynamics.DefineEventSet("Feed cut")
                .AddStepChange("feed", "PROP_MS_2", 0.0, at: cutAtSeconds.Seconds(), description: "feed cut");

            var result = Integrate(fs, 1.0, drainSeconds, "Feed cut",
                i => i.Monitor("R-01", "Operating Pressure", "Pa", "reactor pressure")
                      .Monitor("R-01", "Liquid Level", "m", "liquid level"));

            var temperature = result.GetSeries("reactor temperature");
            var pressure = result.GetSeries("reactor pressure");
            var level = result.GetSeries("liquid level");
            var holdup = Reactor(fs).AccumulationStream.GetMassFlow();

            foreach (var t in new[] { 0.0, 20.0, 40.0, 60.0, 80.0, 100.0, drainSeconds })
                Console.WriteLine("t = " + t.ToString("F0").PadLeft(3) + " s: " + temperature.ValueAt(t).ToString("F2") + " K, " +
                    (pressure.ValueAt(t) / 1e5).ToString("F3") + " bar, liquid level " + level.ValueAt(t).ToString("F4") + " m");

            new ResultTable("CSTR drawn empty after its feed is cut")
                .RowInRange("highest reactor temperature", steadyT - 5.0, steadyT + 5.0, temperature.Max, "K")
                .RowInRange("reactor temperature at the end", steadyT - 5.0, steadyT + 5.0, temperature.Final, "K")
                .Row("reactor pressure at the end (the minimum pressure)", 101325.0, pressure.Final, 1e-6, "Pa")
                .Row("liquid level at the end", 0.0, level.Final, 1e-9, "m")
                .Row("mass held at the end", 0.0, double.IsNaN(holdup) ? 1.0 : holdup, 1e-9, "kg")
                .PrintAndThrowIfFailed();
        }

        private static Reactor_CSTR Reactor(Flowsheet fs) =>
            (Reactor_CSTR)fs.Inner.SimulationObjects.Values.First(o => o.GraphicObject.Tag == "R-01");

        private static Flowsheet Build()
        {
            var fs = Flowsheet.Create("FluentDynamicsCstr")
                .WithCompound("Methanol")
                .WithCompound("Carbon monoxide")
                .WithCompound("Acetic acid")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            // CH3OH + CO -> CH3COOH, first order in methanol and half order in CO (mol/m3), k = 3.5e6 exp(-83680/RT)
            var reaction = fs.DefineKineticReaction("carbonylation",
                new Dictionary<string, double> { { "Methanol", -1.0 }, { "Carbon monoxide", -1.0 }, { "Acetic acid", 1.0 } },
                new Dictionary<string, double> { { "Methanol", 1.0 }, { "Carbon monoxide", 0.5 }, { "Acetic acid", 0.0 } },
                new Dictionary<string, double> { { "Methanol", 0.0 }, { "Carbon monoxide", 0.0 }, { "Acetic acid", 0.0 } },
                "Methanol", "Mixture", "Molar Concentration", "mol/m3", "mol/[m3.s]", 3.5e6, 83680.0);

            fs.ReactionSet("carbonylation set").Add(reaction);

            var feed = fs.AddMaterialStream("feed")
                .At(360.Kelvin(), 35.Bar())
                .WithMassFlow(10.0.KgPerSecond())
                .WithComposition(c => c.Mole("Methanol", 0.5).Mole("Carbon monoxide", 0.5))
                .AsFlowSpec();

            var product = fs.AddMaterialStream("product").At(360.Kelvin(), 35.Bar()).AsFlowSpec();
            var duty = fs.AddEnergyStream("e1");

            fs.AddCSTR("R-01")
                .WithVolume(10.0.CubicMeters())
                .WithReactionSet("carbonylation set")
                .WithPressureDrop(0.0.Pascal())
                .Adiabatic()
                .ConnectFeed(feed, 0)
                .ConnectProduct(product, 0)
                .ConnectEnergyFeed(duty, 1);

            fs.AutoLayout();
            fs.Solve();

            return fs;
        }

        private static DynamicsResult Integrate(Flowsheet fs, double stepSeconds, double durationSeconds = DurationSeconds,
            string eventSet = null, Func<IntegratorBuilder, IntegratorBuilder> extra = null)
        {
            var name = (eventSet ?? "Left alone") + ", " + stepSeconds.ToString("F1") + " s";

            fs.Inner.LoadProcessData(fs.Inner.StoredSolutions["Steady state"]);

            var reactor = Reactor(fs);
            reactor.SetDynamicProperty("Reset Contents", 1);
            reactor.SetDynamicProperty("Initialize using Inlet Stream", 0);

            var integrator = fs.Dynamics.DefineIntegrator(name)
                .WithIntegrationStep(stepSeconds.Seconds())
                .WithDuration(durationSeconds.Seconds())
                .Monitor("R-01", "PROP_CS_5", "K", "reactor temperature")
                .Monitor("R-01", "Methanol: Conversion", "%", "methanol conversion");

            extra?.Invoke(integrator);

            var schedule = fs.Dynamics.DefineSchedule(name).WithIntegrator(name);
            if (eventSet != null) schedule.WithEventSet(eventSet);
            schedule.MakeCurrent();

            var result = fs.RunDynamics(name).Execute();

            if (!result.Completed)
                throw new Exception("Integration did not complete: " +
                    (result.Error == null ? "aborted" : result.Error.Message));

            return result;
        }
    }
}
