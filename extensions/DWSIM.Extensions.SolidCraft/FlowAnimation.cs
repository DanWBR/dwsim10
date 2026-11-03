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
    /// The flow animation: SolidCraft animates parts on joints, not fluids, so each straight run of
    /// a line carries a row of bands that slide along it on slider joints. In one loop of the motion
    /// study every band moves one spacing, so the looped animation reads as a steady flow; the
    /// spacing grows with the velocity in the line, so faster lines run faster.
    /// </summary>
    public static class FlowAnimation
    {
        public const string StudyName = "Flow";

        // frames of one loop
        const int Steps = 40;

        // more bands than this makes the run slow and the picture busy: the spacing is widened
        const int MaxBands = 300;

        const string BandColor = "#fff36b";

        public static List<SolidCraftScript.Command> Create(PlantModel model, string gifPath)
        {
            var cmds = new List<SolidCraftScript.Command>();

            var runs = model.Pipes.Where(r => r.Velocity > 0 && r.Path.Count >= 2).ToList();
            if (runs.Count == 0) return cmds;

            // spacing per line from its velocity, widened for every line alike if there are too many bands
            var factor = 1.0;
            for (var pass = 0; pass < 8; pass++)
            {
                var total = runs.Sum(r => Segments(r).Sum(s => Count(s.Item3, Spacing(r, factor))));
                if (total <= MaxBands) break;
                factor *= 1.0 * total / MaxBands;
            }

            foreach (var run in runs)
            {
                var spacing = Spacing(run, factor);
                var line = SolidCraftScript.LineComponent(run) + ":1";
                var k = 0;
                foreach (var seg in Segments(run))
                {
                    var start = seg.Item1;
                    var dir = seg.Item2;
                    var count = Count(seg.Item3, spacing);
                    var band = Math.Max(60, run.OuterDiameter);
                    for (var i = 0; i < count; i++)
                    {
                        k++;
                        var name = "Flow " + run.Tag + " " + k;
                        var subject = "flow " + run.Tag + " " + k;
                        var at = start + dir * (spacing * (i + 0.5));
                        var body = name + " band";

                        SolidCraftScript.Add(cmds, "component.create", subject, new JObject { ["name"] = name, ["parent"] = "root", ["grounded"] = false });
                        SolidCraftScript.Add(cmds, "solid.cylinder", subject, new JObject
                        {
                            ["diameter"] = SolidCraftScript.Round(run.OuterDiameter * 1.5),
                            ["height"] = SolidCraftScript.Round(band),
                            ["base"] = new JArray((at - dir * (band / 2)).ToArray()),
                            ["axis"] = Axis(dir),
                            ["body_name"] = body
                        });
                        SolidCraftScript.Add(cmds, "appearance.assign", subject, new JObject { ["bodies"] = new JArray(body), ["color"] = BandColor });
                        // a marker, not a part of the plant: kept out of the bill of materials
                        SolidCraftScript.Add(cmds, "component.metadata.set", subject, new JObject { ["component"] = name, ["bom_type"] = "reference", ["description"] = "Flow marker" });
                        SolidCraftScript.Add(cmds, "joint.create", subject, new JObject
                        {
                            ["type"] = "slider",
                            ["name"] = JointName(run, k),
                            ["flip"] = true,
                            ["quiet"] = true,
                            ["a"] = new JObject { ["occurrence"] = line, ["point"] = new JArray(at.ToArray()), ["z"] = Axis(dir) },
                            ["b"] = new JObject { ["occurrence"] = name + ":1", ["point"] = new JArray(at.ToArray()), ["z"] = Axis(dir) },
                            // the slide of one loop, kept here for the study (not a SolidCraft parameter)
                            ["$travel"] = SolidCraftScript.Round(spacing)
                        });
                    }
                }
            }

            SolidCraftScript.Add(cmds, "motion.study", "the flow study", new JObject { ["name"] = StudyName, ["steps"] = Steps });
            SolidCraftScript.Add(cmds, "motion.export_animation", "the flow animation", new JObject
            {
                ["study"] = StudyName,
                ["path"] = gifPath,
                ["width"] = 1280,
                ["height"] = 800,
                ["fps"] = 20,
                ["samples"] = Steps,
                ["grid"] = false,
                ["axes"] = false
            });

            return cmds;
        }

        /// <summary>
        /// Fills in the keys of the motion study from the joints still in the script (the runner
        /// leaves out the bands it cannot make) and strips the private travel field.
        /// </summary>
        public static void Prepare(List<SolidCraftScript.Command> cmds)
        {
            var keys = new JArray();
            foreach (var c in cmds.Where(c => c.Name == "joint.create"))
            {
                var travel = c.Params["$travel"];
                if (travel == null) continue;
                keys.Add(new JObject
                {
                    ["joint"] = c.Params["name"],
                    ["points"] = new JArray(new JArray(0, 0), new JArray(Steps, travel))
                });
            }
            foreach (var c in cmds)
            {
                if (c.Name == "motion.study") c.Params["keys"] = keys;
            }
        }

        /// <summary>The script as SolidCraft reads it, without the private fields.</summary>
        public static List<SolidCraftScript.Command> Clean(List<SolidCraftScript.Command> cmds)
        {
            return cmds.Select(c =>
            {
                var p = (JObject)c.Params.DeepClone();
                p.Remove("$travel");
                return new SolidCraftScript.Command { Name = c.Name, Params = p, Subject = c.Subject };
            }).ToList();
        }

        static string JointName(PipeRun run, int k) => "flow " + run.Tag + " " + k;

        /// <summary>Band spacing, mm: 0.5 s of travel at the line velocity, within 300 mm and 4 m.</summary>
        static double Spacing(PipeRun run, double factor) => Math.Min(Math.Max(run.Velocity * 500, 300), 4000) * factor;

        static int Count(double length, double spacing) => (int)Math.Floor(length / spacing);

        /// <summary>Straight runs of a line: start, unit direction and length, leaving the ends free.</summary>
        static IEnumerable<Tuple<Vec3, Vec3, double>> Segments(PipeRun run)
        {
            for (var i = 1; i < run.Path.Count; i++)
            {
                var d = run.Path[i] - run.Path[i - 1];
                var l = d.Length;
                var margin = run.OuterDiameter;
                if (l <= 2 * margin) continue;
                var dir = d.Normalized();
                yield return Tuple.Create(run.Path[i - 1] + dir * margin, dir, l - 2 * margin);
            }
        }

        static JArray Axis(Vec3 d) => new JArray(Math.Round(d.X, 6), Math.Round(d.Y, 6), Math.Round(d.Z, 6));
    }
}
