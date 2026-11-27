using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    /// <summary>
    /// The edits of the plant designer, each one undoable. Drawing a leg joins its ends to the points already there,
    /// makes a tee when an end falls inside another pipe, and splits the new leg at the points it passes through.
    /// Undo keeps whole snapshots of the drawing, so consumers must read <see cref="Drawing"/> again after an undo.
    /// </summary>
    public sealed class DrawingEditor
    {
        private readonly List<XElement> _undo = new List<XElement>();
        private readonly List<XElement> _redo = new List<XElement>();

        public PlantDrawing Drawing { get; private set; }

        /// <summary>The number of snapshots kept for undo.</summary>
        public int UndoLimit = 200;

        /// <summary>Raised after every edit, undo and redo.</summary>
        public event EventHandler Changed;

        public DrawingEditor(PlantDrawing d) { Drawing = d ?? new PlantDrawing(); }

        public bool CanUndo { get { return _undo.Count > 0; } }
        public bool CanRedo { get { return _redo.Count > 0; } }

        /// <summary>Keeps a snapshot before an edit.</summary>
        public void Checkpoint()
        {
            _undo.Add(Drawing.SaveData());
            if (_undo.Count > UndoLimit) _undo.RemoveAt(0);
            _redo.Clear();
        }

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            _redo.Add(Drawing.SaveData());
            Replace(_undo[_undo.Count - 1]); _undo.RemoveAt(_undo.Count - 1);
            return true;
        }

        public bool Redo()
        {
            if (_redo.Count == 0) return false;
            _undo.Add(Drawing.SaveData());
            Replace(_redo[_redo.Count - 1]); _redo.RemoveAt(_redo.Count - 1);
            return true;
        }

        private void Replace(XElement x)
        {
            var d = PlantDrawing.LoadData(x);
            Drawing = d;
            Raise();
        }

        private void Raise() { var h = Changed; if (h != null) h(this, EventArgs.Empty); }

        /// <summary>Replaces the whole drawing (read again from its source) as one undo step.</summary>
        public void ReplaceWith(PlantDrawing d)
        {
            Checkpoint();
            Drawing = d.Clone();
            Raise();
        }

        /// <summary>Runs an edit as one undo step. If the edit throws, the drawing goes back to where it was.</summary>
        public T Edit<T>(Func<T> edit)
        {
            Checkpoint();
            T r;
            try { r = edit(); }
            catch
            {
                Drawing = PlantDrawing.LoadData(_undo[_undo.Count - 1]);
                _undo.RemoveAt(_undo.Count - 1);
                throw;
            }
            Raise();
            return r;
        }

        public void Edit(Action edit) { Edit(() => { edit(); return true; }); }

        // ------------------------------------------------------------------ edits (one undo step each)

        /// <summary>Draws a polyline of pipe legs with the current pen and returns the point at its end.</summary>
        public PlantVertex DrawRun(IList<V3> points)
        {
            if (points == null || points.Count < 2) throw new ArgumentException("A run needs at least two points.");
            return Edit(() =>
            {
                PlantVertex last = null;
                for (int i = 1; i < points.Count; i++) last = AddPipe(points[i - 1], points[i]);
                return last;
            });
        }

        /// <summary>Draws one pipe leg from p to q (see <see cref="DrawRun"/>).</summary>
        public PlantVertex DrawLeg(V3 p, V3 q) { return DrawRun(new[] { p, q }); }

        /// <summary>Puts a component (pump, valve...) inside a pipe leg, centred at a point of it, with the given
        /// length along the leg. The component runs in the leg's drawing direction. Returns the component leg.</summary>
        public PlantLeg PlaceComponent(int legId, V3 at, string kind, double lengthM)
        {
            return Edit(() => InsertComponent(legId, at, kind, lengthM));
        }

        /// <summary>Puts a terminal item on a point.</summary>
        public PlantItem PlaceItem(int vertexId, string kind)
        {
            if (Drawing.Vertex(vertexId) == null) throw new ArgumentException("The point does not exist.");
            return Edit(() => Drawing.AddItem(vertexId, kind));
        }

        /// <summary>Puts a terminal item at a plant point: on the point there, or on a new point that splits the pipe
        /// passing through it, or on a new loose point.</summary>
        public PlantItem PlaceItemAt(V3 p, string kind)
        {
            return Edit(() => Drawing.AddItem(Attach(p).Id, kind));
        }

        public void DeleteLeg(int legId)
        {
            Edit(() => { var l = Drawing.Leg(legId); if (l != null) Drawing.RemoveLeg(l); Drawing.RemoveLoosePoints(); });
        }

        public void DeleteVertex(int vertexId)
        {
            Edit(() => { Drawing.RemoveVertex(vertexId); Drawing.RemoveLoosePoints(); });
        }

        public void DeleteItem(int itemId)
        {
            Edit(() => { Drawing.Items.RemoveAll(i => i.Id == itemId); Drawing.RemoveLoosePoints(); });
        }

        /// <summary>Moves a point; its legs follow it.</summary>
        public void MoveVertex(int vertexId, V3 to)
        {
            Edit(() => { var v = Drawing.Vertex(vertexId); if (v != null) v.P = to; });
        }

        /// <summary>Reverses the flow direction of a component leg.</summary>
        public void FlipComponent(int legId)
        {
            Edit(() => { var l = Drawing.Leg(legId); if (l != null) { int a = l.A; l.A = l.B; l.B = a; } });
        }

        /// <summary>Gives the legs a copy of a pen.</summary>
        public void SetPen(IEnumerable<int> legIds, PipePen pen)
        {
            var ids = legIds.ToList();
            Edit(() => { foreach (var l in Drawing.Legs.Where(x => ids.Contains(x.Id))) l.Pen = pen.Clone(); });
        }

        // ------------------------------------------------------------------ building blocks (no undo step)

        /// <summary>The point at p: the one there, or a new one that splits the pipe passing through p, or a new loose one.</summary>
        private PlantVertex Attach(V3 p)
        {
            var v = Drawing.FindVertex(p);
            if (v != null) return v;
            var l = Drawing.LegThrough(p);
            if (l != null) return Drawing.SplitLeg(l, p);
            return Drawing.AddVertex(p);
        }

        private PlantVertex AddPipe(V3 p, V3 q)
        {
            if (V3.Distance(p, q) <= PlantDrawing.Tolerance) return Attach(q);
            var a = Attach(p);
            var b = Attach(q);
            // the points the new leg passes through split it, so that a leg drawn across a point joins it
            var dir = (b.P - a.P).Normalized();
            double len = V3.Distance(a.P, b.P);
            var inner = Drawing.Vertices.Where(v => v.Id != a.Id && v.Id != b.Id)
                .Select(v => new { v, t = V3.Dot(v.P - a.P, dir) })
                .Where(x => x.t > PlantDrawing.Tolerance && x.t < len - PlantDrawing.Tolerance && V3.Distance(a.P + dir * x.t, x.v.P) <= PlantDrawing.Tolerance)
                .OrderBy(x => x.t).Select(x => x.v).ToList();
            var chain = new List<PlantVertex> { a };
            chain.AddRange(inner);
            chain.Add(b);
            for (int i = 1; i < chain.Count; i++)
            {
                int u = chain[i - 1].Id, w = chain[i].Id;
                if (Drawing.Legs.Any(l => (l.A == u && l.B == w) || (l.A == w && l.B == u))) continue;
                Drawing.AddLeg(u, w);
            }
            return b;
        }

        private PlantLeg InsertComponent(int legId, V3 at, string kind, double lengthM)
        {
            var l = Drawing.Leg(legId);
            if (l == null || l.Kind != LegKind.Pipe) throw new ArgumentException("A component goes inside a pipe leg.");
            V3 a = Drawing.P(l.A), b = Drawing.P(l.B);
            double len = V3.Distance(a, b);
            var dir = (b - a).Normalized();
            double tol = PlantDrawing.Tolerance;
            double L = Math.Min(Math.Max(lengthM, 4 * tol), len);
            double t = Math.Max(0, Math.Min(len, V3.Dot(at - a, dir)));
            double t1 = Math.Max(0, Math.Min(len - L, t - L / 2)), t2 = t1 + L;
            PlantLeg mid = l;
            // split off the pipe before the component, then the pipe after it
            if (t1 > tol) { var v1 = Drawing.SplitLeg(l, a + dir * t1); mid = Drawing.LegsAt(v1.Id).First(x => x.A == v1.Id); }
            if (len - t2 > tol) { Drawing.SplitLeg(mid, a + dir * t2); }
            mid.Kind = LegKind.Component;
            mid.ComponentKind = kind ?? "";
            mid.BlockName = "";
            mid.Tag = "";
            return mid;
        }
    }
}
