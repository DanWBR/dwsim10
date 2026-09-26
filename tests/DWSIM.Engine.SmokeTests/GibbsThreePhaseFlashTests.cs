using System;
using System.Linq;
using NUnit.Framework;
using DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The Gibbs three-phase flash: the one caller in the engine that poses constraints. It builds
    /// m = n + 1 of them, `0 &lt;= z_i F - x_i - x_(i+m) &lt;= 1000`, with a Jacobian that is -1 in two
    /// places per row.
    ///
    /// No sample flowsheet selects this algorithm, so it is driven here directly and checked
    /// against the flash that is the default for a liquid split, NestedLoops3PV3. The reference
    /// numbers are what the native Ipopt39.dll produces for the same two cases, from
    /// `DWSIM.Automation.FluentAPI.Tests.exe gibbs3p` in the DWSIM_Private tree.
    /// </summary>
    [TestFixture]
    public class GibbsThreePhaseFlashTests
    {
        private const double P = 101325.0;

        private sealed class Split
        {
            public double Liquid1;
            public double Vapour;
            public double Liquid2;
            public double[] X1 = Array.Empty<double>();
            public double[] Y = Array.Empty<double>();
            public double[] X2 = Array.Empty<double>();

            /// <summary>The tuple every flash returns: {L1/F, V/F, x1, y, iterations, L2/F, x2, ...}.</summary>
            public static Split Of(object[] result)
            {
                return new Split
                {
                    Liquid1 = Convert.ToDouble(result[0]),
                    Vapour = Convert.ToDouble(result[1]),
                    X1 = (double[])result[2],
                    Y = (double[])result[3],
                    Liquid2 = Convert.ToDouble(result[5]),
                    X2 = (double[])result[6],
                };
            }
        }

        private static DWSIM.Thermodynamics.PropertyPackages.PropertyPackage Package(params string[] compounds)
            => Package(new DWSIM.Thermodynamics.PropertyPackages.NRTLPropertyPackage(), compounds);

        private static DWSIM.Thermodynamics.PropertyPackages.PropertyPackage Package(
            DWSIM.Thermodynamics.PropertyPackages.PropertyPackage pp, params string[] compounds)
        {
            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();

            foreach (var name in compounds)
            {
                Assert.That(flowsheet.AvailableCompounds.ContainsKey(name),
                            name + " is not in the compound database");

                flowsheet.AddCompound(name);
            }

            pp.Flowsheet = flowsheet;

            var obj = flowsheet.AddObject(
                Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "feed");

            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)flowsheet.SimulationObjects[obj.Name];
            ms.SetFlowsheet(flowsheet);
            ms.SetPropertyPackage(pp);
            pp.CurrentMaterialStream = ms;

            return pp;
        }

        /// <summary>
        /// What used to fail here was the solve, and the flash believing it. The constrained solver
        /// measured its trial points under the new barrier parameter against the current point
        /// under the old one, so after every fall of the parameter the line search rejected all
        /// its trial points; and the flash's stall callback (objective repeated to 1e-10) read the
        /// repeat as convergence. The solver now measures both under the same parameter, and the
        /// flash takes Newton steps on the exact Hessian of the Gibbs energy with no stall callback.
        ///
        /// Water, n-hexane and methane at 10 bar and 350 K split into a vapour and two liquids, so
        /// the Gibbs flash runs its constrained minimization from a genuine second liquid. It is
        /// checked against NestedLoops3PV3 on the same package in the same run, which keeps the
        /// comparison free of cross-architecture last-bit differences.
        /// </summary>
        [Test]
        public void TheGibbsFlashMatchesTheNativeSolver()
        {
            var pp = Package(new DWSIM.Thermodynamics.PropertyPackages.PengRobinsonPropertyPackage(),
                             "Water", "N-hexane", "Methane");
            var feed = new[] { 0.5, 0.3, 0.2 };
            const double p = 10e5, t = 350.0;

            var nested = Split.Of((object[])new NestedLoops3PV3
            {
                StabSearchSeverity = 0,
                StabSearchCompIDs = pp.RET_VNAMES()
            }.Flash_PT((double[])feed.Clone(), p, t, pp));

            var gibbs = Split.Of((object[])new GibbsMinimization3P
            {
                StabSearchSeverity = 0,
                StabSearchCompIDs = pp.RET_VNAMES()
            }.Flash_PT((double[])feed.Clone(), p, t, pp));

            TestContext.WriteLine("nested V {0:R17}  L1 {1:R17}  L2 {2:R17}", nested.Vapour, nested.Liquid1, nested.Liquid2);
            TestContext.WriteLine("gibbs  V {0:R17}  L1 {1:R17}  L2 {2:R17}", gibbs.Vapour, gibbs.Liquid1, gibbs.Liquid2);

            Assert.That(nested.Vapour, Is.GreaterThan(0.0), "the reference is not three-phase");
            Assert.That(nested.Liquid2, Is.GreaterThan(0.0), "the reference is not three-phase");
            // which liquid is called 1 and which 2 is each algorithm's own convention
            Assert.That(gibbs.Vapour, Is.EqualTo(nested.Vapour).Within(1e-3));
            Assert.That(Math.Min(gibbs.Liquid1, gibbs.Liquid2), Is.EqualTo(Math.Min(nested.Liquid1, nested.Liquid2)).Within(1e-3));
            Assert.That(Math.Max(gibbs.Liquid1, gibbs.Liquid2), Is.EqualTo(Math.Max(nested.Liquid1, nested.Liquid2)).Within(1e-3));
        }

        /// <summary>
        /// Ethanol and water at 355 K have no second liquid. The stability test's only candidate is
        /// a copy of the liquid, which the Gibbs flash drops, so it returns the two-phase result of
        /// the nested-loops flash it starts from.
        /// </summary>
        [Test]
        public void TheGibbsFlashKeepsATwoPhaseSplitTwoPhase()
        {
            var pp = Package("Ethanol", "Water");
            var feed = new[] { 0.4, 0.6 };

            var gibbs = Split.Of((object[])new GibbsMinimization3P
            {
                ForceTwoPhaseOnly = true,
                StabSearchSeverity = 0,
                StabSearchCompIDs = pp.RET_VNAMES()
            }.Flash_PT((double[])feed.Clone(), P, 355.0, pp));

            TestContext.WriteLine("gibbs V {0:R17}  y {1:R17}  x {2:R17}", gibbs.Vapour, gibbs.Y[0], gibbs.X1[0]);

            Assert.That(gibbs.Liquid2, Is.EqualTo(0.0).Within(1e-12));
            Assert.That(gibbs.Vapour, Is.EqualTo(0.42112183178205903).Within(1e-4));
            Assert.That(gibbs.Y[0], Is.EqualTo(0.5738157532569774).Within(1e-4));
            Assert.That(gibbs.X1[0], Is.EqualTo(0.27354160543063455).Within(1e-4));
        }

        [Test]
        public void TheDefaultThreePhaseFlashStillAgreesWithItself()
        {
            // The control the ignored test above is measured against, so that a change in the
            // thermodynamics rather than in the solver cannot be mistaken for progress on it.
            var pp = Package("Ethanol", "Water");

            var nested = Split.Of((object[])new NestedLoops3PV3
            {
                StabSearchSeverity = 0,
                StabSearchCompIDs = pp.RET_VNAMES()
            }.Flash_PT(new[] { 0.4, 0.6 }, P, 355.0, pp));

            TestContext.WriteLine("nested V {0:R17}  y {1:R17}  x {2:R17}",
                                  nested.Vapour, nested.Y[0], nested.X1[0]);

            Assert.That(nested.Vapour, Is.EqualTo(0.42112183178205903).Within(1e-4),
                        "the default flash moved, so the reference for the Gibbs flash moved too");
            Assert.That(nested.Y[0], Is.EqualTo(0.5738157532569774).Within(1e-4));
            Assert.That(nested.X1[0], Is.EqualTo(0.27354160543063455).Within(1e-4));
        }
    }
}
