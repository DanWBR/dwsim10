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

namespace DWSIM.Extensions.SolidCraft.Viewer3D.ModelImport
{
    /// <summary>
    /// Reads the elements of an IFC file (IFC2x3, IFC4 and IFC4x3, the STEP physical file of ISO
    /// 10303-21) as meshes in metres with Z up. Each element becomes a part with its name, its tag and
    /// the role its class says (a tank is equipment, a pipe segment piping, a beam structure), and its
    /// path goes from the file through the assemblies that hold it. The reader draws extrusions of the
    /// standard profiles, revolutions, swept disks and swept profiles, faceted and tessellated breps,
    /// mapped items and the CSG primitives; a boolean difference keeps its first operand (openings are
    /// not cut), and the geometry it cannot draw is counted in the warnings.
    /// </summary>
    public static class IfcReader
    {
        public static ImportedModel Read(string path)
        {
            return Read(File.ReadAllText(path), path);
        }

        public static ImportedModel Read(string text, string sourcePath)
        {
            return new Reader(text, sourcePath).Run();
        }

        sealed class Ref { public int Id; public Ref(int id) { Id = id; } }
        sealed class EnumValue { public string V; public EnumValue(string v) { V = v; } }
        sealed class Typed { public string Type; public object V; }

        sealed class Entity
        {
            public int Id;
            public string Type;
            public int Start, End;
            public List<object> Args;
        }

        struct V3
        {
            public double X, Y, Z;
            public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
            public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static V3 operator *(V3 a, double s) => new V3(a.X * s, a.Y * s, a.Z * s);
            public double Dot(V3 b) => X * b.X + Y * b.Y + Z * b.Z;
            public V3 Cross(V3 b) => new V3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
            public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
            public V3 Unit() { var l = Length; return l < 1e-15 ? new V3(0, 0, 0) : new V3(X / l, Y / l, Z / l); }
        }

        /// <summary>An affine transform, row-major 3x4: p' = R p + t.</summary>
        sealed class Xf
        {
            public readonly double[] M = new double[12];
            public static Xf Identity() { var x = new Xf(); x.M[0] = x.M[5] = x.M[10] = 1; return x; }
            public static Xf FromAxes(V3 x, V3 y, V3 z, V3 t)
            {
                var r = new Xf();
                r.M[0] = x.X; r.M[1] = y.X; r.M[2] = z.X; r.M[3] = t.X;
                r.M[4] = x.Y; r.M[5] = y.Y; r.M[6] = z.Y; r.M[7] = t.Y;
                r.M[8] = x.Z; r.M[9] = y.Z; r.M[10] = z.Z; r.M[11] = t.Z;
                return r;
            }
            /// <summary>This transform after <paramref name="b"/>: (this * b)(p) = this(b(p)).</summary>
            public Xf Times(Xf b)
            {
                var r = new Xf();
                for (var i = 0; i < 3; i++)
                {
                    for (var j = 0; j < 3; j++)
                        r.M[4 * i + j] = M[4 * i] * b.M[j] + M[4 * i + 1] * b.M[4 + j] + M[4 * i + 2] * b.M[8 + j];
                    r.M[4 * i + 3] = M[4 * i] * b.M[3] + M[4 * i + 1] * b.M[7] + M[4 * i + 2] * b.M[11] + M[4 * i + 3];
                }
                return r;
            }
            public V3 Apply(V3 p) => new V3(
                M[0] * p.X + M[1] * p.Y + M[2] * p.Z + M[3],
                M[4] * p.X + M[5] * p.Y + M[6] * p.Z + M[7],
                M[8] * p.X + M[9] * p.Y + M[10] * p.Z + M[11]);
            public double Determinant =>
                M[0] * (M[5] * M[10] - M[6] * M[9]) - M[1] * (M[4] * M[10] - M[6] * M[8]) + M[2] * (M[4] * M[9] - M[5] * M[8]);
        }

        /// <summary>A mesh in the frame of a representation item.</summary>
        sealed class Mesh
        {
            public readonly List<V3> P = new List<V3>();
            public readonly List<int> I = new List<int>();
            /// <summary>True when the mesh bounds a solid, so its triangles can be turned to face out.</summary>
            public bool Closed = true;
            public int Add(V3 p) { P.Add(p); return P.Count - 1; }
            public void Tri(int a, int b, int c) { if (a != b && b != c && a != c) { I.Add(a); I.Add(b); I.Add(c); } }

            /// <summary>Turns the triangles of a closed mesh round when they face in.</summary>
            public void FaceOut()
            {
                if (!Closed || I.Count == 0) return;
                double v = 0;
                for (var k = 0; k + 2 < I.Count; k += 3) v += P[I[k]].Dot(P[I[k + 1]].Cross(P[I[k + 2]]));
                if (v >= 0) return;
                for (var k = 0; k + 2 < I.Count; k += 3) { var t = I[k + 1]; I[k + 1] = I[k + 2]; I[k + 2] = t; }
            }
        }

        /// <summary>A profile: its outer loop and its holes, in the plane of the profile.</summary>
        sealed class Profile
        {
            public List<V3> Outer = new List<V3>();
            public List<List<V3>> Holes = new List<List<V3>>();
        }

        static readonly HashSet<string> Skipped = new HashSet<string>
        {
            "IFCOPENINGELEMENT", "IFCOPENINGSTANDARDCASE", "IFCVOIDINGFEATURE", "IFCSPACE", "IFCVIRTUALELEMENT", "IFCANNOTATION",
            "IFCGRID", "IFCSPATIALZONE", "IFCREINFORCINGBAR", "IFCREINFORCINGMESH", "IFCTENDON", "IFCALIGNMENT", "IFCPROJECT",
            "IFCBUILDING", "IFCBUILDINGSTOREY", "IFCFACILITY", "IFCFACILITYPART", "IFCEXTERNALSPATIALELEMENT", "IFCZONE"
        };

        static readonly HashSet<string> Spatial = new HashSet<string>
        {
            "IFCPROJECT", "IFCSITE", "IFCBUILDING", "IFCBUILDINGSTOREY", "IFCSPACE", "IFCFACILITY", "IFCFACILITYPART", "IFCBRIDGE",
            "IFCROAD", "IFCRAILWAY", "IFCMARINEFACILITY", "IFCEXTERNALSPATIALELEMENT", "IFCSPATIALZONE"
        };

        static readonly string[] EquipmentClasses =
        {
            "IFCTANK", "IFCPUMP", "IFCCOMPRESSOR", "IFCFAN", "IFCHEATEXCHANGER", "IFCBOILER", "IFCCHILLER", "IFCCOOLINGTOWER", "IFCCOOLEDBEAM",
            "IFCFLOWSTORAGEDEVICE", "IFCFLOWMOVINGDEVICE", "IFCENERGYCONVERSIONDEVICE", "IFCFLOWTREATMENTDEVICE", "IFCFILTER", "IFCINTERCEPTOR",
            "IFCUNITARYEQUIPMENT", "IFCBURNER", "IFCENGINE", "IFCELECTRICGENERATOR", "IFCELECTRICMOTOR", "IFCEVAPORATOR", "IFCCONDENSER",
            "IFCAIRTOAIRHEATRECOVERY", "IFCCOIL", "IFCEVAPORATIVECOOLER", "IFCHUMIDIFIER", "IFCSOLARDEVICE", "IFCTRANSFORMER",
            "IFCFIRESUPPRESSIONTERMINAL", "IFCMOTORCONNECTION", "IFCTUBEBUNDLE", "IFCSPACEHEATER", "IFCDAMPER"
        };

        static readonly string[] PipingClasses =
        {
            "IFCPIPESEGMENT", "IFCPIPEFITTING", "IFCFLOWSEGMENT", "IFCFLOWFITTING", "IFCDUCTSEGMENT", "IFCDUCTFITTING", "IFCVALVE",
            "IFCFLOWCONTROLLER", "IFCFLOWMETER", "IFCDISTRIBUTIONPORT", "IFCDUCTSILENCER"
        };

        static readonly string[] StructureClasses =
        {
            "IFCBEAM", "IFCCOLUMN", "IFCMEMBER", "IFCPLATE", "IFCSLAB", "IFCSTAIR", "IFCSTAIRFLIGHT", "IFCRAILING", "IFCRAMP", "IFCRAMPFLIGHT",
            "IFCWALL", "IFCWALLSTANDARDCASE", "IFCWALLELEMENTEDCASE", "IFCROOF", "IFCCOVERING", "IFCDOOR", "IFCWINDOW", "IFCCURTAINWALL",
            "IFCBUILDINGELEMENTPART", "IFCCHIMNEY", "IFCSHADINGDEVICE", "IFCCABLECARRIERSEGMENT", "IFCCABLECARRIERFITTING", "IFCCABLESEGMENT",
            "IFCDISCRETEACCESSORY", "IFCFASTENER", "IFCMECHANICALFASTENER", "IFCBEAMSTANDARDCASE", "IFCCOLUMNSTANDARDCASE",
            "IFCMEMBERSTANDARDCASE", "IFCPLATESTANDARDCASE", "IFCSLABSTANDARDCASE", "IFCDOORSTANDARDCASE", "IFCWINDOWSTANDARDCASE"
        };

        static readonly string[] SceneryClasses =
        {
            "IFCSITE", "IFCGEOGRAPHICELEMENT", "IFCCIVILELEMENT", "IFCEARTHWORKSFILL", "IFCEARTHWORKSCUT", "IFCPAVEMENT", "IFCCOURSE",
            "IFCKERB", "IFCFOOTING", "IFCPILE"
        };

        /// <summary>equipment, piping, structure or scenery by the class of an element; null when the class does not say.</summary>
        public static string RoleOfClass(string type)
        {
            var t = (type ?? "").ToUpperInvariant();
            if (EquipmentClasses.Contains(t)) return "equipment";
            if (PipingClasses.Contains(t)) return "piping";
            if (StructureClasses.Contains(t)) return "structure";
            if (SceneryClasses.Contains(t)) return "scenery";
            return null;
        }

        sealed class Reader
        {
            readonly string _t;
            readonly string _source;
            readonly Dictionary<int, Entity> _e = new Dictionary<int, Entity>();
            readonly Dictionary<int, Xf> _placements = new Dictionary<int, Xf>();
            readonly Dictionary<int, float[]> _styles = new Dictionary<int, float[]>();
            readonly Dictionary<string, int> _materials = new Dictionary<string, int>();
            readonly Dictionary<string, int> _unsupported = new Dictionary<string, int>();
            ImportedModel _model;
            double _len = 1, _angle = 1;

