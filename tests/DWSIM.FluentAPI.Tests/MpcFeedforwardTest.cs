//    Measured disturbances as feedforward in the MPC controller.
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.UnitOperations.SpecialOps;
using Valve = DWSIM.UnitOperations.UnitOperations.Valve;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The MPC controller's measured disturbances. A disturbance model makes the change of a measured
    /// disturbance enter the predicted trajectory the way a manipulated variable move does, so the
    /// controller acts on a feed step before the level it controls has moved.
    /// </summary>
    /// <remarks>
    /// The level loops are the API reference example: a tank fed through a valve, its level held at
    /// 1 m by the outlet valve, and a feed step from 10 to 15 kg/s. On a single tank the feedforward
    /// adds little: the level integrates the feed at once, and the controller's ramp extrapolation of
    /// the model error already sees the new slope one sample after the step, as the disturbance model
    /// does. Put a second tank in front and the feed reaches the controlled tank through a lag; there
    /// the disturbance model lets the controller open the valve while the level has not yet moved.
    /// </remarks>
    internal static class MpcFeedforwardTest
    {
        private const double StepSeconds = 600.0;
        private const double TwoTankStepSeconds = 1500.0;

        /// <summary>
        /// Disturbances without a model, and disturbance models switched off, leave the controller
        /// moving exactly as it does with no disturbances at all.
        /// </summary>
        public static void RunCompatibility()
        {
            var reference = SingleTank(dv: Feedforward.None);
            var measuredOnly = SingleTank(dv: Feedforward.MeasuredOnly);
            var switchedOff = SingleTank(dv: Feedforward.SwitchedOff);

            AssertIdentical("a disturbance without a model", reference, measuredOnly);
            AssertIdentical("disturbance models switched off", reference, switchedOff);

            if (measuredOnly.Mpc.DVHistory.Count != measuredOnly.Mpc.CVHistory.Count)
                throw new Exception("The controller did not record the disturbance at each control step: " +
                    measuredOnly.Mpc.DVHistory.Count + " readings for " + measuredOnly.Mpc.CVHistory.Count + " steps.");
            if (reference.Mpc.DVHistory.Count != 0)
                throw new Exception("A controller without disturbances recorded disturbance readings.");

            var feedAfter = measuredOnly.Mpc.DVHistory.Last()[0];
            if (Math.Abs(feedAfter - 15.0) > 1e-6)
                throw new Exception("The disturbance was read as " + feedAfter + " kg/s after the feed step to 15 kg/s.");

            Console.WriteLine("Same moves with no disturbance, an unmodelled one and switched-off models: " +
                reference.Level.Count + " samples.");
        }

        /// <summary>
        /// One tank: the feed integrates into the level at once, so the feedforward and the ramp
        /// extrapolation of the model error see the same slope, and the level follows nearly the same path.
        /// </summary>
        public static void RunSingleTank()
        {
            var without = SingleTank(dv: Feedforward.None);
            var with = SingleTank(dv: Feedforward.Modelled);

            var peakWithout = Peak(without.Level, StepSeconds);
            var peakWith = Peak(with.Level, StepSeconds);
            var largest = without.Level.Values.Zip(with.Level.Values, (a, b) => Math.Abs(a - b)).Max();

            Console.WriteLine($"One tank: peak {peakWithout:F4} m without the disturbance model, {peakWith:F4} m with it; " +
                $"largest difference between the two runs {largest:G3} m; final {without.Level.Final:F4} and {with.Level.Final:F4} m.");

            new ResultTable("MPC feedforward, one tank")
                .Row("peak with the model, against without", peakWithout, peakWith, 0.05, "m")
                .RowInRange("final level with the model", 0.99, 1.01, with.Level.Final, "m")
                .PrintAndThrowIfFailed();
        }

        /// <summary>
        /// Two tanks in series: the feed reaches the controlled tank through the first tank's lag, and
        /// the disturbance model, an integrator behind that lag, keeps the level much closer to its target.
        /// </summary>
        public static void RunTwoTanks()
        {
            var without = TwoTanks(feedforward: false, gainFactor: 1.0);
            var with = TwoTanks(feedforward: true, gainFactor: 1.0);
            var mismatched = TwoTanks(feedforward: true, gainFactor: 1.3);

            var peakWithout = Peak(without, TwoTankStepSeconds);
            var peakWith = Peak(with, TwoTankStepSeconds);
            var peakMismatched = Peak(mismatched, TwoTankStepSeconds);
            var iaeWithout = Iae(without, TwoTankStepSeconds);
            var iaeWith = Iae(with, TwoTankStepSeconds);
            var iaeMismatched = Iae(mismatched, TwoTankStepSeconds);

            Console.WriteLine($"Two tanks: peak deviation {peakWithout:F4} m without the disturbance model, {peakWith:F4} m with it, " +
                $"{peakMismatched:F4} m with a gain 30 % high; IAE {iaeWithout:F2}, {iaeWith:F2} and {iaeMismatched:F2} m.s.");

            new ResultTable("MPC feedforward, two tanks")
                .RowInRange("peak with the model / without", 0.0, 0.5, peakWith / peakWithout)
                .RowInRange("IAE with the model / without", 0.0, 0.5, iaeWith / iaeWithout)
                .RowInRange("peak with a gain 30 % high / without", 0.0, 0.85, peakMismatched / peakWithout)
                .RowInRange("IAE with a gain 30 % high / without", 0.0, 0.85, iaeMismatched / iaeWithout)
                .RowInRange("final level with the model", 0.99, 1.01, with.Final, "m")
                .PrintAndThrowIfFailed();
        }

        /// <summary>
        /// The disturbance models, their indices and the switch survive SaveData/LoadData, CloneXML and a
        /// saved file; data written before the disturbance models existed loads with none.
        /// </summary>
        public static void RunSaveAndLoad()
        {
            var fs = Loop("MPCSave", minutes: 1.0, feedStep: false);
            var mpc = fs.AddMPCController("LC-MPC")
                .Controls("T-01", "Liquid Level", "m", 0.9, 1.1)
                .Manipulates("V-02", "PROP_VA_5", "", 0.0, 100.0)
                .Measures("Inlet", "PROP_MS_2", "kg/s")
                .WithIntegratingModel(0, 0, -0.0004)
                .WithIntegratingDisturbanceModel(0, 0, 0.001, 30.0.Seconds(), 5.0.Seconds())
                .WithFirstOrderDisturbanceModel(0, 0, 0.25, 60.0.Seconds())
                .UseMeasuredDisturbances(false);

            void Check(string how, MPCController copy)
            {
                if (copy.DisturbanceVariables.Count != 1 || copy.DisturbanceVariables[0].PropertyName != "PROP_MS_2" ||
                    copy.DisturbanceVariables[0].Units != "kg/s")
                    throw new Exception(how + ": the disturbance variable was not kept.");
                if (copy.DisturbanceModels.Count != 2)
                    throw new Exception(how + ": " + copy.DisturbanceModels.Count + " disturbance models instead of 2.");
                var a = copy.DisturbanceModels[0];
                var b = copy.DisturbanceModels[1];
                if (a.CVIndex != 0 || a.DVIndex != 0 || a.Gain != 0.001 || a.TimeConstant != 30.0 || a.DeadTime != 5.0 || !a.Integrating)
                    throw new Exception(how + ": the integrating disturbance model changed.");
                if (b.Gain != 0.25 || b.TimeConstant != 60.0 || b.DeadTime != 0.0 || b.Integrating)
                    throw new Exception(how + ": the first order disturbance model changed.");
                if (copy.StepResponseModels.Count != 1 || copy.StepResponseModels[0].Gain != -0.0004 || !copy.StepResponseModels[0].Integrating)
                    throw new Exception(how + ": the step response model changed.");
                if (copy.UseMeasuredDisturbances)
                    throw new Exception(how + ": Use Measured Disturbances came back on.");
            }

            var loaded = new MPCController();
            loaded.LoadData(mpc.Object.SaveData());
            Check("SaveData/LoadData", loaded);
            Check("CloneXML", (MPCController)mpc.Object.CloneXML());

            var path = Path.Combine(Path.GetTempPath(), "dwsim-mpc-feedforward-" + Guid.NewGuid().ToString("N") + ".dwxmz");
            try
            {
                fs.Save(path);
                var reopened = Flowsheet.Load(path);
                var copy = reopened.Inner.SimulationObjects.Values.OfType<MPCController>().Single();
                Check("saved file", copy);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }

            // what an earlier version wrote: no disturbance models, no Integrating attribute
            var old = mpc.Object.SaveData()
                .Where(x => x.Name != "MPCDisturbanceModels" && x.Name != "UseMeasuredDisturbances").ToList();
            foreach (var model in old.Single(x => x.Name == "MPCStepResponseModels").Elements("Model"))
                model.Attribute("Integrating")?.Remove();
            var fromOld = new MPCController();
            fromOld.LoadData(old);
            if (fromOld.DisturbanceModels.Count != 0 || !fromOld.UseMeasuredDisturbances)
                throw new Exception("Data without disturbance models did not load with none, switched on.");
            if (fromOld.StepResponseModels.Count != 1 || fromOld.StepResponseModels[0].Integrating ||
                fromOld.StepResponseModels[0].Gain != -0.0004)
                throw new Exception("A step response model without the Integrating attribute did not load as a self-regulating one.");

            Console.WriteLine("Disturbance models kept through SaveData/LoadData, CloneXML and a saved file.");
        }

        /// <summary>
        /// A variable whose object is gone reads as NaN: a disturbance like that brings no move, and a
        /// controlled variable like that keeps the controller from moving anything.
        /// </summary>
        public static void RunMissingObjects()
        {
            var fs = Loop("MPCMissing", minutes: 20.0, feedStep: true);
            var mpc = fs.AddMPCController("LC-MPC")
                .Controls("T-01", "Liquid Level", "m", 0.9, 1.1)
                .Manipulates("V-02", "PROP_VA_5", "", 0.0, 100.0)
                .Measures("Inlet", "PROP_MS_2", "kg/s")
                .WithIntegratingModel(0, 0, -0.0004)
                .WithIntegratingDisturbanceModel(0, 0, 0.001)
                .WithSampleTime(5.0.Seconds())
                .WithHorizons(30, 5)
                .WithMoveSuppression(0.1);
            mpc.Object.DisturbanceVariables[0].ObjectID = "no such object";

            if (!double.IsNaN(mpc.DisturbanceValue(0)))
                throw new Exception("A disturbance whose object is gone read " + mpc.DisturbanceValue(0) + " instead of NaN.");

            var result = fs.RunDynamics("Run").Execute();
            if (result.Errors.Count > 0) throw new Exception(string.Join("; ", result.Errors.Select(e => e.Message)));
            var reference = SingleTank(dv: Feedforward.None);
            AssertIdentical("a disturbance whose object is gone", reference, new Run(result, mpc.Object));

            var fs2 = Loop("MPCMissingCV", minutes: 1.0, feedStep: false);
            var mpc2 = fs2.AddMPCController("LC-MPC")
                .Controls("T-01", "Liquid Level", "m", 0.9, 1.1)
                .Manipulates("V-02", "PROP_VA_5", "", 0.0, 100.0)
                .WithIntegratingModel(0, 0, -0.0004)
                .WithSampleTime(5.0.Seconds());
            mpc2.Object.ControlledVariables[0].ObjectID = "no such object";
            var result2 = fs2.RunDynamics("Run").Execute();
            if (result2.Errors.Count > 0) throw new Exception(string.Join("; ", result2.Errors.Select(e => e.Message)));
            var opening = result2.GetSeries("opening");
            if (opening.Values.Any(v => Math.Abs(v - 29.0) > 1e-9) || mpc2.Object.CVHistory.Count != 0)
                throw new Exception("The controller moved the valve with a controlled variable it cannot read.");

            Console.WriteLine("A disturbance that cannot be read brings no move; a controlled variable that cannot be read stops the controller.");
        }

        // ------------------------------------------------------------------ flowsheets

        private enum Feedforward { None, MeasuredOnly, SwitchedOff, Modelled }

        private sealed class Run
        {
            public Run(DynamicsResult result, MPCController mpc)
            {
                Level = result.GetSeries("level");
                Opening = result.GetSeries("opening");
                Mpc = mpc;
            }

            public DynamicsSeries Level { get; }
            public DynamicsSeries Opening { get; }
            public MPCController Mpc { get; }
        }

        /// <summary>The API reference example: feed valve, tank, outlet valve, 5 s steps.</summary>
        private static Flowsheet Loop(string name, double minutes, bool feedStep)
        {
            var fs = Flowsheet.Create(name).WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var inlet = fs.AddMaterialStream("Inlet").At(25.0.Celsius(), 130000.0.Pascal())
                .WithMassFlow(10.0.KgPerSecond()).AsFlowSpec();
            var s1 = fs.AddMaterialStream("S-1").AsPressureSpec();
            var s2 = fs.AddMaterialStream("S-2").AsPressureSpec();
            var outlet = fs.AddMaterialStream("Outlet").AsPressureSpec();
            fs.AddValve("V-01").WithCalcMode(Valve.CalculationMode.Kv_Liquid).WithKv(100.0)
                .WithOpeningPercent(50.0).ConnectFeed(inlet).ConnectProduct(s1);
            fs.AddTank("T-01").WithVolume(2.0.CubicMeters()).WithHeight(2.0.Meters())
                .ConnectFeed(s1).ConnectProduct(s2);
            fs.AddValve("V-02").WithCalcMode(Valve.CalculationMode.Kv_Liquid).WithKv(400.0)
                .WithOpeningKvRelationship().WithOpeningPercent(29.0)
                .ConnectFeed(s2).ConnectProduct(outlet);
            fs.Solve();
            outlet.Object.Phases[0].Properties.pressure = 101325.0;
            Schedule(fs, minutes, feedStep ? StepSeconds : (double?)null);
            return fs;
        }

        private static void Schedule(Flowsheet fs, double minutes, double? stepAt)
        {
            fs.Dynamics.DefineIntegrator("Int1")
                .WithIntegrationStep(5.0.Seconds()).WithDuration((minutes * 60.0).Seconds())
                .Monitor("T-01", "Liquid Level", "m", "level")
                .Monitor("V-02", "PROP_VA_5", "", "opening");
            var schedule = fs.Dynamics.DefineSchedule("Run").WithIntegrator("Int1").UseCurrentStateAsInitial(true);
            if (stepAt.HasValue)
            {
                fs.Dynamics.DefineEventSet("FeedStep")
                    .AddStepChange("Inlet", "PROP_MS_2", 15.0, stepAt.Value.Seconds(), "kg/s", "feed 10 -> 15 kg/s");
                schedule.WithEventSet("FeedStep");
            }
            schedule.MakeCurrent();
        }

        private static Run SingleTank(Feedforward dv)
        {
            var fs = Loop("MPCFeedforward", minutes: 20.0, feedStep: true);
            var mpc = fs.AddMPCController("LC-MPC")
                .Controls("T-01", "Liquid Level", "m", 0.9, 1.1)
                .Manipulates("V-02", "PROP_VA_5", "", 0.0, 100.0)
                .WithIntegratingModel(0, 0, -0.0004)
                .WithSampleTime(5.0.Seconds())
                .WithHorizons(30, 5)
                .WithMoveSuppression(0.1);
            if (dv != Feedforward.None) mpc.Measures("Inlet", "PROP_MS_2", "kg/s");
            // 1 kg/s more feed raises the level of a 1 m2 tank by 1 / density m/s
            if (dv == Feedforward.Modelled || dv == Feedforward.SwitchedOff) mpc.WithIntegratingDisturbanceModel(0, 0, 1.0 / 997.0);
            if (dv == Feedforward.SwitchedOff) mpc.UseMeasuredDisturbances(false);

            var result = fs.RunDynamics("Run").Execute();
            if (result.Errors.Count > 0) throw new Exception(string.Join("; ", result.Errors.Select(e => e.Message)));
            return new Run(result, mpc.Object);
        }

        /// <summary>
        /// A first tank in front of the controlled one: its outlet valve is fixed, so its outflow
        /// follows the feed through a first order lag.
        /// </summary>
        private static DynamicsSeries TwoTanks(bool feedforward, double gainFactor)
        {
            var fs = Flowsheet.Create("MPCTwoTanks").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var inlet = fs.AddMaterialStream("Inlet").At(25.0.Celsius(), 130000.0.Pascal())
                .WithMassFlow(10.0.KgPerSecond()).AsFlowSpec();
            var s1 = fs.AddMaterialStream("S-1").AsPressureSpec();
            var sa = fs.AddMaterialStream("S-A").AsPressureSpec();
            var sb = fs.AddMaterialStream("S-B").AsPressureSpec();
            var s2 = fs.AddMaterialStream("S-2").AsPressureSpec();
            var outlet = fs.AddMaterialStream("Outlet").AsPressureSpec();
            fs.AddValve("V-01").WithCalcMode(Valve.CalculationMode.Kv_Liquid).WithKv(100.0)
                .WithOpeningPercent(50.0).ConnectFeed(inlet).ConnectProduct(s1);
            fs.AddTank("T-00").WithVolume(2.0.CubicMeters()).WithHeight(2.0.Meters())
                .ConnectFeed(s1).ConnectProduct(sa);
            fs.AddValve("V-00").WithCalcMode(Valve.CalculationMode.Kv_Liquid).WithKv(TwoTankKv)
                .WithOpeningKvRelationship().WithOpeningPercent(50.0)
                .ConnectFeed(sa).ConnectProduct(sb);
            fs.AddTank("T-01").WithVolume(2.0.CubicMeters()).WithHeight(2.0.Meters())
                .ConnectFeed(sb).ConnectProduct(s2);
            fs.AddValve("V-02").WithCalcMode(Valve.CalculationMode.Kv_Liquid).WithKv(400.0)
                .WithOpeningKvRelationship().WithOpeningPercent(29.0)
                .ConnectFeed(s2).ConnectProduct(outlet);
            fs.Solve();
            outlet.Object.Phases[0].Properties.pressure = 101325.0;

            fs.Dynamics.DefineIntegrator("Int1")
                .WithIntegrationStep(5.0.Seconds()).WithDuration((TwoTankStepSeconds + 1200.0).Seconds())
                .Monitor("T-01", "Liquid Level", "m", "level")
                .Monitor("V-02", "PROP_VA_5", "", "opening");
            fs.Dynamics.DefineEventSet("FeedStep")
                .AddStepChange("Inlet", "PROP_MS_2", 15.0, TwoTankStepSeconds.Seconds(), "kg/s", "feed 10 -> 15 kg/s");
            fs.Dynamics.DefineSchedule("Run").WithIntegrator("Int1").WithEventSet("FeedStep")
                .UseCurrentStateAsInitial(true).MakeCurrent();

            var mpc = fs.AddMPCController("LC-MPC")
                .Controls("T-01", "Liquid Level", "m", 0.9, 1.1)
                .Manipulates("V-02", "PROP_VA_5", "", 0.0, 100.0)
                .Measures("Inlet", "PROP_MS_2", "kg/s")
                .WithIntegratingModel(0, 0, -0.0004)
                .WithSampleTime(5.0.Seconds())
                .WithHorizons(30, 5)
                .WithMoveSuppression(0.1);
            // the feed reaches T-01 through T-00, a lag of about TwoTankLag seconds
            if (feedforward)
                mpc.WithIntegratingDisturbanceModel(0, 0, gainFactor / 997.0, TwoTankLag.Seconds());

            var result = fs.RunDynamics("Run").Execute();
            if (result.Errors.Count > 0) throw new Exception(string.Join("; ", result.Errors.Select(e => e.Message)));
            return result.GetSeries("level");
        }

        /// <summary>Kv of the first tank's outlet valve, fully open.</summary>
        private const double TwoTankKv = 330.0;

        /// <summary>
        /// Time constant of the first tank, from an open-loop run of this flowsheet: its outflow covers
        /// 63 % of the feed step in about 115 s.
        /// </summary>
        private const double TwoTankLag = 115.0;

        // ------------------------------------------------------------------ measures

        private static double Peak(Run run, double stepAt) => Peak(run.Level, stepAt);

        private static double Peak(DynamicsSeries level, double stepAt)
        {
            var peak = 0.0;
            for (var i = 0; i < level.Count; i++)
                if (level.TimeSeconds[i] >= stepAt) peak = Math.Max(peak, Math.Abs(level.Values[i] - 1.0));
            return peak;
        }

        private static double Iae(DynamicsSeries level, double stepAt)
        {
            var iae = 0.0;
            for (var i = 1; i < level.Count; i++)
                if (level.TimeSeconds[i] >= stepAt)
                    iae += Math.Abs(level.Values[i] - 1.0) * (level.TimeSeconds[i] - level.TimeSeconds[i - 1]);
            return iae;
        }

        private static void AssertIdentical(string what, Run expected, Run actual)
        {
            void Same(string name, DynamicsSeries a, DynamicsSeries b)
            {
                if (a.Count != b.Count)
                    throw new Exception(what + ": " + b.Count + " " + name + " samples instead of " + a.Count + ".");
                for (var i = 0; i < a.Count; i++)
                {
                    // bit for bit: the disturbance must not change a single move
                    if (a.Values[i].Equals(b.Values[i])) continue;
                    throw new Exception(what + ": the " + name + " at t = " + a.TimeSeconds[i] + " s is " +
                        b.Values[i].ToString("R") + " instead of " + a.Values[i].ToString("R") + ".");
                }
            }

            Same("level", expected.Level, actual.Level);
            Same("opening", expected.Opening, actual.Opening);
        }
    }
}
