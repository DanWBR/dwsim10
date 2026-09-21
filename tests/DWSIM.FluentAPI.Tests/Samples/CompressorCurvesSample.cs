using System;
using System.Collections.Generic;
using DWSIM.Automation.FluentAPI;
using CompressorUO = DWSIM.UnitOperations.UnitOperations.Compressor;
using Curve = DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps.Curve;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Fuel gas booster compressor described by its performance map: head and efficiency against
    /// inlet actual flow, measured at 8000, 10000 and 12000 rpm, running at 11000 rpm between two of
    /// the measured speeds, with an aftercooler on the discharge.
    /// Checks: the head at 11000 rpm sits between the 10000 and 12000 rpm curves, the discharge
    /// pressure and the shaft power are consistent with it, and the aftercooler meets its outlet
    /// temperature.
    /// </summary>
    internal static class CompressorCurvesSample
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("CompressorCurves")
                .WithCompounds("Methane", "Ethane", "Nitrogen")
                .WithPropertyPackage(PropertyPackages.PengRobinson);

            var suction = fs.AddMaterialStream("fuel gas suction")
                .At(298.15.Kelvin(), 200000.0.Pascal())
                .WithMassFlow(0.60.KgPerSecond())
                .WithComposition(c => c.Mass("Methane", 0.90).Mass("Ethane", 0.07).Mass("Nitrogen", 0.03));

            var discharge = fs.AddMaterialStream("compressor discharge");
            var cooled = fs.AddMaterialStream("fuel gas to header");
            var power = fs.AddEnergyStream("W compressor");

            var compressor = fs.AddCompressor("K-101")
                .WithCalcMode(CompressorUO.CalculationMode.Curves)
                .WithProcessPath(CompressorUO.ProcessPathType.Adiabatic)
                .WithAdiabaticEfficiencyPercent(75.0)
                .ConnectFeed(suction, 0)
                .ConnectProduct(discharge, 0)
                .ConnectEnergyFeed(power, 1);

            // the performance map: one set of curves per measured speed, flow as actual m3/h at the
            // inlet, head in metres of gas, efficiency in per cent
            var map = compressor.Object.Curves;
            map.Clear();
            AddSpeed(compressor.Object, map, 8000,
                     new List<double> { 1000.0, 2000.0, 3000.0, 4000.0 },
                     new List<double> { 2500.0, 2300.0, 1900.0, 1200.0 },
                     new List<double> { 60.0, 72.0, 75.0, 68.0 });
            AddSpeed(compressor.Object, map, 10000,
                     new List<double> { 1200.0, 2400.0, 3600.0, 4800.0 },
                     new List<double> { 3900.0, 3600.0, 3000.0, 1900.0 },
                     new List<double> { 61.0, 73.0, 76.0, 69.0 });
            AddSpeed(compressor.Object, map, 12000,
                     new List<double> { 1400.0, 2800.0, 4200.0, 5600.0 },
                     new List<double> { 5600.0, 5200.0, 4300.0, 2700.0 },
                     new List<double> { 59.0, 71.0, 74.0, 66.0 });

            compressor.Object.Speed = 11000;

            fs.AddCooler("E-101")
                .WithOutletTemperature(313.15.Kelvin())
                .WithPressureDrop(20000.0.Pascal())
                .ConnectFeed(discharge, 0)
                .ConnectProduct(cooled, 0);

            fs.Solve();

            double headAt11000 = compressor.Object.CurveHead;
            double dischargeP = discharge.PressurePa;
            double shaft = compressor.Object.DeltaQ;

            // the same machine at the two measured speeds that bracket it
            compressor.Object.Speed = 10000;
            fs.Solve();
            double headAt10000 = compressor.Object.CurveHead;

            compressor.Object.Speed = 12000;
            fs.Solve();
            double headAt12000 = compressor.Object.CurveHead;

            compressor.Object.Speed = 11000;
            fs.Solve();

            Console.WriteLine($"   head at 10000 rpm: {headAt10000:F0} m");
            Console.WriteLine($"   head at 11000 rpm: {headAt11000:F0} m");
            Console.WriteLine($"   head at 12000 rpm: {headAt12000:F0} m");

            new ResultTable("Fuel gas booster compressor on its performance map")
                .RowInRange("Head at 11000 rpm between the measured speeds", headAt10000 + 10.0, headAt12000 - 10.0, headAt11000, "m")
                .RowInRange("Discharge pressure above suction", 250000.0, 2000000.0, dischargeP, "Pa")
                .RowInRange("Shaft power", 1.0, 100.0, shaft, "kW")
                .Row("Aftercooler outlet", 313.15, cooled.TemperatureK, 0.001, "K")
                .Row("Mass conservation", 0.60, cooled.MassFlowKgPerSecond, 0.001, "kg/s")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(fs, "compressor-performance-map");
        }

        private static void AddSpeed(CompressorUO machine, Dictionary<int, Dictionary<string, Curve>> map,
                                     int rpm, List<double> flow, List<double> head, List<double> efficiency)
        {
            var set = machine.CreateCurves();

            var h = set["HEAD"];
            h.Enabled = true; h.xunit = "m3/h @ P,T"; h.yunit = "m"; h.X = flow; h.Y = head;

            var e = set["EFF"];
            e.Enabled = true; e.xunit = "m3/h @ P,T"; e.yunit = "%"; e.X = flow; e.Y = efficiency;

            map[rpm] = set;
        }
    }
}
