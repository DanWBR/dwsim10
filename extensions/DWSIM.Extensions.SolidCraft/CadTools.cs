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

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>The items of the CAD Tools menu.</summary>
    public enum CadTool
    {
        PlantModel,
        UpdatePlantModel,
        ResetLayout,
        ProcessFlowDiagram,
        PipingAndInstrumentation,
        EquipmentDrawings,
        PipingIsometrics,
        DataSheets,
        PlotPlan
    }

    /// <summary>
    /// What a CAD Tools dialog shows and returns: the program that does the work and where it was
    /// found, where the output goes, and the options of the tool. Both interfaces draw their dialog
    /// from it (UI\WinForms for the Windows interface, UI\Avalonia for the cross-platform one).
    /// </summary>
    public class CadToolRequest
    {
        public CadTool Tool;

        /// <summary>The dialog title and the line under it.</summary>
        public string Title, Description;

        /// <summary>SolidCraft or CADCraft; null for a tool that runs neither.</summary>
        public string Program;

        /// <summary>The program's command-line executable, without extension.</summary>
        public string ProgramExe;

        /// <summary>The folder that holds the program: found, or chosen in the dialog.</summary>
        public string ProgramFolder;

        /// <summary>True when the tool writes one drawing (the output is its file), false for a folder.</summary>
        public bool OutputIsFile;

        /// <summary>The output folder, or the drawing's DXF file.</summary>
        public string Output;

        /// <summary>The label of the output field.</summary>
        public string OutputLabel;

        /// <summary>Open the result in the program when it is done.</summary>
        public bool OpenInApp = true;

        /// <summary>The label of the button that runs the tool.</summary>
        public string RunText = "Export";

        /// <summary>The options of the drawings and of the 3D model, kept in the settings.</summary>
        public ExportOptions Options;

        /// <summary>The simulation's name, which the drawing files start with.</summary>
        public string ModelName;

        /// <summary>Move the files the tool would replace to a Superseded folder before it runs.</summary>
        public bool KeepOldCopies;

        public bool HasProgram => !string.IsNullOrEmpty(Program);

        public bool IsDrawing => Program == "CADCraft";

        /// <summary>The 3D model's own options (what the model holds, the picture).</summary>
        public bool ShowsModelOptions => Tool == CadTool.PlantModel;

        /// <summary>The PFD and the P&amp;ID route their lines, and the clearance applies to them.</summary>
        public bool ShowsClearance => Tool == CadTool.ProcessFlowDiagram || Tool == CadTool.PipingAndInstrumentation;

        /// <summary>The tool opens what it made in the program.</summary>
        public bool ShowsOpenInApp => Tool != CadTool.UpdatePlantModel && Tool != CadTool.ResetLayout;

        /// <summary>True when the program's executable is in <see cref="ProgramFolder"/>.</summary>
        public bool ProgramFound => !HasProgram || ProgramFoundIn(ProgramFolder);

        public bool ProgramFoundIn(string folder)
            => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder.Trim(), CADCraftSession.Exe(ProgramExe)));

        /// <summary>The window icon: the 3D model for the SolidCraft tools, drawing sheets for the CADCraft ones.</summary>
        public Stream Icon()
            => typeof(CadToolRequest).Assembly.GetManifestResourceStream("DWSIM.Extensions.SolidCraft." + (IsDrawing ? "Sheets.ico" : "Model3D.ico"));

        /// <summary>The path for the export: the folder, or the drawing's file without its extension.</summary>
        public string OutputBase => OutputIsFile ? Path.Combine(Path.GetDirectoryName(Output), Path.GetFileNameWithoutExtension(Output)) : Output;
    }

    /// <summary>Fills a <see cref="CadToolRequest"/> for a flowsheet and keeps what the user chose.</summary>
    public static class CadTools
    {
        /// <summary>The outputs chosen in this session, by flowsheet and tool.</summary>
        static readonly Dictionary<string, string> Outputs = new Dictionary<string, string>();

        /// <summary>The 3D model, its data update and the layout reset share one folder.</summary>
        static string Key(IFlowsheet fs, CadTool tool)
        {
            var file = fs.FlowsheetOptions?.FilePath;
            var group = tool == CadTool.UpdatePlantModel || tool == CadTool.ResetLayout ? CadTool.PlantModel : tool;
            return (string.IsNullOrWhiteSpace(file) ? "flowsheet:" + fs.GetHashCode() : file) + "|" + group;
        }

        public static CadToolRequest Request(IFlowsheet fs, CadTool tool)
        {
            var name = PlantModelExtender.ModelName(fs);
            var drawings = ProcessFlowDiagramExtender.DrawingsFolder(fs);
            var r = new CadToolRequest { Tool = tool, Options = ExportOptions.Load(), OutputLabel = "Output folder", ModelName = name };
            switch (tool)
            {
                case CadTool.PlantModel:
                    r.Title = "3D Plant Model";
                    r.Description = "Builds the 3D model of the flowsheet and saves it as a SolidCraft design, a STEP file, a picture, the line list, the material take-off and a web viewer.";
                    r.Output = PlantModelExtender.OutputFolder(fs);
                    break;
                case CadTool.UpdatePlantModel:
                    r.Title = "Update 3D Plant Model Data";
                    r.Description = "Sends the operating data of the solved flowsheet to the 3D model without building it again: to SolidCraft when the model is open there, otherwise into the saved design.";
                    r.Output = PlantModelExtender.OutputFolder(fs);
                    r.OutputLabel = "3D model folder";
                    r.RunText = "Update";
                    break;
                case CadTool.ResetLayout:
                    r.Title = "Reset 3D Plant Layout";
                    r.Description = "Forgets the places of the items kept with the simulation, so the next 3D model follows the flowsheet drawing again. The last design is kept aside, renamed.";
                    r.Output = PlantModelExtender.OutputFolder(fs);
                    r.OutputLabel = "3D model folder";
                    r.RunText = "Reset";
                    break;
                case CadTool.ProcessFlowDiagram:
                    r.Title = "Process Flow Diagram";
                    r.Description = "Draws the process flow diagram of the flowsheet with its stream table, saved as DXF, DWG, PDF and PNG.";
                    r.OutputIsFile = true;
                    r.Output = Path.Combine(drawings, name + " - PFD.dxf");
                    break;
                case CadTool.PipingAndInstrumentation:
                    r.Title = "Piping and Instrumentation Diagram";
                    r.Description = "Draws the P&ID of the flowsheet with line numbers, control loops, indicators, relief valves and a legend sheet, saved as DXF, DWG, PDF and PNG.";
                    r.OutputIsFile = true;
                    r.Output = Path.Combine(drawings, name + " - PID.dxf");
                    break;
                case CadTool.EquipmentDrawings:
                    r.Title = "Equipment Drawings";
                    r.Description = "Draws the general arrangement of each vessel, column, tank, exchanger, pump and compressor, one drawing each, saved as DXF, DWG, PDF and PNG.";
                    r.Output = drawings;
                    break;
                case CadTool.PipingIsometrics:
                    r.Title = "Piping Isometrics";
                    r.Description = "Draws the isometric of each line of the 3D plant model with its line data, welds, spools and bill of materials, saved as DXF, DWG, PDF and PNG.";
                    r.Output = drawings;
                    break;
                case CadTool.DataSheets:
                    r.Title = "Data Sheets";
                    r.Description = "Writes the data sheets of the equipment, the control loops and the local indicators, saved as DXF, DWG, PDF and PNG.";
                    r.Output = drawings;
                    break;
                case CadTool.PlotPlan:
                    r.Title = "Plot Plan";
                    r.Description = "Draws the plot plan of the 3D plant model with the equipment coordinates, the pipe rack, the road and the battery limit, saved as DXF, DWG, PDF and PNG.";
                    r.OutputIsFile = true;
                    r.Output = Path.Combine(drawings, name + " - Plot Plan.dxf");
                    break;
            }
            if (r.OutputIsFile) r.OutputLabel = "Drawing file";
            if (tool == CadTool.PlantModel || tool == CadTool.UpdatePlantModel)
            {
                r.Program = "SolidCraft";
                r.ProgramExe = "solidcraft-cli";
                r.ProgramFolder = SolidCraftRunner.FindInstallFolder() ?? GlobalSettings.Settings.SolidCraftPath ?? "";
            }
            else if (tool != CadTool.ResetLayout)
            {
                r.Program = "CADCraft";
                r.ProgramExe = "cadcraft-cli";
                r.ProgramFolder = CADCraftSession.FindInstallFolder() ?? r.Options.CADCraftPath ?? "";
            }
            if (Outputs.TryGetValue(Key(fs, tool), out var kept)) r.Output = kept;
            return r;
        }

        /// <summary>Keeps the program folder and the options in the settings, and the output for this session.</summary>
        public static void Accept(IFlowsheet fs, CadToolRequest r)
        {
            var folder = (r.ProgramFolder ?? "").Trim();
            if (r.Program == "SolidCraft" && folder != SolidCraftRunner.FindInstallFolder())
                GlobalSettings.Settings.SolidCraftPath = folder;
            else if (r.Program == "CADCraft" && folder != CADCraftSession.FindInstallFolder())
                r.Options.CADCraftPath = folder;
            GlobalSettings.Settings.SolidCraftOptions = r.Options.ToJson();
            Outputs[Key(fs, r.Tool)] = r.Output;
        }

        /// <summary>The render sizes offered in the dialogs.</summary>
        public static readonly (int Width, int Height, string Text)[] RenderSizes =
        {
            (1280, 800, "1280 x 800"),
            (1600, 1000, "1600 x 1000"),
            (1920, 1080, "1920 x 1080 (Full HD)"),
            (2560, 1440, "2560 x 1440 (QHD)"),
            (3840, 2160, "3840 x 2160 (4K UHD)")
        };

        /// <summary>The samples per pixel of SolidCraft's GPU path tracer when the setup gives none.</summary>
        public const int DefaultRenderSamples = 64;

        /// <summary>The render sample counts offered in the dialogs; 0 is SolidCraft's own.</summary>
        public static readonly (int Samples, string Text)[] RenderSamples =
        {
            (16, "16 (draft)"),
            (0, "64 (SolidCraft default)"),
            (256, "256"),
            (1024, "1024"),
            (4096, "4096 (final)")
        };

        /// <summary>
        /// The time a render takes on a recent graphics card, s: about 25 s to load the scene, then
        /// 0.03 s per million pixel-samples (measured from 640 x 400 at 16 samples to 1920 x 1080 at 256).
        /// </summary>
        public static double RenderSeconds(int width, int height, int samples)
            => 25 + 0.03 * width * height / 1e6 * (samples > 0 ? samples : DefaultRenderSamples);

        /// <summary>"about 3 min on a recent graphics card".</summary>
        public static string RenderTimeText(int width, int height, int samples)
        {
            var t = RenderSeconds(width, height, samples);
            var text = t < 90 ? "1 min" : t < 5400 ? Math.Round(t / 60).ToString(CultureInfo.InvariantCulture) + " min"
                : (t / 3600).ToString("0.#", CultureInfo.InvariantCulture) + " h";
            return "about " + text + " on a recent graphics card";
        }

        /// <summary>Renders longer than this get the estimate in the warning colour.</summary>
        public const double LongRenderSeconds = 600;

        public static readonly string[] Standards = { "ISO", "ASME", "Petrobras" };

        static readonly string[] DrawingExtensions = { ".dxf", ".dwg", ".pdf", ".png" };

        /// <summary>
        /// The files of an earlier export that the drawing tool would replace: the drawing with its DWG,
        /// PDF and PNG (and the P&amp;ID's legend sheet), or, for the tools that draw a sheet per item or
        /// line, the sheets of this simulation already in the folder. The 3D model is left out: a new
        /// export keeps what was moved in SolidCraft, and replacing the model is how it is updated.
        /// </summary>
        public static List<string> ExistingOutputs(CadToolRequest r)
        {
            var found = new List<string>();
            try
            {
                switch (r.Tool)
                {
                    case CadTool.ProcessFlowDiagram:
                    case CadTool.PipingAndInstrumentation:
                    case CadTool.PlotPlan:
                        var b = r.OutputBase;
                        found.AddRange(DrawingExtensions.Select(e => b + e).Where(File.Exists));
                        if (r.Tool == CadTool.PipingAndInstrumentation) found.AddRange(new[] { b + " legend.pdf", b + " legend.png" }.Where(File.Exists));
                        break;
                    case CadTool.EquipmentDrawings:
                    case CadTool.PipingIsometrics:
                    case CadTool.DataSheets:
                        if (!Directory.Exists(r.Output)) break;
                        var prefix = r.ModelName + (r.Tool == CadTool.EquipmentDrawings ? " - GA " : r.Tool == CadTool.PipingIsometrics ? " - ISO " : " - DS ");
                        found.AddRange(Directory.GetFiles(r.Output, prefix + "*")
                            .Where(f => DrawingExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
                        break;
                }
            }
            catch (Exception) { }
            return found;
        }

        /// <summary>The warning the dialogs show before an export replaces <paramref name="files"/>.</summary>
        public static string OverwriteWarning(List<string> files)
        {
            var names = files.Select(Path.GetFileName).ToList();
            var shown = string.Join(", ", names.Take(6)) + (names.Count > 6 ? " and " + (names.Count - 6) + " more" : "");
            return (names.Count == 1 ? "1 file" : names.Count + " files") + " from an earlier export will be replaced: " + shown +
                ". Changes made to them in CADCraft or another CAD program are lost unless the old files are kept.";
        }

        /// <summary>Moves the files to a "Superseded" folder, named by date and time, next to them; returns the folder, or null when there was nothing to move.</summary>
        public static string SetAside(List<string> files)
        {
            if (files.Count == 0) return null;
            var folder = Path.Combine(Path.GetDirectoryName(files[0]), "Superseded " + DateTime.Now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            foreach (var f in files) File.Move(f, Path.Combine(folder, Path.GetFileName(f)));
            return folder;
        }

        /// <summary>Checks the request before the tool runs; returns the problem, or null.</summary>
        public static string Validate(CadToolRequest r)
        {
            if (!r.ProgramFound)
                return r.Program + " was not found in " + (string.IsNullOrWhiteSpace(r.ProgramFolder) ? "the folder given" : r.ProgramFolder) +
                    ": choose the folder that holds " + CADCraftSession.Exe(r.ProgramExe) + ".";
            if (string.IsNullOrWhiteSpace(r.Output)) return "Choose where to save the output.";
            try
            {
                var full = Path.GetFullPath(r.Output);
                if (r.OutputIsFile && string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(full))) return "Give the drawing a file name.";
            }
            catch (Exception ex)
            {
                return "The output path is not valid (" + ex.Message + ").";
            }
            return null;
        }
    }
}
