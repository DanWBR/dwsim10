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
using System.ComponentModel;
using System.Linq;
using DWSIM.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// The objects of the site round the plant that the user built in the 3D window (see
    /// <see cref="SiteCatalog"/>): buildings, roads, fences, trees and the rest. They are kept with
    /// the flowsheet (its extra properties, key Plant3DSite) apart from the layout of the equipment,
    /// which the SolidCraft model reads, and the relief of the ground (key Plant3DTerrain). Their
    /// ids start with "site:".
    /// </summary>
    /// <remarks>
    /// The text is {"v": 2, "adopted": bool, "items": [buildings]}. A flowsheet saved before kept
    /// only the list of buildings, which still loads. "adopted" turns true when the window hands over
    /// the site it drew by itself round the plant (fence, road, cars, trees) as objects; from then on
    /// the window draws the site from the objects only.
    /// </remarks>
    public static class SiteLayout
    {
        public const string Key = "Plant3DSite";
        public const string TerrainKey = "Plant3DTerrain";
        /// <summary>The summary of the site for the TEA and the LCA (see <see cref="SiteCatalog.Summary"/>), kept up to date here.</summary>
        public const string SummaryKey = "Plant3DSiteSummary";
        public const string Prefix = "site:";

        public class Building
        {
            public string Id;
            public string Kind;
            /// <summary>The place on the ground (a line's or an area's first point), mm, and the turn about Z, degrees.</summary>
            public double X, Y, Z;
            public int Rot;
            /// <summary>The points of a line or the outline of an area, mm, [x, y] each; null for a point object.</summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<double[]> Points;
            /// <summary>A look of the kind (a colour, a model), as the window names it.</summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Variant;
            [DefaultValue(1.0)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            public double Scale = 1.0;
        }

        public class Site
        {
            [JsonProperty("v")] public int Version = 2;
            [JsonProperty("adopted")] public bool Adopted;
            [JsonProperty("items")] public List<Building> Items = new List<Building>();
        }

        public static bool IsSite(string id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

        static IDictionary<string, object> Props(IFlowsheet fs) => fs?.ExtraProperties as IDictionary<string, object>;

        static string Read(IFlowsheet fs, string key)
        {
            var props = Props(fs);
            object value;
            return props != null && props.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }

        static void Write(IFlowsheet fs, string key, string text)
        {
            var props = Props(fs);
            if (props == null) return;
            if (text == null) props.Remove(key);
            else props[key] = text;
        }

        /// <summary>The site of the flowsheet; an empty one when it has none or its text does not read.</summary>
        public static Site LoadSite(IFlowsheet fs)
        {
            var text = Read(fs, Key);
            if (string.IsNullOrWhiteSpace(text)) return new Site();
            try
            {
                var token = JToken.Parse(text);
                if (token.Type == JTokenType.Array)
                    return new Site { Items = token.ToObject<List<Building>>() ?? new List<Building>() };
                var site = token.ToObject<Site>() ?? new Site();
                if (site.Items == null) site.Items = new List<Building>();
                site.Items.RemoveAll(b => b?.Id == null);
                return site;
            }
            catch (Exception) { return new Site(); }
        }

        public static void SaveSite(IFlowsheet fs, Site site)
        {
            if (site == null || !site.Adopted && site.Items.Count == 0) Write(fs, Key, null);
            else Write(fs, Key, JsonConvert.SerializeObject(site));
            UpdateSummary(fs);
        }

        /// <summary>
        /// Writes the summary of the site the TEA and the LCA read (key Plant3DSiteSummary), or takes
        /// it away when there is neither a site nor a relief. Never throws.
        /// </summary>
        public static void UpdateSummary(IFlowsheet fs)
        {
            try
            {
                var site = LoadSite(fs);
                var terrain = LoadTerrain(fs);
                if (site.Items.Count == 0 && terrain == null) { Write(fs, SummaryKey, null); return; }
                Write(fs, SummaryKey, SiteCatalog.Summary(site.Items, terrain).ToString(Formatting.None));
            }
            catch (Exception) { }
        }

        /// <summary>The objects of the site.</summary>
        public static List<Building> Load(IFlowsheet fs) => LoadSite(fs).Items;

        /// <summary>Keeps the objects of the site; whether the site was adopted stays as it was.</summary>
        public static void Save(IFlowsheet fs, List<Building> items)
        {
            var site = LoadSite(fs);
            site.Items = items ?? new List<Building>();
            SaveSite(fs, site);
        }

        public static string Text(IFlowsheet fs) => Read(fs, Key);

        public static void SetText(IFlowsheet fs, string text)
        {
            Write(fs, Key, text);
            UpdateSummary(fs);
        }

        /// <summary>A new id for an object of a kind: site:kind:n, the first n not taken.</summary>
        public static string NewId(List<Building> site, string kind)
        {
            for (var n = 1; ; n++)
            {
                var id = Prefix + kind + ":" + n;
                if (!site.Any(b => b.Id == id)) return id;
            }
        }

        public static string NameOf(string kind) => SiteCatalog.NameOf(kind);

        #region Geometry

        /// <summary>The largest distance from the origin a place of the site may have, mm.</summary>
        public const double Reach = 2000000;

        /// <summary>The most points a line or an area may have.</summary>
        public const int MaxPoints = 500;

        /// <summary>Moves an object to a new place and turn: a line or an area moves and turns its points with it, about its first point.</summary>
        public static void MoveTo(Building b, double x, double y, double z, int rot)
        {
            if (b.Points != null && b.Points.Count > 0)
            {
                var turn = (rot - b.Rot) * Math.PI / 180.0;
                double c = Math.Cos(turn), s = Math.Sin(turn);
                var pts = new List<double[]>();
                foreach (var p in b.Points)
                {
                    double dx = p[0] - b.X, dy = p[1] - b.Y;
                    pts.Add(new[] { Math.Round(x + c * dx - s * dy), Math.Round(y + s * dx + c * dy) });
                }
                b.Points = pts;
            }
            b.X = x;
            b.Y = y;
            b.Z = z;
            b.Rot = rot;
        }

        /// <summary>The length of a line, mm.</summary>
        public static double Length(List<double[]> pts)
        {
            var sum = 0.0;
            for (var i = 1; i < pts.Count; i++) sum += Math.Sqrt(Sq(pts[i][0] - pts[i - 1][0]) + Sq(pts[i][1] - pts[i - 1][1]));
            return sum;
        }

        /// <summary>The area inside an outline, mm2 (shoelace).</summary>
        public static double Area(List<double[]> pts)
        {
            var sum = 0.0;
            for (var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                sum += a[0] * b[1] - b[0] * a[1];
            }
            return Math.Abs(sum) / 2.0;
        }

        static double Sq(double v) => v * v;

        /// <summary>
        /// Whether an object touches a rectangle (x0, y0, x1, y1, mm), as the bulldozer sees it: a
        /// point object by its footprint, a line by any of its segments, an area by its outline or
        /// by holding the rectangle inside it.
        /// </summary>
        public static bool Touches(Building b, double x0, double y0, double x1, double y1)
        {
            if (x0 > x1) { var t = x0; x0 = x1; x1 = t; }
            if (y0 > y1) { var t = y0; y0 = y1; y1 = t; }
            var e = SiteCatalog.Find(b.Kind);
            if (b.Points == null || b.Points.Count == 0)
            {
                // the footprint turned with the object, as the bounds of the turned rectangle
                var k = 500.0 * (b.Scale > 0 ? b.Scale : 1);
                double w = (e?.W ?? 1) * k, d = (e?.D ?? 1) * k, a = b.Rot * Math.PI / 180.0;
                double hx = Math.Abs(Math.Cos(a)) * w + Math.Abs(Math.Sin(a)) * d;
                double hy = Math.Abs(Math.Sin(a)) * w + Math.Abs(Math.Cos(a)) * d;
                return b.X + hx >= x0 && b.X - hx <= x1 && b.Y + hy >= y0 && b.Y - hy <= y1;
            }
            var pts = b.Points;
            var closed = e?.Shape == SiteCatalog.Shape.Area;
            var n = closed ? pts.Count : pts.Count - 1;
            for (var i = 0; i < n; i++)
                if (SegmentTouches(pts[i], pts[(i + 1) % pts.Count], x0, y0, x1, y1)) return true;
            if (pts.Count == 1) return Inside(pts[0][0], pts[0][1], x0, y0, x1, y1);
            return closed && Contains(pts, (x0 + x1) / 2.0, (y0 + y1) / 2.0);
        }

        static bool Inside(double x, double y, double x0, double y0, double x1, double y1) => x >= x0 && x <= x1 && y >= y0 && y <= y1;

        static bool SegmentTouches(double[] a, double[] b, double x0, double y0, double x1, double y1)
        {
            if (Inside(a[0], a[1], x0, y0, x1, y1) || Inside(b[0], b[1], x0, y0, x1, y1)) return true;
            var corners = new[] { new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 } };
            for (var i = 0; i < 4; i++)
                if (Cross(a, b, corners[i], corners[(i + 1) % 4])) return true;
            return false;
        }

        static double Side(double[] a, double[] b, double[] c) => (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]);

        static bool Cross(double[] a, double[] b, double[] c, double[] d)
        {
            double d1 = Side(c, d, a), d2 = Side(c, d, b), d3 = Side(a, b, c), d4 = Side(a, b, d);
            return (d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0);
        }

        /// <summary>Whether a point lies inside an outline (even-odd rule).</summary>
        public static bool Contains(List<double[]> pts, double x, double y)
        {
            var inside = false;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                double xi = pts[i][0], yi = pts[i][1], xj = pts[j][0], yj = pts[j][1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        #endregion

        #region Terrain

        /// <summary>
        /// The relief of the ground: a grid of nx by ny heights from (x0, y0), one every "cell" mm,
        /// each a 16-bit integer of centimetres (little-endian, base 64 in "h"), and optionally a
        /// byte per cell with the depth of the water over it in decimetres, 0 where it is dry ("w").
        /// The window shapes it; DWSIM checks and keeps it.
        /// </summary>
        public class Terrain
        {
            [JsonProperty("x0")] public double X0;
            [JsonProperty("y0")] public double Y0;
            [JsonProperty("cell")] public double Cell;
            [JsonProperty("nx")] public int Nx;
            [JsonProperty("ny")] public int Ny;
            [JsonProperty("h")] public string Heights;
            [JsonProperty("w", NullValueHandling = NullValueHandling.Ignore)] public string Water;
        }

        public const int MaxTerrainCells = 1 << 20;

        public static string TerrainText(IFlowsheet fs) => Read(fs, TerrainKey);

        public static void SetTerrainText(IFlowsheet fs, string text)
        {
            Write(fs, TerrainKey, text);
            UpdateSummary(fs);
        }

        public static Terrain LoadTerrain(IFlowsheet fs)
        {
            var text = Read(fs, TerrainKey);
            if (string.IsNullOrWhiteSpace(text)) return null;
            try { return JsonConvert.DeserializeObject<Terrain>(text); }
            catch (Exception) { return null; }
        }

        /// <summary>What is wrong with a terrain, or null when it is sound.</summary>
        public static string Check(Terrain t)
        {
            if (t == null) return "there is no terrain";
            if (t.Nx < 2 || t.Ny < 2 || (long)t.Nx * t.Ny > MaxTerrainCells) return "the terrain grid must be at least 2 x 2 and at most " + MaxTerrainCells + " cells";
            if (double.IsNaN(t.Cell) || t.Cell < 250 || t.Cell > 10000) return "a terrain cell must be between 0.25 and 10 m";
            if (double.IsNaN(t.X0) || double.IsNaN(t.Y0) || Math.Abs(t.X0) > Reach || Math.Abs(t.Y0) > Reach) return "the terrain lies too far away";
            byte[] h;
            try { h = Convert.FromBase64String(t.Heights ?? ""); }
            catch (FormatException) { return "the terrain heights are not base 64"; }
            if (h.Length != 2 * t.Nx * t.Ny) return "the terrain needs " + t.Nx * t.Ny + " heights";
            if (t.Water != null)
            {
                byte[] w;
                try { w = Convert.FromBase64String(t.Water); }
                catch (FormatException) { return "the terrain water is not base 64"; }
                if (w.Length != t.Nx * t.Ny) return "the terrain water needs one byte per cell";
            }
            return null;
        }

        #endregion
    }
}
