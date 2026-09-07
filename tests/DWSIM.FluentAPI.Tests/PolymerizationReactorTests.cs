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

        [Test]
        public void ReactorEmitsAMolecularWeightDistribution()
        {
            var poly = new ConstantProperties
            {
                Name = "Polystyrene", CAS_Number = "9003-53-6", Formula = "(C8H8)n", Molar_Weight = 100000.0,
                Critical_Temperature = 1200.0, Critical_Pressure = 5.0e5, Acentric_Factor = 0.5,
                Normal_Boiling_Point = 800.0, IsHYPO = 1, CurrentDB = "User", OriginalDB = "User"
            };

            var fs = Flowsheet.Create("PolyDist")
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
            var reactor = (DWSIM.UnitOperations.Reactors.Reactor_Polymerization)robj;
            reactor.MonomerID = "Ethylbenzene";
            reactor.InitiatorID = "N-pentane";
            reactor.PolymerID = "Polystyrene";
            reactor.IsothermalTemperature = 333.15;
            reactor.Volume = 3.0;
            reactor.NumberOfCuts = 8;

            inner.ConnectObjects(feed.Object.GraphicObject, robj.GraphicObject, 0, 0);
            inner.ConnectObjects(robj.GraphicObject, product.Object.GraphicObject, 0, 0);

            fs.Solve();                               // 1) lumped solve gives Mn/PDI
            double Mn0 = reactor.Mn;
            reactor.GenerateDistributionCompounds();  // 2) create the cut compounds on the flowsheet
            reactor.EmitDistribution = true;          // 3) emit the distribution
            fs.Solve();

            TestContext.WriteLine($"dist: Mn={reactor.Mn:F0} cuts={reactor.CutCompoundNames.Count}");

            double num = 0.0, den = 0.0;
            foreach (var name in reactor.CutCompoundNames)
            {
                var comp = product.Object.Phases[0].Compounds[name];
                double x = comp.MoleFraction.GetValueOrDefault();
                double M = comp.ConstantProperties.Molar_Weight;
                num += x * M; den += x;
            }
            double MnCuts = den > 0.0 ? num / den : 0.0;
            TestContext.WriteLine($"dist: cut-weighted Mn={MnCuts:F0} sum(x_cut)={den:E2}");

            Assert.Multiple(() =>
            {
                Assert.That(reactor.CutCompoundNames.Count, Is.EqualTo(8), "the cuts must be generated");
                Assert.That(den, Is.GreaterThan(0.0), "the product must carry the polymer distribution");
                Assert.That(product.Object.Phases[0].Compounds["Polystyrene"].MoleFraction.GetValueOrDefault(),
                            Is.LessThan(1.0e-9), "the lumped polymer compound is not used when distributing");
                Assert.That(MnCuts, Is.EqualTo(reactor.Mn).Within(reactor.Mn * 0.15),
                            "the emitted distribution's number-average molar mass matches the reactor Mn");
            });
        }

        [Test]
        public void CopolymerReactorSolvesWithTwoMonomers()
        {
            // A second monomer switches the reactor to the terminal copolymerization model. Ethylbenzene (A)
            // and toluene (B) stand in for the styrene/MMA pair (no shipped PC-SAFT styrene/MMA), n-pentane is
            // the initiator, polystyrene the product. The copolymer composition must equal the Mayo-Lewis
            // prediction at the outlet monomer composition, and both monomers must be partly consumed.
            var poly = new ConstantProperties
            {
                Name = "Polystyrene", CAS_Number = "9003-53-6", Formula = "(C8H8)n", Molar_Weight = 100000.0,
                Critical_Temperature = 1200.0, Critical_Pressure = 5.0e5, Acentric_Factor = 0.5,
                Normal_Boiling_Point = 800.0, IsHYPO = 1, CurrentDB = "User", OriginalDB = "User"
            };

            var fs = Flowsheet.Create("CopolyReactor")
                .WithCompounds("Ethylbenzene", "Toluene", "N-pentane")
                .WithCompound(poly)
                .WithPropertyPackage(PropertyPackages.PCSAFT);

            var feed = fs.AddMaterialStream("feed")
                .At(333.15.Kelvin(), 5.0e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Ethylbenzene", 0.49)
                .SetCompoundMolarFlow("Toluene", 0.49)
                .SetCompoundMolarFlow("N-pentane", 0.02)
                .SetCompoundMolarFlow("Polystyrene", 0.0);
            var product = fs.AddMaterialStream("product");

            var inner = fs.Inner;
            var robj = inner.AddObject(OT.RCT_Polymerization, 100, 100, "R-1");
            var reactor = (DWSIM.UnitOperations.Reactors.Reactor_Polymerization)robj;
            reactor.MonomerID = "Ethylbenzene";
            reactor.MonomerBID = "Toluene";
            reactor.InitiatorID = "N-pentane";
            reactor.PolymerID = "Polystyrene";
            reactor.ReactivityRatioA = 0.52;
            reactor.ReactivityRatioB = 0.46;
            reactor.IsothermalTemperature = 333.15;
            reactor.Volume = 3.0;

            inner.ConnectObjects(feed.Object.GraphicObject, robj.GraphicObject, 0, 0);
            inner.ConnectObjects(robj.GraphicObject, product.Object.GraphicObject, 0, 0);

            fs.Solve();

            double xA = product.Object.Phases[0].Compounds["Ethylbenzene"].MoleFraction.GetValueOrDefault();
            double xB = product.Object.Phases[0].Compounds["Toluene"].MoleFraction.GetValueOrDefault();
            double fAout = xA / (xA + xB);
            double fBout = 1.0 - fAout;
            double rA = 0.52, rB = 0.46;
            double mayo = (rA * fAout * fAout + fAout * fBout) /
                          (rA * fAout * fAout + 2.0 * fAout * fBout + rB * fBout * fBout);

            TestContext.WriteLine($"copolymer: X={reactor.Conversion:F3} F_A={reactor.CopolymerCompositionA:F4} " +
                                  $"mayo(f_A={fAout:F4})={mayo:F4} Mn={reactor.Mn:F0} PDI={reactor.PDI:F3}");

            Assert.Multiple(() =>
            {
                Assert.That(reactor.IsCopolymer(), Is.True, "a second monomer selects the copolymer model");
                Assert.That(reactor.Conversion, Is.GreaterThan(0.0).And.LessThan(1.0), "some monomer must convert");
                Assert.That(xA, Is.GreaterThan(0.0), "monomer A is only partly consumed");
                Assert.That(xB, Is.GreaterThan(0.0), "monomer B is only partly consumed");
                Assert.That(reactor.CopolymerCompositionA, Is.InRange(0.3, 0.7), "near-equimolar copolymer for a 50/50 feed");
                Assert.That(reactor.CopolymerCompositionA, Is.EqualTo(mayo).Within(0.02), "composition follows Mayo-Lewis");
                Assert.That(reactor.Mn, Is.GreaterThan(1.0e4), "a polymer of substantial molar mass must form");
                Assert.That(product.Object.Phases[0].Compounds["Polystyrene"].MoleFraction.GetValueOrDefault(),
                            Is.GreaterThan(0.0), "the product stream must contain polymer");
            });
        }

        [Test]
        public void PlugFlowCopolymerReactorSolvesOnAFlowsheet()
        {
            // The plug-flow flag routes the copolymer reactor through the batch/PFR solver. A skewed feed run to
            // high conversion must show composition drift: the copolymer composition leaving the reactor differs
            // from the Mayo-Lewis value at the feed (the more reactive monomer has depleted).
            var poly = new ConstantProperties
            {
                Name = "Polystyrene", CAS_Number = "9003-53-6", Formula = "(C8H8)n", Molar_Weight = 100000.0,
                Critical_Temperature = 1200.0, Critical_Pressure = 5.0e5, Acentric_Factor = 0.5,
                Normal_Boiling_Point = 800.0, IsHYPO = 1, CurrentDB = "User", OriginalDB = "User"
            };

            var fs = Flowsheet.Create("PFRCopoly")
                .WithCompounds("Ethylbenzene", "Toluene", "N-pentane")
                .WithCompound(poly)
                .WithPropertyPackage(PropertyPackages.PCSAFT);

            var feed = fs.AddMaterialStream("feed")
                .At(333.15.Kelvin(), 5.0e5.Pascal())
                .WithMolarFlow(1.0.MolPerSecond())
                .SetCompoundMolarFlow("Ethylbenzene", 0.24)
                .SetCompoundMolarFlow("Toluene", 0.74)
                .SetCompoundMolarFlow("N-pentane", 0.02)
                .SetCompoundMolarFlow("Polystyrene", 0.0);
            var product = fs.AddMaterialStream("product");

            var inner = fs.Inner;
            var robj = inner.AddObject(OT.RCT_Polymerization, 100, 100, "R-1");
            var reactor = (DWSIM.UnitOperations.Reactors.Reactor_Polymerization)robj;
            reactor.MonomerID = "Ethylbenzene";
            reactor.MonomerBID = "Toluene";
            reactor.InitiatorID = "N-pentane";
            reactor.PolymerID = "Polystyrene";
            reactor.ReactivityRatioA = 0.52;
            reactor.ReactivityRatioB = 0.46;
            reactor.IsothermalTemperature = 333.15;
            reactor.Volume = 6.0;                  // long residence -> high conversion, short of complete
            reactor.PlugFlow = true;

            inner.ConnectObjects(feed.Object.GraphicObject, robj.GraphicObject, 0, 0);
            inner.ConnectObjects(robj.GraphicObject, product.Object.GraphicObject, 0, 0);

            fs.Solve();

            double fA0 = 0.24 / (0.24 + 0.74);
            double fB0 = 1.0 - fA0;
            double rA = 0.52, rB = 0.46;
            double mayoFeed = (rA * fA0 * fA0 + fA0 * fB0) / (rA * fA0 * fA0 + 2.0 * fA0 * fB0 + rB * fB0 * fB0);
            TestContext.WriteLine($"PFR reactor: X={reactor.Conversion:F3} cumF_A={reactor.CopolymerCompositionA:F4} mayoFeed={mayoFeed:F4} Mn={reactor.Mn:F0} PDI={reactor.PDI:F3}");

            Assert.Multiple(() =>
            {
                Assert.That(reactor.IsCopolymer(), Is.True);
                Assert.That(reactor.Conversion, Is.GreaterThan(0.5), "the long-residence PFR must reach high conversion");
                Assert.That(reactor.CopolymerCompositionA, Is.InRange(0.0, 1.0));
                Assert.That(System.Math.Abs(reactor.CopolymerCompositionA - mayoFeed), Is.GreaterThan(0.02),
                            "composition drift moves the product composition away from the feed Mayo-Lewis value");
                Assert.That(reactor.Mn, Is.GreaterThan(1.0e4), "a polymer of substantial molar mass must form");
                Assert.That(product.Object.Phases[0].Compounds["Polystyrene"].MoleFraction.GetValueOrDefault(),
                            Is.GreaterThan(0.0), "the product stream must contain polymer");
            });
        }

        [Test]
        public void AdiabaticReactorHeatsUpFromTheExotherm()
        {
            // Phase 3: adiabatic operation. The exothermic polymerization has no cooling duty, so the reactor
            // temperature rises above the feed and couples to the conversion through the Arrhenius kinetics.
            var poly = new ConstantProperties
            {
                Name = "Polystyrene", CAS_Number = "9003-53-6", Formula = "(C8H8)n", Molar_Weight = 100000.0,
                Critical_Temperature = 1200.0, Critical_Pressure = 5.0e5, Acentric_Factor = 0.5,
                Normal_Boiling_Point = 800.0, IsHYPO = 1, CurrentDB = "User", OriginalDB = "User"
            };

            var fs = Flowsheet.Create("PolyReactorAdiabatic")
                .WithCompounds("Ethylbenzene", "N-pentane")
                .WithCompound(poly)
                .WithPropertyPackage(PropertyPackages.PCSAFT);

            double Tfeed = 333.15;
            var feed = fs.AddMaterialStream("feed")
                .At(Tfeed.Kelvin(), 5.0e5.Pascal())
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
            reactor.Volume = 3.0;
            reactor.ReactorOperationMode = DWSIM.UnitOperations.Reactors.OperationMode.Adiabatic;

            inner.ConnectObjects(feed.Object.GraphicObject, robj.GraphicObject, 0, 0);
            inner.ConnectObjects(robj.GraphicObject, product.Object.GraphicObject, 0, 0);
            inner.ConnectObjects(eobj.GraphicObject, robj.GraphicObject, 0, 1);

            fs.Solve();

            double Tout = product.Object.Phases[0].Properties.temperature.GetValueOrDefault();
            TestContext.WriteLine($"adiabatic: X={reactor.Conversion:F3} dT={reactor.DeltaT:F1}K Tout={Tout:F1}K Mn={reactor.Mn:F0}");

            Assert.Multiple(() =>
            {
                Assert.That(reactor.Conversion, Is.GreaterThan(0.0).And.LessThan(1.0), "some monomer must convert");
                Assert.That(reactor.DeltaT.GetValueOrDefault(), Is.GreaterThan(1.0), "the exotherm must raise the temperature");
                Assert.That(Tout, Is.GreaterThan(Tfeed + 1.0), "the product leaves hotter than the feed");
            });
        }
    }
}
