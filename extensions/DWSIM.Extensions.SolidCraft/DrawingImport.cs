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
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// What was read off a process flow diagram or a P&amp;ID: the items with their tags, the
    /// lines between them, the stream table's values and what the drawing tells about the process.
    /// The reading comes from the AI Assistant (a vision model) and the user corrects it in the
    /// import wizard before the flowsheet is built from it.
    /// </summary>
    public class DrawingReading
    {
        /// <summary>"pfd" or "pid".</summary>
        public string Kind = "pfd";

        /// <summary>The drawing file the reading came from.</summary>
        public string Source = "";

        public UnitsUsed Units = new UnitsUsed();
        public List<ReadItem> Items = new List<ReadItem>();
        public List<ReadLine> Lines = new List<ReadLine>();
        public ProcessGuess Process = new ProcessGuess();

        /// <summary>The text on the sheet that tells about the process (services, names, notes, legend).</summary>
        public List<string> Texts = new List<string>();

        /// <summary>
        /// The tags, names and line numbers written on a CAD drawing, read straight from the file
        /// (empty for an image), to check the reading's tags against.
        /// </summary>
        public List<string> CadTexts = new List<string>();

        /// <summary>The sheet as the model saw it (PNG), to show the reading on.</summary>
        public byte[] Overview;

        /// <summary>
        /// Reads the JSON the assistant returns: "units" (tag, category, x, y), "streams" (PFD) or
        /// "lines" (P&amp;ID) with their ends, "units_system" and "process".
        /// </summary>
        public static DrawingReading FromJson(string json, string kind = null)
        {
            var o = JObject.Parse(json);
            var r = new DrawingReading();
            r.Kind = kind ?? (string)o["kind"] ?? (o["lines"] != null && o["streams"] == null ? "pid" : "pfd");
            r.Source = (string)o["source"] ?? "";
            var us = o["units_system"] as JObject;
            if (us != null)
            {
                r.Units.Temperature = Str(us["temperature"]) ?? r.Units.Temperature;
                r.Units.Pressure = Str(us["pressure"]) ?? r.Units.Pressure;
                r.Units.MassFlow = Str(us["massflow"]) ?? r.Units.MassFlow;
                r.Units.Energy = Str(us["energy"]) ?? r.Units.Energy;
            }
            foreach (var u in (o["units"] as JArray) ?? new JArray())
            {
                var item = new ReadItem
                {
                    Tag = (Str(u["tag"]) ?? "").Trim(),
                    Category = Str(u["category"]) ?? "Generic",
                    X = Num(u["x"]) ?? double.NaN,
                    Y = Num(u["y"]) ?? double.NaN,
                };
                item.Type = Str(u["type"]) ?? DrawingImport.DefaultType(item.Category);
                if (item.Tag.Length > 0) r.Items.Add(item);
            }
            foreach (var s in ((o["streams"] ?? o["lines"]) as JArray) ?? new JArray())
            {
                var line = new ReadLine
                {
                    Id = Str(s["id"]) ?? "",
                    Name = Str(s["name"]) ?? "",
                    LineNumber = Str(s["line_number"]) ?? "",
                    From = (Str(s["from"]) ?? "").Trim(),
                    To = (Str(s["to"]) ?? "").Trim(),
                    FromPort = Str(s["from_port"]) ?? "",
                    ToPort = Str(s["to_port"]) ?? "",
                    T = Num(s["T"]),
                    P = Num(s["P"]),
                    W = Num(s["W"]),
                    VaporFraction = Num(s["vf"]),
                };
                if (s["compounds"] is JObject cs)
                    foreach (var p in cs.Properties())
                    {
                        var v = Num(p.Value);
                        if (v.HasValue) line.Compounds[p.Name] = v.Value;
                    }
                r.Lines.Add(line);
            }
            foreach (var t in (o["texts"] as JArray) ?? new JArray())
                if (Str(t) is string text && text.Trim().Length > 0) r.Texts.Add(text.Trim());
            foreach (var t in (o["cad_texts"] as JArray) ?? new JArray())
                if (Str(t) is string text && text.Trim().Length > 0) r.CadTexts.Add(text.Trim());
            if (Str(o["overview"]) is string png && png.Length > 0) try { r.Overview = Convert.FromBase64String(png); } catch (FormatException) { }
            var pr = o["process"] as JObject;
            if (pr != null)
            {
                r.Process.Description = Str(pr["description"] ?? pr["process"]) ?? "";
                r.Process.Class = Str(pr["class"]) ?? "";
                r.Process.PropertyPackage = Str(pr["property_package"]) ?? "";
                r.Process.Confidence = pr["confidence"] is JObject conf ? string.Join(", ", conf.Properties().Select(p => p.Name + " " + p.Value)) : Str(pr["confidence"]) ?? "";
                r.Process.Evidence = Str(pr["evidence"]) ?? "";
                foreach (var c in (pr["compounds"] as JArray) ?? new JArray())
                    if (Str(c) is string name && name.Length > 0) r.Process.Compounds.Add(name);
            }
            return r;
        }

        public string ToJson()
        {
            JToken N(double? v) => v.HasValue && !double.IsNaN(v.Value) ? (JToken)v.Value : JValue.CreateNull();
            var o = new JObject
            {
                ["kind"] = Kind,
                ["source"] = Source,
                ["units_system"] = new JObject { ["temperature"] = Units.Temperature, ["pressure"] = Units.Pressure, ["massflow"] = Units.MassFlow, ["energy"] = Units.Energy },
                ["units"] = new JArray(Items.Select(i => new JObject { ["tag"] = i.Tag, ["category"] = i.Category, ["type"] = i.Type, ["x"] = N(i.X), ["y"] = N(i.Y), ["include"] = i.Include })),
                ["streams"] = new JArray(Lines.Select(l => new JObject
                {
                    ["id"] = l.Id, ["name"] = l.Name, ["line_number"] = l.LineNumber, ["from"] = l.From, ["to"] = l.To,
                    ["from_port"] = l.FromPort, ["to_port"] = l.ToPort, ["T"] = N(l.T), ["P"] = N(l.P), ["W"] = N(l.W), ["vf"] = N(l.VaporFraction),
                    ["compounds"] = new JObject(l.Compounds.Select(c => new JProperty(c.Key, c.Value))), ["include"] = l.Include,
                })),
                ["texts"] = new JArray(Texts),
                ["cad_texts"] = new JArray(CadTexts),
                ["overview"] = Overview == null ? "" : Convert.ToBase64String(Overview),
                ["process"] = new JObject
                {
                    ["description"] = Process.Description, ["class"] = Process.Class, ["property_package"] = Process.PropertyPackage,
                    ["confidence"] = Process.Confidence, ["evidence"] = Process.Evidence, ["compounds"] = new JArray(Process.Compounds),
                },
            };
            return o.ToString();
        }

        static string Str(JToken t) => t == null || t.Type == JTokenType.Null ? null : t.ToString();

        static double? Num(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return null;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return (double)t;
            return double.TryParse(t.ToString().Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
        }

        /// <summary>The items that take part in the import, by tag.</summary>
        public Dictionary<string, ReadItem> ItemsByTag()
        {
            var d = new Dictionary<string, ReadItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var i in Items.Where(i => i.Include)) if (!d.ContainsKey(i.Tag)) d[i.Tag] = i;
            return d;
        }

        /// <summary>The name a line gets in the flowsheet: the table's name, the line number or the stream number.</summary>
        public string StreamTag(ReadLine l)
        {
            if (!string.IsNullOrWhiteSpace(l.Name)) return l.Name.Trim();
            if (!string.IsNullOrWhiteSpace(l.LineNumber)) return l.LineNumber.Trim();
            return string.IsNullOrWhiteSpace(l.Id) ? "S" + (Lines.IndexOf(l) + 1) : (char.IsDigit(l.Id.Trim()[0]) ? "S-" + l.Id.Trim() : l.Id.Trim());
        }
    }

    /// <summary>The units the drawing's values are in, as DWSIM unit symbols.</summary>
    public class UnitsUsed
    {
        public string Temperature = "C", Pressure = "bar", MassFlow = "kg/h", Energy = "kW";
    }

    /// <summary>A piece of equipment read off the drawing.</summary>
    public class ReadItem
    {
        public string Tag = "", Category = "Generic";

        /// <summary>The DWSIM object it becomes (an <c>ObjectType</c> name).</summary>
        public string Type = "";

        /// <summary>The item's centre on the sheet, 0 to 1 from the left and from the top (NaN when not known).</summary>
        public double X = double.NaN, Y = double.NaN;

        public bool Include = true;
    }

    /// <summary>A process line (stream) read off the drawing, with the stream table's values in the drawing's units.</summary>
    public class ReadLine
    {
        public string Id = "", Name = "", LineNumber = "", From = "", To = "";

        /// <summary>Which nozzle, when the drawing tells: top, bottom, vapor, liquid, distillate, bottoms, hot, cold.</summary>
        public string FromPort = "", ToPort = "";

        public double? T, P, W, VaporFraction;

        /// <summary>The mass flow of each compound the table lists, in the table's mass flow unit.</summary>
        public Dictionary<string, double> Compounds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public bool Include = true;

        public bool Feed => From.Length == 0;
        public bool Product => To.Length == 0;
        public bool HasData => T.HasValue || P.HasValue || W.HasValue;
    }

    /// <summary>What the text on the drawing tells about the process.</summary>
    public class ProcessGuess
    {
        public string Description = "", Class = "", PropertyPackage = "", Confidence = "", Evidence = "";
        public List<string> Compounds = new List<string>();
    }

    /// <summary>The fixed tables of the import: categories, DWSIM types and the property package for each kind of process.</summary>
    public static class DrawingImport
    {
        /// <summary>The categories the reading uses for the items.</summary>
        public static readonly string[] Categories =
        {
            "Vessel", "Tank", "Column", "Pump", "Compressor", "Expander", "Heater", "Cooler", "Exchanger", "AirCooler",
            "Valve", "Relief", "Reactor", "Cstr", "Pfr", "Filter", "Separator", "Pipe", "Orifice", "Mixer", "Splitter", "Generic",
        };

        /// <summary>The DWSIM objects an item of a category can become, the usual one first.</summary>
        public static string[] TypesFor(string category)
        {
            switch (category)
            {
                case "Vessel": return new[] { "Vessel" };
                case "Tank": return new[] { "Tank", "Vessel" };
                case "Column": return new[] { "ShortcutColumn" };
                case "Pump": return new[] { "Pump" };
                case "Compressor": return new[] { "Compressor" };
                case "Expander": return new[] { "Expander", "Valve" };
                case "Heater": return new[] { "Heater", "HeatExchanger" };
                case "Cooler": return new[] { "Cooler", "HeatExchanger", "AirCooler2" };
                case "Exchanger": return new[] { "HeatExchanger", "Heater", "Cooler" };
                case "AirCooler": return new[] { "AirCooler2", "Cooler" };
                case "Valve": return new[] { "Valve" };
                case "Relief": return new[] { "Valve" };
                case "Reactor": return new[] { "RCT_Conversion", "RCT_Equilibrium", "RCT_Gibbs" };
                case "Cstr": return new[] { "RCT_CSTR", "RCT_Conversion" };
                case "Pfr": return new[] { "RCT_PFR", "RCT_Conversion" };
                case "Filter": return new[] { "Filter", "ComponentSeparator" };
                case "Separator": return new[] { "ComponentSeparator", "Vessel" };
                case "Pipe": return new[] { "Pipe" };
                case "Orifice": return new[] { "OrificePlate" };
                case "Mixer": return new[] { "Mixer" };
                case "Splitter": return new[] { "Splitter" };
                default: return new[] { "ComponentSeparator", "Vessel" };
            }
        }

        public static string DefaultType(string category) => TypesFor(category)[0];

        /// <summary>
        /// The kinds of process the reading tells apart, and the property packages that suit them
        /// (DWSIM names, the first one recommended). The package comes from the kind of process,
        /// never straight from the model.
        /// </summary>
        public static readonly (string Class, string Description, string[] Packages)[] ProcessClasses =
        {
            ("natural gas", "Natural gas processing, NGL recovery, gas conditioning", new[] { "Peng-Robinson (PR)", "Soave-Redlich-Kwong (SRK)" }),
            ("refining", "Crude and refinery fractionation, oil cuts", new[] { "Peng-Robinson (PR)", "Lee-Kesler-Plöcker", "Grayson-Streed" }),
            ("light hydrocarbons", "Light hydrocarbons, olefins, LPG, oil and gas separation", new[] { "Peng-Robinson (PR)", "Soave-Redlich-Kwong (SRK)" }),
            ("refrigeration", "Refrigeration and heat pump cycles", new[] { "Peng-Robinson (PR)", "CoolProp" }),
            ("synthesis gas", "Syngas, ammonia, methanol and hydrogen synthesis loops", new[] { "Peng-Robinson (PR)", "Soave-Redlich-Kwong (SRK)" }),
            ("polar organics", "Alcohols, ketones, esters, acids, water mixtures, azeotropic and extractive distillation", new[] { "NRTL", "UNIQUAC", "UNIFAC" }),
            ("biochemical", "Fermentation, ethanol, biodiesel, sugars", new[] { "NRTL", "UNIQUAC" }),
            ("water and steam", "Water, steam and utility systems", new[] { "Steam Tables (IAPWS-IF97)", "IAPWS-08 Seawater" }),
            ("amines", "Amine gas treating (CO2, H2S)", new[] { "Electrolyte NRTL", "Sour Water" }),
            ("electrolytes", "Aqueous electrolytes, salts", new[] { "Electrolyte NRTL" }),
            ("absorption cooling", "Lithium bromide and water absorption chillers and heat pumps", new[] { "CoolProp Incompressible Mixture" }),
            ("cryogenic", "Air separation, nitrogen, oxygen, argon", new[] { "Peng-Robinson (PR)", "Soave-Redlich-Kwong (SRK)" }),
            ("ideal", "Low pressure mixtures of similar compounds", new[] { "Raoult's Law", "NRTL" }),
        };

        /// <summary>The recommended property package for a kind of process, or null for an unknown one.</summary>
        public static string[] PackagesFor(string processClass)
        {
            foreach (var c in ProcessClasses)
                if (string.Equals(c.Class, (processClass ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return c.Packages;
            return null;
        }
    }
}
