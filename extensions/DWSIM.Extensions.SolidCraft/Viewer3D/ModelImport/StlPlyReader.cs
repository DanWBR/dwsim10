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
using System.Text;

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// Reads an STL file, binary or text, as one part. STL keeps neither names, materials, the unit
    /// nor the up axis; most CAD programs write it in millimetres with Z up.
    /// </summary>
    public static class StlReader
    {
        public static ImportedModel Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var model = new ImportedModel { SourcePath = path, Format = "stl", Up = UpAxis.Z };
            var b = new PartBuilder();
            if (IsBinary(bytes))
            {
                var n = (int)BitConverter.ToUInt32(bytes, 80);
                for (var i = 0; i < n; i++)
                {
                    var at = 84 + 50 * i + 12;
                    var a = b.Add(BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4), BitConverter.ToSingle(bytes, at + 8));
                    var c = b.Add(BitConverter.ToSingle(bytes, at + 12), BitConverter.ToSingle(bytes, at + 16), BitConverter.ToSingle(bytes, at + 20));
                    var d = b.Add(BitConverter.ToSingle(bytes, at + 24), BitConverter.ToSingle(bytes, at + 28), BitConverter.ToSingle(bytes, at + 32));
                    b.Triangle(a, c, d);
                }
            }
            else
            {
                var corners = new List<int>();
                foreach (var raw in Encoding.ASCII.GetString(bytes).Split('\n'))
                {
                    var t = raw.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (t.Length == 0) continue;
                    var key = t[0].ToLowerInvariant();
                    if (key == "outer") corners.Clear();
                    else if (key == "vertex" && t.Length >= 4)
                        corners.Add(b.Add(Num(t[1]), Num(t[2]), Num(t[3])));
                    else if (key == "endloop") b.Polygon(corners);
                }
            }
            if (!b.Empty) model.Parts.Add(b.Build(Path.GetFileNameWithoutExtension(path), -1));
            return model;
        }

        static bool IsBinary(byte[] bytes)
        {
            if (bytes.Length < 84) return false;
            var n = BitConverter.ToUInt32(bytes, 80);
            if (84L + 50L * n == bytes.Length) return true;
            var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart();
            return !(head.StartsWith("solid", StringComparison.OrdinalIgnoreCase) && head.IndexOf("facet", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static double Num(string s)
        {
            double d;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0;
        }
    }

    /// <summary>
    /// Reads a PLY file (text, or binary in either byte order) as one part: the vertex positions
    /// and the faces. Scans and photogrammetry are often written in PLY.
    /// </summary>
    public static class PlyReader
    {
        class Property
        {
            public string Name, Type, CountType;
            public bool IsList => CountType != null;
        }

        class Element
        {
            public string Name;
            public int Count;
            public List<Property> Properties = new List<Property>();
        }

        public static ImportedModel Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var model = new ImportedModel { SourcePath = path, Format = "ply" };
            // the header is text up to "end_header"
            var end = IndexOf(bytes, Encoding.ASCII.GetBytes("end_header"));
            if (end < 0) throw new InvalidDataException("The PLY file does not have a header.");
            var bodyAt = end + "end_header".Length;
            while (bodyAt < bytes.Length && bytes[bodyAt] != '\n') bodyAt++;
            bodyAt++;
            var header = Encoding.ASCII.GetString(bytes, 0, end).Split('\n');
            string format = "ascii";
            var elements = new List<Element>();
            foreach (var raw in header)
            {
                var t = raw.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length == 0) continue;
                if (t[0] == "format" && t.Length > 1) format = t[1];
                else if (t[0] == "element" && t.Length > 2) elements.Add(new Element { Name = t[1], Count = int.Parse(t[2], CultureInfo.InvariantCulture) });
                else if (t[0] == "property" && elements.Count > 0)
                {
                    if (t.Length >= 5 && t[1] == "list") elements[elements.Count - 1].Properties.Add(new Property { CountType = t[2], Type = t[3], Name = t[4] });
                    else if (t.Length >= 3) elements[elements.Count - 1].Properties.Add(new Property { Type = t[1], Name = t[2] });
                }
            }

            var b = new PartBuilder();
            var vertexBase = 0;
            var corners = new List<int>();
            Func<double> next;
            if (format == "ascii")
            {
                var words = Encoding.ASCII.GetString(bytes, bodyAt, bytes.Length - bodyAt)
                    .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var w = 0;
                next = () => w < words.Length ? StlReader.Num(words[w++]) : 0;
                Body(elements, b, corners, ref vertexBase, (type) => next());
            }
            else
            {
                var big = format == "binary_big_endian";
                var at = bodyAt;
                Body(elements, b, corners, ref vertexBase, (type) => Binary(bytes, ref at, type, big));
            }
            if (!b.Empty) model.Parts.Add(b.Build(Path.GetFileNameWithoutExtension(path), -1));
            return model;
        }

        delegate double Reader(string type);

        static void Body(List<Element> elements, PartBuilder b, List<int> corners, ref int vertexBase, Reader read)
        {
            foreach (var e in elements)
            {
                if (e.Name == "vertex")
                {
                    int ix = e.Properties.FindIndex(p => p.Name == "x"), iy = e.Properties.FindIndex(p => p.Name == "y"), iz = e.Properties.FindIndex(p => p.Name == "z");
                    var values = new double[e.Properties.Count];
                    vertexBase = b.Positions.Count / 3;
                    for (var i = 0; i < e.Count; i++)
                    {
                        for (var k = 0; k < e.Properties.Count; k++)
                        {
                            var p = e.Properties[k];
                            if (p.IsList) { var n = (int)read(p.CountType); for (var j = 0; j < n; j++) read(p.Type); }
                            else values[k] = read(p.Type);
                        }
                        b.Add(ix >= 0 ? values[ix] : 0, iy >= 0 ? values[iy] : 0, iz >= 0 ? values[iz] : 0);
                    }
                }
                else
                {
                    var face = e.Name == "face";
                    for (var i = 0; i < e.Count; i++)
                        foreach (var p in e.Properties)
                        {
                            if (!p.IsList) { read(p.Type); continue; }
                            var n = (int)read(p.CountType);
                            var indices = face && (p.Name == "vertex_indices" || p.Name == "vertex_index");
                            corners.Clear();
                            for (var j = 0; j < n; j++)
                            {
                                var v = (int)read(p.Type);
                                if (indices) corners.Add(vertexBase + v);
                            }
                            if (indices) b.Polygon(corners);
                        }
                }
            }
        }

        static double Binary(byte[] bytes, ref int at, string type, bool big)
        {
            int size;
            switch (type)
            {
                case "char": case "int8": case "uchar": case "uint8": size = 1; break;
                case "short": case "int16": case "ushort": case "uint16": size = 2; break;
                case "double": case "float64": size = 8; break;
                default: size = 4; break;
            }
            if (at + size > bytes.Length) throw new InvalidDataException("The PLY file is cut short.");
            var buf = new byte[size];
            Buffer.BlockCopy(bytes, at, buf, 0, size);
            at += size;
            if (big == BitConverter.IsLittleEndian) Array.Reverse(buf);
            switch (type)
            {
                case "char": case "int8": return (sbyte)buf[0];
                case "uchar": case "uint8": return buf[0];
                case "short": case "int16": return BitConverter.ToInt16(buf, 0);
                case "ushort": case "uint16": return BitConverter.ToUInt16(buf, 0);
                case "int": case "int32": return BitConverter.ToInt32(buf, 0);
                case "uint": case "uint32": return BitConverter.ToUInt32(buf, 0);
                case "float": case "float32": return BitConverter.ToSingle(buf, 0);
                case "double": case "float64": return BitConverter.ToDouble(buf, 0);
                default: throw new InvalidDataException("The PLY file has a property type DWSIM does not read (" + type + ").");
            }
        }

        static int IndexOf(byte[] data, byte[] pattern)
        {
            var limit = Math.Min(data.Length, 1 << 16) - pattern.Length;
            for (var i = 0; i <= limit; i++)
            {
                var ok = true;
                for (var k = 0; k < pattern.Length && ok; k++) ok = data[i + k] == pattern[k];
                if (ok) return i;
            }
            return -1;
        }
    }
}
