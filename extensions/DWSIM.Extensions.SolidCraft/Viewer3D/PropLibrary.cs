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
using System.Text;
using DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport;
using Newtonsoft.Json;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>A 3D model the user brought in as an object of the site (a tree, a vehicle, a building).</summary>
    public class PropInfo
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        /// <summary>The category of the build bar and of the budget (see <see cref="SiteCatalog.Categories"/>).</summary>
        [JsonProperty("cat")] public string Category;
        /// <summary>Width, depth and height, metres.</summary>
        [JsonProperty("w")] public double W;
        [JsonProperty("d")] public double D;
        [JsonProperty("h")] public double H;
        /// <summary>The cost of one, USD, for the budget (0 when the user did not give one).</summary>
        [JsonProperty("cost")] public double Cost;
        /// <summary>The hash of the file it came from.</summary>
        [JsonProperty("sha")] public string Sha256;
        /// <summary>The name of the file it came from (the library only, never the flowsheet).</summary>
        [JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)] public string Source;
        [JsonProperty("tris", DefaultValueHandling = DefaultValueHandling.Ignore)] public int Triangles;

        [JsonIgnore] public string Kind => PropLibrary.Prefix + Id;

        /// <summary>What a flowsheet keeps of a prop it uses: enough for the budget and a stand-in box, never the model or where it came from.</summary>
        public PropInfo Reference() => new PropInfo { Id = Id, Name = Name, Category = Category, W = W, D = D, H = H, Cost = Cost, Sha256 = Sha256 };
    }

    /// <summary>
    /// The user's own objects for the site builder, kept in the user's data folder: each prop is a
    /// folder with the model ready for the window (prop.glb: metres, the origin at the middle of its
    /// base) and its description (prop.json). A flowsheet that uses a prop keeps only its
    /// reference (<see cref="PropInfo.Reference"/>), never the model, because the licence of a
    /// bought model seldom lets it be passed on; on a computer without the prop, the window draws a
    /// box of its size, and the budget still counts it.
    /// </summary>
    public static class PropLibrary
    {
        public const string Prefix = "user:";

        /// <summary>Where the props are kept; tests point it somewhere else.</summary>
        public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DWSIM", "Props");

        static readonly object Gate = new object();
        static Dictionary<string, PropInfo> _props;
        static string _root;

        public static bool IsUser(string kind) => kind != null && kind.StartsWith(Prefix, StringComparison.Ordinal);

        static string IdOf(string kindOrId) => kindOrId == null ? null : IsUser(kindOrId) ? kindOrId.Substring(Prefix.Length) : kindOrId;

        public static string Folder(string id) => Path.Combine(Root, id);

        public static string GlbPath(PropInfo p) => Path.Combine(Folder(p.Id), "prop.glb");

        static Dictionary<string, PropInfo> Props()
        {
            lock (Gate)
            {
                if (_props != null && _root == Root) return _props;
                var d = new Dictionary<string, PropInfo>();
                try
                {
                    if (Directory.Exists(Root))
                        foreach (var dir in Directory.GetDirectories(Root))
                        {
                            var json = Path.Combine(dir, "prop.json");
                            if (!File.Exists(json) || !File.Exists(Path.Combine(dir, "prop.glb"))) continue;
                            try
                            {
                                var p = JsonConvert.DeserializeObject<PropInfo>(File.ReadAllText(json));
                                if (p?.Id != null && p.Id == Path.GetFileName(dir)) d[p.Id] = p;
                            }
                            catch (Exception) { }
                        }
                }
                catch (Exception) { }
                _props = d;
                _root = Root;
                return d;
            }
        }

        /// <summary>Reads the library again (after another program changed it).</summary>
        public static void Reload()
        {
            lock (Gate) _props = null;
        }

        /// <summary>Every prop of the library, by name.</summary>
        public static List<PropInfo> All() => Props().Values.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        /// <summary>The prop of a kind ("user:id") or id, or null.</summary>
        public static PropInfo Find(string kindOrId)
        {
            var id = IdOf(kindOrId);
            PropInfo p;
            return id != null && Props().TryGetValue(id, out p) ? p : null;
        }

        /// <summary>
        /// Brings a model file in as a prop: read in any format the plant model importer reads,
        /// in metres (the unit guessed for a prop when the file does not give it, or the height the
        /// user asks for), Z up, with the origin at the middle of its base. The same file brought in
        /// again replaces its prop.
        /// </summary>
        public static PropInfo Import(string path, string name = null, string category = null, double? cost = null, double? height = null)
        {
            var model = ModelImporter.Read(path);
            if (model.TriangleCount == 0) throw new InvalidDataException("The file does not have any surfaces to draw.");
            var place = new Placement3D { Scale = ModelGeometry.GuessScale(model, true), Up = ModelGeometry.GuessUp(model) };
            ModelGeometry.Normalize(model, place);
            ModelGeometry.CenterOnBase(model);
            if (height.HasValue) Resize(model, height.Value);
            var sha = ModelImporter.Hash(path);
            var p = new PropInfo
            {
                Id = sha.Substring(0, 16),
                Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(path) : name.Trim(),
                Sha256 = sha,
                Source = Path.GetFileName(path),
                Triangles = model.TriangleCount
            };
            p.Category = CategoryOr(category, GuessCategory(p.Name));
            p.Cost = Math.Max(0, cost ?? 0);
            Save(p, model);
            return p;
        }

        /// <summary>
        /// Changes a prop: its name, category, cost, or its height (the model is scaled to it, the
        /// same in every direction). Null leaves a field as it is.
        /// </summary>
        public static PropInfo Edit(string kindOrId, string name = null, string category = null, double? cost = null, double? height = null)
        {
            var p = Find(kindOrId) ?? throw new InvalidOperationException("there is no prop '" + kindOrId + "' in the library");
            if (!string.IsNullOrWhiteSpace(name)) p.Name = name.Trim();
            if (category != null) p.Category = CategoryOr(category, p.Category);
            if (cost.HasValue) p.Cost = Math.Max(0, cost.Value);
            ImportedModel model = null;
            if (height.HasValue && Math.Abs(height.Value - p.H) > 1e-4)
            {
                // the GLB has Y up; read back in the plant frame and scaled
                model = GltfReader.Read(GlbPath(p));
                ModelGeometry.Normalize(model, new Placement3D { Scale = 1, Up = UpAxis.Y });
                ModelGeometry.CenterOnBase(model);
                Resize(model, height.Value);
            }
            Save(p, model);
            return p;
        }

        /// <summary>Takes a prop out of the library; the flowsheets that use it draw a box in its place.</summary>
        public static bool Remove(string kindOrId)
        {
            var p = Find(kindOrId);
            if (p == null) return false;
            try { Directory.Delete(Folder(p.Id), true); }
            catch (Exception) { return false; }
            Reload();
            return true;
        }

        static void Save(PropInfo p, ImportedModel model)
        {
            var dir = Folder(p.Id);
            Directory.CreateDirectory(dir);
            if (model != null)
            {
                double[] min, max;
                model.Bounds(out min, out max);
                p.W = Math.Round(max[0] - min[0], 3);
                p.D = Math.Round(max[1] - min[1], 3);
                p.H = Math.Round(max[2] - min[2], 3);
                p.Triangles = model.TriangleCount;
                GlbWriter.WriteProp(model, p.Name, Path.Combine(dir, "prop.glb"));
            }
            File.WriteAllText(Path.Combine(dir, "prop.json"), JsonConvert.SerializeObject(p, Formatting.Indented));
            Reload();
        }

        /// <summary>Scales a model standing on its base to a height, the same in every direction.</summary>
        static void Resize(ImportedModel model, double height)
        {
            if (height <= 0 || height > 500) throw new ArgumentOutOfRangeException(nameof(height), "a height must lie between 0 and 500 m");
            double[] min, max;
            if (!model.Bounds(out min, out max) || max[2] - min[2] <= 0) return;
            var f = (float)(height / (max[2] - min[2]));
            foreach (var part in model.Parts)
                for (var i = 0; i < part.Positions.Length; i++) part.Positions[i] *= f;
        }

        static string CategoryOr(string category, string fallback)
        {
            var c = SiteCatalog.Categories.FirstOrDefault(x => string.Equals(x, category, StringComparison.OrdinalIgnoreCase));
            return c ?? fallback;
        }

        static readonly KeyValuePair<string, string[]>[] Words =
        {
            new KeyValuePair<string, string[]>(SiteCatalog.Nature, new[] { "TREE", "BUSH", "SHRUB", "PLANT", "PALM", "PINE", "OAK", "BIRCH", "ROCK", "STONE", "GRASS", "FLOWER", "HEDGE", "ARVORE", "PEDRA" }),
            new KeyValuePair<string, string[]>(SiteCatalog.Vehicles, new[] { "CAR", "TRUCK", "VAN", "BUS", "VEHICLE", "FORKLIFT", "PICKUP", "TRACTOR", "CRANE", "CARRO", "CAMINHAO" }),
            new KeyValuePair<string, string[]>(SiteCatalog.Buildings, new[] { "BUILDING", "HOUSE", "SHED", "OFFICE", "WAREHOUSE", "HANGAR", "GARAGE", "PREDIO", "CASA", "GALPAO" }),
            new KeyValuePair<string, string[]>(SiteCatalog.Fences, new[] { "FENCE", "WALL", "BARRIER", "GATE", "CERCA", "MURO", "PORTAO" }),
            new KeyValuePair<string, string[]>(SiteCatalog.Safety, new[] { "HYDRANT", "EXTINGUISHER", "SHOWER", "HIDRANTE", "EXTINTOR" }),
        };

        /// <summary>The category the name of a prop says (a tree is nature, a truck a vehicle), Utilities when it says none.</summary>
        public static string GuessCategory(string name)
        {
            var u = (name ?? "").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in u)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToUpperInvariant(ch));
            // whole words: a short keyword is the word itself (CAR, not JACARANDA), a longer one may
            // start it (TREES, BUILDINGS)
            var words = sb.ToString().Split(new[] { ' ', '_', '-', '.', ',', '(', ')', '/', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var w in Words)
                if (words.Any(word => w.Value.Any(x => word == x || x.Length >= 4 && word.StartsWith(x, StringComparison.Ordinal)))) return w.Key;
            return SiteCatalog.Utilities;
        }

        /// <summary>The prop as an entry of the site catalog: a point object of its size and cost.</summary>
        public static SiteCatalog.Entry EntryOf(PropInfo p) => new SiteCatalog.Entry
        {
            Kind = p.Kind,
            Name = p.Name,
            Category = p.Category ?? SiteCatalog.Utilities,
            Shape = SiteCatalog.Shape.Point,
            W = p.W,
            D = p.D,
            H = p.H,
            Cost = p.Cost
        };
    }
}
