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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// Writes a flowsheet as a DEXPI 2.0.1 process model (XML): the unit operations as process
    /// steps with their material ports, the material streams with temperature, pressure, mass flow
    /// and composition, the feeds and products ending at sources and sinks, the energy streams as
    /// thermal and mechanical energy flows, the PID controllers and the gauges as instrumentation
    /// activities joined to their steps by information flows, and a diagram that keeps the
    /// flowsheet's layout. Adjusts, specifications and the other logical blocks are left out.
    /// </summary>
    public static class DexpiExport
    {
        /// <summary>What was written.</summary>
        public class Result
        {
            public string File = "";
            public int Steps, Streams, Sources, Sinks, Compounds;

            /// <summary>The energy streams written as energy flows, the controllers written as control loops and the gauges as measurements.</summary>
            public int EnergyFlows, ControlLoops, Indicators;

            /// <summary>What the model leaves out or could not tell.</summary>
            public List<string> Notes = new List<string>();

            public override string ToString() => Steps + " process steps, " + Streams + " streams (" + Sources + " feeds, " + Sinks + " products) and " +
                Compounds + " compounds" + (EnergyFlows > 0 ? ", " + EnergyFlows + " energy flows" : "") + (ControlLoops > 0 ? ", " + ControlLoops + " control loops" : "") +
                (Indicators > 0 ? ", " + Indicators + " indicators" : "") + " written to " + File;
        }

        /// <summary>Diagram units for each unit of the flowsheet's canvas.</summary>
        const double Scale = 0.25;

        /// <summary>Writes the flowsheet as a DEXPI 2.0.1 process model into <paramref name="file"/>.</summary>
        public static Result ExportProcessModel(IFlowsheet fs, string file)
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

        static XElement Stroke(double width, string dash = "Solid") => Agg("Core/Diagram.Stroke", ("Color", new object[] { Color(0, 0, 0) }),
            ("DashStyle", new object[] { Enum("Core/Diagram.DashStyle." + dash) }), ("Width", new object[] { Dbl(width) }));

        #endregion

        /// <summary>A process step being written: the DEXPI object, where it is drawn and its ports.</summary>
        sealed class Step
        {
            public Obj Object;
            public ISimulationObject Unit;
            public double X, Y, Width, Height;
            public int Ports;
            public List<(Obj Port, Obj Position, double X, double Y)> Drawn = new List<(Obj, Obj, double, double)>();
        }

        sealed class Writer
        {
            readonly IFlowsheet fs;
            public readonly Result Result = new Result();
            readonly Dictionary<string, int> ids = new Dictionary<string, int>();
            readonly Dictionary<string, Obj> roles = new Dictionary<string, Obj>();
            readonly Dictionary<string, Step> steps = new Dictionary<string, Step>();
            readonly List<Step> ordered = new List<Step>();
            readonly HashSet<string> identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public Writer(IFlowsheet flowsheet) { fs = flowsheet; }

            string Id(string prefix)
            {
                ids.TryGetValue(prefix, out var n);
                ids[prefix] = ++n;
                return prefix + "_" + n.ToString(CultureInfo.InvariantCulture);
            }

            /// <summary>A step's or stream's identifier: its tag, numbered when another object has the same one.</summary>
            string Identifier(string tag)
            {
                var name = string.IsNullOrWhiteSpace(tag) ? "Object" : tag.Trim();
                var unique = name;
                for (var i = 2; !identifiers.Add(unique); i++) unique = name + " (" + i + ")";
                return unique;
            }

            Obj Role(string name)
            {
                if (!roles.TryGetValue(name, out var role))
                    roles[name] = role = new Obj("Role_" + name, "Core/Role").Data("Name", Str(name));
                return role;
            }

            Obj Value(object value) => new Obj(Id("Value"), "Core/QualifiedValue").Data("Value", value);

            Obj Value(string unit, double v) => Value(Quantity(unit, v));

            public XElement Build()
            {
                var objects = fs.SimulationObjects.Values.Where(o => o?.GraphicObject != null).ToList();
                var streams = objects.Where(o => o.GraphicObject.ObjectType == ObjectType.MaterialStream && o is IMaterialStream)
                    .OrderBy(o => o.GraphicObject.Tag, StringComparer.OrdinalIgnoreCase).ToList();
                var compounds = fs.SelectedCompounds.Keys.ToList();

                var process = new Obj("ProcessModel_1", "Process/ProcessModel");

                // the compounds, and the one template every stream reads its composition against
                Obj template = null;
                if (compounds.Count > 0)
                {
                    var components = compounds.Select(name =>
                    {
                        var c = new Obj(Id("Component"), "Process/Process.PureMaterialComponent").Data("Identifier", Str(name)).Data("Label", Str(name));
                        var cas = fs.SelectedCompounds[name]?.CAS_Number;
                        if (!string.IsNullOrWhiteSpace(cas)) c.Data("Description", Text("CAS " + cas.Trim()));
                        return c;
                    }).ToArray();
                    var list = new Obj("ComponentList_1", "Process/Process.ListOfMaterialComponents").Ref("Component", components);
                    template = new Obj("MaterialTemplate_1", "Process/Process.MaterialTemplate").Ref("ListOfComponents", list)
                        .Data("Description", Text("The compounds of the simulation, in the order of the composition vectors"))
                        .Data("Identifier", Str("MT-1"))
                        .Data("Label", Str(Short(string.Join(", ", compounds))))
                        .Data("NumberOfMaterialComponents", Int(compounds.Count))
                        .Data("NumberOfPhases", Int(PhaseLabels.Length))
                        .Data("PhaseLabel", PhaseLabels.Select(l => (object)Str(l)).ToArray());
                    process.Comp("MaterialComponents", components).Comp("ListsOfMaterialComponents", list).Comp("MaterialTemplates", template);
                }
                Result.Compounds = compounds.Count;

                // the unit operations the streams join, in the flowsheet's order
                foreach (var o in objects)
                {
                    var type = o.GraphicObject.ObjectType;
                    if (type == ObjectType.MaterialStream || type == ObjectType.EnergyStream || !Joined(o.GraphicObject)) continue;
                    AddStep(o);
                }

                var lines = new List<(Obj Stream, Step From, int FromPort, Step To, int ToPort, IGraphicObject Go)>();
                foreach (var s in streams)
                {
                    var go = s.GraphicObject;
                    var ms = (IMaterialStream)s;
                    var cin = go.InputConnectors.Count > 0 && go.InputConnectors[0].IsAttached ? go.InputConnectors[0].AttachedConnector : null;
                    var cout = go.OutputConnectors.Count > 0 && go.OutputConnectors[0].IsAttached ? go.OutputConnectors[0].AttachedConnector : null;
                    var from = cin?.AttachedFrom != null && steps.TryGetValue(cin.AttachedFrom.Name, out var a) ? a : null;
                    var to = cout?.AttachedTo != null && steps.TryGetValue(cout.AttachedTo.Name, out var b) ? b : null;

                    var tag = Identifier(go.Tag);
                    var stream = new Obj(Id("Stream"), "Process/Process.Stream").Data("Identifier", Str(tag)).Data("Label", Str(go.Tag ?? tag));
                    temperatureValue = pressureValue = flowValue = null;
                    Conditions(stream, ms, template, compounds, tag);

                    // a stream that comes from nowhere starts at a source, one that goes nowhere ends at a sink
                    if (from == null) { from = Boundary(go, true, tag); Result.Sources++; }
                    if (to == null) { to = Boundary(go, false, tag); Result.Sinks++; }
                    var outIndex = from.Unit == null ? 0 : cin.AttachedFromConnectorIndex;
                    var inIndex = to.Unit == null ? 0 : cout.AttachedToConnectorIndex;
                    var source = Port(from, stream, true, outIndex, s);
                    var sourceDrawn = from.Drawn.Count - 1;
                    var target = Port(to, stream, false, inIndex, s);
                    stream.Ref("Source", source).Ref("Target", target);
                    process.Comp("ProcessConnections", stream);
                    lines.Add((stream, from, sourceDrawn, to, to.Drawn.Count - 1, go));
                    written[s.Name] = new Written { Stream = stream, From = from, Temperature = temperatureValue, Pressure = pressureValue, Flow = flowValue };
                    Result.Streams++;
                }

                Energy(process, objects);
                Control(process, objects);

                process.Comp("MaterialStates", states.ToArray());
                process.Comp("Roles", roles.Values.ToArray());
                process.Comp("ProcessSteps", ordered.Select(s => s.Object).ToArray());
                Result.Steps = ordered.Count(s => s.Unit != null);

                var left = objects.Count(o => o.GraphicObject.ObjectType == ObjectType.EnergyStream) - Result.EnergyFlows;
                if (left > 0) Result.Notes.Add(left + " energy streams left out: they are not connected to a unit operation that has material streams");
                var loose = objects.Where(o => o.GraphicObject.ObjectType != ObjectType.MaterialStream && o.GraphicObject.ObjectType != ObjectType.EnergyStream &&
                        !steps.ContainsKey(o.Name) && !instruments.Contains(o.Name))
                    .Select(o => o.GraphicObject.Tag).ToList();
                if (loose.Count > 0) Result.Notes.Add("objects without material streams left out: " + string.Join(", ", loose));

                var model = new Obj("EngineeringModel_1", "Core/EngineeringModel").Comp("ConceptualModel", process).Comp("Diagram", Diagram(process, lines, energyLines))
                    .Data("ExportDateTime", new XElement("DateTime", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)))
                    .Data("OriginatingSystemName", Str("DWSIM"))
                    .Data("OriginatingSystemVendorName", Str("Daniel Wagner O. de Medeiros"))
                    .Data("OriginatingSystemVersion", Str(Version()));

                return new XElement("Model", new XAttribute("name", Short(Title())), new XAttribute("uri", "https://dwsim.org"),
                    Import("Core"), Import("Plant"), Import("Process"), model.Xml());
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

            /// <summary>Whether a material stream is attached to a unit operation.</summary>
            static bool Joined(IGraphicObject go)
            {
                bool Material(IConnectionPoint cp, bool inlet)
                {
                    var other = cp.IsAttached ? (inlet ? cp.AttachedConnector?.AttachedFrom : cp.AttachedConnector?.AttachedTo) : null;
                    return other != null && other.ObjectType == ObjectType.MaterialStream;
                }
                return go.InputConnectors.Any(c => Material(c, true)) || go.OutputConnectors.Any(c => Material(c, false));
            }

            #region steps

            void AddStep(ISimulationObject unit)
            {
                var go = unit.GraphicObject;
                var tag = Identifier(go.Tag);
                var step = new Step
                {
                    Unit = unit,
                    Object = new Obj(Id("Step"), "Process/Process." + StepClass(unit)).Data("Identifier", Str(tag)).Data("Label", Str(go.Tag ?? tag))
                        .Data("HierarchyLevel", Enum("Process/Enumerations.ProcessStepHierarchyLevel.UnitOperation")),
                    X = go.X * Scale, Y = go.Y * Scale, Width = Math.Max(4, go.Width * Scale), Height = Math.Max(4, go.Height * Scale),
                };
                try { Attributes(step.Object, unit); }
                catch (Exception ex) { Result.Notes.Add(go.Tag + ": its data could not be read (" + ex.GetBaseException().Message + ")"); }
                steps[unit.Name] = step;
                ordered.Add(step);
            }

            /// <summary>The source a feed starts at or the sink a product ends at, drawn where the stream is.</summary>
            Step Boundary(IGraphicObject stream, bool source, string tag)
            {
                const double size = 5;
                var x = (stream.X + stream.Width / 2.0) * Scale + (source ? -3 * size : 2 * size);
                var y = (stream.Y + stream.Height / 2.0) * Scale - size / 2;
                var step = new Step
                {
                    Object = new Obj(Id(source ? "Source" : "Sink"), source ? "Process/Process.Source" : "Process/Process.Sink")
                        .Data("Identifier", Str(Identifier(tag + (source ? " (source)" : " (sink)")))).Data("Label", Str(tag)),
                    X = x, Y = y, Width = size, Height = size,
                };
                ordered.Add(step);
                return step;
            }

            static string StepClass(ISimulationObject unit)
            {
                switch (unit.GraphicObject.ObjectType)
                {
                    case ObjectType.Pump: return "Pumping";
                    case ObjectType.Compressor: return "Compressing";
                    case ObjectType.Expander: return "DrivingByTurbine";
                    case ObjectType.CompressorExpander: return Num(unit, "DeltaP") < 0 ? "DrivingByTurbine" : "Compressing";
                    case ObjectType.Heater: return "SupplyingThermalEnergy";
                    case ObjectType.Cooler:
                    case ObjectType.AirCooler2: return "RemovingThermalEnergy";
                    case ObjectType.HeaterCooler: return Num(unit, "DeltaQ") < 0 ? "RemovingThermalEnergy" : "SupplyingThermalEnergy";
                    case ObjectType.HeatExchanger: return "ExchangingThermalEnergy";
                    case ObjectType.ShortcutColumn:
                    case ObjectType.DistillationColumn:
                    case ObjectType.RefluxedAbsorber:
                    case ObjectType.ReboiledAbsorber: return "Distilling";
                    case ObjectType.AbsorptionColumn: return "Absorbing";
                    case ObjectType.RCT_Conversion:
                    case ObjectType.RCT_Equilibrium:
                    case ObjectType.RCT_Gibbs:
                    case ObjectType.RCT_GibbsReaktoro:
                    case ObjectType.RCT_CSTR:
                    case ObjectType.RCT_PFR: return "ReactingChemicals";
                    case ObjectType.Vessel:
                    case ObjectType.TPVessel: return "SeparatingByFlash";
                    case ObjectType.Tank: return "StoringInTank";
                    case ObjectType.Valve: return "RegulatingFlow";
                    case ObjectType.Mixer:
                    case ObjectType.NodeIn: return "MixingSimple";
                    case ObjectType.Splitter:
                    case ObjectType.NodeOut: return "SplittingMaterial";
                    case ObjectType.Filter: return "Filtering";
                    case ObjectType.ComponentSeparator:
                    case ObjectType.SolidSeparator: return "Separating";
                    case ObjectType.Pipe: return "TransportingFluidsInPipe";
                    case ObjectType.OrificePlate: return "LimitingFlow";
                    case ObjectType.OT_Recycle: return "TransportingFluids";
                    default: return "TransportingMaterial";
                }
            }

            /// <summary>What the solved unit operation tells that its DEXPI class has a place for.</summary>
            void Attributes(Obj step, ISimulationObject unit)
            {
                void Put(string property, string unitName, double? v) { if (v.HasValue) step.Comp(property, Value(unitName, v.Value)); }
                double? Bar(double? pa) => pa.HasValue ? Math.Abs(pa.Value) / 1e5 : (double?)null;
                double? Positive(double? v) => v.HasValue ? Math.Abs(v.Value) : (double?)null;
                const string bar = "PressureAbsoluteUnit.Bar", kW = "PowerUnit.Kilowatt", percent = "RatioUnit.Percent";

                var type = unit.GraphicObject.ObjectType;
                switch (StepClass(unit))
                {
                    case "Pumping":
                        Put("PressureDifference", bar, Bar(Num(unit, "DeltaP")));
                        var head = Num(unit, "Head");
                        if (head > 0) Put("Head", "LengthUnit.Metre", head);
                        break;
                    case "Compressing":
                        step.Data("Method", Enum("Process/Enumerations.CompressionMethod.Unspecified"));
                        Put("PressureDifference", bar, Bar(Num(unit, "DeltaP")));
                        Put("ShaftPower", kW, Positive(Num(unit, "DeltaQ")));
                        Put("PolytropicEfficiency", percent, Num(unit, "PolytropicEfficiency"));
                        break;
                    case "DrivingByTurbine":
                        step.Data("Method", Enum("Process/Enumerations.TurbineDriveMethod.Expander"));
                        Put("PressureDifference", bar, Bar(Num(unit, "DeltaP")));
                        Put("ShaftPower", kW, Positive(Num(unit, "DeltaQ")));
                        Put("Efficiency", percent, Num(unit, "AdiabaticEfficiency"));
                        break;
                    case "SupplyingThermalEnergy":
                        Put("Duty", kW, Positive(Num(unit, "DeltaQ")));
                        break;
                    case "RemovingThermalEnergy":
                        step.Data("Method", Enum("Process/Enumerations.HeatExchangeMethod.Generic"));
                        Put("Duty", kW, Positive(Num(unit, "DeltaQ")));
                        break;
                    case "ExchangingThermalEnergy":
                        step.Data("Method", Enum("Process/Enumerations.HeatExchangeMethod.Generic"));
                        Put("Duty", kW, Positive(Num(unit, "Q")));
                        var area = Num(unit, "Area");
                        if (area > 0) Put("Area", "AreaUnit.MetreSquared", area);
                        break;
                    case "Distilling":
                        if (type != ObjectType.ShortcutColumn)
                        {
                            var reflux = Num(unit, "RefluxRatio");
                            if (reflux > 0 && type != ObjectType.ReboiledAbsorber) step.Comp("RefluxRatio", Value(Dbl(reflux.Value)));
                            var condenser = Positive(Num(unit, "CondenserDuty"));
                            if (condenser > 0 && type != ObjectType.ReboiledAbsorber) Put("CondenserDuty", kW, condenser);
                            var reboiler = Positive(Num(unit, "ReboilerDuty"));
                            if (reboiler > 0 && type != ObjectType.RefluxedAbsorber) Put("Duty", kW, reboiler);
                        }
                        break;
                    case "ReactingChemicals":
                        step.Data("Method", Enum("Process/Enumerations.ReactionProcessType." +
                            (type == ObjectType.RCT_CSTR ? "Tank" : type == ObjectType.RCT_PFR ? "Tubular" : "Unspecified")));
                        var volume = type == ObjectType.RCT_CSTR || type == ObjectType.RCT_PFR ? Num(unit, "Volume") : null;
                        if (volume > 0) Put("Volume", "VolumeUnit.MetreCubed", volume);
                        break;
                    case "RegulatingFlow":
                    case "LimitingFlow":
                    case "TransportingFluidsInPipe":
                        Put("PressureDifference", bar, Bar(Num(unit, "DeltaP")));
                        break;
                }
            }

            /// <summary>A number the unit operation holds in a property of this name; null when it has none or it is not finite.</summary>
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

            #region ports

            /// <summary>The port of a step that a stream leaves from or arrives at, with the role its connector has.</summary>
            Obj Port(Step step, Obj stream, bool outlet, int index, ISimulationObject streamObject)
            {
                step.Ports++;
                var role = step.Unit == null ? (outlet ? "Outlet" : "Inlet") : RoleOf(step.Unit, outlet, index, streamObject);
                var port = new Obj(Id("Port"), "Process/Process.MaterialPort").Ref("ConnectorReference", stream).Ref("PerformedRoles", Role(role))
                    .Data("Identifier", Str("N" + step.Ports.ToString(CultureInfo.InvariantCulture)))
                    .Data("NominalDirection", Enum("Process/Enumerations.PortDirection." + (outlet ? "Outlet" : "Inlet")));
                step.Object.Comp("Ports", port);
                var at = Where(step, outlet, index);
                var position = new Obj(Id("Position"), "Process/Diagram.PortNodePosition").Ref("Port", port).Data("Position", Point(at.X, at.Y));
                step.Drawn.Add((port, position, at.X, at.Y));
                return port;
            }

            static string RoleOf(ISimulationObject unit, bool outlet, int index, ISimulationObject stream)
            {
                switch (unit.GraphicObject.ObjectType)
                {
                    case ObjectType.Vessel:
                    case ObjectType.TPVessel:
                        return !outlet ? "Inlet" : index == 0 ? "Vapour" : index == 1 ? "Liquid" : "HeavyLiquid";
                    case ObjectType.RCT_Conversion:
                    case ObjectType.RCT_Equilibrium:
                    case ObjectType.RCT_Gibbs:
                    case ObjectType.RCT_GibbsReaktoro:
                        return !outlet ? "Inlet" : index == 0 ? "Vapour" : "Liquid";
                    case ObjectType.ShortcutColumn:
                        return !outlet ? "Feed" : index == 0 ? "TopProduct" : "BottomProduct";
                    case ObjectType.DistillationColumn:
                    case ObjectType.AbsorptionColumn:
                    case ObjectType.RefluxedAbsorber:
                    case ObjectType.ReboiledAbsorber:
                        switch (ColumnBehavior(unit, stream))
                        {
                            case "Distillate":
                            case "OverheadVapor": return "TopProduct";
                            case "BottomsLiquid": return "BottomProduct";
                            case "Sidedraw":
                            case "SideOpLiquidProduct":
                            case "SideOpVaporProduct": return "Sidedraw";
                            case "FreeWater": return "HeavyLiquid";
                            default: return outlet ? "Product" : "Feed";
                        }
                    default:
                        return outlet ? "Outlet" : "Inlet";
                }
            }

            /// <summary>What a stream is to a rigorous column (Feed, Distillate, BottomsLiquid, Sidedraw...), "" when the column does not tell.</summary>
            static string ColumnBehavior(ISimulationObject column, ISimulationObject stream)
            {
                try
                {
                    if (!(column.GetType().GetProperty("MaterialStreams")?.GetValue(column, null) is IDictionary list)) return "";
                    foreach (var info in list.Values)
                    {
                        var id = info.GetType().GetProperty("StreamID")?.GetValue(info, null) as string;
                        if (id == stream.Name) return info.GetType().GetProperty("StreamBehavior")?.GetValue(info, null)?.ToString() ?? "";
                    }
                }
                catch (Exception) { }
                return "";
            }

            /// <summary>
            /// Where a port is drawn: at the flowsheet's connector when that lies on the object, else
            /// spread along the left side (inlets) or the right side (outlets) of the block.
            /// </summary>
            static (double X, double Y) Where(Step step, bool outlet, int index)
            {
                var go = step.Unit?.GraphicObject;
                if (go != null)
                {
                    var list = outlet ? go.OutputConnectors : go.InputConnectors;
                    var p = index >= 0 && index < list.Count ? list[index].Position : null;
                    if (p != null)
                    {
                        double x = p.X * Scale, y = p.Y * Scale;
                        const double slack = 0.5;
                        if (x >= step.X - slack && x <= step.X + step.Width + slack && y >= step.Y - slack && y <= step.Y + step.Height + slack)
                            return (Math.Min(Math.Max(x, step.X), step.X + step.Width), Math.Min(Math.Max(y, step.Y), step.Y + step.Height));
                    }
                    var count = Math.Max(1, list.Count);
                    var slot = Math.Min(Math.Max(index, 0), count - 1);
                    return (outlet ? step.X + step.Width : step.X, step.Y + step.Height * (slot + 1.0) / (count + 1.0));
                }
                return (outlet ? step.X + step.Width : step.X, step.Y + step.Height / 2);
            }

            #endregion

            #region streams

            /// <summary>The phases a stream's state is split into, with the flowsheet's phase each one is read from.</summary>
            static readonly string[] PhaseLabels = { "Vapour", "Liquid", "Solid" };
            static readonly int[] PhaseIndices = { 2, 1, 7 };

            /// <summary>
            /// A phase, or the overall mixture, as a DEXPI material state type: its mass flow, the
            /// composition in mole fractions and in the mass flow of each compound, and its density,
            /// viscosity, volume flow and specific enthalpy where the flowsheet has them. Null for a
            /// phase without a composition.
            /// </summary>
            Obj StateType(IPhase phase, List<string> compounds, double? flow, string label, string identifier, string description)
            {
                bool Real(double? v) => v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value);
                var moles = new double[compounds.Count];
                var mass = new double[compounds.Count];
                for (var i = 0; i < compounds.Count; i++)
                    if (phase.Compounds.TryGetValue(compounds[i], out var c) && c != null)
                    {
                        moles[i] = Real(c.MoleFraction) ? Math.Max(0, c.MoleFraction.Value) : 0;
                        mass[i] = Real(c.MassFraction) ? Math.Max(0, c.MassFraction.Value) : 0;
                    }
                if (moles.Sum() <= 0) return null;

                var composition = new Obj(Id("Composition"), "Process/Process.Composition")
                    .Comp("MoleFractions", Value(Agg("Core/PhysicalQuantities.PhysicalQuantityVector",
                        ("Unit", new object[] { Enum("Core/PhysicalQuantities.RatioUnit.Fraction") }),
                        ("Values", Normalized(moles).Select(x => (object)Dbl(x)).ToArray()))));
                if (flow.HasValue && mass.Sum() > 0)
                {
                    var fractions = Normalized(mass);
                    composition.Comp("MassFlow", fractions.Select(x => Value("MassFlowRateUnit.KilogramPerHour", Round(x * flow.Value))).ToArray());
                }
                var type = new Obj(Id("MaterialStateType"), "Process/Process.MaterialStateType").Comp("Composition", composition)
                    .Data("Description", Text(description)).Data("Identifier", Str(identifier)).Data("Label", Str(label));
                if (flow.HasValue) type.Comp("MassFlow", Value("MassFlowRateUnit.KilogramPerHour", Round(flow.Value)));
                var props = phase.Properties;
                if (Real(props.density) && props.density.Value > 0) type.Comp("Density", Value("DensityUnit.KilogramPerMetreCubed", Round(props.density.Value)));
                if (Real(props.viscosity) && props.viscosity.Value > 0) type.Comp("Viscosity", Value("DynamicViscosityUnit.PascalSecond", Round(props.viscosity.Value)));
                if (Real(props.volumetric_flow) && props.volumetric_flow.Value > 0) type.Comp("VolumeFlow", Value("VolumeFlowRateUnit.MetreCubedPerHour", Round(props.volumetric_flow.Value * 3600)));
                if (Real(props.enthalpy)) type.Comp("SpecificEnthalpy", Value("MassSpecificEnergyUnit.KilojoulePerKilogram", Round(props.enthalpy.Value)));
                return type;
            }

            /// <summary>The stream's temperature, pressure and mass flow, and its material state: the overall mixture and each phase present.</summary>
            void Conditions(Obj stream, IMaterialStream ms, Obj template, List<string> compounds, string tag)
            {
                bool Real(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
                double t, p, w;
                try { t = ms.GetTemperature(); p = ms.GetPressure(); w = ms.GetMassFlow(); }
                catch (Exception) { Result.Notes.Add(tag + ": no conditions to write"); return; }
                if (Real(t) && t > 0) stream.Comp("Temperature", temperatureValue = Value("TemperatureUnit.DegreeCelsius", Math.Round(t - 273.15, 6)));
                if (Real(p) && p > 0) stream.Comp("Pressure", pressureValue = Value("PressureAbsoluteUnit.Bar", Math.Round(p / 1e5, 8)));
                var flow = Real(w) && w >= 0 ? w * 3600 : (double?)null;
                if (flow.HasValue) stream.Comp("MassFlow", flowValue = Value("MassFlowRateUnit.KilogramPerHour", Round(flow.Value)));
                if (template == null) return;
                stream.Ref("MaterialTemplateReference", template);

                // the overall composition, in the template's order
                IPhase overall = null;
                try { overall = ms.Phases[0]; } catch (Exception) { }
                if (overall == null) return;
                var type = StateType(overall, compounds, flow, tag, "MST-" + tag, "Overall state of " + tag);
                if (type == null) return;
                var state = new Obj(Id("MaterialState"), "Process/Process.MaterialState").Comp("State", type)
                    .Data("Description", Str("Material state of " + tag)).Data("Identifier", Str("MS-" + tag)).Data("Label", Str(tag));

                // the phases present, each with its own flow, composition and properties
                for (var k = 0; k < PhaseIndices.Length; k++)
                {
                    IPhase phase = null;
                    try { phase = ms.Phases[PhaseIndices[k]]; } catch (Exception) { }
                    var phaseFlow = phase?.Properties.massflow;
                    if (phase == null || !phaseFlow.HasValue || !Real(phaseFlow.Value) || phaseFlow.Value <= 0) continue;
                    var part = StateType(phase, compounds, phaseFlow.Value * 3600, PhaseLabels[k], "MST-" + tag + "-" + PhaseLabels[k], PhaseLabels[k] + " phase of " + tag);
                    if (part != null) state.Comp("Phases", part);
                }
                states.Add(state);
                stream.Ref("MaterialStateReference", state);
            }

            readonly List<Obj> states = new List<Obj>();

            /// <summary>The fractions made to add up to one, rounded, with the rounding's remainder on the largest.</summary>
            static double[] Normalized(double[] values)
            {
                var total = values.Sum();
                var x = values.Select(v => Math.Round(v / total, 10)).ToArray();
                var largest = Array.IndexOf(x, x.Max());
                x[largest] = Math.Round(x[largest] + 1.0 - x.Sum(), 10);
                return x;
            }

            static double Round(double v) => v == 0 ? 0 : Math.Round(v, Math.Min(15, Math.Max(0, 9 - (int)Math.Floor(Math.Log10(Math.Abs(v))) - 1)));

            #endregion

            #region energy

            /// <summary>A material stream as written: its connection, the step it leaves and the values a measurement refers to.</summary>
            sealed class Written
            {
                public Obj Stream, Temperature, Pressure, Flow;
                public Step From;
            }

            readonly Dictionary<string, Written> written = new Dictionary<string, Written>();
            readonly HashSet<string> instruments = new HashSet<string>();
            readonly List<(Obj Stream, Step From, int FromPort, Step To, int ToPort, IGraphicObject Go)> energyLines =
                new List<(Obj, Step, int, Step, int, IGraphicObject)>();
            readonly Dictionary<Step, int> energyPorts = new Dictionary<Step, int>();
            Obj temperatureValue, pressureValue, flowValue;

            static bool Mechanical(Step step)
            {
                var type = step?.Unit?.GraphicObject.ObjectType;
                return type == ObjectType.Pump || type == ObjectType.Compressor || type == ObjectType.Expander || type == ObjectType.CompressorExpander;
            }

            /// <summary>
            /// Each energy stream attached to a step as an energy flow: mechanical for the shaft power
            /// of a pump, a compressor or an expander, thermal for a duty. The end that is open on
            /// the flowsheet starts at a source (the energy the step takes) or ends at a sink (the
            /// energy it gives off), as the material streams do.
            /// </summary>
            void Energy(Obj process, List<ISimulationObject> objects)
            {
                foreach (var e in objects.Where(o => o.GraphicObject.ObjectType == ObjectType.EnergyStream).OrderBy(o => o.GraphicObject.Tag, StringComparer.OrdinalIgnoreCase))
                {
                    var go = e.GraphicObject;
                    var cin = go.InputConnectors.Count > 0 && go.InputConnectors[0].IsAttached ? go.InputConnectors[0].AttachedConnector : null;
                    var cout = go.OutputConnectors.Count > 0 && go.OutputConnectors[0].IsAttached ? go.OutputConnectors[0].AttachedConnector : null;
                    var from = cin?.AttachedFrom != null && steps.TryGetValue(cin.AttachedFrom.Name, out var a) ? a : null;
                    var to = cout?.AttachedTo != null && steps.TryGetValue(cout.AttachedTo.Name, out var b) ? b : null;
                    if (from == null && to == null) continue;

                    var mechanical = Mechanical(from) || Mechanical(to);
                    var tag = Identifier(go.Tag);
                    var flow = new Obj(Id("EnergyFlow"), "Process/Process." + (mechanical ? "MechanicalEnergyFlow" : "ThermalEnergyFlow"))
                        .Data("Identifier", Str(tag)).Data("Label", Str(go.Tag ?? tag));
                    double? duty = null;
                    try { duty = (e as IEnergyStream)?.GetEnergyFlow(); } catch (Exception) { }
                    if (duty.HasValue && !double.IsNaN(duty.Value) && !double.IsInfinity(duty.Value))
                        flow.Comp("Duty", Value("PowerUnit.Kilowatt", Round(Math.Abs(duty.Value))));

                    if (from == null) from = Boundary(go, true, tag);
                    if (to == null) to = Boundary(go, false, tag);
                    var source = EnergyPort(from, flow, true, mechanical);
                    var sourceDrawn = from.Drawn.Count - 1;
                    var target = EnergyPort(to, flow, false, mechanical);
                    flow.Ref("Source", source).Ref("Target", target);
                    process.Comp("ProcessConnections", flow);
                    energyLines.Add((flow, from, sourceDrawn, to, to.Drawn.Count - 1, go));
                    Result.EnergyFlows++;
                }
            }

            /// <summary>The energy port of a step for a flow, drawn on the bottom side of a unit operation, the ports side by side.</summary>
            Obj EnergyPort(Step step, Obj flow, bool outlet, bool mechanical)
            {
                step.Ports++;
                var port = new Obj(Id("Port"), "Process/Process." + (mechanical ? "MechanicalEnergyPort" : "ThermalEnergyPort"))
                    .Ref("ConnectorReference", flow).Ref("PerformedRoles", Role(outlet ? "Output" : "Input"))
                    .Data("Identifier", Str("E" + step.Ports.ToString(CultureInfo.InvariantCulture)))
                    .Data("NominalDirection", Enum("Process/Enumerations.PortDirection." + (outlet ? "Outlet" : "Inlet")));
                step.Object.Comp("Ports", port);
                energyPorts.TryGetValue(step, out var n);
                energyPorts[step] = ++n;
                double x, y;
                if (step.Unit == null) { x = outlet ? step.X + step.Width : step.X; y = step.Y + step.Height / 2; }
                else { x = step.X + step.Width * Math.Min(0.9, 0.25 * n); y = step.Y + step.Height; }
                var position = new Obj(Id("Position"), "Process/Diagram.PortNodePosition").Ref("Port", port).Data("Position", Point(x, y));
                step.Drawn.Add((port, position, x, y));
                return port;
            }

            #endregion

            #region control

            /// <summary>
            /// The PID controllers and the gauges as instrumentation activities. A controller is an
            /// instrumentation system with a measuring activity and a controlling activity: an
            /// information flow goes from the measured step (for a stream, the step it leaves) to the
            /// measurement, one from the measurement to the controller and one from the controller
            /// to the step it moves. A gauge is a system with the measuring activity alone.
            /// </summary>
            void Control(Obj process, List<ISimulationObject> objects)
            {
                var systems = new List<Obj>();
                foreach (var o in objects.OrderBy(x => x.GraphicObject.Tag, StringComparer.OrdinalIgnoreCase))
                {
                    var go = o.GraphicObject;
                    var controller = go.ObjectType == ObjectType.Controller_PID && o is IAdjust;
                    var indicator = !controller && o is IIndicator;
                    if (!controller && !indicator) continue;

                    string measuredId, property, units;
                    UnitOfMeasure kind;
                    if (controller)
                    {
                        var data = ((IAdjust)o).ControlledObjectData;
                        measuredId = data?.ID; property = data?.PropertyName ?? ""; units = data?.Units ?? ""; kind = data?.UnitsType ?? UnitOfMeasure.none;
                    }
                    else
                    {
                        var gauge = (IIndicator)o;
                        measuredId = gauge.SelectedObjectID; property = gauge.SelectedProperty ?? ""; units = gauge.SelectedPropertyUnits ?? ""; kind = gauge.SelectedPropertyType;
                    }
                    if (string.IsNullOrEmpty(measuredId) || !fs.SimulationObjects.TryGetValue(measuredId, out var measured))
                    {
                        Result.Notes.Add(go.Tag + ": it does not measure an object of the flowsheet; left out");
                        continue;
                    }
                    var letter = Instruments.Letter(fs, kind, property, go.Tag);
                    var quantity = letter == "P" ? "Pressure" : letter == "F" ? "Flow" : letter == "L" ? "Level" : null;
                    var what = letter == "T" ? "temperature" : letter == "P" ? "pressure" : letter == "F" ? "flow" : letter == "L" ? "level" : "value";
                    var measuredTag = measured.GraphicObject?.Tag ?? measured.Name;

                    // where the measurement is taken: on a stream (read at the step it leaves) or on a step
                    Step at;
                    Written stream = null;
                    if (written.TryGetValue(measured.Name, out stream)) at = stream.From;
                    else if (!steps.TryGetValue(measured.Name, out at))
                    {
                        Result.Notes.Add(go.Tag + ": " + measuredTag + ", which it measures, is not in the model; left out");
                        continue;
                    }

                    var tag = Identifier(go.Tag);
                    double Finite(double? v) => v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value) ? v.Value : 0.0;
                    var pv = Finite(controller ? Num(o, "PVValue") : Num(o, "CurrentValue"));
                    var description = "the " + what + " of " + measuredTag + (units.Length > 0 ? " in " + units : "");

                    var sensed = Signal(tag + ".1", "Measured " + what + " of " + measuredTag, pv);
                    var measuring = new Obj(Id("Activity"), "Process/Process.MeasuringProcessVariable")
                        .Data("Identifier", Str(Identifier(controller ? tag + ".M" : tag + ".I")))
                        .Data("Label", Str(letter + (controller ? "T" : "I") + " " + measuredTag))
                        .Data("Description", Text("Measures " + description))
                        .Comp("OutputValue", Value(Dbl(pv)));
                    if (quantity != null) measuring.Data("MeasuredVariable", Enum("Process/Enumerations.MeasuredQuantity." + quantity));
                    if (stream != null)
                    {
                        measuring.Ref("ConnectionReference", stream.Stream);
                        var value = letter == "T" ? stream.Temperature : letter == "P" ? stream.Pressure : letter == "F" && property == "PROP_MS_2" ? stream.Flow : null;
                        if (value != null) measuring.Ref("MeasuredVariableReference", value);
                    }
                    else measuring.Ref("ProcessStepReference", at.Object);
                    sensed.Ref("Source", InformationPort(at.Object, "Ports", ref at.Ports, "S", sensed, true)).Ref("Target", ActivityPort(measuring, tag + (controller ? ".M" : ".I"), 1, sensed, false));
                    process.Comp("ProcessConnections", sensed);

                    var system = new Obj(Id("InstrumentationSystem"), "Process/Process.InstrumentationSystemActivity")
                        .Comp("InstrumentationActivities", measuring)
                        .Data("Identifier", Str(tag)).Data("Label", Str(go.Tag ?? tag))
                        .Data("Description", Text((controller ? "Controls " : "Indicates ") + description));
                    systems.Add(system);
                    instruments.Add(o.Name);
                    if (!controller) { Result.Indicators++; continue; }
                    Result.ControlLoops++;

                    var sp = Finite(Num(o, "SetPoint"));
                    var mv = Finite(Num(o, "MVValue"));
                    var controlling = new Obj(Id("Activity"), "Process/Process.ControllingProcessVariable")
                        .Data("Identifier", Str(Identifier(tag + ".C")))
                        .Data("Label", Str(letter + "C " + (go.Tag ?? tag)))
                        .Data("Description", Text("Controls " + description + (units.Length > 0 ? "; the setpoint is in " + units : "")))
                        .Comp("InputValue", Value(Dbl(pv))).Comp("OutputValue", Value(Dbl(mv))).Comp("Setpoint", Value(Dbl(sp)));
                    system.Comp("InstrumentationActivities", controlling);
                    var reading = Signal(tag + ".2", "Reading of " + tag, pv);
                    reading.Ref("Source", ActivityPort(measuring, tag + ".M", 2, reading, true)).Ref("Target", ActivityPort(controlling, tag + ".C", 1, reading, false));
                    process.Comp("ProcessConnections", reading);

                    // what the controller moves: a step (a valve), or the step a manipulated stream leaves
                    var moved = ((IAdjust)o).ManipulatedObjectData;
                    Step target = null;
                    if (!string.IsNullOrEmpty(moved?.ID) && !steps.TryGetValue(moved.ID, out target) && written.TryGetValue(moved.ID, out var movedStream)) target = movedStream.From;
                    if (target == null)
                    {
                        Result.Notes.Add(go.Tag + ": what it moves is not in the model; the loop is written without its output");
                        continue;
                    }
                    var movedTag = fs.SimulationObjects.TryGetValue(moved.ID, out var movedObject) ? movedObject.GraphicObject?.Tag ?? moved.ID : moved.ID;
                    var output = Signal(tag + ".3", "Output of " + tag + " to " + movedTag, mv);
                    output.Ref("Source", ActivityPort(controlling, tag + ".C", 2, output, true)).Ref("Target", InformationPort(target.Object, "Ports", ref target.Ports, "S", output, false));
                    process.Comp("ProcessConnections", output);
                }
                process.Comp("InstrumentationSystemActivities", systems.ToArray());
            }

            /// <summary>An information flow carrying one number.</summary>
            Obj Signal(string identifier, string label, double value) => new Obj(Id("Signal"), "Process/Process.InformationFlow")
                .Comp("InformationValue", new Obj(Id("SignalValue"), "Process/Process.InformationVariant")
                    .Data("BooleanValue", new XElement("Boolean", "false")).Data("DoubleValue", Dbl(value)).Data("IntegerValue", Int(0))
                    .Data("VariantType", Enum("Process/Enumerations.InformationVariantType.Double")).Data("VectorSize", Int(1)))
                .Data("Identifier", Str(Identifier(identifier))).Data("Label", Str(label));

            /// <summary>An information port among a step's ports, numbered with them.</summary>
            Obj InformationPort(Obj owner, string slot, ref int count, string prefix, Obj flow, bool outlet)
            {
                count++;
                var port = new Obj(Id("Port"), "Process/Process.InformationPort").Ref("ConnectorReference", flow).Ref("PerformedRoles", Role(outlet ? "Output" : "Input"))
                    .Data("Identifier", Str(prefix + count.ToString(CultureInfo.InvariantCulture)))
                    .Data("NominalDirection", Enum("Process/Enumerations.PortDirection." + (outlet ? "Outlet" : "Inlet")));
                owner.Comp(slot, port);
                return port;
            }

            /// <summary>An information port of an instrumentation activity.</summary>
            Obj ActivityPort(Obj activity, string name, int number, Obj flow, bool outlet)
            {
                var port = new Obj(Id("Port"), "Process/Process.InformationPort").Ref("ConnectorReference", flow).Ref("PerformedRoles", Role(outlet ? "Output" : "Input"))
                    .Data("Identifier", Str(name + "." + number.ToString(CultureInfo.InvariantCulture)))
                    .Data("NominalDirection", Enum("Process/Enumerations.PortDirection." + (outlet ? "Outlet" : "Inlet")));
                activity.Comp("InformationPorts", port);
                return port;
            }

            #endregion

            #region diagram

            /// <summary>
            /// The flowsheet's layout: a block with its tag for each step, its ports where the
            /// flowsheet's connectors are, and a line for each stream through where the stream is drawn.
            /// </summary>
            Obj Diagram(Obj process, List<(Obj Stream, Step From, int FromPort, Step To, int ToPort, IGraphicObject Go)> lines,
                List<(Obj Stream, Step From, int FromPort, Step To, int ToPort, IGraphicObject Go)> energy)
            {
                var diagram = new Obj("Diagram_1", "Core/Diagram.Diagram").Ref("Represents", process);
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                void Grow(double x, double y) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }

                foreach (var s in ordered)
                {
                    Grow(s.X, s.Y);
                    Grow(s.X + s.Width, s.Y + s.Height);
                    var outline = new Obj(Id("Outline"), "Core/Diagram.PolyLine")
                        .Data("Points", Point(s.X, s.Y), Point(s.X + s.Width, s.Y), Point(s.X + s.Width, s.Y + s.Height), Point(s.X, s.Y + s.Height), Point(s.X, s.Y))
                        .Data("Stroke", Stroke(0.35));
                    var shapes = new Obj(Id("Static"), "Core/Diagram.Static").Comp("Elements", outline);
                    if (s.Unit != null)
                        shapes.Comp("Elements", new Obj(Id("Text"), "Core/Diagram.Text")
                            .Data("Alignment", Enum("Core/Diagram.TextAlignment.CenterCenter"))
                            .Data("Color", Color(0, 0, 0)).Data("Font", Str("Arial"))
                            .Data("Position", Point(s.X + s.Width / 2, s.Y + s.Height / 2))
                            .Data("Rotation", Dbl(0)).Data("Size", Dbl(1.8)).Data("Text", Str(s.Unit.GraphicObject.Tag ?? "")));
                    var group = new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup").Comp("Groups", shapes)
                        .Comp("NodePositions", s.Drawn.Select(d => d.Position).ToArray()).Ref("Represents", s.Object);
                    diagram.Comp("Groups", group);
                }
                // the energy flows are drawn as the streams are, with a dashed line
                foreach (var l in lines.Select(x => (Line: x, Dash: "Solid")).Concat(energy.Select(x => (Line: x, Dash: "Dash"))).Select(x => (x.Line.Stream, x.Line.From, x.Line.FromPort, x.Line.To, x.Line.ToPort, x.Line.Go, x.Dash)))
                {
                    var a = l.From.Drawn[l.FromPort];
                    var b = l.To.Drawn[l.ToPort];
                    var line = new Obj(Id("Line"), "Core/Diagram.ConnectorLine").Ref("Source", a.Position).Ref("Target", b.Position).Data("Stroke", Stroke(0.25, l.Dash));
                    // through the stream's own place on the flowsheet, when both ends are unit operations
                    if (l.From.Unit != null && l.To.Unit != null)
                    {
                        double x = (l.Go.X + l.Go.Width / 2.0) * Scale, y = (l.Go.Y + l.Go.Height / 2.0) * Scale;
                        line.Data("InnerPoints", Point(x, y));
                        Grow(x, y);
                    }
                    diagram.Comp("Groups", new Obj(Id("Drawing"), "Core/Diagram.RepresentationGroup")
                        .Comp("Groups", new Obj(Id("Static"), "Core/Diagram.Static").Comp("Elements", line)).Ref("Represents", l.Stream));
                }
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
