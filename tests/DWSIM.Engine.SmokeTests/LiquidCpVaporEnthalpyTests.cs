//    Vapor enthalpy in the "Experimental Liquid Cp" mode below the normal boiling point.
//
//    The vapor enthalpy in that mode follows the path liquid at 25 C -> liquid at T -> vapor at T.
//    Below the normal boiling point the ideal-gas leg from 25 C up to T was added on top of that
//    path, so the sensible heat was counted twice and the latent heat of water at 62 C came out
//    2424 kJ/kg against 2353 kJ/kg from the steam tables (an evaporator duty 3 % too high).

using NUnit.Framework;
using DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class LiquidCpVaporEnthalpyTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            DWSIM.GlobalSettings.Settings.AutomationMode = true;
            DWSIM.GlobalSettings.Settings.InspectorEnabled = false;
            DWSIM.GlobalSettings.Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
        }

        [TestCase(335.15, 2353.0)]   // 62 C, IAPWS-97
        [TestCase(313.15, 2406.0)]   // 40 C
        [TestCase(363.15, 2282.5)]   // 90 C
        public void TheLatentHeatOfWaterBelowTheBoilingPointMatchesTheSteamTables(double T, double hfgSteamTables)
        {
            var fs = new DWSIM.DynamicRunner.Flowsheet(null, null);
            fs.Init();
            fs.AddCompound("Water");

            var pp = new PengRobinsonPropertyPackage();
            pp.Flowsheet = fs;
            pp.LiquidEnthalpyEntropyCpCvCalculationMode_EOS = PropertyPackage.LiquidEnthalpyEntropyCpCvCalcMode_EOS.ExpData;

            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, "s");
            var ms = (DWSIM.Thermodynamics.Streams.MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs);
            ms.PropertyPackage = pp;
            ms.AssignSelfToPP();
            ms.SetOverallComposition(new[] { 1.0 });
            pp.CurrentMaterialStream = ms;

            double P = pp.AUX_PVAPM(T, new[] { 1.0 });
            double hl = pp.DW_CalcEnthalpy(new[] { 1.0 }, T, P, State.Liquid);
            double hv = pp.DW_CalcEnthalpy(new[] { 1.0 }, T, P, State.Vapor);
            double hfg = hv - hl;

            TestContext.WriteLine("T = {0:F2} K, Psat = {1:F0} Pa, hL = {2:F1}, hV = {3:F1}, hfg = {4:F1} kJ/kg (steam tables {5:F1})",
                                  T, P, hl, hv, hfg, hfgSteamTables);

            Assert.That(hfg, Is.EqualTo(hfgSteamTables).Within(0.5).Percent);
        }
    }
}
