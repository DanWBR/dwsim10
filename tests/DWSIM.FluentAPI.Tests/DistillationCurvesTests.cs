using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using NUnit.Framework;
using Conv = DWSIM.Thermodynamics.Utilities.PetroleumCharacterization.Methods.DistillationCurveConversion;
using DWSIM.Thermodynamics.Utilities.PetroleumProperties;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Distillation curves of a stream. The conversions are checked against the worked examples of the
    /// API Technical Data Book (procedures 3A1.1, 3A2.1, 3A3.1, 2B7.1, 2B9.1, 2B10.1, 2B13.1) and
    /// Riazi's MNL50 (example 3.2). The stream curves are checked by cutting pseudocomponents from a known
    /// TBP curve and getting that curve back.
    /// </summary>
    [TestFixture]
    public class DistillationCurvesTests
    {
        private static double FToK(double f) => (f - 32) * 5 / 9 + 273.15;
        private static double KToF(double k) => (k - 273.15) * 9 / 5 + 32;
        private static double CToK(double c) => c + 273.15;

        [Test]
        public void D86ToTBPMatchesTheDataBookExample()
        {
            // 3A1.1: D86 10/30/50/70/90 = 350 380 404 433 469 F gives TBP 316.6 372.6 411.2 451.2 496.7 F
            // (0 and 100 % are not in the example and do not affect the other points)
            var d86 = new[] { 330.0, 350, 380, 404, 433, 469, 490 }.Select(FToK).ToArray();
            var tbp = Conv.ASTMD86ToTBP_Daubert(d86).Select(KToF).ToArray();
            var expected = new[] { 316.6, 372.6, 411.2, 451.2, 496.7 };
            for (int i = 0; i < 5; i++)
                Assert.That(tbp[i + 1], Is.EqualTo(expected[i]).Within(0.2), $"TBP point {i + 1}");
        }

        [Test]
        public void TBPToD86MatchesTheDataBookExample()
        {
            // 3A1.1 reversed: TBP 50 % = 409 F gives D86 401.9 F; TBP 30 % = 371 F gives D86 378.4 F
            var tbp = new[] { 300.0, 321, 371, 409, 447, 491, 520 }.Select(FToK).ToArray();
            var d86 = Conv.TBPToASTMD86_Daubert(tbp).Select(KToF).ToArray();
            Assert.That(d86[3], Is.EqualTo(401.9).Within(0.1));
            Assert.That(d86[2], Is.EqualTo(378.4).Within(0.1));
        }

        [Test]
        public void D86RoundTripReturnsTheCurve()
        {
            var d86 = new[] { 35.0, 79.4, 145.6, 201.7, 235.6, 270.6, 300.0 }.Select(CToK).ToArray();
            var back = Conv.TBPToASTMD86_Daubert(Conv.ASTMD86ToTBP_Daubert(d86));
            for (int i = 0; i < 7; i++) Assert.That(back[i], Is.EqualTo(d86[i]).Within(1e-6));
        }

        [Test]
        public void TBPToD2887ReversesTheDataBookExample()
        {
            // 3A3.1: SD 5/10/30/50/70/90/95 = 293 305 324 336 344 359 369 F gives TBP 322.2 327.7 332.4 336 339.6 350.1 357.4 F
            var sd = new[] { 293.0, 305, 324, 336, 344, 359, 369, 380 };
            var tbp = Conv.ASTMD2887ToPEV_Daubert(sd.Select(FToK).ToArray()).Select(KToF).ToArray();
            var expected = new[] { 322.2, 327.7, 332.4, 336, 339.6, 350.1, 357.4 };
            for (int i = 0; i < 7; i++)
                Assert.That(tbp[i], Is.EqualTo(expected[i]).Within(0.2), $"TBP point {i}");

            var back = Conv.TBPToASTMD2887_Daubert(tbp.Select(FToK).ToArray()).Select(KToF).ToArray();
            for (int i = 0; i < 8; i++)
                Assert.That(back[i], Is.EqualTo(sd[i]).Within(1e-6), $"SD point {i}");
        }

        [Test]
        public void D1160ToTBPMatchesTheDataBookExample()
        {
            // figure 3A2.1: D1160 at 10 mmHg 10/30/50/70/90 = 300 400 475 550 650 F gives TBP 287 393 475 550 650 F
            var d1160 = new[] { 250.0, 300, 400, 475, 550, 650, 700 }.Select(FToK).ToArray();
            var tbp = Conv.ASTMD1160ToTBP10mmHg_EdmisterOkamoto(d1160).Select(KToF).ToArray();
            var expected = new[] { 287.0, 393, 475, 550, 650 };
            for (int i = 0; i < 5; i++)
                Assert.That(tbp[i + 1], Is.EqualTo(expected[i]).Within(1.0), $"TBP point {i + 1}");

            var back = Conv.TBP10mmHgToASTMD1160_EdmisterOkamoto(Conv.ASTMD1160ToTBP10mmHg_EdmisterOkamoto(d1160));
            for (int i = 0; i < 7; i++) Assert.That(back[i], Is.EqualTo(d1160[i]).Within(1e-6));
        }

        [Test]
        public void MaxwellBonnellRoundTrip()
        {
            foreach (var kw in new[] { 11.0, 12.0, 12.5 })
                foreach (var tb in new[] { 450.0, 600.0, 750.0, 850.0 })
                {
                    var t10 = Conv.PEVToPEVsub_MaxwellBonnel(tb, 1333.224, kw);
                    Assert.That(t10, Is.LessThan(tb));
                    var back = Conv.PEVsubToPEV_MaxwellBonnel(t10, 1333.224, kw);
                    Assert.That(back, Is.EqualTo(tb).Within(0.05), $"Kw {kw}, Tb {tb}");
                }
        }

        [Test]
        public void D86ToEFVMatchesRiaziExample()
        {
            // Riazi MNL50 example 3.2: D86 0 % = 305 K and SG 0.7862 give EFV 0 % = 340.9 K;
            // the other points compare with the measured EFV within the correlation's error
            var d86 = new[] { 305.0, CToK(79.4), CToK(145.6), CToK(201.7), CToK(235.6), CToK(270.6), CToK(300) };
            var efv = Conv.ASTMD86ToEFV_RiaziDaubert(d86, 0.7862);
            Assert.That(efv[0], Is.EqualTo(340.9).Within(0.2));
            var measured = new[] { 107.2, 151.1, 182.2, 207.2, 228.3 }.Select(CToK).ToArray();
            for (int i = 0; i < 5; i++)
                Assert.That(efv[i + 1], Is.EqualTo(measured[i]).Within(10.0), $"EFV point {i + 1}");
        }

        [Test]
        public void ProductPropertiesMatchTheDataBookExamples()
        {
            // 2B7.1 open cup: D86 10 % = 403 F gives 164 F
            Assert.That(KToF(PetroleumCorrelations.FlashPointOpenCup(FToK(403))), Is.EqualTo(164.4).Within(0.1));
            // 2B9.1: SG 0.8304, MeABP 570.2 F, K 12.16 gives 635.4 R
            Assert.That(PetroleumCorrelations.AnilinePoint(0.8304, FToK(570.2), 12.16) * 1.8, Is.EqualTo(635.4).Within(0.5));
            // 2B10.1: MeABP 414.5 F, K 11.21 gives 16.7 mm
            Assert.That(PetroleumCorrelations.SmokePoint(FToK(414.5), 11.21) * 1000, Is.EqualTo(16.7).Within(0.1));
            // 2B13.1: API 32.3, MeABP 617 F gives 57.1
            Assert.That(PetroleumCorrelations.CetaneIndex(32.3, FToK(617)), Is.EqualTo(57.1).Within(0.1));
            // D4737 at its reference point (T10 215 C, T50 260 C, T90 310 C, 850 kg/m3) is 45.2
            Assert.That(PetroleumCorrelations.CetaneIndexD4737(CToK(215), CToK(260), CToK(310), 850), Is.EqualTo(45.2).Within(1e-9));
        }

        [Test]
        public void TBPFromCompositionReturnsTheCurveTheCutsCameFrom()
        {
            var curve = new[] { 0.0, 10, 30, 50, 70, 90, 100 };
            var temps = new[] { 10.0, 71.1, 143.3, 204.4, 250.6, 291.7, 320.0 }.Select(CToK).ToArray();

            int n = 40;
            var nbp = new double[n];
            var vol = new double[n];
            for (int i = 0; i < n; i++)
            {
                nbp[i] = DistillationCurves.InterpolateMonotone(curve, temps, 100.0 * (i + 0.5) / n);
                vol[i] = 1.0;
            }
            var pts = new[] { 10.0, 30, 50, 70, 90 };
            var tbp = DistillationCurves.TBPFromComposition(nbp, vol, pts);
            for (int i = 0; i < pts.Length; i++)
                Assert.That(tbp[i], Is.EqualTo(temps[i + 1]).Within(0.5), $"{pts[i]} %");
        }

        [Test]
        public void TheCurvesOfAPseudocomponentStreamReturnItsTBPCurve()
        {
            // a naphtha-kerosene blend (Riazi example 3.2) cut into 16 pseudocomponents of equal volume
            var curve = new[] { 0.0, 10, 30, 50, 70, 90, 100 };
            var temps = new[] { 10.0, 71.1, 143.3, 204.4, 250.6, 291.7, 320.0 }.Select(CToK).ToArray();
            int n = 16;
            const double kw = 11.8;

            var fs = Flowsheet.Create("DistCurves");
            var names = new string[n];
            var mass = new double[n];
            for (int i = 0; i < n; i++)
            {
                double tb = DistillationCurves.InterpolateMonotone(curve, temps, 100.0 * (i + 0.5) / n);
                double sg = Math.Pow(1.8 * tb, 1.0 / 3.0) / kw;
                double mw = DWSIM.Thermodynamics.Utilities.PetroleumCharacterization.Methods.PropertyMethods.MW_Riazi(tb, sg);
                names[i] = $"NBP_{tb - 273.15:F0}";
                fs = fs.WithPseudoComponent(names[i], tb.Kelvin(), sg, mw);
                mass[i] = sg; // equal volumes
            }
            fs = fs.WithPropertyPackage(PropertyPackages.PengRobinson);

            double msum = mass.Sum();
            var feed = fs.AddMaterialStream("feed")
                .At(300.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.KgPerSecond())
                .WithComposition(c => { for (int i = 0; i < n; i++) c.Mass(names[i], mass[i] / msum); });
            fs.Solve();

            var res = DistillationCurves.Calculate(feed.Object);
            foreach (var m in res.Messages) Console.WriteLine(m);

            var tbp = res.Curves[DistillationCurveType.TBP];
            foreach (var p in new[] { 10.0, 30, 50, 70, 90 })
            {
                double expected = DistillationCurves.InterpolateMonotone(curve, temps, p);
                Assert.That(tbp.TemperatureAt(p), Is.EqualTo(expected).Within(1.5), $"TBP {p} %");
            }

            foreach (var type in res.Curves.Keys)
            {
                var t = res.Curves[type].Temperatures;
                Console.WriteLine($"{type}: " + string.Join(" ", t.Select(x => (x - 273.15).ToString("F1"))));
                for (int i = 1; i < t.Length; i++)
                    Assert.That(t[i], Is.GreaterThanOrEqualTo(t[i - 1] - 1e-6), $"{type} is not increasing at point {i}");
            }

            // the D86 curve of this blend was measured: 35.0 79.4 145.6 201.7 235.6 270.6 C at 0/10/30/50/70/90 %
            var d86 = res.Curves[DistillationCurveType.ASTM_D86];
            Assert.That(d86.TemperatureAt(50) - 273.15, Is.EqualTo(201.7).Within(6.0));
            Assert.That(d86.TemperatureAt(90) - 273.15, Is.EqualTo(270.6).Within(10.0));

            // the flashed EFV curve lies inside the TBP curve, and its 50 % point near the measured 182.2 C
            var efv = res.Curves[DistillationCurveType.EFV];
            Assert.That(efv.TemperatureAt(50) - 273.15, Is.EqualTo(182.2).Within(15.0));

            var pr = res.Properties;
            Console.WriteLine($"SG {pr.SpecificGravity:F4} API {pr.API:F1} K {pr.WatsonK:F2} MeABP {pr.MeanAverageBoilingPoint - 273.15:F1} C " +
                              $"d15 {pr.Density15C:F1} d20 {pr.Density20C:F1} FP {pr.FlashPointClosedCup - 273.15:F1} C " +
                              $"nu40 {pr.KinematicViscosity40C * 1e6:F3} cSt CI {pr.CetaneIndex:F1} CCI {pr.CetaneIndexD4737:F1}");
            Assert.That(pr.WatsonK, Is.EqualTo(kw).Within(0.15));
            Assert.That(pr.Density15C, Is.EqualTo(pr.SpecificGravity * 999.0).Within(40.0));
            Assert.That(pr.Density20C, Is.LessThan(pr.Density15C));
            Assert.That(pr.KinematicViscosity40C * 1e6, Is.InRange(0.5, 5.0));
            Assert.That(pr.KinematicViscosity40C, Is.GreaterThan(pr.KinematicViscosity100C));
        }

        [Test]
        public void ATraceOfAHeavyCompoundDoesNotMoveThe95PercentPoints()
        {
            // issue 117: a C9-C16 n-paraffin cut, equal moles, with n-eicosane at a trace mole fraction
            var cut = new[] { "N-nonane", "N-decane", "N-undecane", "N-dodecane", "N-tridecane", "N-tetradecane", "N-pentadecane", "N-hexadecane" };
            var fs = Flowsheet.Create("DistCurvesTrace");
            foreach (var c in cut) fs = fs.WithCompound(c);
            fs = fs.WithCompound("N-eicosane").WithPropertyPackage(PropertyPackages.PengRobinson);

            var traces = new[] { 0.0, 1e-15, 1e-9, 1e-6, 1e-3 };
            var streams = traces.Select((tr, k) => fs.AddMaterialStream("s" + k)
                .At(298.15.Kelvin(), 101325.0.Pascal())
                .WithMolarFlow(1.MolPerSecond())
                .WithComposition(c => { foreach (var n in cut) c.Mole(n, (1 - tr) / cut.Length); c.Mole("N-eicosane", tr); })).ToArray();
            fs.Solve();

            var tbp95 = new double[traces.Length];
            var d86_95 = new double[traces.Length];
            for (int k = 0; k < traces.Length; k++)
            {
                var res = DistillationCurves.Calculate(streams[k].Object);
                tbp95[k] = res.Curves[DistillationCurveType.TBP].TemperatureAt(95) - 273.15;
                d86_95[k] = res.Curves[DistillationCurveType.ASTM_D86].TemperatureAt(95) - 273.15;
                Console.WriteLine($"{traces[k]:g}: TBP 95 % {tbp95[k]:F2} C, D86 95 % {d86_95[k]:F2} C");
            }
            for (int k = 1; k < 4; k++)
            {
                Assert.That(tbp95[k], Is.EqualTo(tbp95[0]).Within(0.01), $"TBP 95 % at {traces[k]:g}");
                Assert.That(d86_95[k], Is.EqualTo(d86_95[0]).Within(0.01), $"D86 95 % at {traces[k]:g}");
            }
            Assert.That(tbp95[4] - tbp95[0], Is.InRange(0.0, 1.0), "TBP 95 % at 1e-3");
            Assert.That(d86_95[4] - d86_95[0], Is.InRange(0.0, 1.0), "D86 95 % at 1e-3");
        }

        [Test]
        public void TheAttachedUtilityExposesTheCurvePointsAndSurvivesASave()
        {
            var fs = Flowsheet.Create("DistCurvesUtility")
                .WithCompound("N-heptane").WithCompound("N-decane").WithCompound("N-dodecane").WithCompound("N-hexadecane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            var s = fs.AddMaterialStream("diesel")
                .At(320.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.KgPerSecond())
                .WithComposition(c => { c.Mass("N-heptane", 0.1); c.Mass("N-decane", 0.3); c.Mass("N-dodecane", 0.4); c.Mass("N-hexadecane", 0.2); });
            fs.Solve();

            var util = new DWSIM.Thermodynamics.Utilities.PetroleumProperties.DistillationCurvesUtility { ID = 1, Name = "DC1", AttachedTo = s.Object };
            util.SetPropertyValue(DWSIM.Thermodynamics.Utilities.PetroleumProperties.DistillationCurvesUtility.KeyPercents, "0, 10, 50, 90, 95, 100");
            s.Object.AttachedUtilities.Add(util);
            util.Update();

            var su = fs.Inner.FlowsheetOptions.SelectedUnitSystem;
            var d86_95 = Convert.ToDouble(util.GetPropertyValue("D86 95%"));
            var viaStream = Convert.ToDouble(s.Object.GetPropertyValue("DC1: D86 95%", su));
            Console.WriteLine($"D86 95% = {d86_95} {util.GetPropertyUnits("D86 95%")}, through the stream {viaStream}");
            Assert.That(viaStream, Is.EqualTo(d86_95));
            Assert.That(s.Object.GetProperties(DWSIM.Interfaces.Enums.PropertyType.ALL), Does.Contain("DC1: Flash Point (Closed Cup)"));

            // n-hexadecane boils at 560 K: the TBP end point is close to it, the start close to n-heptane (371.6 K)
            var tbp100 = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(su.temperature, Convert.ToDouble(util.GetPropertyValue("TBP 100%")));
            var tbp0 = DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI(su.temperature, Convert.ToDouble(util.GetPropertyValue("TBP 0%")));
            Assert.That(tbp100, Is.InRange(540.0, 600.0));
            Assert.That(tbp0, Is.InRange(340.0, 375.0));

            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dwsim-distcurves-" + Guid.NewGuid().ToString("N") + ".dwxmz");
            try
            {
                fs.Save(path);
                var back = Flowsheet.Load(path);
                var ms = back.Inner.SimulationObjects.Values.First(o => o.GraphicObject.Tag == "diesel");
                var u2 = ms.AttachedUtilities.OfType<DWSIM.Thermodynamics.Utilities.PetroleumProperties.DistillationCurvesUtility>().Single();
                Assert.That(u2.GetPercents(), Is.EqualTo(new[] { 0.0, 10, 50, 90, 95, 100 }));
                u2.Update();
                Assert.That(Convert.ToDouble(u2.GetPropertyValue("D86 95%")), Is.EqualTo(d86_95).Within(1e-6));
            }
            finally
            {
                try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch (Exception) { }
            }
        }

        private static string TestFlowsheet(string name)
        {
            var folder = AppContext.BaseDirectory;
            while (folder != null && !System.IO.Directory.Exists(System.IO.Path.Combine(folder, "tests", "flowsheets")))
                folder = System.IO.Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "tests/flowsheets not found");
            return System.IO.Path.Combine(folder!, "tests", "flowsheets", name);
        }

        [Test]
        public void AColumnMeetsSecondarySpecificationsOnItsProducts()
        {
            var fs = Flowsheet.Load(TestFlowsheet("PetroleumDistillation.dwxml"));
            fs.Solve();
            var col = fs.Inner.SimulationObjects.Values.OfType<DWSIM.UnitOperations.UnitOperations.DistillationColumn>().Single();
            var dist = col.MaterialStreams.Values.First(x => x.StreamBehavior == DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.StreamInformation.Behavior.Distillate);
            var side = col.MaterialStreams.Values.First(x => x.StreamBehavior == DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.StreamInformation.Behavior.Sidedraw);
            // reflux ratio on the condenser, as a real tower runs; with the distillate rate fixed too, every
            // product rate would be pinned by the feed balance and no single rate could move
            col.Specs["C"].SType = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSpec.SpecType.Stream_Ratio;
            col.Specs["C"].SpecUnit = "";
            col.Specs["C"].SpecValue = col.RefluxRatio;
            fs.Solve();
            double dRate0 = col.Specs["R"].SpecValue, sRate0 = side.FlowRate.Value;

            var s1 = new DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSecondarySpec
            {
                Name = "Distillate D86 90%", ProductStreamID = dist.StreamID, PropertyName = "D86 90%",
                ManipulatedVariable = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSecondarySpec.ManipulatedVariableType.ReboilerSpecValue,
                Tolerance = 0.02
            };
            var s2 = new DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSecondarySpec
            {
                Name = "Side draw D86 50%", ProductStreamID = side.StreamID, PropertyName = "D86 50%",
                ManipulatedVariable = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSecondarySpec.ManipulatedVariableType.SideDrawRate,
                ManipulatedStreamID = side.StreamID, Tolerance = 0.02
            };
            double b1 = col.EvaluateSecondarySpec(s1), b2 = col.EvaluateSecondarySpec(s2);
            Console.WriteLine($"base: reflux {col.RefluxRatio:F3}, bottoms rate {dRate0:F2} mol/s, D86 90% {b1 - 273.15:F2} C; side draw {sRate0:F2} mol/s, D86 50% {b2 - 273.15:F2} C");
            // targets taken from a known solution (bottoms 130 mol/s, side draw 30 mol/s), so they can be met; the two
            // properties move almost together, so the loop may meet them at another pair of rates
            col.Specs["R"].SpecValue = 130.0;
            side.FlowRate.Value = 30.0;
            fs.Solve();
            s1.TargetValue = col.EvaluateSecondarySpec(s1);
            s2.TargetValue = col.EvaluateSecondarySpec(s2);
            col.Specs["R"].SpecValue = dRate0;
            side.FlowRate.Value = sRate0;
            Console.WriteLine($"targets: distillate D86 90% {s1.TargetValue - 273.15:F2} C, side draw D86 50% {s2.TargetValue - 273.15:F2} C");
            col.SecondarySpecs.Add(s1);
            col.SecondarySpecs.Add(s2);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            fs.Solve();
            sw.Stop();
            foreach (var l in col.SecondarySpecsLog) Console.WriteLine(l);
            foreach (var s in new[] { s1, s2 })
                Console.WriteLine($"{s.Name}: target {s.TargetValue - 273.15:F2} C, calculated {s.CalculatedValue - 273.15:F2} C, variable {s.ManipulatedValue:F3}, converged {s.Converged}");
            Console.WriteLine($"{sw.Elapsed.TotalSeconds:F1} s");

            Assert.That(s1.Converged && s2.Converged, Is.True);
            Assert.That(s1.CalculatedValue, Is.EqualTo(s1.TargetValue).Within(0.02));
            Assert.That(s2.CalculatedValue, Is.EqualTo(s2.TargetValue).Within(0.02));

            // the stream the column wrote carries the same curve the loop evaluated
            var written = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.Inner.SimulationObjects[dist.StreamID];
            var onStream = DistillationCurves.Calculate(written, new DistillationCurveSettings { EFVMethod = EFVCalculationMethod.Correlation });
            Assert.That(onStream.Curves[DistillationCurveType.ASTM_D86].TemperatureAt(90), Is.EqualTo(s1.TargetValue).Within(0.05));

            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dwsim-secspecs-" + Guid.NewGuid().ToString("N") + ".dwxmz");
            try
            {
                fs.Save(path);
                var back = Flowsheet.Load(path);
                var col2 = back.Inner.SimulationObjects.Values.OfType<DWSIM.UnitOperations.UnitOperations.DistillationColumn>().Single();
                Assert.That(col2.SecondarySpecs.Count, Is.EqualTo(2));
                Assert.That(col2.SecondarySpecs[1].PropertyName, Is.EqualTo("D86 50%"));
                Assert.That(col2.SecondarySpecs[1].TargetValue, Is.EqualTo(s2.TargetValue));
            }
            finally
            {
                try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch (Exception) { }
            }
        }
    }
}
