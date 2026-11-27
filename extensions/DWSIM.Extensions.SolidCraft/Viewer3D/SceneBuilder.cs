//    Copyright 2026 Daniel Wagner O. de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Interfaces;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// The scene the DWSIM 3D window draws: the plant model of the flowsheet (see
    /// <see cref="PlantModelBuilder"/>), with the same sizes, places, lines and rack as the
    /// SolidCraft model, as JSON.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "items" holds one entry per unit operation: "id" (the object name, which the window sends back
    /// in its commands), "tag", "desc", "type" (the object type), "o" (the origin on the ground),
    /// "rot" (quarter turns, degrees), "placed" (whether the place comes from the saved layout),
    /// "inst", "label", "props" and "parts". The parts are around the origin and already turned, so
    /// the window puts them under a node at "o" and moves that node when the user drags the item.
    /// </para>
    /// <para>
    /// "lines" holds the pipes in world coordinates: "id" (the stream's name; null for an item's own
    /// line), "tag", "desc", "c", "od", "ins", "v", "phase", "from", "to", "path", "fl" and "props".
    /// Lines are routed again whenever an item moves, so a change sends them all.
    /// </para>
    /// </remarks>
    public static class SceneBuilder
    {
        /// <summary>The plant model of the flowsheet, with the saved layout and the SolidCraft settings.</summary>
        public static PlantModel Model(IFlowsheet fs, ExportOptions options = null)
            => PlantModelBuilder.Build(fs, NameOf(fs), PlantLayout.Load(fs), options ?? ExportOptions.Load());

        public static JObject Build(IFlowsheet fs, ExportOptions options = null) => FromModel(Model(fs, options), fs);

        public static JObject FromModel(PlantModel model, IFlowsheet fs = null)
        {
            var items = new JArray();
            var ids = new HashSet<string>();
            foreach (var item in model.Equipment)
            {
                var parts = new JArray();
                foreach (var part in item.Parts)
                {
                    var o = WebViewer.Part(part.Moved(new Vec3(0, 0, 0)));
                    if (o != null) parts.Add(o);
                }
                items.Add(new JObject
                {
                    ["id"] = Unique(ids, item.Name ?? item.Tag ?? "item"),
                    ["tag"] = item.Tag,
                    ["desc"] = item.Description,
                    ["type"] = TypeOf(fs, item.Name),
                    ["o"] = WebViewer.V(item.Origin),
                    ["rot"] = item.Rotation,
                    ["placed"] = item.Placed,
                    ["inst"] = item.IsInstrument,
                    ["label"] = item.Label,
                    ["props"] = WebViewer.Props(item.Properties),
                    ["style"] = StyleOf(item),
                    ["parts"] = parts,
                    ["ports"] = Ports(item, fs)
                });
            }

            if (fs != null)
            {
                AddBadges(items, ids, model, fs);
                AddSite(items, ids, fs);
                AddEnergyStreams(items, ids, model, fs);
            }

            // a line of an item's own (a column's overhead line, a pipe of a network) belongs to that
            // item: the window selects and edits the item when the line is clicked
            var owners = new Dictionary<string, string>();
            foreach (var item in model.Equipment)
                if (item.Tag != null && !owners.ContainsKey(item.Tag)) owners[item.Tag] = item.Name;
            var lines = new JArray();
            foreach (var run in model.Pipes.Where(r => r.Path.Count > 1))
                lines.Add(new JObject
                {
                    ["id"] = run.Name,
                    ["owner"] = run.Name == null && run.From == run.To && run.From != null && owners.TryGetValue(run.From, out var owner) ? owner : null,
                    ["tag"] = run.Tag,
                    ["desc"] = "Line " + run.Tag + " (" + run.Size + ", " + (run.Phase ?? "").ToLowerInvariant() + "), " + run.From + " to " + run.To,
                    ["c"] = run.Color,
                    ["od"] = run.OuterDiameter,
                    ["ins"] = run.Insulation,
                    ["v"] = Math.Round(run.Velocity, 3),
                    ["phase"] = run.Phase,
                    ["from"] = run.From,
                    ["to"] = run.To,
                    ["path"] = new JArray(run.Path.Select(WebViewer.V)),
                    ["fl"] = new JArray(run.Flanges.Select(f => new JArray(WebViewer.V(f.Item1), WebViewer.V(f.Item2)))),
                    ["props"] = WebViewer.Props(run.Properties)
                });

            var scene = new JObject
            {
                ["name"] = model.Name,
                ["items"] = items,
                ["lines"] = lines,
                ["rack"] = null,
                ["warnings"] = new JArray(model.Warnings)
            };
            if (fs != null)
            {
                var site = SiteLayout.LoadSite(fs);
                scene["site"] = new JObject { ["adopted"] = site.Adopted, ["budget"] = SiteCatalog.Budget(site.Items, SiteLayout.LoadTerrain(fs)) };
                var terrain = SiteLayout.LoadTerrain(fs);
                scene["terrain"] = terrain == null ? null : JObject.FromObject(terrain);
            }
            if (model.Rack != null)
                scene["rack"] = new JObject
                {
                    ["x0"] = model.Rack.X0, ["x1"] = model.Rack.X1, ["y0"] = model.Rack.Y0, ["w"] = model.Rack.Width,
                    ["top"] = model.Rack.Top, ["bay"] = PipeRack.BayLength, ["beam"] = PipeRack.BeamHeight
                };
            // the posts under the lines that run overhead off the rack: [x, y, z of the lowest pipe bottom, dx, dy, crossbar width]
            if (model.Supports.Count > 0)
            {
                if (scene["rack"] == null || scene["rack"].Type == JTokenType.Null) scene["rack"] = new JObject();
                scene["rack"]["supports"] = new JArray(model.Supports.Select(s => new JArray(
                    Math.Round(s.Item1.X), Math.Round(s.Item1.Y), Math.Round(s.Item1.Z), Math.Round(s.Item2.X, 3), Math.Round(s.Item2.Y, 3), Math.Round(s.Item2.Z))));
            }
            return scene;
        }

        /// <summary>
        /// The effects of an item: the builder marks fired heaters; a unit operation of an extension
        /// whose name says flare or cooling tower gets a flame or a plume.
        /// </summary>
        static string StyleOf(EquipmentItem item)
        {
            if (!string.IsNullOrEmpty(item.Style)) return item.Style;
            var d = ((item.Description ?? "") + " " + (item.Tag ?? "")).ToLowerInvariant();
            if (d.Contains("flare")) return "flare";
            if (d.Contains("cooling tower")) return "coolingtower";
            return null;
        }

        /// <summary>
        /// The values of the scene without its geometry, for a running dynamic simulation: the
        /// operating data of each item, the data and velocity of each line, the energy flows, the
        /// gauges in alarm with the objects they measure, and the objects with an error. The window
        /// merges the properties into what it has.
        /// </summary>
        public static JObject Values(IFlowsheet fs, JObject scene)
        {
            var items = new JObject();
            var lines = new JObject();
            var warnings = new List<string>();
            var itemIds = new HashSet<string>((scene?["items"] as JArray ?? new JArray()).Select(i => (string)i["id"]));
            var lineIds = new HashSet<string>((scene?["lines"] as JArray ?? new JArray()).Select(l => (string)l["id"]).Where(id => id != null));
            foreach (var obj in fs.SimulationObjects.Values)
            {
                var go = obj.GraphicObject;
                if (go == null) continue;
                try
                {
                    if (lineIds.Contains(obj.Name) && obj is IMaterialStream ms)
                    {
                        var run = PlantModelBuilder.SizePipe(ms, go.Tag);
                        lines[obj.Name] = new JObject { ["props"] = WebViewer.Props(run.Properties), ["v"] = Math.Round(run.Velocity, 3), ["phase"] = run.Phase, ["c"] = run.Color };
                    }
                    else if (itemIds.Contains(obj.Name))
                    {
                        if (obj is IEnergyStream es)
                        {
                            items[obj.Name] = new JObject { ["props"] = new JObject { ["Energy flow (kW)"] = Math.Round(es.GetEnergyFlow(), 3).ToString(System.Globalization.CultureInfo.InvariantCulture) } };
                        }
                        else if (!IsBadge(go.ObjectType))
                        {
                            var item = PlantModelBuilder.CreateItem(obj, go.ObjectType, warnings);
                            item.Tag = go.Tag;
                            PlantModelBuilder.AddOperatingData(item, obj);
                            items[obj.Name] = new JObject { ["props"] = WebViewer.Props(item.Properties) };
                        }
                    }
                }
                catch (Exception)
                {
                    // an object that cannot give its values now keeps the ones the window has
                }
            }
            var message = SceneProtocol.Message(SceneProtocol.Values);
            message["items"] = items;
            message["lines"] = lines;
            message["alarms"] = new JArray(Alarms(fs));
            message["errors"] = new JArray(fs.SimulationObjects.Values.Where(o => !string.IsNullOrEmpty(o.ErrorMessage)).Select(o => o.Name));
            return message;
        }

        /// <summary>The gauges with an active alarm and the objects they measure.</summary>
        public static List<string> Alarms(IFlowsheet fs)
        {
            var ids = new List<string>();
            foreach (var obj in fs.SimulationObjects.Values)
            {
                var g = obj as IIndicator;
                if (g == null || !(g.VeryLowAlarmActive || g.LowAlarmActive || g.HighAlarmActive || g.VeryHighAlarmActive)) continue;
                ids.Add(obj.Name);
                if (!string.IsNullOrEmpty(g.SelectedObjectID)) ids.Add(g.SelectedObjectID);
            }
            return ids.Distinct().ToList();
        }

        /// <summary>
        /// The objects of the site (see <see cref="SiteCatalog"/>) as items without solids: "site"
        /// (the kind), "cat", "shape", and "points", "variant" and "scale" when they have them. The
        /// window draws them, and they move, turn and go like any item. The scene also says whether
        /// the site was adopted ("site") and carries the relief of the ground ("terrain").
        /// </summary>
        static void AddSite(JArray items, HashSet<string> ids, IFlowsheet fs)
        {
            foreach (var b in SiteLayout.Load(fs))
            {
                if (b?.Id == null) continue;
                var e = SiteCatalog.Find(b.Kind);
                var item = new JObject
                {
                    ["id"] = Unique(ids, b.Id),
                    ["tag"] = SiteLayout.NameOf(b.Kind),
                    ["desc"] = SiteLayout.NameOf(b.Kind) + " (site)",
                    ["type"] = "Site",
                    ["site"] = b.Kind,
                    ["o"] = WebViewer.V(new Vec3(b.X, b.Y, b.Z)),
                    ["rot"] = b.Rot,
                    ["placed"] = true,
                    ["inst"] = false,
                    ["label"] = SiteLayout.NameOf(b.Kind),
                    ["props"] = new JObject(),
                    ["parts"] = new JArray(),
                    ["ports"] = new JArray(),
                    ["cat"] = e?.Category,
                    ["shape"] = (e?.Shape ?? SiteCatalog.Shape.Point).ToString().ToLowerInvariant()
                };
                if (b.Points != null) item["points"] = new JArray(b.Points.Select(p => new JArray(p[0], p[1])));
                if (b.Variant != null) item["variant"] = b.Variant;
                if (b.Scale != 1.0) item["scale"] = b.Scale;
                items.Add(item);
            }
        }

        /// <summary>The height a badge floats at when it has no place of its own, mm.</summary>
        public const double BadgeHeight = 3000;

        /// <summary>
        /// Whether an object is a logical block: no body in the plant, a badge in the 3D view
        /// (adjust, specification, energy recycle, controllers, switches, inputs, energy mixers).
        /// </summary>
        public static bool IsBadge(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType t)
        {
            switch (t)
            {
                case DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.OT_Adjust:
                case DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.OT_Spec:
                case DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.OT_EnergyRecycle:
                case DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.Switch:
                case DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.Input:
                case DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.NodeEn:
                case DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.EnergyMixer:
                    return true;
            }
            return t.ToString().StartsWith("Controller_");
        }

        /// <summary>
        /// Whether a badge floats: the logical blocks do, the controllers do not, since a controller
        /// is a cabinet that stands on the ground.
        /// </summary>
        public static bool Floats(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType t) => IsBadge(t) && !t.ToString().StartsWith("Controller_");

        /// <summary>
        /// The logical blocks as items without solids ("badge": true), where the saved layout puts
        /// them, or over their place on the flowsheet drawing.
        /// </summary>
        static void AddBadges(JArray items, HashSet<string> ids, PlantModel model, IFlowsheet fs)
        {
            var layout = PlantLayout.Load(fs);
            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.GraphicObject?.Tag))
            {
                var go = obj.GraphicObject;
                if (go == null || !IsBadge(go.ObjectType)) continue;
                Vec3 origin;
                Placement placed;
                var isPlaced = layout.TryGetValue(obj.Name, out placed);
                if (isPlaced) origin = new Vec3(placed.X, placed.Y, placed.Z);
                else
                {
                    var w = PlantModelBuilder.ToWorld(model, go.X + go.Width / 2.0, go.Y + go.Height / 2.0);
                    origin = new Vec3(w.X, w.Y, Floats(go.ObjectType) ? BadgeHeight : 0);
                }
                string desc;
                try { desc = obj.GetDisplayName(); } catch (Exception) { desc = go.ObjectType.ToString(); }
                items.Add(new JObject
                {
                    ["id"] = Unique(ids, obj.Name),
                    ["tag"] = go.Tag,
                    ["desc"] = desc,
                    ["type"] = go.ObjectType.ToString(),
                    ["o"] = WebViewer.V(origin),
                    ["rot"] = 0,
                    ["placed"] = isPlaced,
                    ["inst"] = false,
                    ["badge"] = true,
                    ["label"] = go.Tag,
                    ["props"] = new JObject(),
                    ["parts"] = new JArray(),
                    ["ports"] = new JArray()
                });
            }
        }

        /// <summary>
        /// The nozzles of an item, where its streams leave it: "k" (in, out, ein or eout: material
        /// or energy, into or out of the item), "i" (the connector of the 2D icon; -1 for the energy
        /// connector), "p" (world, mm), "d" (outward direction) and "used" (a stream is on it).
        /// </summary>
        static JArray Ports(EquipmentItem item, IFlowsheet fs)
        {
            var ports = new JArray();
            IGraphicObject go;
            if (fs == null || item.Name == null || !fs.GraphicObjects.TryGetValue(item.Name, out go) || go == null) return ports;
            Add(ports, item, go, go.InputConnectors, false);
            Add(ports, item, go, go.OutputConnectors, true);
            var ec = go.EnergyConnector;
            if (ec != null && ec.Active)
            {
                Vec3 at, dir;
                PlantModelBuilder.Nozzle(item, go, ec, 0, 1, true, out at, out dir);
                ports.Add(Port("eout", -1, at, dir, ec.IsAttached));
            }
            return ports;
        }

        static void Add(JArray ports, EquipmentItem item, IGraphicObject go, List<IConnectionPoint> list, bool output)
        {
            if (list == null) return;
            for (var i = 0; i < list.Count; i++)
            {
                var cp = list[i];
                if (cp == null) continue;
                var energy = cp.Type == DWSIM.Interfaces.Enums.GraphicObjects.ConType.ConEn;
                Vec3 at, dir;
                PlantModelBuilder.Nozzle(item, go, cp, i, list.Count, output, out at, out dir);
                ports.Add(Port((energy ? "e" : "") + (output ? "out" : "in"), i, at, dir, cp.IsAttached));
            }
        }

        static JObject Port(string kind, int index, Vec3 at, Vec3 dir, bool used) => new JObject
        {
            ["k"] = kind,
            ["i"] = index,
            ["p"] = WebViewer.V(at),
            ["d"] = WebViewer.V(dir),
            ["used"] = used
        };

        /// <summary>
        /// The energy streams, as items without solids ("energy": true): the window draws a bolt
        /// where the stream is, and "links" are the nozzles it joins. The place is the saved one, or
        /// above the middle of its ends, or beside its only end.
        /// </summary>
        static void AddEnergyStreams(JArray items, HashSet<string> ids, PlantModel model, IFlowsheet fs)
        {
            var layout = PlantLayout.Load(fs);
            var equipment = model.Equipment.Where(e => e.Name != null).GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First());
            var badges = items.OfType<JObject>().Where(i => (bool?)i["badge"] == true).ToDictionary(i => (string)i["id"], i => i["o"]);
            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.GraphicObject?.Tag))
            {
                var es = obj.GraphicObject;
                if (es == null || es.ObjectType != DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.EnergyStream) continue;
                var ends = new List<Vec3>();
                var source = es.InputConnectors.Count > 0 && es.InputConnectors[0].IsAttached ? es.InputConnectors[0].AttachedConnector?.AttachedFrom : null;
                var sink = es.OutputConnectors.Count > 0 && es.OutputConnectors[0].IsAttached ? es.OutputConnectors[0].AttachedConnector?.AttachedTo : null;
                Vec3 end;
                if (End(source, es, true, equipment, badges, out end)) ends.Add(end);
                if (End(sink, es, false, equipment, badges, out end)) ends.Add(end);

                Vec3 origin;
                Placement placed;
                var isPlaced = layout.TryGetValue(obj.Name, out placed);
                if (isPlaced) origin = new Vec3(placed.X, placed.Y, placed.Z);
                else if (ends.Count == 2) origin = new Vec3((ends[0].X + ends[1].X) / 2, (ends[0].Y + ends[1].Y) / 2, Math.Max(ends[0].Z, ends[1].Z) + 1500);
                else if (ends.Count == 1) origin = new Vec3(ends[0].X + (source != null ? 2500 : -2500), ends[0].Y, ends[0].Z + 1000);
                else
                {
                    var w = PlantModelBuilder.ToWorld(model, es.X + es.Width / 2.0, es.Y + es.Height / 2.0);
                    origin = new Vec3(w.X, w.Y, BadgeHeight);
                }

                var props = new JObject();
                var e = obj as IEnergyStream;
                if (e != null)
                    try { props["Energy flow (kW)"] = Math.Round(e.GetEnergyFlow(), 3).ToString(System.Globalization.CultureInfo.InvariantCulture); }
                    catch (Exception) { }
                items.Add(new JObject
                {
                    ["id"] = Unique(ids, obj.Name),
                    ["tag"] = es.Tag,
                    ["desc"] = "Energy stream" + (source != null ? " from " + source.Tag : "") + (sink != null ? " to " + sink.Tag : ""),
                    ["type"] = "EnergyStream",
                    ["o"] = WebViewer.V(origin),
                    ["rot"] = 0,
                    ["placed"] = isPlaced,
                    ["inst"] = false,
                    ["badge"] = true,
                    ["energy"] = true,
                    ["links"] = new JArray(ends.Select(WebViewer.V)),
                    ["label"] = es.Tag,
                    ["props"] = props,
                    ["parts"] = new JArray(),
                    ["ports"] = new JArray()
                });
            }
        }

        /// <summary>The world point of the nozzle of a unit that an energy stream is on (or the badge of a logical block).</summary>
        static bool End(IGraphicObject unit, IGraphicObject es, bool unitIsSource, Dictionary<string, EquipmentItem> equipment, Dictionary<string, JToken> badges, out Vec3 point)
        {
            point = new Vec3(0, 0, 0);
            if (unit == null) return false;
            EquipmentItem item;
            if (equipment.TryGetValue(unit.Name, out item))
            {
                var list = unitIsSource ? unit.OutputConnectors : unit.InputConnectors;
                for (var i = 0; i < list.Count; i++)
                {
                    var cp = list[i];
                    if (cp == null || !cp.IsAttached || cp.AttachedConnector == null) continue;
                    var other = unitIsSource ? cp.AttachedConnector.AttachedTo : cp.AttachedConnector.AttachedFrom;
                    if (other != es) continue;
                    Vec3 dir;
                    PlantModelBuilder.Nozzle(item, unit, cp, i, list.Count, unitIsSource, out point, out dir);
                    return true;
                }
                var ec = unit.EnergyConnector;
                if (unitIsSource && ec != null && ec.IsAttached && ec.AttachedConnector?.AttachedTo == es)
                {
                    Vec3 dir;
                    PlantModelBuilder.Nozzle(item, unit, ec, 0, 1, true, out point, out dir);
                    return true;
                }
                point = item.Origin;
                return true;
            }
            JToken o;
            if (badges.TryGetValue(unit.Name, out o))
            {
                point = new Vec3((double)o[0], (double)o[1], (double)o[2]);
                return true;
            }
            return false;
        }

        /// <summary>
        /// The canvas point of a plot plan point (mm), the inverse of the builder's own map: through
        /// the columns and rows of a packed layout, or through the scale of a scaled one.
        /// </summary>
        public static void ToCanvas(PlantModel model, double x, double y, out double cx, out double cy)
        {
            if (model.MapCanvasX != null && model.MapCanvasX.Length > 0)
            {
                cx = Unmap(model.MapCanvasX, model.MapWorldX, model.MapHalfX, x);
                cy = Unmap(model.MapCanvasY, model.MapWorldY, model.MapHalfY, -y);
                return;
            }
            var scale = model.Scale > 0 ? model.Scale : 15;
            cx = x / scale + model.CanvasOriginX;
            cy = -y / scale + model.CanvasOriginY;
        }

        /// <summary>The inverse of PlantModelBuilder.Map, with the same 15 mm per canvas unit past the outer columns.</summary>
        static double Unmap(double[] canvas, double[] world, double[] half, double w)
        {
            const double beyond = 15;
            var n = canvas.Length;
            var low = world[0] - half[0];
            var high = world[n - 1] + half[n - 1];
            if (w <= low) return canvas[0] - (low - w) / beyond;
            if (w >= high) return canvas[n - 1] + (w - high) / beyond;
            if (w <= world[0]) return canvas[0];
            for (var k = 1; k < n; k++)
                if (w <= world[k])
                {
                    var span = world[k] - world[k - 1];
                    return span > 0 ? canvas[k - 1] + (w - world[k - 1]) / span * (canvas[k] - canvas[k - 1]) : canvas[k];
                }
            return canvas[n - 1];
        }

        static string NameOf(IFlowsheet fs) => PlantModelExtender.ModelName(fs);

        static string TypeOf(IFlowsheet fs, string name)
        {
            if (fs == null || name == null) return null;
            IGraphicObject go;
            return fs.GraphicObjects.TryGetValue(name, out go) && go != null ? go.ObjectType.ToString() : null;
        }

        static string Unique(HashSet<string> ids, string id)
        {
            var candidate = id;
            for (var k = 2; !ids.Add(candidate); k++) candidate = id + "#" + k;
            return candidate;
        }
    }
}
