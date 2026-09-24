//    Column internals rating: tests against the worked examples of the reference books.
//    Copyright 2026 Daniel Wagner Oliveira de Medeiros
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
using DWSIM.Automation.DynamicRunner.ColumnInternals;
using DWSIM.Interfaces;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class ColumnInternalsTests
    {
        // ------------------------------------------------------------------ sieve trays

        /// <summary>Towler and Sinnott (2008), Example 11.11: acetone-water column, bottom plate.</summary>
        private static StageProperties TowlerBottomPlate()
        {
            return new StageProperties
            {
                Stage = 25,
                T = 379.15, P = 125925,
                VaporMassFlow = 162.3 * 18.0 / 3600.0,   // 0.8115 kg/s
                LiquidMassFlow = 811.6 * 18.0 / 3600.0,  // 4.06 kg/s
                VaporDensity = 0.72, LiquidDensity = 954,
                VaporViscosity = 1.2e-5, LiquidViscosity = 2.7e-4,
                SurfaceTension = 0.057
            };
        }

        private static InternalsSection TowlerPlate()
        {
            return new InternalsSection
            {
                Name = "Stripping", FromStage = 25, ToStage = 25, Type = InternalType.SieveTray,
                Diameter = 0.79, TraySpacing = 0.5, DowncomerAreaFraction = 0.12, WeirHeight = 0.05,
                HoleDiameter = 0.005, HoleAreaFraction = 0.10, PlateThickness = 0.005, DowncomerClearance = 0.04
            };
        }

        [Test]
        public void FairFloodingReproducesTowlerExample1111()
        {
            var sp = TowlerBottomPlate();
            var flv = TrayHydraulics.FlowParameter(sp.LiquidMassFlow, sp.VaporMassFlow, sp.LiquidDensity, sp.VaporDensity);
            Assert.That(flv, Is.EqualTo(0.14).Within(0.005), "F_LV");
            // Fig. 11.29 read by the authors: K1 = 0.075 at 0.5 m; top of column (F_LV = 0.03): 0.090
            Assert.That(TrayHydraulics.FairCapacityFactor(flv, 0.5), Is.EqualTo(0.075).Within(0.005), "K1 base");
            Assert.That(TrayHydraulics.FairCapacityFactor(0.03, 0.5), Is.EqualTo(0.090).Within(0.005), "K1 top");
            var uf = TrayHydraulics.FairFloodingVelocity(flv, 0.5, 954, 0.72, 0.057);
            Assert.That(uf, Is.EqualTo(3.38).Within(0.2), "flooding velocity, base");
            var ufTop = TrayHydraulics.FairFloodingVelocity(0.03, 0.5, 753, 2.05, 0.023);
            Assert.That(ufTop, Is.EqualTo(1.78).Within(0.1), "flooding velocity, top");
        }

        [Test]
        public void SieveTrayRatingReproducesTowlerExample1111()
        {
            var s = TowlerPlate();
            var sp = TowlerBottomPlate();
            var r = TrayHydraulics.RateSieveTray(s, sp, 0.79, 0.7, 3.0, 1.0);

            // weir length 0.76 Dc for a 12 % downcomer (Fig. 11.33)
            Assert.That(TrayHydraulics.WeirLengthRatio(0.12), Is.EqualTo(0.76).Within(0.01), "lw/Dc");
            Assert.That(r.WeirCrest, Is.EqualTo(27.0).Within(1.0), "h_ow at maximum rate, mm");
            // weep point at turndown: h_w + h_ow = 72 mm -> K2 = 30.6 -> u_h,min = 14 m/s
            Assert.That(TrayHydraulics.EduljeeK2(72.0), Is.EqualTo(30.6).Within(0.1), "K2");
            Assert.That(r.WeepPointVelocity, Is.EqualTo(14.0).Within(0.5), "weep point velocity");
            Assert.That(r.HoleVelocity, Is.EqualTo(29.7).Within(1.0), "hole velocity");
            Assert.That(r.WeepRatioTurndown, Is.GreaterThan(1.0), "no weeping at turndown");
            // dry drop with C0 = 0.84 -> 48 mm; residual 13.1; total 138
            Assert.That(TrayHydraulics.OrificeCoefficient(1.0, 0.10), Is.EqualTo(0.84).Within(0.01), "C0");
            Assert.That(r.DryPressureDrop, Is.EqualTo(48.0).Within(3.0), "h_d");
            Assert.That(TrayHydraulics.ResidualHead(954), Is.EqualTo(13.1).Within(0.1), "h_r");
            Assert.That(r.TotalHead, Is.EqualTo(138.0).Within(5.0), "h_t");
            Assert.That(r.PressureDrop, Is.EqualTo(1.3e3).Within(60), "plate pressure drop, Pa");
            // downcomer: h_dc = 5.2 mm (A_ap = 0.6 x 0.04), backup 221 mm, residence 3.1 s
            Assert.That(r.DowncomerBackup, Is.EqualTo(221.0).Within(8.0), "h_b");
            Assert.That(r.DowncomerBackup, Is.LessThan(r.DowncomerBackupLimit), "backup criterion");
            Assert.That(r.DowncomerResidenceTime, Is.EqualTo(3.1).Within(0.25), "t_r");
            // entrainment: 76 % flood, F_LV 0.14 -> psi = 0.018
            Assert.That(r.FloodFraction, Is.EqualTo(0.76).Within(0.05), "fraction of flood");
            Assert.That(r.Entrainment, Is.EqualTo(0.018).Within(0.008), "psi");
            Assert.That(r.Warnings, Is.Empty, string.Join(" | ", r.Warnings));
        }

        [Test]
        public void SizingForTheTargetFloodGivesTheBookDiameter()
        {
            // the book sizes for 85 % flood and gets 0.77 m at the base
            var d = TrayHydraulics.DiameterForFloodFraction(TowlerPlate(), TowlerBottomPlate(), 0.85);
            Assert.That(d, Is.EqualTo(0.77).Within(0.03));
        }

        [Test]
        public void KisterHaasTransitionHeightMatchesKistersSizingExample()
        {
            // Kister (1992) sec. 6.5, first trial: d_h 0.5 in, A_f 0.1, Q_L 6.45 gpm/in -> h_ct,water 0.937 in
            var ql = 6.45 / 402.6; // m3/(s m)
            var hct = TrayHydraulics.ClearLiquidHeightAtTransition(0.5 * 0.0254, 0.10, 62.2 * 16.01846, 1.0, ql, 0.05);
            Assert.That(hct / 0.0254, Is.EqualTo(0.937).Within(0.02));
        }

        [Test]
        public void EntrainmentChartIsMonotonic()
        {
            // more flood or less F_LV: more entrainment
            Assert.That(TrayHydraulics.FairEntrainment(0.1, 0.8), Is.GreaterThan(TrayHydraulics.FairEntrainment(0.1, 0.6)));
            Assert.That(TrayHydraulics.FairEntrainment(0.02, 0.7), Is.GreaterThan(TrayHydraulics.FairEntrainment(0.2, 0.7)));
            Assert.That(TrayHydraulics.FairEntrainment(0.5, 0.3), Is.LessThan(0.002));
        }

        // ------------------------------------------------------------------ packings

        /// <summary>Seader and Henley (2nd ed.), Example 6.12: holdup of Hiflow 50 mm metal and Montz B1-200.</summary>
        [Test]
        public void BilletHoldupReproducesSeaderExample612()
        {
            var hiflow = PackingCatalogue.Find("Hiflow rings Metal 50 mm");
            var montz = PackingCatalogue.Find("Montz Metal B1-200");
            Assert.That(hiflow, Is.Not.Null); Assert.That(montz, Is.Not.Null);
            // oil with three times the kinematic viscosity of water: nu = 3e-6, take rho = 1000
            var hL1 = PackingHydraulics.BilletHoldup(0.01, 1000, 3e-3, hiflow.a, hiflow.Ch);
            var hL2 = PackingHydraulics.BilletHoldup(0.01, 1000, 3e-3, montz.a, montz.Ch);
            Assert.That(hL1, Is.EqualTo(0.0637).Within(0.001));
            Assert.That(hL2, Is.EqualTo(0.0722).Within(0.001));
        }

        /// <summary>Seader Example 6.14: 25 mm metal Bialecki rings, loading, flooding, holdup and pressure drop.</summary>
        [Test]
        public void BilletLoadingAndPressureDropReproduceSeaderExample614()
        {
            var p = PackingCatalogue.Find("Bialecki rings Metal 25 mm");
            Assert.That(p, Is.Not.Null);
            double rhoV = 1.182, rhoL = 1000, muV = 1.78e-5, muL = 1.0e-3;
            double lOverV = 1361.0 / 515.0;
            var uVl = PackingHydraulics.BilletLoadingVelocity(lOverV, rhoV, rhoL, muV, muL, p.a, p.Epsilon, p.Cs);
            Assert.That(uVl, Is.EqualTo(1.46).Within(0.03), "loading velocity");
            Assert.That(PackingHydraulics.BilletFloodingVelocity(uVl), Is.EqualTo(2.09).Within(0.05), "flooding velocity");
            var uL = uVl * rhoV * lOverV / rhoL; // 0.00457 m/s
            Assert.That(uL, Is.EqualTo(0.00457).Within(0.0001));
            Assert.That(PackingHydraulics.BilletHoldup(uL, rhoL, muL, p.a, p.Ch), Is.EqualTo(0.0440).Within(0.001), "holdup at loading");
            var dT = 0.325;
            Assert.That(PackingHydraulics.BilletWallFactor(p.a, p.Epsilon, dT), Is.EqualTo(0.944).Within(0.003), "wall factor");
            Assert.That(PackingHydraulics.BilletDryPressureDrop(uVl, rhoV, muV, p.a, p.Epsilon, p.Cp, dT), Is.EqualTo(281.0).Within(6.0), "dry pressure drop");
            Assert.That(PackingHydraulics.BilletPressureDrop(uVl, uL, rhoV, rhoL, muV, muL, p.a, p.Epsilon, p.Ch, p.Cp, dT), Is.EqualTo(331.0).Within(8.0), "irrigated pressure drop");
        }

        /// <summary>Seader Example 6.15: 1.5-in metal Pall-like rings, H_L, H_G, H_OG and HETP by Billet and Schultes.</summary>
        [Test]
        public void BilletMassTransferReproducesSeaderExample615()
        {
            var p = new PackingData { Name = "Pall-like", Material = "Metal", Size = "1.5 in", a = 149.6, Epsilon = 0.952, Ch = 0.7, CL = 1.227, CV = 0.341, NominalSize = 0.038 };
            double rhoL = 61.5 * 16.01846, rhoV = 0.121 * 16.01846;
            double muL = 0.64e-6 * rhoL, muV = 0.75e-5 * rhoV;
            double sigma = 0.101, DL = 1.82e-9, DV = 7.75e-6;
            double uL = 0.0017, uV = 2.49;
            // The example states a_h/a = 0.045 and h_L = 0.0128 with C_h = 0.7, but eq. (6-101) with its own
            // numbers gives a_h/a = 0.449 and eq. (6-97) then 0.0182 (the code reproduces Example 6.12 exactly,
            // so the slip is in the printed example). The transfer units below use the book's holdup so that
            // eqs. (6-132) and (6-133) are checked on their own.
            Assert.That(PackingHydraulics.BilletHoldup(uL, rhoL, muL, p.a, p.Ch), Is.EqualTo(0.0182).Within(0.0005), "holdup by eq. 6-97");
            var aph = PackingHydraulics.BilletInterfaceAreaRatio(uL, rhoL, muL, sigma, p.a, p.Epsilon);
            Assert.That(aph, Is.EqualTo(0.242).Within(0.01), "a_Ph/a");
            var hl = PackingHydraulics.BilletHL(uL, DL, p.a, p.Epsilon, p.CL, 0.0128, aph);
            var hg = PackingHydraulics.BilletHG(uV, rhoV, muV, DV, p.a, p.Epsilon, p.CV, 0.0128, aph);
            Assert.That(hl, Is.EqualTo(0.26).Within(0.015), "H_L");
            Assert.That(hg, Is.EqualTo(1.03).Within(0.05), "H_G");
            var lambda = 0.69;
            var hog = hg + lambda * hl;
            Assert.That(hog / 0.3048, Is.EqualTo(3.96).Within(0.15), "H_OG, ft");
            Assert.That(PackingHydraulics.HetpFromHOG(hog, lambda) / 0.3048, Is.EqualTo(4.73).Within(0.2), "HETP, ft");
        }

        [Test]
        public void RobbinsPressureDropIsWithinTheGpdcRangeOfSeaderExample613()
        {
            // Seader Example 6.13: 1-in metal IMTP, F_LV 0.092 at 70 % of flood: GPDC gives 0.88 in/ft, the
            // vendor data 0.63 in/ft. Robbins should land between the two.
            var p = PackingCatalogue.Find("Metal Intalox (IMTP) Metal 25 mm");
            Assert.That(p, Is.Not.Null);
            double rhoV = 0.0738 * 16.01846, rhoL = 1000, muL = 1e-3;
            double uVf = PackingHydraulics.RobbinsFloodingVelocity(0.0, rhoV, rhoL, muL, p.Fpd, p.Fp);
            Assert.That(uVf, Is.GreaterThan(1.0), "a dry-ish bed floods at a sensible velocity");
            // the book's flooding velocity for IMTP: 8.5 ft/s = 2.59 m/s, operating at 70 %: 5.95 ft/s
            double uV = 5.95 * 0.3048;
            double G = uV * rhoV;
            double L = G * 0.092 * Math.Sqrt(rhoL / rhoV); // from F_LV = (L/G) sqrt(rhoV/rhoL)
            var dp = PackingHydraulics.RobbinsPressureDrop(G, L, rhoV, rhoL, muL, p.Fpd) / 817.2;
            Assert.That(dp, Is.InRange(0.45, 1.1), "in H2O/ft");
            var floodDp = PackingHydraulics.KisterGillFloodPressureDrop(p.Fp) / 817.2;
            Assert.That(floodDp, Is.EqualTo(0.115 * Math.Pow(41, 0.7)).Within(0.02), "Kister-Gill at F_p = 41 ft2/ft3");
        }

        [Test]
        public void OndaGivesHeightsOfTheSameOrderAsBilletForTheSeaderPacking()
        {
            var p = PackingCatalogue.Find("Pall rings Metal 38 mm");
            Assert.That(p, Is.Not.Null);
            double rhoL = 985, rhoV = 1.94, muL = 6.3e-4, muV = 1.45e-5, sigma = 0.101, DL = 1.82e-9, DV = 7.75e-6;
            double uL = 0.0017, uV = 2.49;
            var aw = p.a * PackingHydraulics.OndaWettedAreaRatio(uL * rhoL, rhoL, muL, sigma, PackingHydraulics.CriticalSurfaceTension(p.Material), p.a);
            Assert.That(aw / p.a, Is.InRange(0.15, 0.8), "wetted fraction");
            var kL = PackingHydraulics.OndaKL(uL * rhoL, rhoL, muL, DL, p.a, aw, p.NominalSize);
            var kG = PackingHydraulics.OndaKG(uV * rhoV, rhoV, muV, DV, p.a, p.NominalSize);
            var HL = uL / (kL * aw); var HG = uV / (kG * aw);
            Assert.That(HL, Is.InRange(0.1, 1.0), "H_L");
            Assert.That(HG, Is.InRange(0.3, 2.5), "H_G");
        }

        [Test]
        public void RulesOfThumbFollowKister()
        {
            var pall = PackingCatalogue.Find("Pall rings Metal 50 mm");
            Assert.That(PackingHydraulics.RuleOfThumbHETP(pall, 1.5), Is.EqualTo(0.075).Within(1e-6), "1.5 d_p");
            var mella = PackingCatalogue.Find("Mellapak Sheet metal 250Y");
            // 100/a + 4/12 ft with a = 250 m2/m3 = 76.2 ft2/ft3 -> 1.65 ft = 0.50 m
            Assert.That(PackingHydraulics.RuleOfThumbHETP(mella, 1.5), Is.EqualTo(0.50).Within(0.01), "structured");
            Assert.That(PackingHydraulics.RuleOfThumbHETP(pall, 0.4), Is.EqualTo(0.4).Within(1e-6), "small column: at least the diameter");
        }

        // ------------------------------------------------------------------ catalogue and case file

        [Test]
        public void CatalogueCarriesTheBookData()
        {
            Assert.That(PackingCatalogue.All.Count, Is.GreaterThan(120));
            var pall = PackingCatalogue.Find("Pall rings Metal 50 mm");
            Assert.That(pall.Fp, Is.EqualTo(27 * 3.2808).Within(0.1));
            Assert.That(pall.HasBilletHydraulics && pall.HasBilletMassTransfer);
            var perryPall = PackingCatalogue.Find("Pall rings Metal 50 mm (Perry)");
            Assert.That(perryPall.Fp, Is.EqualTo(89)); Assert.That(perryPall.Fpd, Is.EqualTo(79));
            Assert.That(PackingCatalogue.All.Count(p => p.Structured), Is.GreaterThan(20));
            Assert.That(PackingCatalogue.Find("Sulzer Gauze BX").CorrugationAngle, Is.EqualTo(60));
        }

        [Test]
        public void ACaseRoundTripsThroughItsFile()
        {
            var inp = new ColumnInternalsInput { ColumnName = "DC-1", TargetFloodFractionTrays = 0.75, Turndown = 0.6 };
            inp.Sections.Add(new InternalsSection { Name = "Top", FromStage = 2, ToStage = 10, Type = InternalType.SieveTray, Diameter = 1.2, WeirHeight = 0.04 });
            inp.Sections.Add(new InternalsSection
            {
                Name = "Bottom", FromStage = 11, ToStage = 20, Type = InternalType.RandomPacking, PackingName = "Pall rings Metal 50 mm",
                PackingModel = PackingModel.BilletSchultes, HetpModel = HetpModel.BilletSchultes, BedHeight = 4.5,
                CustomPacking = new PackingData { Name = "Mine", Material = "Metal", Size = "40", a = 150, Epsilon = 0.95, Fp = 80, NominalSize = 0.04 }
            });
            var path = Path.Combine(Path.GetTempPath(), "internals_" + Guid.NewGuid().ToString("N") + ColumnInternalsInput.FileExtension);
            try
            {
                inp.SaveToFile(path);
                var back = ColumnInternalsInput.LoadFromFile(path);
                Assert.That(back.ColumnName, Is.EqualTo("DC-1"));
                Assert.That(back.TargetFloodFractionTrays, Is.EqualTo(0.75));
                Assert.That(back.Turndown, Is.EqualTo(0.6));
                Assert.That(back.Sections.Count, Is.EqualTo(2));
                Assert.That(back.Sections[0].WeirHeight, Is.EqualTo(0.04));
                Assert.That(back.Sections[1].Type, Is.EqualTo(InternalType.RandomPacking));
                Assert.That(back.Sections[1].PackingModel, Is.EqualTo(PackingModel.BilletSchultes));
                Assert.That(back.Sections[1].BedHeight, Is.EqualTo(4.5));
                Assert.That(back.Sections[1].CustomPacking.a, Is.EqualTo(150));
                Assert.That(double.IsNaN(back.Sections[1].CustomPacking.Ch));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void RatingASectionSizesAndReportsTheLimitingStage()
        {
            var props = Enumerable.Range(1, 5).Select(i => { var sp = TowlerBottomPlate(); sp.Stage = i; sp.VaporMassFlow *= 1.0 + 0.05 * i; return sp; }).ToList();
            var inp = new ColumnInternalsInput { ColumnName = "C" };
            inp.Sections.Add(new InternalsSection { Name = "S", FromStage = 1, ToStage = 5, Type = InternalType.SieveTray, Diameter = 0, TraySpacing = 0.5, DowncomerClearance = 0.04 });
            var res = ColumnInternalsStudy.Rate(props, inp);
            var sr = res.Sections[0];
            Assert.That(sr.RequiredDiameter, Is.GreaterThan(0.7));
            Assert.That(sr.Diameter, Is.EqualTo(sr.RequiredDiameter));
            Assert.That(sr.LimitingStage, Is.EqualTo(5));
            Assert.That(sr.MaxFloodFraction, Is.EqualTo(0.80).Within(0.01));
            Assert.That(sr.Stages.Count, Is.EqualTo(5));
            Assert.That(sr.TotalPressureDrop, Is.GreaterThan(5000));
            Assert.That(res.TotalHeight, Is.EqualTo(2.5).Within(1e-9));
        }

        // ------------------------------------------------------------------ on a solved column

        private static DWSIM.DynamicRunner.Flowsheet Load(string filename)
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");
            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", filename));
            return flowsheet;
        }

        /// <summary>The extractive distillation sample: stage properties come out of the solved column and a
        /// sieve tray section sized for the target flood rates every tray between the condenser and the reboiler.</summary>
        [Test]
        public void ASolvedColumnIsRatedFromItsStageProfiles()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            var errors = flowsheet.SolveFlowsheet2();
            Assert.That(errors, Is.Empty, string.Join("; ", errors.Select(e => e.Message)));

            var names = ColumnInternalsStudy.ColumnNames(flowsheet);
            Assert.That(names, Is.Not.Empty, "no rigorous column on the flowsheet");
            var column = ColumnInternalsStudy.FindColumn(flowsheet, names[0]);
            int n = ColumnInternalsStudy.StageCount(column);
            Assert.That(n, Is.GreaterThan(3));

            var props = ColumnInternalsStudy.ExtractStageProperties(flowsheet, column);
            Assert.That(props.Count, Is.EqualTo(n));
            foreach (var sp in props) TestContext.Out.WriteLine("stage {0}: T={1:F2} P={2:F0} V={3:E3} L={4:E3} MWv={5:F2} MWl={6:F2} rhoV={7:F4} rhoL={8:F1} muV={9:E2} muL={10:E2} sigma={11:E3} lambda={12:F3}", sp.Stage, sp.T, sp.P, sp.VaporMassFlow, sp.LiquidMassFlow, sp.VaporMW, sp.LiquidMW, sp.VaporDensity, sp.LiquidDensity, sp.VaporViscosity, sp.LiquidViscosity, sp.SurfaceTension, sp.StrippingFactor);
            foreach (var sp in props.Skip(1).Take(n - 2))
            {
                Assert.That(sp.VaporDensity, Is.InRange(0.05, 200), "rho_V stage " + sp.Stage);
                Assert.That(sp.LiquidDensity, Is.InRange(300, 2000), "rho_L stage " + sp.Stage);
                Assert.That(sp.SurfaceTension, Is.InRange(0.001, 0.1), "sigma stage " + sp.Stage);
                Assert.That(sp.LiquidViscosity, Is.InRange(1e-5, 1), "mu_L stage " + sp.Stage);
                Assert.That(sp.VaporMassFlow, Is.GreaterThan(0), "V stage " + sp.Stage);
                Assert.That(sp.LiquidMassFlow, Is.GreaterThan(0), "L stage " + sp.Stage);
            }
            // the probe stream must not stay on the flowsheet
            Assert.That(flowsheet.SimulationObjects.Values.Count(o => o.GraphicObject.Tag.StartsWith("internals_probe_")), Is.EqualTo(0));

            var inp = new ColumnInternalsInput { ColumnName = names[0] };
            inp.Sections.Add(new InternalsSection { Name = "Trays", FromStage = 2, ToStage = n - 1, Type = InternalType.SieveTray, Diameter = 0 });
            inp.Sections.Add(new InternalsSection { Name = "Packed", FromStage = 2, ToStage = n - 1, Type = InternalType.RandomPacking, PackingName = "Pall rings Metal 50 mm", PackingModel = PackingModel.BilletSchultes, HetpModel = HetpModel.BilletSchultes, Diameter = 0 });
            var res = ColumnInternalsStudy.Run(flowsheet, inp);
            var trays = res.Sections[0];
            Assert.That(trays.Diameter, Is.InRange(0.2, 10), "sized diameter");
            Assert.That(trays.MaxFloodFraction, Is.EqualTo(0.80).Within(0.01), "sized to the target");
            Assert.That(trays.Stages.Count, Is.EqualTo(n - 2));
            Assert.That(trays.TotalPressureDrop, Is.GreaterThan(0));
            foreach (var r in trays.Stages)
            {
                Assert.That(r.FloodFraction, Is.InRange(0.05, 0.81), "flood fraction stage " + r.Stage);
                Assert.That(r.TotalHead, Is.InRange(30, 400), "h_t stage " + r.Stage);
            }
            var packed = res.Sections[1];
            Assert.That(packed.Diameter, Is.InRange(0.2, 10));
            Assert.That(packed.MaxFloodFraction, Is.EqualTo(0.70).Within(0.01));
            Assert.That(packed.BedHeight, Is.GreaterThan(0));
            Assert.That(packed.AverageHETP, Is.InRange(0.1, 3.0), "HETP");
            foreach (var r in packed.Stages) Assert.That(r.PressureDrop, Is.InRange(10, 3000), "dP/m stage " + r.Stage);
        }

        [Test]
        public void OConnellFollowsTheEduljeeFit()
        {
            Assert.That(TrayHydraulics.OConnellEfficiency(1e-3, 1.0), Is.EqualTo(0.51).Within(1e-9), "mu alpha = 1");
            Assert.That(TrayHydraulics.OConnellEfficiency(1e-4, 1.0), Is.EqualTo(0.835).Within(1e-9), "mu alpha = 0.1");
            Assert.That(TrayHydraulics.OConnellEfficiency(3e-4, 2.0), Is.EqualTo(0.51 - 0.325 * Math.Log10(0.6)).Within(1e-9));
            Assert.That(double.IsNaN(TrayHydraulics.OConnellEfficiency(1e-3, double.NaN)));
        }

        /// <summary>The rated pressure drops and O'Connell efficiencies go into the column stages and the column solves again.</summary>
        [Test]
        public void TheRatingCanBeWrittenBackIntoTheColumn()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty);
            var name = ColumnInternalsStudy.ColumnNames(flowsheet)[0];
            var column = ColumnInternalsStudy.FindColumn(flowsheet, name);
            int n = ColumnInternalsStudy.StageCount(column);
            var inp = new ColumnInternalsInput { ColumnName = name };
            inp.Sections.Add(new InternalsSection { Name = "Trays", FromStage = 2, ToStage = n - 1, Type = InternalType.SieveTray });
            var res = ColumnInternalsStudy.Run(flowsheet, inp);
            var rated = res.Sections[0].Stages.Where(r => !double.IsNaN(r.OConnellEfficiency)).ToList();
            Assert.That(rated.Count, Is.GreaterThan(n / 2), "O'Connell efficiencies on the trays");
            foreach (var r in rated) Assert.That(r.OConnellEfficiency, Is.InRange(0.1, 1.0));
            dynamic c = column;
            double topBefore = (double)((dynamic)((System.Collections.IList)c.Stages)[0]).P;
            var msg = ColumnInternalsStudy.ApplyToColumn(column, res, true, true);
            TestContext.Out.WriteLine(msg);
            var stages = (System.Collections.IList)c.Stages;
            Assert.That((double)((dynamic)stages[0]).P, Is.EqualTo(topBefore));
            Assert.That((double)((dynamic)stages[n - 1]).P, Is.GreaterThan(topBefore + 0.5 * res.Sections[0].TotalPressureDrop), "pressure rises down the column");
            Assert.That(double.IsNaN((double)c.ColumnPressureDrop), "linear profile switched off");
            Assert.That((double)((dynamic)stages[1]).Efficiency, Is.InRange(0.1, 1.0));
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty, "the column solves with the rated profile");
            var res2 = ColumnInternalsStudy.Run(flowsheet, inp);
            Assert.That(res2.Sections[0].TotalPressureDrop, Is.EqualTo(res.Sections[0].TotalPressureDrop).Within(0.25 * res.Sections[0].TotalPressureDrop));
        }

        // ------------------------------------------------------------------ valve trays (Klein)

        /// <summary>Ludwig vol. 2, Example 8-41 (Klein's procedure): venturi valves, 16 gauge (0.060 in), four legs, carbon
        /// steel, rho_V 1.91 and rho_L 31.0 lb/ft3: closed balance point 3.06 ft/s, open balance point 8.01 ft/s, and a dry
        /// drop of 1.77 in of liquid while the valves are opening.</summary>
        [Test]
        public void ValveTrayBalancePointsReproduceKleinsExample()
        {
            var rvw = TrayHydraulics.ValveLegRatio(ValveLegs.FourLegs, true);
            Assert.That(rvw, Is.EqualTo(1.45));
            var kc = TrayHydraulics.ValveClosedCoefficient(true);
            var uCbp = TrayHydraulics.ValveClosedBalanceVelocityFt(0.060, rvw, kc, 490.0, 1.91);
            Assert.That(uCbp, Is.EqualTo(3.06).Within(0.02), "closed balance point, ft/s");
            var uObp = uCbp * Math.Sqrt(kc / TrayHydraulics.ValveOpenCoefficient(true, 0.104));
            Assert.That(uObp, Is.EqualTo(8.01).Within(0.05), "open balance point, ft/s");
            Assert.That(kc * (1.91 / 31.0) * uCbp * uCbp, Is.EqualTo(1.77).Within(0.03), "dry drop between the balance points, in");
            Assert.That(TrayHydraulics.AerationFactor(1.04), Is.EqualTo(0.61).Within(0.01), "Klein's aeration factor");

            // the same tray as a section: hole area 1.63 ft2 on an active area of 9.5 ft2 (F_va = 1.04), 4.40 ft/s through the holes
            var rhoV = 1.91 * 16.01846; var rhoL = 31.0 * 16.01846;
            var sp = new StageProperties
            {
                Stage = 5, T = 350, P = 2e5,
                VaporMassFlow = 4.40 * 1.63 * 0.0283168 * rhoV, LiquidMassFlow = 205.0 / 15850.32 * rhoL,
                VaporDensity = rhoV, LiquidDensity = rhoL, VaporViscosity = 1e-5, LiquidViscosity = 3e-4, SurfaceTension = 0.02
            };
            var diameter = Math.Sqrt(4.0 / Math.PI * 9.5 * 0.09290304 / 0.76);
            var Aa = 0.76 * Math.PI / 4.0 * diameter * diameter;
            var s = new InternalsSection
            {
                Name = "Valves", FromStage = 5, ToStage = 5, Type = InternalType.ValveTray, Diameter = diameter,
                TraySpacing = 0.6, DowncomerAreaFraction = 0.12, WeirHeight = 3 * 0.0254, PlateThickness = 0.104 * 0.0254,
                ValveVenturi = true, ValveLegs = ValveLegs.FourLegs, ValveThickness = 0.060 * 0.0254, ValveDensity = 490.0 * 16.01846,
                ValveHoleDiameter = 0.0381, ValvesPerArea = 1.63 * 0.09290304 / Aa / (Math.PI / 4.0 * 0.0381 * 0.0381)
            };
            var r = TrayHydraulics.RateValveTray(s, sp, diameter, 0.7, 3.0, 1.0);
            TestContext.Out.WriteLine("valve tray: u_h={0:F3} m/s CBP={1:F3} OBP={2:F3} h_d={3:F1} mm h_L={4:F1} mm h_t={5:F1} mm regime='{6}' warnings: {7}",
                r.HoleVelocity, r.ClosedBalanceVelocity, r.OpenBalanceVelocity, r.DryPressureDrop, r.AeratedLiquidHead, r.TotalHead, r.Regime, string.Join(" | ", r.Warnings));
            Assert.That(r.HoleVelocity / 0.3048, Is.EqualTo(4.40).Within(0.05), "hole velocity, ft/s");
            Assert.That(r.ClosedBalanceVelocity / 0.3048, Is.EqualTo(3.06).Within(0.02));
            Assert.That(r.OpenBalanceVelocity / 0.3048, Is.EqualTo(8.01).Within(0.05));
            Assert.That(r.Regime, Does.Contain("opening"));
            Assert.That(r.DryPressureDrop / 25.4, Is.EqualTo(1.77).Within(0.03), "dry drop, in");
            Assert.That(r.AeratedLiquidHead, Is.EqualTo(TrayHydraulics.AerationFactor(1.04) * (76.2 + r.WeirCrest)).Within(1.0), "aerated liquid head");
            Assert.That(r.WeepRatioTurndown, Is.GreaterThan(1.0), "valves open at turndown");
            Assert.That(r.OConnellEfficiency, Is.NaN, "no relative volatility given");
        }

        /// <summary>Glitsch Bulletin 4900 example design problem (C3 splitter, 20 in spacing, two passes): CAF 0.395, downcomer design
        /// velocity 170 gpm/ft2, 68.6 % of flood on 42.94 ft2 of active area with a 32.5 in flow path, dry drop 1.75 in through 534 V-1
        /// units (16 gauge stainless, 10 gauge deck), total 3.88 in with a 181.7 in weir 2 in high, backup 7.88 in.</summary>
        [Test]
        public void GlitschProcedureReproducesTheBulletin4900Example()
        {
            Assert.That(TrayHydraulics.GlitschCaf0(2.75, 20.0), Is.EqualTo(0.395).Within(0.012), "CAF_0 from the chart");
            Assert.That(TrayHydraulics.GlitschCaf0(0.1, 24.0), Is.EqualTo(0.42).Within(0.03), "low density equation, 24 in");
            Assert.That(TrayHydraulics.GlitschCaf0(8.0, 48.0), Is.EqualTo(0.281).Within(0.01), "limit line");
            Assert.That(TrayHydraulics.GlitschDowncomerDesignVelocity(29.33, 2.75, 20.0, 1.0), Is.EqualTo(170).Within(4), "the manual rounds 172.9 to 170");
            Assert.That(TrayHydraulics.GlitschFloodFraction(8.86, 1100, 32.5, 42.94, 0.395), Is.EqualTo(0.686).Within(0.003));
            double k1, k2;
            TrayHydraulics.GlitschCoefficients(false, 0.134, out k1, out k2);
            Assert.That(k1, Is.EqualTo(0.20)); Assert.That(k2, Is.EqualTo(0.86));
            var vh = 27.52 / (534 / 78.5);
            Assert.That(vh * vh * 2.75 / 29.33, Is.EqualTo(1.55).Within(0.02), "V_H^2 D_V/D_L");
            var dry = TrayHydraulics.GlitschDryDropIn(0.060, 500, 29.33, 2.75, vh, k1, k2);
            Assert.That(dry, Is.EqualTo(1.75).Within(0.08), "dry drop (the manual read 1.75 off its nomogram drawn for a 510 lb/ft3 valve)");
            var how = 0.4 * Math.Pow(1100 / 181.7, 2.0 / 3.0);
            Assert.That(how, Is.EqualTo(1.33).Within(0.02));
            Assert.That(dry + how + 0.4 * 2.0, Is.EqualTo(3.88).Within(0.1), "total pressure drop");
            var vud = 1100 / (448.83 * 4.0);
            var hud = 0.65 * vud * vud;
            Assert.That(hud, Is.EqualTo(0.25).Within(0.01));
            Assert.That(2.0 + how + (dry + how + 0.8 + hud) * 29.33 / (29.33 - 2.75), Is.EqualTo(7.88).Within(0.15), "downcomer backup");
            Assert.That(TrayHydraulics.GlitschLeakagePoint(false, 3.33), Is.EqualTo(0.73).Within(0.02), "leakage point at the example's liquid level");

            // the same loads on a single-pass tray through the section rating
            var rhoV = 2.75 * 16.01846; var rhoL = 29.33 * 16.01846;
            var sp = new StageProperties
            {
                Stage = 3, T = 300, P = 1.7e6, VaporMassFlow = 27.52 * 0.0283168 * rhoV, LiquidMassFlow = 1100 / 15850.32 * rhoL,
                VaporDensity = rhoV, LiquidDensity = rhoL, VaporViscosity = 9e-6, LiquidViscosity = 1e-4, SurfaceTension = 0.008
            };
            var s = new InternalsSection
            {
                Name = "Ballast", FromStage = 3, ToStage = 3, Type = InternalType.ValveTray, ValveModel = ValveTrayModel.Glitsch, Diameter = 9 * 0.3048,
                TraySpacing = 20 * 0.0254, DowncomerAreaFraction = 0.163, WeirHeight = 2 * 0.0254, PlateThickness = 0.134 * 0.0254, DowncomerClearance = 4 * 0.0254,
                ValveThickness = 0.060 * 0.0254, ValveDensity = 500 * 16.01846, ValveVenturi = false, ValvesPerArea = 534 / (42.94 * 0.09290304)
            };
            var r = TrayHydraulics.RateTray(s, sp, 9 * 0.3048, 0.7, 3.0, 1.0);
            TestContext.Out.WriteLine("Glitsch single pass: flood {0:F2}, downcomer {1:F2}, dry {2:F1} mm, h_t {3:F1} mm, backup {4:F0} mm (limit {5:F0}), leak ratio {6:F2}; {7}; {8}",
                r.FloodFraction, r.DowncomerFloodFraction, r.DryPressureDrop, r.TotalHead, r.DowncomerBackup, r.DowncomerBackupLimit, r.WeepRatio, r.Regime, string.Join(" | ", r.Warnings));
            Assert.That(r.Regime, Does.StartWith("Glitsch V-1"));
            Assert.That(r.DryPressureDrop / 25.4, Is.EqualTo(1.75).Within(0.1), "dry drop through the section (the hole area follows the valve count)");
            Assert.That(r.FloodFraction, Is.InRange(0.7, 1.1), "single pass flow path is twice the example's, so the liquid term is larger");
            Assert.That(r.DowncomerFloodFraction, Is.InRange(0.3, 1.2));
            var d = TrayHydraulics.DiameterForFloodFraction(s, sp, 0.7);
            Assert.That(d, Is.InRange(2.0, 4.5), "sized for 70 % flood, m");
            var back = ColumnInternalsInput.FromXml(new ColumnInternalsInput { Sections = { s } }.ToXml()).Sections[0];
            Assert.That(back.ValveModel, Is.EqualTo(ValveTrayModel.Glitsch));
        }

        // ------------------------------------------------------------------ bubble caps (Bolles)

        /// <summary>Ludwig vol. 2, Example 8-36, top tray of the 6 ft vacuum finishing tower: 129 caps of 3 7/8 in ID with
        /// 2.68 in risers on 5.5 in centres, 50 slots 1/8 x 1.5 in per cap, 0.5 in static seal, 4 ft weir 2.5 in high;
        /// 132.2 ft3/s of vapour at 0.0138 lb/ft3 and 3.74 gpm of liquid at 50.5 lb/ft3. Bolles: h_pc 0.118 in,
        /// h_s 0.626 in, h_ow 0.099 in, gradient 0.12 in, total 1.50 in of liquid.</summary>
        [Test]
        public void BubbleCapTrayReproducesLudwigExample836()
        {
            Assert.That(TrayHydraulics.BollesCapConstant(1.073), Is.EqualTo(0.598).Within(0.01), "K_c from Fig. 8-114");
            Assert.That(TrayHydraulics.BollesCapDropIn(0.598, 0.0138, 50.5, 132.2, 4.95), Is.EqualTo(0.118).Within(0.003), "h_pc, in");
            Assert.That(TrayHydraulics.BollesSlotOpeningIn(0.0138, 50.5, 132.2, 129, 50, 0.125), Is.EqualTo(0.626).Within(0.01), "h_s, in");
            Assert.That(TrayHydraulics.BollesGradientPerRow(0.75, 0.375, 2.7, 0.75), Is.EqualTo(0.02).Within(0.008), "gradient per row, in");
            Assert.That(TrayHydraulics.DaviesVaporCorrection(0.548, 0.75), Is.EqualTo(0.55).Within(0.03), "C_v");

            var rhoV = 0.0138 * 16.01846; var rhoL = 50.5 * 16.01846;
            var sp = new StageProperties
            {
                Stage = 2, T = 330, P = 1e4,
                VaporMassFlow = 132.2 * 0.0283168 * rhoV, LiquidMassFlow = 0.00834 * 0.0283168 * rhoL,
                VaporDensity = rhoV, LiquidDensity = rhoL, VaporViscosity = 8e-6, LiquidViscosity = 5e-4, SurfaceTension = 0.025
            };
            var capId = 3.875 * 0.0254;
            var s = new InternalsSection
            {
                Name = "Caps", FromStage = 2, ToStage = 2, Type = InternalType.BubbleCapTray, Diameter = 6 * 0.3048,
                TraySpacing = 24 * 0.0254, DowncomerAreaFraction = TrayHydraulics.SegmentAreaFraction(4.0 / 6.0), WeirHeight = 2.5 * 0.0254,
                DowncomerClearance = 2.25 * 0.0254, CapDiameter = capId, RiserDiameter = 2.68 * 0.0254, CapPitchRatio = 5.5 * 0.0254 / (capId + 0.004),
                SlotsPerCap = 50, SlotWidth = 0.125 * 0.0254, SlotHeight = 1.5 * 0.0254, StaticSeal = 0.5 * 0.0254, SkirtClearance = 0.75 * 0.0254
            };
            var r = TrayHydraulics.RateBubbleCapTray(s, sp, 6 * 0.3048, 0.7, 5.0, 1.0);
            TestContext.Out.WriteLine("bubble caps: h_pc={0:F2} h_s={1:F2} h_ow={2:F2} delta={3:F2} h_t={4:F2} in; slot load {5:F2}, {6}; backup {7:F1} mm; warnings: {8}",
                r.CapPressureDrop / 25.4, r.SlotOpening / 25.4, r.WeirCrest / 25.4, r.LiquidGradient / 25.4, r.TotalHead / 25.4, r.SlotLoad, r.Regime, r.DowncomerBackup, string.Join(" | ", r.Warnings));
            Assert.That(r.CapPressureDrop / 25.4, Is.EqualTo(0.118).Within(0.015), "h_pc (the cap count comes out within 3 % of the book's)");
            Assert.That(r.SlotOpening / 25.4, Is.EqualTo(0.626).Within(0.03), "h_s");
            Assert.That(r.WeirCrest / 25.4, Is.EqualTo(0.099).Within(0.01), "h_ow");
            Assert.That(r.LiquidGradient / 25.4, Is.EqualTo(0.12).Within(0.06), "liquid gradient");
            Assert.That(r.TotalHead / 25.4, Is.EqualTo(1.50).Within(0.2), "h_t");
            Assert.That(r.SlotLoad, Is.InRange(0.2, 0.35), "slot load (the book finds the slot velocity low)");
            Assert.That(r.VaporDistributionRatio, Is.LessThan(0.5));
            Assert.That(r.Regime, Does.Contain("slots"));
            Assert.That(r.Warnings.Any(w => w.Contains("pulse")), "the book too finds the slot opening on the low side");

            // the same tray by the modified Dauphine relations: h_r 0.0633, h_ra 0.045, h'_s 0.0308, C_w 0.16, h_c 0.87, h_t 1.528 in
            Assert.That(TrayHydraulics.DauphineRiserDropIn(2.68, 0.0138, 50.5, 132.2, 4.95, 5.43, 7.95), Is.EqualTo(0.0633).Within(0.003), "h_r");
            Assert.That(TrayHydraulics.DauphineReversalDropIn(0.0138, 50.5, 132.2, 4.95, 5.43, 7.95, 11.79), Is.EqualTo(0.045).Within(0.004), "h_ra");
            Assert.That(TrayHydraulics.DauphineDrySlotDropIn(3.875, 0.0138, 50.5, 132.2, 8.40), Is.EqualTo(0.0308).Within(0.002), "h'_s");
            Assert.That(TrayHydraulics.DauphineWetCapCorrection(0.33), Is.EqualTo(0.16).Within(0.03), "C_w");
            s.CapMethod = BubbleCapMethod.Dauphine; s.RiserHeight = 3.0 * 0.0254; s.CapInsideHeight = 3.94 * 0.0254;
            var rd = TrayHydraulics.RateBubbleCapTray(s, sp, 6 * 0.3048, 0.7, 5.0, 1.0);
            TestContext.Out.WriteLine("Dauphine: h_r+h_ra={0:F3} h_c={1:F2} h_c,max={2:F2} h_t={3:F2} in; warnings: {4}", rd.CapPressureDrop / 25.4, rd.DryPressureDrop / 25.4, rd.CapDropLimit / 25.4, rd.TotalHead / 25.4, string.Join(" | ", rd.Warnings));
            Assert.That(rd.CapPressureDrop / 25.4, Is.EqualTo(0.108).Within(0.015), "h_r + h_ra");
            Assert.That(rd.DryPressureDrop / 25.4, Is.EqualTo(0.87).Within(0.12), "wet cap drop h_c");
            Assert.That(rd.TotalHead / 25.4, Is.EqualTo(1.528).Within(0.2), "h_t by Dauphine");
            Assert.That(rd.DryPressureDrop, Is.LessThan(rd.CapDropLimit), "the cap is not blowing under the shroud ring (book: 0.87 against 1.8 in)");
        }

        /// <summary>Bolles' generalised slot correlation (Figure 8-107): rectangular slots open as (V/V_m)^(2/3), triangular ones as (V/V_m)^0.4,
        /// and the capacity coefficients are 0.63, 0.74 and 0.79 for top over bottom widths of 0, 0.5 and 1.</summary>
        [Test]
        public void TrapezoidalSlotsFollowBollesGeneralisedCorrelation()
        {
            Assert.That(TrayHydraulics.SlotOpeningFraction(0.25, 1.0), Is.EqualTo(Math.Pow(0.25, 2.0 / 3.0)).Within(1e-6), "rectangular");
            Assert.That(TrayHydraulics.SlotOpeningFraction(0.25, 0.0), Is.EqualTo(Math.Pow(0.25, 0.4)).Within(1e-6), "triangular");
            Assert.That(TrayHydraulics.SlotOpeningFraction(0.5, 0.5), Is.InRange(0.63, 0.72), "trapezoidal, between the two");
            Assert.That(TrayHydraulics.SlotOpeningFraction(1.0, 0.5), Is.EqualTo(1.0).Within(1e-6), "fully open at the capacity");
            var k = Math.Sqrt(1.5 * 50.49 / 0.0138);
            Assert.That(TrayHydraulics.BollesSlotCapacityFt3s(1.0, 1.5, 0.0138, 50.5, 0.0) / k, Is.EqualTo(0.63).Within(0.005));
            Assert.That(TrayHydraulics.BollesSlotCapacityFt3s(1.0, 1.5, 0.0138, 50.5, 0.5) / k, Is.EqualTo(0.74).Within(0.01));
            Assert.That(TrayHydraulics.BollesSlotCapacityFt3s(1.0, 1.5, 0.0138, 50.5, 1.0) / k, Is.EqualTo(0.79).Within(0.005));
            var inp = new ColumnInternalsInput();
            inp.Sections.Add(new InternalsSection { Type = InternalType.BubbleCapTray, SlotTopWidthRatio = 0.5, CapMethod = BubbleCapMethod.Dauphine, RiserHeight = 0.08, CapInsideHeight = 0.11 });
            var back = ColumnInternalsInput.FromXml(inp.ToXml()).Sections[0];
            Assert.That(back.SlotTopWidthRatio, Is.EqualTo(0.5)); Assert.That(back.CapMethod, Is.EqualTo(BubbleCapMethod.Dauphine)); Assert.That(back.CapInsideHeight, Is.EqualTo(0.11));
        }

        // ------------------------------------------------------------------ structured packings (Rocha, Bravo and Fair)

        /// <summary>Mellapak 250Y at total reflux with cyclohexane / n-heptane-like properties at 1 atm: the model must be
        /// self-consistent (pressure drop rising with the vapour rate, no solution above its own flood velocity, holdup
        /// and HETP in the range the packing shows in practice, 0.3 to 0.6 m).</summary>
        [Test]
        public void RochaBravoFairIsSelfConsistentOnMellapak250Y()
        {
            var p = PackingCatalogue.Find("Mellapak Sheet metal 250Y");
            Assert.That(p, Is.Not.Null);
            Assert.That(p.CorrugationSide, Is.EqualTo(0.0171));
            Assert.That(PackingCatalogue.Find("Flexipac Sheet metal 2").EffectiveCorrugationSide, Is.EqualTo(0.0177));
            var estimated = PackingCatalogue.Find("Mellapak Sheet metal 350Y").EffectiveCorrugationSide;
            Assert.That(estimated, Is.InRange(0.010, 0.014), "estimated side of a packing without measured geometry");

            double rhoV = 3.0, rhoL = 650, muV = 8e-6, muL = 3e-4, sigma = 0.018;
            var dpFlood = PackingHydraulics.KisterGillFloodPressureDrop(p.Fp);
            Assert.That(dpFlood, Is.EqualTo(770).Within(30), "Kister-Gill flood pressure drop, Pa/m");
            var uf = PackingHydraulics.RbfFloodingVelocity(1.0, rhoV, rhoL, muV, muL, sigma, p.CorrugationSide, p.Epsilon, p.CorrugationAngle, dpFlood);
            TestContext.Out.WriteLine("RBF flood: u_V,f = {0:F3} m/s (F = {1:F2} Pa^0.5)", uf, uf * Math.Sqrt(rhoV));
            Assert.That(uf * Math.Sqrt(rhoV), Is.InRange(2.2, 4.0), "flood F-factor of a 250 m2/m3 sheet packing");
            double lastDp = 0;
            foreach (var f in new[] { 1.0, 1.5, 2.0, 2.5 })
            {
                var uV = f / Math.Sqrt(rhoV); var uL = uV * rhoV / rhoL;
                double hold;
                var dp = PackingHydraulics.RbfPressureDrop(uV, uL, rhoV, rhoL, muV, muL, sigma, p.CorrugationSide, p.Epsilon, p.CorrugationAngle, dpFlood, out hold);
                TestContext.Out.WriteLine("F={0:F1}: dP={1:F0} Pa/m holdup={2:F4}", f, dp, hold);
                if (uV < uf)
                {
                    Assert.That(dp, Is.GreaterThan(lastDp), "pressure drop rises with the vapour rate");
                    Assert.That(dp, Is.LessThan(dpFlood));
                    Assert.That(hold, Is.InRange(0.01, 0.15), "holdup");
                    lastDp = dp;
                }
                else Assert.That(double.IsNaN(dp), "flooded above the flood velocity");
            }
            double hold2;
            Assert.That(double.IsNaN(PackingHydraulics.RbfPressureDrop(1.05 * uf, 1.05 * uf * rhoV / rhoL, rhoV, rhoL, muV, muL, sigma, p.CorrugationSide, p.Epsilon, p.CorrugationAngle, dpFlood, out hold2)));

            var s = new InternalsSection { Name = "Bed", FromStage = 3, ToStage = 3, Type = InternalType.StructuredPacking, PackingName = p.DisplayName, PackingModel = PackingModel.RochaBravoFair, HetpModel = HetpModel.RochaBravoFair, Diameter = 1.0 };
            var uVd = 2.0 / Math.Sqrt(rhoV);
            var A = Math.PI / 4.0;
            var sp = new StageProperties
            {
                Stage = 3, T = 350, P = 101325, VaporMassFlow = uVd * A * rhoV, LiquidMassFlow = uVd * A * rhoV,
                VaporDensity = rhoV, LiquidDensity = rhoL, VaporViscosity = muV, LiquidViscosity = muL, SurfaceTension = sigma,
                VaporDiffusivity = 4e-6, LiquidDiffusivity = 4e-9, StrippingFactor = 1.0
            };
            var r = PackingHydraulics.RatePacking(s, p, sp, 1.0);
            TestContext.Out.WriteLine("RBF rating at F=2: flood {0:F2}, dP {1:F0} Pa/m, holdup {2:F4}, HG {3:F3} HL {4:F3} HETP {5:F3} (rule of thumb {6:F2}); {7}",
                r.FloodFraction, r.PressureDrop, r.LiquidHoldup, r.HG, r.HL, r.HETP, r.HETPRuleOfThumb, string.Join(" | ", r.Warnings));
            Assert.That(r.FloodFraction, Is.InRange(0.5, 0.95));
            Assert.That(r.HETP, Is.InRange(0.25, 0.8), "HETP of Mellapak 250Y");
            Assert.That(r.HG, Is.GreaterThan(0)); Assert.That(r.HL, Is.GreaterThan(0));

            // a random packing asked for the structured model falls back with a note
            var pall = PackingCatalogue.Find("Pall rings Metal 50 mm");
            var s2 = new InternalsSection { Name = "Bed", FromStage = 3, ToStage = 3, Type = InternalType.RandomPacking, PackingName = pall.DisplayName, PackingModel = PackingModel.RochaBravoFair, HetpModel = HetpModel.RochaBravoFair, Diameter = 1.0 };
            var r2 = PackingHydraulics.RatePacking(s2, pall, sp, 1.0);
            Assert.That(r2.Warnings.Any(w => w.Contains("Robbins")), "falls back to Robbins");
            Assert.That(r2.PressureDrop, Is.GreaterThan(0));
        }

        [Test]
        public void TheNewGeometryFieldsRoundTripThroughTheCaseFile()
        {
            var inp = new ColumnInternalsInput { ColumnName = "C1", MaxIterations = 3, IterationTolerance = 0.05, IteratePressures = true, IterateEfficiencies = false };
            inp.Sections.Add(new InternalsSection { Name = "Valves", Type = InternalType.ValveTray, ValvesPerArea = 150, ValveVenturi = true, ValveLegs = ValveLegs.Caged, ValveThickness = 0.002, ValveDensity = 8030 });
            inp.Sections.Add(new InternalsSection { Name = "Caps", Type = InternalType.BubbleCapTray, CapDiameter = 0.1, RiserDiameter = 0.07, SlotsPerCap = 40, SlotWidth = 0.004, SlotHeight = 0.03, StaticSeal = 0.02, SkirtClearance = 0.02, LiquidGradient = 0.01 });
            inp.Sections.Add(new InternalsSection { Name = "Bed", Type = InternalType.StructuredPacking, PackingModel = PackingModel.RochaBravoFair, HetpModel = HetpModel.RochaBravoFair, CustomPacking = new PackingData { Name = "X", Structured = true, a = 300, Epsilon = 0.96, Fp = 80, CorrugationSide = 0.015, SurfaceEnhancement = 0.3 } });
            var back = ColumnInternalsInput.FromXml(inp.ToXml());
            Assert.That(back.MaxIterations, Is.EqualTo(3)); Assert.That(back.IterationTolerance, Is.EqualTo(0.05)); Assert.That(back.IterateEfficiencies, Is.False);
            Assert.That(back.Sections[0].ValvesPerArea, Is.EqualTo(150)); Assert.That(back.Sections[0].ValveVenturi, Is.True); Assert.That(back.Sections[0].ValveLegs, Is.EqualTo(ValveLegs.Caged));
            Assert.That(back.Sections[1].SlotsPerCap, Is.EqualTo(40)); Assert.That(back.Sections[1].LiquidGradient, Is.EqualTo(0.01)); Assert.That(back.Sections[1].StaticSeal, Is.EqualTo(0.02));
            Assert.That(back.Sections[2].PackingModel, Is.EqualTo(PackingModel.RochaBravoFair)); Assert.That(back.Sections[2].HetpModel, Is.EqualTo(HetpModel.RochaBravoFair));
            Assert.That(back.Sections[2].CustomPacking.CorrugationSide, Is.EqualTo(0.015)); Assert.That(back.Sections[2].CustomPacking.SurfaceEnhancement, Is.EqualTo(0.3));
        }

        /// <summary>Every tray type rates the stages of the extractive distillation column, and the automatic iteration
        /// settles the pressure profile with the column solver.</summary>
        [Test]
        public void TheIterationSettlesTheRatedProfileWithTheSolver()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty);
            var name = ColumnInternalsStudy.ColumnNames(flowsheet)[0];
            var column = ColumnInternalsStudy.FindColumn(flowsheet, name);
            int n = ColumnInternalsStudy.StageCount(column);

            var all = new ColumnInternalsInput { ColumnName = name };
            all.Sections.Add(new InternalsSection { Name = "Valves", FromStage = 2, ToStage = n - 1, Type = InternalType.ValveTray });
            all.Sections.Add(new InternalsSection { Name = "Caps", FromStage = 2, ToStage = n - 1, Type = InternalType.BubbleCapTray });
            all.Sections.Add(new InternalsSection { Name = "Sheets", FromStage = 2, ToStage = n - 1, Type = InternalType.StructuredPacking, PackingName = "Mellapak Sheet metal 250Y", PackingModel = PackingModel.RochaBravoFair, HetpModel = HetpModel.RochaBravoFair });
            var res = ColumnInternalsStudy.Run(flowsheet, all);
            foreach (var sr in res.Sections)
            {
                TestContext.Out.WriteLine("{0}: D={1:F2} m, max flood {2:F2} on stage {3}, dP {4:F0} Pa; {5}", sr.Section.Name, sr.Diameter, sr.MaxFloodFraction, sr.LimitingStage, sr.TotalPressureDrop, string.Join(" | ", sr.Warnings));
                Assert.That(sr.Diameter, Is.InRange(0.2, 10), sr.Section.Name + " diameter");
                Assert.That(sr.TotalPressureDrop, Is.GreaterThan(0), sr.Section.Name + " pressure drop");
                foreach (var r in sr.Stages.Where(x => !double.IsNaN(x.FloodFraction)))
                {
                    Assert.That(r.PressureDropTotal, Is.InRange(1, 5000), sr.Section.Name + " stage " + r.Stage);
                    if (sr.Section.IsTray) Assert.That(r.Regime, Is.Not.Empty);
                }
            }
            Assert.That(res.Sections[2].AverageHETP, Is.InRange(0.15, 1.5), "structured packing HETP");

            var inp = new ColumnInternalsInput { ColumnName = name, MaxIterations = 4, IterationTolerance = 0.03 };
            inp.Sections.Add(new InternalsSection { Name = "Trays", FromStage = 2, ToStage = n - 1, Type = InternalType.ValveTray });
            var it = ColumnInternalsStudy.RunIterating(flowsheet, inp, line => TestContext.Out.WriteLine(line));
            foreach (var l in it.Log) TestContext.Out.WriteLine(l);
            Assert.That(it.Iterations, Is.GreaterThanOrEqualTo(1));
            Assert.That(column.Calculated, "the column is solved at the end");
            Assert.That(it.Converged, "the iteration settled: " + string.Join(" | ", it.Log));
            dynamic c = column;
            Assert.That(double.IsNaN((double)c.ColumnPressureDrop), "the column carries the rated stage pressures");
        }

        /// <summary>A packed section with a bed height sets the number of stages of the column from the HETP: stages are added
        /// evenly or removed where nothing is connected, the feeds keep their stages, the case is kept in the column and the
        /// column solves again with the new count.</summary>
        [Test]
        public void PackedBedsRestageTheColumnFromTheBedHeight()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty);
            var name = ColumnInternalsStudy.ColumnNames(flowsheet)[0];
            var column = (DWSIM.UnitOperations.UnitOperations.Column)ColumnInternalsStudy.FindColumn(flowsheet, name);
            int n = column.Stages.Count;
            var feedIds = column.MaterialStreams.Values.Where(si => si.StreamBehavior == DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.StreamInformation.Behavior.Feed).Select(si => si.AssociatedStage).ToList();
            Assert.That(feedIds, Is.Not.Empty);
            var feedStagesBefore = feedIds.Select(id => column.StageIndex(id)).ToList();

            var inp = new ColumnInternalsInput { ColumnName = name, MaxIterations = 3, IterationTolerance = 0.05 };
            var bed = new InternalsSection { Name = "Bed", FromStage = 2, ToStage = n - 1, Type = InternalType.RandomPacking, PackingName = "Pall rings Metal 50 mm", Diameter = 0 };
            inp.Sections.Add(bed);
            var first = ColumnInternalsStudy.Run(flowsheet, inp);
            var hetp = first.Sections[0].AverageHETP;
            Assert.That(hetp, Is.InRange(0.1, 3.0));

            // a bed 1.5 times taller than the stages the column has: stages are inserted
            bed.BedHeight = 1.5 * (n - 2) * hetp;
            var msg = ColumnInternalsStudy.ApplyStagesToColumn(column, first, inp);
            TestContext.Out.WriteLine(msg);
            int expected = (int)Math.Round(bed.BedHeight / hetp);
            Assert.That(column.Stages.Count, Is.EqualTo(expected + 2), "stages after growing the bed");
            Assert.That(column.NumberOfStages, Is.EqualTo(column.Stages.Count));
            Assert.That(bed.ToStage, Is.EqualTo(column.Stages.Count - 1), "the section range follows");
            foreach (var id in feedIds) Assert.That(column.StageIndex(id), Is.InRange(1, column.Stages.Count - 2), "the feed keeps a stage inside the column");
            Assert.That(column.Stages[3].StageHeight, Is.EqualTo(hetp).Within(0.5 * hetp).Or.EqualTo(0.0), "stage heights carry the HETP where rated");
            Assert.That(column.EstimatedDiameter, Is.InRange(0.2, 10));
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty, "the column solves with the new stages");
            Assert.That(column.Calculated);

            // the case lives in the column
            ColumnInternalsStudy.StoreCaseInColumn(column, inp);
            var kept = ColumnInternalsStudy.LoadCaseFromColumn(column);
            Assert.That(kept, Is.Not.Null);
            Assert.That(kept.Sections[0].ToStage, Is.EqualTo(bed.ToStage));
            Assert.That(column.SaveData().Any(x => x.Name == "InternalsCase"), "saved with the column");

            // a shorter bed: stages without connections are removed and the feeds stay
            var second = ColumnInternalsStudy.Run(flowsheet, inp);
            bed.BedHeight = 0.5 * (bed.ToStage - bed.FromStage + 1) * second.Sections[0].AverageHETP;
            int before = column.Stages.Count;
            msg = ColumnInternalsStudy.ApplyStagesToColumn(column, second, inp);
            TestContext.Out.WriteLine(msg);
            Assert.That(column.Stages.Count, Is.LessThan(before));
            foreach (var id in feedIds) Assert.That(column.StageIndex(id), Is.InRange(1, column.Stages.Count - 2), "the feed stage was not removed");
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty, "the column solves after the shrink");

            // the iteration re-stages on its own and settles
            bed.BedHeight = (bed.ToStage - bed.FromStage + 1) * second.Sections[0].AverageHETP * 1.2;
            var it = ColumnInternalsStudy.RunIterating(flowsheet, inp, line => TestContext.Out.WriteLine(line));
            Assert.That(it.Input, Is.Not.Null);
            Assert.That(column.Calculated);
            Assert.That(it.Input.Sections[0].ToStage - it.Input.Sections[0].FromStage + 1, Is.EqualTo(column.Stages.Count - 2), "the case follows the column");
        }

        /// <summary>The dynamic column reads a packed stage through its bed correlations: the pressure drop and holdup inversions recover
        /// the velocities they were built from, the rating marks the stages of a packed section, and the dynamic model runs on them.</summary>
        [Test]
        public void PackedStagesDriveTheDynamicColumnThroughTheBedCorrelations()
        {
            var pall = PackingCatalogue.Find("Pall rings Metal 50 mm");
            var mella = PackingCatalogue.Find("Mellapak Sheet metal 250Y");
            double rhoV = 3.0, rhoL = 650, muV = 8e-6, muL = 3e-4, sigma = 0.018;
            foreach (var pk in new[] { pall, mella })
            {
                var st = new DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.Stage(Guid.NewGuid().ToString()) { PackingModel = pk.Structured ? 2 : 0 };
                double uV = 1.0, uL = 0.005;
                var dp = DWSIM.UnitOperations.UnitOperations.Column.PackedBedPressureDrop(st, pk, uV, uL, rhoV, rhoL, muV, muL, sigma, 1.0);
                Assert.That(dp, Is.InRange(20, 2000), pk.DisplayName + " dP/m");
                var back = DWSIM.UnitOperations.UnitOperations.Column.PackedBedVaporVelocity(st, pk, dp, uL, rhoV, rhoL, muV, muL, sigma, 1.0);
                Assert.That(back, Is.EqualTo(uV).Within(1e-3), pk.DisplayName + " vapour velocity recovered from its pressure drop");
                var hold = DWSIM.UnitOperations.UnitOperations.Column.PackedBedHoldup(st, pk, uL, rhoV, rhoL, muL, sigma);
                Assert.That(hold, Is.InRange(0.005, 0.2), pk.DisplayName + " holdup");
                var uLback = DWSIM.UnitOperations.UnitOperations.Column.PackedBedLiquidVelocity(st, pk, hold, rhoV, rhoL, muL, sigma);
                Assert.That(uLback, Is.EqualTo(uL).Within(1e-5), pk.DisplayName + " liquid velocity recovered from its holdup");
                TestContext.Out.WriteLine("{0}: dP {1:F0} Pa/m at u_V {2}, holdup {3:F4} at u_L {4}", pk.DisplayName, dp, uV, hold, uL);
            }

            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty);
            var name = ColumnInternalsStudy.ColumnNames(flowsheet)[0];
            var column = (DWSIM.UnitOperations.UnitOperations.Column)ColumnInternalsStudy.FindColumn(flowsheet, name);
            int n = column.Stages.Count;
            var inp = new ColumnInternalsInput { ColumnName = name };
            inp.Sections.Add(new InternalsSection { Name = "Bed", FromStage = 2, ToStage = n - 1, Type = InternalType.RandomPacking, PackingName = pall.DisplayName });
            var res = ColumnInternalsStudy.Run(flowsheet, inp);
            ColumnInternalsStudy.ApplyToColumn(column, res, true, true);
            Assert.That(column.Stages.Skip(1).Take(n - 2).All(x => x.IsPacked), "the bed stages are marked packed");
            Assert.That(column.Stages[0].IsPacked, Is.False);
            Assert.That(column.Stages[2].PackingName, Is.EqualTo(pall.DisplayName));
            Assert.That(column.Stages[2].StageHeight, Is.InRange(0.05, 3.0));
            Assert.That(string.Concat(column.SaveData().Select(x => x.ToString())), Does.Contain("IsPacked"), "the packing travels with the stage");
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty);

            // a few seconds of dynamics on the packed column
            column.CreateDynamicProperties();
            var integrator = new DWSIM.DynamicsManager.Integrator { ID = "packed", Description = "packed", IntegrationStep = TimeSpan.FromSeconds(1), Duration = TimeSpan.FromSeconds(5), RealTime = false };
            var schedule = new DWSIM.DynamicsManager.Schedule { ID = "packed", Description = "packed", CurrentIntegrator = "packed", UseCurrentStateAsInitial = true };
            flowsheet.DynamicsManager.IntegratorList["packed"] = integrator;
            flowsheet.DynamicsManager.ScheduleList["packed"] = schedule;
            flowsheet.DynamicsManager.CurrentSchedule = "packed";
            var result = new DWSIM.Automation.DynamicRunner.IntegratorRunner(flowsheet).Run(new DWSIM.Automation.DynamicRunner.IntegratorRunOptions { Schedule = "packed", RestoreInitialState = false });
            TestContext.Out.WriteLine("dynamics: {0}", result);
            for (int i = 1; i < n - 1; i++)
            {
                Assert.That(column.Stages[i].Vout.Value, Is.GreaterThan(0).And.LessThan(1e6), "vapour leaving packed stage " + (i + 1));
                Assert.That(column.Stages[i].Lout.Value, Is.GreaterThan(0).And.LessThan(1e6), "liquid leaving packed stage " + (i + 1));
            }
        }

        // ------------------------------------------------------------------ rate-based column

        /// <summary>Perry 7th ed. Example 12 (ethylbenzene-styrene sieve tray): with h_L 23.23 mm, froth density 0.284, 74 % of flood,
        /// D_G 2.09e-5 and D_L 3.74e-9 m2/s, lambda 1.17 the Chan-Fair route gives N_G 1.51, N_L 18.6 and E_OG 0.75.</summary>
        [Test]
        public void ChanFairPointEfficiencyReproducesPerryExample12()
        {
            double phi = 0.284, hL = 0.02323, Aa = 4.41, QG = 14.73, QL = 0.00727;
            var tG = (1 - phi) * hL * Aa / (phi * QG);
            Assert.That(tG, Is.EqualTo(0.0175).Within(0.0005));
            var ng = TrayMassTransfer.GasTransferUnits(TrayMassTransferMethod.ChanFair, 0.05, 0, 0, 1, 2.09e-5, hL, phi, tG, 0.74);
            Assert.That(ng, Is.EqualTo(1.51).Within(0.03), "N_G");
            var tL = hL * Aa / QL;
            Assert.That(tL, Is.EqualTo(14.1).Within(0.2));
            var nl = TrayMassTransfer.LiquidTransferUnits(3.74e-9, 3.34 * Math.Sqrt(0.481), tL);
            Assert.That(nl, Is.EqualTo(18.6).Within(0.6), "N_L");
            var nog = 1 / (1 / ng + 1.17 / nl);
            Assert.That(1 - Math.Exp(-nog), Is.EqualTo(0.75).Within(0.01), "E_OG");
            Assert.That(TrayMassTransfer.FrothDensity(3.34, 0.481, 841), Is.EqualTo(0.284).Within(0.01), "froth density");
            Assert.That(TrayMassTransfer.MurphreeFromPoint(0.75, 1.17, 0), Is.EqualTo(0.75).Within(1e-9), "fully mixed liquid");
            Assert.That(TrayMassTransfer.MurphreeFromPoint(0.75, 1.17, 1e5), Is.EqualTo((Math.Exp(1.17 * 0.75) - 1) / 1.17).Within(1e-6), "plug flow");
            var partial = TrayMassTransfer.MurphreeFromPoint(0.75, 1.17, 10);
            Assert.That(partial, Is.InRange(0.75, (Math.Exp(1.17 * 0.75) - 1) / 1.17), "partial mixing between the limits");
            Assert.That(TrayMassTransfer.PackedStageEfficiency(1.0, 1.5), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(TrayMassTransfer.PackedStageEfficiency(0.5, 1.0), Is.EqualTo(0.5).Within(1e-9));
            var dg = Diffusivities.Fuller(298.15, 101325, 78.1, 28.0, 90.7, 17.9);
            Assert.That(dg, Is.EqualTo(0.96e-5).Within(0.15e-5), "benzene in nitrogen, Fuller");
            var dl = Diffusivities.WilkeChang(298.15, 0.89e-3, 18.0, 2.6, 96.5);
            Assert.That(dl, Is.EqualTo(1.0e-9).Within(0.3e-9), "benzene in water, Wilke-Chang");
        }

        /// <summary>The rate-based column: efficiencies from the mass transfer on the sieve trays of the extractive distillation
        /// sample, settled by the solve-rate-solve passes, on the Wang-Henke and on the Naphtali-Sandholm solvers, and on a packed bed.</summary>
        [Test]
        public void TheRateBasedColumnSettlesItsEfficienciesFromMassTransfer()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty);
            var name = ColumnInternalsStudy.ColumnNames(flowsheet)[0];
            var column = (DWSIM.UnitOperations.UnitOperations.Column)ColumnInternalsStudy.FindColumn(flowsheet, name);
            int n = column.Stages.Count;
            var inp = new ColumnInternalsInput { ColumnName = name };
            inp.Sections.Add(new InternalsSection { Name = "Trays", FromStage = 2, ToStage = n - 1, Type = InternalType.SieveTray });
            var rated = ColumnInternalsStudy.Run(flowsheet, inp);
            ColumnInternalsStudy.ApplyToColumn(column, rated, false, false);   // diameter and heights
            ColumnInternalsStudy.StoreCaseInColumn(column, inp);
            var oconnell = rated.Sections[0].Stages.Where(r => !double.IsNaN(r.OConnellEfficiency)).Select(r => r.OConnellEfficiency).Average();

            column.RateBased = true;
            column.RateBasedTrayMethod = 1;
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty, "rate-based Wang-Henke");
            AssertNoNegativeFractions(column);
            Assert.That(column.RateBasedEfficiencies, Is.Not.Null);
            foreach (var l in column.RateBasedLog) TestContext.Out.WriteLine(l);
            foreach (var l in column.RateBasedStageNotes.Take(4)) TestContext.Out.WriteLine(l);
            var means = Enumerable.Range(1, n - 2).Select(i => column.RateBasedStageEfficiency(i)).ToList();
            TestContext.Out.WriteLine("stage efficiencies: " + string.Join(" ", means.Select(m => m.ToString("0.00"))) + "; O'Connell " + oconnell.ToString("0.00"));
            foreach (var m in means) Assert.That(m, Is.InRange(0.15, 3.0), "a tray efficiency from mass transfer");
            Assert.That(means.Average(), Is.InRange(0.4 * oconnell, 2.5 * oconnell), "the same order as O'Connell");
            Assert.That(column.RateBasedLog.Last(), Does.Not.Contain("ran out"), "the passes settled");
            Assert.That(column.ColumnPropertiesProfile, Does.Contain("Rate-Based Stage Efficiencies"));
            Assert.That(string.Concat(column.SaveData().Select(x => x.ToString())), Does.Contain("RateBased"), "the mode is saved");

            column.SolvingMethodName = "Naphtali-Sandholm (Simultaneous Correction)";
            var errors = flowsheet.SolveFlowsheet2();
            TestContext.Out.WriteLine("Naphtali-Sandholm: " + string.Join("; ", errors.Select(e => e.Message)) + " | " + string.Join(" | ", column.RateBasedLog));
            if (errors.Count == 0) AssertNoNegativeFractions(column);
            if (errors.Count == 0)
            {
                var meansNR = Enumerable.Range(1, n - 2).Select(i => column.RateBasedStageEfficiency(i)).ToList();
                Assert.That(meansNR.Average(), Is.EqualTo(means.Average()).Within(0.15), "both solvers land on the same efficiencies");
            }
            column.SolvingMethodName = "Wang-Henke (Bubble Point)";

            // a packed bed: efficiencies from the HETP of each component against the height of the stage
            inp.Sections.Clear();
            inp.Sections.Add(new InternalsSection { Name = "Bed", FromStage = 2, ToStage = n - 1, Type = InternalType.RandomPacking, PackingName = "Pall rings Metal 50 mm" });
            column.RateBased = false;
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty);
            var packed = ColumnInternalsStudy.Run(flowsheet, inp);
            ColumnInternalsStudy.ApplyToColumn(column, packed, false, false);
            ColumnInternalsStudy.StoreCaseInColumn(column, inp);
            column.RateBased = true;
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty, "rate-based on a packed bed");
            var meansPacked = Enumerable.Range(1, n - 2).Select(i => column.RateBasedStageEfficiency(i)).ToList();
            TestContext.Out.WriteLine("packed stage efficiencies: " + string.Join(" ", meansPacked.Select(m => m.ToString("0.00"))) + " | " + string.Join(" | ", column.RateBasedLog));
            foreach (var m in meansPacked) Assert.That(m, Is.InRange(0.02, 1.0));
            Assert.That(meansPacked.Average(), Is.InRange(0.3, 1.0), "a slice of bed one HETP tall is close to a theoretical stage");
        }

        static void AssertNoNegativeFractions(DWSIM.UnitOperations.UnitOperations.Column column)
        {
            double miny = double.MaxValue, minx = double.MaxValue;
            for (int i = 0; i < column.yf.Count; i++)
            {
                foreach (var v in (double[])column.yf[i]) miny = Math.Min(miny, v);
                foreach (var v in (double[])column.xf[i]) minx = Math.Min(minx, v);
            }
            TestContext.Out.WriteLine("smallest vapour fraction " + miny.ToString("0.###E+0") + ", smallest liquid fraction " + minx.ToString("0.###E+0"));
            Assert.That(miny, Is.GreaterThanOrEqualTo(0.0), "no negative vapour fraction with efficiencies above 1");
            Assert.That(minx, Is.GreaterThanOrEqualTo(0.0), "no negative liquid fraction with efficiencies above 1");
        }

        /// <summary>The utility attached to the column keeps the case in the simulation and rates on Update.</summary>
        [Test]
        public void TheAttachedUtilityKeepsTheCaseAndRatesTheColumn()
        {
            var flowsheet = Load("ExtractiveDistillation.dwxmz");
            Assert.That(flowsheet.SolveFlowsheet2(), Is.Empty);
            var name = ColumnInternalsStudy.ColumnNames(flowsheet)[0];
            var column = ColumnInternalsStudy.FindColumn(flowsheet, name);
            int n = ColumnInternalsStudy.StageCount(column);
            var u = flowsheet.GetUtility(DWSIM.Interfaces.Enums.FlowsheetUtility.ColumnInternals) as ColumnInternalsUtility;
            Assert.That(u, Is.Not.Null, "the engine factory finds the utility in the runner assembly");
            u.AttachedTo = column;
            u.Name = "Internals1";
            column.AttachedUtilities.Add(u);
            var inp = u.GetInput();
            Assert.That(inp.ColumnName, Is.EqualTo(name));
            inp.Sections.Add(new InternalsSection { Name = "Trays", FromStage = 2, ToStage = n - 1, Type = InternalType.SieveTray });
            u.SetInput(inp);
            u.Update();
            Assert.That(u.LastResult, Is.Not.Null);
            Assert.That(Convert.ToDouble(u.GetPropertyValue("Column Pressure Drop")), Is.GreaterThan(0));
            Assert.That(Convert.ToDouble(u.GetPropertyValue("Highest Fraction of Flood")), Is.EqualTo(0.8).Within(0.01));
            var data = u.SaveData();
            Assert.That(data.ContainsKey(ColumnInternalsUtility.CaseKey));
            var u2 = new ColumnInternalsUtility { AttachedTo = column };
            u2.LoadData(data);
            Assert.That(u2.GetInput().Sections.Count, Is.EqualTo(1));
            Assert.That(u2.GetInput().Sections[0].ToStage, Is.EqualTo(n - 1));
        }

        /// <summary>Prints the numbers the user guide validation tables quote (run with a detailed logger).</summary>
        [Test]
        public void PrintValidationTable()
        {
            var o = TestContext.Out;
            var s = TowlerPlate(); var sp = TowlerBottomPlate();
            var flv = TrayHydraulics.FlowParameter(sp.LiquidMassFlow, sp.VaporMassFlow, sp.LiquidDensity, sp.VaporDensity);
            var r = TrayHydraulics.RateSieveTray(s, sp, 0.79, 0.7, 3.0, 1.0);
            o.WriteLine("TOWLER FLV={0:F3} K1base={1:F4} K1top={2:F4} ufbase={3:F2} uftop={4:F2} lwDc={5:F3} how={6:F1} K2={7:F1} uhmin={8:F1} uh={9:F1} C0={10:F3} hd={11:F1} hr={12:F1} ht={13:F1} dP={14:F0} hb={15:F0} tr={16:F2} flood={17:F1} psi={18:F4} Dsized85={19:F3}",
                flv, TrayHydraulics.FairCapacityFactor(flv, 0.5), TrayHydraulics.FairCapacityFactor(0.03, 0.5), TrayHydraulics.FairFloodingVelocity(flv, 0.5, 954, 0.72, 0.057),
                TrayHydraulics.FairFloodingVelocity(0.03, 0.5, 753, 2.05, 0.023), TrayHydraulics.WeirLengthRatio(0.12), r.WeirCrest, TrayHydraulics.EduljeeK2(72.0), r.WeepPointVelocity,
                r.HoleVelocity, TrayHydraulics.OrificeCoefficient(1.0, 0.10), r.DryPressureDrop, TrayHydraulics.ResidualHead(954), r.TotalHead, r.PressureDrop, r.DowncomerBackup,
                r.DowncomerResidenceTime, r.FloodFraction * 100, r.Entrainment, TrayHydraulics.DiameterForFloodFraction(TowlerPlate(), TowlerBottomPlate(), 0.85));
            var ql = 6.45 / 402.6;
            o.WriteLine("KISTER hct_in={0:F3}", TrayHydraulics.ClearLiquidHeightAtTransition(0.5 * 0.0254, 0.10, 62.2 * 16.01846, 1.0, ql, 0.05) / 0.0254);
            var hiflow = PackingCatalogue.Find("Hiflow rings Metal 50 mm"); var montz = PackingCatalogue.Find("Montz Metal B1-200");
            o.WriteLine("SEADER612 hL_hiflow={0:F4} hL_montz={1:F4}", PackingHydraulics.BilletHoldup(0.01, 1000, 3e-3, hiflow.a, hiflow.Ch), PackingHydraulics.BilletHoldup(0.01, 1000, 3e-3, montz.a, montz.Ch));
            var p = PackingCatalogue.Find("Bialecki rings Metal 25 mm");
            double rhoV = 1.182, rhoL = 1000, muV = 1.78e-5, muL = 1.0e-3, lOverV = 1361.0 / 515.0;
            var uVl = PackingHydraulics.BilletLoadingVelocity(lOverV, rhoV, rhoL, muV, muL, p.a, p.Epsilon, p.Cs);
            var uL = uVl * rhoV * lOverV / rhoL;
            o.WriteLine("SEADER614 uVl={0:F3} uVf={1:F3} uL={2:F5} hL={3:F4} KW={4:F3} dP0={5:F0} dP={6:F0}", uVl, PackingHydraulics.BilletFloodingVelocity(uVl), uL,
                PackingHydraulics.BilletHoldup(uL, rhoL, muL, p.a, p.Ch), PackingHydraulics.BilletWallFactor(p.a, p.Epsilon, 0.325),
                PackingHydraulics.BilletDryPressureDrop(uVl, rhoV, muV, p.a, p.Epsilon, p.Cp, 0.325), PackingHydraulics.BilletPressureDrop(uVl, uL, rhoV, rhoL, muV, muL, p.a, p.Epsilon, p.Ch, p.Cp, 0.325));
            var pk = new PackingData { Name = "Pall-like", Material = "Metal", Size = "1.5 in", a = 149.6, Epsilon = 0.952, Ch = 0.7, CL = 1.227, CV = 0.341, NominalSize = 0.038 };
            double rhoL2 = 61.5 * 16.01846, rhoV2 = 0.121 * 16.01846, muL2 = 0.64e-6 * rhoL2, muV2 = 0.75e-5 * rhoV2;
            var aph = PackingHydraulics.BilletInterfaceAreaRatio(0.0017, rhoL2, muL2, 0.101, pk.a, pk.Epsilon);
            var hl = PackingHydraulics.BilletHL(0.0017, 1.82e-9, pk.a, pk.Epsilon, pk.CL, 0.0128, aph);
            var hg = PackingHydraulics.BilletHG(2.49, rhoV2, muV2, 7.75e-6, pk.a, pk.Epsilon, pk.CV, 0.0128, aph);
            var hog = hg + 0.69 * hl;
            o.WriteLine("SEADER615 hL_eq697={0:F4} aPh={1:F3} HL_m={2:F3} HG_m={3:F3} HOG_ft={4:F2} HETP_ft={5:F2}", PackingHydraulics.BilletHoldup(0.0017, rhoL2, muL2, pk.a, pk.Ch), aph, hl, hg, hog / 0.3048, PackingHydraulics.HetpFromHOG(hog, 0.69) / 0.3048);
            var imtp = PackingCatalogue.Find("Metal Intalox (IMTP) Metal 25 mm");
            double rhoV3 = 0.0738 * 16.01846, uV3 = 5.95 * 0.3048, G3 = uV3 * rhoV3, L3 = G3 * 0.092 * Math.Sqrt(1000 / rhoV3);
            o.WriteLine("SEADER613 robbins_inft={0:F3} kistergill_inft={1:F3} uVflood_robbins={2:F2}", PackingHydraulics.RobbinsPressureDrop(G3, L3, rhoV3, 1000, 1e-3, imtp.Fpd) / 817.2,
                PackingHydraulics.KisterGillFloodPressureDrop(imtp.Fp) / 817.2, PackingHydraulics.RobbinsFloodingVelocity(L3, rhoV3, 1000, 1e-3, imtp.Fpd, imtp.Fp));
        }
    }
}
