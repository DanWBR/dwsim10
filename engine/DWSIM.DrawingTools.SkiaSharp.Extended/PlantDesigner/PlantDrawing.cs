using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    /// <summary>What a leg of the drawing is: a straight run of pipe, or a component (pump, valve...) between two
    /// points, which the host turns into its own block.</summary>
    public enum LegKind { Pipe = 0, Component = 1 }

    /// <summary>The pipe a leg is drawn with: size, material and line tag. The host decides what the pipe type means
    /// (a water pipe or a multiphase pipe in a network, a section of a pipe segment).</summary>
    public sealed class PipePen
    {
        public string PipeType = "";
        /// <summary>The size as the user knows it ("4 in STD", "DN 100").</summary>
        public string Size = "4\" STD";
        public double InnerDiameterM = 0.1023;
        public double OuterDiameterM = 0.1143;
        public string Material = "Carbon Steel";
        public string LineTag = "";
        public double RoughnessM = 4.6e-5;
        public double HazenWilliamsC = 120.0;

        public PipePen Clone() { return (PipePen)MemberwiseClone(); }

        public bool SameAs(PipePen o)
        {
            return o != null && PipeType == o.PipeType && Size == o.Size && Math.Abs(InnerDiameterM - o.InnerDiameterM) < 1e-9
                && Math.Abs(OuterDiameterM - o.OuterDiameterM) < 1e-9 && Material == o.Material && LineTag == o.LineTag
                && Math.Abs(RoughnessM - o.RoughnessM) < 1e-12 && Math.Abs(HazenWilliamsC - o.HazenWilliamsC) < 1e-9;
        }

        internal XElement Save(string name)
        {
            return new XElement(name,
                new XAttribute("Type", PipeType ?? ""), new XAttribute("Size", Size ?? ""),
                new XAttribute("ID", D(InnerDiameterM)), new XAttribute("OD", D(OuterDiameterM)),
                new XAttribute("Material", Material ?? ""), new XAttribute("LineTag", LineTag ?? ""),
                new XAttribute("Roughness", D(RoughnessM)), new XAttribute("C", D(HazenWilliamsC)));
        }

        internal static PipePen Load(XElement x)
        {
            var p = new PipePen();
            if (x == null) return p;
            p.PipeType = S(x, "Type", p.PipeType); p.Size = S(x, "Size", p.Size);
            p.InnerDiameterM = N(x, "ID", p.InnerDiameterM); p.OuterDiameterM = N(x, "OD", p.OuterDiameterM);
            p.Material = S(x, "Material", p.Material); p.LineTag = S(x, "LineTag", p.LineTag);
            p.RoughnessM = N(x, "Roughness", p.RoughnessM); p.HazenWilliamsC = N(x, "C", p.HazenWilliamsC);
            return p;
        }

        internal static string D(double v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        internal static string S(XElement x, string a, string def) { var t = x.Attribute(a); return t != null ? t.Value : def; }
        internal static double N(XElement x, string a, double def)
        {
            var t = x.Attribute(a); double v;
            return t != null && double.TryParse(t.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
        }
        internal static int I(XElement x, string a, int def)
        {
            var t = x.Attribute(a); int v;
            return t != null && int.TryParse(t.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : def;
        }
    }

    /// <summary>A point of the drawing: a pipe end, a corner, a branch or the end of a component.</summary>
    public sealed class PlantVertex
    {
        public int Id;
        public V3 P;
        /// <summary>The host block this point became (a network node); empty before the first conversion.</summary>
        public string BlockName = "";
        public string Tag = "";
    }

    /// <summary>A straight leg between two points: pipe, or a component drawn from A (inlet) to B (outlet).</summary>
    public sealed class PlantLeg
    {
        public int Id;
        public int A, B;
        public LegKind Kind = LegKind.Pipe;
        public PipePen Pen = new PipePen();
        /// <summary>The kind of component (host-defined, such as "Pump" or "Valve"); empty for a pipe.</summary>
        public string ComponentKind = "";
        /// <summary>The host block this leg belongs to. The legs of one pipe run share it.</summary>
        public string BlockName = "";
        public string Tag = "";
    }

    /// <summary>A terminal item on a point (a hydrant, a nozzle, a tank, a source or a sink).</summary>
    public sealed class PlantItem
    {
        public int Id;
        public int Vertex;
        public string Kind = "";
        public string BlockName = "";
        public string Tag = "";
    }

    /// <summary>The cube grid the drawing snaps to.</summary>
    public sealed class PlantGrid
    {
        /// <summary>Length of a horizontal cube edge, m.</summary>
        public double StepH = 5.0;
        /// <summary>Length of a vertical cube edge, m.</summary>
        public double StepV = 5.0;
        /// <summary>Elevation of level 0, m.</summary>
        public double BaseZ = 0.0;
        /// <summary>The working level: new points go to the elevation BaseZ + Level x StepV.</summary>
        public int Level = 0;
        /// <summary>Lengths snap to whole cube edges while drawing.</summary>
        public bool Snap = true;

        public double LevelZ { get { return BaseZ + Level * StepV; } }

        public double Step(Axis a) { return a == Axis.Z ? StepV : StepH; }
    }

    /// <summary>
    /// A pipe drawing on the plant: points, straight legs between them (pipes or components) and terminal items.
    /// Host-agnostic: a pipe network turns it into nodes, pipes and blocks, a pipe segment into profile sections.
    /// </summary>
    public sealed class PlantDrawing
    {
        public readonly List<PlantVertex> Vertices = new List<PlantVertex>();
        public readonly List<PlantLeg> Legs = new List<PlantLeg>();
        public readonly List<PlantItem> Items = new List<PlantItem>();
        public PlantGrid Grid = new PlantGrid();
        /// <summary>The pen new legs are drawn with.</summary>
        public PipePen Pen = new PipePen();
        /// <summary>The host blocks the last conversion created or adopted, so that the next one can remove the
        /// blocks of the elements deleted since.</summary>
        public readonly List<string> OwnedBlocks = new List<string>();
        private int _next = 1;

        /// <summary>Points closer than this are one point, m.</summary>
        public const double Tolerance = 1e-3;

        public bool IsEmpty { get { return Vertices.Count == 0 && Legs.Count == 0 && Items.Count == 0; } }

        public PlantVertex Vertex(int id) { return Vertices.FirstOrDefault(v => v.Id == id); }
        public PlantLeg Leg(int id) { return Legs.FirstOrDefault(l => l.Id == id); }
        public PlantItem Item(int id) { return Items.FirstOrDefault(i => i.Id == id); }

        public V3 P(int vertexId) { return Vertex(vertexId).P; }

        public double Length(PlantLeg l) { return V3.Distance(P(l.A), P(l.B)); }

        public int Other(PlantLeg l, int v) { return l.A == v ? l.B : l.A; }

        public PlantVertex AddVertex(V3 p)
        {
            var v = new PlantVertex { Id = _next++, P = p };
            Vertices.Add(v);
            return v;
        }

        /// <summary>The point at p within the tolerance; null when there is none.</summary>
        public PlantVertex FindVertex(V3 p, double tol = Tolerance)
        {
            PlantVertex best = null; double bd = tol;
            foreach (var v in Vertices)
            {
                double d = V3.Distance(v.P, p);
                if (d <= bd) { bd = d; best = v; }
            }
            return best;
        }

        /// <summary>The point at p, created when there is none.</summary>
        public PlantVertex VertexAt(V3 p)
        {
            return FindVertex(p) ?? AddVertex(p);
        }

        public PlantLeg AddLeg(int a, int b, LegKind kind = LegKind.Pipe, PipePen pen = null)
        {
            if (a == b) throw new ArgumentException("A leg needs two different points.");
            var l = new PlantLeg { Id = _next++, A = a, B = b, Kind = kind, Pen = (pen ?? Pen).Clone() };
            Legs.Add(l);
            return l;
        }

        public PlantItem AddItem(int vertex, string kind)
        {
            var i = new PlantItem { Id = _next++, Vertex = vertex, Kind = kind ?? "" };
            Items.Add(i);
            return i;
        }

        public IEnumerable<PlantLeg> LegsAt(int v) { return Legs.Where(l => l.A == v || l.B == v); }

        public IEnumerable<PlantItem> ItemsAt(int v) { return Items.Where(i => i.Vertex == v); }

        public int Degree(int v) { return Legs.Count(l => l.A == v || l.B == v); }

        public int PipeDegree(int v) { return Legs.Count(l => l.Kind == LegKind.Pipe && (l.A == v || l.B == v)); }

        /// <summary>The parameter (0 to 1) of the point of the leg nearest to p, and the distance to it.</summary>
        public double Nearest(PlantLeg l, V3 p, out double distance)
        {
            V3 a = P(l.A), b = P(l.B), ab = b - a;
            double l2 = V3.Dot(ab, ab);
            double t = l2 > 1e-24 ? Math.Max(0, Math.Min(1, V3.Dot(p - a, ab) / l2)) : 0;
            distance = V3.Distance(a + ab * t, p);
            return t;
        }

        /// <summary>The leg whose inside (not its ends) passes through p within the tolerance; null when none.</summary>
        public PlantLeg LegThrough(V3 p, double tol = Tolerance, LegKind? kind = LegKind.Pipe)
        {
            foreach (var l in Legs)
            {
                if (kind.HasValue && l.Kind != kind.Value) continue;
                double d; double t = Nearest(l, p, out d);
                if (d > tol) continue;
                double len = Length(l);
                if (t * len > tol && (1 - t) * len > tol) return l;
            }
            return null;
        }

        /// <summary>Splits a leg at a point of it: the leg keeps A to the new point, a copy runs from the new point to B
        /// (same kind, pen, block and tag). Returns the new point.</summary>
        public PlantVertex SplitLeg(PlantLeg l, V3 at)
        {
            var v = AddVertex(at);
            var tail = new PlantLeg { Id = _next++, A = v.Id, B = l.B, Kind = l.Kind, Pen = l.Pen.Clone(), ComponentKind = l.ComponentKind, BlockName = l.BlockName, Tag = l.Tag };
            l.B = v.Id;
            Legs.Insert(Legs.IndexOf(l) + 1, tail);
            return v;
        }

        public void RemoveLeg(PlantLeg l) { Legs.Remove(l); }

        /// <summary>Removes a point with its legs and items.</summary>
        public void RemoveVertex(int v)
        {
            Legs.RemoveAll(l => l.A == v || l.B == v);
            Items.RemoveAll(i => i.Vertex == v);
            Vertices.RemoveAll(x => x.Id == v);
        }

        /// <summary>Removes the points that do not have legs or items.</summary>
        public int RemoveLoosePoints()
        {
            var used = new HashSet<int>(Legs.SelectMany(l => new[] { l.A, l.B }).Concat(Items.Select(i => i.Vertex)));
            return Vertices.RemoveAll(v => !used.Contains(v.Id));
        }

        /// <summary>The smallest box around the drawing; false when it is empty.</summary>
        public bool Bounds(out V3 min, out V3 max)
        {
            min = max = V3.Zero;
            if (Vertices.Count == 0) return false;
            min = new V3(Vertices.Min(v => v.P.X), Vertices.Min(v => v.P.Y), Vertices.Min(v => v.P.Z));
            max = new V3(Vertices.Max(v => v.P.X), Vertices.Max(v => v.P.Y), Vertices.Max(v => v.P.Z));
            return true;
        }

        // ------------------------------------------------------------------ persistence

        public XElement SaveData(string name = "PlantDrawing")
        {
            var x = new XElement(name, new XAttribute("Version", 1), new XAttribute("Next", _next));
            x.Add(new XElement("Grid", new XAttribute("StepH", PipePen.D(Grid.StepH)), new XAttribute("StepV", PipePen.D(Grid.StepV)),
                new XAttribute("BaseZ", PipePen.D(Grid.BaseZ)), new XAttribute("Level", Grid.Level), new XAttribute("Snap", Grid.Snap)));
            x.Add(Pen.Save("Pen"));
            var xv = new XElement("Vertices");
            foreach (var v in Vertices)
                xv.Add(new XElement("V", new XAttribute("Id", v.Id), new XAttribute("P", v.P.ToString()), new XAttribute("Block", v.BlockName ?? ""), new XAttribute("Tag", v.Tag ?? "")));
            x.Add(xv);
            var xl = new XElement("Legs");
            foreach (var l in Legs)
            {
                var e = l.Pen.Save("L");
                e.Add(new XAttribute("Id", l.Id), new XAttribute("A", l.A), new XAttribute("B", l.B), new XAttribute("Kind", l.Kind.ToString()),
                    new XAttribute("Component", l.ComponentKind ?? ""), new XAttribute("Block", l.BlockName ?? ""), new XAttribute("LegTag", l.Tag ?? ""));
                xl.Add(e);
            }
            x.Add(xl);
            var xi = new XElement("Items");
            foreach (var i in Items)
                xi.Add(new XElement("I", new XAttribute("Id", i.Id), new XAttribute("V", i.Vertex), new XAttribute("Kind", i.Kind ?? ""), new XAttribute("Block", i.BlockName ?? ""), new XAttribute("Tag", i.Tag ?? "")));
            x.Add(xi);
            x.Add(new XElement("Owned", OwnedBlocks.Select(b => new XElement("B", b))));
            return x;
        }

        public static PlantDrawing LoadData(XElement x)
        {
            var d = new PlantDrawing();
            if (x == null) return d;
            var g = x.Element("Grid");
            if (g != null)
            {
                d.Grid.StepH = PipePen.N(g, "StepH", d.Grid.StepH); d.Grid.StepV = PipePen.N(g, "StepV", d.Grid.StepV);
                d.Grid.BaseZ = PipePen.N(g, "BaseZ", d.Grid.BaseZ); d.Grid.Level = PipePen.I(g, "Level", 0);
                d.Grid.Snap = PipePen.S(g, "Snap", "true").ToLowerInvariant() != "false";
            }
            d.Pen = PipePen.Load(x.Element("Pen"));
            foreach (var e in x.Element("Vertices")?.Elements("V") ?? Enumerable.Empty<XElement>())
                d.Vertices.Add(new PlantVertex { Id = PipePen.I(e, "Id", 0), P = V3.Parse(PipePen.S(e, "P", "")), BlockName = PipePen.S(e, "Block", ""), Tag = PipePen.S(e, "Tag", "") });
            foreach (var e in x.Element("Legs")?.Elements("L") ?? Enumerable.Empty<XElement>())
            {
                LegKind k;
                if (!Enum.TryParse(PipePen.S(e, "Kind", "Pipe"), out k)) k = LegKind.Pipe;
                d.Legs.Add(new PlantLeg
                {
                    Id = PipePen.I(e, "Id", 0), A = PipePen.I(e, "A", 0), B = PipePen.I(e, "B", 0), Kind = k, Pen = PipePen.Load(e),
                    ComponentKind = PipePen.S(e, "Component", ""), BlockName = PipePen.S(e, "Block", ""), Tag = PipePen.S(e, "LegTag", "")
                });
            }
            foreach (var e in x.Element("Items")?.Elements("I") ?? Enumerable.Empty<XElement>())
                d.Items.Add(new PlantItem { Id = PipePen.I(e, "Id", 0), Vertex = PipePen.I(e, "V", 0), Kind = PipePen.S(e, "Kind", ""), BlockName = PipePen.S(e, "Block", ""), Tag = PipePen.S(e, "Tag", "") });
            foreach (var e in x.Element("Owned")?.Elements("B") ?? Enumerable.Empty<XElement>()) d.OwnedBlocks.Add(e.Value);
            int max = d.Vertices.Select(v => v.Id).Concat(d.Legs.Select(l => l.Id)).Concat(d.Items.Select(i => i.Id)).DefaultIfEmpty(0).Max();
            d._next = Math.Max(PipePen.I(x, "Next", 1), max + 1);
            return d;
        }

        public PlantDrawing Clone() { return LoadData(SaveData()); }
    }
}
