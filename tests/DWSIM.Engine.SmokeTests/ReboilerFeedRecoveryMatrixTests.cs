using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.GlobalSettings;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class ReboilerFeedRecoveryMatrixTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
        }

        // The requested recovery is chosen independently of the 50 mol/s seed.
        [TestCase(2.5, 40.0, true, 0.0, 1.0)]
        [TestCase(2.5, 50.0, true, 0.0, 1.0, Category="ParentComparison")]
        [TestCase(2.5, 60.0, true, 0.0, 1.0)]
        [TestCase(3.0, 40.0, true, 0.0, 1.0)]
        [TestCase(3.0, 50.0, true, 0.0, 1.0, Category="ParentComparison")]
        [TestCase(3.0, 60.0, true, 0.0, 1.0)]
        [TestCase(2.5, 50.0, true, -5.0, 1.0)]
        [TestCase(2.5, 50.0, true, 5.0, 1.0)]
        [TestCase(2.5, 40.0, false, 0.0, 1.0)]
        [TestCase(2.5, 50.0, false, 0.0, 1.0, Category="ParentComparison")]
        [TestCase(2.5, 60.0, false, 0.0, 1.0)]
        [TestCase(3.0, 50.0, false, 0.0, 1.0, Category="ParentComparison")]
        [TestCase(2.5, 40.0, true, 0.0, 2.0)]
        [TestCase(2.5, 50.0, true, 0.0, 2.0, Category="ParentComparison")]
        [TestCase(2.5, 60.0, true, 0.0, 2.0)]
        [TestCase(2.5, 50.0, false, 0.0, 2.0, Category="ParentComparison")]
        public void RecoveryIsIndependentOfCondenserTarget(double reflux, double targetPercent,
            bool recovery, double temperaturePerturbation, double flowScale)
        {
            var fs = Flowsheet.Create("RecoveryMatrix")
                .WithCompounds("Benzene", "Toluene").WithPropertyPackage(PropertyPackages.PengRobinson);
            var feed = fs.AddMaterialStream("feed").At(368.15.Kelvin(), 150000.0.Pascal())
                .WithMolarFlow((100.0*flowScale).MolPerSecond()).SetCompoundMolarFlow("Benzene", 50.0*flowScale)
                .SetCompoundMolarFlow("Toluene", 50.0*flowScale).AsFlowSpec();
            var dist = fs.AddMaterialStream("distillate"); var bot = fs.AddMaterialStream("bottoms");
            var qc = fs.AddEnergyStream("cond duty"); var qr = fs.AddEnergyStream("reb duty");
            var col = fs.AddDistillationColumn("T-01").WithNumberOfStages(16).WithFeed(feed, 8)
                .WithDistillate(dist).WithBottoms(bot).WithCondenserDuty(qc).WithReboilerDuty(qr)
                .WithCondenserSpec("Reflux Ratio", reflux, "")
                .WithReboilerSpec("Product Molar Flow Rate", 50.0*flowScale, "mol/s")
                .WithTopPressure(120000.0.Pascal()).WithColumnPressureDrop(10000.0.Pascal()).Object;
            col.AutoUpdateInitialEstimates = true;
            Assert.That(fs.TrySolve(), Is.Empty, "public bubble-point seed");
            var r=col.Specs["R"];
            r.SType=recovery ? ColumnSpec.SpecType.Feed_Recovery : ColumnSpec.SpecType.Product_Molar_Flow_Rate;
            r.SpecUnit=recovery ? "%" : "mol/s";
            var feedFlow=feed.Object.Phases[0].Properties.molarflow.Value;
            r.SpecValue=recovery ? targetPercent : targetPercent*feedFlow/100.0;
            if (temperaturePerturbation != 0.0)
            {
                col.UseTemperatureEstimates=true;
                foreach (var t in col.InitialEstimates.StageTemps) t.Value+=temperaturePerturbation;
            }
            col.SolvingMethodName="Naphtali-Sandholm";
            Assert.That(fs.TrySolve(), Is.Empty, "native recovery solve");
            var actual=100.0*bot.Object.Phases[0].Properties.molarflow.Value/feedFlow;
            Assert.That(actual, Is.EqualTo(targetPercent).Within(1e-6), "requested recovery percentage");
            Assert.That(col.Lf[0]/dist.Object.Phases[0].Properties.molarflow.Value,
                Is.EqualTo(reflux).Within(1e-6), "requested condenser ratio");
            foreach (var name in fs.Inner.SelectedCompounds.Keys)
                Assert.That(dist.Object.Phases[0].Compounds[name].MolarFlow.Value
                    +bot.Object.Phases[0].Compounds[name].MolarFlow.Value,
                    Is.EqualTo(feed.Object.Phases[0].Compounds[name].MolarFlow.Value).Within(1e-5),name+" balance");
            double Energy(MaterialStream stream) => stream.Phases[0].Properties.massflow.Value
                *stream.Phases[0].Properties.enthalpy.Value;
            Assert.That(Energy(dist.Object)+Energy(bot.Object)-Energy(feed.Object)
                +qc.EnergyFlowKW-qr.EnergyFlowKW,Is.EqualTo(0.0).Within(1e-3),"reported energy kW");
        }
    }
}
