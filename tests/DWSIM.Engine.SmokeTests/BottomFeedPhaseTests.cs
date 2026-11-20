using System;
using System.Linq;
using System.Reflection;
using DWSIM.GlobalSettings;
using DWSIM.Interfaces.Enums;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;
using NUnit.Framework;

namespace DWSIM.Engine.SmokeTests
{
    [TestFixture]
    public class BottomFeedPhaseTests
    {
        private DWSIM.DynamicRunner.Flowsheet fs;
        private PengRobinsonPropertyPackage pp;
        [SetUp]
        public void Setup()
        {
            Settings.AutomationMode = true; Settings.InspectorEnabled = false; Settings.CultureInfo = "en";
            FlowsheetBase.FlowsheetBase.AddPropPacks();
            fs = new DWSIM.DynamicRunner.Flowsheet(null, null); fs.Init();
            fs.AddCompound("Propane"); fs.AddCompound("N-butane");
            pp = new PengRobinsonPropertyPackage { Flowsheet = fs }; fs.AddPropertyPackage(pp);
        }
        private MaterialStream Stream(double[] z, double t, double p)
        {
            var obj = fs.AddObject(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream, 0, 0, Guid.NewGuid().ToString());
            var ms = (MaterialStream)fs.SimulationObjects[obj.Name];
            ms.SetFlowsheet(fs); ms.SetPropertyPackage(pp); ms.SetOverallComposition(z);
            ms.SetTemperature(t); ms.SetPressure(p); ms.SetMassFlow(1.0); ms.Calculate(); pp.CurrentMaterialStream = ms; return ms;
        }
        private double[] Boundary(double[] z, double hMolar, double pressure, double t)
        {
            var type = typeof(DWSIM.UnitOperations.UnitOperations.AbsorptionColumn).Assembly.GetTypes()
                .Single(x => x.GetMethod("FlashBottomFeedVapour", BindingFlags.NonPublic | BindingFlags.Static) != null);
            var method = type.GetMethod("FlashBottomFeedVapour", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (double[])method.Invoke(null, new object[] { pp, z, hMolar, pressure, t });
        }
        private double[] Y(MaterialStream s) => new[] { s.Phases[2].Compounds["Propane"].MoleFraction.Value, s.Phases[2].Compounds["N-butane"].MoleFraction.Value };
        [TestCase(254.75, 246000.0, true)]
        [TestCase(330.0, 100000.0, false)]
        public void UsesVapourRatherThanOverallComposition(double t, double p, bool twoPhase)
        {
            var z = new[] { .95, .05 }; var s = Stream(z, t, p);
            var actual = Boundary(z, s.GetMassEnthalpy() * pp.AUX_MMM(z), p, t);
            Assert.That(actual, Is.EqualTo(Y(s)).Within(1e-6));
            if (twoPhase) Assert.That(Math.Abs(actual[0] - z[0]), Is.GreaterThan(1e-3));
        }
        [Test]
        public void LiquidOnlyFeedHasNoIncomingVapour()
        {
            var z = new[] { .95, .05 }; var s = Stream(z, 200.0, 246000.0);
            Assert.That(s.Phases[2].Properties.molarfraction.GetValueOrDefault(), Is.EqualTo(0.0));
            Assert.That(Boundary(z, s.GetMassEnthalpy() * pp.AUX_MMM(z), 246000.0, 200.0), Is.Null);
        }
        [Test]
        public void MixedFeedsUseMolarEnthalpyAtReceivingPressure()
        {
            var z1 = new[] { .95, .05 }; var z2 = new[] { .2, .8 };
            var a = Stream(z1, 254.75, 246000.0); var b = Stream(z2, 310.0, 300000.0);
            double[] z = z1.Zip(z2, (x,y) => (2*x+y)/3).ToArray();
            double hm = (2*a.GetMassEnthalpy()*pp.AUX_MMM(z1)+b.GetMassEnthalpy()*pp.AUX_MMM(z2))/3;
            var reference = Stream(z, 280.0, 100000.0);
            reference.SetMassEnthalpy(hm/pp.AUX_MMM(z)); reference.SpecType = StreamSpec.Pressure_and_Enthalpy; reference.Calculate(); pp.CurrentMaterialStream = reference;
            var actual = Boundary(z, hm, 100000.0, 280.0);
            Assert.That(actual, Is.EqualTo(Y(reference)).Within(1e-6));
            Assert.That(a.GetPressure(), Is.EqualTo(246000.0));
            Assert.That(b.GetPressure(), Is.EqualTo(300000.0));
        }
    }
}
