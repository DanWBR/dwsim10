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
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>An item of the P&amp;ID: a unit operation with the symbol the drawing gives it.</summary>
    internal sealed class PidItem
    {
        public ISimulationObject Obj;
        public IGraphicObject Go;

        /// <summary>The name of the drawing's symbol (VesselV, Pump, Valve, Node, PassThrough...).</summary>
        public string Kind = "Generic";
    }

    /// <summary>A line of the P&amp;ID: a material stream with its line number, its size and the nozzles it joins.</summary>
    internal sealed class PidLine
    {
        public string Tag = "", Id = "", Number = "", Size = "", InsulationService = "";
        public IGraphicObject Icon, StartGo, EndGo;
        public IConnectionPoint StartCp, EndCp;
        public IMaterialStream Stream;

        /// <summary>The marks of the nozzles at the ends, as on the items' drawings; null where the drawing has none.</summary>
        public string StartMark, EndMark;

        /// <summary>Where the line leaves and enters its items, on the flowsheet's canvas.</summary>
        public double StartX, StartY, EndX, EndY;
    }

    /// <summary>A controller or a gauge of the P&amp;ID, with its loop number and what it measures and moves.</summary>
    internal sealed class PidLoop
    {
        public ISimulationObject Obj;
        public string Letter = "", Loop = "", MeasuredId = "", MovedId = "";
        public bool Controller;
    }

    /// <summary>What the P&amp;ID drawing shows, for a writer of another format to say the same.</summary>
    internal sealed class PidContent
    {
        public List<PidItem> Items = new List<PidItem>();
        public List<PidLine> Lines = new List<PidLine>();
        public List<PidLoop> Loops = new List<PidLoop>();
    }

    public static partial class ProcessFlowDiagram
    {
        /// <summary>
        /// The content of the P&amp;ID as the drawing has it: the items with their symbols, the
        /// lines with their numbers, sizes and nozzle marks, and the instrument loops.
        /// </summary>
        internal static PidContent PidData(IFlowsheet fs)
        {
            var content = new PidContent();
            var model = PidModel(fs);
            var lineData = model == null ? new Dictionary<string, PipeRun>() : model.Pipes.GroupBy(r => r.Tag).ToDictionary(g => g.Key, g => g.First());
            var units = Units(fs);
            var unitOf = new Dictionary<string, Unit>();
            foreach (var u in units)
            {
                unitOf[u.Obj.Name] = u;
                content.Items.Add(new PidItem { Obj = u.Obj, Go = u.Go, Kind = u.Symbol.ToString() });
            }

            foreach (var l in Lines(fs).Where(l => !l.Energy))
            {
                lineData.TryGetValue(l.Tag ?? "", out var run);
                var line = new PidLine
                {
                    Tag = l.Tag ?? "", Id = l.Id ?? "", Number = LineNumber(l, lineData), Size = run?.Size ?? "", InsulationService = run?.InsulationService ?? "",
                    Icon = l.Icon, Stream = l.Stream, StartGo = l.StartGo, EndGo = l.EndGo, StartCp = l.StartCp, EndCp = l.EndCp,
                };
                foreach (var end in new[] { l.StartGo, l.EndGo })
                {
                    // an object the drawing does not have a symbol for still ends its lines
                    if (end == null || unitOf.ContainsKey(end.Name) || !fs.SimulationObjects.TryGetValue(end.Name, out var obj)) continue;
                    var unit = new Unit { Obj = obj, Go = end, Symbol = Symbol.Generic };
                    unitOf[end.Name] = unit;
                    content.Items.Add(new PidItem { Obj = obj, Go = end, Kind = "Generic" });
                }
                if (l.StartCp != null && l.StartGo != null)
                {
                    var p = Where(l.StartCp, l.StartGo);
                    line.StartX = p.X; line.StartY = p.Y;
                    if (unitOf.TryGetValue(l.StartGo.Name, out var u)) line.StartMark = NozzleMark(model, u, l.StartCp);
                }
                if (l.EndCp != null && l.EndGo != null)
                {
                    var p = Where(l.EndCp, l.EndGo);
                    line.EndX = p.X; line.EndY = p.Y;
                    if (unitOf.TryGetValue(l.EndGo.Name, out var u)) line.EndMark = NozzleMark(model, u, l.EndCp);
                }
                content.Lines.Add(line);
            }

            foreach (var x in InstrumentLoops(fs))
            {
                var loop = new PidLoop { Obj = x.Obj, Letter = x.Letter, Loop = x.Loop, Controller = x.Controller };
                if (x.Obj is IAdjust c)
                {
                    loop.MeasuredId = c.ControlledObjectData?.ID ?? "";
                    loop.MovedId = c.ManipulatedObjectData?.ID ?? "";
                }
                else if (x.Obj is IIndicator g) loop.MeasuredId = g.SelectedObjectID ?? "";
                content.Loops.Add(loop);
            }
            return content;
        }
    }

    /// <summary>
    /// Writes the flowsheet's P&amp;ID as a DEXPI 2.0.1 plant model (XML), with what the P&amp;ID
    /// drawing shows: the equipment with its nozzles, the lines with their numbers and sizes, the
    /// valves and the other pieces in the lines, the feeds and products as off-page connectors, the
    /// instrument loops with their transmitters, controllers and control valves, and a diagram
    /// that keeps the flowsheet's layout.
    /// </summary>
    public static class DexpiPlantExport
    {
        /// <summary>What was written.</summary>
        public class Result
        {
            public string File = "";
            public int Equipment, Valves, Fittings, Lines, Nozzles, Connectors, Instruments, ControlLoops;

            /// <summary>What the model leaves out or could not tell.</summary>
            public List<string> Notes = new List<string>();

            public override string ToString() => Equipment + " equipment items, " + Valves + " valves, " + Lines + " lines, " + Instruments +
                " instruments (" + ControlLoops + " control loops) written to " + File;
        }

        /// <summary>Diagram units for each unit of the flowsheet's canvas.</summary>
        const double Scale = 0.25;

        /// <summary>Writes the flowsheet's P&amp;ID as a DEXPI 2.0.1 plant model into <paramref name="file"/>.</summary>
        public static Result ExportPlantModel(IFlowsheet fs, string file)
        {
            if (fs == null) throw new ArgumentNullException(nameof(fs));
            if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("No file to write the DEXPI model to.", nameof(file));
            var w = new Writer(fs);
            var model = w.Build();
            w.Result.File = Path.GetFullPath(file);
            var folder = Path.GetDirectoryName(w.Result.File);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true };
            using (var xw = XmlWriter.Create(w.Result.File, settings)) model.Save(xw);
            return w.Result;
        }

        #region DEXPI XML

        /// <summary>
        /// An object of the DEXPI XML: its components, references and data, written in that order
        /// and by property name, the way the DEXPI tools write them.
        /// </summary>
        sealed class Obj
        {
            public readonly string Id, Type;
            readonly SortedDictionary<string, List<Obj>> components = new SortedDictionary<string, List<Obj>>(StringComparer.Ordinal);
            readonly SortedDictionary<string, List<string>> references = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            readonly SortedDictionary<string, object[]> data = new SortedDictionary<string, object[]>(StringComparer.Ordinal);

            public Obj(string id, string type) { Id = id; Type = type; }

            public Obj Comp(string property, params Obj[] objects)
            {
                if (objects.Length == 0) return this;
                if (!components.TryGetValue(property, out var list)) components[property] = list = new List<Obj>();
                list.AddRange(objects);
                return this;
            }

            public Obj Ref(string property, params Obj[] objects)
            {
                if (objects.Length == 0) return this;
                if (!references.TryGetValue(property, out var list)) references[property] = list = new List<string>();
                list.AddRange(objects.Select(o => o.Id));
                return this;
            }

            public Obj Data(string property, params object[] values)
            {
                if (values.Length > 0) data[property] = values;
                return this;
            }

            public XElement Xml() => new XElement("Object", new XAttribute("id", Id), new XAttribute("type", Type),
                components.Select(c => new XElement("Components", new XAttribute("property", c.Key), c.Value.Select(o => o.Xml()))),
                references.Select(r => new XElement("References", new XAttribute("objects", string.Join(" ", r.Value.Select(id => "#" + id))), new XAttribute("property", r.Key))),
                data.Select(d => new XElement("Data", new XAttribute("property", d.Key), d.Value)));
        }

        static XElement Str(string s) => new XElement("String", s ?? "");

        static XElement Int(int i) => new XElement("Integer", i.ToString(CultureInfo.InvariantCulture));

        static XElement Dbl(double v)
        {
            var s = v.ToString("R", CultureInfo.InvariantCulture);
            return new XElement("Double", s.IndexOfAny(new[] { '.', 'E', 'e' }) < 0 ? s + ".0" : s);
        }

        static XElement Enum(string path) => new XElement("DataReference", new XAttribute("data", path));

        static XElement Agg(string type, params (string Property, object[] Values)[] data) => new XElement("AggregatedDataValue", new XAttribute("type", type),
            data.OrderBy(d => d.Property, StringComparer.Ordinal).Select(d => new XElement("Data", new XAttribute("property", d.Property), d.Values)));

        static XElement Text(string s) => Agg("Core/DataTypes.MultiLanguageString", ("SingleLanguageStrings", new object[]
        {
            Agg("Core/DataTypes.SingleLanguageString", ("Language", new object[] { Str("en") }), ("Value", new object[] { Str(s) }))
        }));

        static XElement Quantity(string unit, double v) => Agg("Core/PhysicalQuantities.PhysicalQuantity",
            ("Unit", new object[] { Enum("Core/PhysicalQuantities." + unit) }), ("Value", new object[] { Dbl(v) }));

        static XElement Point(double x, double y) => Agg("Core/Diagram.Point", ("X", new object[] { Dbl(Math.Round(x, 3)) }), ("Y", new object[] { Dbl(Math.Round(y, 3)) }));

        static XElement Color(int r, int g, int b) => Agg("Core/Diagram.Color", ("R", new object[] { Int(r) }), ("G", new object[] { Int(g) }), ("B", new object[] { Int(b) }));

        static XElement Stroke(double width, bool dashed = false, int r = 0, int g = 0, int b = 0) => Agg("Core/Diagram.Stroke", ("Color", new object[] { Color(r, g, b) }),
            ("DashStyle", new object[] { Enum("Core/Diagram.DashStyle." + (dashed ? "Dash" : "Solid")) }), ("Width", new object[] { Dbl(width) }));

        #endregion

        /// <summary>An item being written: an equipment item with nozzles, or a piece in the lines with piping nodes.</summary>
        sealed class Item
        {
            public PidItem Source;
            public Obj Object;

            /// <summary>True for the pieces that sit in the lines (valves, tees, couplings); false for the equipment.</summary>
            public bool InLine;
            public string Class = "";
            public double X, Y, Width, Height;
            public int Marks;

            /// <summary>The nodes of a piece in the lines, the ones the flow comes in by and the ones it leaves by.</summary>
            public List<End> Ins = new List<End>(), Outs = new List<End>();

            /// <summary>The nozzles of an equipment item, in the order the lines found them.</summary>
            public List<End> Nozzles = new List<End>();

            /// <summary>The segment a piece in the lines is an item of, and the first segment that touches it.</summary>
            public Obj Segment, Touching;
        }

        /// <summary>The end of a pipe: the item it is on (a nozzle, a piece in the line or an off-page connector), its node and where it is drawn.</summary>
        sealed class End
        {
            public Obj Owner, Node, Position;
            public double X, Y;
            public string Mark = "";
        }

        sealed class Writer
        {
            readonly IFlowsheet fs;
            public readonly Result Result = new Result();
            readonly Dictionary<string, int> ids = new Dictionary<string, int>();
            readonly Dictionary<string, Item> items = new Dictionary<string, Item>();
            readonly List<Item> ordered = new List<Item>();
            readonly Dictionary<string, Obj> segments = new Dictionary<string, Obj>();
            readonly List<Obj> groups = new List<Obj>();
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

            public Writer(IFlowsheet flowsheet) { fs = flowsheet; }

            string Id(string prefix)
            {
                ids.TryGetValue(prefix, out var n);
                ids[prefix] = ++n;
                return prefix + n.ToString(CultureInfo.InvariantCulture);
            }

            void Grow(double x, double y) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }

            public XElement Build()
            {
                var content = ProcessFlowDiagram.PidData(fs);
                var plant = new Obj("PlantModel1", "Plant/PlantModel");

                foreach (var source in content.Items.OrderBy(i => i.Go.Tag, StringComparer.OrdinalIgnoreCase)) AddItem(source);
                var systems = new List<Obj>();
                foreach (var line in content.Lines.OrderBy(l => l.Tag, StringComparer.OrdinalIgnoreCase)) systems.Add(AddLine(line));

                // a piece in the lines without a line into it belongs to the first line that leaves it
                foreach (var item in ordered.Where(i => i.InLine))
                {
                    item.Object.Comp("Nodes", item.Ins.Take(1).Concat(item.Outs.Take(1)).Concat(item.Ins.Skip(1)).Concat(item.Outs.Skip(1)).Select(e => e.Node).ToArray());
                    item.Segment = item.Segment ?? item.Touching;
                    if (item.Segment == null)
                    {
                        Result.Notes.Add(item.Source.Go.Tag + ": not joined to a line, left out");
                        continue;
                    }
                    item.Segment.Comp("Items", item.Object);
                }
                var equipment = ordered.Where(i => !i.InLine).ToList();
                foreach (var item in equipment) Design(item);
                AddInstruments(content, plant);

                plant.Comp("PipingNetworkSystems", systems.ToArray());
                plant.Comp("TaggedPlantItems", equipment.Select(i => i.Object).ToArray());
                Result.Equipment = equipment.Count;
                Result.Valves = ordered.Count(i => i.InLine && (i.Class.EndsWith("Valve", StringComparison.Ordinal)));
                Result.Fittings = ordered.Count(i => i.InLine) - Result.Valves;
                Result.Lines = systems.Count;
                Result.Nozzles = equipment.Sum(i => i.Nozzles.Count);

                var energy = fs.SimulationObjects.Values.Count(o => o?.GraphicObject != null && o.GraphicObject.ObjectType == ObjectType.EnergyStream);
                if (energy > 0) Result.Notes.Add(energy + " energy streams left out: a P&ID model has the piping, the equipment and the instruments");

                foreach (var item in ordered) Draw(item);
                var model = new Obj("EngineeringModel1", "Core/EngineeringModel").Comp("ConceptualModel", plant).Comp("Diagram", Diagram(plant))
                    .Data("ExportDateTime", new XElement("DateTime", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)))
                    .Data("OriginatingSystemName", Str("DWSIM"))
                    .Data("OriginatingSystemVendorName", Str("Daniel Wagner O. de Medeiros"))
                    .Data("OriginatingSystemVersion", Str(Version()));
                return new XElement("Model", new XAttribute("name", Short(Title())), new XAttribute("uri", "https://dwsim.org"),
                    Import("Core"), Import("Plant"), model.Xml());
            }

            static XElement Import(string prefix) => new XElement("Import", new XAttribute("prefix", prefix),
                new XAttribute("source", "https://data.dexpi.org/models/2.0.1/" + prefix + ".xml"));

            string Title()
            {
                var name = fs.FlowsheetOptions?.SimulationName;
                return string.IsNullOrWhiteSpace(name) ? "DWSIM flowsheet" : name.Trim();
            }

            static string Version()
            {
                var v = GlobalSettings.Settings.CurrentRunningVersion;
                if (string.IsNullOrWhiteSpace(v)) v = GlobalSettings.Settings.CurrentVersion;
                return string.IsNullOrWhiteSpace(v) ? typeof(IFlowsheet).Assembly.GetName().Version.ToString() : v.Trim();
            }

            static string Short(string s) => s.Length > 120 ? s.Substring(0, 117) + "..." : s;

            #region items

            /// <summary>
            /// The DEXPI class of an item, by the symbol the P&amp;ID draws it with, and whether it
            /// is a piece in the lines: valves, the tees of mixers and splitters, and the pieces the
            /// drawing passes its lines through (recycle blocks, pipe segments, orifice plates).
            /// </summary>
            static string ClassOf(PidItem item, out bool inLine)
            {
                inLine = true;
                switch (item.Kind)
                {
                    case "Valve": return "GlobeValve";
                    case "Relief": return "SpringLoadedGlobeSafetyValve";
                    case "Node": return "PipeTee";
                    case "PassThrough": return item.Go.ObjectType == ObjectType.OrificePlate ? "RestrictionOrifice" : "PipeCoupling";
                }
                inLine = false;
                switch (item.Kind)
                {
                    case "VesselV":
                    case "VesselH":
                    case "Reactor":
                    case "Cstr":
                    case "Pfr":
                        return "PressureVessel";
                    case "Tank": return "Tank";
                    case "Column": return "ProcessColumn";
                    case "Pump": return "CentrifugalPump";
                    case "Compressor": return "CentrifugalCompressor";
                    case "Expander": return "GasTurbine";
                    case "Heater": return "Heater";
                    case "Cooler":
                    case "Exchanger":
                        return "TubularHeatExchanger";
                    case "AirCooler": return "AirCoolingSystem";
                    case "Filter": return "Filter";
                    case "Separator": return "Separator";
                    default: return "ProcessEquipment";
                }
            }

            void AddItem(PidItem source)
            {
                var go = source.Go;
                var attached = go.InputConnectors.Concat(go.OutputConnectors).Any(c => c.IsAttached && c.AttachedConnector != null &&
                    (c.AttachedConnector.AttachedFrom?.ObjectType == ObjectType.MaterialStream || c.AttachedConnector.AttachedTo?.ObjectType == ObjectType.MaterialStream));
                if (!attached) return;
                var cls = ClassOf(source, out var inLine);
                var item = new Item
                {
                    Source = source, InLine = inLine, Class = cls,
                    X = go.X * Scale, Y = go.Y * Scale, Width = Math.Max(go.Width * Scale, 4), Height = Math.Max(go.Height * Scale, 4),
                };
                var tag = (go.Tag ?? "").Trim();
                if (inLine)
                {
                    item.Object = new Obj(Id(cls), "Plant/Piping." + cls);
                    // a safety valve does not have a component name: its position number carries the tag
                    item.Object.Data(cls == "SpringLoadedGlobeSafetyValve" ? "PositionNumber" : "PipingComponentName", Str(tag));
                }
                else
                {
                    item.Object = new Obj(Id(cls), "Plant/ProcessEquipment." + cls).Data("TagName", Str(tag));
                    var description = source.Obj.GetType().Name;
                    try { description = source.Obj.GetDisplayName(); } catch (Exception) { }
                    item.Object.Data("EquipmentDescription", Text(description));
                }
                items[source.Obj.Name] = item;
                ordered.Add(item);
            }

            /// <summary>The design data DEXPI has a place for, from what the solved flowsheet holds for the item.</summary>
            void Design(Item item)
            {
                var unit = item.Source.Obj;
                void Put(string property, string unitName, double? v) { if (v.HasValue && v.Value > 0) item.Object.Data(property, Quantity(unitName, Math.Round(v.Value, 6))); }
                double? Abs(double? v) => v.HasValue ? Math.Abs(v.Value) : (double?)null;
                double? VolumeFlow()
                {
                    // the volume flow of the first stream into the item, m3/h
                    foreach (var cp in item.Source.Go.InputConnectors)
                    {
                        var from = cp.IsAttached ? cp.AttachedConnector?.AttachedFrom : null;
                        if (from == null || !fs.SimulationObjects.TryGetValue(from.Name, out var o) || !(o is IMaterialStream ms)) continue;
                        try
                        {
                            var q = ms.Phases[0].Properties.volumetric_flow;
                            if (q.HasValue && !double.IsNaN(q.Value) && !double.IsInfinity(q.Value)) return q.Value * 3600;
                        }
                        catch (Exception) { }
                    }
                    return null;
                }
                const string kW = "PowerUnit.Kilowatt", m3h = "VolumeFlowRateUnit.MetreCubedPerHour";
                switch (item.Class)
                {
                    case "CentrifugalPump":
                        Put("DesignPressureHead", "LengthUnit.Metre", Num(unit, "Head"));
                        Put("DesignShaftPower", kW, Abs(Num(unit, "DeltaQ")));
                        Put("DesignVolumeFlowRate", m3h, VolumeFlow());
                        break;
                    case "CentrifugalCompressor":
                        Put("DesignShaftPower", kW, Abs(Num(unit, "DeltaQ")));
                        Put("DesignVolumeFlowRate", m3h, VolumeFlow());
                        break;
                    case "GasTurbine":
                        Put("DesignPower", kW, Abs(Num(unit, "DeltaQ")));
                        break;
                    case "Heater":
                        Put("DesignHeatFlowRate", kW, Abs(Num(unit, "DeltaQ")));
                        break;
                    case "TubularHeatExchanger":
                    case "AirCoolingSystem":
                        Put("DesignHeatFlowRate", kW, Abs(Num(unit, "Q") ?? Num(unit, "DeltaQ")));
                        Put("DesignHeatTransferArea", "AreaUnit.MetreSquared", Num(unit, "Area"));
                        break;
                    case "Tank":
                    case "PressureVessel":
                        Put("NominalCapacityVolume", "VolumeUnit.MetreCubed", Num(unit, "Volume"));
                        break;
                }
            }

            /// <summary>A number the unit operation holds in a property of this name; null when it does not have one or it is not finite.</summary>
            static double? Num(object o, string property)
            {
                try
                {
                    var value = o.GetType().GetProperty(property)?.GetValue(o, null);
                    if (value == null) return null;
                    var v = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return double.IsNaN(v) || double.IsInfinity(v) ? (double?)null : v;
                }
                catch (Exception) { return null; }
            }

            #endregion

            #region lines

            /// <summary>The size of a line as the DEXPI nominal diameter texts: the whole of it, its number and its kind (DN or NPS).</summary>
            static void Diameter(Obj target, string size)
            {
                size = (size ?? "").Trim();
                if (size.Length == 0) return;
                var digits = new string(size.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '/' || ch == ' ' || ch == '-').ToArray()).Trim();
                target.Data("NominalDiameterRepresentation", Str(size));
                if (digits.Length > 0) target.Data("NominalDiameterNumericalValueRepresentation", Str(digits));
                target.Data("NominalDiameterTypeRepresentation", Str(size.StartsWith("DN", StringComparison.OrdinalIgnoreCase) ? "DN" : "NPS"));
            }

            /// <summary>
            /// The end of a line on an item: a nozzle of an equipment item, with the mark the
            /// item's drawing gives it, or a node of a piece in the line.
            /// </summary>
            End EndOn(Item item, bool outlet, double x, double y, string mark, string size)
            {
                var end = new End { X = x * Scale, Y = y * Scale };
                Snap(item, end, outlet);
                end.Node = new Obj(Id("PipingNode"), "Plant/Piping.PipingNode");
                Diameter(end.Node, size);
                if (item.InLine)
                {
                    end.Owner = item.Object;
                    (outlet ? item.Outs : item.Ins).Add(end);
                }
                else
                {
                    item.Marks++;
                    end.Mark = string.IsNullOrWhiteSpace(mark) ? "N" + item.Marks : mark.Trim();
                    // the same mark twice on an item would make two nozzles one: number the repeat
                    if (item.Nozzles.Any(n => n.Mark == end.Mark)) end.Mark = "N" + (item.Nozzles.Count + 1) + "-" + item.Marks;
                    end.Owner = new Obj(Id("Nozzle"), "Plant/ProcessEquipment.ProcessNozzle").Comp("Nodes", end.Node).Data("SubTagName", Str(end.Mark));
                    item.Object.Comp("Nozzles", end.Owner);
                    item.Nozzles.Add(end);
                }
                end.Position = new Obj(Id("PipingNodePosition"), "Plant/Diagram.PipingNodePosition").Ref("Node", end.Node).Data("Position", Point(end.X, end.Y));
                Grow(end.X, end.Y);
                return end;
            }

            /// <summary>
            /// Puts the end of a line on the outline of its item: on the nearest side of an
            /// equipment item, where the flowsheet has its connection point inside the icon, and on
            /// the middle of the side of a piece in the line that the connection point is nearer to.
            /// </summary>
            static void Snap(Item item, End end, bool outlet)
            {
                double x0 = item.X, y0 = item.Y, x1 = item.X + item.Width, y1 = item.Y + item.Height;
                if (item.InLine)
                {
                    var left = Math.Abs(end.X - x0) < Math.Abs(end.X - x1) || (Math.Abs(end.X - x0) == Math.Abs(end.X - x1) && !outlet);
                    var tee = item.Class == "PipeTee" || item.Class == "PipeCoupling";
                    end.X = tee ? (x0 + x1) / 2 : left ? x0 : x1;
                    end.Y = (y0 + y1) / 2;
                    return;
                }
                end.X = Math.Min(Math.Max(end.X, x0), x1);
                end.Y = Math.Min(Math.Max(end.Y, y0), y1);
                double dl = end.X - x0, dr = x1 - end.X, dt = end.Y - y0, db = y1 - end.Y;
                var m = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
                if (m == dl) end.X = x0;
                else if (m == dr) end.X = x1;
                else if (m == dt) end.Y = y0;
                else end.Y = y1;
            }

            /// <summary>The end of a line at the battery limit: an off-page connector where the stream's icon is.</summary>
            End Boundary(PidLine line, bool feed, Obj segment)
            {
                var go = line.Icon;
                var end = new End { X = (feed ? go.X : go.X + go.Width) * Scale, Y = (go.Y + go.Height / 2.0) * Scale };
                end.Node = new Obj(Id("PipingNode"), "Plant/Piping.PipingNode");
                Diameter(end.Node, line.Size);
                var cls = feed ? "FlowInPipeOffPageConnector" : "FlowOutPipeOffPageConnector";
                end.Owner = new Obj(Id(cls), "Plant/Piping." + cls).Comp("Nodes", end.Node)
                    .Data("PipeConnectorDescription", Text(line.Tag)).Data("PipeConnectorNumber", Str(line.Id));
                segment.Comp("Items", end.Owner);
                end.Position = new Obj(Id("PipingNodePosition"), "Plant/Diagram.PipingNodePosition").Ref("Node", end.Node).Data("Position", Point(end.X, end.Y));
                Grow(end.X, end.Y);
                Result.Connectors++;

                // an arrow with the stream's name, pointing the way of the flow
                double w = 6, h = 2;
                var x0 = feed ? end.X - w : end.X;
                var arrow = new Obj(Id("Outline"), "Core/Diagram.PolyLine").Data("Points", Point(x0, end.Y - h), Point(x0 + w - h, end.Y - h), Point(x0 + w, end.Y),
                    Point(x0 + w - h, end.Y + h), Point(x0, end.Y + h), Point(x0, end.Y - h)).Data("Stroke", Stroke(0.25));
                var name = Label(line.Tag, feed ? x0 - 1 : x0 + w + 1, end.Y, 1.8, feed ? "RightCenter" : "LeftCenter");
                Grow(x0 - (feed ? line.Tag.Length * 1.2 + 2 : 0), end.Y - h);
                Grow(x0 + w + (feed ? 0 : line.Tag.Length * 1.2 + 2), end.Y + h);
                groups.Add(new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup")
                    .Comp("Groups", new Obj(Id("Static"), "Core/Diagram.Static").Comp("Elements", arrow, name))
                    .Comp("NodePositions", end.Position).Ref("Represents", end.Owner));
                return end;
            }

            Obj Label(string text, double x, double y, double size, string alignment = "CenterCenter", int r = 0, int g = 0, int b = 0) =>
                new Obj(Id("Text"), "Core/Diagram.Text").Data("Alignment", Enum("Core/Diagram.TextAlignment." + alignment))
                    .Data("Color", Color(r, g, b)).Data("Font", Str("Arial")).Data("Position", Point(x, y))
                    .Data("Rotation", Dbl(0)).Data("Size", Dbl(size)).Data("Text", Str(text ?? ""));

            /// <summary>
            /// A material stream as a line: a piping network system with its line number, fluid
            /// code and size, one segment and one pipe from the nozzle or piece it leaves to the one
            /// it enters. A piece in the lines is an item of the segment of the first line into it,
            /// which then ends on its outlet, as the DEXPI tools write a segment that ends in an item.
            /// </summary>
            Obj AddLine(PidLine line)
            {
                var segment = new Obj(Id("PipingNetworkSegment"), "Plant/Piping.PipingNetworkSegment").Data("FluidCode", Str("P")).Data("SegmentNumber", Str("S1"));
                Diameter(segment, line.Size);
                var system = new Obj(Id("PipingNetworkSystem"), "Plant/Piping.PipingNetworkSystem").Comp("Segments", segment)
                    .Data("FluidCode", Str("P")).Data("LineNumber", Str(line.Id));
                Diameter(system, line.Size);
                if (line.InsulationService.Length > 0) system.Data("InsulationType", Str(line.InsulationService));
                segments[line.Icon.Name] = segment;

                Item from = line.StartGo != null && items.TryGetValue(line.StartGo.Name, out var a) ? a : null;
                Item to = line.EndGo != null && items.TryGetValue(line.EndGo.Name, out var b) ? b : null;
                var source = from == null ? Boundary(line, true, segment) : EndOn(from, true, line.StartX, line.StartY, line.StartMark, line.Size);
                var target = to == null ? Boundary(line, false, segment) : EndOn(to, false, line.EndX, line.EndY, line.EndMark, line.Size);

                var pipe = new Obj(Id("Pipe"), "Plant/Piping.Pipe").Ref("SourceItem", source.Owner).Ref("SourceNode", source.Node)
                    .Ref("TargetItem", target.Owner).Ref("TargetNode", target.Node);
                segment.Comp("Connections", pipe);

                // a piece in the lines belongs to the first line into it (to the first one out of it when a line does not come into it)
                if (from != null && from.Touching == null) from.Touching = segment;
                if (to != null && to.Touching == null) to.Touching = segment;
                var ownsTarget = to != null && to.InLine && to.Segment == null;
                if (ownsTarget) to.Segment = segment;
                var ownsSource = from != null && from.InLine && from.Segment == null && from.Source.Go.InputConnectors.All(c => !c.IsAttached);
                if (ownsSource) from.Segment = segment;
                segment.Ref("SourceItem", source.Owner).Ref("SourceNode", source.Node).Ref("TargetItem", target.Owner);
                deferredTargets.Add((segment, to, target, ownsTarget));

                // the pipe on the diagram, through where the stream's icon is, with the line number over it
                var connector = new Obj(Id("Line"), "Core/Diagram.ConnectorLine").Ref("Source", source.Position).Ref("Target", target.Position).Data("Stroke", Stroke(0.35));
                double mx = (line.Icon.X + line.Icon.Width / 2.0) * Scale, my = (line.Icon.Y + line.Icon.Height / 2.0) * Scale;
                if (from != null && to != null) { connector.Data("InnerPoints", Point(mx, my)); Grow(mx, my); }
                else { mx = (source.X + target.X) / 2; my = (source.Y + target.Y) / 2; }
                groups.Add(new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup")
                    .Comp("Groups", new Obj(Id("Static"), "Core/Diagram.Static").Comp("Elements", connector)).Ref("Represents", pipe));
                groups.Add(new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup")
                    .Comp("Groups", new Obj(Id("Static"), "Core/Diagram.Static").Comp("Elements", Label(line.Number, mx, my - 1.6, 1.5, "CenterCenter", 0, 0, 160)))
                    .Ref("Represents", system));
                return system;
            }

            /// <summary>The segments whose target node waits for the nodes of the piece they end in.</summary>
            readonly List<(Obj Segment, Item To, End Target, bool Owns)> deferredTargets = new List<(Obj, Item, End, bool)>();

            /// <summary>
            /// The target node of each segment: the node its pipe enters, or, for a segment that
            /// ends in a piece of its own, that piece's outlet.
            /// </summary>
            void CloseSegments()
            {
                foreach (var d in deferredTargets)
                {
                    var node = d.Owns && d.To.Outs.Count > 0 ? d.To.Outs[0].Node : d.Target.Node;
                    d.Segment.Ref("TargetNode", node);
                }
            }

            #endregion

            #region instruments

            /// <summary>
            /// The controllers and gauges as process instrumentation functions, numbered as on the
            /// drawing: each with the function that generates its signal where the measurement is
            /// taken (the line's segment, the piece in the line, or an instrument nozzle on the
            /// equipment), and a controller with the actuating function and the actuator of the
            /// valve it moves.
            /// </summary>
            void AddInstruments(PidContent content, Obj plant)
            {
                CloseSegments();
                foreach (var loop in content.Loops)
                {
                    var go = loop.Obj.GraphicObject;
                    var tag = (go.Tag ?? "").Trim();
                    Obj sensing = null;
                    double sx = 0, sy = 0;
                    var measured = "";
                    if (segments.TryGetValue(loop.MeasuredId, out var segment) && fs.SimulationObjects.TryGetValue(loop.MeasuredId, out var stream))
                    {
                        sensing = segment;
                        sx = (stream.GraphicObject.X + stream.GraphicObject.Width / 2.0) * Scale;
                        sy = (stream.GraphicObject.Y + stream.GraphicObject.Height / 2.0) * Scale;
                        measured = stream.GraphicObject.Tag;
                    }
                    else if (items.TryGetValue(loop.MeasuredId, out var item))
                    {
                        measured = item.Source.Go.Tag;
                        sx = item.X + item.Width; sy = item.Y + item.Height * 0.3;
                        if (item.InLine) { sensing = item.Object; sx = item.X + item.Width / 2; sy = item.Y; }
                        else
                        {
                            // an instrument nozzle on the item, marked after its process nozzles
                            item.Marks++;
                            sensing = new Obj(Id("Nozzle"), "Plant/ProcessEquipment.InstrumentNozzle").Data("SubTagName", Str("N" + item.Marks));
                            item.Object.Comp("Nozzles", sensing);
                        }
                    }
                    if (sensing == null)
                    {
                        Result.Notes.Add(tag + ": what it measures is not on the P&ID, left out");
                        continue;
                    }

                    var function = new Obj(Id("ProcessInstrumentationFunction"), "Plant/Instrumentation.ProcessInstrumentationFunction")
                        .Data("Location", Enum("Plant/Enumerations.LocationClassification." + (loop.Controller ? "CentralLocation" : "Field")))
                        .Data("ProcessInstrumentationFunctionCategory", Str(loop.Letter))
                        .Data("ProcessInstrumentationFunctionNumber", Str(loop.Loop))
                        .Data("ProcessInstrumentationFunctions", Str(loop.Controller ? "IC" : "I"))
                        .Data("DeviceInformation", Str(tag + (measured.Length > 0 ? ", measures " + measured : "")));
                    var signal = new Obj(Id("ProcessSignalGeneratingFunction"), "Plant/Instrumentation.ProcessSignalGeneratingFunction")
                        .Data("ProcessSignalGeneratingFunctionNumber", Str(loop.Letter + (loop.Controller ? "T" : "E") + loop.Loop)).Ref("SensingLocation", sensing);
                    var measuring = new Obj(Id("MeasuringLineFunction"), "Plant/Instrumentation.MeasuringLineFunction").Ref("Source", signal).Ref("Target", function);
                    function.Comp("ProcessSignalGeneratingFunctions", signal).Comp("SignalConveyingFunctions", measuring);
                    Result.Instruments++;

                    // the bubble where the controller or the gauge is on the flowsheet
                    double bx = (go.X + go.Width / 2.0) * Scale, by = (go.Y + go.Height / 2.0) * Scale;
                    const double radius = 3.2;
                    Grow(bx - radius, by - radius); Grow(bx + radius, by + radius);
                    var bubble = new Obj(Id("Bubble"), "Core/Diagram.Ellipse").Data("Center", Point(bx, by)).Data("HorizontalSemiAxis", Dbl(radius))
                        .Data("VerticalSemiAxis", Dbl(radius)).Data("Rotation", Dbl(0)).Data("Stroke", Stroke(0.25, false, 0, 110, 40))
                        .Data("FillStyle", Enum("Core/Diagram.FillStyle.Transparent"));
                    var marks = new Obj(Id("Static"), "Core/Diagram.Static").Comp("Elements", bubble,
                        Label(loop.Letter + (loop.Controller ? "IC" : "I"), bx, by - 1.1, 1.6, "CenterCenter", 0, 110, 40),
                        Label(loop.Loop, bx, by + 1.3, 1.5, "CenterCenter", 0, 110, 40));
                    groups.Add(new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup").Comp("Groups", marks).Ref("Represents", function));
                    groups.Add(Dashed(measuring, sx, sy, bx, by, radius));

                    Item valve = null;
                    if (loop.Controller && items.TryGetValue(loop.MovedId, out valve) && valve.Class == "GlobeValve")
                    {
                        var number = loop.Letter + "V" + loop.Loop;
                        var system = new Obj(Id("ActuatingSystem"), "Plant/Instrumentation.ActuatingSystem").Data("ActuatingSystemNumber", Str(number))
                            .Comp("ControlledActuator", new Obj(Id("ControlledActuator"), "Plant/Instrumentation.ControlledActuator").Data("SubTagName", Str(number + "_YC")))
                            .Comp("OperatedValveReference", new Obj(Id("OperatedValveReference"), "Plant/Instrumentation.OperatedValveReference")
                                .Data("SubTagName", Str(number + "_YV")).Ref("Valve", valve.Object));
                        plant.Comp("ActuatingSystems", system);
                        var actuating = new Obj(Id("ActuatingFunction"), "Plant/Instrumentation.ActuatingFunction").Data("ActuatingFunctionNumber", Str(number)).Ref("Systems", system);
                        if (valve.Segment != null) actuating.Ref("ActuatingLocation", valve.Segment);
                        var conveying = new Obj(Id("SignalConveyingFunction"), "Plant/Instrumentation.SignalConveyingFunction").Ref("Source", function).Ref("Target", actuating);
                        function.Comp("ActuatingFunctions", actuating).Comp("SignalConveyingFunctions", conveying);
                        groups.Add(Dashed(conveying, bx, by, valve.X + valve.Width / 2, valve.Y, radius, true));
                        groups.Add(new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup").Comp("Groups", new Obj(Id("Static"), "Core/Diagram.Static")
                            .Comp("Elements", Label(number, valve.X + valve.Width / 2, valve.Y - 1.6, 1.5, "CenterCenter", 0, 110, 40))).Ref("Represents", actuating));
                        Result.ControlLoops++;
                    }
                    else if (loop.Controller)
                        Result.Notes.Add(tag + ": it does not move a valve of the P&ID, written as a controller without an actuator");

                    plant.Comp("ProcessInstrumentationFunctions", function);
                    plant.Comp("InstrumentationLoopFunctions", new Obj(Id("InstrumentationLoopFunction"), "Plant/Instrumentation.InstrumentationLoopFunction")
                        .Data("InstrumentationLoopFunctionNumber", Str(loop.Letter + "-" + loop.Loop)).Ref("ProcessInstrumentationFunctions", function));
                }
            }

            /// <summary>A dashed signal line between an instrument's bubble and what it reads or moves, stopping at the bubble's edge.</summary>
            Obj Dashed(Obj represents, double x0, double y0, double x1, double y1, double radius, bool fromBubble = false)
            {
                double dx = x1 - x0, dy = y1 - y0, length = Math.Sqrt(dx * dx + dy * dy);
                if (length > radius)
                {
                    if (fromBubble) { x0 += dx / length * radius; y0 += dy / length * radius; }
                    else { x1 -= dx / length * radius; y1 -= dy / length * radius; }
                }
                Grow(x0, y0); Grow(x1, y1);
                var line = new Obj(Id("Signal"), "Core/Diagram.PolyLine").Data("Points", Point(x0, y0), Point(x1, y1)).Data("Stroke", Stroke(0.2, true, 0, 110, 40));
                return new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup").Comp("Groups", new Obj(Id("Static"), "Core/Diagram.Static").Comp("Elements", line))
                    .Ref("Represents", represents);
            }

            #endregion

            #region diagram

            /// <summary>
            /// An item on the diagram, where the flowsheet has it: an outline by its kind with the
            /// tag, and inside its group a group for each nozzle (a tick with its mark) or node.
            /// </summary>
            void Draw(Item item)
            {
                if (item.InLine && item.Segment == null) return;
                double x = item.X, y = item.Y, w = item.Width, h = item.Height, cx = x + w / 2, cy = y + h / 2;
                Grow(x, y); Grow(x + w, y + h);
                var shapes = new Obj(Id("Static"), "Core/Diagram.Static");
                Obj Line(params XElement[] points) => new Obj(Id("Outline"), "Core/Diagram.PolyLine").Data("Points", points.Cast<object>().ToArray()).Data("Stroke", Stroke(0.35));
                Obj Round(double rx, double ry) => new Obj(Id("Outline"), "Core/Diagram.Ellipse").Data("Center", Point(cx, cy)).Data("HorizontalSemiAxis", Dbl(rx))
                    .Data("VerticalSemiAxis", Dbl(ry)).Data("Rotation", Dbl(0)).Data("Stroke", Stroke(0.35)).Data("FillStyle", Enum("Core/Diagram.FillStyle.Transparent"));
                var tag = item.Source.Go.Tag ?? "";
                var labelY = y + h + 2.2;
                switch (item.Class)
                {
                    case "GlobeValve":
                    case "SpringLoadedGlobeSafetyValve":
                        // two triangles tip to tip
                        shapes.Comp("Elements", Line(Point(x, y), Point(x, y + h), Point(x + w, y), Point(x + w, y + h), Point(x, y)));
                        break;
                    case "PipeTee":
                    case "PipeCoupling":
                        shapes.Comp("Elements", Round(Math.Min(w, 1.2) / 2 + 0.3, Math.Min(h, 1.2) / 2 + 0.3));
                        break;
                    case "RestrictionOrifice":
                        shapes.Comp("Elements", Line(Point(cx - 0.6, y), Point(cx - 0.6, y + h)), Line(Point(cx + 0.6, y), Point(cx + 0.6, y + h)));
                        break;
                    case "CentrifugalPump":
                    case "CentrifugalCompressor":
                    case "GasTurbine":
                        shapes.Comp("Elements", Round(w / 2, h / 2));
                        break;
                    case "TubularHeatExchanger":
                    case "Heater":
                    case "AirCoolingSystem":
                        shapes.Comp("Elements", Round(w / 2, h / 2), Line(Point(x, cy), Point(cx - w / 6, cy - h / 4), Point(cx + w / 6, cy + h / 4), Point(x + w, cy)));
                        break;
                    default:
                        shapes.Comp("Elements", Line(Point(x, y), Point(x + w, y), Point(x + w, y + h), Point(x, y + h), Point(x, y)));
                        break;
                }
                var showTag = item.Class != "PipeTee" && item.Class != "PipeCoupling";
                if (showTag)
                {
                    shapes.Comp("Elements", Label(tag, cx, labelY, 1.8));
                    Grow(cx - tag.Length * 0.6, labelY + 1.2); Grow(cx + tag.Length * 0.6, labelY + 1.2);
                }
                var group = new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup").Comp("Groups", shapes).Ref("Represents", item.Object);

                foreach (var end in item.InLine ? item.Ins.Concat(item.Outs) : item.Nozzles)
                {
                    var node = new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup").Comp("NodePositions", end.Position).Ref("Represents", end.Node);
                    if (item.InLine) { group.Comp("Groups", node); continue; }
                    // a nozzle: a tick across the line's end and its mark
                    const double t = 0.8;
                    var vertical = Math.Abs(end.Y - y) < 0.01 || Math.Abs(end.Y - (y + h)) < 0.01;
                    var tick = vertical ? Line(Point(end.X - t, end.Y), Point(end.X + t, end.Y)) : Line(Point(end.X, end.Y - t), Point(end.X, end.Y + t));
                    var mark = Label(end.Mark, end.X + (end.X < cx ? -1.6 : 1.6), end.Y - 1.3, 1.2);
                    group.Comp("Groups", new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup")
                        .Comp("Groups", new Obj(Id("Static"), "Core/Diagram.Static").Comp("Elements", tick, mark), node).Ref("Represents", end.Owner));
                }
                groups.Add(group);
            }

            Obj Diagram(Obj plant)
            {
                var diagram = new Obj("Diagram1", "Core/Diagram.Diagram").Ref("Represents", plant).Comp("Groups", groups.ToArray());
                if (minX > maxX) { minX = minY = 0; maxX = 420; maxY = 297; }
                const double margin = 10;
                return diagram.Data("BackgroundColor", Color(255, 255, 255))
                    .Data("MinX", Dbl(Math.Floor(minX - margin))).Data("MinY", Dbl(Math.Floor(minY - margin)))
                    .Data("MaxX", Dbl(Math.Ceiling(maxX + margin))).Data("MaxY", Dbl(Math.Ceiling(maxY + margin)))
                    .Data("Name", Str(Short(Title())));
            }

            #endregion
        }
    }
}
