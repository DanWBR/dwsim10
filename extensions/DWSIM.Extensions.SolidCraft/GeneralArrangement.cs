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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Command = DWSIM.Extensions.SolidCraft.SolidCraftScript.Command;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The general arrangement drawing of the 3D model: the plan and the front and right elevations
    /// on an A1 sheet, drawn by SolidCraft from the model alone (before the labels and the flow
    /// markers), with the item tags as notes and the title block filled in, written as PDF and DXF.
    /// </summary>
    public static class GeneralArrangement
    {
        public const string Sheet = "GA-001";
        const string DrawingName = "General arrangement";
        const double TagHeight = 3.5;

        public class Result
        {
            public string Pdf, Dxf, Error, DxfError;
        }

        /// <summary>
        /// Draws the arrangement of <paramref name="design"/> in three runs: the views go into a copy of
        /// the design, their places on the sheet are read back, and then the tags are written where the
        /// items are and the sheet exported. The design itself is unchanged.
        /// </summary>
        /// <param name="title">The title block cells (title, identification_number, document_type, ...); empty values keep the template's.</param>
        /// <param name="logo">A PNG or JPEG for the title block, or null.</param>
        public static Result Make(string cli, string design, PlantModel model, IDictionary<string, string> title, string logo, string outputBase)
        {
            var res = new Result();
            var temp = Path.Combine(Path.GetTempPath(), "dwsim-arrangement-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(temp);
            try
            {
                // 1: the drawing and its views, saved in a copy of the design
                var copy = Path.Combine(temp, "arrangement.sc");
                var cmds = new List<Command>();
                SolidCraftScript.Add(cmds, "drawing.create", "the drawing", new JObject
                {
                    ["name"] = DrawingName,
                    ["template"] = "iso-a1-assembly",
                    ["first_sheet_name"] = Sheet
                });
                SolidCraftScript.Add(cmds, "drawing.view.standard", "the views", new JObject { ["sheet"] = Sheet, ["iso"] = false });
                var overrides = new JObject();
                foreach (var kv in title.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))) overrides[kv.Key] = kv.Value;
                if (overrides.Count > 0)
                    SolidCraftScript.Add(cmds, "drawing.sheet.set", "the title block", new JObject { ["sheet"] = Sheet, ["title_block_overrides"] = overrides });
                if (!string.IsNullOrEmpty(logo) && File.Exists(logo))
                    SolidCraftScript.Add(cmds, "drawing.title_block.logo", "the logo", new JObject { ["cell"] = "general_tolerances", ["file"] = Path.GetFullPath(logo) });
                var first = Run(cli, cmds, Path.Combine(temp, "views.json"), design, copy, out res.Error);
                if (first == null) return res;
                var drawing = (string)first[0]?["id"];
                var views = first[1];
                string plan = (string)views?["top"], front = (string)views?["view"];
                if (drawing == null || plan == null || front == null)
                {
                    res.Error = "SolidCraft did not report the views of the drawing.";
                    return res;
                }

                // 2: where the plan and the front elevation landed on the sheet
                cmds = new List<Command>();
                SolidCraftScript.Add(cmds, "drawing.open", "the drawing", new JObject { ["drawing"] = drawing });
                SolidCraftScript.Add(cmds, "drawing.view.inspect", "the plan", new JObject { ["view"] = plan });
                SolidCraftScript.Add(cmds, "drawing.view.inspect", "the elevation", new JObject { ["view"] = front });
                var placed = Run(cli, cmds, Path.Combine(temp, "inspect.json"), copy, null, out res.Error);
                if (placed == null) return res;
                var planMap = ViewMap.From(placed[1]);
                var frontMap = ViewMap.From(placed[2]);

                // 3: the tags, then the PDF and, last, the DXF (a DXF that fails leaves the PDF)
                cmds = new List<Command>();
                SolidCraftScript.Add(cmds, "drawing.open", "the drawing", new JObject { ["drawing"] = drawing });
                var taken = new List<double[]>();
                foreach (var item in model.Equipment.Where(e => e.Visible && !e.IsInstrument && !string.IsNullOrEmpty(e.Tag)))
                {
                    var c = item.FootprintCentre;
                    var halfY = item.FootprintY > 0 ? item.FootprintY : item.Envelope?.HalfY ?? 0;
                    if (planMap != null)
                    {
                        // under the item's footprint in the plan
                        var p = planMap.ToPaper(new Vec3(c.X, c.Y - halfY, c.Z));
                        AddTag(cmds, taken, item.Tag, p[0], p[1] - 2 - TagHeight, -1);
                    }
                    if (frontMap != null)
                    {
                        // under the ground line of the front elevation, clear of the piping
                        var p = frontMap.ToPaper(new Vec3(c.X, c.Y, 0));
                        AddTag(cmds, taken, item.Tag, p[0], p[1] - 3 - TagHeight, -1);
                    }
                }
                var pdf = outputBase + " - general arrangement.pdf";
                var dxf = outputBase + " - general arrangement.dxf";
                foreach (var f in new[] { pdf, dxf }) if (File.Exists(f)) File.Delete(f);
                SolidCraftScript.Add(cmds, "drawing.export", "the PDF", new JObject { ["format"] = "pdf", ["path"] = Path.GetFullPath(pdf) });
                SolidCraftScript.Add(cmds, "drawing.export", "the DXF", new JObject { ["sheet"] = Sheet, ["format"] = "dxf", ["path"] = Path.GetFullPath(dxf) });
                Run(cli, cmds, Path.Combine(temp, "export.json"), copy, null, out var error);
                if (File.Exists(pdf)) res.Pdf = pdf;
                if (File.Exists(dxf)) res.Dxf = dxf;
                if (res.Pdf == null) res.Error = error ?? "SolidCraft wrote no PDF.";
                else if (res.Dxf == null) res.DxfError = error ?? "SolidCraft wrote no DXF.";
                return res;
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch (Exception) { }
            }
        }

        /// <summary>
        /// A tag centred on (x, y), moved a line at a time along <paramref name="away"/> (down in the plan,
        /// up in the elevation) while it would cover a tag already written.
        /// </summary>
        static void AddTag(List<Command> cmds, List<double[]> taken, string tag, double x, double y, int away)
        {
            var halfWidth = 0.5 * 0.75 * TagHeight * tag.Length + 1;
            for (var i = 0; i < 8 && taken.Any(r => Math.Abs(r[0] - x) < r[2] + halfWidth && Math.Abs(r[1] - y) < TagHeight + 1.5); i++)
                y += away * (TagHeight + 2);
            taken.Add(new[] { x, y, halfWidth });
            SolidCraftScript.Add(cmds, "drawing.note.add", "tag " + tag, new JObject
            {
                ["anchor"] = new JObject { ["sheet"] = Sheet },
                ["at"] = new JArray(Math.Round(x, 2), Math.Round(y, 2)),
                ["content"] = tag,
                ["cap_mm"] = TagHeight,
                ["align"] = "centre"
            });
        }

        /// <summary>
        /// Runs a script on <paramref name="input"/> (saving it to <paramref name="save"/> when given) and
        /// returns the results of its steps; null when it failed, with the error.
        /// </summary>
        static JArray Run(string cli, List<Command> cmds, string script, string input, string save, out string error)
        {
            File.WriteAllText(script, SolidCraftScript.ToJson(cmds), new UTF8Encoding(false));
            var json = Path.ChangeExtension(script, ".out.json");
            var exit = SolidCraftRunner.Execute(cli, "run " + SolidCraftRunner.Quote(script) + " --in " + SolidCraftRunner.Quote(input)
                + (save != null ? " --save " + SolidCraftRunner.Quote(save) : "") + " --results --json-out " + SolidCraftRunner.Quote(json) + " --quiet",
                900, out var output);
            error = null;
            if (exit != 0 || (save != null && !File.Exists(save)))
            {
                error = output.Trim().Split('\n').LastOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "SolidCraft failed.";
                return null;
            }
            try
            {
                var token = JToken.Parse(File.ReadAllText(json));
                return token as JArray ?? token["results"] as JArray;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// How a view puts the model on the sheet: its plane's axes, the model's extents on that plane
        /// and the box they take on paper.
        /// </summary>
        class ViewMap
        {
            Vec3 ax, ay;
            double[] modelMin, paperMin;
            double scale;

            public static ViewMap From(JToken view)
            {
                try
                {
                    var dir = view["direction"];
                    var b = view["boundary_mm"];
                    var e = view["extents"];
                    var m = new ViewMap
                    {
                        ax = Vector(dir["x"]),
                        ay = Vector(dir["y"]),
                        modelMin = new[] { (double)e["min"][0], (double)e["min"][1] },
                        paperMin = new[] { (double)b["min"][0], (double)b["min"][1] }
                    };
                    var modelWidth = (double)e["max"][0] - m.modelMin[0];
                    var paperWidth = (double)b["max"][0] - m.paperMin[0];
                    if (modelWidth <= 0 || paperWidth <= 0) return null;
                    m.scale = paperWidth / modelWidth;
                    return m;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            static Vec3 Vector(JToken t) => new Vec3((double)t[0], (double)t[1], (double)t[2]);

            static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

            public double[] ToPaper(Vec3 p) => new[]
            {
                paperMin[0] + (Dot(p, ax) - modelMin[0]) * scale,
                paperMin[1] + (Dot(p, ay) - modelMin[1]) * scale
            };
        }
    }
}
