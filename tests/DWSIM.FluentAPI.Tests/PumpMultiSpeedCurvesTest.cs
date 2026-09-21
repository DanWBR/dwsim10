using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps;
using PumpUO = DWSIM.UnitOperations.UnitOperations.Pump;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// A variable-frequency pump carries the manufacturer's curves measured at several speeds. Between
    /// two measured speeds the operating point is read off both sets and blended; outside the measured
    /// range the nearest set is scaled with the affinity laws, which is what a pump with a single
    /// published curve has always done. https://github.com/DanWBR/dwsim10/issues/82
    /// </summary>
    internal static class PumpMultiSpeedCurvesTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("FluentPumpMultiSpeedTest")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var inlet = fs.AddMaterialStream("inlet")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(20.KgPerSecond());

            var outlet = fs.AddMaterialStream("outlet");

            var builder = fs.AddPump("PUMP-1")
                .WithCalcMode(PumpUO.CalculationMode.Curves)
                .WithEfficiencyPercent(75.0);

            builder.ConnectFeed(inlet, 0).ConnectProduct(outlet, 0);

            var pump = builder.Object;

            // reference set, measured at 1450 rpm
            pump.PumpCurveSet.ImpellerSpeed = 1450.0;
            SetHead(pump.PumpCurveSet,
                    new List<double> { 0.000, 0.010, 0.020, 0.030, 0.040 },
                    new List<double> { 60.0, 57.0, 50.0, 38.0, 20.0 });

            // a second measured set at 1750 rpm, deliberately away from the affinity parabola of the
            // first one, so a result that came from scaling instead of from the measurement shows
            var fast = new CurveSet { ImpellerSpeed = 1750.0, Name = "1750 rpm" };
            SetHead(fast,
                    new List<double> { 0.000, 0.012, 0.024, 0.036, 0.048 },
                    new List<double> { 85.0, 80.0, 70.0, 52.0, 25.0 });
            pump.CurveSets[1750] = fast;

            fs.AutoLayout();

            double h1450 = SolveAt(fs, pump, 1450.0);
            double h1750 = SolveAt(fs, pump, 1750.0);

            Console.WriteLine($"   head at 1450 rpm: {h1450:F4} m (reference set)");
            Console.WriteLine($"   head at 1750 rpm: {h1750:F4} m (second measured set)");

            // the measured set must be used as measured, not scaled from the reference one
            pump.CurveSets.Clear();
            double hAffinity = SolveAt(fs, pump, 1750.0);
            pump.CurveSets[1750] = fast;
            Console.WriteLine($"   head at 1750 rpm from the affinity laws alone: {hAffinity:F4} m");

            if (Math.Abs(h1750 - hAffinity) / hAffinity < 0.01)
                throw new Exception($"The measured 1750 rpm set gave {h1750:F4} m, the same as scaling the 1450 rpm one ({hAffinity:F4} m): the second set is not being read.");

            // halfway between the two measured speeds, the head is the blend of the two readings
            double h1600 = SolveAt(fs, pump, 1600.0);
            double expected = 0.5 * (h1450 + h1750);
            Console.WriteLine($"   head at 1600 rpm: {h1600:F4} m, expected {expected:F4} m (half of each set)");

            if (Math.Abs(h1600 - expected) > 1e-6 * Math.Abs(expected))
                throw new Exception($"Head at 1600 rpm is {h1600:F6} m; halfway between the 1450 and 1750 rpm sets it must be {expected:F6} m.");

            if (h1600 <= h1450 || h1600 >= h1750)
                throw new Exception($"Head at 1600 rpm ({h1600:F4} m) is outside the two measured sets ({h1450:F4} and {h1750:F4} m).");

            // above the highest measured speed, the nearest set is scaled by the affinity laws: the
            // answer must be the one this pump gives with that set as its only curve
            double hAbove = SolveAt(fs, pump, 2100.0);

            var reference = pump.PumpCurveSet;
            pump.PumpCurveSet = fast;
            pump.CurveSets.Clear();
            double hAboveSingle = SolveAt(fs, pump, 2100.0);
            pump.PumpCurveSet = reference;
            pump.CurveSets[1750] = fast;

            Console.WriteLine($"   head at 2100 rpm: {hAbove:F4} m, against {hAboveSingle:F4} m from the 1750 rpm set alone");

            if (Math.Abs(hAbove - hAboveSingle) > 1e-9 * Math.Abs(hAboveSingle))
                throw new Exception($"Above the measured range the pump gave {hAbove:F6} m, not the affinity scaling of the nearest set ({hAboveSingle:F6} m).");

            // a pump with one set must still behave exactly as it did before there were sets
            pump.CurveSets.Clear();
            double hSingleRef = SolveAt(fs, pump, 1450.0);
            if (Math.Abs(hSingleRef - h1450) > 1e-9 * Math.Abs(h1450))
                throw new Exception($"With a single curve set the pump gave {hSingleRef:F6} m instead of {h1450:F6} m.");
            pump.CurveSets[1750] = fast;

            CheckXmlRoundTrip(pump);
        }

        /// <summary>The sets have to survive a save and a reload, keyed by the speed they were measured at.</summary>
        private static void CheckXmlRoundTrip(PumpUO pump)
        {
            var clone = (PumpUO)pump.CloneXML();

            if (clone.CurveSets == null || clone.CurveSets.Count != pump.CurveSets.Count)
                throw new Exception($"The clone carries {clone.CurveSets?.Count ?? 0} extra curve set(s), the original has {pump.CurveSets.Count}.");

            foreach (var entry in pump.CurveSets)
            {
                if (!clone.CurveSets.ContainsKey(entry.Key))
                    throw new Exception($"The curve set measured at {entry.Key} rpm did not survive the round trip.");

                var original = entry.Value.CurveHead;
                var restored = clone.CurveSets[entry.Key].CurveHead;

                if (restored.Enabled != original.Enabled || restored.X.Count != original.X.Count)
                    throw new Exception($"The head curve of the {entry.Key} rpm set came back with {restored.X.Count} point(s), against {original.X.Count}.");

                for (int i = 0; i < original.X.Count; i++)
                {
                    if (Math.Abs(restored.X[i] - original.X[i]) > 1e-12 || Math.Abs(restored.Y[i] - original.Y[i]) > 1e-12)
                        throw new Exception($"Point {i} of the {entry.Key} rpm head curve came back as ({restored.X[i]}, {restored.Y[i]}) instead of ({original.X[i]}, {original.Y[i]}).");
                }

                if (Math.Abs(clone.CurveSets[entry.Key].ImpellerSpeed - entry.Value.ImpellerSpeed) > 1e-9)
                    throw new Exception($"The {entry.Key} rpm set came back measured at {clone.CurveSets[entry.Key].ImpellerSpeed} rpm.");
            }

            Console.WriteLine($"   {pump.CurveSets.Count} extra curve set(s) survived the XML round trip");
        }

        private static void SetHead(CurveSet set, List<double> x, List<double> y)
        {
            var head = set.CurveHead;
            head.Enabled = true;
            head.xunit = "m3/s";
            head.yunit = "m";
            head.X = x;
            head.Y = y;
        }

        private static double SolveAt(Flowsheet fs, PumpUO pump, double rpm)
        {
            pump.OperatingSpeed = rpm;

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception($"the pump did not solve at {rpm} rpm: " +
                                    string.Join("; ", errors.Select(e => e.Message)));

            return pump.CurveHead;
        }
    }
}
