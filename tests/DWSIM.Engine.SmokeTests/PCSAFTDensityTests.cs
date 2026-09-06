//    PC-SAFT density-root robustness.
//
//    The reduced density (packing fraction) is found by bracketing the roots of P - Pcalc(eta) over
//    the physical range (0, ~0.74) and choosing the liquid (highest-eta) or gas (lowest-eta) root.
//    The previous simplex-on-squared-objective could slide onto a spurious low-density root or wander
//    past close packing into the NaN region - which made a high segment-number polymer's log fugacity
//    coefficient NaN for polymer-rich compositions. These guard both the small-molecule behaviour and
//    the polymer finiteness.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class PCSAFTDensityTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage Package(
            Action<DWSIM.DynamicRunner.Flowsheet> addCompounds)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            addCompounds(fs);
            var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "feed");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.SetPropertyPackage(pp);
            pp.CurrentMaterialStream = ms;
            return pp;
        }

        /// <summary>
        /// The Schulz-Zimm pseudo-component generator must reproduce the distribution moments: the cut
        /// number-average equals Mn and the weight-average equals Mn*PDI exactly (Gauss-Laguerre quadrature
        /// is exact through the third moment for two or more cuts), with the fractions summing to one.
        /// </summary>
        [Test]
        public void SchulzZimmCutsMatchDistributionMoments()
        {
            foreach (var (Mn, PDI, N) in new[] { (50000.0, 2.0, 4), (120000.0, 1.6, 3), (8000.0, 2.5, 5), (30000.0, 2.0, 1) })
            {
                double[] M = null, z = null;
                DWSIM.Thermodynamics.Polymers.PolymerCharacterization.SchulzZimmCuts(Mn, PDI, N, ref M, ref z);
                double sz = z.Sum(), m1 = 0, m2 = 0;
                for (int i = 0; i < M.Length; i++) { m1 += z[i] * M[i]; m2 += z[i] * M[i] * M[i]; }
                double MnCut = m1 / sz, MwCut = m2 / m1;
                TestContext.WriteLine($"Mn={Mn} PDI={PDI} N={N}: sum z={sz:F6} Mn_cut={MnCut:F1} Mw_cut={MwCut:F1} (Mw={Mn * PDI:F1})");
                Assert.That(sz, Is.EqualTo(1.0).Within(1e-9), "fractions must sum to one");
                Assert.That(MnCut, Is.EqualTo(Mn).Within(Mn * 1e-6), "number-average must equal Mn");
                Assert.That(M.All(x => x > 0), "all cut molar masses must be positive");
                if (N >= 2)
                    Assert.That(MwCut, Is.EqualTo(Mn * PDI).Within(Mn * PDI * 1e-6), "weight-average must equal Mn*PDI");
            }
        }

        /// <summary>
        /// The log-normal pseudo-component generator must reproduce the number- and weight-average molar
        /// mass. Unlike Schulz-Zimm, the mass is exponential in the Gauss-Hermite node, so the spread is
        /// solved to make the discrete polydispersity equal PDI exactly; with enough cuts the cut Mn equals
        /// Mn and Mw equals Mn*PDI. A finite cut count caps the reachable PDI, so the cases give the
        /// generator room (two cuts reach only PDI = 2).
        /// </summary>
        [Test]
        public void LogNormalCutsMatchDistributionMoments()
        {
            foreach (var (Mn, PDI, N) in new[] { (50000.0, 2.0, 6), (120000.0, 1.6, 4), (8000.0, 1.8, 5), (30000.0, 2.0, 1) })
            {
                double[] M = null, z = null;
                DWSIM.Thermodynamics.Polymers.PolymerCharacterization.LogNormalCuts(Mn, PDI, N, ref M, ref z);
                double sz = z.Sum(), m1 = 0, m2 = 0;
                for (int i = 0; i < M.Length; i++) { m1 += z[i] * M[i]; m2 += z[i] * M[i] * M[i]; }
                double MnCut = m1 / sz, MwCut = m2 / m1;
                TestContext.WriteLine($"Mn={Mn} PDI={PDI} N={N}: sum z={sz:F6} Mn_cut={MnCut:F1} Mw_cut={MwCut:F1} (Mw={Mn * PDI:F1})");
                Assert.That(sz, Is.EqualTo(1.0).Within(1e-9), "fractions must sum to one");
                Assert.That(MnCut, Is.EqualTo(Mn).Within(Mn * 1e-6), "number-average must equal Mn");
                Assert.That(M.All(x => x > 0), "all cut molar masses must be positive");
                if (N >= 2)
                    Assert.That(MwCut, Is.EqualTo(Mn * PDI).Within(Mn * PDI * 1e-4), "weight-average must equal Mn*PDI");
            }
        }

        /// <summary>
        /// A polydisperse polymer (two polypropylene cuts of different molar mass in n-pentane) must reach
        /// its liquid-liquid split from the ordinary Simple LLE flash with no manual seed, and the flash
        /// must fractionate: the heavier cut concentrates in the polymer-rich phase and is depleted from the
        /// solvent-rich phase. Exercises the multi-component spinodal seed.
        /// </summary>
        [Test]
        public void PolydispersePolymerSplitIsReachableAndFractionates()
        {
            // n-pentane + two polypropylene cuts (same CAS -> same PC-SAFT params, different molar mass)
            double M1 = 30000.0, M2 = 70000.0, Msolv = 72.15;
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("N-pentane");
            foreach (var cut in new[] { ("PP-30k", M1), ("PP-70k", M2) })
                fs.Options.SelectedComponents.Add(cut.Item1, new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
                {
                    Name = cut.Item1, CAS_Number = "9003-07-0", Formula = "(C3H6)n", Molar_Weight = cut.Item2,
                    Critical_Temperature = 1200.0, Critical_Pressure = 5.0e5, Acentric_Factor = 0.5,
                    Normal_Boiling_Point = 800.0, IsHYPO = 1
                });
            var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
            var o = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "feed");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[o.Name];
            ms.SetFlowsheet(fs); ms.SetPropertyPackage(pp); pp.CurrentMaterialStream = ms;

            double gC5 = 80, g1 = 10, g2 = 10;
            double nC5 = gC5 / Msolv, n1 = g1 / M1, n2 = g2 / M2, nt = nC5 + n1 + n2;
            var z = new[] { nC5 / nt, n1 / nt, n2 / nt };
            // unseeded: the multi-component spinodal auto-seed must find the split on its own
            var flash = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.SimpleLLE();
            var res = (object[])flash.Flash_PT(z, 40e5, 460.15, pp);
            double La = Convert.ToDouble(res[0]), Lb = Convert.ToDouble(res[5]);
            var xa = (double[])res[2]; var xb = (double[])res[6];
            double MassPP(double[] x) { double mp = x[1] * M1 + x[2] * M2; return mp / (mp + x[0] * Msolv); }
            double wa = MassPP(xa), wb = MassPP(xb);
            // orient so 'lean' is the solvent-rich phase and 'rich' the polymer-rich phase
            double wLean = Math.Min(wa, wb), wRich = Math.Max(wa, wb);
            var xLean = wa <= wb ? xa : xb;
            double feedRatio = z[2] / z[1];
            double leanRatio = xLean[2] / Math.Max(xLean[1], 1e-30);
            TestContext.WriteLine($"La={La:F3} Lb={Lb:F3}  wPP: lean={wLean:F4} rich={wRich:F4}");
            TestContext.WriteLine($"70k/30k ratio  feed={feedRatio:F4}  solventPhase={leanRatio:F4}");

            Assert.That(Math.Min(La, Lb), Is.GreaterThan(0.01), "two liquid phases must be present");
            Assert.That(wRich, Is.GreaterThan(0.15), "one phase must be polymer-rich");
            Assert.That(wLean, Is.LessThan(0.02), "the other phase must be nearly pure solvent");
            Assert.That(leanRatio, Is.LessThan(feedRatio),
                "the solvent-rich phase must be depleted in the heavier cut (fractionation)");
        }

        /// <summary>
        /// Pins the 2B association math (SolveXa + mu_Ass + obj_muAss) for a hydrogen-bonding
        /// mixture, so the site-multiplicity refactor stays behaviour-preserving for the
        /// non-polymer associating compounds. Water/ethanol liquid log fugacity coefficients at
        /// 298.15 K, 1 atm. Reference values captured from the pre-refactor code.
        /// </summary>
        [Test]
        public void WaterEthanolAssociationLogFugacityIsStable()
        {
            var pp = Package(fs => { fs.AddCompound("Water"); fs.AddCompound("Ethanol"); });
            var lnphi = pp.DW_CalcLnFugCoeff(new[] { 0.5, 0.5 }, 298.15, 101325.0,
                DWSIM.Thermodynamics.PropertyPackages.State.Liquid);
            TestContext.WriteLine($"lnphi water={lnphi[0]:R}  ethanol={lnphi[1]:R}");
            // Values include water-ethanol cross-association AND the shipped water/ethanol kij = 0.06.
            // Before the max() off-by-one fix the unlike-pair association strength was read off the
            // (always zero) matrix diagonal, so cross-association was silently absent and these were
            // -2.01189 / -1.72609; with cross-association alive and kij = 0 they are -3.07407 / -2.73131.
            Assert.That(lnphi[0], Is.EqualTo(-2.7369900645199534).Within(1e-9), "water lnphi (2B cross-association + kij)");
            Assert.That(lnphi[1], Is.EqualTo(-2.626709161126869).Within(1e-9), "ethanol lnphi (2B cross-association + kij)");
        }

        /// <summary>
        /// The shipped water-alcohol kij rows (pcsaft_ip.dat) must load and restore the positive deviation
        /// that live cross-association otherwise over-suppresses. Without a kij the arithmetic-mean cross
        /// association drags the alcohol's activity below one (wrong sign); the fitted kij brings the
        /// alcohol infinite-dilution activity coefficient back near the DECHEMA/Gmehling value. Proxy for
        /// gamma^inf is the activity coefficient at x_alcohol = 0.01, 323.15 K, 1 atm liquid.
        /// </summary>
        [Test]
        public void WaterAlcoholKijRestoresPositiveDeviation()
        {
            var st = DWSIM.Thermodynamics.PropertyPackages.State.Liquid;
            double T = 323.15;
            // alcohol DB name, experimental gamma_alcohol^inf in water, accepted band
            var cases = new (string a, double gExp, double lo, double hi)[]
            {
                ("Methanol", 1.8, 1.4, 2.3),
                ("Ethanol", 5.0, 4.0, 6.5),
                ("1-propanol", 14.0, 11.0, 18.0),
            };
            foreach (var (a, gExp, lo, hi) in cases)
            {
                // Package built the normal way, so the kij comes from the shipped pcsaft_ip.dat, not injected.
                var pp = Package(fs => { fs.AddCompound("Water"); fs.AddCompound(a); });
                double lnApure = pp.DW_CalcLnFugCoeff(new[] { 0.0, 1.0 }, T, 101325.0, st)[1];
                double gA = Math.Exp(pp.DW_CalcLnFugCoeff(new[] { 0.99, 0.01 }, T, 101325.0, st)[1] - lnApure);
                TestContext.WriteLine($"Water/{a}: gamma^inf={gA:F2} (exp ~{gExp}, band {lo}-{hi})");
                Assert.That(gA, Is.GreaterThan(1.0), $"{a}: kij must restore a positive deviation (gamma^inf > 1)");
                Assert.That(gA, Is.InRange(lo, hi), $"{a}: gamma^inf must be near the experimental value");
            }
        }

        /// <summary>
        /// The caloric guard: Lee-Kesler caloric properties rely on Tc/Pc/omega corresponding states, which
        /// cannot represent hydrogen-bonding enthalpy and use placeholder criticals for a polymer. So a
        /// mixture with an associating compound or a polymer must fall back to the PC-SAFT departure
        /// regardless of the Use Lee-Kesler flags, while a plain non-associating mixture keeps using
        /// Lee-Kesler when it is enabled.
        /// </summary>
        [Test]
        public void AssociatingAndPolymerCaloricBypassLeeKesler()
        {
            var st = DWSIM.Thermodynamics.PropertyPackages.State.Liquid;

            // Polymer: guard fires (LK relies on placeholder criticals).
            var pp = PolypropyleneInNPentane(out var z);
            pp.UseLeeKeslerEnthalpy = true;
            double hPolyLk = pp.DW_CalcEnthalpy(z, 460.15, 40e5, st);
            pp.UseLeeKeslerEnthalpy = false;
            double hPolyNat = pp.DW_CalcEnthalpy(z, 460.15, 40e5, st);
            TestContext.WriteLine($"polymer H: LK-flag-on={hPolyLk:G6}  native={hPolyNat:G6}");
            Assert.That(hPolyLk, Is.EqualTo(hPolyNat).Within(1e-9),
                "a polymer mixture must use the PC-SAFT departure even with Lee-Kesler enabled");

            // Associating: guard fires (LK cannot represent hydrogen-bonding enthalpy).
            var ppa = Package(fs => { fs.AddCompound("Water"); fs.AddCompound("Ethanol"); });
            var za = new[] { 0.5, 0.5 };
            ppa.UseLeeKeslerEnthalpy = true;
            double hAssocLk = ppa.DW_CalcEnthalpy(za, 350.0, 2e5, st);
            ppa.UseLeeKeslerEnthalpy = false;
            double hAssocNat = ppa.DW_CalcEnthalpy(za, 350.0, 2e5, st);
            TestContext.WriteLine($"water/ethanol H: LK-flag-on={hAssocLk:G6}  native={hAssocNat:G6}");
            Assert.That(hAssocLk, Is.EqualTo(hAssocNat).Within(1e-9),
                "an associating mixture must use the PC-SAFT departure even with Lee-Kesler enabled");

            // Non-associating, non-polymer: guard inactive, Lee-Kesler still used.
            var pp2 = Package(fs => { fs.AddCompound("Ethane"); fs.AddCompound("N-pentane"); });
            var z2 = new[] { 0.5, 0.5 };
            pp2.UseLeeKeslerEnthalpy = true;
            double hLk = pp2.DW_CalcEnthalpy(z2, 300.0, 10e5, st);
            pp2.UseLeeKeslerEnthalpy = false;
            double hNat2 = pp2.DW_CalcEnthalpy(z2, 300.0, 10e5, st);
            TestContext.WriteLine($"C2/nC5 H: LK={hLk:G6}  native={hNat2:G6}");
            Assert.That(Math.Abs(hLk - hNat2), Is.GreaterThan(1e-6),
                "a plain non-associating mixture keeps using Lee-Kesler (differs from the PC-SAFT departure)");
        }

        private static DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage PmmaChlorobutane(double Mn, double Msolv)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.Options.SelectedComponents.Add("1-chlorobutane", new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
            {
                Name = "1-chlorobutane", CAS_Number = "109-69-3", Formula = "C4H9Cl", Molar_Weight = Msolv,
                Critical_Temperature = 542.0, Critical_Pressure = 3.684e6, Acentric_Factor = 0.2216,
                Normal_Boiling_Point = 351.58
            });
            fs.Options.SelectedComponents.Add("PMMA", new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
            {
                Name = "PMMA", CAS_Number = "9011-14-7", Formula = "(C5H8O2)n", Molar_Weight = Mn,
                Critical_Temperature = 1500.0, Critical_Pressure = 5.0e5, Acentric_Factor = 0.5,
                Normal_Boiling_Point = 900.0, IsHYPO = 1
            });
            var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
            var o = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "feed");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[o.Name];
            ms.SetFlowsheet(fs); ms.SetPropertyPackage(pp); pp.CurrentMaterialStream = ms;
            return pp;
        }

        // Liquid-liquid binodal at (T,P) by the convex-hull-of-Gibbs-energy tie-line construction on a
        // geometric polymer-mole-fraction grid: a gap in the lower hull is the miscibility gap. Returns the
        // two phases' polymer mass fractions, or (-1,-1) for a single phase. The polymer-rich branch is
        // required above 5 wt% to reject the numerical micro-gaps that appear as the dome closes.
        private static (double wL, double wR) LleBinodal(
            DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage pp, double T, double P, double Mn, double Msolv)
        {
            int n = 140; double xmin = 1e-7, xmax = 3e-3;
            var x = new double[n]; var g = new double[n];
            for (int i = 0; i < n; i++)
            {
                double xp = xmin * Math.Pow(xmax / xmin, (double)i / (n - 1));
                var ln = pp.DW_CalcLnFugCoeff(new[] { 1.0 - xp, xp }, T, P,
                    DWSIM.Thermodynamics.PropertyPackages.State.Liquid);
                x[i] = xp; g[i] = (1.0 - xp) * (Math.Log(1.0 - xp) + ln[0]) + xp * (Math.Log(xp) + ln[1]);
            }
            var hull = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n; i++)
            {
                while (hull.Count >= 2)
                {
                    int a = hull[hull.Count - 2], b = hull[hull.Count - 1];
                    double cross = (x[b] - x[a]) * (g[i] - g[a]) - (g[b] - g[a]) * (x[i] - x[a]);
                    if (cross <= 0) hull.RemoveAt(hull.Count - 1); else break;
                }
                hull.Add(i);
            }
            double best = 0; int ia = -1, ib = -1;
            for (int h = 0; h < hull.Count - 1; h++)
            {
                int a = hull[h], b = hull[h + 1];
                if (b - a >= 2) { double s = Math.Log(x[b]) - Math.Log(x[a]); if (s > best) { best = s; ia = a; ib = b; } }
            }
            if (ia < 0) return (-1, -1);
            double wR = x[ib] * Mn / (x[ib] * Mn + (1 - x[ib]) * Msolv);
            if (wR < 0.05) return (-1, -1);
            double wL = x[ia] * Mn / (x[ia] * Mn + (1 - x[ia]) * Msolv);
            return (wL, wR);
        }

        /// <summary>
        /// PMMA (Mw 36500) + 1-chlorobutane liquid-liquid equilibrium against Kontogeorgis and Folas,
        /// Application of SAFT to Polymers, Figure 14.4 (left): a UCST dome with the critical point near
        /// 281 K at a polymer weight fraction around 0.10, using the shipped PC-SAFT parameters and the
        /// kij = -0.0032 from pcsaft_ip.dat. The model reproduces the UCST within a few kelvin and the
        /// dome width, confirming the spinodal-seeded EoS LLE path on a non-associating polymer solution.
        /// </summary>
        [Test]
        public void PmmaChlorobutaneCloudPointAgainstFig144()
        {
            double Mn = 36500.0, Msolv = 92.568, P = 1e5;
            var pp = PmmaChlorobutane(Mn, Msolv);

            var (wl260, wr260) = LleBinodal(pp, 260.0, P, Mn, Msolv);
            var (wl278, wr278) = LleBinodal(pp, 278.0, P, Mn, Msolv);
            var (wl290, wr290) = LleBinodal(pp, 290.0, P, Mn, Msolv);
            double ucst = 0.0;
            for (double T = 270.0; T <= 288.0; T += 1.0)
            {
                var (_, wr) = LleBinodal(pp, T, P, Mn, Msolv);
                if (wr >= 0) ucst = T;
            }
            TestContext.WriteLine($"UCST~{ucst:F0}K  260K:[{wl260:F3},{wr260:F3}]  278K:[{wl278:F3},{wr278:F3}]  290K wR={wr290:F3}");

            Assert.That(wr260, Is.GreaterThan(0.28).And.LessThan(0.36), "polymer-rich branch at 260 K");
            Assert.That(wr278, Is.GreaterThan(0.12).And.LessThan(0.22), "polymer-rich branch at 278 K");
            Assert.That(wr290, Is.LessThan(0.0), "single phase above the UCST");
            Assert.That(ucst, Is.GreaterThanOrEqualTo(279.0).And.LessThanOrEqualTo(285.0), "UCST near experimental 281 K");
        }

        /// <summary>
        /// A small-molecule PC-SAFT flash stays physical: ethane/n-pentane at 350 K condenses
        /// monotonically as pressure rises and the vapour keeps getting richer in the light component.
        /// </summary>
        [Test]
        public void EthaneNPentaneVLEIsPhysical()
        {
            var pp = Package(fs => { fs.AddCompound("Ethane"); fs.AddCompound("N-pentane"); });
            var flash = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.NestedLoops();

            double prevL = -1.0, prevY = -1.0;
            foreach (var Pbar in new[] { 10.0, 20.0, 30.0 })
            {
                var r = (object[])flash.Flash_PT(new[] { 0.5, 0.5 }, Pbar * 1e5, 350.0, pp);
                double L = Convert.ToDouble(r[0]), V = Convert.ToDouble(r[1]);
                var y = (double[])r[3];
                TestContext.WriteLine($"{Pbar:F0} bar: L={L:F3} V={V:F3} y(C2)={y[0]:F3}");

                Assert.That(L + V, Is.EqualTo(1.0).Within(1e-6), "phase fractions must sum to one");
                Assert.That(L, Is.GreaterThan(prevL), "liquid fraction must grow with pressure");
                Assert.That(y[0], Is.GreaterThan(0.5), "vapour must be enriched in the lighter ethane");
                Assert.That(y[0], Is.GreaterThan(prevY), "vapour must get richer in ethane with pressure");
                prevL = L; prevY = y[0];
            }
        }

        /// <summary>
        /// A polymer's log fugacity coefficient must stay finite across the whole composition range,
        /// pure solvent to pure polymer. Its magnitude is on the order of 1e3 (proportional to the
        /// segment number), so the coefficient itself underflows to zero - the density root-finder must
        /// still return a real value, especially for polymer-rich compositions.
        /// </summary>
        [Test]
        public void PolymerLogFugacityIsFiniteAcrossComposition()
        {
            var pp = Package(fs =>
            {
                fs.AddCompound("N-pentane");
                var poly = new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
                {
                    Name = "Polypropylene",
                    CAS_Number = "9003-07-0",
                    Formula = "(C3H6)n",
                    Molar_Weight = 50400.0,
                    Critical_Temperature = 1200.0,
                    Critical_Pressure = 5.0e5,
                    Acentric_Factor = 0.5,
                    Normal_Boiling_Point = 800.0,
                    IsHYPO = 1
                };
                fs.Options.SelectedComponents.Add(poly.Name, poly);
            });

            double T = 450.15, P = 35e5;
            foreach (var xpp in new[] { 0.0, 1e-4, 1e-2, 0.1, 0.5, 0.9, 1.0 })
            {
                var w = new[] { 1.0 - xpp, xpp };
                var ln = pp.DW_CalcLnFugCoeff(w, T, P, DWSIM.Thermodynamics.PropertyPackages.State.Liquid);
                TestContext.WriteLine($"x(PP)={xpp:E2}  lnPhi = [{ln[0]:F3}, {ln[1]:F2}]");
                Assert.That(double.IsNaN(ln[0]) || double.IsNaN(ln[1]), Is.False, $"NaN log fugacity at x(PP)={xpp:E2}");
                Assert.That(ln[1], Is.LessThan(0.0).And.GreaterThan(-5000.0), $"polymer lnPhi out of range at x(PP)={xpp:E2}");
            }
        }

        /// <summary>
        /// The polymer liquid-liquid split must come out of the ordinary Simple LLE flash with no manual
        /// phase seed. Inside the miscibility gap (polypropylene in n-pentane at 460 K / 40 bar) the flash
        /// has to demix into a nearly pure solvent phase and a polymer-rich one, driven only by the EoS
        /// spinodal seed the flash builds for itself; without it the iteration collapses onto the feed and
        /// reports a single phase. The window sits at extreme dilution (polymer mole fractions ~1e-5..1e-3)
        /// and the feed is metastable, outside the spinodal, which the activity-model seeding cannot reach.
        /// </summary>
        [Test]
        public void PolymerLiquidLiquidSplitIsReachableUnseeded()
        {
            var pp = PolypropyleneInNPentane(out var z);
            var flash = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.SimpleLLE();
            var r = (object[])flash.Flash_PT(z, 40e5, 460.15, pp);

            double L1 = Convert.ToDouble(r[0]), L2 = Convert.ToDouble(r[5]);
            double w1 = MassFractionPP(((double[])r[2])[1]);
            double w2 = MassFractionPP(((double[])r[6])[1]);
            TestContext.WriteLine($"L1={L1:F3} L2={L2:F3}  w1(PP)={w1:F4} w2(PP)={w2:F4}");

            Assert.That(Math.Min(L1, L2), Is.GreaterThan(0.01), "two liquid phases must be present");
            Assert.That(Math.Max(w1, w2), Is.GreaterThan(0.15), "one phase must be polymer-rich");
            Assert.That(Math.Min(w1, w2), Is.LessThan(0.01), "the other phase must be nearly pure solvent");
        }

        /// <summary>
        /// The same split must come out of the Nested Loops (VLLE) flash that a MaterialStream uses, not only
        /// the dedicated Simple LLE flash. For a Gibbs-minimization package the VLLE flash splits the liquid
        /// first (seeded from the EoS spinodal) instead of running a vapour flash the non-volatile polymer
        /// cannot satisfy, which previously threw "Error calculating amount of the vapor phase".
        /// </summary>
        [Test]
        public void PolymerLiquidLiquidSplitIsReachableViaVLLEFlash()
        {
            var pp = PolypropyleneInNPentane(out var z);
            var flash = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.NestedLoops3PV3();
            var r = (object[])flash.Flash_PT(z, 40e5, 460.15, pp);

            double L1 = Convert.ToDouble(r[0]), V = Convert.ToDouble(r[1]), L2 = Convert.ToDouble(r[5]);
            double w1 = MassFractionPP(((double[])r[2])[1]);
            double w2 = MassFractionPP(((double[])r[6])[1]);
            TestContext.WriteLine($"L1={L1:F3} V={V:F3} L2={L2:F3}  w1(PP)={w1:F4} w2(PP)={w2:F4}");

            Assert.That(V, Is.EqualTo(0.0).Within(1e-6), "no vapour forms at this pressure");
            Assert.That(Math.Min(L1, L2), Is.GreaterThan(0.01), "two liquid phases must be present");
            Assert.That(Math.Max(w1, w2), Is.GreaterThan(0.15), "one phase must be polymer-rich");
            Assert.That(Math.Min(w1, w2), Is.LessThan(0.01), "the other phase must be nearly pure solvent");
        }

        /// <summary>
        /// A phase that contains a polymer must report a physical density from the equation of state (not the
        /// low-molecular-weight Rackett correlation), a viscosity that reflects the user's polymer data, and
        /// finite, physical transport properties throughout. The polymer's mole fraction is a trace, so only
        /// the mass-weighted blends let its large, user-supplied viscosity raise the solution viscosity; a mole
        /// average would leave it at essentially the solvent's.
        /// </summary>
        [Test]
        public void PolymerPhaseUsesEoSDensityAndUserTransportProperties()
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("N-pentane");
            var poly = new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
            {
                Name = "Polypropylene",
                CAS_Number = "9003-07-0",
                Formula = "(C3H6)n",
                Molar_Weight = 50400.0,
                Critical_Temperature = 1200.0,
                Critical_Pressure = 5.0e5,
                Acentric_Factor = 0.5,
                Normal_Boiling_Point = 800.0,
                IsHYPO = 1,
                OriginalDB = "",
                // user-supplied liquid viscosity: eta = exp(A + B/T + ...) = exp(ln 1000) ~ 1000 Pa.s
                Liquid_Viscosity_Const_A = Math.Log(1000.0)
            };
            fs.Options.SelectedComponents.Add(poly.Name, poly);

            var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.PropertyPackage = pp;
            ms.AssignSelfToPP();

            const double mwP = 50400.0, mwC5 = 72.15, wFeed = 0.20;
            double nPP = wFeed / mwP, nC5 = (1.0 - wFeed) / mwC5, tot = nPP + nC5;
            ms.SetMassFlow(1.0);
            ms.SetPressure(60e5);      // 60 bar / 460 K keeps a single, miscible liquid
            ms.SetTemperature(460.15);
            ms.SetOverallComposition(new[] { nC5 / tot, nPP / tot });
            ms.SetFlashSpec("PT");
            ms.Calculate();

            var liq = ms.Phases[3]; // Liquid1
            double rho = liq.Properties.density.GetValueOrDefault();
            double mu = liq.Properties.viscosity.GetValueOrDefault();

            pp.CurrentMaterialStream = ms;
            double muSolvent = pp.AUX_LIQVISCi("N-pentane", 460.15, 60e5);
            double muPolymer = pp.AUX_LIQVISCi("Polypropylene", 460.15, 60e5);
            TestContext.WriteLine($"rho={rho:F1} kg/m3  mu={mu:E3}  muSolvent={muSolvent:E3}  muPolymer={muPolymer:E3}");

            // Thermal conductivity and surface tension take the same mass-weighted route. With no user data for
            // the polymer, its value is the tabulated polymer estimate (not the low-molecular-weight garbage),
            // so the phase value is the mass-weighted blend of the solvent value and that estimate.
            double k = pp.AUX_CONDTL(460.15, 3);
            double sigma = pp.AUX_SURFTM(460.15);
            double wPoly = liq.Compounds["Polypropylene"].MassFraction.GetValueOrDefault();
            double wPent = liq.Compounds["N-pentane"].MassFraction.GetValueOrDefault();
            double kPent = pp.AUX_LIQTHERMCONDi(liq.Compounds["N-pentane"].ConstantProperties, 460.15);
            double sPent = pp.AUX_SURFTi(liq.Compounds["N-pentane"].ConstantProperties, 460.15);
            const double kPolyEst = 0.16559;   // PP: Van Krevelen shape on lambda(298)=0.19 W/mK, Tg=260 K, at 460 K
            const double sPolyEst = 0.020414;  // PP: Wu sigma(20C)=30.1 mN/m, dsigma/dT=-0.058 mN/m.K, at 460 K
            double kExp = wPoly * kPolyEst + wPent * kPent;
            double sExp = wPoly * sPolyEst + wPent * sPent;
            TestContext.WriteLine($"k={k:E3} (exp {kExp:E3})  sigma={sigma:E3} (exp {sExp:E3})");

            Assert.That(rho, Is.GreaterThan(100.0).And.LessThan(1500.0), "EoS liquid density must be physical");
            Assert.That(muPolymer, Is.GreaterThan(muSolvent * 100.0), "test setup: the polymer is far more viscous");
            Assert.That(mu, Is.GreaterThan(muSolvent * 3.0), "the polymer must raise the solution viscosity");
            Assert.That(mu, Is.LessThan(muPolymer), "the blend cannot exceed the pure polymer viscosity");
            Assert.That(k, Is.EqualTo(kExp).Within(3.0).Percent, "conductivity blends the solvent value with the PP estimate");
            Assert.That(sigma, Is.EqualTo(sExp).Within(3.0).Percent, "surface tension blends the solvent value with the PP estimate");
        }

        /// <summary>
        /// Devolatilization flash: a polystyrene solution in ethylbenzene stripped under vacuum. The polymer
        /// is non-volatile, so a vapour-liquid flash must keep it entirely in the liquid and let the solvent
        /// flash off. Two things break a naive flash here: a Newton step on the vapour fraction overshoots to
        /// V = 1 and reports the whole feed (polymer included) as vapour; and the solvent K-value swings over
        /// orders of magnitude between a solvent-rich and a polymer-rich liquid, so the true two-phase root is
        /// an unstable fixed point that plain successive substitution oscillates around. The fix caps the
        /// vapour fraction at 1 - sum(z_nonvol) and solves the vapour fraction by a damped bracketed
        /// Rachford-Rice step. This pins the physical result: nearly all the solvent vaporizes, the vapour
        /// carries no polymer, the liquid is a concentrated melt, and the whole polymer feed is conserved.
        /// </summary>
        [Test]
        public void PolymerDevolatilizationFlashKeepsPolymerInLiquid()
        {
            string addcomps = Path.GetFullPath(Path.Combine(SourceDir(), "..", "..", "content", "addcomps"));
            var poly = Newtonsoft.Json.JsonConvert.DeserializeObject<DWSIM.Thermodynamics.BaseClasses.ConstantProperties>(
                File.ReadAllText(Path.Combine(addcomps, "Polystyrene.json")));
            poly.CurrentDB = "User"; poly.OriginalDB = "User"; poly.Molar_Weight = 50000.0;

            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Ethylbenzene");
            fs.Options.SelectedComponents.Add(poly.Name, poly);
            var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs); ms.PropertyPackage = pp; ms.AssignSelfToPP(); pp.CurrentMaterialStream = ms;
            double nEB = 0.75 / 106.165, nPS = 0.25 / 50000.0, tot = nEB + nPS;
            ms.SetMassFlow(1.0);
            ms.SetOverallComposition(new[] { nEB / tot, nPS / tot });

            // 470 K under vacuum (0.15 bar), a representative devolatilizer operating point.
            ms.SetTemperature(470.0); ms.SetPressure(15000.0); ms.SetFlashSpec("PT");
            ms.Calculate();

            double vf = ms.Phases[2].Properties.molarfraction.GetValueOrDefault();
            double psVmass = ms.Phases[2].Compounds["Polystyrene"].MassFraction.GetValueOrDefault();
            double psLmass = ms.Phases[3].Compounds["Polystyrene"].MassFraction.GetValueOrDefault();

            // Polymer mole balance: everything fed must end up in the liquid, not lost to a clamp artifact.
            double zPS = ms.Phases[0].Compounds["Polystyrene"].MoleFraction.GetValueOrDefault();
            double L = 1.0 - vf;
            double xPS = ms.Phases[3].Compounds["Polystyrene"].MoleFraction.GetValueOrDefault();
            double polymerInLiquidFraction = L * xPS / zPS;

            Assert.Multiple(() =>
            {
                Assert.That(vf, Is.GreaterThan(0.9).And.LessThan(1.0),
                    "almost all solvent moles vaporize, but the flash must not collapse to all-vapour");
                Assert.That(psVmass, Is.LessThan(1.0e-6),
                    "the non-volatile polymer must not appear in the vapour");
                Assert.That(psLmass, Is.GreaterThan(0.9),
                    "the liquid left behind is a concentrated polymer melt");
                Assert.That(polymerInLiquidFraction, Is.EqualTo(1.0).Within(0.01),
                    "the whole polymer feed is conserved in the liquid (no mass lost to a vapour-fraction clamp)");
            });
        }

        /// <summary>
        /// Every polymer shipped as an addcomps JSON must deserialize the way DWSIM's user-compound loader
        /// does, match its pcsaft.dat CAS number, and run through a PC-SAFT flash in a solvent to give
        /// physical phase properties. This is the path a user takes: pick the polymer from the list, set Mn,
        /// solve. A dilute solution at high pressure keeps a single liquid for every polymer.
        /// </summary>
        [Test]
        public void PolymerAddcompsLoadAndSolveWithPcsaft()
        {
            string addcomps = Path.GetFullPath(Path.Combine(SourceDir(), "..", "..", "content", "addcomps"));

            var polymers = new[]
            {
                ("Polyethylene_HDPE.json", "9002-88-4"),
                ("Polyethylene_LDPE.json", "9002-88-4-L"),
                ("Polypropylene.json", "9003-07-0"),
                ("Polybutene.json", "9003-28-5"),
                ("Polyisobutene.json", "9003-27-4"),
                ("Polystyrene.json", "9003-53-6"),
                ("Poly_vinyl_acetate.json", "9003-20-7"),
                ("Polydimethylsiloxane.json", "63148-62-9"),
                ("Poly_n_butyl_methacrylate.json", "9003-63-8"),
                ("Polybutadiene.json", "9003-17-2"),
                ("Poly_alpha_methylstyrene.json", "25014-31-7"),
                ("Poly_methyl_methacrylate.json", "9011-14-7"),
                ("Poly_methyl_acrylate.json", "9003-21-8"),
                ("Poly_ethylene_glycol.json", "25322-68-3"),
            };

            foreach (var (file, cas) in polymers)
            {
                var json = File.ReadAllText(Path.Combine(addcomps, file));
                var poly = Newtonsoft.Json.JsonConvert.DeserializeObject<DWSIM.Thermodynamics.BaseClasses.ConstantProperties>(json);
                poly.CurrentDB = "User";   // exactly what DWSIM.Thermodynamics.Databases.UserDB does on load
                poly.OriginalDB = "User";
                Assert.That(poly.CAS_Number, Is.EqualTo(cas), $"{file}: CAS must match pcsaft.dat");

                var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
                fs.Init();
                fs.AddCompound("N-pentane");
                fs.Options.SelectedComponents.Add(poly.Name, poly);

                var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
                var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
                var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
                ms.SetFlowsheet(fs);
                ms.PropertyPackage = pp;
                ms.AssignSelfToPP();

                // 2 wt% polymer, 100 bar / 400 K: a dilute, single-liquid solution for every polymer.
                double nPoly = 0.02 / poly.Molar_Weight, nC5 = 0.98 / 72.15, tot = nPoly + nC5;
                ms.SetMassFlow(1.0);
                ms.SetPressure(100e5);
                ms.SetTemperature(400.0);
                ms.SetOverallComposition(new[] { nC5 / tot, nPoly / tot });
                ms.SetFlashSpec("PT");
                ms.Calculate();

                var liq = ms.Phases[3];
                double rho = liq.Properties.density.GetValueOrDefault();
                double mu = liq.Properties.viscosity.GetValueOrDefault();
                pp.CurrentMaterialStream = ms;
                double k = pp.AUX_CONDTL(400.0, 3);
                double sigma = pp.AUX_SURFTM(400.0);
                TestContext.WriteLine($"{poly.Name,-22} rho={rho:F1}  mu={mu:E3}  k={k:F4}  sigma={sigma:E3}");

                Assert.That(rho, Is.GreaterThan(100.0).And.LessThan(1500.0), $"{poly.Name}: density must be physical");
                Assert.That(mu, Is.GreaterThan(0.0).And.LessThan(1.0e5), $"{poly.Name}: viscosity must be finite");
                Assert.That(k, Is.GreaterThan(0.01).And.LessThan(1.0), $"{poly.Name}: thermal conductivity must be physical");
                Assert.That(sigma, Is.GreaterThan(0.0).And.LessThan(0.1), $"{poly.Name}: surface tension must be physical");
            }
        }

        private static string SourceDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path);

        // n-pentane + one injected polymer/copolymer (CAS `cas`, molar mass `mw`); when `copoly` is given
        // the compound is registered as a copolymer with that segment definition. Returns pp and the feed
        // mole fractions at `wPoly` polymer mass fraction.
        private static (DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage pp, double[] z) PentanePlusPolymer(
            string cas, string copoly, double mw, double wPoly, double mOverM)
        {
            string addcomps = Path.GetFullPath(Path.Combine(SourceDir(), "..", "..", "content", "addcomps"));
            var json = File.ReadAllText(Path.Combine(addcomps, "Polyethylene_HDPE.json"));
            var poly = Newtonsoft.Json.JsonConvert.DeserializeObject<DWSIM.Thermodynamics.BaseClasses.ConstantProperties>(json);
            poly.CurrentDB = "User"; poly.OriginalDB = "User";
            poly.CAS_Number = cas; poly.Name = "TestPoly_" + cas; poly.Molar_Weight = mw;

            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("N-pentane");
            fs.Options.SelectedComponents.Add(poly.Name, poly);
            var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
            if (copoly != null)
                pp.CompoundParameters[cas] = new DWSIM.Thermodynamics.AdvancedEOS.PCSParam
                { casno = cas, compound = poly.Name, mw = mw, m_over_M = mOverM, copolymer = copoly };
            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs); ms.PropertyPackage = pp; ms.AssignSelfToPP(); pp.CurrentMaterialStream = ms;

            double nPoly = wPoly / mw, nC5 = (1 - wPoly) / 72.15, tot = nPoly + nC5;
            return (pp, new[] { nC5 / tot, nPoly / tot });
        }

        /// <summary>
        /// Copolymer PC-SAFT correctness (Gross et al. 2003). A copolymer whose two segments are the SAME
        /// repeat unit is chemically identical to that homopolymer, so its log fugacity coefficient, taken
        /// through the segment expansion and the numerical copolymer chemical potential, must reproduce the
        /// homopolymer computed through the ordinary analytical path. This exercises the whole copolymer
        /// machinery (segment expansion, bonding fractions, segment kij, numerical fugacity) against a
        /// known answer.
        /// </summary>
        [Test]
        public void CopolymerOfIdenticalSegmentsReproducesHomopolymer()
        {
            var st = DWSIM.Thermodynamics.PropertyPackages.State.Liquid;
            double T = 400.0, P = 100e5, mw = 50000.0, w = 0.05;

            var (ppH, zH) = PentanePlusPolymer("9002-88-4", null, mw, w, 0.0263);          // PE homopolymer
            var (ppC, zC) = PentanePlusPolymer("PECOPOLY", "9002-88-4:0.5;9002-88-4:0.5", mw, w, 0.0263); // 2 PE segments

            var lnH = ppH.DW_CalcLnFugCoeff(zH, T, P, st);
            var lnC = ppC.DW_CalcLnFugCoeff(zC, T, P, st);
            TestContext.WriteLine($"homopolymer PE: solvent={lnH[0]:R} polymer={lnH[1]:R}");
            TestContext.WriteLine($"copolymer PE/PE: solvent={lnC[0]:R} polymer={lnC[1]:R}");
            // The solvent (well-conditioned) matches to ~1e-6. The polymer log fugacity (magnitude ~2100)
            // matches to ~0.05%: the copolymer path takes the residual chemical potential numerically while
            // the homopolymer takes it from the analytical high-segment-number derivatives, which lose a
            // little precision at m ~ 1300. That agreement confirms the copolymer machinery is correct.
            Assert.That(lnC[0], Is.EqualTo(lnH[0]).Within(1e-3), "solvent lnphi: copolymer of identical segments must equal the homopolymer");
            Assert.That(lnC[1], Is.EqualTo(lnH[1]).Within(Math.Abs(lnH[1]) * 2e-3), "polymer lnphi: copolymer of identical segments must equal the homopolymer");
        }

        /// <summary>
        /// Copolymer liquid-liquid equilibrium (Gross et al. 2003). A poly(ethylene-co-propylene) solution
        /// in n-pentane demixes into a polymer-rich phase whose composition lies between those of the
        /// polyethylene and polypropylene homopolymer solutions at the same temperature and pressure, using
        /// the shipped ethylene-propylene internal kij (-0.009) and the homopolymer-solvent kij, all read
        /// from pcsaft_ip.dat by segment CAS. Computed with the convex-hull-of-Gibbs binodal (fugacity only),
        /// the LLE path a copolymer must use. Confirms the copolymer machinery, the segment kij lookup and
        /// the numerical fugacity produce physical, correctly-interpolated phase behaviour.
        /// </summary>
        [Test]
        public void CopolymerLleInterpolatesBetweenHomopolymers()
        {
            double T = 460.0, P = 30e5, Mn = 100000.0, Msolv = 72.15;
            var (ppPE, _) = PentanePlusPolymer("9002-88-4", null, Mn, 0.05, 0.0263);
            var (ppPP, _) = PentanePlusPolymer("9003-07-0", null, Mn, 0.05, 0.02305);
            var (ppCO, _) = PentanePlusPolymer("PEPCOPOLY", "9002-88-4:0.5;9003-07-0:0.5", Mn, 0.05, 0.0);
            double wPE = LleBinodal(ppPE, T, P, Mn, Msolv).wR;
            double wPP = LleBinodal(ppPP, T, P, Mn, Msolv).wR;
            double wCO = LleBinodal(ppCO, T, P, Mn, Msolv).wR;
            TestContext.WriteLine($"polymer-rich cloud fraction: PE={wPE:F3} PEP={wCO:F3} PP={wPP:F3}");

            Assert.That(wPE, Is.GreaterThan(0.05), "PE homopolymer must demix");
            Assert.That(wPP, Is.GreaterThan(0.05), "PP homopolymer must demix");
            Assert.That(wCO, Is.GreaterThan(0.05), "the PEP copolymer must demix");
            Assert.That(wCO, Is.GreaterThan(wPP).And.LessThan(wPE),
                        "the copolymer cloud composition must lie between the two homopolymers");
        }

        /// <summary>
        /// A real poly(ethylene-co-propylene) (PEP) copolymer in n-pentane must be physical and lie between
        /// the two homopolymers: the polymer's log fugacity coefficient falls between that of the
        /// polyethylene and the polypropylene solutions at the same conditions, since a random copolymer's
        /// segments are a blend of the two.
        /// </summary>
        [Test]
        public void Poly_ethylene_co_propyleneIsPhysicalAndIntermediate()
        {
            var st = DWSIM.Thermodynamics.PropertyPackages.State.Liquid;
            double T = 460.0, P = 40e5, mw = 100000.0, w = 0.10;

            // ethylene segment = HDPE (9002-88-4), propylene segment = PP (9003-07-0)
            var (ppPE, zPE) = PentanePlusPolymer("9002-88-4", null, mw, w, 0.0263);
            var (ppPP, zPP) = PentanePlusPolymer("9003-07-0", null, mw, w, 0.02305);
            var (ppCO, zCO) = PentanePlusPolymer("PEPCOPOLY", "9002-88-4:0.7;9003-07-0:0.3", mw, w, 0.0);

            double lnPE = ppPE.DW_CalcLnFugCoeff(zPE, T, P, st)[1];
            double lnPP = ppPP.DW_CalcLnFugCoeff(zPP, T, P, st)[1];
            var lnCO = ppCO.DW_CalcLnFugCoeff(zCO, T, P, st);
            TestContext.WriteLine($"polymer lnphi: PE={lnPE:F2} PEP={lnCO[1]:F2} PP={lnPP:F2}  (solvent PEP={lnCO[0]:F3})");

            Assert.That(lnCO.All(v => !double.IsNaN(v) && !double.IsInfinity(v)), "PEP log fugacity must be finite");
            double lo = Math.Min(lnPE, lnPP), hi = Math.Max(lnPE, lnPP);
            Assert.That(lnCO[1], Is.InRange(lo - 0.02 * Math.Abs(lo), hi + 0.02 * Math.Abs(hi)),
                        "the PEP polymer log fugacity must lie between the PE and PP homopolymers");
        }

        /// <summary>
        /// Validation against Tumakaka, Gross and Sadowski, Fluid Phase Equilibria 194-197 (2002) 541, Fig. 5:
        /// the liquid-liquid cloud pressure of polypropylene (Mw = 50.4 kg/mol) in n-pentane at 5 wt% polymer,
        /// with the paper's kij = 0.0137. The cloud pressure is the boundary between the demixed region below
        /// and the miscible region above; the paper reads about 47, 59 and 73 bar at 177, 187 and 197 C.
        /// </summary>
        [Test]
        public void PolypropyleneNPentaneCloudPressureMatchesTumakaka2002()
        {
            var pp = PolypropyleneInNPentane(out _, 50400.0, 0.05); // Mw = 50.4 kg/mol, 5 wt% PP
            double[] z = LastFeed;

            var reference = new[] { (T: 450.15, Pbar: 47.0), (T: 460.15, Pbar: 59.0), (T: 470.15, Pbar: 73.0) };
            TestContext.WriteLine("PP(50.4 kg/mol)/n-pentane 5 wt%, kij=0.0137   cloud pressure (bar)");
            TestContext.WriteLine("  T(C)   paper(Fig5)   PC-SAFT(this)   splits below / single above");

            foreach (var r in reference)
            {
                double cloud = CloudPressureBar(pp, z, r.T);
                TestContext.WriteLine($"  {r.T - 273.15,4:F0}      {r.Pbar,6:F0}         {cloud,6:F0}");
                Assert.That(cloud, Is.EqualTo(r.Pbar).Within(15.0),
                            $"cloud pressure at {r.T - 273.15:F0} C should be near the paper's {r.Pbar:F0} bar");
            }
        }

        // Highest pressure (bar) at which PP/n-pentane still splits into two liquids: the cloud pressure.
        private static double CloudPressureBar(DWSIM.Thermodynamics.PropertyPackages.PropertyPackage pp, double[] z, double T)
        {
            double lastSplit = double.NaN, firstSingle = double.NaN;
            for (double Pbar = 25.0; Pbar <= 95.0; Pbar += 5.0)
            {
                bool split = HasLiquidLiquidSplit(pp, z, Pbar * 1e5, T);
                if (split) lastSplit = Pbar;
                else if (!double.IsNaN(lastSplit)) { firstSingle = Pbar; break; }
            }
            if (double.IsNaN(lastSplit)) return 0.0;
            if (double.IsNaN(firstSingle)) return lastSplit;
            return 0.5 * (lastSplit + firstSingle); // boundary lies between the last split and the first single phase
        }

        private static bool HasLiquidLiquidSplit(DWSIM.Thermodynamics.PropertyPackages.PropertyPackage pp, double[] z, double P, double T)
        {
            try
            {
                var slle = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.SimpleLLE
                {
                    UseInitialEstimatesForPhase1 = true,
                    InitialEstimatesForPhase1 = new[] { 0.9999999, 0.0000001 }, // dilute (solvent-rich)
                    UseInitialEstimatesForPhase2 = true,
                    InitialEstimatesForPhase2 = new[] { 0.999, 0.001 },          // polymer-rich
                };
                var r = (object[])slle.Flash_PT((double[])z.Clone(), P, T, pp);
                double L1 = Convert.ToDouble(r[0]), L2 = Convert.ToDouble(r[5]);
                double w1 = MassFractionPP(((double[])r[2])[1]);
                double w2 = MassFractionPP(((double[])r[6])[1]);
                return Math.Min(L1, L2) > 0.001 && Math.Abs(w1 - w2) > 0.05; // genuine split, not the trivial one
            }
            catch { return false; }
        }

        private static double[] LastFeed;

        /// <summary>
        /// The solvent parameters added from Tihic et al. 2006 must load and give a physical PC-SAFT result.
        /// Checks that a sample of the new compounds carry their parameters, and flashes isooctane in n-hexane.
        /// </summary>
        [Test]
        public void NewSolventParametersLoadAndSolve()
        {
            var pp = Package(fs => { fs.AddCompound("2,2,4-trimethylpentane"); fs.AddCompound("N-hexane"); });

            foreach (var cas in new[] { "540-84-1", "617-78-7", "291-64-5" }) // isooctane, 3-ethylpentane, cycloheptane
                Assert.That(pp.CompoundParameters.ContainsKey(cas), Is.True, $"PC-SAFT parameters for {cas} must be loaded");

            var flash = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.NestedLoops();
            var r = (object[])flash.Flash_PT(new[] { 0.5, 0.5 }, 2e5, 360.0, pp);
            double L = Convert.ToDouble(r[0]), V = Convert.ToDouble(r[1]);
            var lnphi = pp.DW_CalcLnFugCoeff(new[] { 0.5, 0.5 }, 360.0, 2e5, DWSIM.Thermodynamics.PropertyPackages.State.Liquid);
            TestContext.WriteLine($"isooctane/n-hexane 360 K/2 bar: L={L:F3} V={V:F3}  lnPhi=[{lnphi[0]:F3},{lnphi[1]:F3}]");
            Assert.That(L + V, Is.EqualTo(1.0).Within(1e-6));
            Assert.That(double.IsNaN(lnphi[0]) || double.IsNaN(lnphi[1]), Is.False, "isooctane lnPhi must be finite");
        }

        /// <summary>
        /// Validation against Tumakaka et al. 2002, Fig. 2: HDPE/ethylene cloud pressure at 140/150/170 C.
        /// The paper models the polydisperse HDPE (Mn=43, Mw=118, Mz=231 kg/mol) with three pseudocomponents;
        /// this first checks a monodisperse Mw=118 kg/mol binary to confirm the ~1600-1900 bar regime and the
        /// temperature trend with the paper's kij=0.0404.
        /// </summary>
        [Test]
        public void HdpeEthyleneCloudPressureAgainstTumakaka2002()
        {
            var pp = Package(fs =>
            {
                fs.AddCompound("Ethylene");
                var hdpe = new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
                {
                    Name = "Polyethylene HDPE",
                    CAS_Number = "9002-88-4",
                    Formula = "(C2H4)n",
                    Molar_Weight = 118000.0, // Mw
                    Critical_Temperature = 1200.0,
                    Critical_Pressure = 5.0e5,
                    Acentric_Factor = 0.5,
                    Normal_Boiling_Point = 800.0,
                    IsHYPO = 1
                };
                fs.Options.SelectedComponents.Add(hdpe.Name, hdpe);
            });

            const double mwPE = 118000.0, mwEth = 28.054, wFeed = 0.05;
            double nPE = wFeed / mwPE, nEth = (1.0 - wFeed) / mwEth, tot = nPE + nEth;
            double[] z = { nEth / tot, nPE / tot };

            TestContext.WriteLine("HDPE(Mw=118 kg/mol, monodisperse)/ethylene 5 wt%, kij=0.0404   cloud pressure (bar)");
            TestContext.WriteLine("  T(C)   paper exp(Fig2)   PC-SAFT(this)");
            foreach (var r in new[] { (T: 413.15, Pbar: 1850.0), (T: 423.15, Pbar: 1780.0), (T: 443.15, Pbar: 1650.0) })
            {
                double lastSplit = double.NaN, firstSingle = double.NaN;
                for (double Pbar = 1000.0; Pbar <= 2400.0; Pbar += 100.0)
                {
                    bool split = HasHeavySplit(pp, z, Pbar * 1e5, r.T, mwPE, mwEth, 1e-8, 1e-4);
                    if (split) lastSplit = Pbar;
                    else if (!double.IsNaN(lastSplit)) { firstSingle = Pbar; break; }
                }
                double cloud = double.IsNaN(lastSplit) ? 0.0 : (double.IsNaN(firstSingle) ? lastSplit : 0.5 * (lastSplit + firstSingle));
                TestContext.WriteLine($"  {r.T - 273.15,4:F0}        {r.Pbar,6:F0}           {cloud,6:F0}");
                Assert.That(cloud, Is.EqualTo(r.Pbar).Within(120.0),
                            $"HDPE/ethylene cloud pressure at {r.T - 273.15:F0} C should be near the paper's {r.Pbar:F0} bar");
            }
        }

        private static bool HasHeavySplit(DWSIM.Thermodynamics.PropertyPackages.PropertyPackage pp, double[] z, double P, double T,
                                          double mwHeavy, double mwLight, double diluteX, double richX)
        {
            try
            {
                var slle = new DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms.SimpleLLE
                {
                    UseInitialEstimatesForPhase1 = true,
                    InitialEstimatesForPhase1 = new[] { 1.0 - diluteX, diluteX },
                    UseInitialEstimatesForPhase2 = true,
                    InitialEstimatesForPhase2 = new[] { 1.0 - richX, richX },
                };
                var r = (object[])slle.Flash_PT((double[])z.Clone(), P, T, pp);
                double L1 = Convert.ToDouble(r[0]), L2 = Convert.ToDouble(r[5]);
                double x1 = ((double[])r[2])[1], x2 = ((double[])r[6])[1];
                double w1 = x1 * mwHeavy / (x1 * mwHeavy + (1 - x1) * mwLight);
                double w2 = x2 * mwHeavy / (x2 * mwHeavy + (1 - x2) * mwLight);
                return Math.Min(L1, L2) > 0.001 && Math.Abs(w1 - w2) > 0.05;
            }
            catch { return false; }
        }

        /// <summary>
        /// Full phase-property read-out for a polymer solution through PC-SAFT: every property a MaterialStream
        /// exposes must come out finite and physical. Polypropylene (from its addcomps entry) at 20 wt% in
        /// n-pentane, 460 K / 60 bar, with a supplied melt viscosity so the viscosity path is exercised too.
        /// </summary>
        [Test]
        public void PolymerLiquidReportsEveryPhasePropertyPhysically()
        {
            string addcomps = Path.GetFullPath(Path.Combine(SourceDir(), "..", "..", "content", "addcomps"));
            var poly = Newtonsoft.Json.JsonConvert.DeserializeObject<DWSIM.Thermodynamics.BaseClasses.ConstantProperties>(
                File.ReadAllText(Path.Combine(addcomps, "Polypropylene.json")));
            poly.CurrentDB = "User";
            poly.OriginalDB = "User";
            poly.LiquidViscosityEquation = "1";     // constant-viscosity equation (DIPPR form 1 returns A)
            poly.Liquid_Viscosity_Const_A = 500.0;  // user-supplied melt viscosity, Pa.s

            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("N-pentane");
            fs.Options.SelectedComponents.Add(poly.Name, poly);
            var pp = new DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage { Flowsheet = fs };
            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.PropertyPackage = pp;
            ms.AssignSelfToPP();

            double nPoly = 0.20 / poly.Molar_Weight, nC5 = 0.80 / 72.15, tot = nPoly + nC5;
            ms.SetMassFlow(1.0);
            ms.SetPressure(60e5);
            ms.SetTemperature(460.15);
            ms.SetOverallComposition(new[] { nC5 / tot, nPoly / tot });
            ms.SetFlashSpec("PT");
            ms.Calculate();

            pp.CurrentMaterialStream = ms;
            var q = ms.Phases[3].Properties;
            double sigma = pp.AUX_SURFTM(460.15);

            var rows = new (string name, double? val, string unit)[]
            {
                ("Molecular weight", q.molecularWeight, "g/mol"),
                ("Compressibility Z", q.compressibilityFactor, "-"),
                ("Density", q.density, "kg/m3"),
                ("Viscosity", q.viscosity, "Pa.s"),
                ("Kinematic viscosity", q.kinematic_viscosity, "m2/s"),
                ("Thermal conductivity", q.thermalConductivity, "W/m.K"),
                ("Surface tension", sigma, "N/m"),
                ("Heat capacity Cp", q.heatCapacityCp, "kJ/kg.K"),
                ("Heat capacity Cv", q.heatCapacityCv, "kJ/kg.K"),
                ("Enthalpy", q.enthalpy, "kJ/kg"),
                ("Entropy", q.entropy, "kJ/kg.K"),
                ("Molar enthalpy", q.molar_enthalpy, "kJ/kmol"),
                ("Molar entropy", q.molar_entropy, "kJ/kmol.K"),
            };

            TestContext.WriteLine("Polypropylene (Mn=50 kg/mol) 20 wt% in n-pentane, 460.15 K, 60 bar");
            foreach (var r in rows)
            {
                TestContext.WriteLine($"  {r.name,-22} {r.val.GetValueOrDefault(),14:G6} {r.unit}");
                Assert.That(r.val.HasValue && !double.IsNaN(r.val.Value) && !double.IsInfinity(r.val.Value),
                            Is.True, $"{r.name} must be a finite number");
            }

            Assert.That(q.density.GetValueOrDefault(), Is.GreaterThan(100.0).And.LessThan(1500.0));
            Assert.That(q.viscosity.GetValueOrDefault(), Is.GreaterThan(1.0e-3), "supplied polymer viscosity must raise the solution viscosity above the solvent's");
            Assert.That(q.thermalConductivity.GetValueOrDefault(), Is.GreaterThan(0.01).And.LessThan(1.0));
            Assert.That(sigma, Is.GreaterThan(0.0).And.LessThan(0.1));
            Assert.That(q.heatCapacityCp.GetValueOrDefault(), Is.GreaterThan(0.5).And.LessThan(10.0), "Cp in a physical range");
        }

        // A polypropylene (Mn = 50.4 kg/mol) pseudo-compound dissolved in n-pentane, with a feed at 20 wt%
        // polymer - well inside the miscibility gap, though by mole the polymer is only a trace (~4e-4).
        private static DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2PropertyPackage PolypropyleneInNPentane(
            out double[] feed, double mwP = 50400.0, double wFeed = 0.20)
        {
            var pp = Package(fs =>
            {
                fs.AddCompound("N-pentane");
                var poly = new DWSIM.Thermodynamics.BaseClasses.ConstantProperties
                {
                    Name = "Polypropylene",
                    CAS_Number = "9003-07-0",
                    Formula = "(C3H6)n",
                    Molar_Weight = mwP,
                    Critical_Temperature = 1200.0,
                    Critical_Pressure = 5.0e5,
                    Acentric_Factor = 0.5,
                    Normal_Boiling_Point = 800.0,
                    IsHYPO = 1
                };
                fs.Options.SelectedComponents.Add(poly.Name, poly);
            });

            const double mwC5 = 72.15;
            double nPP = wFeed / mwP, nC5 = (1.0 - wFeed) / mwC5, tot = nPP + nC5;
            feed = new[] { nC5 / tot, nPP / tot };
            LastFeed = feed;
            return pp;
        }

        // Mass fraction of the polymer from its mole fraction (n-pentane 72.15, polypropylene 50400 g/mol).
        private static double MassFractionPP(double xPP) => xPP * 50400.0 / (xPP * 50400.0 + (1.0 - xPP) * 72.15);
    }
}