            public Reader(string text, string source)
            {
                _t = text ?? "";
                _source = source;
            }

            public ImportedModel Run()
            {
                _model = new ImportedModel { SourcePath = _source, Format = "ifc", Up = UpAxis.Z, Units = ModelUnits.Meters };
                Index();
                if (_e.Count == 0) throw new InvalidDataException("The file is not an IFC file (it does not have a DATA section).");
                Units();
                foreach (var e in _e.Values.Where(x => x.Type == "IFCSTYLEDITEM")) Style(e);

                // the assemblies and the nests that hold each element
                var parent = new Dictionary<int, int>();
                foreach (var rel in _e.Values.Where(x => x.Type == "IFCRELAGGREGATES" || x.Type == "IFCRELNESTS"))
                {
                    var a = Args(rel);
                    var whole = AsRef(At(a, 4));
                    if (whole == null) continue;
                    foreach (var r in AsList(At(a, 5)).OfType<Ref>()) parent[r.Id] = whole.Id;
                }

                var root = string.IsNullOrEmpty(_source) ? "model" : Path.GetFileNameWithoutExtension(_source);
                var names = new Dictionary<int, string>();
                var taken = new HashSet<string>();
                Func<int, string> nameOf = null;
                nameOf = id =>
                {
                    string n;
                    if (names.TryGetValue(id, out n)) return n;
                    n = DisplayName(Get(id));
                    // two elements of the same name under the same parent stay two groups
                    var under = (parent.ContainsKey(id) ? parent[id] : 0) + "\u001f";
                    var unique = n;
                    var k = 1;
                    while (!taken.Add(under + unique)) unique = n + " (" + ++k + ")";
                    names[id] = unique;
                    return unique;
                };

                foreach (var e in _e.Values.OrderBy(x => x.Id))
                {
                    if (Skipped.Contains(e.Type)) continue;
                    var a = Args(e);
                    if (a.Count < 7) continue;
                    var place = Get(At(a, 5));
                    var shape = Get(At(a, 6));
                    if (shape == null || shape.Type != "IFCPRODUCTDEFINITIONSHAPE") continue;
                    if (place != null && place.Type != "IFCLOCALPLACEMENT" && place.Type != "IFCGRIDPLACEMENT" && place.Type != "IFCLINEARPLACEMENT") continue;

                    var chain = new List<int> { e.Id };
                    var seen = new HashSet<int> { e.Id };
                    int up;
                    var cursor = e.Id;
                    while (parent.TryGetValue(cursor, out up) && seen.Add(up))
                    {
                        var pe = Get(up);
                        if (pe == null || Spatial.Contains(pe.Type)) break;
                        chain.Insert(0, up);
                        cursor = up;
                    }
                    var path = new List<string> { root };
                    path.AddRange(chain.Select(id => nameOf(id)));
                    var top = Get(chain[0]);
                    var role = RoleOfClass(top.Type) ?? RoleOfClass(e.Type);

                    var xf = Placement(place);
                    var byStyle = new Dictionary<int, Mesh>();
                    try
                    {
                        foreach (var rep in Representations(shape))
                            foreach (var item in AsList(At(Args(rep), 3)).OfType<Ref>())
                                Item(Get(item), xf, -1, byStyle);
                    }
                    catch (Exception ex) when (!(ex is OutOfMemoryException))
                    {
                        Warn("an element that could not be drawn (" + e.Type + ")");
                    }
                    var k2 = 0;
                    foreach (var pair in byStyle)
                    {
                        var m = pair.Value;
                        if (m.I.Count == 0) continue;
                        var part = new MeshPart
                        {
                            Name = string.Join("/", path) + (byStyle.Count > 1 ? "#" + k2 : ""),
                            Path = path.ToArray(),
                            ObjectId = Text(At(a, 0)),
                            Role = role,
                            Material = pair.Key >= 0 ? pair.Key : DefaultMaterial(role),
                            Positions = new float[m.P.Count * 3],
                            Indices = m.I.ToArray()
                        };
                        for (var i = 0; i < m.P.Count; i++)
                        {
                            part.Positions[3 * i] = (float)m.P[i].X;
                            part.Positions[3 * i + 1] = (float)m.P[i].Y;
                            part.Positions[3 * i + 2] = (float)m.P[i].Z;
                        }
                        _model.Parts.Add(part);
                        k2++;
                    }
                }
                foreach (var u in _unsupported.OrderByDescending(x => x.Value))
                    _model.Warnings.Add(u.Value + " " + u.Key + " left out.");
                if (_model.Parts.Count == 0) _model.Warnings.Add("The IFC file does not have any element with a body DWSIM can draw.");
                return _model;
            }

            void Warn(string what)
            {
                int n;
                _unsupported.TryGetValue(what, out n);
                _unsupported[what] = n + 1;
            }

            // ---------------------------------------------------------------- the file

            void Index()
            {
                var t = _t;
                var data = t.IndexOf("DATA;", StringComparison.Ordinal);
                if (data < 0) return;
                var i = data + 5;
                var n = t.Length;
                while (i < n)
                {
                    // to the next instance
                    while (i < n && t[i] != '#')
                    {
                        if (t[i] == '/' && i + 1 < n && t[i + 1] == '*')
                        {
                            var close = t.IndexOf("*/", i + 2, StringComparison.Ordinal);
                            i = close < 0 ? n : close + 2;
                            continue;
                        }
                        if (t[i] == 'E' && string.CompareOrdinal(t, i, "ENDSEC;", 0, 7) == 0) return;
                        i++;
                    }
                    if (i >= n) return;
                    i++;
                    var id = 0;
                    while (i < n && char.IsDigit(t[i])) id = id * 10 + (t[i++] - '0');
                    while (i < n && (t[i] == ' ' || t[i] == '=' || t[i] == '\r' || t[i] == '\n' || t[i] == '\t')) i++;
                    var ts = i;
                    while (i < n && (char.IsLetterOrDigit(t[i]) || t[i] == '_')) i++;
                    var type = t.Substring(ts, i - ts).ToUpperInvariant();
                    while (i < n && char.IsWhiteSpace(t[i])) i++;
                    var start = i;
                    // to the end of the record: a ';' outside strings
                    var depth = 0;
                    while (i < n)
                    {
                        var c = t[i];
                        if (c == '\'')
                        {
                            i++;
                            while (i < n)
                            {
                                if (t[i] == '\'')
                                {
                                    if (i + 1 < n && t[i + 1] == '\'') { i += 2; continue; }
                                    break;
                                }
                                i++;
                            }
                        }
                        else if (c == '(') depth++;
                        else if (c == ')') depth--;
                        else if (c == ';' && depth <= 0) break;
                        i++;
                    }
                    if (type.Length > 0 && start < i) _e[id] = new Entity { Id = id, Type = type, Start = start, End = i };
                    i++;
                }
            }

            List<object> Args(Entity e)
            {
                if (e.Args != null) return e.Args;
                var pos = e.Start;
                var v = Value(ref pos) as List<object>;
                e.Args = v ?? new List<object>();
                return e.Args;
            }

            object Value(ref int i)
            {
                var t = _t;
                while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
                if (i >= t.Length) return null;
                var c = t[i];
                if (c == '(')
                {
                    i++;
                    var list = new List<object>();
                    while (i < t.Length)
                    {
                        while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
                        if (i < t.Length && t[i] == ')') { i++; break; }
                        list.Add(Value(ref i));
                        while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
                        if (i < t.Length && t[i] == ',') i++;
                    }
                    return list;
                }
                if (c == '$' || c == '*') { i++; return null; }
                if (c == '#')
                {
                    i++;
                    var id = 0;
                    while (i < t.Length && char.IsDigit(t[i])) id = id * 10 + (t[i++] - '0');
                    return new Ref(id);
                }
                if (c == '\'')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < t.Length)
                    {
                        if (t[i] == '\'')
                        {
                            if (i + 1 < t.Length && t[i + 1] == '\'') { sb.Append('\''); i += 2; continue; }
                            i++;
                            break;
                        }
                        sb.Append(t[i++]);
                    }
                    return Decode(sb.ToString());
                }
                if (c == '"')
                {
                    var close = t.IndexOf('"', i + 1);
                    if (close < 0) close = t.Length - 1;
                    var s = t.Substring(i + 1, close - i - 1);
                    i = close + 1;
                    return s;
                }
                if (c == '.')
                {
                    var close = t.IndexOf('.', i + 1);
                    if (close > i && close - i < 64)
                    {
                        var word = t.Substring(i + 1, close - i - 1);
                        if (word.Length > 0 && char.IsLetter(word[0]))
                        {
                            i = close + 1;
                            return new EnumValue(word.ToUpperInvariant());
                        }
                    }
                }
                if (char.IsLetter(c))
                {
                    var s0 = i;
                    while (i < t.Length && (char.IsLetterOrDigit(t[i]) || t[i] == '_')) i++;
                    var type = t.Substring(s0, i - s0).ToUpperInvariant();
                    while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
                    if (i < t.Length && t[i] == '(')
                    {
                        var inner = Value(ref i) as List<object>;
                        return new Typed { Type = type, V = inner != null && inner.Count == 1 ? inner[0] : inner };
                    }
                    return new EnumValue(type);
                }
                {
                    var s0 = i;
                    while (i < t.Length && (char.IsDigit(t[i]) || t[i] == '.' || t[i] == '-' || t[i] == '+' || t[i] == 'E' || t[i] == 'e')) i++;
                    if (i == s0) { i++; return null; }
                    double d;
                    return double.TryParse(t.Substring(s0, i - s0), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? (object)d : null;
                }
            }

