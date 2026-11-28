using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DWSIM.Automation.FluentAPI;
using DWSIM.Extensions.SolidCraft.Viewer3D;
using DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>The user's own 3D objects in the site builder (synthetic models only; a bought model never goes in the repository).</summary>
    [TestFixture]
    public class PropLibraryTests
    {
        string _dir, _root, _cache;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dwsim-props-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _root = PropLibrary.Root;
            _cache = ModelImporter.CacheRoot;
            PropLibrary.Root = Path.Combine(_dir, "library");
            ModelImporter.CacheRoot = Path.Combine(_dir, "cache");
            PropLibrary.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            PropLibrary.Root = _root;
            ModelImporter.CacheRoot = _cache;
            PropLibrary.Reload();
            try { Directory.Delete(_dir, true); } catch { }
        }

        /// <summary>A tree as an OBJ file in centimetres, Y up, standing off the origin: a trunk 40 cm thick and 300 cm high, a crown 300 cm wide up to 700 cm.</summary>
        string Tree(string name)
        {
            var sb = new StringBuilder();
            var v = 0;
            Action<double, double, double, double, double, double> box = (x0, y0, z0, x1, y1, z1) =>
            {
                foreach (var p in new[] { new[] { x0, y0, z0 }, new[] { x1, y0, z0 }, new[] { x1, y1, z0 }, new[] { x0, y1, z0 }, new[] { x0, y0, z1 }, new[] { x1, y0, z1 }, new[] { x1, y1, z1 }, new[] { x0, y1, z1 } })
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}", p[0], p[1], p[2]));
                foreach (var f in new[] { new[] { 1, 2, 3, 4 }, new[] { 5, 8, 7, 6 }, new[] { 1, 5, 6, 2 }, new[] { 2, 6, 7, 3 }, new[] { 3, 7, 8, 4 }, new[] { 4, 8, 5, 1 } })
                    sb.AppendLine("f " + string.Join(" ", f.Select(i => (i + v).ToString(CultureInfo.InvariantCulture))));
                v += 8;
            };
            // OBJ is Y up: x, height, z
            box(480, 0, 980, 520, 300, 1020);
            box(350, 300, 850, 650, 700, 1150);
            var path = Path.Combine(_dir, name);
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        [Test]
        public void AModelFileBecomesAPropStandingOnItsBase()
        {
            var p = PropLibrary.Import(Tree("oak tree.obj"));
            Assert.That(p.Name, Is.EqualTo("oak tree"));
            Assert.That(p.Category, Is.EqualTo(SiteCatalog.Nature), "the name says it is a tree");
            Assert.That(p.H, Is.EqualTo(7.0).Within(1e-3), "centimetres, guessed from the size of a prop");
            Assert.That(p.W, Is.EqualTo(3.0).Within(1e-3));
            Assert.That(p.D, Is.EqualTo(3.0).Within(1e-3));
            Assert.That(p.Kind, Does.StartWith("user:"));
            var glb = GltfReader.Read(PropLibrary.GlbPath(p));
            double[] min, max;
            glb.Bounds(out min, out max);
            Assert.That(min[1], Is.EqualTo(0).Within(1e-4), "the base on the ground (Y up in the file)");
            Assert.That((min[0] + max[0]) / 2, Is.EqualTo(0).Within(1e-4), "the middle of the base at the origin");
            Assert.That((min[2] + max[2]) / 2, Is.EqualTo(0).Within(1e-4));

            // the same file again replaces its prop; another name is another one
            Assert.That(PropLibrary.Import(Tree("oak tree.obj")).Id, Is.EqualTo(p.Id));
            Assert.That(PropLibrary.All(), Has.Count.EqualTo(1));

            var e = SiteCatalog.Find(p.Kind);
            Assert.That(e.Shape, Is.EqualTo(SiteCatalog.Shape.Point));
            Assert.That(e.H, Is.EqualTo(7.0).Within(1e-3));
            Assert.That(PropLibrary.GuessCategory("Forklift truck"), Is.EqualTo(SiteCatalog.Vehicles));
            Assert.That(PropLibrary.GuessCategory("Pump skid"), Is.EqualTo(SiteCatalog.Utilities));
        }

        [Test]
        public void APropIsRenamedResizedPricedAndTakenOut()
        {
            var p = PropLibrary.Import(Tree("tree.obj"), "Jacaranda", null, 250, 14);
            Assert.That(p.H, Is.EqualTo(14).Within(1e-3), "the height asked for");
            Assert.That(p.W, Is.EqualTo(6).Within(1e-3), "scaled the same in every direction");
            Assert.That(p.Cost, Is.EqualTo(250));
            p = PropLibrary.Edit(p.Kind, "Big jacaranda", SiteCatalog.Buildings, 400, 7);
            Assert.That(p.Name, Is.EqualTo("Big jacaranda"));
            Assert.That(p.Category, Is.EqualTo(SiteCatalog.Buildings));
            Assert.That(p.H, Is.EqualTo(7).Within(1e-3));
            Assert.That(p.W, Is.EqualTo(3).Within(1e-3));
            PropLibrary.Reload();
            Assert.That(PropLibrary.Find(p.Kind).Cost, Is.EqualTo(400), "kept in the library");
            Assert.That(PropLibrary.Edit(p.Kind, category: "nonsense").Category, Is.EqualTo(SiteCatalog.Buildings), "an unknown category leaves it");
            Assert.That(PropLibrary.Remove(p.Kind), Is.True);
            Assert.That(PropLibrary.Find(p.Kind), Is.Null);
            Assert.That(Directory.Exists(PropLibrary.Folder(p.Id)), Is.False);
        }

        [Test]
        public void TheSiteKeepsOnlyTheReferenceAndAnotherComputerDrawsABox()
        {
            var p = PropLibrary.Import(Tree("my tree.obj"), cost: 300);
            var fs = Flowsheet.Create("props").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var link = new FlowsheetLink(fs.Inner);
            var answer = link.Handle(new JObject { ["type"] = SceneProtocol.SiteAdd, ["items"] = new JArray(
                new JObject { ["kind"] = p.Kind, ["x"] = 10000, ["y"] = 5000, ["rot"] = 30 },
                new JObject { ["kind"] = p.Kind, ["x"] = 20000, ["y"] = 5000 }) });
            Assert.That((string)answer["type"], Is.EqualTo(SceneProtocol.Added), (string)answer["message"]);
            var site = SiteLayout.LoadSite(fs.Inner);
            Assert.That(site.Props.Keys, Is.EquivalentTo(new[] { p.Kind }));
            Assert.That(site.Props[p.Kind].Cost, Is.EqualTo(300));
            Assert.That(site.Props[p.Kind].Source, Is.Null, "where the model came from stays in the library");
            var text = SiteLayout.Text(fs.Inner);
            Assert.That(text, Does.Not.Contain("my tree.obj").And.Not.Contain("prop.glb"), "the flowsheet never holds the model or its file");
            Assert.That((double)SiteCatalog.Budget(site.Items)["total"], Is.EqualTo(600).Within(1e-6), "two props at 300 each");

            link.Sync();
            var item = link.Scene["items"].OfType<JObject>().First(i => (string)i["site"] == p.Kind);
            Assert.That((string)item["cat"], Is.EqualTo(SiteCatalog.Nature));
            var catalog = SiteCatalog.ToJson(site);
            var entry = catalog.OfType<JObject>().Single(e => (string)e["kind"] == p.Kind);
            Assert.That((bool?)entry["user"], Is.True);
            Assert.That(File.Exists((string)entry["glb"]), Is.True, "the window loads the model from the library");

            // another computer: an empty library, the same flowsheet
            PropLibrary.Root = Path.Combine(_dir, "elsewhere");
            PropLibrary.Reload();
            var again = SiteLayout.LoadSite(fs.Inner);
            var missing = SiteCatalog.ToJson(again).OfType<JObject>().Single(e => (string)e["kind"] == p.Kind);
            Assert.That((bool?)missing["missing"], Is.True, "the window draws a box");
            Assert.That((double)missing["h"], Is.EqualTo(7.0).Within(1e-3));
            Assert.That(missing["glb"], Is.Null);
            Assert.That((double)SiteCatalog.Budget(again.Items)["total"], Is.EqualTo(600).Within(1e-6), "the budget still counts them");
            SiteLayout.SaveSite(fs.Inner, again);
            Assert.That(SiteLayout.LoadSite(fs.Inner).Props.ContainsKey(p.Kind), Is.True, "saving keeps the reference of a prop the library does not have");
        }

        [Test]
        public void TheWindowImportsChangesAndRemovesProps()
        {
            var fs = Flowsheet.Create("props link").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var link = new FlowsheetLink(fs.Inner) { ModelEditsInline = true };
            var sent = new List<JObject>();
            link.MessageSent += m => { lock (sent) sent.Add(m); };
            var answer = link.Handle(new JObject { ["type"] = SceneProtocol.PropImport, ["path"] = Tree("palm.obj"), ["height"] = 9 });
            Assert.That((string)answer["type"], Is.EqualTo(SceneProtocol.Ack));
            var status = sent.Last(m => (string)m["type"] == SceneProtocol.PropStatus);
            Assert.That((string)status["state"], Is.EqualTo("done"), (string)status["message"]);
            Assert.That((string)status["message"], Does.Contain("palm").And.Contain("9 m high"));
            var kind = (string)status["kind"];
            var catalog = sent.Last(m => (string)m["type"] == SceneProtocol.SiteCatalogMessage);
            Assert.That(catalog["site"].Any(e => (string)e["kind"] == kind), Is.True, "the window gets the catalog with the new prop");

            link.Handle(new JObject { ["type"] = SceneProtocol.PropEdit, ["kind"] = kind, ["name"] = "Royal palm", ["cost"] = 700 });
            Assert.That(PropLibrary.Find(kind).Name, Is.EqualTo("Royal palm"));
            Assert.That(PropLibrary.Find(kind).H, Is.EqualTo(9).Within(1e-3), "a height not given stays");

            var wrong = link.Handle(new JObject { ["type"] = SceneProtocol.PropEdit, ["kind"] = kind, ["height"] = -1 });
            Assert.That((string)wrong["type"], Is.EqualTo(SceneProtocol.Error));
            wrong = link.Handle(new JObject { ["type"] = SceneProtocol.PropImport, ["path"] = Path.Combine(_dir, "none.obj") });
            Assert.That((string)wrong["type"], Is.EqualTo(SceneProtocol.Error));

            link.Handle(new JObject { ["type"] = SceneProtocol.PropRemove, ["kind"] = kind });
            Assert.That(PropLibrary.Find(kind), Is.Null);
            Assert.That((string)link.Handle(new JObject { ["type"] = SceneProtocol.PropRemove, ["kind"] = kind })["type"], Is.EqualTo(SceneProtocol.Error));
        }
    }
}
