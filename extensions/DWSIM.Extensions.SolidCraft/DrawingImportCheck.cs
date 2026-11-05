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

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>A finding of the checks on a reading, or of the build.</summary>
    public class ImportIssue
    {
        public enum Levels { Error, Warning, Info }

        public Levels Level;

        /// <summary>The item or line it is about (a tag or a stream name), empty for the whole drawing.</summary>
        public string Subject = "";

        public string Text = "";

        public ImportIssue(Levels level, string subject, string text) { Level = level; Subject = subject ?? ""; Text = text; }

        public override string ToString() => (Level == Levels.Error ? "Error" : Level == Levels.Warning ? "Warning" : "Note") + (Subject.Length > 0 ? " (" + Subject + ")" : "") + ": " + Text;
    }

    /// <summary>
    /// Checks a reading without the model that made it: every line ends at a known item or at the
    /// battery limit, no item has more lines than its DWSIM object has nozzles, every item is fed
    /// and drained, the stream table balances around each item, and the tags the drawing's text
    /// holds (when its text is known) match the items read.
    /// </summary>
    public static class DrawingImportCheck
    {
        /// <summary>The material inlets and outlets of each DWSIM object the import makes.</summary>
        public static void Nozzles(string type, out int inlets, out int outlets)
        {
            switch (type)
            {
                case "Mixer": inlets = 6; outlets = 1; break;
                case "Splitter": inlets = 1; outlets = 3; break;
                case "Vessel": inlets = 6; outlets = 3; break;
                case "ShortcutColumn": inlets = 1; outlets = 2; break;
                case "DistillationColumn": inlets = 10; outlets = 10; break;
                case "AbsorptionColumn": inlets = 10; outlets = 10; break;
                case "HeatExchanger": inlets = 2; outlets = 2; break;
                case "RCT_Conversion":
                case "RCT_Equilibrium":
                case "RCT_Gibbs": inlets = 1; outlets = 2; break;
                case "Filter":
                case "ComponentSeparator": inlets = 1; outlets = 2; break;
                default: inlets = 1; outlets = 1; break;
            }
        }

        public static List<ImportIssue> Check(DrawingReading r, IEnumerable<string> drawingTexts = null)
        {
            var issues = new List<ImportIssue>();
            var items = r.ItemsByTag();
            var lines = r.Lines.Where(r.Takes).ToList();
            var leftOut = r.Lines.Count(l => l.Include && !r.Takes(l));
            if (leftOut > 0) issues.Add(new ImportIssue(ImportIssue.Levels.Info, "", leftOut + " lines end at items left out and are not built"));

            foreach (var g in r.Items.Where(i => i.Include).GroupBy(i => i.Tag, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                issues.Add(new ImportIssue(ImportIssue.Levels.Error, g.Key, "the tag is used by " + g.Count() + " items; give each its own tag"));

            foreach (var l in lines)
            {
                var name = r.StreamTag(l);
                if (l.Feed && l.Product) issues.Add(new ImportIssue(ImportIssue.Levels.Error, name, "the line has no item at either end"));
                if (!l.Feed && !items.ContainsKey(l.From)) issues.Add(new ImportIssue(ImportIssue.Levels.Error, name, "it comes from " + l.From + ", which is not among the items"));
                if (!l.Product && !items.ContainsKey(l.To)) issues.Add(new ImportIssue(ImportIssue.Levels.Error, name, "it goes to " + l.To + ", which is not among the items"));
                if (!l.Feed && !l.Product && string.Equals(l.From, l.To, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new ImportIssue(ImportIssue.Levels.Error, name, "it starts and ends at " + l.From));
            }
            foreach (var g in lines.GroupBy(l => r.StreamTag(l), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                issues.Add(new ImportIssue(ImportIssue.Levels.Warning, g.Key, g.Count() + " lines have this name; the flowsheet numbers the repeats"));

            foreach (var item in items.Values)
            {
                var ins = lines.Count(l => string.Equals(l.To, item.Tag, StringComparison.OrdinalIgnoreCase));
                var outs = lines.Count(l => string.Equals(l.From, item.Tag, StringComparison.OrdinalIgnoreCase));
                Nozzles(item.Type, out var maxIn, out var maxOut);
                if (ins == 0 && outs == 0) issues.Add(new ImportIssue(ImportIssue.Levels.Warning, item.Tag, "no line reaches it; it is built unconnected"));
                else if (ins == 0) issues.Add(new ImportIssue(ImportIssue.Levels.Error, item.Tag, "nothing flows into it"));
                else if (outs == 0) issues.Add(new ImportIssue(ImportIssue.Levels.Error, item.Tag, "nothing flows out of it"));
                if (ins > maxIn) issues.Add(new ImportIssue(ImportIssue.Levels.Error, item.Tag, ins + " lines go in, and a " + item.Type + " takes " + maxIn + (item.Type == "ShortcutColumn" ? "; make it a rigorous column" : "")));
                if (outs > maxOut) issues.Add(new ImportIssue(ImportIssue.Levels.Error, item.Tag, outs + " lines come out, and a " + item.Type + " has " + maxOut + " outlets" + (item.Type == "ShortcutColumn" ? "; make it a rigorous column" : "")));
                if (item.Type == "HeatExchanger" && (ins != 2 || outs != 2) && ins + outs > 0)
                    issues.Add(new ImportIssue(ImportIssue.Levels.Warning, item.Tag, "a two-stream exchanger needs two lines in and two out; with one side only, make it a heater or a cooler"));
                if (item.Category == "Generic") issues.Add(new ImportIssue(ImportIssue.Levels.Warning, item.Tag, "the drawing does not say what it is; it becomes a " + item.Type));
                if (item.Type.StartsWith("RCT_"))
                    issues.Add(new ImportIssue(ImportIssue.Levels.Warning, item.Tag, "the drawing does not give the reactions; add them and the reactor's specifications after building"));
                if (item.Category == "Relief") issues.Add(new ImportIssue(ImportIssue.Levels.Info, item.Tag, "a relief valve becomes a valve; it does not open by itself in a steady state run"));

                // the stream table's mass balance around the item (a reactor too keeps its mass)
                var wIn = lines.Where(l => string.Equals(l.To, item.Tag, StringComparison.OrdinalIgnoreCase)).Select(l => l.W).ToList();
                var wOut = lines.Where(l => string.Equals(l.From, item.Tag, StringComparison.OrdinalIgnoreCase)).Select(l => l.W).ToList();
                if (wIn.Count > 0 && wOut.Count > 0 && wIn.All(w => w.HasValue) && wOut.All(w => w.HasValue))
                {
                    double a = wIn.Sum(w => w.Value), b = wOut.Sum(w => w.Value);
                    if (Math.Abs(a - b) > 0.02 * Math.Max(Math.Abs(a), Math.Abs(b)) + 1e-9)
                        issues.Add(new ImportIssue(ImportIssue.Levels.Warning, item.Tag, string.Format(CultureInfo.InvariantCulture,
                            "the table's flows do not balance: {0:G4} in, {1:G4} out ({2}); a line or a value may be read wrong", a, b, r.Units.MassFlow)));
                }
            }

            if (r.Units.MassFlow.StartsWith("m3", StringComparison.OrdinalIgnoreCase) || r.Units.MassFlow.EndsWith("m3/h", StringComparison.OrdinalIgnoreCase))
                issues.Add(new ImportIssue(ImportIssue.Levels.Warning, "", "the drawing gives volume flows (" + r.Units.MassFlow + "); set a mass flow unit on the Process page"));

            if (r.Kind == "pfd")
                foreach (var l in lines.Where(l => l.Feed && !l.Product))
                {
                    if (!l.T.HasValue || !l.P.HasValue || !l.W.HasValue)
                        issues.Add(new ImportIssue(ImportIssue.Levels.Warning, r.StreamTag(l), "the feed lacks its temperature, pressure or flow; enter them before building"));
                    else if (l.Compounds.Count == 0)
                        issues.Add(new ImportIssue(ImportIssue.Levels.Warning, r.StreamTag(l), "the table gives no composition for this feed"));
                }

            // the text of a CAD drawing: every tag read must be on it, and every item tag on it read
            if (drawingTexts != null)
            {
                var texts = new HashSet<string>(drawingTexts.Select(Norm));
                foreach (var item in items.Values.Where(i => i.Category != "Mixer" && i.Category != "Splitter"))
                    if (!texts.Contains(Norm(item.Tag)))
                        issues.Add(new ImportIssue(ImportIssue.Levels.Warning, item.Tag, "this tag is not written on the drawing; it may be read wrong"));
            }
            return issues;
        }

        static string Norm(string s) => new string((s ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    }
}
