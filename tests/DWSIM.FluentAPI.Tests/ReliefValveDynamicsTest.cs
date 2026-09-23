using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;
using DWSIM.UnitOperations.UnitOperations;
using Flowsheet = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Blocks the gas outlet of a fed separator and checks what its relief valve passes.
    /// </summary>
    /// <remarks>
    /// The relief valve works out its lift as a fraction of the band between the set pressure and
    /// the fully opened pressure, and the opening/Kv relationships it shares with the control valve
    /// take the opening in percent. The two used to be mixed, so a fully open relief valve passed
    /// about one percent of its orifice capacity and the vessel ran away past the fully opened
    /// pressure.
    ///
    /// Two runs of the same flowsheet. In the first the orifice can take the whole feed with room to
    /// spare, so once the valve lifts the flare flow has to settle at the feed flow and the vessel
    /// pressure has to stay inside the lift band. In the second the feed exceeds what the orifice
    /// passes at the fully opened pressure, so the valve ends up fully open and its flow has to be
    /// the API 520 critical flow computed from the state of the stream feeding it.
    /// </remarks>
    internal static class ReliefValveDynamicsTest
    {
        private const double SetPointPa = 8.0e5;
        private const double FullyOpenedPa = 8.8e5;
        private const double DischargeCoefficient = 0.975;
        private const double ShutGasValveAtSeconds = 10.0;
        private const double DurationSeconds = 400.0;

        private const double OrificeD = 0.71e-4;
        private const double OrificeE = 1.26e-4;

        public static void Run()
        {
            RunHeldInsideTheLiftBand();
            RunFullyOpen();
        }

        /// <summary>Orifice E at 8.8 bar passes about 0.19 kg/s of this gas: more than the feed.</summary>
        private static void RunHeldInsideTheLiftBand()
        {
            const double feedKgPerSecond = 0.1;

            var c = Build("FluentDynamicsReliefValve", feedKgPerSecond, OrificeE);
            var result = Integrate(c);

            var pressure = result.GetSeries("vessel pressure");
            var flareFlow = result.GetSeries("flare mass flow");
            var gasFlow = result.GetSeries("gas product mass flow");

            PrintSamples(pressure, flareFlow, gasFlow);

            if (flareFlow.ValueAt(ShutGasValveAtSeconds - 1.0) > 1e-9)
                throw new Exception("The relief valve was passing " + flareFlow.ValueAt(ShutGasValveAtSeconds - 1.0) +
                    " kg/s before the outlet was blocked, with the vessel below the set pressure.");

            if (pressure.Max > FullyOpenedPa * 1.01)
                throw new Exception("The vessel pressure reached " + (pressure.Max / 1e5).ToString("F3") +
                    " bar, above the fully opened pressure of " + (FullyOpenedPa / 1e5).ToString("F2") +
                    " bar. The relief valve is not passing what its orifice can.");

            new ResultTable("Relief valve holding a blocked vessel")
                .Row("gas product flow before the valve is shut", feedKgPerSecond, gasFlow.ValueAt(ShutGasValveAtSeconds - 1.0), 0.02, "kg/s")
                .Row("gas product flow at the end of the run", 0.0, gasFlow.Final, 1e-6, "kg/s")
                .Row("flare flow at the end of the run", feedKgPerSecond, flareFlow.Final, 0.003, "kg/s")
                .Row("vessel pressure at the end, above the set pressure", 1.0, pressure.Final > SetPointPa ? 1.0 : 0.0, 0.0)
                .Row("vessel pressure at the end, below the fully opened pressure", 1.0, pressure.Final < FullyOpenedPa ? 1.0 : 0.0, 0.0)
                .Row("feed held at its spec", feedKgPerSecond, result.GetSeries("feed mass flow").Final, 0.001, "kg/s")
                .PrintAndThrowIfFailed();

            // Reopen the gas valve before saving: the case library re-solves what it stores.
            c.GasValve.WithOpeningPercent(100.0).WithOpeningSetpoint(100.0);
            c.Fs.Solve();

            CaseLibraryOutput.Emit(c.Fs, "dynamic_relief_valve");
        }

        /// <summary>Orifice D at 8.8 bar passes about 0.11 kg/s: the feed drives the valve fully open.</summary>
        private static void RunFullyOpen()
        {
            const double feedKgPerSecond = 0.15;

            var c = Build("FluentDynamicsReliefValveFullyOpen", feedKgPerSecond, OrificeD);
            var result = Integrate(c);

            var pressure = result.GetSeries("vessel pressure");
            var flareFlow = result.GetSeries("flare mass flow");

            PrintSamples(pressure, flareFlow, result.GetSeries("gas product mass flow"));

            if (pressure.Final < FullyOpenedPa)
                throw new Exception("The vessel ended at " + (pressure.Final / 1e5).ToString("F3") +
                    " bar, below the fully opened pressure: this run is meant to drive the valve fully open.");

            // API 520 critical flow through the orifice, from the state the valve saw on its last step:
            // W = A Kd Kb sqrt(k P1 rho (2/(k+1))^((k+1)/(k-1))). Fully open means Kvc = 1.
            var inlet = c.ReliefOut.Object;
            var p1 = inlet.GetPressure();
            var rho = inlet.Phases[0].Properties.density.GetValueOrDefault();
            var k = inlet.Phases[2].Properties.idealGasHeatCapacityRatio.GetValueOrDefault();
            var expected = OrificeD * DischargeCoefficient *
                Math.Sqrt(k * p1 * rho * Math.Pow(2.0 / (k + 1.0), (k + 1.0) / (k - 1.0)));

            Console.WriteLine("last step: P1 = " + (p1 / 1e5).ToString("F3") + " bar, rho = " + rho.ToString("F3") +
                " kg/m3, k = " + k.ToString("F4") + ", API 520 critical flow = " + expected.ToString("F5") + " kg/s");

            new ResultTable("Relief valve fully open")
                .Row("flare flow against the API 520 critical flow", expected, flareFlow.Final, expected * 0.005, "kg/s")
                .Row("flare flow below the feed (the vessel is still filling)", 1.0, flareFlow.Final < feedKgPerSecond ? 1.0 : 0.0, 0.0)
                .PrintAndThrowIfFailed();
        }

        private sealed class Case
        {
            public Flowsheet Fs;
            public ValveBuilder GasValve;
            public MaterialStreamBuilder ReliefOut;
        }

        private static Case Build(string name, double feedKgPerSecond, double orificeArea)
        {
            var fs = Flowsheet.Create(name)
                .WithCompound("Methane")
                .WithCompound("Ethane")
                .WithCompound("Propane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("feed")
                .At(40.Celsius(), 7.Bar())
                .WithMassFlow(feedKgPerSecond.KgPerSecond())
                .WithComposition(c => c.Mole("Methane", 0.85).Mole("Ethane", 0.10).Mole("Propane", 0.05))
                .AsFlowSpec();

            var vesselIn = fs.AddMaterialStream("vessel-in").At(40.Celsius(), 7.Bar()).AsPressureSpec();
            var gasOut = fs.AddMaterialStream("gas-out").At(40.Celsius(), 7.Bar()).AsPressureSpec();
            var gasProduct = fs.AddMaterialStream("gas-product").At(40.Celsius(), 6.Bar()).AsPressureSpec();
            var liquidOut = fs.AddMaterialStream("liquid-out").At(40.Celsius(), 7.Bar()).AsPressureSpec();
            // The feed is all vapour at these conditions, so nothing is drawn off the liquid line: a
            // zero flow spec on its product keeps the liquid valve from passing gas at the vessel
            // pressure once the level is zero, and the flowsheet still re-solves as saved.
            var liquidProduct = fs.AddMaterialStream("liquid-product").At(40.Celsius(), 6.Bar())
                .WithMassFlow(0.0.KgPerSecond()).AsFlowSpec();
            var reliefOut = fs.AddMaterialStream("relief-out").At(40.Celsius(), 7.Bar()).AsPressureSpec();
            var flare = fs.AddMaterialStream("flare").At(40.Celsius(), 1.Atm()).AsPressureSpec();

            // Kv gas mode on both sides: with the feed on a flow spec the inlet valve finds its own
            // upstream pressure, and the outlet valve computes what leaves from the pressures either
            // side of it, so a shut opening holds the outlet at zero.
            fs.AddValve("V-201-in")
                .WithCalcMode(Valve.CalculationMode.Kv_Gas)
                .WithKv(20.0)
                .WithOpeningKvRelationship()
                .WithOpeningPercent(100.0)
                .WithOpeningSetpoint(100.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(vesselIn, 0);

            fs.AddSeparator("V-201")
                .WithVolume(2.0.CubicMeters())
                .WithHeight(2.0.Meters())
                .InitializeFromInlet(true)
                .ResetContent()
                .ConnectFeed(vesselIn, 0)
                .ConnectProduct(gasOut, 0)
                .ConnectProduct(liquidOut, 1)
                .ConnectProduct(reliefOut, 3);

            var gasValve = fs.AddValve("V-201-gas")
                .WithCalcMode(Valve.CalculationMode.Kv_Gas)
                .WithKv(20.0)
                .WithOpeningKvRelationship()
                .WithOpeningPercent(100.0)
                .WithOpeningSetpoint(100.0)
                .ConnectFeed(gasOut, 0)
                .ConnectProduct(gasProduct, 0);

            // A pressure-drop valve against a flow-spec product: in dynamics it takes the product's
            // flow (zero) and drops the pressure, and it needs no liquid density to do so.
            fs.AddValve("V-201-liq")
                .WithPressureDrop(1.Bar())
                .ConnectFeed(liquidOut, 0)
                .ConnectProduct(liquidProduct, 0);

            var psv = fs.AddExternalUnitOperation("Relief Valve", "PSV-201")
                .ConnectFeed(reliefOut, 0)
                .ConnectProduct(flare, 0);

            var relief = (ReliefValve)psv.Object;
            relief.SetPointPressure = SetPointPa;
            relief.FullyOpenedPressure = FullyOpenedPa;
            relief.OrificeArea = orificeArea;
            relief.DischargeCoefficient = DischargeCoefficient;
            relief.BackPressureCoefficient = 1.0;
            relief.DefinedOpeningKvRelationShipType = Valve.OpeningKvRelationshipType.Linear;

            fs.AutoLayout();
            fs.Solve();

            fs.Dynamics.DefineIntegrator("Relief")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(DurationSeconds.Seconds())
                .Monitor("V-201", "Operating Pressure", "Pa", "vessel pressure")
                .Monitor("flare", "PROP_MS_2", "kg/s", "flare mass flow")
                .Monitor("gas-product", "PROP_MS_2", "kg/s", "gas product mass flow")
                .Monitor("feed", "PROP_MS_2", "kg/s", "feed mass flow");

            fs.Dynamics.DefineEventSet("Blocked outlet")
                .AddStepChange("V-201-gas", "PROP_VA_5", 0.0, at: ShutGasValveAtSeconds.Seconds(),
                    description: "gas outlet valve shut");

            fs.Dynamics.DefineSchedule("Blocked outlet run")
                .WithIntegrator("Relief")
                .WithEventSet("Blocked outlet")
                .MakeCurrent();

            return new Case { Fs = fs, GasValve = gasValve, ReliefOut = reliefOut };
        }

        private static DynamicsResult Integrate(Case c)
        {
            var blockers = DynamicsDiagnostics.CheckReady(c.Fs.Inner, "Blocked outlet run")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker)
                .ToList();

            if (blockers.Count > 0)
                throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            var result = c.Fs.RunDynamics("Blocked outlet run").Execute();

            if (!result.Completed)
                throw new Exception("Integration did not complete: " +
                    (result.Error == null ? "aborted" : result.Error.Message));

            Console.WriteLine(result);
            return result;
        }

        private static void PrintSamples(DynamicsSeries pressure, DynamicsSeries flareFlow, DynamicsSeries gasFlow)
        {
            foreach (var t in new[] { 0.0, 9.0, 11.0, 20.0, 30.0, 60.0, 120.0, 240.0, DurationSeconds })
                Console.WriteLine("t = " + t.ToString("F0").PadLeft(4) + " s: vessel " +
                    (pressure.ValueAt(t) / 1e5).ToString("F3") + " bar, flare " +
                    flareFlow.ValueAt(t).ToString("F5") + " kg/s, gas product " +
                    gasFlow.ValueAt(t).ToString("F5") + " kg/s");
        }
    }
}
