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

        // thickness of the 3D label lettering, mm
        const double LabelDepth = 15;

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
                        ["placements"] = new JObject(model.Equipment.Where(e => e.Visible).Select(e =>
                            new JProperty(e.Tag, new JObject
                            {
                                ["x"] = Math.Round(e.Origin.X),
                                ["y"] = Math.Round(e.Origin.Y),
                                ["z"] = Math.Round(e.Origin.Z),
                                ["rot"] = e.Rotation
                            })))
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

        /// <summary>
        /// The 3D labels, run on the saved design after the STEP export: lettering is heavy B-rep
        /// that would bloat the STEP file, and the labels are only there for viewing. Each label goes
        /// into the component of its item or line.
        /// </summary>
        public static List<Command> CreateLabels(PlantModel model)
        {
            var cmds = new List<Command>();
            var sketches = new List<string>();

            foreach (var item in model.Equipment.Where(e => e.Visible && !string.IsNullOrEmpty(e.Label)))
            {
                // over the item and clear of its overhead line, in a vertical plane facing -Y, the
                // side the iso view looks from
                var subject = "label " + item.Tag;
                var height = Clamp(0.25 * 2 * item.Envelope.HalfX, 150, 500);
                var lines = item.Label.Split('\n').Length;
                var origin = item.Origin + new Vec3(0, 0, item.Envelope.ZTop + 1500 + (lines - 1) * 1.6 * height);
                var name = "Label " + item.Tag;
                Add(cmds, "component.activate", subject, new JObject { ["component"] = item.Tag });
                AddLabel(cmds, subject, name, item.Label, origin, Vec3.PlusX, height, item.Tag + " label");
                sketches.Add(name);
            }

            var n = 0;
            foreach (var run in model.Pipes)
            {
                n++;
                var label = LineLabel(cmds, run, n);
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
        static string LineLabel(List<Command> cmds, PipeRun run, int n)
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
            AddLabel(cmds, "label " + run.Tag, name, run.Label, mid, dir, height, run.Tag + " label");
            return name;
        }

        /// <summary>Lettering of 3D text in a vertical plane through <paramref name="origin"/>, centred on it.</summary>
        static void AddLabel(List<Command> cmds, string subject, string sketch, string text, Vec3 origin, Vec3 xDir, double height, string body)
        {
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
                ["text"] = text,
                ["at"] = new JArray(Round(-TextWidth(text, height) / 2), 0),
                ["height"] = Round(height)
            });
            Add(cmds, "sketch.finish", subject, new JObject());
            Add(cmds, "solid.extrude", subject, new JObject { ["sketch"] = sketch, ["distance"] = LabelDepth, ["body_name"] = body });
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
            const string subject = "the pipe rack", name = "Pipe Rack", sketch = "Pipe Rack frame";
            var zb = rack.Top - PipeRack.BeamHeight / 2;
            double ya = rack.Y0, yb = rack.Y0 + rack.Width;
            var bays = Math.Max(1, (int)Math.Ceiling((rack.X1 - rack.X0) / PipeRack.BayLength));
            var bay = (rack.X1 - rack.X0) / bays;

            Add(cmds, "component.create", subject, new JObject { ["name"] = name, ["parent"] = "root", ["grounded"] = true });
            Add(cmds, "sketch.create", subject, new JObject { ["plane"] = "XY", ["name"] = sketch });
            Add(cmds, "sketch.options", subject, new JObject { ["sketch_3d"] = true });
            for (var i = 0; i <= bays; i++)
            {
                var x = rack.X0 + i * bay;
                Line(cmds, subject, new Vec3(x, ya, 0), new Vec3(x, ya, zb));
                Line(cmds, subject, new Vec3(x, yb, 0), new Vec3(x, yb, zb));
                Line(cmds, subject, new Vec3(x, ya, zb), new Vec3(x, yb, zb));
            }
            Line(cmds, subject, new Vec3(rack.X0, ya, zb), new Vec3(rack.X1, ya, zb));
            Line(cmds, subject, new Vec3(rack.X0, yb, zb), new Vec3(rack.X1, yb, zb));
            Add(cmds, "sketch.finish", subject, new JObject());
            Add(cmds, "frame.member", subject, new JObject
            {
                ["sketch"] = sketch,
                ["profile"] = new JObject { ["kind"] = "i_beam", ["height"] = PipeRack.BeamHeight, ["width"] = 150, ["web"] = 8, ["flange"] = 12 },
                ["body_name"] = "Pipe Rack beam"
            });
            Add(cmds, "appearance.assign", subject, new JObject { ["components"] = new JArray(name), ["color"] = EquipmentTemplates.Skirt });
            sketches.Add(sketch);
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
