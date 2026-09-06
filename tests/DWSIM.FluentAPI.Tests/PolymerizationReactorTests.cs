using NUnit.Framework;
using DWSIM.Automation.FluentAPI;
using DWSIM.Thermodynamics.BaseClasses;
using OT = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// Phase 0 of the polymerization reactor: the free-radical CSTR as a placed, connected unit operation on a
    /// flowsheet. Ethylbenzene stands in for the styrene monomer (styrene has no shipped PC-SAFT parameters),
    /// n-pentane for a soluble initiator, and polystyrene is the product. The test drops the reactor, wires its
    /// feed / product / energy streams, solves the flowsheet, and reads conversion, Mn, Mw and PDI back off the
    /// unit operation, confirming the product stream carries the polymer.
    /// </summary>
    [TestFixture]
    public class PolymerizationReactorTests
    {
        [Test]
        public void PolymerizationReactorSolvesOnAFlowsheet()
        {
            var poly = new ConstantProperties
            {
                Name = "Polystyrene", CAS_Number = "9003-53-6", Formula = "(C8H8)n", Molar_Weight = 100000.0,
                Critical_Temperature = 1200.0, Critical_Pressure = 5.0e5, Acentric_Factor = 0.5,
                Normal_Boiling_Point = 800.0, IsHYPO = 1, CurrentDB = "User", OriginalDB = "User"
            };

            var fs = Flowsheet.Create("PolyReactor")
                .WithCompounds("Ethylbenzene", "N-pentane")
                .WithCompound(poly)
                .WithPropertyPackage(PropertyPackages.PCSAFT);

            var feed = fs.AddMaterialStream("feed")
                .At(333.15.Kelvin(), 5.0e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Ethylbenzene", 0.98)
                .SetCompoundMolarFlow("N-pentane", 0.02)
                .SetCompoundMolarFlow("Polystyrene", 0.0);

            var product = fs.AddMaterialStream("product");

            var inner = fs.Inner;
            var robj = inner.AddObject(OT.RCT_Polymerization, 100, 100, "R-1");
            var eobj = inner.AddObject(OT.EnergyStream, 40, 180, "Q-1");
            var reactor = (DWSIM.UnitOperations.Reactors.Reactor_Polymerization)robj;
            reactor.MonomerID = "Ethylbenzene";
            reactor.InitiatorID = "N-pentane";
            reactor.PolymerID = "Polystyrene";
            reactor.IsothermalTemperature = 333.15;
            reactor.Volume = 3.0; // m3

            inner.ConnectObjects(feed.Object.GraphicObject, robj.GraphicObject, 0, 0);
            inner.ConnectObjects(robj.GraphicObject, product.Object.GraphicObject, 0, 0);
            inner.ConnectObjects(eobj.GraphicObject, robj.GraphicObject, 0, 1);

            fs.Solve();

            TestContext.WriteLine($"reactor UO: theta={reactor.ResidenceTime:F0}s X={reactor.Conversion:F3} " +
                                  $"Mn={reactor.Mn:F0} Mw={reactor.Mw:F0} PDI={reactor.PDI:F3}");

            Assert.Multiple(() =>
            {
                Assert.That(reactor.Conversion, Is.GreaterThan(0.0).And.LessThan(1.0), "the reactor must convert some monomer");
                Assert.That(reactor.Mn, Is.GreaterThan(1.0e4), "a polymer of substantial molar mass must form");
                Assert.That(reactor.PDI, Is.InRange(1.4, 2.1), "polydispersity near the free-radical range");
                double xPolyProduct = product.Object.Phases[0].Compounds["Polystyrene"].MoleFraction.GetValueOrDefault();
                Assert.That(xPolyProduct, Is.GreaterThan(0.0), "the product stream must contain polymer");
            });
        }
    }
}
