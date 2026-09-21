using System;
using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using ExpanderUO = DWSIM.UnitOperations.UnitOperations.Expander;
using Curve = DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps.Curve;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Letdown turboexpander on a high-pressure gas line, described by the power its maker measured
    /// at 14000, 18000 and 22000 rpm against the inlet actual flow, running at 20000 rpm. The cold
    /// outlet then recovers duty against a warm stream, which is what the letdown is for.
    /// Checks: the power at 20000 rpm sits between the two measured speeds, the gas leaves colder
    /// and at lower pressure, and the recovery exchanger closes its balance.
    /// </summary>
    internal static class ExpanderCurvesSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("ExpanderCurves")
                .WithCompounds("Methane", "Ethane", "Nitrogen", "Water")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var inlet = fs.AddMaterialStream("high pressure gas")
                .At(313.15.Kelvin(), 4000000.0.Pascal())
                .WithMassFlow(2.0.KgPerSecond())
                .WithComposition(c => c.Mass("Methane", 0.92).Mass("Ethane", 0.05).Mass("Nitrogen", 0.03));

            var expanded = fs.AddMaterialStream("expanded gas");
            var toGrid = fs.AddMaterialStream("gas to distribution");
            var power = fs.AddEnergyStream("W generated");

            var warmIn = fs.AddMaterialStream("warm water in")
                .At(318.15.Kelvin(), 300000.0.Pascal())
                .WithMassFlow(3.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 3.0);
            var warmOut = fs.AddMaterialStream("cooled water out");

            var expander = fs.AddExpander("EX-101")
                .WithCalcMode(ExpanderUO.CalculationMode.Curves)
                .WithProcessPath(ExpanderUO.ProcessPathType.Adiabatic)
                .WithAdiabaticEfficiencyPercent(80.0)
                .ConnectFeed(inlet, 0)
                .ConnectProduct(expanded, 0)
                .ConnectEnergyProduct(power, 1);

            // the measured map: power against inlet actual flow, one curve per speed. Power curves
            // rather than head curves, which is how a letdown machine is usually published
            var map = expander.Object.Curves;
            map.Clear();
            AddSpeed(expander.Object, map, 14000,
                     new List<double> { 100.0, 200.0, 300.0, 400.0 },
                     new List<double> { 120.0, 210.0, 270.0, 300.0 },
                     new List<double> { 68.0, 76.0, 80.0, 75.0 });
            AddSpeed(expander.Object, map, 18000,
                     new List<double> { 100.0, 200.0, 300.0, 400.0 },
                     new List<double> { 150.0, 265.0, 345.0, 385.0 },
                     new List<double> { 70.0, 78.0, 82.0, 77.0 });
            AddSpeed(expander.Object, map, 22000,
                     new List<double> { 100.0, 200.0, 300.0, 400.0 },
                     new List<double> { 175.0, 310.0, 405.0, 450.0 },
                     new List<double> { 69.0, 77.0, 81.0, 76.0 });

            expander.Object.Speed = 20000;

            fs.AddHeatExchanger("E-201")
                .WithCalculationMode(DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode.CalcBothTemp_UA)
                .WithGlobalUA(4000.0)
                .WithHotSidePressureDrop(10000.0.Pascal())
                .WithColdSidePressureDrop(10000.0.Pascal())
                .ConnectFeed(warmIn, 0)
                .ConnectFeed(expanded, 1)
                .ConnectProduct(warmOut, 0)
                .ConnectProduct(toGrid, 1);

            fs.Solve();

            double powerAt20000 = expander.Object.DeltaQ;
            double outletT = expanded.TemperatureK;
            double outletP = expanded.PressurePa;

            expander.Object.Speed = 18000;
            fs.Solve();
            double powerAt18000 = expander.Object.DeltaQ;

            expander.Object.Speed = 22000;
            fs.Solve();
            double powerAt22000 = expander.Object.DeltaQ;

            expander.Object.Speed = 20000;
            fs.Solve();

            Console.WriteLine($"   power at 18000 rpm: {powerAt18000:F1} kW");
            Console.WriteLine($"   power at 20000 rpm: {powerAt20000:F1} kW");
            Console.WriteLine($"   power at 22000 rpm: {powerAt22000:F1} kW");
            Console.WriteLine($"   expander outlet: {outletT:F1} K, {outletP / 1e5:F2} bar");

            new ResultTable("Letdown turboexpander on its measured power map")
                .RowInRange("Power at 20000 rpm between the measured speeds",
                            Math.Min(powerAt18000, powerAt22000) + 1.0,
                            Math.Max(powerAt18000, powerAt22000) - 1.0, powerAt20000, "kW")
                .RowInRange("Gas leaves colder than it entered", 150.0, 312.0, outletT, "K")
                .RowInRange("Gas leaves below the inlet pressure", 100000.0, 3900000.0, outletP, "Pa")
                .RowInRange("Water gives up heat", 280.0, 317.9, warmOut.TemperatureK, "K")
                .Row("Gas mass conservation", 2.0, toGrid.MassFlowKgPerSecond, 0.001, "kg/s")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "turboexpander-performance-map");
        }

        private static void AddSpeed(ExpanderUO machine, Dictionary<int, Dictionary<string, Curve>> map,
                                     int rpm, List<double> flow, List<double> power, List<double> efficiency)
        {
            var set = machine.CreateCurves();

            // the head curve stays disabled, so the machine reads its power map
            var p = set["POWER"];
            p.Enabled = true; p.xunit = "m3/h @ P,T"; p.yunit = "kW"; p.X = flow; p.Y = power;

            var e = set["EFF"];
            e.Enabled = true; e.xunit = "m3/h @ P,T"; e.yunit = "%"; e.X = flow; e.Y = efficiency;

            map[rpm] = set;
        }
    }
}
