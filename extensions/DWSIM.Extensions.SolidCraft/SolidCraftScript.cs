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
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// Writes a <see cref="PlantModel"/> as a SolidCraft command script: one component per equipment
    /// item and one per line, named after the tag and carrying the operating conditions as
    /// properties; <see cref="CreateLabels"/> adds a 3D label over each. The script is what
    /// `solidcraft-cli run` replays.
    /// </summary>
    public static class SolidCraftScript
    {
        public class Command
        {
            public string Name;
            public JObject Params;

            /// <summary>
            /// What the command draws, for the report when it has to be left out. Commands of a
            /// line, a label or a flow marker share it and are left out together.
            /// </summary>
            public string Subject;
        }

        public static string LineComponent(PipeRun run) => "Line " + run.Tag;

        public static List<Command> Create(PlantModel model)
        {
            var cmds = new List<Command>();
            var sketches = new List<string>();

            Add(cmds, "document.metadata.set", "the design properties", new JObject
            {
                ["values"] = new JObject
                {
                    ["org.dwsim.flowsheet"] = new JObject
                    {
                        ["name"] = model.Name,
                        ["scale_mm_per_canvas_unit"] = Math.Round(model.Scale, 2),
                        // where each item was put: moving an item in SolidCraft changes its
                        // occurrence's transform from here, which the next export reads back
                        ["placements"] = new JObject(model.Equipment.Where(e => e.Visible && !e.IsInstrument).Select(e =>
                            new JProperty(e.Tag, new JObject
                            {
                                ["x"] = Math.Round(e.Origin.X),
                                ["y"] = Math.Round(e.Origin.Y),
                                ["z"] = Math.Round(e.Origin.Z),
                                ["rot"] = e.Rotation
                            }))),
                        // the size each line is drawn at, so a data update can tell when the
                        // flow now asks for another one
                        ["line_sizes"] = new JObject(model.Pipes.Where(r => !string.IsNullOrEmpty(r.Tag)).GroupBy(r => r.Tag).Select(g => new JProperty(g.Key, g.First().Size)))
                    }
                }
            });

            foreach (var item in model.Equipment.Where(e => e.Visible))
            {
                var subject = item.Tag;
                // not grounded, so it can be moved in SolidCraft and the move read back
                Add(cmds, "component.create", subject, new JObject { ["name"] = item.Tag, ["parent"] = "root", ["grounded"] = false });

                foreach (var part in item.Parts)
                {
                    var body = item.Tag + " " + part.Name;
                    var p = part.Moved(item.Origin);
                    EquipmentTemplates.ApplyFinish(p);
                    switch (p.Kind)
                    {
                        case PrimitiveKind.Cylinder:
                            if (!(p.Diameter > 0 && p.Height > 0)) continue;
                            Add(cmds, "solid.cylinder", subject, new JObject
                            {
                                ["diameter"] = Round(p.Diameter),
                                ["height"] = Round(p.Height),
                                ["base"] = new JArray(p.Point.ToArray()),
                                ["axis"] = p.AxisVector.HasValue ? (JToken)Dir(p.AxisVector.Value) : p.Axis,
                                ["body_name"] = body
                            });
                            break;
                        case PrimitiveKind.Box:
                            if (!(p.SizeX > 0 && p.SizeY > 0 && p.SizeZ > 0)) continue;
                            Add(cmds, "solid.box", subject, new JObject
                            {
                                ["length"] = Round(p.SizeX),
                                ["width"] = Round(p.SizeY),
                                ["height"] = Round(p.SizeZ),
                                ["center"] = new JArray(p.Point.ToArray()),
                                ["body_name"] = body
                            });
                            break;
                        case PrimitiveKind.Sphere:
                            if (!(p.Diameter > 0)) continue;
                            Add(cmds, "solid.sphere", subject, new JObject
                            {
                                ["diameter"] = Round(p.Diameter),
                                ["center"] = new JArray(p.Point.ToArray()),
                                ["body_name"] = body
                            });
                            break;
                        case PrimitiveKind.Head:
                            if (!(p.Diameter > 0 && p.Depth > 0) || !p.AxisVector.HasValue) continue;
                            {
                                // a quarter ellipse in a plane through the axis, turned about it: a
                                // scaled sphere would not go into the STEP file
                                var axis = p.AxisVector.Value;
                                var across = Math.Abs(axis.Z) > 0.5 ? Vec3.PlusX : new Vec3(-axis.Y, axis.X, 0);
                                var headSketch = "Head " + item.Tag + " " + part.Name;
                                var r = Round(p.Diameter / 2);
                                Add(cmds, "sketch.create", subject, new JObject
                                {
                                    ["name"] = headSketch,
                                    ["plane"] = new JObject { ["origin"] = new JArray(p.Point.ToArray()), ["x_dir"] = Dir(across), ["y_dir"] = Dir(axis) }
                                });
                                Add(cmds, "sketch.ellipse", subject, new JObject { ["center"] = new JArray(0, 0), ["major"] = new JArray(r, 0), ["minor_radius"] = Round(p.Depth) });
                                Add(cmds, "sketch.line", subject, new JObject { ["points"] = new JArray(new JArray(0, 0), new JArray(r, 0)) });
                                Add(cmds, "sketch.line", subject, new JObject { ["points"] = new JArray(new JArray(0, 0), new JArray(0, Round(p.Depth))) });
                                Add(cmds, "sketch.finish", subject, new JObject());
                                Add(cmds, "solid.revolve", subject, new JObject
                                {
                                    ["sketch"] = headSketch,
                                    ["profiles"] = new JArray(new JObject { ["point"] = new JArray(Round(r / 3), Round(p.Depth / 3)) }),
                                    ["axis"] = "y",
                                    ["angle"] = 360,
                                    ["body_name"] = body
                                });
                                sketches.Add(headSketch);
                            }
                            break;
                        case PrimitiveKind.Torus:
                            if (!(p.Major > p.Minor && p.Minor > 0)) continue;
                            Add(cmds, "solid.torus", subject, new JObject
                            {
                                ["major"] = Round(p.Major),
                                ["minor"] = Round(p.Minor),
                                ["center"] = new JArray(p.Point.ToArray()),
                                ["body_name"] = body
                            });
                            break;
                    }
                    Add(cmds, "appearance.assign", subject, Look(body, p));
                    if (p.Material != null)
                        Add(cmds, "material.assign", subject, new JObject { ["bodies"] = new JArray(body), ["material"] = p.Material });
                }

                var props = new JObject { ["DWSIM object"] = item.Description };
                foreach (var kv in item.Properties) props[kv.Key] = kv.Value;
                Add(cmds, "component.metadata.set", subject, new JObject
                {
                    ["component"] = item.Tag,
                    ["description"] = item.Description,
                    ["properties"] = props
                });
            }

            if (model.Rack != null) AddRack(cmds, model.Rack, sketches);

            var n = 0;
            foreach (var run in model.Pipes)
            {
                n++;
                var subject = "line " + run.Tag;
                var component = LineComponent(run);
                var sketch = "Route " + n + " " + run.Tag;
                var body = run.Tag + " " + run.Size;

                Add(cmds, "component.create", subject, new JObject { ["name"] = component, ["parent"] = "root", ["grounded"] = true });
                Add(cmds, "sketch.create", subject, new JObject { ["plane"] = "XY", ["name"] = sketch });
                Add(cmds, "sketch.options", subject, new JObject { ["sketch_3d"] = true });
                var elbows = AddRoute(cmds, subject, run, out var straights);
                Add(cmds, "sketch.finish", subject, new JObject());
                sketches.Add(sketch);

                // each straight run its own pipe, each bend its own elbow: a sweep through a bend
                // comes out as thousands of spline patches (10 MB of STEP for two bends), a revolved
                // elbow as an exact torus
                var bodies = new JArray();
                for (var k = 1; k <= straights; k++)
                {
                    var piece = body + " " + k;
                    Add(cmds, "solid.pipe", subject, new JObject
                    {
                        ["path_sketch"] = sketch,
                        ["path"] = new JArray("w" + k),
                        ["diameter"] = run.OuterDiameter,
                        ["wall"] = run.Wall,
                        ["body_name"] = piece
                    });
                    bodies.Add(piece);
                }
                for (var k = 0; k < elbows.Count; k++)
                {
                    var name = "Elbow " + n + "." + (k + 1) + " " + run.Tag;
                    var piece = body + " elbow " + (k + 1);
                    AddElbow(cmds, subject, run, elbows[k], name, piece);
                    sketches.Add(name);
                    bodies.Add(piece);
                }

                // painted carbon steel: the colour tells the phase
                var look = Look(body, new Primitive { Color = run.Color, Metallic = true, Look = "" });
                look["bodies"] = bodies;
                Add(cmds, "appearance.assign", subject, look);
                Add(cmds, "material.assign", subject, new JObject { ["bodies"] = bodies.DeepClone(), ["material"] = "Steel" });

                for (var f = 0; f < run.Flanges.Count; f++)
                    AddFlange(cmds, subject, run, run.Flanges[f].Item1, run.Flanges[f].Item2, body + " flange " + (f + 1));

                // its own subject: a jacket SolidCraft cannot make is left out without the line
                if (run.Insulation > 0) AddInsulation(cmds, "insulation " + run.Tag, run, sketch, straights, elbows, n, body, sketches);

                var props = new JObject();
                foreach (var kv in run.Properties) props[kv.Key] = kv.Value;
                Add(cmds, "component.metadata.set", subject, new JObject
                {
                    ["component"] = component,
                    ["description"] = "Line " + run.Tag + " (" + run.Size + ", " + run.Phase.ToLowerInvariant() + ")",
                    ["properties"] = props
                });
            }

            // the route sketches stay in the design, to edit a line, but hidden
            if (sketches.Count > 0)
                Add(cmds, "browser.visibility", "the sketches", new JObject { ["visible"] = false, ["items"] = new JArray(sketches) });

            return cmds;
        }

        public const string SizeForFlow = "Size for the current flow";

        /// <summary>
        /// The operating data of every item and line as component properties: what a data update
        /// sends to the open model, without building anything again. A line whose flow now asks for
        /// another size than the one it is drawn at (<paramref name="drawnSizes"/>, by tag) keeps the
        /// drawn size in its properties and label, gets the new one as <see cref="SizeForFlow"/>, and
        /// is listed in <paramref name="resized"/> as "tag (drawn, new)".
        /// </summary>
        public static List<Command> DataCommands(PlantModel model, IDictionary<string, string> drawnSizes = null, List<string> resized = null)
        {
            var cmds = new List<Command>();
            foreach (var item in model.Equipment.Where(e => e.Visible))
            {
                var props = new JObject { ["DWSIM object"] = item.Description };
                foreach (var kv in item.Properties) props[kv.Key] = kv.Value;
                Add(cmds, "component.metadata.set", item.Tag, new JObject { ["component"] = item.Tag, ["description"] = item.Description, ["properties"] = props });
            }
            var lineLabels = new Dictionary<PipeRun, string>();
            foreach (var run in model.Pipes)
            {
                var props = new JObject();
                foreach (var kv in run.Properties) props[kv.Key] = kv.Value;
                // a compound the line no longer shows: null takes its old value off the component
                foreach (var c in model.Compounds.Concat(new[] { PipeRun.OtherCompounds }))
                    if (!run.Properties.ContainsKey(PipeRun.CompositionKey(c))) props[PipeRun.CompositionKey(c)] = JValue.CreateNull();
                var size = run.Size;
                var label = run.Label;
                if (drawnSizes != null && run.Tag != null && drawnSizes.TryGetValue(run.Tag, out var drawn) && !string.IsNullOrEmpty(drawn) && drawn != run.Size)
                {
                    // the pipe stays at its drawn size until the model is exported again
                    size = drawn;
                    props["Size"] = drawn;
                    props[SizeForFlow] = run.Size;
                    if (label != null && label.StartsWith(run.Tag + " " + run.Size)) label = run.Tag + " " + drawn + label.Substring((run.Tag + " " + run.Size).Length);
                    resized?.Add(run.Tag + " (" + drawn + " drawn, " + run.Size + " for the current flow)");
                }
                else props[SizeForFlow] = JValue.CreateNull();
                lineLabels[run] = label;
                Add(cmds, "component.metadata.set", "line " + run.Tag, new JObject
                {
                    ["component"] = LineComponent(run),
                    ["description"] = "Line " + run.Tag + " (" + size + ", " + run.Phase.ToLowerInvariant() + ")",
                    ["properties"] = props
                });
            }

            // the labels' words, all in one change so the model rebuilds once; the design may hold
            // only some of them (see KeepLabels)
            var parameters = LabelParameters(model);
            var changes = new JObject();
            foreach (var item in model.Equipment.Where(e => e.Visible && !string.IsNullOrEmpty(e.Label)))
                changes[parameters[ItemKey(item)]] = item.Label;
            foreach (var run in model.Pipes.Where(r => !string.IsNullOrEmpty(lineLabels[r])))
                changes[parameters[LineKey(run)]] = lineLabels[run];
            if (changes.Count > 0) Add(cmds, "parameters.change", LabelsSubject, new JObject { ["changes"] = changes });
            return cmds;
        }

        /// <summary>The subject of the data update's command that rewrites the labels.</summary>
        public const string LabelsSubject = "the labels";

        static string ItemKey(EquipmentItem item) => "item " + item.Tag;

        static string LineKey(PipeRun run) => "line " + run.Tag;

        /// <summary>
        /// The text parameter that holds each label's words, by item and by line: "label_" and the
        /// tag with what a parameter name can't hold turned into underscores, numbered when two tags
        /// come out the same. The export and the data update both take the names from here.
        /// </summary>
        static Dictionary<string, string> LabelParameters(PlantModel model)
        {
            var names = new Dictionary<string, string>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Name(string key, string prefix, string tag)
            {
                var stem = prefix + Regex.Replace(tag ?? "", "[^A-Za-z0-9_]", "_");
                var name = stem;
                for (var i = 2; !used.Add(name); i++) name = stem + "_" + i;
                names[key] = name;
            }
            foreach (var item in model.Equipment.Where(e => e.Visible)) Name(ItemKey(item), "label_", item.Tag);
            foreach (var run in model.Pipes) Name(LineKey(run), "label_line_", run.Tag);
            return names;
        }

        /// <summary>
        /// Keeps in the data update only the labels the design has a parameter for: a design exported
        /// before the labels followed parameters has none, and a line whose label did not fit has none
        /// either. Returns how many labels are left to rewrite.
        /// </summary>
        public static int KeepLabels(List<Command> cmds, ICollection<string> parameters)
        {
            var c = cmds.FirstOrDefault(x => x.Subject == LabelsSubject);
            if (c == null) return 0;
            var changes = (JObject)c.Params["changes"];
            foreach (var p in changes.Properties().ToList())
                if (!parameters.Contains(p.Name)) p.Remove();
            if (changes.Count == 0) cmds.Remove(c);
            return changes.Count;
        }

        /// <summary>The names of the text parameters among a parameters.list reply.</summary>
        public static HashSet<string> TextParameters(JToken list)
        {
            var names = new HashSet<string>();
            foreach (var p in (list?["parameters"] as JArray) ?? new JArray())
                if ((string)p["unit"] == "text" && (string)p["name"] is string n) names.Add(n);
            return names;
        }

        public const string RenderSetup = "DWSIM Plant";

        /// <summary>
        /// A render setup for a picture of the whole plant: path tracing on the GPU under a sky
        /// environment, the camera at the view chosen in the options, fitted to the model.
        /// </summary>
        public static List<Command> CreateRenderSetup(PlantModel model) => CreateRenderSetup(model, model.Options?.RenderSamples ?? 0);

        /// <summary>The render setup with <paramref name="samples"/> samples per pixel (0: SolidCraft's own).</summary>
        public static List<Command> CreateRenderSetup(PlantModel model, int samples)
        {
            var cmds = new List<Command>();
            var camera = RenderCamera(model, model.Options?.RenderView);
            if (camera == null) return cmds;
            // the samples belong to the saved setup: the render command refuses --samples next to --setup.
            // The light comes from SolidCraft's procedural sky, so the metals have something to reflect
            // even when the build doesn't carry the HDR environments of the render library.
            var jobOptions = new JObject
            {
                ["environment"] = new JObject
                {
                    ["kind"] = "sky",
                    ["gradient"] = new JObject
                    {
                        ["zenith"] = new JArray(0.16, 0.32, 0.7),
                        ["horizon"] = new JArray(0.8, 0.9, 1.0),
                        ["ground"] = new JArray(0.12, 0.1, 0.08)
                    },
                    ["rotation_rad"] = 0,
                    ["intensity"] = 1
                }
            };
            if (samples > 0) jobOptions["samples"] = samples;
            Add(cmds, "render.settings.set", "the render", new JObject
            {
                ["name"] = RenderSetup,
                ["set"] = new JObject { ["job"] = new JObject { ["method"] = "gpu_path", ["options"] = jobOptions }, ["camera"] = camera }
            });
            return cmds;
        }

        /// <summary>
        /// The camera of a view (<see cref="CadTools.RenderViews"/>) that frames the plant with its labels
        /// in the picture: the plant's corners are projected on the picture plane, the target goes to
        /// the middle of what they cover, and the camera comes as close as it can while every corner
        /// stays inside the picture's width and height. Null for a model with nothing in it.
        /// </summary>
        public static JObject RenderCamera(PlantModel model, string view)
        {
            var pts = new List<Vec3>();
            void Box(Vec3 lo, Vec3 hi)
            {
                foreach (var x in new[] { lo.X, hi.X })
                    foreach (var y in new[] { lo.Y, hi.Y })
                        foreach (var z in new[] { lo.Z, hi.Z })
                            pts.Add(new Vec3(x, y, z));
            }
            foreach (var e in model.Equipment.Where(e => e.Visible))
            {
                Box(e.FootprintCentre + new Vec3(-e.FootprintX, -e.FootprintY, 0), e.FootprintCentre + new Vec3(e.FootprintX, e.FootprintY, e.Envelope.ZTop));
                if (model.Options.Labels && !string.IsNullOrEmpty(e.Label))
                {
                    // the label over the item (see CreateLabels)
                    var height = Clamp(0.25 * 2 * e.Envelope.HalfX, 150, 500);
                    var top = e.Envelope.ZTop + (e.IsInstrument ? 250 : 1500) + (e.Label.Split('\n').Length + 0.5) * 1.6 * height;
                    var half = e.Label.Split('\n').Max(l => l.Length) * 0.35 * height;
                    pts.Add(e.Origin + new Vec3(-half, 0, top));
                    pts.Add(e.Origin + new Vec3(half, 0, top));
                }
            }
            foreach (var r in model.Pipes) pts.AddRange(r.Path);
            if (model.Rack != null) Box(new Vec3(model.Rack.X0, model.Rack.Y0, 0), new Vec3(model.Rack.X1, model.Rack.Y0 + model.Rack.Width, model.Rack.Top));
            if (pts.Count == 0) return null;

            var v = CadTools.RenderViews.FirstOrDefault(x => x.Id == view);
            if (v.Id == null) v = CadTools.RenderViews[0];
            const double fov = 0.7;
            const double fill = 0.9;
            var aspect = model.Options != null && model.Options.RenderHeight > 0 ? (double)model.Options.RenderWidth / model.Options.RenderHeight : 1.6;

            // SolidCraft's camera: with yaw 0 it looks along +Y from the -Y side, Z is up, and the
            // field of view is the picture's height
            var back = new Vec3(-Math.Sin(v.Yaw) * Math.Cos(v.Pitch), -Math.Cos(v.Yaw) * Math.Cos(v.Pitch), Math.Sin(v.Pitch));
            var forward = back * -1;
            var right = new Vec3(Math.Cos(v.Yaw), -Math.Sin(v.Yaw), 0);
            var up = new Vec3(right.Y * forward.Z - right.Z * forward.Y, right.Z * forward.X - right.X * forward.Z, right.X * forward.Y - right.Y * forward.X);
            double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            var tanV = Math.Tan(fov / 2) * fill;
            var tanH = tanV * aspect;

            var target = new Vec3(pts.Average(p => p.X), pts.Average(p => p.Y), pts.Average(p => p.Z));
            // the nearest the camera can be with every corner inside the picture
            double Fit()
            {
                double nearest = 0;
                foreach (var p in pts)
                {
                    var d = p - target;
                    nearest = Math.Max(nearest, Math.Max(Math.Abs(Dot(d, right)) / tanH, Math.Abs(Dot(d, up)) / tanV) - Dot(d, forward));
                }
                return nearest;
            }
            double distance = 0;
            // the projection depends on the distance and the distance on where the target is, so
            // centre and fit a few times; it settles in two or three passes
            for (var pass = 0; pass < 4; pass++)
            {
                distance = Fit();
                double xMin = double.MaxValue, xMax = double.MinValue, yMin = double.MaxValue, yMax = double.MinValue;
                foreach (var p in pts)
                {
                    var d = p - target;
                    var depth = distance + Dot(d, forward);
                    if (depth <= 1) continue;
                    var sx = Dot(d, right) / depth;
                    var sy = Dot(d, up) / depth;
                    xMin = Math.Min(xMin, sx); xMax = Math.Max(xMax, sx);
                    yMin = Math.Min(yMin, sy); yMax = Math.Max(yMax, sy);
                }
                if (xMin > xMax) break;
                target = target + right * ((xMin + xMax) / 2 * distance) + up * ((yMin + yMax) / 2 * distance);
            }
            distance = Math.Max(Fit(), 1000);
            return new JObject { ["target"] = new JArray(target.ToArray()), ["yaw"] = v.Yaw, ["pitch"] = v.Pitch, ["distance"] = Math.Round(distance), ["fov"] = fov };
        }

        /// <summary>
        /// The 3D labels, run on the saved design after the STEP export: lettering is heavy B-rep
        /// that would bloat the STEP file, and the labels are only there for viewing. Each label goes
        /// into the component of its item or line.
        /// </summary>
        public static List<Command> CreateLabels(PlantModel model)
        {
            var cmds = new List<Command>();
            var sketches = new List<string>();
            var parameters = LabelParameters(model);

            foreach (var item in model.Equipment.Where(e => e.Visible && !string.IsNullOrEmpty(e.Label)))
            {
                // over the item and clear of its overhead line, in a vertical plane facing -Y, the
                // side the iso view looks from
                var subject = "label " + item.Tag;
                var height = Clamp(0.25 * 2 * item.Envelope.HalfX, 150, 500);
                var lines = item.Label.Split('\n').Length;
                var origin = item.Origin + new Vec3(0, 0, item.Envelope.ZTop + (item.IsInstrument ? 250 : 1500) + (lines - 1) * 1.6 * height);
                var name = "Label " + item.Tag;
                Add(cmds, "component.activate", subject, new JObject { ["component"] = item.Tag });
                AddLabel(cmds, subject, name, parameters[ItemKey(item)], item.Label, origin, Vec3.PlusX, height, item.Tag + " label");
                sketches.Add(name);
            }

            var n = 0;
            foreach (var run in model.Pipes)
            {
                n++;
                var label = LineLabel(cmds, run, n, parameters[LineKey(run)]);
                if (label != null) sketches.Add(label);
            }

            if (sketches.Count > 0)
                Add(cmds, "browser.visibility", "the label sketches", new JObject { ["visible"] = false, ["items"] = new JArray(sketches) });
            Add(cmds, "component.activate", "the design", new JObject { ["component"] = "root" });

            return cmds;
        }

        /// <summary>
        /// A label along the longest horizontal run of a line, just above the pipe, written so it
        /// reads left to right from the iso view. Null when no run is long enough to carry it.
        /// </summary>
        static string LineLabel(List<Command> cmds, PipeRun run, int n, string parameter)
        {
            if (string.IsNullOrEmpty(run.Label)) return null;

            Vec3 a = default, b = default;
            double best = 0;
            for (var i = 1; i < run.Path.Count; i++)
            {
                var d = run.Path[i] - run.Path[i - 1];
                if (Math.Abs(d.Z) > 1) continue;
                if (d.Length > best) { best = d.Length; a = run.Path[i - 1]; b = run.Path[i]; }
            }

            var height = Clamp(1.5 * run.OuterDiameter, 120, 300);
            if (best < TextWidth(run.Label, height) + 500) return null;

            var dir = (b - a).Normalized();
            if (dir.X + dir.Y < 0) dir = dir * -1;
            var mid = (a + b) * 0.5 + new Vec3(0, 0, run.OuterDiameter / 2 + 80);
            var name = "Label " + n + " " + run.Tag;
            Add(cmds, "component.activate", "label " + run.Tag, new JObject { ["component"] = LineComponent(run) });
            AddLabel(cmds, "label " + run.Tag, name, parameter, run.Label, mid, dir, height, run.Tag + " label");
            return name;
        }

        /// <summary>
        /// Lettering of 3D text in a vertical plane through <paramref name="origin"/>, centred on it.
        /// The words come from the text parameter <paramref name="parameter"/>, so changing the
        /// parameter (<see cref="DataCommands"/>) rewrites the label in place.
        /// </summary>
        static void AddLabel(List<Command> cmds, string subject, string sketch, string parameter, string text, Vec3 origin, Vec3 xDir, double height, string body)
        {
            Add(cmds, "parameters.add", subject, new JObject { ["name"] = parameter, ["expression"] = text, ["unit"] = "text" });
            Add(cmds, "sketch.create", subject, new JObject
            {
                ["name"] = sketch,
                ["plane"] = new JObject
                {
                    ["origin"] = new JArray(origin.ToArray()),
                    ["x_dir"] = new JArray(Math.Round(xDir.X, 6), Math.Round(xDir.Y, 6), 0),
                    ["y_dir"] = new JArray(0, 0, 1)
                }
            });
            Add(cmds, "sketch.text", subject, new JObject
            {
                ["text_expression"] = parameter,
                ["at"] = new JArray(0, 0),
                ["height"] = Round(height),
                ["align"] = "center"
            });
            Add(cmds, "sketch.finish", subject, new JObject());
            // flat letters: a planar face per letter is a fraction of the work of an extruded solid
            // and of its size in the design, and it reads the same in the pictures
            Add(cmds, "surface.patch", subject, new JObject { ["sketch"] = sketch, ["body_name"] = body });
        }

        /// <summary>Approximate width of the longest line of a text, for a sans-serif face.</summary>
        static double TextWidth(string text, double height) => 0.62 * height * text.Split('\n').Max(l => l.Length);

        /// <summary>A bend: where it starts, the direction it starts in, the way to its centre, its radius and angle.</summary>
        class Elbow
        {
            public Vec3 Start, Along, ToCentre;
            public double Radius, Degrees;
        }

        /// <summary>
        /// The straight runs of a line in its 3D sketch, w1 to wN in order (<paramref name="straights"/>),
        /// and the long-radius bends between them (1.5 times the outside diameter, smaller where the
        /// runs are short; a corner too tight for a bend of at least one diameter stays sharp).
        /// </summary>
        static List<Elbow> AddRoute(List<Command> cmds, string subject, PipeRun run, out int straights)
        {
            var pts = run.Path;
            var elbows = new List<Elbow>();
            var count = 0;
            var current = pts[0];

            for (var i = 1; i < pts.Count - 1; i++)
            {
                var a = (pts[i] - pts[i - 1]).Normalized();
                var b = (pts[i + 1] - pts[i]).Normalized();
                var cos = Math.Max(-1, Math.Min(1, Vec3.Dot(a, b)));
                var theta = Math.Acos(cos);
                var tanHalf = Math.Tan(theta / 2);

                // tangent length that fits: at most 45 % of each neighbouring run, the incoming one
                // counted from where the previous bend ended
                var room = Math.Min((pts[i] - current).Length, 0.45 * (pts[i + 1] - pts[i]).Length);
                var preferred = Math.Max(1.5 * run.OuterDiameter, 1.1 * (run.OuterDiameter / 2 + run.Insulation));
                var radius = Math.Min(preferred, tanHalf > 1e-6 ? room / tanHalf : 0);
                if (theta < 0.01 || radius < run.OuterDiameter)
                {
                    // no room for a bend: a sharp corner, the two runs stopping 2 mm short of it
                    // (3D lines that share an end are joined into one wire, and a pipe along a
                    // wire that is not flat fails)
                    if ((pts[i] - current).Length > 5) count += Line(cmds, subject, current, pts[i] - a * 2);
                    current = pts[i] + b * 2;
                    continue;
                }

                var t = radius * tanHalf;
                var start = pts[i] - a * t;
                var end = pts[i] + b * t;

                if ((start - current).Length > 1) count += Line(cmds, subject, current, start);
                elbows.Add(new Elbow { Start = start, Along = a, ToCentre = (b - a * cos).Normalized(), Radius = radius, Degrees = theta * 180 / Math.PI });
                current = end;
            }

            var last = pts[pts.Count - 1];
            if ((last - current).Length > 1) count += Line(cmds, subject, current, last);
            straights = count;
            return elbows;
        }

        /// <summary>
        /// An elbow: the pipe's ring section, drawn square to the line where the bend starts, turned
        /// about the bend's centre (an exact torus in the STEP file).
        /// </summary>
        static void AddElbow(List<Command> cmds, string subject, PipeRun run, Elbow e, string sketch, string body)
            => AddElbow(cmds, subject, run.OuterDiameter, run.OuterDiameter - 2 * run.Wall, e, sketch, body);

        static void AddElbow(List<Command> cmds, string subject, double outer, double inner, Elbow e, string sketch, string body)
        {
            // sketch x towards the centre and y their cross product, so the sketch faces along the line
            var y = Cross(e.Along, e.ToCentre);
            Add(cmds, "sketch.create", subject, new JObject
            {
                ["name"] = sketch,
                ["plane"] = new JObject
                {
                    ["origin"] = new JArray(e.Start.ToArray()),
                    ["x_dir"] = Dir(e.ToCentre),
                    ["y_dir"] = Dir(y)
                }
            });
            Add(cmds, "sketch.circle.center", subject, new JObject { ["center"] = new JArray(0, 0), ["diameter"] = Round(outer) });
            Add(cmds, "sketch.circle.center", subject, new JObject { ["center"] = new JArray(0, 0), ["diameter"] = Round(inner) });
            Add(cmds, "sketch.finish", subject, new JObject());
            Add(cmds, "solid.revolve", subject, new JObject
            {
                ["sketch"] = sketch,
                ["axis"] = new JObject { ["origin"] = new JArray((e.Start + e.ToCentre * e.Radius).ToArray()), ["dir"] = Dir(y) },
                ["angle"] = Math.Round(e.Degrees, 4),
                ["body_name"] = body
            });
        }

        /// <summary>
        /// The insulation of a line: a sleeve over each straight run and each bend with an aluminium
        /// cladding finish. A bend too tight for the sleeve (its radius under the sleeve's) is left bare.
        /// </summary>
        static void AddInsulation(List<Command> cmds, string subject, PipeRun run, string sketch, int straights, List<Elbow> elbows, int n, string body, List<string> sketches)
        {
            var outer = run.OuterDiameter + 2 * run.Insulation;
            var jackets = new JArray();
            for (var k = 1; k <= straights; k++)
            {
                var piece = body + " insulation " + k;
                Add(cmds, "solid.pipe", subject, new JObject
                {
                    ["path_sketch"] = sketch,
                    ["path"] = new JArray("w" + k),
                    ["diameter"] = Round(outer),
                    ["wall"] = Round(run.Insulation),
                    ["body_name"] = piece
                });
                jackets.Add(piece);
            }
            for (var k = 0; k < elbows.Count; k++)
            {
                if (elbows[k].Radius <= outer / 2 + 5) continue;
                var name = "Insulation " + n + "." + (k + 1) + " " + run.Tag;
                var piece = body + " insulation elbow " + (k + 1);
                AddElbow(cmds, subject, outer, run.OuterDiameter, elbows[k], name, piece);
                sketches.Add(name);
                jackets.Add(piece);
            }
            if (jackets.Count > 0)
                Add(cmds, "appearance.assign", subject, new JObject { ["bodies"] = jackets, ["appearance"] = "starter.metal.aluminum-bead-blast" });
        }

        static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        static JArray Dir(Vec3 d) => new JArray(Math.Round(d.X, 6), Math.Round(d.Y, 6), Math.Round(d.Z, 6));

        /// <summary>
        /// The pipe rack: a bent (two columns and a cross beam) every bay, tied by a beam along each
        /// side, all I-beams drawn as frame members on one 3D sketch.
        /// </summary>
        static void AddRack(List<Command> cmds, PipeRack rack, List<string> sketches)
        {
            const string subject = "the pipe rack", name = "Pipe Rack";
            var zb = rack.Top - PipeRack.BeamHeight / 2;
            double ya = rack.Y0, yb = rack.Y0 + rack.Width;
            var bays = Math.Max(1, (int)Math.Ceiling((rack.X1 - rack.X0) / PipeRack.BayLength));
            var bay = (rack.X1 - rack.X0) / bays;

            Add(cmds, "component.create", subject, new JObject { ["name"] = name, ["parent"] = "root", ["grounded"] = true });
            // the columns, the cross beams and the side beams, each a frame member of its standard section
            // on a 3D sketch of its own, centred on the lines
            void Members(string what, string profile, Action<Action<Vec3, Vec3>> lines)
            {
                var sketch = "Pipe Rack " + what;
                Add(cmds, "sketch.create", subject, new JObject { ["plane"] = "XY", ["name"] = sketch });
                Add(cmds, "sketch.options", subject, new JObject { ["sketch_3d"] = true });
                lines((a, b) => Line(cmds, subject, a, b));
                Add(cmds, "sketch.finish", subject, new JObject());
                Add(cmds, "frame.member", subject, new JObject
                {
                    ["sketch"] = sketch,
                    ["profile"] = new JObject { ["size"] = profile },
                    ["anchor"] = "center",
                    ["body_name"] = "Pipe Rack " + what.TrimEnd('s') + " " + profile
                });
                sketches.Add(sketch);
            }
            Members("columns", PipeRack.ColumnProfile, line =>
            {
                for (var i = 0; i <= bays; i++)
                {
                    var x = rack.X0 + i * bay;
                    line(new Vec3(x, ya, 0), new Vec3(x, ya, zb));
                    line(new Vec3(x, yb, 0), new Vec3(x, yb, zb));
                }
            });
            Members("cross beams", PipeRack.CrossBeamProfile, line =>
            {
                for (var i = 0; i <= bays; i++)
                {
                    var x = rack.X0 + i * bay;
                    line(new Vec3(x, ya, zb), new Vec3(x, yb, zb));
                }
            });
            Members("side beams", PipeRack.SideBeamProfile, line =>
            {
                line(new Vec3(rack.X0, ya, zb), new Vec3(rack.X1, ya, zb));
                line(new Vec3(rack.X0, yb, zb), new Vec3(rack.X1, yb, zb));
            });
            Add(cmds, "appearance.assign", subject, new JObject { ["components"] = new JArray(name), ["color"] = EquipmentTemplates.Skirt });
        }

        static int Line(List<Command> cmds, string subject, Vec3 from, Vec3 to)
        {
            Add(cmds, "sketch.line3d", subject, new JObject { ["points"] = new JArray(new JArray(from.ToArray()), new JArray(to.ToArray())) });
            return 1;
        }

        /// <summary>A flange: a disc centred on its face, square to the line.</summary>
        static void AddFlange(List<Command> cmds, string subject, PipeRun run, Vec3 face, Vec3 dir, string body)
        {
            var thickness = Math.Max(30, 0.25 * run.OuterDiameter);
            Add(cmds, "solid.cylinder", subject, new JObject
            {
                ["diameter"] = Round(run.OuterDiameter * 1.7),
                ["height"] = Round(thickness),
                ["base"] = new JArray((face - dir * (thickness / 2)).ToArray()),
                ["axis"] = new JArray(Math.Round(dir.X, 6), Math.Round(dir.Y, 6), Math.Round(dir.Z, 6)),
                ["body_name"] = body
            });
            Add(cmds, "appearance.assign", subject, Look(body, new Primitive { Color = EquipmentTemplates.Skirt }));
            Add(cmds, "material.assign", subject, new JObject { ["bodies"] = new JArray(body), ["material"] = "Steel" });
        }

        /// <summary>A library look when the part has one, else its colour, with a metal finish when metallic.</summary>
        internal static JObject Look(string body, Primitive p)
        {
            var look = new JObject { ["bodies"] = new JArray(body) };
            if (!string.IsNullOrEmpty(p.Look))
            {
                look["appearance"] = p.Look;
                return look;
            }
            look["color"] = p.Color;
            if (p.Metallic) look["pbr"] = new JObject { ["metallic"] = 0.6, ["roughness"] = 0.45, ["dielectric_ior"] = 1.5 };
            return look;
        }

        internal static void Add(List<Command> cmds, string name, string subject, JObject p)
            => cmds.Add(new Command { Name = name, Params = p, Subject = subject });

        internal static double Round(double x) => Math.Round(x, 1);

        static double Clamp(double x, double a, double b) => x < a ? a : (x > b ? b : x);

        public static string ToJson(IEnumerable<Command> cmds)
        {
            var arr = new JArray(cmds.Select(c => new JObject { ["command"] = c.Name, ["params"] = c.Params }));
            return new JObject { ["commands"] = arr }.ToString();
        }
    }
}
