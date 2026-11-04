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
using DWSIM.Interfaces.Enums.GraphicObjects;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The plot plan of the 3D plant model: each item's outline in plan where the model places it,
    /// with its tag and coordinates, the pipe rack with its bays, the access road round the plant
    /// and the battery limit, a coordinate grid along the battery limit, the overall dimensions,
    /// a north arrow and the equipment location table, at a standard scale on the sheet and title
    /// block of the chosen standard. Drawn in CADCraft, saved as DXF and DWG and plotted to PDF and PNG.
    /// </summary>
    public static class PlotPlan
    {
        public class Result
        {
            public string Drawing, Pdf, Png, Error, Sheet;
            public int Items;
            public List<string> Failed = new List<string>();
        }

        static readonly int[] Scales = { 50, 100, 200, 250, 500, 1000, 2000 };

        const double TextHeight = 2.5, RoadWidth = 6000, RoadClearance = 6000, LimitClearance = 3000;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string M(double mm) => (mm / 1000).ToString("0.000", Inv);

        /// <summary>Items that go on the plot plan: equipment, not instruments, valves, mixers or splitters.</summary>
        static bool OnPlan(IFlowsheet fs, EquipmentItem e)
        {
            if (!e.Visible || e.IsInstrument || e.Name == null) return false;
            if (!fs.SimulationObjects.TryGetValue(e.Name, out var o) || o.GraphicObject == null) return false;
            switch (o.GraphicObject.ObjectType)
            {
                case ObjectType.Valve:
                case ObjectType.NodeIn:
                case ObjectType.NodeOut:
                case ObjectType.NodeEn:
                case ObjectType.OT_Recycle:
                case ObjectType.OT_EnergyRecycle:
                case ObjectType.OrificePlate:
                    return false;
            }
            return o.GetType().Name != "ReliefValve";
        }

        public static Result Export(IFlowsheet fs, string cadcraftFolder, string outputBase, string name, DrawingStyle style, TitleData title)
        {
            var result = new Result();
            PlantModel model;
            try { model = PlantModelBuilder.Build(fs, name, PlantLayout.Load(fs), ExportOptions.Load()); }
            catch (Exception ex) { result.Error = "the plant model could not be built (" + ex.Message + ")"; return result; }
            var items = model.Equipment.Where(e => OnPlan(fs, e)).OrderBy(e => e.Tag, StringComparer.OrdinalIgnoreCase).ToList();
            if (items.Count == 0) { result.Error = "the 3D model has no equipment"; return result; }
            result.Items = items.Count;

            // each item's outline in plan, about its origin, and the extents of the plant
            var temp = Path.Combine(Path.GetTempPath(), "dwsim-plot-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(temp);
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            var outlines = new Dictionary<EquipmentItem, (Dxf D, double X0, double Y0, double X1, double Y1)>();
            foreach (var item in items)
            {
                var d = new Dxf();
                double ix0 = double.MaxValue, iy0 = double.MaxValue, ix1 = double.MinValue, iy1 = double.MinValue;
                void Grow(double x, double y) { ix0 = Math.Min(ix0, x); iy0 = Math.Min(iy0, y); ix1 = Math.Max(ix1, x); iy1 = Math.Max(iy1, y); }
                foreach (var p in item.Parts.Where(p => !p.NoClash && p.Kind != PrimitiveKind.Torus))
                    Outline(d, p, Grow);
                if (ix0 > ix1) continue;
                outlines[item] = (d, ix0, iy0, ix1, iy1);
                x0 = Math.Min(x0, item.Origin.X + ix0); y0 = Math.Min(y0, item.Origin.Y + iy0);
                x1 = Math.Max(x1, item.Origin.X + ix1); y1 = Math.Max(y1, item.Origin.Y + iy1);
            }
            var rack = model.Rack;
            if (rack != null)
            {
                x0 = Math.Min(x0, rack.X0); x1 = Math.Max(x1, rack.X1);
                y0 = Math.Min(y0, rack.Y0); y1 = Math.Max(y1, rack.Y0 + rack.Width);
            }
            // the road round the plant and the battery limit outside it
            double rx0 = x0 - RoadClearance, ry0 = y0 - RoadClearance, rx1 = x1 + RoadClearance, ry1 = y1 + RoadClearance;
            double bx0 = rx0 - RoadWidth - LimitClearance, by0 = ry0 - RoadWidth - LimitClearance, bx1 = rx1 + RoadWidth + LimitClearance, by1 = ry1 + RoadWidth + LimitClearance;
            // round to whole metres, the grid starting on a whole step
            bx0 = Math.Floor(bx0 / 1000) * 1000; by0 = Math.Floor(by0 / 1000) * 1000; bx1 = Math.Ceiling(bx1 / 1000) * 1000; by1 = Math.Ceiling(by1 / 1000) * 1000;

            // the sheet and scale: the plan beside the location table, the smallest sheet on which
            // it comes out at 1:250 or larger, else the largest at the scale that fits
            DrawingSheets.Margins(style, out var ml, out var mo);
            SheetSize sheet = null; var scale = Scales.Last();
            double areaX0 = 0, areaX1 = 0, areaY0 = 0, areaY1 = 0;
            double pw = bx1 - bx0, ph = by1 - by0;
            foreach (var size in SheetSize.For(style))
            {
                sheet = size;
                areaX0 = ml + 12; areaX1 = size.Width - mo - DrawingSheets.TitleWidth - 12;
                areaY0 = mo + 12; areaY1 = size.Height - mo - 12;
                var fit = Scales.FirstOrDefault(s => pw / s + 40 <= areaX1 - areaX0 && ph / s + 40 <= areaY1 - areaY0);
                if (fit > 0) scale = fit;
                if (fit > 0 && fit <= 250) break;
            }
            double S = scale;
            result.Sheet = sheet.Name;
            var th = TextHeight * S;

            var cmds = new List<(string Name, JObject P)>();
            void C(string n, JObject p) => cmds.Add((n, p));
            JArray P(double x, double y) => new JArray(Math.Round(x, 1), Math.Round(y, 1));
            void Text(double x, double y, string t, double h, string just, double rot = 0)
            {
                var o = new JObject { ["at"] = P(x, y), ["text"] = t, ["height"] = h, ["justify"] = just };
                if (rot != 0) o["rotation"] = rot;
                C("text", o);
            }
            void Rect(double ax, double ay, double bx, double by) =>
                C("pline", new JObject { ["vertices"] = new JArray(P(ax, ay), P(bx, ay), P(bx, by), P(ax, by)), ["closed"] = true });

            C("linetype", new JObject { ["load"] = "*" });
            C("style", new JObject { ["name"] = DrawingSheets.TextStyle, ["font"] = DrawingSheets.Font, ["current"] = true });
            foreach (var (n, lt, lw) in new[] { ("EQUIPMENT", "Continuous", 0.5), ("RACK", "Continuous", 0.35), ("ROAD", "Continuous", 0.35), ("LIMIT", "PHANTOM", 0.5),
                ("GRID", "Continuous", 0.13), ("CENTER", "CENTER", 0.18), ("DIMENSION", "Continuous", 0.18), ("TEXT", "Continuous", 0.25),
                ("TABLE", "Continuous", 0.25), ("BORDER", "Continuous", 0.7), ("TITLE", "Continuous", 0.35), ("VIEWPORT", "Continuous", 0.0) })
                C("layer.new", new JObject { ["name"] = n, ["color"] = "white", ["linetype"] = lt, ["lineweight"] = lw });
            C("ltscale", new JObject { ["scale"] = 0.5 * S });

            // the battery limit, the road and the grid along the limit, every 5 or 10 m
            C("layer.current", new JObject { ["name"] = "LIMIT" });
            Rect(bx0, by0, bx1, by1);
            C("layer.current", new JObject { ["name"] = "ROAD" });
            Rect(rx0, ry0, rx1, ry1);
            Rect(rx0 - RoadWidth, ry0 - RoadWidth, rx1 + RoadWidth, ry1 + RoadWidth);
            C("layer.current", new JObject { ["name"] = "CENTER" });
            Rect(rx0 - RoadWidth / 2, ry0 - RoadWidth / 2, rx1 + RoadWidth / 2, ry1 + RoadWidth / 2);
            var step = Math.Max(pw, ph) > 80000 ? 10000 : 5000;
            C("layer.current", new JObject { ["name"] = "GRID" });
            for (var gx = Math.Ceiling(bx0 / step) * step; gx <= bx1; gx += step)
            {
                C("line", new JObject { ["points"] = new JArray(P(gx, by0), P(gx, by0 - 3 * S)) });
                C("line", new JObject { ["points"] = new JArray(P(gx, by1), P(gx, by1 + 3 * S)) });
            }
            for (var gy = Math.Ceiling(by0 / step) * step; gy <= by1; gy += step)
            {
                C("line", new JObject { ["points"] = new JArray(P(bx0, gy), P(bx0 - 3 * S, gy)) });
                C("line", new JObject { ["points"] = new JArray(P(bx1, gy), P(bx1 + 3 * S, gy)) });
            }
            C("layer.current", new JObject { ["name"] = "TEXT" });
            for (var gx = Math.Ceiling(bx0 / step) * step; gx <= bx1; gx += step)
                Text(gx, by0 - 4 * S, "E " + (gx / 1000).ToString("0", Inv), 0.8 * th, "TC");
            for (var gy = Math.Ceiling(by0 / step) * step; gy <= by1; gy += step)
                Text(bx0 - 4 * S, gy, "N " + (gy / 1000).ToString("0", Inv), 0.8 * th, "MR");
            Text((rx0 + rx1) / 2, ry0 - RoadWidth / 2 + 0.2 * th, "ACCESS ROAD, 6 m", th, "BC");
            Text(bx0 + 2 * S, by1 - 2 * S, "BATTERY LIMIT", th, "TL");

            // the pipe rack: its outline, a column at each bay and its top of steel
            if (rack != null)
            {
                C("layer.current", new JObject { ["name"] = "RACK" });
                Rect(rack.X0, rack.Y0, rack.X1, rack.Y0 + rack.Width);
                var bays = Math.Max(1, (int)Math.Ceiling((rack.X1 - rack.X0) / PipeRack.BayLength));
                for (var i = 0; i <= bays; i++)
                {
                    var x = Math.Min(rack.X1, rack.X0 + i * PipeRack.BayLength);
                    foreach (var y in new[] { rack.Y0, rack.Y0 + rack.Width })
                        Rect(x - 150, y - 150, x + 150, y + 150);
                    C("line", new JObject { ["points"] = new JArray(P(x, rack.Y0), P(x, rack.Y0 + rack.Width)) });
                }
                C("layer.current", new JObject { ["name"] = "TEXT" });
                Text((rack.X0 + rack.X1) / 2, rack.Y0 + rack.Width / 2, "PIPE RACK, TOS EL " + (rack.Top / 1000).ToString("+0.000", Inv), th, "MC");
            }

            // the equipment: outlines as blocks at their places, tag and coordinates under them,
            // a centre mark on each
            var used = new HashSet<string>();
            var k = 0;
            foreach (var item in items.Where(outlines.ContainsKey))
            {
                var o = outlines[item];
                var file = Path.Combine(temp, "e" + (++k) + ".dxf");
                o.D.Save(file);
                var block = "PP_" + System.Text.RegularExpressions.Regex.Replace(item.Tag ?? "", "[^A-Za-z0-9_-]", "_");
                var bn = block; var j = 1;
                while (!used.Add(bn.ToUpperInvariant())) bn = block + "_" + (++j);
                C("layer.current", new JObject { ["name"] = "EQUIPMENT" });
                C("insert", new JObject { ["name"] = bn, ["file"] = file.Replace('\\', '/'), ["at"] = P(item.Origin.X, item.Origin.Y) });
                C("layer.current", new JObject { ["name"] = "CENTER" });
                var cm = Math.Min(Math.Min(o.X1 - o.X0, o.Y1 - o.Y0) * 0.3, 2 * S);
                C("line", new JObject { ["points"] = new JArray(P(item.Origin.X - cm, item.Origin.Y), P(item.Origin.X + cm, item.Origin.Y)) });
                C("line", new JObject { ["points"] = new JArray(P(item.Origin.X, item.Origin.Y - cm), P(item.Origin.X, item.Origin.Y + cm)) });
                C("layer.current", new JObject { ["name"] = "TEXT" });
                var ty = item.Origin.Y + o.Y0 - 1.5 * S;
                Text(item.Origin.X + (o.X0 + o.X1) / 2, ty, item.Tag, 1.2 * th, "TC");
                Text(item.Origin.X + (o.X0 + o.X1) / 2, ty - 1.8 * th, "E " + M(item.Origin.X) + "  N " + M(item.Origin.Y), 0.8 * th, "TC");
            }

            // the overall dimensions of the battery limit
            C("dimstyle", new JObject { ["name"] = "PP", ["DIMSCALE"] = S, ["DIMTXT"] = TextHeight, ["DIMASZ"] = 2.5, ["DIMEXO"] = 1.5, ["DIMEXE"] = 1.5, ["DIMGAP"] = 0.8, ["DIMDEC"] = 0, ["DIMTXSTY"] = DrawingSheets.TextStyle, ["current"] = true });
            C("layer.current", new JObject { ["name"] = "DIMENSION" });
            C("dimlinear", new JObject { ["p1"] = P(bx0, by1), ["p2"] = P(bx1, by1), ["at"] = P((bx0 + bx1) / 2, by1 + 10 * S) });
            C("dimlinear", new JObject { ["p1"] = P(bx1, by0), ["p2"] = P(bx1, by1), ["at"] = P(bx1 + 12 * S, (by0 + by1) / 2), ["rotation"] = 90 });

            // the sheet: border, title block, location table, north arrow and notes
            title.Scale = "1:" + scale;
            C("layout.new", new JObject { ["name"] = "PLOT", ["viewport"] = false });
            C("pagesetup", new JObject
            {
                ["layout"] = "PLOT", ["device"] = "CADCraft PDF", ["width"] = sheet.Width, ["height"] = sheet.Height, ["landscape"] = true,
                ["margins"] = new JArray(0, 0, 0, 0), ["plotArea"] = "layout", ["scale"] = 1, ["lineweights"] = true, ["plotStyleTable"] = "monochrome.ctb"
            });
            C("layout.set", new JObject { ["name"] = "PLOT" });
            C("pspace", new JObject());
            var border = Path.Combine(temp, "border.dxf");
            DrawingSheets.Border(style, sheet).Save(border);
            C("layer.current", new JObject { ["name"] = "BORDER" });
            C("insert", new JObject { ["name"] = "SHEET_BORDER", ["file"] = border.Replace('\\', '/'), ["at"] = P(0, 0) });
            var tb = Path.Combine(temp, "title.dxf");
            DrawingSheets.TitleBlock(style, out var titleHeight).Save(tb);
            C("layer.current", new JObject { ["name"] = "TITLE" });
            C("insert", new JObject
            {
                ["name"] = "TITLE_BLOCK", ["file"] = tb.Replace('\\', '/'), ["at"] = P(sheet.Width - mo, mo),
                ["attribs"] = JObject.FromObject(DrawingSheets.Attributes(title, sheet, style))
            });

            C("tablestyle", new JObject { ["name"] = "PP", ["textHeight"] = TextHeight, ["margin"] = 0.8, ["current"] = true });
            foreach (var cs in new[] { "Header", "Data" })
                C("tablestyle.cellstyle", new JObject { ["style"] = "PP", ["name"] = cs, ["textStyle"] = DrawingSheets.TextStyle, ["textHeight"] = TextHeight });
            C("layer.current", new JObject { ["name"] = "TABLE" });
            var tx = sheet.Width - mo - DrawingSheets.TitleWidth;
            var top = sheet.Height - mo - 5;
            const double rh = 5.5;
            var notes = new[]
            {
                "NOTES",
                "1. COORDINATES AND ELEVATIONS IN M, DIMENSIONS IN MM.",
                "2. NORTH IS +Y OF THE PLANT. GRADE EL +0.000.",
                "3. PRELIMINARY LAYOUT FROM THE DWSIM 3D PLANT MODEL.",
            };
            var room = (int)Math.Floor((top - (mo + titleHeight + 8 + notes.Length * 4.5)) / rh) - 1;
            string Size(EquipmentItem e) { var o = outlines[e]; return ((o.X1 - o.X0) / 1000).ToString("0.0", Inv) + " x " + ((o.Y1 - o.Y0) / 1000).ToString("0.0", Inv); }
            var rows = items.Where(outlines.ContainsKey).Select(e => new JArray(Short(e.Tag, 12), Short(e.Description ?? "", 22), M(e.Origin.X), M(e.Origin.Y), Size(e))).ToList();
            if (rows.Count > room) { rows = rows.Take(Math.Max(1, room - 1)).ToList(); rows.Add(new JArray("...", "", "", "", "")); }
            C("table", new JObject
            {
                ["at"] = P(tx, top), ["rows"] = rows.Count, ["cols"] = 5, ["rowHeight"] = rh, ["colWidth"] = DrawingSheets.TitleWidth / 5,
                ["header"] = new JArray("TAG", "DESCRIPTION", "E (m)", "N (m)", "SIZE E x N (m)"), ["cells"] = new JArray(rows)
            });
            C("layer.current", new JObject { ["name"] = "TEXT" });
            var ny = mo + titleHeight + 6 + (notes.Length - 1) * 4.5;
            for (var i = 0; i < notes.Length; i++)
                C("text", new JObject { ["at"] = P(tx, ny - i * 4.5), ["text"] = notes[i], ["height"] = i == 0 ? 2.5 : 2.2, ["justify"] = "BL" });

            // the north arrow, top left of the plan
            {
                double cx = areaX0 + 12, cy = areaY1 - 14;
                C("layer.current", new JObject { ["name"] = "TEXT" });
                C("circle", new JObject { ["center"] = P(cx, cy), ["radius"] = 7 });
                C("solid", new JObject { ["points"] = new JArray(P(cx, cy + 7), P(cx - 2.5, cy - 5), P(cx, cy - 2.5)) });
                C("pline", new JObject { ["vertices"] = new JArray(P(cx, cy + 7), P(cx + 2.5, cy - 5), P(cx, cy - 2.5)), ["closed"] = true });
                C("text", new JObject { ["at"] = P(cx, cy + 9), ["text"] = "N", ["height"] = 3.5, ["justify"] = "BC" });
                C("text", new JObject { ["at"] = P(cx, cy - 10), ["text"] = "SCALE 1:" + scale, ["height"] = 2.5, ["justify"] = "TC" });
            }

            File.Delete(outputBase + ".dxf");
            CadJob.Report("CADCraft: drawing the plot plan");
            using (var session = new CADCraftSession(cadcraftFolder))
            {
                if (session.Tool("new_drawing", new JObject { ["metric"] = true }, "new drawing") == null)
                {
                    result.Error = string.Join("; ", session.Failed);
                    return result;
                }
                foreach (var c in cmds) session.Execute(c.Name, c.P);

                // the viewport over the plan, at 1:S
                session.Execute("layer.current", new JObject { ["name"] = "VIEWPORT" });
                session.Execute("layer.set", new JObject { ["name"] = "VIEWPORT", ["plot"] = false });
                var vp = session.Execute("mview", new JObject { ["p1"] = P(areaX0, areaY0), ["p2"] = P(areaX1, areaY1) });
                var handle = vp == null ? null : (JObject.Parse(vp)["viewports"] as JArray)?.FirstOrDefault()?.ToString();
                double mx0 = bx0 - 14 * S, mx1 = bx1 + 20 * S, my0 = by0 - 10 * S, my1 = by1 + 16 * S;
                if (handle != null)
                    session.Execute("viewport.set", new JObject { ["handle"] = handle, ["scale"] = 1.0 / S, ["locked"] = true, ["center"] = P((mx0 + mx1) / 2, (my0 + my1) / 2) });

                DrawingSheets.Save(session, outputBase, "PLOT", null);
                result.Failed.AddRange(session.Failed);
            }
            try { Directory.Delete(temp, true); } catch (Exception) { }
            if (File.Exists(outputBase + ".dxf")) result.Drawing = outputBase + ".dxf";
            if (File.Exists(outputBase + ".pdf")) result.Pdf = outputBase + ".pdf";
            if (File.Exists(outputBase + ".png")) result.Png = outputBase + ".png";
            if (result.Drawing == null) result.Error = "CADCraft did not save the drawing" + (result.Failed.Count > 0 ? " (" + result.Failed.Last() + ")" : "");
            return result;
        }

        static string Short(string s, int n) => s == null || s.Length <= n ? s ?? "" : s.Substring(0, n - 1) + ".";

        /// <summary>A part seen from above, about the item's origin: circles for upright cylinders and spheres, rectangles for the rest.</summary>
        static void Outline(Dxf d, Primitive p, Action<double, double> grow)
        {
            var at = p.Point;
            switch (p.Kind)
            {
                case PrimitiveKind.Box:
                    d.Rect(at.X - p.SizeX / 2, at.Y - p.SizeY / 2, at.X + p.SizeX / 2, at.Y + p.SizeY / 2);
                    grow(at.X - p.SizeX / 2, at.Y - p.SizeY / 2); grow(at.X + p.SizeX / 2, at.Y + p.SizeY / 2);
                    break;
                case PrimitiveKind.Cylinder:
                    {
                        var axis = p.AxisVector ?? (p.Axis == "X" ? Vec3.PlusX : p.Axis == "Y" ? new Vec3(0, 1, 0) : Vec3.PlusZ);
                        var r = p.Diameter / 2;
                        if (Math.Abs(axis.Z) > 0.7)
                        {
                            d.Circle(at.X, at.Y, r);
                            grow(at.X - r, at.Y - r); grow(at.X + r, at.Y + r);
                        }
                        else
                        {
                            var e = at + axis * p.Height;
                            if (Math.Abs(axis.X) >= Math.Abs(axis.Y)) { d.Rect(Math.Min(at.X, e.X), at.Y - r, Math.Max(at.X, e.X), at.Y + r); grow(Math.Min(at.X, e.X), at.Y - r); grow(Math.Max(at.X, e.X), at.Y + r); }
                            else { d.Rect(at.X - r, Math.Min(at.Y, e.Y), at.X + r, Math.Max(at.Y, e.Y)); grow(at.X - r, Math.Min(at.Y, e.Y)); grow(at.X + r, Math.Max(at.Y, e.Y)); }
                        }
                        break;
                    }
                case PrimitiveKind.Head:
                    {
                        var axis = p.AxisVector ?? Vec3.PlusZ;
                        if (Math.Abs(axis.Z) > 0.7) break;
                        var r = p.Diameter / 2;
                        if (Math.Abs(axis.X) >= Math.Abs(axis.Y))
                        {
                            if (axis.X > 0) d.Ellipse(at.X, at.Y, p.Depth, r, -Math.PI / 2, Math.PI / 2); else d.Ellipse(at.X, at.Y, p.Depth, r, Math.PI / 2, 3 * Math.PI / 2);
                            grow(at.X + Math.Sign(axis.X) * p.Depth, at.Y - r); grow(at.X, at.Y + r);
                        }
                        else
                        {
                            if (axis.Y > 0) d.Ellipse(at.X, at.Y, r, p.Depth, 0, Math.PI); else d.Ellipse(at.X, at.Y, r, p.Depth, Math.PI, 2 * Math.PI);
                            grow(at.X - r, at.Y + Math.Sign(axis.Y) * p.Depth); grow(at.X + r, at.Y);
                        }
                        break;
                    }
                case PrimitiveKind.Sphere:
                    {
                        // a hemispherical head lies inside its shell's outline from above
                        if ((p.Name ?? "").Contains("head")) break;
                        var r = p.Diameter / 2;
                        d.Circle(at.X, at.Y, r);
                        grow(at.X - r, at.Y - r); grow(at.X + r, at.Y + r);
                        break;
                    }
            }
        }
    }
}
