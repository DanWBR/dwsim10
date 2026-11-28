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

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// Reads a Wavefront OBJ file and its MTL materials: one part for each object, group and
    /// material in the order the file gives them, with the diffuse colour, the diffuse texture and
    /// the transparency of each material. The unit and the up axis are not in the file.
    /// </summary>
    public static class ObjReader
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static ImportedModel Read(string path)
        {
            var model = new ImportedModel { SourcePath = path, Format = "obj" };
            var folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
            var v = new List<double>();
            var vt = new List<double>();
            var materials = new Dictionary<string, int>(StringComparer.Ordinal);
            string obj = null, group = null, mtl = null;
            PartBuilder part = null;
            Dictionary<long, int> corners = null;
            var corner = new List<int>();

            Action flush = () =>
            {
                if (part != null && !part.Empty)
                {
                    var name = obj ?? group ?? Path.GetFileNameWithoutExtension(path);
                    if (obj != null && group != null && group != obj) name = obj + "/" + group;
                    int m;
                    model.Parts.Add(part.Build(name, mtl != null && materials.TryGetValue(mtl, out m) ? m : -1));
                }
                part = null;
                corners = null;
            };

            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var t = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                switch (t[0])
                {
                    case "v":
                        v.Add(D(t, 1)); v.Add(D(t, 2)); v.Add(D(t, 3));
                        break;
                    case "vt":
                        vt.Add(D(t, 1)); vt.Add(t.Length > 2 ? D(t, 2) : 0);
                        break;
                    case "o":
                        flush(); obj = Rest(line); group = null;
                        break;
                    case "g":
                        flush(); group = Rest(line);
                        break;
                    case "usemtl":
                        flush(); mtl = Rest(line);
                        break;
                    case "mtllib":
                        foreach (var file in MtlFiles(Rest(line), folder))
                            ReadMtl(file, model, materials);
                        break;
                    case "f":
                        if (part == null) { part = new PartBuilder(); corners = new Dictionary<long, int>(); }
                        corner.Clear();
                        for (var i = 1; i < t.Length; i++)
                        {
                            var refs = t[i].Split('/');
                            int vi, ti = -1;
                            if (!int.TryParse(refs[0], NumberStyles.Integer, Inv, out vi)) continue;
                            vi = vi < 0 ? v.Count / 3 + vi : vi - 1;
                            if (refs.Length > 1 && refs[1].Length > 0 && int.TryParse(refs[1], NumberStyles.Integer, Inv, out ti))
                                ti = ti < 0 ? vt.Count / 2 + ti : ti - 1;
                            else ti = -1;
                            if (vi < 0 || vi >= v.Count / 3) continue;
                            if (ti >= vt.Count / 2) ti = -1;
                            var key = ((long)vi << 32) | (uint)(ti + 1);
                            int index;
                            if (!corners.TryGetValue(key, out index))
                            {
                                index = ti >= 0
                                    ? part.Add(v[3 * vi], v[3 * vi + 1], v[3 * vi + 2], vt[2 * ti], 1 - vt[2 * ti + 1])
                                    : part.Add(v[3 * vi], v[3 * vi + 1], v[3 * vi + 2]);
                                corners[key] = index;
                            }
                            corner.Add(index);
                        }
                        part.Polygon(corner);
                        break;
                }
            }
            flush();
            return model;
        }

        static double D(string[] t, int i)
        {
            double d;
            return i < t.Length && double.TryParse(t[i], NumberStyles.Float, Inv, out d) ? d : 0;
        }

        static string Rest(string line)
        {
            var i = line.IndexOfAny(new[] { ' ', '\t' });
            return i < 0 ? "" : line.Substring(i + 1).Trim();
        }

        /// <summary>The MTL files of an mtllib line: one name with spaces when that file exists, otherwise each word.</summary>
        static IEnumerable<string> MtlFiles(string rest, string folder)
        {
            var whole = Path.Combine(folder, rest);
            if (File.Exists(whole)) { yield return whole; yield break; }
            foreach (var w in rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = Path.Combine(folder, w);
                if (File.Exists(f)) yield return f;
            }
        }

        static void ReadMtl(string file, ImportedModel model, Dictionary<string, int> materials)
        {
            var folder = Path.GetDirectoryName(file) ?? "";
            ModelMaterial m = null;
            foreach (var raw in File.ReadLines(file))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var t = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var key = t[0].ToLowerInvariant();
                if (key == "newmtl")
                {
                    m = new ModelMaterial { Name = Rest(line) };
                    materials[m.Name] = model.AddMaterial(m);
                }
                else if (m == null) continue;
                else if (key == "kd" && t.Length >= 4)
                    m.Color = new[] { (float)D(t, 1), (float)D(t, 2), (float)D(t, 3), m.Color[3] };
                else if (key == "d")
                    SetAlpha(m, (float)D(t, 1));
                else if (key == "tr")
                    SetAlpha(m, 1f - (float)D(t, 1));
                else if (key == "ns")
                    m.Roughness = (float)Math.Max(0.05, Math.Min(1.0, 1.0 - Math.Sqrt(Math.Max(0, D(t, 1)) / 1000.0)));
                else if (key == "map_kd")
                    LoadTexture(m, Map(line, folder), model);
                else if (key == "map_d")
                {
                    m.AlphaMode = "MASK";
                    m.DoubleSided = true;
                    if (m.Texture == null) LoadTexture(m, Map(line, folder), model);
                }
            }
        }

        static void SetAlpha(ModelMaterial m, float a)
        {
            a = Math.Max(0f, Math.Min(1f, a));
            m.Color[3] = a;
            if (a < 0.999f && m.AlphaMode == "OPAQUE") m.AlphaMode = "BLEND";
        }

        /// <summary>The file a map statement names: what is left after its options, or its last word.</summary>
        static string Map(string line, string folder)
        {
            var rest = Rest(line);
            var words = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var i = 0;
            while (i < words.Length && words[i].StartsWith("-"))
            {
                var opt = words[i].ToLowerInvariant();
                var args = opt == "-o" || opt == "-s" || opt == "-t" ? 3 : opt == "-mm" ? 2 : 1;
                i += 1 + args;
            }
            if (i < words.Length)
            {
                var name = string.Join(" ", words, i, words.Length - i);
                var full = Path.Combine(folder, name);
                if (File.Exists(full)) return full;
            }
            return words.Length > 0 ? Path.Combine(folder, words[words.Length - 1]) : null;
        }

        static void LoadTexture(ModelMaterial m, string file, ImportedModel model)
        {
            if (file == null) return;
            if (!File.Exists(file))
            {
                model.Warnings.Add("The texture " + Path.GetFileName(file) + " was not found.");
                return;
            }
            var data = File.ReadAllBytes(file);
            var mime = GltfReader.ImageMime(data);
            if (mime == null)
            {
                model.Warnings.Add("The texture " + Path.GetFileName(file) + " is not PNG or JPEG and was left out.");
                return;
            }
            m.Texture = data;
            m.TextureMime = mime;
            // a texture multiplies the colour, and an OBJ diffuse colour is often grey or black when a texture is set
            m.Color = new[] { 1f, 1f, 1f, m.Color[3] };
        }
    }
}
