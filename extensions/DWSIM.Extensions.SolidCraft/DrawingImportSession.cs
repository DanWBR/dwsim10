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

        /// <summary>
        /// Bring the flowsheet's own items and streams up to date with the reading, found by their
        /// tags, and add nothing: for a new revision of a drawing or model already imported.
        /// </summary>
        public bool UpdateExisting;
        public List<ImportIssue> Solve = new List<ImportIssue>();

        /// <summary>The equipment parameters the fit may move, found after a build where the Patreon edition's reconciliation is installed.</summary>
        public List<DrawingImportFit.Parameter> FitParameters = new List<DrawingImportFit.Parameter>();

        /// <summary>What the last fit did, null before one.</summary>
        public DrawingImportFit.Outcome Fitted;

        /// <summary>What <see cref="DrawingReading.Tidy"/> set straight in the reading.</summary>
        public List<ImportIssue> Tidied = new List<ImportIssue>();

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

        /// <summary>
        /// Reads the drawing and infers the process, on a worker thread: it does not touch the
        /// flowsheet (the Windows interface reaches its compounds and packages through the main
        /// window, which a worker thread does not see). Call <see cref="Propose"/> on the UI
        /// thread afterwards.
        /// </summary>
        public void Read(CancellationToken ct, Action<string> step)
        {
            if (DexpiImport.IsDexpi(File))
            {
                // a DEXPI model is read as it is, by dwsim-dexpi: no assistant, nothing to infer
                Reading = DrawingReading.FromJson(DexpiImport.Read(File, ct, step));
                return;
            }
            Reading = DrawingImportClient.Read(File, Kind, ct, step);
            step?.Invoke("AI Assistant: inferring the process from the drawing's text");
            try { DrawingImportClient.Infer(Reading, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Reading.Process.Evidence = "The process could not be inferred: " + ex.GetBaseException().Message; }
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
            Tidied = Reading.Tidy();
            // by a name the database knows, else by the CAS number an IUPAC name or a ChEBI identifier stands for
            var renamed = Reading.ResolveCompounds(n => DrawingImportBuilder.CompoundName(Flowsheet, n) ??
                (DexpiIdentifiers.CasOf(n) is string cas ? DrawingImportBuilder.CompoundName(Flowsheet, cas) : null));
            if (renamed.Count > 0)
                Tidied.Add(new ImportIssue(ImportIssue.Levels.Info, "", "compounds found by their CAS number or other name: " + string.Join(", ", renamed)));
            Setup = new ImportSetup();
            var names = Reading.Process.Compounds.Concat(Reading.Lines.SelectMany(l => l.Compounds.Keys.Concat(l.MoleFractions.Keys))).Distinct(StringComparer.OrdinalIgnoreCase);
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
        public List<string> UnmatchedCompounds() => Reading.Process.Compounds.Concat(Reading.Lines.SelectMany(l => l.Compounds.Keys.Concat(l.MoleFractions.Keys)))
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
                issues.Add(new ImportIssue(ImportIssue.Levels.Info, "", UpdateExisting
                    ? "the items and streams of the flowsheet with the reading's tags are brought up to date; nothing is added"
                    : "the flowsheet is not empty; the drawing is added to what is there"));
            issues.AddRange(Tidied);
            return issues.OrderBy(i => i.Level).ToList();
        }

        /// <summary>Builds the flowsheet; with <paramref name="solve"/>, solves it, settles the columns' reflux and compares with the table.</summary>
        public void Build(bool solve, Action<string> step)
        {
            step?.Invoke("Building the flowsheet");
            Built = UpdateExisting ? DrawingImportBuilder.Update(Flowsheet, Reading, Setup) : DrawingImportBuilder.Build(Flowsheet, Reading, Setup);
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
            Solve.AddRange(DrawingImportBuilder.DesignCheck(Flowsheet, Reading, Built));
            Solve.AddRange(DrawingImportBuilder.SettleControllers(Flowsheet, Built));
            Fitted = null;
            FitParameters = DrawingImportFit.Available ? DrawingImportFit.Parameters(Reading, Built) : new List<DrawingImportFit.Parameter>();
            try { Flowsheet.UpdateInterface(); } catch (Exception) { }
        }

        /// <summary>Whether the built flowsheet has parameters the fit can move and the fit is installed.</summary>
        public bool CanFit => Built != null && FitParameters.Count > 0 && DrawingImportFit.Available;

        /// <summary>
        /// Fits the equipment parameters to the table with the data reconciliation (Patreon edition).
        /// It may run off the interface's thread; <see cref="FinishFit"/> then solves and compares.
        /// </summary>
        public void Fit(CancellationToken ct, Action<string> log)
        {
            Fitted = DrawingImportFit.Fit(Flowsheet, Reading, Built, FitParameters, ct, log);
        }

        /// <summary>Solves the flowsheet at the fitted values and compares it with the table again.</summary>
        public void FinishFit()
        {
            if (Fitted == null) return;
            if (Fitted.Iterations == 0 && !Fitted.Converged)
            {
                Solve.Insert(0, new ImportIssue(ImportIssue.Levels.Warning, "", "the fit did not run: " + Fitted.Message));
                return;
            }
            var errors = Flowsheet.RequestCalculationAndWait() ?? new List<Exception>();
            Solve = new List<ImportIssue> { new ImportIssue(ImportIssue.Levels.Info, "", DrawingImportFit.Describe(Fitted)) };
            foreach (var e in errors.Take(10)) Solve.Add(new ImportIssue(ImportIssue.Levels.Error, "", e.GetBaseException().Message));
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
            return Built.Items.Count + " items and " + Built.Streams.Count + (Built.Updated ? " streams updated" : " streams built") +
                (Built.Pipes > 0 ? ", " + Built.Pipes + " pipe segments" : "") + (Built.Recycles > 0 ? ", " + Built.Recycles + " recycles" : "") + ". " +
                (withData > 0 ? (withData - off) + " of " + withData + " streams match the drawing's table" + (errors > 0 ? "; the solver reported errors" : "") + "." : "");
        }

        /// <summary>How many of the reading's items the flowsheet already has, by tag: an update is offered when there are any.</summary>
        public int MatchingTags()
        {
            if (Reading == null) return 0;
            return Reading.ItemsByTag().Keys.Count(tag =>
            {
                var obj = Flowsheet.GetFlowsheetSimulationObject(tag);
                return obj != null && obj.GraphicObject != null && obj.GraphicObject.ObjectType != DWSIM.Interfaces.Enums.GraphicObjects.ObjectType.MaterialStream;
            });
        }

        /// <summary>How many of the reading's lines give their size in the line number: pipe segments are offered when there are any.</summary>
        public int SizedLines() => Reading == null ? 0 : Reading.Lines.Count(l => l.Include && DrawingImportBuilder.LineSize(l.LineNumber) != null);

        public static bool IsReading(string file) => file.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        public static string FileFilterText => "Drawings and DEXPI models (*.png, *.jpg, *.pdf, *.dxf, *.dwg, *.xml)";

        /// <summary>The file types the import takes: the drawings the assistant reads and DEXPI models.</summary>
        public static string[] FileExtensions => DrawingImportClient.Extensions.Concat(DexpiImport.Extensions).ToArray();
    }
}
