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
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// Puts the segmentation, the user's changes and the matching together for a flowsheet. The
    /// flowsheet keeps the equipment groups (with the object the user chose and the one the matching
    /// proposes) and the groups the user made; the other groups come from the cache again.
    /// </summary>
    public static class ModelAnalysis
    {
        public static readonly string[] Roles = { "equipment", "piping", "structure", "scenery", "hidden" };

        /// <summary>
        /// The groups of the stored model with fresh proposals. The groups the user made take their
        /// islands from the groups the segmentation finds; the objects the user chose stay with their
        /// groups (found again by id, or by name when the groups changed), and an object that left the
        /// flowsheet is dropped.
        /// </summary>
        public static List<ModelGroup> Analyze(PlantModelImport stored, ModelImportResult index, List<FlowsheetUnit> units, out List<SegmentGroup> segments)
        {
            segments = ModelSegmentation.Groups(index);
            var before = stored?.Groups ?? new List<ModelGroup>();
            var users = before.Where(b => b.User && b.Islands != null && b.Islands.Count > 0).ToList();
            if (users.Count > 0)
            {
                var n = index.Islands.Count;
                var claimed = new HashSet<int>(users.SelectMany(u => u.Islands));
                foreach (var s in segments) s.Islands.RemoveAll(claimed.Contains);
                segments.RemoveAll(s => s.Islands.Count == 0);
                foreach (var u in users)
                    segments.Add(new SegmentGroup
                    {
                        Id = u.Id,
                        Role = u.Role,
                        Name = u.Name,
                        Tag = u.Tag ?? ModelSegmentation.TagOf(u.Name ?? ""),
                        Islands = u.Islands.Where(i => i >= 0 && i < n).Distinct().ToList(),
                        User = true
                    });
                segments.RemoveAll(s => s.Islands.Count == 0);
                ModelSegmentation.Remeasure(segments, index.Islands);
            }

            var names = new HashSet<string>(units.Select(u => u.Name));
            var accepted = new Dictionary<string, string>();
            foreach (var g in segments.Where(x => x.Role == "equipment"))
            {
                var old = before.FirstOrDefault(b => b.Id == g.Id && (b.User || b.Name == g.Name)) ?? (g.User ? null : before.FirstOrDefault(b => !b.User && b.Name != null && b.Name == g.Name));
                if (old?.Object != null && names.Contains(old.Object) && !accepted.ContainsValue(old.Object)) accepted[g.Id] = old.Object;
            }
            var proposals = ModelMatching.Suggest(segments, units, accepted).ToDictionary(s => s.Group);
            var result = new List<ModelGroup>();
            foreach (var g in segments.Where(x => x.Role == "equipment" || x.User))
            {
                var m = new ModelGroup { Id = g.Id, Name = g.Name, Tag = g.Tag, Class = g.Category, Role = g.Role, Islands = g.Islands.ToList(), User = g.User };
                string chosen;
                if (accepted.TryGetValue(g.Id, out chosen)) m.Object = chosen;
                Suggestion s;
                if (proposals.TryGetValue(g.Id, out s))
                {
                    m.Suggested = s.Object;
                    m.Score = s.Score;
                    m.Why = s.Why;
                }
                result.Add(m);
            }
            return result;
        }

        /// <summary>A line for the status: how many equipment groups, how many proposed or chosen.</summary>
        public static string Summary(List<ModelGroup> groups, List<SegmentGroup> segments)
        {
            var equipment = groups.Where(g => g.Role == "equipment").ToList();
            var proposed = equipment.Count(g => g.Suggested != null);
            var chosen = equipment.Count(g => g.Object != null);
            var lines = segments.Count(s => s.Role == "piping");
            var text = equipment.Count + " equipment, " + lines + " piping groups; " + proposed + " proposed for flowsheet objects";
            if (chosen > 0) text += ", " + chosen + " chosen";
            return text + ".";
        }

        /// <summary>
        /// Every group of the model, as the window's review shows them (id, name, tag, role, kind, the
        /// islands, the object chosen and the one proposed, the score and why, the size and the
        /// middle), and the flowsheet objects one can choose from.
        /// </summary>
        public static JObject Payload(List<SegmentGroup> segments, List<ModelGroup> groups, List<FlowsheetUnit> units)
        {
            var byId = groups.ToDictionary(g => g.Id);
            var list = new JArray();
            foreach (var s in segments)
            {
                ModelGroup g;
                byId.TryGetValue(s.Id, out g);
                var o = new JObject
                {
                    ["id"] = s.Id,
                    ["name"] = s.Name,
                    ["role"] = s.Role,
                    ["islands"] = new JArray(s.Islands),
                    ["c"] = new JArray(s.Center.Select(x => Math.Round(x, 3))),
                    ["length"] = Math.Round(s.Length, 2),
                    ["diameter"] = Math.Round(s.Diameter, 2)
                };
                if (s.Tag != null) o["tag"] = s.Tag;
                if (s.Category != null) o["kind"] = s.Category;
                if (s.User) o["user"] = true;
                if (g?.Object != null) o["object"] = g.Object;
                if (g?.Suggested != null)
                {
                    o["suggested"] = g.Suggested;
                    o["score"] = g.Score;
                    o["why"] = g.Why;
                }
                list.Add(o);
            }
            return new JObject
            {
                ["type"] = SceneProtocol.ModelGroups,
                ["groups"] = list,
                ["units"] = new JArray(units.OrderBy(u => u.Tag, StringComparer.OrdinalIgnoreCase).Select(u => new JObject { ["name"] = u.Name, ["tag"] = u.Tag, ["kind"] = u.Category }))
            };
        }

        static string NewId(List<ModelGroup> groups)
        {
            var k = 1;
            while (groups.Any(g => g.Id == "u" + k)) k++;
            return "u" + k;
        }

        static ModelGroup UserCopy(List<ModelGroup> groups, SegmentGroup s, string role)
        {
            var existing = groups.FirstOrDefault(g => g.Id == s.Id);
            if (existing != null && existing.User) { existing.Role = role; return existing; }
            var copy = new ModelGroup { Id = NewId(groups), Name = s.Name, Tag = s.Tag, Class = s.Category, Role = role, Islands = s.Islands.ToList(), User = true };
            if (existing != null)
            {
                copy.Object = role == "equipment" ? existing.Object : null;
                groups.Remove(existing);
            }
            groups.Add(copy);
            return copy;
        }

        /// <summary>
        /// The group stands for the object (GraphicObject.Name), or for none when it is null; another
        /// group that stood for the object lets it go. A group that was not equipment becomes equipment.
        /// </summary>
        public static string Assign(List<ModelGroup> groups, List<SegmentGroup> segments, string id, string obj)
        {
            var s = segments.FirstOrDefault(x => x.Id == id);
            if (s == null) return "there is no group '" + id + "'";
            var g = groups.FirstOrDefault(x => x.Id == id) ?? UserCopy(groups, s, "equipment");
            if (g.Role != "equipment") g.Role = "equipment";
            if (obj != null) foreach (var other in groups.Where(x => x.Object == obj)) other.Object = null;
            g.Object = obj;
            return null;
        }

        public static string SetRole(List<ModelGroup> groups, List<SegmentGroup> segments, string id, string role)
        {
            if (!Roles.Contains(role)) return "unknown role '" + role + "'";
            var s = segments.FirstOrDefault(x => x.Id == id);
            if (s == null) return "there is no group '" + id + "'";
            if (s.Role == role) return null;
            UserCopy(groups, s, role);
            return null;
        }

        /// <summary>The groups become one; it is equipment when one of them was, and keeps the first tag and object found.</summary>
        public static string Merge(List<ModelGroup> groups, List<SegmentGroup> segments, IList<string> ids)
        {
            var parts = ids.Distinct().Select(id => segments.FirstOrDefault(x => x.Id == id)).ToList();
            if (parts.Count < 2 || parts.Any(p => p == null)) return "join needs two groups or more of the model";
            var stored = parts.Select(p => groups.FirstOrDefault(g => g.Id == p.Id)).Where(g => g != null).ToList();
            var merged = new ModelGroup
            {
                Id = NewId(groups),
                Name = parts.Select(p => p.Name).FirstOrDefault(n => ModelSegmentation.TagOf(n ?? "") != null) ?? parts[0].Name,
                Tag = parts.Select(p => p.Tag).FirstOrDefault(t => t != null),
                Role = parts.Any(p => p.Role == "equipment") ? "equipment" : parts[0].Role,
                Islands = parts.SelectMany(p => p.Islands).Distinct().ToList(),
                Object = stored.Select(g => g.Object).FirstOrDefault(o => o != null),
                User = true
            };
            foreach (var g in stored) groups.Remove(g);
            groups.Add(merged);
            return null;
        }

        /// <summary>Some islands of a group become a group of their own, with the same role.</summary>
        public static string Split(List<ModelGroup> groups, List<SegmentGroup> segments, string id, IList<int> islands)
        {
            var s = segments.FirstOrDefault(x => x.Id == id);
            if (s == null) return "there is no group '" + id + "'";
            var take = islands.Distinct().Where(s.Islands.Contains).ToList();
            if (take.Count == 0 || take.Count == s.Islands.Count) return "split needs some of the group's islands, and not all of them";
            var g = groups.FirstOrDefault(x => x.Id == id);
            if (g != null && g.User) g.Islands.RemoveAll(take.Contains);
            groups.Add(new ModelGroup { Id = NewId(groups), Name = s.Name + " (part)", Role = s.Role, Islands = take, User = true });
            return null;
        }

        /// <summary>The flowsheet object type DWSIM adds for a kind of equipment of the model.</summary>
        public static string ObjectTypeFor(string kind)
        {
            switch (kind)
            {
                case "column": return "ShortcutColumn";
                case "tank": return "Tank";
                case "exchanger": return "HeatExchanger";
                case "aircooler": return "AirCooler2";
                case "pump": return "Pump";
                case "compressor": return "Compressor";
                case "furnace": return "Heater";
                case "reactor": return "RCT_Conversion";
                default: return "Vessel";
            }
        }
    }
}
