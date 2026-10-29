using DWSIM.Automation.FluentAPI;
using DWSIM.GlobalSettings;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.Pipe;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class PipeSolarRadiationTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
        }

        // the sun on 300 m of a 4.5 in line, with next to no exchange with the surroundings (U = 1e-9, colder air so the
        // heat is not capped at the air's temperature): the heat it takes in is the
        // irradiance (kW/m2) times the absorption efficiency on the projected area, however finely the line is cut
        [TestCase(5)]
        [TestCase(50)]
        public void TheSunsHeatIsTheIrradianceOnTheProjectedArea(int increments)
        {
            var fs = Flowsheet.Create("PipeSolarRadiation")
                .WithCompounds("Methane", "Ethane", "Propane")
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            var feed = fs.AddMaterialStream("feed").At(280.0.Kelvin(), 30e5.Pascal()).WithMassFlow(1.0.KgPerSecond())
                .WithComposition(cb => cb.Mass("Methane", 0.6).Mass("Ethane", 0.3).Mass("Propane", 0.1));
            var outlet = fs.AddMaterialStream("out");
            var pipe = fs.AddPipe("LINE").ConnectFeed(feed, 0).ConnectProduct(outlet, 0);
            var prof = pipe.HydraulicProfile;
            prof.Sections.Clear();
            prof.Sections.Add(1, new PipeSection
            { Indice = 1, TipoSegmento = "Straight Tube", Quantidade = 1, Incrementos = increments, Material = "CarbonSteel", Comprimento = 300.0, Elevacao = 0.0, DE = 4.5, DI = 4.026 });
            prof.Status = PipeEditorStatus.OK;
            var th = pipe.ThermalProfile;
            th.TipoPerfil = ThermalEditorDefinitions.ThermalProfileType.Definir_CGTC;
            th.CGTC_Definido = 1e-9;
            th.Temp_amb_definir = 200.0;
            th.IncludeSolarRadiation = true;
            th.SolarRadiationValue_kWh_m2 = 0.8;
            th.SolarRadiationAbsorptionEfficiency = 0.5;
            Assert.That(fs.TrySolve(), Is.Empty);
            double expected = 0.5 * 0.8 * 4.5 * 0.0254 * 300.0;
            double q = pipe.Object.DeltaQ.GetValueOrDefault();
            TestContext.Out.WriteLine($"{increments} increments: {q:F4} kW, expected {expected:F4} kW");
            Assert.That(q, Is.EqualTo(expected).Within(1e-3 * expected));
        }
    }
}
