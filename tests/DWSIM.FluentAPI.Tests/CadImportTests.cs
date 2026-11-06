using System;
using System.IO;
using System.Linq;
using DWSIM.Automation.FluentAPI;
using DWSIM.Extensions.SolidCraft;
using DWSIM.Interfaces.Enums.GraphicObjects;
using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The CAD imports from a script, on readings saved from the import wizards (our own tutorial drawings, isometrics
    /// and data sheets), so they run without the AI Assistant.
    /// </summary>
    [TestFixture]
    public class CadImportTests
    {
        private static string Reading(string name)
        {
            var folder = AppContext.BaseDirectory;
            while (folder != null && !Directory.Exists(Path.Combine(folder, "tests", "flowsheets")))
                folder = Path.GetDirectoryName(folder);
            Assert.That(folder, Is.Not.Null, "tests/flowsheets not found");
            return Path.Combine(folder!, "tests", "flowsheets", "cad", name);
        }

        [Test]
        public void ADrawingReadingBuildsTheFlowsheet()
        {
            var fs = Flowsheet.Create("from a drawing");
            var s = fs.Inner.ImportFromDrawing(Reading("refrigeration-cycle-drawing.json"), solve: true);

            Assert.That(s.Built, Is.Not.Null);
            var tags = fs.Inner.SimulationObjects.Values.Select(o => o.GraphicObject.Tag).ToList();
            Assert.That(tags, Is.SupersetOf(new[] { "C-1", "CD-1", "V-1", "EV-1" }));
            Assert.That(fs.Inner.SelectedCompounds.Keys, Does.Contain("Propane"));
            Assert.That(fs.Inner.PropertyPackages.Count, Is.EqualTo(1));
            Assert.That(s.Summary(), Does.Contain("items"));
        }

        [Test]
        public void ADrawingCanTakeItsOwnCompoundsAndPackage()
        {
            var fs = Flowsheet.Create("from a drawing");
            fs.Inner.ImportFromDrawing(Reading("refrigeration-cycle-drawing.json"), solve: false,
                propertyPackage: "Soave-Redlich-Kwong (SRK)", compounds: new[] { "Propane" });
            Assert.That(fs.Inner.PropertyPackages.Values.Single().GetType().Name, Does.Contain("SRK"));
        }

        [Test]
        public void AnIsometricReadingMakesThePipeProfile()
        {
            var fs = Flowsheet.Create("from an isometric").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var s = fs.Inner.ImportPipeFromIsometric(Reading("fresh-feed-isometric.json"));

            Assert.That(s.Applied, Is.Not.Null);
            Assert.That(s.Applied.GraphicObject.ObjectType, Is.EqualTo(ObjectType.Pipe));
            Assert.That(s.Sections, Is.Not.Empty);
            dynamic pipe = s.Applied;
            Assert.That((int)pipe.Profile.Sections.Count, Is.EqualTo(s.Sections.Count), "one profile section per leg");
        }

        [Test]
        public void ADataSheetReadingSetsThePumpUp()
        {
            var fs = Flowsheet.Create("from a data sheet").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var s = fs.Inner.SetUpFromDataSheet(Reading("pump-data-sheet.json"));

            Assert.That(s.Target, Is.Not.Null);
            Assert.That(s.Target.GraphicObject.ObjectType, Is.EqualTo(ObjectType.Pump));
            Assert.That(s.Results.First().Text, Does.Contain("changes made from the data sheet"));
        }

        [Test]
        public void AMissingTargetIsReported()
        {
            var fs = Flowsheet.Create("from an isometric").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
                fs.Inner.ImportPipeFromIsometric(Reading("fresh-feed-isometric.json"), "NO-SUCH-PIPE"));
        }
    }
}
