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
    /// item, named after its tag and carrying its data as properties, and a Piping component with one
    /// swept pipe per line. The script is what `solidcraft-cli run` replays.
    /// </summary>
    public static class SolidCraftScript
    {
        public class Command
        {
            public string Name;
            public JObject Params;

            /// <summary>What the command draws, for the report when it has to be left out.</summary>
            public string Subject;
        }

        public static List<Command> Create(PlantModel model)
        {
            var cmds = new List<Command>();

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
                Add(cmds, "component.create", subject, new JObject { ["name"] = item.Tag, ["parent"] = "root" });

                foreach (var part in item.Parts)
                {
                    var body = item.Tag + " " + part.Name;
                    var p = part.Moved(item.Origin);
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
                    Add(cmds, "appearance.assign", subject, Look(body, p.Color, p.Metallic));
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

            if (model.Pipes.Count > 0)
            {
                Add(cmds, "component.create", "the Piping component", new JObject { ["name"] = "Piping", ["parent"] = "root" });

                var n = 0;
                foreach (var run in model.Pipes)
                {
                    n++;
                    var subject = "line " + run.Tag;
                    var sketch = "Route " + n + " " + run.Tag;
                    var body = run.Tag + " " + run.Size;
                    var curves = new JArray(Enumerable.Range(1, run.Path.Count - 1).Select(i => "w" + i));

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
                    Add(cmds, "appearance.assign", subject, Look(body, run.Color, false));

                    if (run.OpenStart) AddFlange(cmds, subject, run, run.Path[0], run.Path[1], body + " flange in");
                    if (run.OpenEnd) AddFlange(cmds, subject, run, run.Path[run.Path.Count - 1], run.Path[run.Path.Count - 2], body + " flange out");
                }

                // the route sketches stay in the design, to edit a line, but hidden behind the pipes
                Add(cmds, "browser.visibility", "the route sketches", new JObject { ["visible"] = false, ["folder"] = "sketches" });
            }

            return cmds;
        }

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
            Add(cmds, "appearance.assign", subject, Look(body, EquipmentTemplates.Skirt, false));
        }

        static JObject Look(string body, string color, bool metallic)
        {
            var look = new JObject { ["bodies"] = new JArray(body), ["color"] = color };
            if (metallic) look["pbr"] = new JObject { ["metallic"] = 0.7, ["roughness"] = 0.4, ["dielectric_ior"] = 1.5 };
            return look;
        }

        static void Add(List<Command> cmds, string name, string subject, JObject p)
            => cmds.Add(new Command { Name = name, Params = p, Subject = subject });

        static double Round(double x) => Math.Round(x, 1);

        public static string ToJson(IEnumerable<Command> cmds)
        {
            var arr = new JArray(cmds.Select(c => new JObject { ["command"] = c.Name, ["params"] = c.Params }));
            return new JObject { ["commands"] = arr }.ToString();
        }
    }
}
