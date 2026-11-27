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
using DWSIM.Interfaces;
using Newtonsoft.Json;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// The buildings of the site that are not part of the simulation, such as control rooms: where
    /// the user put them in the 3D window. They are kept with the flowsheet (its extra properties,
    /// key Plant3DSite) apart from the layout of the equipment, which the SolidCraft model reads.
    /// Their ids start with "site:".
    /// </summary>
    public static class SiteLayout
    {
        public const string Key = "Plant3DSite";
        public const string Prefix = "site:";

        public class Building
        {
            public string Id;
            public string Kind;
            /// <summary>Centre on the ground, mm, and the turn about Z, degrees.</summary>
            public double X, Y, Z;
            public int Rot;
        }

        /// <summary>The kinds the palette offers: id suffix and name.</summary>
        public static readonly (string Kind, string Name)[] Kinds = { ("controlroom", "Control Room") };

        public static bool IsSite(string id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

        public static List<Building> Load(IFlowsheet fs)
        {
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            object value;
            if (props == null || !props.TryGetValue(Key, out value) || value == null) return new List<Building>();
            try { return JsonConvert.DeserializeObject<List<Building>>(value.ToString()) ?? new List<Building>(); }
            catch (Exception) { return new List<Building>(); }
        }

        public static void Save(IFlowsheet fs, List<Building> site)
        {
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            if (props == null) return;
            if (site == null || site.Count == 0) props.Remove(Key);
            else props[Key] = JsonConvert.SerializeObject(site);
        }

        public static string Text(IFlowsheet fs)
        {
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            object value;
            return props != null && props.TryGetValue(Key, out value) && value != null ? value.ToString() : null;
        }

        public static void SetText(IFlowsheet fs, string text)
        {
            var props = fs?.ExtraProperties as IDictionary<string, object>;
            if (props == null) return;
            if (text == null) props.Remove(Key);
            else props[Key] = text;
        }

        /// <summary>A new id for a building of a kind: site:kind:n, the first n not taken.</summary>
        public static string NewId(List<Building> site, string kind)
        {
            for (var n = 1; ; n++)
            {
                var id = Prefix + kind + ":" + n;
                if (!site.Any(b => b.Id == id)) return id;
            }
        }

        public static string NameOf(string kind) => Kinds.Where(k => k.Kind == kind).Select(k => k.Name).FirstOrDefault() ?? kind;
    }
}
