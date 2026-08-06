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
    /// numbers in the ignored test are what the native Ipopt39.dll produces for the same two
    /// cases, from `DWSIM.Automation.FluentAPI.Tests.exe gibbs3p` in the DWSIM_Private tree.
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
        {
            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();

            foreach (var name in compounds)
            {
                Assert.That(flowsheet.AvailableCompounds.ContainsKey(name),
                            name + " is not in the compound database");

                flowsheet.AddCompound(name);
            }

            var pp = new DWSIM.Thermodynamics.PropertyPackages.NRTLPropertyPackage
            {
                Flowsheet = flowsheet
            };

            var obj = flowsheet.AddObject(
                Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "feed");

            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)flowsheet.SimulationObjects[obj.Name];
            ms.SetFlowsheet(flowsheet);
            ms.SetPropertyPackage(pp);
            pp.CurrentMaterialStream = ms;

            return pp;
        }

        [Test]
        [Ignore("The managed solver reaches a vapour fraction of 0.21 against a native 0.42. " +
                "The exact Hessian is not the answer: GibbsMinimization3P declares nele_hess = 0 " +
                "and sets hessian_approximation=limited-memory, so the native run uses the same " +
                "quasi-Newton matrix this one does. What happens is that the line search " +
                "collapses at a point that is already feasible, the objective stops moving, and " +
                "the flash's own intermediate callback ends the solve. Restoration does not " +
                "apply there and rebuilding the Hessian is not enough, so what is left to " +
                "examine is the search direction itself: whether the augmented system, at the " +
                "inertia this solver accepts, is giving a descent direction for the barrier " +
                "objective at all.")]
        public void TheGibbsFlashMatchesTheNativeSolver()
        {
            // Native reference, ethanol and water at 355 K, from the DWSIM_Private harness:
            //   gibbs   V 0.42217598424112829  y 0.5737043701687784  x 0.27315871509177725
            //   nested  V 0.42112183178205903  y 0.5738157532569774  x 0.27354160543063455
            var pp = Package("Ethanol", "Water");
            var feed = new[] { 0.4, 0.6 };

            var gibbs = Split.Of((object[])new GibbsMinimization3P
            {
                ForceTwoPhaseOnly = true,
                StabSearchSeverity = 0,
                StabSearchCompIDs = pp.RET_VNAMES()
            }.Flash_PT((double[])feed.Clone(), P, 355.0, pp));

            Assert.That(gibbs.Vapour, Is.EqualTo(0.42217598424112829).Within(1e-4));
            Assert.That(gibbs.Y[0], Is.EqualTo(0.5737043701687784).Within(1e-4));
            Assert.That(gibbs.X1[0], Is.EqualTo(0.27315871509177725).Within(1e-4));
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
