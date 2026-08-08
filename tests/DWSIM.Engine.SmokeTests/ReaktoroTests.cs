//    The Reaktoro property package's own data, and the binding over Reaktoro's flat C API.
//
//    The package reaches Reaktoro 2 directly now; it used to go through the Reaktoro 1 Python
//    package over the CPython bridge, which pinned DWSIM to a Python between 3.7 and 3.9.

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
    }
}
