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
using System.Reflection;
using System.Threading.Tasks;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>Registers the 3D model export in the flowsheet Tools menu.</summary>
    public class Handler : IExtenderCollection, IExtenderCollection2
    {
        public string ID => "5f0c7a3e-2d4b-4e8a-9a51-6c3e1b7d2f90";

        public string Description => "3D plant model of the flowsheet with SolidCraft";

        public string DisplayText => "3D Plant Model";

        public ExtenderCategory Category => ExtenderCategory.Tools;

        public ExtenderLevel Level => ExtenderLevel.FlowsheetWindow;

        public List<IExtender> Collection { get; } = new List<IExtender> { new PlantModelExtender(), new UpdateDataExtender(), new ResetLayoutExtender(), new ProcessFlowDiagramExtender(), new PipingAndInstrumentationExtender(), new EquipmentDrawingsExtender(), new PipingIsometricsExtender() };

        public int InsertAtPosition => -1;

        public void SetMenuItem(object menuitem) { }
    }

    /// <summary>
    /// Builds the 3D model of the active flowsheet, saves it as a SolidCraft design, a STEP file and
    /// a picture in a folder next to the simulation, and opens the design in SolidCraft.
    /// </summary>
    public class PlantModelExtender : IExtender, IExtender3, IExtender6
    {
        IFlowsheet Flowsheet;

        static byte[] _icon;

        public string ID => "9b2e64d1-7c0f-4f3a-b8d5-3a1e9c6f4b27";

        public string DisplayText => "Export 3D Plant Model (SolidCraft)";

        public byte[] DisplayImage => Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void SetFlowsheetGUI(object FlowsheetGUI) { }

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            var fs = Flowsheet;
            if (fs == null) return;

            var folder = SolidCraftRunner.FindInstallFolder();
            if (folder == null)
            {
                fs.ShowMessage("SolidCraft was not found. Install it, or set the folder that holds solidcraft-cli in Settings > Other > SolidCraft.",
                    IFlowsheet.MessageType.GeneralError);
                return;
            }

            fs.ShowMessage("Building the 3D plant model with SolidCraft...", IFlowsheet.MessageType.Information);

            // SolidCraft takes seconds to minutes on a large flowsheet: keep the interface free
            Task.Run(() =>
            {
                try
                {
                    Export(fs, folder, true);
                }
                catch (Exception ex)
                {
                    fs.ShowMessage("3D plant model: " + ex.Message, IFlowsheet.MessageType.GeneralError);
                }
            });
        }

        /// <summary>Builds and saves the model; returns the run result. Public for tests and scripts.</summary>
        public static SolidCraftRunner.Result Export(IFlowsheet fs, string solidcraftFolder, bool openInApp, string outputFolder = null)
        {
            var name = ModelName(fs);
            if (outputFolder == null) outputFolder = OutputFolder(fs);
            Directory.CreateDirectory(outputFolder);
            var outputBase = Path.Combine(outputFolder, name);

            // the layout: the places kept with the simulation, updated with what was moved in the
            // design exported last time (so a move made in SolidCraft is never lost to a new export)
            var layout = PlantLayout.Load(fs);
            if (File.Exists(outputBase + ".sc"))
            {
                var warnings = new List<string>();
                var moves = PlantLayout.ReadMoves(solidcraftFolder, outputBase + ".sc", warnings);
                foreach (var w in warnings) fs.ShowMessage("3D plant model: " + w, IFlowsheet.MessageType.Warning);
                var kept = 0;
                foreach (var obj in fs.SimulationObjects.Values)
                {
                    var tag = obj.GraphicObject?.Tag;
                    if (tag == null || !moves.TryGetValue(tag, out var place)) continue;
                    layout[obj.Name] = place;
                    kept++;
                }
                if (kept > 0)
                {
                    PlantLayout.Save(fs, layout);
                    fs.ShowMessage("3D plant model: the places of " + kept + " items moved in SolidCraft are kept with the simulation; save it to keep them.",
                        IFlowsheet.MessageType.Information);
                }
            }

            var options = ExportOptions.Load();
            var model = PlantModelBuilder.Build(fs, name, layout, options);

            // the line list and the take-off, written first: they need no SolidCraft
            var lineList = outputBase + " - line list.csv";
            var takeOff = outputBase + " - material take-off.csv";
            try
            {
                LineReports.WriteLineList(model, lineList, CultureInfo.CurrentCulture);
                LineReports.WriteTakeOff(model, takeOff, CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                fs.ShowMessage("3D plant model: the line list could not be written (" + ex.Message + ").", IFlowsheet.MessageType.Warning);
            }
            // the model for a browser, for whoever has no SolidCraft
            var viewer = outputBase + " - 3D viewer.html";
            try { WebViewer.Write(model, viewer); }
            catch (Exception ex)
            {
                viewer = null;
                fs.ShowMessage("3D plant model: the web viewer could not be written (" + ex.Message + ").", IFlowsheet.MessageType.Warning);
            }
            // where the model runs into itself
            var clashes = ClashCheck.Find(model);
            try
            {
                var clashFile = outputBase + " - clashes.csv";
                if (clashes.Count > 0) LineReports.WriteClashes(clashes, clashFile, CultureInfo.CurrentCulture);
                else if (File.Exists(clashFile)) File.Delete(clashFile);
            }
            catch (Exception) { }
            foreach (var c in clashes.Take(10))
                fs.ShowMessage("3D plant model: " + (c.Kind == "line through item"
                        ? string.Format(CultureInfo.InvariantCulture, "line {0} passes through {1} ({2:0.##} m inside).", c.A, c.B, c.Amount)
                        : c.Kind == "lines meet"
                            ? string.Format(CultureInfo.InvariantCulture, "lines {0} and {1} meet (they cut {2:0} mm into each other).", c.A, c.B, c.Amount)
                            : string.Format(CultureInfo.InvariantCulture, "{0} and {1} overlap ({2:0} mm).", c.A, c.B, c.Amount)),
                    IFlowsheet.MessageType.Warning);
            if (clashes.Count > 10)
                fs.ShowMessage("3D plant model: " + (clashes.Count - 10) + " more clashes, listed in " + outputBase + " - clashes.csv.", IFlowsheet.MessageType.Warning);
            if (clashes.Count > 0)
                fs.ShowMessage("3D plant model: move the items in SolidCraft and export again to clear the clashes; the new places are kept.", IFlowsheet.MessageType.Information);

            foreach (var run in model.Pipes.Where(r => r.Hydraulics != null && r.Hydraulics.Check.Length > 0))
                fs.ShowMessage(string.Format(CultureInfo.InvariantCulture, "3D plant model: line {0} ({1}, {2:0} m, {3} bends): {4}; pressure drop {5:0.###} bar, which the simulation does not include.",
                    run.Tag, run.Size, run.Hydraulics.Length, run.Hydraulics.Bends, run.Hydraulics.Check, run.Hydraulics.Total),
                    IFlowsheet.MessageType.Warning);
            var commands = SolidCraftScript.Create(model);
            var extras = options.Labels ? SolidCraftScript.CreateLabels(model) : new List<SolidCraftScript.Command>();
            if (options.FlowAnimation) extras.AddRange(FlowAnimation.Create(model, outputBase + " - flow.gif"));
            if (options.Render) extras.AddRange(SolidCraftScript.CreateRenderSetup(model));
            var replayNotes = new List<string>();
            if (options.DynamicReplay)
            {
                var replay = DynamicReplay.Collect(fs);
                if (replay != null) extras.AddRange(DynamicReplay.Create(model, fs, replay, outputBase + " - dynamic replay.gif", replayNotes));
            }
            var result = SolidCraftRunner.Run(solidcraftFolder, commands, extras, outputBase);

            foreach (var w in model.Warnings)
                fs.ShowMessage("3D plant model: " + w, IFlowsheet.MessageType.Information);
            foreach (var s in result.Skipped)
                fs.ShowMessage("3D plant model: left out " + s, IFlowsheet.MessageType.Warning);

            if (!result.Success)
            {
                fs.ShowMessage("SolidCraft could not build the model: " + result.Error + " The script is in " + result.Script + ".",
                    IFlowsheet.MessageType.GeneralError);
                return result;
            }

            if (result.AnimationError != null)
                fs.ShowMessage("3D plant model: the labels and the flow animation were left out: " + result.AnimationError, IFlowsheet.MessageType.Warning);

            fs.ShowMessage(string.Format("3D plant model: {0} items and {1} lines saved to {2} (SolidCraft design, STEP, line list, material take-off{3}{4}).",
                model.Equipment.Count(e => e.Visible), model.Pipes.Count, outputFolder,
                (result.Picture != null ? ", picture" : "") + (result.Render != null ? ", rendering" : "") + (viewer != null ? ", web viewer" : ""),
                result.Animation != null ? ", flow animation: play the \"" + FlowAnimation.StudyName + "\" motion study in SolidCraft" : ""),
                IFlowsheet.MessageType.Information);

            if (result.AnimationError == null)
                foreach (var note in replayNotes) fs.ShowMessage("3D plant model: " + note, IFlowsheet.MessageType.Information);

            if (openInApp) SolidCraftRunner.OpenInApp(solidcraftFolder, result.Design);

            return result;
        }

        internal static string ModelName(IFlowsheet fs)
        {
            var file = fs.FlowsheetOptions?.FilePath;
            return string.IsNullOrWhiteSpace(file) ? "Flowsheet" : Path.GetFileNameWithoutExtension(file);
        }

        /// <summary>The folder of the 3D model: next to the simulation, or in the temporary folder when it has not been saved.</summary>
        internal static string OutputFolder(IFlowsheet fs)
        {
            var file = fs.FlowsheetOptions?.FilePath;
            var root = string.IsNullOrWhiteSpace(file) || !Directory.Exists(Path.GetDirectoryName(file))
                ? Path.Combine(Path.GetTempPath(), "DWSIM")
                : Path.GetDirectoryName(file);
            return Path.Combine(root, ModelName(fs) + " - 3D model");
        }

        internal static byte[] Icon()
        {
            if (_icon != null) return _icon;
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(".Plant3D.png", StringComparison.OrdinalIgnoreCase));
            if (name == null) return _icon = new byte[0];
            using (var s = asm.GetManifestResourceStream(name))
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return _icon = ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Sends the operating data of the solved flowsheet to the 3D model without building it again:
    /// to the SolidCraft application when it is open on this model, otherwise into the saved design.
    /// The line list and the take-off are written again too.
    /// </summary>
    public class UpdateDataExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "e7a3c915-0b6d-4f28-9d41-5c2b8e7f1a60";

        public string DisplayText => "Update 3D Plant Model Data";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            var fs = Flowsheet;
            if (fs == null) return;
            var folder = SolidCraftRunner.FindInstallFolder();
            if (folder == null)
            {
                fs.ShowMessage("SolidCraft was not found. Install it, or set the folder that holds solidcraft-cli in Settings > Other > SolidCraft.", IFlowsheet.MessageType.GeneralError);
                return;
            }
            Task.Run(() =>
            {
                try { Update(fs, folder); }
                catch (Exception ex) { fs.ShowMessage("3D plant model: " + ex.Message, IFlowsheet.MessageType.GeneralError); }
            });
        }

        public static void Update(IFlowsheet fs, string folder)
        {
            var name = PlantModelExtender.ModelName(fs);
            var outputBase = Path.Combine(PlantModelExtender.OutputFolder(fs), name);
            var model = PlantModelBuilder.Build(fs, name, PlantLayout.Load(fs), ExportOptions.Load());
            var cmds = SolidCraftScript.DataCommands(model);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputBase));
                LineReports.WriteLineList(model, outputBase + " - line list.csv", CultureInfo.CurrentCulture);
                LineReports.WriteTakeOff(model, outputBase + " - material take-off.csv", CultureInfo.CurrentCulture);
            }
            catch (Exception) { }

            if (LiveLink.TryRun(folder, name, cmds, out var failed, out var why))
            {
                fs.ShowMessage("3D plant model: the operating data of " + (cmds.Count - failed) + " items and lines were sent to the model open in SolidCraft" +
                    (failed > 0 ? " (" + failed + " not in the model: export it again)" : "") + ".", IFlowsheet.MessageType.Information);
                return;
            }
            if (!File.Exists(outputBase + ".sc"))
            {
                fs.ShowMessage("3D plant model: " + why + ", and there is no exported model yet: use Export 3D Plant Model first.", IFlowsheet.MessageType.Warning);
                return;
            }
            var skipped = new List<string>();
            var error = SolidCraftRunner.UpdateDesign(folder, cmds, outputBase, skipped);
            fs.ShowMessage(error == null
                ? "3D plant model: " + why + "; the data were written into the saved model (" + outputBase + ".sc)" + (skipped.Count > 0 ? ", " + skipped.Count + " items not in it" : "") + "."
                : "3D plant model: the data could not be written (" + error + ").", error == null ? IFlowsheet.MessageType.Information : IFlowsheet.MessageType.Warning);
        }
    }

    /// <summary>
    /// Draws the process flow diagram of the flowsheet in CADCraft and saves it as DXF, DWG, PDF and
    /// PNG in a folder next to the simulation, then opens it in CADCraft.
    /// </summary>
    public class ProcessFlowDiagramExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "c41d8e27-5b9a-4f63-a0e2-7d3f1b6c9e84";

        public string DisplayText => "Export Process Flow Diagram (CADCraft)";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            var fs = Flowsheet;
            if (fs == null) return;
            var folder = CADCraftSession.FindInstallFolder();
            if (folder == null)
            {
                fs.ShowMessage("CADCraft was not found. Install it (it comes on its own or with SolidCraft in the ArtCraft suite), or set CADCRAFT_HOME to the folder that holds cadcraft-cli.",
                    IFlowsheet.MessageType.GeneralError);
                return;
            }
            fs.ShowMessage("Drawing the process flow diagram with CADCraft...", IFlowsheet.MessageType.Information);
            Task.Run(() =>
            {
                try { Export(fs, folder, true); }
                catch (Exception ex) { fs.ShowMessage("Process flow diagram: " + ex.Message, IFlowsheet.MessageType.GeneralError); }
            });
        }

        /// <summary>The folder of the drawings: next to the simulation, or in the temporary folder when it has not been saved.</summary>
        internal static string DrawingsFolder(IFlowsheet fs)
            => Path.Combine(Path.GetDirectoryName(PlantModelExtender.OutputFolder(fs)), PlantModelExtender.ModelName(fs) + " - drawings");

        /// <summary>Draws and saves the diagram; returns the result. Public for tests and scripts.</summary>
        public static ProcessFlowDiagram.Result Export(IFlowsheet fs, string cadcraftFolder, bool openInApp, string outputFolder = null, bool pid = false)
        {
            var what = pid ? "P&ID" : "Process flow diagram";
            var name = PlantModelExtender.ModelName(fs);
            if (outputFolder == null) outputFolder = DrawingsFolder(fs);
            Directory.CreateDirectory(outputFolder);
            var options = ExportOptions.Load();
            var project = fs.FlowsheetOptions?.SimulationName;
            var title = new TitleData
            {
                Owner = options.DrawingOwner ?? "",
                Project = string.IsNullOrWhiteSpace(project) ? name : project,
                Title = pid ? "PIPING AND INSTRUMENTATION DIAGRAM" : "PROCESS FLOW DIAGRAM",
                Subtitle = name,
                Number = pid ? "PID-001" : "PFD-001",
                DocumentType = pid ? "P&ID" : "PFD",
                Status = "PRELIMINARY",
                Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PreparedBy = fs.FlowsheetOptions?.SimulationAuthor ?? ""
            };
            var result = ProcessFlowDiagram.Export(fs, cadcraftFolder, Path.Combine(outputFolder, name + (pid ? " - PID" : " - PFD")), options.Style, title, pid);

            foreach (var f in result.Failed.Take(5))
                fs.ShowMessage(what + ": left out " + f, IFlowsheet.MessageType.Warning);
            if (result.Failed.Count > 5)
                fs.ShowMessage(what + ": " + (result.Failed.Count - 5) + " more drawing steps left out.", IFlowsheet.MessageType.Warning);
            if (result.Drawing == null)
            {
                fs.ShowMessage("CADCraft could not draw the " + (pid ? "P&ID" : "process flow diagram") + ": " + result.Error, IFlowsheet.MessageType.GeneralError);
                return result;
            }
            fs.ShowMessage(string.Format("{0}: {1} items and {2} lines on an {3} sheet ({4}), saved to {5} (DXF{6}{7}{8}).",
                what, result.Units, result.Streams, result.Sheet, options.Style, outputFolder,
                result.Dwg != null ? ", DWG" : "", result.Pdf != null ? ", PDF" : "", result.Png != null ? ", PNG" : ""),
                IFlowsheet.MessageType.Information);
            if (!DrawingSheets.FontInstalled())
            {
                string font = null;
                try { font = DrawingSheets.WriteFont(outputFolder); } catch (Exception) { }
                fs.ShowMessage(what + ": the drawing is lettered in osifont, the free ISO 3098 font for CAD, which is not installed on this computer, so CADCraft used its own lettering." +
                    (font != null ? " Install " + font + " to see it in CADCraft and in other CAD programs, then export again." : ""), IFlowsheet.MessageType.Information);
            }
            if (openInApp) CADCraftSession.OpenInApp(cadcraftFolder, result.Drawing);
            return result;
        }
    }

    /// <summary>
    /// Draws the piping and instrumentation diagram of the flowsheet in CADCraft: the process flow
    /// diagram's items and lines with line numbers, the control loops, indicators and relief
    /// valves; saved as DXF, DWG, PDF and PNG next to the simulation and opened in CADCraft.
    /// </summary>
    public class PipingAndInstrumentationExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "f2a7c6d9-3e18-4b5a-9c40-8d1e6b2f7a35";

        public string DisplayText => "Export P&ID (CADCraft)";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            var fs = Flowsheet;
            if (fs == null) return;
            var folder = CADCraftSession.FindInstallFolder();
            if (folder == null)
            {
                fs.ShowMessage("CADCraft was not found. Install it (it comes on its own or with SolidCraft in the ArtCraft suite), or set CADCRAFT_HOME to the folder that holds cadcraft-cli.",
                    IFlowsheet.MessageType.GeneralError);
                return;
            }
            fs.ShowMessage("Drawing the P&ID with CADCraft...", IFlowsheet.MessageType.Information);
            Task.Run(() =>
            {
                try { ProcessFlowDiagramExtender.Export(fs, folder, true, null, true); }
                catch (Exception ex) { fs.ShowMessage("P&ID: " + ex.Message, IFlowsheet.MessageType.GeneralError); }
            });
        }
    }

    /// <summary>
    /// Draws the general arrangement (dimensional) drawing of each vessel, column, tank and
    /// exchanger of the flowsheet in CADCraft, from the 3D plant model, and saves them next to the
    /// simulation with the other drawings.
    /// </summary>
    public class EquipmentDrawingsExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "8d3b1f64-27c9-4e0a-b5f8-6a1c2e9d4b73";

        public string DisplayText => "Export Equipment Drawings (CADCraft)";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            var fs = Flowsheet;
            if (fs == null) return;
            var folder = CADCraftSession.FindInstallFolder();
            if (folder == null)
            {
                fs.ShowMessage("CADCraft was not found. Install it (it comes on its own or with SolidCraft in the ArtCraft suite), or set CADCRAFT_HOME to the folder that holds cadcraft-cli.",
                    IFlowsheet.MessageType.GeneralError);
                return;
            }
            fs.ShowMessage("Drawing the equipment with CADCraft...", IFlowsheet.MessageType.Information);
            Task.Run(() =>
            {
                try { Export(fs, folder, true); }
                catch (Exception ex) { fs.ShowMessage("Equipment drawings: " + ex.Message, IFlowsheet.MessageType.GeneralError); }
            });
        }

        /// <summary>Draws and saves the drawings; returns the result. Public for tests and scripts.</summary>
        public static EquipmentDrawings.Result Export(IFlowsheet fs, string cadcraftFolder, bool openInApp, string outputFolder = null)
        {
            var name = PlantModelExtender.ModelName(fs);
            if (outputFolder == null) outputFolder = ProcessFlowDiagramExtender.DrawingsFolder(fs);
            Directory.CreateDirectory(outputFolder);
            var options = ExportOptions.Load();
            var project = fs.FlowsheetOptions?.SimulationName;
            var result = EquipmentDrawings.Export(fs, cadcraftFolder, outputFolder, name, options.Style, item => new TitleData
            {
                Owner = options.DrawingOwner ?? "",
                Project = string.IsNullOrWhiteSpace(project) ? name : project,
                Title = "GENERAL ARRANGEMENT",
                Subtitle = item.Tag + (string.IsNullOrWhiteSpace(item.Description) ? "" : " - " + item.Description.ToUpperInvariant()),
                Number = "GA-" + item.Tag,
                DocumentType = "GA",
                Status = "PRELIMINARY",
                Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PreparedBy = fs.FlowsheetOptions?.SimulationAuthor ?? ""
            });

            foreach (var f in result.Failed.Take(5))
                fs.ShowMessage("Equipment drawings: left out " + f, IFlowsheet.MessageType.Warning);
            if (result.Drawings.Count == 0)
            {
                fs.ShowMessage("CADCraft could not draw the equipment: " + result.Error, IFlowsheet.MessageType.GeneralError);
                return result;
            }
            fs.ShowMessage(string.Format("Equipment drawings: {0} general arrangement drawings saved to {1} (DXF, DWG, PDF and PNG).", result.Drawings.Count, outputFolder),
                IFlowsheet.MessageType.Information);
            if (openInApp) CADCraftSession.OpenInApp(cadcraftFolder, result.Drawings[0]);
            return result;
        }
    }

    /// <summary>
    /// Draws the piping isometric of each line of the flowsheet's 3D plant model in CADCraft and
    /// saves them next to the simulation with the other drawings.
    /// </summary>
    public class PipingIsometricsExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "c41e7a20-93d5-4b8f-a6e2-5f0b9d3c1e84";

        public string DisplayText => "Export Piping Isometrics (CADCraft)";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            var fs = Flowsheet;
            if (fs == null) return;
            var folder = CADCraftSession.FindInstallFolder();
            if (folder == null)
            {
                fs.ShowMessage("CADCraft was not found. Install it (it comes on its own or with SolidCraft in the ArtCraft suite), or set CADCRAFT_HOME to the folder that holds cadcraft-cli.",
                    IFlowsheet.MessageType.GeneralError);
                return;
            }
            fs.ShowMessage("Drawing the piping isometrics with CADCraft...", IFlowsheet.MessageType.Information);
            Task.Run(() =>
            {
                try { Export(fs, folder, true); }
                catch (Exception ex) { fs.ShowMessage("Piping isometrics: " + ex.Message, IFlowsheet.MessageType.GeneralError); }
            });
        }

        /// <summary>Draws and saves the isometrics; returns the result. Public for tests and scripts.</summary>
        public static PipingIsometrics.Result Export(IFlowsheet fs, string cadcraftFolder, bool openInApp, string outputFolder = null)
        {
            var name = PlantModelExtender.ModelName(fs);
            if (outputFolder == null) outputFolder = ProcessFlowDiagramExtender.DrawingsFolder(fs);
            Directory.CreateDirectory(outputFolder);
            var options = ExportOptions.Load();
            var project = fs.FlowsheetOptions?.SimulationName;
            var result = PipingIsometrics.Export(fs, cadcraftFolder, outputFolder, name, options.Style, (run, number, id) => new TitleData
            {
                Owner = options.DrawingOwner ?? "",
                Project = string.IsNullOrWhiteSpace(project) ? name : project,
                Title = "PIPING ISOMETRIC",
                Subtitle = number,
                Number = "ISO-" + id,
                DocumentType = "ISO",
                Status = "PRELIMINARY",
                Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PreparedBy = fs.FlowsheetOptions?.SimulationAuthor ?? ""
            });

            foreach (var f in result.Failed.Take(5))
                fs.ShowMessage("Piping isometrics: left out " + f, IFlowsheet.MessageType.Warning);
            if (result.Drawings.Count == 0)
            {
                fs.ShowMessage("CADCraft could not draw the isometrics: " + result.Error, IFlowsheet.MessageType.GeneralError);
                return result;
            }
            fs.ShowMessage(string.Format("Piping isometrics: {0} drawings saved to {1} (DXF, DWG, PDF and PNG).", result.Drawings.Count, outputFolder),
                IFlowsheet.MessageType.Information);
            if (openInApp) CADCraftSession.OpenInApp(cadcraftFolder, result.Drawings[0]);
            return result;
        }
    }

    /// <summary>
    /// Goes back to the layout of the flowsheet drawing: forgets the places kept with the simulation
    /// and sets the last design aside, so the next export does not read its moves again.
    /// </summary>
    public class ResetLayoutExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "c41f2a87-5e3d-4b9a-8f62-1d7e0a5b9c34";

        public string DisplayText => "Reset 3D Plant Layout";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            var fs = Flowsheet;
            if (fs == null) return;

            PlantLayout.Save(fs, null);

            var design = Path.Combine(PlantModelExtender.OutputFolder(fs), PlantModelExtender.ModelName(fs) + ".sc");
            var aside = Path.Combine(Path.GetDirectoryName(design), PlantModelExtender.ModelName(fs) + " - before layout reset.sc");
            try
            {
                if (File.Exists(design))
                {
                    if (File.Exists(aside)) File.Delete(aside);
                    File.Move(design, aside);
                }
                fs.ShowMessage("3D plant model: the layout follows the flowsheet drawing again" +
                    (File.Exists(aside) ? "; the last design was kept as " + aside : "") + ". Save the simulation to keep the change.",
                    IFlowsheet.MessageType.Information);
            }
            catch (Exception ex)
            {
                fs.ShowMessage("3D plant model: the layout was reset, but the last design could not be set aside (" + ex.Message +
                    "); close it in SolidCraft before the next export.", IFlowsheet.MessageType.Warning);
            }
        }
    }
}