            /// <summary>The escapes of ISO 10303-21 strings: \X2\..\X0\ (UTF-16), \X\hh, \S\c and \\.</summary>
            static string Decode(string s)
            {
                if (s.IndexOf('\\') < 0) return s;
                var sb = new StringBuilder(s.Length);
                for (var i = 0; i < s.Length; i++)
                {
                    if (s[i] != '\\') { sb.Append(s[i]); continue; }
                    if (string.CompareOrdinal(s, i, "\\X2\\", 0, 4) == 0)
                    {
                        var end = s.IndexOf("\\X0\\", i + 4, StringComparison.Ordinal);
                        if (end < 0) break;
                        var hex = s.Substring(i + 4, end - i - 4);
                        for (var k = 0; k + 3 < hex.Length; k += 4)
                        {
                            int code;
                            if (int.TryParse(hex.Substring(k, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) sb.Append((char)code);
                        }
                        i = end + 3;
                        continue;
                    }
                    if (string.CompareOrdinal(s, i, "\\X\\", 0, 3) == 0 && i + 4 < s.Length)
                    {
                        int code;
                        if (int.TryParse(s.Substring(i + 3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) sb.Append((char)code);
                        i += 4;
                        continue;
                    }
                    if (string.CompareOrdinal(s, i, "\\S\\", 0, 3) == 0 && i + 3 < s.Length)
                    {
                        sb.Append((char)(s[i + 3] + 128));
                        i += 3;
                        continue;
                    }
                    if (i + 1 < s.Length && s[i + 1] == '\\') { sb.Append('\\'); i++; continue; }
                    sb.Append('\\');
                }
                return sb.ToString();
            }

            Entity Get(int id) { Entity e; return _e.TryGetValue(id, out e) ? e : null; }
            Entity Get(object o) { var r = AsRef(o); return r == null ? null : Get(r.Id); }
            static object At(List<object> a, int i) => a != null && i >= 0 && i < a.Count ? a[i] : null;
            static Ref AsRef(object o) => o as Ref;
            static List<object> AsList(object o) => o as List<object> ?? new List<object>();
            static object Unwrap(object o) { var t = o as Typed; return t != null ? t.V : o; }
            static double Num(object o, double fallback = 0) { var v = Unwrap(o); return v is double ? (double)v : fallback; }
            static string Text(object o) => Unwrap(o) as string;
            static bool? Bool(object o)
            {
                var v = Unwrap(o) as EnumValue;
                if (v == null) return null;
                if (v.V == "T") return true;
                if (v.V == "F") return false;
                return null;
            }

            // ---------------------------------------------------------------- units, names and styles

            void Units()
            {
                var project = _e.Values.FirstOrDefault(x => x.Type == "IFCPROJECT");
                var assignment = project == null ? null : Get(At(Args(project), 8));
                IEnumerable<Entity> units = assignment != null
                    ? AsList(At(Args(assignment), 0)).Select(Get).Where(x => x != null)
                    : _e.Values.Where(x => x.Type == "IFCSIUNIT" || x.Type == "IFCCONVERSIONBASEDUNIT");
                foreach (var u in units)
                {
                    var a = Args(u);
                    var kind = Unwrap(At(a, 1)) as EnumValue;
                    if (kind == null) continue;
                    if (kind.V == "LENGTHUNIT") _len = UnitScale(u);
                    else if (kind.V == "PLANEANGLEUNIT") _angle = UnitScale(u);
                }
            }

            double UnitScale(Entity u)
            {
                var a = Args(u);
                if (u.Type == "IFCSIUNIT")
                {
                    var prefix = Unwrap(At(a, 2)) as EnumValue;
                    return Prefix(prefix == null ? null : prefix.V);
                }
                if (u.Type == "IFCCONVERSIONBASEDUNIT" || u.Type == "IFCCONVERSIONBASEDUNITWITHOFFSET")
                {
                    var measure = Get(At(a, 3));
                    if (measure == null) return 1;
                    var ma = Args(measure);
                    var value = Num(At(ma, 0), 1);
                    var si = Get(At(ma, 1));
                    return value * (si == null ? 1 : UnitScale(si));
                }
                return 1;
            }

            static double Prefix(string p)
            {
                switch (p)
                {
                    case "EXA": return 1e18;
                    case "PETA": return 1e15;
                    case "TERA": return 1e12;
                    case "GIGA": return 1e9;
                    case "MEGA": return 1e6;
                    case "KILO": return 1e3;
                    case "HECTO": return 1e2;
                    case "DECA": return 1e1;
                    case "DECI": return 1e-1;
                    case "CENTI": return 1e-2;
                    case "MILLI": return 1e-3;
                    case "MICRO": return 1e-6;
                    case "NANO": return 1e-9;
                    default: return 1;
                }
            }

            /// <summary>The name of an element as the path shows it: its name, with the tag in front when only the tag has one.</summary>
            string DisplayName(Entity e)
            {
                if (e == null) return "element";
                var a = Args(e);
                var name = (Text(At(a, 2)) ?? "").Trim();
                var objectType = (Text(At(a, 4)) ?? "").Trim();
                var tag = a.Count > 7 ? (Text(At(a, 7)) ?? "").Trim() : "";
                if (name.Length == 0) name = objectType;
                if (name.Length == 0) name = e.Type.StartsWith("IFC", StringComparison.Ordinal) ? Char(e.Type.Substring(3)) : e.Type;
                if (tag.Length > 0 && ModelSegmentation.TagOf(name) == null && ModelSegmentation.TagOf(tag) != null) name = tag + " " + name;
                return name;
            }

            static string Char(string upper) => upper.Length == 0 ? upper : upper.Substring(0, 1) + upper.Substring(1).ToLowerInvariant();

            void Style(Entity styled)
            {
                var a = Args(styled);
                var item = AsRef(At(a, 0));
                if (item == null) return;
                var color = SurfaceColor(At(a, 1), 0);
                if (color != null) _styles[item.Id] = color;
            }

            /// <summary>The colour of a surface style, through IFC2x3's style assignments and IFC4's direct styles.</summary>
            float[] SurfaceColor(object o, int depth)
            {
                if (depth > 6) return null;
                var r = AsRef(o);
                if (r == null)
                {
                    foreach (var x in AsList(o))
                    {
                        var c = SurfaceColor(x, depth + 1);
                        if (c != null) return c;
                    }
                    return null;
                }
                var e = Get(r.Id);
                if (e == null) return null;
                var a = Args(e);
                switch (e.Type)
                {
                    case "IFCPRESENTATIONSTYLEASSIGNMENT": return SurfaceColor(At(a, 0), depth + 1);
                    case "IFCSURFACESTYLE": return SurfaceColor(At(a, 2), depth + 1);
                    case "IFCSURFACESTYLERENDERING":
                    case "IFCSURFACESTYLESHADING":
                        {
                            var rgb = Get(At(a, 0));
                            if (rgb == null || rgb.Type != "IFCCOLOURRGB") return null;
                            var c = Args(rgb);
                            var transparency = Num(At(a, 1), 0);
                            return new[] { (float)Num(At(c, 1), 0.7), (float)Num(At(c, 2), 0.7), (float)Num(At(c, 3), 0.7), (float)Math.Max(0.05, 1 - transparency) };
                        }
                    default: return null;
                }
            }

            int MaterialFor(float[] c)
            {
                var key = string.Join(",", c.Select(x => Math.Round(x, 3).ToString(CultureInfo.InvariantCulture)));
                int m;
                if (_materials.TryGetValue(key, out m)) return m;
                // IFC colours are in display (sRGB) values; glTF keeps linear ones
                var lin = new[] { Linear(c[0]), Linear(c[1]), Linear(c[2]), c[3] };
                m = _model.AddMaterial(new ModelMaterial
                {
                    Name = "ifc " + key,
                    Color = lin,
                    AlphaMode = c[3] < 0.99f ? "BLEND" : "OPAQUE",
                    Roughness = 0.7f
                });
                _materials[key] = m;
                return m;
            }

            static float Linear(float s) => s <= 0.04045f ? s / 12.92f : (float)Math.Pow((s + 0.055) / 1.055, 2.4);

            int DefaultMaterial(string role)
            {
                switch (role)
                {
                    case "equipment": return MaterialFor(new[] { 0.78f, 0.80f, 0.82f, 1f });
                    case "piping": return MaterialFor(new[] { 0.55f, 0.62f, 0.68f, 1f });
                    case "structure": return MaterialFor(new[] { 0.62f, 0.55f, 0.42f, 1f });
                    case "scenery": return MaterialFor(new[] { 0.55f, 0.58f, 0.50f, 1f });
                    default: return MaterialFor(new[] { 0.72f, 0.72f, 0.72f, 1f });
                }
            }

            // ---------------------------------------------------------------- placements

            V3 Point(Entity e)
            {
                if (e == null) return new V3(0, 0, 0);
                var c = AsList(At(Args(e), 0));
                return new V3(Num(At(c, 0)) * _len, Num(At(c, 1)) * _len, Num(At(c, 2)) * _len);
            }

            V3 Direction(Entity e, V3 fallback)
            {
                if (e == null) return fallback;
                var a = Args(e);
                if (e.Type == "IFCVECTOR") return Direction(Get(At(a, 0)), fallback);
                var c = AsList(At(a, 0));
                var v = new V3(Num(At(c, 0)), Num(At(c, 1)), Num(At(c, 2)));
                return v.Length < 1e-12 ? fallback : v.Unit();
            }

            Xf Axis2(Entity e)
            {
                if (e == null) return Xf.Identity();
                var a = Args(e);
                var loc = Point(Get(At(a, 0)));
                if (e.Type == "IFCAXIS2PLACEMENT2D")
                {
                    var x2 = Direction(Get(At(a, 1)), new V3(1, 0, 0));
                    x2 = new V3(x2.X, x2.Y, 0).Unit();
                    if (x2.Length < 0.5) x2 = new V3(1, 0, 0);
                    return Xf.FromAxes(x2, new V3(-x2.Y, x2.X, 0), new V3(0, 0, 1), loc);
                }
                if (e.Type == "IFCAXIS1PLACEMENT")
                {
                    var z1 = Direction(Get(At(a, 1)), new V3(0, 0, 1));
                    var ref1 = Math.Abs(z1.X) < 0.9 ? new V3(1, 0, 0) : new V3(0, 1, 0);
                    var x1 = (ref1 - z1 * ref1.Dot(z1)).Unit();
                    return Xf.FromAxes(x1, z1.Cross(x1), z1, loc);
                }
                var z = Direction(Get(At(a, 1)), new V3(0, 0, 1));
                var r = Direction(Get(At(a, 2)), Math.Abs(z.X) < 0.9 ? new V3(1, 0, 0) : new V3(0, 1, 0));
                var x = r - z * r.Dot(z);
                if (x.Length < 1e-9) { r = Math.Abs(z.X) < 0.9 ? new V3(1, 0, 0) : new V3(0, 1, 0); x = r - z * r.Dot(z); }
                x = x.Unit();
                return Xf.FromAxes(x, z.Cross(x), z, loc);
            }

            Xf Placement(Entity e)
            {
                if (e == null) return Xf.Identity();
                Xf cached;
                if (_placements.TryGetValue(e.Id, out cached)) return cached;
                _placements[e.Id] = Xf.Identity(); // a cycle stops here
                Xf result;
                var a = Args(e);
                if (e.Type == "IFCLOCALPLACEMENT")
                {
                    var rel = Get(At(a, 0));
                    var local = Axis2(Get(At(a, 1)));
                    result = rel == null ? local : Placement(rel).Times(local);
                }
                else result = Xf.Identity();
                _placements[e.Id] = result;
                return result;
            }

            /// <summary>A cartesian transformation operator (2D or 3D, uniform or not), as a mapped item and a derived profile use it.</summary>
            Xf Operator(Entity e)
            {
                if (e == null) return Xf.Identity();
                var a = Args(e);
                var two = e.Type.Contains("2D");
                var origin = Point(Get(At(a, 2)));
                var scale = Num(At(a, 3), 1);
                var x = Direction(Get(At(a, 0)), new V3(1, 0, 0));
                V3 y, z;
                if (two)
                {
                    x = new V3(x.X, x.Y, 0).Unit();
                    y = Direction(Get(At(a, 1)), new V3(-x.Y, x.X, 0));
                    z = new V3(0, 0, 1);
                }
                else
                {
                    z = Direction(Get(At(a, 4)), new V3(0, 0, 1));
                    x = (x - z * x.Dot(z));
                    if (x.Length < 1e-9) x = Math.Abs(z.X) < 0.9 ? new V3(1, 0, 0) : new V3(0, 1, 0);
                    x = x.Unit();
                    y = z.Cross(x);
                }
                var s2 = scale;
                var s3 = scale;
                if (e.Type.Contains("NONUNIFORM"))
                {
                    s2 = Num(At(a, two ? 4 : 5), scale);
                    if (!two) s3 = Num(At(a, 6), scale);
                }
                return Xf.FromAxes(x * scale, y * s2, z * s3, origin);
            }

            // ---------------------------------------------------------------- representations and items

            IEnumerable<Entity> Representations(Entity shape)
            {
                var reps = AsList(At(Args(shape), 2)).Select(Get).Where(x => x != null).ToList();
                Func<Entity, string> id = r => (Text(At(Args(r), 1)) ?? "").ToUpperInvariant();
                var body = reps.Where(r => id(r) == "BODY").ToList();
                if (body.Count > 0) return body;
                var facet = reps.Where(r => id(r) == "FACETATION").ToList();
                if (facet.Count > 0) return facet;
                var other = reps.Where(r => !new[] { "AXIS", "FOOTPRINT", "BOX", "ANNOTATION", "PROFILE", "CLEARANCE", "REFERENCE", "COG", "LIGHTING", "SURVEY", "PLAN" }.Contains(id(r))).ToList();
                if (other.Count > 0) return other;
                return reps.Where(r => id(r) == "BOX");
            }

            Mesh MeshOf(Dictionary<int, Mesh> byStyle, int style)
            {
                Mesh m;
                if (!byStyle.TryGetValue(style, out m)) byStyle[style] = m = new Mesh();
                return m;
            }

            void Item(Entity item, Xf xf, int style, Dictionary<int, Mesh> byStyle, int depth = 0)
            {
                if (item == null || depth > 24) return;
                float[] c;
                if (_styles.TryGetValue(item.Id, out c)) style = MaterialFor(c);
                var a = Args(item);
                switch (item.Type)
                {
                    case "IFCMAPPEDITEM":
                        {
                            var map = Get(At(a, 0));
                            if (map == null) return;
                            var ma = Args(map);
                            var origin = Get(At(ma, 0));
                            var originXf = origin != null && origin.Type.StartsWith("IFCCARTESIANTRANSFORMATIONOPERATOR", StringComparison.Ordinal) ? Operator(origin) : Axis2(origin);
                            var place = xf.Times(Operator(Get(At(a, 1)))).Times(originXf);
                            var rep = Get(At(ma, 1));
                            if (rep == null) return;
                            foreach (var r in AsList(At(Args(rep), 3)).OfType<Ref>()) Item(Get(r.Id), place, style, byStyle, depth + 1);
                            return;
                        }
                    case "IFCBOOLEANRESULT":
                    case "IFCBOOLEANCLIPPINGRESULT":
                        {
                            var op = Unwrap(At(a, 0)) as EnumValue;
                            Item(Get(At(a, 1)), xf, style, byStyle, depth + 1);
                            if (op != null && op.V == "UNION") Item(Get(At(a, 2)), xf, style, byStyle, depth + 1);
                            return;
                        }
                    case "IFCCSGSOLID":
                        Item(Get(At(a, 0)), xf, style, byStyle, depth + 1);
                        return;
                    case "IFCHALFSPACESOLID":
                    case "IFCBOXEDHALFSPACE":
                    case "IFCPOLYGONALBOUNDEDHALFSPACE":
                        return;
                }
                var mesh = Solid(item);
                if (mesh == null)
                {
                    Warn(Char(item.Type.Substring(Math.Min(3, item.Type.Length))) + " items");
                    return;
                }
                mesh.FaceOut();
                var target = MeshOf(byStyle, style);
                var mirrored = xf.Determinant < 0;
                var offset = target.P.Count;
                foreach (var p in mesh.P) target.P.Add(xf.Apply(p));
                for (var k = 0; k + 2 < mesh.I.Count; k += 3)
                {
                    if (mirrored) target.Tri(offset + mesh.I[k], offset + mesh.I[k + 2], offset + mesh.I[k + 1]);
                    else target.Tri(offset + mesh.I[k], offset + mesh.I[k + 1], offset + mesh.I[k + 2]);
                }
            }

            /// <summary>The mesh of a geometric item in its own frame, or null when the reader does not draw it.</summary>
            Mesh Solid(Entity item)
            {
                var a = Args(item);
                switch (item.Type)
                {
                    case "IFCEXTRUDEDAREASOLID":
                    case "IFCEXTRUDEDAREASOLIDTAPERED":
                        {
                            var profiles = Profiles(Get(At(a, 0)));
                            if (profiles.Count == 0) return null;
                            var place = Axis2(Get(At(a, 1)));
                            var dir = Direction(Get(At(a, 2)), new V3(0, 0, 1));
                            var depth = Num(At(a, 3)) * _len;
                            var m = new Mesh();
                            foreach (var p in profiles) Extrude(m, p, dir * depth);
                            return Transform(m, place);
                        }
                    case "IFCREVOLVEDAREASOLID":
                    case "IFCREVOLVEDAREASOLIDTAPERED":
                        {
                            var profiles = Profiles(Get(At(a, 0)));
                            if (profiles.Count == 0) return null;
                            var place = Axis2(Get(At(a, 1)));
                            var axis = Get(At(a, 2));
                            var aa = axis == null ? null : Args(axis);
                            var origin = aa == null ? new V3(0, 0, 0) : Point(Get(At(aa, 0)));
                            var dir = aa == null ? new V3(0, 1, 0) : Direction(Get(At(aa, 1)), new V3(0, 1, 0));
                            var angle = Num(At(a, 3)) * _angle;
                            var m = new Mesh();
                            foreach (var p in profiles) Revolve(m, p, origin, dir, angle);
                            return Transform(m, place);
                        }
                    case "IFCSWEPTDISKSOLID":
                    case "IFCSWEPTDISKSOLIDPOLYGONAL":
                        {
                            var path = Curve(Get(At(a, 0)));
                            var r = Num(At(a, 1)) * _len;
                            var inner = Num(At(a, 2)) * _len;
                            if (path.Count < 2 || r <= 0) return null;
                            var profile = new Profile { Outer = Circle(new V3(0, 0, 0), r, CircleSegments(r)) };
                            if (inner > 0 && inner < r) profile.Holes.Add(Reverse(Circle(new V3(0, 0, 0), inner, CircleSegments(r))));
                            var m = new Mesh();
                            Sweep(m, profile, path, null);
                            return m;
                        }
                    case "IFCSURFACECURVESWEPTAREASOLID":
                    case "IFCFIXEDREFERENCESWEPTAREASOLID":
                    case "IFCDIRECTRIXCURVESWEPTAREASOLID":
                        {
                            var profiles = Profiles(Get(At(a, 0)));
                            var place = Axis2(Get(At(a, 1)));
                            var path = Curve(Get(At(a, 2)));
                            if (profiles.Count == 0 || path.Count < 2) return null;
                            V3? fixedRef = null;
                            if (item.Type == "IFCFIXEDREFERENCESWEPTAREASOLID") fixedRef = Direction(Get(At(a, 5)), new V3(1, 0, 0));
                            var m = new Mesh();
                            foreach (var p in profiles) Sweep(m, p, path, fixedRef);
                            return Transform(m, place);
                        }
                    case "IFCFACETEDBREP":
                    case "IFCFACETEDBREPWITHVOIDS":
                    case "IFCADVANCEDBREP":
                    case "IFCADVANCEDBREPWITHVOIDS":
                        {
                            var m = new Mesh();
                            Shell(m, Get(At(a, 0)));
                            if (item.Type.EndsWith("WITHVOIDS", StringComparison.Ordinal))
                                foreach (var v in AsList(At(a, 1)).OfType<Ref>()) Shell(m, Get(v.Id));
                            return m.I.Count == 0 ? null : m;
                        }
                    case "IFCSHELLBASEDSURFACEMODEL":
                    case "IFCFACEBASEDSURFACEMODEL":
                        {
                            var m = new Mesh { Closed = false };
                            foreach (var s in AsList(At(a, 0)).OfType<Ref>()) Shell(m, Get(s.Id));
                            return m.I.Count == 0 ? null : m;
                        }
                    case "IFCCLOSEDSHELL":
                    case "IFCOPENSHELL":
                    case "IFCCONNECTEDFACESET":
                        {
                            var m = new Mesh { Closed = item.Type == "IFCCLOSEDSHELL" };
                            Shell(m, item);
                            return m.I.Count == 0 ? null : m;
                        }
                    case "IFCTRIANGULATEDFACESET":
                    case "IFCTRIANGULATEDIRREGULARNETWORK":
                        {
                            var pts = PointList(Get(At(a, 0)));
                            var closed = Bool(At(a, 2));
                            var index = AsList(At(a, 3));
                            var pn = a.Count > 4 ? AsList(At(a, 4)).Select(x => (int)Num(x)).ToList() : new List<int>();
                            var m = new Mesh { Closed = closed == true };
                            foreach (var p in pts) m.Add(p);
                            Func<int, int> map = k => pn.Count > 0 ? (k >= 1 && k <= pn.Count ? pn[k - 1] - 1 : -1) : k - 1;
                            foreach (var t in index)
                            {
                                var tl = AsList(t);
                                if (tl.Count < 3) continue;
                                var i0 = map((int)Num(tl[0])); var i1 = map((int)Num(tl[1])); var i2 = map((int)Num(tl[2]));
                                if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= pts.Count || i1 >= pts.Count || i2 >= pts.Count) continue;
                                m.Tri(i0, i1, i2);
                            }
                            return m.I.Count == 0 ? null : m;
                        }
                    case "IFCPOLYGONALFACESET":
                        {
                            var pts = PointList(Get(At(a, 0)));
                            var closed = Bool(At(a, 1));
                            var pn = a.Count > 3 ? AsList(At(a, 3)).Select(x => (int)Num(x)).ToList() : new List<int>();
                            Func<int, int> map = k => pn.Count > 0 ? (k >= 1 && k <= pn.Count ? pn[k - 1] - 1 : -1) : k - 1;
                            var m = new Mesh { Closed = closed == true };
                            foreach (var f in AsList(At(a, 2)).Select(Get).Where(x => x != null))
                            {
                                var fa = Args(f);
                                var outer = AsList(At(fa, 0)).Select(x => map((int)Num(x))).Where(k => k >= 0 && k < pts.Count).Select(k => pts[k]).ToList();
                                var holes = new List<List<V3>>();
                                if (f.Type == "IFCINDEXEDPOLYGONALFACEWITHVOIDS")
                                    foreach (var h in AsList(At(fa, 1)))
                                        holes.Add(AsList(h).Select(x => map((int)Num(x))).Where(k => k >= 0 && k < pts.Count).Select(k => pts[k]).ToList());
                                Face(m, outer, holes);
                            }
                            return m.I.Count == 0 ? null : m;
                        }
                    case "IFCBLOCK":
                        {
                            var place = Axis2(Get(At(a, 0)));
                            var x = Num(At(a, 1)) * _len; var y = Num(At(a, 2)) * _len; var z = Num(At(a, 3)) * _len;
                            var m = new Mesh();
                            Extrude(m, new Profile { Outer = new List<V3> { new V3(0, 0, 0), new V3(x, 0, 0), new V3(x, y, 0), new V3(0, y, 0) } }, new V3(0, 0, z));
                            return Transform(m, place);
                        }
                    case "IFCRIGHTCIRCULARCYLINDER":
                        {
                            var place = Axis2(Get(At(a, 0)));
                            var h = Num(At(a, 1)) * _len; var r = Num(At(a, 2)) * _len;
                            var m = new Mesh();
                            Extrude(m, new Profile { Outer = Circle(new V3(0, 0, 0), r, CircleSegments(r)) }, new V3(0, 0, h));
                            return Transform(m, place);
                        }
                    case "IFCRIGHTCIRCULARCONE":
                        {
                            var place = Axis2(Get(At(a, 0)));
                            var h = Num(At(a, 1)) * _len; var r = Num(At(a, 2)) * _len;
                            var m = new Mesh();
                            var ring = Circle(new V3(0, 0, 0), r, CircleSegments(r));
                            var apex = m.Add(new V3(0, 0, h));
                            var centre = m.Add(new V3(0, 0, 0));
                            var first = m.P.Count;
                            foreach (var p in ring) m.Add(p);
                            for (var k = 0; k < ring.Count; k++)
                            {
                                var k1 = (k + 1) % ring.Count;
                                m.Tri(first + k, first + k1, apex);
                                m.Tri(first + k1, first + k, centre);
                            }
                            return Transform(m, place);
                        }
                    case "IFCRECTANGULARPYRAMID":
                        {
                            var place = Axis2(Get(At(a, 0)));
                            var x = Num(At(a, 1)) * _len; var y = Num(At(a, 2)) * _len; var h = Num(At(a, 3)) * _len;
                            var m = new Mesh();
                            var b = new[] { m.Add(new V3(0, 0, 0)), m.Add(new V3(x, 0, 0)), m.Add(new V3(x, y, 0)), m.Add(new V3(0, y, 0)) };
                            var apex = m.Add(new V3(x / 2, y / 2, h));
                            for (var k = 0; k < 4; k++) m.Tri(b[k], b[(k + 1) % 4], apex);
                            m.Tri(b[0], b[2], b[1]); m.Tri(b[0], b[3], b[2]);
                            return Transform(m, place);
                        }
                    case "IFCSPHERE":
                        {
                            var place = Axis2(Get(At(a, 0)));
                            var r = Num(At(a, 1)) * _len;
                            var m = new Mesh();
                            Sphere(m, r);
                            return Transform(m, place);
                        }
                    case "IFCBOUNDINGBOX":
                        {
                            var corner = Point(Get(At(a, 0)));
                            var x = Num(At(a, 1)) * _len; var y = Num(At(a, 2)) * _len; var z = Num(At(a, 3)) * _len;
                            var m = new Mesh();
                            Extrude(m, new Profile { Outer = new List<V3> { corner, corner + new V3(x, 0, 0), corner + new V3(x, y, 0), corner + new V3(0, y, 0) } }, new V3(0, 0, z));
                            return m;
                        }
                    default:
                        return null;
                }
            }

            static Mesh Transform(Mesh m, Xf xf)
            {
                for (var i = 0; i < m.P.Count; i++) m.P[i] = xf.Apply(m.P[i]);
                if (xf.Determinant < 0)
                    for (var k = 0; k + 2 < m.I.Count; k += 3) { var t = m.I[k + 1]; m.I[k + 1] = m.I[k + 2]; m.I[k + 2] = t; }
                return m;
            }

            List<V3> PointList(Entity e)
            {
                var list = new List<V3>();
                if (e == null) return list;
                foreach (var c in AsList(At(Args(e), 0)))
                {
                    var cl = AsList(c);
                    list.Add(new V3(Num(At(cl, 0)) * _len, Num(At(cl, 1)) * _len, Num(At(cl, 2)) * _len));
                }
                return list;
            }

            void Shell(Mesh m, Entity shell)
            {
                if (shell == null) return;
                foreach (var f in AsList(At(Args(shell), 0)).Select(Get).Where(x => x != null))
                {
                    var bounds = AsList(At(Args(f), 0)).Select(Get).Where(x => x != null).ToList();
                    List<V3> outer = null;
                    var holes = new List<List<V3>>();
                    foreach (var b in bounds)
                    {
                        var ba = Args(b);
                        var loop = Get(At(ba, 0));
                        if (loop == null) continue;
                        List<V3> pts;
                        if (loop.Type == "IFCPOLYLOOP") pts = AsList(At(Args(loop), 0)).Select(x => Point(Get(x))).ToList();
                        else if (loop.Type == "IFCEDGELOOP") pts = EdgeLoop(loop);
                        else continue;
                        if (Bool(At(ba, 1)) == false) pts.Reverse();
                        if (b.Type == "IFCFACEOUTERBOUND" && outer == null) outer = pts;
                        else holes.Add(pts);
                    }
                    if (outer == null && holes.Count > 0)
                    {
                        // without an outer bound, the largest loop is the outer one
                        var largest = holes.OrderByDescending(h => Newell(h).Length).First();
                        holes.Remove(largest);
                        outer = largest;
                    }
                    if (outer != null) Face(m, outer, holes);
                }
            }

            /// <summary>The corners of an edge loop whose edges run straight between vertex points.</summary>
            List<V3> EdgeLoop(Entity loop)
            {
                var pts = new List<V3>();
                foreach (var oe in AsList(At(Args(loop), 0)).Select(Get).Where(x => x != null))
                {
                    var oa = Args(oe);
                    var edge = Get(At(oa, 3));
                    if (edge == null) continue;
                    var ea = Args(edge);
                    var sense = Bool(At(oa, 4)) != false;
                    var start = Get(sense ? At(ea, 0) : At(ea, 1));
                    if (start == null) continue;
                    pts.Add(Point(Get(At(Args(start), 0))));
                }
                return pts;
            }

            static V3 Newell(IList<V3> pts)
            {
                var n = new V3(0, 0, 0);
                for (var i = 0; i < pts.Count; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % pts.Count];
                    n = n + new V3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
                }
                return n;
            }

            /// <summary>A planar face with holes, triangulated in its own plane.</summary>
            static void Face(Mesh m, List<V3> outer, List<List<V3>> holes)
            {
                outer = Clean(outer);
                if (outer.Count < 3) return;
                if (outer.Count == 3 && holes.Count == 0)
                {
                    m.Tri(m.Add(outer[0]), m.Add(outer[1]), m.Add(outer[2]));
                    return;
                }
                var n = Newell(outer).Unit();
                if (n.Length < 0.5) return;
                var u = (Math.Abs(n.X) < 0.9 ? new V3(1, 0, 0) : new V3(0, 1, 0));
                u = (u - n * u.Dot(n)).Unit();
                var v = n.Cross(u);
                var all = new List<V3>(outer);
                var flat = outer.Select(p => new V3(p.Dot(u), p.Dot(v), 0)).ToList();
                var flatHoles = new List<List<V3>>();
                foreach (var h in holes)
                {
                    var hc = Clean(h);
                    if (hc.Count < 3) continue;
                    all.AddRange(hc);
                    flatHoles.Add(hc.Select(p => new V3(p.Dot(u), p.Dot(v), 0)).ToList());
                }
                var tris = Triangulate(flat, flatHoles);
                var first = m.P.Count;
                foreach (var p in all) m.Add(p);
                for (var k = 0; k + 2 < tris.Count; k += 3) m.Tri(first + tris[k], first + tris[k + 1], first + tris[k + 2]);
            }

            static List<V3> Clean(List<V3> pts)
            {
                var r = new List<V3>();
                foreach (var p in pts)
                    if (r.Count == 0 || (p - r[r.Count - 1]).Length > 1e-9) r.Add(p);
                while (r.Count > 1 && (r[0] - r[r.Count - 1]).Length <= 1e-9) r.RemoveAt(r.Count - 1);
                return r;
            }

            // ---------------------------------------------------------------- profiles and curves

            static int CircleSegments(double r) => r < 0.06 ? 12 : r < 0.3 ? 16 : r < 1.5 ? 24 : 36;

            static List<V3> Circle(V3 c, double r, int n)
            {
                var pts = new List<V3>(n);
                for (var k = 0; k < n; k++)
                {
                    var t = 2 * Math.PI * k / n;
                    pts.Add(new V3(c.X + r * Math.Cos(t), c.Y + r * Math.Sin(t), c.Z));
                }
                return pts;
            }

            static List<V3> Reverse(List<V3> pts) { var r = new List<V3>(pts); r.Reverse(); return r; }

            static List<V3> Rect(double w, double h) => new List<V3> { new V3(-w / 2, -h / 2, 0), new V3(w / 2, -h / 2, 0), new V3(w / 2, h / 2, 0), new V3(-w / 2, h / 2, 0) };

            static double Area2(IList<V3> pts)
            {
                double s = 0;
                for (var i = 0; i < pts.Count; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; s += a.X * b.Y - b.X * a.Y; }
                return s / 2;
            }

            /// <summary>The profiles an area definition makes (a composite profile makes several), placed in their plane.</summary>
            List<Profile> Profiles(Entity e, int depth = 0)
            {
                var list = new List<Profile>();
                if (e == null || depth > 8) return list;
                var a = Args(e);
                if (e.Type == "IFCCOMPOSITEPROFILEDEF")
                {
                    foreach (var p in AsList(At(a, 2)).Select(Get)) list.AddRange(Profiles(p, depth + 1));
                    return list;
                }
                if (e.Type == "IFCDERIVEDPROFILEDEF" || e.Type == "IFCMIRROREDPROFILEDEF")
                {
                    var op = Operator(Get(At(a, 3)));
                    foreach (var p in Profiles(Get(At(a, 2)), depth + 1))
                    {
                        var q = new Profile { Outer = p.Outer.Select(op.Apply).ToList(), Holes = p.Holes.Select(h => h.Select(op.Apply).ToList()).ToList() };
                        list.Add(Oriented(q));
                    }
                    return list;
                }
                var profile = new Profile();
                Func<int, double> L = i => Num(At(a, i)) * _len;
                switch (e.Type)
                {
                    case "IFCARBITRARYCLOSEDPROFILEDEF":
                    case "IFCARBITRARYPROFILEDEFWITHVOIDS":
                        profile.Outer = Clean(Curve(Get(At(a, 2)))).Select(p => new V3(p.X, p.Y, 0)).ToList();
                        if (e.Type == "IFCARBITRARYPROFILEDEFWITHVOIDS")
                            foreach (var h in AsList(At(a, 3)).Select(Get))
                                profile.Holes.Add(Clean(Curve(h)).Select(p => new V3(p.X, p.Y, 0)).ToList());
                        break;
                    case "IFCRECTANGLEPROFILEDEF":
                    case "IFCROUNDEDRECTANGLEPROFILEDEF":
                        profile.Outer = Rect(L(3), L(4));
                        break;
                    case "IFCRECTANGLEHOLLOWPROFILEDEF":
                        {
                            var w = L(3); var h = L(4); var t = L(5);
                            profile.Outer = Rect(w, h);
                            if (t > 0 && 2 * t < Math.Min(w, h)) profile.Holes.Add(Rect(w - 2 * t, h - 2 * t));
                            break;
                        }
                    case "IFCCIRCLEPROFILEDEF":
                        profile.Outer = Circle(new V3(0, 0, 0), L(3), CircleSegments(L(3)));
                        break;
                    case "IFCCIRCLEHOLLOWPROFILEDEF":
                        {
                            var r = L(3); var t = L(4);
                            profile.Outer = Circle(new V3(0, 0, 0), r, CircleSegments(r));
                            if (t > 0 && t < r) profile.Holes.Add(Circle(new V3(0, 0, 0), r - t, CircleSegments(r)));
                            break;
                        }
                    case "IFCELLIPSEPROFILEDEF":
                        {
                            var r1 = L(3); var r2 = L(4);
                            var n = CircleSegments(Math.Max(r1, r2));
                            for (var k = 0; k < n; k++) { var t = 2 * Math.PI * k / n; profile.Outer.Add(new V3(r1 * Math.Cos(t), r2 * Math.Sin(t), 0)); }
                            break;
                        }
                    case "IFCISHAPEPROFILEDEF":
                    case "IFCASYMMETRICISHAPEPROFILEDEF":
                        {
                            var w = L(3); var d = L(4); var tw = L(5); var tf = L(6);
                            if (tf <= 0 || tw <= 0) { profile.Outer = Rect(w, d); break; }
                            profile.Outer = new List<V3>
                            {
                                new V3(-w / 2, -d / 2, 0), new V3(w / 2, -d / 2, 0), new V3(w / 2, -d / 2 + tf, 0), new V3(tw / 2, -d / 2 + tf, 0),
                                new V3(tw / 2, d / 2 - tf, 0), new V3(w / 2, d / 2 - tf, 0), new V3(w / 2, d / 2, 0), new V3(-w / 2, d / 2, 0),
                                new V3(-w / 2, d / 2 - tf, 0), new V3(-tw / 2, d / 2 - tf, 0), new V3(-tw / 2, -d / 2 + tf, 0), new V3(-w / 2, -d / 2 + tf, 0)
                            };
                            break;
                        }
                    case "IFCLSHAPEPROFILEDEF":
                        {
                            var d = L(3); var w = L(4); var t = L(5);
                            if (w <= 0) w = d;
                            if (t <= 0) { profile.Outer = Rect(w, d); break; }
                            profile.Outer = new List<V3> { new V3(-w / 2, -d / 2, 0), new V3(w / 2, -d / 2, 0), new V3(w / 2, -d / 2 + t, 0), new V3(-w / 2 + t, -d / 2 + t, 0), new V3(-w / 2 + t, d / 2, 0), new V3(-w / 2, d / 2, 0) };
                            break;
                        }
                    case "IFCTSHAPEPROFILEDEF":
                        {
                            var d = L(3); var w = L(4); var tw = L(5); var tf = L(6);
                            if (tf <= 0 || tw <= 0) { profile.Outer = Rect(w, d); break; }
                            profile.Outer = new List<V3> { new V3(-tw / 2, -d / 2, 0), new V3(tw / 2, -d / 2, 0), new V3(tw / 2, d / 2 - tf, 0), new V3(w / 2, d / 2 - tf, 0), new V3(w / 2, d / 2, 0), new V3(-w / 2, d / 2, 0), new V3(-w / 2, d / 2 - tf, 0), new V3(-tw / 2, d / 2 - tf, 0) };
                            break;
                        }
                    case "IFCUSHAPEPROFILEDEF":
                        {
                            var d = L(3); var w = L(4); var tw = L(5); var tf = L(6);
                            if (tf <= 0 || tw <= 0) { profile.Outer = Rect(w, d); break; }
                            profile.Outer = new List<V3> { new V3(-w / 2, -d / 2, 0), new V3(w / 2, -d / 2, 0), new V3(w / 2, -d / 2 + tf, 0), new V3(-w / 2 + tw, -d / 2 + tf, 0), new V3(-w / 2 + tw, d / 2 - tf, 0), new V3(w / 2, d / 2 - tf, 0), new V3(w / 2, d / 2, 0), new V3(-w / 2, d / 2, 0) };
                            break;
                        }
                    case "IFCCSHAPEPROFILEDEF":
                        {
                            var d = L(3); var w = L(4); var t = L(5); var g = L(6);
                            if (t <= 0) { profile.Outer = Rect(w, d); break; }
                            g = Math.Max(g, t);
                            profile.Outer = new List<V3>
                            {
                                new V3(-w / 2, -d / 2, 0), new V3(w / 2, -d / 2, 0), new V3(w / 2, -d / 2 + g, 0), new V3(w / 2 - t, -d / 2 + g, 0),
                                new V3(w / 2 - t, -d / 2 + t, 0), new V3(-w / 2 + t, -d / 2 + t, 0), new V3(-w / 2 + t, d / 2 - t, 0), new V3(w / 2 - t, d / 2 - t, 0),
                                new V3(w / 2 - t, d / 2 - g, 0), new V3(w / 2, d / 2 - g, 0), new V3(w / 2, d / 2, 0), new V3(-w / 2, d / 2, 0)
                            };
                            break;
                        }
                    case "IFCZSHAPEPROFILEDEF":
                        {
                            var d = L(3); var w = L(4); var tw = L(5); var tf = L(6);
                            if (tf <= 0 || tw <= 0) { profile.Outer = Rect(w, d); break; }
                            profile.Outer = new List<V3> { new V3(-w + tw / 2, -d / 2, 0), new V3(tw / 2, -d / 2, 0), new V3(tw / 2, d / 2 - tf, 0), new V3(w - tw / 2, d / 2 - tf, 0), new V3(w - tw / 2, d / 2, 0), new V3(-tw / 2, d / 2, 0), new V3(-tw / 2, -d / 2 + tf, 0), new V3(-w + tw / 2, -d / 2 + tf, 0) };
                            break;
                        }
                    case "IFCCENTERLINEPROFILEDEF":
                        {
                            // an open curve with a thickness: a band round it
                            var c = Clean(Curve(Get(At(a, 2)))).Select(p => new V3(p.X, p.Y, 0)).ToList();
                            var t = L(3);
                            if (c.Count < 2 || t <= 0) return list;
                            var left = new List<V3>();
                            var right = new List<V3>();
                            for (var k = 0; k < c.Count; k++)
                            {
                                var dir = (c[Math.Min(k + 1, c.Count - 1)] - c[Math.Max(k - 1, 0)]).Unit();
                                var nrm = new V3(-dir.Y, dir.X, 0) * (t / 2);
                                left.Add(c[k] + nrm);
                                right.Add(c[k] - nrm);
                            }
                            right.Reverse();
                            profile.Outer = left.Concat(right).ToList();
                            break;
                        }
                    default:
                        Warn(Char(e.Type.Substring(Math.Min(3, e.Type.Length))) + " profiles");
                        return list;
                }
                if (profile.Outer.Count < 3) return list;
                // the parameterized profiles have their own position in the plane
                if (e.Type != "IFCARBITRARYCLOSEDPROFILEDEF" && e.Type != "IFCARBITRARYPROFILEDEFWITHVOIDS" && e.Type != "IFCCENTERLINEPROFILEDEF")
                {
                    var pos = Get(At(a, 2));
                    if (pos != null)
                    {
                        var xf = Axis2(pos);
                        profile.Outer = profile.Outer.Select(xf.Apply).ToList();
                        profile.Holes = profile.Holes.Select(h => h.Select(xf.Apply).ToList()).ToList();
                    }
                }
                list.Add(Oriented(profile));
                return list;
            }

            static Profile Oriented(Profile p)
            {
                if (Area2(p.Outer) < 0) p.Outer.Reverse();
                foreach (var h in p.Holes) if (Area2(h) > 0) h.Reverse();
                p.Holes = p.Holes.Where(h => h.Count >= 3).ToList();
                return p;
            }

            /// <summary>The points of a curve (a 2D one with z = 0), in the frame of the curve.</summary>
            List<V3> Curve(Entity e, int depth = 0)
            {
                var pts = new List<V3>();
                if (e == null || depth > 12) return pts;
                var a = Args(e);
                switch (e.Type)
                {
                    case "IFCPOLYLINE":
                        pts.AddRange(AsList(At(a, 0)).Select(x => Point(Get(x))));
                        break;
                    case "IFCINDEXEDPOLYCURVE":
                        {
                            var p = PointList(Get(At(a, 0)));
                            var segs = At(a, 1) as List<object>;
                            if (segs == null || segs.Count == 0) { pts.AddRange(p); break; }
                            foreach (var s in segs)
                            {
                                var ts = s as Typed;
                                if (ts == null) continue;
                                var idx = AsList(ts.V is List<object> ? ts.V : new List<object> { ts.V }).Select(x => (int)Num(x) - 1).Where(k => k >= 0 && k < p.Count).ToList();
                                if (ts.Type == "IFCARCINDEX" && idx.Count == 3) Append(pts, Arc3(p[idx[0]], p[idx[1]], p[idx[2]]));
                                else Append(pts, idx.Select(k => p[k]).ToList());
                            }
                            break;
                        }
                    case "IFCCOMPOSITECURVE":
                    case "IFCCOMPOSITECURVEONSURFACE":
                    case "IFCGRADIENTCURVE":
                    case "IFCSEGMENTEDREFERENCECURVE":
                        foreach (var seg in AsList(At(a, 0)).Select(Get).Where(x => x != null))
                        {
                            var sa = Args(seg);
                            var sameSense = Bool(At(sa, 1)) != false;
                            var c = Curve(Get(At(sa, 2)), depth + 1);
                            if (!sameSense) c.Reverse();
                            Append(pts, c);
                        }
                        break;
                    case "IFCTRIMMEDCURVE":
                        pts.AddRange(Trimmed(e, depth));
                        break;
                    case "IFCCIRCLE":
                        {
                            var xf = Axis2(Get(At(a, 0)));
                            var r = Num(At(a, 1)) * _len;
                            pts.AddRange(Circle(new V3(0, 0, 0), r, CircleSegments(r)).Select(xf.Apply));
                            break;
                        }
                    case "IFCELLIPSE":
                        {
                            var xf = Axis2(Get(At(a, 0)));
                            var r1 = Num(At(a, 1)) * _len; var r2 = Num(At(a, 2)) * _len;
                            var n = CircleSegments(Math.Max(r1, r2));
                            for (var k = 0; k < n; k++) { var t = 2 * Math.PI * k / n; pts.Add(xf.Apply(new V3(r1 * Math.Cos(t), r2 * Math.Sin(t), 0))); }
                            break;
                        }
                    case "IFCBSPLINECURVEWITHKNOTS":
                    case "IFCRATIONALBSPLINECURVEWITHKNOTS":
                    case "IFCBEZIERCURVE":
                        pts.AddRange(AsList(At(a, 1)).Select(x => Point(Get(x))));
                        break;
                    default:
                        Warn(Char(e.Type.Substring(Math.Min(3, e.Type.Length))) + " curves");
                        break;
                }
                return pts;
            }

            static void Append(List<V3> to, List<V3> pts)
            {
                foreach (var p in pts)
                    if (to.Count == 0 || (p - to[to.Count - 1]).Length > 1e-9) to.Add(p);
            }

            /// <summary>The arc from a through b to c.</summary>
            static List<V3> Arc3(V3 a, V3 b, V3 c)
            {
                var ab = b - a; var ac = c - a;
                var n = ab.Cross(ac);
                if (n.Length < 1e-14) return new List<V3> { a, b, c };
                // the centre of the circle through the three points
                var centre = a + (n.Cross(ab) * ac.Dot(ac) + ac.Cross(n) * ab.Dot(ab)) * (1.0 / (2 * n.Dot(n)));
                var r = (a - centre).Length;
                var u = (a - centre).Unit();
                var w = n.Unit();
                var v = w.Cross(u);
                Func<V3, double> angle = p => { var d = p - centre; var t = Math.Atan2(d.Dot(v), d.Dot(u)); return t < 0 ? t + 2 * Math.PI : t; };
                var tc = angle(c);
                var steps = Math.Max(2, (int)Math.Ceiling(tc / (Math.PI / 18)));
                var pts = new List<V3>();
                for (var k = 0; k <= steps; k++)
                {
                    var t = tc * k / steps;
                    pts.Add(centre + u * (r * Math.Cos(t)) + v * (r * Math.Sin(t)));
                }
                return pts;
            }

            List<V3> Trimmed(Entity e, int depth)
            {
                var a = Args(e);
                var basis = Get(At(a, 0));
                var sense = Bool(At(a, 3)) != false;
                if (basis == null) return new List<V3>();
                var ba = Args(basis);
                if (basis.Type == "IFCLINE")
                {
                    var p0 = Point(Get(At(ba, 0)));
                    var vec = Get(At(ba, 1));
                    var dir = Direction(vec, new V3(1, 0, 0));
                    var mag = vec != null && vec.Type == "IFCVECTOR" ? Num(At(Args(vec), 1), 1) * _len : _len;
                    Func<object, V3> at = trim =>
                    {
                        foreach (var t in AsList(trim))
                        {
                            var pe = Get(t);
                            if (pe != null && pe.Type == "IFCCARTESIANPOINT") return Point(pe);
                        }
                        foreach (var t in AsList(trim))
                        {
                            var tv = t as Typed;
                            if (tv != null && tv.Type == "IFCPARAMETERVALUE") return p0 + dir * (Num(tv.V) * mag);
                        }
                        return p0;
                    };
                    return new List<V3> { at(At(a, 1)), at(At(a, 2)) };
                }
                if (basis.Type == "IFCCIRCLE" || basis.Type == "IFCELLIPSE")
                {
                    var xf = Axis2(Get(At(ba, 0)));
                    var r1 = Num(At(ba, 1)) * _len;
                    var r2 = basis.Type == "IFCELLIPSE" ? Num(At(ba, 2)) * _len : r1;
                    // the frame of the circle, to turn trimming points into angles
                    var o = xf.Apply(new V3(0, 0, 0));
                    var ux = xf.Apply(new V3(1, 0, 0)) - o;
                    var uy = xf.Apply(new V3(0, 1, 0)) - o;
                    Func<object, double> angleOf = trim =>
                    {
                        foreach (var t in AsList(trim))
                        {
                            var tv = t as Typed;
                            if (tv != null && tv.Type == "IFCPARAMETERVALUE") return Num(tv.V) * _angle;
                        }
                        foreach (var t in AsList(trim))
                        {
                            var pe = Get(t);
                            if (pe != null && pe.Type == "IFCCARTESIANPOINT")
                            {
                                var d = Point(pe) - o;
                                return Math.Atan2(d.Dot(uy) / Math.Max(r2, 1e-12), d.Dot(ux) / Math.Max(r1, 1e-12));
                            }
                        }
                        return 0;
                    };
                    var t1 = angleOf(At(a, 1));
                    var t2 = angleOf(At(a, 2));
                    double sweep;
                    if (sense) { sweep = t2 - t1; while (sweep <= 1e-9) sweep += 2 * Math.PI; }
                    else { sweep = t2 - t1; while (sweep >= -1e-9) sweep -= 2 * Math.PI; }
                    var steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 18)));
                    var pts = new List<V3>();
                    for (var k = 0; k <= steps; k++)
                    {
                        var t = t1 + sweep * k / steps;
                        pts.Add(xf.Apply(new V3(r1 * Math.Cos(t), r2 * Math.Sin(t), 0)));
                    }
                    return pts;
                }
                var c = Curve(basis, depth + 1);
                if (!sense) c.Reverse();
                return c;
            }

