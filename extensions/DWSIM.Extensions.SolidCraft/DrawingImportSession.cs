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
using System.IO;
using System.Linq;
using System.Threading;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The import wizard's state and steps, shared by both interfaces: the drawing, the reading the
    /// user corrects page by page, the compounds, package and units, the checks, and the build.
    /// </summary>
    public class DrawingImportSession
    {
        public readonly IFlowsheet Flowsheet;

        public string File = "";

        /// <summary>"auto", "pfd" or "pid".</summary>
        public string Kind = "auto";

        public DrawingReading Reading;
        public ImportSetup Setup = new ImportSetup();
        public DrawingImportBuilder.Result Built;
        public List<ImportIssue> Solve = new List<ImportIssue>();

        public static readonly string[] Steps = { "Drawing", "Equipment", "Lines", "Process", "Check and build" };

        public static readonly string[] StepTexts =
        {
            "Choose the PFD or P&ID: an image, a PDF or a DXF/DWG drawing. The AI Assistant reads it with the model set up in its settings.",
            "The items read on the drawing. Correct the tags and what each one is; clear the ones not to build. The dots show where each item was found.",
            "The lines between the items, with the stream table's values. Correct where each line comes from and goes to; the sketch shows the connections.",
            "The kind of process, inferred from the drawing's text, and the compounds, property package and units of the flowsheet.",
            "The checks on the reading. Build the flowsheet when there are no errors left; the items are set up from the stream table and the flowsheet is solved.",
        };

        public static readonly string[] TemperatureUnits = { "C", "K", "F", "R" };
        public static readonly string[] PressureUnits = { "bar", "barg", "Pa", "kPa", "MPa", "atm", "mbar", "kgf/cm2", "kgf/cm2g", "psi", "psig", "mmHg" };
        public static readonly string[] MassFlowUnits = { "kg/h", "kg/s", "g/s", "t/h", "kg/d", "lb/h", "lb/s" };
        public static readonly string[] EnergyUnits = { "kW", "MW", "W", "kcal/h", "Mcal/h", "BTU/h", "MMBTU/h", "kJ/h" };

        public DrawingImportSession(IFlowsheet fs) { Flowsheet = fs; }

        /// <summary>Reads the drawing and infers the process; then the compounds, package and units are proposed.</summary>
        public void Read(CancellationToken ct, Action<string> step)
        {
            Reading = DrawingImportClient.Read(File, Kind, ct, step);
            step?.Invoke("AI Assistant: inferring the process from the drawing's text");
            try { DrawingImportClient.Infer(Reading, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Reading.Process.Evidence = "The process could not be inferred: " + ex.GetBaseException().Message; }
            Propose();
        }

        public void Open(string json)
        {
            Reading = DrawingReading.FromJson(json);
            if (File.Length == 0) File = Reading.Source;
            Propose();
        }

        public void SaveReading(string path) => System.IO.File.WriteAllText(path, Reading.ToJson());

        /// <summary>
        /// What the reading suggests for the flowsheet: the compounds the model named and those the
        /// stream table lists, matched to the database, and the package the kind of process calls for.
        /// </summary>
        public void Propose()
        {
            Setup = new ImportSetup();
            var names = Reading.Process.Compounds.Concat(Reading.Lines.SelectMany(l => l.Compounds.Keys)).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var n in names)
            {
                var db = DrawingImportBuilder.CompoundName(Flowsheet, n);
                if (db != null && !Setup.Compounds.Contains(db, StringComparer.OrdinalIgnoreCase)) Setup.Compounds.Add(db);
            }
            foreach (var c in Flowsheet.SelectedCompounds.Keys)
                if (!Setup.Compounds.Contains(c, StringComparer.OrdinalIgnoreCase)) Setup.Compounds.Add(c);
            Setup.PropertyPackage = Package(Reading.Process.Class) ?? AvailablePackages().FirstOrDefault(p => p == "Peng-Robinson (PR)") ?? AvailablePackages().FirstOrDefault() ?? "";
        }

        /// <summary>The names in the reading the database did not match.</summary>
        public List<string> UnmatchedCompounds() => Reading.Process.Compounds.Concat(Reading.Lines.SelectMany(l => l.Compounds.Keys))
            .Distinct(StringComparer.OrdinalIgnoreCase).Where(n => DrawingImportBuilder.CompoundName(Flowsheet, n) == null).ToList();

        /// <summary>The first package of a kind of process that this DWSIM has.</summary>
        public string Package(string processClass)
        {
            var available = AvailablePackages();
            foreach (var p in DrawingImport.PackagesFor(processClass) ?? new string[0])
            {
                var hit = available.FirstOrDefault(a => string.Equals(a, p, StringComparison.OrdinalIgnoreCase))
                    ?? available.FirstOrDefault(a => a.StartsWith(p.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
            }
            return null;
        }

        public List<string> AvailablePackages()
        {
            try { return Flowsheet.GetAvailablePropertyPackages().OrderBy(p => p).ToList(); }
            catch (Exception) { return Flowsheet.AvailablePropertyPackages.Keys.OrderBy(p => p).ToList(); }
        }

        public List<string> AvailableCompounds(string filter, int max = 200)
        {
            var f = (filter ?? "").Trim();
            return Flowsheet.AvailableCompounds
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .Where(kv => f.Length == 0 || kv.Key.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             (kv.Value.Formula ?? "").IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0 || (kv.Value.CAS_Number ?? "") == f)
                .Select(kv => kv.Key).OrderBy(k => k.Length).ThenBy(k => k).Take(max).ToList();
        }

        /// <summary>
        /// Puts the proposed compounds and package in the flowsheet, so the Simulation Wizard opens
        /// on them; <see cref="TakeCompoundsAndPackage"/> reads back what the user left there.
        /// </summary>
        public void ApplyCompoundsAndPackage()
        {
            foreach (var c in Setup.Compounds)
                if (!Flowsheet.SelectedCompounds.ContainsKey(c) && Flowsheet.AvailableCompounds.ContainsKey(c)) Flowsheet.AddCompound(c);
            if (!string.IsNullOrWhiteSpace(Setup.PropertyPackage) && Flowsheet.AvailablePropertyPackages.TryGetValue(Setup.PropertyPackage, out var model) &&
                !Flowsheet.PropertyPackages.Values.Any(p => p.GetType() == model.GetType()))
                Flowsheet.CreateAndAddPropertyPackage(Setup.PropertyPackage);
        }

        public void TakeCompoundsAndPackage()
        {
            Setup.Compounds = Flowsheet.SelectedCompounds.Keys.ToList();
            var pp = Flowsheet.PropertyPackages.Values.FirstOrDefault();
            if (pp != null)
                Setup.PropertyPackage = Flowsheet.AvailablePropertyPackages.FirstOrDefault(kv => kv.Value.GetType() == pp.GetType()).Key ?? Setup.PropertyPackage;
        }

        public List<ImportIssue> Check()
        {
            var issues = DrawingImportCheck.Check(Reading, Reading.CadTexts.Count > 0 ? Reading.CadTexts : null);
            if (Setup.Compounds.Count == 0) issues.Add(new ImportIssue(ImportIssue.Levels.Error, "", "choose the compounds"));
            if (string.IsNullOrWhiteSpace(Setup.PropertyPackage)) issues.Add(new ImportIssue(ImportIssue.Levels.Error, "", "choose a property package"));
            foreach (var n in UnmatchedCompounds())
                issues.Add(new ImportIssue(ImportIssue.Levels.Warning, n, "not found in the compound database; its flows are left out"));
            if (Flowsheet.SimulationObjects.Count > 0)
                issues.Add(new ImportIssue(ImportIssue.Levels.Info, "", "the flowsheet is not empty; the drawing is added to what is there"));
            return issues.OrderBy(i => i.Level).ToList();
        }

        /// <summary>Builds the flowsheet; with <paramref name="solve"/>, solves it, settles the columns' reflux and compares with the table.</summary>
        public void Build(bool solve, Action<string> step)
        {
            step?.Invoke("Building the flowsheet");
            Built = DrawingImportBuilder.Build(Flowsheet, Reading, Setup);
            try { Flowsheet.UpdateInterface(); } catch (Exception) { }
            Solve = new List<ImportIssue>();
            if (!solve) return;
            if (!GlobalSettings.Settings.CalculatorActivated)
            {
                Solve.Add(new ImportIssue(ImportIssue.Levels.Info, "", "the calculator is paused, so the flowsheet was not solved; resume it and solve to compare with the table"));
                return;
            }
            step?.Invoke("Solving the flowsheet");
            var errors = Flowsheet.RequestCalculationAndWait() ?? new List<Exception>();
            if (DrawingImportBuilder.SettleReflux(Built))
            {
                step?.Invoke("Solving again with the columns' reflux ratios");
                errors = Flowsheet.RequestCalculationAndWait() ?? new List<Exception>();
            }
            foreach (var e in errors.Take(10))
                Solve.Add(new ImportIssue(ImportIssue.Levels.Error, "", e.GetBaseException().Message));
            Solve.AddRange(DrawingImportBuilder.Compare(Reading, Built));
            try { Flowsheet.UpdateInterface(); } catch (Exception) { }
        }

        /// <summary>A line of the build's summary.</summary>
        public string Summary()
        {
            if (Built == null) return "";
            var withData = Built.Streams.Count(kv => !kv.Key.Feed && kv.Key.HasData);
            var off = Solve.Count(i => i.Level == ImportIssue.Levels.Warning && i.Subject.Length > 0);
            var errors = Solve.Count(i => i.Level == ImportIssue.Levels.Error);
            return Built.Items.Count + " items and " + Built.Streams.Count + " streams built" + (Built.Recycles > 0 ? ", " + Built.Recycles + " recycles" : "") + ". " +
                (withData > 0 ? (withData - off) + " of " + withData + " streams match the drawing's table" + (errors > 0 ? "; the solver reported errors" : "") + "." : "");
        }

        public static bool IsReading(string file) => file.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        public static string FileFilterText => "Drawings (*.png, *.jpg, *.pdf, *.dxf, *.dwg)";
    }
}
