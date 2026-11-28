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
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// Writes a normalized model (metres, Z up) as a GLB file the 3D window loads at run time:
    /// positions, normals, texture coordinates, the materials with their base colour textures. The
    /// file has Y up, as glTF and Godot have it: the plant's (x, y, z) is written as (x, z, -y).
    /// </summary>
    public static class GlbWriter
    {
        /// <summary>One node per island, named "island_N", with the island number and its part's name in its extras.</summary>
        public static void WriteIslands(ImportedModel model, IList<Island> islands, string path)
        {
            var w = new Writer(model);
            foreach (var island in islands)
            {
                var part = model.Parts[island.Part];
                var mesh = w.Mesh("island_" + island.Index, new[] { w.Primitive(part, island.Triangles, part.Material) });
                w.Node("island_" + island.Index, mesh, new JObject { ["island"] = island.Index, ["part"] = part.Name });
            }
            w.Save(path);
        }

        /// <summary>
        /// The whole model as one node and one mesh, with one primitive per material: the way a
        /// prop of the site is drawn, with as few draw calls as its materials allow.
        /// </summary>
        public static void WriteProp(ImportedModel model, string name, string path)
        {
            var w = new Writer(model);
            var prims = new List<JObject>();
            foreach (var group in model.Parts.Select((p, i) => i).GroupBy(i => model.Parts[i].Material))
            {
                var merged = Merge(model, group.ToList());
                prims.Add(w.Primitive(merged, Enumerable.Range(0, merged.TriangleCount).ToArray(), group.Key));
            }
            w.Node(name, w.Mesh(name, prims), null);
            w.Save(path);
        }

        static MeshPart Merge(ImportedModel model, List<int> parts)
        {
            if (parts.Count == 1) return model.Parts[parts[0]];
            var anyUv = parts.Any(i => model.Parts[i].UV != null);
            var pos = new List<float>();
            var uv = anyUv ? new List<float>() : null;
            var idx = new List<int>();
            foreach (var i in parts)
            {
                var p = model.Parts[i];
                var at = pos.Count / 3;
                pos.AddRange(p.Positions);
                if (uv != null) { if (p.UV != null) uv.AddRange(p.UV); else uv.AddRange(new float[p.VertexCount * 2]); }
                foreach (var k in p.Indices) idx.Add(k + at);
            }
            return new MeshPart { Positions = pos.ToArray(), UV = uv?.ToArray(), Indices = idx.ToArray() };
        }

        class Writer
        {
            readonly ImportedModel _model;
            readonly MemoryStream _bin = new MemoryStream();
            readonly JArray _views = new JArray(), _accessors = new JArray(), _meshes = new JArray(), _nodes = new JArray();
            readonly JArray _materials = new JArray(), _textures = new JArray(), _images = new JArray();
            readonly Dictionary<int, int> _materialMap = new Dictionary<int, int>();

            public Writer(ImportedModel model) { _model = model; }

            public int Mesh(string name, IEnumerable<JObject> primitives)
            {
                _meshes.Add(new JObject { ["name"] = name, ["primitives"] = new JArray(primitives) });
                return _meshes.Count - 1;
            }

            public void Node(string name, int mesh, JObject extras)
            {
                var n = new JObject { ["name"] = name, ["mesh"] = mesh };
                if (extras != null) n["extras"] = extras;
                _nodes.Add(n);
            }

            /// <summary>A primitive of some triangles of a part, with its vertices numbered again from 0.</summary>
            public JObject Primitive(MeshPart part, int[] triangles, int material)
            {
                var remap = new Dictionary<int, int>();
                var order = new List<int>();
                var idx = new int[triangles.Length * 3];
                for (var t = 0; t < triangles.Length; t++)
                    for (var k = 0; k < 3; k++)
                    {
                        var src = part.Indices[3 * triangles[t] + k];
                        int dst;
                        if (!remap.TryGetValue(src, out dst)) { dst = order.Count; remap[src] = dst; order.Add(src); }
                        idx[3 * t + k] = dst;
                    }
                var n = order.Count;
                var pos = new float[n * 3];
                for (var i = 0; i < n; i++)
                {
                    var s = order[i] * 3;
                    pos[3 * i] = part.Positions[s];
                    pos[3 * i + 1] = part.Positions[s + 2];
                    pos[3 * i + 2] = -part.Positions[s + 1];
                }
                var nor = Normals(pos, idx);
                float[] uv = null;
                if (part.UV != null)
                {
                    uv = new float[n * 2];
                    for (var i = 0; i < n; i++) { uv[2 * i] = part.UV[2 * order[i]]; uv[2 * i + 1] = part.UV[2 * order[i] + 1]; }
                }

                var attrs = new JObject
                {
                    ["POSITION"] = Floats(pos, 3, true),
                    ["NORMAL"] = Floats(nor, 3, false)
                };
                if (uv != null) attrs["TEXCOORD_0"] = Floats(uv, 2, false);
                var prim = new JObject { ["attributes"] = attrs, ["indices"] = Indices(idx, n), ["mode"] = 4 };
                prim["material"] = Material(material);
                return prim;
            }

            static float[] Normals(float[] pos, int[] idx)
            {
                var nor = new float[pos.Length];
                for (var t = 0; t + 2 < idx.Length; t += 3)
                {
                    int a = idx[t] * 3, b = idx[t + 1] * 3, c = idx[t + 2] * 3;
                    float ux = pos[b] - pos[a], uy = pos[b + 1] - pos[a + 1], uz = pos[b + 2] - pos[a + 2];
                    float wx = pos[c] - pos[a], wy = pos[c + 1] - pos[a + 1], wz = pos[c + 2] - pos[a + 2];
                    float nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
                    foreach (var v in new[] { a, b, c }) { nor[v] += nx; nor[v + 1] += ny; nor[v + 2] += nz; }
                }
                for (var i = 0; i + 2 < nor.Length; i += 3)
                {
                    var l = (float)Math.Sqrt(nor[i] * nor[i] + nor[i + 1] * nor[i + 1] + nor[i + 2] * nor[i + 2]);
                    if (l > 1e-20f) { nor[i] /= l; nor[i + 1] /= l; nor[i + 2] /= l; }
                    else { nor[i] = 0; nor[i + 1] = 1; nor[i + 2] = 0; }
                }
                return nor;
            }

            int Material(int index)
            {
                int found;
                if (_materialMap.TryGetValue(index, out found)) return found;
                var m = index >= 0 && index < _model.Materials.Count ? _model.Materials[index] : new ModelMaterial { Name = "default" };
                var pbr = new JObject
                {
                    ["baseColorFactor"] = new JArray(m.Color.Select(c => (double)Math.Round(c, 5))),
                    ["metallicFactor"] = Math.Round(m.Metallic, 4),
                    ["roughnessFactor"] = Math.Round(m.Roughness, 4)
                };
                if (m.Texture != null)
                {
                    var view = View(m.Texture, null);
                    _images.Add(new JObject { ["bufferView"] = view, ["mimeType"] = m.TextureMime ?? "image/png" });
                    _textures.Add(new JObject { ["source"] = _images.Count - 1, ["sampler"] = 0 });
                    pbr["baseColorTexture"] = new JObject { ["index"] = _textures.Count - 1 };
                }
                var mat = new JObject { ["name"] = m.Name ?? ("material" + _materials.Count), ["pbrMetallicRoughness"] = pbr };
                if (m.AlphaMode == "MASK" || m.AlphaMode == "BLEND") mat["alphaMode"] = m.AlphaMode;
                if (m.AlphaMode == "MASK") mat["alphaCutoff"] = m.AlphaCutoff;
                if (m.DoubleSided) mat["doubleSided"] = true;
                _materials.Add(mat);
                found = _materials.Count - 1;
                _materialMap[index] = found;
                return found;
            }

            int View(byte[] data, int? target)
            {
                Pad();
                var at = (int)_bin.Position;
                _bin.Write(data, 0, data.Length);
                var v = new JObject { ["buffer"] = 0, ["byteOffset"] = at, ["byteLength"] = data.Length };
                if (target.HasValue) v["target"] = target.Value;
                _views.Add(v);
                return _views.Count - 1;
            }

            void Pad()
            {
                while (_bin.Position % 4 != 0) _bin.WriteByte(0);
            }

            int Floats(float[] data, int width, bool bounds)
            {
                var bytes = new byte[data.Length * 4];
                Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
                var view = View(bytes, 34962);
                var a = new JObject
                {
                    ["bufferView"] = view,
                    ["componentType"] = 5126,
                    ["count"] = data.Length / width,
                    ["type"] = width == 3 ? "VEC3" : "VEC2"
                };
                if (bounds)
                {
                    var min = new double[width];
                    var max = new double[width];
                    for (var k = 0; k < width; k++) { min[k] = double.MaxValue; max[k] = double.MinValue; }
                    for (var i = 0; i < data.Length; i++)
                    {
                        var k = i % width;
                        if (data[i] < min[k]) min[k] = data[i];
                        if (data[i] > max[k]) max[k] = data[i];
                    }
                    a["min"] = new JArray(min);
                    a["max"] = new JArray(max);
                }
                _accessors.Add(a);
                return _accessors.Count - 1;
            }

            int Indices(int[] idx, int vertices)
            {
                byte[] bytes;
                int type;
                if (vertices < 65536)
                {
                    var s = new ushort[idx.Length];
                    for (var i = 0; i < idx.Length; i++) s[i] = (ushort)idx[i];
                    bytes = new byte[s.Length * 2];
                    Buffer.BlockCopy(s, 0, bytes, 0, bytes.Length);
                    type = 5123;
                }
                else
                {
                    bytes = new byte[idx.Length * 4];
                    Buffer.BlockCopy(idx, 0, bytes, 0, bytes.Length);
                    type = 5125;
                }
                var view = View(bytes, 34963);
                _accessors.Add(new JObject { ["bufferView"] = view, ["componentType"] = type, ["count"] = idx.Length, ["type"] = "SCALAR" });
                return _accessors.Count - 1;
            }

            public void Save(string path)
            {
                Pad();
                var doc = new JObject
                {
                    ["asset"] = new JObject { ["version"] = "2.0", ["generator"] = "DWSIM model import" },
                    ["scene"] = 0,
                    ["scenes"] = new JArray(new JObject { ["nodes"] = new JArray(Enumerable.Range(0, _nodes.Count)) }),
                    ["nodes"] = _nodes,
                    ["meshes"] = _meshes,
                    ["materials"] = _materials,
                    ["accessors"] = _accessors,
                    ["bufferViews"] = _views,
                    ["buffers"] = new JArray(new JObject { ["byteLength"] = _bin.Length })
                };
                if (_images.Count > 0)
                {
                    doc["images"] = _images;
                    doc["textures"] = _textures;
                    doc["samplers"] = new JArray(new JObject { ["magFilter"] = 9729, ["minFilter"] = 9987, ["wrapS"] = 10497, ["wrapT"] = 10497 });
                }
                var json = Encoding.UTF8.GetBytes(doc.ToString(Formatting.None));
                var jsonPad = (4 - json.Length % 4) % 4;
                var bin = _bin.ToArray();
                var total = 12 + 8 + json.Length + jsonPad + 8 + bin.Length;
                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                using (var f = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (var o = new BinaryWriter(f))
                {
                    o.Write(0x46546C67u); o.Write(2u); o.Write((uint)total);
                    o.Write((uint)(json.Length + jsonPad)); o.Write(0x4E4F534Au);
                    o.Write(json);
                    for (var i = 0; i < jsonPad; i++) o.Write((byte)0x20);
                    o.Write((uint)bin.Length); o.Write(0x004E4942u);
                    o.Write(bin);
                }
            }
        }
    }
}
