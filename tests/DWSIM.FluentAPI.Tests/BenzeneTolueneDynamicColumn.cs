using System;
using System.Linq;
using DWSIM.Automation.DynamicRunner.ColumnInternals;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.UnitOperations.UnitOperations;
using Flowsheet = DWSIM.Automation.FluentAPI.Flowsheet;
using Valve = DWSIM.UnitOperations.UnitOperations.Valve;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The benzene-toluene column the dynamic cases share: the steady state, the product valves, the tray
    /// hydraulics from the column internals rating and the three control loops. The feed-step case and the
    /// startup case build the same column and differ only in what the schedule does to it.
    /// </summary>
    internal sealed class BenzeneTolueneDynamicColumn
    {
        public Flowsheet Fs;
        public DistillationColumnBuilder Column;
        public DistillationColumn Col => Column.Object;
        public MaterialStreamBuilder Feed, Distillate, Bottoms, DistProduct, BotProduct;
        public EnergyStreamBuilder CondDuty, RebDuty;
        public ValveBuilder Lv1, Lv2;
        public PIDControllerBuilder Lc1, Lc2, Pc1;
        public double Diameter, WeirLength, HoleArea;
        /// <summary>Steady-state values: condenser level and sump level as seeded, top pressure, duties.</summary>
        public double Level0, Sump0, PTop0, Qc0, Qr0, FeedKgs;
        /// <summary>Design purities: benzene in the distillate, toluene in the bottoms.</summary>
        public double XdBenzene0, XbToluene0;

        public static BenzeneTolueneDynamicColumn Build(double substeps)
        {
            var c = new BenzeneTolueneDynamicColumn();
            var fs = Flowsheet.Create("BenzeneTolueneColumnDynamics")
                .WithCompounds("Benzene", "Toluene")
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            c.Fs = fs;

            // ---------------------------------------------------------------- steady state
            c.Feed = fs.AddMaterialStream("feed")
                .At(368.15.Kelvin(), 150000.0.Pascal())
                .WithMolarFlow(100.0.MolPerSecond())
                .SetCompoundMolarFlow("Benzene", 50.0)
                .SetCompoundMolarFlow("Toluene", 50.0)
                .AsFlowSpec();

            c.Distillate = fs.AddMaterialStream("distillate");
            c.Bottoms = fs.AddMaterialStream("bottoms");
            c.CondDuty = fs.AddEnergyStream("cond duty");
            c.RebDuty = fs.AddEnergyStream("reb duty");

            c.Column = fs.AddDistillationColumn("T-01")
                .WithNumberOfStages(16)
                .WithFeed(c.Feed, 8)
                .WithDistillate(c.Distillate)
                .WithBottoms(c.Bottoms)
                .WithCondenserDuty(c.CondDuty)
                .WithReboilerDuty(c.RebDuty)
                .WithCondenserSpec("Reflux Ratio", 2.5, "")
                .WithReboilerSpec("Product Molar Flow Rate", 50.0, "mol/s")
                .WithTopPressure(120000.0.Pascal())
                .WithColumnPressureDrop(10000.0.Pascal());

            // Product valves: Kv mode so each valve computes its own flow from the pressure either side of
            // it, which is what the level controllers move. Downstream the products are pressure boundaries.
            c.DistProduct = fs.AddMaterialStream("distillate product").At(353.15.Kelvin(), 90000.0.Pascal()).AsPressureSpec();
            c.BotProduct = fs.AddMaterialStream("bottoms product").At(383.15.Kelvin(), 90000.0.Pascal()).AsPressureSpec();

            c.Lv1 = fs.AddValve("LV-01")
                .WithCalcMode(Valve.CalculationMode.Kv_Liquid)
                .WithKv(150.0)
                .WithOpeningKvRelationship()
                .WithOpeningPercent(50.0)
                .WithOpeningSetpoint(50.0)
                .ConnectFeed(c.Distillate, 0)
                .ConnectProduct(c.DistProduct, 0);

            c.Lv2 = fs.AddValve("LV-02")
                .WithCalcMode(Valve.CalculationMode.Kv_Liquid)
                .WithKv(150.0)
                .WithOpeningKvRelationship()
                .WithOpeningPercent(50.0)
                .WithOpeningSetpoint(50.0)
                .ConnectFeed(c.Bottoms, 0)
                .ConnectProduct(c.BotProduct, 0);

            fs.AutoLayout();
            fs.Solve();

            // The steady state hands each product boundary the pressure the half-open valve produces at the
            // design rate (about 1.2 bar), not the 0.9 bar asked for; a smaller Kv is no answer, because the
            // saturated distillate then chokes the steady-state valve. The boundaries are put back at 0.9 bar
            // for the dynamic run, where the valves pass what the pressure difference gives (about 20 % open
            // at the design rate), and a column starting up at atmospheric pressure can push its bottoms out.
            SetProductBoundaries(c);
            Console.WriteLine($"product boundaries: distillate {c.Distillate.Object.GetPressure():F0} -> {c.DistProduct.Object.GetPressure():F0} Pa, bottoms {c.Bottoms.Object.GetPressure():F0} -> {c.BotProduct.Object.GetPressure():F0} Pa");

            var col = c.Col;
            foreach (var si in col.MaterialStreams.Values)
                Console.WriteLine($"  registered material stream {si.StreamID} ({fs.Inner.SimulationObjects[si.StreamID].GraphicObject.Tag}): type {si.StreamType}, behaviour {si.StreamBehavior}, stage '{si.AssociatedStage}'");
            foreach (var si in col.EnergyStreams.Values)
                Console.WriteLine($"  registered energy stream {si.StreamID}: type {si.StreamType}, behaviour {si.StreamBehavior}, stage '{si.AssociatedStage}'");
            Console.WriteLine($"steady state: Qc = {c.CondDuty.EnergyFlowKW:F1} kW, Qr = {c.RebDuty.EnergyFlowKW:F1} kW, D = {c.Distillate.MolarFlowMolPerSecond:F2} mol/s, B = {c.Bottoms.MolarFlowMolPerSecond:F2} mol/s");
            Console.WriteLine($"distillate benzene {c.Distillate.OverallMoleFraction("Benzene"):F4}, bottoms toluene {c.Bottoms.OverallMoleFraction("Toluene"):F4}");
            Console.WriteLine($"distillate P {c.Distillate.Object.GetPressure():F0} Pa -> {c.DistProduct.Object.GetPressure():F0} Pa; bottoms P {c.Bottoms.Object.GetPressure():F0} Pa -> {c.BotProduct.Object.GetPressure():F0} Pa");

            // ---------------------------------------------------------------- tray hydraulics from the rating
            var input = new ColumnInternalsInput { ColumnName = "T-01", TargetFloodFractionTrays = 0.75 };
            input.Sections.Add(new InternalsSection
            {
                Name = "Trays", FromStage = 2, ToStage = 15, Type = InternalType.SieveTray, Diameter = 0.0,
                TraySpacing = 0.5, DowncomerAreaFraction = 0.12, WeirHeight = 0.05, HoleAreaFraction = 0.10
            });
            var rating = ColumnInternalsStudy.Run(fs.Inner, input);
            var D = rating.Sections.SelectMany(sec => sec.Stages).Where(r => r.Diameter > 0).Select(r => r.Diameter).DefaultIfEmpty(0.0).Max();
            if (D <= 0) throw new Exception("the rating gave no diameter");
            var area = Math.PI * D * D / 4.0;
            c.Diameter = D;
            c.WeirLength = 0.73 * D;                 // chord of a 12 % downcomer segment
            c.HoleArea = 0.10 * 0.76 * area;         // 10 % of the active area (two 12 % downcomers)
            Console.WriteLine($"rated diameter {D:F2} m, weir {c.WeirLength:F2} m, hole area {c.HoleArea:F3} m2");

            col.EstimatedDiameter = D;
            col.TraySpacing = 0.5;
            col.BottomSpacing = 2.0;   // the condenser stage doubles as the reflux drum: 2 m of vessel
            col.TopSpacing = 1.0;      // the sump: the reboiler stage height plus this
            for (int i = 0; i < col.Stages.Count; i++)
            {
                var st = col.Stages[i];
                st.DowncomerLength = c.WeirLength;
                st.TotalHoleArea = c.HoleArea;
                st.StageHeight = 0.5;
                st.DowncomerHeight = 0.05;
            }
            col.Stages[0].DowncomerHeight = 1.0;                    // reflux drum: the weir relation holds ~0.9 m of liquid
            col.Stages[0].DowncomerLength = 0.05;                   // a reflux line, not a weir: the reflux answers the drum level gently
            col.Stages[col.Stages.Count - 1].StageHeight = 1.0;     // reboiler stage
            col.Stages[col.Stages.Count - 1].DowncomerHeight = 0.5;
            c.Column.WithDynamicProperty("Quasi-Steady Vapor", true)
                    .WithDynamicProperty("Calibrate Tray Coefficients", true)
                    .WithDynamicProperty("Souders-Brown Coefficient", 0.05)
                    .WithDynamicProperty("Time step discretization", substeps);

            // the steady-state seeding gives the operating levels the controllers will hold
            col.InitializeDynamicsFromSteadyStateSolution();
            c.Level0 = col.Stages[0].LiquidLevel;
            c.Sump0 = col.BottomLiquidLevel;
            c.PTop0 = col.Stages[0].P;
            c.Qc0 = c.CondDuty.EnergyFlowKW;
            c.Qr0 = c.RebDuty.EnergyFlowKW;
            c.FeedKgs = c.Feed.Object.GetMassFlow();
            c.XdBenzene0 = c.Distillate.OverallMoleFraction("Benzene");
            c.XbToluene0 = c.Bottoms.OverallMoleFraction("Toluene");
            Console.WriteLine($"initial: condenser level {c.Level0:F3} m, sump level {c.Sump0:F3} m, top P {c.PTop0:F0} Pa, Qc {c.Qc0:F1} kW, Qr {c.Qr0:F1} kW");
            for (int i = 0; i < col.Stages.Count; i++)
                Console.WriteLine($"  stage {i + 1}: level {col.Stages[i].LiquidLevel:F4} m, K {col.Stages[i].DryTrayPressureDropCoefficient:E2}, P {col.Stages[i].P:F0} Pa, T {col.Stages[i].T - 273.15:F1} C");

            // ---------------------------------------------------------------- controllers
            c.Lc1 = fs.AddPIDController("LC-01")
                .Controls("T-01", "Stage_LiquidLevel_1", "m")
                .Manipulates("LV-01", "PROP_VA_5", "")
                .WithSetPoint(c.Level0)
                .WithTuning(1.5, 0.01, 0.0)
                .WithOutputLimits(0.0, 100.0)
                .WithOffset(50.0)
                .ReverseActing(true)
                .Configure(o => o.ManipulatedVariableSpan = 100.0);

            c.Lc2 = fs.AddPIDController("LC-02")
                .Controls("T-01", "Sump_LiquidLevel", "m")
                .Manipulates("LV-02", "PROP_VA_5", "")
                .WithSetPoint(c.Sump0)
                .WithTuning(2.0, 0.02, 0.0)
                .WithOutputLimits(0.0, 100.0)
                .WithOffset(50.0)
                .ReverseActing(true)
                .Configure(o => o.ManipulatedVariableSpan = 100.0);

            c.Pc1 = fs.AddPIDController("PC-01")
                .Controls("T-01", "Stage_Pressure_1", "Pa")
                .Manipulates("cond duty", "PROP_ES_0", "kW")
                .WithSetPoint(c.PTop0)
                .WithTuning(2.0, 0.02, 0.0)
                .WithOutputLimits(0.2 * c.Qc0, 3.0 * c.Qc0)   // the duty stream carries the heat removed, positive; the cooling water never fully closes
                .WithOffset(c.Qc0)
                .ReverseActing(true)                  // pressure up: remove more heat
                .Configure(o => { o.ManipulatedVariableSpan = c.Qc0; o.WindupGuard = 100.0; });   // the integral may hold the duty at its minimum through a long wait

            fs.AddIndicator("LI-01", IndicatorKind.Level).Reads("T-01", "Stage_LiquidLevel_1").WithRange(0.0, 2.0);
            fs.AddIndicator("LI-02", IndicatorKind.Level).Reads("T-01", "Sump_LiquidLevel").WithRange(0.0, 2.0);
            fs.AddIndicator("PI-01", IndicatorKind.Analog).Reads("T-01", "Stage_Pressure_1").WithRange(100000.0, 150000.0);
            fs.AddIndicator("TI-08", IndicatorKind.Analog).Reads("T-01", "Stage_Temperature_8").WithRange(350.0, 400.0);

            return c;
        }

        /// <summary>The product boundaries at 0.9 bar (the steady-state valve calculation leaves them where the
        /// half-open valve puts them); call again after a steady-state solve.</summary>
        public static void SetProductBoundaries(BenzeneTolueneDynamicColumn c)
        {
            c.DistProduct.Object.SetPressure(90000.0);
            c.BotProduct.Object.SetPressure(90000.0);
        }

        /// <summary>The feed pressure of the case. The dynamic run leaves the feed stream at the pressure of its
        /// stage (about 1.25 bar); call before the steady-state solve that prepares the file.</summary>
        public static void RestoreFeedPressure(BenzeneTolueneDynamicColumn c)
        {
            c.Feed.Object.SetPressure(150000.0);
        }

        /// <summary>The diameter the tray hydraulics were sized for. Every steady-state solve of the column
        /// re-estimates its diameter from a flooding correlation (2.78 m here), and the dynamic model reads that
        /// field for the tray areas and the drum and sump volumes; call after the solve that prepares the file.</summary>
        public static void RestoreRatedDiameter(BenzeneTolueneDynamicColumn c)
        {
            c.Col.EstimatedDiameter = c.Diameter;
        }

        /// <summary>The variables every dynamic case of this column records.</summary>
        public static readonly string[] MonitorNames =
        {
            "condenser level", "sump level", "feed tray level", "reboiler tray level", "top pressure", "bottom pressure",
            "condenser temperature", "feed tray temperature", "reboiler temperature", "distillate valve opening",
            "bottoms valve opening", "condenser duty", "reboiler duty", "feed mass flow", "distillate mass flow", "bottoms mass flow",
            "distillate benzene fraction", "bottoms toluene fraction", "vapor to condenser", "reflux",
            "distillate draw", "bottoms draw", "bottoms pressure"
        };

        public IntegratorBuilder DefineIntegrator(string name, double step, double duration)
        {
            return Fs.Dynamics.DefineIntegrator(name)
                .WithIntegrationStep(step.Seconds())
                .WithDuration(duration.Seconds())
                .Monitor("T-01", "Stage_LiquidLevel_1", "m", "condenser level")
                .Monitor("T-01", "Sump_LiquidLevel", "m", "sump level")
                .Monitor("T-01", "Stage_LiquidLevel_8", "m", "feed tray level")
                .Monitor("T-01", "Stage_LiquidLevel_16", "m", "reboiler tray level")
                .Monitor("T-01", "Stage_Pressure_1", "Pa", "top pressure")
                .Monitor("T-01", "Stage_Pressure_16", "Pa", "bottom pressure")
                .Monitor("T-01", "Stage_Temperature_1", "K", "condenser temperature")
                .Monitor("T-01", "Stage_Temperature_8", "K", "feed tray temperature")
                .Monitor("T-01", "Stage_Temperature_16", "K", "reboiler temperature")
                .Monitor("LV-01", "PROP_VA_5", "", "distillate valve opening")
                .Monitor("LV-02", "PROP_VA_5", "", "bottoms valve opening")
                .Monitor("cond duty", "PROP_ES_0", "kW", "condenser duty")
                .Monitor("reb duty", "PROP_ES_0", "kW", "reboiler duty")
                .Monitor("feed", "PROP_MS_2", "kg/s", "feed mass flow")
                .Monitor("distillate product", "PROP_MS_2", "kg/s", "distillate mass flow")
                .Monitor("bottoms product", "PROP_MS_2", "kg/s", "bottoms mass flow")
                .Monitor("distillate product", "PROP_MS_102/Benzene", "", "distillate benzene fraction")
                .Monitor("bottoms product", "PROP_MS_102/Toluene", "", "bottoms toluene fraction")
                .Monitor("T-01", "Stage_VaporFlow_2", "mol/s", "vapor to condenser")
                .Monitor("T-01", "Stage_LiquidFlow_1", "mol/s", "reflux")
                .Monitor("distillate", "PROP_MS_2", "kg/s", "distillate draw")
                .Monitor("bottoms", "PROP_MS_2", "kg/s", "bottoms draw")
                .Monitor("bottoms", "PROP_MS_1", "Pa", "bottoms pressure");
        }
    }
}
