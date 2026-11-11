//    The CAD imports from scripts: a flowsheet from a drawing, a pipe segment from an isometric,
//    equipment from its data sheet.
//    Copyright 2026 Daniel Wagner Oliveira de Medeiros
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
using System.IO;
using System.Linq;
using System.Threading;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The three CAD imports of the CAD Tools menu, for scripts and the FluentAPI (<c>fs.Inner</c>). Each one takes
    /// either the document itself, which the AI Assistant reads (it must be running, with a model that reads images),
    /// or a reading saved from the wizard or from an earlier call as JSON, which needs no assistant and gives the
    /// same result every time. The steps are those of the wizards, with the wizard's proposals taken as they come;
    /// the session returned holds the reading, the checks and what was built or changed.
    /// </summary>
    public static class CadImport
    {
        /// <summary>
        /// Builds a flowsheet from a process flow diagram or a P&amp;ID (image, PDF, DXF or DWG), from a DEXPI model
        /// (.xml, read by dwsim-dexpi without the assistant; several files separated by semicolons are read as one
        /// plant, the sheets of a P&amp;ID joined and a process model giving its stream data), or from a reading saved as .json. The compounds and the property package are the ones the reading suggests unless given. With
        /// <paramref name="solve"/> the flowsheet is solved and compared with the drawing's stream table; with
        /// <paramref name="fitToTable"/> (Patreon Edition) the equipment efficiencies and column reflux ratios are then
        /// fitted to the table. With <paramref name="pipeSegments"/> each line whose number gives its size gets a pipe
        /// segment. Errors from the checks stop the build: they are in the session's <c>Check()</c>.
        /// </summary>
        public static DrawingImportSession ImportFromDrawing(this IFlowsheet fs, string file, bool solve = true,
            string propertyPackage = null, IEnumerable<string> compounds = null, bool fitToTable = false,
            string kind = "auto", Action<string> progress = null, CancellationToken cancel = default, bool pipeSegments = false)
        {
            var s = new DrawingImportSession(fs);
            if (DrawingImportSession.IsReading(file))
                s.Open(ReadText(file));
            else
            {
                s.File = Existing(file);
                s.Kind = kind ?? "auto";
                s.Read(cancel, progress);
                s.Propose();
            }
            s.Setup.PipeSegments = pipeSegments;
            if (!string.IsNullOrWhiteSpace(propertyPackage)) s.Setup.PropertyPackage = propertyPackage;
            if (compounds != null) s.Setup.Compounds = compounds.ToList();

            var errors = s.Check().Where(i => i.Level == ImportIssue.Levels.Error).ToList();
            if (errors.Count > 0)
                throw new InvalidOperationException("The drawing was not built: " + string.Join("; ", errors.Select(Describe)) + ".");

            s.Build(solve, progress);
            if (solve && fitToTable && s.CanFit)
            {
                s.Fit(cancel, progress);
                s.FinishFit();
            }
            return s;
        }

        /// <summary>
        /// Brings a flowsheet up to date with a new revision of its drawing or DEXPI model (or a reading saved as
        /// .json): the items and streams are found by their tags, the feeds take the conditions, flows and
        /// compositions of the reading and the items its specifications, and nothing is added. With
        /// <paramref name="solve"/> the flowsheet is solved and compared with the reading's stream data; what was
        /// not found on either side is in the session's <c>Built.Issues</c>.
        /// </summary>
        public static DrawingImportSession UpdateFromDrawing(this IFlowsheet fs, string file, bool solve = true,
            string kind = "auto", Action<string> progress = null, CancellationToken cancel = default)
        {
            var s = new DrawingImportSession(fs) { UpdateExisting = true };
            if (DrawingImportSession.IsReading(file))
                s.Open(ReadText(file));
            else
            {
                s.File = Existing(file);
                s.Kind = kind ?? "auto";
                s.Read(cancel, progress);
                s.Propose();
            }
            s.Setup.UseDrawingUnits = false;
            if (s.MatchingTags() == 0)
                throw new InvalidOperationException("Nothing to update: the flowsheet has none of the items of the drawing, by their tags.");
            s.Build(solve, progress);
            return s;
        }

        /// <summary>
        /// Puts the route of a piping isometric (image or PDF, or a reading saved as .json) in the hydraulic profile of
        /// the pipe segment tagged <paramref name="pipeTag"/>, or of a new pipe segment named after the line when no
        /// tag is given. The checks against the bill of materials and the elevations are in the session's <c>Issues</c>.
        /// </summary>
        public static IsometricImportSession ImportPipeFromIsometric(this IFlowsheet fs, string file, string pipeTag = null,
            Action<string> progress = null, CancellationToken cancel = default)
        {
            var s = new IsometricImportSession(fs);
            if (DrawingImportSession.IsReading(file))
                s.Open(ReadText(file));
            else
            {
                s.File = Existing(file);
                s.Read(cancel, progress);
                s.Propose();
            }
            if (!string.IsNullOrWhiteSpace(pipeTag) && fs.GetFlowsheetSimulationObject(pipeTag) == null)
                throw new KeyNotFoundException("No object tagged '" + pipeTag + "' in the flowsheet.");
            s.Apply(string.IsNullOrWhiteSpace(pipeTag) ? IsometricImportSession.NewPipe : pipeTag);
            return s;
        }

        /// <summary>
        /// Sets equipment up from its data sheet (PDF or image, or a reading saved as .json): pumps, compressors,
        /// expanders, shell and tube exchangers, vessels and control valves. The equipment is the one tagged
        /// <paramref name="equipmentTag"/>, else the one the sheet names, else a new one with its streams from the
        /// operating case. Every change the wizard proposes is made; the flowsheet is then solved and the outlet
        /// compared with the sheet (the session's <c>Changes</c> and <c>Results</c>).
        /// </summary>
        public static DataSheetImportSession SetUpFromDataSheet(this IFlowsheet fs, string file, string equipmentTag = null,
            string operatingCase = null, Action<string> progress = null, CancellationToken cancel = default)
        {
            var s = new DataSheetImportSession(fs);
            if (DrawingImportSession.IsReading(file))
                s.Open(ReadText(file));
            else
            {
                s.File = Existing(file);
                s.Read(cancel, progress);
                s.Loaded();
            }
            if (!string.IsNullOrWhiteSpace(operatingCase)) s.Case = operatingCase;
            if (!string.IsNullOrWhiteSpace(equipmentTag))
            {
                if (fs.GetFlowsheetSimulationObject(equipmentTag) == null)
                    throw new KeyNotFoundException("No object tagged '" + equipmentTag + "' in the flowsheet.");
                s.Choose(equipmentTag);
            }
            s.Propose();
            s.Apply();
            return s;
        }

        private static string Existing(string file)
        {
            // several DEXPI files go as one text, separated by semicolons
            if (!File.Exists(file) && !(DexpiImport.IsDexpi(file) && DexpiImport.Exist(file))) throw new FileNotFoundException("Document not found.", file);
            return file;
        }

        private static string ReadText(string file) => File.ReadAllText(Existing(file));

        private static string Describe(ImportIssue i) => string.IsNullOrEmpty(i.Subject) ? i.Text : i.Subject + ": " + i.Text;
    }
}
