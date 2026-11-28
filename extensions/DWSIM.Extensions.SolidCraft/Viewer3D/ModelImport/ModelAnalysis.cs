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
            var also = new Dictionary<string, List<string>>();
            var objectsTaken = new HashSet<string>();
            foreach (var g in segments.Where(x => x.Role == "equipment"))
            {
                var old = before.FirstOrDefault(b => b.Id == g.Id && (b.User || b.Name == g.Name)) ?? (g.User ? null : before.FirstOrDefault(b => !b.User && b.Name != null && b.Name == g.Name));
                if (old?.Object != null && names.Contains(old.Object) && objectsTaken.Add(old.Object)) accepted[g.Id] = old.Object;
                if (old?.Also != null && accepted.ContainsKey(g.Id))
                {
                    var more = old.Also.Where(o => names.Contains(o) && objectsTaken.Add(o)).ToList();
                    if (more.Count > 0) also[g.Id] = more;
                }
            }
            var proposals = ModelMatching.Suggest(segments, units, accepted, also.Values.SelectMany(x => x)).ToDictionary(s => s.Group);
            var result = new List<ModelGroup>();
            foreach (var g in segments.Where(x => x.Role == "equipment" || x.User))
            {
                var m = new ModelGroup
                {
                    Id = g.Id, Name = g.Name, Tag = g.Tag, Class = g.Category, Role = g.Role, Islands = g.Islands.ToList(), User = g.User,
                    Min = g.Min?.Select(x => Math.Round(x, 3)).ToArray(), Max = g.Max?.Select(x => Math.Round(x, 3)).ToArray()
                };
                string chosen;
                if (accepted.TryGetValue(g.Id, out chosen)) m.Object = chosen;
                List<string> more;
                if (also.TryGetValue(g.Id, out more)) m.Also = more;
                Suggestion s;
                if (proposals.TryGetValue(g.Id, out s))
                {
                    m.Suggested = s.Object;
                    m.Score = s.Score;
                    m.Why = s.Why;
                    if (s.Also.Count > 0) m.SuggestedAlso = s.Also.ToList();
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
                if (g?.Also != null && g.Also.Count > 0) o["also"] = new JArray(g.Also);
                if (g?.Suggested != null || g?.SuggestedAlso != null)
                {
                    if (g.Suggested != null) o["suggested"] = g.Suggested;
                    if (g.SuggestedAlso != null) o["suggestedAlso"] = new JArray(g.SuggestedAlso);
                    o["score"] = g.Score;
                    o["why"] = g.Why;
                }
                list.Add(o);
            }
            // the pipes of the model that join two groups whose objects no stream joins: a stream the
            // flowsheet may be missing
            var links = new JArray();
            var unitOf = units.ToDictionary(u => u.Name);
            foreach (var a in segments.Where(x => x.Role == "equipment"))
                foreach (var bid in a.Neighbours)
                {
                    if (string.CompareOrdinal(a.Id, bid) >= 0) continue;
                    ModelGroup ga, gb;
                    if (!byId.TryGetValue(a.Id, out ga) || !byId.TryGetValue(bid, out gb)) continue;
                    var oa = ga.Objects().Where(unitOf.ContainsKey).ToList();
                    var ob = gb.Objects().Where(unitOf.ContainsKey).ToList();
                    if (oa.Count == 0 || ob.Count == 0) continue;
                    if (oa.Any(x => ob.Any(y => unitOf[x].Neighbours.Contains(y)))) continue;
                    links.Add(new JObject { ["a"] = a.Id, ["b"] = bid, ["from"] = oa[0], ["to"] = ob[0], ["fromTag"] = unitOf[oa[0]].Tag, ["toTag"] = unitOf[ob[0]].Tag });
                }
            return new JObject
            {
                ["type"] = SceneProtocol.ModelGroups,
                ["groups"] = list,
                ["units"] = new JArray(units.OrderBy(u => u.Tag, StringComparer.OrdinalIgnoreCase).Select(u => new JObject { ["name"] = u.Name, ["tag"] = u.Tag, ["kind"] = u.Category })),
                ["links"] = links
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
            var copy = new ModelGroup { Id = NewId(groups), Name = s.Name, Tag = s.Tag, Class = s.Category, Role = role, Islands = s.Islands.ToList(), User = true, Min = s.Min, Max = s.Max };
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
        public static string Assign(List<ModelGroup> groups, List<SegmentGroup> segments, string id, string obj, bool add = false)
        {
            var s = segments.FirstOrDefault(x => x.Id == id);
            if (s == null) return "there is no group '" + id + "'";
            var g = groups.FirstOrDefault(x => x.Id == id) ?? UserCopy(groups, s, "equipment");
            if (g.Role != "equipment") g.Role = "equipment";
            if (obj != null)
                foreach (var other in groups)
                {
                    if (other.Object == obj) other.Object = null;
                    other.Also?.Remove(obj);
                }
            if (add && obj != null && g.Object != null)
            {
                if (g.Also == null) g.Also = new List<string>();
                if (!g.Also.Contains(obj)) g.Also.Add(obj);
            }
            else
            {
                g.Object = obj;
                if (obj == null) g.Also = null;
            }
            return null;
        }

        /// <summary>The group takes what the matching proposes for it: the object and the others of the same tag.</summary>
        public static string Accept(List<ModelGroup> groups, List<SegmentGroup> segments, string id)
        {
            var g = groups.FirstOrDefault(x => x.Id == id);
            if (g == null || g.Suggested == null && (g.SuggestedAlso == null || g.SuggestedAlso.Count == 0)) return "the group does not have a proposal";
            var more = g.SuggestedAlso?.ToList() ?? new List<string>();
            string problem = null;
            if (g.Suggested != null) problem = Assign(groups, segments, id, g.Suggested);
            foreach (var o in more) if (problem == null) problem = Assign(groups, segments, id, o, true);
            return problem;
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

        /// <summary>
        /// The objects the groups stand for, each with the place of its group in the plant: the middle
        /// of the plan and the bottom of the box (mm, Z up), where the 3D view and the SolidCraft model
        /// put the object.
        /// </summary>
        public static Dictionary<string, double[]> Placements(IEnumerable<ModelGroup> groups)
        {
            var result = new Dictionary<string, double[]>();
            foreach (var g in groups ?? Enumerable.Empty<ModelGroup>())
            {
                if (g.Min == null || g.Max == null || g.Min.Length < 3 || g.Max.Length < 3) continue;
                var place = new[] { Math.Round((g.Min[0] + g.Max[0]) / 2 * 1000), Math.Round((g.Min[1] + g.Max[1]) / 2 * 1000), Math.Round(g.Min[2] * 1000) };
                foreach (var o in g.Objects()) result[o] = place;
            }
            return result;
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
