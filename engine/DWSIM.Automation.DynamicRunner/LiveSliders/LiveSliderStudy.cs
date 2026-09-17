//    Live sliders: drag a specification and watch the flowsheet re-solve.
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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using DWSIM.Automation.DynamicRunner.Scenarios;
using DWSIM.Interfaces;

namespace DWSIM.Automation.DynamicRunner.LiveSliders
{
    /// <summary>A specification driven by a slider, with its range in the flowsheet's units.</summary>
    public sealed class SliderDefinition
    {
        public string ObjectName = "";
        public string Property = "";
        public double Min;
        public double Max;
    }

    /// <summary>A result shown while the sliders move.</summary>
    public sealed class WatchDefinition
    {
        public string ObjectName = "";
        public string Property = "";
    }

    /// <summary>The sliders and the watched results; both lists travel as text so the case file stays flat.</summary>
    public sealed class LiveSliderInput
    {
        public const string FileExtension = ".dwsld";

        /// <summary>"object|property|min|max;..." in the flowsheet's units, invariant culture.</summary>
        public string SliderText = "";
        /// <summary>"object|property;..."</summary>
        public string WatchText = "";
        public bool SolveWhileDragging = true;

        public List<SliderDefinition> Sliders()
        {
            var list = new List<SliderDefinition>();
            foreach (var item in SliderText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split('|');
                if (parts.Length < 4) continue;
                double lo, hi;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out lo)) continue;
                if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out hi)) continue;
                list.Add(new SliderDefinition { ObjectName = parts[0], Property = parts[1], Min = lo, Max = hi });
            }
            return list;
        }

        public void SetSliders(IEnumerable<SliderDefinition> sliders)
        {
            var ci = CultureInfo.InvariantCulture;
            SliderText = string.Join(";", sliders.Select(s => s.ObjectName + "|" + s.Property + "|" + s.Min.ToString("R", ci) + "|" + s.Max.ToString("R", ci)));
        }

        public List<WatchDefinition> Watches()
        {
            var list = new List<WatchDefinition>();
            foreach (var item in WatchText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split('|');
                if (parts.Length < 2) continue;
                list.Add(new WatchDefinition { ObjectName = parts[0], Property = parts[1] });
            }
            return list;
        }

        public void SetWatches(IEnumerable<WatchDefinition> watches)
        {
            WatchText = string.Join(";", watches.Select(w => w.ObjectName + "|" + w.Property));
        }

        public void CopyFrom(LiveSliderInput other) { SliderText = other.SliderText; WatchText = other.WatchText; SolveWhileDragging = other.SolveWhileDragging; }

        public void SaveToFile(string path) { McCabeThiele.CaseFile.Save(this, path, "LiveSliderCase"); }

        public static LiveSliderInput LoadFromFile(string path)
        {
            var input = new LiveSliderInput();
            McCabeThiele.CaseFile.Load(input, path, "LiveSliderCase", "live slider case");
            return input;
        }
    }

    /// <summary>What one solve gave: the slider values that were applied and the watched results, in the flowsheet's units.</summary>
    public sealed class LiveSliderSample
    {
        public double[] Inputs = new double[0];
        public double[] Outputs = new double[0];
        public bool Solved;
        public string Error = "";
        public double Seconds;
        public DateTime Time = DateTime.Now;
    }

    /// <summary>
    /// Live sliders: a specification of the flowsheet bound to a slider, a few results watched
    /// while it moves, every position solved as it comes. The specifications an object offers are
    /// the ones its calculation mode reads; anything else is a result and can be watched.
    /// </summary>
    public static class LiveSliderStudy
    {
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>Objects that offer at least one specification, by tag.</summary>
        public static List<ISimulationObject> ObjectsWithSpecifications(IFlowsheet fs)
        {
            return fs.SimulationObjects.Values.Where(o => !SpecificationFinder.IsDecoration(o) && SpecificationFinder.Specifications(fs, o).Count > 0)
                .OrderBy(o => o.GraphicObject.Tag, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        /// <summary>Objects that carry results, by tag.</summary>
        public static List<ISimulationObject> ObjectsWithResults(IFlowsheet fs)
        {
            return fs.SimulationObjects.Values.Where(o => !SpecificationFinder.IsDecoration(o))
                .OrderBy(o => o.GraphicObject.Tag, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static List<PropertyChoice> Specifications(IFlowsheet fs, ISimulationObject obj) { return SpecificationFinder.SpecificationChoices(fs, obj); }

        public static List<PropertyChoice> Results(IFlowsheet fs, ISimulationObject obj) { return SpecificationFinder.ResultChoices(fs, obj); }

        /// <summary>Current value of a property in the flowsheet's units (NaN when unreadable).</summary>
        public static double GetValue(IFlowsheet fs, string objectName, string property)
        {
            ISimulationObject obj;
            if (!fs.SimulationObjects.TryGetValue(objectName, out obj)) return double.NaN;
            try { return Convert.ToDouble(obj.GetPropertyValue(property, fs.FlowsheetOptions.SelectedUnitSystem), Ci); } catch (Exception) { return double.NaN; }
        }

        public static string GetUnit(IFlowsheet fs, string objectName, string property)
        {
            ISimulationObject obj;
            if (!fs.SimulationObjects.TryGetValue(objectName, out obj)) return "";
            try { return obj.GetPropertyUnit(property, fs.FlowsheetOptions.SelectedUnitSystem) ?? ""; } catch (Exception) { return ""; }
        }

        public static string GetName(IFlowsheet fs, string property)
        {
            try { var n = fs.GetTranslatedString(property); return string.IsNullOrEmpty(n) ? property : n; } catch (Exception) { return property; }
        }

        /// <summary>A range around the current value for a new slider: half to one and a half times it (0 to 1 for a fraction, 0 to 100 for a percentage).</summary>
        public static void DefaultRange(double current, string unit, out double min, out double max)
        {
            if (unit == "%") { min = 0; max = 100; return; }
            if (Math.Abs(current) < 1e-12) { min = 0; max = 1; return; }
            if (current > 0 && current <= 1 && string.IsNullOrEmpty(unit)) { min = 0; max = 1; return; }
            double lo = 0.5 * current, hi = 1.5 * current;
            min = Math.Min(lo, hi); max = Math.Max(lo, hi);
        }

        /// <summary>Writes the slider values into the flowsheet, solves it and reads the watched results. Runs on the caller's thread.</summary>
        public static LiveSliderSample Apply(IFlowsheet fs, IList<SliderDefinition> sliders, double[] values, IList<WatchDefinition> watches)
        {
            if (fs == null) throw new ArgumentNullException("fs");
            var sample = new LiveSliderSample { Inputs = (double[])values.Clone(), Outputs = new double[watches.Count] };
            var su = fs.FlowsheetOptions.SelectedUnitSystem;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < sliders.Count && i < values.Length; i++)
            {
                ISimulationObject obj;
                if (!fs.SimulationObjects.TryGetValue(sliders[i].ObjectName, out obj)) { sample.Error = "The object of slider " + (i + 1) + " is no longer on the flowsheet."; return sample; }
                try { obj.SetPropertyValue(sliders[i].Property, values[i], su); }
                catch (Exception ex) { sample.Error = obj.GraphicObject.Tag + ": " + ex.Message; return sample; }
            }
            List<Exception> errors = null;
            try { errors = fs.RequestCalculationAndWait(); }
            catch (Exception ex) { errors = new List<Exception> { ex }; }
            sample.Seconds = sw.Elapsed.TotalSeconds;
            sample.Solved = errors == null || errors.Count == 0;
            if (!sample.Solved) sample.Error = errors[0].Message;
            for (int i = 0; i < watches.Count; i++) sample.Outputs[i] = GetValue(fs, watches[i].ObjectName, watches[i].Property);
            return sample;
        }
    }
}
