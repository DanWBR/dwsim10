using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UnitOperations.SpecialOps;
using FS = DWSIM.Automation.FluentAPI.Flowsheet;

namespace DWSIM.FluentAPI.Tests
{
    // Recycle "Global Convergence (Broyden)" used to wreck its tear stream on the second solver
    // pass. broydn returns a step (new x = x + P), but the solver mixed 0.3 of the point with 0.7 of
    // the step, so temperature, pressure and flow collapsed to about 0.3 of their values (the flow
    // going negative). The mass-flow residual handed to Broyden was also an unsigned sum, so it could
    // only ever push the flow one way. A water/ethanol heater loop with Broyden on must converge to
    // the same steady state as plain successive substitution. https://github.com/DanWBR/dwsim10/issues/63
    internal static class RecycleBroydenTest
    {
        public static void Run()
        {
            // Feed 100 kg/h of a 50/50 water/ethanol mixture at 25 C and 200 kPa into a mixer with the
            // recycle return, heat to 60 C, split in half, half back through the recycle. At steady
            // state the split closes R = (100 + R) / 2, so the return settles at 100 kg/h, 60 C, 200 kPa.
            var fs = FS.Create("RecycleBroyden")
                       .WithCompound("Water")
                       .WithCompound("Ethanol")
                       .WithPropertyPackage(PropertyPackages.PengRobinson);

            var feed = fs.AddMaterialStream("FEED")
                         .At((25.0 + 273.15).Kelvin(), 200000.0.Pascal())
                         .WithMassFlow((100.0 / 3600.0).KgPerSecond())
                         .WithComposition(c => c.Mass("Water", 0.5).Mass("Ethanol", 0.5));
            var mixed = fs.AddMaterialStream("MIXED");
            var hot = fs.AddMaterialStream("HOT");
            var prod = fs.AddMaterialStream("PRODUCT");
            var tear = fs.AddMaterialStream("TEAR");

            // the recycle return, seeded with an estimate the solver iterates away from
            var ret = fs.AddMaterialStream("RETURN")
                        .At((60.0 + 273.15).Kelvin(), 200000.0.Pascal())
                        .WithMassFlow((50.0 / 3600.0).KgPerSecond())
                        .WithComposition(c => c.Mass("Water", 0.5).Mass("Ethanol", 0.5));

            fs.AddMixer("MIX-1").ConnectFeed(feed, 0).ConnectFeed(ret, 1).ConnectProduct(mixed, 0);
            fs.AddHeater("HTR-1").ConnectFeed(mixed, 0).ConnectProduct(hot, 0)
              .WithOutletTemperature((60.0 + 273.15).Kelvin());
            fs.AddSplitter("SPL-1").ConnectFeed(hot, 0).ConnectProduct(prod, 0).ConnectProduct(tear, 1)
              .Configure(s => { s.Ratios.Clear(); s.Ratios.Add(0.5); s.Ratios.Add(0.5); s.Ratios.Add(0.0); });
            var rec = fs.AddUnitOperation(ObjectType.OT_Recycle, "REC-1").ConnectFeed(tear, 0).ConnectProduct(ret, 0);

            var recycle = (Recycle)rec.Object;
            recycle.AccelerationMethod = AccelMethod.GlobalBroyden;
            recycle.MaximumIterations = 100;
            recycle.ConvergenceParameters.Temperatura = 0.01;
            recycle.ConvergenceParameters.Pressao = 1.0;
            recycle.ConvergenceParameters.VazaoMassica = 1.0E-05;

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("the Broyden recycle did not solve: " +
                                    string.Join("; ", errors.Select(e => e.Message)));

            var r = ret.Object;
            double wKgH = r.Phases[0].Properties.massflow.GetValueOrDefault() * 3600.0;
            double tK = r.Phases[0].Properties.temperature.GetValueOrDefault();
            double pPa = r.Phases[0].Properties.pressure.GetValueOrDefault();

            Console.WriteLine($"  Broyden recycle return: {wKgH:F2} kg/h, {tK:F2} K, {pPa:F0} Pa");

            // the bug left the return at ~0.3 of its values (99.94 K, 60,000 Pa, -2.50 kg/h)
            if (wKgH <= 0.0)
                throw new Exception($"return mass flow went non-positive: {wKgH:F2} kg/h");

            Check("return mass flow (kg/h)", wKgH, 100.0, 3.0);
            Check("return temperature (K)", tK, 333.15, 1.0);
            Check("return pressure (Pa)", pPa, 200000.0, 2000.0);
        }

        private static void Check(string what, double value, double expected, double tol)
        {
            if (Math.Abs(value - expected) > tol)
                throw new Exception($"{what}: expected {expected} +/- {tol}, got {value}");
        }
    }
}
