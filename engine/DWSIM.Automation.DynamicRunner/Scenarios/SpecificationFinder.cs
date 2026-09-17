//    Which properties of an object are the specifications of its current calculation mode.
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
using System.Linq;
using DWSIM.Automation.FluentAPI.Diagnostics;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.Interfaces.Enums.GraphicObjects;

namespace DWSIM.Automation.DynamicRunner.Scenarios
{
    /// <summary>One property of an object, with its readable name and its value in the flowsheet's units.</summary>
    public sealed class PropertyChoice
    {
        public string ObjectName = "";
        public string ObjectTag = "";
        public string Property = "";
        public string Name = "";
        public string Unit = "";
        public double Value = double.NaN;
        public string Label { get { return Name + (string.IsNullOrEmpty(Unit) ? "" : " (" + Unit + ")"); } }
    }

    /// <summary>
    /// Tells the specifications of an object (the numbers its calculation mode reads) from its
    /// results. The property sets an object reports (WR, RW) are far too loose for this, so a
    /// writable property counts as a specification only when its SI value matches a slot of the
    /// degrees-of-freedom analysis. A stream fed by a unit and an energy stream carry results only.
    /// </summary>
    public static class SpecificationFinder
    {
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>True for graphic-only objects (text, tables, charts), which carry no properties.</summary>
        public static bool IsDecoration(ISimulationObject obj)
        {
            if (obj == null || obj.GraphicObject == null) return true;
            var type = obj.GraphicObject.ObjectType;
            return type == ObjectType.GO_Text || type == ObjectType.GO_Image || type == ObjectType.GO_Table || type == ObjectType.GO_MasterTable ||
                   type == ObjectType.GO_SpreadsheetTable || type == ObjectType.GO_FloatingTable || type == ObjectType.GO_Chart || type == ObjectType.GO_Rectangle || type == ObjectType.GO_HTMLText;
        }

        /// <summary>The property ids that are specifications of the object's current mode.</summary>
        public static HashSet<string> Specifications(IFlowsheet fs, ISimulationObject obj)
        {
            var result = new HashSet<string>();
            if (IsDecoration(obj)) return result;
            var type = obj.GraphicObject.ObjectType;
            bool isStream = type == ObjectType.MaterialStream || type == ObjectType.EnergyStream;
            bool fedByUnit = type == ObjectType.EnergyStream || (isStream && obj.GraphicObject.InputConnectors.Any(c => c.IsAttached));
            if (fedByUnit) return result;
            string[] writable;
            try { writable = obj.GetProperties(PropertyType.WR) ?? new string[0]; } catch (Exception) { return result; }
            List<double> specValues = null;
            try
            {
                var dof = DegreesOfFreedomAnalysis.Analyze(fs, obj);
                if (dof != null && dof.Supported) specValues = dof.Slots.Where(sl => sl.Required && sl.IsSet && sl.Value.HasValue).Select(sl => sl.Value.Value).ToList();
            }
            catch (Exception) { }
            foreach (var prop in writable.Distinct())
            {
                if (specValues == null) { result.Add(prop); continue; }
                double si;
                try { si = Convert.ToDouble(obj.GetPropertyValue(prop, null), Ci); } catch (Exception) { continue; }
                if (!double.IsNaN(si) && specValues.Any(sv => Math.Abs(sv - si) <= 1e-7 * Math.Max(1.0, Math.Abs(sv)))) result.Add(prop);
            }
            return result;
        }

        /// <summary>The specifications of the object as choices, with values in the flowsheet's units.</summary>
        public static List<PropertyChoice> SpecificationChoices(IFlowsheet fs, ISimulationObject obj)
        {
            return Choices(fs, obj, Specifications(fs, obj), true);
        }

        /// <summary>Every numeric property of the object that is not a specification, as choices.</summary>
        public static List<PropertyChoice> ResultChoices(IFlowsheet fs, ISimulationObject obj)
        {
            return Choices(fs, obj, Specifications(fs, obj), false);
        }

        private static List<PropertyChoice> Choices(IFlowsheet fs, ISimulationObject obj, HashSet<string> specs, bool wantSpecs)
        {
            var list = new List<PropertyChoice>();
            if (IsDecoration(obj)) return list;
            var su = fs.FlowsheetOptions.SelectedUnitSystem;
            string[] all;
            try { all = obj.GetProperties(PropertyType.ALL) ?? new string[0]; } catch (Exception) { return list; }
            foreach (var prop in all.Distinct())
            {
                if (specs.Contains(prop) != wantSpecs) continue;
                var c = new PropertyChoice { ObjectName = obj.Name, ObjectTag = obj.GraphicObject.Tag, Property = prop };
                try { c.Value = Convert.ToDouble(obj.GetPropertyValue(prop, su), Ci); } catch (Exception) { continue; }
                if (double.IsNaN(c.Value) && !wantSpecs) continue;
                try { c.Unit = obj.GetPropertyUnit(prop, su) ?? ""; } catch (Exception) { }
                try { c.Name = fs.GetTranslatedString(prop); } catch (Exception) { c.Name = prop; }
                if (string.IsNullOrEmpty(c.Name)) c.Name = prop;
                list.Add(c);
            }
            return list;
        }
    }
}
