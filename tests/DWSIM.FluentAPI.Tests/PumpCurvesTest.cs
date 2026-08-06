using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Pump in Curves calculation mode, swept over the flow rate like a sensitivity analysis does.</summary>
    internal static class PumpCurvesTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("FluentPumpCurvesTest")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var inlet = fs.AddMaterialStream("inlet")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(20.KgPerSecond());

            var outlet = fs.AddMaterialStream("outlet");

            var pump = fs.AddPump("PUMP-1")
                .WithCalcMode(DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode.Curves)
                .WithEfficiencyPercent(75.0);

            pump.ConnectFeed(inlet, 0).ConnectProduct(outlet, 0);

            // typical centrifugal pump: head falls off with flow, entered in descending flow order
            // on purpose, to exercise the node ordering.
            var head = pump.Object.PumpCurveSet.CurveHead;
            head.Enabled = true;
            head.xunit = "m3/s";
            head.yunit = "m";
            head.X = new List<double> { 0.040, 0.030, 0.020, 0.010, 0.000 };
            head.Y = new List<double> { 20.0, 38.0, 50.0, 57.0, 60.0 };

            fs.AutoLayout();

            Console.WriteLine("   Q (m3/s)   head (m)    dP (kPa)   power (kW)   dT (K)");

            var powers = new List<double>();

            foreach (var massflow in new[] { 10.0, 15.0, 20.0, 25.0, 30.0 })
            {
                inlet.WithMassFlow(massflow.KgPerSecond());
                fs.Solve();

                var q = pump.Object.CurveFlow;
                var h = pump.Object.CurveHead;
                var dp = pump.Object.DeltaP.GetValueOrDefault();
                var power = pump.Object.DeltaQ.GetValueOrDefault();
                var dt = pump.Object.DeltaT.GetValueOrDefault();

                Console.WriteLine($"   {q:F5}    {h:F3}     {dp / 1000:F2}      {power:F4}      {dt:F5}");
                powers.Add(power);

                if (power <= 0.0) throw new Exception($"Power is {power} kW at {massflow} kg/s: the pump is doing no work.");
                if (dp <= 0.0) throw new Exception($"Delta P is {dp} Pa at {massflow} kg/s.");
                if (dt <= 0.0) throw new Exception($"Delta T is {dt} K at {massflow} kg/s: no energy reached the fluid.");

                // W.g.h/eff, with eff = 75%
                var expected = massflow * 9.81 * h / 0.75 / 1000;
                if (Math.Abs(power - expected) > 1e-6) throw new Exception($"Power {power} kW != expected {expected} kW.");
            }

            // the whole point of a sensitivity sweep: the response must actually vary
            if (powers.Distinct().Count() != powers.Count) throw new Exception("Power did not respond to the flow rate.");

            // an interpolated node must reproduce the curve
            inlet.WithMassFlow((0.020 * 996.0).KgPerSecond());
            fs.Solve();
            Console.WriteLine($"   node check: Q = {pump.Object.CurveFlow:F5} m3/s -> head = {pump.Object.CurveHead:F3} m (curve says 50.0 m at 0.020)");
            if (Math.Abs(pump.Object.CurveHead - 50.0) > 0.5) throw new Exception($"Head {pump.Object.CurveHead} m at the 0.020 m3/s node should be ~50 m.");

            // off the curve, the pump must complain instead of extrapolating
            inlet.WithMassFlow(60.KgPerSecond());
            var errors = fs.TrySolve();
            Console.WriteLine($"   out-of-range run reported {errors.Count} error(s)");
            if (errors.Count == 0) throw new Exception("A flow rate past the end of the head curve was silently extrapolated.");
            Console.WriteLine($"   -> {errors[0].Message}");

            CheckAffinityLaws(fs, inlet, pump.Object);

            CheckWritableProperties(pump.Object);

            // once an energy stream feeds the pump, it dictates the power and the power stops being a spec
            pump.ConnectEnergyFeed(fs.AddEnergyStream("power-in"), 1);
            pump.Object.CalcMode = DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode.EnergyStream;
            var withES = pump.Object.GetProperties(PropertyType.WR).Where(p => p.StartsWith("PROP_PU_")).ToArray();
            Console.WriteLine($"   EnergyStream + connected stream writable: [{string.Join(", ", withES)}]");
            if (withES.Contains("PROP_PU_3"))
                throw new Exception("Power is offered as writable although the connected energy stream overwrites it.");
        }

        /// <summary>The speed must scale the curves by the affinity laws: Q~N, H~N^2, P~N^3, efficiency invariant.</summary>
        private static void CheckAffinityLaws(Flowsheet fs, DWSIM.Automation.FluentAPI.Builders.MaterialStreamBuilder inlet, DWSIM.UnitOperations.UnitOperations.Pump pump)
        {
            var nref = pump.PumpCurveSet.ImpellerSpeed;
            Console.WriteLine();
            Console.WriteLine($"=== affinity laws (curves measured at {nref} rpm)");

            // enable the power and efficiency curves, so that the N^3 scaling is read off the power
            // curve rather than falling out of the hydraulic formula, and the efficiency is a real
            // function of flow rather than a constant
            var cpower = pump.PumpCurveSet.CurvePower;
            cpower.Enabled = true;
            cpower.xunit = "m3/s";
            cpower.yunit = "kW";
            cpower.X = new List<double> { 0.000, 0.010, 0.020, 0.030, 0.040 };
            cpower.Y = new List<double> { 5.0, 8.0, 11.0, 13.0, 14.0 };

            var ceff = pump.PumpCurveSet.CurveEfficiency;
            ceff.Enabled = true;
            ceff.xunit = "m3/s";
            ceff.yunit = "%";
            ceff.X = new List<double> { 0.000, 0.010, 0.020, 0.030, 0.040 };
            ceff.Y = new List<double> { 10.0, 55.0, 70.0, 68.0, 50.0 };

            // an unset operating speed must leave the curves exactly as they were
            pump.OperatingSpeed = 0.0;
            inlet.WithMassFlow(20.KgPerSecond());
            fs.Solve();
            double h0 = pump.CurveHead, p0 = pump.DeltaQ.GetValueOrDefault(), q0 = pump.CurveFlow, e0 = pump.CurveEff;

            pump.OperatingSpeed = nref;
            fs.Solve();
            if (Math.Abs(pump.CurveHead - h0) > 1e-9 || Math.Abs(pump.DeltaQ.GetValueOrDefault() - p0) > 1e-9)
                throw new Exception("Setting the operating speed to the curve speed changed the result; it must be a no-op.");
            Console.WriteLine($"   speed unset == speed {nref}: head {h0:F4} m, power {p0:F4} kW (unscaled, as before)");

            // at speed r*nref and flow r*q0, the affinity laws put us on the same point of the reference
            // curve, so head must scale by r^2, power by r^3 and efficiency must not move at all
            Console.WriteLine("   {0,7} {1,10} {2,12} {3,12} {4,10}", "r", "Q (m3/s)", "head (m)", "power (kW)", "eff (%)");
            Console.WriteLine("   {0,7:F2} {1,10:F5} {2,12:F4} {3,12:F4} {4,10:F4}", 1.0, q0, h0, p0, e0);

            foreach (var r in new[] { 0.8, 0.9, 1.1 })
            {
                pump.OperatingSpeed = nref * r;
                inlet.WithMassFlow((20.0 * r).KgPerSecond());   // keeps Q/N constant, to first order
                fs.Solve();

                double q = pump.CurveFlow, h = pump.CurveHead, p = pump.DeltaQ.GetValueOrDefault(), e = pump.CurveEff;
                Console.WriteLine("   {0,7:F2} {1,10:F5} {2,12:F4} {3,12:F4} {4,10:F4}", r, q, h, p, e);

                // the flow only tracks r to the extent that density is constant, so allow a little slack
                if (Math.Abs(q / q0 - r) > 5e-3) throw new Exception($"r={r}: Q/Q0 = {q / q0:F5}, expected ~{r}.");
                if (Math.Abs(h / h0 - r * r) > 5e-3) throw new Exception($"r={r}: H/H0 = {h / h0:F5}, expected ~{r * r} (N^2).");
                if (Math.Abs(p / p0 - r * r * r) > 5e-3) throw new Exception($"r={r}: P/P0 = {p / p0:F5}, expected ~{r * r * r} (N^3).");
                if (Math.Abs(e - e0) > 0.05) throw new Exception($"r={r}: efficiency moved from {e0:F4} to {e:F4}; it is invariant under affinity.");
            }

            // a speed with no reference to scale from cannot be honoured silently
            var saved = pump.PumpCurveSet.ImpellerSpeed;
            pump.PumpCurveSet.ImpellerSpeed = 0.0;
            pump.OperatingSpeed = 1000.0;
            var errs = fs.TrySolve();
            Console.WriteLine($"   operating speed with no curve speed -> {errs.Count} error(s)");
            if (errs.Count == 0) throw new Exception("An operating speed with no Impeller Speed to scale from was accepted silently.");
            Console.WriteLine($"   -> {errs[0].Message}");

            pump.PumpCurveSet.ImpellerSpeed = saved;
            pump.OperatingSpeed = 0.0;
            cpower.Enabled = false;
            ceff.Enabled = false;
            inlet.WithMassFlow(20.KgPerSecond());
            fs.Solve();
        }

        /// <summary>Only the properties the active calc mode actually reads may be offered as independent variables.</summary>
        private static void CheckWritableProperties(DWSIM.UnitOperations.UnitOperations.Pump pump)
        {
            // PROP_PU_0 delta P, _1 efficiency, _2 delta T, _3 power, _4 NPSH, _5 outlet P, _6 head,
            // _7 NPSH, _8 operating speed
            var expected = new Dictionary<DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode, string[]>
            {
                { DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode.Delta_P, new[] { "PROP_PU_0", "PROP_PU_1" } },
                { DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode.OutletPressure, new[] { "PROP_PU_1", "PROP_PU_5" } },
                { DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode.Power, new[] { "PROP_PU_1", "PROP_PU_3" } },
                // no energy stream is connected in this test, so the power stays a spec
                { DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode.EnergyStream, new[] { "PROP_PU_1", "PROP_PU_3" } },
                // the speed is the one thing a Curves-mode pump can be told
                { DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode.Curves, new[] { "PROP_PU_1", "PROP_PU_8" } },
            };

            var original = pump.CalcMode;

            foreach (var kv in expected)
            {
                pump.CalcMode = kv.Key;
                var writable = pump.GetProperties(PropertyType.WR).Where(p => p.StartsWith("PROP_PU_")).OrderBy(p => p).ToArray();
                var ro = pump.GetProperties(PropertyType.RO).Where(p => p.StartsWith("PROP_PU_")).ToArray();
                var all = pump.GetProperties(PropertyType.ALL).Where(p => p.StartsWith("PROP_PU_")).ToArray();

                Console.WriteLine($"   {kv.Key,-15} writable: [{string.Join(", ", writable)}]");

                if (!writable.SequenceEqual(kv.Value))
                    throw new Exception($"{kv.Key}: writable is [{string.Join(", ", writable)}], expected [{string.Join(", ", kv.Value)}].");
                if (writable.Intersect(ro).Any())
                    throw new Exception($"{kv.Key}: a property is both read-only and writable.");
                if (writable.Length + ro.Length != all.Length || all.Length != 9)
                    throw new Exception($"{kv.Key}: RO + WR must partition the 9 pump properties, got {ro.Length} + {writable.Length} of {all.Length}.");
            }

            // with an efficiency curve the speed is all that is left to specify
            pump.CalcMode = DWSIM.UnitOperations.UnitOperations.Pump.CalculationMode.Curves;
            pump.PumpCurveSet.CurveEfficiency.Enabled = true;
            var withcurve = pump.GetProperties(PropertyType.WR).Where(p => p.StartsWith("PROP_PU_")).ToArray();
            Console.WriteLine($"   Curves + eff curve writable: [{string.Join(", ", withcurve)}]");
            if (!withcurve.SequenceEqual(new[] { "PROP_PU_8" }))
                throw new Exception($"Curves mode with an efficiency curve should expose only the speed, got [{string.Join(", ", withcurve)}].");
            pump.PumpCurveSet.CurveEfficiency.Enabled = false;

            pump.CalcMode = original;
        }
    }
}
