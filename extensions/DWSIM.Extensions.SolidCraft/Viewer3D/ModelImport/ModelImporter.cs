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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>What an import of a plant model found and where it left its cache.</summary>
    public class ModelImportResult
    {
        public ModelReference Reference;
        public ImportedModel Model;
        public List<Island> Islands;
        /// <summary>The folder of the cache: model.glb (one node per island), view.glb (the model in tiles, for the window) and index.json.</summary>
        public string CacheFolder;
        public string GlbPath => Path.Combine(CacheFolder, "model.glb");
        public string ViewPath => Path.Combine(CacheFolder, "view.glb");
        public string IndexPath => Path.Combine(CacheFolder, "index.json");
        /// <summary>The triangles and the parts of the model.</summary>
        public int Triangles, Parts;
        /// <summary>The name of each part, and its path in the file's tree when the file has one (else null).</summary>
        public List<string> PartNames = new List<string>();
        public List<string[]> PartPaths = new List<string[]>();
        /// <summary>The role the file gives each part (an IFC class does), or null.</summary>
        public List<string> PartRoles = new List<string>();
        /// <summary>True when the cache of an earlier import was used as it was.</summary>
        public bool FromCache;
        public List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Reads a 3D model of the plant (glTF, GLB, OBJ, STL, PLY, 3D DWF, IFC, FBX through the 3D window, or
    /// STEP, IGES, Rhino and FreeCAD through SolidCraft), puts
    /// it in the frame of the plant, splits it into islands and keeps a cache of the result in the
    /// user's data folder: the model file itself is never copied into the flowsheet.
    /// </summary>
    public static class ModelImporter
    {
        /// <summary>The version of what the cache holds; a cache of another version is made again.</summary>
        public const int CacheVersion = 4;

        public static readonly string[] Extensions = { ".glb", ".gltf", ".obj", ".stl", ".ply", ".fbx", ".dwf", ".dwfx", ".ifc", ".step", ".stp", ".igs", ".iges", ".3dm", ".fcstd" };

        /// <summary>The CAD formats SolidCraft reads (STEP, IGES, Rhino and FreeCAD) and writes as GLB for the importer.</summary>
        public static readonly string[] CadExtensions = { ".step", ".stp", ".igs", ".iges", ".3dm", ".fcstd" };

        /// <summary>Where the caches go; tests point it somewhere else.</summary>
        public static string CacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DWSIM", "Plant3DModels");

        /// <summary>Turns an FBX file into a GLB file (in, out); the 3D window does it when this is not set.</summary>
        public static Func<string, string, bool> FbxConverter = ConvertWithWindow;

        /// <summary>Turns a STEP, IGES, Rhino or FreeCAD file into a GLB file (in, out); SolidCraft does it when this is not set.</summary>
        public static Func<string, string, bool> CadConverter = ConvertWithSolidCraft;

        /// <summary>Reads a model file in the frame of the file, by its extension.</summary>
        public static ImportedModel Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The model file was not found.", path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".glb":
                case ".gltf": return GltfReader.Read(path);
                case ".obj": return ObjReader.Read(path);
                case ".stl": return StlReader.Read(path);
                case ".ply": return PlyReader.Read(path);
                case ".dwf":
                case ".dwfx": return DwfReader.Read(path);
                case ".ifc": return IfcReader.Read(path);
                case ".step":
                case ".stp":
                case ".igs":
                case ".iges":
                case ".3dm":
                case ".fcstd":
                    {
                        var glb = Path.Combine(Path.GetTempPath(), "dwsim-cad-" + Guid.NewGuid().ToString("N") + ".glb");
                        try
                        {
                            if (CadConverter == null || !CadConverter(path, glb) || !File.Exists(glb))
                                throw new InvalidOperationException("DWSIM reads STEP, IGES, Rhino and FreeCAD files through SolidCraft, and SolidCraft could not convert this one (or it is not installed). Install SolidCraft and set its folder in the options, or export the model as glTF (GLB), FBX or OBJ.");
                            var model = GltfReader.Read(glb);
                            model.SourcePath = path;
                            model.Format = ext.TrimStart('.');
                            TidyCadPaths(model);
                            return model;
                        }
                        finally { try { if (File.Exists(glb)) File.Delete(glb); } catch { } }
                    }
                case ".fbx":
                    {
                        var glb = Path.Combine(Path.GetTempPath(), "dwsim-fbx-" + Guid.NewGuid().ToString("N") + ".glb");
                        try
                        {
                            if (FbxConverter == null || !FbxConverter(path, glb) || !File.Exists(glb))
                                throw new InvalidOperationException("DWSIM could not convert the FBX file. Open it in the program it came from and export it as glTF (GLB) or OBJ.");
                            var model = GltfReader.Read(glb);
                            model.SourcePath = path;
                            model.Format = "fbx";
                            return model;
                        }
                        finally { try { if (File.Exists(glb)) File.Delete(glb); } catch { } }
                    }
                default:
                    throw new NotSupportedException("DWSIM does not read " + ext + " files. Export the model as glTF (GLB), FBX, OBJ, STL, PLY, IFC, STEP, IGES or 3D DWF.");
            }
        }

        static readonly System.Text.RegularExpressions.Regex OccurrenceSuffix = new System.Text.RegularExpressions.Regex(@":\d+$");

        /// <summary>
        /// SolidCraft names each placement of a component "name:1" and puts the design and the
        /// imported file at the top of the tree: the paths start at the file and keep the names of its
        /// own products, and a product with a tag (V-101) inside an area becomes the top of its branch.
        /// </summary>
        internal static void TidyCadPaths(ImportedModel model)
        {
            var paths = model.Parts.Select(p =>
            {
                var segments = (p.Path ?? (p.Name ?? "").Split('/')).Select(x => OccurrenceSuffix.Replace(x, "")).ToList();
                // the design goes; the component named after the file is the root, as a DWF's model is
                if (segments.Count > 2) segments.RemoveAt(0);
                return segments;
            }).ToList();
            // the file's own top assembly, when every part is in it, is the root too
            while (paths.Count > 0 && paths.All(s => s.Count > 3) && paths.All(s => s[1] == paths[0][1]))
                foreach (var s in paths) s.RemoveAt(1);
            for (var i = 0; i < model.Parts.Count; i++)
            {
                var segments = paths[i];
                // an area or a unit that holds tagged products: the tagged product is the group
                if (segments.Count > 3 && ModelSegmentation.TagOf(segments[1]) == null)
                {
                    var tagged = -1;
                    for (var k = 2; k < segments.Count - 1 && tagged < 0; k++)
                        if (ModelSegmentation.TagOf(segments[k]) != null) tagged = k;
                    if (tagged > 1) segments.RemoveRange(1, tagged - 1);
                }
                model.Parts[i].Path = segments.ToArray();
                model.Parts[i].Name = string.Join("/", segments);
            }
        }

        public static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var f = File.OpenRead(path))
                return string.Concat(sha.ComputeHash(f).Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// The reference to a model file that a first import starts from: its hash, and the unit and
        /// up axis guessed from the file.
        /// </summary>
        public static ModelReference Reference(string path, ImportedModel model, string flowsheetPath = null)
        {
            var info = new FileInfo(path);
            return new ModelReference
            {
                Path = info.FullName,
                RelativePath = Relative(flowsheetPath, info.FullName),
                Sha256 = Hash(path),
                Size = info.Length,
                Modified = info.LastWriteTimeUtc,
                Format = model.Format,
                Place = new Placement3D { Scale = ModelGeometry.GuessScale(model), Up = ModelGeometry.GuessUp(model) }
            };
        }

        /// <summary>
        /// Imports a plant model: reads it, normalizes it with the placement of the reference (or the
        /// guessed one), splits it into islands and writes the cache. When the cache of the same file,
        /// placement and tolerance exists, it is read back and the model file is not read again. A
        /// first import (without a reference) also centres the model on the plant: the middle of its
        /// plan at the origin and its lowest point at the ground.
        /// </summary>
        public static ModelImportResult Import(string path, ModelReference reference = null, string flowsheetPath = null)
        {
            var sha = Hash(path);
            ImportedModel model = null;
            var first = reference == null;
            var normalized = false;
            if (reference == null || reference.Sha256 != sha)
            {
                model = Read(path);
                var keep = reference?.Place;
                var tol = reference?.WeldTolerance ?? 1e-4;
                reference = Reference(path, model, flowsheetPath);
                if (keep != null) reference.Place = keep;
                reference.WeldTolerance = tol;
            }
            if (first)
            {
                reference.Place.Offset = new double[3];
                ModelGeometry.Normalize(model, reference.Place);
                normalized = true;
                double[] min, max;
                if (model.Bounds(out min, out max))
                {
                    var off = new[] { -Math.Round((min[0] + max[0]) / 2, 3), -Math.Round((min[1] + max[1]) / 2, 3), -Math.Round(min[2], 3) };
                    foreach (var part in model.Parts)
                        for (var i = 0; i + 2 < part.Positions.Length; i += 3)
                        {
                            part.Positions[i] += (float)off[0];
                            part.Positions[i + 1] += (float)off[1];
                            part.Positions[i + 2] += (float)off[2];
                        }
                    reference.Place.Offset = off;
                }
            }
            var result = new ModelImportResult { Reference = reference, CacheFolder = CacheFolder(reference) };
            if (ReadIndex(result) && File.Exists(result.GlbPath) && File.Exists(result.ViewPath))
            {
                result.FromCache = true;
                return result;
            }
            if (model == null) model = Read(path);
            if (!normalized) ModelGeometry.Normalize(model, reference.Place);
            var islands = ModelGeometry.Islands(model, reference.WeldTolerance);
            var shapes = IslandShapes.Classify(model, islands);
            for (var i = 0; i < islands.Count; i++) islands[i].Shape = shapes[i];
            Directory.CreateDirectory(result.CacheFolder);
            GlbWriter.WriteIslands(model, islands, result.GlbPath);
            GlbWriter.WriteTiles(model, result.ViewPath);
            WriteIndex(result.IndexPath, reference, model, islands);
            result.Model = model;
            result.Islands = islands;
            result.Triangles = model.TriangleCount;
            result.Parts = model.Parts.Count;
            result.PartNames = model.Parts.Select(x => x.Name).ToList();
            result.PartPaths = model.Parts.Select(x => x.Path).ToList();
            result.PartRoles = model.Parts.Select(x => x.Role).ToList();
            result.Warnings.AddRange(model.Warnings);
            return result;
        }

        /// <summary>The view file of a reference's cache, which the window draws; it may not exist yet.</summary>
        public static string ViewPathOf(ModelReference r) => Path.Combine(CacheFolder(r), "view.glb");

        /// <summary>The file with one node per island ("island_N"), which the window's review draws.</summary>
        public static string IslandsPathOf(ModelReference r) => Path.Combine(CacheFolder(r), "model.glb");

        /// <summary>The cache of a file and its placement: a change of unit, axis or tolerance makes another one.</summary>
        public static string CacheFolder(ModelReference r)
        {
            var p = r.Place ?? new Placement3D();
            var key = string.Format(CultureInfo.InvariantCulture, "{0}|{1:R}|{2}|{3:R}|{4:R},{5:R},{6:R}|{7:R}|{8}",
                r.Sha256, p.Scale, p.Up, p.Rotation, p.Offset[0], p.Offset[1], p.Offset[2], r.WeldTolerance, CacheVersion);
            string h;
            using (var sha = SHA256.Create())
                h = string.Concat(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key)).Take(8).Select(b => b.ToString("x2")));
            return Path.Combine(CacheRoot, r.Sha256.Substring(0, Math.Min(16, r.Sha256.Length)) + "-" + h);
        }

        static void WriteIndex(string path, ModelReference r, ImportedModel model, List<Island> islands)
        {
            double[] min, max;
            model.Bounds(out min, out max);
            var o = new JObject
            {
                ["v"] = CacheVersion,
                ["sha"] = r.Sha256,
                ["format"] = model.Format,
                ["triangles"] = model.TriangleCount,
                ["min"] = new JArray(min.Select(R)),
                ["max"] = new JArray(max.Select(R)),
                ["parts"] = new JArray(model.Parts.Select(p => p.Name)),
                ["paths"] = new JArray(model.Parts.Select(p => p.Path == null ? (JToken)JValue.CreateNull() : new JArray(p.Path))),
                ["roles"] = new JArray(model.Parts.Select(p => p.Role)),
                ["islands"] = new JArray(islands.Select(i =>
                {
                    var j = new JObject
                    {
                        ["i"] = i.Index,
                        ["part"] = i.Part,
                        ["tris"] = i.Triangles.Length,
                        ["area"] = R(i.Area),
                        ["min"] = new JArray(i.Min.Select(R)),
                        ["max"] = new JArray(i.Max.Select(R)),
                        ["c"] = new JArray(i.Centroid.Select(R))
                    };
                    if (i.Shape != null)
                    {
                        j["k"] = (int)i.Shape.Class;
                        j["ax"] = new JArray(i.Shape.Axis.Select(x => Math.Round(x, 4)));
                        j["len"] = R(i.Shape.Length);
                        j["r"] = R(i.Shape.Radius);
                        j["ext"] = new JArray(i.Shape.Extents.Select(R));
                    }
                    return j;
                }))
            };
            File.WriteAllText(path, o.ToString(Formatting.None));
        }

        static double R(double d) => Math.Round(d, 5);

        /// <summary>Fills a result from its cache index (islands without their triangles, with their shapes; the parts); false when it does not fit the reference.</summary>
        static bool ReadIndex(ModelImportResult result)
        {
            var path = result.IndexPath;
            var r = result.Reference;
            if (!File.Exists(path)) return false;
            try
            {
                var o = JObject.Parse(File.ReadAllText(path));
                if ((int?)o["v"] != CacheVersion || (string)o["sha"] != r.Sha256) return false;
                result.Triangles = (int?)o["triangles"] ?? 0;
                result.PartNames = (o["parts"] as JArray ?? new JArray()).Select(t => (string)t).ToList();
                result.Parts = result.PartNames.Count;
                result.PartPaths = (o["paths"] as JArray ?? new JArray()).Select(t => t.Type == JTokenType.Array ? t.Select(x => (string)x).ToArray() : null).ToList();
                result.PartRoles = (o["roles"] as JArray ?? new JArray()).Select(t => (string)t).ToList();
                result.Islands = ((JArray)o["islands"]).Select(t => new Island
                {
                    Index = (int)t["i"],
                    Part = (int)t["part"],
                    Triangles = new int[0],
                    Area = (double)t["area"],
                    Min = t["min"].Select(x => (double)x).ToArray(),
                    Max = t["max"].Select(x => (double)x).ToArray(),
                    Centroid = t["c"].Select(x => (double)x).ToArray(),
                    Shape = t["k"] == null ? null : new IslandShape
                    {
                        Class = (ShapeClass)(int)t["k"],
                        Axis = t["ax"].Select(x => (double)x).ToArray(),
                        Length = (double)t["len"],
                        Radius = (double)t["r"],
                        Extents = t["ext"].Select(x => (double)x).ToArray()
                    }
                }).ToList();
                return true;
            }
            catch { return false; }
        }

        /// <summary>The cached index of a reference (islands with shapes, parts and paths), or null when the cache is not there.</summary>
        public static ModelImportResult Cached(ModelReference r)
        {
            var result = new ModelImportResult { Reference = r, CacheFolder = CacheFolder(r) };
            return ReadIndex(result) ? result : null;
        }

        static string Relative(string flowsheetPath, string target)
        {
            if (string.IsNullOrWhiteSpace(flowsheetPath)) return null;
            try
            {
                var from = new Uri(Path.GetDirectoryName(Path.GetFullPath(flowsheetPath)) + Path.DirectorySeparatorChar);
                var to = new Uri(target);
                if (from.Scheme != to.Scheme) return null;
                return Uri.UnescapeDataString(from.MakeRelativeUri(to).ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch { return null; }
        }

        /// <summary>
        /// The model file of a reference: the absolute path, else the relative one next to the
        /// flowsheet (a project folder that was moved), else null.
        /// </summary>
        public static string Locate(ModelReference r, string flowsheetPath)
        {
            if (r == null) return null;
            if (!string.IsNullOrEmpty(r.Path) && File.Exists(r.Path)) return r.Path;
            if (!string.IsNullOrEmpty(r.RelativePath) && !string.IsNullOrWhiteSpace(flowsheetPath))
            {
                var p = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(flowsheetPath)) ?? "", r.RelativePath));
                if (File.Exists(p)) return p;
            }
            return null;
        }

        /// <summary>
        /// Converts an FBX file with the 3D window in headless mode (Godot reads FBX at run time and
        /// writes glTF). False when this installation has no window or the conversion fails.
        /// </summary>
        public static bool ConvertWithSolidCraft(string cad, string glb)
        {
            var folder = SolidCraftRunner.FindInstallFolder();
            if (folder == null) return false;
            var cli = Path.Combine(folder, Environment.OSVersion.Platform == PlatformID.Win32NT ? "solidcraft-cli.exe" : "solidcraft-cli");
            var script = Path.Combine(Path.GetTempPath(), "dwsim-cad-" + Guid.NewGuid().ToString("N") + ".json");
            var commands = new JObject
            {
                ["commands"] = new JArray
                {
                    new JObject { ["command"] = "file.insert_step", ["params"] = new JObject { ["path"] = Path.GetFullPath(cad), ["link"] = false } },
                    new JObject { ["command"] = "file.export", ["params"] = new JObject { ["path"] = Path.GetFullPath(glb) } }
                }
            };
            File.WriteAllText(script, commands.ToString());
            try
            {
                var info = new ProcessStartInfo(cli, "run \"" + script + "\" --quiet")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetTempPath()
                };
                using (var p = Process.Start(info))
                {
                    if (p == null) return false;
                    p.OutputDataReceived += (s, e) => { };
                    p.ErrorDataReceived += (s, e) => { };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(900000)) { try { p.Kill(); } catch { } return false; }
                    return p.ExitCode == 0 && File.Exists(glb);
                }
            }
            finally { try { File.Delete(script); } catch { } }
        }

        public static bool ConvertWithWindow(string fbx, string glb)
        {
            string exe, prefix;
            if (!ViewerLauncher.Resolve(out exe, out prefix)) return false;
            var args = prefix + "--headless --audio-driver Dummy -- \"--convert-model=" + Path.GetFullPath(fbx) + "\" \"--convert-out=" + Path.GetFullPath(glb) + "\"";
            var info = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(exe)) ?? ""
            };
            using (var p = Process.Start(info))
            {
                if (p == null) return false;
                p.OutputDataReceived += (s, e) => { };
                p.ErrorDataReceived += (s, e) => { };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(180000)) { try { p.Kill(); } catch { } return false; }
                return p.ExitCode == 0 && File.Exists(glb);
            }
        }
    }

    /// <summary>
    /// The model file a flowsheet uses and how it is put in the plant. The flowsheet keeps it (key
    /// Plant3DModel, see <see cref="ModelStore"/>) and never the model itself.
    /// </summary>
    public class ModelReference
    {
        [JsonProperty("path")] public string Path;
        /// <summary>The path from the flowsheet's folder, for a project folder that is moved.</summary>
        [JsonProperty("rel", NullValueHandling = NullValueHandling.Ignore)] public string RelativePath;
        [JsonProperty("sha")] public string Sha256;
        [JsonProperty("size")] public long Size;
        [JsonProperty("modified")] public DateTime Modified;
        [JsonProperty("format")] public string Format;
        [JsonProperty("place")] public Placement3D Place = new Placement3D();
        /// <summary>The distance below which two vertices are one, metres.</summary>
        [JsonProperty("tol")] public double WeldTolerance = 1e-4;
    }

    /// <summary>A group of islands that stands for one thing: an equipment, a pipe, a structure or scenery.</summary>
    public class ModelGroup
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("islands")] public List<int> Islands = new List<int>();
        /// <summary>The name the model gives the group (a node of its tree), and the tag read from it.</summary>
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)] public string Name;
        [JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)] public string Tag;
        /// <summary>The kind of equipment the segmentation found (column, vessel, pump...).</summary>
        [JsonProperty("class", NullValueHandling = NullValueHandling.Ignore)] public string Class;
        /// <summary>The flowsheet object (GraphicObject.Name) the user said the group stands for, or null.</summary>
        [JsonProperty("object", NullValueHandling = NullValueHandling.Ignore)] public string Object;
        /// <summary>The object the matching proposes, how sure it is (0 to 1) and why.</summary>
        [JsonProperty("suggested", NullValueHandling = NullValueHandling.Ignore)] public string Suggested;
        [JsonProperty("score", NullValueHandling = NullValueHandling.Ignore)] public double? Score;
        [JsonProperty("why", NullValueHandling = NullValueHandling.Ignore)] public string Why;
        /// <summary>A group the user made (joined, split or given a role): its islands leave the groups the segmentation finds.</summary>
        [JsonProperty("user", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool User;
        /// <summary>
        /// More flowsheet objects the group stands for, besides <see cref="Object"/>: a flowsheet may
        /// draw one piece of equipment as several objects (the two sides of an exchanger).
        /// </summary>
        [JsonProperty("also", NullValueHandling = NullValueHandling.Ignore)] public List<string> Also;
        /// <summary>More objects the matching proposes with <see cref="Suggested"/>.</summary>
        [JsonProperty("suggestedAlso", NullValueHandling = NullValueHandling.Ignore)] public List<string> SuggestedAlso;
        /// <summary>The box round the group in the plant (metres, Z up), where its objects are placed.</summary>
        [JsonProperty("min", NullValueHandling = NullValueHandling.Ignore)] public double[] Min;
        [JsonProperty("max", NullValueHandling = NullValueHandling.Ignore)] public double[] Max;

        /// <summary>Every flowsheet object the group stands for.</summary>
        public IEnumerable<string> Objects()
        {
            if (Object != null) yield return Object;
            if (Also != null) foreach (var o in Also) if (o != null && o != Object) yield return o;
        }
        /// <summary>equipment, piping, structure, scenery or hidden.</summary>
        [JsonProperty("role")] public string Role = "scenery";
    }

    /// <summary>The plant model of a flowsheet: the file and the groups found in it.</summary>
    public class PlantModelImport
    {
        [JsonProperty("v")] public int Version = 1;
        [JsonProperty("model")] public ModelReference Model;
        [JsonProperty("groups")] public List<ModelGroup> Groups = new List<ModelGroup>();
    }

    /// <summary>Keeps the plant model of a flowsheet in its extra properties (key Plant3DModel).</summary>
    public static class ModelStore
    {
        public const string Key = "Plant3DModel";

        static IDictionary<string, object> Props(DWSIM.Interfaces.IFlowsheet fs) => fs?.ExtraProperties as IDictionary<string, object>;

        /// <summary>The plant model of the flowsheet, or null when it has none or its text does not read.</summary>
        public static PlantModelImport Load(DWSIM.Interfaces.IFlowsheet fs)
        {
            var props = Props(fs);
            object value;
            if (props == null || !props.TryGetValue(Key, out value) || value == null) return null;
            try
            {
                var m = JsonConvert.DeserializeObject<PlantModelImport>(value.ToString());
                if (m?.Model == null) return null;
                if (m.Groups == null) m.Groups = new List<ModelGroup>();
                if (m.Model.Place == null) m.Model.Place = new Placement3D();
                if (m.Model.Place.Offset == null || m.Model.Place.Offset.Length != 3) m.Model.Place.Offset = new double[3];
                return m;
            }
            catch { return null; }
        }

        /// <summary>The key's text as it is kept, or null: what the undo of the 3D window keeps.</summary>
        public static string Text(DWSIM.Interfaces.IFlowsheet fs)
        {
            var props = Props(fs);
            object value;
            return props != null && props.TryGetValue(Key, out value) && value != null ? value.ToString() : null;
        }

        public static void SetText(DWSIM.Interfaces.IFlowsheet fs, string text)
        {
            var props = Props(fs);
            if (props == null) return;
            if (text == null) props.Remove(Key);
            else props[Key] = text;
        }

        /// <summary>Keeps the plant model, or takes it away when it is null.</summary>
        public static void Save(DWSIM.Interfaces.IFlowsheet fs, PlantModelImport model)
        {
            var props = Props(fs);
            if (props == null) return;
            if (model?.Model == null) props.Remove(Key);
            else props[Key] = JsonConvert.SerializeObject(model, new Newtonsoft.Json.Converters.StringEnumConverter());
        }
    }
}
