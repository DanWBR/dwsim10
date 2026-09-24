using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using PumpUO = DWSIM.UnitOperations.UnitOperations.Pump;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// A positive displacement pump is the opposite of a centrifugal one: it delivers the volume its
    /// displacement and speed give and the system decides the discharge pressure, where a centrifugal
    /// machine gives the head its curve has at the flow the system asks for. The flow must therefore
    /// follow the speed exactly, ignore the discharge pressure, and stop at the relief setting.
    /// At steady state the balance still closes: the outlet carries the feed (2.0 kg/s here, against
    /// 1.894 displaced) and the pump reports the flow its displacement and speed give.
    /// </summary>
    internal static class PositiveDisplacementPumpTest
    {
        public static void Run()
        {
            var fs = Flowsheet.Create("PositiveDisplacementPump")
                .WithCompound("Water")
                .WithPropertyPackage(PropertyPackages.SteamTables);

            var feed = fs.AddMaterialStream("feed")
                .At(298.15.Kelvin(), 200000.0.Pascal())
                .WithMassFlow(2.0.KgPerSecond())
                .SetCompoundMassFlow("Water", 2.0);

            var outlet = fs.AddMaterialStream("dosed stream");

            var builder = fs.AddPump("P-201")
                .WithCalcMode(PumpUO.CalculationMode.PositiveDisplacement)
                .WithEfficiencyPercent(90.0);

            builder.ConnectFeed(feed, 0).ConnectProduct(outlet, 0);

            var pump = builder.Object;

            // a metering pump: 0.5 L per revolution, 95 % volumetric efficiency, running at 240 rpm
            pump.Displacement = 0.0005;
            pump.VolumetricEfficiency = 95.0;
            pump.OperatingSpeed = 240.0;
            pump.ReliefPressure = 2500000.0;
            pump.Pout = 1200000.0;

            fs.AutoLayout();
            fs.Solve();

            double rho = feed.Object.Phases[0].Properties.density.GetValueOrDefault();
            double expectedQ = 0.0005 * (240.0 / 60.0) * 0.95;          // m3/s
            double expectedW = expectedQ * rho;                          // kg/s

            Console.WriteLine($"   displaced: {pump.DeliveredVolumetricFlow * 3600:F3} m3/h = {pump.DeliveredMassFlow:F4} kg/s at 240 rpm");
            Console.WriteLine($"   discharge: {outlet.PressurePa / 1e5:F2} bar, power {pump.DeltaQ.GetValueOrDefault():F3} kW");

            // the flow follows the speed and nothing else
            pump.OperatingSpeed = 360.0;
            fs.Solve();
            double atHigherSpeed = pump.DeliveredMassFlow;

            // ... and it does not care what the discharge pressure is
            pump.OperatingSpeed = 240.0;
            pump.Pout = 1800000.0;
            fs.Solve();
            double atHigherPressure = pump.DeliveredMassFlow;
            double powerAtHigherPressure = pump.DeltaQ.GetValueOrDefault();

            // the relief is what stops it against a closed discharge
            pump.Pout = 4000000.0;
            fs.Solve();
            double reliefPressure = outlet.PressurePa;

            Console.WriteLine($"   at 360 rpm: {atHigherSpeed:F4} kg/s (1.5 x)");
            Console.WriteLine($"   at 18 bar:  {atHigherPressure:F4} kg/s, power {powerAtHigherPressure:F3} kW");
            Console.WriteLine($"   asked for 40 bar: held at {reliefPressure / 1e5:F2} bar by the relief");

            new ResultTable("Positive displacement pump")
                .Row("delivered volumetric flow", expectedQ, pump.DeliveredVolumetricFlow, 1e-9, "m3/s")
                .Row("displaced mass flow", expectedW, pump.DeliveredMassFlow, 1e-6, "kg/s")
                .Row("the steady state passes the feed", 2.0, outlet.MassFlowKgPerSecond, 1e-6, "kg/s")
                .Row("the flow follows the speed", expectedW * 1.5, atHigherSpeed, 1e-6, "kg/s")
                .Row("the flow ignores the discharge pressure", expectedW, atHigherPressure, 1e-6, "kg/s")
                .Row("the relief caps the discharge", 2500000.0, reliefPressure, 1e-9, "Pa")
                .Row("power rises with the pressure it works against",
                     2.0 / rho * (1800000.0 - 200000.0) / 1000.0 / 0.9, powerAtHigherPressure, 1e-6, "kW")
                .PrintAndThrowIfFailed();

            CheckSpeedIsRequired(fs, pump);
        }

        /// <summary>A displacement machine with no speed has no flow, and says so.</summary>
        private static void CheckSpeedIsRequired(Flowsheet fs, PumpUO pump)
        {
            var saved = pump.OperatingSpeed;
            pump.OperatingSpeed = 0.0;

            var errors = fs.TrySolve();
            Console.WriteLine($"   with no speed -> {errors.Count} error(s)");
            if (errors.Count == 0)
                throw new Exception("A displacement pump with no operating speed was accepted silently.");
            Console.WriteLine($"   -> {errors[0].Message}");

            pump.OperatingSpeed = saved;
            fs.Solve();
        }
    }
}
