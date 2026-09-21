using System;
using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using PumpUO = DWSIM.UnitOperations.UnitOperations.Pump;
using CurveSet = DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps.CurveSet;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Cooling water transfer pump on a variable-frequency drive. The manufacturer's curves were
    /// measured at 1450 and 1750 rpm, the pump runs at 1600 rpm, and the operating point is read off
    /// both sets and blended rather than scaled from one of them.
    /// Checks: the head at 1600 rpm sits between the two measured sets, the efficiency follows the
    /// measured curves, and the discharge valve delivers the header pressure.
    /// </summary>
    internal static class PumpCurvesSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("PumpVariableSpeed")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var suction = fs.AddMaterialStream("cooling water suction")
                .At(303.15.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(20.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 20.0);

            var discharge = fs.AddMaterialStream("pump discharge");
            var header = fs.AddMaterialStream("cooling water header");

            var pump = fs.AddPump("P-101")
                .WithCalcMode(PumpUO.CalculationMode.Curves)
                .WithEfficiencyPercent(70.0)
                .WithCurves(1450.0, set =>
                {
                    set.Name = "1450 rpm";
                    set.ImpellerDiameter = 250.0;
                    Head(set, new List<double> { 0.000, 0.010, 0.020, 0.030, 0.040 },
                              new List<double> { 60.0, 57.0, 50.0, 38.0, 20.0 });
                    Efficiency(set, new List<double> { 0.000, 0.010, 0.020, 0.030, 0.040 },
                                    new List<double> { 10.0, 55.0, 70.0, 68.0, 50.0 });
                    Npshr(set, new List<double> { 0.000, 0.010, 0.020, 0.030, 0.040 },
                               new List<double> { 1.0, 1.5, 2.2, 3.2, 4.5 });
                })
                .WithCurvesAtSpeed(1750, set =>
                {
                    // measured, not scaled: at the top of the range the real machine falls below the
                    // affinity parabola of the 1450 rpm curve, and its best efficiency moves
                    set.Name = "1750 rpm";
                    set.ImpellerDiameter = 250.0;
                    Head(set, new List<double> { 0.000, 0.012, 0.024, 0.036, 0.048 },
                              new List<double> { 87.0, 82.0, 72.0, 55.0, 29.0 });
                    Efficiency(set, new List<double> { 0.000, 0.012, 0.024, 0.036, 0.048 },
                                    new List<double> { 10.0, 56.0, 71.0, 67.0, 47.0 });
                    Npshr(set, new List<double> { 0.000, 0.012, 0.024, 0.036, 0.048 },
                               new List<double> { 1.4, 2.1, 3.1, 4.6, 6.4 });
                })
                .WithOperatingSpeed(1600.0)
                .ConnectFeed(suction, 0)
                .ConnectProduct(discharge, 0);

            fs.AddValve("FV-101")
                .WithOutletPressure(400000.0.Pascal())
                .ConnectFeed(discharge, 0)
                .ConnectProduct(header, 0);

            fs.Solve();

            // the same pump read at each measured speed, to show the blend is between them
            double headAt1600 = pump.Object.CurveHead;
            double effAt1600 = pump.Object.CurveEff;
            double powerAt1600 = pump.Object.DeltaQ.GetValueOrDefault();

            pump.Object.OperatingSpeed = 1450.0;
            fs.Solve();
            double headAt1450 = pump.Object.CurveHead;

            pump.Object.OperatingSpeed = 1750.0;
            fs.Solve();
            double headAt1750 = pump.Object.CurveHead;

            // leave the flowsheet at the service point before it is saved
            pump.Object.OperatingSpeed = 1600.0;
            fs.Solve();

            Console.WriteLine($"   head at 1450 rpm: {headAt1450:F2} m");
            Console.WriteLine($"   head at 1600 rpm: {headAt1600:F2} m");
            Console.WriteLine($"   head at 1750 rpm: {headAt1750:F2} m");

            new ResultTable("Variable-speed cooling water pump")
                .RowInRange("Head at 1600 rpm between the measured sets", headAt1450 + 0.5, headAt1750 - 0.5, headAt1600, "m")
                .RowInRange("Efficiency read off the curves", 60.0, 75.0, effAt1600, "%")
                .RowInRange("Shaft power", 10.0, 30.0, powerAt1600, "kW")
                .Row("Header pressure", 400000.0, header.PressurePa, 1.0, "Pa")
                .Row("Mass conservation", 20.0, header.MassFlowKgPerSecond, 0.01, "kg/s")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "pump-variable-frequency-drive");
        }

        private static void Head(CurveSet set, List<double> q, List<double> h)
        {
            var c = set.CurveHead;
            c.Enabled = true; c.xunit = "m3/s"; c.yunit = "m"; c.X = q; c.Y = h;
        }

        private static void Efficiency(CurveSet set, List<double> q, List<double> e)
        {
            var c = set.CurveEfficiency;
            c.Enabled = true; c.xunit = "m3/s"; c.yunit = "%"; c.X = q; c.Y = e;
        }

        private static void Npshr(CurveSet set, List<double> q, List<double> n)
        {
            var c = set.CurveNPSHr;
            c.Enabled = true; c.xunit = "m3/s"; c.yunit = "m"; c.X = q; c.Y = n;
        }
    }
}