            // ---------------------------------------------------------------- solids

            /// <summary>A profile pulled along a vector: the two caps and the walls of every loop.</summary>
            static void Extrude(Mesh m, Profile p, V3 v)
            {
                var loops = new List<List<V3>> { p.Outer };
                loops.AddRange(p.Holes);
                var tris = Triangulate(p.Outer, p.Holes);
                var flat = loops.SelectMany(x => x).ToList();
                var bottom = m.P.Count;
                foreach (var q in flat) m.Add(q);
                var top = m.P.Count;
                foreach (var q in flat) m.Add(q + v);
                for (var k = 0; k + 2 < tris.Count; k += 3)
                {
                    m.Tri(bottom + tris[k], bottom + tris[k + 2], bottom + tris[k + 1]);
                    m.Tri(top + tris[k], top + tris[k + 1], top + tris[k + 2]);
                }
                foreach (var loop in loops)
                {
                    var n = loop.Count;
                    for (var k = 0; k < n; k++)
                    {
                        var k1 = (k + 1) % n;
                        var a0 = m.Add(loop[k]); var b0 = m.Add(loop[k1]);
                        var b1 = m.Add(loop[k1] + v); var a1 = m.Add(loop[k] + v);
                        m.Tri(a0, b0, b1);
                        m.Tri(a0, b1, a1);
                    }
                }
            }

