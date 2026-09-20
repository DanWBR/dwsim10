//    The M/b liquid density guard must not replace a trustworthy correlation density.
//
//    The guard added for issue #40 caps the correlation liquid density at M/b, with b the Peng-Robinson
//    covolume, and hands the calculation to the equation of state when the cap is exceeded. Real liquids
//    can be denser than the PR covolume allows: water at 80 C is 971.8 kg/m3 while M/b is 949.6 kg/m3.
//    The guard fired on every water stream, and for the Raoult's Law package (whose AUX_Z is 1) the
//    "equation of state" density is that of an ideal gas, 0.61 kg/m3.

using NUnit.Framework;
using DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class LiquidDensityGuardTests
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
            "SRK" => new SRKPropertyPackage(),
            "NRTL" => new NRTLPropertyPackage(),
            "Chao-Seader" => new ChaoSeaderPropertyPackage(),
            _ => new RaoultPropertyPackage(),
        };

        [TestCase("Raoult", 353.15, 100000.0, 971.8)]
        [TestCase("Peng-Robinson", 353.15, 100000.0, 971.8)]
        [TestCase("SRK", 353.15, 100000.0, 971.8)]
        [TestCase("NRTL", 353.15, 100000.0, 971.8)]
        [TestCase("Chao-Seader", 353.15, 100000.0, 971.8)]
        [TestCase("Raoult", 298.15, 130000.0, 997.1)]   // as reported: 10 kg/s came out as 10.58 m3/s
        public void LiquidWaterHasTheDensityOfLiquidWater(string package, double T, double P, double rhoIapws)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Water");

            var pp = Create(package);
            pp.Flowsheet = fs;

            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.PropertyPackage = pp;
            ms.AssignSelfToPP();
            ms.SetMassFlow(1.0);
            ms.SetTemperature(T);
            ms.SetPressure(P);
            ms.SetOverallComposition(new[] { 1.0 });
            ms.SetFlashSpec("PT");
            ms.Calculate();

            double rhoL = ms.Phases[3].Properties.density.GetValueOrDefault();
            double rhoMix = ms.Phases[0].Properties.density.GetValueOrDefault();
            TestContext.WriteLine("{0,-16} {1:F2} K {2:F0} Pa: liquid {3:F2} kg/m3, mixture {4:F2} kg/m3 (IAPWS {5:F1})", package, T, P, rhoL, rhoMix, rhoIapws);

            Assert.That(rhoL, Is.EqualTo(rhoIapws).Within(2.0).Percent, $"{package}: liquid density {rhoL:F2}");
            Assert.That(rhoMix, Is.EqualTo(rhoL).Within(0.1).Percent);
        }

        /// <summary>
        /// The case of issue #40: a CO2-rich natural gas at 51 bar flashed from 298 K down to 200 K
        /// with Peng-Robinson in the default (Rackett and experimental data) density mode. The
        /// liquid density reported near the mixture critical point has to stay below the physical
        /// bound the guard enforces, and the vapor and liquid densities can never cross.
        /// </summary>
        [TestCase(298.15)]
        [TestCase(260.15)]
        [TestCase(220.15)]
        [TestCase(200.15)]
        public void TheCO2RichGasOfIssue40StaysPhysical(double T)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Methane");
            fs.AddCompound("Carbon dioxide");
            fs.AddCompound("Ethane");
            fs.AddCompound("Propane");

            var pp = new PengRobinsonPropertyPackage();
            pp.Flowsheet = fs;

            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.PropertyPackage = pp;
            ms.AssignSelfToPP();
            ms.SetMassFlow(1.0);
            ms.SetTemperature(T);
            ms.SetPressure(5107000.0);
            ms.SetOverallComposition(new[] { 0.72, 0.22, 0.04, 0.02 });
            ms.SetFlashSpec("PT");
            ms.Calculate();

            double xl = ms.Phases[3].Properties.molarfraction.GetValueOrDefault();
            double rhoL = ms.Phases[3].Properties.density.GetValueOrDefault();
            double rhoV = ms.Phases[2].Properties.density.GetValueOrDefault();
            TestContext.WriteLine("T = {0:F2} K: liquid fraction {1:F3}, liquid {2:F1} kg/m3, vapor {3:F1} kg/m3", T, xl, rhoL, rhoV);

            if (xl > 0.0)
            {
                pp.CurrentMaterialStream = ms;
                double vc = 0.0, mm = 0.0; int i = 0;
                var vvc = pp.RET_VVC();
                foreach (var c in ms.Phases[3].Compounds.Values)
                {
                    vc += c.MoleFraction.GetValueOrDefault() * vvc[i++];
                    mm += c.MoleFraction.GetValueOrDefault() * c.ConstantProperties.Molar_Weight;
                }
                Assert.That(rhoL, Is.LessThan(mm / (0.2 * vc)), $"liquid density {rhoL:F1} above the bound at {T:F2} K");
                if (ms.Phases[2].Properties.molarfraction.GetValueOrDefault() > 0.0)
                    Assert.That(rhoL, Is.GreaterThan(rhoV), $"liquid {rhoL:F1} lighter than vapor {rhoV:F1} at {T:F2} K");
            }
        }
    }
}
