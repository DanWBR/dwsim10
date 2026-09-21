using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;
using PumpUO = DWSIM.UnitOperations.UnitOperations.Pump;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// A metering pump under flow control, in dynamics. A positive displacement machine is the
    /// opposite of a centrifugal one: it sets the flow of the line from its displacement and its
    /// speed, and the system decides the pressure. The drive is therefore the only handle on the
    /// dosing rate, and the rate follows the speed exactly: the operator raises the rate, the
    /// controller finds the speed, and the motor takes it there at the rate its inertia allows.
    /// </summary>
    internal static class DosingPumpDynamicsSample
    {
        private const double SecondRateKgPerSecond = 2.6;
        private const double RateChangeAtSeconds = 60.0;
        private const double DurationSeconds = 180.0;

        public static void Run()
        {
            var fs = Flowsheet.Create("DosingPumpDynamics")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("additive tank")
                .At(298.15.Kelvin(), 200000.0.Pascal())
                .WithMassFlow(1.9.KgPerSecond())
                .AsFlowSpec();

            var dosed = fs.AddMaterialStream("dosed to the line")
                .At(298.15.Kelvin(), 1200000.0.Pascal())
                .AsPressureSpec();

            var builder = fs.AddPump("P-201")
                .WithCalcMode(PumpUO.CalculationMode.PositiveDisplacement)
                .WithEfficiencyPercent(90.0);

            builder.ConnectFeed(feed, 0).ConnectProduct(dosed, 0);

            var pump = builder.Object;
            pump.Displacement = 0.0005;          // 0.5 litre per revolution
            pump.VolumetricEfficiency = 95.0;
            pump.OperatingSpeed = 240.0;
            pump.ReliefPressure = 2500000.0;
            pump.Pout = 1200000.0;

            pump.CreateDynamicProperties();
            pump.SetDynamicProperty("Current Speed", 240.0);
            pump.SetDynamicProperty("Target Speed", 240.0);
            // the motor is not instantaneous: the trend shows the drive ramping to the new speed
            pump.SetDynamicProperty("Rotational Inertia", 0.8);
            pump.SetDynamicProperty("Motor Torque", 2.0);

            fs.AutoLayout();
            fs.Solve();

            // the loop starts balanced, at the rate the pump delivers at its starting speed
            double firstRate = pump.DeliveredMassFlow;

            // the error the controller works on is (PV - SP)/SP and its output moves the speed by
            // the span, so the loop gain is Kp x span x (flow per rpm) / setpoint, which is of order
            // Kp here: a gain of one is already a brisk loop, not the hundreds a raw rpm span suggests
            var fic = fs.AddPIDController("FIC-201")
                .Controls("dosed to the line", "PROP_MS_2", "kg/s")
                .Manipulates("P-201", "Target Speed", "")
                .WithSetPoint(firstRate)
                .WithTuning(0.25, 0.06, 0.0)
                .WithOutputLimits(60.0, 600.0)
                .WithOffset(240.0)
                .ReverseActing(false)
                .Configure(o => { o.ManipulatedVariableSpan = 540.0; o.WindupGuard = 50.0; });

            var fi = fs.AddIndicator("FI-201", IndicatorKind.Analog).Reads("dosed to the line", "PROP_MS_2", "kg/s")
                .WithRange(0.0, 4.0)
                .WithAlarms(low: 1.2, high: 3.2);

            // the process line is laid out automatically; the instruments are placed by hand above
            // it, which keeps them off the piping and off each other
            fic.PositionAt((int)pump.GraphicObject.X - 20, (int)pump.GraphicObject.Y - 70);
            fi.PositionAt((int)pump.GraphicObject.X + 140, (int)pump.GraphicObject.Y - 75);

            fs.Solve();

            fs.Dynamics.DefineIntegrator("Dosing run")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(DurationSeconds.Seconds())
                .Monitor("dosed to the line", "PROP_MS_2", "kg/s", "dosing rate")
                .Monitor("P-201", "Current Speed", description: "pump speed");

            fs.Dynamics.DefineEventSet("Rate change")
                .AddStepChange("FIC-201", "SetPointAbs", SecondRateKgPerSecond,
                               at: RateChangeAtSeconds.Seconds(), units: "kg/s",
                               description: "dosing rate raised to 2.6 kg/s");

            fs.Dynamics.DefineSchedule("Dosing run")
                .WithIntegrator("Dosing run")
                .WithEventSet("Rate change")
                .MakeCurrent();

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, "Dosing run")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker)
                .ToList();
            if (blockers.Count > 0)
                throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            var result = fs.RunDynamics("Dosing run").Execute();
            if (!result.Completed)
                throw new Exception("Integration did not complete: " +
                    (result.Error == null ? "aborted" : result.Error.Message));

            Console.WriteLine(result);

            var rate = result.GetSeries("dosing rate");
            var speed = result.GetSeries("pump speed");

            // the sample just before the setpoint change, taken from the series itself so the
            // lookup never lands between two samples
            int beforeIndex = LastIndexBefore(rate, RateChangeAtSeconds);

            double rateBefore = rate.Values[beforeIndex];
            double speedBefore = speed.Values[beforeIndex];

            for (var i = 0; i < rate.Count; i += 15)
                Console.WriteLine($"   t={rate.TimeSeconds[i],5:F0} s   rate {rate.Values[i],6:F3} kg/s   speed {speed.Values[i],6:F1} rpm");

            new ResultTable("Metering pump under flow control")
                .Row("the rate held the first setpoint", firstRate, rateBefore, 0.02, "kg/s")
                .Row("the rate reaches the new setpoint", SecondRateKgPerSecond, rate.Final, 0.02, "kg/s")
                // the machine displaces the same volume every revolution, so the flow ratio is the
                // speed ratio, and nothing else in the line can change it
                .Row("the flow ratio is the speed ratio", speed.Final / speedBefore, rate.Final / rateBefore, 0.01, "")
                .RowInRange("the drive stayed inside its range", 60.0, 600.0, speed.Final, "rpm")
                .PrintAndThrowIfFailed();

            // the run ends at the second rate, with the event's setpoint still in the controller.
            // Put the loop back where the run starts, so whoever opens the case sees the same
            // first steady state and can simply press play.
            fic.WithSetPoint(firstRate);
            pump.SetDynamicProperty("Current Speed", 240.0);
            pump.SetDynamicProperty("Target Speed", 240.0);
            fs.Solve();

            CaseLibraryOutput.Emit(fs, "dosing-pump-flow-control-dynamics");
        }

        private static int LastIndexBefore(DynamicsSeries series, double time)
        {
            int index = 0;
            for (int i = 0; i < series.Count; i++)
            {
                if (series.TimeSeconds[i] >= time) break;
                index = i;
            }
            return index;
        }
    }
}
