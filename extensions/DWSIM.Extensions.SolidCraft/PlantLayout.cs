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
using DWSIM.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The 3D layout kept with the simulation: where each item was moved to in SolidCraft. It is
    /// saved in the flowsheet's extra properties (with the .dwxmz file), keyed by the object name so
    /// it survives a change of tag. Items without a place follow the flowsheet drawing.
    /// </summary>
    public static class PlantLayout
    {
        const string Key = "SolidCraftLayout";

        public static Dictionary<string, Placement> Load(IFlowsheet fs)
        {
            var result = new Dictionary<string, Placement>();
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            if (props == null || !props.TryGetValue(Key, out var value) || value == null) return result;
            try
            {
                var saved = JsonConvert.DeserializeObject<Dictionary<string, Placement>>(value.ToString());
                if (saved != null) foreach (var kv in saved) result[kv.Key] = kv.Value;
            }
            catch (Exception)
            {
                // a layout that cannot be read is no layout: the drawing places everything again
            }
            return result;
        }

        public static void Save(IFlowsheet fs, Dictionary<string, Placement> layout)
        {
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            if (props == null) return;
            if (layout == null || layout.Count == 0) props.Remove(Key);
            else props[Key] = JsonConvert.SerializeObject(layout);
        }

        /// <summary>The design whose places were last read, and its time then, kept with the simulation.</summary>
        const string ReadKey = "SolidCraftDesignRead";

        class DesignRead
        {
            public string Design;
            public long Written;
        }

        /// <summary>Notes that the places of the items in <paramref name="design"/> are in the layout as the file stands now.</summary>
        public static void MarkRead(IFlowsheet fs, string design)
        {
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            if (props == null || !File.Exists(design)) return;
            props[ReadKey] = JsonConvert.SerializeObject(new DesignRead { Design = Path.GetFullPath(design), Written = File.GetLastWriteTimeUtc(design).Ticks });
        }

        /// <summary>
        /// True when <paramref name="design"/> was saved (in SolidCraft) after DWSIM last read the places
        /// of its items, or was never read: items moved there may not be in the layout yet.
        /// </summary>
        public static bool DesignChanged(IFlowsheet fs, string design)
        {
            if (!File.Exists(design)) return false;
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            if (props == null || !props.TryGetValue(ReadKey, out var value) || value == null) return true;
            try
            {
                var read = JsonConvert.DeserializeObject<DesignRead>(value.ToString());
                if (read == null || !string.Equals(read.Design, Path.GetFullPath(design), StringComparison.OrdinalIgnoreCase)) return true;
                // a second of slack for file systems that keep coarse times
                return File.GetLastWriteTimeUtc(design).Ticks > read.Written + TimeSpan.TicksPerSecond;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// The places of the items moved in an exported design: each item's place when the design was
        /// written (in its metadata) carried through its occurrence's transform. Keyed by tag; items
        /// left where they were are not returned; null when the design cannot be read. A turn that is
        /// not a quarter turn about the vertical is rounded to one, and a tilt is ignored; both are reported.
        /// </summary>
        public static Dictionary<string, Placement> ReadMoves(string solidcraftFolder, string design, List<string> warnings)
        {
            var moves = new Dictionary<string, Placement>();
            var results = SolidCraftRunner.Query(solidcraftFolder, design, new JArray
            {
                new JObject { ["command"] = "document.metadata", ["params"] = new JObject() },
                new JObject { ["command"] = "component.list", ["params"] = new JObject() }
            });
            if (results == null || results.Count < 2)
            {
                warnings.Add("the last design could not be read, so the places of the items moved in it were not taken.");
                return null;
            }

            var placements = results[0]?["metadata"]?["org.dwsim.flowsheet"]?["placements"] as JObject;
            if (placements == null) return moves;

            var root = (results[1]?["components"] as JArray)?.FirstOrDefault(c => c["parent"] == null || c["parent"].Type == JTokenType.Null);
            var children = root?["children"] as JArray;
            if (children == null) return moves;

            foreach (var child in children.OfType<JObject>())
            {
                var occurrence = (string)child["name"];
                if (string.IsNullOrEmpty(occurrence)) continue;
                var colon = occurrence.LastIndexOf(':');
                var tag = colon > 0 ? occurrence.Substring(0, colon) : occurrence;
                if (!(placements[tag] is JObject basePlace)) continue;

                var m = child["transform"] as JArray;
                if (m == null || m.Count < 4) continue;
                double M(int r, int c) => (double)m[r][c];

                // row-vector convention: p' = p . M, the last row the translation
                double ox = (double)basePlace["x"], oy = (double)basePlace["y"], oz = (double)basePlace["z"];
                var x = ox * M(0, 0) + oy * M(1, 0) + oz * M(2, 0) + M(3, 0);
                var y = ox * M(0, 1) + oy * M(1, 1) + oz * M(2, 1) + M(3, 1);
                var z = ox * M(0, 2) + oy * M(1, 2) + oz * M(2, 2) + M(3, 2);
                var turn = Math.Atan2(M(0, 1), M(0, 0)) * 180 / Math.PI;
                var quarter = (int)Math.Round(turn / 90) * 90;

                var moved = Math.Abs(x - ox) > 0.5 || Math.Abs(y - oy) > 0.5 || Math.Abs(z - oz) > 0.5 || quarter != 0;
                if (!moved) continue;

                if (Math.Abs(turn - quarter) > 2)
                    warnings.Add(tag + ": turned by " + turn.ToString("0", CultureInfo.InvariantCulture) + " degrees in SolidCraft; kept as " + quarter + " (only quarter turns are kept).");
                if (Math.Abs(M(2, 2) - 1) > 1e-3)
                    warnings.Add(tag + ": tilted in SolidCraft; the tilt is not kept.");

                moves[tag] = new Placement
                {
                    X = Math.Round(x),
                    Y = Math.Round(y),
                    Z = Math.Round(z),
                    Rot = ((((int)basePlace["rot"] + quarter) % 360) + 360) % 360
                };
            }
            return moves;
        }
    }
}
