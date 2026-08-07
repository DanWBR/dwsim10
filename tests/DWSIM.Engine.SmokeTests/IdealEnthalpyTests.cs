//    Every property package has to carry the ideal-gas part of the enthalpy.
//
//    This is https://github.com/DanWBR/dwsim/issues/1114. Five packages filled a stream's enthalpy
//    and entropy from the departure term alone, because they asked for the ideal part through an
//    overload that reads the phase composition off the stream at a point in the calculation where
//    it is not populated yet, and got zero. The result is a heat capacity of nearly zero: on the
//    reporter's flowsheet a mixer of two streams both at 50 C produced an outlet at 24.8 C, and a
//    valve with a 2 bar pressure drop cooled the gas by 26 K.

using System;
using System.Linq;
using NUnit.Framework;
using DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class IdealEnthalpyTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";

            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        private static PropertyPackage Create(string name) => name switch
        {
            "Peng-Robinson" => new PengRobinsonPropertyPackage(),
            "Peng-Robinson 1978" => new PengRobinson1978PropertyPackage(),
            "SRK" => new SRKPropertyPackage(),
            "PRSV2" => new PRSV2PropertyPackage(),
            "PRSV2-VL" => new PRSV2VLPropertyPackage(),
            "Lee-Kesler-Plocker" => new LKPPropertyPackage(),
            "Peng-Robinson / Lee-Kesler" => new PengRobinsonLKPropertyPackage(),
            "Chao-Seader" => new ChaoSeaderPropertyPackage(),
            "Grayson-Streed" => new GraysonStreedPropertyPackage(),
            _ => new RaoultPropertyPackage(),
        };

        /// <summary>
        /// The heat capacity a stream implies, read off its own enthalpy, against the ideal-gas
        /// heat capacity of the compound. Nitrogen at 9 bar is far enough from its critical point
        /// that the departure term is small, so the two have to agree closely; what is being
        /// caught is a package that reports the departure alone, which gives nearly zero.
        /// </summary>
        [TestCase("Peng-Robinson")]
        [TestCase("Peng-Robinson 1978")]
        [TestCase("SRK")]
        [TestCase("PRSV2")]
        [TestCase("PRSV2-VL")]
        [TestCase("Lee-Kesler-Plocker")]
        [TestCase("Peng-Robinson / Lee-Kesler")]
        [TestCase("Chao-Seader")]
        [TestCase("Grayson-Streed")]
        [TestCase("Raoult")]
        public void AStreamCarriesTheIdealGasEnthalpy(string package)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Nitrogen");

            var pp = Create(package);
            pp.Flowsheet = fs;

            var obj = fs.AddObject(
                DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.PropertyPackage = pp;
            ms.AssignSelfToPP();
            ms.SetMassFlow(1.0);
            ms.SetPressure(901325.0);
            ms.SetOverallComposition(new[] { 1.0 });

            double H(double t)
            {
                ms.SetTemperature(t);
                ms.SetFlashSpec("PT");
                ms.Calculate();
                return ms.GetMassEnthalpy();
            }

            var cp = (H(323.15) - H(293.15)) / 30.0;

            // The ideal-gas value the compound database gives over the same interval.
            pp.CurrentMaterialStream = ms;
            var ideal = pp.RET_Hid(293.15, 323.15, new[] { 1.0 }) / 30.0;

            TestContext.WriteLine("{0,-28} cp from enthalpy {1:F4}, ideal gas {2:F4} kJ/kg.K",
                                  package, cp, ideal);

            Assert.That(cp, Is.EqualTo(ideal).Within(3.0).Percent,
                        $"{package} reports a heat capacity of {cp:F4} against an ideal-gas {ideal:F4}");
        }
    }
}
