using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    /// <summary>
    /// The Gibbs reactor of the Gibbs and Equilibrium Reactors sample, pinned against what the
    /// native IPOPT produces.
    ///
    /// This is the end-to-end check on the managed solver. The reactor runs in direct
    /// minimisation mode with UseIPOPTSolver on, so it minimises the Gibbs energy through
    /// MathEx.Optimization.IPOPTSolver with the element balance folded into the objective as a
    /// penalty: a bound-constrained problem, which is the case the managed solver serves. The
    /// expected values below were produced by the .NET Framework build of this same engine,
    /// where that wrapper binds Ipopt39.dll, with
    ///
    ///   DWSIM.Automation.FluentAPI.Tests.exe gibbsdump
    ///
    /// in the DWSIM_Private tree. A steam reformer at 1000 K: methane and water in, synthesis
    /// gas out.
    /// </summary>
    [TestFixture]
    public class GibbsReactorTests
    {
        private const string Sample = "GibbsAndEquilibriumReactors.dwxml";

        // Mole fractions of the reactor outlet, from the native run.
        private static readonly (string Compound, double Fraction)[] Expected =
        {
            ("Carbon dioxide",  0.03687075218353978),
            ("Carbon monoxide", 0.17421807267619011),
            ("Hydrogen",        0.67013598646554906),
            ("Methane",         0.020079580519437049),
            ("Water",           0.098695608155284109),
        };

        private const double NativeInitialGibbs = -808.69816311351428;
        private const double NativeFinalGibbs = -984.06203055084325;

        // Measured, not guessed. The managed solver lands 2.7e-5 percent from the native one on
        // the final Gibbs energy, which is the objective both minimised, and up to 8.9e-5 away in
        // mole fraction. The two numbers are consistent with each other rather than independent:
        // at a minimum the energy is stationary, so an error eps in composition costs eps^2 in
        // energy, and sqrt(2.7e-7) is 5e-4, the order of the relative composition spread seen
        // here. In other words both solvers found the same minimum and stopped at slightly
        // different points of its floor. The reactor asks for tol = 1e-20, which neither can
        // reach, so where each stops is its own business.
        private const double EnergyTolerancePercent = 1e-4;
        private const double FractionTolerance = 1e-4;

        [Test]
        public void TheGibbsReactorMatchesTheNativeSolver()
        {
            var folder = TestContext.CurrentContext.TestDirectory;

            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
            {
                folder = Path.GetDirectoryName(folder);
            }

            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets above the test directory");

            var path = Path.Combine(folder, "tests", "flowsheets", Sample);

            Assert.That(File.Exists(path), Sample + " is not in tests/flowsheets");

            var flowsheet = new DWSIM.DynamicRunner.Flowsheet(null, null);
            flowsheet.Init();
            flowsheet.LoadFromXML(System.Xml.Linq.XDocument.Load(path));

            var errors = flowsheet.SolveFlowsheet2();

            Assert.That(errors, Is.Empty,
                        "the solver reported: " + string.Join("; ", errors.Select(e => e.Message)));

            dynamic reactor = flowsheet.SimulationObjects.Values
                .First(o => o.GetType().Name == "Reactor_Gibbs");

            Assert.That((bool)reactor.UseIPOPTSolver, Is.True,
                        "the reactor is not the IPOPT path any more, so this test no longer checks it");
            Assert.That((bool)reactor.AlternateSolvingMethod, Is.False,
                        "the reactor switched to the Lagrange method, which does not use IPOPT");

            TestContext.WriteLine("initial Gibbs energy  native {0:R17}  managed {1:R17}",
                                  NativeInitialGibbs, (double)reactor.InitialGibbsEnergy);
            TestContext.WriteLine("final Gibbs energy    native {0:R17}  managed {1:R17}",
                                  NativeFinalGibbs, (double)reactor.FinalGibbsEnergy);

            Assert.That((double)reactor.InitialGibbsEnergy,
                        Is.EqualTo(NativeInitialGibbs).Within(EnergyTolerancePercent).Percent);
            Assert.That((double)reactor.FinalGibbsEnergy,
                        Is.EqualTo(NativeFinalGibbs).Within(EnergyTolerancePercent).Percent);

            var outlet = flowsheet.SimulationObjects.Values
                .First(o => o.GraphicObject != null && o.GraphicObject.Tag == "2");

            dynamic stream = outlet;

            foreach (var (compound, fraction) in Expected)
            {
                var found = ((System.Collections.IEnumerable)stream.Phases[0].Compounds.Values)
                            .Cast<dynamic>()
                            .First(c => (string)c.Name == compound);

                double x = found.MoleFraction ?? 0.0;

                TestContext.WriteLine("{0,-18} native {1:R17}  managed {2:R17}  diff {3:E3}",
                                      compound, fraction, x, Math.Abs(x - fraction));

                Assert.That(x, Is.EqualTo(fraction).Within(FractionTolerance),
                            compound + " leaves the reactor at a different fraction than the native solver gave");
            }
        }
    }
}
