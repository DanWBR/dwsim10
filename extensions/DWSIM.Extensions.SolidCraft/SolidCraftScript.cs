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
                        ["scale_mm_per_canvas_unit"] = Math.Round(model.Scale, 2)
                    }
                }
            });

            foreach (var item in model.Equipment.Where(e => e.Visible))
            {
                var subject = item.Tag;
                Add(cmds, "component.create", subject, new JObject { ["name"] = item.Tag, ["parent"] = "root", ["grounded"] = true });

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
                                ["axis"] = p.Axis,
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

            var n = 0;
            foreach (var run in model.Pipes)
            {
                n++;
                var subject = "line " + run.Tag;
                var component = LineComponent(run);
                var sketch = "Route " + n + " " + run.Tag;
                var body = run.Tag + " " + run.Size;
                var curves = new JArray(Enumerable.Range(1, run.Path.Count - 1).Select(i => "w" + i));

                Add(cmds, "component.create", subject, new JObject { ["name"] = component, ["parent"] = "root", ["grounded"] = true });
                Add(cmds, "sketch.create", subject, new JObject { ["plane"] = "XY", ["name"] = sketch });
                Add(cmds, "sketch.options", subject, new JObject { ["sketch_3d"] = true });
                Add(cmds, "sketch.line3d", subject, new JObject { ["points"] = new JArray(run.Path.Select(v => new JArray(v.ToArray()))) });
                Add(cmds, "sketch.finish", subject, new JObject());
                Add(cmds, "solid.pipe", subject, new JObject
                {
                    ["path_sketch"] = sketch,
                    ["path"] = curves,
                    ["diameter"] = run.OuterDiameter,
                    ["wall"] = run.Wall,
                    ["body_name"] = body
                });
                sketches.Add(sketch);

                // painted carbon steel: the colour tells the phase
                Add(cmds, "appearance.assign", subject, Look(body, new Primitive { Color = run.Color, Metallic = true, Look = "" }));
                Add(cmds, "material.assign", subject, new JObject { ["bodies"] = new JArray(body), ["material"] = "Steel" });

                if (run.OpenStart) AddFlange(cmds, subject, run, run.Path[0], run.Path[1], body + " flange in");
                if (run.OpenEnd) AddFlange(cmds, subject, run, run.Path[run.Path.Count - 1], run.Path[run.Path.Count - 2], body + " flange out");

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

        /// <summary>A blind flange at the battery limit, square to the last segment of the line.</summary>
        static void AddFlange(List<Command> cmds, string subject, PipeRun run, Vec3 end, Vec3 previous, string body)
        {
            var dir = (end - previous).Normalized();
            const double thickness = 60;
            Add(cmds, "solid.cylinder", subject, new JObject
            {
                ["diameter"] = Round(run.OuterDiameter * 1.7),
                ["height"] = thickness,
                ["base"] = new JArray(end.ToArray()),
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
