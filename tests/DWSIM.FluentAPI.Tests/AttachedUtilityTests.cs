using System;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Automation.FluentAPI.Builders;
using NUnit.Framework;
using ColdFlow = DWSIM.Thermodynamics.Utilities.PetroleumProperties.ColdFlowProperties;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The petroleum cold flow utility and the depressurization utility as the cross-platform interface
    /// keeps them: the cold flow calculation leaves its stream alone and reports its 10 % temperatures and
    /// dynamic viscosities, and a depressurization study attached to a stream survives a save.
    /// </summary>
    [TestFixture]
    public class AttachedUtilityTests
    {
        private static Flowsheet Kerosene(out MaterialStreamBuilder stream)
        {
            var fs = Flowsheet.Create("Kerosene")
                .WithPseudoComponent("NBP 180C", (180.0 + 273.15).Kelvin(), 0.79, 150.0)
                .WithPseudoComponent("NBP 220C", (220.0 + 273.15).Kelvin(), 0.81, 175.0)
                .WithPseudoComponent("NBP 260C", (260.0 + 273.15).Kelvin(), 0.83, 205.0)
                .WithPropertyPackage(PropertyPackages.PengRobinson);
            stream = fs.AddMaterialStream("kerosene")
                .At(320.Kelvin(), 101325.0.Pascal())
                .WithMassFlow(1.KgPerSecond())
                .WithComposition(c => { c.Mass("NBP 180C", 0.3); c.Mass("NBP 220C", 0.4); c.Mass("NBP 260C", 0.3); });
            fs.Solve();
            return fs;
        }

        [Test]
        public void TheColdFlowCalculationLeavesTheStreamAsItWas()
        {
            var fs = Kerosene(out var s);
            var ms = s.Object;
            double t = ms.Phases[0].Properties.temperature.GetValueOrDefault();
            var liquid = ms.Phases[1].Compounds.Values.Select(c => c.MoleFraction.GetValueOrDefault()).ToArray();

            var res = ColdFlow.Calculate(ms);

            Assert.That(ms.Phases[0].Properties.temperature.GetValueOrDefault(), Is.EqualTo(t));
            Assert.That(ms.Phases[1].Compounds.Values.Select(c => c.MoleFraction.GetValueOrDefault()).ToArray(), Is.EqualTo(liquid));
            Assert.That(res.T10TBP - 273.15, Is.InRange(150.0, 260.0));
            Assert.That(res.T10ASTMD86 - 273.15, Is.InRange(150.0, 260.0));
            // the flash point follows the D86 10 % point (API 2B7.1)
            Assert.That(res.FlashPoint, Is.EqualTo(((0.69 * ((res.T10ASTMD86 - 273.15) * 9 / 5 + 32) - 118.2) - 32) * 5 / 9 + 273.15).Within(1e-9));
        }

        [Test]
        public void TheColdFlowUtilityReportsItsTenPercentPointsAndDynamicViscosities()
        {
            var fs = Kerosene(out var s);
            var su = fs.Inner.FlowsheetOptions.SelectedUnitSystem;
            var util = (DWSIM.Interfaces.IAttachedUtility)fs.Inner.GetUtility(DWSIM.Interfaces.Enums.FlowsheetUtility.PetroleumProperties);
            util.AttachedTo = s.Object;
            util.Update();

            Assert.That(util.GetPropertyUnits("Viscosity @ 100 F"), Is.EqualTo(su.viscosity));
            Assert.That(util.GetPropertyList(), Does.Contain("T 10% ASTM D86"));
            Assert.That(Convert.ToDouble(util.GetPropertyValue("T 10% ASTM D86")), Is.GreaterThan(0.0));
            Assert.That(Convert.ToDouble(util.GetPropertyValue("T 10% TBP")), Is.GreaterThan(0.0));
        }

        [Test]
        public void ADepressurizationStudyAttachedToAStreamSurvivesASave()
        {
            var fs = Kerosene(out var s);
            var util = fs.Inner.GetUtility(DWSIM.Interfaces.Enums.FlowsheetUtility.Depressurization);
            Assert.That(util, Is.Not.Null, "the engine has no depressurization utility");
            util.ID = 7;
            util.Name = "Depressurization1";
            util.AttachedTo = s.Object;
            util.SetPropertyValue("Diameter", "2.5");
            util.SetPropertyValue("Mode", 1);
            util.SetPropertyValue("Homogeneous", true);
            s.Object.AttachedUtilities.Add(util);

            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dwsim-depress-" + Guid.NewGuid().ToString("N") + ".dwxmz");
            try
            {
                fs.Save(path);
                var back = Flowsheet.Load(path);
                var ms = back.Inner.SimulationObjects.Values.First(o => o.GraphicObject.Tag == "kerosene");
                var u2 = ms.AttachedUtilities.Single(u => u.GetUtilityType() == DWSIM.Interfaces.Enums.FlowsheetUtility.Depressurization);
                Assert.That(u2.Name, Is.EqualTo("Depressurization1"));
                Assert.That(Convert.ToString(u2.GetPropertyValue("Diameter")), Is.EqualTo("2.5"));
                Assert.That(Convert.ToInt32(u2.GetPropertyValue("Mode")), Is.EqualTo(1));
                Assert.That(Convert.ToBoolean(u2.GetPropertyValue("Homogeneous")), Is.True);
            }
            finally
            {
                try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch (Exception) { }
            }
        }
    }
}
