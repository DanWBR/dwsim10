using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
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

        // Two heater loops on one flowsheet, loop A on Broyden and loop B on plain substitution.
        // The solver wrote every recycle's outlet from its Values after a Broyden step, and a
        // substitution recycle fills Values from the outlet BEFORE copying the inlet across, so
        // loop B's tear stream was put back as it was on every pass and never left its estimate.
        // https://github.com/DanWBR/dwsim10/issues/83
        public static void RunBesideSubstitution()
        {
            // B sends half its stream back, so it closes at 100 kg/h like A does.
            SolveTwoLoops(0.5, out var returnA, out var returnB);

            Check("loop A return mass flow (kg/h)", returnA.Flow, 100.0, 3.0);
            Check("loop A return temperature (K)", returnA.T, 333.15, 1.0);
            Check("loop B return mass flow (kg/h)", returnB.Flow, 100.0, 3.0);
            Check("loop B return temperature (K)", returnB.T, 353.15, 1.0);
        }

        // The same pair, with loop B slow enough that it keeps the solver looping long after the
        // Broyden recycle has converged. broydn was then called every pass on variables that no
        // longer move: p'Hy is exactly zero, the update divided by it and filled the Hessian with
        // NaN, and loop A's tear stream followed. https://github.com/DanWBR/dwsim10/issues/83
        public static void RunBesideSlowerSubstitution()
        {
            // B sends 0.8 of its stream back: R = 0.8 (100 + R) closes at 400 kg/h, and takes
            // about 36 passes to get there, against the 8 loop A needs.
            SolveTwoLoops(0.8, out var returnA, out var returnB);

            Check("loop A return mass flow (kg/h)", returnA.Flow, 100.0, 3.0);
            Check("loop A return temperature (K)", returnA.T, 333.15, 1.0);
            Check("loop B return mass flow (kg/h)", returnB.Flow, 400.0, 5.0);
            Check("loop B return temperature (K)", returnB.T, 353.15, 1.0);
        }

        private struct StreamState
        {
            public double Flow;   // kg/h
            public double T;      // K
        }

        /// <summary>
        /// Two independent copies of the heater loop above. Loop A heats to 60 C, splits in half and
        /// runs on Global Broyden; loop B heats to 80 C, returns <paramref name="recycleRatioB"/> of
        /// its stream and runs on successive substitution, from an estimate far from its answer.
        /// </summary>
        private static void SolveTwoLoops(double recycleRatioB, out StreamState returnA, out StreamState returnB)
        {
            var fs = FS.Create("RecycleBroydenBesideSubstitution")
                       .WithCompound("Water")
                       .WithCompound("Ethanol")
                       .WithPropertyPackage(PropertyPackages.PengRobinson);

            var recA = BuildLoop(fs, "A", 60.0, 0.5, (50.0, 60.0 + 273.15));
            var recB = BuildLoop(fs, "B", 80.0, recycleRatioB, (20.0, 25.0 + 273.15));

            recA.Recycle.AccelerationMethod = AccelMethod.GlobalBroyden;
            recB.Recycle.AccelerationMethod = AccelMethod.None;

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("the two-loop flowsheet did not solve: " +
                                    string.Join("; ", errors.Select(e => e.Message)));

            returnA = Read(recA.Return, "A");
            returnB = Read(recB.Return, "B");
        }

        private static StreamState Read(MaterialStreamBuilder ret, string loop)
        {
            var r = ret.Object;
            var state = new StreamState
            {
                Flow = r.Phases[0].Properties.massflow.GetValueOrDefault() * 3600.0,
                T = r.Phases[0].Properties.temperature.GetValueOrDefault()
            };
            Console.WriteLine($"  loop {loop} return: {state.Flow:F2} kg/h, {state.T:F2} K");
            return state;
        }

        private static (Recycle Recycle, MaterialStreamBuilder Return) BuildLoop(
            FS fs, string loop, double heaterOutletC, double recycleRatio, (double Flow, double T) estimate)
        {
            var feed = fs.AddMaterialStream("FEED-" + loop)
                         .At((25.0 + 273.15).Kelvin(), 200000.0.Pascal())
                         .WithMassFlow((100.0 / 3600.0).KgPerSecond())
                         .WithComposition(c => c.Mass("Water", 0.5).Mass("Ethanol", 0.5));
            var mixed = fs.AddMaterialStream("MIXED-" + loop);
            var hot = fs.AddMaterialStream("HOT-" + loop);
            var prod = fs.AddMaterialStream("PRODUCT-" + loop);
            var tear = fs.AddMaterialStream("TEAR-" + loop);

            var ret = fs.AddMaterialStream("RETURN-" + loop)
                        .At(estimate.T.Kelvin(), 200000.0.Pascal())
                        .WithMassFlow((estimate.Flow / 3600.0).KgPerSecond())
                        .WithComposition(c => c.Mass("Water", 0.5).Mass("Ethanol", 0.5));

            fs.AddMixer("MIX-" + loop).ConnectFeed(feed, 0).ConnectFeed(ret, 1).ConnectProduct(mixed, 0);
            fs.AddHeater("HTR-" + loop).ConnectFeed(mixed, 0).ConnectProduct(hot, 0)
              .WithOutletTemperature((heaterOutletC + 273.15).Kelvin());
            fs.AddSplitter("SPL-" + loop).ConnectFeed(hot, 0).ConnectProduct(prod, 0).ConnectProduct(tear, 1)
              .Configure(s =>
              {
                  s.Ratios.Clear();
                  s.Ratios.Add(1.0 - recycleRatio);
                  s.Ratios.Add(recycleRatio);
                  s.Ratios.Add(0.0);
              });

            var rec = fs.AddUnitOperation(ObjectType.OT_Recycle, "REC-" + loop)
                        .ConnectFeed(tear, 0).ConnectProduct(ret, 0);

            var recycle = (Recycle)rec.Object;
            recycle.MaximumIterations = 100;
            recycle.ConvergenceParameters.Temperatura = 0.01;
            recycle.ConvergenceParameters.Pressao = 1.0;
            recycle.ConvergenceParameters.VazaoMassica = 1.0E-05;

            return (recycle, ret);
        }

        private static void Check(string what, double value, double expected, double tol)
        {
            if (Math.Abs(value - expected) > tol)
                throw new Exception($"{what}: expected {expected} +/- {tol}, got {value}");
        }
    }
}
