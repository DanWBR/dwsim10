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
using System.IO;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    public static partial class EquipmentDrawings
    {
        /// <summary>A pump, compressor or expander on its skid: drawn as a machine.</summary>
        static bool IsMachine(IFlowsheet fs, EquipmentItem e)
        {
            if (e.Name == null || !fs.SimulationObjects.TryGetValue(e.Name, out var o) || o.GraphicObject == null) return false;
            var t = o.GraphicObject.ObjectType;
            return (t == ObjectType.Pump || t == ObjectType.Compressor || t == ObjectType.Expander || t == ObjectType.CompressorExpander) &&
                e.Parts.Any(p => p.Name == "skid");
        }

        /// <summary>
        /// The general arrangement of a pump or a compressor train: front and plan views of the skid,
        /// the casing and the driver, the shaft centre line, the anchor bolts and the foundation
        /// under grade, the overall dimensions, the shaft height and the bolt spacing, the nozzles
        /// with their schedule, and the machine's data.
        /// </summary>
        static string DrawMachine(CADCraftSession session, IFlowsheet fs, PlantModel model, EquipmentItem item, string outputBase, DrawingStyle style, TitleData title, string temp)
        {
            var unturn = (360 - item.Rotation) % 360;
            Vec3 Local(Vec3 world) => (world - item.Origin).TurnedZ(unturn);
            Vec3 Axis(Primitive p) => (p.AxisVector ?? (p.Axis == "X" ? Vec3.PlusX : p.Axis == "Y" ? new Vec3(0, 1, 0) : Vec3.PlusZ)).TurnedZ(unturn);
            var odd = (item.Rotation / 90) % 2 == 1;
            var parts = item.Parts.Where(p => !p.NoClash && p.Kind != PrimitiveKind.Torus).ToList();
            var nozzles = Nozzles(model, item);
            fs.SimulationObjects.TryGetValue(item.Name, out var obj);
            var type = obj?.GraphicObject?.ObjectType ?? ObjectType.Pump;

            // the extents of the parts and the nozzles, in the machine's own frame
            double fx0 = double.MaxValue, fx1 = double.MinValue, py0 = double.MaxValue, py1 = double.MinValue, fz1 = 0;
            void Grow(Vec3 a, double r)
            {
                fx0 = Math.Min(fx0, a.X - r); fx1 = Math.Max(fx1, a.X + r);
                py0 = Math.Min(py0, a.Y - r); py1 = Math.Max(py1, a.Y + r);
                fz1 = Math.Max(fz1, a.Z + r);
            }
            foreach (var p in parts)
            {
                var at = Local(item.Origin + p.Point);
                switch (p.Kind)
                {
                    case PrimitiveKind.Box:
                        {
                            double sx = odd ? p.SizeY : p.SizeX, sy = odd ? p.SizeX : p.SizeY;
                            Grow(at + new Vec3(-sx / 2, -sy / 2, -p.SizeZ / 2), 0);
                            Grow(at + new Vec3(sx / 2, sy / 2, p.SizeZ / 2), 0);
                            break;
                        }
                    case PrimitiveKind.Cylinder:
                        Grow(at, p.Diameter / 2);
                        Grow(at + Axis(p) * p.Height, p.Diameter / 2);
                        break;
                    default:
                        Grow(at, p.Diameter / 2);
                        break;
                }
            }
            foreach (var n in nozzles) { Grow(n.Face, n.OD); Grow(n.Base, 0); }

            // the skid, the shaft (the casing's centre height, the driver's axis) and the anchor bolts
            var skid = parts.First(p => p.Name == "skid");
            var sc = Local(item.Origin + skid.Point);
            double skL = odd ? skid.SizeY : skid.SizeX, skW = odd ? skid.SizeX : skid.SizeY, skH = skid.SizeZ;
            double skX0 = sc.X - skL / 2, skX1 = sc.X + skL / 2, skY0 = sc.Y - skW / 2, skY1 = sc.Y + skW / 2;
            var casing = parts.FirstOrDefault(p => p.Name == "casing");
            var zc = casing != null ? Local(item.Origin + casing.Point).Z : skH + 300;
            var driver = parts.FirstOrDefault(p => p.Name == "motor" || p.Name == "driver" || p.Name == "generator");
            var shaftY = driver != null ? Local(item.Origin + driver.Point).Y : 0;
            var inset = Math.Min(150, 0.12 * Math.Min(skL, skW));
            double bx0 = skX0 + inset, bx1 = skX1 - inset, by0 = skY0 + inset, by1 = skY1 - inset;
            double fw = fx1 - fx0 + 600, fh = fz1 + 600, pw = fw, ph = py1 - py0;

            // the sheet and scale: the front view over the plan, the smallest sheet on which the
            // machine comes out at 1:50 or larger, else the largest at the scale that fits
            DrawingSheets.Margins(style, out var ml, out var mo);
            SheetSize sheet = null; var scale = Scales.Last();
            double areaX0 = 0, areaX1 = 0, areaY0 = 0, areaY1 = 0;
            foreach (var size in SheetSize.For(style).Take(3))
            {
                sheet = size;
                areaX0 = ml + 30; areaX1 = size.Width - mo - DrawingSheets.TitleWidth - 15;
                areaY0 = mo + 25; areaY1 = size.Height - mo - 15;
                var fit = Scales.FirstOrDefault(s => Math.Max(fw, pw) / s + 60 <= areaX1 - areaX0 && (fh + ph) / s + 70 <= areaY1 - areaY0);
                if (fit > 0) scale = fit;
                if (fit > 0 && fit <= 50) break;
            }
            double S = scale;
            foreach (var n in nozzles) n.OD = Math.Max(n.OD, 1.5 * S);

            var front = new Dxf(); var plan = new Dxf();
            foreach (var view in new[] { View.Front, View.Plan })
            {
                var d = view == View.Front ? front : plan;
                foreach (var p in parts) Project(d, view, p, Local(item.Origin + p.Point), Axis(p), item, false, zc);
                foreach (var n in nozzles)
                {
                    var neck = Math.Max(0, (n.Face - n.Base).Length);
                    Cylinder(d, view, n.Base, n.Dir, neck, n.OD, false);
                    var t = Math.Max(30, 0.25 * n.OD);
                    Cylinder(d, view, n.Face - n.Dir * t, n.Dir, t, n.OD * 1.7, false);
                }
                d.Layer = "CENTER";
                d.Line(fx0 - 100, view == View.Front ? zc : shaftY, fx1 + 100, view == View.Front ? zc : shaftY);
                d.Layer = "0";
            }
            // the anchor bolts in plan, with their centre marks
            foreach (var (x, y) in new[] { (bx0, by0), (bx1, by0), (bx0, by1), (bx1, by1) })
            {
                plan.Circle(x, y, 13);
                plan.Layer = "CENTER";
                plan.Line(x - 40, y, x + 40, y); plan.Line(x, y - 40, x, y + 40);
                plan.Layer = "0";
            }
            // grade, and the foundation block under it (by civil), hidden
            front.Line(fx0 - 300, 0, fx1 + 300, 0);
            front.Layer = "HIDDEN";
            front.Rect(skX0 - 150, -400, skX1 + 150, 0);
            foreach (var x in new[] { bx0, bx1 }) front.Line(x, -300, x, skH * 0.8);
            front.Layer = "0";

            var planAt = new Vec3(0, -40 * S - py1, 0);
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

            // dimensions: skid length, overall height and shaft height in front; skid width and
            // anchor bolt spacing in plan
            C("dimstyle", new JObject { ["name"] = "GA", ["DIMSCALE"] = S, ["DIMTXT"] = TextHeight, ["DIMASZ"] = 2.5, ["DIMEXO"] = 1.5, ["DIMEXE"] = 1.5, ["DIMGAP"] = 0.8, ["DIMDEC"] = 0, ["DIMTXSTY"] = DrawingSheets.TextStyle, ["current"] = true });
            C("layer.current", new JObject { ["name"] = "DIMENSION" });
            void Dim(double x1, double y1, double x2, double y2, double ax, double ay, bool rotated)
            {
                var o = new JObject { ["p1"] = P(x1, y1), ["p2"] = P(x2, y2), ["at"] = P(ax, ay) };
                if (rotated) o["rotation"] = 90;
                C("dimlinear", o);
            }
            Dim(skX0, 0, skX1, 0, (skX0 + skX1) / 2, -16 * S, false);
            Dim(fx0, 0, fx0, zc, fx0 - 12 * S, zc / 2, true);
            Dim(fx0, 0, fx0, fz1, fx0 - 25 * S, fz1 / 2, true);
            Dim(skX1, planAt.Y + skY0, skX1, planAt.Y + skY1, fx1 + 12 * S, planAt.Y + (skY0 + skY1) / 2, true);
            Dim(bx1, planAt.Y + by0, bx1, planAt.Y + by1, fx1 + 24 * S, planAt.Y + (by0 + by1) / 2, true);
            Dim(bx0, planAt.Y + by0, bx1, planAt.Y + by0, (bx0 + bx1) / 2, planAt.Y + py0 - 8 * S, false);

            // nozzle marks, the shaft and the view titles
            C("layer.current", new JObject { ["name"] = "TEXT" });
            var th = TextHeight * S;
            foreach (var n in nozzles)
            {
                var at = n.Face + n.Dir * (6 * S);
                if (Math.Abs(n.Dir.Y) > 0.7) at = n.Face + new Vec3(n.OD + 4 * S, 0, 0);
                C("text", new JObject { ["at"] = P(at.X, at.Z), ["text"] = n.Mark, ["height"] = th, ["justify"] = "MC" });
                var pt = new Vec3(n.Face.X, n.Face.Y, 0) + new Vec3(n.Dir.X, n.Dir.Y, 0) * (6 * S);
                if (Math.Abs(n.Dir.Z) < 0.7) C("text", new JObject { ["at"] = P(planAt.X + pt.X, planAt.Y + pt.Y), ["text"] = n.Mark, ["height"] = th, ["justify"] = "MC" });
                else C("text", new JObject { ["at"] = P(planAt.X + n.Face.X + n.OD + 3 * S, planAt.Y + n.Face.Y), ["text"] = n.Mark, ["height"] = th, ["justify"] = "ML" });
            }
            C("text", new JObject { ["at"] = P(fx1 + 100 + 2 * S, zc), ["text"] = "CL SHAFT", ["height"] = th * 0.8, ["justify"] = "ML" });
            var scaleText = "1:" + scale;
            C("text", new JObject { ["at"] = P((fx0 + fx1) / 2, -26 * S), ["text"] = "FRONT VIEW  " + scaleText, ["height"] = 3.5 * S, ["justify"] = "TC" });
            C("text", new JObject { ["at"] = P((fx0 + fx1) / 2, planAt.Y + py0 - 16 * S), ["text"] = "PLAN VIEW  " + scaleText, ["height"] = 3.5 * S, ["justify"] = "TC" });

            // the machine's data, with the materials from the conditions at its nozzles
            var temps = nozzles.Where(n => n.Run.Temperature > 0).Select(n => n.Run.Temperature - 273.15).DefaultIfEmpty(double.NaN).ToList();
            var press = nozzles.Where(n => n.Run.Pressure > 0).Select(n => n.Run.Pressure / 1e5 - 1.01325).DefaultIfEmpty(double.NaN).ToList();
            var design = DesignBasis.For(temps.Max(), press.Max(), temps.Min());
            var data = new List<KeyValuePair<string, string>>();
            void Add(string k, string v) { if (!string.IsNullOrEmpty(v)) data.Add(new KeyValuePair<string, string>(k, v)); }
            Add("Service", item.Description ?? "");
            Add("Type", type == ObjectType.Pump ? "Centrifugal pump" : type == ObjectType.Expander ? "Turboexpander" : "Centrifugal compressor");
            item.Properties.TryGetValue("Power (kW)", out var power);
            Add("Power (kW)", power);
            if (type == ObjectType.Expander) Add("Driven machine", "Generator or compressor");
            else if (double.TryParse(power, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var kw))
                Add("Driver", "Electric motor, " + DataSheets.Motor(kw) + " kW");
            Add("Skid L x W x H (mm)", Mm(skL) + " x " + Mm(skW) + " x " + Mm(skH));
            Add("Shaft centre line (mm)", Mm(zc));
            Add("Casing material", design.Casting);
            Add("Flange rating", design.Class + " RF");
            Add("Anchor bolts", "4 x M24, " + Mm(bx1 - bx0) + " x " + Mm(by1 - by0));
            Add("Foundation", "Concrete block (by civil)");

            var rows = NozzleRows(nozzles, n => Math.Sqrt(Math.Pow(n.Face.Y - shaftY, 2) + Math.Pow(n.Face.Z - zc, 2)));
            double mx0 = fx0 - 32 * S, mx1 = fx1 + 30 * S, my0 = planAt.Y + py0 - 22 * S, my1 = fz1 + 12 * S;
            return Finish(session, cmds, item, title, scaleText, sheet, style, data, rows, areaX0, areaY0, areaX1, areaY1, mx0, mx1, my0, my1, S, outputBase, temp);
        }
    }
}
