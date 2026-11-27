using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using DWSIM.Automation.FluentAPI;
using DWSIM.Extensions.SolidCraft;
using DWSIM.Extensions.SolidCraft.Viewer3D;
using DWSIM.Interfaces;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DWSIM.FluentAPI.Tests
{
    /// <summary>
    /// The link between a flowsheet and the DWSIM 3D window: the scene it builds, the changes it
    /// sends, the commands it carries out on the flowsheet, and the connection itself, with a test
    /// client in place of the window.
    /// </summary>
    [TestFixture]
    public class Viewer3DLinkTests
    {
        static Flowsheet PumpAndHeater()
        {
            var fs = Flowsheet.Create("3D link").WithCompound("Water").WithPropertyPackage(PropertyPackages.SteamTables);
            var feed = fs.AddMaterialStream("1").At(300.Kelvin(), 101325.0.Pascal()).WithMassFlow(10.KgPerSecond());
            var s2 = fs.AddMaterialStream("2");
            var s3 = fs.AddMaterialStream("3");
            fs.AddPump("P-1").ConnectFeed(feed, 0).ConnectProduct(s2, 0);
            fs.AddHeater("E-1").ConnectFeed(s2, 0).ConnectProduct(s3, 0);
            fs.AutoLayout();
            fs.Solve();
            return fs;
        }

        static string Id(Flowsheet fs, string tag) => fs.Inner.GetFlowsheetSimulationObject(tag).Name;

        static JObject Item(JObject scene, string id) => scene["items"].OfType<JObject>().SingleOrDefault(i => (string)i["id"] == id);

        static JObject Command(string type, object fields = null)
        {
            var m = fields == null ? new JObject() : JObject.FromObject(fields);
            m["type"] = type;
            return m;
        }

        [Test]
        public void TheSceneHoldsTheEquipmentAndTheLines()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            link.Sync();
            var scene = link.Scene;

            Assert.That(scene, Is.Not.Null);
            var pump = Item(scene, Id(fs, "P-1"));
            Assert.That(pump, Is.Not.Null, "the pump is an item");
            Assert.That((string)pump["tag"], Is.EqualTo("P-1"));
            Assert.That((string)pump["type"], Is.EqualTo("Pump"));
            Assert.That(pump["parts"], Is.Not.Empty);
            Assert.That(Item(scene, Id(fs, "E-1")), Is.Not.Null, "the heater is an item");
            Assert.That(Item(scene, Id(fs, "2")), Is.Null, "a stream is a line, not an item");

            var lineIds = scene["lines"].Select(l => (string)l["id"]).ToList();
            Assert.That(lineIds, Does.Contain(Id(fs, "2")), "the stream between the pump and the heater is a line");
        }

        [Test]
        public void NothingIsBuiltAgainWhenNothingChanged()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            link.Sync();
            var first = link.Scene;
            link.Sync();
            Assert.That(link.Scene, Is.SameAs(first));

            fs.Inner.GetFlowsheetSimulationObject("E-1").GraphicObject.X += 200;
            link.Sync();
            Assert.That(link.Scene, Is.Not.SameAs(first), "an icon moved on the canvas makes a new scene");
        }

        [Test]
        public void PlacingAnItemKeepsItInTheLayout()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            link.Sync();
            var pumpId = Id(fs, "P-1");

            var answer = link.Handle(Command(SceneProtocol.Place, new { id = pumpId, x = 50000.4, y = 20000, z = 0, rot = 95 }));
            Assert.That((string)answer["type"], Is.EqualTo(SceneProtocol.Ack), (string)answer["message"]);

            link.Sync();
            var pump = Item(link.Scene, pumpId);
            Assert.That(pump["o"].Select(v => (double)v), Is.EqualTo(new[] { 50000.0, 20000.0, 0.0 }));
            Assert.That((int)pump["rot"], Is.EqualTo(90), "turns are quarter turns");
            Assert.That((bool)pump["placed"], Is.True);
            Assert.That(PlantLayout.Load(fs.Inner).ContainsKey(pumpId), "the place is saved with the flowsheet");

            link.Handle(Command(SceneProtocol.LayoutReset, new { ids = new[] { pumpId } }));
            link.Sync();
            Assert.That((bool)Item(link.Scene, pumpId)["placed"], Is.False, "a reset item follows the drawing again");
        }

        [Test]
        public void MovesInTheWindowUndoAsOneStepEach()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            var p = Id(fs, "P-1");
            var e = Id(fs, "E-1");
            string Type(JObject a) => (string)a["type"];

            var batch = new JObject
            {
                ["type"] = SceneProtocol.Place,
                ["items"] = new JArray(
                    new JObject { ["id"] = p, ["x"] = 1000, ["y"] = 0, ["z"] = 0, ["rot"] = 0 },
                    new JObject { ["id"] = e, ["x"] = 5000, ["y"] = 0, ["z"] = 2500, ["rot"] = 270 })
            };
            Assert.That(Type(link.Handle(batch)), Is.EqualTo(SceneProtocol.Ack));
            var layout = PlantLayout.Load(fs.Inner);
            Assert.That(layout[e].Z, Is.EqualTo(2500), "the elevation is kept");
            Assert.That(layout[e].Rot, Is.EqualTo(270));

            link.Handle(Command(SceneProtocol.Place, new { id = p, x = 3000, y = 0, z = 0, rot = 0 }));
            Assert.That(PlantLayout.Load(fs.Inner)[p].X, Is.EqualTo(3000));

            Assert.That(Type(link.Handle(Command(SceneProtocol.Undo))), Is.EqualTo(SceneProtocol.Ack));
            Assert.That(PlantLayout.Load(fs.Inner)[p].X, Is.EqualTo(1000), "undo takes back the last move");
            link.Handle(Command(SceneProtocol.Undo));
            Assert.That(PlantLayout.Load(fs.Inner), Is.Empty, "the two items of the batch go back together");
            Assert.That(Type(link.Handle(Command(SceneProtocol.Undo))), Is.EqualTo(SceneProtocol.Error), "nothing is left to undo");

            link.Handle(Command(SceneProtocol.Redo));
            Assert.That(PlantLayout.Load(fs.Inner).Count, Is.EqualTo(2));
            link.Handle(Command(SceneProtocol.Redo));
            Assert.That(PlantLayout.Load(fs.Inner)[p].X, Is.EqualTo(3000));

            link.Handle(Command(SceneProtocol.Undo));
            link.Handle(Command(SceneProtocol.Place, new { id = p, x = 7000, y = 0, z = 0, rot = 0 }));
            Assert.That(Type(link.Handle(Command(SceneProtocol.Redo))), Is.EqualTo(SceneProtocol.Error), "a new move ends what could be redone");

            Assert.That(Type(link.Handle(Command(SceneProtocol.Place, new { id = p, x = 0, y = 0, z = 900000, rot = 0 }))), Is.EqualTo(SceneProtocol.Error), "an elevation of 900 m is refused");
            Assert.That(Type(link.Handle(Command(SceneProtocol.Place, new { id = p, x = "far", y = 0 }))), Is.EqualTo(SceneProtocol.Error), "a place must be a number");
            Assert.That(PlantLayout.Load(fs.Inner)[p].X, Is.EqualTo(7000), "a refused move changes nothing");
        }

        [Test]
        public void TheWindowCanAddConnectRenameAndDelete()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            link.Sync();

            var added = link.Handle(Command(SceneProtocol.Add, new { objectType = "Cooler", tag = "C-1", x = 30000, y = 0 }));
            Assert.That((string)added["type"], Is.EqualTo(SceneProtocol.Added), (string)added["message"]);
            var coolerId = (string)added["id"];
            Assert.That(fs.Inner.SimulationObjects.ContainsKey(coolerId));
            Assert.That(fs.Inner.GraphicObjects[coolerId].Tag, Is.EqualTo("C-1"));

            var heaterId = Id(fs, "E-1");
            var outletId = Id(fs, "3");
            link.Handle(Command(SceneProtocol.Disconnect, new { from = heaterId, to = outletId }));
            var joined = link.Handle(Command(SceneProtocol.Connect, new { from = heaterId, to = coolerId }));
            Assert.That((string)joined["type"], Is.EqualTo(SceneProtocol.Added), (string)joined["message"]);
            var streamId = (string)joined["id"];
            var stream = fs.Inner.GraphicObjects[streamId];
            Assert.That(stream.InputConnectors[0].AttachedConnector.AttachedFrom.Name, Is.EqualTo(heaterId));
            Assert.That(stream.OutputConnectors[0].AttachedConnector.AttachedTo.Name, Is.EqualTo(coolerId));

            link.Sync();
            Assert.That(Item(link.Scene, coolerId), Is.Not.Null);
            Assert.That((bool)Item(link.Scene, coolerId)["placed"], Is.True, "an object added in 3D keeps the place it was dropped at");

            var renamed = link.Handle(Command(SceneProtocol.Rename, new { id = coolerId, tag = "C-101" }));
            Assert.That((string)renamed["type"], Is.EqualTo(SceneProtocol.Ack), (string)renamed["message"]);
            var taken = link.Handle(Command(SceneProtocol.Rename, new { id = coolerId, tag = "P-1" }));
            Assert.That((string)taken["type"], Is.EqualTo(SceneProtocol.Error), "two objects cannot share a tag");

            link.Sync();
            Assert.That((string)Item(link.Scene, coolerId)["tag"], Is.EqualTo("C-101"));

            var deleted = link.Handle(Command(SceneProtocol.Delete, new { ids = new[] { coolerId } }));
            Assert.That((string)deleted["type"], Is.EqualTo(SceneProtocol.Ack), (string)deleted["message"]);
            link.Sync();
            Assert.That(Item(link.Scene, coolerId), Is.Null);
            Assert.That(PlantLayout.Load(fs.Inner).ContainsKey(coolerId), Is.False, "a deleted object leaves the layout");
        }

        [Test]
        public void BuildingInTheWindowUndoesStepByStep()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            link.Sync();
            string Type(JObject a) => (string)a["type"];

            var added = link.Handle(Command(SceneProtocol.Add, new { objectType = "Cooler", tag = "C-1", x = 30000, y = 0 }));
            var cooler = (string)added["id"];
            var product = link.Handle(Command(SceneProtocol.Connect, new { from = cooler }));
            Assert.That(Type(product), Is.EqualTo(SceneProtocol.Added), (string)product["message"]);
            var feed = link.Handle(Command(SceneProtocol.Connect, new { to = cooler }));
            Assert.That(Type(feed), Is.EqualTo(SceneProtocol.Added), (string)feed["message"]);
            var go = fs.Inner.GraphicObjects[cooler];
            Assert.That(go.InputConnectors[0].IsAttached && go.OutputConnectors[0].IsAttached, "a feed on the inlet and a product on the outlet");
            Assert.That(Type(link.Handle(Command(SceneProtocol.Connect, new { to = cooler }))), Is.EqualTo(SceneProtocol.Error), "the inlet is taken now");

            link.Sync();
            var ports = Item(link.Scene, cooler)["ports"].Where(p => !((string)p["k"]).StartsWith("e")).ToList();
            Assert.That(ports.Select(p => (string)p["k"]), Is.EquivalentTo(new[] { "in", "out" }), "one material inlet and one outlet");
            Assert.That(ports.All(p => (bool)p["used"]), "both material nozzles carry a stream");
            Assert.That(link.Scene["lines"].Select(l => (string)l["id"]), Does.Contain((string)feed["id"]), "the feed is a line to the battery limit");

            link.Handle(Command(SceneProtocol.Undo));
            link.Handle(Command(SceneProtocol.Undo));
            Assert.That(fs.Inner.SimulationObjects.ContainsKey((string)feed["id"]), Is.False, "undo takes the feed back");
            Assert.That(fs.Inner.SimulationObjects.ContainsKey((string)product["id"]), Is.False, "and then the product");
            Assert.That(fs.Inner.GraphicObjects[cooler].OutputConnectors[0].IsAttached, Is.False);
            link.Handle(Command(SceneProtocol.Undo));
            Assert.That(fs.Inner.SimulationObjects.ContainsKey(cooler), Is.False, "and then the cooler");
            Assert.That(PlantLayout.Load(fs.Inner).ContainsKey(cooler), Is.False, "with its place");
            link.Handle(Command(SceneProtocol.Redo));
            Assert.That(fs.Inner.SimulationObjects.ContainsKey(cooler), Is.True, "redo brings the cooler back");

            var pump = Id(fs, "P-1");
            var feedOfPump = Id(fs, "1");
            Assert.That(Type(link.Handle(Command(SceneProtocol.Delete, new { ids = new[] { pump } }))), Is.EqualTo(SceneProtocol.Ack));
            Assert.That(fs.Inner.SimulationObjects.ContainsKey(pump), Is.False);
            link.Handle(Command(SceneProtocol.Undo));
            Assert.That(fs.Inner.SimulationObjects.ContainsKey(pump), Is.True, "undo brings a deleted pump back");
            Assert.That(fs.Inner.GraphicObjects[pump].InputConnectors[0].AttachedConnector?.AttachedFrom?.Name, Is.EqualTo(feedOfPump), "with its connections");

            var heater = Id(fs, "E-1");
            link.Handle(Command(SceneProtocol.Rename, new { id = heater, tag = "E-101" }));
            Assert.That(fs.Inner.GraphicObjects[heater].Tag, Is.EqualTo("E-101"));
            link.Handle(Command(SceneProtocol.Undo));
            Assert.That(fs.Inner.GraphicObjects[heater].Tag, Is.EqualTo("E-1"), "undo takes a new tag back");
        }

        [Test]
        public void EnergyStreamsJoinEnergyNozzles()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            string Type(JObject a) => (string)a["type"];
            var heater = Id(fs, "E-1");
            var pump = Id(fs, "P-1");

            var duty = link.Handle(Command(SceneProtocol.Connect, new { to = heater, energy = true }));
            Assert.That(Type(duty), Is.EqualTo(SceneProtocol.Added), (string)duty["message"]);
            var es = fs.Inner.GraphicObjects[(string)duty["id"]];
            Assert.That(es.ObjectType, Is.EqualTo(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.EnergyStream));
            Assert.That(es.OutputConnectors[0].AttachedConnector.AttachedTo.Name, Is.EqualTo(heater), "the duty feeds the heater");

            // a cooler's duty (its energy connector) to the pump's energy inlet
            var cooler = (string)link.Handle(Command(SceneProtocol.Add, new { objectType = "Cooler", x = 30000, y = 0 }))["id"];
            var work = link.Handle(Command(SceneProtocol.Connect, new { from = cooler, fromPort = -1, to = pump, energy = true }));
            Assert.That(Type(work), Is.EqualTo(SceneProtocol.Added), (string)work["message"]);

            link.Sync();
            var bolt = Item(link.Scene, (string)work["id"]);
            Assert.That(bolt, Is.Not.Null, "an energy stream is an item of the scene");
            Assert.That((bool)bolt["energy"], Is.True);
            Assert.That(bolt["links"].Count(), Is.EqualTo(2), "linked to both nozzles");
            Assert.That(Item(link.Scene, (string)duty["id"])["links"].Count(), Is.EqualTo(1), "a duty from the battery limit has one link");
            var heaterPorts = Item(link.Scene, heater)["ports"];
            Assert.That(heaterPorts.Any(p => (string)p["k"] == "ein" && (bool)p["used"]), "the heater shows its energy inlet in use");
            Assert.That(Item(link.Scene, cooler)["ports"].Any(p => (string)p["k"] == "eout" && (int)p["i"] == -1), "the cooler's energy connector is an energy outlet");

            Assert.That(Type(link.Handle(Command(SceneProtocol.Connect, new { from = pump, fromPort = -1, energy = true }))), Is.EqualTo(SceneProtocol.Error), "a pump gives no energy");
            link.Handle(Command(SceneProtocol.Undo));
            Assert.That(fs.Inner.SimulationObjects.ContainsKey((string)work["id"]), Is.False, "undo takes the energy stream back");
        }

        [Test]
        public void LogicalBlocksAndControllersAreBadges()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            var adjust = (string)link.Handle(Command(SceneProtocol.Add, new { objectType = "OT_Adjust", x = 0, y = 5000 }))["id"];
            var pid = (string)link.Handle(Command(SceneProtocol.Add, new { objectType = "Controller_PID", x = 4000, y = 5000 }))["id"];
            link.Sync();
            var a = Item(link.Scene, adjust);
            var c = Item(link.Scene, pid);
            Assert.That((bool)a["badge"] && (bool)c["badge"], Is.True);
            Assert.That((double)a["o"][2], Is.EqualTo(SceneBuilder.BadgeHeight), "a logical block floats");
            Assert.That((double)c["o"][2], Is.EqualTo(0), "a controller stands on the ground");
            Assert.That(link.PaletteWithExtensions().Select(e => (string)e["objectType"]), Does.Contain("Controller_PID"));
        }

        [Test]
        public void TheWindowAsksForEditorsAndExtensionOperations()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            var pump = Id(fs, "P-1");
            Assert.That((string)link.Handle(Command(SceneProtocol.Edit, new { id = pump }))["type"], Is.EqualTo(SceneProtocol.Error), "without an editor host there is nothing to open");
            string asked = null;
            link.EditHandler = id => { asked = id; return true; };
            Assert.That((string)link.Handle(Command(SceneProtocol.Edit, new { id = pump }))["type"], Is.EqualTo(SceneProtocol.Ack));
            Assert.That(asked, Is.EqualTo(pump));
            Assert.That((string)link.Handle(Command(SceneProtocol.Edit, new { id = "nope" }))["type"], Is.EqualTo(SceneProtocol.Error));

            var extensions = link.PaletteWithExtensions().Where(e => e["external"] != null).ToList();
            TestContext.Progress.WriteLine("extension unit operations in the palette: " + extensions.Count);
            Assume.That(extensions, Is.Not.Empty, "this host loaded no extension unit operations");
            var key = (string)extensions[0]["external"];
            var added = link.Handle(new JObject { ["type"] = SceneProtocol.Add, ["objectType"] = "External", ["external"] = key, ["x"] = 20000, ["y"] = 0 });
            Assert.That((string)added["type"], Is.EqualTo(SceneProtocol.Added), (string)added["message"]);
            Assert.That(fs.Inner.GraphicObjects[(string)added["id"]].ObjectType, Is.EqualTo(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.External));
        }

        [Test]
        public void ADynamicRunSendsValuesWithoutGeometry()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            var sent = new System.Collections.Generic.List<JObject>();
            link.MessageSent += m => sent.Add(m);
            link.Sync();
            Assert.That(sent.Select(m => (string)m["type"]).Where(t => t != SceneProtocol.Select).ToArray(), Is.EqualTo(new[] { SceneProtocol.SceneFull, SceneProtocol.Values }), "the scene, then its values");

            fs.Inner.DynamicMode = true;
            sent.Clear();
            var feed = (DWSIM.Interfaces.IMaterialStream)fs.Inner.GetFlowsheetSimulationObject("1");
            ((dynamic)feed).Phases[0].Properties.temperature = 330.0;
            link.Sync();
            Assert.That(sent.Select(m => (string)m["type"]).ToArray(), Is.EqualTo(new[] { SceneProtocol.Values }), "in dynamics a new result sends values only");
            var line = sent[0]["lines"][Id(fs, "1")];
            Assert.That(line, Is.Not.Null);
            Assert.That((string)line["props"]["Temperature (C)"], Does.StartWith("56.9"), "330 K is 56.9 C");
            Assert.That(sent[0]["items"][Id(fs, "P-1")]?["props"], Is.Not.Null, "the pump's operating data come along");

            sent.Clear();
            link.Sync();
            Assert.That(sent, Is.Empty, "nothing changed, nothing is sent");

            fs.Inner.GetFlowsheetSimulationObject("E-1").GraphicObject.X += 300;
            link.Sync();
            Assert.That(sent.Select(m => (string)m["type"]), Does.Contain(SceneProtocol.SceneDelta), "a move still changes the geometry");
        }

        [Test]
        public void FiredHeatersAndFlaresHaveTheirStyle()
        {
            var fs = PumpAndHeater();
            var heater = (DWSIM.UnitOperations.UnitOperations.Heater)fs.Inner.GetFlowsheetSimulationObject("E-1");
            heater.DeltaQ = 8000;
            var scene = SceneBuilder.Build(fs.Inner);
            Assert.That((string)Item(scene, heater.Name)["style"], Is.EqualTo("fired"), "a heater of 8 MW is a fired heater");
            Assert.That((string)Item(scene, Id(fs, "P-1"))["style"], Is.Null, "a pump has no effect");
        }

        [Test]
        public void TheFlybyFollowsTheProcess()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            // a cooler after the heater, fed by its product
            var added = (string)link.Handle(Command(SceneProtocol.Add, new { objectType = "Cooler", tag = "C-1", x = 40000, y = 0 }))["id"];
            link.Handle(Command(SceneProtocol.Connect, new { from = Id(fs, "3"), to = added }));
            Assert.That(ProcessOrder.Of(fs.Inner).Select(id => fs.Inner.SimulationObjects[id].GraphicObject.Tag), Is.EqualTo(new[] { "P-1", "E-1", "C-1" }), "from the feed to the product");
            link.Sync();
            var plan = link.Handle(Command(SceneProtocol.Flyby));
            Assert.That((string)plan["type"], Is.EqualTo(SceneProtocol.FlybyPlan));
            Assert.That(plan["ids"].Select(t => (string)t), Is.EqualTo(new[] { Id(fs, "P-1"), Id(fs, "E-1"), added }));
            Assert.That(FlybyRecorder.Width, Is.EqualTo(1920));
        }

        [Test]
        public void AControlRoomIsABuildingOfTheSite()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            string Type(JObject a) => (string)a["type"];
            Assert.That(link.PaletteWithExtensions().Select(e => (string)e["objectType"]), Does.Contain("site:controlroom"));

            var added = link.Handle(Command(SceneProtocol.Add, new { objectType = "site:controlroom", x = -20000, y = -15000 }));
            Assert.That(Type(added), Is.EqualTo(SceneProtocol.Added), (string)added["message"]);
            var id = (string)added["id"];
            Assert.That(id, Is.EqualTo("site:controlroom:1"));
            link.Sync();
            var room = Item(link.Scene, id);
            Assert.That((string)room["site"], Is.EqualTo("controlroom"));
            Assert.That(room["o"].Select(v => (double)v), Is.EqualTo(new[] { -20000.0, -15000.0, 0.0 }));
            Assert.That(fs.Inner.SimulationObjects.ContainsKey(id), Is.False, "no object of the simulation");

            Assert.That(Type(link.Handle(Command(SceneProtocol.Place, new { id, x = -25000, y = -15000, z = 0, rot = 90 }))), Is.EqualTo(SceneProtocol.Ack));
            link.Sync();
            Assert.That((int)Item(link.Scene, id)["rot"], Is.EqualTo(90));
            Assert.That(PlantLayout.Load(fs.Inner).ContainsKey(id), Is.False, "the equipment layout SolidCraft reads stays clean");
            link.Handle(Command(SceneProtocol.Undo));
            link.Sync();
            Assert.That((double)Item(link.Scene, id)["o"][0], Is.EqualTo(-20000.0), "undo puts it back");

            Assert.That(Type(link.Handle(Command(SceneProtocol.Edit, new { id }))), Is.EqualTo(SceneProtocol.Error), "no editor for a building");
            Assert.That(Type(link.Handle(Command(SceneProtocol.Delete, new { ids = new[] { id } }))), Is.EqualTo(SceneProtocol.Ack));
            link.Sync();
            Assert.That(Item(link.Scene, id), Is.Null);
            link.Handle(Command(SceneProtocol.Undo));
            link.Sync();
            Assert.That(Item(link.Scene, id), Is.Not.Null, "undo brings a deleted building back");
        }

        [Test]
        public void BadCommandsAreAnsweredWithAnError()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            Assert.That((string)link.Handle(Command(SceneProtocol.Place, new { id = "nope", x = 0, y = 0 }))["type"], Is.EqualTo(SceneProtocol.Error));
            Assert.That((string)link.Handle(Command("fly"))["type"], Is.EqualTo(SceneProtocol.Error));
            Assert.That((string)link.Handle(Command(SceneProtocol.Add, new { objectType = "Spaceship" }))["type"], Is.EqualTo(SceneProtocol.Error));
            var answer = link.Handle(Command(SceneProtocol.Delete, new { ids = new[] { "nope" }, @ref = "r7" }));
            Assert.That((string)answer["ref"], Is.EqualTo("r7"), "the answer repeats the ref of the command");
        }

        [Test]
        public void SelectionGoesBothWays()
        {
            var fs = PumpAndHeater();
            var link = new FlowsheetLink(fs.Inner);
            var pumpId = Id(fs, "P-1");
            link.Handle(Command(SceneProtocol.Select, new { ids = new[] { pumpId } }));
            Assert.That(FlowsheetLink.Selection.Get(fs.Inner), Is.EqualTo(new[] { pumpId }));
        }

        [Test]
        public void ADeltaTurnsTheOldSceneIntoTheNewOne()
        {
            var fs = PumpAndHeater();
            var before = SceneBuilder.Build(fs.Inner);
            Assert.That(SceneDiff.Delta(before, SceneBuilder.Build(fs.Inner)), Is.Null, "the same flowsheet makes the same scene");

            var layout = PlantLayout.Load(fs.Inner);
            layout[Id(fs, "E-1")] = new Placement { X = 40000, Y = 10000, Z = 0, Rot = 180 };
            PlantLayout.Save(fs.Inner, layout);
            fs.Inner.DeleteSelectedObject(this, EventArgs.Empty, fs.Inner.GetFlowsheetSimulationObject("P-1").GraphicObject, false, false);
            var after = SceneBuilder.Build(fs.Inner);

            var delta = SceneDiff.Delta(before, after);
            Assert.That(delta, Is.Not.Null);
            Assert.That(delta["remove"], Is.Not.Null);
            Assert.That(SceneDiff.Same(SceneDiff.Apply(before, delta), after), Is.True);
        }

        [Test]
        public void TheWindowConnectsWithItsTokenAndGetsTheScene()
        {
            var fs = PumpAndHeater();
            using (var link = new FlowsheetLink(fs.Inner) { PollInterval = 100 })
            {
                link.Start();
                Assert.That(link.Port, Is.GreaterThan(0));

                using (var intruder = Connect(link.Port))
                {
                    intruder.Writer.Write(SceneProtocol.Encode(Command(SceneProtocol.Hello, new { token = "wrong", version = SceneProtocol.Version })));
                    Assert.That((string)intruder.Next()["type"], Is.EqualTo(SceneProtocol.Error));
                    Assert.That(intruder.Reader.ReadLine(), Is.Null, "a window with the wrong token is turned away");
                }

                using (var window = Connect(link.Port))
                {
                    window.Writer.Write(SceneProtocol.Encode(Command(SceneProtocol.Hello, new { token = link.Token, version = SceneProtocol.Version })));
                    var welcome = window.Next();
                    Assert.That((string)welcome["type"], Is.EqualTo(SceneProtocol.Welcome));
                    Assert.That(welcome["palette"].Select(e => (string)e["objectType"]), Does.Contain("DistillationColumn"), "the welcome carries the palette");
                    var full = window.Until(SceneProtocol.SceneFull);
                    var pumpId = Id(fs, "P-1");
                    Assert.That(Item((JObject)full["scene"], pumpId), Is.Not.Null);

                    window.Writer.Write(SceneProtocol.Encode(Command(SceneProtocol.Place, new { id = pumpId, x = 1000, y = 2000, z = 0, rot = 0, @ref = "a1" })));
                    var ack = window.Until(SceneProtocol.Ack);
                    Assert.That((string)ack["ref"], Is.EqualTo("a1"));
                    var delta = window.Until(SceneProtocol.SceneDelta);
                    Assert.That(delta["upsert"].Select(i => (string)i["id"]), Does.Contain(pumpId));
                }
                link.Stop();
            }
        }

        [Test]
        public void TheLauncherPassesThePortAndToken()
        {
            var fs = PumpAndHeater();
            using (var link = new FlowsheetLink(fs.Inner))
            {
                link.Start();
                var args = ViewerLauncher.Arguments(link, compatibilityRenderer: true);
                Assert.That(args, Does.StartWith("--rendering-method gl_compatibility -- "));
                Assert.That(args, Does.Contain("--dwsim-port=" + link.Port));
                Assert.That(args, Does.Contain("--dwsim-token=" + link.Token));
            }
        }

        [Test]
        public void TheLauncherRunsTheProjectFolderInTheGodotEditor()
        {
            var project = Path.Combine(Path.GetTempPath(), "dwsim3d-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, "project.godot"), "config_version=5\n");
            var editor = Path.Combine(project, "godot.exe");
            File.WriteAllText(editor, "");
            var oldPath = Environment.GetEnvironmentVariable(ViewerLauncher.PathVariable);
            var oldGodot = Environment.GetEnvironmentVariable(ViewerLauncher.GodotVariable);
            try
            {
                Environment.SetEnvironmentVariable(ViewerLauncher.PathVariable, project);
                Environment.SetEnvironmentVariable(ViewerLauncher.GodotVariable, editor);
                string exe, prefix;
                Assert.That(ViewerLauncher.Resolve(out exe, out prefix), Is.True);
                Assert.That(exe, Is.EqualTo(editor));
                Assert.That(prefix, Is.EqualTo("--path \"" + Path.GetFullPath(project) + "\" "));
            }
            finally
            {
                Environment.SetEnvironmentVariable(ViewerLauncher.PathVariable, oldPath);
                Environment.SetEnvironmentVariable(ViewerLauncher.GodotVariable, oldGodot);
                Directory.Delete(project, true);
            }
        }

        sealed class TestWindow : IDisposable
        {
            public TcpClient Client;
            public StreamReader Reader;
            public StreamWriter Writer;

            public JObject Next() => SceneProtocol.Decode(Reader.ReadLine());

            /// <summary>Reads messages until one of a type comes (others, like the selection, may come first).</summary>
            public JObject Until(string type)
            {
                for (var k = 0; k < 20; k++)
                {
                    var m = Next();
                    Assert.That(m, Is.Not.Null, "the link closed before sending " + type);
                    if ((string)m["type"] == type) return m;
                }
                Assert.Fail("no " + type + " came");
                return null;
            }

            public void Dispose() => Client.Close();
        }

        static TestWindow Connect(int port)
        {
            var c = new TcpClient("127.0.0.1", port) { ReceiveTimeout = 30000 };
            var s = c.GetStream();
            return new TestWindow
            {
                Client = c,
                Reader = new StreamReader(s, new UTF8Encoding(false)),
                Writer = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true }
            };
        }
    }
}
