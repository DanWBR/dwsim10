//    Column internals rating as a utility attached to a rigorous column.
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
using System.Globalization;
using System.Xml.Linq;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;

namespace DWSIM.Automation.DynamicRunner.ColumnInternals
{
    /// <summary>
    /// The column internals case attached to a column: the case travels with the simulation and the
    /// rating figures show up among the column's properties. The interfaces open the full tool on it;
    /// a host without one still restores it and rates the column when the flowsheet is solved.
    /// </summary>
    public class ColumnInternalsUtility : DWSIM.FlowsheetBase.Utilities.AttachedUtility
    {
        public const string CaseKey = "Case";

        /// <summary>The last rating, kept for the interface that shows it; not saved.</summary>
        public ColumnInternalsResult LastResult;

        public ColumnInternalsUtility()
        {
            Settings[CaseKey] = "";
            Results["Highest Fraction of Flood"] = 0.0;
            Results["Column Pressure Drop"] = 0.0;
            Results["Required Diameter"] = 0.0;
            Results["Internals Height"] = 0.0;
        }

        public override FlowsheetUtility GetUtilityType() { return FlowsheetUtility.ColumnInternals; }

        public override string GetPropertyUnits(string pname)
        {
            var su = AttachedTo?.GetFlowsheet()?.FlowsheetOptions?.SelectedUnitSystem;
            if (su == null) return "";
            switch (pname)
            {
                case "Column Pressure Drop": return su.deltaP;
                case "Required Diameter":
                case "Internals Height": return su.distance;
                default: return "";
            }
        }

        /// <summary>The case as the tool edits it; a fresh one on the attached column when nothing was saved yet.</summary>
        public ColumnInternalsInput GetInput()
        {
            var xml = Convert.ToString(Settings[CaseKey], CultureInfo.InvariantCulture);
            ColumnInternalsInput input = null;
            if (!string.IsNullOrWhiteSpace(xml))
            {
                try { input = ColumnInternalsInput.FromXml(XElement.Parse(xml)); } catch { input = null; }
            }
            if (input == null) input = new ColumnInternalsInput();
            if (AttachedTo != null && AttachedTo.GraphicObject != null) input.ColumnName = AttachedTo.GraphicObject.Tag;
            return input;
        }

        public void SetInput(ColumnInternalsInput input)
        {
            if (input == null) { Settings[CaseKey] = ""; return; }
            Settings[CaseKey] = input.ToXml().ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>Rates the attached column with the saved case (nothing to do without sections or before the column solves).</summary>
        public override void Update()
        {
            var fs = AttachedTo?.GetFlowsheet();
            if (fs == null || AttachedTo == null || !AttachedTo.Calculated) return;
            var input = GetInput();
            if (input.Sections.Count == 0) return;
            var result = ColumnInternalsStudy.Run(fs, input);
            Store(result);
        }

        /// <summary>Keeps a rating produced by the tool and publishes its figures.</summary>
        public void Store(ColumnInternalsResult result)
        {
            LastResult = result;
            if (result == null) return;
            var su = AttachedTo?.GetFlowsheet()?.FlowsheetOptions?.SelectedUnitSystem;
            double maxFlood = 0, reqD = 0;
            foreach (var sr in result.Sections)
            {
                if (sr.MaxFloodFraction > maxFlood) maxFlood = sr.MaxFloodFraction;
                if (sr.RequiredDiameter > reqD) reqD = sr.RequiredDiameter;
            }
            Results["Highest Fraction of Flood"] = maxFlood;
            Results["Column Pressure Drop"] = su != null ? SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, result.TotalPressureDrop) : result.TotalPressureDrop;
            Results["Required Diameter"] = su != null ? SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(su.distance, reqD) : reqD;
            Results["Internals Height"] = su != null ? SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(su.distance, result.TotalHeight) : result.TotalHeight;
        }
    }
}
