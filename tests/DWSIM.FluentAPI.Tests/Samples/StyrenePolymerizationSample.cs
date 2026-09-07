using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DWSIM.Automation.FluentAPI;
using OT = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>Bulk styrene polymerization with monomer recovery. A free-radical polymerization reactor (CSTR,
    /// isothermal at 90 C) converts part of a styrene feed to polystyrene by the method of moments, reporting the
    /// conversion, the number- and weight-average molar mass and the polydispersity. The reactor effluent - the
    /// polymer dissolved in unreacted monomer - is then heated under vacuum and flashed, so the residual monomer
    /// is recovered as vapour and the polystyrene leaves as a concentrated melt (devolatilization).
    /// Ethylbenzene stands in for styrene (styrene has no shipped PC-SAFT parameters; ethylbenzene is its
    /// saturated analogue and flashes cleanly), and n-pentane for a soluble initiator.
    /// Property package: PC-SAFT (polystyrene as a non-volatile polymer), with the polymer flash for the
    /// devolatilizer.
    /// Checks: the reactor makes polymer of a substantial molar mass, the overall mass balance closes, the
    /// recovered vapour carries no polymer, and the melt is concentrated polystyrene.</summary>
    internal static class StyrenePolymerizationSample
    {
        private static string AddcompsDir([CallerFilePath] string sourceFile = "")
        {
            var dir = Path.GetDirectoryName(sourceFile);
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "content", "addcomps"));
        }

        public static void Run()
        {
            var poly = Newtonsoft.Json.JsonConvert.DeserializeObject<DWSIM.Thermodynamics.BaseClasses.ConstantProperties>(
                File.ReadAllText(Path.Combine(AddcompsDir(), "Polystyrene.json")));
            poly.CurrentDB = "User"; poly.OriginalDB = "User";

            const string polyName = "Polystyrene";
            const string monomer = "Ethylbenzene";     // styrene analogue for PC-SAFT
            const string initiator = "N-pentane";       // soluble initiator surrogate

            var fs = Flowsheet.Create("StyrenePolymerization")
                .WithCompounds(monomer, initiator)
                .WithCompound(poly)
                .WithPropertyPackage(PropertyPackages.PCSAFT);

            var feed = fs.AddMaterialStream("styrene feed")
                .At(363.15.Kelvin(), 200000.0.Pascal())
                .WithMassFlow(1.0.KgPerSecond())
                .SetCompoundMassFlow(monomer, 0.99)
                .SetCompoundMassFlow(initiator, 0.01)     // ~1 wt% soluble initiator
                .SetCompoundMassFlow(polyName, 0.0);

            // Free-radical polymerization reactor (no dedicated Fluent builder yet: place it and wire it directly).
            var reactorEffluent = fs.AddMaterialStream("polymer solution");
            var inner = fs.Inner;
            var robj = inner.AddObject(OT.RCT_Polymerization, 150, 150, "R-1");
            var qRx = inner.AddObject(OT.EnergyStream, 90, 230, "Q reactor");
            var reactor = (DWSIM.UnitOperations.Reactors.Reactor_Polymerization)robj;
            reactor.MonomerID = monomer;
            reactor.InitiatorID = initiator;
            reactor.PolymerID = polyName;
            reactor.LoadStyrenePreset();
            reactor.MonomerMolarMass = 106.17;          // ethylbenzene, the monomer stand-in
            reactor.IsothermalTemperature = 363.15;
            reactor.Volume = 30.0;                       // m3, sized for an appreciable conversion

            inner.ConnectObjects(feed.Object.GraphicObject, robj.GraphicObject, 0, 0);
            inner.ConnectObjects(robj.GraphicObject, reactorEffluent.Object.GraphicObject, 0, 0);
            inner.ConnectObjects(qRx.GraphicObject, robj.GraphicObject, 0, 1);

            // Devolatilizer: heat the effluent under vacuum and flash off the unreacted monomer.
            var hotEffluent = fs.AddMaterialStream("heated effluent");
            fs.AddHeater("H-1")
                .WithOutletTemperature(470.0.Kelvin())
                .WithPressureDrop((200000.0 - 15000.0).Pascal())
                .WithEfficiencyPercent(100.0)
                .ConnectFeed(reactorEffluent, 0)
                .ConnectProduct(hotEffluent, 0);

            var recoveredMonomer = fs.AddMaterialStream("recovered monomer");
            var polymerMelt = fs.AddMaterialStream("polystyrene melt");
            fs.AddSeparator("V-1")
                .ConnectFeed(hotEffluent, 0)
                .ConnectProduct(recoveredMonomer, 0)
                .ConnectProduct(polymerMelt, 1);

            var errors = fs.TrySolve();
            if (errors.Count > 0)
                throw new Exception("Solver reported: " +
                    string.Join("; ", errors.Select(e => e.Message)));

            double conversion = reactor.Conversion;
            double mn = reactor.Mn, mw = reactor.Mw, pdi = reactor.PDI;
            double meltPolymer = polymerMelt.OverallMassFraction(polyName);
            double vaporPolymer = recoveredMonomer.OverallMassFraction(polyName);

            new ResultTable("Bulk styrene polymerization with monomer recovery")
                .RowInRange("Reactor conversion (a useful fraction reacts)", 0.10, 0.95, conversion, "-")
                .RowInRange("Number-average molar mass Mn (substantial polymer)", 5.0e3, 3.0e5, mn, "g/mol")
                .RowInRange("Polydispersity near the free-radical range", 1.4, 2.1, pdi, "-")
                .Row("Overall mass balance F = monomer + melt", feed.MassFlowKgPerSecond,
                     recoveredMonomer.MassFlowKgPerSecond + polymerMelt.MassFlowKgPerSecond, 0.005, "kg/s")
                .RowInRange("No polymer in the recovered vapour (<1 ppm)", 0.0, 1e-6, vaporPolymer, "-")
                .RowInRange("The melt is concentrated polystyrene (>60 wt%)", 0.60, 1.0, meltPolymer, "-")
                .PrintAndThrowIfFailed();

            Console.WriteLine($"  R-1: X={conversion:F3}  Mn={mn:F0}  Mw={mw:F0}  PDI={pdi:F3}");

            CaseLibraryOutput.Emit(fs, "styrene-polymerization");
        }
    }
}
