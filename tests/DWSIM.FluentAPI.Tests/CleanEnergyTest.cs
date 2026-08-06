using System;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Smoke test for the typed clean-energy builders. Builds each unit in isolation
    /// (no solve) and confirms its underlying object lands in the flowsheet.
    /// </summary>
    internal static class CleanEnergyTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("CleanEnergyProbe");

            var wt = fs.AddWindTurbine("WT-1")
                .WithDiskAreaM2(50.0)
                .WithEfficiencyPercent(85.0)
                .WithTurbineCount(3);

            var ht = fs.AddHydroelectricTurbine("HYT-1")
                .WithStaticHeadM(20.0)
                .WithEfficiencyPercent(80.0);

            var sp = fs.AddSolarPanel("SP-1")
                .WithPanelAreaM2(2.0)
                .WithPanelEfficiencyPercent(18.0)
                .WithPanelCount(50)
                .WithSolarIrradiationKWPerM2(0.9);

            var we = fs.AddWaterElectrolyzer("WE-1")
                .WithCellCount(100)
                .WithCellVoltage(1.85)
                .WithEfficiencyPercent(70.0);

            var fc = fs.AddPEMFuelCell("FC-1");

            Console.WriteLine("Wind disk area    = " + wt.Object.DiskArea + " m^2");
            Console.WriteLine("Hydro static head = " + ht.Object.StaticHead + " m");
            Console.WriteLine("Solar irradiation = " + sp.Object.SolarIrradiation_kW_m2 + " kW/m^2");
            Console.WriteLine("Electrolyzer cells= " + we.Object.NumberOfCells);
            Console.WriteLine("Fuel cell name    = " + fc.Object.Name);

            if (fs.Inner.SimulationObjects.Count < 5)
                throw new Exception("Expected at least 5 clean-energy objects, got " + fs.Inner.SimulationObjects.Count);
        }
    }
}
