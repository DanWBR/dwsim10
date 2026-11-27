using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    public enum PointerButton { Left, Middle, Right }

    [Flags]
    public enum KeyMods { None = 0, Shift = 1, Ctrl = 2, Alt = 4 }

    /// <summary>The designer's tools.</summary>
    public enum PlantTool { Select, Pipe, Place }

    /// <summary>
    /// Turns mouse and keyboard input into camera moves, selection and, when an editor is attached, drawing. The hosts
    /// pass device-pixel coordinates, the wheel in notches and the key names of their toolkit (WinForms Keys and Avalonia
    /// Key spell the letters, digits and function keys the same way).
    /// <para>Viewing: right drag orbits, middle drag pans, a left drag that starts on empty space pans, the wheel zooms at
    /// the cursor, a click selects (Ctrl adds) and a double-click activates an element (the host opens its editor) or fits
    /// the drawing.</para>
    /// <para>Pipe tool: the first click takes a point (an existing point, a point of a pipe, or a grid point of the working
    /// level); then the leg follows the axis the mouse points along, snapped to whole cube edges, to an existing point or
    /// to a pipe it meets, and each click fixes a corner. A typed number and Enter give an exact length. A leg that ends on
    /// the network joins it and ends the run; Esc, a right click or a double-click end it too.</para>
    /// <para>Select tool: dragging a point moves it along an axis (the legs follow it); Del deletes the selection.</para>
    /// </summary>
    public sealed class PlantViewController
    {
        public readonly PlantView View;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        /// <summary>The view needs a redraw.</summary>
        public event Action Invalidated;
        public event Action SelectionChanged;
        /// <summary>An element was double-clicked.</summary>
        public event Action<Hit> Activated;
        /// <summary>The hover changed (for a status line).</summary>
        public event Action<Hit> HoverChanged;
        /// <summary>The tool, the axis lock, the working level or the undo state changed (the host updates its toolbar).</summary>
        public event Action StateChanged;

        private PointerButton? _button;
        private double _downX, _downY, _lastX, _lastY;
        private bool _moved, _panning;
        private Hit _downHit;

        /// <summary>A press must move this far, device px, before it counts as a drag.</summary>
        public double DragThreshold = 4;
        /// <summary>The mouse snaps to points and pipes this close, device px (times the interface scale).</summary>
        public double SnapPx = 10;

        // ------------------------------------------------------------------ designer state

        /// <summary>The editor of the drawing; null makes the controller a viewer.</summary>
        public DrawingEditor Editor { get; private set; }
        public PlantTool Tool { get; private set; } = PlantTool.Select;
        /// <summary>The kind the place tool puts down.</summary>
        public PlantKind PlaceKind { get; private set; }
        public AxisLock Lock { get; private set; } = AxisLock.Auto;
        private V3? _anchor;
        private string _typed = "";
        private int _movingVertex = -1;

        public PlantViewController(PlantView view)
        {
            View = view;
            if (View.Shortcuts.Count == 0) View.Shortcuts.AddRange(PlantView.ViewerShortcuts());
        }

        public double Now { get { return _clock.Elapsed.TotalSeconds; } }

        /// <summary>The start of the leg being drawn; null when the pipe tool is not in a run.</summary>
        public V3? Anchor { get { return _anchor; } }

        private void Redraw() { var h = Invalidated; if (h != null) h(); }
        private void Changed() { UpdateChip(); var h = StateChanged; if (h != null) h(); }

        /// <summary>Makes the controller a designer that edits the drawing through the editor.</summary>
        public void AttachEditor(DrawingEditor editor, PlantTool tool = PlantTool.Pipe)
        {
            Editor = editor;
            View.Drawing = editor.Drawing;
            editor.Changed += (s, e) =>
            {
                View.Drawing = editor.Drawing;
                // the selection keeps only what is still there (an undo replaces the drawing)
                View.SelectedLegs.RemoveWhere(id => View.Drawing.Leg(id) == null);
                View.SelectedVertices.RemoveWhere(id => View.Drawing.Vertex(id) == null);
                View.SelectedItems.RemoveWhere(id => View.Drawing.Item(id) == null);
                Changed();
                Redraw();
            };
            SetTool(tool);
        }

        public void SetTool(PlantTool t)
        {
            Tool = Editor == null ? PlantTool.Select : t;
            EndRun();
            View.Shortcuts.Clear();
            View.Shortcuts.AddRange(Editor == null ? PlantView.ViewerShortcuts() : Tool == PlantTool.Pipe ? PlantView.PipeToolShortcuts()
                : Tool == PlantTool.Place ? PlantView.PlaceToolShortcuts(PlaceKind) : PlantView.SelectToolShortcuts());
            Changed();
            Redraw();
        }

        /// <summary>Picks the kind to place and switches to the place tool (null goes back to the select tool).</summary>
        public void SetPlaceKind(string id)
        {
            PlaceKind = View.Kinds.FirstOrDefault(k => k.Id == id);
            SetTool(PlaceKind == null ? PlantTool.Select : PlantTool.Place);
            RefreshPreview();
        }

        public void SetLock(AxisLock l) { Lock = l; RefreshPreview(); Changed(); Redraw(); }

        /// <summary>Moves the working level by whole vertical steps.</summary>
        public void ShiftLevel(int by) { View.Drawing.Grid.Level += by; RefreshPreview(); Changed(); Redraw(); }

        /// <summary>Ends the run of the pipe tool (the legs drawn so far stay).</summary>
        public void EndRun()
        {
            _anchor = null;
            _typed = "";
            View.Typed = "";
            View.Preview.Clear();
            View.PreviewKind = "";
        }

        private void UpdateChip()
        {
            if (Editor == null) { View.ToolChip = ""; return; }
            var g = View.Drawing.Grid;
            string lk = Lock == AxisLock.Auto ? "any axis" : Lock == AxisLock.Horizontal ? "horizontal" : Lock == AxisLock.Vertical ? "vertical" : Lock.ToString() + " axis";
            string tool = Tool == PlantTool.Pipe ? "Pipe" : Tool == PlantTool.Place ? "Place " + (PlaceKind != null ? PlaceKind.Name : "") : "Select";
            View.ToolChip = tool + "   ·   " + lk + "   ·   level " + g.Level.ToString(CultureInfo.InvariantCulture)
                + " (z = " + g.LevelZ.ToString("0.###", CultureInfo.InvariantCulture) + " m)   ·   step " + g.StepH.ToString("0.###", CultureInfo.InvariantCulture)
                + (Math.Abs(g.StepV - g.StepH) > 1e-12 ? " / " + g.StepV.ToString("0.###", CultureInfo.InvariantCulture) : "") + " m" + (g.Snap ? "" : ", snap off");
        }

        public void Resize(double width, double height)
        {
            View.Camera.Width = Math.Max(1, width);
            View.Camera.Height = Math.Max(1, height);
            Redraw();
        }

        // ------------------------------------------------------------------ pointer

        public void PointerDown(double x, double y, PointerButton button, KeyMods mods)
        {
            _button = button;
            _downX = _lastX = x; _downY = _lastY = y;
            _moved = false;
            _downHit = PlantRenderer.HitTest(View, x, y);
            _movingVertex = -1;
            if (Editor != null && Tool == PlantTool.Select && button == PointerButton.Left && _downHit.Kind == HitKind.Vertex) _movingVertex = _downHit.Id;
            _panning = button == PointerButton.Middle || (button == PointerButton.Right && (mods & KeyMods.Shift) != 0)
                || (button == PointerButton.Left && _movingVertex < 0 && (_downHit.Kind == HitKind.None || (Editor != null && Tool != PlantTool.Select && !_downHit.IsOverlay())));
            View.Camera.StopAnimation();
        }

        public void PointerMove(double x, double y, KeyMods mods)
        {
            View.MouseX = x; View.MouseY = y;
            if (_button.HasValue)
            {
                if (!_moved && Math.Abs(x - _downX) + Math.Abs(y - _downY) < DragThreshold) return;
                _moved = true;
                double dx = x - _lastX, dy = y - _lastY;
                _lastX = x; _lastY = y;
                if (_movingVertex >= 0) PreviewMove(x, y);
                else if (_panning) { View.Camera.Pan(dx, dy); RefreshPreview(); }
                else if (_button == PointerButton.Right) { View.Camera.Orbit(dx, dy); RefreshPreview(); }
                else if (_button == PointerButton.Left && _downHit.Kind == HitKind.Minimap) CenterOnMinimap(x, y);
                Redraw();
                return;
            }
            var h = PlantRenderer.HitTest(View, x, y);
            bool cardHot = h.Kind == HitKind.Card;
            bool changed = !h.Same(View.Hover);
            View.Hover = h.IsElement && !(Editor != null && Tool != PlantTool.Select) ? h : Hit.None;
            View.CardHot = cardHot;
            if (changed) { var hc = HoverChanged; if (hc != null) hc(View.Hover); }
            if (Editor != null && Tool == PlantTool.Pipe) PreviewPipe(x, y, h);
            if (Editor != null && Tool == PlantTool.Place) PreviewPlace(x, y, h);
            Redraw();
        }

        public void PointerUp(double x, double y, PointerButton button, KeyMods mods)
        {
            var b = _button;
            _button = null;
            if (b != button) return;
            if (_movingVertex >= 0)
            {
                int id = _movingVertex;
                _movingVertex = -1;
                if (_moved && View.Preview.HasLeg && V3.Distance(View.Preview.From, View.Preview.To) > PlantDrawing.Tolerance)
                    Editor.MoveVertex(id, View.Preview.To);
                View.Preview.Clear();
                if (_moved) { Redraw(); return; }
            }
            if (_moved) return;
            var h = PlantRenderer.HitTest(View, x, y);
            if (button == PointerButton.Right)
            {
                if (Editor != null && Tool == PlantTool.Pipe && _anchor.HasValue) { EndRun(); PreviewPipe(x, y, h); Redraw(); }
                return;
            }
            if (button != PointerButton.Left) return;
            if (h.Kind == HitKind.CardToggle) { View.Options.ShowCard = !View.Options.ShowCard; Redraw(); return; }
            if (h.Kind == HitKind.Card) return;
            if (h.Kind == HitKind.Minimap) { CenterOnMinimap(x, y); Redraw(); return; }
            if (Editor != null && Tool == PlantTool.Pipe) { PipeClick(x, y, h); Redraw(); return; }
            if (Editor != null && Tool == PlantTool.Place) { PlaceClick(x, y, h); Redraw(); return; }
            bool add = (mods & KeyMods.Ctrl) != 0;
            if (h.IsElement) View.Toggle(h, add);
            else if (!add) View.ClearSelection();
            var sc = SelectionChanged; if (sc != null) sc();
            Redraw();
        }

        /// <summary>Selects these legs (after the host drew them, as an import does) and tells the host.</summary>
        public void SelectLegs(IEnumerable<int> legIds)
        {
            View.ClearSelection();
            foreach (var id in legIds) if (View.Drawing.Leg(id) != null) View.SelectedLegs.Add(id);
            var sc = SelectionChanged; if (sc != null) sc();
            Redraw();
        }

        public void PointerLeave()
        {
            View.MouseX = View.MouseY = -1;
            View.Hover = Hit.None; View.CardHot = false;
            if (_movingVertex < 0) View.Preview.HasPoint = false;
            Redraw();
        }

        public void DoubleClick(double x, double y)
        {
            if (Editor != null && Tool == PlantTool.Pipe)
            {
                // the first click of the pair already fixed the corner: the second ends the run there
                EndRun();
                PreviewPipe(x, y, PlantRenderer.HitTest(View, x, y));
                Redraw();
                return;
            }
            var h = PlantRenderer.HitTest(View, x, y);
            if (h.IsElement) { var a = Activated; if (a != null) a(h); }
            else if (h.Kind == HitKind.None) { View.FitAll(); Redraw(); }
        }

        /// <summary>Zooms by wheel notches (positive zooms in) at the cursor.</summary>
        public void Wheel(double notches, double x, double y)
        {
            View.Camera.ZoomAt(x, y, Math.Pow(1.2, notches));
            RefreshPreview();
            Redraw();
        }

        private void CenterOnMinimap(double x, double y)
        {
            V3 p;
            if (PlantRenderer.MinimapPoint(View, x, y, out p)) View.Camera.Target = p;
        }

        // ------------------------------------------------------------------ pipe tool

        private double Snap { get { return SnapPx * View.Options.UiScale; } }

        private double ScreenDist(V3 p, double x, double y)
        {
            var s = View.Camera.Project(p);
            return Math.Sqrt((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y));
        }

        /// <summary>The point a click starts a run from: a point of the drawing, a point of a pipe (on whole cube edges
        /// from its start when the grid snaps), or a grid point of the working level.</summary>
        public bool StartPoint(double x, double y, out V3 p, out bool onNetwork)
        {
            var d = View.Drawing;
            onNetwork = true;
            var v = d.Vertices.Select(q => new { q, s = ScreenDist(q.P, x, y) }).Where(t => t.s <= Snap).OrderBy(t => t.s).FirstOrDefault();
            if (v != null) { p = v.q.P; return true; }
            PlantLeg best = null; double bt = 0, bd = double.MaxValue;
            foreach (var l in d.Legs.Where(l => l.Kind == LegKind.Pipe))
            {
                var a = View.Camera.Project(d.P(l.A)); var b = View.Camera.Project(d.P(l.B));
                double abx = b.X - a.X, aby = b.Y - a.Y, l2 = abx * abx + aby * aby;
                if (l2 < 1e-9) continue;
                double t = Math.Max(0, Math.Min(1, ((x - a.X) * abx + (y - a.Y) * aby) / l2));
                double px = a.X + t * abx - x, py = a.Y + t * aby - y, dist = Math.Sqrt(px * px + py * py);
                if (dist <= Snap && dist < bd) { bd = dist; best = l; bt = t; }
            }
            if (best != null)
            {
                V3 a3 = d.P(best.A), b3 = d.P(best.B);
                double len = V3.Distance(a3, b3), s = bt * len;
                var ax = (b3 - a3).AlongAxis();
                if (d.Grid.Snap && ax.HasValue)
                {
                    double step = d.Grid.Step(ax.Value);
                    if (len > step) s = Math.Max(0, Math.Min(len, Math.Round(s / step) * step));
                }
                p = a3 + (b3 - a3) * (len > 0 ? s / len : 0);
                return true;
            }
            onNetwork = false;
            return AxisSnap.LevelPoint(View.Camera, d.Grid, x, y, out p);
        }

        /// <summary>The end of the leg the mouse points at from the anchor: the axis and length of <see cref="AxisSnap"/>,
        /// then an existing point on that axis line, or the crossing of a pipe, when the mouse is near it on the screen.</summary>
        public bool LegEnd(double x, double y, out V3 end, out bool joins, out AxisChoice c)
        {
            end = V3.Zero; joins = false;
            var a = _anchor.Value;
            double typed;
            bool hasTyped = double.TryParse(_typed, NumberStyles.Float, CultureInfo.InvariantCulture, out typed) && typed > 0;
            c = AxisSnap.Choose(View.Camera, a, x, y, Lock, View.Drawing.Grid, hasTyped ? typed : double.NaN);
            if (!c.Valid) return false;
            end = c.End;
            if (hasTyped) { joins = View.Drawing.FindVertex(end) != null || View.Drawing.LegThrough(end) != null; return true; }
            var dir = V3.Of(c.Axis, c.Sign);
            var d = View.Drawing;
            // an existing point on the axis line, ahead of the anchor
            double bestS = double.MaxValue; V3? target = null;
            foreach (var v in d.Vertices)
            {
                var r = v.P - a;
                double s = V3.Dot(r, dir);
                if (s <= PlantDrawing.Tolerance || (r - dir * s).Length > PlantDrawing.Tolerance) continue;
                double sd = ScreenDist(v.P, x, y);
                if (sd <= Snap && sd < bestS) { bestS = sd; target = v.P; }
            }
            if (!target.HasValue)
            {
                // a pipe the axis line crosses
                foreach (var l in d.Legs.Where(l => l.Kind == LegKind.Pipe))
                {
                    V3 p; if (!Crossing(a, dir, d.P(l.A), d.P(l.B), out p)) continue;
                    double sd = ScreenDist(p, x, y);
                    if (sd <= Snap && sd < bestS) { bestS = sd; target = p; }
                }
            }
            if (target.HasValue) { end = target.Value; joins = true; c.Length = V3.Distance(a, end); c.End = end; }
            else joins = d.FindVertex(end) != null || d.LegThrough(end) != null;
            return V3.Distance(a, end) > PlantDrawing.Tolerance;
        }

        /// <summary>Where the ray from a along dir crosses the segment pq (within the drawing tolerance), ahead of a.</summary>
        private static bool Crossing(V3 a, V3 dir, V3 p, V3 q, out V3 at)
        {
            at = V3.Zero;
            var u = q - p;
            var w = a - p;
            double b = V3.Dot(dir, u), c = V3.Dot(u, u), d = V3.Dot(dir, w), e = V3.Dot(u, w);
            double den = c - b * b;
            if (c < 1e-12 || Math.Abs(den) < 1e-12) return false;
            double s = (b * e - c * d) / den, t = (e - b * d) / den;
            if (s <= PlantDrawing.Tolerance || t < 0 || t > 1) return false;
            var p1 = a + dir * s; var p2 = p + u * t;
            if (V3.Distance(p1, p2) > PlantDrawing.Tolerance) return false;
            at = p2;
            return true;
        }

        private static string AxisName(Axis a, int sign)
        {
            switch (a)
            {
                case Axis.X: return sign > 0 ? "east" : "west";
                case Axis.Y: return sign > 0 ? "north" : "south";
                default: return sign > 0 ? "up" : "down";
            }
        }

        private void PreviewPipe(double x, double y, Hit h)
        {
            var pv = View.Preview;
            pv.Clear();
            if (h.IsOverlay()) return;
            if (!_anchor.HasValue)
            {
                V3 p; bool on;
                if (StartPoint(x, y, out p, out on)) { pv.HasPoint = true; pv.Point = p; pv.Joins = on; }
                return;
            }
            V3 end; bool joins; AxisChoice c;
            pv.HasPoint = true; pv.Point = _anchor.Value;
            if (!LegEnd(x, y, out end, out joins, out c)) return;
            pv.HasLeg = true; pv.From = _anchor.Value; pv.To = end; pv.Joins = joins;
            pv.Label = V3.Distance(pv.From, end).ToString("0.###", CultureInfo.InvariantCulture) + " m " + AxisName(c.Axis, c.Sign);
        }

        /// <summary>Recomputes the preview after the camera, the lock or the level changed under a still mouse.</summary>
        private void RefreshPreview()
        {
            if (Editor == null || View.MouseX < 0) return;
            if (_movingVertex >= 0) { PreviewMove(View.MouseX, View.MouseY); return; }
            if (Tool == PlantTool.Pipe) PreviewPipe(View.MouseX, View.MouseY, PlantRenderer.HitTest(View, View.MouseX, View.MouseY));
            if (Tool == PlantTool.Place) PreviewPlace(View.MouseX, View.MouseY, PlantRenderer.HitTest(View, View.MouseX, View.MouseY));
        }

        // ------------------------------------------------------------------ place tool

        /// <summary>The pipe leg under the mouse and the point of it nearest the mouse; null when none is near.</summary>
        public PlantLeg PipeUnder(double x, double y, out V3 at)
        {
            at = V3.Zero;
            var d = View.Drawing;
            PlantLeg best = null; double bd = double.MaxValue, bt = 0;
            foreach (var l in d.Legs.Where(l => l.Kind == LegKind.Pipe))
            {
                var a = View.Camera.Project(d.P(l.A)); var b = View.Camera.Project(d.P(l.B));
                double abx = b.X - a.X, aby = b.Y - a.Y, l2 = abx * abx + aby * aby;
                if (l2 < 1e-9) continue;
                double t = Math.Max(0, Math.Min(1, ((x - a.X) * abx + (y - a.Y) * aby) / l2));
                double px = a.X + t * abx - x, py = a.Y + t * aby - y, dist = Math.Sqrt(px * px + py * py);
                if (dist <= Snap && dist < bd) { bd = dist; best = l; bt = t; }
            }
            if (best != null) at = d.P(best.A) + (d.P(best.B) - d.P(best.A)) * bt;
            return best;
        }

        /// <summary>Where a component of the given length would sit in a leg: centred on the point, kept inside the leg.</summary>
        private static void Extent(PlantDrawing d, PlantLeg l, V3 at, double length, out V3 p1, out V3 p2)
        {
            V3 a = d.P(l.A), b = d.P(l.B);
            double len = V3.Distance(a, b);
            var dir = (b - a).Normalized();
            double L = Math.Min(length, len);
            double t = Math.Max(0, Math.Min(len, V3.Dot(at - a, dir)));
            double t1 = Math.Max(0, Math.Min(len - L, t - L / 2));
            p1 = a + dir * t1; p2 = a + dir * (t1 + L);
        }

        private void PreviewPlace(double x, double y, Hit h)
        {
            var pv = View.Preview;
            pv.Clear();
            View.PreviewKind = "";
            if (PlaceKind == null || h.IsOverlay()) return;
            if (PlaceKind.Inline)
            {
                V3 at;
                var l = PipeUnder(x, y, out at);
                if (l == null) return;
                V3 p1, p2; Extent(View.Drawing, l, at, PlaceKind.Length, out p1, out p2);
                pv.HasLeg = true; pv.From = p1; pv.To = p2; pv.Joins = true; pv.Label = PlaceKind.Name;
                View.PreviewKind = PlaceKind.Id;
                return;
            }
            V3 p; bool on;
            if (!StartPoint(x, y, out p, out on)) return;
            pv.HasPoint = true; pv.Point = p; pv.Joins = on; pv.Label = PlaceKind.Name;
            View.PreviewKind = PlaceKind.Id;
        }

        private void PlaceClick(double x, double y, Hit h)
        {
            if (PlaceKind == null || h.IsOverlay()) return;
            if (PlaceKind.Inline)
            {
                V3 at;
                var l = PipeUnder(x, y, out at);
                if (l == null) return;
                Editor.PlaceComponent(l.Id, at, PlaceKind.Id, PlaceKind.Length);
            }
            else
            {
                V3 p; bool on;
                if (!StartPoint(x, y, out p, out on)) return;
                Editor.PlaceItemAt(p, PlaceKind.Id);
            }
            PreviewPlace(x, y, PlantRenderer.HitTest(View, x, y));
        }

        private void PipeClick(double x, double y, Hit h)
        {
            if (h.IsOverlay()) return;
            if (!_anchor.HasValue)
            {
                V3 p; bool on;
                if (StartPoint(x, y, out p, out on)) { _anchor = p; FollowLevel(p.Z); }
                PreviewPipe(x, y, h);
                Changed();
                return;
            }
            V3 end; bool joins; AxisChoice c;
            if (!LegEnd(x, y, out end, out joins, out c)) return;
            CommitLeg(end, joins);
            PreviewPipe(x, y, h);
        }

        private void CommitLeg(V3 end, bool joins)
        {
            Editor.DrawLeg(_anchor.Value, end);
            _typed = ""; View.Typed = "";
            FollowLevel(end.Z);
            // a leg that ends on the network joins it, and the run is over
            _anchor = joins ? (V3?)null : end;
            Changed();
        }

        /// <summary>The working level goes to the height the run is at, when that height is on the grid.</summary>
        private void FollowLevel(double z)
        {
            var g = View.Drawing.Grid;
            if (g.StepV <= 0) return;
            double k = (z - g.BaseZ) / g.StepV;
            if (Math.Abs(k - Math.Round(k)) < 1e-6) g.Level = (int)Math.Round(k);
        }

        private void PreviewMove(double x, double y)
        {
            var v = View.Drawing.Vertex(_movingVertex);
            var pv = View.Preview;
            pv.Clear();
            if (v == null) return;
            var c = AxisSnap.Choose(View.Camera, v.P, x, y, Lock, View.Drawing.Grid);
            if (!c.Valid) return;
            pv.HasLeg = true; pv.From = v.P; pv.To = c.End; pv.HasPoint = true; pv.Point = c.End;
            pv.Label = "move " + c.Length.ToString("0.###", CultureInfo.InvariantCulture) + " m " + AxisName(c.Axis, c.Sign);
        }

        // ------------------------------------------------------------------ keyboard

        /// <summary>Handles a key by its name; returns true when it did something.</summary>
        public bool KeyDown(string key, KeyMods mods)
        {
            var k = (key ?? "").ToUpperInvariant();
            if (k.StartsWith("NUMPAD")) k = "D" + k.Substring(6);
            if (Editor != null && DesignerKey(k, mods)) { Redraw(); return true; }
            var cam = View.Camera;
            switch (k)
            {
                case "D1": cam.SetView(CameraView.IsoSE, Now); break;
                case "D2": cam.SetView(CameraView.IsoSW, Now); break;
                case "D3": cam.SetView(CameraView.IsoNW, Now); break;
                case "D4": cam.SetView(CameraView.IsoNE, Now); break;
                case "D5": cam.SetView(CameraView.Plan, Now); break;
                case "D6": cam.SetView(CameraView.Front, Now); break;
                case "D7": cam.SetView(CameraView.Right, Now); break;
                case "Q": cam.Turn(-1, Now); break;
                case "E": cam.Turn(1, Now); break;
                case "F": View.FitAll(); break;
                case "L": View.Options.ShowLabels = !View.Options.ShowLabels; break;
                case "G": View.Options.ShowGrid = !View.Options.ShowGrid; break;
                case "F1": View.Options.ShowCard = !View.Options.ShowCard; break;
                case "ESCAPE": View.ClearSelection(); { var sc = SelectionChanged; if (sc != null) sc(); } break;
                case "ADD": case "OEMPLUS": cam.ZoomAt(cam.Width / 2, cam.Height / 2, 1.25); break;
                case "SUBTRACT": case "OEMMINUS": cam.ZoomAt(cam.Width / 2, cam.Height / 2, 0.8); break;
                default: return false;
            }
            RefreshPreview();
            Redraw();
            return true;
        }

        private bool DesignerKey(string k, KeyMods mods)
        {
            bool ctrl = (mods & KeyMods.Ctrl) != 0;
            if (ctrl && k == "Z") { if ((mods & KeyMods.Shift) != 0) Editor.Redo(); else Editor.Undo(); EndRun(); RefreshPreview(); return true; }
            if (ctrl && k == "Y") { Editor.Redo(); EndRun(); RefreshPreview(); return true; }
            if (ctrl) return false;
            // a run takes digits, the decimal point and Enter as its length
            if (Tool == PlantTool.Pipe && _anchor.HasValue)
            {
                string ch = null;
                if (k.Length == 2 && k[0] == 'D' && char.IsDigit(k[1])) ch = k.Substring(1);
                else if (k == "DECIMAL" || k == "OEMPERIOD" || k == "OEMCOMMA") ch = _typed.Contains(".") ? "" : ".";
                if (ch != null) { _typed += ch; View.Typed = _typed; RefreshPreview(); return true; }
                if (k == "BACK" && _typed.Length > 0) { _typed = _typed.Substring(0, _typed.Length - 1); View.Typed = _typed; RefreshPreview(); return true; }
                if ((k == "RETURN" || k == "ENTER") && _typed.Length > 0)
                {
                    V3 end; bool joins; AxisChoice c;
                    if (View.MouseX >= 0 && LegEnd(View.MouseX, View.MouseY, out end, out joins, out c)) CommitLeg(end, joins);
                    RefreshPreview();
                    return true;
                }
                if (k == "ESCAPE")
                {
                    if (_typed.Length > 0) { _typed = ""; View.Typed = ""; }
                    else EndRun();
                    RefreshPreview(); Changed();
                    return true;
                }
            }
            switch (k)
            {
                case "H": SetLock(Lock == AxisLock.Horizontal ? AxisLock.Auto : AxisLock.Horizontal); return true;
                case "V": SetLock(Lock == AxisLock.Vertical ? AxisLock.Auto : AxisLock.Vertical); return true;
                case "X": SetLock(Lock == AxisLock.X ? AxisLock.Auto : AxisLock.X); return true;
                case "Y": SetLock(Lock == AxisLock.Y ? AxisLock.Auto : AxisLock.Y); return true;
                case "Z": SetLock(Lock == AxisLock.Z ? AxisLock.Auto : AxisLock.Z); return true;
                case "PAGEUP": case "PRIOR": ShiftLevel(1); return true;
                case "PAGEDOWN": case "NEXT": ShiftLevel(-1); return true;
                case "P": SetTool(PlantTool.Pipe); RefreshPreview(); return true;
                case "S": SetTool(PlantTool.Select); return true;
                case "ESCAPE":
                    if (Tool != PlantTool.Place) return false;
                    SetTool(PlantTool.Select);
                    return true;
                case "DELETE":
                    if (!View.HasSelection) return false;
                    DeleteSelection();
                    return true;
            }
            return false;
        }

        /// <summary>Deletes the selected legs, points and items in one undo step.</summary>
        public void DeleteSelection()
        {
            if (Editor == null || !View.HasSelection) return;
            var legs = View.SelectedLegs.ToList(); var pts = View.SelectedVertices.ToList(); var items = View.SelectedItems.ToList();
            Editor.Edit(() =>
            {
                var d = Editor.Drawing;
                d.Items.RemoveAll(i => items.Contains(i.Id));
                d.Legs.RemoveAll(l => legs.Contains(l.Id));
                foreach (var p in pts) d.RemoveVertex(p);
                d.RemoveLoosePoints();
            });
            View.ClearSelection();
            var sc = SelectionChanged; if (sc != null) sc();
        }

        /// <summary>Advances the camera animation; returns true while the host should keep ticking.</summary>
        public bool Tick()
        {
            bool running = View.Camera.Tick(Now);
            if (running || View.Camera.Animating) { RefreshPreview(); Redraw(); }
            return View.Camera.Animating;
        }
    }

    internal static class HitExtensions
    {
        public static bool IsOverlay(this Hit h) { return h.Kind == HitKind.Card || h.Kind == HitKind.CardToggle || h.Kind == HitKind.Minimap; }
    }
}
