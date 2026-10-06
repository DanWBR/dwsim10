using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces.Enums;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class BiodieselOutletPHFlashTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        // The aqueous outlet of the sample's extractor. Near its vaporization front,
        // a small temperature interval still spans more than the specified enthalpy tolerance.
        // No column solve is needed to reproduce the PH flash's premature bracket exit.
        [TestCase(419.92439723830364)]
        [TestCase(373.8384910602255)]
        [TestCase(374.8384910602255)]
        [TestCase(372.8384910602255)]
        public void ABracketedPHFlashMeetsItsEnthalpySpecification(double startingTemperature)
        {
            var folder = TestContext.CurrentContext.TestDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "could not find tests/flowsheets");
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.LoadZippedXML(Path.Combine(folder, "tests", "flowsheets", "BiodieselProduction.dwxmz"));
            var ms = fs.SimulationObjects.Values.OfType<MaterialStream>()
                .Single(s => s.GraphicObject.Tag == "Water + Impurities");
            var pp = (PropertyPackage)ms.PropertyPackage;
            pp.CurrentMaterialStream = ms;
            var composition = new Dictionary<string, double> {
                ["Ethanol_BD"] = 5.7994069587731994e-5,
                ["Water_BD"] = 0.9770655287692047,
                ["Glycerol_BD"] = 0.017451843348165644,
                ["EtP"] = 0.0011445995520457898,
                ["NaOH_BD"] = 0.00370822031222029,
                ["PPP"] = 0.0005718139487758016
            };
            ms.SetOverallComposition(pp.RET_VNAMES().Select(n => composition[n]).ToArray());
            ms.SetMassFlow(0.30455302323136935);
            ms.SetPressure(101325.0);
            ms.SetTemperature(startingTemperature);
            const double targetH = -1849.3398745106788;
            ms.SetMassEnthalpy(targetH);
            ms.SpecType = StreamSpec.Pressure_and_Enthalpy;
            var tolerance = double.Parse(pp.FlashBase.FlashSettings[FlashSetting.PHFlash_External_Loop_Tolerance],
                CultureInfo.InvariantCulture);
            ms.Calculate();
            var residual = ms.GetMassEnthalpy() - targetH;
            TestContext.WriteLine($"Tseed={startingTemperature:R}, T={ms.GetTemperature():R}, Herror={residual:R}, tolerance={tolerance:R}");
            Assert.That(Math.Abs(residual), Is.LessThanOrEqualTo(tolerance),
                "a narrow temperature bracket does not establish enthalpy convergence");
        }
    }
}
