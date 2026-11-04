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
    public static partial class ProcessFlowDiagram
    {
        /// <summary>A utility: the codes of its supply and return lines and what it is.</summary>
        class Utility
        {
            public string Supply, Return, Name;
            public bool Heating;
            /// <summary>For a column: "CONDENSER" or "REBOILER"; empty for an item heated or cooled as a whole.</summary>
            public string Role = "";
        }

        /// <summary>The 3D plant model for the P&amp;ID: line sizes and insulation, and the nozzles with their marks; null when it cannot be built.</summary>
        static PlantModel PidModel(IFlowsheet fs)
        {
            try { return PlantModelBuilder.Build(fs, "P&ID", PlantLayout.Load(fs), ExportOptions.Load()); }
            catch (Exception) { return null; }
        }

        static double OutletTemperature(IGraphicObject go, bool highest)
        {
            var ts = new List<double>();
            foreach (var cp in go.OutputConnectors)
            {
                if (!cp.IsAttached || cp.AttachedConnector?.AttachedTo == null) continue;
                if (go.Flowsheet?.SimulationObjects.TryGetValue(cp.AttachedConnector.AttachedTo.Name, out var o) == true && o is IMaterialStream ms)
                {
                    var t = ms.GetTemperature() - 273.15;
                    if (!double.IsNaN(t) && !double.IsInfinity(t)) ts.Add(t);
                }
            }
            return ts.Count == 0 ? double.NaN : highest ? ts.Max() : ts.Min();
        }

        /// <summary>
        /// The utility for heating a process fluid to <paramref name="t"/> C (steam at three levels,
        /// hot oil, fuel gas for a fired heater), or for cooling it to <paramref name="t"/> C (cooling
        /// water, chilled water, propane, ethylene or mixed refrigerant), with the usual approach.
        /// </summary>
        static Utility UtilityFor(bool heating, double t)
        {
            if (double.IsNaN(t)) t = heating ? 100 : 40;
            if (heating)
            {
                if (t < 125) return new Utility { Supply = "LPS", Return = "LPC", Name = "LP steam 3.5 bar g / condensate", Heating = true };
                if (t < 170) return new Utility { Supply = "MPS", Return = "MPC", Name = "MP steam 10 bar g / condensate", Heating = true };
                if (t < 230) return new Utility { Supply = "HPS", Return = "HPC", Name = "HP steam 40 bar g / condensate", Heating = true };
                if (t < 300) return new Utility { Supply = "HOS", Return = "HOR", Name = "Hot oil supply / return", Heating = true };
                return new Utility { Supply = "FG", Return = "", Name = "Fuel gas (fired heater)", Heating = true };
            }
            if (t >= 35) return new Utility { Supply = "CWS", Return = "CWR", Name = "Cooling water supply / return, 30/40 C" };
            if (t >= 12) return new Utility { Supply = "CHS", Return = "CHR", Name = "Chilled water supply / return, 7/12 C" };
            if (t >= -35) return new Utility { Supply = "PRL", Return = "PRV", Name = "Propane refrigerant liquid / vapor, -40 C" };
            if (t >= -95) return new Utility { Supply = "ERL", Return = "ERV", Name = "Ethylene refrigerant liquid / vapor, -100 C" };
            if (t >= -155) return new Utility { Supply = "MRL", Return = "MRV", Name = "Mixed refrigerant liquid / vapor, -160 C" };
            return new Utility { Supply = "CRS", Return = "CRR", Name = "Cryogenic refrigerant (helium or hydrogen cycle) supply / return" };
        }

        /// <summary>The utilities an item uses: a heater or a cooler, a reactor with a duty, a column's condenser and reboiler.</summary>
        static List<Utility> Utilities(Unit u)
        {
            var list = new List<Utility>();
            var go = u.Go;
            switch (go.ObjectType)
            {
                case ObjectType.Heater:
                    list.Add(UtilityFor(true, OutletTemperature(go, true)));
                    break;
                case ObjectType.Cooler:
                    list.Add(UtilityFor(false, OutletTemperature(go, false)));
                    break;
                case ObjectType.DistillationColumn:
                case ObjectType.RefluxedAbsorber:
                case ObjectType.ReboiledAbsorber:
                    {
                        var qc = Number(u.Obj, "CondenserDuty");
                        var qr = Number(u.Obj, "ReboilerDuty");
                        if (qc.HasValue && Math.Abs(qc.Value) > 0) { var c = UtilityFor(false, OutletTemperature(go, false)); c.Role = "CONDENSER"; list.Add(c); }
                        if (qr.HasValue && Math.Abs(qr.Value) > 0) { var r = UtilityFor(true, OutletTemperature(go, true)); r.Role = "REBOILER"; list.Add(r); }
                        break;
                    }
                default:
                    if (go.ObjectType == ObjectType.HeaterCooler || go.ObjectType.ToString().StartsWith("RCT_"))
                    {
                        var q = Number(u.Obj, "DeltaQ");
                        if (q.HasValue && Math.Abs(q.Value) > 1e-6) list.Add(UtilityFor(q.Value > 0, OutletTemperature(go, q.Value > 0)));
                    }
                    break;
            }
            return list;
        }

        /// <summary>Where a heated or cooled item's utility lines go: two short lines up from the top of its icon.</summary>
        static void UtilityStubs(Pt c, double w, double h, out Pt supplyTop, out Pt returnTop)
        {
            var dx = Math.Max(2.5, 0.25 * w);
            supplyTop = new Pt(c.X - dx, c.Y + h / 2 + 8);
            returnTop = new Pt(c.X + dx, c.Y + h / 2 + 8);
        }

        /// <summary>The utility lines of an item: supply in and return out, dashed, with their codes over them.</summary>
        static void DrawUtility(List<SolidCraftScript.Command> cmds, Utility ut, Pt c, Pt half, double w, double h, string tag)
        {
            void C(string name, JObject p) => cmds.Add(new SolidCraftScript.Command { Name = name, Params = p, Subject = tag + " utility" });
            UtilityStubs(c, w, h, out var sTop, out var rTop);
            var top = c.Y + half.Y;
            var sBottom = new Pt(sTop.X, top);
            var rBottom = new Pt(rTop.X, top);
            C("layer.current", new JObject { ["name"] = "UTILITY" });
            C("pline", new JObject { ["vertices"] = new JArray(A(sTop), A(sBottom)) });
            Arrow(cmds, sTop, sBottom, tag);
            if (ut.Return.Length > 0)
            {
                C("pline", new JObject { ["vertices"] = new JArray(A(rBottom), A(rTop)) });
                Arrow(cmds, rBottom, rTop, tag);
            }
            C("layer.current", new JObject { ["name"] = "TEXT" });
            C("text", new JObject { ["at"] = new JArray(sTop.X, Math.Round(sTop.Y + 1, 2)), ["text"] = ut.Supply, ["height"] = 2.0, ["justify"] = "BC" });
            if (ut.Return.Length > 0)
                C("text", new JObject { ["at"] = new JArray(rTop.X, Math.Round(rTop.Y + 1, 2)), ["text"] = ut.Return, ["height"] = 2.0, ["justify"] = "BC" });
        }

        /// <summary>The mark of the nozzle a connection point stands for, as on the item's drawings; null for none.</summary>
        static string NozzleMark(PlantModel model, Unit u, IConnectionPoint cp)
        {
            if (model == null) return null;
            var con = cp.AttachedConnector;
            if (con == null) return null;
            var outlet = u.Go.OutputConnectors.Contains(cp);
            var other = outlet ? con.AttachedTo : con.AttachedFrom;
            if (other == null || other.ObjectType != ObjectType.MaterialStream) return null;
            var item = model.Equipment.FirstOrDefault(e => e.Name == u.Obj.Name && !e.IsInstrument);
            var run = model.Pipes.FirstOrDefault(r => r.Tag == other.Tag);
            if (item == null || run == null) return null;
            try { return EquipmentDrawings.NozzleMark(model, item, run, outlet); }
            catch (Exception) { return null; }
        }

        /// <summary>A nozzle mark beside the nozzle's stub, over it when the stub runs across, beside it when it runs up or down.</summary>
        static void DrawNozzleMark(List<SolidCraftScript.Command> cmds, string mark, Pt p, Pt q, string tag)
        {
            void C(string name, JObject o) => cmds.Add(new SolidCraftScript.Command { Name = name, Params = o, Subject = tag + " nozzle mark" });
            var across = Math.Abs(p.X - q.X) >= Math.Abs(p.Y - q.Y);
            var m = Dist(p, q) > 4 ? new Pt((p.X + q.X) / 2, (p.Y + q.Y) / 2) : q;
            C("layer.current", new JObject { ["name"] = "TEXT" });
            C("text", across
                ? new JObject { ["at"] = new JArray(m.X, Math.Round(m.Y + 0.8, 2)), ["text"] = mark, ["height"] = 1.8, ["justify"] = "BC" }
                : new JObject { ["at"] = new JArray(Math.Round(m.X - 0.8, 2), m.Y), ["text"] = mark, ["height"] = 1.8, ["justify"] = "MR" });
            C("layer.current", new JObject { ["name"] = "PROCESS" });
        }

        static string Caption(Symbol s)
        {
            switch (s)
            {
                case Symbol.VesselV: return "Vertical vessel";
                case Symbol.VesselH: return "Horizontal vessel";
                case Symbol.Tank: return "Storage tank";
                case Symbol.Column: return "Column";
                case Symbol.Pump: return "Centrifugal pump";
                case Symbol.Compressor: return "Compressor";
                case Symbol.Expander: return "Expander";
                case Symbol.Heater: return "Heater";
                case Symbol.Cooler: return "Cooler";
                case Symbol.Exchanger: return "Shell and tube exchanger";
                case Symbol.AirCooler: return "Air cooler";
                case Symbol.Valve: return "Valve";
                case Symbol.Relief: return "Relief valve, set pressure under it";
                case Symbol.Reactor: return "Reactor";
                case Symbol.Cstr: return "Stirred tank reactor";
                case Symbol.Pfr: return "Tubular reactor";
                case Symbol.Filter: return "Filter";
                case Symbol.Separator: return "Separator";
                default: return "Other equipment";
            }
        }

        /// <summary>
        /// The legend sheet of the P&amp;ID: the equipment symbols it uses, the valves, the lines, the
        /// instruments and their letters, the line number and the utilities, on a sheet of its own
        /// (the second of the drawing), plotted next to the diagram.
        /// </summary>
        static void Legend(CADCraftSession session, string outputBase, DrawingStyle style, TitleData title, List<Unit> units, Dictionary<string, string> actuated,
            bool loops, bool gauges, List<Utility> utilities, bool nozzleMarks, string temp, Result result, bool alarms = false, bool interlocks = false)
        {
            var sheet = SheetSize.For(style).First();
            DrawingSheets.Margins(style, out var ml, out var mo);
            DrawingSheets.TitleBlock(style, out var titleHeight);
            var cmds = new List<SolidCraftScript.Command>();
            void C(string name, JObject p) => cmds.Add(new SolidCraftScript.Command { Name = name, Params = p, Subject = "legend" });

            C("layout.new", new JObject { ["name"] = "LEGEND", ["viewport"] = false });
            C("pagesetup", new JObject
            {
                ["layout"] = "LEGEND", ["device"] = "CADCraft PDF", ["width"] = sheet.Width, ["height"] = sheet.Height, ["landscape"] = true,
                ["margins"] = new JArray(0, 0, 0, 0), ["plotArea"] = "layout", ["scale"] = 1, ["lineweights"] = true, ["plotStyleTable"] = "monochrome.ctb"
            });
            C("layout.set", new JObject { ["name"] = "LEGEND" });
            C("pspace", new JObject());
            var border = Path.Combine(temp, "legend-border.dxf");
            DrawingSheets.Border(style, sheet).Save(border);
            C("layer.current", new JObject { ["name"] = "BORDER" });
            C("insert", new JObject { ["name"] = "LEGEND_BORDER", ["file"] = border.Replace('\\', '/'), ["at"] = new JArray(0, 0) });
            var tb = Path.Combine(temp, "legend-title.dxf");
            DrawingSheets.TitleBlock(style, out _).Save(tb);
            var t2 = JObject.FromObject(title).ToObject<TitleData>();
            t2.Subtitle = "LEGEND"; t2.Sheet = "2/2"; t2.Scale = "NONE";
            C("layer.current", new JObject { ["name"] = "TITLE" });
            C("insert", new JObject
            {
                ["name"] = "LEGEND_TITLE", ["file"] = tb.Replace('\\', '/'), ["at"] = new JArray(sheet.Width - mo, mo),
                ["attribs"] = JObject.FromObject(DrawingSheets.Attributes(t2, sheet, style))
            });

            // the entries flow in rows of cells, each heading on a row of its own; cells over the
            // title block are skipped
            const double cellW = 120, cellH = 17, sym = 22;
            double x0 = ml + 10, x1 = sheet.Width - mo - 10, top = sheet.Height - mo - 10;
            var cols = Math.Max(1, (int)((x1 - x0) / cellW));
            var col = 0; var y = top;
            bool Free(double x, double yy) => !(x + cellW > sheet.Width - mo - DrawingSheets.TitleWidth - 2 && yy - cellH < mo + titleHeight + 4);
            void Heading(string text)
            {
                if (col > 0) { y -= cellH; col = 0; }
                y -= 9;
                C("layer.current", new JObject { ["name"] = "TEXT" });
                C("text", new JObject { ["at"] = new JArray(x0, Math.Round(y, 2)), ["text"] = text, ["height"] = 3.5, ["justify"] = "BL" });
                y -= 2;
            }
            Pt Next()
            {
                while (true)
                {
                    if (col >= cols) { col = 0; y -= cellH; }
                    var x = x0 + col * cellW;
                    col++;
                    if (Free(x, y)) return new Pt(x, y);
                }
            }
            void Text(Pt cell, string caption)
            {
                C("layer.current", new JObject { ["name"] = "TEXT" });
                C("text", new JObject { ["at"] = new JArray(Math.Round(cell.X + sym + 6, 2), Math.Round(cell.Y - cellH / 2, 2)), ["text"] = caption, ["height"] = TextHeight, ["justify"] = "ML" });
            }
            var n = 0;
            // the symbols drawn inside a box of the cell, a control valve small enough for its actuator
            void Sym(Pt cell, Symbol s, double aspect, bool act)
            {
                double boxW = act ? 9 : 18, boxH = act ? 5 : cellH - 5;
                double w = boxW, h = boxW / Math.Max(0.2, aspect);
                if (h > boxH) { h = boxH; w = h * aspect; }
                var d = new Dxf();
                Draw(d, s, w, h, false, act);
                var file = Path.Combine(temp, "legend" + (++n) + ".dxf");
                d.Save(file);
                C("layer.current", new JObject { ["name"] = "EQUIPMENT" });
                C("insert", new JObject { ["name"] = "LEGEND_" + n, ["file"] = file.Replace('\\', '/'), ["at"] = new JArray(Math.Round(cell.X + sym / 2, 2), Math.Round(cell.Y - cellH / 2 - (act ? 2 : 0), 2)) });
            }

            Heading("EQUIPMENT");
            foreach (var g in units.Where(u => u.Symbol != Symbol.PassThrough && u.Symbol != Symbol.Node &&
                    u.Symbol != Symbol.Valve && u.Symbol != Symbol.Relief).GroupBy(u => u.Symbol))
            {
                var first = g.First();
                var cell = Next();
                Sym(cell, g.Key, Math.Max(0.2, first.Go.Width / (double)Math.Max(1, first.Go.Height)), false);
                Text(cell, Caption(g.Key));
            }
            if (units.Any(u => u.Symbol == Symbol.Node))
            {
                var cell = Next();
                C("layer.current", new JObject { ["name"] = "PROCESS" });
                C("pline", new JObject { ["vertices"] = new JArray(new JArray(cell.X, cell.Y - cellH / 2), new JArray(cell.X + sym, cell.Y - cellH / 2)) });
                C("donut", new JObject { ["center"] = new JArray(cell.X + sym / 2, cell.Y - cellH / 2), ["inside"] = 0, ["outside"] = 1.6 });
                Text(cell, "Mixing or splitting point");
            }

            var valves = units.Where(u => u.Symbol == Symbol.Valve).ToList();
            var reliefs = units.Any(u => u.Symbol == Symbol.Relief);
            if (valves.Count > 0 || reliefs)
            {
                Heading("VALVES");
                if (valves.Any(v => !actuated.ContainsKey(v.Obj.Name))) { var cell = Next(); Sym(cell, Symbol.Valve, 1.6, false); Text(cell, "Valve"); }
                if (valves.Any(v => actuated.ContainsKey(v.Obj.Name))) { var cell = Next(); Sym(cell, Symbol.Valve, 1.6, true); Text(cell, "Control valve, diaphragm actuator"); }
                if (reliefs) { var cell = Next(); Sym(cell, Symbol.Relief, 1.6, false); Text(cell, Caption(Symbol.Relief)); }
            }

            Heading("LINES");
            void LineEntry(string layer, string caption, bool arrow)
            {
                var cell = Next();
                var a = new Pt(cell.X, cell.Y - cellH / 2); var b = new Pt(cell.X + sym, cell.Y - cellH / 2);
                C("layer.current", new JObject { ["name"] = layer });
                C("pline", new JObject { ["vertices"] = new JArray(A(a), A(b)) });
                if (arrow) Arrow(cmds, a, b, "legend");
                Text(cell, caption);
            }
            LineEntry("PROCESS", "Process line, flow direction", true);
            if (utilities.Count > 0) LineEntry("UTILITY", "Utility line", true);
            if (loops) LineEntry("SIGNAL", "Instrument signal", false);
            {
                var cell = Next();
                C("layer.current", new JObject { ["name"] = "TEXT" });
                C("text", new JObject { ["at"] = new JArray(Math.Round(cell.X, 2), Math.Round(cell.Y - cellH / 2, 2)), ["text"] = "3\"-P-12-H", ["height"] = TextHeight, ["justify"] = "ML" });
                Text(cell, "Line number: size, fluid code (P process), number,");
                C("text", new JObject { ["at"] = new JArray(Math.Round(cell.X + sym + 6, 2), Math.Round(cell.Y - cellH / 2 - 4, 2)), ["text"] = "insulation (H hot, C cold)", ["height"] = TextHeight, ["justify"] = "ML" });
            }
            if (nozzleMarks)
            {
                var cell = Next();
                C("layer.current", new JObject { ["name"] = "TEXT" });
                C("text", new JObject { ["at"] = new JArray(Math.Round(cell.X + sym / 2, 2), Math.Round(cell.Y - cellH / 2, 2)), ["text"] = "N1", ["height"] = 1.8, ["justify"] = "MC" });
                Text(cell, "Nozzle mark, as on the equipment drawings");
            }

            if (loops || gauges)
            {
                Heading("INSTRUMENTS");
                var cell = Next();
                DrawBubble(cmds, new Pt(cell.X + sym / 2, cell.Y - cellH / 2), "LT", "101", false, "legend");
                Text(cell, "Field instrument (transmitter, local indicator)");
                if (loops)
                {
                    cell = Next();
                    DrawBubble(cmds, new Pt(cell.X + sym / 2, cell.Y - cellH / 2), "LIC", "101", true, "legend");
                    Text(cell, "Controller, shared display (DCS)");
                }
                cell = Next();
                C("layer.current", new JObject { ["name"] = "TEXT" });
                C("text", new JObject { ["at"] = new JArray(Math.Round(cell.X, 2), Math.Round(cell.Y - cellH / 2, 2)), ["text"] = "F L P T A", ["height"] = TextHeight, ["justify"] = "ML" });
                Text(cell, "First letter: flow, level, pressure, temperature, analysis");
                cell = Next();
                C("layer.current", new JObject { ["name"] = "TEXT" });
                C("text", new JObject { ["at"] = new JArray(Math.Round(cell.X, 2), Math.Round(cell.Y - cellH / 2, 2)), ["text"] = "T IC I V", ["height"] = TextHeight, ["justify"] = "ML" });
                Text(cell, "Then: transmitter, controller, indicator, valve");
                if (alarms)
                {
                    cell = Next();
                    C("layer.current", new JObject { ["name"] = "TEXT" });
                    C("text", new JObject { ["at"] = new JArray(Math.Round(cell.X, 2), Math.Round(cell.Y - cellH / 2, 2)), ["text"] = "LAH 2.5 m", ["height"] = 1.8, ["justify"] = "ML" });
                    Text(cell, "Alarm: AH, AL high, low; AHH, ALL very high, very low");
                }
            }
            if (interlocks)
            {
                if (!(loops || gauges)) Heading("INSTRUMENTS");
                var cell = Next();
                var c0 = new Pt(cell.X + sym / 2, cell.Y - cellH / 2);
                C("layer.current", new JObject { ["name"] = "INSTRUMENT" });
                C("pline", new JObject { ["vertices"] = new JArray(A(new Pt(c0.X - Bubble, c0.Y)), A(new Pt(c0.X, c0.Y + Bubble)), A(new Pt(c0.X + Bubble, c0.Y)), A(new Pt(c0.X, c0.Y - Bubble))), ["closed"] = true });
                C("layer.current", new JObject { ["name"] = "TEXT" });
                C("text", new JObject { ["at"] = new JArray(c0.X, Math.Round(c0.Y + 1.2, 2)), ["text"] = "I", ["height"] = 1.8, ["justify"] = "MC" });
                C("text", new JObject { ["at"] = new JArray(c0.X, Math.Round(c0.Y - 1.6, 2)), ["text"] = "1", ["height"] = 1.6, ["justify"] = "MC" });
                Text(cell, "Interlock, from a hand switch (HS), with the values it sets");
            }

            var used = utilities.GroupBy(u => u.Supply).Select(g => g.First()).ToList();
            if (used.Count > 0)
            {
                Heading("UTILITIES");
                foreach (var ut in used)
                {
                    var cell = Next();
                    C("layer.current", new JObject { ["name"] = "TEXT" });
                    C("text", new JObject { ["at"] = new JArray(Math.Round(cell.X, 2), Math.Round(cell.Y - cellH / 2, 2)), ["text"] = ut.Supply + (ut.Return.Length > 0 ? " / " + ut.Return : ""), ["height"] = TextHeight, ["justify"] = "ML" });
                    Text(cell, ut.Name);
                }
            }

            foreach (var c in cmds) session.Execute(c.Name, c.Params);
            var pdf = outputBase + " legend.pdf"; var png = outputBase + " legend.png";
            foreach (var f in new[] { pdf, png }) if (File.Exists(f)) File.Delete(f);
            var o = ExportOptions.Load();
            if (o.SavePdf) session.Execute("plot", new JObject { ["path"] = pdf, ["layout"] = "LEGEND" }, 300);
            if (o.SavePng) session.Execute("plot", new JObject { ["path"] = png, ["layout"] = "LEGEND", ["device"] = "CADCraft PNG" }, 300);
            if (File.Exists(pdf)) result.LegendPdf = pdf;
            if (File.Exists(png)) result.LegendPng = png;
        }
    }
}
