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

using System.Collections.Generic;
using System.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// Puts the segmentation and the matching together for a flowsheet: the equipment groups of
    /// the model, each with the object the user chose for it or the one the matching proposes.
    /// The piping, the structure and the scenery are not kept in the flowsheet, since the cache
    /// gives them again.
    /// </summary>
    public static class ModelAnalysis
    {
        /// <summary>
        /// The equipment groups of the stored model with fresh proposals. The objects the user chose
        /// before stay with their groups (found again by id, or by name when the groups changed),
        /// and an object that left the flowsheet is dropped.
        /// </summary>
        public static List<ModelGroup> Analyze(PlantModelImport stored, ModelImportResult index, List<FlowsheetUnit> units, out List<SegmentGroup> segments)
        {
            segments = ModelSegmentation.Groups(index);
            var names = new HashSet<string>(units.Select(u => u.Name));
            var before = stored?.Groups ?? new List<ModelGroup>();
            var accepted = new Dictionary<string, string>();
            foreach (var g in segments.Where(x => x.Role == "equipment"))
            {
                var old = before.FirstOrDefault(b => b.Id == g.Id && b.Name == g.Name) ?? before.FirstOrDefault(b => b.Name != null && b.Name == g.Name);
                if (old?.Object != null && names.Contains(old.Object) && !accepted.ContainsValue(old.Object)) accepted[g.Id] = old.Object;
            }
            var proposals = ModelMatching.Suggest(segments, units, accepted).ToDictionary(s => s.Group);
            var result = new List<ModelGroup>();
            foreach (var g in segments.Where(x => x.Role == "equipment"))
            {
                var m = new ModelGroup { Id = g.Id, Name = g.Name, Tag = g.Tag, Class = g.Category, Role = g.Role, Islands = g.Islands.ToList() };
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
            var eq = groups.Count;
            var proposed = groups.Count(g => g.Suggested != null);
            var chosen = groups.Count(g => g.Object != null);
            var lines = segments.Count(s => s.Role == "piping");
            var text = eq + " equipment, " + lines + " piping groups; " + proposed + " proposed for flowsheet objects";
            if (chosen > 0) text += ", " + chosen + " chosen";
            return text + ".";
        }
    }
}
