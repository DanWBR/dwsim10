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
using DWSIM.Interfaces;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The general arrangement (dimensional) drawing of each vessel, column, tank and exchanger,
    /// from the 3D plant model's geometry: a front view and a plan view at a standard scale, the
    /// main dimensions, the nozzles marked N1, N2... with a nozzle schedule, the item's data from
    /// the simulation, and the sheet and title block of the chosen standard. Drawn in CADCraft,
    /// one drawing per item, saved as DXF and DWG and plotted to PDF and PNG.
    /// </summary>
    public static class EquipmentDrawings
    {
        public class Result
        {
            public List<string> Drawings = new List<string>(), Pdfs = new List<string>(), Failed = new List<string>();
            public string Error;
        }

        static readonly int[] Scales = { 5, 10, 20, 25, 50, 100, 200, 250, 500 };

        const double TextHeight = 2.5;

        class Nozzle
        {
            public string Mark, Size, Service;
            public Vec3 Base, Face, Dir;
            public double OD;
            public PipeRun Run;
            public bool Outlet;
        }

        enum View { Front, Plan }

        /// <summary>Draws every vessel, column, tank and exchanger of the flowsheet; <paramref name="titleFor"/> fills each title block.</summary>
        public static Result Export(IFlowsheet fs, string cadcraftFolder, string outputFolder, string name, DrawingStyle style, Func<EquipmentItem, TitleData> titleFor)
        {
            var result = new Result();
            PlantModel model;
            try { model = PlantModelBuilder.Build(fs, name, PlantLayout.Load(fs), ExportOptions.Load()); }
            catch (Exception ex) { result.Error = "the plant model could not be built (" + ex.Message + ")"; return result; }

            var items = model.Equipment.Where(e => e.Visible && !e.IsInstrument && e.Envelope != null &&
                (e.Envelope.Kind == EnvelopeKind.VerticalCylinder || e.Envelope.Kind == EnvelopeKind.HorizontalCylinder)).ToList();
            if (items.Count == 0) { result.Error = "the flowsheet has no vessels, columns, tanks or exchangers"; return result; }

            var temp = Path.Combine(Path.GetTempPath(), "dwsim-ga-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(temp);
            using (var session = new CADCraftSession(cadcraftFolder))
            {
                foreach (var item in items)
                {
                    try
                    {
                        var file = Draw(session, model, item, Path.Combine(outputFolder, name + " - GA " + Safe(item.Tag)), style, titleFor(item), temp);
                        if (file != null)
                        {
                            result.Drawings.Add(file);
                            if (File.Exists(Path.ChangeExtension(file, ".pdf"))) result.Pdfs.Add(Path.ChangeExtension(file, ".pdf"));
                        }
                    }
                    catch (Exception ex) { result.Failed.Add(item.Tag + ": " + ex.Message); }
                }
                result.Failed.AddRange(session.Failed);
            }
            try { Directory.Delete(temp, true); } catch (Exception) { }
            if (result.Drawings.Count == 0 && result.Error == null) result.Error = "CADCraft did not save any drawing" + (result.Failed.Count > 0 ? " (" + result.Failed.Last() + ")" : "");
            return result;
        }

        static string Safe(string s) => string.Concat((s ?? "item").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        static string Mm(double v) => Math.Round(v).ToString("0", CultureInfo.InvariantCulture);

        /// <summary>One item's drawing; returns the DXF file, or null.</summary>
        static string Draw(CADCraftSession session, PlantModel model, EquipmentItem item, string outputBase, DrawingStyle style, TitleData title, string temp)
        {
            var env = item.Envelope;
            var vertical = env.Kind == EnvelopeKind.VerticalCylinder;
            var unturn = (360 - item.Rotation) % 360;
            Vec3 Local(Vec3 world) => (world - item.Origin).TurnedZ(unturn);
            Vec3 Axis(Primitive p) => (p.AxisVector ?? (p.Axis == "X" ? Vec3.PlusX : p.Axis == "Y" ? new Vec3(0, 1, 0) : Vec3.PlusZ)).TurnedZ(unturn);

            // the body: the parts that make up the item, without platforms, ladders, rails and the
            // column's own reflux drum and reboiler
            var parts = item.Parts.Where(p => !p.NoClash && p.Kind != PrimitiveKind.Torus &&
                !new[] { "reflux", "reboiler", "platform", "handrail", "ladder" }.Any(x => (p.Name ?? "").StartsWith(x))).ToList();

            var nozzles = Nozzles(model, item);

            // the main dimensions, in the item's own frame
            var shell = parts.FirstOrDefault(p => p.Name == "shell");
            double r = vertical ? env.HalfX : (env.ZTop - env.ZBottom) / 2;
            if (shell != null) r = shell.Diameter / 2;
            double zc = (env.ZTop + env.ZBottom) / 2;
            double tlA = 0, tlB = 0;
            if (shell != null)
            {
                var b = Local(item.Origin + shell.Point);
                var a = Axis(shell);
                tlA = vertical ? b.Z : Math.Min(b.X, b.X + a.X * shell.Height);
                tlB = vertical ? b.Z + shell.Height : Math.Max(b.X, b.X + a.X * shell.Height);
            }

            // the extents of the views, in millimetres of the item
            double fx0 = -env.HalfX, fx1 = env.HalfX;
            foreach (var n in nozzles) { fx0 = Math.Min(fx0, n.Face.X - n.OD); fx1 = Math.Max(fx1, n.Face.X + n.OD); }
            double fz1 = env.ZTop;
            foreach (var n in nozzles) fz1 = Math.Max(fz1, n.Face.Z + n.OD);
            double py0 = -Math.Max(env.HalfY, r), py1 = Math.Max(env.HalfY, r);
            foreach (var n in nozzles) { py0 = Math.Min(py0, n.Face.Y - n.OD); py1 = Math.Max(py1, n.Face.Y + n.OD); }
            double fw = fx1 - fx0 + 600, fh = fz1 + 300, pw = fw, ph = py1 - py0;

            // the sheet and scale: the views side by side (vertical) or one over the other
            // (horizontal), with room for the dimensions, beside the tables
            // the smallest sheet on which the item comes out at 1:50 or larger, else the largest
            // sheet at the scale that fits
            DrawingSheets.Margins(style, out var ml, out var mo);
            SheetSize sheet = null; var scale = Scales.Last();
            double areaX0 = 0, areaX1 = 0, areaY0 = 0, areaY1 = 0;
            foreach (var size in SheetSize.For(style).Take(3))
            {
                sheet = size;
                areaX0 = ml + 30; areaX1 = size.Width - mo - DrawingSheets.TitleWidth - 15;
                areaY0 = mo + 25; areaY1 = size.Height - mo - 15;
                var fit = Scales.FirstOrDefault(s =>
                    (vertical ? (fw + pw) / s + 40 : Math.Max(fw, pw) / s + 30) <= areaX1 - areaX0 &&
                    (vertical ? Math.Max(fh, ph) / s + 20 : (fh + ph) / s + 50) <= areaY1 - areaY0);
                if (fit > 0) scale = fit;
                if (fit > 0 && fit <= 50) break;
            }
            double S = scale;
            // nozzles too small to see at this scale are drawn at 1.5 mm on the sheet
            foreach (var n in nozzles) n.OD = Math.Max(n.OD, 1.5 * S);

            // the two views, each a block of its own
            var front = new Dxf(); var plan = new Dxf();
            foreach (var view in new[] { View.Front, View.Plan })
            {
                var d = view == View.Front ? front : plan;
                foreach (var p in parts) Project(d, view, p, Local(item.Origin + p.Point), Axis(p), item, vertical, zc);
                foreach (var n in nozzles)
                {
                    var neck = Math.Max(0, (n.Face - n.Base).Length);
                    Cylinder(d, view, n.Base, n.Dir, neck, n.OD, false);
                    var t = Math.Max(30, 0.25 * n.OD);
                    Cylinder(d, view, n.Face - n.Dir * t, n.Dir, t, n.OD * 1.7, false);
                }
                // centre lines
                d.Layer = "CENTER";
                if (vertical)
                {
                    if (view == View.Front) d.Line(0, -0.03 * env.ZTop - 100, 0, env.ZTop * 1.03 + 100);
                    else { d.Line(-r * 1.25, 0, r * 1.25, 0); d.Line(0, -r * 1.25, 0, r * 1.25); }
                }
                else
                {
                    var x0 = -env.HalfX * 1.04 - 100; var x1 = env.HalfX * 1.04 + 100;
                    d.Line(x0, view == View.Front ? zc : 0, x1, view == View.Front ? zc : 0);
                }
                d.Layer = "0";
            }

            // the bottom head inside the skirt of a vertical vessel, hidden
            if (vertical && parts.Any(p => p.Name == "skirt") && env.HeadDepth > 0 && shell != null)
            {
                front.Layer = "HIDDEN";
                front.Ellipse(0, tlA, r, env.HeadDepth, Math.PI, 2 * Math.PI);
                front.Layer = "0";
            }
            // tangent lines
            front.Layer = "CENTER";
            if (shell != null)
            {
                if (vertical) { front.Line(-r * 1.15, tlA, r * 1.15, tlA); front.Line(-r * 1.15, tlB, r * 1.15, tlB); }
                else { front.Line(tlA, zc - r * 1.15, tlA, zc + r * 1.15); front.Line(tlB, zc - r * 1.15, tlB, zc + r * 1.15); }
            }
            front.Layer = "0";
            // grade
            front.Line(fx0 - 300, 0, fx1 + 300, 0);

            // where the plan view goes in model space
            // to the right of the front view (vertical items), or under it, in line (horizontal)
            var planAt = vertical
                ? new Vec3(fx1 + 40 * S - fx0, fz1 / 2, 0)
                : new Vec3(0, -40 * S - py1, 0);

            var frontFile = Path.Combine(temp, Safe(item.Tag) + "-front.dxf");
            var planFile = Path.Combine(temp, Safe(item.Tag) + "-plan.dxf");
            front.Save(frontFile); plan.Save(planFile);

            var cmds = new List<(string Name, JObject P)>();
            void C(string n, JObject p) => cmds.Add((n, p));
            JArray P(double x, double y) => new JArray(Math.Round(x, 1), Math.Round(y, 1));

            C("linetype", new JObject { ["load"] = "*" });
            C("style", new JObject { ["name"] = DrawingSheets.TextStyle, ["font"] = DrawingSheets.Font, ["current"] = true });
            foreach (var (n, lt, lw) in new[] { ("EQUIPMENT", "Continuous", 0.5), ("HIDDEN", "DASHED", 0.25), ("CENTER", "CENTER", 0.18), ("DIMENSION", "Continuous", 0.18),
                ("TEXT", "Continuous", 0.25), ("TABLE", "Continuous", 0.25), ("BORDER", "Continuous", 0.7), ("TITLE", "Continuous", 0.35), ("VIEWPORT", "Continuous", 0.0) })
                C("layer.new", new JObject { ["name"] = n, ["color"] = "white", ["linetype"] = lt, ["lineweight"] = lw });
            C("ltscale", new JObject { ["scale"] = 0.4 * S });
            C("layer.current", new JObject { ["name"] = "EQUIPMENT" });
            C("insert", new JObject { ["name"] = "GA_FRONT", ["file"] = frontFile.Replace('\\', '/'), ["at"] = P(0, 0) });
            C("insert", new JObject { ["name"] = "GA_PLAN", ["file"] = planFile.Replace('\\', '/'), ["at"] = P(planAt.X, planAt.Y) });

            // dimensions
            C("dimstyle", new JObject { ["name"] = "GA", ["DIMSCALE"] = S, ["DIMTXT"] = TextHeight, ["DIMASZ"] = 2.5, ["DIMEXO"] = 1.5, ["DIMEXE"] = 1.5, ["DIMGAP"] = 0.8, ["DIMDEC"] = 0, ["DIMTXSTY"] = DrawingSheets.TextStyle, ["current"] = true });
            C("layer.current", new JObject { ["name"] = "DIMENSION" });
            void Dim(double x1, double y1, double x2, double y2, double ax, double ay, bool rotated, string text = null)
            {
                var o = new JObject { ["p1"] = P(x1, y1), ["p2"] = P(x2, y2), ["at"] = P(ax, ay) };
                if (rotated) o["rotation"] = 90;
                if (text != null) o["text"] = text;
                C("dimlinear", o);
            }
            if (vertical)
            {
                var xo = fx0 - 25 * S; var xi = fx0 - 12 * S;
                Dim(-r, 0, -r, env.ZTop, xo, env.ZTop / 2, true);
                if (tlA > 1) Dim(-r, 0, -r, tlA, xi, tlA / 2, true);
                if (shell != null) Dim(-r, tlA, -r, tlB, xi, (tlA + tlB) / 2, true);
                if (env.ZTop - tlB > 1) Dim(-r, tlB, -r, env.ZTop, xi, (tlB + env.ZTop) / 2, true);
                var ym = shell != null ? (tlA + tlB) / 2 : env.ZTop / 2;
                Dim(-r, ym, r, ym, 0, ym, false, "%%c" + Mm(2 * r));
            }
            else
            {
                var zo = fz1 + 25 * S; var zi = fz1 + 12 * S;
                Dim(-env.HalfX, zc, env.HalfX, zc, 0, zo, false);
                if (shell != null) Dim(tlA, zc, tlB, zc, (tlA + tlB) / 2, zi, false);
                Dim(fx0, zc - r, fx0, zc + r, fx0 - 12 * S, zc, true, "%%c" + Mm(2 * r));
                Dim(fx0, 0, fx0, zc, fx0 - 25 * S, zc / 2, true);
                var saddles = parts.Where(p => (p.Name ?? "").StartsWith("saddle")).Select(p => Local(item.Origin + p.Point).X).OrderBy(x => x).ToList();
                if (saddles.Count == 2) Dim(saddles[0], 0, saddles[1], 0, (saddles[0] + saddles[1]) / 2, -12 * S, false);
            }

            // nozzle marks, tangent lines and view titles
            C("layer.current", new JObject { ["name"] = "TEXT" });
            var th = TextHeight * S;
            foreach (var n in nozzles)
            {
                var at = n.Face + n.Dir * (6 * S);
                if (Math.Abs(n.Dir.Y) > 0.7) at = n.Face + new Vec3(n.OD + 4 * S, 0, 0);
                C("text", new JObject { ["at"] = P(at.X, at.Z), ["text"] = n.Mark, ["height"] = th, ["justify"] = "MC" });
                var pt = new Vec3(n.Face.X, n.Face.Y, 0) + new Vec3(n.Dir.X, n.Dir.Y, 0) * (6 * S);
                if (Math.Abs(n.Dir.Z) < 0.7) C("text", new JObject { ["at"] = P(planAt.X + pt.X, planAt.Y + pt.Y), ["text"] = n.Mark, ["height"] = th, ["justify"] = "MC" });
            }
            if (shell != null)
            {
                if (vertical)
                {
                    C("text", new JObject { ["at"] = P(r * 1.15 + 2 * S, tlA), ["text"] = "BTL", ["height"] = th * 0.8, ["justify"] = "ML" });
                    C("text", new JObject { ["at"] = P(r * 1.15 + 2 * S, tlB), ["text"] = "TTL", ["height"] = th * 0.8, ["justify"] = "ML" });
                }
                else
                {
                    C("text", new JObject { ["at"] = P(tlA, zc - r * 1.15 - 2 * S), ["text"] = "TL", ["height"] = th * 0.8, ["justify"] = "TC" });
                    C("text", new JObject { ["at"] = P(tlB, zc - r * 1.15 - 2 * S), ["text"] = "TL", ["height"] = th * 0.8, ["justify"] = "TC" });
                }
            }
            var scaleText = "1:" + scale;
            C("text", new JObject { ["at"] = P((fx0 + fx1) / 2, -(vertical ? 10 : 22) * S), ["text"] = "FRONT VIEW  " + scaleText, ["height"] = 3.5 * S, ["justify"] = "TC" });
            C("text", new JObject { ["at"] = P(planAt.X, planAt.Y + py0 - 10 * S), ["text"] = "PLAN VIEW  " + scaleText, ["height"] = 3.5 * S, ["justify"] = "TC" });

            // the sheet
            title.Scale = scaleText;
            C("layout.new", new JObject { ["name"] = "GA", ["viewport"] = false });
            C("pagesetup", new JObject
            {
                ["layout"] = "GA", ["device"] = "CADCraft PDF", ["width"] = sheet.Width, ["height"] = sheet.Height, ["landscape"] = true,
                ["margins"] = new JArray(0, 0, 0, 0), ["plotArea"] = "layout", ["scale"] = 1, ["lineweights"] = true, ["plotStyleTable"] = "monochrome.ctb"
            });
            C("layout.set", new JObject { ["name"] = "GA" });
            C("pspace", new JObject());
            var border = Path.Combine(temp, Safe(item.Tag) + "-border.dxf");
            DrawingSheets.Border(style, sheet).Save(border);
            C("layer.current", new JObject { ["name"] = "BORDER" });
            C("insert", new JObject { ["name"] = "SHEET_BORDER", ["file"] = border.Replace('\\', '/'), ["at"] = P(0, 0) });
            var tb = Path.Combine(temp, Safe(item.Tag) + "-title.dxf");
            DrawingSheets.TitleBlock(style, out var titleHeight).Save(tb);
            C("layer.current", new JObject { ["name"] = "TITLE" });
            C("insert", new JObject
            {
                ["name"] = "TITLE_BLOCK", ["file"] = tb.Replace('\\', '/'), ["at"] = P(sheet.Width - mo, mo),
                ["attribs"] = JObject.FromObject(DrawingSheets.Attributes(title, sheet, style))
            });

            // the tables: the item's data and the nozzle schedule, over the title block
            C("tablestyle", new JObject { ["name"] = "GA", ["textHeight"] = TextHeight, ["margin"] = 0.8, ["current"] = true });
            foreach (var cs in new[] { "Header", "Data" })
                C("tablestyle.cellstyle", new JObject { ["style"] = "GA", ["name"] = cs, ["textStyle"] = DrawingSheets.TextStyle, ["textHeight"] = TextHeight });
            C("layer.current", new JObject { ["name"] = "TABLE" });
            var tx = sheet.Width - mo - DrawingSheets.TitleWidth;
            var top = sheet.Height - mo - 5;
            var data = ItemData(item, r, tlB - tlA, vertical);
            C("table", new JObject
            {
                ["at"] = P(tx, top), ["rows"] = data.Count, ["cols"] = 2, ["rowHeight"] = 6, ["colWidth"] = DrawingSheets.TitleWidth / 2,
                ["header"] = new JArray("ITEM", item.Tag), ["cells"] = new JArray(data.Select(kv => new JArray(kv.Key, kv.Value)))
            });
            top -= (data.Count + 1) * 6 + 6;
            if (nozzles.Count > 0)
            {
                var rows = nozzles.Select(n => new JArray(n.Mark, n.Size, Short(n.Service, 14), Mm(n.Face.Z),
                    Math.Abs(n.Dir.Z) > 0.7 ? (n.Dir.Z > 0 ? "TOP" : "BOTTOM") : Mm((Math.Atan2(n.Dir.Y, n.Dir.X) * 180 / Math.PI + 360) % 360) + "%%d",
                    Mm(vertical ? Math.Sqrt(n.Face.X * n.Face.X + n.Face.Y * n.Face.Y) : Math.Sqrt(Math.Pow(n.Face.Y, 2) + Math.Pow(n.Face.Z - zc, 2))))).ToList();
                C("table", new JObject
                {
                    ["at"] = P(tx, top), ["rows"] = rows.Count, ["cols"] = 6, ["rowHeight"] = 6, ["colWidth"] = DrawingSheets.TitleWidth / 6,
                    ["header"] = new JArray("MARK", "SIZE", "SERVICE", "ELEV.", "ORIENT.", "PROJ."), ["cells"] = new JArray(rows)
                });
            }

            File.Delete(outputBase + ".dxf");
            if (session.Tool("new_drawing", new JObject { ["metric"] = true }, "new drawing for " + item.Tag) == null) return null;
            foreach (var c in cmds) session.Execute(c.Name, c.P);

            // the viewport over the views, at the drawing's scale
            session.Execute("layer.current", new JObject { ["name"] = "VIEWPORT" });
            session.Execute("layer.set", new JObject { ["name"] = "VIEWPORT", ["plot"] = false });
            var vp = session.Execute("mview", new JObject { ["p1"] = P(areaX0, areaY0), ["p2"] = P(areaX1, areaY1) });
            var handle = vp == null ? null : (JObject.Parse(vp)["viewports"] as JArray)?.FirstOrDefault()?.ToString();
            double mx0 = fx0 - 32 * S, mx1 = vertical ? planAt.X + fx1 + 10 * S : fx1 + 10 * S;
            double my0 = Math.Min(-16 * S, planAt.Y + py0 - 16 * S), my1 = vertical ? Math.Max(fz1, planAt.Y + py1) + 10 * S : fz1 + 32 * S;
            if (handle != null)
                session.Execute("viewport.set", new JObject { ["handle"] = handle, ["scale"] = 1.0 / S, ["locked"] = true, ["center"] = P((mx0 + mx1) / 2, (my0 + my1) / 2) });

            foreach (var ext in new[] { ".dxf", ".dwg", ".pdf", ".png" }) if (File.Exists(outputBase + ext)) File.Delete(outputBase + ext);
            session.Execute("plot", new JObject { ["path"] = outputBase + ".pdf", ["layout"] = "GA" }, 300);
            session.Execute("plot", new JObject { ["path"] = outputBase + ".png", ["layout"] = "GA", ["device"] = "CADCraft PNG" }, 300);
            session.Tool("save", new JObject { ["path"] = outputBase + ".dwg" }, "save DWG of " + item.Tag);
            session.Tool("save", new JObject { ["path"] = outputBase + ".dxf" }, "save DXF of " + item.Tag);
            return File.Exists(outputBase + ".dxf") ? outputBase + ".dxf" : null;
        }

        /// <summary>
        /// The nozzles of an item: where the lines meet it, at the flange faces of the 3D model, in
        /// the item's own frame, marked N1, N2... (inlets first, from the top or the left).
        /// </summary>
        static List<Nozzle> Nozzles(PlantModel model, EquipmentItem item)
        {
            var unturn = (360 - item.Rotation) % 360;
            Vec3 Local(Vec3 world) => (world - item.Origin).TurnedZ(unturn);
            var vertical = item.Envelope?.Kind == EnvelopeKind.VerticalCylinder;
            var nozzles = new List<Nozzle>();
            foreach (var run in model.Pipes)
            {
                var k = 0;
                if (run.From == item.Tag && run.Flanges.Count > k) nozzles.Add(MakeNozzle(run, run.Flanges[k], true, Local));
                if (run.From != "battery limit") k++;
                if (run.To == item.Tag && run.Flanges.Count > k) nozzles.Add(MakeNozzle(run, run.Flanges[k], false, Local));
            }
            nozzles = nozzles.OrderBy(n => n.Outlet ? 1 : 0).ThenByDescending(n => vertical ? n.Face.Z : -n.Face.X).ToList();
            for (var i = 0; i < nozzles.Count; i++) nozzles[i].Mark = "N" + (i + 1);
            return nozzles;
        }

        /// <summary>The mark of the nozzle a line leaves (<paramref name="outlet"/>) or enters an item by, as on the item's drawing; null for none.</summary>
        internal static string NozzleMark(PlantModel model, EquipmentItem item, PipeRun run, bool outlet)
            => Nozzles(model, item).FirstOrDefault(n => n.Run == run && n.Outlet == outlet)?.Mark;

        static string Short(string s, int n) => s == null || s.Length <= n ? s ?? "" : s.Substring(0, n - 1) + ".";

        static Nozzle MakeNozzle(PipeRun run, Tuple<Vec3, Vec3> flange, bool outlet, Func<Vec3, Vec3> local)
        {
            var dir = flange.Item2.Normalized();
            var neck = Math.Max(150, 1.5 * run.OuterDiameter);
            var face = flange.Item1;
            var baseW = face - dir * neck;
            var l0 = local(baseW); var l1 = local(face);
            return new Nozzle
            {
                Size = run.Size, OD = run.OuterDiameter, Service = run.Tag + (outlet ? " outlet" : " inlet"), Run = run, Outlet = outlet,
                Base = l0, Face = l1, Dir = (l1 - l0).Normalized()
            };
        }

        /// <summary>The item's data for the table: its service, sizes and the operating data the 3D model carries.</summary>
        static List<KeyValuePair<string, string>> ItemData(EquipmentItem item, double r, double tt, bool vertical)
        {
            var list = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Service", item.Description ?? ""),
                new KeyValuePair<string, string>("Diameter (mm)", Mm(2 * r)),
            };
            if (tt > 0) list.Add(new KeyValuePair<string, string>(vertical ? "Height T/T (mm)" : "Length T/T (mm)", Mm(tt)));
            foreach (var kv in item.Properties)
            {
                if (kv.Key.StartsWith("Diameter") || kv.Key.StartsWith("Height") || kv.Key.StartsWith("Length T/T")) continue;
                list.Add(new KeyValuePair<string, string>(Short(kv.Key, 26), Short(kv.Value, 28)));
            }
            list.Add(new KeyValuePair<string, string>("Material", "Carbon steel"));
            return list.Take(14).ToList();
        }

        /// <summary>A part seen in a view: cylinders, heads, boxes and spheres as their outlines.</summary>
        static void Project(Dxf d, View view, Primitive p, Vec3 at, Vec3 axis, EquipmentItem item, bool vertical, double zc)
        {
            switch (p.Kind)
            {
                case PrimitiveKind.Cylinder:
                    Cylinder(d, view, at, axis, p.Height, p.Diameter, false);
                    break;
                case PrimitiveKind.Box:
                    {
                        // the item was un-turned: a box turned by an odd quarter has its sizes swapped
                        var odd = (item.Rotation / 90) % 2 == 1;
                        double sx = odd ? p.SizeY : p.SizeX, sy = odd ? p.SizeX : p.SizeY;
                        if (view == View.Front) d.Rect(at.X - sx / 2, at.Z - p.SizeZ / 2, at.X + sx / 2, at.Z + p.SizeZ / 2);
                        else d.Rect(at.X - sx / 2, at.Y - sy / 2, at.X + sx / 2, at.Y + sy / 2);
                        break;
                    }
                case PrimitiveKind.Head:
                    Head(d, view, at, axis, p.Diameter / 2, p.Depth);
                    break;
                case PrimitiveKind.Sphere:
                    {
                        // a hemispherical head is a sphere at the tangent line: its outer half
                        if ((p.Name ?? "").Contains("head"))
                        {
                            var outward = vertical ? (at.Z >= (item.Envelope.ZBottom + item.Envelope.ZTop) / 2 ? Vec3.PlusZ : Vec3.MinusZ)
                                                   : (at.X >= 0 ? Vec3.PlusX : Vec3.MinusX);
                            Head(d, view, at, outward, p.Diameter / 2, p.Diameter / 2);
                        }
                        else if (view == View.Front) d.Circle(at.X, at.Z, p.Diameter / 2);
                        else d.Circle(at.X, at.Y, p.Diameter / 2);
                        break;
                    }
            }
        }

        static double U(View v, Vec3 p) => p.X;
        static double W(View v, Vec3 p) => v == View.Front ? p.Z : p.Y;

        /// <summary>A cylinder from its base along its axis: a rectangle when the axis lies in the view, a circle when it points out of it.</summary>
        static void Cylinder(Dxf d, View view, Vec3 b, Vec3 axis, double length, double dia, bool hidden)
        {
            var normal = view == View.Front ? axis.Y : axis.Z;
            var r = dia / 2;
            if (Math.Abs(normal) > 0.7)
            {
                d.Circle(U(view, b), W(view, b), r);
                return;
            }
            var e = b + axis * length;
            double u0 = U(view, b), w0 = W(view, b), u1 = U(view, e), w1 = W(view, e);
            if (Math.Abs(u1 - u0) >= Math.Abs(w1 - w0)) d.Rect(Math.Min(u0, u1), w0 - r, Math.Max(u0, u1), w0 + r);
            else d.Rect(u0 - r, Math.Min(w0, w1), u0 + r, Math.Max(w0, w1));
        }

        /// <summary>A head: half an ellipse beyond its base when its axis lies in the view, the shell's circle (already drawn) when it does not.</summary>
        static void Head(Dxf d, View view, Vec3 b, Vec3 axis, double r, double depth)
        {
            var normal = view == View.Front ? axis.Y : axis.Z;
            if (Math.Abs(normal) > 0.7) return;
            double u = U(view, b), w = W(view, b);
            double au = axis.X, aw = view == View.Front ? axis.Z : axis.Y;
            if (Math.Abs(au) >= Math.Abs(aw))
            {
                if (au > 0) d.Ellipse(u, w, depth, r, -Math.PI / 2, Math.PI / 2);
                else d.Ellipse(u, w, depth, r, Math.PI / 2, 3 * Math.PI / 2);
            }
            else
            {
                if (aw > 0) d.Ellipse(u, w, r, depth, 0, Math.PI);
                else d.Ellipse(u, w, r, depth, Math.PI, 2 * Math.PI);
            }
        }
    }
}
