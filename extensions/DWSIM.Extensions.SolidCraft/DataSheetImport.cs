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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.SharedClasses.SystemsOfUnits;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>A value read off a data sheet.</summary>
    public class SheetField
    {
        public string Label = "", Key = "", Value = "", Unit = "", Case = "", Side = "", Stream = "";
        public double? Number;
        public bool Include = true;

        public bool Inlet => Regex.IsMatch(Stream ?? "", @"\b(in|inlet|entrada|suction|sucç[aã]o)\b", RegexOptions.IgnoreCase);
        public bool Outlet => Regex.IsMatch(Stream ?? "", @"\b(out|outlet|sa[ií]da|discharge|descarga)\b", RegexOptions.IgnoreCase);

        /// <summary>The stream's name in the column heading, without the side and "inlet" or "outlet".</summary>
        public string StreamName => Regex.Replace(Stream ?? "", @"\b(shell|tube|casco|tubo|inlet|outlet|in|out|entrada|sa[ií]da|suction|discharge)\b", "", RegexOptions.IgnoreCase).Trim();
    }

    /// <summary>A performance curve read off a data sheet.</summary>
    public class SheetCurve
    {
        public string Type = "", XUnit = "", YUnit = "";
        public double? Speed, Impeller;
        public List<double[]> Points = new List<double[]>();
    }

    /// <summary>What was read off an equipment data sheet.</summary>
    public class DataSheetReading
    {
        public string Source = "", EquipmentType = "other", Tag = "", Service = "";
        public List<string> Cases = new List<string>();
        public List<SheetField> Fields = new List<SheetField>();
        public List<SheetCurve> Curves = new List<SheetCurve>();
        public Dictionary<string, string> TitleBlock = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> Notes = new List<string>();
        public byte[] Overview;

        static string Str(JToken t) => t == null || t.Type == JTokenType.Null ? null : t.ToString().Trim();

        static double? Num(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return null;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return (double)t;
            var m = Regex.Match(t.ToString().Replace(",", "."), @"[-+]?\d+(\.\d+)?([eE][-+]?\d+)?");
            return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
        }

        public static DataSheetReading FromJson(string json)
        {
            var o = JObject.Parse(json);
            var r = new DataSheetReading
            {
                Source = Str(o["source"]) ?? "",
                EquipmentType = (Str(o["equipment_type"]) ?? "other").ToLowerInvariant().Replace(" ", "_"),
                Tag = Str(o["tag"]) ?? "",
                Service = Str(o["service"]) ?? "",
            };
            foreach (var c in (o["cases"] as JArray) ?? new JArray()) if (Str(c) is string v && v.Length > 0) r.Cases.Add(v);
            foreach (var f in (o["fields"] as JArray) ?? new JArray())
            {
                var field = new SheetField
                {
                    Label = Str(f["label"]) ?? "", Key = (Str(f["key"]) ?? "").ToLowerInvariant(), Value = Str(f["value"]) ?? "",
                    Number = Num(f["number"]), Unit = Str(f["unit"]) ?? "", Case = Str(f["case"]) ?? "", Side = (Str(f["side"]) ?? "").ToLowerInvariant(),
                    Stream = Str(f["stream"]) ?? "",
                };
                if (!field.Number.HasValue && Regex.IsMatch(field.Value, @"^\s*[-+]?\d")) field.Number = Num(f["value"]);
                if (f["include"] != null && f["include"].Type == JTokenType.Boolean) field.Include = (bool)f["include"];
                r.Fields.Add(field);
            }
            foreach (var c in (o["curves"] as JArray) ?? new JArray())
            {
                var curve = new SheetCurve { Type = (Str(c["type"]) ?? "").ToLowerInvariant(), XUnit = Str(c["x_unit"]) ?? "", YUnit = Str(c["y_unit"]) ?? "", Speed = Num(c["speed_rpm"]), Impeller = Num(c["impeller_mm"]) };
                foreach (var p in (c["points"] as JArray) ?? new JArray())
                    if (p is JArray a && a.Count >= 2 && Num(a[0]).HasValue && Num(a[1]).HasValue) curve.Points.Add(new[] { Num(a[0]).Value, Num(a[1]).Value });
                if (curve.Points.Count > 1) r.Curves.Add(curve);
            }
            if (o["title_block"] is JObject tb) foreach (var p in tb.Properties()) if (Str(p.Value) is string v && v.Length > 0) r.TitleBlock[p.Name] = v;
            foreach (var t in (o["notes"] as JArray) ?? new JArray()) if (Str(t) is string v && v.Length > 0) r.Notes.Add(v);
            if (Str(o["overview"]) is string png && png.Length > 0) try { r.Overview = Convert.FromBase64String(png); } catch (FormatException) { }
            // the operating cases: the columns operating values are given in (a design value's minimum and maximum are not cases)
            bool Operating(SheetField f) => f.Key.Length > 0 && !f.Key.StartsWith("design");
            if (r.Cases.Count == 0) r.Cases.AddRange(r.Fields.Where(Operating).Select(f => f.Case).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
            r.Cases.RemoveAll(c => !r.Fields.Any(f => Operating(f) && string.Equals(f.Case, c, StringComparison.OrdinalIgnoreCase)));
            return r;
        }

        public string ToJson()
        {
            JToken N(double? v) => v.HasValue && !double.IsNaN(v.Value) ? (JToken)v.Value : JValue.CreateNull();
            return new JObject
            {
                ["kind"] = "datasheet", ["source"] = Source, ["equipment_type"] = EquipmentType, ["tag"] = Tag, ["service"] = Service,
                ["cases"] = new JArray(Cases),
                ["fields"] = new JArray(Fields.Select(f => new JObject
                {
                    ["label"] = f.Label, ["key"] = f.Key, ["value"] = f.Value, ["number"] = N(f.Number), ["unit"] = f.Unit, ["case"] = f.Case,
                    ["side"] = f.Side, ["stream"] = f.Stream, ["include"] = f.Include,
                })),
                ["curves"] = new JArray(Curves.Select(c => new JObject
                {
                    ["type"] = c.Type, ["x_unit"] = c.XUnit, ["y_unit"] = c.YUnit, ["speed_rpm"] = N(c.Speed), ["impeller_mm"] = N(c.Impeller),
                    ["points"] = new JArray(c.Points.Select(p => new JArray(p[0], p[1]))),
                })),
                ["title_block"] = new JObject(TitleBlock.Select(kv => new JProperty(kv.Key, kv.Value))),
                ["notes"] = new JArray(Notes),
                ["overview"] = Overview == null ? "" : Convert.ToBase64String(Overview),
            }.ToString();
        }
    }

    /// <summary>A change the data sheet makes to the equipment, offered to the user before it is made.</summary>
    public class SheetChange
    {
        public string What = "", Current = "", New = "";
        public bool Use = true;
        public Action Apply;
        public override string ToString() => What + ": " + Current + " > " + New;
    }

    /// <summary>
    /// Sets equipment up from its data sheet: the sheet is read by the AI Assistant into labelled
    /// values with standard keys, and the values the equipment's model takes become changes the
    /// user ticks (efficiencies, pressures, curves, the exchanger's shell and tube geometry, the
    /// vessel's dimensions, the control valve's flow coefficient and opening). The sheet's
    /// operating conditions check the simulation; its own numbers check each other.
    /// </summary>
    public static class DataSheetImport
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The kinds of equipment the import sets up, and the DWSIM objects each can go into.</summary>
        public static ObjectType[] TypesFor(string equipmentType)
        {
            switch (equipmentType)
            {
                case "pump": return new[] { ObjectType.Pump };
                case "compressor": return new[] { ObjectType.Compressor };
                case "expander": return new[] { ObjectType.Expander };
                case "heat_exchanger": return new[] { ObjectType.HeatExchanger };
                case "vessel": return new[] { ObjectType.Vessel };
                case "control_valve": return new[] { ObjectType.Valve };
                default: return new ObjectType[0];
            }
        }

        public static bool Supported(string equipmentType) => TypesFor(equipmentType).Length > 0;

        #region values

        /// <summary>A value in SI from the unit written on the sheet; null when the unit is not known for the quantity.</summary>
        public static double? ToSI(double v, string unit, string quantity)
        {
            var u = (unit ?? "").Trim().Replace("°", "").Replace("º", "").Replace("²", "2").Replace("³", "3").Replace("·", ".");
            var uc = u.Replace(" ", "").ToLowerInvariant();
            switch (quantity)
            {
                case "length":
                    if (uc == "mm" || uc == "") return v / 1000;
                    if (uc == "m") return v;
                    if (uc == "cm") return v / 100;
                    if (uc == "in" || uc == "\"" || uc == "inch") return v * 0.0254;
                    if (uc == "ft") return v * 0.3048;
                    return null;
                case "fraction":
                    if (uc == "%" || uc == "") return v > 1.0 ? v / 100 : v;
                    return v;
                case "speed":
                    return v;
                case "area":
                    if (uc == "m2" || uc == "") return v;
                    if (uc == "ft2") return v * 0.09290304;
                    return null;
                case "u":
                    if (uc == "w/m2.k" || uc == "w/m2k" || uc == "w/m2.c" || uc == "w/(m2.k)" || uc == "") return v;
                    if (uc.StartsWith("kcal/h.m2") || uc.StartsWith("kcal/(h.m2")) return v * 1.163;
                    if (uc.StartsWith("btu/h.ft2") || uc.StartsWith("btu/(h.ft2")) return v * 5.678263;
                    return null;
                case "fouling":
                    if (uc == "m2.k/w" || uc == "m2k/w" || uc == "m2.c/w" || uc == "") return v;
                    if (uc.StartsWith("h.m2") && uc.Contains("kcal")) return v / 1.163;
                    if (uc.StartsWith("h.ft2") && uc.Contains("btu")) return v / 5.678263;
                    return null;
                case "deltat":
                    if (uc == "c" || uc == "k" || uc == "") return v;
                    if (uc == "f" || uc == "r") return v / 1.8;
                    return null;
                case "head":
                    if (uc == "m" || uc == "") return v;
                    if (uc == "ft") return v * 0.3048;
                    return null;
            }
            var unitName = DrawingImport.Unit(u) ?? "";
            if (quantity == "pressure" && unitName.Length == 0) unitName = "bar";
            if (quantity == "deltap" && unitName.Length == 0) unitName = "bar";
            if (quantity == "deltap") unitName = Regex.Replace(unitName, "(?<=[a-z0-9])g$", "");
            if (quantity == "temperature" && unitName.Length == 0) unitName = "C";
            if (quantity == "power")
            {
                // in W: DWSIM's own SI unit for power and duty is kW
                if (unitName.Equals("hp", StringComparison.OrdinalIgnoreCase)) return v * 745.7;
                if (uc == "w") return v;
                try { return Converter.ConvertToSI(unitName.Length == 0 ? "kW" : unitName, v) * 1000; } catch (Exception) { return null; }
            }
            try { return Converter.ConvertToSI(unitName, v); } catch (Exception) { return null; }
        }

        /// <summary>The fields of a key in the chosen case: those of the case, else those of no case, else any.</summary>
        static List<SheetField> Of(DataSheetReading r, string key, string @case, Func<SheetField, bool> where = null)
        {
            var all = r.Fields.Where(f => f.Include && f.Number.HasValue && f.Key == key && (where == null || where(f))).ToList();
            var c = all.Where(f => string.Equals(f.Case, @case ?? "", StringComparison.OrdinalIgnoreCase)).ToList();
            if (c.Count > 0) return c;
            c = all.Where(f => f.Case.Length == 0).ToList();
            return c.Count > 0 ? c : all;
        }

        /// <summary>A value in SI: the first field of the key in the case.</summary>
        public static double? SI(DataSheetReading r, string key, string quantity, string @case, Func<SheetField, bool> where = null)
        {
            foreach (var f in Of(r, key, @case, where))
            {
                var v = ToSI(f.Number.Value, f.Unit, quantity);
                if (v.HasValue) return v;
            }
            return null;
        }

        /// <summary>A text field of the key ("" when none).</summary>
        public static string Text(DataSheetReading r, string key, Func<SheetField, bool> where = null) =>
            r.Fields.Where(f => f.Include && f.Key == key && (where == null || where(f))).Select(f => f.Value).FirstOrDefault() ?? "";

        /// <summary>
        /// An operating condition at the inlet or the outlet: the inlet_/outlet_ key, or the plain
        /// quantity in a column headed by an inlet or an outlet stream.
        /// </summary>
        public static double? Operating(DataSheetReading r, string quantity, bool inlet, string unitQuantity, string @case, string stream = null)
        {
            var key = (inlet ? "inlet_" : "outlet_") + quantity;
            Func<SheetField, bool> pick = f => stream == null || f.StreamName.IndexOf(stream, StringComparison.OrdinalIgnoreCase) >= 0;
            var v = SI(r, key, unitQuantity, @case, f => pick(f) && (f.Stream.Length == 0 || (inlet ? !f.Outlet : !f.Inlet)));
            if (v.HasValue) return v;
            return SI(r, quantity, unitQuantity, @case, f => pick(f) && (inlet ? f.Inlet : f.Outlet));
        }

        /// <summary>The case to use by default: rated for machines, else normal, else the first.</summary>
        public static string DefaultCase(DataSheetReading r)
        {
            string Find(string name) => r.Cases.FirstOrDefault(c => c.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            return (r.EquipmentType == "pump" || r.EquipmentType == "compressor" ? Find("rated") : null) ?? Find("normal") ?? r.Cases.FirstOrDefault() ?? "";
        }

        static string F(double v, string format = "G4") => double.IsNaN(v) ? "" : v.ToString(format, Inv);

        #endregion

        #region targets

        /// <summary>The flowsheet's objects the sheet can go into, the one with the sheet's tag first.</summary>
        public static List<ISimulationObject> Targets(IFlowsheet fs, DataSheetReading r)
        {
            var types = TypesFor(r.EquipmentType);
            var tags = Regex.Matches(r.Tag + " " + Text(r, "tag"), @"[A-Za-z0-9][A-Za-z0-9\-_.]*").Cast<Match>().Select(m => m.Value).ToList();
            return fs.SimulationObjects.Values.Where(o => types.Contains(o.GraphicObject.ObjectType))
                .OrderByDescending(o => tags.Any(t => string.Equals(t, o.GraphicObject.Tag, StringComparison.OrdinalIgnoreCase)))
                .ThenBy(o => o.GraphicObject.Tag).ToList();
        }

        /// <summary>The material streams attached to an object: inlets, then outlets.</summary>
        static List<IMaterialStream> Attached(IFlowsheet fs, ISimulationObject obj, bool inlet)
        {
            var list = new List<IMaterialStream>();
            var cps = inlet ? obj.GraphicObject.InputConnectors : obj.GraphicObject.OutputConnectors;
            foreach (var cp in cps)
            {
                if (!cp.IsAttached || cp.AttachedConnector == null) continue;
                var other = inlet ? cp.AttachedConnector.AttachedFrom : cp.AttachedConnector.AttachedTo;
                if (other != null && fs.SimulationObjects.TryGetValue(other.Name, out var o) && o is IMaterialStream ms) list.Add(ms);
            }
            return list;
        }

        #endregion

        #region changes

        /// <summary>The changes the sheet makes to the object, in the chosen case.</summary>
        public static List<SheetChange> Changes(IFlowsheet fs, DataSheetReading r, ISimulationObject obj, string @case, List<ImportIssue> issues)
        {
            var list = new List<SheetChange>();
            void Add(string what, string current, string value, Action apply, bool use = true) =>
                list.Add(new SheetChange { What = what, Current = current, New = value, Apply = apply, Use = use });
            var tag = obj.GraphicObject.Tag;
            var inlets = Attached(fs, obj, true);
            switch (obj)
            {
                case Pump pump:
                    {
                        var eff = SI(r, "efficiency", "fraction", @case);
                        if (eff.HasValue) Add("Efficiency (%)", F(pump.Eficiencia.GetValueOrDefault()), F(eff.Value * 100), () => pump.Eficiencia = eff.Value * 100);
                        var dp = SI(r, "differential_pressure", "deltap", @case);
                        var head = SI(r, "differential_head", "head", @case);
                        var rho = SI(r, "density", "density", @case) ?? (inlets.Count > 0 ? inlets[0].Phases[0].Properties.density : null);
                        if (!dp.HasValue && head.HasValue && rho.HasValue && rho > 0) dp = head.Value * rho.Value * 9.80665;
                        var curves = r.Curves.Where(c => c.Type.Contains("head") || c.Type.Contains("eff") || c.Type.Contains("power") || c.Type.Contains("npsh")).ToList();
                        if (dp.HasValue)
                            Add("Pressure rise (bar), fixed", F(pump.DeltaP.GetValueOrDefault() / 1e5), F(dp.Value / 1e5),
                                () => { pump.CalcMode = Pump.CalculationMode.Delta_P; pump.DeltaP = dp.Value; }, curves.Count == 0);
                        if (curves.Any(c => c.Type.Contains("head")))
                            Add("Performance curves", pump.CalcMode == Pump.CalculationMode.Curves ? "curves" : pump.CalcMode.ToString(),
                                string.Join(", ", curves.Select(c => c.Type + " (" + c.Points.Count + " points)")), () => PumpCurves(pump, curves, r, @case));
                        break;
                    }
                case Compressor comp:
                    {
                        var ad = SI(r, "adiabatic_efficiency", "fraction", @case);
                        var po = SI(r, "polytropic_efficiency", "fraction", @case);
                        if (ad.HasValue) Add("Adiabatic efficiency (%)", F(comp.AdiabaticEfficiency), F(ad.Value * 100), () => { comp.AdiabaticEfficiency = ad.Value * 100; if (!po.HasValue) comp.ProcessPath = Compressor.ProcessPathType.Adiabatic; });
                        if (po.HasValue) Add("Polytropic efficiency (%)", F(comp.PolytropicEfficiency), F(po.Value * 100), () => { comp.PolytropicEfficiency = po.Value * 100; if (!ad.HasValue) comp.ProcessPath = Compressor.ProcessPathType.Polytropic; });
                        var pout = OutletPressure(r, @case, inlets);
                        if (pout.HasValue) Add("Outlet pressure (bar)", F(comp.POut / 1e5), F(pout.Value / 1e5), () => { comp.CalcMode = Compressor.CalculationMode.OutletPressure; comp.POut = pout.Value; });
                        var speed = SI(r, "speed", "speed", @case);
                        if (speed.HasValue) Add("Speed (rpm)", comp.Speed.ToString(Inv), F(speed.Value, "0"), () => comp.Speed = (int)Math.Round(speed.Value));
                        break;
                    }
                case Expander exp:
                    {
                        var ad = SI(r, "adiabatic_efficiency", "fraction", @case);
                        var po = SI(r, "polytropic_efficiency", "fraction", @case);
                        if (ad.HasValue) Add("Adiabatic efficiency (%)", F(exp.AdiabaticEfficiency), F(ad.Value * 100), () => { exp.AdiabaticEfficiency = ad.Value * 100; if (!po.HasValue) exp.ProcessPath = Expander.ProcessPathType.Adiabatic; });
                        if (po.HasValue) Add("Polytropic efficiency (%)", F(exp.PolytropicEfficiency), F(po.Value * 100), () => { exp.PolytropicEfficiency = po.Value * 100; if (!ad.HasValue) exp.ProcessPath = Expander.ProcessPathType.Polytropic; });
                        var pout = OutletPressure(r, @case, inlets);
                        if (pout.HasValue) Add("Outlet pressure (bar)", F(exp.POut / 1e5), F(pout.Value / 1e5), () => { exp.CalcMode = Expander.CalculationMode.OutletPressure; exp.POut = pout.Value; });
                        var speed = SI(r, "speed", "speed", @case);
                        if (speed.HasValue) Add("Speed (rpm)", exp.Speed.ToString(Inv), F(speed.Value, "0"), () => exp.Speed = (int)Math.Round(speed.Value));
                        break;
                    }
                case HeatExchanger hx:
                    Exchanger(fs, r, hx, @case, inlets, Add, issues);
                    break;
                case Vessel ve:
                    {
                        var d = SI(r, "inside_diameter", "length", @case);
                        var l = SI(r, "length_tangent_to_tangent", "length", @case);
                        var orient = Text(r, "orientation");
                        var heads = Text(r, "head_type");
                        var extra = (IDictionary<string, object>)ve.ExtraProperties;
                        if (d.HasValue) Add("Inside diameter (mm)", F(ve.Dimensions[0].Value, "0"), F(d.Value * 1000, "0"), () => { ve.Dimensions[0].Value = d.Value * 1000; ve.Dimensions[0].IsUserDefined = true; });
                        if (l.HasValue) Add("Length tangent to tangent (m)", F(ve.Dimensions[1].Value, "0.###"), F(l.Value, "0.###"), () => { ve.Dimensions[1].Value = l.Value; ve.Dimensions[1].IsUserDefined = true; });
                        var head = HeadType(heads);
                        if (head != null) Add("Heads", ve.HeadType, head, () => ve.HeadType = head);
                        if (orient.Length > 0)
                        {
                            var horizontal = orient.ToLowerInvariant().StartsWith("h");
                            Add("Orientation", extra.TryGetValue("Vessel Orientation", out var o0) && Convert.ToDouble(o0, Inv) == 1 ? "horizontal" : "vertical",
                                horizontal ? "horizontal" : "vertical", () => extra["Vessel Orientation"] = horizontal ? 1.0 : 0.0);
                        }
                        if (d.HasValue && l.HasValue)
                            Add("Volume and liquid height from the dimensions", "", "on", () => { extra["Get Volume from Dimensions"] = true; extra["Get Height from Dimensions"] = true; });
                        break;
                    }
                case Valve v:
                    Valve(fs, r, v, obj, @case, Add, issues);
                    break;
            }
            if (list.Count == 0) issues.Add(new ImportIssue(ImportIssue.Levels.Warning, tag, "nothing on the sheet sets up a " + obj.GraphicObject.ObjectType));
            return list;
        }

        static double? OutletPressure(DataSheetReading r, string @case, List<IMaterialStream> inlets)
        {
            var pout = Operating(r, "pressure", false, "pressure", @case);
            if (pout.HasValue) return pout;
            var ratio = SI(r, "pressure_ratio", "speed", @case);
            var pin = Operating(r, "pressure", true, "pressure", @case) ?? (inlets.Count > 0 ? (double?)inlets[0].GetPressure() : null);
            if (ratio.HasValue && pin.HasValue) return pin.Value * ratio.Value;
            var dp = SI(r, "differential_pressure", "deltap", @case);
            return dp.HasValue && pin.HasValue ? pin.Value + dp.Value : (double?)null;
        }

        static void PumpCurves(Pump pump, List<SheetCurve> curves, DataSheetReading r, string @case)
        {
            var set = pump.PumpCurveSet;
            Curve Fill(Curve target, SheetCurve c, string yDefault)
            {
                target.X = c.Points.OrderBy(p => p[0]).Select(p => p[0]).ToList();
                target.Y = c.Points.OrderBy(p => p[0]).Select(p => p[1]).ToList();
                target.xunit = DrawingImport.Unit(c.XUnit) ?? "m3/h";
                target.yunit = c.YUnit.Trim().Length == 0 ? yDefault : (c.YUnit.Trim() == "%" ? "%" : DrawingImport.Unit(c.YUnit) ?? yDefault);
                target.Enabled = true;
                return target;
            }
            foreach (var c in curves)
            {
                if (c.Type.Contains("head")) Fill(set.CurveHead, c, "m");
                else if (c.Type.Contains("eff")) Fill(set.CurveEfficiency, c, "%");
                else if (c.Type.Contains("power")) Fill(set.CurvePower, c, "kW");
                else if (c.Type.Contains("npsh")) Fill(set.CurveNPSHr, c, "m");
            }
            var speed = curves.Select(c => c.Speed).FirstOrDefault(s => s.HasValue) ?? SI(r, "speed", "speed", @case);
            var imp = curves.Select(c => c.Impeller).FirstOrDefault(s => s.HasValue) ?? (SI(r, "impeller_diameter", "length", @case) * 1000);
            if (speed.HasValue) set.ImpellerSpeed = speed.Value;
            if (imp.HasValue) { set.ImpellerDiameter = imp.Value; set.ImpellerDiameterUnit = "mm"; }
            pump.CalcMode = Pump.CalculationMode.Curves;
        }

        static string HeadType(string written)
        {
            var h = (written ?? "").ToLowerInvariant();
            if (h.Length == 0) return null;
            if (h.Contains("ellip") || h.Contains("2:1")) return "Ellipsoidal (2:1)";
            if (h.Contains("hemi")) return "Hemispherical";
            if (h.Contains("80") && h.Contains("10")) return "Torispherical (80:10 F&D)";
            if (h.Contains("asme")) return "Torispherical (ASME F&D)";
            if (h.Contains("tori") || h.Contains("f&d") || h.Contains("dished")) return "Torispherical (Standard F&D)";
            if (h.Contains("flat") || h.Contains("plan")) return "Flat";
            return null;
        }

        /// <summary>
        /// A shell and tube exchanger: its geometry in shell and tube rating, the hot fluid on the
        /// side the sheet puts it; without the geometry, the area and the overall coefficient.
        /// </summary>
        static void Exchanger(IFlowsheet fs, DataSheetReading r, HeatExchanger hx, string @case, List<IMaterialStream> inlets, Action<string, string, string, Action, bool> add, List<ImportIssue> issues)
        {
            void Add(string what, string current, string value, Action apply) => add(what, current, value, apply, true);
            var st = hx.STProperties;
            var shellId = SI(r, "shell_inside_diameter", "length", @case);
            var tubes = SI(r, "tubes_per_shell", "speed", @case);
            var od = SI(r, "tube_outside_diameter", "length", @case);
            var wall = SI(r, "tube_wall", "length", @case);
            var id = SI(r, "tube_inside_diameter", "length", @case);
            var length = SI(r, "tube_length", "length", @case);
            var geometry = shellId.HasValue && tubes.HasValue && od.HasValue && length.HasValue;
            if (geometry)
            {
                // which side the hot fluid is on: as the sheet says, else by the inlet the side's fluid names
                var shellText = (Text(r, "fluid", f => f.Side == "shell") + " " + r.Fields.Where(f => f.Side == "shell" || f.Label.ToLowerInvariant().StartsWith("shell side")).Select(f => f.Value).FirstOrDefault()).ToLowerInvariant();
                int? shellHot = null;
                if (shellText.Contains("(hot)") || Regex.IsMatch(shellText, @"\bhot\b")) shellHot = 1;
                else if (shellText.Contains("(cold)") || Regex.IsMatch(shellText, @"\bcold\b")) shellHot = 0;
                else if (inlets.Count == 2)
                {
                    var hot = inlets[0].GetTemperature() > inlets[1].GetTemperature() ? inlets[0] : inlets[1];
                    var hotTag = ((ISimulationObject)(object)hot).GraphicObject.Tag;
                    if (shellText.Contains(hotTag.ToLowerInvariant())) shellHot = 1;
                    else if (inlets.Any(i => shellText.Contains(((ISimulationObject)(object)i).GraphicObject.Tag.ToLowerInvariant()))) shellHot = 0;
                }
                var mode = hx.CalculationMode;
                Add("Calculation mode", mode.ToString(), "shell and tube rating", () => hx.CalculationMode = HeatExchangerCalcMode.ShellandTube_Rating);
                if (shellHot.HasValue) Add("Hot fluid in the", st.Shell_Fluid == 1 ? "shell" : "tubes", shellHot == 1 ? "shell" : "tubes", () => { st.Shell_Fluid = shellHot.Value; st.Tube_Fluid = 1 - shellHot.Value; });
                else issues.Add(new ImportIssue(ImportIssue.Levels.Warning, hx.GraphicObject.Tag, "the sheet does not tell which fluid goes in the shell; check the hot fluid's side"));
                var tema = Text(r, "tema_type").ToUpperInvariant().Replace(" ", "");
                var temaMatch = Regex.Match(tema, @"\b([A-D N])([EFGHJKX])([L-W])\b".Replace(" ", ""));
                if (temaMatch.Success)
                {
                    var t = temaMatch.Value;
                    Add("TEMA type", st.TEMA_FrontHeadType + (st.Shell_NumberOfPasses >= 2 ? "F" : "E") + st.TEMA_RearHeadType, t, () =>
                    {
                        if (Enum.TryParse(t.Substring(0, 1), out DWSIM.UnitOperations.UnitOperations.TEMAFrontHeadType front)) st.TEMA_FrontHeadType = front;
                        if (Enum.TryParse(t.Substring(2, 1), out DWSIM.UnitOperations.UnitOperations.TEMARearHeadType rear)) st.TEMA_RearHeadType = rear;
                        st.Shell_NumberOfPasses = t[1] == 'F' ? 2 : 1;
                    });
                }
                var shells = SI(r, "shells_in_series", "speed", @case);
                if (shells.HasValue) Add("Shells in series", st.Shell_NumberOfShellsInSeries.ToString(Inv), F(shells.Value, "0"), () => st.Shell_NumberOfShellsInSeries = Math.Max(1, (int)Math.Round(shells.Value)));
                Add("Shell inside diameter (mm)", F(st.Shell_Di, "0"), F(shellId.Value * 1000, "0"), () => st.Shell_Di = shellId.Value * 1000);
                Add("Tubes per shell", st.Tube_NumberPerShell.ToString(Inv), F(tubes.Value, "0"), () => st.Tube_NumberPerShell = (int)Math.Round(tubes.Value));
                var di = id ?? (wall.HasValue ? od.Value - 2 * wall.Value : (double?)null);
                Add("Tube OD (mm)", F(st.Tube_De, "0.##"), F(od.Value * 1000, "0.##"), () => st.Tube_De = od.Value * 1000);
                if (di.HasValue) Add("Tube ID (mm)", F(st.Tube_Di, "0.##"), F(di.Value * 1000, "0.##"), () => st.Tube_Di = di.Value * 1000);
                Add("Tube length (m)", F(st.Tube_Length, "0.###"), F(length.Value, "0.###"), () => st.Tube_Length = length.Value);
                var passes = SI(r, "tube_passes", "speed", @case);
                if (passes.HasValue) Add("Tube passes per shell", st.Tube_PassesPerShell.ToString(Inv), F(passes.Value, "0"), () => st.Tube_PassesPerShell = Math.Max(1, (int)Math.Round(passes.Value)));
                var pitch = SI(r, "tube_pitch", "length", @case);
                if (pitch.HasValue) Add("Tube pitch (mm)", F(st.Tube_Pitch, "0.##"), F(pitch.Value * 1000, "0.##"), () => st.Tube_Pitch = pitch.Value * 1000);
                var layout = (Text(r, "tube_layout") + " " + r.Fields.Where(f => f.Key == "tube_pitch").Select(f => f.Value).FirstOrDefault()).ToLowerInvariant();
                int? lay = layout.Contains("rot") && layout.Contains("square") ? 3 : layout.Contains("square") ? 2 : layout.Contains("rot") && layout.Contains("tri") ? 1 : layout.Contains("tri") ? 0 : (int?)null;
                if (lay.HasValue) Add("Tube layout", st.Tube_Layout.ToString(Inv), lay.Value.ToString(Inv) + (lay <= 1 ? " (triangular" : " (square") + (lay == 1 || lay == 3 ? ", rotated)" : ")"), () => st.Tube_Layout = lay.Value);
                var cut = SI(r, "baffle_cut", "fraction", @case);
                if (cut.HasValue) Add("Baffle cut (%)", F(st.Shell_BaffleCut, "0"), F(cut.Value * 100, "0"), () => st.Shell_BaffleCut = cut.Value * 100);
                var spacing = SI(r, "baffle_spacing", "length", @case);
                if (spacing.HasValue) Add("Baffle spacing (mm)", F(st.Shell_BaffleSpacing, "0"), F(spacing.Value * 1000, "0"), () => st.Shell_BaffleSpacing = spacing.Value * 1000);
                var fs_ = SI(r, "fouling", "fouling", @case, f => f.Side == "shell");
                var ft = SI(r, "fouling", "fouling", @case, f => f.Side == "tube");
                if (fs_.HasValue) Add("Shell fouling (m2.K/W)", F(st.Shell_Fouling), F(fs_.Value), () => st.Shell_Fouling = fs_.Value);
                if (ft.HasValue) Add("Tube fouling (m2.K/W)", F(st.Tube_Fouling), F(ft.Value), () => st.Tube_Fouling = ft.Value);
            }
            var area = SI(r, "area", "area", @case);
            var u = SI(r, "overall_coefficient", "u", @case);
            if (geometry)
            {
                // the alternative to the rating, not ticked: the outlet temperatures from U and the area
                if (area.HasValue && u.HasValue)
                {
                    add("Alternative: calculation mode", hx.CalculationMode.ToString(), "outlet temperatures from U and A", () => hx.CalculationMode = HeatExchangerCalcMode.CalcBothTemp_UA, false);
                    add("Alternative: area (m2)", F(hx.Area.GetValueOrDefault()), F(area.Value), () => hx.Area = area.Value, false);
                    add("Alternative: overall coefficient (W/m2.K)", F(hx.OverallCoefficient.GetValueOrDefault()), F(u.Value), () => hx.OverallCoefficient = u.Value, false);
                }
            }
            else if (area.HasValue && u.HasValue)
            {
                Add("Calculation mode", hx.CalculationMode.ToString(), "outlet temperatures from U and A", () => hx.CalculationMode = HeatExchangerCalcMode.CalcBothTemp_UA);
                Add("Area (m2)", F(hx.Area.GetValueOrDefault()), F(area.Value), () => hx.Area = area.Value);
                Add("Overall coefficient (W/m2.K)", F(hx.OverallCoefficient.GetValueOrDefault()), F(u.Value), () => hx.OverallCoefficient = u.Value);
            }
            else issues.Add(new ImportIssue(ImportIssue.Levels.Warning, hx.GraphicObject.Tag, "the sheet gives neither the shell and tube geometry nor the area and the overall coefficient"));
        }

        /// <summary>
        /// A control valve: its rated flow coefficient, its characteristic, and its opening: as the
        /// sheet writes it, else the one that brings the valve's outlet to the sheet's outlet
        /// pressure (the valve's own equations, choked and flashing flow included), else the one
        /// that passes the required coefficient.
        /// </summary>
        static void Valve(IFlowsheet fs, DataSheetReading r, Valve v, ISimulationObject obj, string @case, Action<string, string, string, Action, bool> add, List<ImportIssue> issues)
        {
            void Add(string what, string current, string value, Action apply) => add(what, current, value, apply, true);
            double? Coef(params string[] keys)
            {
                foreach (var k in keys)
                {
                    var x = SI(r, k, "speed", @case);
                    if (x.HasValue) return k.EndsWith("cv") ? x / 1.156 : x;
                }
                return null;
            }
            var rated = Coef("rated_kv", "rated_cv", "kv", "cv");
            var required = Coef("required_kv", "required_cv");
            if (!rated.HasValue && required.HasValue) { rated = required; required = null; }
            if (!required.HasValue)
            {
                // the coefficient the sheet's operating point needs, when the sheet does not give it
                required = RequiredKv(r, @case);
                if (required.HasValue) issues.Add(new ImportIssue(ImportIssue.Levels.Info, v.GraphicObject.Tag, string.Format(Inv, "the operating point on the sheet needs Kv {0:G4} m3/h", required)));
            }
            if (!rated.HasValue) { issues.Add(new ImportIssue(ImportIssue.Levels.Warning, v.GraphicObject.Tag, "the sheet gives no Kv or Cv")); return; }
            var cvSheet = r.Fields.Any(f => f.Include && (f.Key == "cv" || f.Key == "rated_cv")) && !r.Fields.Any(f => f.Include && (f.Key == "kv" || f.Key == "rated_kv"));
            // a liquid that flashes across the valve chokes it: on its flow coefficient the valve could
            // not let the pressure down to the sheet's, so it keeps its pressure specification
            var vfIn = SI(r, "vapor_fraction", "fraction", @case, f => f.Inlet || f.Key.StartsWith("inlet"));
            var vfOut = SI(r, "vapor_fraction", "fraction", @case, f => f.Outlet || f.Key.StartsWith("outlet"));
            var flashing = vfIn.HasValue && vfOut.HasValue && vfIn < 0.005 && vfOut > 0.005;
            if (flashing)
                issues.Add(new ImportIssue(ImportIssue.Levels.Warning, v.GraphicObject.Tag, "the liquid flashes across the valve and the flow chokes: the valve keeps its pressure specification; tick the flow coefficient changes for dynamics only"));
            var mode = v.CalcMode;
            add("Calculation mode", v.CalcMode.ToString(), "flow coefficient (any phase)", () => v.CalcMode = DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode.Kv_General, !flashing);
            Add(cvSheet ? "Rated Cv" : "Rated Kv (m3/h)", F(v.Kv), F(cvSheet ? rated.Value * 1.156 : rated.Value), () =>
            {
                v.FlowCoefficient = cvSheet ? DWSIM.UnitOperations.UnitOperations.Valve.FlowCoefficientType.Cv : DWSIM.UnitOperations.UnitOperations.Valve.FlowCoefficientType.Kv;
                v.Kv = cvSheet ? rated.Value * 1.156 : rated.Value;
            });
            var ch = Text(r, "characteristic").ToLowerInvariant();
            var type = ch.Contains("equal") || ch.Contains("%") ? DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType.EqualPercentage :
                ch.Contains("linear") ? DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType.Linear :
                ch.Contains("quick") ? DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType.QuickOpening :
                DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType.Linear;
            var range = SI(r, "rangeability", "speed", @case) ?? v.CharacteristicParameter;
            var opening = SI(r, "opening", "fraction", @case);
            var p2 = Operating(r, "pressure", false, "pressure", @case);
            var fit = !opening.HasValue && p2.HasValue && Attached(fs, obj, true).Count == 1 && Attached(fs, obj, false).Count == 1;
            if (!opening.HasValue && required.HasValue && rated > 0)
            {
                var x = required.Value / rated.Value;
                opening = type == DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType.EqualPercentage && range > 1 ? 1 + Math.Log(x) / Math.Log(range) :
                    type == DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType.QuickOpening ? x * x : x;
                opening = Math.Max(1e-5, Math.Min(1, opening.Value));
            }
            if (ch.Length > 0 || opening.HasValue || fit)
                Add("Characteristic", v.EnableOpeningKvRelationship ? v.DefinedOpeningKvRelationShipType.ToString() : "none", type.ToString(), () =>
                {
                    v.EnableOpeningKvRelationship = true;
                    v.DefinedOpeningKvRelationShipType = type;
                    if (type == DWSIM.UnitOperations.UnitOperations.Valve.OpeningKvRelationshipType.EqualPercentage) v.CharacteristicParameter = range;
                });
            if (fit)
            {
                var about = opening.HasValue ? string.Format(Inv, " (about {0:0.##})", opening * 100) : "";
                var guess = opening;
                add("Opening (%)", F(v.OpeningPct, "0.#"), "to reach the sheet's outlet pressure" + about, () =>
                {
                    var op = v.CalcMode == mode && mode != DWSIM.UnitOperations.UnitOperations.Valve.CalculationMode.Kv_General ? null : FitOpening(fs, v, obj, p2.Value);
                    v.OpeningPct = op ?? (guess.HasValue ? guess.Value * 100 : v.OpeningPct);
                    if (op.HasValue && (op < 10 || op > 95))
                        issues.Add(new ImportIssue(ImportIssue.Levels.Warning, v.GraphicObject.Tag, string.Format(Inv, "the valve runs {0:0.##} % open at the sheet's operating point: check the rated Kv", op)));
                    else if (op.HasValue)
                        issues.Add(new ImportIssue(ImportIssue.Levels.Info, v.GraphicObject.Tag, string.Format(Inv, "opening {0:0.#} % brings the outlet to the sheet's pressure", op)));
                    else if (v.CalcMode != mode)
                    {
                        // the valve cannot get there on its coefficient: back to its pressure specification
                        v.CalcMode = mode;
                        issues.Add(new ImportIssue(ImportIssue.Levels.Warning, v.GraphicObject.Tag, "on its flow coefficient the valve cannot reach the sheet's outlet pressure (choked flow, or the rated Kv): it keeps its pressure specification"));
                    }
                }, !flashing);
            }
            else if (opening.HasValue)
            {
                add("Opening (%)", F(v.OpeningPct, "0.#"), F(opening.Value * 100, "0.##"), () => v.OpeningPct = opening.Value * 100, !flashing);
                if (opening < 0.1 || opening > 0.95)
                    issues.Add(new ImportIssue(ImportIssue.Levels.Warning, v.GraphicObject.Tag, string.Format(Inv, "the valve runs {0:0.##} % open at the sheet's operating point: check the rated Kv", opening * 100)));
            }
        }

        /// <summary>
        /// The opening (%) at which the valve, solved on its own with its inlet as it stands, lets
        /// its outlet down to the pressure given: bisection on the opening's logarithm, the valve's
        /// "capacity too small" counting as too far closed. Null when the valve cannot get there.
        /// </summary>
        static double? FitOpening(IFlowsheet fs, Valve v, ISimulationObject obj, double p2)
        {
            var outs = Attached(fs, obj, false);
            if (outs.Count == 0) return null;
            double P(double op)
            {
                v.OpeningPct = op;
                try { obj.Calculate(); return outs[0].GetPressure(); }
                catch (Exception) { return double.NaN; }
            }
            double lo = 1e-4, hi = 100;
            var top = P(hi);
            if (double.IsNaN(top) || top < p2) return null;
            for (var i = 0; i < 60 && hi / lo > 1.0002; i++)
            {
                var mid = Math.Sqrt(lo * hi);
                var pm = P(mid);
                if (double.IsNaN(pm) || pm < p2) lo = mid; else hi = mid;
            }
            return hi;
        }

        /// <summary>
        /// The Kv (m3/h) the sheet's operating point needs: from the flow, the pressure drop and the
        /// inlet density (IEC 60534, liquid; gas with the expansion factor at xT 0.72); null when
        /// the sheet lacks one of them or the inlet is two-phase.
        /// </summary>
        public static double? RequiredKv(DataSheetReading r, string @case)
        {
            var w = Operating(r, "mass_flow", true, "massflow", @case) ?? SI(r, "mass_flow", "massflow", @case);
            var p1 = Operating(r, "pressure", true, "pressure", @case);
            var p2 = Operating(r, "pressure", false, "pressure", @case);
            var dp = SI(r, "pressure_drop", "deltap", @case) ?? (p1.HasValue && p2.HasValue ? p1 - p2 : null);
            var rho = SI(r, "density", "density", @case, f => !f.Outlet);
            var vf = SI(r, "vapor_fraction", "fraction", @case, f => !f.Outlet) ?? 0.0;
            if (!w.HasValue || !dp.HasValue || !rho.HasValue || dp <= 0 || rho <= 0) return null;
            var wh = w.Value * 3600;
            if (vf < 0.005) return wh / rho.Value * Math.Sqrt(rho.Value / 1000 / (dp.Value / 1e5));
            if (vf > 0.995 && p1.HasValue)
            {
                const double xT = 0.72;
                var x = Math.Min(xT, dp.Value / p1.Value);
                return wh / (31.6 * (1 - x / (3 * xT)) * Math.Sqrt(x * p1.Value / 1e5 * rho.Value));
            }
            return null;
        }

        #endregion

        #region checks

        /// <summary>
        /// The sheet's numbers against each other (the pump's power and head, the exchanger's area
        /// and duty, the valve's coefficients) and against the simulation's operating point.
        /// </summary>
        public static List<ImportIssue> Check(IFlowsheet fs, DataSheetReading r, ISimulationObject obj, string @case)
        {
            var issues = new List<ImportIssue>();
            var tag = obj?.GraphicObject.Tag ?? r.Tag;
            void Compare(string what, double sheet, double other, double tolerance, string unit, string against)
            {
                if (double.IsNaN(sheet) || double.IsNaN(other) || Math.Abs(sheet) < 1e-12) return;
                var off = Math.Abs(other - sheet) / Math.Abs(sheet);
                issues.Add(new ImportIssue(off > tolerance ? ImportIssue.Levels.Warning : ImportIssue.Levels.Info, tag,
                    string.Format(Inv, "{0}: {1:G4} {2} on the sheet, {3:G4} {2} {4}", what, sheet, unit, other, against)));
            }
            if (!Supported(r.EquipmentType))
                issues.Add(new ImportIssue(ImportIssue.Levels.Warning, tag, "a " + r.EquipmentType.Replace("_", " ") + " data sheet: only pumps, compressors, expanders, shell and tube exchangers, vessels and control valves are set up"));
            if (r.Cases.Count > 1) issues.Add(new ImportIssue(ImportIssue.Levels.Info, tag, "cases on the sheet: " + string.Join(", ", r.Cases) + "; using " + (@case.Length > 0 ? @case : "the values of no case")));

            switch (r.EquipmentType)
            {
                case "pump":
                    {
                        var q = SI(r, "rated_flow", "volumeflow", @case) ?? SI(r, "volume_flow", "volumeflow", @case);
                        var dp = SI(r, "differential_pressure", "deltap", @case);
                        var head = SI(r, "differential_head", "head", @case);
                        var rho = SI(r, "density", "density", @case);
                        var eff = SI(r, "efficiency", "fraction", @case);
                        var shaft = SI(r, "shaft_power", "power", @case);
                        var hyd = SI(r, "hydraulic_power", "power", @case);
                        if (dp.HasValue && head.HasValue && rho.HasValue) Compare("pressure rise", dp.Value / 1e5, head.Value * rho.Value * 9.80665 / 1e5, 0.03, "bar", "from the head and the density");
                        if (hyd.HasValue && shaft.HasValue && eff.HasValue) Compare("shaft power", shaft.Value / 1000, hyd.Value / eff.Value / 1000, 0.05, "kW", "from the hydraulic power and the efficiency");
                        else if (q.HasValue && dp.HasValue && eff.HasValue && shaft.HasValue) Compare("shaft power", shaft.Value / 1000, q.Value * dp.Value / eff.Value / 1000, 0.1, "kW", "from the flow, the pressure rise and the efficiency (rated flow)");
                        break;
                    }
                case "heat_exchanger":
                    {
                        var area = SI(r, "area", "area", @case);
                        var tubes = SI(r, "tubes_per_shell", "speed", @case);
                        var od = SI(r, "tube_outside_diameter", "length", @case);
                        var length = SI(r, "tube_length", "length", @case);
                        var shells = SI(r, "shells_in_series", "speed", @case) ?? 1;
                        if (area.HasValue && tubes.HasValue && od.HasValue && length.HasValue)
                            Compare("area", area.Value, tubes.Value * Math.PI * od.Value * length.Value * shells, 0.15, "m2", "from the tubes");
                        var duty = SI(r, "duty", "power", @case);
                        var u = SI(r, "overall_coefficient", "u", @case);
                        var lmtd = SI(r, "lmtd", "deltat", @case);
                        if (duty.HasValue && u.HasValue && area.HasValue && lmtd.HasValue) Compare("duty", duty.Value / 1000, u.Value * area.Value * lmtd.Value / 1000, 0.15, "kW", "from U, the area and the LMTD (no F correction)");
                        break;
                    }
                case "control_valve":
                    {
                        double? Coef(string kv, string cv) => SI(r, kv, "speed", @case) ?? SI(r, cv, "speed", @case) / 1.156;
                        var rated = Coef("rated_kv", "rated_cv") ?? Coef("kv", "cv");
                        var required = Coef("required_kv", "required_cv") ?? RequiredKv(r, @case);
                        if (rated.HasValue && required.HasValue && required > rated)
                            issues.Add(new ImportIssue(ImportIssue.Levels.Warning, tag, string.Format(Inv, "the required Kv ({0:G4}) is above the rated Kv ({1:G4}): the valve cannot pass the flow", required, rated)));
                        break;
                    }
            }

            // the simulation's operating point against the sheet's
            if (obj != null)
            {
                var ins = Attached(fs, obj, true);
                var outs = Attached(fs, obj, false);
                foreach (var pair in new[] { (ins, true), (outs, false) })
                {
                    if (pair.Item1.Count != 1) continue;
                    var ms = pair.Item1[0];
                    var where = pair.Item2 ? "inlet" : "outlet";
                    try
                    {
                        var t = Operating(r, "temperature", pair.Item2, "temperature", @case);
                        var p = Operating(r, "pressure", pair.Item2, "pressure", @case);
                        var w = Operating(r, "mass_flow", pair.Item2, "massflow", @case) ?? SI(r, "mass_flow", "massflow", @case);
                        if (t.HasValue) Compare(where + " temperature", t.Value - 273.15, ms.GetTemperature() - 273.15, 0.01, "C", "in the simulation");
                        if (p.HasValue) Compare(where + " pressure", p.Value / 1e5, ms.GetPressure() / 1e5, 0.02, "bar", "in the simulation");
                        if (w.HasValue) Compare(where + " mass flow", w.Value * 3600, ms.GetMassFlow() * 3600, 0.02, "kg/h", "in the simulation");
                    }
                    catch (Exception) { }
                }
            }
            return issues;
        }

        /// <summary>
        /// The operating conditions after the changes: the outlet the simulation gives against the
        /// sheet's, once the flowsheet is solved.
        /// </summary>
        public static List<ImportIssue> CompareOutlet(IFlowsheet fs, DataSheetReading r, ISimulationObject obj, string @case)
        {
            var list = new List<ImportIssue>();
            var outs = Attached(fs, obj, false);
            if (outs.Count == 0) return list;
            foreach (var ms in outs)
            {
                var name = ((ISimulationObject)(object)ms).GraphicObject.Tag;
                var stream = outs.Count > 1 ? name : null;
                var t = Operating(r, "temperature", false, "temperature", @case, stream);
                var p = Operating(r, "pressure", false, "pressure", @case, stream);
                try
                {
                    if (t.HasValue)
                    {
                        var d = ms.GetTemperature() - t.Value;
                        list.Add(new ImportIssue(Math.Abs(d) > 1 ? ImportIssue.Levels.Warning : ImportIssue.Levels.Info, name,
                            string.Format(Inv, "outlet temperature {0:0.0} C, the sheet {1:0.0} C", ms.GetTemperature() - 273.15, t.Value - 273.15)));
                    }
                    if (p.HasValue)
                    {
                        var d = (ms.GetPressure() - p.Value) / p.Value;
                        list.Add(new ImportIssue(Math.Abs(d) > 0.02 ? ImportIssue.Levels.Warning : ImportIssue.Levels.Info, name,
                            string.Format(Inv, "outlet pressure {0:0.###} bar, the sheet {1:0.###} bar", ms.GetPressure() / 1e5, p.Value / 1e5)));
                    }
                }
                catch (Exception) { }
            }
            return list;
        }

        #endregion

        #region new equipment

        /// <summary>
        /// A new object for the sheet, with its inlet and outlet streams; the inlets take the
        /// operating case's temperature, pressure and flow, and a fluid named after one of the
        /// flowsheet's compounds as its composition.
        /// </summary>
        public static ISimulationObject Create(IFlowsheet fs, DataSheetReading r, string @case, List<ImportIssue> issues)
        {
            var types = TypesFor(r.EquipmentType);
            if (types.Length == 0) throw new InvalidOperationException("a " + r.EquipmentType + " data sheet cannot make a new object");
            var tag = string.IsNullOrWhiteSpace(r.Tag) ? r.EquipmentType.ToUpperInvariant() : r.Tag.Trim();
            if (fs.GetFlowsheetSimulationObject(tag) != null) tag += " (" + (fs.SimulationObjects.Count + 1) + ")";
            var obj = fs.AddObject(types[0], 300, 300, tag);
            var go = obj.GraphicObject;
            var pp = fs.PropertyPackages.Values.FirstOrDefault();
            if (pp != null) try { obj.PropertyPackage = pp; } catch (Exception) { }
            var two = types[0] == ObjectType.HeatExchanger;
            var inletNames = r.Fields.Where(f => f.Inlet && f.StreamName.Length > 0).Select(f => f.StreamName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var outletNames = r.Fields.Where(f => f.Outlet && f.StreamName.Length > 0).Select(f => f.StreamName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            for (var i = 0; i < (two ? 2 : 1); i++)
            {
                var name = i < inletNames.Count ? inletNames[i] : tag + (two ? "-IN" + (i + 1) : "-IN");
                var ms = fs.AddObject(ObjectType.MaterialStream, (int)go.X - 120, (int)go.Y + 40 * i, FreeTag(fs, name));
                if (pp != null) try { ms.PropertyPackage = pp; } catch (Exception) { }
                fs.ConnectObjects(ms.GraphicObject, go, 0, i);
                var stream = i < inletNames.Count ? inletNames[i] : null;
                var t = Operating(r, "temperature", true, "temperature", @case, stream);
                var p = Operating(r, "pressure", true, "pressure", @case, stream);
                var w = Operating(r, "mass_flow", true, "massflow", @case, stream) ?? (two ? null : SI(r, "mass_flow", "massflow", @case));
                var m = (IMaterialStream)ms;
                if (t.HasValue) m.SetTemperature(t.Value);
                if (p.HasValue) m.SetPressure(p.Value);
                if (w.HasValue) m.SetMassFlow(w.Value);
                var fluid = Text(r, "fluid", f => stream == null || f.StreamName.IndexOf(stream, StringComparison.OrdinalIgnoreCase) >= 0);
                var compound = fs.SelectedCompounds.Keys.FirstOrDefault(c => fluid.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0);
                if (compound != null && ms is DWSIM.Thermodynamics.Streams.MaterialStream mat)
                    foreach (var c in fs.SelectedCompounds.Keys) mat.SetOverallCompoundMassFlow(c, c == compound ? 1.0 : 0.0);
                else issues.Add(new ImportIssue(ImportIssue.Levels.Warning, ms.GraphicObject.Tag, "set the composition of the inlet" + (fluid.Length > 0 ? " (" + fluid + ")" : "")));
                if (w.HasValue && compound != null) m.SetMassFlow(w.Value);
            }
            var outlets = types[0] == ObjectType.Vessel ? 2 : two ? 2 : 1;
            for (var i = 0; i < outlets; i++)
            {
                var name = i < outletNames.Count ? outletNames[i] : tag + (outlets > 1 ? "-OUT" + (i + 1) : "-OUT");
                var ms = fs.AddObject(ObjectType.MaterialStream, (int)(go.X + go.Width + 80), (int)go.Y + 40 * i, FreeTag(fs, name));
                if (pp != null) try { ms.PropertyPackage = pp; } catch (Exception) { }
                fs.ConnectObjects(go, ms.GraphicObject, i, 0);
            }
            // the energy stream a pump, compressor or expander exchanges
            if (types[0] == ObjectType.Pump || types[0] == ObjectType.Compressor)
            {
                var e = fs.AddObject(ObjectType.EnergyStream, (int)go.X - 60, (int)(go.Y + go.Height + 40), FreeTag(fs, "E-" + tag));
                for (var i = 0; i < go.InputConnectors.Count; i++)
                    if (go.InputConnectors[i].Type == ConType.ConEn) { fs.ConnectObjects(e.GraphicObject, go, 0, i); break; }
            }
            else if (types[0] == ObjectType.Expander)
            {
                var e = fs.AddObject(ObjectType.EnergyStream, (int)(go.X + go.Width + 30), (int)(go.Y - 40), FreeTag(fs, "E-" + tag));
                fs.ConnectObjects(go, e.GraphicObject, 0, 0);
            }
            return obj;
        }

        static string FreeTag(IFlowsheet fs, string tag)
        {
            var t = string.IsNullOrWhiteSpace(tag) ? "S" : tag.Trim();
            if (fs.GetFlowsheetSimulationObject(t) == null) return t;
            for (var i = 2; ; i++) if (fs.GetFlowsheetSimulationObject(t + " (" + i + ")") == null) return t + " (" + i + ")";
        }

        #endregion

        /// <summary>Reads a data sheet (image, PDF or CAD drawing) through the AI Assistant.</summary>
        public static DataSheetReading Read(string file, CancellationToken ct, Action<string> step = null)
        {
            var o = DrawingImportClient.ReadRaw(file, "datasheet", ct, step, out var overview);
            var r = DataSheetReading.FromJson(o.ToString());
            r.Source = file;
            r.Overview = overview;
            return r;
        }
    }

    /// <summary>The data sheet import as the wizards of both interfaces run it: read, correct, choose the equipment, apply.</summary>
    public class DataSheetImportSession
    {
        public readonly IFlowsheet Flowsheet;
        public string File = "";
        public DataSheetReading Reading;
        public string Case = "";
        public ISimulationObject Target;
        public List<SheetChange> Changes = new List<SheetChange>();
        public List<ImportIssue> Issues = new List<ImportIssue>();
        public List<ImportIssue> Results = new List<ImportIssue>();

        public static readonly string[] Steps = { "Data sheet", "Values", "Equipment" };

        public static readonly string[] StepTexts =
        {
            "Choose the equipment data sheet: an image, a PDF or a DXF/DWG drawing. The AI Assistant reads its values with the model set up in its settings.",
            "The values read, with the standard key each one stands for. Correct a value, a unit or a key, or clear the ones not to use.",
            "The equipment the sheet goes into, the operating case, and the changes it makes; tick the ones to make. The checks compare the sheet with itself and with the simulation.",
        };

        public static readonly string NewObject = "(new equipment)";

        public static readonly string[] Keys =
        {
            "", "fluid", "mass_flow", "volume_flow", "inlet_temperature", "outlet_temperature", "inlet_pressure", "outlet_pressure", "density", "viscosity",
            "rated_flow", "differential_pressure", "differential_head", "efficiency", "shaft_power", "hydraulic_power", "npsh_required", "speed", "impeller_diameter",
            "pressure_ratio", "adiabatic_efficiency", "polytropic_efficiency", "polytropic_head",
            "duty", "area", "overall_coefficient", "lmtd", "tema_type", "shells_in_series", "shell_inside_diameter", "tubes_per_shell", "tube_outside_diameter",
            "tube_wall", "tube_inside_diameter", "tube_length", "tube_passes", "tube_pitch", "tube_layout", "baffle_cut", "baffle_spacing", "fouling",
            "orientation", "inside_diameter", "length_tangent_to_tangent", "head_type", "volume",
            "kv", "cv", "rated_kv", "rated_cv", "required_kv", "required_cv", "characteristic", "opening", "rangeability",
        };

        public DataSheetImportSession(IFlowsheet fs) { Flowsheet = fs; }

        public void Read(CancellationToken ct, Action<string> step)
        {
            Reading = DataSheetImport.Read(File, ct, step);
            Loaded();
        }

        public void Open(string json)
        {
            Reading = DataSheetReading.FromJson(json);
            if (Reading.Source.Length > 0) File = Reading.Source;
            Loaded();
        }

        void Loaded()
        {
            Case = DataSheetImport.DefaultCase(Reading);
            // the object named on the sheet (its tag, or a tag written beside it, "PV-012 (PV-01)")
            var tokens = System.Text.RegularExpressions.Regex.Matches(Reading.Tag + " " + DataSheetImport.Text(Reading, "tag"), @"[A-Za-z0-9][A-Za-z0-9\-_.]*")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToList();
            Target = DataSheetImport.Targets(Flowsheet, Reading).FirstOrDefault(o => tokens.Any(t => string.Equals(t, o.GraphicObject.Tag, StringComparison.OrdinalIgnoreCase)));
        }

        public void SaveReading(string path) => System.IO.File.WriteAllText(path, Reading.ToJson());

        /// <summary>The objects the sheet can go into, after the choice of a new one.</summary>
        public List<string> Targets()
        {
            var list = new List<string>();
            if (DataSheetImport.Supported(Reading.EquipmentType)) list.Add(NewObject);
            list.InsertRange(0, DataSheetImport.Targets(Flowsheet, Reading).Select(o => o.GraphicObject.Tag));
            return list;
        }

        public void Choose(string target) => Target = target == NewObject ? null : Flowsheet.GetFlowsheetSimulationObject(target);

        /// <summary>The changes and the checks for the chosen equipment and case.</summary>
        public void Propose()
        {
            Issues = DataSheetImport.Check(Flowsheet, Reading, Target, Case);
            Changes = Target == null ? new List<SheetChange>() : DataSheetImport.Changes(Flowsheet, Reading, Target, Case, Issues);
            // sorted in place: the changes add to this list when they are made
            var sorted = Issues.OrderBy(i => i.Level).ToList();
            Issues.Clear();
            Issues.AddRange(sorted);
        }

        /// <summary>
        /// Makes the ticked changes (on a new object when none was chosen), solves the flowsheet
        /// and compares the outlet with the sheet.
        /// </summary>
        public void Apply()
        {
            var created = new List<ImportIssue>();
            if (Target == null)
            {
                Target = DataSheetImport.Create(Flowsheet, Reading, Case, created);
                Changes = DataSheetImport.Changes(Flowsheet, Reading, Target, Case, created);
            }
            var done = 0;
            var before = Issues.Count;
            foreach (var c in Changes.Where(c => c.Use))
            {
                try { c.Apply(); done++; }
                catch (Exception ex) { created.Add(new ImportIssue(ImportIssue.Levels.Error, Target.GraphicObject.Tag, c.What + ": " + ex.GetBaseException().Message)); }
            }
            Results = new List<ImportIssue> { new ImportIssue(ImportIssue.Levels.Info, Target.GraphicObject.Tag, done + " changes made from the data sheet") };
            Results.AddRange(created);
            Results.AddRange(Issues.Skip(before));
            if (GlobalSettings.Settings.CalculatorActivated)
            {
                var errors = Flowsheet.RequestCalculationAndWait() ?? new List<Exception>();
                foreach (var e in errors.Take(5)) Results.Add(new ImportIssue(ImportIssue.Levels.Error, "", e.GetBaseException().Message));
                Results.AddRange(DataSheetImport.CompareOutlet(Flowsheet, Reading, Target, Case));
            }
            else Results.Add(new ImportIssue(ImportIssue.Levels.Info, "", "the calculator is paused, so the flowsheet was not solved"));
            try { Flowsheet.UpdateInterface(); } catch (Exception) { }
        }
    }
}
