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
