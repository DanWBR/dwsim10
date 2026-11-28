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
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// Reads a glTF 2.0 file (.gltf with its buffers, or .glb): every triangle mesh of the scene with
    /// the node transforms applied, the node names as paths, and the base colour, its texture and the
    /// transparency of each material. glTF is in metres with Y up.
    /// </summary>
    public static class GltfReader
    {
        const uint Magic = 0x46546C67, JsonChunk = 0x4E4F534A, BinChunk = 0x004E4942;

        public static ImportedModel Read(string path)
        {
            return Read(File.ReadAllBytes(path), Path.GetDirectoryName(Path.GetFullPath(path)), path);
        }

        public static ImportedModel Read(byte[] bytes, string folder, string sourcePath = null)
        {
            JObject doc;
            byte[] bin = null;
            if (bytes.Length >= 12 && BitConverter.ToUInt32(bytes, 0) == Magic)
            {
                var length = (int)Math.Min(BitConverter.ToUInt32(bytes, 8), (uint)bytes.Length);
                var at = 12;
                doc = null;
                while (at + 8 <= length)
                {
                    var size = (int)BitConverter.ToUInt32(bytes, at);
                    var type = BitConverter.ToUInt32(bytes, at + 4);
                    if (at + 8 + size > length) throw new InvalidDataException("The GLB file is cut short.");
                    if (type == JsonChunk) doc = JObject.Parse(Encoding.UTF8.GetString(bytes, at + 8, size));
                    else if (type == BinChunk && bin == null) { bin = new byte[size]; Buffer.BlockCopy(bytes, at + 8, bin, 0, size); }
                    at += 8 + size + ((4 - size % 4) % 4);
                }
                if (doc == null) throw new InvalidDataException("The GLB file does not have a JSON chunk.");
            }
            else
            {
                var text = Encoding.UTF8.GetString(bytes);
                if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
                doc = JObject.Parse(text);
            }
            return new Reader(doc, bin, folder, sourcePath).Run();
        }

        class Reader
        {
            readonly JObject _doc;
            readonly byte[] _bin;
            readonly string _folder;
            readonly ImportedModel _model;
            readonly List<byte[]> _buffers = new List<byte[]>();
            readonly Dictionary<int, int> _materialIndex = new Dictionary<int, int>();
            readonly Dictionary<int, byte[]> _images = new Dictionary<int, byte[]>();
            readonly Dictionary<int, string> _imageMimes = new Dictionary<int, string>();

            public Reader(JObject doc, byte[] bin, string folder, string source)
            {
                _doc = doc; _bin = bin; _folder = folder ?? "";
                _model = new ImportedModel { SourcePath = source, Format = "gltf", Up = UpAxis.Y, Units = ModelUnits.Meters };
            }

            JArray Arr(string name) => _doc[name] as JArray ?? new JArray();

            public ImportedModel Run()
            {
                var required = (_doc["extensionsRequired"] as JArray)?.Select(t => (string)t).ToList() ?? new List<string>();
                foreach (var ext in required)
                    if (ext == "KHR_draco_mesh_compression" || ext == "EXT_meshopt_compression")
                        throw new NotSupportedException("The glTF file uses " + ext + ", which DWSIM cannot read. Export it again without mesh compression.");

                foreach (JObject b in Arr("buffers").OfType<JObject>()) _buffers.Add(LoadBuffer(b));

                var nodes = Arr("nodes");
                var scenes = Arr("scenes");
                var sceneIndex = (int?)_doc["scene"] ?? 0;
                IEnumerable<int> roots;
                if (scenes.Count > sceneIndex && scenes[sceneIndex]["nodes"] is JArray list)
                    roots = list.Select(t => (int)t);
                else
                {
                    // without a scene, every node that is not a child of another is a root
                    var children = new HashSet<int>();
                    foreach (var n in nodes.OfType<JObject>())
                        foreach (var c in (n["children"] as JArray ?? new JArray())) children.Add((int)c);
                    roots = Enumerable.Range(0, nodes.Count).Where(i => !children.Contains(i));
                }
                var seen = new HashSet<int>();
                foreach (var r in roots) Visit(r, Mat.Identity, "", seen);
                return _model;
            }

            void Visit(int index, double[] parent, string path, HashSet<int> seen)
            {
                var nodes = Arr("nodes");
                if (index < 0 || index >= nodes.Count || !seen.Add(index)) return;
                var node = (JObject)nodes[index];
                var name = (string)node["name"];
                if (string.IsNullOrWhiteSpace(name)) name = "node" + index;
                var here = path.Length == 0 ? name : path + "/" + name;
                var world = Mat.Multiply(parent, Local(node));
                var mesh = (int?)node["mesh"];
                if (mesh.HasValue) AddMesh(mesh.Value, world, here);
                foreach (var c in (node["children"] as JArray ?? new JArray())) Visit((int)c, world, here, seen);
            }

            static double[] Local(JObject node)
            {
                if (node["matrix"] is JArray m && m.Count == 16) return m.Select(t => (double)t).ToArray();
                var t3 = node["translation"] as JArray;
                var r4 = node["rotation"] as JArray;
                var s3 = node["scale"] as JArray;
                return Mat.Trs(
                    t3 == null ? new double[3] : t3.Select(v => (double)v).ToArray(),
                    r4 == null ? new[] { 0.0, 0, 0, 1 } : r4.Select(v => (double)v).ToArray(),
                    s3 == null ? new[] { 1.0, 1, 1 } : s3.Select(v => (double)v).ToArray());
            }

            void AddMesh(int index, double[] world, string name)
            {
                var meshes = Arr("meshes");
                if (index < 0 || index >= meshes.Count) return;
                var mirrored = Mat.Determinant3(world) < 0;
                var prims = meshes[index]["primitives"] as JArray ?? new JArray();
                var k = 0;
                foreach (JObject prim in prims.OfType<JObject>())
                {
                    var mode = (int?)prim["mode"] ?? 4;
                    if (mode < 4)
                    {
                        _model.Warnings.Add("Points and lines of " + name + " were left out.");
                        continue;
                    }
                    var attrs = prim["attributes"] as JObject;
                    var posAcc = (int?)attrs?["POSITION"];
                    if (!posAcc.HasValue) continue;
                    var pos = Floats(posAcc.Value, 3);
                    var uvAcc = (int?)attrs["TEXCOORD_0"];
                    var uv = uvAcc.HasValue ? Floats(uvAcc.Value, 2) : null;
                    var count = pos.Length / 3;
                    int[] idx = prim["indices"] != null ? Ints((int)prim["indices"]) : Enumerable.Range(0, count).ToArray();
                    var tri = Triangles(idx, mode);

                    var p = new float[pos.Length];
                    for (var i = 0; i < count; i++)
                    {
                        var v = Mat.Apply(world, pos[3 * i], pos[3 * i + 1], pos[3 * i + 2]);
                        p[3 * i] = (float)v[0]; p[3 * i + 1] = (float)v[1]; p[3 * i + 2] = (float)v[2];
                    }
                    if (mirrored)
                        for (var i = 0; i + 2 < tri.Length; i += 3) { var t = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = t; }
                    var valid = tri.Where(i => i >= 0 && i < count).Count() == tri.Length;
                    if (!valid) { _model.Warnings.Add("A primitive of " + name + " has indices out of range and was left out."); continue; }

                    _model.Parts.Add(new MeshPart
                    {
                        Name = prims.Count > 1 ? name + "#" + k : name,
                        Material = Material((int?)prim["material"]),
                        Positions = p,
                        UV = uv != null && uv.Length == count * 2 ? uv : null,
                        Indices = tri
                    });
                    k++;
                }
            }

            static int[] Triangles(int[] idx, int mode)
            {
                if (mode == 4) return idx.Take(idx.Length - idx.Length % 3).ToArray();
                var list = new List<int>();
                if (mode == 5)
                    for (var i = 0; i + 2 < idx.Length; i++)
                    {
                        if (i % 2 == 0) { list.Add(idx[i]); list.Add(idx[i + 1]); list.Add(idx[i + 2]); }
                        else { list.Add(idx[i + 1]); list.Add(idx[i]); list.Add(idx[i + 2]); }
                    }
                else if (mode == 6)
                    for (var i = 1; i + 1 < idx.Length; i++) { list.Add(idx[0]); list.Add(idx[i]); list.Add(idx[i + 1]); }
                return list.ToArray();
            }

            int Material(int? index)
            {
                if (!index.HasValue) return -1;
                int found;
                if (_materialIndex.TryGetValue(index.Value, out found)) return found;
                var mats = Arr("materials");
                var m = new ModelMaterial();
                if (index.Value >= 0 && index.Value < mats.Count && mats[index.Value] is JObject j)
                {
                    m.Name = (string)j["name"];
                    var pbr = j["pbrMetallicRoughness"] as JObject;
                    var sg = j["extensions"]?["KHR_materials_pbrSpecularGlossiness"] as JObject;
                    var factor = (pbr?["baseColorFactor"] ?? sg?["diffuseFactor"]) as JArray;
                    if (factor != null && factor.Count == 4) m.Color = factor.Select(t => (float)t).ToArray();
                    if (pbr != null)
                    {
                        m.Metallic = (float?)pbr["metallicFactor"] ?? 1f;
                        m.Roughness = (float?)pbr["roughnessFactor"] ?? 1f;
                    }
                    var tex = (int?)(pbr?["baseColorTexture"]?["index"] ?? sg?["diffuseTexture"]?["index"]);
                    if (tex.HasValue) Texture(tex.Value, m);
                    m.AlphaMode = ((string)j["alphaMode"] ?? "OPAQUE").ToUpperInvariant();
                    m.AlphaCutoff = (float?)j["alphaCutoff"] ?? 0.5f;
                    m.DoubleSided = (bool?)j["doubleSided"] ?? false;
                }
                found = _model.AddMaterial(m);
                _materialIndex[index.Value] = found;
                return found;
            }

            void Texture(int index, ModelMaterial m)
            {
                var textures = Arr("textures");
                if (index < 0 || index >= textures.Count) return;
                var t = (JObject)textures[index];
                var source = (int?)t["source"] ?? (int?)t["extensions"]?["EXT_texture_webp"]?["source"];
                if (!source.HasValue) return;
                byte[] data;
                string mime;
                if (!_images.TryGetValue(source.Value, out data))
                {
                    data = null; mime = null;
                    var images = Arr("images");
                    if (source.Value >= 0 && source.Value < images.Count && images[source.Value] is JObject img)
                    {
                        try
                        {
                            if (img["bufferView"] != null) data = View((int)img["bufferView"]);
                            else if (img["uri"] != null) data = LoadUri((string)img["uri"]);
                        }
                        catch (Exception ex) { _model.Warnings.Add("A texture could not be read: " + ex.Message); }
                        mime = ImageMime(data, (string)img["mimeType"]);
                        if (data != null && mime == null)
                        {
                            _model.Warnings.Add("A texture that is not PNG or JPEG was left out (" + ((string)img["uri"] ?? (string)img["mimeType"]) + ").");
                            data = null;
                        }
                    }
                    _images[source.Value] = data;
                    _imageMimes[source.Value] = mime;
                }
                m.Texture = data;
                m.TextureMime = data == null ? null : _imageMimes[source.Value];
            }

            byte[] LoadBuffer(JObject b)
            {
                var uri = (string)b["uri"];
                if (uri == null) return _bin ?? new byte[0];
                return LoadUri(uri);
            }

            byte[] LoadUri(string uri)
            {
                if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var comma = uri.IndexOf(',');
                    return Convert.FromBase64String(uri.Substring(comma + 1));
                }
                return File.ReadAllBytes(Path.Combine(_folder, Uri.UnescapeDataString(uri)));
            }

            byte[] View(int index)
            {
                var v = (JObject)Arr("bufferViews")[index];
                var buffer = _buffers[(int)v["buffer"]];
                var offset = (int?)v["byteOffset"] ?? 0;
                var length = (int)v["byteLength"];
                var data = new byte[length];
                Buffer.BlockCopy(buffer, offset, data, 0, length);
                return data;
            }

            float[] Floats(int accessor, int width)
            {
                var a = (JObject)Arr("accessors")[accessor];
                var count = (int)a["count"];
                var type = (int)a["componentType"];
                var normalized = (bool?)a["normalized"] ?? false;
                var result = new float[count * width];
                if (a["bufferView"] == null) return result;
                var v = (JObject)Arr("bufferViews")[(int)a["bufferView"]];
                var buffer = _buffers[(int)v["buffer"]];
                var start = ((int?)v["byteOffset"] ?? 0) + ((int?)a["byteOffset"] ?? 0);
                var size = ComponentSize(type);
                var stride = (int?)v["byteStride"] ?? size * width;
                for (var i = 0; i < count; i++)
                    for (var k = 0; k < width; k++)
                    {
                        var at = start + i * stride + k * size;
                        float f;
                        switch (type)
                        {
                            case 5126: f = BitConverter.ToSingle(buffer, at); break;
                            case 5121: f = normalized ? buffer[at] / 255f : buffer[at]; break;
                            case 5120: f = normalized ? Math.Max((sbyte)buffer[at] / 127f, -1f) : (sbyte)buffer[at]; break;
                            case 5123: f = normalized ? BitConverter.ToUInt16(buffer, at) / 65535f : BitConverter.ToUInt16(buffer, at); break;
                            case 5122: f = normalized ? Math.Max(BitConverter.ToInt16(buffer, at) / 32767f, -1f) : BitConverter.ToInt16(buffer, at); break;
                            default: throw new InvalidDataException("Accessor " + accessor + " has a component type DWSIM does not read (" + type + ").");
                        }
                        result[i * width + k] = f;
                    }
                if (a["sparse"] != null) _model.Warnings.Add("A sparse accessor was read without its sparse values.");
                return result;
            }

            int[] Ints(int accessor)
            {
                var a = (JObject)Arr("accessors")[accessor];
                var count = (int)a["count"];
                var type = (int)a["componentType"];
                var result = new int[count];
                if (a["bufferView"] == null) return result;
                var v = (JObject)Arr("bufferViews")[(int)a["bufferView"]];
                var buffer = _buffers[(int)v["buffer"]];
                var start = ((int?)v["byteOffset"] ?? 0) + ((int?)a["byteOffset"] ?? 0);
                var size = ComponentSize(type);
                var stride = (int?)v["byteStride"] ?? size;
                for (var i = 0; i < count; i++)
                {
                    var at = start + i * stride;
                    switch (type)
                    {
                        case 5121: result[i] = buffer[at]; break;
                        case 5123: result[i] = BitConverter.ToUInt16(buffer, at); break;
                        case 5125: result[i] = (int)BitConverter.ToUInt32(buffer, at); break;
                        default: throw new InvalidDataException("The indices of accessor " + accessor + " have a type DWSIM does not read (" + type + ").");
                    }
                }
                return result;
            }

            static int ComponentSize(int type)
            {
                switch (type)
                {
                    case 5120: case 5121: return 1;
                    case 5122: case 5123: return 2;
                    default: return 4;
                }
            }
        }

        /// <summary>"image/png" or "image/jpeg" from the first bytes, or null for any other image.</summary>
        internal static string ImageMime(byte[] data, string declared = null)
        {
            if (data != null && data.Length > 8)
            {
                if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return "image/png";
                if (data[0] == 0xFF && data[1] == 0xD8) return "image/jpeg";
                return null;
            }
            return declared == "image/png" || declared == "image/jpeg" ? declared : null;
        }
    }

    /// <summary>4 x 4 matrices in glTF order (column-major, 16 numbers).</summary>
    internal static class Mat
    {
        public static double[] Identity => new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

        public static double[] Multiply(double[] a, double[] b)
        {
            var r = new double[16];
            for (var c = 0; c < 4; c++)
                for (var row = 0; row < 4; row++)
                {
                    double s = 0;
                    for (var k = 0; k < 4; k++) s += a[k * 4 + row] * b[c * 4 + k];
                    r[c * 4 + row] = s;
                }
            return r;
        }

        public static double[] Trs(double[] t, double[] q, double[] s)
        {
            double x = q[0], y = q[1], z = q[2], w = q[3];
            var r = new double[16];
            r[0] = (1 - 2 * (y * y + z * z)) * s[0];
            r[1] = (2 * (x * y + z * w)) * s[0];
            r[2] = (2 * (x * z - y * w)) * s[0];
            r[4] = (2 * (x * y - z * w)) * s[1];
            r[5] = (1 - 2 * (x * x + z * z)) * s[1];
            r[6] = (2 * (y * z + x * w)) * s[1];
            r[8] = (2 * (x * z + y * w)) * s[2];
            r[9] = (2 * (y * z - x * w)) * s[2];
            r[10] = (1 - 2 * (x * x + y * y)) * s[2];
            r[12] = t[0]; r[13] = t[1]; r[14] = t[2]; r[15] = 1;
            return r;
        }

        public static double[] Apply(double[] m, double x, double y, double z)
        {
            return new[]
            {
                m[0] * x + m[4] * y + m[8] * z + m[12],
                m[1] * x + m[5] * y + m[9] * z + m[13],
                m[2] * x + m[6] * y + m[10] * z + m[14]
            };
        }

        public static double Determinant3(double[] m)
        {
            return m[0] * (m[5] * m[10] - m[9] * m[6])
                 - m[4] * (m[1] * m[10] - m[9] * m[2])
                 + m[8] * (m[1] * m[6] - m[5] * m[2]);
        }
    }
}
