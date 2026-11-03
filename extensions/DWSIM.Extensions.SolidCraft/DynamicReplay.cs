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
using DWSIM.Interfaces;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// A replay of the last dynamic run in the 3D model: the monitored variables of the integrator
    /// that ran (kept in memory after a run, not in the file) drive markers on bar gauges, so a level
    /// rises and falls on the front of its vessel and a valve's opening or a line's flow moves on a
    /// short gauge beside it. A "Dynamic Replay" motion study plays the run, and a GIF is saved.
    /// </summary>
    public static class DynamicReplay
    {
        public const string StudyName = "Dynamic Replay";

        // most frames the study gets, longer runs resampled: SolidCraft takes at most about 100 GIF
        // frames at 1024 x 640
        const int MaxSteps = 100;

        public class Series
        {
            public string Description, ObjectId, PropertyId, Name;
            public List<double> Values = new List<double>();
        }

        public class Run
        {
            public List<double> Times = new List<double>();   // s
            public List<Series> Series = new List<Series>();
        }

        /// <summary>The monitored variables of the last dynamic run, or null when nothing has run in this session.</summary>
        public static Run Collect(IFlowsheet fs)
        {
            var dm = fs?.DynamicsManager;
            if (dm == null) return null;

            IDynamicsIntegrator integrator = null;
            try
            {
                if (dm.CurrentSchedule != null && dm.ScheduleList.TryGetValue(dm.CurrentSchedule, out var schedule))
                    dm.IntegratorList.TryGetValue(schedule.CurrentIntegrator, out integrator);
            }
            catch (Exception) { }
            if (integrator == null || integrator.MonitoredVariableValues.Count < 2)
                integrator = dm.IntegratorList.Values.Where(i => i.MonitoredVariableValues.Count >= 2).OrderByDescending(i => i.MonitoredVariableValues.Count).FirstOrDefault();
            if (integrator == null || integrator.MonitoredVariableValues.Count < 2) return null;

            var run = new Run();
            var vars = integrator.MonitoredVariables;
            foreach (var v in vars)
            {
                string name = v.PropertyID;
                try { name = fs.GetTranslatedString(v.PropertyID) ?? v.PropertyID; } catch { }
                run.Series.Add(new Series { Description = string.IsNullOrWhiteSpace(v.Description) ? name : v.Description, ObjectId = v.ObjectID, PropertyId = v.PropertyID, Name = name });
            }
            foreach (var kv in integrator.MonitoredVariableValues.OrderBy(kv => kv.Key))
            {
                run.Times.Add(new TimeSpan(kv.Key).TotalSeconds);
                for (var i = 0; i < run.Series.Count; i++)
                {
                    double x = double.NaN;
                    if (i < kv.Value.Count)
                    {
                        var mv = kv.Value[i];
                        if (double.TryParse(mv.PropertyValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw))
                        {
                            x = raw;
                            // a level in its own units goes to metres
                            if (IsLevel(run.Series[i]) && !string.IsNullOrEmpty(mv.PropertyUnits))
                                try { x = SharedClasses.SystemsOfUnits.Converter.ConvertToSI(mv.PropertyUnits, raw); } catch { }
                        }
                    }
                    run.Series[i].Values.Add(x);
                }
            }
            return run;
        }

        static bool IsLevel(Series s) => (s.PropertyId + " " + s.Name + " " + s.Description).ToLowerInvariant().Contains("level");

        static bool IsOpening(Series s) => (s.Name + " " + s.Description).ToLowerInvariant().Contains("opening");

        /// <summary>The replay commands for the extras run: gauges, their markers on slider joints, the study and its GIF.</summary>
        public static List<SolidCraftScript.Command> Create(PlantModel model, IFlowsheet fs, Run run, string gifPath, List<string> notes)
        {
            var cmds = new List<SolidCraftScript.Command>();
            if (run == null) return cmds;

            var items = model.Equipment.Where(e => e.Name != null && e.Visible && !e.IsInstrument).ToDictionary(e => e.Name, e => e);
            var lines = model.Pipes.GroupBy(r => r.Tag).ToDictionary(g => g.Key, g => g.First());

            var steps = Math.Min(MaxSteps, run.Times.Count - 1);
            var perItem = new Dictionary<string, int>();
            var perLine = new Dictionary<string, int>();
            var n = 0;

            foreach (var s in run.Series)
            {
                var values = s.Values.Where(v => !double.IsNaN(v)).ToList();
                if (values.Count < 2 || !fs.SimulationObjects.TryGetValue(s.ObjectId ?? "", out var obj)) continue;
                var tag = obj.GraphicObject?.Tag ?? obj.Name;

                Vec3 bottom;
                double height;
                double lo, hi;
                if (items.TryGetValue(obj.Name, out var item) && (IsLevel(s) || IsOpening(s)))
                {
                    var env = item.Envelope;
                    perItem.TryGetValue(item.Name, out var k);
                    perItem[item.Name] = k + 1;
                    var front = env.Kind == EnvelopeKind.HorizontalCylinder || env.Kind == EnvelopeKind.Box
                        ? (item.Rotation % 180 == 0 ? env.HalfY : env.HalfX) : env.HalfX;
                    var dx = (k % 2 == 0 ? 1 : -1) * 300.0 * ((k + 1) / 2) + 150;
                    if (IsLevel(s))
                    {
                        // a level glass from the bottom to the top tangent of the vessel, in metres of liquid
                        height = Math.Max(500, env.ZTop - env.HeadDepth - env.ZBottom);
                        bottom = item.Origin + new Vec3(dx, -front - 250, env.ZBottom);
                        lo = 0;
                        hi = height / 1000;
                    }
                    else
                    {
                        // a valve's travel, 0 to 100 %
                        height = 400;
                        bottom = item.Origin + new Vec3(dx + 250, -front - 200, env.ZBottom);
                        lo = 0;
                        hi = values.Max() > 1.0001 ? 100 : 1;
                    }
                }
                else if (lines.TryGetValue(tag, out var line) && line.Path.Count >= 2)
                {
                    perLine.TryGetValue(line.Tag, out var k);
                    perLine[line.Tag] = k + 1;
                    var p = PointAlong(line.Path, Math.Min(0.95, 0.6 + 0.12 * k));
                    height = 500;
                    bottom = p + new Vec3(0, 0, line.OuterDiameter / 2 + line.Insulation + 150);
                    lo = values.Min();
                    hi = values.Max();
                }
                else continue;

                if (!(hi > lo)) hi = lo + 1;
                n++;
                var travel = new List<double>();
                for (var i = 0; i <= steps; i++)
                {
                    var v = Sample(run.Times, s.Values, run.Times[0] + (run.Times[run.Times.Count - 1] - run.Times[0]) * i / steps);
                    travel.Add(Math.Round(Math.Max(0, Math.Min(1, (v - lo) / (hi - lo))) * height, 1));
                }
                AddGauge(cmds, n, tag + " " + s.Description, bottom, height, travel);
            }

            if (n == 0)
            {
                notes.Add("the last dynamic run has no levels, valve openings or line variables to replay.");
                return cmds;
            }

            SolidCraftScript.Add(cmds, "motion.study", "the dynamic replay", new JObject { ["name"] = StudyName, ["steps"] = steps });
            SolidCraftScript.Add(cmds, "motion.export_animation", "the dynamic replay animation", new JObject
            {
                ["study"] = StudyName,
                ["path"] = gifPath,
                ["width"] = 1024,
                ["height"] = 640,
                ["fps"] = 12,
                ["samples"] = steps + 1,
                ["grid"] = false,
                ["axes"] = false
            });
            notes.Add(n + " variables of the last dynamic run (" + (run.Times[run.Times.Count - 1] - run.Times[0]).ToString("0", CultureInfo.InvariantCulture) +
                " s) replayed on gauges: play the \"" + StudyName + "\" motion study in SolidCraft.");
            return cmds;
        }

        /// <summary>A bar gauge: a fixed scale (grounded) and a marker on a vertical slider joint that follows the variable.</summary>
        static void AddGauge(List<SolidCraftScript.Command> cmds, int n, string description, Vec3 bottom, double height, List<double> travel)
        {
            var subject = "replay " + n;
            var scale = "Replay " + n + " " + description;
            var marker = "Replay " + n + " marker";
            SolidCraftScript.Add(cmds, "component.create", subject, new JObject { ["name"] = scale, ["parent"] = "root", ["grounded"] = true });
            SolidCraftScript.Add(cmds, "solid.box", subject, new JObject
            {
                ["length"] = 60, ["width"] = 60, ["height"] = SolidCraftScript.Round(height),
                ["center"] = new JArray((bottom + new Vec3(0, 0, height / 2)).ToArray()),
                ["body_name"] = scale + " scale"
            });
            SolidCraftScript.Add(cmds, "appearance.assign", subject, new JObject { ["bodies"] = new JArray(scale + " scale"), ["color"] = "#9fb3c8" });
            SolidCraftScript.Add(cmds, "component.metadata.set", subject, new JObject { ["component"] = scale, ["bom_type"] = "reference", ["description"] = description });

            SolidCraftScript.Add(cmds, "component.create", subject, new JObject { ["name"] = marker, ["parent"] = "root", ["grounded"] = false });
            // a marker that shows at the scale of the plant: bigger on a tall level glass
            var size = Math.Max(160, 0.04 * height);
            SolidCraftScript.Add(cmds, "solid.box", subject, new JObject
            {
                ["length"] = SolidCraftScript.Round(size), ["width"] = SolidCraftScript.Round(size), ["height"] = SolidCraftScript.Round(size / 3),
                ["center"] = new JArray((bottom + new Vec3(0, 0, size / 6)).ToArray()),
                ["body_name"] = marker + " body"
            });
            SolidCraftScript.Add(cmds, "appearance.assign", subject, new JObject { ["bodies"] = new JArray(marker + " body"), ["color"] = "#ff7a1a" });
            SolidCraftScript.Add(cmds, "component.metadata.set", subject, new JObject { ["component"] = marker, ["bom_type"] = "reference", ["description"] = "Replay marker" });
            SolidCraftScript.Add(cmds, "joint.create", subject, new JObject
            {
                ["type"] = "slider",
                ["name"] = "replay " + n,
                ["flip"] = true,
                ["quiet"] = true,
                ["a"] = new JObject { ["occurrence"] = scale + ":1", ["point"] = new JArray(bottom.ToArray()), ["z"] = new JArray(0, 0, 1) },
                ["b"] = new JObject { ["occurrence"] = marker + ":1", ["point"] = new JArray(bottom.ToArray()), ["z"] = new JArray(0, 0, 1) },
                ["$study"] = StudyName,
                ["$points"] = new JArray(travel.Select((t, i) => new JArray(i, t)))
            });
        }

        static double Sample(List<double> times, List<double> values, double t)
        {
            var i = times.BinarySearch(t);
            if (i >= 0) return Value(values, i);
            i = ~i;
            if (i <= 0) return Value(values, 0);
            if (i >= times.Count) return Value(values, times.Count - 1);
            var f = (t - times[i - 1]) / (times[i] - times[i - 1]);
            return Value(values, i - 1) * (1 - f) + Value(values, i) * f;
        }

        static double Value(List<double> values, int i)
        {
            for (var k = i; k >= 0; k--) if (!double.IsNaN(values[k])) return values[k];
            for (var k = i; k < values.Count; k++) if (!double.IsNaN(values[k])) return values[k];
            return 0;
        }

        static Vec3 PointAlong(List<Vec3> path, double fraction)
        {
            double total = 0;
            for (var i = 1; i < path.Count; i++) total += (path[i] - path[i - 1]).Length;
            var target = fraction * total;
            for (var i = 1; i < path.Count; i++)
            {
                var l = (path[i] - path[i - 1]).Length;
                if (target <= l) return path[i - 1] + (path[i] - path[i - 1]) * (l > 0 ? target / l : 0);
                target -= l;
            }
            return path[path.Count - 1];
        }
    }
}
