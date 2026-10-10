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
    public class ReboilerReportedComponentTests
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
        // Both targets use the same basis, isolating the reporting component selector.
        // The achieved reboiler value must refer to its requested component.
        [TestCase("Molar", "distinct")]
        [TestCase("M", "distinct")]
        [TestCase("Molar", "same")]
        [TestCase("Mass", "distinct")]
        public void TheReboilerReportsItsOwnComponentFraction(string basis, string componentMode)
        {
            var fs = Flowsheet.Create("ReboilerReportedComponent")
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
            string condenserBasis = basis, reboilerBasis = basis;
            string condenserCompound = componentMode == "same" ? "Toluene" : "Benzene";
            if (condenserBasis != "ratio")
            {
                var c = column.Specs["C"];
                c.SType = ColumnSpec.SpecType.Component_Fraction;
                c.SpecUnit = condenserBasis;
                c.ComponentID = condenserCompound;
                c.ComponentIndex = Array.IndexOf(names, condenserCompound);
                c.SpecValue = Fraction(dist.Object, condenserCompound, condenserBasis);
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
            Assert.That(r.CalculatedValue, Is.EqualTo(Fraction(bot.Object, "Toluene", reboilerBasis)).Within(1e-10),
                "reported reboiler component fraction");
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
