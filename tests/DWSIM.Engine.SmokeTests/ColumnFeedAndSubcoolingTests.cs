using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.GlobalSettings;
using DWSIM.Thermodynamics.Streams;
using DistillationColumn = DWSIM.UnitOperations.UnitOperations.DistillationColumn;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class ColumnFeedAndSubcoolingTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
        }

        private const string WangHenke = "Wang-Henke (Bubble Point)";
        private const string NaphtaliSandholm = "Naphtali-Sandholm (Simultaneous Correction)";

        // benzene and toluene in 16 stages, reflux 2 and 50 mol/s of bottoms; optionally a second feed on the feed stage
        private static (Flowsheet fs, DistillationColumn column) Build(string method, double subcooling, bool secondFeed)
        {
            var fs = Flowsheet.Create("ColumnFeedAndSubcooling")
                .WithCompounds("Benzene", "Toluene")
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            var feed = fs.AddMaterialStream("feed").At(368.15.Kelvin(), 150000.0.Pascal())
                .WithMolarFlow(100.0.MolPerSecond()).SetCompoundMolarFlow("Benzene", 50.0).SetCompoundMolarFlow("Toluene", 50.0);
            var column = fs.AddDistillationColumn("T-01").WithNumberOfStages(16).WithFeed(feed, 8)
                .WithDistillate(fs.AddMaterialStream("distillate")).WithBottoms(fs.AddMaterialStream("bottoms"))
                .WithCondenserDuty(fs.AddEnergyStream("cond duty")).WithReboilerDuty(fs.AddEnergyStream("reb duty"))
                .WithCondenserSpec("Reflux Ratio", 2.0, "").WithReboilerSpec("Product Molar Flow Rate", 50.0, "mol/s")
                .WithTopPressure(120000.0.Pascal()).WithColumnPressureDrop(10000.0.Pascal());
            if (secondFeed)
                column.WithFeed(fs.AddMaterialStream("feed 2").At(350.0.Kelvin(), 150000.0.Pascal())
                    .WithMolarFlow(20.0.MolPerSecond()).SetCompoundMolarFlow("Benzene", 15.0).SetCompoundMolarFlow("Toluene", 5.0), 8);
            column.Object.TotalCondenserSubcoolingDeltaT = subcooling;
            column.Object.SolvingMethodName = method;
            Assert.That(fs.TrySolve(), Is.Empty, method);
            return (fs, column.Object);
        }

        private static MaterialStream Stream(Flowsheet fs, string tag) => (MaterialStream)fs.Inner.GetFlowsheetSimulationObject(tag);

        // a total condenser subcooled 10 K: the distillate leaves 10 K below its bubble point, with the duty that takes,
        // in both solvers
        [TestCase(WangHenke)]
        [TestCase(NaphtaliSandholm)]
        public void ASubcooledDistillateLeavesBelowItsBubblePoint(string method)
        {
            var (fs0, _) = Build(method, 0.0, false);
            double q0 = ((DWSIM.UnitOperations.Streams.EnergyStream)fs0.Inner.GetFlowsheetSimulationObject("cond duty")).EnergyFlow.GetValueOrDefault();

            var (fs, column) = Build(method, 10.0, false);
            var d = Stream(fs, "distillate");
            var pp = (DWSIM.Thermodynamics.PropertyPackages.PropertyPackage)column.PropertyPackage;
            pp.CurrentMaterialStream = d;
            var x = d.Phases[0].Compounds.Values.Select(c => c.MoleFraction.GetValueOrDefault()).ToArray();
            double bubble = (double)((object[])pp.DW_CalcBubT(x, d.GetPressure(), d.GetTemperature(), null, false))[4];
            double q = ((DWSIM.UnitOperations.Streams.EnergyStream)fs.Inner.GetFlowsheetSimulationObject("cond duty")).EnergyFlow.GetValueOrDefault();
            TestContext.Out.WriteLine($"{method}: distillate {d.GetTemperature():F3} K, bubble point {bubble:F3} K, duty {q:F1} kW (saturated {q0:F1} kW)");
            Assert.That(bubble - d.GetTemperature(), Is.EqualTo(10.0).Within(0.05));
            Assert.That(q, Is.GreaterThan(q0), "a subcooled condenser removes more heat");
        }

        // two feeds on one stage both reach the column: the products carry 120 mol/s and the bottoms specification is met
        [TestCase(WangHenke)]
        [TestCase(NaphtaliSandholm)]
        public void TwoFeedsOnOneStageBothReachTheColumn(string method)
        {
            var (fs, _) = Build(method, 0.0, true);
            double d = Stream(fs, "distillate").GetMolarFlow(), b = Stream(fs, "bottoms").GetMolarFlow();
            double benzene = Stream(fs, "distillate").Phases[0].Compounds["Benzene"].MolarFlow.GetValueOrDefault()
                + Stream(fs, "bottoms").Phases[0].Compounds["Benzene"].MolarFlow.GetValueOrDefault();
            TestContext.Out.WriteLine($"{method}: distillate {d:F4}, bottoms {b:F4}, benzene out {benzene:F4} mol/s");
            Assert.That(d + b, Is.EqualTo(120.0).Within(1e-3));
            Assert.That(b, Is.EqualTo(50.0).Within(1e-3));
            Assert.That(benzene, Is.EqualTo(65.0).Within(1e-3));
        }
    }
}