            /// <summary>A profile turned round an axis in its plane, with caps when the turn is not whole.</summary>
            static void Revolve(Mesh m, Profile p, V3 origin, V3 axis, double angle)
            {
                if (Math.Abs(angle) < 1e-9) return;
                var whole = Math.Abs(angle) >= 2 * Math.PI - 1e-6;
                if (whole) angle = 2 * Math.PI * Math.Sign(angle);
                var steps = Math.Max(3, (int)Math.Ceiling(Math.Abs(angle) / (2 * Math.PI) * 32));
                axis = axis.Unit();
                Func<V3, double, V3> turn = (q, t) =>
                {
                    var d = q - origin;
                    var along = axis * d.Dot(axis);
                    var perp = d - along;
                    return origin + along + perp * Math.Cos(t) + axis.Cross(perp) * Math.Sin(t);
                };
                var loops = new List<List<V3>> { p.Outer };
                loops.AddRange(p.Holes);
                foreach (var loop in loops)
                {
                    var n = loop.Count;
                    var rings = new List<int[]>();
                    for (var s = 0; s <= steps; s++)
                    {
                        if (whole && s == steps) { rings.Add(rings[0]); break; }
                        var t = angle * s / steps;
                        rings.Add(loop.Select(q => m.Add(turn(q, t))).ToArray());
                    }
                    for (var s = 0; s < steps; s++)
                        for (var k = 0; k < n; k++)
                        {
                            var k1 = (k + 1) % n;
                            m.Tri(rings[s][k], rings[s][k1], rings[s + 1][k1]);
                            m.Tri(rings[s][k], rings[s + 1][k1], rings[s + 1][k]);
                        }
                }
                if (!whole)
                {
                    var tris = Triangulate(p.Outer, p.Holes);
                    var flat = loops.SelectMany(x => x).ToList();
                    var start = m.P.Count;
                    foreach (var q in flat) m.Add(q);
                    var end = m.P.Count;
                    foreach (var q in flat) m.Add(turn(q, angle));
                    for (var k = 0; k + 2 < tris.Count; k += 3)
                    {
                        m.Tri(start + tris[k], start + tris[k + 2], start + tris[k + 1]);
                        m.Tri(end + tris[k], end + tris[k + 1], end + tris[k + 2]);
                    }
                }
            }

