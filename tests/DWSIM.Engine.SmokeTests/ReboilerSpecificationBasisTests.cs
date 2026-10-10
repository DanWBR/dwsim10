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
    public class ReboilerSpecificationBasisTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            Settings.AutomationMode = true;
            Settings.InspectorEnabled = false;
            Settings.CultureInfo = "en";
        }

        private static double Fraction(MaterialStream stream, string compound, string basis)
        {
            var c = stream.Phases[0].Compounds[compound];
            return basis == "Mass"
                ? c.MassFlow.Value / stream.Phases[0].Properties.massflow.Value
                : c.MolarFlow.Value / stream.Phases[0].Properties.molarflow.Value;
        }

        // The physical inputs are the steady state from BenzeneTolueneDynamicColumn.Build.
        // A feasible bubble-point solution supplies both fraction targets. The simultaneous
        // solver must enforce the reboiler's own basis independently of the condenser's spec.
        [TestCase("ratio", "Molar")]
        [TestCase("Molar", "Mass")]
        [TestCase("Mass", "Molar")]
        [TestCase("ratio", "Mass")]
        [TestCase("Molar", "Molar")]
        [TestCase("Mass", "Mass")]
        public void TheReboilerUsesItsOwnComponentFractionBasis(string condenserBasis, string reboilerBasis)
        {
            var fs = Flowsheet.Create("ReboilerFractionBasis")
                .WithCompounds("Benzene", "Toluene")
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            var feed = fs.AddMaterialStream("feed").At(368.15.Kelvin(), 150000.0.Pascal())
                .WithMolarFlow(100.0.MolPerSecond()).SetCompoundMolarFlow("Benzene", 50.0)
                .SetCompoundMolarFlow("Toluene", 50.0).AsFlowSpec();
            var dist = fs.AddMaterialStream("distillate");
            var bot = fs.AddMaterialStream("bottoms");
            var qc = fs.AddEnergyStream("cond duty");
            var qr = fs.AddEnergyStream("reb duty");
            var column = fs.AddDistillationColumn("T-01").WithNumberOfStages(16).WithFeed(feed, 8)
                .WithDistillate(dist).WithBottoms(bot).WithCondenserDuty(qc).WithReboilerDuty(qr)
                .WithCondenserSpec("Reflux Ratio", 2.5, "")
                .WithReboilerSpec("Product Molar Flow Rate", 50.0, "mol/s")
                .WithTopPressure(120000.0.Pascal()).WithColumnPressureDrop(10000.0.Pascal()).Object;
            column.AutoUpdateInitialEstimates = true;
            Assert.That(fs.TrySolve(), Is.Empty, "feasible bubble-point seed");
            var names = fs.Inner.SelectedCompounds.Keys.ToArray();
            if (condenserBasis != "ratio")
            {
                var c = column.Specs["C"];
                c.SType = ColumnSpec.SpecType.Component_Fraction;
                c.SpecUnit = condenserBasis;
                c.ComponentID = "Benzene";
                c.ComponentIndex = Array.IndexOf(names, "Benzene");
                c.SpecValue = Fraction(dist.Object, "Benzene", condenserBasis);
            }
            var r = column.Specs["R"];
            r.SType = ColumnSpec.SpecType.Component_Fraction;
            r.SpecUnit = reboilerBasis;
            r.ComponentID = "Toluene";
            r.ComponentIndex = Array.IndexOf(names, "Toluene");
            r.SpecValue = Fraction(bot.Object, "Toluene", reboilerBasis);
            var target = r.SpecValue;
            column.SolvingMethodName = "Naphtali-Sandholm";
            Assert.That(fs.TrySolve(), Is.Empty);
            Assert.That(Fraction(bot.Object, "Toluene", reboilerBasis),
                Is.EqualTo(target).Within(1e-6), "requested reboiler fraction basis");
            foreach (var name in names)
            {
                var input = feed.Object.Phases[0].Compounds[name].MolarFlow.Value;
                var output = dist.Object.Phases[0].Compounds[name].MolarFlow.Value
                    + bot.Object.Phases[0].Compounds[name].MolarFlow.Value;
                Assert.That(output, Is.EqualTo(input).Within(1e-5), name + " balance");
            }
            double Energy(MaterialStream stream) => stream.Phases[0].Properties.massflow.Value
                * stream.Phases[0].Properties.enthalpy.Value;
            Assert.That(Energy(dist.Object) + Energy(bot.Object) - Energy(feed.Object)
                + qc.EnergyFlowKW - qr.EnergyFlowKW, Is.EqualTo(0.0).Within(1e-3), "reported energy, kW");
        }
    }
}
