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
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;
using Newtonsoft.Json.Linq;
using Converter = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// Data sheets of the flowsheet: one per item of equipment (its operating conditions stream by
    /// stream, its process data, preliminary design conditions and materials, its main dimensions
    /// and its nozzles), one per control loop (transmitter, controller and control valve, with the
    /// valve's flow coefficient) and one per local indicator, on the sheet and title block of the
    /// chosen standard, in metric engineering units. Drawn in CADCraft, saved as DXF and DWG and
    /// plotted to PDF and PNG.
    /// </summary>
    public static class DataSheets
    {
        public class Result
        {
            public List<string> Drawings = new List<string>(), Pdfs = new List<string>(), Failed = new List<string>();
            public string Error;
        }

        const double RowHeight = 5.5, TextHeight = 2.5, Gap = 3;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A table of the sheet: label and value rows, or a grid with a header row.</summary>
        class Section
        {
            public string Title;
            public string[] Header;
            public string Corner = "";
            public List<string[]> Rows = new List<string[]>();
            public double LabelShare = 0.45;

            /// <summary>The width of a grid's label column, mm; 0 for the default.</summary>
            public double LabelWidth;
            public int Lines => 1 + (Header != null ? 1 : 0) + Rows.Count;

            public Section(string title) { Title = title; }

            public void Add(string label, string value) { if (!string.IsNullOrEmpty(value)) Rows.Add(new[] { label, value }); }
        }

        class Sheet
        {
            public string Tag, Kind, Subtitle, Number;
            public List<Section> Sections = new List<Section>();
        }

        /// <summary>Writes every data sheet; <paramref name="titleFor"/> fills each title block from its title, subtitle and number.</summary>
        public static Result Export(IFlowsheet fs, string cadcraftFolder, string outputFolder, string name, DrawingStyle style, Func<string, string, string, TitleData> titleFor)
        {
            var result = new Result();
            PlantModel model;
            try { model = PlantModelBuilder.Build(fs, name, PlantLayout.Load(fs), ExportOptions.Load()); }
            catch (Exception ex) { result.Error = "the plant model could not be built (" + ex.Message + ")"; return result; }

            Dictionary<string, (string Number, string Id)> lineNumbers;
            try { lineNumbers = ProcessFlowDiagram.LineNumbers(fs, model.Pipes.GroupBy(r => r.Tag).ToDictionary(g => g.Key, g => g.First())); }
            catch (Exception) { lineNumbers = new Dictionary<string, (string, string)>(); }

            var sheets = new List<Sheet>();
            var actuated = Instruments.ControlledValves(fs);
            foreach (var item in model.Equipment.Where(e => e.Visible && !e.IsInstrument && e.Name != null))
            {
                if (!fs.SimulationObjects.TryGetValue(item.Name, out var obj) || obj.GraphicObject == null) continue;
                if (Skip(obj.GraphicObject.ObjectType) || actuated.ContainsKey(obj.Name)) continue;
                try { sheets.Add(Equipment(fs, model, item, obj, name)); }
                catch (Exception ex) { result.Failed.Add(item.Tag + ": " + ex.Message); }
            }
            foreach (var loop in ProcessFlowDiagram.InstrumentLoops(fs))
            {
                try { sheets.Add(loop.Controller ? Loop(fs, model, loop.Obj, loop.Letter, loop.Loop, lineNumbers, name) : Indicator(fs, model, loop.Obj, loop.Letter, loop.Loop, lineNumbers, name)); }
                catch (Exception ex) { result.Failed.Add(loop.Obj.GraphicObject?.Tag + ": " + ex.Message); }
            }
            if (sheets.Count == 0) { result.Error = "the flowsheet has no equipment or instruments"; return result; }

            var temp = Path.Combine(Path.GetTempPath(), "dwsim-ds-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(temp);
            using (var session = new CADCraftSession(cadcraftFolder))
            {
                foreach (var sheet in sheets)
                {
                    try
                    {
                        var file = Draw(session, sheet, Path.Combine(outputFolder, name + " - DS " + Safe(sheet.Tag)), style, titleFor(sheet.Kind, sheet.Subtitle, sheet.Number), temp);
                        if (file != null)
                        {
                            result.Drawings.Add(file);
                            if (File.Exists(Path.ChangeExtension(file, ".pdf"))) result.Pdfs.Add(Path.ChangeExtension(file, ".pdf"));
                        }
                    }
                    catch (Exception ex) { result.Failed.Add(sheet.Tag + ": " + ex.Message); }
                }
                result.Failed.AddRange(session.Failed);
            }
            try { Directory.Delete(temp, true); } catch (Exception) { }
            if (result.Drawings.Count == 0 && result.Error == null) result.Error = "CADCraft did not save any data sheet" + (result.Failed.Count > 0 ? " (" + result.Failed.Last() + ")" : "");
            return result;
        }

        /// <summary>Mixers, splitters, recycles and the logical blocks get no data sheet.</summary>
        static bool Skip(ObjectType t)
        {
            switch (t)
            {
                case ObjectType.NodeIn:
                case ObjectType.NodeOut:
                case ObjectType.NodeEn:
                case ObjectType.OT_Recycle:
                case ObjectType.OT_EnergyRecycle:
                case ObjectType.OT_Adjust:
                case ObjectType.OT_Spec:
                    return true;
                default:
                    return t.ToString().StartsWith("Controller_") || t.ToString().StartsWith("Switch") || t.ToString().StartsWith("Input") || t.ToString().EndsWith("Gauge");
            }
        }

        static string Safe(string s) => string.Concat((s ?? "item").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

        static string Fmt(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            var a = Math.Abs(v);
            if (a == 0) return "0";
            if (a >= 1e6 || a < 1e-3) return v.ToString("0.00E+0", Inv);
            var dec = a >= 1000 ? 0 : a >= 100 ? 1 : a >= 10 ? 2 : a >= 1 ? 3 : 4;
            return Math.Round(v, dec).ToString(dec == 0 ? "0" : "0." + new string('#', dec), Inv);
        }

        static string F(double v, string format) => double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString(format, Inv);

        static double Si(Func<double> f)
        {
            try { var v = f(); return double.IsNaN(v) || double.IsInfinity(v) ? double.NaN : v; }
            catch (Exception) { return double.NaN; }
        }

        /// <summary>A public property of a unit operation, by name: a number, or null.</summary>
        static double? Number(object o, string property)
        {
            try
            {
                var v = o.GetType().GetProperty(property)?.GetValue(o);
                if (v is double d && !double.IsNaN(d) && !double.IsInfinity(d)) return d;
                if (v is int i) return i;
            }
            catch (Exception) { }
            return null;
        }

        static bool Flag(object o, string property)
        {
            try { return o.GetType().GetProperty(property)?.GetValue(o) is bool b && b; }
            catch (Exception) { return false; }
        }

        /// <summary>The streams attached to an item: material and energy, in and out.</summary>
        static List<(ISimulationObject Obj, bool In)> Attached(IFlowsheet fs, ISimulationObject obj)
        {
            var list = new List<(ISimulationObject, bool)>();
            var go = obj.GraphicObject;
            void Add(IGraphicObject other, bool inlet)
            {
                if (other != null && fs.SimulationObjects.TryGetValue(other.Name, out var o) && !list.Any(x => x.Item1 == o)) list.Add((o, inlet));
            }
            foreach (var cp in go.InputConnectors) if (cp.IsAttached) Add(cp.AttachedConnector?.AttachedFrom, true);
            foreach (var cp in go.OutputConnectors) if (cp.IsAttached) Add(cp.AttachedConnector?.AttachedTo, false);
            var ec = go.EnergyConnector;
            if (ec != null && ec.IsAttached)
            {
                var c = ec.AttachedConnector;
                if (c != null) Add(c.AttachedFrom == go ? c.AttachedTo : c.AttachedFrom, c.AttachedTo == go);
            }
            return list;
        }

        /// <summary>The operating conditions of a stream, in the units of the data sheets.</summary>
        static double TempC(IMaterialStream ms) => Si(() => ms.GetTemperature()) - 273.15;
        static double PressG(IMaterialStream ms) => Si(() => ms.GetPressure()) / 1e5 - 1.01325;
        static double VapFrac(IMaterialStream ms) => Si(() => ms.Phases[2].Properties.molarfraction.GetValueOrDefault());

        /// <summary>The design basis of an item: from the highest pressure and temperature of its streams, and the lowest temperature when below zero.</summary>
        static DesignBasis DesignOf(IEnumerable<IMaterialStream> streams)
        {
            var list = streams.ToList();
            var t = list.Select(TempC).Where(v => !double.IsNaN(v)).DefaultIfEmpty(double.NaN);
            var p = list.Select(PressG).Where(v => !double.IsNaN(v)).DefaultIfEmpty(double.NaN);
            return DesignBasis.For(t.Max(), p.Max(), t.Min());
        }

        static void AddDesign(Section s, DesignBasis d, bool vessel)
        {
            s.Add("Design pressure (bar g)", F(d.Pressure, "0.0"));
            s.Add("Design temperature (C)", d.Cold ? F(d.MinT, "0") + " / " + F(d.MaxT, "0") : F(d.MaxT, "0"));
            s.Add("Hydrotest pressure (bar g)", F(d.Test, "0.0"));
            s.Add(vessel ? "Shell and heads material" : "Casing material", vessel ? d.Plate : d.Casting);
            s.Add("Corrosion allowance (mm)", F(d.CorrosionAllowance, "0.0"));
            s.Add("Flange rating", d.Class + " RF");
        }

        /// <summary>Insulation for an item, as for the lines: heat conservation above 60 C, cold service below 0 C.</summary>
        static string Insulation(DesignBasis d)
        {
            var t = d.OperatingT;
            if (double.IsNaN(t)) return "none";
            if (t > 60) return F(t < 150 ? 40 : t < 250 ? 65 : 90, "0") + " mm, heat conservation";
            if (d.Cold) return F(t > -50 ? 50 : t > -100 ? 80 : 120, "0") + " mm, cold service";
            return "none";
        }

        static readonly double[] MotorSizes = { 0.37, 0.55, 0.75, 1.1, 1.5, 2.2, 3, 4, 5.5, 7.5, 11, 15, 18.5, 22, 30, 37, 45, 55, 75, 90, 110, 132, 160, 200, 250, 315, 355, 400, 450, 500, 560, 630, 710, 800, 900, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000 };

        /// <summary>The IEC motor size for a shaft power, with a 10 % margin.</summary>
        internal static string Motor(double kw)
        {
            if (double.IsNaN(kw) || kw <= 0) return "";
            var need = 1.1 * kw;
            var size = MotorSizes.FirstOrDefault(m => m >= need);
            return size > 0 ? Fmt(size) : Fmt(Math.Ceiling(need / 100) * 100);
        }

        /// <summary>An equipment data sheet.</summary>
        static Sheet Equipment(IFlowsheet fs, PlantModel model, EquipmentItem item, ISimulationObject obj, string flowsheetName)
        {
            var type = obj.GraphicObject.ObjectType;
            var sheet = new Sheet { Tag = item.Tag, Kind = "EQUIPMENT DATA SHEET", Number = "DS-" + item.Tag };
            string kind;
            try { kind = obj.GetDisplayName(); } catch (Exception) { kind = type.ToString(); }
            sheet.Subtitle = item.Tag + " - " + (kind ?? "").ToUpperInvariant();

            var attached = Attached(fs, obj);
            var streams = attached.Where(a => a.Obj is IMaterialStream).ToList();
            var energy = attached.Where(a => a.Obj is IEnergyStream).ToList();
            var d = DesignOf(streams.Select(a => (IMaterialStream)a.Obj));
            var vessel = item.Envelope != null && (item.Envelope.Kind == EnvelopeKind.VerticalCylinder || item.Envelope.Kind == EnvelopeKind.HorizontalCylinder);
            var rotating = type == ObjectType.Pump || type == ObjectType.Compressor || type == ObjectType.Expander || type == ObjectType.CompressorExpander;

            var general = new Section("GENERAL");
            general.Add("Item", item.Tag);
            general.Add("Type", kind);
            if (!string.IsNullOrWhiteSpace(item.Description) && item.Description != kind) general.Add("Service", item.Description);
            general.Add("Quantity", type == ObjectType.Pump ? "2 (1 operating + 1 spare)" : "1");
            general.Add("Simulation", flowsheetName);
            if (vessel) general.Add("Drawing", "GA-" + item.Tag);
            sheet.Sections.Add(general);

            // the operating conditions, stream by stream, five to a table
            for (var k = 0; k < streams.Count; k += 5)
            {
                var chunk = streams.Skip(k).Take(5).Select(a => (Ms: (IMaterialStream)a.Obj, a.In, Tag: a.Obj.GraphicObject?.Tag ?? a.Obj.Name)).ToList();
                var s = new Section(k == 0 ? "OPERATING CONDITIONS" : "OPERATING CONDITIONS (CONTINUED)")
                {
                    Header = chunk.Select(c => (c.In ? "IN " : "OUT ") + c.Tag).ToArray(),
                    Corner = "Stream"
                };
                void Row(string label, Func<IMaterialStream, double> f) => s.Rows.Add(new[] { label }.Concat(chunk.Select(c => Fmt(f(c.Ms)))).ToArray());
                Row("Temperature (C)", TempC);
                Row("Pressure (bar g)", PressG);
                Row("Mass flow (kg/h)", ms => Si(() => ms.GetMassFlow()) * 3600);
                Row("Vapor fraction (molar)", VapFrac);
                Row("Volumetric flow (m3/h)", ms => Si(() => ms.GetVolumetricFlow()) * 3600);
                Row("Liquid density (kg/m3)", ms => VapFrac(ms) < 0.9999 ? Si(() => ms.Phases[1].Properties.density.GetValueOrDefault()) : double.NaN);
                Row("Vapor density (kg/m3)", ms => VapFrac(ms) > 1e-4 ? Si(() => ms.Phases[2].Properties.density.GetValueOrDefault()) : double.NaN);
                Row("Liquid viscosity (cP)", ms => VapFrac(ms) < 0.9999 ? Si(() => ms.Phases[1].Properties.viscosity.GetValueOrDefault()) * 1000 : double.NaN);
                Row("Molar mass (kg/kmol)", ms => Si(() => ms.Phases[0].Properties.molecularWeight.GetValueOrDefault()));
                sheet.Sections.Add(s);
            }

            var process = new Section("PROCESS DATA");
            ProcessData(process, obj, type, streams);
            var column = type == ObjectType.DistillationColumn || type == ObjectType.AbsorptionColumn || type == ObjectType.RefluxedAbsorber || type == ObjectType.ReboiledAbsorber;
            foreach (var e in column ? energy.Take(0) : energy)
                process.Add("Energy stream " + e.Obj.GraphicObject?.Tag + " (kW)", Fmt(Math.Abs(Si(() => ((IEnergyStream)e.Obj).GetEnergyFlow()))));
            if (process.Rows.Count > 0) sheet.Sections.Add(process);

            var design = new Section("DESIGN");
            AddDesign(design, d, vessel);
            design.Add("Insulation", Insulation(d));
            sheet.Sections.Add(design);

            var mech = new Section(vessel ? "MECHANICAL" : rotating ? "DRIVER" : "CONSTRUCTION");
            if (vessel) Mechanical(mech, item);
            else if (rotating)
            {
                var power = Number(obj, "DeltaQ") ?? double.NaN;
                if (type == ObjectType.Expander) mech.Add("Driven machine", "Generator or compressor (to be defined)");
                else
                {
                    mech.Add("Driver", "Electric motor");
                    var rated = Motor(Math.Abs(power));
                    mech.Add("Rated power (kW, est.)", rated);
                    // low voltage up to 250 kW, medium voltage above
                    var kw = double.TryParse(rated, NumberStyles.Float, Inv, out var r) ? r : 0;
                    mech.Add("Supply", (kw <= 250 ? "400 V" : kw <= 3000 ? "6.6 kV" : "11 kV") + ", 3 phase, 50 Hz");
                }
            }
            // what else the 3D model knows of the item (tubes of an exchanger), numbers rounded
            var known = new[] { "Operating", "Orientation", "Heads", "Diameter", "Height", "Length", "Stages", "Service", "Power", "Volume", "Duty" };
            foreach (var kv in item.Properties.Where(kv => !known.Any(kv.Key.StartsWith)))
                mech.Add(kv.Key, double.TryParse(kv.Value, NumberStyles.Float, Inv, out var v) ? Fmt(v) : kv.Value);
            if (mech.Rows.Count > 0) sheet.Sections.Add(mech);

            var nozzles = EquipmentDrawings.Nozzles(model, item);
            if (nozzles.Count > 0)
            {
                var s = new Section("NOZZLES") { Header = new[] { "Size", "Rating", "Facing", "Service" }, Corner = "Mark", LabelWidth = 16 };
                foreach (var n in nozzles) s.Rows.Add(new[] { n.Mark, n.Size, d.Class, "RF", n.Service });
                sheet.Sections.Add(s);
            }

            var notes = new Section("NOTES") { LabelShare = 0.06 };
            notes.Add("1", "Preliminary data sheet from the DWSIM simulation, at its solved case.");
            notes.Add("2", "Design conditions, materials and flange rating are estimates from the operating conditions.");
            notes.Add("3", "Pressures in bar g, temperatures in C" + (nozzles.Count > 0 && vessel ? "; nozzle marks as on GA-" + item.Tag + "." : "."));
            sheet.Sections.Add(notes);
            return sheet;
        }

        /// <summary>What the simulation worked out for each kind of item.</summary>
        static void ProcessData(Section s, ISimulationObject obj, ObjectType type, List<(ISimulationObject Obj, bool In)> streams)
        {
            string N(string property, double factor = 1) { var v = Number(obj, property); return v.HasValue ? Fmt(v.Value * factor) : ""; }
            string Abs(string property) { var v = Number(obj, property); return v.HasValue ? Fmt(Math.Abs(v.Value)) : ""; }
            var inlet = streams.Where(a => a.In).Select(a => a.Obj).OfType<IMaterialStream>().FirstOrDefault();
            switch (type)
            {
                case ObjectType.Pump:
                    {
                        s.Add("Pump type", "Centrifugal");
                        s.Add("Rated flow (m3/h)", inlet == null ? "" : Fmt(1.1 * Si(() => inlet.GetVolumetricFlow()) * 3600));
                        s.Add("Differential pressure (bar)", N("DeltaP", 1e-5));
                        s.Add("Differential head (m)", N("Head"));
                        s.Add("Efficiency (%)", N("Efficiency"));
                        s.Add("Shaft power (kW)", Abs("DeltaQ"));
                        if (inlet != null)
                        {
                            var hyd = Si(() => inlet.GetVolumetricFlow()) * (Number(obj, "DeltaP") ?? double.NaN) / 1000;
                            s.Add("Hydraulic power (kW)", Fmt(hyd));
                        }
                        break;
                    }
                case ObjectType.Compressor:
                case ObjectType.Expander:
                case ObjectType.CompressorExpander:
                    s.Add("Machine type", type == ObjectType.Expander ? "Turboexpander" : "Centrifugal");
                    if (inlet != null) s.Add("Inlet flow (m3/h, actual)", Fmt(Si(() => inlet.GetVolumetricFlow()) * 3600));
                    { var dp = Number(obj, "DeltaP"); s.Add(type == ObjectType.Expander ? "Pressure drop (bar)" : "Differential pressure (bar)", dp.HasValue ? Fmt(Math.Abs(dp.Value) / 1e5) : ""); }
                    s.Add("Pressure ratio", N("PressureRatio"));
                    s.Add("Adiabatic efficiency (%)", N("AdiabaticEfficiency"));
                    s.Add("Polytropic efficiency (%)", N("PolytropicEfficiency"));
                    s.Add("Polytropic head (m)", N("PolytropicHead"));
                    s.Add(type == ObjectType.Expander ? "Power produced (kW)" : "Shaft power (kW)", Abs("DeltaQ"));
                    break;
                case ObjectType.Heater:
                case ObjectType.Cooler:
                case ObjectType.HeaterCooler:
                    s.Add("Duty (kW)", Abs("DeltaQ"));
                    s.Add("Pressure drop (bar)", N("DeltaP", 1e-5));
                    s.Add("Efficiency (%)", N("Efficiency"));
                    break;
                case ObjectType.HeatExchanger:
                    s.Add("Duty (kW)", Abs("Q"));
                    s.Add("Heat transfer area (m2)", N("Area"));
                    s.Add("Overall coefficient (W/m2.K)", N("OverallCoefficient"));
                    s.Add("LMTD (C)", N("LMTD"));
                    s.Add("Hot side pressure drop (bar)", N("HotSidePressureDrop", 1e-5));
                    s.Add("Cold side pressure drop (bar)", N("ColdSidePressureDrop", 1e-5));
                    break;
                case ObjectType.DistillationColumn:
                case ObjectType.AbsorptionColumn:
                case ObjectType.RefluxedAbsorber:
                case ObjectType.ReboiledAbsorber:
                    s.Add("Theoretical stages", N("NumberOfStages"));
                    var rr = Number(obj, "RefluxRatio");
                    if (rr.HasValue && rr.Value > 0) s.Add("Reflux ratio", Fmt(rr.Value));
                    s.Add("Condenser duty (kW)", Abs("CondenserDuty"));
                    s.Add("Reboiler duty (kW)", Abs("ReboilerDuty"));
                    break;
                case ObjectType.Valve:
                    s.Add("Pressure drop (bar)", N("DeltaP", 1e-5));
                    var kv = Number(obj, "ActualKv");
                    if (kv.HasValue && kv.Value > 0) s.Add("Kv (m3/h)", Fmt(kv.Value));
                    break;
                default:
                    if (type.ToString().StartsWith("RCT_"))
                    {
                        s.Add("Duty (kW)", Abs("DeltaQ"));
                        s.Add("Volume (m3)", N("Volume"));
                        s.Add("Residence time (s)", N("ResidenceTime"));
                    }
                    else
                    {
                        var q = Number(obj, "DeltaQ");
                        if (q.HasValue && Math.Abs(q.Value) > 0) s.Add("Duty or power (kW)", Fmt(Math.Abs(q.Value)));
                    }
                    break;
            }
        }

        /// <summary>The main dimensions of a vessel, column, tank or exchanger, from the 3D model.</summary>
        static void Mechanical(Section s, EquipmentItem item)
        {
            var env = item.Envelope;
            var vertical = env.Kind == EnvelopeKind.VerticalCylinder;
            var shell = item.Parts.FirstOrDefault(p => p.Name == "shell");
            var dia = shell?.Diameter ?? (vertical ? 2 * env.HalfX : env.ZTop - env.ZBottom);
            var tt = shell?.Height ?? double.NaN;
            item.Properties.TryGetValue("Heads", out var heads);
            s.Add("Orientation", vertical ? "Vertical" : "Horizontal");
            s.Add("Inside diameter (mm)", F(Math.Round(dia / 10) * 10, "0"));
            s.Add(vertical ? "Height T/T (mm)" : "Length T/T (mm)", F(Math.Round(tt / 10) * 10, "0"));
            s.Add("Heads", heads ?? "2:1 ellipsoidal");
            if (!double.IsNaN(tt))
            {
                var dm = dia / 1000; var head = (heads ?? "").StartsWith("Hemi") ? Math.PI * Math.Pow(dm, 3) / 12 : Math.PI * Math.Pow(dm, 3) / 24;
                s.Add("Volume (m3)", Fmt(Math.PI / 4 * dm * dm * tt / 1000 + 2 * head));
            }
            if (item.Parts.Any(p => p.Name == "skirt")) s.Add("Support", "Skirt");
            else if (item.Parts.Any(p => (p.Name ?? "").StartsWith("saddle"))) s.Add("Support", "Saddles");
            else if (item.Parts.Any(p => (p.Name ?? "").StartsWith("leg"))) s.Add("Support", "Legs");
            s.Add("Design code", "ASME VIII Div. 1");
        }

        /// <summary>What an instrument measures: the object, the property's name and where it is taken.</summary>
        static (ISimulationObject Obj, string Property, string Where) Measured(IFlowsheet fs, PlantModel model, string id, string property, Dictionary<string, (string Number, string Id)> lines)
        {
            if (string.IsNullOrEmpty(id) || !fs.SimulationObjects.TryGetValue(id, out var obj)) return (null, property, "");
            var name = Pretty(fs, property);
            var tag = obj.GraphicObject?.Tag ?? obj.Name;
            var where = obj is IMaterialStream && lines.TryGetValue(tag, out var ln) ? "Line " + ln.Number + " (stream " + tag + ")" : tag;
            return (obj, name, where);
        }

        /// <summary>A property's name in English, its identifier split into words when it has no translation (Stage_LiquidLevel_1: Stage Liquid Level 1).</summary>
        static string Pretty(IFlowsheet fs, string property)
        {
            var name = property ?? "";
            try { name = fs.GetTranslatedString(property, "en") ?? name; } catch (Exception) { }
            if (name.Contains(" ") && !name.Contains("_")) return name;
            name = System.Text.RegularExpressions.Regex.Replace(name.Replace('_', ' '), "(?<=[a-z])(?=[A-Z0-9])", " ");
            return System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim();
        }

        static double Value(ISimulationObject obj, string property, string units)
        {
            try
            {
                var v = Convert.ToDouble(obj.GetPropertyValue(property), Inv);
                return string.IsNullOrEmpty(units) ? v : Converter.ConvertFromSI(units, v);
            }
            catch (Exception) { return double.NaN; }
        }

        /// <summary>A round number at or above <paramref name="x"/>: 1, 1.5, 2, 2.5, 3, 4, 5, 6 or 8 times a power of ten.</summary>
        static double Nice(double x)
        {
            if (double.IsNaN(x) || x <= 0) return double.NaN;
            var e = Math.Pow(10, Math.Floor(Math.Log10(x)));
            foreach (var m in new[] { 1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10 }) if (m * e >= x - 1e-12) return m * e;
            return 10 * e;
        }

        static string Range(double sp, double pv, string units)
        {
            var hi = Nice(1.5 * Math.Max(Math.Abs(double.IsNaN(sp) ? 0 : sp), Math.Abs(double.IsNaN(pv) ? 0 : pv)));
            if (double.IsNaN(hi)) return "";
            var lo = Math.Min(double.IsNaN(sp) ? 0 : sp, double.IsNaN(pv) ? 0 : pv) < 0 ? -hi : 0;
            return Fmt(lo) + " to " + Fmt(hi) + (string.IsNullOrEmpty(units) ? "" : " " + units);
        }

        /// <summary>A control loop's data sheet: transmitter, controller and the control valve it moves.</summary>
        static Sheet Loop(IFlowsheet fs, PlantModel model, ISimulationObject obj, string letter, string loop, Dictionary<string, (string Number, string Id)> lines, string flowsheetName)
        {
            var c = (IAdjust)obj;
            var tagOf = letter + "IC-" + loop;
            var sheet = new Sheet { Tag = tagOf, Kind = "INSTRUMENT DATA SHEET", Number = "DS-" + tagOf, Subtitle = "LOOP " + letter + "-" + loop + " (" + obj.GraphicObject.Tag + ")" };
            var units = c.ControlledObjectData?.Units ?? "";
            var m = Measured(fs, model, c.ControlledObjectData?.ID, c.ControlledObjectData?.PropertyName, lines);
            var pv = m.Obj == null ? double.NaN : Value(m.Obj, c.ControlledObjectData.PropertyName, units);
            var sp = c.AdjustValue;

            var general = new Section("GENERAL");
            general.Add("Loop", letter + "-" + loop);
            general.Add("Controller in the simulation", obj.GraphicObject.Tag);
            general.Add("Controls", m.Property + (m.Obj != null ? " of " + (m.Obj.GraphicObject?.Tag ?? m.Obj.Name) : ""));
            general.Add("Simulation", flowsheetName);
            sheet.Sections.Add(general);

            var tx = new Section("TRANSMITTER");
            tx.Add("Tag", letter + "T-" + loop);
            tx.Add("Measured variable", m.Property + (units.Length > 0 ? " (" + units + ")" : ""));
            tx.Add("Location", m.Where);
            tx.Add("Operating value", Fmt(pv) + (units.Length > 0 && !double.IsNaN(pv) ? " " + units : ""));
            tx.Add("Range (est.)", Range(sp, pv, units));
            tx.Add("Output", "4-20 mA, HART");
            tx.Add("Housing", "Weatherproof, explosion proof (to be confirmed)");
            if (m.Obj is IMaterialStream ms)
            {
                var d = DesignBasis.For(TempC(ms), PressG(ms));
                tx.Add("Process connection", d.Class + " RF");
                tx.Add("Wetted parts", d.Cold && d.MinT < -46 ? "316L SS" : "316 SS");
            }
            sheet.Sections.Add(tx);

            var ctl = new Section("CONTROLLER");
            ctl.Add("Tag", tagOf);
            ctl.Add("Set point", Fmt(sp) + (units.Length > 0 && !double.IsNaN(sp) ? " " + units : ""));
            ctl.Add("Action", Flag(obj, "ReverseActing") ? "Reverse" : "Direct");
            var kp = Number(obj, "Kp");
            if (kp.HasValue)
            {
                ctl.Add("Algorithm", "PID");
                ctl.Add("Gain Kp", Fmt(kp.Value));
                ctl.Add("Integral Ki", Fmt(Number(obj, "Ki") ?? double.NaN));
                ctl.Add("Derivative Kd", Fmt(Number(obj, "Kd") ?? double.NaN));
            }
            ctl.Add("Location", "Control room, shared display (DCS)");
            sheet.Sections.Add(ctl);

            var mvId = c.ManipulatedObjectData?.ID;
            if (!string.IsNullOrEmpty(mvId) && fs.SimulationObjects.TryGetValue(mvId, out var mv))
            {
                if (mv.GraphicObject?.ObjectType == ObjectType.Valve)
                    sheet.Sections.Add(ControlValve(fs, model, mv, letter + "V-" + loop, lines));
                else
                {
                    var s = new Section("MANIPULATED VARIABLE");
                    var mvName = Pretty(fs, c.ManipulatedObjectData.PropertyName);
                    s.Add("Object", mv.GraphicObject?.Tag ?? mv.Name);
                    s.Add("Property", mvName);
                    var mu = c.ManipulatedObjectData.Units ?? "";
                    var v = Value(mv, c.ManipulatedObjectData.PropertyName, mu);
                    s.Add("Operating value", Fmt(v) + (mu.Length > 0 && !double.IsNaN(v) ? " " + mu : ""));
                    sheet.Sections.Add(s);
                }
            }

            var notes = new Section("NOTES") { LabelShare = 0.06 };
            notes.Add("1", "Preliminary data sheet from the DWSIM simulation, at its solved case.");
            notes.Add("2", "Ranges, ratings and the valve size are estimates; pressures in bar g, temperatures in C.");
            sheet.Sections.Add(notes);
            return sheet;
        }

        /// <summary>Globe valve bodies and their flow coefficient Kv (m3/h) wide open.</summary>
        static readonly (string Size, double Kv)[] Bodies =
        {
            ("1\"", 13), ("1.5\"", 30), ("2\"", 50), ("3\"", 110), ("4\"", 190), ("6\"", 420), ("8\"", 750), ("10\"", 1150), ("12\"", 1650), ("16\"", 2900)
        };

        /// <summary>
        /// A control valve's data: the flowing conditions from its streams and the flow coefficient
        /// after IEC 60534-2-1 (liquid, or gas with the expansion factor and choked at xT = 0.72),
        /// the body the smallest whose Kv wide open is 1.4 times the one needed (about 70 % open).
        /// </summary>
        static Section ControlValve(IFlowsheet fs, PlantModel model, ISimulationObject valve, string tag, Dictionary<string, (string Number, string Id)> lines)
        {
            var s = new Section("CONTROL VALVE");
            var vtag = valve.GraphicObject?.Tag ?? valve.Name;
            s.Add("Tag", tag + (string.Equals(tag, vtag, StringComparison.OrdinalIgnoreCase) ? "" : " (" + vtag + ")"));
            var attached = Attached(fs, valve);
            var inlet = attached.Where(a => a.In).Select(a => a.Obj).OfType<IMaterialStream>().FirstOrDefault();
            var outlet = attached.Where(a => !a.In).Select(a => a.Obj).OfType<IMaterialStream>().FirstOrDefault();
            if (inlet == null) return s;
            var itag = ((ISimulationObject)inlet).GraphicObject?.Tag ?? "";
            if (lines.TryGetValue(itag, out var ln)) s.Add("Line", ln.Number);
            var run = model.Pipes.FirstOrDefault(r => r.Tag == itag);

            var w = Si(() => inlet.GetMassFlow()) * 3600;
            var p1 = Si(() => inlet.GetPressure()) / 1e5;
            var p2 = outlet == null ? double.NaN : Si(() => outlet.GetPressure()) / 1e5;
            var dp = p1 - p2;
            var vf = VapFrac(inlet);
            var rho = Si(() => inlet.Phases[0].Properties.density.GetValueOrDefault());
            s.Add("Fluid phase", vf > 0.995 ? "Vapor" : vf < 0.005 ? "Liquid" : "Two-phase");
            s.Add("Flow (kg/h)", Fmt(w));
            s.Add("Inlet pressure (bar g)", Fmt(p1 - 1.01325));
            s.Add("Outlet pressure (bar g)", Fmt(p2 - 1.01325));
            s.Add("Pressure drop (bar)", Fmt(dp));
            s.Add("Inlet temperature (C)", Fmt(TempC(inlet)));
            s.Add("Inlet density (kg/m3)", Fmt(rho));

            double kv = double.NaN;
            var note = "";
            if (dp > 0 && rho > 0 && w > 0)
            {
                if (vf > 0.995)
                {
                    const double xT = 0.72;
                    var x = dp / p1;
                    if (x >= xT) { x = xT; note = " (choked)"; }
                    var y = 1 - x / (3 * xT);
                    kv = w / (31.6 * y * Math.Sqrt(x * p1 * rho));
                }
                else
                {
                    kv = w / rho * Math.Sqrt(rho / 1000 / dp);
                    if (vf >= 0.005) note = " (homogeneous, indicative)";
                }
            }
            s.Add("Required Kv (m3/h)", Fmt(kv) + (double.IsNaN(kv) ? "" : note));
            s.Add("Required Cv (US gpm)", Fmt(1.156 * kv));
            if (!double.IsNaN(kv))
            {
                var body = Bodies.FirstOrDefault(b => b.Kv >= 1.4 * kv);
                s.Add("Body size (est.)", body.Size ?? "larger than 16\"");
                if (body.Size != null) s.Add("Rated Kv (m3/h)", Fmt(body.Kv));
            }
            var d = DesignBasis.For(TempC(inlet), p1 - 1.01325);
            s.Add("Body", "Globe, " + d.Class + " RF, " + d.Flange);
            s.Add("Trim", "Equal percentage, 316 SS");
            s.Add("Actuator", "Pneumatic diaphragm, with positioner");
            s.Add("Fail position", "To be defined");
            return s;
        }

        /// <summary>A local indicator's data sheet.</summary>
        static Sheet Indicator(IFlowsheet fs, PlantModel model, ISimulationObject obj, string letter, string loop, Dictionary<string, (string Number, string Id)> lines, string flowsheetName)
        {
            var g = (IIndicator)obj;
            var tag = letter + "I-" + loop;
            var sheet = new Sheet { Tag = tag, Kind = "INSTRUMENT DATA SHEET", Number = "DS-" + tag, Subtitle = "LOCAL INDICATOR " + tag + " (" + obj.GraphicObject.Tag + ")" };
            var m = Measured(fs, model, g.SelectedObjectID, g.SelectedProperty, lines);
            var units = g.SelectedPropertyUnits ?? "";
            var u = units.Length > 0 ? " " + units : "";

            var s = new Section("INDICATOR");
            s.Add("Tag", tag);
            s.Add("Gauge in the simulation", obj.GraphicObject.Tag);
            s.Add("Measured variable", m.Property + (units.Length > 0 ? " (" + units + ")" : ""));
            s.Add("Location", m.Where);
            // the gauge keeps the value of its last dynamic step: the measured one when it can be read
            var pv = m.Obj == null ? double.NaN : Value(m.Obj, g.SelectedProperty, units);
            s.Add("Operating value", Fmt(double.IsNaN(pv) ? g.CurrentValue : pv) + u);
            if (g.MaximumValue > g.MinimumValue) s.Add("Range", Fmt(g.MinimumValue) + " to " + Fmt(g.MaximumValue) + u);
            foreach (var (label, v) in new[] { ("Low low alarm", g.VeryLowAlarmValue), ("Low alarm", g.LowAlarmValue), ("High alarm", g.HighAlarmValue), ("High high alarm", g.VeryHighAlarmValue) })
                if (v != 0) s.Add(label, Fmt(v) + u);
            s.Add("Type", letter == "P" ? "Bourdon tube gauge, 100 mm dial" : letter == "T" ? "Bimetallic thermometer in thermowell, 100 mm dial" : letter == "L" ? "Level gauge" : "Local indicator");
            s.Add("Simulation", flowsheetName);
            sheet.Sections.Add(s);
            if (m.Obj is IMaterialStream ms)
            {
                var d = DesignBasis.For(TempC(ms), PressG(ms));
                var p = new Section("PROCESS CONNECTION");
                p.Add("Design pressure (bar g)", F(d.Pressure, "0.0"));
                p.Add("Design temperature (C)", d.Cold ? F(d.MinT, "0") + " / " + F(d.MaxT, "0") : F(d.MaxT, "0"));
                p.Add("Connection", d.Class + " RF");
                sheet.Sections.Add(p);
            }
            var notes = new Section("NOTES") { LabelShare = 0.06 };
            notes.Add("1", "Preliminary data sheet from the DWSIM simulation, at its solved case.");
            sheet.Sections.Add(notes);
            return sheet;
        }

        /// <summary>One data sheet: its tables in two columns, the left one full height, the right one over the title block.</summary>
        static string Draw(CADCraftSession session, Sheet sheet, string outputBase, DrawingStyle style, TitleData title, string temp)
        {
            DrawingSheets.Margins(style, out var ml, out var mo);
            var size = SheetSize.For(style).First();
            DrawingSheets.TitleBlock(style, out var titleHeight);
            var rightX = size.Width - mo - DrawingSheets.TitleWidth;
            var columns = new[]
            {
                (X: ml + 5, W: rightX - 10 - (ml + 5), Top: size.Height - mo - 5, Bottom: mo + 5),
                (X: rightX, W: DrawingSheets.TitleWidth, Top: size.Height - mo - 5, Bottom: mo + titleHeight + 5)
            };

            File.Delete(outputBase + ".dxf");
            if (session.Tool("new_drawing", new JObject { ["metric"] = true }, "new drawing for " + sheet.Tag) == null) return null;
            void C(string n, JObject p) => session.Execute(n, p);
            C("style", new JObject { ["name"] = DrawingSheets.TextStyle, ["font"] = DrawingSheets.Font, ["current"] = true });
            foreach (var (ln, lw) in new[] { ("TABLE", 0.25), ("BORDER", 0.7), ("TITLE", 0.35) })
                C("layer.new", new JObject { ["name"] = ln, ["color"] = "white", ["linetype"] = "Continuous", ["lineweight"] = lw });
            C("layout.new", new JObject { ["name"] = "DS", ["viewport"] = false });
            C("pagesetup", new JObject
            {
                ["layout"] = "DS", ["device"] = "CADCraft PDF", ["width"] = size.Width, ["height"] = size.Height, ["landscape"] = true,
                ["margins"] = new JArray(0, 0, 0, 0), ["plotArea"] = "layout", ["scale"] = 1, ["lineweights"] = true, ["plotStyleTable"] = "monochrome.ctb"
            });
            C("layout.set", new JObject { ["name"] = "DS" });
            C("pspace", new JObject());
            var border = Path.Combine(temp, "border.dxf");
            DrawingSheets.Border(style, size).Save(border);
            C("layer.current", new JObject { ["name"] = "BORDER" });
            C("insert", new JObject { ["name"] = "SHEET_BORDER", ["file"] = border.Replace('\\', '/'), ["at"] = new JArray(0, 0) });
            var tb = Path.Combine(temp, "title.dxf");
            DrawingSheets.TitleBlock(style, out _).Save(tb);
            title.Scale = "NONE";
            C("layer.current", new JObject { ["name"] = "TITLE" });
            C("insert", new JObject
            {
                ["name"] = "TITLE_BLOCK", ["file"] = tb.Replace('\\', '/'), ["at"] = new JArray(size.Width - mo, mo),
                ["attribs"] = JObject.FromObject(DrawingSheets.Attributes(title, size, style))
            });

            C("tablestyle", new JObject { ["name"] = "DS", ["textHeight"] = TextHeight, ["margin"] = 0.8, ["current"] = true });
            foreach (var cs in new[] { "Title", "Header", "Data" })
                C("tablestyle.cellstyle", new JObject { ["style"] = "DS", ["name"] = cs, ["textStyle"] = DrawingSheets.TextStyle, ["textHeight"] = cs == "Title" ? 3.0 : TextHeight });
            C("layer.current", new JObject { ["name"] = "TABLE" });

            // the tables go down the left column, then down the right one; one that fits in neither
            // is cut short at the foot of the right column
            var col = 0;
            var y = columns.Select(c => c.Top).ToArray();
            foreach (var s in sheet.Sections)
            {
                if (s.Rows.Count == 0) continue;
                var need = s.Lines * RowHeight;
                while (col < columns.Length - 1 && y[col] - need < columns[col].Bottom) col++;
                var room = (int)Math.Floor((y[col] - columns[col].Bottom) / RowHeight) - 1 - (s.Header != null ? 1 : 0);
                if (room < 1) break;
                var rows = s.Rows.Count > room ? s.Rows.Take(room - 1).Concat(new[] { new[] { "...", "" } }).ToList() : s.Rows;
                Table(session, s, rows, columns[col].X, y[col], columns[col].W);
                y[col] -= (1 + (s.Header != null ? 1 : 0) + rows.Count) * RowHeight + Gap;
            }

            foreach (var ext in new[] { ".dxf", ".dwg", ".pdf", ".png" }) if (File.Exists(outputBase + ext)) File.Delete(outputBase + ext);
            session.Execute("plot", new JObject { ["path"] = outputBase + ".pdf", ["layout"] = "DS" }, 300);
            session.Execute("plot", new JObject { ["path"] = outputBase + ".png", ["layout"] = "DS", ["device"] = "CADCraft PNG" }, 300);
            session.Tool("save", new JObject { ["path"] = outputBase + ".dwg" }, "save DWG of " + sheet.Tag);
            session.Tool("save", new JObject { ["path"] = outputBase + ".dxf" }, "save DXF of " + sheet.Tag);
            return File.Exists(outputBase + ".dxf") ? outputBase + ".dxf" : null;
        }

        /// <summary>
        /// A table: its title across the top, the header row of a grid, then the rows, the labels in
        /// a column of their own, the texts cut to their cells.
        /// </summary>
        static void Table(CADCraftSession session, Section s, List<string[]> rows, double x, double top, double width)
        {
            var values = s.Header?.Length ?? 1;
            var labelW = s.LabelWidth > 0 ? s.LabelWidth : s.Header != null ? Math.Min(52, 0.32 * width) : Math.Round(s.LabelShare * width, 1);
            var colW = (width - labelW) / values;
            string Cut(string t, double w)
            {
                var n = (int)Math.Floor((w - 1.6) / (0.72 * TextHeight));
                t = t ?? "";
                return t.Length <= n ? t : t.Substring(0, Math.Max(1, n - 1)) + ".";
            }
            var cells = new JArray(rows.Select(r => new JArray(Enumerable.Range(1, values).Select(i => Cut(i < r.Length ? r[i] : "", colW)))));
            var o = new JObject
            {
                ["at"] = new JArray(Math.Round(x, 2), Math.Round(top, 2)), ["rows"] = rows.Count, ["cols"] = values,
                ["rowHeight"] = RowHeight, ["colWidth"] = Math.Round(colW, 2), ["title"] = s.Title, ["cells"] = cells
            };
            if (s.Header != null) o["header"] = new JArray(s.Header.Select(h => Cut(h, colW)));
            var made = session.Execute("table", o);
            var th = made == null ? null : JObject.Parse(made)["handle"]?.ToString();
            if (th == null || session.Execute("table.insertcol", new JObject { ["handle"] = th, ["col"] = 0, ["width"] = labelW }) == null) return;
            var first = 1;
            if (s.Header != null) { session.Execute("table.set", new JObject { ["handle"] = th, ["row"] = 1, ["col"] = 0, ["text"] = s.Corner }); first = 2; }
            for (var r = 0; r < rows.Count; r++)
                session.Execute("table.set", new JObject { ["handle"] = th, ["row"] = first + r, ["col"] = 0, ["text"] = Cut(rows[r][0], labelW) });
            session.Execute("table.merge", new JObject { ["handle"] = th, ["row"] = 0, ["col"] = 0, ["rows"] = 1, ["cols"] = values + 1 });
            session.Execute("table.set", new JObject { ["handle"] = th, ["row"] = 0, ["col"] = 0, ["text"] = s.Title });
        }
    }
}
