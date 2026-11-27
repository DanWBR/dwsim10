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
using System.Text.RegularExpressions;
using System.Threading;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.Pipe;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>A straight leg of an isometric's route, with what sits on it or at its end.</summary>
    public class IsoLeg
    {
        /// <summary>N, S, E, W, up or down.</summary>
        public string Direction = "";

        /// <summary>The leg's length and how much it climbs (negative when it falls), mm; null when not read.</summary>
        public double? Length, Rise;

        /// <summary>The fittings and valves on the leg or at its end, in order ("elbow 90", "gate valve", ...).</summary>
        public List<string> Fittings = new List<string>();

        public bool Include = true;

        public bool Vertical => Direction.Equals("up", StringComparison.OrdinalIgnoreCase) || Direction.Equals("down", StringComparison.OrdinalIgnoreCase);

        /// <summary>The rise, mm: as read, else the length up or down a vertical leg, else none.</summary>
        public double RiseOrVertical => Rise ?? (Vertical && Length.HasValue ? (Direction.Equals("down", StringComparison.OrdinalIgnoreCase) ? -Length.Value : Length.Value) : 0.0);
    }

    /// <summary>What was read off a piping isometric: the line, its size and material, and its route leg by leg.</summary>
    public class IsoReading
    {
        public string Source = "", LineNumber = "", Size = "", Schedule = "", Material = "", From = "", To = "";
        public double? OuterDiameter, Wall;

        /// <summary>The start and end coordinates and elevations as written, m (null when not written).</summary>
        public double? StartE, StartN, StartEl, EndE, EndN, EndEl;

        public List<IsoLeg> Legs = new List<IsoLeg>();
        public List<string> Elevations = new List<string>(), Texts = new List<string>(), Notes = new List<string>();
        public List<KeyValuePair<string, string>> Bom = new List<KeyValuePair<string, string>>();
        public Dictionary<string, string> Conditions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> TitleBlock = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public byte[] Overview;

        /// <summary>The number of sheets the reading was joined from, and what the joining found (see <see cref="Merge"/>).</summary>
        public int Sheets = 1;
        public List<string> SheetWarnings = new List<string>();

        static string Str(JToken t) => t == null || t.Type == JTokenType.Null ? null : t.ToString().Trim();

        static double? Num(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return null;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return (double)t;
            var m = Regex.Match(t.ToString().Replace(",", "."), @"[-+]?\d+(\.\d+)?");
            return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
        }

        public static IsoReading FromJson(string json)
        {
            var o = JObject.Parse(json);
            var r = new IsoReading
            {
                Source = Str(o["source"]) ?? "",
                LineNumber = Str(o["line_number"]) ?? "",
                Size = Str(o["size"]) ?? "",
                Schedule = Str(o["schedule"]) ?? "",
                Material = Str(o["material"]) ?? "",
                From = Str(o["from"]) ?? "",
                To = Str(o["to"]) ?? "",
                OuterDiameter = Num(o["od_mm"]),
                Wall = Num(o["wall_mm"]),
            };
            if (o["start"] is JObject s) { r.StartE = Num(s["e"]); r.StartN = Num(s["n"]); r.StartEl = Num(s["el"]); }
            if (o["end"] is JObject e) { r.EndE = Num(e["e"]); r.EndN = Num(e["n"]); r.EndEl = Num(e["el"]); }
            // dimensions in other units than mm
            var unit = (Str(o["dimension_units"]) ?? "mm").ToLowerInvariant();
            var f = unit.StartsWith("m") && !unit.StartsWith("mm") ? 1000.0 : unit.StartsWith("in") || unit == "\"" ? 25.4 : unit.StartsWith("ft") ? 304.8 : 1.0;
            foreach (var l in (o["legs"] as JArray) ?? new JArray())
            {
                var leg = new IsoLeg { Direction = Str(l["direction"]) ?? "", Length = Num(l["length_mm"]) * f, Rise = Num(l["rise_mm"]) * f };
                if (l["include"] != null && l["include"].Type == JTokenType.Boolean) leg.Include = (bool)l["include"];
                foreach (var x in (l["fittings"] as JArray) ?? new JArray()) if (Str(x) is string fit && fit.Length > 0) leg.Fittings.Add(fit);
                r.Legs.Add(leg);
            }
            foreach (var t in (o["elevations"] as JArray) ?? new JArray()) if (Str(t) is string v && v.Length > 0) r.Elevations.Add(v);
            foreach (var t in (o["texts"] as JArray) ?? new JArray()) if (Str(t) is string v && v.Length > 0) r.Texts.Add(v);
            foreach (var t in (o["notes"] as JArray) ?? new JArray()) if (Str(t) is string v && v.Length > 0) r.Notes.Add(v);
            foreach (var b in (o["bom"] as JArray) ?? new JArray())
                if (b is JObject bo) r.Bom.Add(new KeyValuePair<string, string>(Str(bo["item"]) ?? "", Str(bo["quantity"]) ?? ""));
            if (o["conditions"] is JObject c) foreach (var p in c.Properties()) if (Str(p.Value) is string v && v.Length > 0) r.Conditions[p.Name] = v;
            if (o["title_block"] is JObject tb) foreach (var p in tb.Properties()) if (Str(p.Value) is string v && v.Length > 0) r.TitleBlock[p.Name] = v;
            if (Str(o["overview"]) is string png && png.Length > 0) try { r.Overview = Convert.FromBase64String(png); } catch (FormatException) { }
            if (o["sheets"] != null && o["sheets"].Type == JTokenType.Integer) r.Sheets = Math.Max(1, (int)o["sheets"]);
            foreach (var t in (o["merge_warnings"] as JArray) ?? new JArray()) if (Str(t) is string v && v.Length > 0) r.SheetWarnings.Add(v);
            return r;
        }

        /// <summary>The sheet number and the number of sheets the title block writes ("2 of 3", "2/3", "FL. 02/03"); 0 when
        /// it does not write them.</summary>
        public int SheetNumber(out int total)
        {
            total = 0;
            if (!TitleBlock.TryGetValue("sheet", out var s) || string.IsNullOrWhiteSpace(s)) return 0;
            var m = Regex.Match(s, @"(\d+)\s*(?:of|de|/)\s*(\d+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                total = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            m = Regex.Match(s, @"\d+");
            return m.Success ? int.Parse(m.Value, CultureInfo.InvariantCulture) : 0;
        }

        /// <summary>
        /// Joins the readings of the sheets of one line (pages of a PDF, or several files) into one: the sheets go in the
        /// order of their numbers when every sheet writes a different one, else in the order given; the legs follow one
        /// another; the line number, size, material and start come from the first sheet that writes them and the end from
        /// the last one. The joining warns when the sheets carry different line numbers or sizes, when a sheet ends away from
        /// where the next one starts (both write their coordinates), and when a sheet is missing.
        /// </summary>
        public static IsoReading Merge(IList<IsoReading> sheets)
        {
            if (sheets == null || sheets.Count == 0) throw new ArgumentException("There are not any sheets to join.");
            if (sheets.Count == 1) return sheets[0];
            var nums = sheets.Select(r => r.SheetNumber(out _)).ToList();
            var list = nums.All(n => n > 0) && nums.Distinct().Count() == nums.Count
                ? sheets.Zip(nums, (r, n) => new { r, n }).OrderBy(x => x.n).Select(x => x.r).ToList() : sheets.ToList();
            string First(Func<IsoReading, string> get) => list.Select(get).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";
            var a = list[0]; var z = list[list.Count - 1];
            var m = new IsoReading
            {
                Sheets = list.Count,
                Source = string.Join("; ", list.Select(r => r.Source).Where(s => !string.IsNullOrEmpty(s)).Distinct()),
                LineNumber = First(r => r.LineNumber), Size = First(r => r.Size), Schedule = First(r => r.Schedule), Material = First(r => r.Material),
                From = First(r => r.From), To = list.Select(r => r.To).LastOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "",
                OuterDiameter = list.Select(r => r.OuterDiameter).FirstOrDefault(v => v.HasValue), Wall = list.Select(r => r.Wall).FirstOrDefault(v => v.HasValue),
                StartE = a.StartE, StartN = a.StartN, StartEl = a.StartEl, EndE = z.EndE, EndN = z.EndN, EndEl = z.EndEl,
                Overview = a.Overview,
            };
            foreach (var r in list)
            {
                m.Legs.AddRange(r.Legs);
                m.Elevations.AddRange(r.Elevations.Where(x => !m.Elevations.Contains(x)));
                m.Texts.AddRange(r.Texts.Where(x => !m.Texts.Contains(x)));
                m.Notes.AddRange(r.Notes.Where(x => !m.Notes.Contains(x)));
                m.Bom.AddRange(r.Bom);
                foreach (var kv in r.Conditions) if (!m.Conditions.ContainsKey(kv.Key)) m.Conditions[kv.Key] = kv.Value;
                foreach (var kv in r.TitleBlock) if (!m.TitleBlock.ContainsKey(kv.Key)) m.TitleBlock[kv.Key] = kv.Value;
                m.SheetWarnings.AddRange(r.SheetWarnings);
            }
            m.TitleBlock.Remove("sheet");
            string Norm(string s) => Regex.Replace((s ?? "").ToUpperInvariant(), @"[^A-Z0-9]", "");
            var lines = list.Select(r => r.LineNumber).Where(s => !string.IsNullOrWhiteSpace(s)).GroupBy(Norm).Select(g => g.First()).ToList();
            if (lines.Count > 1) m.SheetWarnings.Add("The sheets carry different line numbers (" + string.Join(", ", lines) + "); they were joined as one line.");
            var sizes = list.Select(r => r.Size).Where(s => !string.IsNullOrWhiteSpace(s)).GroupBy(Norm).Select(g => g.First()).ToList();
            if (sizes.Count > 1) m.SheetWarnings.Add("The size changes between the sheets (" + string.Join(", ", sizes) + "); the line takes the size of the first sheet.");
            for (int i = 0; i + 1 < list.Count; i++)
            {
                var p = list[i]; var q = list[i + 1];
                if (!(p.EndE.HasValue && p.EndN.HasValue && q.StartE.HasValue && q.StartN.HasValue)) continue;
                double dz = p.EndEl.HasValue && q.StartEl.HasValue ? p.EndEl.Value - q.StartEl.Value : 0.0;
                double gap = Math.Sqrt(Math.Pow(p.EndE.Value - q.StartE.Value, 2) + Math.Pow(p.EndN.Value - q.StartN.Value, 2) + dz * dz);
                if (gap > 0.1)
                    m.SheetWarnings.Add(string.Format(CultureInfo.InvariantCulture, "Sheet {0} ends {1:0.##} m away from where sheet {2} starts; check the order of the sheets.", i + 1, gap, i + 2));
            }
            int total = list.Select(r => { r.SheetNumber(out var t); return t; }).DefaultIfEmpty(0).Max();
            if (total > list.Count) m.SheetWarnings.Add("The line runs over " + total + " sheets, and " + list.Count + " were read.");
            if (nums.All(n => n == 0)) m.SheetWarnings.Add("The sheets do not write their numbers; they were joined in the order given.");
            return m;
        }

        public string ToJson()
        {
            JToken N(double? v) => v.HasValue && !double.IsNaN(v.Value) ? (JToken)v.Value : JValue.CreateNull();
            return new JObject
            {
                ["kind"] = "iso", ["source"] = Source, ["line_number"] = LineNumber, ["size"] = Size, ["schedule"] = Schedule, ["material"] = Material,
                ["from"] = From, ["to"] = To, ["od_mm"] = N(OuterDiameter), ["wall_mm"] = N(Wall), ["dimension_units"] = "mm",
                ["start"] = new JObject { ["e"] = N(StartE), ["n"] = N(StartN), ["el"] = N(StartEl) },
                ["end"] = new JObject { ["e"] = N(EndE), ["n"] = N(EndN), ["el"] = N(EndEl) },
                ["legs"] = new JArray(Legs.Select(l => new JObject
                {
                    ["direction"] = l.Direction, ["length_mm"] = N(l.Length), ["rise_mm"] = N(l.Rise), ["fittings"] = new JArray(l.Fittings), ["include"] = l.Include,
                })),
                ["elevations"] = new JArray(Elevations),
                ["bom"] = new JArray(Bom.Select(b => new JObject { ["item"] = b.Key, ["quantity"] = b.Value })),
                ["conditions"] = new JObject(Conditions.Select(kv => new JProperty(kv.Key, kv.Value))),
                ["texts"] = new JArray(Texts), ["notes"] = new JArray(Notes),
                ["title_block"] = new JObject(TitleBlock.Select(kv => new JProperty(kv.Key, kv.Value))),
                ["overview"] = Overview == null ? "" : Convert.ToBase64String(Overview),
                ["sheets"] = Sheets, ["merge_warnings"] = new JArray(SheetWarnings),
            }.ToString();
        }
    }

    /// <summary>
    /// Makes the hydraulic profile of a Pipe Segment from a piping isometric: a straight section
    /// for each leg with its length and rise, a fitting section for the elbows, valves and tees
    /// along the route, the diameters from the size read (outside diameter and wall, or the
    /// nominal size and schedule) and the roughness from the material. The isometric is read by
    /// the AI Assistant, like the PFDs and P&amp;IDs of <see cref="DrawingImportClient"/>.
    /// </summary>
    public static class IsometricImport
    {
        /// <summary>The reading's fitting names and the pipe segment's, by the name before the index ("Elbow 90 dg").</summary>
        static readonly (string Read, string Pipe)[] FittingMap =
        {
            ("elbow 90", "Elbow 90 dg"), ("elbow 45", "Elbow 45 dg"), ("elbow 180", "Elbow 180 dg"), ("return", "Elbow 180 dg"), ("bend", "Bend 90 dg"),
            ("tee", "Tee (Branch Blanked)"), ("gate", "Gate Valve (Open)"), ("globe", "Globe Valve"), ("control", "Globe Valve"),
            ("ball", "Ball Valve"), ("plug", "Ball Valve"), ("butterfly", "Butterfly Valve (2 to 14 in.)"), ("check", "Check Valve (Swing)"),
            ("angle", "Angle Valve"), ("reducer", "Normal Reduction 4:3"), ("expander", "Quick Expansion d/D = 3/4"),
        };

        /// <summary>What the pipe segment has no fitting for: left out of the profile.</summary>
        static readonly string[] Ignored = { "flange", "weld", "support", "instrument", "orifice", "strainer", "spectacle", "blind", "gasket", "olet" };

        static List<string> pipeFittings;

        /// <summary>The fittings of the pipe segment, as its profile stores them ("Elbow 90 dg [0]").</summary>
        public static List<string> PipeFittings()
        {
            if (pipeFittings != null) return pipeFittings;
            var list = new List<string>();
            try
            {
                using (var stream = typeof(PipeSection).Assembly.GetManifestResourceStream("DWSIM.UnitOperations.fittings.dat"))
                using (var reader = new StreamReader(stream, Encoding.GetEncoding(28591)))
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line)) list.Add(line.Split(';')[0]);
                    }
            }
            catch (Exception) { }
            return pipeFittings = list;
        }

        /// <summary>The pipe segment's fitting for a fitting read, null for one it does not model.</summary>
        public static string PipeFitting(string read)
        {
            var r = (read ?? "").ToLowerInvariant();
            if (Ignored.Any(r.Contains)) return null;
            foreach (var m in FittingMap)
                if (r.Contains(m.Read))
                    return PipeFittings().FirstOrDefault(f => f.StartsWith(m.Pipe + " [", StringComparison.OrdinalIgnoreCase)) ?? m.Pipe;
            if (r.Contains("elbow")) return PipeFittings().FirstOrDefault(f => f.StartsWith("Elbow 90 dg [")) ?? "Elbow 90 dg [0]";
            if (r.Contains("valve")) return PipeFittings().FirstOrDefault(f => f.StartsWith("Gate Valve (Open) [")) ?? "Gate Valve (Open) [6]";
            return null;
        }

        /// <summary>The pipe segment's material for the material or piping class read.</summary>
        public static string Material(string read)
        {
            var m = (read ?? "").ToLowerInvariant();
            if (Regex.IsMatch(m, @"stainless|\bss\b|a312|a358|\b31[06]l?\b|\b304l?\b|inox")) return "StainlessSteel";
            if (m.Contains("pvc") && (m.Contains("frp") || m.Contains("prfv") || m.Contains("pfrv"))) return "PVC+PFRV";
            if (m.Contains("pvc") || m.Contains("hdpe") || m.Contains("pead")) return "PVC";
            if (m.Contains("copper") || m.Contains("cobre")) return "CommercialCopper";
            if (m.Contains("cast iron") || m.Contains("ferro fundido") || m.Contains("ductile")) return "CastIron";
            return "CarbonSteel";
        }

        /// <summary>
        /// The outside and inside diameters, inches: from the outside diameter and the wall read, else
        /// from the nominal size and the schedule (STD when none) in the pipe segment's table.
        /// </summary>
        public static bool Diameters(IsoReading r, out double de, out double di, out string how)
        {
            de = di = 0; how = "";
            if (r.OuterDiameter.HasValue && r.Wall.HasValue && r.OuterDiameter > 2 * r.Wall && r.Wall > 0)
            {
                de = r.OuterDiameter.Value / 25.4;
                di = (r.OuterDiameter.Value - 2 * r.Wall.Value) / 25.4;
                how = string.Format(CultureInfo.InvariantCulture, "OD {0:0.#} mm and wall {1:0.##} mm from the drawing", r.OuterDiameter, r.Wall);
                return true;
            }
            var nps = NominalSize(r.Size);
            if (nps == null) return false;
            var rows = PipeTable().Where(x => x.Nps == nps).ToList();
            if (rows.Count == 0) return false;
            var sch = (r.Schedule ?? "").ToUpperInvariant().Replace("SCH", "").Replace(" ", "");
            var row = rows.FirstOrDefault(x => sch.Length > 0 && (x.Name == sch || x.Schedule == sch || x.SSchedule == sch))
                ?? rows.FirstOrDefault(x => x.Name == "STD") ?? rows[0];
            de = row.Od; di = row.Id;
            how = "nominal size " + nps + "\" " + (row.Name != "---" ? row.Name : "SCH " + row.SSchedule) + " from the pipe table";
            return true;
        }

        /// <summary>The nominal pipe size in inches as the pipe table writes it ("2", "1 1/2", "3/4"), from "2\"", "DN 50", "2 in", "1.5".</summary>
        public static string NominalSize(string size)
        {
            var s = (size ?? "").Trim();
            var dn = Regex.Match(s, @"DN\s*(\d+)", RegexOptions.IgnoreCase);
            if (dn.Success)
            {
                var map = new Dictionary<int, string> { { 15, "1/2" }, { 20, "3/4" }, { 25, "1" }, { 32, "1 1/4" }, { 40, "1 1/2" }, { 50, "2" }, { 65, "2 1/2" }, { 80, "3" },
                    { 90, "3 1/2" }, { 100, "4" }, { 125, "5" }, { 150, "6" }, { 200, "8" }, { 250, "10" }, { 300, "12" }, { 350, "14" }, { 400, "16" }, { 450, "18" },
                    { 500, "20" }, { 600, "24" }, { 750, "30" }, { 900, "36" } };
                return map.TryGetValue(int.Parse(dn.Groups[1].Value), out var n) ? n : null;
            }
            var m = Regex.Match(s, @"(\d+)(?:\s*-?\s*(\d)/(\d))?|(\d)/(\d)|(\d+\.\d+)");
            if (!m.Success) return null;
            if (m.Groups[6].Success)
            {
                var v = double.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture);
                var whole = (int)Math.Floor(v);
                var frac = v - whole;
                var f = Math.Abs(frac - 0.5) < 0.01 ? "1/2" : Math.Abs(frac - 0.25) < 0.01 ? "1/4" : Math.Abs(frac - 0.75) < 0.01 ? "3/4" : "";
                return whole == 0 ? f : (whole + (f.Length > 0 ? " " + f : ""));
            }
            if (m.Groups[4].Success) return m.Groups[4].Value + "/" + m.Groups[5].Value;
            return m.Groups[2].Success ? m.Groups[1].Value + " " + m.Groups[2].Value + "/" + m.Groups[3].Value : m.Groups[1].Value;
        }

        class PipeRow { public string Nps, Name, Schedule, SSchedule; public double Od, Id; }

        static List<PipeRow> pipeTable;

        static List<PipeRow> PipeTable()
        {
            if (pipeTable != null) return pipeTable;
            var list = new List<PipeRow>();
            try
            {
                using (var stream = typeof(PipeSection).Assembly.GetManifestResourceStream("DWSIM.UnitOperations.pipes.dat"))
                using (var reader = new StreamReader(stream))
                    while (!reader.EndOfStream)
                    {
                        var p = (reader.ReadLine() ?? "").Split(';');
                        if (p.Length < 7) continue;
                        if (!double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var od) ||
                            !double.TryParse(p[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var id)) continue;
                        list.Add(new PipeRow { Nps = p[0].Trim(), Od = od, Name = p[2].Trim(), Schedule = p[3].Trim(), SSchedule = p[4].Trim(), Id = id });
                    }
            }
            catch (Exception) { }
            return pipeTable = list;
        }

        /// <summary>
        /// The pipe segment's sections from the route: each leg a straight section (its length and
        /// rise in m), then its fittings, the same fitting in a row as one section with a quantity.
        /// </summary>
        public static List<PipeSection> Sections(IsoReading r, List<ImportIssue> issues)
        {
            var list = new List<PipeSection>();
            var ok = Diameters(r, out var de, out var di, out var how);
            if (ok) issues.Add(new ImportIssue(ImportIssue.Levels.Info, "", "diameters: " + how));
            else
            {
                de = 2.375; di = 2.067;
                issues.Add(new ImportIssue(ImportIssue.Levels.Error, "", "the size " + (r.Size.Length > 0 ? "\"" + r.Size + "\" " : "") + "is not known; enter the outside diameter and the wall"));
            }
            var material = Material(r.Material);
            var k = 1;
            for (var i = 0; i < r.Legs.Count; i++)
            {
                var leg = r.Legs[i];
                if (!leg.Include) continue;
                if (leg.Length.HasValue && leg.Length.Value > 0)
                {
                    var length = leg.Length.Value / 1000;
                    var rise = Math.Max(-length, Math.Min(length, leg.RiseOrVertical / 1000));
                    list.Add(new PipeSection
                    {
                        Indice = k++, TipoSegmento = "Tubulaosimples", Quantidade = 1, Incrementos = Math.Max(5, Math.Min(50, (int)Math.Ceiling(length))),
                        Material = material, Comprimento = length, Elevacao = rise, DE = de, DI = di,
                    });
                }
                else issues.Add(new ImportIssue(ImportIssue.Levels.Error, "Leg " + (i + 1), "no length read; enter it"));
                string last = null;
                foreach (var f in leg.Fittings)
                {
                    var fit = PipeFitting(f);
                    if (fit == null) continue;
                    if (fit == last) { list[list.Count - 1].Quantidade++; continue; }
                    list.Add(new PipeSection
                    {
                        Indice = k++, TipoSegmento = fit, Quantidade = 1, Incrementos = 1, Material = material, Comprimento = 0, Elevacao = 0, DE = de, DI = di,
                    });
                    last = fit;
                }
            }
            return list;
        }

        static double? Quantity(string q)
        {
            var m = Regex.Match((q ?? "").Replace(",", "."), @"\d+(\.\d+)?");
            return m.Success ? double.Parse(m.Value, CultureInfo.InvariantCulture) : (double?)null;
        }

        /// <summary>
        /// Checks the route against what else the drawing says: the pipe length and the elbows of
        /// the bill of materials, and the rise from the start's elevation to the end's.
        /// </summary>
        public static List<ImportIssue> Check(IsoReading r)
        {
            var issues = new List<ImportIssue>();
            if (r.Sheets > 1) issues.Add(new ImportIssue(ImportIssue.Levels.Info, "", "the line was joined from " + r.Sheets + " sheets"));
            foreach (var w in r.SheetWarnings) issues.Add(new ImportIssue(ImportIssue.Levels.Warning, "", w));
            var legs = r.Legs.Where(l => l.Include).ToList();
            if (legs.Count == 0) { issues.Add(new ImportIssue(ImportIssue.Levels.Error, "", "no legs read")); return issues; }
            var total = legs.Sum(l => l.Length ?? 0) / 1000;
            var ci = CultureInfo.InvariantCulture;
            var pipe = r.Bom.FirstOrDefault(b => Regex.IsMatch(b.Key, @"^\s*(pipe|tubo)", RegexOptions.IgnoreCase));
            var bomLength = Quantity(pipe.Value);
            if (bomLength.HasValue && (pipe.Value ?? "").ToLowerInvariant().Contains("mm")) bomLength /= 1000;
            if (bomLength.HasValue && bomLength > 0)
            {
                var off = Math.Abs(total - bomLength.Value) / bomLength.Value;
                issues.Add(new ImportIssue(off > 0.03 ? ImportIssue.Levels.Warning : ImportIssue.Levels.Info, "",
                    string.Format(ci, "the legs add up to {0:0.00} m of pipe, the bill of materials gives {1:0.0#} m{2}", total, bomLength, off > 0.03 ? "; a leg may be missing or read wrong" : "")));
            }
            var elbows = legs.Sum(l => l.Fittings.Count(f => f.ToLowerInvariant().Contains("elbow") || f.ToLowerInvariant().Contains("bend")));
            var bomElbows = r.Bom.Where(b => Regex.IsMatch(b.Key, @"elbow|bend|cotovelo|curva", RegexOptions.IgnoreCase)).Select(b => Quantity(b.Value) ?? 0).Sum();
            if (bomElbows > 0)
                issues.Add(new ImportIssue(Math.Abs(bomElbows - elbows) > 0.5 ? ImportIssue.Levels.Warning : ImportIssue.Levels.Info, "",
                    string.Format(ci, "{0} elbows on the route, {1} in the bill of materials", elbows, bomElbows)));
            var rise = legs.Sum(l => l.RiseOrVertical) / 1000;
            if (r.StartEl.HasValue && r.EndEl.HasValue)
            {
                var drawn = r.EndEl.Value - r.StartEl.Value;
                issues.Add(new ImportIssue(Math.Abs(drawn - rise) > 0.05 ? ImportIssue.Levels.Warning : ImportIssue.Levels.Info, "",
                    string.Format(ci, "the legs climb {0:+0.000;-0.000} m, the end is {1:+0.000;-0.000} m from the start's elevation{2}", rise, drawn,
                        Math.Abs(drawn - rise) > 0.05 ? "; check the vertical legs" : "")));
            }
            for (var i = 0; i < r.Legs.Count; i++)
            {
                var l = r.Legs[i];
                if (!l.Include) continue;
                if (!Regex.IsMatch(l.Direction ?? "", @"^(n|s|e|w|up|down|ne|nw|se|sw)$", RegexOptions.IgnoreCase))
                    issues.Add(new ImportIssue(ImportIssue.Levels.Warning, "Leg " + (i + 1), "the direction \"" + l.Direction + "\" is not one of N, S, E, W, up, down"));
                foreach (var f in l.Fittings.Where(f => PipeFitting(f) == null && !Ignored.Any(f.ToLowerInvariant().Contains)))
                    issues.Add(new ImportIssue(ImportIssue.Levels.Warning, "Leg " + (i + 1), "\"" + f + "\" has no fitting in the pipe segment; it is left out"));
            }
            return issues;
        }

        /// <summary>
        /// Puts the profile in a pipe segment: the one given, else a new one named after the line.
        /// The sections replace the ones the segment had.
        /// </summary>
        public static ISimulationObject Apply(IFlowsheet fs, IsoReading r, List<PipeSection> sections, ISimulationObject target = null)
        {
            var obj = target != null && target.GraphicObject.ObjectType == ObjectType.Pipe ? target : null;
            if (obj == null)
            {
                var tag = string.IsNullOrWhiteSpace(r.LineNumber) ? "PIPE-" + (fs.SimulationObjects.Count + 1) : r.LineNumber.Trim();
                if (fs.GetFlowsheetSimulationObject(tag) != null) tag += " (" + (fs.SimulationObjects.Count + 1) + ")";
                obj = fs.AddObject(ObjectType.Pipe, 200, 200, tag);
            }
            var pipe = (Pipe)obj;
            pipe.Profile.Sections.Clear();
            var k = 1;
            foreach (var s in sections) { s.Indice = k; pipe.Profile.Sections.Add(k++, s); }
            pipe.Profile.Status = PipeEditorStatus.OK;
            if (sections.Count > 0) { pipe.Profile.DefaultMaterial = sections[0].Material; }
            try { fs.UpdateInterface(); } catch (Exception) { }
            return obj;
        }

        /// <summary>
        /// Reads an isometric (image, PDF or CAD drawing) through the AI Assistant. Every page of a PDF is read as a sheet,
        /// and several files can be given separated by semicolons; the sheets are joined into one line (see
        /// <see cref="IsoReading.Merge"/>).
        /// </summary>
        public static IsoReading Read(string file, CancellationToken ct, Action<string> step = null)
        {
            var files = Files(file);
            if (files.Count == 0) throw new ArgumentException("Choose the isometric's file.");
            var sheets = new List<IsoReading>();
            foreach (var f in files)
            {
                int pages = 1;
                for (int page = 0; page < pages; page++)
                {
                    int shown = page;
                    Action<string> say = step == null ? null : (Action<string>)(msg => step(
                        (files.Count > 1 ? Path.GetFileName(f) + (pages > 1 ? ", " : ": ") : "") + (pages > 1 ? "sheet " + (shown + 1) + " of " + pages + ": " : "") + msg));
                    var o = DrawingImportClient.ReadRaw(f, "iso", page, ct, say, out var overview, out var n);
                    if (page == 0) pages = n;
                    var r = IsoReading.FromJson(o.ToString());
                    r.Source = f;
                    r.Overview = overview;
                    sheets.Add(r);
                }
            }
            return IsoReading.Merge(sheets);
        }

        /// <summary>The files a file box names: the file itself, or several separated by semicolons.</summary>
        public static List<string> Files(string text)
        {
            var t = (text ?? "").Trim();
            if (t.Length == 0) return new List<string>();
            if (File.Exists(t)) return new List<string> { t };
            return t.Split(';').Select(x => x.Trim().Trim('"')).Where(x => x.Length > 0).ToList();
        }

        /// <summary>True when the file box names files that all exist.</summary>
        public static bool FilesExist(string text)
        {
            var f = Files(text);
            return f.Count > 0 && f.All(File.Exists);
        }

        /// <summary>
        /// The route's corners in 3D, mm, from the start: each included leg along its direction
        /// (east is +x, north +y, up +z), a sloped leg climbing its rise. A leg without a length
        /// is drawn 1 m long.
        /// </summary>
        public static List<double[]> Route(IsoReading r)
        {
            var points = new List<double[]> { new[] { 0.0, 0.0, 0.0 } };
            foreach (var leg in r.Legs.Where(l => l.Include))
            {
                var length = leg.Length ?? 1000;
                var d = (leg.Direction ?? "").Trim().ToLowerInvariant().Replace("north", "n").Replace("south", "s").Replace("east", "e").Replace("west", "w");
                double x = 0, y = 0, z = 0;
                if (d == "up" || d == "down") z = d == "up" ? length : -length;
                else
                {
                    if (d.Contains("n")) y = 1; if (d.Contains("s")) y = -1;
                    if (d.Contains("e")) x = 1; if (d.Contains("w")) x = -1;
                    var n = Math.Sqrt(x * x + y * y);
                    if (n == 0) { x = 1; n = 1; }
                    z = Math.Max(-length, Math.Min(length, leg.Rise ?? 0));
                    var plan = Math.Sqrt(Math.Max(0, length * length - z * z));
                    x *= plan / n; y *= plan / n;
                }
                var last = points[points.Count - 1];
                points.Add(new[] { last[0] + x, last[1] + y, last[2] + z });
            }
            return points;
        }

        /// <summary>A point of the route on the isometric sheet: east down to the right, north up to the right, up straight up (y up).</summary>
        public static double[] Project(double[] p) => new[] { (p[0] + p[1]) * Math.Cos(Math.PI / 6), (p[1] - p[0]) * 0.5 + p[2] };

        /// <summary>The route's length, rise and fittings, for a summary line.</summary>
        public static string Summary(List<PipeSection> sections)
        {
            var straight = sections.Where(s => s.TipoSegmento == "Tubulaosimples").ToList();
            return string.Format(CultureInfo.InvariantCulture, "{0} straight sections, {1:0.00} m, rising {2:+0.00;-0.00} m; {3} fittings",
                straight.Count, straight.Sum(s => s.Comprimento), straight.Sum(s => s.Elevacao), sections.Where(s => s.TipoSegmento != "Tubulaosimples").Sum(s => s.Quantidade));
        }
    }
}

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>The isometric import as the wizards of both interfaces run it: read, correct, check, put in a pipe segment.</summary>
    public class IsometricImportSession
    {
        public readonly IFlowsheet Flowsheet;
        public string File = "";
        public IsoReading Reading;
        public List<PipeSection> Sections = new List<PipeSection>();
        public List<ImportIssue> Issues = new List<ImportIssue>();

        /// <summary>The pipe segment the last profile went into, null before one.</summary>
        public ISimulationObject Applied;

        public static readonly string[] Steps = { "Isometric", "Route", "Pipe segment" };

        public static readonly string[] StepTexts =
        {
            "Choose the piping isometric: an image, a PDF or a DXF/DWG drawing. When the line runs over several sheets, choose them all (or a PDF with a page per sheet), and they are joined into one line. The AI Assistant reads its route, size and material with the model set up in its settings.",
            "The line and its route leg by leg, from where it starts to where it ends. Correct the directions, lengths, rises and fittings; the sketch redraws the route as you go.",
            "The checks against the bill of materials and the elevations, and the sections the pipe segment gets. Choose the pipe segment and make its hydraulic profile.",
        };

        public static readonly string NewPipe = "(a new pipe segment)";

        /// <summary>The tag of the pipe segment the import was opened for (from its editor); the last step offers it first.</summary>
        public string Target = "";

        public IsometricImportSession(IFlowsheet fs) { Flowsheet = fs; }

        /// <summary>The tag of the pipe segment with this name, or "" when there isn't one.</summary>
        public static string TagOf(IFlowsheet fs, string pipeName)
        {
            if (string.IsNullOrEmpty(pipeName) || !fs.SimulationObjects.TryGetValue(pipeName, out var o)) return "";
            return o.GraphicObject.ObjectType == ObjectType.Pipe ? o.GraphicObject.Tag : "";
        }

        /// <summary>Reads the isometric on a worker thread, without touching the flowsheet; call <see cref="Propose"/> on the UI thread afterwards.</summary>
        public void Read(CancellationToken ct, Action<string> step) => Reading = IsometricImport.Read(File, ct, step);

        public void Open(string json)
        {
            Reading = IsoReading.FromJson(json);
            if (Reading.Source.Length > 0) File = Reading.Source;
            Propose();
        }

        public void SaveReading(string path) => System.IO.File.WriteAllText(path, Reading.ToJson());

        /// <summary>The sections and the checks from the reading as it stands.</summary>
        public void Propose()
        {
            if (Reading == null) return;
            Issues = IsometricImport.Check(Reading);
            Sections = IsometricImport.Sections(Reading, Issues);
            Issues = Issues.OrderBy(i => i.Level).ToList();
        }

        /// <summary>The pipe segments of the flowsheet, after the choice of a new one.</summary>
        public List<string> Targets()
        {
            var list = new List<string> { NewPipe };
            list.AddRange(Flowsheet.SimulationObjects.Values.Where(o => o.GraphicObject.ObjectType == ObjectType.Pipe).Select(o => o.GraphicObject.Tag).OrderBy(t => t));
            return list;
        }

        /// <summary>Puts the sections in the pipe segment tagged <paramref name="target"/>, or in a new one.</summary>
        public ISimulationObject Apply(string target)
        {
            Propose();
            var obj = target == NewPipe ? null : Flowsheet.GetFlowsheetSimulationObject(target);
            Applied = IsometricImport.Apply(Flowsheet, Reading, Sections, obj);
            return Applied;
        }
    }
}
