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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// Reads a 3D DWF or DWFx file, as Navisworks and the AutoCAD family publish plant models: a zip
    /// package with the tree of objects in XML and the geometry in W3D streams (HSF). Each object of
    /// the tree is a segment of the stream with the object's id as its name, so every part comes out
    /// with the name and the path the plant design program gave it (for instance
    /// /UPGN-I/EQUIPAMENTOS, /UPGN-I/VASOS, /V-100).
    /// </summary>
    /// <remarks>
    /// The stream holds cylinders, circles, lines and shells. The shells come in two kinds: a face
    /// list of packed vertex numbers, or Edgebreaker connectivity (see <see cref="EdgeBreaker"/>),
    /// with the points as floats in both. When the reader meets a record it does not know, it goes
    /// on from the next object of the tree and says so in the warnings; a shell it cannot decode
    /// is left out and counted.
    /// </remarks>
    public static class DwfReader
    {
        public class Stats
        {
            public int Objects, Cylinders, Circles, Lines, FaceShells, EdgeBreakerShells, HandleShells, SkippedShells, Resyncs;
            public Dictionary<string, int> Unknown = new Dictionary<string, int>();
            /// <summary>The objects that lost a shell (their path in the tree), with how many.</summary>
            public Dictionary<string, int> SkippedObjects = new Dictionary<string, int>();

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "{0} objects, {1} cylinders, {2} circles, {3} face shells, {4} Edgebreaker shells ({5} with duplicated vertices), {6} shells left out, {7} records skipped",
                    Objects, Cylinders, Circles, FaceShells, EdgeBreakerShells, HandleShells, SkippedShells, Resyncs);
            }
        }

        /// <summary>What the last read found, for the import's report and the tests.</summary>
        [ThreadStatic] public static Stats LastStats;

        public static ImportedModel Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var model = new ImportedModel { SourcePath = path, Format = "dwf", Up = UpAxis.Z };
            var stats = new Stats();
            LastStats = stats;

            var zipAt = IndexOf(bytes, new byte[] { 0x50, 0x4B, 0x03, 0x04 }, 0, Math.Min(bytes.Length, 4096));
            if (zipAt < 0) throw new InvalidDataException("The DWF file is not a package DWSIM can read (DWF 6 or later, or DWFx).");
            using (var zip = OpenPackage(bytes, zipAt))
            {
                var tree = new ObjectTree();
                foreach (var entry in zip.Entries)
                {
                    var name = entry.FullName.ToLowerInvariant();
                    if (name.EndsWith(".xml"))
                    {
                        var text = ReadText(entry);
                        if (text.IndexOf("<Objects", StringComparison.Ordinal) >= 0) tree.Load(text);
                        var units = Between(text, "Units type=\"", "\"");
                        if (units != null) model.Units = UnitsOf(units);
                    }
                }
                stats.Objects = tree.Count;
                foreach (var entry in zip.Entries)
                    if (entry.FullName.EndsWith(".w3d", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] w3d;
                        using (var s = entry.Open())
                        using (var ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            w3d = ms.ToArray();
                        }
                        new Walker(model, tree, stats).Run(Inflate(w3d));
                    }
            }
            if (stats.SkippedShells > 0)
                model.Warnings.Add(stats.SkippedShells + " shells DWSIM could not decode were left out.");
            if (stats.Resyncs > 0)
                model.Warnings.Add("Records DWSIM does not read were skipped " + stats.Resyncs + " times (" +
                    string.Join(", ", stats.Unknown.OrderByDescending(k => k.Value).Take(6).Select(k => k.Key + " " + k.Value)) + ").");
            if (model.Units == ModelUnits.Unknown) model.Units = ModelUnits.Meters;
            return model;
        }

        /// <summary>
        /// The zip package after the "(DWF V06.00)" header. Its offsets usually count from the start
        /// of the file, header included; a package written with offsets from its own start is read
        /// from there.
        /// </summary>
        static ZipArchive OpenPackage(byte[] bytes, int zipAt)
        {
            try
            {
                var whole = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
                if (whole.Entries.Count > 0) return whole;
            }
            catch (InvalidDataException) { }
            return new ZipArchive(new MemoryStream(bytes, zipAt, bytes.Length - zipAt, false), ZipArchiveMode.Read);
        }

        static ModelUnits UnitsOf(string u)
        {
            switch (u.Trim().ToLowerInvariant())
            {
                case "m": case "meters": return ModelUnits.Meters;
                case "cm": case "centimeters": return ModelUnits.Centimeters;
                case "mm": case "millimeters": return ModelUnits.Millimeters;
                case "ft": case "feet": return ModelUnits.Feet;
                case "in": case "inches": return ModelUnits.Inches;
                default: return ModelUnits.Unknown;
            }
        }

        static string ReadText(ZipArchiveEntry entry)
        {
            using (var s = entry.Open())
            using (var r = new StreamReader(s, Encoding.UTF8))
                return r.ReadToEnd();
        }

        static string Between(string text, string a, string b)
        {
            var i = text.IndexOf(a, StringComparison.Ordinal);
            if (i < 0) return null;
            i += a.Length;
            var j = text.IndexOf(b, i, StringComparison.Ordinal);
            return j < 0 ? null : text.Substring(i, j - i);
        }

        static int IndexOf(byte[] data, byte[] pattern, int from, int to)
        {
            for (var i = from; i <= to - pattern.Length; i++)
            {
                var ok = true;
                for (var k = 0; k < pattern.Length && ok; k++) ok = data[i + k] == pattern[k];
                if (ok) return i;
            }
            return -1;
        }

        /// <summary>
        /// The opcodes of a W3D stream: the text header and the file information come as they are,
        /// and the rest after the start of compression ('Z') is a zlib stream.
        /// </summary>
        static byte[] Inflate(byte[] w3d)
        {
            for (var i = 0; i + 2 < w3d.Length && i < 4096; i++)
                if (w3d[i] == (byte)'Z' && w3d[i + 1] == 0x78 && (w3d[i + 1] * 256 + w3d[i + 2]) % 31 == 0)
                {
                    using (var input = new MemoryStream(w3d, i + 3, w3d.Length - i - 3))
                    using (var z = new DeflateStream(input, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        z.CopyTo(output);
                        return output.ToArray();
                    }
                }
            throw new InvalidDataException("The W3D stream of the DWF file is not compressed as DWSIM expects.");
        }

        /// <summary>The objects of the package: names and parents by id.</summary>
        class ObjectTree
        {
            public readonly Dictionary<string, string> Names = new Dictionary<string, string>();
            public readonly Dictionary<string, string> Parents = new Dictionary<string, string>();
            public int Count => Names.Count;

            public void Load(string xml)
            {
                using (var r = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
                {
                    var current = new Stack<string>();
                    while (r.Read())
                    {
                        if (r.NodeType == XmlNodeType.Element && r.LocalName == "Object")
                        {
                            var id = r.GetAttribute("id");
                            var children = r.GetAttribute("children");
                            if (id != null)
                            {
                                if (!Names.ContainsKey(id)) Names[id] = null;
                                if (children != null)
                                    foreach (var c in children.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                                        Parents[c] = id;
                                if (!r.IsEmptyElement) current.Push(id);
                            }
                        }
                        else if (r.NodeType == XmlNodeType.EndElement && r.LocalName == "Object")
                        {
                            if (current.Count > 0) current.Pop();
                        }
                        else if (r.NodeType == XmlNodeType.Element && r.LocalName == "Property" && current.Count > 0
                            && r.GetAttribute("name") == "_name")
                            Names[current.Peek()] = r.GetAttribute("value");
                    }
                }
            }

            public string Name(string id)
            {
                string n;
                return Names.TryGetValue(id, out n) && !string.IsNullOrEmpty(n) ? n : id;
            }

            public string[] Path(string id)
            {
                var list = new List<string>();
                var guard = 0;
                while (id != null && guard++ < 256)
                {
                    list.Add(Name(id));
                    string p;
                    id = Parents.TryGetValue(id, out p) ? p : null;
                }
                list.Reverse();
                return list.ToArray();
            }
        }

        /// <summary>Walks the opcodes of a stream, keeping the segments, their transforms and colours.</summary>
        class Walker
        {
            readonly ImportedModel _model;
            readonly ObjectTree _tree;
            readonly Stats _stats;
            byte[] _d;
            int _p;
            /// <summary>Set by a record that ends in a layout the reader skips on purpose (normals by vertex): the walker goes on from the next object without a warning.</summary>
            bool _quiet;

            class Frame
            {
                public string Id;
                /// <summary>Row vectors: the point times the 3 x 3 part, plus the translation.</summary>
                public double[] M;
                public float[] Color;
            }

            readonly List<Frame> _stack = new List<Frame>();
            readonly Dictionary<string, PartBuilder> _parts = new Dictionary<string, PartBuilder>();
            readonly Dictionary<string, int> _materials = new Dictionary<string, int>();
            readonly List<string> _order = new List<string>();

            public Walker(ImportedModel model, ObjectTree tree, Stats stats)
            {
                _model = model; _tree = tree; _stats = stats;
            }

            static readonly double[] Identity = { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };

            Frame Top => _stack[_stack.Count - 1];

            public void Run(byte[] d)
            {
                _d = d;
                _p = 0;
                _stack.Clear();
                _stack.Add(new Frame { M = Identity, Color = null });
                while (_p < _d.Length)
                {
                    var at = _p;
                    var op = (char)_d[_p++];
                    bool ok;
                    _quiet = false;
                    try { ok = Record(op); }
                    catch (IndexOutOfRangeException) { ok = false; }
                    catch (ArgumentException) { ok = false; }
                    if (!ok && _quiet)
                    {
                        if (!Resync(_p)) break;
                    }
                    else if (!ok)
                    {
                        var key = op >= ' ' && op < 127 ? op.ToString() : "0x" + ((int)op).ToString("x2");
                        int n;
                        _stats.Unknown.TryGetValue(key, out n);
                        _stats.Unknown[key] = n + 1;
                        _stats.Resyncs++;
                        if (!Resync(at + 1)) break;
                    }
                }
                Flush();
            }

            bool Record(char op)
            {
                switch (op)
                {
                    case '\0': return true;
                    case '(':
                        {
                            var n = _d[_p++];
                            var name = Encoding.ASCII.GetString(_d, _p, n);
                            _p += n;
                            Open(name);
                            return true;
                        }
                    case ')':
                        if (_stack.Count > 1) _stack.RemoveAt(_stack.Count - 1);
                        return true;
                    case 'q': return true;
                    case 'U':
                        {
                            var n = BitConverter.ToUInt16(_d, _p);
                            _p += 2 + n;
                            return true;
                        }
                    case '}':
                        {
                            _p += 1 + 44;
                            var n = _d[_p++];
                            _p += n;
                            return true;
                        }
                    case '%':
                        {
                            var m = new double[12];
                            for (var k = 0; k < 12; k++) m[k] = F();
                            Top.M = Compose(m, Parent().M);
                            return true;
                        }
                    case '"': return ColorRecord();
                    case '~':
                        {
                            _p++;
                            Top.Color = new[] { _d[_p] / 255f, _d[_p + 1] / 255f, _d[_p + 2] / 255f };
                            _p += 3;
                            return true;
                        }
                    case 'C': return CircleRecord();
                    case 'Y': return CylinderRecord();
                    case 'l':
                        _p += 24;
                        _stats.Lines++;
                        return true;
                    case 'S': return ShellRecord();
                    default: return false;
                }
            }

            Frame Parent() => _stack.Count > 1 ? _stack[_stack.Count - 2] : _stack[0];

            /// <summary>
            /// Opens a segment. A segment named after an object of the tree goes under its parent
            /// object, wherever the stream left the stack, so a skipped record does not shift the
            /// rest of the plant.
            /// </summary>
            void Open(string name)
            {
                if (name.Length > 0 && _tree.Names.ContainsKey(name))
                {
                    string parent;
                    if (_tree.Parents.TryGetValue(name, out parent))
                    {
                        var at = _stack.FindLastIndex(f => f.Id == parent);
                        if (at >= 0 && at < _stack.Count - 1) _stack.RemoveRange(at + 1, _stack.Count - at - 1);
                        else if (at < 0 && _stack.Count > 1 && _stack.Any(f => f.Id != null))
                            _stack.RemoveRange(1, _stack.Count - 1);
                    }
                    var top = Top;
                    _stack.Add(new Frame { Id = name, M = top.M, Color = top.Color });
                }
                else
                {
                    var top = Top;
                    _stack.Add(new Frame { Id = null, M = top.M, Color = top.Color });
                }
            }

            /// <summary>Goes on from the next segment named after an object of the tree.</summary>
            bool Resync(int from)
            {
                for (var q = from; q + 2 < _d.Length; q++)
                {
                    if (_d[q] != (byte)'(') continue;
                    int n = _d[q + 1];
                    if (n < 1 || n > 40 || q + 2 + n > _d.Length) continue;
                    var ok = true;
                    for (var k = 0; k < n && ok; k++) { var c = _d[q + 2 + k]; ok = c >= 0x21 && c < 0x7F; }
                    if (!ok) continue;
                    var name = Encoding.ASCII.GetString(_d, q + 2, n);
                    if (!_tree.Names.ContainsKey(name)) continue;
                    _p = q;
                    return true;
                }
                _p = _d.Length;
                return false;
            }

            float F()
            {
                var v = BitConverter.ToSingle(_d, _p);
                _p += 4;
                return v;
            }

            int I()
            {
                var v = BitConverter.ToInt32(_d, _p);
                _p += 4;
                return v;
            }

            static double[] Compose(double[] child, double[] parent)
            {
                var r = new double[12];
                for (var row = 0; row < 3; row++)
                    for (var col = 0; col < 3; col++)
                        r[row * 3 + col] = child[row * 3] * parent[col] + child[row * 3 + 1] * parent[3 + col] + child[row * 3 + 2] * parent[6 + col];
                for (var col = 0; col < 3; col++)
                    r[9 + col] = child[9] * parent[col] + child[10] * parent[3 + col] + child[11] * parent[6 + col] + parent[9 + col];
                return r;
            }

            static void Apply(double[] m, double x, double y, double z, out double ox, out double oy, out double oz)
            {
                ox = x * m[0] + y * m[3] + z * m[6] + m[9];
                oy = x * m[1] + y * m[4] + z * m[7] + m[10];
                oz = x * m[2] + y * m[5] + z * m[8] + m[11];
            }

            /// <summary>
            /// A colour: which geometry it applies to (a mask), the channels it sets, and each channel.
            /// The diffuse colour is three bytes; specular, mirror, transmission and emission are a
            /// byte (0, or the length of a texture name that follows instead) and three bytes; gloss
            /// and index are floats.
            /// </summary>
            bool ColorRecord()
            {
                int mask = _d[_p++];
                if ((mask & 0x80) != 0) mask |= _d[_p++] << 8;
                var channels = BitConverter.ToUInt16(_d, _p);
                _p += 2;
                if ((channels & ~0x7F) != 0) return false;
                if ((channels & 1) != 0)
                {
                    Top.Color = new[] { _d[_p] / 255f, _d[_p + 1] / 255f, _d[_p + 2] / 255f };
                    _p += 3;
                }
                for (var bit = 1; bit <= 4; bit++)
                    if ((channels & (1 << bit)) != 0)
                    {
                        var n = _d[_p++];
                        _p += n == 0 ? 3 : n;
                    }
                if ((channels & 0x20) != 0) _p += 4;
                if ((channels & 0x40) != 0) _p += 4;
                return true;
            }

            bool CircleRecord()
            {
                var pts = new double[9];
                for (var k = 0; k < 9; k++) pts[k] = F();
                var flags = _d[_p++];
                double cx, cy, cz;
                if ((flags & 1) != 0) { cx = F(); cy = F(); cz = F(); }
                else if (!Circumcentre(pts, out cx, out cy, out cz)) return true;
                _stats.Circles++;
                double ux = pts[0] - cx, uy = pts[1] - cy, uz = pts[2] - cz;
                var r = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                if (r <= 0) return true;
                // the plane of the circle from its three points
                double ax = pts[3] - pts[0], ay = pts[4] - pts[1], az = pts[5] - pts[2];
                double bx = pts[6] - pts[0], by = pts[7] - pts[1], bz = pts[8] - pts[2];
                double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
                var nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (nl <= 0) return true;
                nx /= nl; ny /= nl; nz /= nl;
                ux /= r; uy /= r; uz /= r;
                double vx = ny * uz - nz * uy, vy = nz * ux - nx * uz, vz = nx * uy - ny * ux;
                var seg = Segments(r);
                var b = Builder();
                var m = Top.M;
                double ox, oy, oz;
                Apply(m, cx, cy, cz, out ox, out oy, out oz);
                var centre = b.Add(ox, oy, oz);
                var ring = new int[seg];
                for (var k = 0; k < seg; k++)
                {
                    var t = 2 * Math.PI * k / seg;
                    double c = Math.Cos(t), s = Math.Sin(t);
                    Apply(m, cx + r * (c * ux + s * vx), cy + r * (c * uy + s * vy), cz + r * (c * uz + s * vz), out ox, out oy, out oz);
                    ring[k] = b.Add(ox, oy, oz);
                }
                for (var k = 0; k < seg; k++) b.Triangle(centre, ring[k], ring[(k + 1) % seg]);
                return true;
            }

            static bool Circumcentre(double[] p, out double cx, out double cy, out double cz)
            {
                double ax = p[0], ay = p[1], az = p[2];
                double bx = p[3] - ax, by = p[4] - ay, bz = p[5] - az;
                double qx = p[6] - ax, qy = p[7] - ay, qz = p[8] - az;
                double nx = by * qz - bz * qy, ny = bz * qx - bx * qz, nz = bx * qy - by * qx;
                var n2 = nx * nx + ny * ny + nz * nz;
                cx = cy = cz = 0;
                if (n2 <= 0) return false;
                double b2 = bx * bx + by * by + bz * bz, q2 = qx * qx + qy * qy + qz * qz;
                // centre = a + ((|b|² q - |q|² b) x n) / (2 |n|²)
                double tx = b2 * qx - q2 * bx, ty = b2 * qy - q2 * by, tz = b2 * qz - q2 * bz;
                cx = ax + (ty * nz - tz * ny) / (2 * n2);
                cy = ay + (tz * nx - tx * nz) / (2 * n2);
                cz = az + (tx * ny - ty * nx) / (2 * n2);
                return true;
            }

            static int Segments(double r) => r < 0.05 ? 12 : r < 0.3 ? 16 : r < 1.5 ? 24 : 32;

            bool CylinderRecord()
            {
                double ax = F(), ay = F(), az = F(), bx = F(), by = F(), bz = F();
                double r = F();
                var caps = _d[_p++];
                _stats.Cylinders++;
                double wx = bx - ax, wy = by - ay, wz = bz - az;
                var len = Math.Sqrt(wx * wx + wy * wy + wz * wz);
                if (len <= 0 || r <= 0) return true;
                wx /= len; wy /= len; wz /= len;
                double hx = Math.Abs(wz) < 0.9 ? 0 : 1, hy = 0, hz = Math.Abs(wz) < 0.9 ? 1 : 0;
                double ux = wy * hz - wz * hy, uy = wz * hx - wx * hz, uz = wx * hy - wy * hx;
                var ul = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                ux /= ul; uy /= ul; uz /= ul;
                double vx = wy * uz - wz * uy, vy = wz * ux - wx * uz, vz = wx * uy - wy * ux;
                var seg = Segments(r);
                var b = Builder();
                var m = Top.M;
                var ra = new int[seg];
                var rb = new int[seg];
                double ox, oy, oz;
                for (var k = 0; k < seg; k++)
                {
                    var t = 2 * Math.PI * k / seg;
                    double c = Math.Cos(t) * r, s = Math.Sin(t) * r;
                    double dx = c * ux + s * vx, dy = c * uy + s * vy, dz = c * uz + s * vz;
                    Apply(m, ax + dx, ay + dy, az + dz, out ox, out oy, out oz);
                    ra[k] = b.Add(ox, oy, oz);
                    Apply(m, bx + dx, by + dy, bz + dz, out ox, out oy, out oz);
                    rb[k] = b.Add(ox, oy, oz);
                }
                for (var k = 0; k < seg; k++)
                {
                    var k1 = (k + 1) % seg;
                    b.Triangle(ra[k], ra[k1], rb[k1]);
                    b.Triangle(ra[k], rb[k1], rb[k]);
                }
                if ((caps & 1) != 0) Cap(b, m, ax, ay, az, ra, true);
                if ((caps & 2) != 0) Cap(b, m, bx, by, bz, rb, false);
                return true;
            }

            static void Cap(PartBuilder b, double[] m, double x, double y, double z, int[] ring, bool reverse)
            {
                double ox, oy, oz;
                Apply(m, x, y, z, out ox, out oy, out oz);
                var c = b.Add(ox, oy, oz);
                for (var k = 0; k < ring.Length; k++)
                {
                    var k1 = (k + 1) % ring.Length;
                    if (reverse) b.Triangle(c, ring[k1], ring[k]);
                    else b.Triangle(c, ring[k], ring[k1]);
                }
            }

            bool ShellRecord()
            {
                var sub = _d[_p++];
                _p++; // level of detail
                if ((sub & 0x80) != 0 || (sub & 0x20) != 0) return false;
                if ((sub & 0x40) != 0) return EdgeBreakerShell(sub);
                if ((sub & 0x01) != 0 || (sub & 0x04) != 0) return false;
                var n = I();
                if (n < 0 || _p + 12L * n > _d.Length) return false;
                var pts = new float[3 * n];
                Buffer.BlockCopy(_d, _p, pts, 0, 12 * n);
                _p += 12 * n;
                if ((sub & 0x02) == 0) return false;
                var scheme = _d[_p++];
                var size = I();
                if (scheme != 1 || size < 1 || _p + size > _d.Length) return false;
                var bits = _d[_p];
                var values = new List<int>();
                if (bits == 8) for (var k = 1; k < size; k++) values.Add(_d[_p + k]);
                else if (bits == 16) for (var k = 1; k + 1 < size; k += 2) values.Add(BitConverter.ToUInt16(_d, _p + k));
                else if (bits == 32) for (var k = 1; k + 3 < size; k += 4) values.Add(BitConverter.ToInt32(_d, _p + k));
                else return false;
                _p += size;

                var b = Builder();
                var map = AddPoints(b, pts);
                var face = new List<int>();
                for (var k = 0; k < values.Count;)
                {
                    var count = values[k++];
                    if (count <= 0 || k + count > values.Count) return false;
                    face.Clear();
                    for (var j = 0; j < count; j++)
                    {
                        var v = values[k + j];
                        if (v < 0 || v >= n) return false;
                        face.Add(map[v]);
                    }
                    k += count;
                    b.Polygon(face);
                }
                _stats.FaceShells++;
                return Optionals(sub);
            }

            int[] AddPoints(PartBuilder b, float[] pts)
            {
                var m = Top.M;
                var map = new int[pts.Length / 3];
                double ox, oy, oz;
                for (var k = 0; k < map.Length; k++)
                {
                    Apply(m, pts[3 * k], pts[3 * k + 1], pts[3 * k + 2], out ox, out oy, out oz);
                    map[k] = b.Add(ox, oy, oz);
                }
                return map;
            }

            /// <summary>
            /// The optional records after the points (normals and the like), skipped: 1, type, scheme,
            /// length, data, and 0 at the end. Normals by vertex (2) have a layout the reader does not
            /// follow; they end the shell's segment, and the walker goes on from the next object.
            /// </summary>
            bool Optionals(int sub)
            {
                if ((sub & 0x08) == 0) return true;
                var guard = 0;
                while (guard++ < 64)
                {
                    var more = _d[_p++];
                    if (more == 0) return true;
                    if (more == 2) { _quiet = true; return false; }
                    if (more != 1) return false;
                    _p += 2;
                    var len = I();
                    if (len < 0 || _p + (long)len > _d.Length) return false;
                    _p += len;
                }
                return false;
            }

            bool EdgeBreakerShell(byte sub)
            {
                var scheme = _d[_p++];
                var size = I();
                if (size < 24 || _p + (long)size > _d.Length) return false;
                var blk = _p;
                _p += size;
                if (_d[blk] != 2) return false;
                var opsLen = BitConverter.ToInt32(_d, blk + 4);
                var tableLen = BitConverter.ToInt32(_d, blk + 8);
                var points = BitConverter.ToInt32(_d, blk + 16);
                var tableAt = blk + 24 + ((opsLen + 3) & ~3);
                if (opsLen < 0 || tableLen < 0 || points < 0 || tableAt + tableLen > blk + size) return false;
                if (_p + 12L * points > _d.Length) return false;
                var mask = tableLen >= 4 ? BitConverter.ToInt32(_d, tableAt) : 0;
                var pts = new float[3 * points];
                Buffer.BlockCopy(_d, _p, pts, 0, 12 * points);
                _p += 12 * points;

                var decoded = false;
                List<int> dummies;
                Dictionary<int, int> duplicates;
                List<int> tris;
                int vertices;
                if ((mask & ~0x98) == 0 && Tables(tableAt, tableLen, mask, out dummies, out duplicates)
                    && EdgeBreaker.Decode(_d, blk + 24, opsLen, out tris, out vertices)
                    && vertices - dummies.Count - duplicates.Count == points)
                {
                    // the points are the decoded vertices in order, without the dummies and the
                    // duplicates; a duplicate takes the point its pair names
                    var point = new int[vertices];
                    foreach (var x in dummies) if (x >= 0 && x < vertices) point[x] = -1;
                    foreach (var x in duplicates.Keys) if (x >= 0 && x < vertices) point[x] = -2;
                    var k2 = 0;
                    for (var v = 0; v < vertices; v++) if (point[v] == 0) point[v] = ++k2;
                    if (k2 == points)
                    {
                        for (var v = 0; v < vertices; v++)
                        {
                            if (point[v] > 0) point[v]--;
                            else if (point[v] == -2)
                            {
                                var o = duplicates[v];
                                point[v] = o >= 0 && o < points ? o : -1;
                            }
                        }
                        var b = Builder();
                        var map = AddPoints(b, pts);
                        for (var t = 0; t + 2 < tris.Count; t += 3)
                        {
                            int a = point[tris[t]], c = point[tris[t + 1]], e = point[tris[t + 2]];
                            if (a < 0 || c < 0 || e < 0 || a == c || c == e || a == e) continue;
                            b.Triangle(map[a], map[c], map[e]);
                        }
                        decoded = true;
                        _stats.EdgeBreakerShells++;
                        if (duplicates.Count > 0) _stats.HandleShells++;
                    }
                }
                if (!decoded)
                {
                    _stats.SkippedShells++;
                    var current = CurrentId();
                    var id = current == null ? "" : string.Join(" ", _tree.Path(current).Skip(2));
                    int n;
                    _stats.SkippedObjects.TryGetValue(id, out n);
                    _stats.SkippedObjects[id] = n + 1;
                }
                return Optionals(0x08);
            }

            string CurrentId()
            {
                string id = null;
                for (var k = _stack.Count - 1; k >= 0 && id == null; k--) id = _stack[k].Id;
                return id;
            }

            /// <summary>
            /// The table after the opcodes: a mask, then the number of dummy vertices (bit 3) and the
            /// number of ints of the duplicate pairs (bit 4), then the dummies as deltas, then the
            /// pairs, then the quantization of the points (bit 7, three ints). A dummy closes a hole of
            /// the mesh and has no point. A duplicate is a vertex that the encoder split, where a face
            /// with a hole or a handle touches itself: each pair is the delta of its decoded number
            /// and the point it stands for.
            /// </summary>
            bool Tables(int at, int length, int mask, out List<int> dummies, out Dictionary<int, int> duplicates)
            {
                dummies = new List<int>();
                duplicates = new Dictionary<int, int>();
                var end = at + length;
                var k = at + 4;
                int nd = 0, npi = 0;
                if ((mask & 0x08) != 0) { if (k + 4 > end) return false; nd = BitConverter.ToInt32(_d, k); k += 4; }
                if ((mask & 0x10) != 0) { if (k + 4 > end) return false; npi = BitConverter.ToInt32(_d, k); k += 4; }
                if (nd < 0 || npi < 0 || (npi & 1) != 0 || k + 4L * (nd + npi) > end) return false;
                var sum = 0;
                for (var i = 0; i < nd; i++, k += 4)
                {
                    sum += BitConverter.ToInt32(_d, k);
                    dummies.Add(sum);
                }
                sum = 0;
                for (var i = 0; i < npi / 2; i++, k += 8)
                {
                    sum += BitConverter.ToInt32(_d, k);
                    duplicates[sum] = BitConverter.ToInt32(_d, k + 4);
                }
                return duplicates.Count == npi / 2;
            }

            PartBuilder Builder()
            {
                // the object is the nearest segment named after an object of the tree
                var id = CurrentId();
                var color = Top.Color ?? new[] { 0.6f, 0.6f, 0.6f };
                var colorKey = string.Join(",", color.Select(c => ((int)Math.Round(c * 255)).ToString(CultureInfo.InvariantCulture)));
                var key = (id ?? "") + "|" + colorKey;
                PartBuilder b;
                if (!_parts.TryGetValue(key, out b))
                {
                    b = new PartBuilder();
                    _parts[key] = b;
                    _order.Add(key);
                    int m;
                    if (!_materials.TryGetValue(colorKey, out m))
                    {
                        m = _model.AddMaterial(new ModelMaterial
                        {
                            Name = "dwf " + colorKey,
                            Color = new[] { color[0], color[1], color[2], 1f },
                            DoubleSided = true,
                            Roughness = 0.6f
                        });
                        _materials[colorKey] = m;
                    }
                }
                return b;
            }

            void Flush()
            {
                foreach (var key in _order)
                {
                    var b = _parts[key];
                    if (b.Empty) continue;
                    var bar = key.LastIndexOf('|');
                    var id = key.Substring(0, bar);
                    var colorKey = key.Substring(bar + 1);
                    var path = id.Length > 0 ? _tree.Path(id) : new[] { "(no object)" };
                    var part = b.Build(path[path.Length - 1], _materials[colorKey]);
                    part.Path = path;
                    part.ObjectId = id.Length > 0 ? id : null;
                    _model.Parts.Add(part);
                }
                _parts.Clear();
                _order.Clear();
            }
        }
    }
}