            /// <summary>
            /// A profile swept along a path: the frame goes along by parallel transport (or keeps the
            /// fixed reference), and at each bend the section lies in the plane that halves the bend.
            /// </summary>
            static void Sweep(Mesh m, Profile p, List<V3> path, V3? fixedRef)
            {
                path = Clean(path);
                if (path.Count < 2) return;
                var t0 = (path[1] - path[0]).Unit();
                V3 nrm;
                if (fixedRef.HasValue && (fixedRef.Value - t0 * fixedRef.Value.Dot(t0)).Length > 1e-6) nrm = (fixedRef.Value - t0 * fixedRef.Value.Dot(t0)).Unit();
                else
                {
                    // the profile's y up where the path allows it
                    var up = new V3(0, 0, 1);
                    var side = t0.Cross(up);
                    nrm = side.Length > 1e-6 ? side.Unit() * -1 : new V3(1, 0, 0);
                    nrm = (nrm - t0 * nrm.Dot(t0)).Unit();
                }
                var bin = t0.Cross(nrm);
                var loops = new List<List<V3>> { p.Outer };
                loops.AddRange(p.Holes);
                var flat = loops.SelectMany(x => x).ToList();
                var rings = new List<int[]>();
                Func<V3, V3, V3, int[]> ringAt = (centre, n, b) => flat.Select(q => m.Add(centre + n * q.X + b * q.Y)).ToArray();
                rings.Add(ringAt(path[0], nrm, bin));
                var tPrev = t0;
                for (var i = 1; i < path.Count; i++)
                {
                    var last = i == path.Count - 1;
                    if (last)
                    {
                        rings.Add(ringAt(path[i], nrm, bin));
                        break;
                    }
                    var tNext = (path[i + 1] - path[i]).Unit();
                    var miter = (tPrev + tNext).Unit();
                    if (miter.Length < 0.5) miter = tNext;
                    var cosHalf = tPrev.Dot(miter);
                    var ring = flat.Select(q =>
                    {
                        var o = nrm * q.X + bin * q.Y;
                        var shift = cosHalf > 1e-3 ? o.Dot(miter) / cosHalf : 0;
                        return m.Add(path[i] + o - tPrev * shift);
                    }).ToArray();
                    rings.Add(ring);
                    // carry the frame round the bend
                    var axis = tPrev.Cross(tNext);
                    var s = axis.Length;
                    if (s > 1e-9)
                    {
                        axis = axis * (1 / s);
                        var ang = Math.Atan2(s, tPrev.Dot(tNext));
                        Func<V3, V3> rot = x => x * Math.Cos(ang) + axis.Cross(x) * Math.Sin(ang) + axis * (axis.Dot(x) * (1 - Math.Cos(ang)));
                        nrm = rot(nrm).Unit();
                        bin = tNext.Cross(nrm).Unit();
                    }
                    tPrev = tNext;
                }
                var offset = 0;
                foreach (var loop in loops)
                {
                    var n = loop.Count;
                    for (var r = 0; r + 1 < rings.Count; r++)
                        for (var k = 0; k < n; k++)
                        {
                            var k1 = (k + 1) % n;
                            m.Tri(rings[r][offset + k], rings[r][offset + k1], rings[r + 1][offset + k1]);
                            m.Tri(rings[r][offset + k], rings[r + 1][offset + k1], rings[r + 1][offset + k]);
                        }
                    offset += n;
                }
                // the caps; the mesh is turned to face out afterwards
                var tris = Triangulate(p.Outer, p.Holes);
                var a = rings[0];
                var z = rings[rings.Count - 1];
                for (var k = 0; k + 2 < tris.Count; k += 3)
                {
                    m.Tri(a[tris[k]], a[tris[k + 2]], a[tris[k + 1]]);
                    m.Tri(z[tris[k]], z[tris[k + 1]], z[tris[k + 2]]);
                }
            }

