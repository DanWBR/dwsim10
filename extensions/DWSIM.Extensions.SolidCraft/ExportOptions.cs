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
using Newtonsoft.Json;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// What the 3D export draws, set in the settings (Other > SolidCraft) of both interfaces and kept
    /// as one JSON text in GlobalSettings.Settings.SolidCraftOptions. A setting left out keeps its default.
    /// </summary>
    public class ExportOptions
    {
        public bool Labels = true;
        public bool FlowAnimation = true;
        public bool Insulation = true;
        public bool Platforms = true;
        public bool Instruments = true;

        /// <summary>Replay the last dynamic run (its monitored variables) on gauges in the model.</summary>
        public bool DynamicReplay = true;

        /// <summary>A photorealistic picture (path tracing in an HDR environment) next to the model.</summary>
        public bool Render = true;

        /// <summary>Top of the pipe rack steel, m.</summary>
        public double RackHeight = 6;

        /// <summary>Smallest gap kept between neighbouring items when the drawing is scaled to the plot plan, m.</summary>
        public double ItemGap = 2;

        /// <summary>The conventions of the CADCraft drawings: ISO, ASME or Petrobras (see <see cref="SolidCraft.DrawingStyle"/>).</summary>
        public string DrawingStandard = "ISO";

        /// <summary>The company in the title block of the drawings (legal owner, client).</summary>
        public string DrawingOwner = "";

        /// <summary>The space the PFD and P&amp;ID lines keep from the items and from each other, mm on the sheet.</summary>
        public double LineClearance = 4;

        /// <summary>The folder that holds cadcraft-cli, when the user chose one; empty to search for it.</summary>
        public string CADCraftPath = "";

        /// <summary>The size of the photorealistic picture, pixels.</summary>
        public int RenderWidth = 1600;
        public int RenderHeight = 1000;

        /// <summary>Path tracing samples per pixel of the picture; 0 leaves SolidCraft's own.</summary>
        public int RenderSamples = 0;

        /// <summary>The files saved next to each drawing's DXF, which is always saved.</summary>
        public bool SaveDwg = true, SavePdf = true, SavePng = true;

        /// <summary>Export All Drawings also binds the PDFs into one drawing set with a drawing list.</summary>
        public bool DrawingSet = true;

        /// <summary>
        /// The title block of the drawings: the project number (put before each document number), the
        /// revision and its description, the document status, the people (an empty Prepared by leaves
        /// the simulation's author), the department and area, and a logo drawing (DXF or DWG).
        /// </summary>
        public string ProjectNumber = "", Revision = "0", RevisionText = "ORIGINAL ISSUE", Status = "PRELIMINARY",
            PreparedBy = "", CheckedBy = "", ApprovedBy = "", Department = "", Area = "", Logo = "";

        /// <summary>The view of the photorealistic picture (see <see cref="CadTools.RenderViews"/>).</summary>
        public string RenderView = "iso";

        [JsonIgnore]
        public DrawingStyle Style => Enum.TryParse<DrawingStyle>(DrawingStandard, true, out var s) ? s : DrawingStyle.ISO;

        public static ExportOptions Load()
        {
            var text = GlobalSettings.Settings.SolidCraftOptions;
            if (string.IsNullOrWhiteSpace(text)) return new ExportOptions();
            try
            {
                var o = JsonConvert.DeserializeObject<ExportOptions>(text) ?? new ExportOptions();
                o.RackHeight = Math.Max(2.5, Math.Min(30, o.RackHeight));
                o.ItemGap = Math.Max(0.5, Math.Min(20, o.ItemGap));
                o.LineClearance = Math.Max(1, Math.Min(15, o.LineClearance));
                o.RenderWidth = Math.Max(320, Math.Min(7680, o.RenderWidth));
                o.RenderHeight = Math.Max(200, Math.Min(4320, o.RenderHeight));
                o.RenderSamples = Math.Max(0, Math.Min(16384, o.RenderSamples));
                // the old "64 (draft)" entry is SolidCraft's own count
                if (o.RenderSamples == CadTools.DefaultRenderSamples) o.RenderSamples = 0;
                return o;
            }
            catch (Exception)
            {
                return new ExportOptions();
            }
        }

        public string ToJson() => JsonConvert.SerializeObject(this);
    }
}
