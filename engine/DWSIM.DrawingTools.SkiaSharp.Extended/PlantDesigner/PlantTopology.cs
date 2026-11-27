using System;
using System.Collections.Generic;
using System.Linq;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    /// <summary>A run of edges between two stop points: the points in order and the edges between them.</summary>
    public sealed class Chain
    {
        public readonly List<int> Points = new List<int>();
        public readonly List<int> Edges = new List<int>();
        public int Start { get { return Points[0]; } }
        public int End { get { return Points[Points.Count - 1]; } }
    }

    /// <summary>
    /// Splits a graph of straight lines into runs between stop points. A point is a stop when it does not have exactly
    /// two edges (an end or a branch) or when the caller forces it (an outlet, a component end). The runs between stops
    /// become the pipes of a network; the corners inside a run become the corners of the pipe's route.
    /// </summary>
    public static class PlantTopology
    {
        /// <summary>
        /// The runs of a graph of <paramref name="pointCount"/> points and the given edges (pairs of point indices).
        /// A ring without any stop gets one at its first point, and a closed run (a ring through a single stop) is split
        /// at its middle point, so that a run never joins a point to itself; a closed run of fewer than three edges is
        /// dropped. The stops set gets the points added that way.
        /// </summary>
        public static List<Chain> Chains(int pointCount, IList<int[]> edges, Func<int, bool> forcedStop, out HashSet<int> stops)
        {
            var adj = Enumerable.Range(0, pointCount).Select(v => new List<int>()).ToList();
            for (int e = 0; e < edges.Count; e++) { adj[edges[e][0]].Add(e); adj[edges[e][1]].Add(e); }
            Func<int, int, int> other = (e, v) => edges[e][0] == v ? edges[e][1] : edges[e][0];

            var st = new HashSet<int>(Enumerable.Range(0, pointCount).Where(v => adj[v].Count > 0 && (adj[v].Count != 2 || (forcedStop != null && forcedStop(v)))));
            var used = new HashSet<int>();
            var chains = new List<Chain>();
            Action<int> walk = start =>
            {
                foreach (var e0 in adj[start])
                {
                    if (used.Contains(e0)) continue;
                    var c = new Chain();
                    c.Points.Add(start);
                    int v = start, e = e0;
                    while (true)
                    {
                        used.Add(e);
                        c.Edges.Add(e);
                        v = other(e, v);
                        c.Points.Add(v);
                        if (st.Contains(v)) break;
                        int next = adj[v][0] == e ? adj[v][1] : adj[v][0];
                        if (used.Contains(next)) { st.Add(v); break; }
                        e = next;
                    }
                    chains.Add(c);
                }
            };
            foreach (var v in st.ToList()) walk(v);
            for (int e = 0; e < edges.Count; e++)
                if (!used.Contains(e)) { st.Add(edges[e][0]); walk(edges[e][0]); }
            foreach (var c in chains.Where(c => c.Start == c.End).ToList())
            {
                chains.Remove(c);
                if (c.Points.Count < 4) continue;
                int mid = c.Points.Count / 2;
                st.Add(c.Points[mid]);
                var a = new Chain(); a.Points.AddRange(c.Points.Take(mid + 1)); a.Edges.AddRange(c.Edges.Take(mid));
                var b = new Chain(); b.Points.AddRange(c.Points.Skip(mid)); b.Edges.AddRange(c.Edges.Skip(mid));
                chains.Add(a); chains.Add(b);
            }
            stops = st;
            return chains;
        }

        /// <summary>The pipe runs of a drawing, in point and leg ids. A point is a stop when it does not have exactly two
        /// pipe legs, when a component leg or an item touches it, or when the caller forces it. The stops are returned
        /// too, including the points that do not have pipe legs but carry a component or an item.</summary>
        public static List<Chain> PipeRuns(PlantDrawing d, Func<PlantVertex, bool> forcedStop, out HashSet<int> stopIds)
        {
            var index = new Dictionary<int, int>();
            for (int i = 0; i < d.Vertices.Count; i++) index[d.Vertices[i].Id] = i;
            var pipes = d.Legs.Where(l => l.Kind == LegKind.Pipe).ToList();
            var edges = pipes.Select(l => new[] { index[l.A], index[l.B] }).ToList();
            var touched = new HashSet<int>(d.Legs.Where(l => l.Kind != LegKind.Pipe).SelectMany(l => new[] { l.A, l.B }).Concat(d.Items.Select(i => i.Vertex)));
            HashSet<int> st;
            var runs = Chains(d.Vertices.Count, edges, i => touched.Contains(d.Vertices[i].Id) || (forcedStop != null && forcedStop(d.Vertices[i])), out st);
            stopIds = new HashSet<int>(st.Select(i => d.Vertices[i].Id));
            // a point without pipe legs is a stop too when a component or an item needs it (a pump between two tanks)
            stopIds.UnionWith(touched);
            var res = new List<Chain>();
            foreach (var c in runs)
            {
                var r = new Chain();
                r.Points.AddRange(c.Points.Select(i => d.Vertices[i].Id));
                r.Edges.AddRange(c.Edges.Select(e => pipes[e].Id));
                res.Add(r);
            }
            return res;
        }

        /// <summary>The corners of a polyline: the inner points where the direction changes (collinear points dropped).</summary>
        public static List<V3> Corners(IList<V3> pts, double tol = 1e-3)
        {
            var res = new List<V3>();
            for (int i = 1; i + 1 < pts.Count; i++)
            {
                V3 a = pts[i] - pts[i - 1], b = pts[i + 1] - pts[i];
                if (a.Length < tol || b.Length < tol) continue;
                if (V3.Cross(a.Normalized(), b.Normalized()).Length > tol) res.Add(pts[i]);
            }
            return res;
        }

        public static double Length(IList<V3> pts)
        {
            double s = 0;
            for (int i = 1; i < pts.Count; i++) s += V3.Distance(pts[i - 1], pts[i]);
            return s;
        }
    }
}
