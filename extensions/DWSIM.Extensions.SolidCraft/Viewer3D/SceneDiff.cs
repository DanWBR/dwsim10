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
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// What changed between two scenes (see <see cref="SceneBuilder"/>), as a
    /// <see cref="SceneProtocol.SceneDelta"/> message: "upsert" holds the items that are new or
    /// different, whole; "remove" the ids of the items that are gone; "lines", "rack", "name" and
    /// "warnings", "site" and "terrain" are there only when they changed, and then whole.
    /// </summary>
    public static class SceneDiff
    {
        /// <summary>The change from one scene to the next; null when nothing changed.</summary>
        public static JObject Delta(JObject before, JObject after)
        {
            var delta = SceneProtocol.Message(SceneProtocol.SceneDelta);
            var old = Index(before?["items"] as JArray);
            var now = Index(after["items"] as JArray);

            var upsert = new JArray();
            foreach (var kv in now)
            {
                JObject previous;
                if (!old.TryGetValue(kv.Key, out previous) || !JToken.DeepEquals(previous, kv.Value)) upsert.Add(kv.Value.DeepClone());
            }
            var remove = new JArray(old.Keys.Where(id => !now.ContainsKey(id)));

            var changed = false;
            if (upsert.Count > 0) { delta["upsert"] = upsert; changed = true; }
            if (remove.Count > 0) { delta["remove"] = remove; changed = true; }
            foreach (var key in Keys)
            {
                var a = before?[key];
                var b = after[key];
                if (JToken.DeepEquals(a ?? JValue.CreateNull(), b ?? JValue.CreateNull())) continue;
                delta[key] = b?.DeepClone() ?? JValue.CreateNull();
                changed = true;
            }
            return changed ? delta : null;
        }

        /// <summary>
        /// The scene a delta makes of another one, as the window applies it: removed items leave,
        /// upserted ones replace the item with their id in its place or go at the end.
        /// </summary>
        public static JObject Apply(JObject scene, JObject delta)
        {
            var result = (JObject)scene.DeepClone();
            var items = result["items"] as JArray;
            if (items == null) result["items"] = items = new JArray();

            var removed = new HashSet<string>((delta["remove"] as JArray ?? new JArray()).Select(t => (string)t));
            foreach (var item in items.OfType<JObject>().Where(i => removed.Contains((string)i["id"])).ToList()) item.Remove();

            foreach (var item in (delta["upsert"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var id = (string)item["id"];
                var at = items.OfType<JObject>().FirstOrDefault(i => (string)i["id"] == id);
                if (at != null) at.Replace(item.DeepClone());
                else items.Add(item.DeepClone());
            }
            foreach (var key in Keys)
                if (delta.ContainsKey(key)) result[key] = delta[key].DeepClone();
            return result;
        }

        /// <summary>The parts of a scene besides its items, sent whole when they change.</summary>
        static readonly string[] Keys = { "lines", "rack", "name", "warnings", "site", "terrain", "model" };

        /// <summary>Whether two scenes hold the same items (in any order), lines, rack and name.</summary>
        public static bool Same(JObject a, JObject b) => Delta(a, b) == null && Delta(b, a) == null;

        static Dictionary<string, JObject> Index(JArray items)
        {
            var result = new Dictionary<string, JObject>();
            if (items == null) return result;
            foreach (var item in items.OfType<JObject>())
            {
                var id = (string)item["id"];
                if (id != null) result[id] = item;
            }
            return result;
        }
    }
}
