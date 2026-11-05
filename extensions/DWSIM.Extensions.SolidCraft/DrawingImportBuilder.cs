// Copyright 2026 Daniel Wagner O. de Medeiros
//
// This file is part of DWSIM.
//
// DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DWSIM. If not, see
// <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.SharedClasses.SystemsOfUnits;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>What the import is to set up besides the items and lines.</summary>
    public class ImportSetup
    {
        /// <summary>The compounds, as DWSIM names.</summary>
        public List<string> Compounds = new List<string>();

        /// <summary>The property package, as <see cref="IFlowsheet.GetAvailablePropertyPackages"/> names it.</summary>
        public string PropertyPackage = "";

        /// <summary>Make the drawing's units the flowsheet's system of units.</summary>
        public bool UseDrawingUnits = true;

        /// <summary>Set the items from the stream table: outlet temperatures and pressures, split ratios, feeds.</summary>
        public bool SpecsFromTable = true;
    }

    /// <summary>
    /// Builds the flowsheet from a corrected reading: the compounds and the property package, an
    /// object for each item where the drawing has it, a material stream for each line on the
    /// nozzles the drawing and the table point to, the energy streams the items need, a recycle on
    /// each loop, and the feeds and the items' specifications from the stream table.
    /// </summary>
    public static class DrawingImportBuilder
    {
        /// <summary>The width of the canvas the drawing's sheet maps onto, in flowsheet units.</summary>
        const double CanvasWidth = 2400;

        public class Result
        {
            /// <summary>The flowsheet objects made, by the item tag or the stream name they came from.</summary>
            public Dictionary<string, ISimulationObject> Items = new Dictionary<string, ISimulationObject>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<ReadLine, ISimulationObject> Streams = new Dictionary<ReadLine, ISimulationObject>();
            public List<ImportIssue> Issues = new List<ImportIssue>();
            public int Recycles;
        }

        public static Result Build(IFlowsheet fs, DrawingReading r, ImportSetup setup)
        {
            var res = new Result();
            var wasEmpty = fs.SimulationObjects.Count == 0;
            void Note(ImportIssue.Levels level, string subject, string text) => res.Issues.Add(new ImportIssue(level, subject, text));

            // compounds first, so the streams are made with them
            foreach (var c in setup.Compounds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (fs.SelectedCompounds.ContainsKey(c)) continue;
                var name = CompoundName(fs, c);
                if (name == null) { Note(ImportIssue.Levels.Warning, c, "not in the compound database; add it by hand"); continue; }
                if (!fs.SelectedCompounds.ContainsKey(name)) fs.AddCompound(name);
            }
            // the package: one of the same kind the flowsheet has, else a new one
            IPropertyPackage package = null;
            if (!string.IsNullOrWhiteSpace(setup.PropertyPackage))
            {
                var kind = fs.AvailablePropertyPackages.TryGetValue(setup.PropertyPackage, out var model) ? model.GetType() : null;
                package = fs.PropertyPackages.Values.FirstOrDefault(p => kind != null && p.GetType() == kind);
                if (package == null)
                    try { package = fs.CreateAndAddPropertyPackage(setup.PropertyPackage); }
                    catch (Exception ex) { Note(ImportIssue.Levels.Error, setup.PropertyPackage, "the property package could not be added: " + ex.GetBaseException().Message); }
            }
            var units = setup.UseDrawingUnits ? UnitSystem(fs, r.Units, res) : fs.FlowsheetOptions.SelectedUnitSystem;

            // where the items go: the drawing's layout, scaled onto the canvas
            var items = r.ItemsByTag();
            var lines = r.Lines.Where(l => l.Include && (l.Feed || items.ContainsKey(l.From)) && (l.Product || items.ContainsKey(l.To)) && !(l.Feed && l.Product)).ToList();
            var placed = items.Values.Count(i => !double.IsNaN(i.X) && !double.IsNaN(i.Y));
            var useDrawing = placed >= Math.Max(1, items.Count * 0.6);
            double Px(ReadItem i, int k) => useDrawing && !double.IsNaN(i.X) ? i.X * CanvasWidth : 150 + 180 * k;
            double Py(ReadItem i, int k) => useDrawing && !double.IsNaN(i.Y) ? i.Y * CanvasWidth * 0.7 : 300;

            var n = 0;
            foreach (var item in items.Values)
            {
                ObjectType type;
                if (!Enum.TryParse(item.Type, out type)) { Note(ImportIssue.Levels.Error, item.Tag, "unknown object type " + item.Type); continue; }
                var tag = FreeTag(fs, item.Tag);
                var obj = fs.AddObject(type, (int)Px(item, n), (int)Py(item, n), tag);
                n++;
                if (obj == null) { Note(ImportIssue.Levels.Error, item.Tag, "the flowsheet did not make a " + item.Type); continue; }
                if (tag != item.Tag) Note(ImportIssue.Levels.Info, item.Tag, "the flowsheet already has this tag; the new item is " + tag);
                res.Items[item.Tag] = obj;
            }

            // the streams, between the items they join
            var used = new Dictionary<ISimulationObject, HashSet<int>>();
            foreach (var l in lines)
            {
                var from = l.Feed ? null : Get(res, l.From);
                var to = l.Product ? null : Get(res, l.To);
                if ((!l.Feed && from == null) || (!l.Product && to == null)) continue;
                var pos = StreamPosition(from, to);
                var ms = fs.AddObject(ObjectType.MaterialStream, (int)pos.Item1, (int)pos.Item2, FreeTag(fs, r.StreamTag(l)));
                if (ms == null) continue;
                res.Streams[l] = ms;
            }

            if (package != null)
                foreach (var o in res.Items.Values.Concat(res.Streams.Values))
                    try { o.PropertyPackage = package; } catch (Exception) { }

            // nozzles: outlets by what they carry (top or bottom), exchanger passes by their flows
            foreach (var item in items.Values)
            {
                if (!res.Items.TryGetValue(item.Tag, out var obj)) continue;
                var ins = lines.Where(l => res.Streams.ContainsKey(l) && string.Equals(l.To, item.Tag, StringComparison.OrdinalIgnoreCase)).ToList();
                var outs = lines.Where(l => res.Streams.ContainsKey(l) && string.Equals(l.From, item.Tag, StringComparison.OrdinalIgnoreCase)).ToList();
                var inPorts = Ports(obj.GraphicObject.InputConnectors, ConType.ConIn);
                var outPorts = Ports(obj.GraphicObject.OutputConnectors, ConType.ConOut);
                var inAt = new Dictionary<ReadLine, int>();
                var outAt = new Dictionary<ReadLine, int>();

                if (obj.GraphicObject.ObjectType == ObjectType.HeatExchanger && ins.Count == 2 && outs.Count == 2)
                {
                    // each pass keeps its flow: pair the inlets with the outlets of the same flow
                    inAt[ins[0]] = 0; inAt[ins[1]] = 1;
                    var straight = Mismatch(ins[0], outs[0]) + Mismatch(ins[1], outs[1]);
                    var crossed = Mismatch(ins[0], outs[1]) + Mismatch(ins[1], outs[0]);
                    if (crossed < straight) { outAt[outs[1]] = 0; outAt[outs[0]] = 1; } else { outAt[outs[0]] = 0; outAt[outs[1]] = 1; }
                }
                else
                {
                    for (var i = 0; i < ins.Count && i < inPorts.Count; i++) inAt[ins[i]] = inPorts[i];
                    foreach (var kv in OutletPorts(obj.GraphicObject.ObjectType, outs, outPorts)) outAt[kv.Key] = kv.Value;
                }
                foreach (var l in ins.Where(l => !inAt.ContainsKey(l)))
                    Note(ImportIssue.Levels.Warning, item.Tag, r.StreamTag(l) + " has no free inlet left on the item; connect it by hand");
                foreach (var l in outs.Where(l => !outAt.ContainsKey(l)))
                    Note(ImportIssue.Levels.Warning, item.Tag, r.StreamTag(l) + " has no free outlet left on the item; connect it by hand");
                foreach (var kv in inAt) Connect(fs, res.Streams[kv.Key], obj, 0, kv.Value, res, item.Tag);
                foreach (var kv in outAt) Connect(fs, obj, res.Streams[kv.Key], kv.Value, 0, res, item.Tag);
            }

            // the energy streams the items calculate with
            foreach (var kv in res.Items)
                AddEnergyStreams(fs, kv.Value, res);

            // a recycle on each loop, where the loop's line goes back against the flow
            foreach (var l in TearLines(lines, res))
            {
                var ms = res.Streams[l];
                var to = Get(res, l.To);
                var go = ms.GraphicObject;
                var conTo = go.OutputConnectors[0].AttachedConnector;
                if (conTo == null) continue;
                var toIndex = conTo.AttachedToConnectorIndex;
                fs.DisconnectObjects(go, to.GraphicObject);
                var rec = fs.AddObject(ObjectType.OT_Recycle, (int)(go.X + 60), (int)go.Y, FreeTag(fs, "REC-" + ms.GraphicObject.Tag));
                var back = fs.AddObject(ObjectType.MaterialStream, (int)(go.X + 120), (int)go.Y, FreeTag(fs, ms.GraphicObject.Tag + "-R"));
                fs.ConnectObjects(go, rec.GraphicObject, 0, 0);
                fs.ConnectObjects(rec.GraphicObject, back.GraphicObject, 0, 0);
                fs.ConnectObjects(back.GraphicObject, to.GraphicObject, 0, toIndex);
                if (setup.SpecsFromTable) SetStream(fs, back, l, r.Units, res, false);
                res.Recycles++;
            }

            if (setup.SpecsFromTable)
            {
                foreach (var kv in res.Streams.Where(s => s.Key.Feed)) SetStream(fs, kv.Value, kv.Key, r.Units, res, true);
                foreach (var item in items.Values)
                    if (res.Items.TryGetValue(item.Tag, out var obj)) Specify(fs, obj, item, lines, r.Units, res);
                // a drum flashed at a fixed temperature or pressure exchanges heat
                foreach (var kv in res.Items)
                    if (kv.Value is Vessel ve && (ve.OverrideT || ve.OverrideP)) AddEnergyStreams(fs, kv.Value, res, true);
            }
            foreach (var kv in res.Streams.Where(s => s.Key.Feed && !s.Key.HasData))
                Note(ImportIssue.Levels.Warning, kv.Value.GraphicObject.Tag, "a feed without data: set its conditions, flow and composition");

            if (!useDrawing) try { fs.NaturalLayout(); } catch (Exception) { }
            if (units != null) fs.FlowsheetOptions.SelectedUnitSystem = units;
            Describe(fs, r, setup, wasEmpty);
            return res;
        }

        /// <summary>
        /// The simulation's name and comments from the drawing: the name is the drawing's number and
        /// title when the flowsheet had no name or nothing in it; the comments get what the reading
        /// found (the title block, the process, the compounds and package, the units, the notes),
        /// after any comments already there. The author is left to the user.
        /// </summary>
        public static void Describe(IFlowsheet fs, DrawingReading r, ImportSetup setup, bool newFlowsheet)
        {
            var o = fs.FlowsheetOptions;
            var name = r.DrawingName();
            if (name.Length > 0 && (newFlowsheet || string.IsNullOrWhiteSpace(o.SimulationName))) o.SimulationName = name;

            var t = new System.Text.StringBuilder();
            t.AppendLine("Imported on " + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " from the " +
                (r.Kind == "pid" ? "P&ID" : "process flow diagram") + " " + Path.GetFileName(r.Source ?? "") + ", read by the AI Assistant.");
            if (r.Process.Description.Length > 0) t.AppendLine("Process: " + r.Process.Description);
            void Line(string label, params string[] fields)
            {
                var v = string.Join(", ", fields.Select(r.Title).Where(x => x.Length > 0));
                if (v.Length > 0) t.AppendLine(label + ": " + v);
            }
            Line("Drawing", "document_number");
            Line("Title", "title");
            Line("Revision", "revision", "date");
            Line("Sheet", "sheet");
            Line("Company", "company", "client");
            Line("Project", "project", "unit");
            Line("Drawn by", "drawn_by");
            if (r.Process.Class.Length > 0) t.AppendLine("Kind of process: " + r.Process.Class + (setup.PropertyPackage.Length > 0 ? " (property package " + setup.PropertyPackage + ")" : ""));
            if (setup.Compounds.Count > 0) t.AppendLine("Compounds: " + string.Join(", ", setup.Compounds));
            t.AppendLine("Units of the drawing: " + string.Join(", ", new[] { r.Units.Temperature, r.Units.Pressure, r.Units.MassFlow, r.Units.Energy }.Where(x => !string.IsNullOrWhiteSpace(x))));
            if (r.Process.Evidence.Length > 0) t.AppendLine("Evidence: " + r.Process.Evidence);
            if (r.Notes.Count > 0)
            {
                t.AppendLine("Notes on the drawing:");
                foreach (var n in r.Notes) t.AppendLine("- " + n);
            }
            var before = o.SimulationComments ?? "";
            o.SimulationComments = (before.Trim().Length > 0 ? before.TrimEnd() + Environment.NewLine + Environment.NewLine : "") + t.ToString().TrimEnd();
        }

        #region items and nozzles

        static ISimulationObject Get(Result res, string tag) => res.Items.TryGetValue(tag ?? "", out var o) ? o : null;

        static string FreeTag(IFlowsheet fs, string tag)
        {
            var t = string.IsNullOrWhiteSpace(tag) ? "OBJ" : tag.Trim();
            if (fs.GetFlowsheetSimulationObject(t) == null) return t;
            for (var i = 2; ; i++) if (fs.GetFlowsheetSimulationObject(t + " (" + i + ")") == null) return t + " (" + i + ")";
        }

        static List<int> Ports(List<IConnectionPoint> cps, ConType type)
        {
            var list = new List<int>();
            for (var i = 0; i < cps.Count; i++) if (cps[i].Type == type) list.Add(i);
            return list;
        }

        static double Mismatch(ReadLine a, ReadLine b) => a.W.HasValue && b.W.HasValue ? Math.Abs(a.W.Value - b.W.Value) / Math.Max(1e-9, Math.Abs(a.W.Value) + Math.Abs(b.W.Value)) : 0.5;

        /// <summary>
        /// The outlet each line leaves from: where the item has a top and a bottom outlet (vessels,
        /// columns, reactors with a gas and a liquid product), the drawing's nozzle name decides, then
        /// the table's vapor fraction, then the temperature (the top runs colder); otherwise the
        /// outlets are taken in order.
        /// </summary>
        static Dictionary<ReadLine, int> OutletPorts(ObjectType type, List<ReadLine> outs, List<int> ports)
        {
            var map = new Dictionary<ReadLine, int>();
            var topBottom = type == ObjectType.Vessel || type == ObjectType.ShortcutColumn || type == ObjectType.RCT_Conversion ||
                type == ObjectType.RCT_Equilibrium || type == ObjectType.RCT_Gibbs || type == ObjectType.TPVessel;
            if (!topBottom || ports.Count < 2)
            {
                for (var i = 0; i < outs.Count && i < ports.Count; i++) map[outs[i]] = ports[i];
                return map;
            }
            double Top(ReadLine l)
            {
                var hint = (l.FromPort ?? "").ToLowerInvariant();
                if (hint.Contains("top") || hint.Contains("vap") || hint.Contains("gas") || hint.Contains("overhead") || hint.Contains("distil")) return 10;
                if (hint.Contains("bot") || hint.Contains("liq")) return -10;
                if (l.VaporFraction.HasValue) return l.VaporFraction.Value - 0.5;
                return 0;
            }
            var ordered = outs.OrderByDescending(Top).ThenBy(l => l.T ?? 0).ToList();
            if (ordered.Count == 1)
            {
                // a single outlet: the bottom one when it carries liquid
                map[ordered[0]] = Top(ordered[0]) < 0 ? ports[1] : ports[0];
                return map;
            }
            for (var i = 0; i < ordered.Count && i < ports.Count; i++) map[ordered[i]] = ports[i];
            return map;
        }

        static void Connect(IFlowsheet fs, ISimulationObject from, ISimulationObject to, int fromIndex, int toIndex, Result res, string subject)
        {
            try { fs.ConnectObjects(from.GraphicObject, to.GraphicObject, fromIndex, toIndex); }
            catch (Exception ex) { res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, subject, "could not connect " + from.GraphicObject.Tag + " to " + to.GraphicObject.Tag + ": " + ex.GetBaseException().Message)); }
        }

        static void AddEnergyStreams(IFlowsheet fs, ISimulationObject obj, Result res, bool always = false)
        {
            var go = obj.GraphicObject;
            if (!always)
            switch (go.ObjectType)
            {
                case ObjectType.Heater:
                case ObjectType.Cooler:
                case ObjectType.Pump:
                case ObjectType.Compressor:
                case ObjectType.Expander:
                case ObjectType.ShortcutColumn:
                case ObjectType.RCT_Conversion:
                case ObjectType.RCT_Equilibrium:
                case ObjectType.RCT_Gibbs:
                case ObjectType.RCT_CSTR:
                case ObjectType.RCT_PFR:
                    break;
                default:
                    return;
            }
            // a duty or power that leaves the item (cooler, expander, condenser)
            if (go.EnergyConnector != null && go.EnergyConnector.Active && !go.EnergyConnector.IsAttached)
            {
                var e = fs.AddObject(ObjectType.EnergyStream, (int)(go.X + go.Width + 30), (int)(go.Y - 40), FreeTag(fs, "E-" + go.Tag));
                Connect(fs, obj, e, 0, 0, res, go.Tag);
            }
            // a duty or power that goes in (heater, pump, compressor, reboiler, reactor)
            for (var i = 0; i < go.InputConnectors.Count; i++)
            {
                var cp = go.InputConnectors[i];
                if (cp.Type != ConType.ConEn || cp.IsAttached) continue;
                var e = fs.AddObject(ObjectType.EnergyStream, (int)(go.X - 60), (int)(go.Y + go.Height + 30), FreeTag(fs, (i > 1 ? "E" + i + "-" : "E-") + go.Tag));
                Connect(fs, e, obj, 0, i, res, go.Tag);
            }
        }

        static Tuple<double, double> StreamPosition(ISimulationObject from, ISimulationObject to)
        {
            IGraphicObject a = from?.GraphicObject, b = to?.GraphicObject;
            if (a != null && b != null) return Tuple.Create((a.X + a.Width + b.X) / 2.0, (a.Y + b.Y) / 2.0 + 10);
            if (b != null) return Tuple.Create(b.X - 110.0, b.Y + b.Height / 2.0 - 10);
            return Tuple.Create(a.X + a.Width + 80.0, a.Y + a.Height / 2.0 - 10);
        }

        /// <summary>The lines that close a loop (back edges of a walk from the feeds), one per loop.</summary>
        static List<ReadLine> TearLines(List<ReadLine> lines, Result res)
        {
            var between = lines.Where(l => !l.Feed && !l.Product && res.Streams.ContainsKey(l)).ToList();
            var next = between.GroupBy(l => l.From, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 1 on the path, 2 done
            var tears = new List<ReadLine>();
            void Walk(string u)
            {
                state[u] = 1;
                if (next.TryGetValue(u, out var outs))
                    foreach (var l in outs)
                    {
                        state.TryGetValue(l.To, out var s);
                        if (s == 1) tears.Add(l);
                        else if (s == 0) Walk(l.To);
                    }
                state[u] = 2;
            }
            var starts = lines.Where(l => l.Feed && !l.Product).Select(l => l.To).Concat(res.Items.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var u in starts) { state.TryGetValue(u, out var s); if (s == 0) Walk(u); }
            return tears;
        }

        #endregion

        #region values

        static double? SI(string unit, double? v, Result res, string subject)
        {
            if (!v.HasValue || double.IsNaN(v.Value)) return null;
            try { return Converter.ConvertToSI(unit, v.Value); }
            catch (Exception) { res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, subject, "the unit " + unit + " is not known to DWSIM; the value was left out")); return null; }
        }

        /// <summary>A stream's conditions, flow and composition from its column of the table.</summary>
        static void SetStream(IFlowsheet fs, ISimulationObject so, ReadLine l, UnitsUsed u, Result res, bool feed)
        {
            var ms = (IMaterialStream)so;
            var tag = so.GraphicObject.Tag;
            var t = SI(u.Temperature, l.T, res, tag);
            var p = SI(u.Pressure, l.P, res, tag);
            var w = SI(u.MassFlow, l.W, res, tag);
            if (t.HasValue) ms.SetTemperature(t.Value);
            if (p.HasValue) ms.SetPressure(p.Value);

            // the compounds' flows the table lists, matched to the flowsheet's compounds
            var flows = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in l.Compounds)
            {
                var name = MatchSelected(fs, c.Key);
                var v = SI(u.MassFlow, c.Value, res, tag);
                if (name != null && v.HasValue) flows[name] = (flows.TryGetValue(name, out var o) ? o : 0) + Math.Max(0, v.Value);
            }
            var total = flows.Values.Sum();
            if (total > 0)
            {
                if (ms is MaterialStream stream)
                    foreach (var c in fs.SelectedCompounds.Keys) stream.SetOverallCompoundMassFlow(c, flows.TryGetValue(c, out var f) ? f : 0.0);
                if (w.HasValue && Math.Abs(total - w.Value) > 0.02 * w.Value && feed)
                    res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, tag, "the compounds the table lists add up to " + Math.Round(100 * total / w.Value) + " % of the flow; the rest is left out"));
            }
            else if (feed && fs.SelectedCompounds.Count > 0)
            {
                // a feed named after a compound ("Water", "NaOH", "Tripalmitin Feed") is taken as that compound
                var named = NamedCompound(fs, tag);
                if (named != null && so is MaterialStream single)
                {
                    foreach (var c in fs.SelectedCompounds.Keys) single.SetOverallCompoundMassFlow(c, c == named ? 1.0 : 0.0);
                    res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, tag, "no composition on the drawing; taken as pure " + named + " from its name"));
                }
                else res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, tag, "no composition from the table; set it before solving"));
            }
            if (w.HasValue && w.Value > 0) ms.SetMassFlow(w.Value);

            // a pure compound at saturation: temperature and pressure do not tell vapor from
            // liquid, the table's vapor fraction does
            object pp = (so as MaterialStream)?.PropertyPackage ?? (object)fs.PropertyPackages.Values.FirstOrDefault();
            if (fs.SelectedCompounds.Count == 1 && l.VaporFraction.HasValue && t.HasValue && p.HasValue && so is MaterialStream pure && pp != null)
            {
                try
                {
                    pp.GetType().GetProperty("CurrentMaterialStream")?.SetValue(pp, pure);
                    var pvap = pp.GetType().GetMethod("AUX_PVAPi", new[] { typeof(string), typeof(double) });
                    var pv = (double)pvap.Invoke(pp, new object[] { fs.SelectedCompounds.Keys.First(), t.Value });
                    if (Math.Abs(pv - p.Value) < 0.03 * p.Value)
                    {
                        pure.Phases[2].Properties.molarfraction = Math.Max(0, Math.Min(1, l.VaporFraction.Value));
                        pure.SpecType = DWSIM.Interfaces.Enums.StreamSpec.Pressure_and_VaporFraction;
                    }
                }
                catch (Exception ex) { res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, tag, "could not check the saturation: " + ex.GetBaseException().Message)); }
            }
        }

        /// <summary>The specifications the table gives: outlet temperatures and pressures, split ratios.</summary>
        static void Specify(IFlowsheet fs, ISimulationObject obj, ReadItem item, List<ReadLine> lines, UnitsUsed u, Result res)
        {
            var tag = item.Tag;
            var ins = lines.Where(l => string.Equals(l.To, tag, StringComparison.OrdinalIgnoreCase)).ToList();
            var outs = lines.Where(l => string.Equals(l.From, tag, StringComparison.OrdinalIgnoreCase)).ToList();
            var tOut = outs.Count == 1 ? SI(u.Temperature, outs[0].T, res, tag) : null;
            var pOut = outs.Count == 1 ? SI(u.Pressure, outs[0].P, res, tag) : null;
            var pIn = ins.Count == 1 ? SI(u.Pressure, ins[0].P, res, tag) : null;
            void Missing(string what) => res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, tag, "the table does not give " + what + "; set it before solving"));
            void Efficiency()
            {
                if (ins.Count == 1 && outs.Count == 1 && ins[0].T.HasValue && outs[0].T.HasValue)
                    res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, tag, "the drawing gives no efficiency; DWSIM's default is used, so the outlet temperature may differ from the table's"));
            }

            switch (obj)
            {
                case Heater h:
                    if (tOut.HasValue) { h.CalcMode = Heater.CalculationMode.OutletTemperature; h.OutletTemperature = tOut; } else Missing("the outlet temperature");
                    if (pIn.HasValue && pOut.HasValue) h.DeltaP = Math.Max(0, pIn.Value - pOut.Value);
                    break;
                case Cooler c:
                    if (tOut.HasValue) { c.CalcMode = Cooler.CalculationMode.OutletTemperature; c.OutletTemperature = tOut; } else Missing("the outlet temperature");
                    if (pIn.HasValue && pOut.HasValue) c.DeltaP = Math.Max(0, pIn.Value - pOut.Value);
                    break;
                case Pump pu:
                    if (pOut.HasValue) { pu.CalcMode = Pump.CalculationMode.OutletPressure; pu.Pout = pOut.Value; } else Missing("the outlet pressure");
                    Efficiency();
                    break;
                case Compressor co:
                    if (pOut.HasValue) { co.CalcMode = Compressor.CalculationMode.OutletPressure; co.POut = pOut.Value; } else Missing("the outlet pressure");
                    Efficiency();
                    break;
                case Expander ex:
                    if (pOut.HasValue) { ex.CalcMode = Expander.CalculationMode.OutletPressure; ex.POut = pOut.Value; } else Missing("the outlet pressure");
                    Efficiency();
                    break;
                case Vessel ve:
                    // a flash at the outlets' conditions where the table shows the drum heated,
                    // cooled or let down
                    var tv = outs.Select(l => SI(u.Temperature, l.T, res, tag)).FirstOrDefault(x => x.HasValue);
                    var pv = outs.Select(l => SI(u.Pressure, l.P, res, tag)).FirstOrDefault(x => x.HasValue);
                    var ti = ins.Select(l => SI(u.Temperature, l.T, res, tag)).Where(x => x.HasValue).Select(x => x.Value).ToList();
                    var pi = ins.Select(l => SI(u.Pressure, l.P, res, tag)).Where(x => x.HasValue).Select(x => x.Value).ToList();
                    if (tv.HasValue && ti.Count > 0 && ti.Any(x => Math.Abs(x - tv.Value) > 0.5)) { ve.OverrideT = true; ve.FlashTemperature = tv.Value; }
                    if (pv.HasValue && pi.Count > 0 && pi.Any(x => Math.Abs(x - pv.Value) > 0.01 * pv.Value)) { ve.OverrideP = true; ve.FlashPressure = pv.Value; }
                    break;
                case Valve v:
                    if (pOut.HasValue) { v.CalcMode = Valve.CalculationMode.OutletPressure; v.OutletPressure = pOut; } else Missing("the outlet pressure");
                    break;
                case Splitter s:
                    var w = outs.Select(l => SI(u.MassFlow, l.W, res, tag)).ToList();
                    if (w.Count > 0 && w.All(x => x.HasValue) && w.Sum(x => x.Value) > 0)
                    {
                        var sum = w.Sum(x => x.Value);
                        s.Ratios.Clear();
                        for (var i = 0; i < 3; i++) s.Ratios.Add(i < w.Count ? w[i].Value / sum : 0.0);
                        s.OperationMode = Splitter.OpMode.SplitRatios;
                    }
                    else Missing("the flows of its outlets");
                    break;
                case HeatExchanger hx:
                    // the hot side's outlet temperature: the outlet of the hotter inlet's pass
                    var hotOut = outs.Where(l => l.T.HasValue).OrderByDescending(l => l.T.Value).FirstOrDefault();
                    var hotIn = ins.Where(l => l.T.HasValue).OrderByDescending(l => l.T.Value).FirstOrDefault();
                    if (hotIn != null && ins.Count == 2 && outs.Count == 2)
                    {
                        var pair = outs.OrderBy(l => Mismatch(hotIn, l)).First();
                        var t = SI(u.Temperature, pair.T, res, tag);
                        if (t.HasValue) { hx.CalculationMode = HeatExchangerCalcMode.CalcTempColdOut; hx.HotSideOutletTemperature = t.Value; break; }
                    }
                    Missing("the exchanger's outlet temperatures");
                    break;
                case ShortcutColumn sc:
                    SpecifyColumn(fs, sc, outs, u, res);
                    break;
                case Reactor_Conversion _:
                case Reactor_Equilibrium _:
                case Reactor_CSTR _:
                case Reactor_PFR _:
                    res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, tag, "add the reactions and the reactor's specifications before solving"));
                    break;
            }
        }

        /// <summary>
        /// A shortcut column from its products' columns in the table: the keys are the compounds
        /// nearest the cut on either side (the one that goes mostly overhead with the most left in
        /// the bottoms, and the one that goes mostly down with the most in the distillate), their
        /// mole fractions in the other product, and the condenser and reboiler pressures.
        /// </summary>
        static void SpecifyColumn(IFlowsheet fs, ShortcutColumn sc, List<ReadLine> outs, UnitsUsed u, Result res)
        {
            var tag = sc.GraphicObject.Tag;
            ReadLine At(int port)
            {
                var con = sc.GraphicObject.OutputConnectors[port].AttachedConnector;
                return con == null ? null : res.Streams.FirstOrDefault(kv => kv.Value.GraphicObject == con.AttachedTo).Key;
            }
            var d = At(0); var b = At(1);
            if (d == null || b == null) { res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, tag, "the column needs a distillate and a bottoms line")); return; }
            var pd = SI(u.Pressure, d.P, res, tag); var pb = SI(u.Pressure, b.P, res, tag);
            if (pd.HasValue) sc.m_condenserpressure = pd.Value;
            if (pb.HasValue) sc.m_boilerpressure = pb.Value;
            if (d.VaporFraction.HasValue && d.VaporFraction.Value > 0.5) sc.condtype = ShortcutColumn.CondenserType.PartialCond;

            Dictionary<string, double> Moles(ReadLine l)
            {
                var m = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in l.Compounds)
                {
                    var name = MatchSelected(fs, c.Key);
                    if (name == null || c.Value <= 0) continue;
                    var mw = fs.SelectedCompounds[name].Molar_Weight;
                    if (mw > 0) m[name] = (m.TryGetValue(name, out var o) ? o : 0) + c.Value / mw;
                }
                return m;
            }
            var nd = Moles(d); var nb = Moles(b);
            double sd = nd.Values.Sum(), sb = nb.Values.Sum();
            if (sd <= 0 || sb <= 0)
            {
                res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, tag, "the table gives no compositions for the products; set the light and heavy keys and their fractions before solving"));
                return;
            }
            var rec = nd.Keys.Union(nb.Keys).ToDictionary(c => c, c => (nd.TryGetValue(c, out var x) ? x : 0) / ((nd.TryGetValue(c, out var y) ? y : 0) + (nb.TryGetValue(c, out var z) ? z : 0)));
            var lk = rec.Where(kv => kv.Value >= 0.5).OrderBy(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            var hk = rec.Where(kv => kv.Value < 0.5).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            if (lk == null || hk == null)
            {
                res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, tag, "the products do not show a split between two compounds; set the keys by hand"));
                return;
            }
            sc.m_lightkey = lk;
            sc.m_heavykey = hk;
            sc.m_lightkeymolarfrac = Math.Max(1e-6, (nb.TryGetValue(lk, out var xl) ? xl : 0) / sb);
            sc.m_heavykeymolarfrac = Math.Max(1e-6, (nd.TryGetValue(hk, out var xh) ? xh : 0) / sd);
            res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, tag, string.Format(CultureInfo.InvariantCulture,
                "keys from the table: {0} (light, {1:G3} in the bottoms) and {2} (heavy, {3:G3} in the distillate); the reflux ratio is not on the drawing, {4} is used",
                lk, sc.m_lightkeymolarfrac, hk, sc.m_heavykeymolarfrac, sc.m_refluxratio)));
        }

        #endregion

        #region compounds and units

        /// <summary>The database name of a compound the reading names (case, a cut-short name, common synonyms).</summary>
        public static string CompoundName(IFlowsheet fs, string name)
        {
            var n = (name ?? "").Trim();
            if (n.Length == 0) return null;
            var keys = fs.AvailableCompounds.Keys.ToList();
            var exact = keys.FirstOrDefault(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            var plain = Plain(n);
            exact = keys.FirstOrDefault(k => Plain(k) == plain);
            if (exact != null) return exact;
            // a name the table cut short ("Carbon dioxi.")
            if (n.EndsWith(".") && n.Length > 4)
            {
                var stem = Plain(n.TrimEnd('.'));
                var hits = keys.Where(k => Plain(k).StartsWith(stem)).ToList();
                if (hits.Count == 1) return hits[0];
            }
            // the formula or a CAS number
            foreach (var kv in fs.AvailableCompounds)
                if (string.Equals(kv.Value.Formula, n, StringComparison.OrdinalIgnoreCase) || string.Equals(kv.Value.CAS_Number, n, StringComparison.OrdinalIgnoreCase)) return kv.Key;
            return null;
        }

        static string MatchSelected(IFlowsheet fs, string name)
        {
            var keys = fs.SelectedCompounds.Keys.ToList();
            var n = (name ?? "").Trim();
            var hit = keys.FirstOrDefault(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase)) ?? keys.FirstOrDefault(k => Plain(k) == Plain(n));
            if (hit == null && n.EndsWith("."))
            {
                var stem = Plain(n.TrimEnd('.'));
                var hits = keys.Where(k => Plain(k).StartsWith(stem)).ToList();
                if (hits.Count == 1) hit = hits[0];
            }
            return hit;
        }

        /// <summary>The flowsheet compound a stream's name is, or holds (its name or formula as a word), if just one.</summary>
        static string NamedCompound(IFlowsheet fs, string streamName)
        {
            var words = System.Text.RegularExpressions.Regex.Split((streamName ?? "").ToLowerInvariant(), "[^a-z0-9]+").Where(w => w.Length > 0).ToList();
            var plain = Plain(streamName);
            var hits = fs.SelectedCompounds.Where(kv => (Plain(kv.Key).Length > 2 && plain.Contains(Plain(kv.Key))) ||
                (!string.IsNullOrEmpty(kv.Value.Formula) && words.Contains(kv.Value.Formula.ToLowerInvariant()))).Select(kv => kv.Key).ToList();
            return hits.Count == 1 ? hits[0] : null;
        }

        static string Plain(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        /// <summary>The flowsheet's system of units that matches the drawing, or a new one made from SI.</summary>
        static IUnitsOfMeasure UnitSystem(IFlowsheet fs, UnitsUsed u, Result res)
        {
            bool Same(string a, string b) => string.Equals((a ?? "").Replace(" ", ""), (b ?? "").Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
            var match = fs.AvailableSystemsOfUnits.FirstOrDefault(s => Same(s.temperature, u.Temperature) && Same(s.pressure, u.Pressure) && Same(s.massflow, u.MassFlow) && Same(s.heatflow, u.Energy))
                ?? fs.AvailableSystemsOfUnits.FirstOrDefault(s => Same(s.temperature, u.Temperature) && Same(s.pressure, u.Pressure) && Same(s.massflow, u.MassFlow));
            if (match != null) return match;
            try
            {
                foreach (var unit in new[] { u.Temperature, u.Pressure, u.MassFlow, u.Energy }) Converter.ConvertToSI(unit, 1.0);
                var units = new SI();
                units.Name = "Drawing (" + u.Temperature + ", " + u.Pressure + ", " + u.MassFlow + ")";
                units.temperature = u.Temperature;
                units.pressure = u.Pressure;
                units.massflow = u.MassFlow;
                units.heatflow = u.Energy;
                fs.AvailableSystemsOfUnits.Add(units);
                return units;
            }
            catch (Exception)
            {
                res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, "", "DWSIM does not know one of the drawing's units (" + u.Temperature + ", " + u.Pressure + ", " + u.MassFlow + ", " + u.Energy + "); the flowsheet keeps its own units"));
                return null;
            }
        }

        #endregion

        /// <summary>
        /// After a first solve: the shortcut columns whose reflux ratio (not on the drawing) is under
        /// the minimum they calculated get 1.3 times the minimum. True when one changed, so the
        /// flowsheet is solved again.
        /// </summary>
        public static bool SettleReflux(Result res)
        {
            var changed = false;
            foreach (var sc in res.Items.Values.OfType<ShortcutColumn>())
            {
                if (double.IsNaN(sc.m_Rmin) || sc.m_Rmin <= 0 || sc.m_refluxratio > 1.05 * sc.m_Rmin) continue;
                sc.m_refluxratio = Math.Round(1.3 * sc.m_Rmin, 3);
                res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, sc.GraphicObject.Tag, "reflux ratio set to 1.3 times the minimum, " + sc.m_refluxratio.ToString(CultureInfo.InvariantCulture)));
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// After a solve: how far the flowsheet's streams are from the drawing's table, the lines
        /// that differ by more than the tolerance (relative, or 1 degree for temperatures).
        /// </summary>
        public static List<ImportIssue> Compare(DrawingReading r, Result res, double tolerance = 0.02)
        {
            var list = new List<ImportIssue>();
            var u = r.Units;
            foreach (var kv in res.Streams)
            {
                var l = kv.Key; var ms = (IMaterialStream)kv.Value;
                if (!l.HasData || l.Feed) continue;
                var diffs = new List<string>();
                void Cmp(string what, string unit, double? table, double si, bool temperature)
                {
                    if (!table.HasValue || double.IsNaN(si)) return;
                    var v = Converter.ConvertFromSI(unit, si);
                    var off = temperature ? Math.Abs(v - table.Value) > 1.0 : Math.Abs(v - table.Value) > tolerance * Math.Max(Math.Abs(table.Value), 1e-6);
                    if (off) diffs.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:G4} {2} (table {3:G4})", what, v, unit, table.Value));
                }
                try
                {
                    Cmp("T", u.Temperature, l.T, ms.GetTemperature(), true);
                    Cmp("P", u.Pressure, l.P, ms.GetPressure(), false);
                    Cmp("flow", u.MassFlow, l.W, ms.GetMassFlow(), false);
                }
                catch (Exception) { }
                if (diffs.Count > 0) list.Add(new ImportIssue(ImportIssue.Levels.Warning, kv.Value.GraphicObject.Tag, string.Join(", ", diffs)));
            }
            return list;
        }
    }
}
