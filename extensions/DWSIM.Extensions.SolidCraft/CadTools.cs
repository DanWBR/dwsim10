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
using Newtonsoft.Json;

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
        PlotPlan,
        AllDrawings
    }

    /// <summary>An item or line a drawing tool draws a sheet of, for the user to choose in its dialog.</summary>
    public class CadToolItem
    {
        public string Tag, Text;
        public bool Checked = true;

        public override string ToString() => Text;
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

        /// <summary>What the tools that draw a sheet per item or line would draw, to choose from; null for the others.</summary>
        public List<CadToolItem> Items;

        /// <summary>The SolidCraft design of the 3D model, when the drawings take the places of its items from it; null when there is none.</summary>
        public string Design;

        /// <summary>The design was saved in SolidCraft after DWSIM last read the places of its items.</summary>
        public bool DesignChanged;

        /// <summary>Read the places of the items moved in SolidCraft before drawing.</summary>
        public bool ReadMoves;

        /// <summary>What the dialogs say about <see cref="Design"/>.</summary>
        public string DesignText => Design == null ? ""
            : DesignChanged
                ? "The 3D model (" + Path.GetFileName(Design) + ") was saved in SolidCraft after DWSIM last read the places of its items: items moved there since then are not in the drawings unless they are read first."
                : "The places of the items in the 3D model (" + Path.GetFileName(Design) + ") are the ones DWSIM has: nothing was moved in SolidCraft since they were read.";

        public bool ShowsItems => Items != null;

        /// <summary>The tags of the items chosen; null when all of them are.</summary>
        public List<string> ChosenTags => Items == null || Items.All(i => i.Checked) ? null : Items.Where(i => i.Checked).Select(i => i.Tag).ToList();

        public bool ShowsDrawingSet => Tool == CadTool.AllDrawings;

        public string OpenInAppText => Tool == CadTool.AllDrawings ? "Open the drawing set when finished" : "Open in " + Program + " when finished";

        public bool HasProgram => !string.IsNullOrEmpty(Program);

        public bool IsDrawing => Program == "CADCraft";

        /// <summary>The 3D model's own options (what the model holds, the picture).</summary>
        public bool ShowsModelOptions => Tool == CadTool.PlantModel;

        /// <summary>The PFD and the P&amp;ID route their lines, and the clearance applies to them.</summary>
        public bool ShowsClearance => Tool == CadTool.ProcessFlowDiagram || Tool == CadTool.PipingAndInstrumentation || Tool == CadTool.AllDrawings;

        /// <summary>The tool opens what it made in the program.</summary>
        public bool ShowsOpenInApp => Tool != CadTool.UpdatePlantModel && Tool != CadTool.ResetLayout;

        /// <summary>True when the program's executable is in <see cref="ProgramFolder"/>.</summary>
        public bool ProgramFound => !HasProgram || ProgramFoundIn(ProgramFolder);

        public bool ProgramFoundIn(string folder)
            => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder.Trim(), CADCraftSession.Exe(ProgramExe)));

        /// <summary>
        /// What the dialogs say about the program in <paramref name="folder"/>: found and its version,
        /// older than DWSIM needs, or not there. <paramref name="level"/> is 0, 1 or 2 in that order.
        /// </summary>
        public string ProgramStatus(string folder, out int level)
        {
            var exe = CADCraftSession.Exe(ProgramExe);
            if (!ProgramFoundIn(folder))
            {
                level = 2;
                return string.IsNullOrWhiteSpace(folder) ? Program + " was not found on this computer: choose the folder that holds " + exe + "." : exe + " is not in this folder.";
            }
            var version = CadTools.ProgramVersion(Path.Combine(folder.Trim(), exe));
            var needed = Program == "SolidCraft" ? CadTools.MinSolidCraft : CadTools.MinCADCraft;
            if (version != null && version < needed)
            {
                level = 1;
                return exe + " " + version + " found in this folder, but DWSIM needs " + Program + " " + needed.ToString(2) + " or newer: parts of the export may fail. Update " + Program + ".";
            }
            level = 0;
            return exe + (version != null ? " " + version : "") + " found in this folder.";
        }

        /// <summary>The flowsheet the dialog is for (the render preview builds its camera from it).</summary>
        public IFlowsheet Flowsheet;

        /// <summary>The output the tool has before anything was chosen for it, for the Defaults button.</summary>
        public string DefaultOutput;

        /// <summary>The length units of the flowsheet: the dialogs show the lengths of the 3D model in them.</summary>
        public string LengthUnits = "m";

        /// <summary>A length of the options (m) in <see cref="LengthUnits"/>.</summary>
        public double ToLength(double meters)
        {
            try { return LengthUnits == "m" ? meters : SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(LengthUnits, meters); }
            catch (Exception) { return meters; }
        }

        /// <summary>A length in <see cref="LengthUnits"/> in metres, for the options.</summary>
        public double FromLength(double value)
        {
            try { return LengthUnits == "m" ? value : SharedClasses.SystemsOfUnits.Converter.ConvertToSI(LengthUnits, value); }
            catch (Exception) { return value; }
        }

        /// <summary>The 3D model's render preview needs the saved design.</summary>
        public bool CanPreview(string output) => ShowsModelOptions && Flowsheet != null && !string.IsNullOrWhiteSpace(output) && File.Exists(Path.Combine(output.Trim(), ModelName + ".sc"));

        /// <summary>The window icon: the 3D model for the SolidCraft tools, drawing sheets for the CADCraft ones.</summary>
        public Stream Icon()
            => typeof(CadToolRequest).Assembly.GetManifestResourceStream("DWSIM.Extensions.SolidCraft." + (IsDrawing ? "Sheets.ico" : "Model3D.ico"));

        /// <summary>The path for the export: the folder, or the drawing's file without its extension.</summary>
        public string OutputBase => OutputIsFile ? Path.Combine(Path.GetDirectoryName(Output), Path.GetFileNameWithoutExtension(Output)) : Output;
    }

    /// <summary>Fills a <see cref="CadToolRequest"/> for a flowsheet and keeps what the user chose.</summary>
    public static class CadTools
    {
        /// <summary>The outputs chosen in the dialogs, kept in the flowsheet's extra properties (saved with the simulation).</summary>
        const string OutputsKey = "CadToolsOutputs";

        /// <summary>The 3D model, its data update and the layout reset share one folder.</summary>
        static string Group(CadTool tool) => (tool == CadTool.UpdatePlantModel || tool == CadTool.ResetLayout ? CadTool.PlantModel : tool).ToString();

        static string SimulationFolder(IFlowsheet fs)
        {
            var file = fs?.FlowsheetOptions?.FilePath;
            try { return string.IsNullOrWhiteSpace(file) ? null : Path.GetDirectoryName(Path.GetFullPath(file)); }
            catch (Exception) { return null; }
        }

        static Dictionary<string, string> SavedOutputs(IFlowsheet fs)
        {
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            if (props == null || !props.TryGetValue(OutputsKey, out var value) || value == null) return new Dictionary<string, string>();
            try { return JsonConvert.DeserializeObject<Dictionary<string, string>>(value.ToString()) ?? new Dictionary<string, string>(); }
            catch (Exception) { return new Dictionary<string, string>(); }
        }

        /// <summary>
        /// The output last chosen for a tool in this simulation; null when none was. A path inside the
        /// simulation's folder is kept relative to it, so it follows the simulation when the folder moves.
        /// </summary>
        public static string SavedOutput(IFlowsheet fs, CadTool tool)
        {
            if (!SavedOutputs(fs).TryGetValue(Group(tool), out var path) || string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (Path.IsPathRooted(path)) return path;
                var home = SimulationFolder(fs);
                return home == null ? null : Path.GetFullPath(Path.Combine(home, path));
            }
            catch (Exception) { return null; }
        }

        static void SaveOutput(IFlowsheet fs, CadTool tool, string output)
        {
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            if (props == null || string.IsNullOrWhiteSpace(output)) return;
            var saved = SavedOutputs(fs);
            var home = SimulationFolder(fs);
            var path = output.Trim();
            if (home != null && path.StartsWith(home.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(home.TrimEnd('\\', '/').Length + 1);
            saved[Group(tool)] = path;
            props[OutputsKey] = JsonConvert.SerializeObject(saved);
        }

        /// <summary>The folder of the 3D model: the one chosen for it in this simulation, else the default next to the simulation.</summary>
        public static string PlantModelFolder(IFlowsheet fs) => SavedOutput(fs, CadTool.PlantModel) ?? PlantModelExtender.OutputFolder(fs);

        /// <summary>The items a tool that draws a sheet each would draw; ticked: the ones selected in the flowsheet, or all when none of them is.</summary>
        static List<CadToolItem> Items(IFlowsheet fs, CadTool tool, string name)
        {
            List<(string Tag, string Text, string Object)> items;
            try
            {
                if (tool == CadTool.DataSheets)
                    items = DataSheets.List(fs, name).Select(s => (s.Tag, s.Tag + "  (" + (s.Kind ?? "").ToLowerInvariant() + ")", s.Object)).ToList();
                else
                {
                    var model = PlantModelBuilder.Build(fs, name, PlantLayout.Load(fs), ExportOptions.Load());
                    items = tool == CadTool.EquipmentDrawings
                        ? EquipmentDrawings.Items(fs, model).Select(e => (e.Tag, e.Tag + (string.IsNullOrWhiteSpace(e.Description) ? "" : "  (" + e.Description + ")"), e.Name)).ToList()
                        : model.Pipes.Where(p => !string.IsNullOrEmpty(p.Tag)).GroupBy(p => p.Tag)
                            .Select(g => (g.Key, "Line " + g.Key + "  (" + g.First().Size + ")", fs.GetFlowsheetSimulationObject(g.Key)?.Name)).ToList();
                }
            }
            catch (Exception) { return null; }
            var onSurface = SelectedNames(fs);
            var selected = new HashSet<string>(items.Where(i => i.Object != null && onSurface.Contains(i.Object)).Select(i => i.Tag));
            return items.Select(i => new CadToolItem { Tag = i.Tag, Text = i.Text, Checked = selected.Count == 0 || selected.Contains(i.Tag) }).ToList();
        }

        /// <summary>
        /// The names of the objects selected on the flowsheet drawing, from its surface (the same class
        /// in both interfaces, read by name so the extension needs no drawing library). The Selected flag
        /// of an object is not used: a simulation file keeps it from the last time it was saved.
        /// </summary>
        static HashSet<string> SelectedNames(IFlowsheet fs)
        {
            var names = new HashSet<string>();
            try
            {
                var surface = fs.GetSurface();
                if (surface == null) return names;
                var type = surface.GetType();
                if (type.GetProperty("SelectedObjects")?.GetValue(surface) is System.Collections.IDictionary many)
                    foreach (var g in many.Values.OfType<IGraphicObject>()) if (g.Name != null) names.Add(g.Name);
                if (type.GetProperty("SelectedObject")?.GetValue(surface) is IGraphicObject one && one.Name != null) names.Add(one.Name);
            }
            catch (Exception) { }
            return names;
        }

        public static CadToolRequest Request(IFlowsheet fs, CadTool tool)
        {
            var name = PlantModelExtender.ModelName(fs);
            var drawings = ProcessFlowDiagramExtender.DrawingsFolder(fs);
            var r = new CadToolRequest { Tool = tool, Options = ExportOptions.Load(), OutputLabel = "Output folder", ModelName = name, Flowsheet = fs };
            var units = fs.FlowsheetOptions?.SelectedUnitSystem?.distance;
            if (!string.IsNullOrWhiteSpace(units)) r.LengthUnits = units;
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
                case CadTool.AllDrawings:
                    r.Title = "All Drawings";
                    r.Description = "Draws the process flow diagram, the P&ID, the equipment drawings, the piping isometrics, the data sheets and the plot plan into one folder, and binds their PDFs into one drawing set that opens with a drawing list.";
                    r.Output = drawings;
                    break;
            }
            if (tool == CadTool.EquipmentDrawings || tool == CadTool.PipingIsometrics || tool == CadTool.DataSheets) r.Items = Items(fs, tool, name);
            // the isometrics and the plot plan place the items where the 3D model has them
            if (tool == CadTool.PipingIsometrics || tool == CadTool.PlotPlan || tool == CadTool.AllDrawings)
            {
                var design = Path.Combine(PlantModelFolder(fs), name + ".sc");
                if (File.Exists(design))
                {
                    r.Design = design;
                    r.DesignChanged = PlantLayout.DesignChanged(fs, design);
                    r.ReadMoves = r.DesignChanged;
                }
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
            r.DefaultOutput = r.Output;
            var kept = SavedOutput(fs, tool);
            if (kept != null) r.Output = kept;
            return r;
        }

        /// <summary>Keeps the program folder and the options in the settings, and the output with the simulation.</summary>
        public static void Accept(IFlowsheet fs, CadToolRequest r)
        {
            var folder = (r.ProgramFolder ?? "").Trim();
            if (r.Program == "SolidCraft" && folder != SolidCraftRunner.FindInstallFolder())
                GlobalSettings.Settings.SolidCraftPath = folder;
            else if (r.Program == "CADCraft" && folder != CADCraftSession.FindInstallFolder())
                r.Options.CADCraftPath = folder;
            GlobalSettings.Settings.SolidCraftOptions = r.Options.ToJson();
            SaveOutput(fs, r.Tool, r.Output);
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

        /// <summary>
        /// The views of the photorealistic picture: the turn about the vertical (0 looks at the front of
        /// the plant, as SolidCraft's front view does) and the height above the horizon, radians.
        /// </summary>
        public static readonly (string Id, string Text, double Yaw, double Pitch)[] RenderViews =
        {
            ("iso", "Isometric, front right", -0.785, 0.55),
            ("iso-front-left", "Isometric, front left", 0.785, 0.55),
            ("iso-back-left", "Isometric, back left", 2.356, 0.55),
            ("iso-back-right", "Isometric, back right", -2.356, 0.55),
            ("front", "Front elevation", 0, 0.05),
            ("side", "Side elevation", -1.5708, 0.05),
            ("plan", "Plan, from above", 0, 1.5)
        };

        /// <summary>Reads a picture size, "1920 x 1080" (x, X, * or a comma between); false when it is not two whole numbers.</summary>
        public static bool TryReadSize(string text, out int width, out int height)
        {
            width = height = 0;
            var m = System.Text.RegularExpressions.Regex.Match(text ?? "", @"^\s*(\d{2,5})\s*[xX*,×]\s*(\d{2,5})");
            if (!m.Success) return false;
            width = Math.Max(320, Math.Min(7680, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)));
            height = Math.Max(200, Math.Min(4320, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
            return true;
        }

        /// <summary>The text of a picture size: the name of the preset it is, else "W x H".</summary>
        public static string SizeText(int width, int height)
            => RenderSizes.Where(s => s.Width == width && s.Height == height).Select(s => s.Text).FirstOrDefault() ?? width + " x " + height;

        /// <summary>The oldest versions the exports were made and tested with: an older one may lack commands they use.</summary>
        public static readonly Version MinSolidCraft = new Version(0, 30, 0), MinCADCraft = new Version(0, 17, 0);

        static readonly Dictionary<string, (DateTime Written, Version Version)> Versions = new Dictionary<string, (DateTime, Version)>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The version a SolidCraft or CADCraft command-line program reports (--version); null when it says none.</summary>
        public static Version ProgramVersion(string exe)
        {
            if (!File.Exists(exe)) return null;
            var written = File.GetLastWriteTimeUtc(exe);
            lock (Versions)
                if (Versions.TryGetValue(exe, out var known) && known.Written == written) return known.Version;
            Version version = null;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, "--version")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    var read = p.StandardOutput.ReadToEndAsync();
                    if (p.WaitForExit(5000) && read.Wait(1000))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(read.Result, @"(\d+)\.(\d+)(?:\.(\d+))?");
                        if (m.Success)
                            version = new Version(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                                m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0);
                    }
                    else try { p.Kill(); } catch (Exception) { }
                }
            }
            catch (Exception) { version = null; }
            lock (Versions) Versions[exe] = (written, version);
            return version;
        }

        /// <summary>
        /// A quick picture of the 3D model as the dialog stands: the saved design with the camera of the
        /// view chosen, 800 x 500 with 16 samples (half a minute or so), opened when it is done.
        /// </summary>
        public static void Preview(CadToolRequest r)
        {
            var fs = r.Flowsheet;
            var folder = (r.ProgramFolder ?? "").Trim();
            var output = (r.Output ?? "").Trim();
            var design = Path.Combine(output, r.ModelName + ".sc");
            var picture = Path.Combine(output, r.ModelName + " - render preview.png");
            var options = r.Options;
            var job = new CadJob(new CadToolRequest { Tool = CadTool.PlantModel, Title = "Render Preview", Output = output, Program = "SolidCraft" });
            CadToolPrompt.Watch(job);
            job.Start(fs, "Render preview", () =>
            {
                CadJob.Report("Building the 3D model of the flowsheet");
                var model = PlantModelBuilder.Build(fs, r.ModelName, PlantLayout.Load(fs), options);
                var error = SolidCraftRunner.Preview(folder, design, SolidCraftScript.CreateRenderSetup(model, 16), picture, 800, 500);
                if (error != null) return "SolidCraft could not render the preview: " + error;
                CadJob.Open(picture);
                return null;
            });
        }

        public static readonly string[] Standards = { "ISO", "ASME", "Petrobras" };

        /// <summary>The document statuses offered for the title block (another can be typed).</summary>
        public static readonly string[] Statuses = { "PRELIMINARY", "FOR REVIEW", "FOR APPROVAL", "APPROVED", "FOR CONSTRUCTION", "AS BUILT" };

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
                        var chosen = r.ChosenTags;
                        found.AddRange(Directory.GetFiles(r.Output, prefix + "*")
                            .Where(f => DrawingExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            // only the sheets of the items chosen are drawn again
                            .Where(f => chosen == null || chosen.Any(t => string.Equals(Path.GetFileNameWithoutExtension(f), prefix + Safe(t), StringComparison.OrdinalIgnoreCase)))
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
                        break;
                    case CadTool.AllDrawings:
                        if (!Directory.Exists(r.Output)) break;
                        var n = r.ModelName;
                        var singles = new[] { n + " - PFD", n + " - PID", n + " - PID legend", n + " - Plot Plan" };
                        found.AddRange(Directory.GetFiles(r.Output, n + " - *")
                            .Where(f => DrawingExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                            .Where(f =>
                            {
                                var stem = Path.GetFileNameWithoutExtension(f);
                                return singles.Contains(stem, StringComparer.OrdinalIgnoreCase) || stem.StartsWith(n + " - GA ", StringComparison.OrdinalIgnoreCase) ||
                                    stem.StartsWith(n + " - ISO ", StringComparison.OrdinalIgnoreCase) || stem.StartsWith(n + " - DS ", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(Path.GetFileName(f), DrawingSetFile(n), StringComparison.OrdinalIgnoreCase);
                            })
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
                        break;
                }
            }
            catch (Exception) { }
            return found;
        }

        /// <summary>The file of the drawing set Export All Drawings binds.</summary>
        public static string DrawingSetFile(string modelName) => modelName + " - drawing set.pdf";

        /// <summary>A tag as the drawing tools put it in a file name.</summary>
        internal static string Safe(string s) => string.Concat((s ?? "item").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

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
            if (r.Items != null && !r.Items.Any(i => i.Checked)) return "Choose at least one " + (r.Tool == CadTool.PipingIsometrics ? "line." : "item.");
            if (r.ShowsDrawingSet && r.Options.DrawingSet && !r.Options.SavePdf) return "The drawing set is bound from the PDF files: tick PDF, or leave the drawing set out.";
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
