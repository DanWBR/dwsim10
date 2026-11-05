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
    /// <summary>
    /// The CAD Tools menu of the flowsheet: the 3D plant model (SolidCraft) and the drawings
    /// (CADCraft). Each item opens a dialog with the program, the output and the options.
    /// </summary>
    public class Handler : IExtenderCollection, IExtenderCollection2
    {
        public string ID => "5f0c7a3e-2d4b-4e8a-9a51-6c3e1b7d2f90";

        public string Description => "3D plant model and drawings of the flowsheet with SolidCraft and CADCraft";

        /// <summary>A collection filed under NewItem gets a top-level menu with this name in both interfaces.</summary>
        public string DisplayText => "CAD Tools";

        public ExtenderCategory Category => ExtenderCategory.NewItem;

        public ExtenderLevel Level => ExtenderLevel.FlowsheetWindow;

        public List<IExtender> Collection { get; } = new List<IExtender> { new PlantModelExtender(), new UpdateDataExtender(), new ResetLayoutExtender(), new ProcessFlowDiagramExtender(), new PipingAndInstrumentationExtender(), new EquipmentDrawingsExtender(), new PipingIsometricsExtender(), new DataSheetsExtender(), new PlotPlanExtender(), new AllDrawingsExtender(), new DrawingImportExtender(), new IsometricImportExtender() };

        /// <summary>After Results, before the menus of the window itself.</summary>
        public int InsertAtPosition => 9;

        public void SetMenuItem(object menuitem) { }
    }

    /// <summary>Opens the dialog of a CAD tool and runs the tool with what the user chose there.</summary>
    internal static class CadToolCommand
    {
        internal static void Start(IFlowsheet fs, CadTool tool)
        {
            if (fs == null) return;
            CadToolPrompt.Ask(CadTools.Request(fs, tool), r =>
            {
                CadTools.Accept(fs, r);
                if (r.KeepOldCopies)
                {
                    try
                    {
                        var aside = CadTools.SetAside(CadTools.ExistingOutputs(r));
                        if (aside != null) fs.ShowMessage(r.Title + ": the files of the earlier export were moved to " + aside + ".", IFlowsheet.MessageType.Information);
                    }
                    catch (Exception ex)
                    {
                        fs.ShowMessage(r.Title + ": the files of the earlier export could not be moved (" + ex.Message + "); close them in CADCraft or the program that has them open, then export again.",
                            IFlowsheet.MessageType.GeneralError);
                        return;
                    }
                }
                Run(fs, r);
            });
        }

        static void Run(IFlowsheet fs, CadToolRequest r)
        {
            var folder = r.ProgramFolder?.Trim();
            var open = r.OpenInApp;
            switch (r.Tool)
            {
                case CadTool.PlantModel:
                    Background(fs, r, "3D plant model", "Building the 3D plant model with SolidCraft...", () =>
                    {
                        var res = PlantModelExtender.Export(fs, folder, open, r.Output);
                        return res.Success ? null : "SolidCraft could not build the model: " + res.Error;
                    });
                    break;
                case CadTool.UpdatePlantModel:
                    Background(fs, r, "3D plant model", null, () => UpdateDataExtender.Update(fs, folder, r.Output));
                    break;
                case CadTool.ResetLayout:
                    ResetLayoutExtender.Reset(fs, r.Output);
                    break;
                case CadTool.ProcessFlowDiagram:
                    Background(fs, r, "Process flow diagram", "Drawing the process flow diagram with CADCraft...", () =>
                    {
                        var res = ProcessFlowDiagramExtender.Export(fs, folder, open, null, false, r.OutputBase);
                        return res.Drawing != null ? null : "CADCraft could not draw the process flow diagram: " + res.Error;
                    });
                    break;
                case CadTool.PipingAndInstrumentation:
                    Background(fs, r, "P&ID", "Drawing the P&ID with CADCraft...", () =>
                    {
                        var res = ProcessFlowDiagramExtender.Export(fs, folder, open, null, true, r.OutputBase);
                        return res.Drawing != null ? null : "CADCraft could not draw the P&ID: " + res.Error;
                    });
                    break;
                case CadTool.EquipmentDrawings:
                    Background(fs, r, "Equipment drawings", "Drawing the equipment with CADCraft...", () =>
                    {
                        var res = EquipmentDrawingsExtender.Export(fs, folder, open, r.Output, r.ChosenTags);
                        return res.Drawings.Count > 0 ? null : "CADCraft could not draw the equipment: " + res.Error;
                    });
                    break;
                case CadTool.PipingIsometrics:
                    Background(fs, r, "Piping isometrics", "Drawing the piping isometrics with CADCraft...", () =>
                    {
                        ReadMovesFirst(fs, r);
                        var res = PipingIsometricsExtender.Export(fs, folder, open, r.Output, r.ChosenTags);
                        return res.Drawings.Count > 0 ? null : "CADCraft could not draw the isometrics: " + res.Error;
                    });
                    break;
                case CadTool.DataSheets:
                    Background(fs, r, "Data sheets", "Writing the data sheets with CADCraft...", () =>
                    {
                        var res = DataSheetsExtender.Export(fs, folder, open, r.Output, r.ChosenTags);
                        return res.Drawings.Count > 0 ? null : "CADCraft could not write the data sheets: " + res.Error;
                    });
                    break;
                case CadTool.PlotPlan:
                    Background(fs, r, "Plot plan", "Drawing the plot plan with CADCraft...", () =>
                    {
                        ReadMovesFirst(fs, r);
                        var res = PlotPlanExtender.Export(fs, folder, open, null, r.OutputBase);
                        return res.Drawing != null ? null : "CADCraft could not draw the plot plan: " + res.Error;
                    });
                    break;
                case CadTool.AllDrawings:
                    Background(fs, r, "All drawings", "Drawing all the drawings with CADCraft...", () =>
                    {
                        ReadMovesFirst(fs, r);
                        return AllDrawingsExtender.Export(fs, folder, open, r.Output);
                    });
                    break;
            }
        }

        /// <summary>Reads the places of the items moved in SolidCraft into the layout, when the dialog asked for it.</summary>
        static void ReadMovesFirst(IFlowsheet fs, CadToolRequest r)
        {
            if (!r.ReadMoves || r.Design == null) return;
            var solidcraft = SolidCraftRunner.FindInstallFolder();
            if (solidcraft == null)
            {
                fs.ShowMessage(r.Title + ": SolidCraft was not found, so the places of the items moved in the 3D model could not be read; the drawings use the places DWSIM has.",
                    IFlowsheet.MessageType.Warning);
                return;
            }
            CadJob.Report("SolidCraft: reading the places of the items moved in the 3D model");
            PlantModelExtender.ReadMoves(fs, solidcraft, r.Design);
        }

        /// <summary>
        /// SolidCraft and CADCraft take seconds to minutes on a large flowsheet (a render can take an
        /// hour): the tool runs in the background with a window that shows its step and can cancel it.
        /// <paramref name="work"/> returns null when the tool made what it should, else the reason.
        /// </summary>
        static void Background(IFlowsheet fs, CadToolRequest r, string what, string starting, Func<string> work)
        {
            if (starting != null) fs.ShowMessage(starting, IFlowsheet.MessageType.Information);
            var job = new CadJob(r);
            CadToolPrompt.Watch(job);
            job.Start(fs, what, work);
        }
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

        public string DisplayText => "Export 3D Plant Model (SolidCraft)...";

        public byte[] DisplayImage => Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void SetFlowsheetGUI(object FlowsheetGUI) { }

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.PlantModel);

        /// <summary>Builds and saves the model; returns the run result. Public for tests and scripts.</summary>
        public static SolidCraftRunner.Result Export(IFlowsheet fs, string solidcraftFolder, bool openInApp, string outputFolder = null)
        {
            var name = ModelName(fs);
            if (outputFolder == null) outputFolder = OutputFolder(fs);
            Directory.CreateDirectory(outputFolder);
            var outputBase = Path.Combine(outputFolder, name);

            // the layout: the places kept with the simulation, updated with what was moved in the
            // design exported last time (so a move made in SolidCraft is never lost to a new export)
            if (File.Exists(outputBase + ".sc"))
            {
                CadJob.Report("SolidCraft: reading the places of the items moved in the last design");
                ReadMoves(fs, solidcraftFolder, outputBase + ".sc");
            }
            var layout = PlantLayout.Load(fs);

            CadJob.Report("Building the 3D model of the flowsheet");
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
            CadJob.Check();
            var result = SolidCraftRunner.Run(solidcraftFolder, commands, extras, outputBase, options);

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
            if (result.RenderError != null)
                fs.ShowMessage("3D plant model: the photorealistic picture could not be rendered: " + result.RenderError, IFlowsheet.MessageType.Warning);

            fs.ShowMessage(string.Format("3D plant model: {0} items and {1} lines saved to {2} (SolidCraft design, STEP, line list, material take-off{3}{4}).",
                model.Equipment.Count(e => e.Visible), model.Pipes.Count, outputFolder,
                (result.Picture != null ? ", picture" : "") + (result.Render != null ? ", rendering" : "") + (viewer != null ? ", web viewer" : ""),
                result.Animation != null ? ", flow animation: play the \"" + FlowAnimation.StudyName + "\" motion study in SolidCraft" : ""),
                IFlowsheet.MessageType.Information);

            if (result.AnimationError == null)
                foreach (var note in replayNotes) fs.ShowMessage("3D plant model: " + note, IFlowsheet.MessageType.Information);

            // the design holds the layout as it is now: nothing in it is left to read
            PlantLayout.MarkRead(fs, result.Design);

            if (openInApp) SolidCraftRunner.OpenInApp(solidcraftFolder, result.Design);

            return result;
        }

        /// <summary>
        /// Takes the places of the items moved in a SolidCraft design into the layout kept with the
        /// simulation (so the next model and the drawings have them); returns how many were taken.
        /// </summary>
        public static int ReadMoves(IFlowsheet fs, string solidcraftFolder, string design)
        {
            var warnings = new List<string>();
            var moves = PlantLayout.ReadMoves(solidcraftFolder, design, warnings);
            foreach (var w in warnings) fs.ShowMessage("3D plant model: " + w, IFlowsheet.MessageType.Warning);
            if (moves == null) return 0;
            var layout = PlantLayout.Load(fs);
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
            PlantLayout.MarkRead(fs, design);
            return kept;
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

        public string DisplayText => "Update 3D Plant Model Data...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.UpdatePlantModel);

        /// <summary>Sends the data; returns null when they reached the model, else the reason.</summary>
        public static string Update(IFlowsheet fs, string folder, string modelFolder = null)
        {
            CadJob.Report("Building the 3D model data");
            var name = PlantModelExtender.ModelName(fs);
            var outputBase = Path.Combine(modelFolder ?? PlantModelExtender.OutputFolder(fs), name);
            var model = PlantModelBuilder.Build(fs, name, PlantLayout.Load(fs), ExportOptions.Load());
            var cmds = SolidCraftScript.DataCommands(model);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputBase));
                LineReports.WriteLineList(model, outputBase + " - line list.csv", CultureInfo.CurrentCulture);
                LineReports.WriteTakeOff(model, outputBase + " - material take-off.csv", CultureInfo.CurrentCulture);
            }
            catch (Exception) { }

            CadJob.Report("Sending the data to SolidCraft");
            if (LiveLink.TryRun(folder, name, cmds, out var failed, out var why))
            {
                fs.ShowMessage("3D plant model: the operating data of " + (cmds.Count - failed) + " items and lines were sent to the model open in SolidCraft" +
                    (failed > 0 ? " (" + failed + " not in the model: export it again)" : "") + ".", IFlowsheet.MessageType.Information);
                return null;
            }
            if (!File.Exists(outputBase + ".sc"))
            {
                fs.ShowMessage("3D plant model: " + why + ", and there is no exported model yet: use Export 3D Plant Model first.", IFlowsheet.MessageType.Warning);
                return "There is no exported 3D model in " + Path.GetDirectoryName(outputBase) + " yet: export it first.";
            }
            CadJob.Report("SolidCraft: writing the data into the saved design");
            // the update rewrites the design: it stays read only if it was read before
            var wasRead = !PlantLayout.DesignChanged(fs, outputBase + ".sc");
            var skipped = new List<string>();
            var error = SolidCraftRunner.UpdateDesign(folder, cmds, outputBase, skipped);
            if (error == null && wasRead) PlantLayout.MarkRead(fs, outputBase + ".sc");
            fs.ShowMessage(error == null
                ? "3D plant model: " + why + "; the data were written into the saved model (" + outputBase + ".sc)" + (skipped.Count > 0 ? ", " + skipped.Count + " items not in it" : "") + "."
                : "3D plant model: the data could not be written (" + error + ").", error == null ? IFlowsheet.MessageType.Information : IFlowsheet.MessageType.Warning);
            return error == null ? null : "The data could not be written: " + error;
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

        public string DisplayText => "Export Process Flow Diagram (CADCraft)...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.ProcessFlowDiagram);

        /// <summary>The folder of the drawings: next to the simulation, or in the temporary folder when it has not been saved.</summary>
        internal static string DrawingsFolder(IFlowsheet fs)
            => Path.Combine(Path.GetDirectoryName(PlantModelExtender.OutputFolder(fs)), PlantModelExtender.ModelName(fs) + " - drawings");

        /// <summary>Draws and saves the diagram; returns the result. Public for tests and scripts.</summary>
        public static ProcessFlowDiagram.Result Export(IFlowsheet fs, string cadcraftFolder, bool openInApp, string outputFolder = null, bool pid = false, string outputBase = null)
        {
            var what = pid ? "P&ID" : "Process flow diagram";
            var name = PlantModelExtender.ModelName(fs);
            if (outputBase != null) outputFolder = Path.GetDirectoryName(outputBase);
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
            var result = ProcessFlowDiagram.Export(fs, cadcraftFolder, outputBase ?? Path.Combine(outputFolder, name + (pid ? " - PID" : " - PFD")), options.Style, title, pid);

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

        public string DisplayText => "Export P&ID (CADCraft)...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.PipingAndInstrumentation);
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

        public string DisplayText => "Export Equipment Drawings (CADCraft)...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.EquipmentDrawings);

        /// <summary>Draws and saves the drawings; returns the result. Public for tests and scripts.</summary>
        public static EquipmentDrawings.Result Export(IFlowsheet fs, string cadcraftFolder, bool openInApp, string outputFolder = null, ICollection<string> only = null)
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
            }, only);

            foreach (var f in result.Failed.Take(5))
                fs.ShowMessage("Equipment drawings: left out " + f, IFlowsheet.MessageType.Warning);
            if (result.Drawings.Count == 0)
            {
                fs.ShowMessage("CADCraft could not draw the equipment: " + result.Error, IFlowsheet.MessageType.GeneralError);
                return result;
            }
            fs.ShowMessage(string.Format("Equipment drawings: {0} general arrangement drawings saved to {1} ({2}).", result.Drawings.Count, outputFolder, DrawingSheets.FormatsText(options)),
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

        public string DisplayText => "Export Piping Isometrics (CADCraft)...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.PipingIsometrics);

        /// <summary>Draws and saves the isometrics; returns the result. Public for tests and scripts.</summary>
        public static PipingIsometrics.Result Export(IFlowsheet fs, string cadcraftFolder, bool openInApp, string outputFolder = null, ICollection<string> only = null)
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
            }, only);

            foreach (var f in result.Failed.Take(5))
                fs.ShowMessage("Piping isometrics: left out " + f, IFlowsheet.MessageType.Warning);
            if (result.Drawings.Count == 0)
            {
                fs.ShowMessage("CADCraft could not draw the isometrics: " + result.Error, IFlowsheet.MessageType.GeneralError);
                return result;
            }
            fs.ShowMessage(string.Format("Piping isometrics: {0} drawings saved to {1} ({2}).", result.Drawings.Count, outputFolder, DrawingSheets.FormatsText(options)),
                IFlowsheet.MessageType.Information);
            if (openInApp) CADCraftSession.OpenInApp(cadcraftFolder, result.Drawings[0]);
            return result;
        }
    }

    /// <summary>
    /// Writes the data sheets of the flowsheet's equipment, control loops and local indicators in
    /// CADCraft and saves them next to the simulation with the other drawings.
    /// </summary>
    public class DataSheetsExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "5b2f9e47-1c8a-4d36-9e0b-7a4c3d18f2e6";

        public string DisplayText => "Export Data Sheets (CADCraft)...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.DataSheets);

        /// <summary>Writes and saves the data sheets; returns the result. Public for tests and scripts.</summary>
        public static DataSheets.Result Export(IFlowsheet fs, string cadcraftFolder, bool openInApp, string outputFolder = null, ICollection<string> only = null)
        {
            var name = PlantModelExtender.ModelName(fs);
            if (outputFolder == null) outputFolder = ProcessFlowDiagramExtender.DrawingsFolder(fs);
            Directory.CreateDirectory(outputFolder);
            var options = ExportOptions.Load();
            var project = fs.FlowsheetOptions?.SimulationName;
            var result = DataSheets.Export(fs, cadcraftFolder, outputFolder, name, options.Style, (kind, subtitle, number) => new TitleData
            {
                Owner = options.DrawingOwner ?? "",
                Project = string.IsNullOrWhiteSpace(project) ? name : project,
                Title = kind,
                Subtitle = subtitle,
                Number = number,
                DocumentType = "DS",
                Status = "PRELIMINARY",
                Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PreparedBy = fs.FlowsheetOptions?.SimulationAuthor ?? ""
            }, only);

            foreach (var f in result.Failed.Take(5))
                fs.ShowMessage("Data sheets: left out " + f, IFlowsheet.MessageType.Warning);
            if (result.Drawings.Count == 0)
            {
                fs.ShowMessage("CADCraft could not write the data sheets: " + result.Error, IFlowsheet.MessageType.GeneralError);
                return result;
            }
            fs.ShowMessage(string.Format("Data sheets: {0} sheets saved to {1} ({2}).", result.Drawings.Count, outputFolder, DrawingSheets.FormatsText(options)),
                IFlowsheet.MessageType.Information);
            if (openInApp) CADCraftSession.OpenInApp(cadcraftFolder, result.Drawings[0]);
            return result;
        }
    }

    /// <summary>
    /// Draws the plot plan of the flowsheet's 3D plant model in CADCraft and saves it next to the
    /// simulation with the other drawings.
    /// </summary>
    public class PlotPlanExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "e7a14c92-6b3d-4f58-8c1e-2d9b0f6a3c51";

        public string DisplayText => "Export Plot Plan (CADCraft)...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.PlotPlan);

        /// <summary>Draws and saves the plot plan; returns the result. Public for tests and scripts.</summary>
        public static PlotPlan.Result Export(IFlowsheet fs, string cadcraftFolder, bool openInApp, string outputFolder = null, string outputBase = null)
        {
            var name = PlantModelExtender.ModelName(fs);
            if (outputBase != null) outputFolder = Path.GetDirectoryName(outputBase);
            if (outputFolder == null) outputFolder = ProcessFlowDiagramExtender.DrawingsFolder(fs);
            Directory.CreateDirectory(outputFolder);
            var options = ExportOptions.Load();
            var project = fs.FlowsheetOptions?.SimulationName;
            var title = new TitleData
            {
                Owner = options.DrawingOwner ?? "",
                Project = string.IsNullOrWhiteSpace(project) ? name : project,
                Title = "PLOT PLAN",
                Subtitle = string.IsNullOrWhiteSpace(project) ? name : project,
                Number = "PP-001",
                DocumentType = "PLOT PLAN",
                Status = "PRELIMINARY",
                Date = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                PreparedBy = fs.FlowsheetOptions?.SimulationAuthor ?? ""
            };
            var result = PlotPlan.Export(fs, cadcraftFolder, outputBase ?? Path.Combine(outputFolder, name + " - Plot Plan"), name, options.Style, title);
            foreach (var f in result.Failed.Take(5))
                fs.ShowMessage("Plot plan: " + f, IFlowsheet.MessageType.Warning);
            if (result.Drawing == null)
            {
                fs.ShowMessage("CADCraft could not draw the plot plan: " + result.Error, IFlowsheet.MessageType.GeneralError);
                return result;
            }
            fs.ShowMessage(string.Format("Plot plan: {0} items on a {1} sheet, saved to {2} ({3}).", result.Items, result.Sheet, outputFolder, DrawingSheets.FormatsText(options)),
                IFlowsheet.MessageType.Information);
            if (openInApp) CADCraftSession.OpenInApp(cadcraftFolder, result.Drawing);
            return result;
        }
    }

    /// <summary>
    /// Draws every drawing of the flowsheet into one folder (the PFD, the P&amp;ID, the equipment
    /// drawings, the piping isometrics, the data sheets and the plot plan) and binds their PDFs into
    /// one drawing set that opens with the drawing list.
    /// </summary>
    public class AllDrawingsExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "3b9e5d71-a2c4-4f08-8e6d-1c7f2a940b56";

        public string DisplayText => "Export All Drawings (CADCraft)...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.AllDrawings);

        /// <summary>
        /// Draws them all and binds the drawing set; returns null when at least one kind of drawing was
        /// made, else the reason. A kind that fails is reported and the others go on. Public for tests and scripts.
        /// </summary>
        public static string Export(IFlowsheet fs, string cadcraftFolder, bool openSet, string outputFolder = null)
        {
            var name = PlantModelExtender.ModelName(fs);
            if (outputFolder == null) outputFolder = ProcessFlowDiagramExtender.DrawingsFolder(fs);
            Directory.CreateDirectory(outputFolder);
            var options = ExportOptions.Load();
            var entries = new List<DrawingSet.Entry>();
            var missing = new List<string>();
            const int parts = 6;

            // the tags as the files have them (a * becomes _) back to the tags themselves, for the drawing list
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in fs.SimulationObjects.Values)
            {
                var tag = obj.GraphicObject?.Tag;
                if (!string.IsNullOrEmpty(tag)) tags[CadTools.Safe(tag)] = tag;
            }

            void Part(int i, string what) => CadJob.Stage(string.Format(CultureInfo.InvariantCulture, "Part {0} of {1}: {2}", i, parts, what));
            string Subject(string pdf, string prefix)
            {
                var stem = Path.GetFileNameWithoutExtension(pdf);
                if (stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) stem = stem.Substring(prefix.Length);
                return tags.TryGetValue(stem, out var tag) ? tag : stem;
            }
            void Sheets(IEnumerable<string> pdfs, string kind, string prefix, string label)
                => entries.AddRange(pdfs.Select(p => new DrawingSet.Entry { Pdf = p, Kind = kind, Subject = label + Subject(p, prefix) }));

            Part(1, "process flow diagram");
            var pfd = ProcessFlowDiagramExtender.Export(fs, cadcraftFolder, false, null, false, Path.Combine(outputFolder, name + " - PFD"));
            if (pfd.Drawing != null) entries.Add(new DrawingSet.Entry { Pdf = pfd.Pdf, Kind = "Process flow diagram" });
            else missing.Add("the process flow diagram");

            Part(2, "P&ID");
            var pid = ProcessFlowDiagramExtender.Export(fs, cadcraftFolder, false, null, true, Path.Combine(outputFolder, name + " - PID"));
            if (pid.Drawing != null)
            {
                entries.Add(new DrawingSet.Entry { Pdf = pid.Pdf, Kind = "P&ID", Subject = "Diagram" });
                entries.Add(new DrawingSet.Entry { Pdf = pid.LegendPdf, Kind = "P&ID", Subject = "Legend" });
            }
            else missing.Add("the P&ID");

            Part(3, "equipment drawings");
            var ga = EquipmentDrawingsExtender.Export(fs, cadcraftFolder, false, outputFolder);
            if (ga.Drawings.Count > 0) Sheets(ga.Pdfs, "General arrangement", name + " - GA ", "");
            else missing.Add("the equipment drawings");

            Part(4, "piping isometrics");
            var iso = PipingIsometricsExtender.Export(fs, cadcraftFolder, false, outputFolder);
            if (iso.Drawings.Count > 0) Sheets(iso.Pdfs, "Piping isometric", name + " - ISO ", "Line ");
            else missing.Add("the piping isometrics");

            Part(5, "data sheets");
            var ds = DataSheetsExtender.Export(fs, cadcraftFolder, false, outputFolder);
            if (ds.Drawings.Count > 0) Sheets(ds.Pdfs, "Data sheet", name + " - DS ", "");
            else missing.Add("the data sheets");

            Part(6, "plot plan");
            var plot = PlotPlanExtender.Export(fs, cadcraftFolder, false, null, Path.Combine(outputFolder, name + " - Plot Plan"));
            if (plot.Drawing != null) entries.Add(new DrawingSet.Entry { Pdf = plot.Pdf, Kind = "Plot plan" });
            else missing.Add("the plot plan");

            CadJob.Stage(null);
            var made = parts - missing.Count;
            if (made == 0) return "CADCraft could not make any of the drawings: the flowsheet messages say why.";

            string set = null;
            if (options.DrawingSet && options.SavePdf)
            {
                CadJob.Check();
                CadJob.Report("Binding the drawing set");
                var path = Path.Combine(outputFolder, CadTools.DrawingSetFile(name));
                var project = fs.FlowsheetOptions?.SimulationName;
                try
                {
                    var bound = DrawingSet.Write(path, string.IsNullOrWhiteSpace(project) ? name : project, options.DrawingOwner, entries);
                    if (bound > 0)
                    {
                        set = path;
                        fs.ShowMessage("All drawings: the drawing set " + path + " has the drawing list and " + bound + " drawings.", IFlowsheet.MessageType.Information);
                    }
                }
                catch (Exception ex)
                {
                    fs.ShowMessage("All drawings: the drawing set could not be bound (" + ex.Message + ").", IFlowsheet.MessageType.Warning);
                }
            }

            fs.ShowMessage(string.Format("All drawings: {0} of {1} kinds of drawing saved to {2}{3}.", made, parts, outputFolder,
                missing.Count > 0 ? "; " + string.Join(", ", missing) + (missing.Count == 1 ? " was" : " were") + " not made" : ""),
                missing.Count > 0 ? IFlowsheet.MessageType.Warning : IFlowsheet.MessageType.Information);
            if (openSet && set != null) CadJob.Open(set);
            return null;
        }
    }

    /// <summary>
    /// Goes back to the layout of the flowsheet drawing: forgets the places kept with the simulation
    /// and sets the last design aside, so the next export does not read its moves again.
    /// </summary>
    /// <summary>
    /// The way back from the drawings: reads a PFD or P&amp;ID (an image, a PDF or a CAD file) with
    /// the AI Assistant's vision model and builds the flowsheet from it, in a wizard where the user
    /// checks and corrects the reading step by step.
    /// </summary>
    public class DrawingImportExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "e7d93c50-2a1f-4b6e-9c84-5f0b3a7d1e62";

        public string DisplayText => "Import Flowsheet from Drawing (PFD, P&ID)...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            if (Flowsheet != null) DrawingImportPrompt.Show(Flowsheet);
        }
    }

    /// <summary>Makes the hydraulic profile of a Pipe Segment from a piping isometric read by the AI Assistant.</summary>
    public class IsometricImportExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "3b8e5d14-7c2a-4f96-a1d0-8e6c2b9f4a57";

        public string DisplayText => "Import Pipe Segment from Piping Isometric...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            if (Flowsheet != null) IsometricImportPrompt.Show(Flowsheet);
        }
    }

    public class ResetLayoutExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "c41f2a87-5e3d-4b9a-8f62-1d7e0a5b9c34";

        public string DisplayText => "Reset 3D Plant Layout...";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run() => CadToolCommand.Start(Flowsheet, CadTool.ResetLayout);

        /// <summary>Resets the layout; the last design is looked for in <paramref name="modelFolder"/> (the 3D model folder when null).</summary>
        public static void Reset(IFlowsheet fs, string modelFolder = null)
        {
            PlantLayout.Save(fs, null);

            var design = Path.Combine(modelFolder ?? PlantModelExtender.OutputFolder(fs), PlantModelExtender.ModelName(fs) + ".sc");
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
