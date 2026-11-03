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

        /// <summary>Top of the pipe rack steel, m.</summary>
        public double RackHeight = 6;

        /// <summary>Smallest gap kept between neighbouring items when the drawing is scaled to the plot plan, m.</summary>
        public double ItemGap = 2;

        public static ExportOptions Load()
        {
            var text = GlobalSettings.Settings.SolidCraftOptions;
            if (string.IsNullOrWhiteSpace(text)) return new ExportOptions();
            try
            {
                var o = JsonConvert.DeserializeObject<ExportOptions>(text) ?? new ExportOptions();
                o.RackHeight = Math.Max(2.5, Math.Min(30, o.RackHeight));
                o.ItemGap = Math.Max(0.5, Math.Min(20, o.ItemGap));
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
