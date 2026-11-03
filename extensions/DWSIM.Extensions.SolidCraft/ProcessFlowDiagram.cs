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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using Newtonsoft.Json.Linq;
using Converter = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using ObjectType = DWSIM.Interfaces.Enums.GraphicObjects.ObjectType;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The process flow diagram of a flowsheet, drawn in CADCraft: each unit operation as its
    /// equipment symbol where its icon is, each stream along the route the flowsheet draws it, with
    /// its number, a stream table with the conditions, flows and compound flows of every stream in
    /// the units of the flowsheet, and a sheet with border, zones and title block in the chosen
    /// style. Saved as DXF and DWG, and plotted to PDF and PNG.
    /// </summary>
    public static class ProcessFlowDiagram
    {
        public class Result
        {
            public string Drawing, Dwg, Pdf, Png, Error, Sheet;
            public int Units, Streams;
            public List<string> Failed = new List<string>();
        }

        enum Symbol { None, PassThrough, Node, VesselV, VesselH, Tank, Column, Pump, Compressor, Expander, Heater, Cooler, Exchanger, AirCooler, Valve, Reactor, Cstr, Pfr, Filter, Separator, Generic }

        class Unit
        {
            public ISimulationObject Obj;
            public IGraphicObject Go;
            public Symbol Symbol;
        }

        class Line
        {
            public string Tag;
            public int Number;

            /// <summary>What the diamond and the table show: the stream's own tag when the flowsheet numbers its streams, else <see cref="Number"/>.</summary>
            public string Id;
            public bool Energy;
            public List<Pt> Points = new List<Pt>();
            public IGraphicObject Icon;
            public bool Feed, Product, ArrowAtEnd = true;
            public string From = "", To = "";
            public IMaterialStream Stream;
        }

        struct Pt
        {
            public double X, Y;
            public Pt(double x, double y) { X = x; Y = y; }
        }

        const double ColumnWidth = 26, LabelWidth = 44, RowHeight = 6, TextHeight = 2.5;

        /// <summary>The compounds listed one by one in the stream table; the others share a row.</summary>
        const int MaxCompounds = 20;

        public static Result Export(IFlowsheet fs, string cadcraftFolder, string outputBase, DrawingStyle style, TitleData title)
        {
            var result = new Result();
            var units = Units(fs);
            var lines = Lines(fs);
            result.Units = units.Count(u => u.Symbol != Symbol.PassThrough);
            result.Streams = lines.Count(l => !l.Energy);
            if (units.Count == 0 && lines.Count == 0)
            {
                result.Error = "the flowsheet has nothing to draw";
                return result;
            }

            // the canvas extents of what is drawn
            var pts = new List<Pt>();
            foreach (var u in units) { pts.Add(new Pt(u.Go.X, u.Go.Y)); pts.Add(new Pt(u.Go.X + u.Go.Width, u.Go.Y + u.Go.Height)); }
            foreach (var l in lines) pts.AddRange(l.Points);
            double bx0 = pts.Min(p => p.X), by0 = pts.Min(p => p.Y), bx1 = pts.Max(p => p.X), by1 = pts.Max(p => p.Y);
            double bw = Math.Max(bx1 - bx0, 50), bh = Math.Max(by1 - by0, 50);
            // room on the sheet, in millimetres, for the names at the battery limits and the labels under the items
            double TextWidth(string t) => (t ?? "").Length * TextHeight * 0.8 + 6;
            var mxL = Math.Max(6, lines.Where(l => l.Feed).Select(l => TextWidth(l.Tag)).DefaultIfEmpty(0).Max());
            var mxR = Math.Max(6, lines.Where(l => l.Product).Select(l => TextWidth(l.Tag)).DefaultIfEmpty(0).Max());
            const double myT = 10, myB = 16;

            // the stream table, split into column groups as wide as the sheet allows
            var table = StreamTable(fs, lines.Where(l => !l.Energy).ToList());

            // the smallest sheet on which the icons come out at a readable size
            SheetSize sheet = null;
            double k = 0, areaX0 = 0, areaY0 = 0, areaX1 = 0, areaY1 = 0, tableTop = 0, titleHeight = 0;
            List<int[]> groups = null;
            DrawingSheets.TitleBlock(style, out titleHeight);
            foreach (var size in SheetSize.For(style))
            {
                sheet = size;
                DrawingSheets.Margins(style, out var left, out var other);
                double ix0 = left + 5, ix1 = size.Width - other - 5;
                var perGroup = Math.Max(1, (int)((ix1 - ix0 - LabelWidth) / ColumnWidth));
                groups = Groups(table.Count > 0 ? table[0].Length - 1 : 0, perGroup);
                var tableHeight = groups.Count * table.Count * RowHeight + (groups.Count - 1) * 4;
                var widest = groups.Count == 0 ? 0 : LabelWidth + groups.Max(g => g.Length) * ColumnWidth;
                var beside = ix0 + widest <= size.Width - other - DrawingSheets.TitleWidth - 8;
                var bottom = beside ? Math.Max(titleHeight, tableHeight) : titleHeight + 6 + tableHeight;
                tableTop = other + (beside ? Math.Max(titleHeight, tableHeight) : bottom);
                areaX0 = left + 8; areaX1 = size.Width - other - 8;
                areaY0 = other + bottom + 10; areaY1 = size.Height - other - 8;
                k = Math.Min((areaX1 - areaX0 - mxL - mxR) / bw, (areaY1 - areaY0 - myT - myB) / bh);
                if (k >= 0.25 && tableHeight <= 0.5 * size.Height) break;
            }
            k = Math.Min(k, 0.8);
            result.Sheet = sheet.Name;

            // canvas to model space: millimetres on the sheet, y up, the extents' corner at the origin
            Pt T(double x, double y) => new Pt(Math.Round((x - bx0) * k, 2), Math.Round(-(y - by0) * k, 2));

            var temp = Path.Combine(Path.GetTempPath(), "dwsim-pfd-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(temp);
            var cmds = new List<SolidCraftScript.Command>();
            void C(string name, JObject p, string what = null) => cmds.Add(new SolidCraftScript.Command { Name = name, Params = p, Subject = what ?? name });

            // layers: line weights of ISO 128 for the process lines and the symbols
            C("linetype", new JObject { ["load"] = "*" });
            C("style", new JObject { ["name"] = "SANS", ["font"] = "Arial", ["current"] = true });
            foreach (var (name, lt, lw) in new[] { ("EQUIPMENT", "Continuous", 0.5), ("PROCESS", "Continuous", 0.5), ("UTILITY", "DASHED", 0.35),
                ("STREAMNUMBER", "Continuous", 0.25), ("TEXT", "Continuous", 0.25), ("TABLE", "Continuous", 0.25), ("BORDER", "Continuous", 0.7), ("TITLE", "Continuous", 0.35), ("VIEWPORT", "Continuous", 0.0) })
                C("layer.new", new JObject { ["name"] = name, ["color"] = "white", ["linetype"] = lt, ["lineweight"] = lw });
            C("ltscale", new JObject { ["scale"] = 0.5 });

            // equipment: one block per item, drawn at its real size on the sheet
            var n = 0;
            var used = new HashSet<string>();
            foreach (var u in units.Where(u => u.Symbol != Symbol.PassThrough && u.Symbol != Symbol.Node))
            {
                n++;
                double w = u.Go.Width * k, h = u.Go.Height * k;
                var c = T(u.Go.X + u.Go.Width / 2.0, u.Go.Y + u.Go.Height / 2.0);
                var d = new Dxf();
                var half = Draw(d, u.Symbol, w, h, u.Go.FlippedH);
                var file = Path.Combine(temp, "u" + n + ".dxf");
                d.Save(file);
                var block = BlockName(u.Go.Tag, used);
                C("layer.current", new JObject { ["name"] = "EQUIPMENT" });
                C("insert", new JObject { ["name"] = block, ["file"] = file.Replace('\\', '/'), ["at"] = A(c) }, u.Go.Tag);

                // nozzles: from the icon's connection points to the outline of the symbol
                C("layer.current", new JObject { ["name"] = "PROCESS" });
                foreach (var cp in Connectors(u.Go).Where(p => p.IsAttached && Drawn(p, u.Go, lines)))
                {
                    var p = T(cp.Position.X, cp.Position.Y);
                    var q = Stub(cp, p, c, half, w, h);
                    if (Dist(p, q) > 0.3) C("pline", new JObject { ["vertices"] = new JArray(A(p), A(q)) }, u.Go.Tag + " nozzle");
                }

                // tag, service and the key figure under the item
                C("layer.current", new JObject { ["name"] = "TEXT" });
                var y = c.Y - h / 2 - 2;
                C("text", new JObject { ["at"] = new JArray(c.X, Math.Round(y, 2)), ["text"] = u.Go.Tag, ["height"] = 3.0, ["justify"] = "TC" }, u.Go.Tag + " tag");
                var service = Service(u.Obj);
                if (string.Equals(service, u.Go.Tag, StringComparison.OrdinalIgnoreCase)) service = "";
                if (service.Length > 0)
                    C("text", new JObject { ["at"] = new JArray(c.X, Math.Round(y - 4.5, 2)), ["text"] = service, ["height"] = TextHeight, ["justify"] = "TC" }, u.Go.Tag + " service");
                var data = KeyData(fs, u.Obj, u.Go.ObjectType);
                if (data.Length > 0)
                    C("text", new JObject { ["at"] = new JArray(c.X, Math.Round(y - (service.Length > 0 ? 8.5 : 4.5), 2)), ["text"] = data, ["height"] = TextHeight, ["justify"] = "TC" }, u.Go.Tag + " data");
            }

            // mixers and splitters: a header, the branches meeting on it
            foreach (var u in units.Where(u => u.Symbol == Symbol.Node))
            {
                var c = T(u.Go.X + u.Go.Width / 2.0, u.Go.Y + u.Go.Height / 2.0);
                C("layer.current", new JObject { ["name"] = "PROCESS" });
                foreach (var cp in Connectors(u.Go).Where(p => p.IsAttached && Drawn(p, u.Go, lines)))
                {
                    var p = T(cp.Position.X, cp.Position.Y);
                    C("pline", new JObject { ["vertices"] = new JArray(A(p), new JArray(c.X, p.Y), A(c)) }, u.Go.Tag);
                }
                C("donut", new JObject { ["center"] = A(c), ["inside"] = 0, ["outside"] = 1.6 }, u.Go.Tag);
            }

            // recycles, pipes and orifices: the line runs through
            foreach (var u in units.Where(u => u.Symbol == Symbol.PassThrough))
            {
                if (u.Go.InputConnectors.Count == 0 || u.Go.OutputConnectors.Count == 0) continue;
                var a = T(u.Go.InputConnectors[0].Position.X, u.Go.InputConnectors[0].Position.Y);
                var b = T(u.Go.OutputConnectors[0].Position.X, u.Go.OutputConnectors[0].Position.Y);
                C("layer.current", new JObject { ["name"] = "PROCESS" });
                C("pline", new JObject { ["vertices"] = Math.Abs(a.Y - b.Y) < 0.1 ? new JArray(A(a), A(b)) : new JArray(A(a), new JArray((a.X + b.X) / 2, a.Y), new JArray((a.X + b.X) / 2, b.Y), A(b)) }, u.Go.Tag);
                if (u.Go.ObjectType == ObjectType.OrificePlate)
                {
                    var m = new Pt((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                    C("line", new JObject { ["points"] = new JArray(new JArray(m.X - 0.8, m.Y - 3), new JArray(m.X - 0.8, m.Y + 3)) }, u.Go.Tag);
                    C("line", new JObject { ["points"] = new JArray(new JArray(m.X + 0.8, m.Y - 3), new JArray(m.X + 0.8, m.Y + 3)) }, u.Go.Tag);
                }
                if (u.Go.ObjectType == ObjectType.Pipe || u.Go.ObjectType == ObjectType.OrificePlate)
                {
                    C("layer.current", new JObject { ["name"] = "TEXT" });
                    C("text", new JObject { ["at"] = new JArray((a.X + b.X) / 2, Math.Max(a.Y, b.Y) + 4.5), ["text"] = u.Go.Tag, ["height"] = TextHeight, ["justify"] = "BC" }, u.Go.Tag);
                }
            }

            // the streams, with their number, arrow and battery limit names
            foreach (var l in lines)
            {
                var p = l.Points.Select(q => T(q.X, q.Y)).ToList();
                p = Simplify(p);
                if (p.Count < 2) continue;
                C("layer.current", new JObject { ["name"] = l.Energy ? "UTILITY" : "PROCESS" });
                C("pline", new JObject { ["vertices"] = new JArray(p.Select(A)) }, "stream " + l.Tag);
                if (l.ArrowAtEnd) Arrow(cmds, p[p.Count - 2], p[p.Count - 1], l.Tag);

                C("layer.current", new JObject { ["name"] = "TEXT" });
                if (l.Feed)
                    C("text", new JObject { ["at"] = new JArray(p[0].X - 2, p[0].Y), ["text"] = l.Tag, ["height"] = TextHeight, ["justify"] = "MR" }, "stream " + l.Tag);
                if (l.Product)
                {
                    var e = p[p.Count - 1];
                    var dir = Math.Sign(e.X - p[p.Count - 2].X);
                    if (dir >= 0) C("text", new JObject { ["at"] = new JArray(e.X + 2, e.Y), ["text"] = l.Tag, ["height"] = TextHeight, ["justify"] = "ML" }, "stream " + l.Tag);
                    else C("text", new JObject { ["at"] = new JArray(e.X - 2, e.Y), ["text"] = l.Tag, ["height"] = TextHeight, ["justify"] = "MR" }, "stream " + l.Tag);
                }

                // the number in its diamond, or the energy stream's name and duty, by the icon
                var ic = T(l.Icon.X + l.Icon.Width / 2.0, l.Icon.Y + l.Icon.Height / 2.0);
                var horizontal = Horizontal(p, ic);
                var at = horizontal ? new Pt(ic.X, ic.Y + 5) : new Pt(ic.X - 5, ic.Y);
                if (l.Energy)
                {
                    var q = Duty(fs, l.Icon.Owner as IEnergyStream);
                    C("text", new JObject { ["at"] = new JArray(at.X, at.Y), ["text"] = l.Tag + (q.Length > 0 ? "  " + q : ""), ["height"] = TextHeight, ["justify"] = horizontal ? "BC" : "MR" }, "stream " + l.Tag);
                }
                else
                {
                    // a diamond wide enough for the number
                    var dw = Math.Max(3.5, l.Id.Length * 1.1 + 1.5);
                    C("layer.current", new JObject { ["name"] = "STREAMNUMBER" });
                    C("pline", new JObject { ["vertices"] = new JArray(new JArray(at.X - dw, at.Y), new JArray(at.X, at.Y + 3.5), new JArray(at.X + dw, at.Y), new JArray(at.X, at.Y - 3.5)), ["closed"] = true }, "stream " + l.Tag);
                    C("text", new JObject { ["at"] = new JArray(at.X, at.Y), ["text"] = l.Id, ["height"] = l.Id.Length > 2 ? 2.0 : TextHeight, ["justify"] = "MC" }, "stream " + l.Tag);
                }
            }

            // the sheet: border and zones, title block, stream table and the viewport on the diagram
            C("layout.new", new JObject { ["name"] = "PFD", ["viewport"] = false });
            C("pagesetup", new JObject
            {
                ["layout"] = "PFD", ["device"] = "CADCraft PDF", ["width"] = sheet.Width, ["height"] = sheet.Height, ["landscape"] = true,
                ["margins"] = new JArray(0, 0, 0, 0), ["plotArea"] = "layout", ["scale"] = 1, ["lineweights"] = true, ["plotStyleTable"] = "monochrome.ctb"
            });
            C("layout.set", new JObject { ["name"] = "PFD" });
            C("pspace", new JObject());
            var border = Path.Combine(temp, "border.dxf");
            DrawingSheets.Border(style, sheet).Save(border);
            C("layer.current", new JObject { ["name"] = "BORDER" });
            C("insert", new JObject { ["name"] = "SHEET_BORDER", ["file"] = border.Replace('\\', '/'), ["at"] = new JArray(0, 0) }, "the border");
            var tb = Path.Combine(temp, "title.dxf");
            DrawingSheets.TitleBlock(style, out _).Save(tb);
            DrawingSheets.Margins(style, out var ml, out var mo);
            C("layer.current", new JObject { ["name"] = "TITLE" });
            C("insert", new JObject
            {
                ["name"] = "TITLE_BLOCK", ["file"] = tb.Replace('\\', '/'), ["at"] = new JArray(sheet.Width - mo, mo),
                ["attribs"] = JObject.FromObject(DrawingSheets.Attributes(title, sheet))
            }, "the title block");

            if (table.Count > 0)
            {
                C("tablestyle", new JObject { ["name"] = "STREAMS", ["textHeight"] = TextHeight, ["margin"] = 0.8, ["current"] = true });
                foreach (var cs in new[] { "Header", "Data" })
                    C("tablestyle.cellstyle", new JObject { ["style"] = "STREAMS", ["name"] = cs, ["textStyle"] = "SANS", ["textHeight"] = TextHeight });
                C("layer.current", new JObject { ["name"] = "TABLE" });
            }

            File.Delete(outputBase + ".dxf");
            using (var session = new CADCraftSession(cadcraftFolder))
            {
                if (session.Tool("new_drawing", new JObject { ["metric"] = true }, "new drawing") == null)
                {
                    result.Error = string.Join("; ", session.Failed);
                    return result;
                }
                foreach (var c in cmds) session.Execute(c.Name, c.Params);

                // the stream table: the streams' columns, then a wider first column for the row names
                var top = tableTop;
                foreach (var g in groups ?? new List<int[]>())
                {
                    if (table.Count == 0) break;
                    var made = session.Execute("table", new JObject
                    {
                        ["at"] = new JArray(ml + 5, Math.Round(top, 2)), ["rows"] = table.Count - 1, ["cols"] = g.Length,
                        ["rowHeight"] = RowHeight, ["colWidth"] = ColumnWidth,
                        ["header"] = new JArray(g.Select(i => table[0][i + 1])),
                        ["cells"] = new JArray(table.Skip(1).Select(r => new JArray(g.Select(i => r[i + 1]))))
                    });
                    var th = made == null ? null : JObject.Parse(made)["handle"]?.ToString();
                    if (th != null && session.Execute("table.insertcol", new JObject { ["handle"] = th, ["col"] = 0, ["width"] = LabelWidth }) != null)
                        for (var r = 0; r < table.Count; r++)
                            session.Execute("table.set", new JObject { ["handle"] = th, ["row"] = r, ["col"] = 0, ["text"] = table[r][0] });
                    top -= table.Count * RowHeight + 4;
                }

                // the viewport over the diagram, at 1:1 on the model's centre
                session.Execute("layer.current", new JObject { ["name"] = "VIEWPORT" });
                session.Execute("layer.set", new JObject { ["name"] = "VIEWPORT", ["plot"] = false });
                var vp = session.Execute("mview", new JObject { ["p1"] = new JArray(Math.Round(areaX0, 2), Math.Round(areaY0, 2)), ["p2"] = new JArray(Math.Round(areaX1, 2), Math.Round(areaY1, 2)) });
                var handle = vp == null ? null : (JObject.Parse(vp)["viewports"] as JArray)?.FirstOrDefault()?.ToString();
                if (handle != null)
                    session.Execute("viewport.set", new JObject
                    {
                        ["handle"] = handle, ["scale"] = 1, ["locked"] = true,
                        ["center"] = new JArray(Math.Round((bw * k + mxR - mxL) / 2, 2), Math.Round((myT - bh * k - myB) / 2, 2))
                    });

                result.Drawing = outputBase + ".dxf";
                result.Dwg = outputBase + ".dwg";
                result.Pdf = outputBase + ".pdf";
                result.Png = outputBase + ".png";
                foreach (var f in new[] { result.Drawing, result.Dwg, result.Pdf, result.Png }) if (File.Exists(f)) File.Delete(f);
                session.Execute("plot", new JObject { ["path"] = result.Pdf, ["layout"] = "PFD" }, 300);
                session.Execute("plot", new JObject { ["path"] = result.Png, ["layout"] = "PFD", ["device"] = "CADCraft PNG" }, 300);
                session.Tool("save", new JObject { ["path"] = result.Dwg }, "save DWG");
                session.Tool("save", new JObject { ["path"] = result.Drawing }, "save DXF");
                result.Failed.AddRange(session.Failed);
            }
            try { Directory.Delete(temp, true); } catch (Exception) { }

            foreach (var f in new[] { "Dwg", "Pdf", "Png" })
            {
                var fi = typeof(Result).GetField(f);
                if (!File.Exists((string)fi.GetValue(result))) fi.SetValue(result, null);
            }
            if (!File.Exists(result.Drawing))
            {
                result.Error = "CADCraft did not save the drawing" + (result.Failed.Count > 0 ? " (" + result.Failed.Last() + ")" : "");
                result.Drawing = null;
            }
            return result;
        }

        #region what is drawn

        static List<Unit> Units(IFlowsheet fs)
        {
            var list = new List<Unit>();
            foreach (var obj in fs.SimulationObjects.Values)
            {
                var go = obj.GraphicObject;
                if (go == null) continue;
                var s = SymbolFor(obj, go);
                if (s != Symbol.None) list.Add(new Unit { Obj = obj, Go = go, Symbol = s });
            }
            return list;
        }

        static Symbol SymbolFor(ISimulationObject obj, IGraphicObject go)
        {
            if (obj.GetType().Name == "ReliefValve") return Symbol.Valve;
            switch (go.ObjectType)
            {
                case ObjectType.Vessel:
                case ObjectType.TPVessel:
                    return go.Width > go.Height * 1.3 ? Symbol.VesselH : Symbol.VesselV;
                case ObjectType.Tank: return Symbol.Tank;
                case ObjectType.DistillationColumn:
                case ObjectType.AbsorptionColumn:
                case ObjectType.RefluxedAbsorber:
                case ObjectType.ReboiledAbsorber:
                case ObjectType.ShortcutColumn:
                    return Symbol.Column;
                case ObjectType.Pump: return Symbol.Pump;
                case ObjectType.Compressor: return Symbol.Compressor;
                case ObjectType.Expander: return Symbol.Expander;
                case ObjectType.CompressorExpander: return Symbol.Compressor;
                case ObjectType.Heater: return Symbol.Heater;
                case ObjectType.Cooler: return Symbol.Cooler;
                case ObjectType.HeaterCooler: return Symbol.Heater;
                case ObjectType.HeatExchanger: return Symbol.Exchanger;
                case ObjectType.AirCooler2: return Symbol.AirCooler;
                case ObjectType.Valve: return Symbol.Valve;
                case ObjectType.RCT_Conversion:
                case ObjectType.RCT_Equilibrium:
                case ObjectType.RCT_Gibbs:
                case ObjectType.RCT_GibbsReaktoro:
                    return Symbol.Reactor;
                case ObjectType.RCT_CSTR:
                case ObjectType.RCT_Polymerization:
                    return Symbol.Cstr;
                case ObjectType.RCT_PFR: return Symbol.Pfr;
                case ObjectType.Filter: return Symbol.Filter;
                case ObjectType.ComponentSeparator:
                case ObjectType.SolidSeparator:
                    return Symbol.Separator;
                case ObjectType.NodeIn:
                case ObjectType.NodeOut:
                case ObjectType.Mixer:
                case ObjectType.Splitter:
                    return Symbol.Node;
                case ObjectType.OT_Recycle:
                case ObjectType.OT_EnergyRecycle:
                case ObjectType.Pipe:
                case ObjectType.OrificePlate:
                    return Symbol.PassThrough;
                case ObjectType.CustomUO:
                case ObjectType.ExcelUO:
                case ObjectType.CapeOpenUO:
                case ObjectType.FlowsheetUO:
                case ObjectType.External:
                case ObjectType.SolidOps:
                case ObjectType.WindTurbine:
                case ObjectType.HydroelectricTurbine:
                case ObjectType.SolarPanel:
                case ObjectType.PEMFuelCell:
                case ObjectType.WaterElectrolyzer:
                    return Symbol.Generic;
                default:
                    return Symbol.None;
            }
        }

        static IEnumerable<IConnectionPoint> Connectors(IGraphicObject go)
        {
            foreach (var c in go.InputConnectors) yield return c;
            foreach (var c in go.OutputConnectors) yield return c;
            if (go.EnergyConnector != null && go.EnergyConnector.Active) yield return go.EnergyConnector;
        }

        /// <summary>Whether the stream on this connection point is drawn (energy streams that only carry a duty are not).</summary>
        static bool Drawn(IConnectionPoint cp, IGraphicObject go, List<Line> lines)
        {
            var con = cp.AttachedConnector;
            if (con == null) return false;
            var other = con.AttachedFrom == go ? con.AttachedTo : con.AttachedFrom;
            return other == null || other.ObjectType != ObjectType.EnergyStream || lines.Any(l => l.Icon == other);
        }

        /// <summary>Whether the last <see cref="Lines"/> kept the streams' own tags as their numbers.</summary>
        [ThreadStatic] static bool OwnTags;

        static List<Line> Lines(IFlowsheet fs)
        {
            var lines = new List<Line>();
            foreach (var obj in fs.SimulationObjects.Values)
            {
                var go = obj.GraphicObject;
                if (go == null || (go.ObjectType != ObjectType.MaterialStream && go.ObjectType != ObjectType.EnergyStream)) continue;
                var l = new Line { Tag = go.Tag, Energy = go.ObjectType == ObjectType.EnergyStream, Icon = go, Stream = obj as IMaterialStream };
                var cin = go.InputConnectors.Count > 0 && go.InputConnectors[0].IsAttached ? go.InputConnectors[0].AttachedConnector : null;
                var cout = go.OutputConnectors.Count > 0 && go.OutputConnectors[0].IsAttached ? go.OutputConnectors[0].AttachedConnector : null;
                if (cin == null && cout == null) continue;
                if (l.Energy && (cin == null || cout == null)) continue;
                if (cin != null)
                {
                    l.Points.AddRange(Route(cin));
                    l.From = cin.AttachedFrom?.Tag ?? "";
                }
                else
                {
                    l.Feed = true;
                    l.Points.Add(new Pt(go.X, go.Y + go.Height / 2.0));
                }
                if (cout != null)
                {
                    l.Points.AddRange(Route(cout));
                    l.To = cout.AttachedTo?.Tag ?? "";
                    // into a recycle the line goes on: no arrow there
                    var to = cout.AttachedTo?.ObjectType;
                    if (to == ObjectType.OT_Recycle || to == ObjectType.OT_EnergyRecycle) l.ArrowAtEnd = false;
                }
                else
                {
                    l.Product = true;
                    l.Points.Add(new Pt(go.X + go.Width, go.Y + go.Height / 2.0));
                }
                lines.Add(l);
            }

            // numbered from left to right, then top to bottom, as the diagram reads; a flowsheet
            // whose streams already have short unique tags keeps them
            var material = lines.Where(l => !l.Energy).ToList();
            var own = material.All(l => !string.IsNullOrWhiteSpace(l.Tag) && l.Tag.Length <= 5) &&
                material.Select(l => l.Tag).Distinct(StringComparer.OrdinalIgnoreCase).Count() == material.Count;
            var ordered = own
                ? material.OrderBy(l => int.TryParse(l.Tag, out var v) ? v : int.MaxValue).ThenBy(l => l.Tag, StringComparer.OrdinalIgnoreCase)
                : material.OrderBy(l => Math.Round(l.Icon.X / 40.0)).ThenBy(l => l.Icon.Y);
            var k = 0;
            foreach (var l in ordered)
            {
                l.Number = ++k;
                l.Id = own ? l.Tag : k.ToString(CultureInfo.InvariantCulture);
            }
            OwnTags = own;
            return lines;
        }

        /// <summary>The points of a connector as the flowsheet routes it.</summary>
        static IEnumerable<Pt> Route(IConnectorGraphicObject con)
        {
            var list = new List<Pt>();
            try
            {
                var t = con.GetType();
                t.GetMethod("SetupPositioning")?.Invoke(con, null);
                if (t.GetProperty("PointList")?.GetValue(con) is IEnumerable points)
                    foreach (var o in points)
                        if (o is IPoint p) list.Add(new Pt(p.X, p.Y));
            }
            catch (Exception) { list.Clear(); }
            if (list.Count >= 2) return list;

            // straight across, with one vertical leg half way
            var a = con.AttachedFrom.OutputConnectors.Count > con.AttachedFromConnectorIndex && con.AttachedFromConnectorIndex >= 0
                ? con.AttachedFrom.OutputConnectors[con.AttachedFromConnectorIndex].Position : null;
            var b = con.AttachedTo.InputConnectors.Count > con.AttachedToConnectorIndex && con.AttachedToConnectorIndex >= 0
                ? con.AttachedTo.InputConnectors[con.AttachedToConnectorIndex].Position : null;
            if (a == null || b == null) return list;
            var xm = (a.X + b.X) / 2;
            return new[] { new Pt(a.X, a.Y), new Pt(xm, a.Y), new Pt(xm, b.Y), new Pt(b.X, b.Y) };
        }

        static List<Pt> Simplify(List<Pt> p)
        {
            var o = new List<Pt>();
            foreach (var q in p)
            {
                if (o.Count > 0 && Dist(o[o.Count - 1], q) < 0.05) continue;
                // drop the middle of three points in a line
                if (o.Count > 1)
                {
                    var a = o[o.Count - 2]; var b = o[o.Count - 1];
                    var cross = (b.X - a.X) * (q.Y - b.Y) - (b.Y - a.Y) * (q.X - b.X);
                    if (Math.Abs(cross) < 0.01 && (b.X - a.X) * (q.X - b.X) + (b.Y - a.Y) * (q.Y - b.Y) >= 0) o.RemoveAt(o.Count - 1);
                }
                o.Add(q);
            }
            return o;
        }

        static bool Horizontal(List<Pt> p, Pt near)
        {
            // the direction of the segment closest to the icon
            var best = double.MaxValue; var h = true;
            for (var i = 0; i + 1 < p.Count; i++)
            {
                var m = new Pt((p[i].X + p[i + 1].X) / 2, (p[i].Y + p[i + 1].Y) / 2);
                var d = Dist(m, near);
                if (d < best) { best = d; h = Math.Abs(p[i + 1].X - p[i].X) >= Math.Abs(p[i + 1].Y - p[i].Y); }
            }
            return h;
        }

        static double Dist(Pt a, Pt b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        static JArray A(Pt p) => new JArray(Math.Round(p.X, 2), Math.Round(p.Y, 2));

        /// <summary>
        /// From a connection point on the icon's edge, straight in to the symbol's outline: along the
        /// flow for an inlet, against it for an outlet, and from the nearest edge for an energy
        /// connection.
        /// </summary>
        static Pt Stub(IConnectionPoint cp, Pt p, Pt c, Pt half, double w, double h)
        {
            if (cp.Type == ConType.ConIn || cp.Type == ConType.ConOut)
            {
                // canvas up is model +y
                double vx = cp.Direction == ConDir.Right ? 1 : cp.Direction == ConDir.Left ? -1 : 0;
                double vy = cp.Direction == ConDir.Up ? 1 : cp.Direction == ConDir.Down ? -1 : 0;
                if (cp.Type == ConType.ConOut) { vx = -vx; vy = -vy; }
                if (vx > 0) return new Pt(Math.Max(p.X, c.X - half.X), p.Y);
                if (vx < 0) return new Pt(Math.Min(p.X, c.X + half.X), p.Y);
                if (vy > 0) return new Pt(p.X, Math.Max(p.Y, c.Y - half.Y));
                if (vy < 0) return new Pt(p.X, Math.Min(p.Y, c.Y + half.Y));
            }
            double dl = Math.Abs(p.X - (c.X - w / 2)), dr = Math.Abs(p.X - (c.X + w / 2)), dt = Math.Abs(p.Y - (c.Y + h / 2)), db = Math.Abs(p.Y - (c.Y - h / 2));
            var m = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
            if (m == dl) return new Pt(Math.Max(p.X, c.X - half.X), p.Y);
            if (m == dr) return new Pt(Math.Min(p.X, c.X + half.X), p.Y);
            if (m == dt) return new Pt(p.X, Math.Min(p.Y, c.Y + half.Y));
            return new Pt(p.X, Math.Max(p.Y, c.Y - half.Y));
        }

        static void Arrow(List<SolidCraftScript.Command> cmds, Pt from, Pt tip, string tag)
        {
            var dx = tip.X - from.X; var dy = tip.Y - from.Y; var l = Math.Sqrt(dx * dx + dy * dy);
            if (l < 1e-6) return;
            dx /= l; dy /= l;
            const double len = 3.5, half = 1.2;
            var bx = tip.X - dx * len; var by = tip.Y - dy * len;
            cmds.Add(new SolidCraftScript.Command
            {
                Name = "solid",
                Subject = "stream " + tag,
                Params = new JObject { ["points"] = new JArray(A(tip), A(new Pt(bx - dy * half, by + dx * half)), A(new Pt(bx + dy * half, by - dx * half))) }
            });
        }

        static string BlockName(string tag, HashSet<string> used)
        {
            var b = "EQ_" + Regex.Replace(tag ?? "", "[^A-Za-z0-9_-]", "_");
            var name = b; var i = 1;
            while (!used.Add(name.ToUpperInvariant())) name = b + "_" + (++i);
            return name;
        }

        static List<int[]> Groups(int count, int perGroup)
        {
            var g = new List<int[]>();
            for (var i = 0; i < count; i += perGroup) g.Add(Enumerable.Range(i, Math.Min(perGroup, count - i)).ToArray());
            return g;
        }

        #endregion

        #region symbols

        /// <summary>
        /// Draws the symbol of an item centred on the origin in a w x h box (ISO 10628 and the
        /// usual PFD shapes) and returns the half sizes of its outline, where the nozzles end.
        /// </summary>
        static Pt Draw(Dxf d, Symbol s, double w, double h, bool flip)
        {
            double f = flip ? -1 : 1;
            switch (s)
            {
                case Symbol.VesselV:
                case Symbol.Column:
                case Symbol.Reactor:
                case Symbol.Cstr:
                    {
                        var vw = s == Symbol.Column ? Math.Min(w * 0.55, h / 4) : Math.Min(w, h * 0.6);
                        var r = vw / 2; var hd = Math.Min(r / 2, h / 4);
                        d.Line(-r, -h / 2 + hd, -r, h / 2 - hd);
                        d.Line(r, -h / 2 + hd, r, h / 2 - hd);
                        d.Ellipse(0, h / 2 - hd, r, hd, 0, Math.PI);
                        d.Ellipse(0, -h / 2 + hd, r, hd, Math.PI, 2 * Math.PI);
                        if (s == Symbol.Column)
                        {
                            // trays, alternating from each side
                            var n = Math.Max(4, Math.Min(12, (int)((h - 2 * hd) / 6)));
                            var step = (h - 2 * hd) / (n + 1);
                            for (var i = 1; i <= n; i++)
                            {
                                var y = -h / 2 + hd + i * step;
                                if (i % 2 == 0) d.Line(-r, y, r * 0.5, y); else d.Line(-r * 0.5, y, r, y);
                            }
                        }
                        else if (s == Symbol.Reactor)
                        {
                            // a catalyst bed between two supports
                            double y0 = -h * 0.2, y1 = h * 0.2;
                            d.Line(-r, y0, r, y0); d.Line(-r, y1, r, y1);
                            for (var x = -r; x < r - 1e-6; x += r / 2) d.Line(x, y0, Math.Min(x + r / 2, r), y1);
                        }
                        else if (s == Symbol.Cstr)
                        {
                            // agitator: motor, shaft and impeller
                            d.Rect(-r * 0.25, h / 2, r * 0.25, h / 2 + r * 0.4);
                            d.Line(0, h / 2, 0, -h * 0.15);
                            d.Line(-r * 0.5, -h * 0.15, r * 0.5, -h * 0.15);
                            d.Line(-r * 0.5, -h * 0.15 - r * 0.15, -r * 0.5, -h * 0.15 + r * 0.15);
                            d.Line(r * 0.5, -h * 0.15 - r * 0.15, r * 0.5, -h * 0.15 + r * 0.15);
                        }
                        return new Pt(r, h / 2);
                    }
                case Symbol.VesselH:
                case Symbol.Pfr:
                    {
                        var vh = Math.Min(h, w * 0.6);
                        var r = vh / 2; var hd = Math.Min(r / 2, w / 4);
                        d.Line(-w / 2 + hd, r, w / 2 - hd, r);
                        d.Line(-w / 2 + hd, -r, w / 2 - hd, -r);
                        d.Ellipse(w / 2 - hd, 0, hd, r, -Math.PI / 2, Math.PI / 2);
                        d.Ellipse(-w / 2 + hd, 0, hd, r, Math.PI / 2, 3 * Math.PI / 2);
                        if (s == Symbol.Pfr)
                        {
                            // packed tubes
                            for (var x = -w / 2 + hd; x < w / 2 - hd - 1e-6; x += r)
                                d.Line(x, -r, Math.Min(x + r, w / 2 - hd), r);
                        }
                        return new Pt(w / 2, r);
                    }
                case Symbol.Tank:
                    {
                        // vertical tank with a cone roof
                        var roof = h * 0.12;
                        d.Poly(false, -w / 2, h / 2 - roof, -w / 2, -h / 2, w / 2, -h / 2, w / 2, h / 2 - roof);
                        d.Poly(false, -w / 2, h / 2 - roof, 0, h / 2, w / 2, h / 2 - roof);
                        return new Pt(w / 2, h / 2);
                    }
                case Symbol.Pump:
                    {
                        // centrifugal pump: casing, tangential discharge, base
                        var r = Math.Min(w, h) * 0.38;
                        d.Circle(0, 0, r);
                        d.Line(0, r, f * w / 2, r);
                        d.Poly(false, -r * 0.7, -r * 0.7, -r, -h / 2, r, -h / 2, r * 0.7, -r * 0.7);
                        return new Pt(r, r);
                    }
                case Symbol.Compressor:
                case Symbol.Expander:
                    {
                        // a trapezoid narrowing in the direction of flow (compressor) or widening (turbine)
                        var big = h / 2; var small = h * 0.22;
                        var compress = (s == Symbol.Compressor) == !flip;
                        var yl = compress ? big : small; var yr = compress ? small : big;
                        d.Poly(true, -w / 2, -yl, -w / 2, yl, w / 2, yr, w / 2, -yr);
                        return new Pt(w / 2, big);
                    }
                case Symbol.Heater:
                case Symbol.Cooler:
                case Symbol.Exchanger:
                    {
                        var r = Math.Min(w, h) * 0.45;
                        d.Circle(0, 0, r);
                        if (s == Symbol.Exchanger)
                        {
                            // tube side across, shell side round it
                            d.Poly(false, -r, 0, -r * 0.45, 0, -r * 0.2, r * 0.45, r * 0.2, -r * 0.45, r * 0.45, 0, r, 0);
                        }
                        else
                        {
                            // the utility coil, its arrow up for heating and down for cooling
                            d.Poly(false, 0, -r * 1.35, 0, -r * 0.55, -r * 0.45, -r * 0.2, r * 0.45, r * 0.2, 0, r * 0.55, 0, r * 1.35);
                            if (s == Symbol.Heater) d.Solid(0, r * 1.35, -r * 0.18, r * 0.95, r * 0.18, r * 0.95);
                            else d.Solid(0, -r * 1.35, -r * 0.18, -r * 0.95, r * 0.18, -r * 0.95);
                        }
                        return new Pt(r, r);
                    }
                case Symbol.AirCooler:
                    {
                        // tube bundle with the fan over it
                        var bh = h * 0.35;
                        d.Rect(-w / 2, -h / 2, w / 2, -h / 2 + bh);
                        d.Poly(false, -w / 2, -h / 2 + bh / 2, -w / 4, -h / 2 + bh * 0.85, 0, -h / 2 + bh * 0.15, w / 4, -h / 2 + bh * 0.85, w / 2, -h / 2 + bh / 2);
                        d.Line(0, -h / 2 + bh, 0, h * 0.25);
                        d.Ellipse(0, h * 0.25, w * 0.35, h * 0.07, 0, 2 * Math.PI);
                        return new Pt(w / 2, h / 2);
                    }
                case Symbol.Valve:
                    {
                        // two triangles tip to tip
                        var vh = Math.Min(h, w * 0.6) / 2;
                        d.Poly(true, -w / 2, -vh, -w / 2, vh, w / 2, -vh, w / 2, vh);
                        return new Pt(w / 2, vh);
                    }
                case Symbol.Filter:
                    {
                        d.Rect(-w / 2, -h / 2, w / 2, h / 2);
                        d.Line(-w / 2, -h / 2, w / 2, h / 2);
                        return new Pt(w / 2, h / 2);
                    }
                case Symbol.Separator:
                    {
                        // a box split by the separation
                        d.Rect(-w / 2, -h / 2, w / 2, h / 2);
                        d.Line(-w / 2, -h / 2, 0, 0); d.Line(-w / 2, h / 2, 0, 0);
                        return new Pt(w / 2, h / 2);
                    }
                default:
                    d.Rect(-w / 2, -h / 2, w / 2, h / 2);
                    return new Pt(w / 2, h / 2);
            }
        }

        #endregion

        #region texts and the stream table

        static string Service(ISimulationObject obj)
        {
            try { return (obj.GetDisplayName() ?? "").ToUpperInvariant(); } catch (Exception) { return ""; }
        }

        static double? Number(object o, string property)
        {
            var v = o.GetType().GetProperty(property)?.GetValue(o);
            if (v is double d && !double.IsNaN(d) && !double.IsInfinity(d)) return d;
            if (v is int i) return i;
            return null;
        }

        /// <summary>The duty or power of an item, or its stages, in the units of the flowsheet.</summary>
        static string KeyData(IFlowsheet fs, ISimulationObject obj, ObjectType type)
        {
            var heat = fs.FlowsheetOptions.SelectedUnitSystem.heatflow;
            string Heat(double kw) => Fmt(Converter.ConvertFromSI(heat, kw)) + " " + heat;
            switch (type)
            {
                case ObjectType.Heater:
                case ObjectType.Cooler:
                case ObjectType.HeaterCooler:
                case ObjectType.RCT_CSTR:
                case ObjectType.RCT_PFR:
                case ObjectType.RCT_Conversion:
                case ObjectType.RCT_Equilibrium:
                case ObjectType.RCT_Gibbs:
                    {
                        var q = Number(obj, "DeltaQ");
                        return q.HasValue && Math.Abs(q.Value) > 0 ? "Q = " + Heat(Math.Abs(q.Value)) : "";
                    }
                case ObjectType.HeatExchanger:
                    {
                        var q = Number(obj, "Q");
                        return q.HasValue && Math.Abs(q.Value) > 0 ? "Q = " + Heat(Math.Abs(q.Value)) : "";
                    }
                case ObjectType.Pump:
                case ObjectType.Compressor:
                case ObjectType.Expander:
                case ObjectType.CompressorExpander:
                    {
                        var q = Number(obj, "DeltaQ");
                        return q.HasValue && Math.Abs(q.Value) > 0 ? "W = " + Heat(Math.Abs(q.Value)) : "";
                    }
                case ObjectType.DistillationColumn:
                case ObjectType.AbsorptionColumn:
                case ObjectType.RefluxedAbsorber:
                case ObjectType.ReboiledAbsorber:
                    {
                        var stages = Number(obj, "NumberOfStages");
                        var parts = new List<string>();
                        if (stages.HasValue) parts.Add(stages.Value.ToString("0", CultureInfo.InvariantCulture) + " STAGES");
                        var qc = Number(obj, "CondenserDuty");
                        var qr = Number(obj, "ReboilerDuty");
                        if (qc.HasValue && Math.Abs(qc.Value) > 0) parts.Add("QC = " + Heat(Math.Abs(qc.Value)));
                        if (qr.HasValue && Math.Abs(qr.Value) > 0) parts.Add("QR = " + Heat(Math.Abs(qr.Value)));
                        return string.Join(", ", parts);
                    }
                default:
                    return "";
            }
        }

        static string Duty(IFlowsheet fs, IEnergyStream es)
        {
            if (es == null) return "";
            var heat = fs.FlowsheetOptions.SelectedUnitSystem.heatflow;
            var q = es.GetEnergyFlow();
            return double.IsNaN(q) ? "" : Fmt(Converter.ConvertFromSI(heat, q)) + " " + heat;
        }

        /// <summary>
        /// The stream table: a header row with the stream numbers, then the name, the ends,
        /// conditions and flows, and the flow of each compound present, in the flowsheet's units.
        /// </summary>
        static List<string[]> StreamTable(IFlowsheet fs, List<Line> streams)
        {
            var rows = new List<string[]>();
            streams = streams.Where(s => s.Stream != null).OrderBy(s => s.Number).ToList();
            if (streams.Count == 0) return rows;
            var u = fs.FlowsheetOptions.SelectedUnitSystem;
            string[] Row(string label, Func<Line, string> value) => new[] { label }.Concat(streams.Select(value)).ToArray();
            double Si(Func<IMaterialStream, double> f, IMaterialStream ms) { try { return f(ms); } catch (Exception) { return double.NaN; } }

            rows.Add(Row("STREAM", s => s.Id));
            if (!OwnTags) rows.Add(Row("Name", s => Short(s.Tag)));
            rows.Add(Row("From", s => Short(s.From)));
            rows.Add(Row("To", s => Short(s.To)));
            rows.Add(Row("Temperature (" + u.temperature + ")", s => Fmt(Converter.ConvertFromSI(u.temperature, Si(m => m.GetTemperature(), s.Stream)))));
            rows.Add(Row("Pressure (" + u.pressure + ")", s => Fmt(Converter.ConvertFromSI(u.pressure, Si(m => m.GetPressure(), s.Stream)))));
            rows.Add(Row("Vapor fraction", s => Fmt(Si(m => m.Phases[2].Properties.molarfraction.GetValueOrDefault(), s.Stream))));
            rows.Add(Row("Mass flow (" + u.massflow + ")", s => Fmt(Converter.ConvertFromSI(u.massflow, Si(m => m.GetMassFlow(), s.Stream)))));
            rows.Add(Row("Molar flow (" + u.molarflow + ")", s => Fmt(Converter.ConvertFromSI(u.molarflow, Si(m => m.GetMolarFlow(), s.Stream)))));

            // the compounds that flow somewhere, by mass: the main ones (by their largest share of
            // a stream), and the rest summed up
            double Flow(string c, IMaterialStream ms) => Si(m => m.Phases[0].Compounds[c].MassFlow.GetValueOrDefault(), ms);
            double Share(string c) => streams.Max(s => { var t = Si(m => m.GetMassFlow(), s.Stream); return t > 0 ? Flow(c, s.Stream) / t : 0; });
            var present = fs.SelectedCompounds.Keys.Where(c => streams.Any(s => Flow(c, s.Stream) > 1e-12)).ToList();
            var main = present.OrderByDescending(Share).Take(MaxCompounds).ToList();
            var names = present.Where(main.Contains).ToList();
            foreach (var c in names)
                rows.Add(Row(Short(c) + " (" + u.massflow + ")", s => Fmt(Converter.ConvertFromSI(u.massflow, Flow(c, s.Stream)))));
            var rest = present.Except(main).ToList();
            if (rest.Count > 0)
                rows.Add(Row(rest.Count + " others (" + u.massflow + ")", s => Fmt(Converter.ConvertFromSI(u.massflow, rest.Sum(c => Flow(c, s.Stream))))));
            return rows;
        }

        static string Short(string s)
        {
            s = s ?? "";
            return s.Length <= 13 ? s : s.Substring(0, 12) + ".";
        }

        static string Fmt(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            var a = Math.Abs(v);
            if (a == 0) return "0";
            if (a >= 1e6 || a < 1e-3) return v.ToString("0.00E+0", CultureInfo.InvariantCulture);
            if (a >= 1000) return v.ToString("0", CultureInfo.InvariantCulture);
            if (a >= 100) return v.ToString("0.0", CultureInfo.InvariantCulture);
            if (a >= 1) return v.ToString("0.00", CultureInfo.InvariantCulture);
            return v.ToString("0.000", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
