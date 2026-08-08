//    The Reaktoro property package's own data, and the binding over Reaktoro's flat C API.
//
//    The package reaches Reaktoro 2 directly now; it used to go through the Reaktoro 1 Python
//    package over the CPython bridge, which pinned DWSIM to a Python between 3.7 and 3.9.

using System;
using System.Linq;
using DWSIM.Thermodynamics.ReaktoroPropertyPackage;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class ReaktoroTests
    {
        /// <summary>
        /// The compound map and the Setschenow coefficients are embedded resources, and both were
        /// missing from this repository: every constructor of the package threw on the null stream,
        /// so nothing it offers could be reached at all.
        /// </summary>
        [Test]
        public void ThePackageDataIsEmbedded()
        {
            var maps = new CompoundMapper().Maps;
            var setschenow = new SetschenowCoefficients();

            TestContext.WriteLine("{0} compounds mapped", maps.Count);

            Assert.That(maps, Is.Not.Empty);
            Assert.That(maps.ContainsKey("Water"), Is.True);
            Assert.That(setschenow.GetValue("carbon dioxide"), Is.EqualTo(-0.2277).Within(1e-6));
        }

        /// <summary>
        /// Reaktoro 1 wrote a doubly charged ion as SO4--; Reaktoro 2 writes SO4-2, and looks its
        /// species up by that name. A map still carrying the old spelling fails at the first
        /// equilibrium with "Could not find any Species object".
        /// </summary>
        [Test]
        public void TheSpeciesNamesAreSpelledAsReaktoro2SpellsThem()
        {
            var maps = new CompoundMapper().Maps;

            var stale = maps.Values
                            .Where(m => m.AqueousName.EndsWith("--") || m.AqueousName.EndsWith("++"))
                            .Select(m => m.AqueousName)
                            .ToList();

            Assert.That(stale, Is.Empty, "species still named the Reaktoro 1 way: " + string.Join(", ", stale));

            Assert.That(maps["Carbonate (ion)"].AqueousName, Is.EqualTo("CO3-2"));
            Assert.That(maps["Sulfate (ion)"].AqueousName, Is.EqualTo("SO4-2"));
        }

        /// <summary>
        /// The binding, end to end: a system with an aqueous and a gaseous phase, and one
        /// equilibrium solved through it.
        /// </summary>
        /// <remarks>
        /// The numbers are the ones the release itself was checked against, from the other side of
        /// the boundary: <c>scripts/check_runtime.py</c> in DanWBR/reaktoro loads the same runtime
        /// through ctypes and gets 1.019233 mol aqueous and 0.050767 mol gaseous for this feed, on
        /// every platform it builds for. Agreeing with that is what proves the marshalling.
        /// </remarks>
        [Test]
        public void TheBindingSolvesAnEquilibrium()
        {
            string version;

            try
            {
                version = Reaktoro.Version();
            }
            catch (DllNotFoundException)
            {
                Assert.Ignore("No Reaktoro runtime on this machine. The build downloads one; this "
                              + "is what an offline build, or win-arm64, looks like.");
                return;
            }

            TestContext.WriteLine("Reaktoro " + version);

            using (var system = Reaktoro.CreateSystem("H2O(aq) H+ OH- CO2(aq) HCO3- CO3-2 Na+ Cl-",
                                                      "CO2(g) H2O(g)"))
            {
                Assert.That(system.SpeciesCount, Is.EqualTo(10));
                Assert.That(system.SpeciesNames[0], Is.EqualTo("H2O(aq)"));

                // A mole of water, a little salt and a little carbon dioxide, at 25 C and one bar.
                var result = system.Equilibrate(298.15, 1.0e5,
                                                new[] { "H2O", "NaCl", "CO2" },
                                                new[] { 1.0, 0.01, 0.05 });

                TestContext.WriteLine("aqueous {0:F6} mol, gaseous {1:F6} mol",
                                      result.AqueousAmount, result.GaseousAmount);

                Assert.That(result.AqueousAmount, Is.EqualTo(1.019233).Within(1e-5));
                Assert.That(result.GaseousAmount, Is.EqualTo(0.050767).Within(1e-5));
                Assert.That(result.SpeciesAmounts.Sum(), Is.EqualTo(1.07).Within(1e-5));

                // Water is the solvent and its activity coefficient sits near one; the ions are
                // well away from it. What matters here is that they are finite and told apart.
                var gamma = result.LnActivityCoefficients;

                Assert.That(gamma.All(g => !double.IsNaN(g) && !double.IsInfinity(g)), Is.True);
                Assert.That(Math.Exp(gamma[0]), Is.EqualTo(1.0).Within(0.05));
            }
        }

        /// <summary>
        /// The other way of building a system, which is how the Gibbs reactor poses its problem:
        /// name the elements and let the database supply every species that can be made from them.
        /// </summary>
        [Test]
        public void ASystemCanBeBuiltFromElements()
        {
            try
            {
                Reaktoro.Version();
            }
            catch (DllNotFoundException)
            {
                Assert.Ignore("No Reaktoro runtime on this machine.");
                return;
            }

            using (var system = Reaktoro.CreateSpeciatedSystem("supcrt", "supcrt07", "H O C Na Cl",
                                                               aqueous: true, gaseous: true,
                                                               liquid: false, mineral: false))
            {
                TestContext.WriteLine("{0} species from five elements", system.SpeciesCount);

                Assert.That(system.SpeciesCount, Is.GreaterThan(20));
                Assert.That(system.SpeciesNames, Does.Contain("H2O(aq)"));

                var result = system.Equilibrate(348.15, 5.0e5,
                                                new[] { "H2O", "NaCl", "CO2" },
                                                new[] { 1.0, 0.05, 0.2 });

                TestContext.WriteLine("aqueous {0:F6} mol, gaseous {1:F6} mol",
                                      result.AqueousAmount, result.GaseousAmount);

                Assert.That(result.AqueousAmount, Is.GreaterThan(0.0));
                Assert.That(result.GaseousAmount, Is.GreaterThan(0.0));
                Assert.That(result.SpeciesAmounts.Sum(), Is.GreaterThan(1.0));
            }
        }

        /// <summary>A database's own species list, which the Gibbs reactor's editor shows.</summary>
        [Test]
        public void ADatabaseListsItsSpecies()
        {
            try
            {
                Reaktoro.Version();
            }
            catch (DllNotFoundException)
            {
                Assert.Ignore("No Reaktoro runtime on this machine.");
                return;
            }

            var species = Reaktoro.ListSpecies("supcrt", "supcrt07");

            var byState = species.GroupBy(s => s.State)
                                 .OrderByDescending(g => g.Count())
                                 .Select(g => g.Key + " " + g.Count());

            TestContext.WriteLine("{0} species: {1}", species.Count, string.Join(", ", byState));

            Assert.That(species, Is.Not.Empty);
            Assert.That(species.Any(s => s.State == "aqueous"), Is.True);
            Assert.That(species.Any(s => s.State == "gas"), Is.True);
            Assert.That(species.First(s => s.Name == "H2O(aq)").Formula, Is.EqualTo("H2O"));
        }
    }
}
