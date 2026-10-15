using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using DWSIM.Thermodynamics.Utilities.PetroleumProperties;
using Methods = DWSIM.Thermodynamics.Utilities.PetroleumCharacterization.Methods;
using SecSpec = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSecondarySpec;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Fractionation of a straight-run distillate (the part of a medium crude that boils up to 420 C) into
    /// naphtha, kerosene, diesel and a light gas oil. The distillate is cut into pseudocomponents from the
    /// crude's TBP curve. A distillation curves utility on each product reports its TBP and D86 curves, flash
    /// point, density and cetane index. The second case converges the tower on the D86 95 % points of the
    /// kerosene and the diesel, moving the two side draw rates.
    /// </summary>
    internal static class DistillateFractionationSample
    {
        // crude TBP, vol % against C
        private static readonly double[] CrudeVol = { 0, 5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 95, 100 };
        private static readonly double[] CrudeTbpC = { 36, 75, 110, 160, 210, 262, 312, 362, 420, 490, 580, 650, 760 };
        // pseudocomponent cut points of the distillate, C
        private static readonly double[] Cuts = { 36, 80, 120, 160, 200, 240, 280, 320, 360, 420 };
        private const double WatsonK = 11.8;

        private sealed class Unit
        {
            public Flowsheet Fs = null!;
            public MaterialStreamBuilder Feed = null!, Naphtha = null!, Kerosene = null!, Diesel = null!, Gasoil = null!;
            public DistillationColumnBuilder Tower = null!;
            public Dictionary<string, double> NbpC = new();
        }

        private static double VolAt(double tC)
        {
            for (int i = 1; i < CrudeTbpC.Length; i++)
                if (tC <= CrudeTbpC[i])
                    return CrudeVol[i - 1] + (CrudeVol[i] - CrudeVol[i - 1]) * (tC - CrudeTbpC[i - 1]) / (CrudeTbpC[i] - CrudeTbpC[i - 1]);
            return 100.0;
        }

        private static Unit Build(string name)
        {
            var u = new Unit();
            var fs = Flowsheet.Create(name);

            var names = new List<string>();
            var vol = new List<double>();
            var sg = new List<double>();
            for (int i = 0; i < Cuts.Length - 1; i++)
            {
                double v = VolAt(Cuts[i + 1]) - VolAt(Cuts[i]);
                // the cut boils at the TBP of the middle of its volume; SG from a constant Watson K
                double mid = (VolAt(Cuts[i + 1]) + VolAt(Cuts[i])) / 2;
                double tbC = DistillationCurves.InterpolateMonotone(CrudeVol, CrudeTbpC, mid);
                double tb = tbC + 273.15;
                double s = Math.Pow(1.8 * tb, 1.0 / 3.0) / WatsonK;
                var cut = $"NBP {tbC:F0}C";
                fs = fs.WithPseudoComponent(cut, tb.Kelvin(), s, Methods.PropertyMethods.MW_Riazi(tb, s));
                names.Add(cut); vol.Add(v); sg.Add(s);
                u.NbpC[cut] = tbC;
            }
            // viscosities as the characterization sets them (Abbott)
            foreach (var cp in fs.Inner.SelectedCompounds.Values.Cast<DWSIM.Thermodynamics.BaseClasses.ConstantProperties>())
            {
                cp.PF_Tv1 = 311;
                cp.PF_Tv2 = 372;
                cp.PF_v1 = Methods.PropertyMethods.Visc37_Abbott(cp.NBP.GetValueOrDefault(), cp.PF_SG.GetValueOrDefault());
                cp.PF_v2 = Methods.PropertyMethods.Visc98_Abbott(cp.NBP.GetValueOrDefault(), cp.PF_SG.GetValueOrDefault());
            }
            fs = fs.WithPropertyPackage(PropertyPackages.PengRobinson);

            double msum = vol.Select((v, i) => v * sg[i]).Sum();
            u.Feed = fs.AddMaterialStream("distillate feed")
                .At((250.0 + 273.15).Kelvin(), 200000.0.Pascal())
                .WithMassFlow(60.0.KgPerSecond())
                .WithComposition(cb => { for (int i = 0; i < names.Count; i++) cb.Mass(names[i], vol[i] * sg[i] / msum); });

            u.Naphtha = fs.AddMaterialStream("naphtha");
            u.Kerosene = fs.AddMaterialStream("kerosene");
            u.Diesel = fs.AddMaterialStream("diesel");
            u.Gasoil = fs.AddMaterialStream("light gas oil");
            var qc = fs.AddEnergyStream("condenser duty");
            var qr = fs.AddEnergyStream("reboiler duty");

            u.Fs = fs;
            u.Tower = fs.AddDistillationColumn("T-101")
                .WithNumberOfStages(20)
                .WithFeed(u.Feed, 10)
                .WithDistillate(u.Naphtha)
                .WithBottoms(u.Gasoil)
                .WithSideDraw(u.Kerosene, 5, 60.0.MolPerSecond())
                .WithSideDraw(u.Diesel, 10, 60.0.MolPerSecond())
                .WithCondenserDuty(qc)
                .WithReboilerDuty(qr)
                .WithTopPressure(150000.0.Pascal())
                .WithColumnPressureDrop(50000.0.Pascal())
                .WithCondenserSpec("Reflux Ratio", 3.0, "")
                .Configure(c =>
                {
                    c.SolvingMethodName = "Wang-Henke (Bubble Point)";
                    c.MaxIterations = 200;
                    c.ExternalLoopTolerance = 1e-3;
                    c.InternalLoopTolerance = 1e-3;
                });

            // the feed is calculated first, then the side draws and the bottoms take what an ideal cut would
            fs.TrySolve();
            var col = u.Tower.Object;
            col.SetReboilerSpec("Product_Molar_Flow_Rate", CutFlow(u, 360, 2000), "mol/s");
            SideDraw(u, u.Kerosene).FlowRate.Value = CutFlow(u, 175, 245);
            SideDraw(u, u.Diesel).FlowRate.Value = CutFlow(u, 245, 360);
            return u;
        }

        private static DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.StreamInformation SideDraw(Unit u, MaterialStreamBuilder s)
            => u.Tower.Object.MaterialStreams.Values.First(x => x.StreamID == s.Object.Name);

        /// <summary>The feed's molar flow that boils between two TBP cut points (C).</summary>
        private static double CutFlow(Unit u, double fromC, double toC)
            => u.Feed.Object.Phases[0].Compounds.Values
                .Where(c => u.NbpC[c.Name] >= fromC && u.NbpC[c.Name] < toC)
                .Sum(c => c.MolarFlow.GetValueOrDefault());

        private static DistillationCurvesUtility Attach(MaterialStreamBuilder s, int id)
        {
            var util = new DistillationCurvesUtility { ID = id, Name = "DistillationCurves1", AttachedTo = s.Object };
            util.SetPropertyValue(DistillationCurvesUtility.KeyEFVMethod, (int)EFVCalculationMethod.Correlation);
            s.Object.AttachedUtilities.Add(util);
            util.Update();
            return util;
        }

        private static double C(DistillationCurvesUtility u, string prop)
            => u.LastResults == null ? double.NaN : DistillationCurvesUtility.GetValueSI(u.LastResults, prop) - 273.15;

        /// <summary>The products and their curves and properties.</summary>
        public static void Run()
        {
            var u = Build("StraightRunDistillateCurves");
            u.Fs.Solve();

            var products = new[] { u.Naphtha, u.Kerosene, u.Diesel, u.Gasoil };
            var utils = products.Select((p, i) => Attach(p, i + 1)).ToArray();
            foreach (var (p, ut) in products.Zip(utils))
            {
                var pr = ut.LastResults!.Properties;
                Console.WriteLine($"{p.Object.GraphicObject.Tag,-14} {p.MassFlowKgPerSecond,7:F2} kg/s  D86 10/50/90/95/100 % {C(ut, "D86 10%"),6:F1} {C(ut, "D86 50%"),6:F1} {C(ut, "D86 90%"),6:F1} {C(ut, "D86 95%"),6:F1} {C(ut, "D86 100%"),6:F1} C" +
                                  $"  flash {pr.FlashPointClosedCup - 273.15,6:F1} C  d15 {pr.Density15C,6:F1}  d20 {pr.Density20C,6:F1}  CCI {pr.CetaneIndexD4737,5:F1}  freeze {pr.FreezingPoint - 273.15,6:F1} C");
            }

            double F = u.Feed.MassFlowKgPerSecond;
            double P = products.Sum(p => p.MassFlowKgPerSecond);
            new ResultTable("Straight-run distillate fractionation: product curves")
                .Row("Mass balance", F, P, 1e-3 * F, "kg/s")
                .RowInRange("Naphtha D86 95 %", 100, 200, C(utils[0], "D86 95%"), "C")
                .RowInRange("Kerosene D86 95 % above naphtha's", C(utils[0], "D86 95%"), 330, C(utils[1], "D86 95%"), "C")
                .RowInRange("Diesel D86 95 % above kerosene's", C(utils[1], "D86 95%"), 420, C(utils[2], "D86 95%"), "C")
                .RowInRange("Kerosene flash point", 10, 90, C(utils[1], "Flash Point (Closed Cup)"), "C")
                .RowInRange("Diesel density at 15 C", 780, 900, utils[2].LastResults!.Properties.Density15C, "kg/m3")
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(u.Fs, "straight-run-distillate-curves");
        }

        /// <summary>The same tower converged on the kerosene and diesel end points, moving the two side draws.</summary>
        public static void RunSecondarySpecs()
        {
            var u = Build("StraightRunDistillateSecondarySpecs");
            u.Fs.Solve();
            var col = u.Tower.Object;
            double k0 = SideDraw(u, u.Kerosene).FlowRate.Value, g0 = SideDraw(u, u.Diesel).FlowRate.Value;

            // a kerosene end point 4 C heavier and a diesel end point 4 C lighter, each moved by its own draw
            var sk = new SecSpec
            {
                Name = "Kerosene D86 95%", ProductStreamID = u.Kerosene.Object.Name, PropertyName = "D86 95%", TargetValue = 274.0 + 273.15,
                ManipulatedVariable = SecSpec.ManipulatedVariableType.SideDrawRate, ManipulatedStreamID = u.Kerosene.Object.Name,
                Tolerance = 0.2, MinValue = 0.5 * k0, MaxValue = 1.5 * k0
            };
            var sg = new SecSpec
            {
                Name = "Diesel D86 95%", ProductStreamID = u.Diesel.Object.Name, PropertyName = "D86 95%", TargetValue = 374.0 + 273.15,
                ManipulatedVariable = SecSpec.ManipulatedVariableType.SideDrawRate, ManipulatedStreamID = u.Diesel.Object.Name,
                Tolerance = 0.2, MinValue = 0.5 * g0, MaxValue = 1.5 * g0
            };
            col.SecondarySpecs.Add(sk);
            col.SecondarySpecs.Add(sg);

            u.Fs.Solve();
            foreach (var l in col.SecondarySpecsLog) Console.WriteLine(l);
            Console.WriteLine($"kerosene draw {k0:F2} -> {sk.ManipulatedValue:F2} mol/s, diesel draw {g0:F2} -> {sg.ManipulatedValue:F2} mol/s");

            var products = new[] { u.Naphtha, u.Kerosene, u.Diesel, u.Gasoil };
            var utils = products.Select((p, i) => Attach(p, i + 1)).ToArray();
            foreach (var (p, ut) in products.Zip(utils))
            {
                var pr = ut.LastResults!.Properties;
                Console.WriteLine($"{p.Object.GraphicObject.Tag,-14} {p.MassFlowKgPerSecond,7:F2} kg/s  D86 10/50/90/95/100 % {C(ut, "D86 10%"),6:F1} {C(ut, "D86 50%"),6:F1} {C(ut, "D86 90%"),6:F1} {C(ut, "D86 95%"),6:F1} {C(ut, "D86 100%"),6:F1} C" +
                                  $"  flash {pr.FlashPointClosedCup - 273.15,6:F1} C  d15 {pr.Density15C,6:F1}  CCI {pr.CetaneIndexD4737,5:F1}  freeze {pr.FreezingPoint - 273.15,6:F1} C");
            }

            new ResultTable("Straight-run distillate fractionation: secondary specifications")
                .Row("Kerosene D86 95 %", sk.TargetValue - 273.15, C(utils[1], "D86 95%"), 0.3, "C")
                .Row("Diesel D86 95 %", sg.TargetValue - 273.15, C(utils[2], "D86 95%"), 0.3, "C")
                .Row("Both specifications met", 1.0, sk.Converged && sg.Converged ? 1.0 : 0.0, 0.0)
                .PrintAndThrowIfFailed();

            CaseLibraryOutput.Emit(u.Fs, "straight-run-distillate-secondary-specs");
        }
    }
}
