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
using DWSIM.UnitOperations.SpecialOps;
using DWSIM.UnitOperations.SpecialOps.Helpers;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.Pipe;

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

        /// <summary>
        /// Make a pipe segment of each line whose line number gives its size (a P&amp;ID): a straight
        /// run of <see cref="PipeLength"/> with the diameters of the size, for the user to give its
        /// true route (by hand or from the line's isometric).
        /// </summary>
        public bool PipeSegments;

        /// <summary>The length the pipe segments start with, in m.</summary>
        public double PipeLength = 10.0;
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

            /// <summary>The pipe segments made of the sized lines.</summary>
            public int Pipes;

            /// <summary>The controllers made of the reading's control loops.</summary>
            public List<ISimulationObject> Controllers = new List<ISimulationObject>();

            /// <summary>True when the reading updated the flowsheet's own items and streams, with nothing added.</summary>
            public bool Updated;
        }

        /// <summary>
        /// Adds a unit operation that does not have an object type of its own (the Relief Valve), by
        /// the name it has among the flowsheet's objects. Null when the flowsheet does not have it or
        /// cannot place it.
        /// </summary>
        static ISimulationObject AddExternal(IFlowsheet fs, string displayName, int x, int y, string tag)
        {
            try
            {
                if (fs.AvailableSimulationObjects == null || !fs.AvailableSimulationObjects.TryGetValue(displayName, out var template)) return null;
                if (!(template is IExternalUnitOperation external)) return null;
                var fresh = external.ReturnInstance(template.GetType().AssemblyQualifiedName) as IExternalUnitOperation;
                if (fresh == null) return null;
                // the cross-platform and automation flowsheets place objects themselves, the Classic one through its surface
                object host = fs;
                var method = fs.GetType().GetMethod("AddObjectToSurface");
                if (method == null)
                {
                    host = fs.GetType().GetField("FormSurface")?.GetValue(fs);
                    method = host?.GetType().GetMethod("AddObjectToSurface");
                }
                if (method == null) return null;
                var args = method.GetParameters().Select(p =>
                {
                    switch (p.Name.ToLowerInvariant())
                    {
                        case "type": return (object)ObjectType.External;
                        case "x": return x;
                        case "y": return y;
                        case "tag": return tag;
                        case "uoobj": return fresh;
                        default: return p.ParameterType == typeof(bool) ? false : p.ParameterType == typeof(string) ? "" : (p.HasDefaultValue ? p.DefaultValue : null);
                    }
                }).ToArray();
                var id = method.Invoke(host, args) as string;
                return id != null && fs.SimulationObjects.TryGetValue(id, out var so) ? so : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// The solved flowsheet against the design limits a P&amp;ID gives for its equipment: the
        /// pressure and the temperature of the streams on each item, against its design pressure and
        /// temperature. A warning for each one out of its limits.
        /// </summary>
        public static List<ImportIssue> DesignCheck(IFlowsheet fs, DrawingReading r, Result res)
        {
            var issues = new List<ImportIssue>();
            var limited = 0;
            foreach (var item in r.ItemsByTag().Values)
            {
                if (!res.Items.TryGetValue(item.Tag, out var obj)) continue;
                double? Limit(string key) => item.Specs.TryGetValue(key, out var v) && !double.IsNaN(v) ? v : (double?)null;
                double? pMax = Limit("design_pressure_max"), pMin = Limit("design_pressure_min"), tMax = Limit("design_temperature_max"), tMin = Limit("design_temperature_min");
                if (!pMax.HasValue && !pMin.HasValue && !tMax.HasValue && !tMin.HasValue) continue;
                limited++;
                var go = obj.GraphicObject;
                foreach (var con in go.InputConnectors.Concat(go.OutputConnectors))
                {
                    var graphic = con.IsAttached ? con.AttachedConnector?.AttachedFrom == go ? con.AttachedConnector?.AttachedTo : con.AttachedConnector?.AttachedFrom : null;
                    if (graphic == null || graphic.ObjectType != ObjectType.MaterialStream || !fs.SimulationObjects.TryGetValue(graphic.Name, out var stream) || !stream.Calculated) continue;
                    double t, p;
                    try
                    {
                        t = Convert.ToDouble(stream.GetPropertyValue("PROP_MS_0"), CultureInfo.InvariantCulture);
                        p = Convert.ToDouble(stream.GetPropertyValue("PROP_MS_1"), CultureInfo.InvariantCulture);
                    }
                    catch (Exception) { continue; }
                    string Barg(double pa) => ((pa - 101325) / 1e5).ToString("0.##", CultureInfo.InvariantCulture) + " bar g";
                    string Celsius(double k) => (k - 273.15).ToString("0.#", CultureInfo.InvariantCulture) + " C";
                    void Out(string what) => issues.Add(new ImportIssue(ImportIssue.Levels.Warning, item.Tag, graphic.Tag + " is at " + what));
                    if (pMax.HasValue && p > pMax.Value * 1.0001) Out(Barg(p) + ", above the design pressure of " + Barg(pMax.Value));
                    if (pMin.HasValue && p < pMin.Value * 0.9999) Out(Barg(p) + ", below the lowest design pressure of " + Barg(pMin.Value));
                    if (tMax.HasValue && t > tMax.Value + 0.01) Out(Celsius(t) + ", above the design temperature of " + Celsius(tMax.Value));
                    if (tMin.HasValue && t < tMin.Value - 0.01) Out(Celsius(t) + ", below the lowest design temperature of " + Celsius(tMin.Value));
                }
            }
            if (limited > 0)
                issues.Insert(0, new ImportIssue(ImportIssue.Levels.Info, "", limited + (limited == 1 ? " item" : " items") + " checked against the design limits: " +
                    (issues.Count == 0 ? "every stream on them is within the design pressure and temperature" : issues.Count + " values out of the limits, listed below")));
            return issues;
        }

        /// <summary>The size a line number starts with (DN80-..., 6"-P-1234, 1 1/2"-...), or null when it does not give one.</summary>
        public static string LineSize(string lineNumber)
        {
            var first = (lineNumber ?? "").Trim();
            var dn = System.Text.RegularExpressions.Regex.Match(first, @"^DN\s*\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (dn.Success) return dn.Value;
            var inches = System.Text.RegularExpressions.Regex.Match(first, @"^(\d+(?:\s+\d/\d|\.\d+)?|\d/\d)\s*(?:""|''|in\b)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return inches.Success ? inches.Groups[1].Value + "\"" : null;
        }

        /// <summary>
        /// A pipe segment on each line that has a size: the line's stream goes into it and a new
        /// stream takes it to where the line went. Each segment is one straight, level run of the
        /// given length with the diameters of the size (standard wall), named by the line number.
        /// </summary>
        static void AddPipes(IFlowsheet fs, DrawingReading r, List<ReadLine> lines, Result res, double length)
        {
            foreach (var l in lines.Where(res.Streams.ContainsKey).ToList())
            {
                var size = LineSize(l.LineNumber);
                if (size == null || !IsometricImport.Diameters(new IsoReading { Size = size }, out var de, out var di, out var how)) continue;
                var ms = res.Streams[l];
                var go = ms.GraphicObject;
                var con = go.OutputConnectors[0].AttachedConnector;
                var toGraphic = con?.AttachedTo;
                var toIndex = con?.AttachedToConnectorIndex ?? 0;
                try
                {
                    if (toGraphic != null) fs.DisconnectObjects(go, toGraphic);
                    var created = fs.AddObject(ObjectType.Pipe, (int)(go.X + 50), (int)go.Y, FreeTag(fs, "PIPE-" + l.LineNumber.Trim()));
                    var after = fs.AddObject(ObjectType.MaterialStream, (int)(go.X + 100), (int)go.Y, FreeTag(fs, go.Tag + "-P"));
                    if (!(created is Pipe pipe) || after == null) continue;
                    try { created.PropertyPackage = ms.PropertyPackage; after.PropertyPackage = ms.PropertyPackage; } catch (Exception) { }
                    fs.ConnectObjects(go, created.GraphicObject, 0, 0);
                    fs.ConnectObjects(created.GraphicObject, after.GraphicObject, 0, 0);
                    if (toGraphic != null)
                    {
                        fs.ConnectObjects(after.GraphicObject, toGraphic, 0, toIndex);
                        // a column knows its feeds by stream: the stream after the pipe takes the stage
                        if (fs.SimulationObjects.TryGetValue(toGraphic.Name, out var to) && to is Column column)
                        {
                            var info = column.MaterialStreams.Values.FirstOrDefault(s => s.StreamID == ms.Name);
                            if (info != null)
                            {
                                column.MaterialStreams.Remove(info.ID);
                                info.ID = after.Name;
                                info.StreamID = after.Name;
                                column.MaterialStreams[info.ID] = info;
                            }
                        }
                    }
                    var material = IsometricImport.Material("");
                    pipe.Profile.Sections.Clear();
                    pipe.Profile.Sections.Add(1, new PipeSection
                    {
                        Indice = 1, TipoSegmento = "Tubulaosimples", Quantidade = 1, Incrementos = Math.Max(5, Math.Min(50, (int)Math.Ceiling(length))),
                        Material = material, Comprimento = length, Elevacao = 0, DE = de, DI = di,
                    });
                    pipe.Profile.DefaultMaterial = material;
                    pipe.Profile.Status = PipeEditorStatus.OK;
                    AddEnergyStreams(fs, created, res, true);
                    res.Pipes++;
                }
                catch (Exception ex)
                {
                    res.Issues.Add(new ImportIssue(ImportIssue.Levels.Warning, l.LineNumber, "the pipe segment could not be made: " + ex.GetBaseException().Message));
                }
            }
            if (res.Pipes > 0)
                res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, "", res.Pipes + " pipe segments made of the sized lines, each a straight run of " +
                    length.ToString("G4", CultureInfo.InvariantCulture) + " m with the diameters of its size: give each one its route, by hand or with Import Pipe Segment from Piping Isometric"));
        }

        /// <summary>
        /// Brings a flowsheet up to date with a reading of the same plant: the items and the streams
        /// are found by their tags (a stream also by the two items it joins), the feeds take the
        /// reading's conditions, flows and compositions and the items its specifications. Nothing is
        /// added or connected; what the flowsheet lacks and what the reading lacks are listed.
        /// </summary>
        public static Result Update(IFlowsheet fs, DrawingReading r, ImportSetup setup)
        {
            var res = new Result { Updated = true };
            MassFlowsFromMoleFractions(fs, r);
            var items = r.ItemsByTag();
            foreach (var item in items.Values)
            {
                var obj = fs.GetFlowsheetSimulationObject(item.Tag);
                if (obj != null && obj.GraphicObject.ObjectType != ObjectType.MaterialStream && obj.GraphicObject.ObjectType != ObjectType.EnergyStream)
                    res.Items[item.Tag] = obj;
                else
                    res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, item.Tag, "not in the flowsheet; an update adds nothing"));
            }

            // a stream by its tag, else the one stream that joins the same two items
            ISimulationObject Joining(ReadLine l)
            {
                var from = l.Feed ? null : Get(res, l.From);
                var to = l.Product ? null : Get(res, l.To);
                if (from == null || to == null) return null;
                var found = new List<ISimulationObject>();
                foreach (var con in from.GraphicObject.OutputConnectors)
                {
                    var stream = con.IsAttached ? con.AttachedConnector?.AttachedTo : null;
                    if (stream == null || stream.ObjectType != ObjectType.MaterialStream) continue;
                    var next = stream.OutputConnectors.Count > 0 && stream.OutputConnectors[0].IsAttached ? stream.OutputConnectors[0].AttachedConnector?.AttachedTo : null;
                    if (next != null && next.Name == to.Name && fs.SimulationObjects.TryGetValue(stream.Name, out var so)) found.Add(so);
                }
                return found.Count == 1 ? found[0] : null;
            }
            var lines = r.Lines.Where(l => l.Include && (l.Feed || items.ContainsKey(l.From)) && (l.Product || items.ContainsKey(l.To)) && !(l.Feed && l.Product)).ToList();
            var taken = new HashSet<string>();
            foreach (var l in lines)
            {
                var stream = fs.GetFlowsheetSimulationObject(r.StreamTag(l));
                if (stream == null || stream.GraphicObject.ObjectType != ObjectType.MaterialStream) stream = Joining(l);
                if (stream != null && taken.Add(stream.Name)) res.Streams[l] = stream;
                else res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, r.StreamTag(l), "not found in the flowsheet by its tag or by the items it joins; left out of the update"));
            }
            var known = new HashSet<string>(res.Items.Values.Select(o => o.Name).Concat(res.Streams.Values.Select(o => o.Name)));
            var extra = fs.SimulationObjects.Values.Where(o => o.GraphicObject != null && !known.Contains(o.Name) &&
                o.GraphicObject.ObjectType != ObjectType.EnergyStream && o.GraphicObject.ObjectType != ObjectType.OT_Recycle &&
                o.GraphicObject.ObjectType != ObjectType.MaterialStream).Select(o => o.GraphicObject.Tag).ToList();
            if (extra.Count > 0)
                res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, "", "in the flowsheet and absent from the reading, left as they are: " + string.Join(", ", extra.Take(20)) + (extra.Count > 20 ? ", ..." : "")));

            if (setup.SpecsFromTable)
            {
                var matched = lines.Where(res.Streams.ContainsKey).ToList();
                foreach (var kv in res.Streams.Where(s => s.Key.Feed && s.Key.HasData)) SetStream(fs, kv.Value, kv.Key, r.Units, res, true);
                foreach (var item in items.Values)
                    if (res.Items.TryGetValue(item.Tag, out var obj)) Specify(fs, obj, item, matched, r.Units, res);
            }
            res.Issues.Insert(0, new ImportIssue(ImportIssue.Levels.Info, "", res.Items.Count + " of " + items.Count + " items and " + res.Streams.Count + " of " + lines.Count +
                " lines of the reading were found in the flowsheet" + (setup.SpecsFromTable ? "; the feeds and the items' specifications were set from it" : "")));
            return res;
        }

        /// <summary>
        /// The compounds' mass flows of the lines that give their composition in mole fractions and
        /// their mass flow, with the molar weights of the flowsheet's compounds.
        /// </summary>
        static void MassFlowsFromMoleFractions(IFlowsheet fs, DrawingReading r)
        {
            foreach (var l in r.Lines.Where(l => l.Compounds.Count == 0 && l.MoleFractions.Count > 0 && l.W.HasValue))
            {
                var mass = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in l.MoleFractions)
                {
                    var name = MatchSelected(fs, c.Key);
                    if (name != null && c.Value > 0) mass[c.Key] = c.Value * fs.SelectedCompounds[name].Molar_Weight;
                }
                var total = mass.Values.Sum();
                if (total <= 0) continue;
                foreach (var c in mass) l.Compounds[c.Key] = l.W.Value * c.Value / total;
            }
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
            MassFlowsFromMoleFractions(fs, r);
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
                var tag = FreeTag(fs, item.Tag);
                ISimulationObject obj;
                if (item.Type == "ReliefValve")
                {
                    // the relief valve does not have an object type of its own: it is added by its name, and where the
                    // flowsheet cannot do that the item becomes a plain valve
                    obj = AddExternal(fs, "Relief Valve", (int)Px(item, n), (int)Py(item, n), tag);
                    if (obj == null)
                    {
                        Note(ImportIssue.Levels.Info, item.Tag, "the Relief Valve could not be added here; a plain valve is used");
                        obj = fs.AddObject(ObjectType.Valve, (int)Px(item, n), (int)Py(item, n), tag);
                    }
                }
                else
                {
                    if (!Enum.TryParse(item.Type, out type)) { Note(ImportIssue.Levels.Error, item.Tag, "unknown object type " + item.Type); continue; }
                    obj = fs.AddObject(type, (int)Px(item, n), (int)Py(item, n), tag);
                }
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

                if (obj is Column column)
                {
                    ConnectColumn(fs, r, column, item, ins, outs, res, setup.SpecsFromTable);
                    continue;
                }
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
                if (to is Column tc)
                {
                    // the column knows its feeds by stream: the recycle's outlet takes the stage
                    var info = tc.MaterialStreams.Values.FirstOrDefault(s => s.StreamID == ms.Name);
                    if (info != null)
                    {
                        tc.MaterialStreams.Remove(info.ID);
                        info.ID = back.Name;
                        info.StreamID = back.Name;
                        tc.MaterialStreams[info.ID] = info;
                    }
                }
                if (setup.SpecsFromTable) SetStream(fs, back, l, r.Units, res, false);
                res.Recycles++;
            }

            if (setup.PipeSegments) AddPipes(fs, r, lines, res, setup.PipeLength);

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
            AddControllers(fs, r, res);
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
                (r.Kind == "pid" ? "P&ID" : "process flow diagram") + " " + Path.GetFileName(r.Source ?? "") +
                (DexpiImport.IsDexpi(r.Source) ? ", a DEXPI model read by " + DexpiImport.Program + "." : ", read by the AI Assistant."));
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
            // the metadata the flowsheet summary, the report and the evaluation read
            var m = o.Metadata;
            if (m != null)
            {
                var type = ProcessType(r, fs);
                if (newFlowsheet || m.ProcessType == DWSIM.Interfaces.Enums.ProcessType.Unspecified) m.ProcessType = type;
                if (r.Process.Description.Length > 0 && (newFlowsheet || string.IsNullOrWhiteSpace(m.ProcessDescription))) m.ProcessDescription = r.Process.Description;
                List<string> Matched(IEnumerable<string> names) => names.Select(n => MatchSelected(fs, n) ?? MatchSelected(fs, CompoundName(fs, n) ?? "")).Where(n => n != null).Distinct().ToList();
                if (newFlowsheet || m.KeyCompounds.Count == 0) { m.KeyCompounds.Clear(); m.KeyCompounds.AddRange(Matched(r.Process.Compounds.Count > 0 ? r.Process.Compounds : setup.Compounds)); }
                if (newFlowsheet || m.KeyReactants.Count == 0) { m.KeyReactants.Clear(); m.KeyReactants.AddRange(Matched(r.Process.KeyReactants)); }
                if (newFlowsheet || m.KeyProducts.Count == 0) { m.KeyProducts.Clear(); m.KeyProducts.AddRange(Matched(r.Process.KeyProducts)); }
                t.AppendLine("Process type: " + m.ProcessType);
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

        /// <summary>
        /// A rigorous column: its stages (the drawing's trays, plus the condenser and the reboiler
        /// of a distillation column), each feed on the stage its nozzle points to, the products at
        /// the top, at the bottom and drawn from the side, the duties, and from the table the top
        /// pressure, the pressure drop and the product flow the column works to.
        /// </summary>
        static void ConnectColumn(IFlowsheet fs, DrawingReading r, Column col, ReadItem item, List<ReadLine> ins, List<ReadLine> outs, Result res, bool fromTable)
        {
            var tag = item.Tag;
            var u = r.Units;
            void Note(ImportIssue.Levels level, string text) => res.Issues.Add(new ImportIssue(level, tag, text));
            var distillation = col is DistillationColumn;
            if (item.Stages.HasValue)
            {
                try
                {
                    col.SetNumberOfStages(Math.Max(4, item.Stages.Value + (distillation ? 2 : 0)));
                    Note(ImportIssue.Levels.Info, item.Stages.Value + " trays on the drawing: " + col.NumberOfStages + " equilibrium stages" +
                        (distillation ? " with the condenser and the reboiler" : "") + "; set the stage efficiency for real trays");
                }
                catch (Exception ex) { Note(ImportIssue.Levels.Warning, "the number of stages could not be set: " + ex.GetBaseException().Message); }
            }
            else Note(ImportIssue.Levels.Warning, "the drawing does not give the number of trays; " + col.NumberOfStages + " stages are used");

            int first = distillation ? 1 : 0, last = col.Stages.Count - (distillation ? 2 : 1);
            var guessed = new List<string>();
            int Stage(ReadLine l, bool inlet)
            {
                var given = inlet ? l.ToStage : l.FromStage;
                if (given.HasValue) return Math.Max(first, Math.Min(last, first + given.Value - 1));
                var name = r.StreamTag(l);
                var tray = System.Text.RegularExpressions.Regex.Match(name, @"(?:tray|prato)\s*#?\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (tray.Success) return Math.Max(first, Math.Min(last, first + int.Parse(tray.Groups[1].Value) - 1));
                var end = r.ColumnEnd(l, inlet);
                if (end < 0) return first;
                if (end > 0) return last;
                var level = inlet ? l.ToLevel : l.FromLevel;
                if (level.HasValue && level.Value >= 0 && level.Value <= 1) return first + (int)Math.Round(level.Value * (last - first));
                if (inlet)
                {
                    // a return from the equipment a product goes to: the reboiler's vapor at the
                    // bottom, the condenser's liquid at the top
                    var tags = System.Text.RegularExpressions.Regex.Matches(name, @"\b[A-Z]{1,4}-?\d{3,5}[A-Z]?\b").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToList();
                    bool Shares(ReadLine o) => tags.Any(t => r.StreamTag(o).IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
                    var products = outs.Where(Shares).Select(o => r.ColumnEnd(o, false)).ToList();
                    if (products.Contains(1)) return last;
                    if (products.Contains(-1)) return first;
                }
                guessed.Add(name);
                return (first + last) / 2;
            }
            bool Vapor(ReadLine l)
            {
                if (l.VaporFraction.HasValue) return l.VaporFraction.Value > 0.5;
                var words = (r.StreamTag(l) + " " + l.FromPort).ToLowerInvariant();
                return words.Contains("vap") || words.Contains("gas") || words.Contains("overhead");
            }
            void Try(Action connect, ReadLine l)
            {
                try { connect(); }
                catch (Exception ex) { Note(ImportIssue.Levels.Warning, r.StreamTag(l) + " could not be connected: " + ex.GetBaseException().Message); }
            }

            foreach (var l in ins)
                Try(() => { if (col is DistillationColumn dcf) dcf.ConnectFeed(res.Streams[l], Stage(l, true)); else ((AbsorptionColumn)col).ConnectFeed(res.Streams[l], Stage(l, true)); }, l);

            // the products: by the nozzle or the name, else the lightest at the top and the heaviest at the bottom
            var top = outs.Where(l => r.ColumnEnd(l, false) < 0).ToList();
            var bottom = outs.Where(l => r.ColumnEnd(l, false) > 0).ToList();
            var side = outs.Except(top).Except(bottom).ToList();
            double Light(ReadLine l) => (l.VaporFraction ?? 0.5) * 1000 - (l.T ?? 0);
            if (top.Count == 0 && side.Count > 0) { var t = side.OrderByDescending(Light).First(); top.Add(t); side.Remove(t); }
            if (bottom.Count == 0 && side.Count > 0) { var b = side.OrderBy(Light).First(); bottom.Add(b); side.Remove(b); }

            ReadLine vapor = null;
            if (col is DistillationColumn dc)
            {
                vapor = top.FirstOrDefault(Vapor);
                var distillate = top.FirstOrDefault(l => l != vapor);
                if (vapor != null) Try(() => dc.ConnectVaporProduct(res.Streams[vapor]), vapor);
                if (distillate != null) Try(() => dc.ConnectDistillate(res.Streams[distillate]), distillate);
                dc.CondenserType = vapor == null ? Column.condtype.Total_Condenser : distillate == null ? Column.condtype.Full_Reflux : Column.condtype.Partial_Condenser;
                side.AddRange(top.Where(l => l != vapor && l != distillate));

                var go = col.GraphicObject;
                var ec = fs.AddObject(ObjectType.EnergyStream, (int)(go.X + go.Width + 30), (int)(go.Y - 40), FreeTag(fs, "E-" + go.Tag + "-C"));
                var er = fs.AddObject(ObjectType.EnergyStream, (int)(go.X + go.Width + 30), (int)(go.Y + go.Height + 30), FreeTag(fs, "E-" + go.Tag + "-R"));
                try { dc.ConnectCondenserDuty(ec); dc.ConnectReboilerDuty(er); }
                catch (Exception ex) { Note(ImportIssue.Levels.Warning, "the duties could not be connected: " + ex.GetBaseException().Message); }
            }
            else if (col is AbsorptionColumn ac && top.Count > 0)
            {
                vapor = top[0];
                Try(() => ac.ConnectTopProduct(res.Streams[vapor]), vapor);
                side.AddRange(top.Skip(1));
            }
            if (bottom.Count > 0)
            {
                var bs = res.Streams[bottom[0]];
                Try(() => { if (col is DistillationColumn dcb) dcb.ConnectBottoms(bs); else ((AbsorptionColumn)col).ConnectBottoms(bs); }, bottom[0]);
                side.AddRange(bottom.Skip(1));
            }

            foreach (var l in side)
            {
                // the draw's rate, in moles, from the table's flow and composition
                var w = fromTable ? SI(u.MassFlow, l.W, res, tag) : null;
                double moles = 0, mass = 0;
                foreach (var c in l.Compounds)
                {
                    var name = MatchSelected(fs, c.Key);
                    if (name == null || c.Value <= 0) continue;
                    mass += c.Value;
                    moles += c.Value / fs.SelectedCompounds[name].Molar_Weight;
                }
                var rate = w.HasValue && moles > 0 ? w.Value * 1000 * moles / mass : 0.0;
                if (rate <= 0) Note(ImportIssue.Levels.Warning, r.StreamTag(l) + " is a side draw without a flow on the drawing; set its rate in the column's editor");
                Try(() => col.ConnectSideDraw(res.Streams[l], Stage(l, false), Vapor(l), rate), l);
            }
            if (guessed.Count > 0)
                Note(ImportIssue.Levels.Info, string.Join(", ", guessed.Distinct()) + ": the drawing does not tell where on the column; put on the middle stage, move in the column's editor");

            if (!fromTable) return;
            var pt = top.Concat(ins.Where(l => r.ColumnEnd(l, true) < 0)).Select(l => SI(u.Pressure, l.P, res, tag)).FirstOrDefault(x => x.HasValue);
            var pb = bottom.Concat(ins.Where(l => r.ColumnEnd(l, true) > 0)).Select(l => SI(u.Pressure, l.P, res, tag)).FirstOrDefault(x => x.HasValue);
            if (pt.HasValue)
            {
                col.SetTopPressure(pt.Value);
                col.ColumnPressureDrop = pb.HasValue ? Math.Max(0, pb.Value - pt.Value) : 0.0;
            }
            else Note(ImportIssue.Levels.Warning, "the table does not give the top pressure; set it before solving");

            if (col is DistillationColumn d)
            {
                var wv = vapor == null ? null : SI(u.MassFlow, vapor.W, res, tag);
                var wb = bottom.Count == 0 ? null : SI(u.MassFlow, bottom[0].W, res, tag);
                var tb = bottom.Count == 0 ? null : SI(u.Temperature, bottom[0].T, res, tag);
                if (d.CondenserType == Column.condtype.Full_Reflux && wv.HasValue)
                {
                    d.SetCondenserSpec("Product_Mass_Flow_Rate", wv.Value, "kg/s");
                    if (tb.HasValue) d.SetReboilerSpec("Temperature", tb.Value, "K");
                    Note(ImportIssue.Levels.Info, "the condenser works to the overhead flow" + (tb.HasValue ? " and the reboiler to the bottoms temperature" : "") + " of the table");
                }
                else if (wb.HasValue)
                {
                    d.SetReboilerSpec("Product_Mass_Flow_Rate", wb.Value, "kg/s");
                    Note(ImportIssue.Levels.Info, "the reboiler works to the bottoms flow of the table" +
                        (item.Specs.ContainsKey("reflux_ratio") ? "" : "; the reflux ratio is not on the drawing, the condenser keeps its default"));
                }
                else Note(ImportIssue.Levels.Warning, "the table gives no product flows; set the condenser and reboiler specifications before solving");
                // the reflux ratio a model gives for the column itself
                if (d.CondenserType != Column.condtype.Full_Reflux && item.Specs.TryGetValue("reflux_ratio", out var modelReflux) && modelReflux > 0)
                {
                    d.SetCondenserSpec("Stream_Ratio", modelReflux, "");
                    Note(ImportIssue.Levels.Info, "from the model: reflux ratio " + modelReflux.ToString("G4", CultureInfo.InvariantCulture));
                }
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
            var issuesBefore = res.Issues.Count;
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
            ApplySpecs(obj, item, pIn, tOut.HasValue, pOut.HasValue, issuesBefore, res);
        }

        /// <summary>
        /// What the model gives for the item itself (a DEXPI process step's duty, efficiency,
        /// pressure ratio, reflux ratio, area, volume, temperature and pressure), where the stream
        /// table left the item's specification open. The warning about that goes with it.
        /// </summary>
        static void ApplySpecs(ISimulationObject obj, ReadItem item, double? pIn, bool tOut, bool pOut, int issuesBefore, Result res)
        {
            if (item.Specs.Count == 0) return;
            double? V(string key) => item.Specs.TryGetValue(key, out var v) && !double.IsNaN(v) && !double.IsInfinity(v) ? v : (double?)null;
            bool Open() => res.Issues.Skip(issuesBefore).Any(i => i.Text.StartsWith("the table does not give", StringComparison.Ordinal));
            var used = new List<string>();
            var settled = false;
            var duty = V("duty");
            var efficiency = V("efficiency");
            var polytropic = V("polytropic_efficiency");
            string Kw(double w) => (Math.Abs(w) / 1000).ToString("G4", CultureInfo.InvariantCulture) + " kW";
            string Percent(double f) => (100 * f).ToString("G3", CultureInfo.InvariantCulture) + " %";

            switch (obj)
            {
                case Heater h:
                    if (!tOut && duty.HasValue) { h.CalcMode = Heater.CalculationMode.HeatAdded; h.DeltaQ = Math.Abs(duty.Value) / 1000; settled = true; used.Add("duty " + Kw(duty.Value)); }
                    break;
                case Cooler c:
                    if (!tOut && duty.HasValue) { c.CalcMode = Cooler.CalculationMode.HeatRemoved; c.DeltaQ = Math.Abs(duty.Value) / 1000; settled = true; used.Add("duty " + Kw(duty.Value)); }
                    break;
                case Pump pu:
                    if (efficiency.HasValue && efficiency.Value > 0 && efficiency.Value <= 1) { pu.Eficiencia = 100 * efficiency.Value; used.Add("efficiency " + Percent(efficiency.Value)); }
                    if (!pOut && V("head") is double head && head > 0)
                    {
                        // the pressure rise of the head on water: the fluid's density is not known before the flowsheet is solved
                        pu.CalcMode = Pump.CalculationMode.Delta_P;
                        pu.DeltaP = 1000 * 9.80665 * head;
                        settled = true;
                        used.Add("pressure rise from the head of " + head.ToString("G4", CultureInfo.InvariantCulture) + " m at 1000 kg/m3 (correct it for the fluid's density)");
                    }
                    break;
                case ReliefValve relief:
                    if (V("set_pressure") is double setPressure && setPressure > 0)
                    {
                        // fully open at 10 % over the set pressure, on the gauge
                        relief.SetPointPressure = setPressure;
                        relief.FullyOpenedPressure = 101325 + 1.1 * (setPressure - 101325);
                        used.Add("set pressure " + ((setPressure - 101325) / 1e5).ToString("G4", CultureInfo.InvariantCulture) + " bar g");
                    }
                    break;
                case Tank tank:
                    if (V("volume") is double vt && vt > 0) { tank.Volume = vt; used.Add("volume " + vt.ToString("G4", CultureInfo.InvariantCulture) + " m3"); }
                    break;
                case Compressor co:
                    if (polytropic.HasValue && polytropic.Value > 0 && polytropic.Value <= 1)
                    { co.ProcessPath = Compressor.ProcessPathType.Polytropic; co.PolytropicEfficiency = 100 * polytropic.Value; used.Add("polytropic efficiency " + Percent(polytropic.Value)); }
                    else if (efficiency.HasValue && efficiency.Value > 0 && efficiency.Value <= 1) { co.AdiabaticEfficiency = 100 * efficiency.Value; used.Add("efficiency " + Percent(efficiency.Value)); }
                    if (!pOut && pIn.HasValue && V("pressure_ratio") is double ratio && ratio > 1)
                    { co.CalcMode = Compressor.CalculationMode.OutletPressure; co.POut = ratio * pIn.Value; settled = true; used.Add("pressure ratio " + ratio.ToString("G4", CultureInfo.InvariantCulture)); }
                    break;
                case Expander ex:
                    if (efficiency.HasValue && efficiency.Value > 0 && efficiency.Value <= 1) { ex.AdiabaticEfficiency = 100 * efficiency.Value; used.Add("efficiency " + Percent(efficiency.Value)); }
                    break;
                case HeatExchanger hx:
                    if (!Open()) break;
                    if (duty.HasValue) { hx.CalculationMode = HeatExchangerCalcMode.CalcBothTemp; hx.Q = Math.Abs(duty.Value) / 1000; settled = true; used.Add("duty " + Kw(duty.Value)); }
                    else if (V("area") is double area && V("u") is double coefficient && area > 0 && coefficient > 0)
                    { hx.CalculationMode = HeatExchangerCalcMode.CalcBothTemp_UA; hx.Area = area; hx.OverallCoefficient = coefficient; settled = true; used.Add("area and overall coefficient"); }
                    break;
                case Vessel ve:
                    if (!ve.OverrideT && V("temperature") is double t) { ve.OverrideT = true; ve.FlashTemperature = t; used.Add("temperature"); }
                    if (!ve.OverrideP && V("pressure") is double p) { ve.OverrideP = true; ve.FlashPressure = p; used.Add("pressure"); }
                    break;
                case ShortcutColumn sc:
                    if (V("reflux_ratio") is double reflux && reflux > 0)
                    {
                        sc.m_refluxratio = reflux;
                        used.Add("reflux ratio " + reflux.ToString("G4", CultureInfo.InvariantCulture));
                        // the note on the keys said the drawing did not give it
                        foreach (var note in res.Issues.Skip(issuesBefore).Where(i => i.Subject == item.Tag))
                        {
                            var at = note.Text.IndexOf("; the reflux ratio is not on the drawing", StringComparison.Ordinal);
                            if (at > 0) note.Text = note.Text.Substring(0, at);
                        }
                    }
                    break;
                case Reactor_CSTR cstr:
                    if (V("volume") is double vc && vc > 0) { cstr.Volume = vc; used.Add("volume"); }
                    break;
                case Reactor_PFR pfr:
                    if (V("volume") is double vp && vp > 0) { pfr.Volume = vp; used.Add("volume"); }
                    break;
            }
            if (used.Count == 0) return;
            if (used.Any(u => u.Contains("efficiency")))
                res.Issues.RemoveAll(i => i.Subject == item.Tag && i.Text.StartsWith("the drawing gives no efficiency", StringComparison.Ordinal));
            if (settled)
                for (var i = res.Issues.Count - 1; i >= issuesBefore; i--)
                    if (res.Issues[i].Text.StartsWith("the table does not give", StringComparison.Ordinal)) res.Issues.RemoveAt(i);
            res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, item.Tag, "from the model: " + string.Join(", ", used)));
        }

        /// <summary>
        /// The control loops of the reading as PID controllers: each one measures a stream's
        /// temperature, pressure or mass flow, or a vessel's level, and moves its valve's opening.
        /// They are left inactive: the setpoint and the tuning are not in a drawing.
        /// </summary>
        static void AddControllers(IFlowsheet fs, DrawingReading r, Result res)
        {
            foreach (var c in r.Controllers)
            {
                var name = c.Tag.Length > 0 ? c.Tag : c.Variable + "C-" + c.Valve;
                void Skip(string why) => res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, name, "control loop not built: " + why));
                var valve = Get(res, c.Valve);
                if (valve == null || valve.GraphicObject.ObjectType != ObjectType.Valve) { Skip("its valve " + c.Valve + " is not in the flowsheet"); continue; }

                ISimulationObject measured = null;
                string property = null;
                var item = c.MeasuredItem.Length > 0 ? Get(res, c.MeasuredItem) : null;
                var line = c.MeasuredLine.HasValue && c.MeasuredLine.Value >= 0 && c.MeasuredLine.Value < r.Lines.Count ? r.Lines[c.MeasuredLine.Value] : null;
                if (c.Variable == "L" && item != null && (item.GraphicObject.ObjectType == ObjectType.Vessel || item.GraphicObject.ObjectType == ObjectType.Tank))
                { measured = item; property = "Liquid Level"; }
                else if (c.Variable == "T" || c.Variable == "P" || c.Variable == "F")
                {
                    // an item's temperature, pressure or flow is read on the first line out of it
                    if (line == null && item != null)
                        line = r.Lines.FirstOrDefault(l => res.Streams.ContainsKey(l) && string.Equals(l.From, c.MeasuredItem, StringComparison.OrdinalIgnoreCase));
                    if (line != null && res.Streams.TryGetValue(line, out var stream))
                    { measured = stream; property = c.Variable == "T" ? "PROP_MS_0" : c.Variable == "P" ? "PROP_MS_1" : "PROP_MS_2"; }
                }
                if (measured == null) { Skip("what it measures (" + c.Variable + ") was not found in the flowsheet"); continue; }

                var created = fs.AddObject(ObjectType.Controller_PID, (int)valve.GraphicObject.X + 40, (int)valve.GraphicObject.Y - 70, FreeTag(fs, name));
                if (!(created is PIDController pid)) { Skip("the flowsheet did not make a controller"); continue; }
                pid.ControlledObjectData = Refer(fs, measured, property);
                pid.ManipulatedObjectData = Refer(fs, valve, "PROP_VA_5");
                pid.ManipulatedVariableSpan = 100.0;
                pid.OutputMin = 0.0;
                pid.OutputMax = 100.0;
                pid.Active = false;
                res.Controllers.Add(created);
                res.Issues.Add(new ImportIssue(ImportIssue.Levels.Info, name, "control loop built inactive: it reads " + measured.GraphicObject.Tag +
                    " and moves " + valve.GraphicObject.Tag + "; set its setpoint and tuning before a dynamic run"));
            }
        }

        /// <summary>
        /// Once the flowsheet is solved, gives each controller made of a control loop the solved
        /// value of what it measures as its setpoint, and the opening its valve has as the output it
        /// starts from. The controllers stay inactive.
        /// </summary>
        public static List<ImportIssue> SettleControllers(IFlowsheet fs, Result res)
        {
            var issues = new List<ImportIssue>();
            foreach (var created in res.Controllers)
            {
                if (!(created is PIDController pid) || pid.ControlledObjectData == null) continue;
                try
                {
                    if (!fs.SimulationObjects.TryGetValue(pid.ControlledObjectData.ID, out var measured) || !measured.Calculated) continue;
                    var si = Convert.ToDouble(measured.GetPropertyValue(pid.ControlledObjectData.PropertyName), CultureInfo.InvariantCulture);
                    if (double.IsNaN(si) || double.IsInfinity(si)) continue;
                    var value = string.IsNullOrEmpty(pid.ControlledObjectData.Units) ? si : DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(pid.ControlledObjectData.Units, si);
                    // a steady state does not hold liquid in a vessel: a level loop keeps the setpoint it has
                    var level = pid.ControlledObjectData.PropertyName == "Liquid Level";
                    if (!level) pid.SetPoint = value;
                    if (fs.SimulationObjects.TryGetValue(pid.ManipulatedObjectData?.ID ?? "", out var valve))
                    {
                        var opening = Convert.ToDouble(valve.GetPropertyValue("PROP_VA_5"), CultureInfo.InvariantCulture);
                        if (!double.IsNaN(opening) && opening >= 0 && opening <= 100) pid.Offset = opening;
                    }
                    if (!level) issues.Add(new ImportIssue(ImportIssue.Levels.Info, created.GraphicObject.Tag, "setpoint at the solved value, " +
                        value.ToString("G5", CultureInfo.InvariantCulture) + " " + pid.ControlledObjectData.Units));
                }
                catch (Exception) { }
            }
            return issues;
        }

        static SpecialOpObjectInfo Refer(IFlowsheet fs, ISimulationObject obj, string property)
        {
            var su = fs.FlowsheetOptions.SelectedUnitSystem;
            var units = "";
            var unitsType = DWSIM.Interfaces.Enums.UnitOfMeasure.none;
            try
            {
                if (obj.IsDynamicProperty(property)) { unitsType = obj.GetDynamicPropertyUnitType(property); units = su.GetCurrentUnits(unitsType); }
                else units = obj.GetPropertyUnit(property, su);
            }
            catch (Exception) { }
            var type = obj.GetType().Name;
            try { type = obj.GetDisplayName(); } catch (Exception) { }
            return new SpecialOpObjectInfo { ID = obj.Name, Name = obj.GraphicObject.Tag, PropertyName = property, ObjectType = type, Units = units ?? "", UnitsType = unitsType };
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

        /// <summary>
        /// The DWSIM process type: the model's choice when it made one, else from what the drawing
        /// holds (a reactor transforms, a refrigeration loop cycles, amines separate chemically,
        /// pipes, pumps, compressors and valves alone transport, the rest separates physically).
        /// </summary>
        static DWSIM.Interfaces.Enums.ProcessType ProcessType(DrawingReading r, IFlowsheet fs)
        {
            if (Enum.TryParse(r.Process.Type ?? "", true, out DWSIM.Interfaces.Enums.ProcessType chosen) && chosen != DWSIM.Interfaces.Enums.ProcessType.Unspecified) return chosen;
            var cats = r.Items.Where(i => i.Include).Select(i => i.Category).ToList();
            if (cats.Any(c => c == "Reactor" || c == "Cstr" || c == "Pfr")) return DWSIM.Interfaces.Enums.ProcessType.Transformation;
            var cls = (r.Process.Class ?? "").ToLowerInvariant();
            if (cls == "refrigeration" || cls == "absorption cooling") return DWSIM.Interfaces.Enums.ProcessType.RefrigerationCycle;
            if (cls == "amines" || cls == "electrolytes") return DWSIM.Interfaces.Enums.ProcessType.ChemicalSeparation;
            if (cats.Count > 0 && cats.All(c => c == "Pipe" || c == "Pump" || c == "Compressor" || c == "Valve" || c == "Orifice" || c == "Mixer" || c == "Splitter"))
                return DWSIM.Interfaces.Enums.ProcessType.Transportation;
            if (cats.Any(c => c == "Vessel" || c == "Column" || c == "Separator" || c == "Filter" || c == "Tank")) return DWSIM.Interfaces.Enums.ProcessType.PhysicalSeparation;
            return DWSIM.Interfaces.Enums.ProcessType.Unspecified;
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
