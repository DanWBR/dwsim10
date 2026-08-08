//    The CoolProp binding and the native library that backs it.
//
//    The binding is over CoolProp's flat C API; the SWIG-generated wrapper it replaced needed a
//    library built with -DCOOLPROP_CSHARP_MODULE=ON, which existed for one architecture only.

using System;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class CoolPropBindingTests
    {
        [Test]
        public void TheNativeLibraryLoadsAndReportsItsVersion()
        {
            var version = CoolProp.get_global_param_string("version");

            TestContext.WriteLine("CoolProp " + version);

            Assert.That(version, Is.Not.Empty);
        }

        /// <summary>
        /// Water at one atmosphere boils at 373.12 K, and its saturated liquid and vapour
        /// enthalpies differ by the latent heat, about 2257 kJ/kg. Values anyone can check.
        /// </summary>
        [Test]
        public void PropsSIAnswersForWater()
        {
            var tsat = CoolProp.PropsSI("T", "P", 101325.0, "Q", 0.0, "Water");

            var hl = CoolProp.PropsSI("H", "P", 101325.0, "Q", 0.0, "Water");
            var hv = CoolProp.PropsSI("H", "P", 101325.0, "Q", 1.0, "Water");

            TestContext.WriteLine("Tsat {0:F3} K, latent heat {1:F1} kJ/kg", tsat, (hv - hl) / 1000.0);

            Assert.That(tsat, Is.EqualTo(373.12).Within(0.1));
            Assert.That((hv - hl) / 1000.0, Is.EqualTo(2257.0).Within(5.0));
        }

        [Test]
        public void PropsSIAnswersForARefrigerant()
        {
            // R134a at 25 C: the saturation pressure is about 6.65 bar.
            var p = CoolProp.PropsSI("P", "T", 298.15, "Q", 0.0, "R134a");

            TestContext.WriteLine("R134a Psat at 25 C: {0:F0} Pa", p);

            Assert.That(p / 1e5, Is.EqualTo(6.65).Within(0.1));
        }

        /// <summary>
        /// The callers are written around the exception the SWIG wrapper threw. The flat API
        /// reports a failure by returning infinity and recording the reason, so the binding has to
        /// turn it back into one: an infinity read as a property value is a wrong answer that
        /// nothing catches.
        /// </summary>
        [Test]
        public void AnUnknownFluidRaisesInsteadOfReturningInfinity()
        {
            Assert.That(() => CoolProp.PropsSI("T", "P", 101325.0, "Q", 0.0, "NotAFluid"),
                        Throws.TypeOf<CoolPropException>());
        }

        [Test]
        public void AnImpossibleStateRaisesInsteadOfReturningInfinity()
        {
            // Below the triple point there is no vapour-liquid equilibrium.
            Assert.That(() => CoolProp.PropsSI("P", "T", 100.0, "Q", 0.0, "Water"),
                        Throws.TypeOf<CoolPropException>());
        }

        [Test]
        public void AFluidParameterComesBack()
        {
            var cas = CoolProp.get_fluid_param_string("Water", "CAS");

            TestContext.WriteLine("Water CAS " + cas);

            Assert.That(cas, Is.EqualTo("7732-18-5"));
        }

        /// <summary>
        /// The CoolProp property package itself, driven through a material stream the way a
        /// flowsheet does: a pressure-temperature flash on water, checked against the same
        /// quantities read straight from the library.
        /// </summary>
        [Test]
        public void TheCoolPropPropertyPackageFlashesAStream()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();

            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Water");

            var pp = new DWSIM.Thermodynamics.PropertyPackages.CoolPropPropertyPackage { Flowsheet = fs };

            var obj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.PropertyPackage = pp;
            ms.AssignSelfToPP();
            ms.SetMassFlow(1.0);
            ms.SetPressure(101325.0);
            ms.SetTemperature(298.15);
            ms.SetOverallComposition(new[] { 1.0 });
            ms.SetFlashSpec("PT");
            ms.Calculate();

            var density = ms.Phases[1].Properties.density ?? 0.0;
            var expected = CoolProp.PropsSI("D", "T", 298.15, "P", 101325.0, "Water");

            TestContext.WriteLine("liquid water at 25 C: package {0:F4} kg/m3, library {1:F4}",
                                  density, expected);

            Assert.That(density, Is.EqualTo(expected).Within(0.5).Percent);
            Assert.That(density, Is.EqualTo(997.0).Within(2.0));
        }

        /// <summary>
        /// The incompressible mixture package took the saturation temperature of the solvent by
        /// asking CoolProp for "V", the viscosity. Saturated water vapour is around 1.2e-5 Pa.s, so
        /// the test that follows it - is this temperature above the boiling point - was true at
        /// every condition, and the vapour viscosity of water below its boiling point came back as
        /// the viscosity of liquid water, some thirty times larger.
        /// </summary>
        [Test]
        public void TheSolventVapourViscosityIsNotTheLiquidOne()
        {
            var pp = new DWSIM.Thermodynamics.PropertyPackages.CoolPropIncompressibleMixturePropertyPackage();

            // 350 K at one atmosphere: below the boiling point, so the vapour is the extrapolated
            // side and CoolProp asked plainly would answer for the liquid.
            var vapour = pp.AUX_VAPVISCMIX(350.0, 101325.0, 0.018);
            var liquid = CoolProp.PropsSI("V", "T", 350.0, "P", 101325.0, "Water");

            TestContext.WriteLine("water at 350 K, 1 atm: vapour {0:E3} Pa.s, liquid {1:E3} Pa.s",
                                  vapour, liquid);

            Assert.That(liquid, Is.GreaterThan(3.0E-04));
            Assert.That(vapour, Is.EqualTo(1.15E-05).Within(30.0).Percent);
        }

        /// <summary>
        /// This one value decides the phase split of the package's pressure-temperature flash. A
        /// temperature outside the solution's own range used to take the flash down with it.
        /// </summary>
        [Test]
        public void AVapourPressureOutsideTheSolutionRangeStillComesBack()
        {
            var pp = new DWSIM.Thermodynamics.PropertyPackages.CoolPropIncompressibleMixturePropertyPackage();

            // The LiBr correlations carry no saturation pressure below 273 K, nor at 273 K itself,
            // and stop at 500 K. Below the floor the answer is the one at the floor, 140 Pa.
            var cold = pp.AUX_PVAPi2(0.5, 200.0);
            var hot = pp.AUX_PVAPi2(0.5, 600.0);
            var inside = pp.AUX_PVAPi2(0.5, 350.0);

            TestContext.WriteLine("LiBr 50%: 200 K {0:F1} Pa, 350 K {1:F1} Pa, 600 K {2:F1} Pa",
                                  cold, inside, hot);

            Assert.That(inside, Is.EqualTo(12585.8).Within(1.0));
            Assert.That(cold, Is.EqualTo(140.3).Within(1.0));
            Assert.That(hot, Is.EqualTo(1152979.0).Within(100.0));
        }
    }
}