            static void Sphere(Mesh m, double r)
            {
                var lon = CircleSegments(r);
                var lat = Math.Max(6, lon / 2);
                var top = m.Add(new V3(0, 0, r));
                var rows = new List<int[]>();
                for (var i = 1; i < lat; i++)
                {
                    var phi = Math.PI * i / lat;
                    var row = new int[lon];
                    for (var k = 0; k < lon; k++)
                    {
                        var th = 2 * Math.PI * k / lon;
                        row[k] = m.Add(new V3(r * Math.Sin(phi) * Math.Cos(th), r * Math.Sin(phi) * Math.Sin(th), r * Math.Cos(phi)));
                    }
                    rows.Add(row);
                }
                var bottom = m.Add(new V3(0, 0, -r));
                for (var k = 0; k < lon; k++)
                {
                    var k1 = (k + 1) % lon;
                    m.Tri(top, rows[0][k], rows[0][k1]);
                    m.Tri(bottom, rows[rows.Count - 1][k1], rows[rows.Count - 1][k]);
                    for (var i = 0; i + 1 < rows.Count; i++)
                    {
                        m.Tri(rows[i][k], rows[i + 1][k], rows[i + 1][k1]);
                        m.Tri(rows[i][k], rows[i + 1][k1], rows[i][k1]);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- triangulation

        /// <summary>
        /// Triangulates a polygon with holes (outer loop counter-clockwise, holes clockwise) by joining
        /// each hole to the outer loop with a bridge and clipping ears. The indices count the outer
        /// loop's corners first, then each hole's in order.
        /// </summary>
        static List<int> Triangulate(List<V3> outer, List<List<V3>> holes)
        {
            var pts = new List<V3>(outer);
            var ring = Enumerable.Range(0, outer.Count).ToList();
            var holeStarts = new List<int>();
            foreach (var h in holes) { holeStarts.Add(pts.Count); pts.AddRange(h); }
            if (outer.Count > 4000)
            {
                var fan = new List<int>();
                for (var k = 1; k + 1 < outer.Count; k++) { fan.Add(0); fan.Add(k); fan.Add(k + 1); }
                return fan;
            }
            // bridges, from the hole that reaches furthest right
            var order = Enumerable.Range(0, holes.Count).OrderByDescending(h => holes[h].Count == 0 ? double.MinValue : holes[h].Max(p => p.X)).ToList();
            foreach (var h in order)
            {
                var hole = holes[h];
                if (hole.Count < 3) continue;
                var start = holeStarts[h];
                var mi = 0;
                for (var k = 1; k < hole.Count; k++) if (hole[k].X > hole[mi].X) mi = k;
                var mp = pts[start + mi];
                var best = -1;
                var bestD = double.MaxValue;
                for (var r = 0; r < ring.Count; r++)
                {
                    var q = pts[ring[r]];
                    var d = (q - mp).Length;
                    if (d >= bestD) continue;
                    if (!Visible(pts, ring, mp, q, ring[r])) continue;
                    best = r;
                    bestD = d;
                }
                if (best < 0)
                {
                    for (var r = 0; r < ring.Count; r++) { var d = (pts[ring[r]] - mp).Length; if (d < bestD) { bestD = d; best = r; } }
                }
                var insert = new List<int> { ring[best] };
                for (var k = 0; k <= hole.Count; k++) insert.Add(start + (mi + k) % hole.Count);
                ring.InsertRange(best + 1, insert);
            }
            return EarClip(pts, ring);
        }

        static bool Visible(List<V3> pts, List<int> ring, V3 a, V3 b, int bIndex)
        {
            for (var k = 0; k < ring.Count; k++)
            {
                var i = ring[k]; var j = ring[(k + 1) % ring.Count];
                if (i == bIndex || j == bIndex) continue;
                if (SegmentsCross(a, b, pts[i], pts[j])) return false;
            }
            return true;
        }

        static double Cross2(V3 o, V3 a, V3 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        static bool SegmentsCross(V3 a, V3 b, V3 c, V3 d)
        {
            var d1 = Cross2(c, d, a); var d2 = Cross2(c, d, b);
            var d3 = Cross2(a, b, c); var d4 = Cross2(a, b, d);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        static List<int> EarClip(List<V3> pts, List<int> ring)
        {
            var result = new List<int>();
            var poly = new List<int>(ring);
            var guard = 0;
            while (poly.Count > 3 && guard++ < 100000)
            {
                var clipped = false;
                for (var k = 0; k < poly.Count; k++)
                {
                    var ia = poly[(k + poly.Count - 1) % poly.Count];
                    var ib = poly[k];
                    var ic = poly[(k + 1) % poly.Count];
                    var a = pts[ia]; var b = pts[ib]; var c = pts[ic];
                    var cross = Cross2(a, b, c);
                    if (cross <= 1e-18) continue;
                    var inside = false;
                    for (var j = 0; j < poly.Count && !inside; j++)
                    {
                        var ip = poly[j];
                        if (ip == ia || ip == ib || ip == ic) continue;
                        var p = pts[ip];
                        if ((p - a).Length < 1e-12 || (p - b).Length < 1e-12 || (p - c).Length < 1e-12) continue;
                        if (Cross2(a, b, p) >= 0 && Cross2(b, c, p) >= 0 && Cross2(c, a, p) >= 0) inside = true;
                    }
                    if (inside) continue;
                    result.Add(ia); result.Add(ib); result.Add(ic);
                    poly.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    // a degenerate or self-touching polygon: the rest as a fan
                    for (var k = 1; k + 1 < poly.Count; k++) { result.Add(poly[0]); result.Add(poly[k]); result.Add(poly[k + 1]); }
                    return result;
                }
            }
            if (poly.Count == 3) { result.Add(poly[0]); result.Add(poly[1]); result.Add(poly[2]); }
            return result;
        }
    }
}
