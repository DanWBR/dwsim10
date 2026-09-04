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
