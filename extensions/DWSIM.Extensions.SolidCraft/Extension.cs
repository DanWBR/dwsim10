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

        public List<IExtender> Collection { get; } = new List<IExtender> { new PlantModelExtender() };

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

        public byte[] DisplayImage
        {
            get
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
            var file = fs.FlowsheetOptions?.FilePath;
            var name = string.IsNullOrWhiteSpace(file) ? "Flowsheet" : Path.GetFileNameWithoutExtension(file);
            if (outputFolder == null)
            {
                var root = string.IsNullOrWhiteSpace(file) || !Directory.Exists(Path.GetDirectoryName(file))
                    ? Path.Combine(Path.GetTempPath(), "DWSIM")
                    : Path.GetDirectoryName(file);
                outputFolder = Path.Combine(root, name + " - 3D model");
            }
            Directory.CreateDirectory(outputFolder);

            var model = PlantModelBuilder.Build(fs, name);
            var outputBase = Path.Combine(outputFolder, name);
            var commands = SolidCraftScript.Create(model);
            var extras = SolidCraftScript.CreateLabels(model);
            extras.AddRange(FlowAnimation.Create(model, outputBase + " - flow.gif"));
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

            fs.ShowMessage(string.Format("3D plant model: {0} items and {1} lines saved to {2} (SolidCraft design, STEP{3}{4}).",
                model.Equipment.Count(e => e.Visible), model.Pipes.Count, outputFolder,
                result.Picture != null ? ", picture" : "",
                result.Animation != null ? ", flow animation: play the \"" + FlowAnimation.StudyName + "\" motion study in SolidCraft" : ""),
                IFlowsheet.MessageType.Information);

            if (openInApp) SolidCraftRunner.OpenInApp(solidcraftFolder, result.Design);

            return result;
        }
    }
}
