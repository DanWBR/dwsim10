//    Scenario comparison: two snapshots of the flowsheet's results side by side, with the differences.
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
using System.Text;
using System.Xml.Linq;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.Automation.DynamicRunner.Scenarios
{
    /// <summary>One property of one object as it was when the snapshot was taken, in the flowsheet's units.</summary>
    public sealed class ScenarioValue
    {
        public string ObjectName = "";
        public string ObjectTag = "";
        public string ObjectType = "";
        public string Property = "";
        /// <summary>The property's readable name.</summary>
        public string Name = "";
        public string Unit = "";
        /// <summary>NaN when the property is not a number; then Text carries it.</summary>
        public double Value = double.NaN;
        public string Text = "";
        /// <summary>True for a specification (a property the user writes), false for a result.</summary>
        public bool IsInput;
    }

    /// <summary>Every numeric property of every solved object, at one moment.</summary>
    public sealed class ScenarioSnapshot
    {
        public const string FileExtension = ".dwsnap";

        public string Label = "";
        public DateTime Taken = DateTime.Now;
        public string FlowsheetPath = "";
        public string UnitSystem = "";
        public List<ScenarioValue> Values = new List<ScenarioValue>();

        public void SaveToFile(string path)
        {
            var ci = CultureInfo.InvariantCulture;
            var root = new XElement("ScenarioSnapshot", new XAttribute("version", 1),
                new XElement("Label", Label), new XElement("Taken", Taken.ToString("o", ci)), new XElement("FlowsheetPath", FlowsheetPath), new XElement("UnitSystem", UnitSystem));
            foreach (var v in Values)
                root.Add(new XElement("Value",
                    new XAttribute("object", v.ObjectName), new XAttribute("tag", v.ObjectTag), new XAttribute("type", v.ObjectType),
                    new XAttribute("property", v.Property), new XAttribute("name", v.Name), new XAttribute("unit", v.Unit),
                    new XAttribute("value", double.IsNaN(v.Value) ? "" : v.Value.ToString("R", ci)), new XAttribute("text", v.Text ?? ""), new XAttribute("input", v.IsInput ? "1" : "0")));
            new XDocument(root).Save(path);
        }

        public static ScenarioSnapshot LoadFromFile(string path)
        {
            var ci = CultureInfo.InvariantCulture;
            var doc = XDocument.Load(path);
            if (doc.Root == null || doc.Root.Name != "ScenarioSnapshot") throw new InvalidOperationException("'" + Path.GetFileName(path) + "' is not a scenario snapshot.");
            var s = new ScenarioSnapshot { Label = (string)doc.Root.Element("Label") ?? "", FlowsheetPath = (string)doc.Root.Element("FlowsheetPath") ?? "", UnitSystem = (string)doc.Root.Element("UnitSystem") ?? "" };
            DateTime t;
            if (DateTime.TryParse((string)doc.Root.Element("Taken"), ci, DateTimeStyles.RoundtripKind, out t)) s.Taken = t;
            foreach (var e in doc.Root.Elements("Value"))
            {
                var v = new ScenarioValue
                {
                    ObjectName = (string)e.Attribute("object") ?? "", ObjectTag = (string)e.Attribute("tag") ?? "", ObjectType = (string)e.Attribute("type") ?? "",
                    Property = (string)e.Attribute("property") ?? "", Name = (string)e.Attribute("name") ?? "", Unit = (string)e.Attribute("unit") ?? "",
                    Text = (string)e.Attribute("text") ?? "", IsInput = ((string)e.Attribute("input") ?? "0") == "1"
                };
                double d;
                if (double.TryParse((string)e.Attribute("value"), NumberStyles.Float, ci, out d)) v.Value = d;
                s.Values.Add(v);
            }
            return s;
        }
    }

    /// <summary>One property in both snapshots.</summary>
    public sealed class ScenarioDifference
    {
        public string ObjectName = "";
        public string ObjectTag = "";
        public string ObjectType = "";
        public string Property = "";
        public string Name = "";
        public string Unit = "";
        public bool IsInput;
        public double A = double.NaN;
        public double B = double.NaN;
        public string TextA = "";
        public string TextB = "";
        public double Delta { get { return B - A; } }
        /// <summary>Relative change against A, in percent; NaN when A is zero or the value is text.</summary>
        public double Percent { get { return double.IsNaN(A) || double.IsNaN(B) || Math.Abs(A) < 1e-300 ? double.NaN : 100.0 * (B - A) / Math.Abs(A); } }
        public bool Changed;
    }

    public sealed class ScenarioComparisonResult
    {
        public string LabelA = "";
        public string LabelB = "";
        public List<ScenarioDifference> Differences = new List<ScenarioDifference>();
        public List<string> OnlyInA = new List<string>();
        public List<string> OnlyInB = new List<string>();
        public int InputsChanged;
        public int OutputsChanged;
        public int Unchanged;
        /// <summary>What the two runs say, one paragraph per entry.</summary>
        public List<string> Summary = new List<string>();
        public string TextReport = "";
    }

    /// <summary>
    /// Scenario comparison: takes a snapshot of every property of every object after a solve,
    /// and lays two snapshots side by side, the changed specifications first and the results
    /// ordered by how much they moved: what changing one number did to the whole flowsheet.
    /// </summary>
    public static class ScenarioComparison
    {
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Every property of every process object, in the flowsheet's units.</summary>
        public static ScenarioSnapshot Snapshot(IFlowsheet fs, string label)
        {
            if (fs == null) throw new ArgumentNullException("fs");
            var su = fs.FlowsheetOptions.SelectedUnitSystem;
            var s = new ScenarioSnapshot { Label = label ?? "", FlowsheetPath = fs.FlowsheetOptions.FilePath ?? "", UnitSystem = su != null ? su.Name : "" };
            foreach (var obj in fs.SimulationObjects.Values.OrderBy(o => o.GraphicObject != null ? o.GraphicObject.Tag : o.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (obj.GraphicObject == null) continue;
                var type = obj.GraphicObject.ObjectType;
                if (type == ObjectType.GO_Text || type == ObjectType.GO_Image || type == ObjectType.GO_Table || type == ObjectType.GO_MasterTable || type == ObjectType.GO_SpreadsheetTable || type == ObjectType.GO_FloatingTable || type == ObjectType.GO_Chart || type == ObjectType.GO_Rectangle || type == ObjectType.GO_HTMLText) continue;
                string[] inputs, all;
                try { inputs = obj.GetProperties(PropertyType.WR) ?? new string[0]; } catch (Exception) { inputs = new string[0]; }
                try { all = obj.GetProperties(PropertyType.ALL) ?? new string[0]; } catch (Exception) { continue; }
                var writable = new HashSet<string>(inputs);
                // Which writable properties are the specifications of the current calculation mode: a stream
                // fed by a unit has none; a unit's are the slots the degrees-of-freedom analysis lists, matched
                // by value in SI (the slots name CLR properties, the property ids name the same numbers).
                bool isStream = type == ObjectType.MaterialStream || type == ObjectType.EnergyStream;
                // an energy stream carries what a unit gives or takes; when a unit reads it, the unit's own duty slot is the spec
                bool fedByUnit = type == ObjectType.EnergyStream || (isStream && obj.GraphicObject.InputConnectors.Any(c => c.IsAttached));
                List<double> specValues = null;
                if (!isStream)
                {
                    try
                    {
                        var dof = DegreesOfFreedomAnalysis.Analyze(fs, obj);
                        if (dof != null && dof.Supported) specValues = dof.Slots.Where(sl => sl.Required && sl.IsSet && sl.Value.HasValue).Select(sl => sl.Value.Value).ToList();
                    }
                    catch (Exception) { }
                }
                foreach (var prop in all.Distinct())
                {
                    object raw;
                    try { raw = obj.GetPropertyValue(prop, su); } catch (Exception) { continue; }
                    if (raw == null) continue;
                    bool isInput = writable.Contains(prop) && !fedByUnit;
                    if (isInput && specValues != null)
                    {
                        double si;
                        try { si = Convert.ToDouble(obj.GetPropertyValue(prop, null), Ci); } catch (Exception) { si = double.NaN; }
                        isInput = !double.IsNaN(si) && specValues.Any(sv => Math.Abs(sv - si) <= 1e-7 * Math.Max(1.0, Math.Abs(sv)));
                    }
                    var v = new ScenarioValue { ObjectName = obj.Name, ObjectTag = obj.GraphicObject.Tag, ObjectType = type.ToString(), Property = prop, IsInput = isInput };
                    try { v.Unit = obj.GetPropertyUnit(prop, su) ?? ""; } catch (Exception) { }
                    try { v.Name = fs.GetTranslatedString(prop); } catch (Exception) { v.Name = prop; }
                    if (string.IsNullOrEmpty(v.Name)) v.Name = prop;
                    double d;
                    if (raw is double || raw is float || raw is int || raw is long || raw is decimal) v.Value = Convert.ToDouble(raw, Ci);
                    else if (raw is bool) v.Value = (bool)raw ? 1 : 0;
                    else if (raw is string && double.TryParse((string)raw, NumberStyles.Float, Ci, out d)) v.Value = d;
                    else v.Text = raw.ToString();
                    if (double.IsNaN(v.Value) && string.IsNullOrEmpty(v.Text)) continue;
                    s.Values.Add(v);
                }
            }
            return s;
        }

        /// <summary>Lays snapshot B against A. A value counts as changed when it moved by more than the relative tolerance (and an absolute floor of 1e-9).</summary>
        public static ScenarioComparisonResult Compare(ScenarioSnapshot a, ScenarioSnapshot b, double relativeTolerance = 1e-6)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (b == null) throw new ArgumentNullException("b");
            var r = new ScenarioComparisonResult { LabelA = a.Label, LabelB = b.Label };
            var keyB = b.Values.ToDictionary(v => v.ObjectName + "" + v.Property, v => v);
            var seen = new HashSet<string>();
            var objectsA = new HashSet<string>(a.Values.Select(v => v.ObjectName));
            var objectsB = new HashSet<string>(b.Values.Select(v => v.ObjectName));
            r.OnlyInA = a.Values.Where(v => !objectsB.Contains(v.ObjectName)).Select(v => v.ObjectTag).Distinct().ToList();
            r.OnlyInB = b.Values.Where(v => !objectsA.Contains(v.ObjectName)).Select(v => v.ObjectTag).Distinct().ToList();

            foreach (var va in a.Values)
            {
                ScenarioValue vb;
                string key = va.ObjectName + "" + va.Property;
                if (!keyB.TryGetValue(key, out vb)) continue;
                seen.Add(key);
                var d = new ScenarioDifference
                {
                    ObjectName = va.ObjectName, ObjectTag = vb.ObjectTag, ObjectType = va.ObjectType, Property = va.Property, Name = va.Name, Unit = va.Unit,
                    IsInput = va.IsInput || vb.IsInput, A = va.Value, B = vb.Value, TextA = va.Text, TextB = vb.Text
                };
                if (!double.IsNaN(d.A) && !double.IsNaN(d.B))
                    d.Changed = Math.Abs(d.B - d.A) > Math.Max(1e-9, relativeTolerance * Math.Max(Math.Abs(d.A), Math.Abs(d.B)));
                else if (double.IsNaN(d.A) != double.IsNaN(d.B))
                    d.Changed = true;
                else
                    d.Changed = !string.Equals(d.TextA, d.TextB, StringComparison.Ordinal);
                r.Differences.Add(d);
            }
            r.InputsChanged = r.Differences.Count(d => d.Changed && d.IsInput);
            r.OutputsChanged = r.Differences.Count(d => d.Changed && !d.IsInput);
            r.Unchanged = r.Differences.Count(d => !d.Changed);

            // changed specifications first, then results by how much they moved
            r.Differences = r.Differences
                .OrderByDescending(d => d.Changed)
                .ThenByDescending(d => d.IsInput)
                .ThenByDescending(d => double.IsNaN(d.Percent) ? (d.Changed ? 1e300 : -1) : Math.Abs(d.Percent))
                .ThenBy(d => d.ObjectTag, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            Summarize(r);
            r.TextReport = Report(r);
            return r;
        }

        private static void Summarize(ScenarioComparisonResult r)
        {
            string la = string.IsNullOrEmpty(r.LabelA) ? "A" : r.LabelA, lb = string.IsNullOrEmpty(r.LabelB) ? "B" : r.LabelB;
            var inputs = r.Differences.Where(d => d.Changed && d.IsInput).ToList();
            var outputs = r.Differences.Where(d => d.Changed && !d.IsInput).ToList();
            if (inputs.Count == 0 && outputs.Count == 0) { r.Summary.Add("The two scenarios are the same: no specification and no result differs."); return; }
            if (inputs.Count > 0)
                r.Summary.Add(inputs.Count + " specification(s) changed from " + la + " to " + lb + ": " + string.Join("; ", inputs.Take(6).Select(d => d.ObjectTag + " " + d.Name + " " + Fmt(d.A) + " -> " + Fmt(d.B) + (string.IsNullOrEmpty(d.Unit) ? "" : " " + d.Unit))) + (inputs.Count > 6 ? "; ..." : "") + ".");
            else
                r.Summary.Add("No specification changed, yet " + outputs.Count + " result(s) differ: the flowsheet was solved from a different starting point, a recycle converged elsewhere, or something outside the properties listed here (a reaction, a property package parameter) was edited.");
            if (outputs.Count > 0)
            {
                var top = outputs.Where(d => !double.IsNaN(d.Percent)).Take(6).ToList();
                r.Summary.Add(outputs.Count + " result(s) moved" + (top.Count > 0 ? "; the largest relative changes: " + string.Join("; ", top.Select(d => d.ObjectTag + " " + d.Name + " " + d.Percent.ToString("+0.0;-0.0", Ci) + " %")) + "." : "."));
                var objects = outputs.Select(d => d.ObjectTag).Distinct().ToList();
                var untouched = r.Differences.Where(d => !d.IsInput).Select(d => d.ObjectTag).Distinct().Except(objects).ToList();
                if (untouched.Count > 0) r.Summary.Add(objects.Count + " object(s) felt the change; " + untouched.Count + " did not (" + string.Join(", ", untouched.Take(8)) + (untouched.Count > 8 ? ", ..." : "") + "): they sit upstream of the change, or on a branch it does not reach.");
                if (inputs.Count > 0 && inputs.Count <= 2)
                    r.Summary.Add("With " + (inputs.Count == 1 ? "one specification" : "two specifications") + " changed, every difference below is its effect: read the table as a sensitivity, from the object edited down the flowsheet.");
            }
            if (r.OnlyInA.Count > 0) r.Summary.Add("Only in " + la + ": " + string.Join(", ", r.OnlyInA) + ".");
            if (r.OnlyInB.Count > 0) r.Summary.Add("Only in " + lb + ": " + string.Join(", ", r.OnlyInB) + ".");
        }

        private static string Fmt(double v) { return double.IsNaN(v) ? "-" : v.ToString("G5", Ci); }

        private static string Report(ScenarioComparisonResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Scenario comparison: " + (string.IsNullOrEmpty(r.LabelA) ? "A" : r.LabelA) + " against " + (string.IsNullOrEmpty(r.LabelB) ? "B" : r.LabelB));
            sb.AppendLine();
            foreach (var s in r.Summary) sb.AppendLine(s);
            sb.AppendLine();
            sb.AppendLine("Object".PadRight(24) + "Property".PadRight(40) + "A".PadLeft(14) + "B".PadLeft(14) + "change".PadLeft(14) + "  %".PadLeft(9) + "  unit");
            foreach (var d in r.Differences.Where(x => x.Changed))
            {
                string a = double.IsNaN(d.A) ? d.TextA : Fmt(d.A), b = double.IsNaN(d.B) ? d.TextB : Fmt(d.B);
                sb.AppendLine((d.ObjectTag + (d.IsInput ? " *" : "")).PadRight(24) + Trunc(d.Name, 39).PadRight(40) + a.PadLeft(14) + b.PadLeft(14) + (double.IsNaN(d.Delta) ? "" : Fmt(d.Delta)).PadLeft(14) + (double.IsNaN(d.Percent) ? "" : d.Percent.ToString("+0.00;-0.00", Ci)).PadLeft(9) + "  " + d.Unit);
            }
            sb.AppendLine();
            sb.AppendLine("* specification. " + r.Unchanged + " value(s) unchanged.");
            return sb.ToString();
        }

        private static string Trunc(string s, int n) { return s.Length <= n ? s : s.Substring(0, n - 1) + "~"; }
    }
}
