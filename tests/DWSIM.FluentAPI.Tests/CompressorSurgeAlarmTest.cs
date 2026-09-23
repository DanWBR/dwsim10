using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Automation.FluentAPI.Dynamics;
using ValveUO = DWSIM.UnitOperations.UnitOperations.Valve;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Steps the feed of a compressor down to 40 % of its design flow and checks that the surge
    /// alarm, set at 50 % of design, goes off.
    /// </summary>
    /// <remarks>
    /// The design flow is the inlet volumetric flow of the last steady-state solve, which the
    /// compressor keeps in <c>DesignInletVolumetricFlow</c> and mirrors into the read-only dynamic
    /// property "Design Inlet Volumetric Flow". The alarm used to compare the running flow against
    /// a fraction of itself, so it never fired.
    ///
    /// The line is a feed specified by flow, the compressor, a 1 m3 separator whose pressure is its
    /// own state, and a Kv gas valve to an outlet specified by pressure. The compressor is a
    /// pressure-flow element by default, so the feed step reaches its inlet within the step.
    /// </remarks>
    internal static class CompressorSurgeAlarmTest
    {
        private const double DesignFlowKgPerSecond = 1.0;
        private const double SurgeFraction = 0.5;
        private const double StepFraction = 0.4;
        private const double StepAtSeconds = 60.0;
        private const double DurationSeconds = 120.0;

        public static void Run()
        {
            var fs = Flowsheet.Create("FluentCompressorSurgeAlarm")
                .WithCompound("Nitrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("feed")
                .At(25.Celsius(), 2.Bar())
                .WithMassFlow(DesignFlowKgPerSecond.KgPerSecond())
                .AsFlowSpec();

            var compressed = fs.AddMaterialStream("compressed");
            var gas = fs.AddMaterialStream("separator-gas").AsPressureSpec();
            var liquid = fs.AddMaterialStream("separator-liquid").AsPressureSpec();
            var product = fs.AddMaterialStream("product").AsPressureSpec();

            var compressor = fs.AddCompressor("K-01")
                .WithOutletPressure(4.Bar())
                .WithAdiabaticEfficiencyPercent(75.0)
                .ConnectFeed(feed, 0)
                .ConnectProduct(compressed, 0);

            fs.AddSeparator("V-01")
                .WithVolume(1.0.CubicMeters())
                .ConnectFeed(compressed, 0)
                .ConnectProduct(gas, 0)
                .ConnectProduct(liquid, 1);

            // A Kv mode lets the valve compute its own flow from the pressure either side of it,
            // which is what lets the separator pressure settle against the outlet.
            fs.AddValve("VLV-01")
                .WithCalcMode(ValveUO.CalculationMode.Kv_Gas)
                .WithKv(100.0)
                .WithOpeningKvRelationship()
                .WithOpeningPercent(100.0)
                .ConnectFeed(gas, 0)
                .ConnectProduct(product, 0);

            fs.AutoLayout();
            fs.Solve();

            var designFlow = feed.Object.GetVolumetricFlow();

            Console.WriteLine("design inlet flow = " + (designFlow * 3600.0).ToString("F1") +
                " m3/h, separator at " + (gas.PressurePa / 1e5).ToString("F2") +
                " bar, product at " + (product.PressurePa / 1e5).ToString("F2") + " bar");

            compressor.WithDynamicProperty("Surge Flow Fraction", SurgeFraction);

            fs.Dynamics.DefineIntegrator("Surge")
                .WithIntegrationStep(1.Seconds())
                .WithDuration(DurationSeconds.Seconds())
                .Monitor("K-01", "Surge Alarm", description: "surge alarm")
                .Monitor("K-01", "Design Inlet Volumetric Flow", "m3/s", "design flow")
                .Monitor("feed", "PROP_MS_2", "kg/s", "feed mass flow")
                .Monitor("V-01", "Operating Pressure", "Pa", "separator pressure");

            fs.Dynamics.DefineEventSet("Feed step")
                .AddStepChange("feed", "PROP_MS_2", StepFraction * DesignFlowKgPerSecond,
                    at: StepAtSeconds.Seconds(), units: "kg/s");

            fs.Dynamics.DefineSchedule("Surge run")
                .WithIntegrator("Surge")
                .WithEventSet("Feed step")
                .MakeCurrent();

            var blockers = DynamicsDiagnostics.CheckReady(fs.Inner, "Surge run")
                .Where(f => f.Severity == DiagnosticSeverity.Blocker)
                .ToList();

            if (blockers.Count > 0)
                throw new Exception("Readiness check blocked the run: " + string.Join("; ", blockers));

            var result = fs.RunDynamics("Surge run").Execute();

            if (!result.Completed)
                throw new Exception("Integration did not complete: " +
                    (result.Error == null ? "aborted" : result.Error.Message));

            Console.WriteLine(result);

            var alarm = result.GetSeries("surge alarm");
            var feedFlow = result.GetSeries("feed mass flow");
            var stored = result.GetSeries("design flow");

            new ResultTable("Compressor surge alarm")
                .Row("design flow kept by the compressor", designFlow,
                    compressor.Object.DesignInletVolumetricFlow, 1e-9, "m3/s")
                .Row("design flow exposed as a dynamic property", designFlow, stored.Final, 1e-6, "m3/s")
                .Row("feed at design before the step, t = 30 s", DesignFlowKgPerSecond,
                    feedFlow.ValueAt(30.0), 0.01, "kg/s")
                .Row("feed at 40 % after the step, t = 90 s", StepFraction * DesignFlowKgPerSecond,
                    feedFlow.ValueAt(90.0), 0.01, "kg/s")
                .Row("alarm off at design flow, t = 30 s", 0.0, alarm.ValueAt(30.0), 0.0)
                .Row("alarm on below the surge flow, t = 90 s", 1.0, alarm.ValueAt(90.0), 0.0)
                .Row("alarm on at the end of the run", 1.0, alarm.Final, 0.0)
                .PrintAndThrowIfFailed();

            // Before the step the flow is the design flow, above any surge limit set below 100 %.
            for (var i = 0; i < alarm.Count && alarm.TimeSeconds[i] < StepAtSeconds; i++)
            {
                if (alarm.Values[i] == 0.0) continue;
                throw new Exception("The surge alarm fired at t = " + alarm.TimeSeconds[i] +
                    " s, before the feed step, with the flow at its design value.");
            }
        }
    }
}
