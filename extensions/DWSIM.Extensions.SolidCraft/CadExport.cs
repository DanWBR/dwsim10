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
using System.IO;
using System.Linq;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The CAD Tools for scripts: the exports of the CAD Tools menu without their dialogs, with the
    /// options of the settings (<see cref="Options"/>) and SolidCraft and CADCraft where the dialogs
    /// find them. A null folder or file is the one the menu would use, next to the simulation.
    /// <para>From the Script Manager (IronPython), with the flowsheet at hand as <c>Flowsheet</c>:</para>
    /// <code>
    /// import clr
    /// clr.AddReference("DWSIM.Extensions.SolidCraft")
    /// from DWSIM.Extensions.SolidCraft import CadExport
    /// o = CadExport.Options
    /// o.DrawingStandard = "ASME"; o.SavePng = False
    /// CadExport.Options = o
    /// print(CadExport.ExportAllDrawings(Flowsheet, r"C:\Projects\Plant\drawings"))
    /// </code>
    /// From Python with the FluentAPI (pythonnet), pass the flowsheet's <c>Inner</c>.
    /// </summary>
    public static class CadExport
    {
        /// <summary>The options of the drawings and of the 3D model (a copy: set it back to change them).</summary>
        public static ExportOptions Options
        {
            get => ExportOptions.Load();
            set => GlobalSettings.Settings.SolidCraftOptions = (value ?? new ExportOptions()).ToJson();
        }

        /// <summary>The folder of solidcraft-cli; null when SolidCraft is not installed.</summary>
        public static string SolidCraftFolder => SolidCraftRunner.FindInstallFolder();

        /// <summary>The folder of cadcraft-cli; null when CADCraft is not installed.</summary>
        public static string CADCraftFolder => CADCraftSession.FindInstallFolder();

        static string SolidCraft() => SolidCraftFolder ?? throw new InvalidOperationException("SolidCraft was not found: install it, or choose its folder in a CAD Tools dialog.");

        static string CADCraft() => CADCraftFolder ?? throw new InvalidOperationException("CADCraft was not found: install it, or choose its folder in a CAD Tools dialog.");

        static string Base(string dxf) => dxf == null ? null : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dxf)), Path.GetFileNameWithoutExtension(dxf));

        /// <summary>Builds the 3D plant model (design, STEP, picture, line list, take-off, web viewer).</summary>
        public static SolidCraftRunner.Result ExportPlantModel(IFlowsheet flowsheet, string folder = null)
            => PlantModelExtender.Export(flowsheet, SolidCraft(), false, folder ?? CadTools.PlantModelFolder(flowsheet));

        /// <summary>Sends the operating data to the 3D model; returns null when they reached it, else the reason.</summary>
        public static string UpdatePlantModel(IFlowsheet flowsheet, string folder = null)
            => UpdateDataExtender.Update(flowsheet, SolidCraft(), folder ?? CadTools.PlantModelFolder(flowsheet));

        /// <summary>Takes the places of the items moved in the 3D model in SolidCraft into the simulation; returns how many.</summary>
        public static int ReadMovesFromPlantModel(IFlowsheet flowsheet, string folder = null)
        {
            var design = Path.Combine(folder ?? CadTools.PlantModelFolder(flowsheet), PlantModelExtender.ModelName(flowsheet) + ".sc");
            return File.Exists(design) ? PlantModelExtender.ReadMoves(flowsheet, SolidCraft(), design) : 0;
        }

        /// <summary>Draws the process flow diagram into <paramref name="dxf"/> (with its DWG, PDF and PNG).</summary>
        public static ProcessFlowDiagram.Result ExportPFD(IFlowsheet flowsheet, string dxf = null)
            => ProcessFlowDiagramExtender.Export(flowsheet, CADCraft(), false, null, false, Base(dxf));

        /// <summary>Draws the P&amp;ID and its legend sheet into <paramref name="dxf"/>.</summary>
        public static ProcessFlowDiagram.Result ExportPID(IFlowsheet flowsheet, string dxf = null)
            => ProcessFlowDiagramExtender.Export(flowsheet, CADCraft(), false, null, true, Base(dxf));

        /// <summary>Draws the general arrangement of the items with these tags (all when none is given).</summary>
        public static EquipmentDrawings.Result ExportEquipmentDrawings(IFlowsheet flowsheet, string folder = null, params string[] tags)
            => EquipmentDrawingsExtender.Export(flowsheet, CADCraft(), false, folder, tags == null || tags.Length == 0 ? null : tags.ToList());

        /// <summary>Draws the isometrics of the lines with these tags (all when none is given).</summary>
        public static PipingIsometrics.Result ExportPipingIsometrics(IFlowsheet flowsheet, string folder = null, params string[] tags)
            => PipingIsometricsExtender.Export(flowsheet, CADCraft(), false, folder, tags == null || tags.Length == 0 ? null : tags.ToList());

        /// <summary>Writes the data sheets of the items and loops with these tags (all when none is given).</summary>
        public static DataSheets.Result ExportDataSheets(IFlowsheet flowsheet, string folder = null, params string[] tags)
            => DataSheetsExtender.Export(flowsheet, CADCraft(), false, folder, tags == null || tags.Length == 0 ? null : tags.ToList());

        /// <summary>Draws the plot plan into <paramref name="dxf"/>.</summary>
        public static PlotPlan.Result ExportPlotPlan(IFlowsheet flowsheet, string dxf = null)
            => PlotPlanExtender.Export(flowsheet, CADCraft(), false, null, Base(dxf));

        /// <summary>
        /// Writes the flowsheet as a DEXPI 2.0.1 process model (XML): the unit operations as process steps, the
        /// material streams with their conditions and compositions, and the flowsheet's layout as the diagram.
        /// Needs neither SolidCraft nor CADCraft. A null file is the simulation's name with ".dexpi.xml" in the
        /// drawings folder.
        /// </summary>
        public static DexpiExport.Result ExportDexpi(IFlowsheet flowsheet, string file = null)
            => DexpiExport.ExportProcessModel(flowsheet, file ?? Path.Combine(ProcessFlowDiagramExtender.DrawingsFolder(flowsheet), PlantModelExtender.ModelName(flowsheet) + ".dexpi.xml"));

        /// <summary>
        /// Writes the flowsheet's P&amp;ID as a DEXPI 2.0.1 plant model (XML), with what the P&amp;ID drawing shows: the
        /// equipment with its nozzles, the lines with their numbers and sizes, the valves, the feeds and products as
        /// off-page connectors, the instrument loops and the flowsheet's layout as the diagram. Needs neither SolidCraft
        /// nor CADCraft. A null file is the simulation's name with ".pid.dexpi.xml" in the drawings folder.
        /// </summary>
        public static DexpiPlantExport.Result ExportDexpiPid(IFlowsheet flowsheet, string file = null)
            => DexpiPlantExport.ExportPlantModel(flowsheet, file ?? Path.Combine(ProcessFlowDiagramExtender.DrawingsFolder(flowsheet), PlantModelExtender.ModelName(flowsheet) + ".pid.dexpi.xml"));

        /// <summary>
        /// Draws them all into <paramref name="folder"/> and binds the drawing set; returns the drawing
        /// set's file (null when the options leave it out). Throws when no drawing could be made.
        /// </summary>
        public static string ExportAllDrawings(IFlowsheet flowsheet, string folder = null)
        {
            folder = folder ?? CadTools.SavedOutput(flowsheet, CadTool.AllDrawings) ?? ProcessFlowDiagramExtender.DrawingsFolder(flowsheet);
            var error = AllDrawingsExtender.Export(flowsheet, CADCraft(), false, folder);
            if (error != null) throw new InvalidOperationException(error);
            var set = Path.Combine(folder, CadTools.DrawingSetFile(PlantModelExtender.ModelName(flowsheet)));
            return File.Exists(set) ? set : null;
        }
    }
}
