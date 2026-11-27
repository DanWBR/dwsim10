using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    /// <summary>
    /// Draws a plant view with SkiaSharp: the grid of the working level, the legs and items from the back to the front
    /// (farther lines fade towards the background, so that depth shows without perspective), the labels where they do
    /// not overlap, and the overlays: the colour legend (top left), the shortcuts card (top right), the minimap (bottom
    /// right) and the axes (bottom left). The same geometry answers the hit tests of the controller.
    /// </summary>
    public static class PlantRenderer
    {
        private sealed class Theme
        {
            public SKColor Background, Ink, Muted, Grid, GridStrong, Accent, Panel, PanelBorder, Fire, Water, Equipment;
        }

        private static Theme ThemeOf(PlantViewOptions o)
        {
            return o.Dark
                ? new Theme
                {
                    Background = SKColor.Parse("#1a1a19"), Ink = SKColor.Parse("#e6e4df"), Muted = SKColor.Parse("#a3a19b"), Grid = SKColor.Parse("#2c2c2a"),
                    GridStrong = SKColor.Parse("#3d3d3a"), Accent = SKColor.Parse("#f0a43a"), Panel = SKColor.Parse("#242423").WithAlpha(236),
                    PanelBorder = SKColor.Parse("#45443f"), Fire = SKColor.Parse("#e0605a"), Water = SKColor.Parse("#6da7ec"), Equipment = SKColor.Parse("#c9c6be")
                }
                : new Theme
                {
                    Background = SKColor.Parse("#fcfcfb"), Ink = SKColor.Parse("#2f2e2b"), Muted = SKColor.Parse("#6b6a65"), Grid = SKColor.Parse("#ecebe7"),
                    GridStrong = SKColor.Parse("#dcdad4"), Accent = SKColor.Parse("#d9730d"), Panel = SKColor.Parse("#ffffff").WithAlpha(240),
                    PanelBorder = SKColor.Parse("#d8d6d0"), Fire = SKColor.Parse("#c8322b"), Water = SKColor.Parse("#2a78d6"), Equipment = SKColor.Parse("#4a4945")
                };
        }

        private static SKColor Mix(SKColor a, SKColor b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return new SKColor((byte)(a.Red + (b.Red - a.Red) * t), (byte)(a.Green + (b.Green - a.Green) * t), (byte)(a.Blue + (b.Blue - a.Blue) * t), (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
        }

        private static SKPoint Pt(ScreenPoint s) { return new SKPoint((float)s.X, (float)s.Y); }

        // ------------------------------------------------------------------ render

        public static void Render(SKCanvas c, PlantView v)
        {
            var th = ThemeOf(v.Options);
            float u = v.Options.UiScale;
            var cam = v.Camera;
            var d = v.Drawing;
            c.Clear(th.Background);

            if (d.Vertices.Count == 0)
            {
                if (string.IsNullOrEmpty(v.ToolChip))
                    using (var p = Text(th.Muted, 14 * u))
                    {
                        float w = p.MeasureText(v.EmptyMessage);
                        c.DrawText(v.EmptyMessage, (float)(cam.Width / 2 - w / 2), (float)(cam.Height / 2), p);
                    }
                else if (v.Options.ShowGrid) DrawGrid(c, v, th);
                if (v.Options.ShowAxes) DrawAxes(c, v, th);
                DrawPreview(c, v, th);
                DrawChip(c, v, th);
                DrawCard(c, v, th);
                DrawTyped(c, v, th);
                return;
            }

            var proj = d.Vertices.ToDictionary(x => x.Id, x => cam.Project(x.P));
            double dmin = proj.Values.Min(s => s.Depth), dmax = proj.Values.Max(s => s.Depth);
            Func<double, double> near = depth => dmax - dmin < 1e-9 ? 1.0 : (depth - dmin) / (dmax - dmin);
            Func<SKColor, double, SKColor> fade = (col, depth) => Mix(th.Background, col, 0.5 + 0.5 * near(depth));

            if (v.Options.ShowGrid) DrawGrid(c, v, th);

            // the scene from the back to the front
            var order = new List<Tuple<double, Action>>();
            foreach (var l in d.Legs)
            {
                var a = proj[l.A]; var b = proj[l.B];
                var leg = l;
                order.Add(Tuple.Create((a.Depth + b.Depth) / 2, (Action)(() => DrawLeg(c, v, th, leg, a, b, fade))));
            }
            var stopIds = NodePoints(d);
            foreach (var x in d.Vertices)
            {
                if (!stopIds.Contains(x.Id) && !v.SelectedVertices.Contains(x.Id) && !(v.Hover.Kind == HitKind.Vertex && v.Hover.Id == x.Id)) continue;
                var s = proj[x.Id]; var vx = x;
                order.Add(Tuple.Create(s.Depth + 1e-6, (Action)(() => DrawVertex(c, v, th, vx, s, fade))));
            }
            foreach (var it in d.Items)
            {
                var s = proj[it.Vertex]; var item = it;
                order.Add(Tuple.Create(s.Depth + 2e-6, (Action)(() => DrawItem(c, v, th, item, s))));
            }
            foreach (var o in order.OrderBy(t => t.Item1)) o.Item2();

            if (v.Options.ShowLabels) DrawLabels(c, v, th, proj, stopIds);
            DrawPreview(c, v, th);
            DrawChip(c, v, th);
            if (v.Options.ShowLegend) DrawLegend(c, v, th);
            if (v.Options.ShowAxes) DrawAxes(c, v, th);
            if (v.Options.ShowMinimap) DrawMinimap(c, v, th);
            else v.MinimapRect = SKRect.Empty;
            DrawCard(c, v, th);
            DrawTyped(c, v, th);
            DrawTooltip(c, v, th);
        }

        /// <summary>The points drawn as nodes: ends, branches and points with an item or a component.</summary>
        private static HashSet<int> NodePoints(PlantDrawing d)
        {
            var res = new HashSet<int>();
            foreach (var x in d.Vertices)
                if (d.Degree(x.Id) != 2 || !string.IsNullOrEmpty(x.BlockName) || d.ItemsAt(x.Id).Any() || d.LegsAt(x.Id).Any(l => l.Kind == LegKind.Component)) res.Add(x.Id);
            return res;
        }

        private static SKPaint Stroke(SKColor col, float w)
        {
            return new SKPaint { Color = col, StrokeWidth = w, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        }

        private static SKPaint Fill(SKColor col) { return new SKPaint { Color = col, Style = SKPaintStyle.Fill, IsAntialias = true }; }

        private static SKPaint Text(SKColor col, float size, bool bold = false)
        {
            return new SKPaint
            {
                Color = col, TextSize = size, IsAntialias = true, Style = SKPaintStyle.Fill,
                Typeface = SKTypeface.FromFamilyName(null, bold ? SKFontStyleWeight.SemiBold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };
        }

        /// <summary>The leg being drawn (dashed, in the accent colour, with its length and direction), the point a click
        /// would take, and the length being typed next to the cursor.</summary>
        private static void DrawPreview(SKCanvas c, PlantView v, Theme th)
        {
            float u = v.Options.UiScale;
            var pv = v.Preview;
            var cam = v.Camera;
            if (pv.HasLeg)
            {
                var a = Pt(cam.Project(pv.From)); var b = Pt(cam.Project(pv.To));
                using (var p = Stroke(th.Accent, 2.6f * u))
                {
                    p.PathEffect = SKPathEffect.CreateDash(new[] { 8f * u, 5f * u }, 0);
                    c.DrawLine(a, b, p);
                }
                // a drop line to the working level shows how high the end is
                var lz = v.Drawing.Grid.LevelZ;
                if (Math.Abs(pv.To.Z - lz) > 1e-6 && cam.CanPlaceOnLevel)
                    using (var p = Stroke(th.Accent.WithAlpha(90), 1f * u))
                    {
                        p.PathEffect = SKPathEffect.CreateDash(new[] { 3f * u, 3f * u }, 0);
                        c.DrawLine(b, Pt(cam.Project(new V3(pv.To.X, pv.To.Y, lz))), p);
                    }
                using (var e = pv.Joins ? Fill(th.Accent) : Stroke(th.Accent, 2f * u)) c.DrawCircle(b, 5f * u, e);
                if (!string.IsNullOrEmpty(pv.Label))
                    using (var t = Text(th.Ink, 12 * u, true))
                    {
                        float w = t.MeasureText(pv.Label);
                        var mid = new SKPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                        var r = new SKRect(mid.X + 10 * u, mid.Y - 24 * u, mid.X + 10 * u + w + 12 * u, mid.Y - 6 * u);
                        Panel(c, r, th, u);
                        c.DrawText(pv.Label, r.Left + 6 * u, r.Bottom - 5 * u, t);
                    }
            }
            if (!string.IsNullOrEmpty(v.PreviewKind))
            {
                // the block being placed, drawn half transparent where a click would put it
                c.SaveLayer(new SKPaint { Color = SKColors.Black.WithAlpha(150) });
                if (pv.HasLeg)
                {
                    var a = Pt(cam.Project(pv.From)); var b = Pt(cam.Project(pv.To));
                    DrawGlyph(c, v, th, v.GlyphOf(v.PreviewKind), new SKPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2), b - a, th.Accent);
                }
                else if (pv.HasPoint) DrawItem(c, v, th, new PlantItem { Kind = v.PreviewKind }, cam.Project(pv.Point));
                c.Restore();
                if (pv.HasPoint && !pv.HasLeg && !string.IsNullOrEmpty(pv.Label))
                    using (var t = Text(th.Ink, 12 * u, true))
                    {
                        var s0 = Pt(cam.Project(pv.Point));
                        float w = t.MeasureText(pv.Label);
                        var r = new SKRect(s0.X + 14 * u, s0.Y - 30 * u, s0.X + 26 * u + w, s0.Y - 12 * u);
                        Panel(c, r, th, u);
                        c.DrawText(pv.Label, r.Left + 6 * u, r.Bottom - 5 * u, t);
                    }
            }
            if (pv.HasPoint)
            {
                var s = Pt(cam.Project(pv.Point));
                using (var o = Stroke(th.Accent, 1.8f * u)) { c.DrawCircle(s, 6f * u, o); c.DrawLine(s.X - 10 * u, s.Y, s.X - 7 * u, s.Y, o); c.DrawLine(s.X + 7 * u, s.Y, s.X + 10 * u, s.Y, o); }
                if (pv.Joins && !pv.HasLeg) using (var f = Fill(th.Accent.WithAlpha(120))) c.DrawCircle(s, 4f * u, f);
            }
        }

        /// <summary>The length being typed, next to the cursor (drawn after the card, whose place it avoids).</summary>
        private static void DrawTyped(SKCanvas c, PlantView v, Theme th)
        {
            float u = v.Options.UiScale;
            var cam = v.Camera;
            if (!string.IsNullOrEmpty(v.Typed) && v.MouseX >= 0)
                using (var t = Text(th.Ink, 13 * u, true))
                {
                    string text = "Length: " + v.Typed + " m";
                    float w = t.MeasureText(text);
                    var r = new SKRect((float)v.MouseX + 18 * u, (float)v.MouseY + 14 * u, (float)v.MouseX + 30 * u + w, (float)v.MouseY + 36 * u);
                    // the box goes to the left of the cursor when the card or the edge is in the way
                    if (r.Right > cam.Width || (!v.CardRect.IsEmpty && v.CardRect.IntersectsWith(r)) || v.CardToggleRect.IntersectsWith(r))
                        r.Offset(-(r.Width + 36 * u), 0);
                    Panel(c, r, th, u);
                    c.DrawText(text, r.Left + 6 * u, r.Bottom - 7 * u, t);
                }
        }

        /// <summary>The tool state at the top centre of the view.</summary>
        private static void DrawChip(SKCanvas c, PlantView v, Theme th)
        {
            if (string.IsNullOrEmpty(v.ToolChip)) return;
            float u = v.Options.UiScale;
            using (var t = Text(th.Ink, 12 * u))
            {
                float w = t.MeasureText(v.ToolChip);
                float cx = (float)v.Camera.Width / 2;
                var r = new SKRect(cx - w / 2 - 12 * u, 12 * u, cx + w / 2 + 12 * u, 38 * u);
                Panel(c, r, th, u);
                c.DrawText(v.ToolChip, r.Left + 12 * u, r.Bottom - 9 * u, t);
            }
        }

        private static void DrawGrid(SKCanvas c, PlantView v, Theme th)
        {
            var cam = v.Camera; var g = v.Drawing.Grid;
            if (!cam.CanPlaceOnLevel || g.StepH <= 0) return;
            V3 min, max;
            if (!v.Drawing.Bounds(out min, out max))
            {
                // an empty drawing gets a grid around the centre of the view, so that the first point has a place to go
                V3 at;
                if (!cam.TryUnprojectToLevel(cam.Width / 2, cam.Height / 2, g.LevelZ, out at)) return;
                double half = 10 * g.StepH;
                min = new V3(at.X - half, at.Y - half, g.LevelZ); max = new V3(at.X + half, at.Y + half, g.LevelZ);
            }
            if (v.Preview.HasLeg || v.Preview.HasPoint)
            {
                // the grid also covers the point being drawn
                var q = v.Preview.HasLeg ? v.Preview.To : v.Preview.Point;
                min = new V3(Math.Min(min.X, q.X), Math.Min(min.Y, q.Y), min.Z); max = new V3(Math.Max(max.X, q.X), Math.Max(max.Y, q.Y), max.Z);
            }
            double step = g.StepH;
            // never more than about 120 lines a side
            while ((max.X - min.X) / step > 120 || (max.Y - min.Y) / step > 120) step *= 2;
            double x0 = Math.Floor(min.X / step - 1) * step, x1 = Math.Ceiling(max.X / step + 1) * step;
            double y0 = Math.Floor(min.Y / step - 1) * step, y1 = Math.Ceiling(max.Y / step + 1) * step;
            double z = g.LevelZ;
            using (var p = Stroke(th.Grid, 1f * v.Options.UiScale))
            {
                for (double x = x0; x <= x1 + 1e-9; x += step) c.DrawLine(Pt(cam.Project(new V3(x, y0, z))), Pt(cam.Project(new V3(x, y1, z))), p);
                for (double y = y0; y <= y1 + 1e-9; y += step) c.DrawLine(Pt(cam.Project(new V3(x0, y, z))), Pt(cam.Project(new V3(x1, y, z))), p);
            }
        }

        private static void DrawLeg(SKCanvas c, PlantView v, Theme th, PlantLeg l, ScreenPoint a, ScreenPoint b, Func<SKColor, double, SKColor> fade)
        {
            float u = v.Options.UiScale;
            double depth = (a.Depth + b.Depth) / 2;
            bool sel = v.SelectedLegs.Contains(l.Id), hot = v.Hover.Kind == HitKind.Leg && v.Hover.Id == l.Id;
            var pa = Pt(a); var pb = Pt(b);
            if (sel || hot)
                using (var h = Stroke(th.Accent.WithAlpha(sel ? (byte)110 : (byte)70), (sel ? 9f : 7f) * u)) c.DrawLine(pa, pb, h);
            if (l.Kind == LegKind.Component)
            {
                using (var p = Stroke(fade(th.Equipment, depth), 1.4f * u)) c.DrawLine(pa, pb, p);
                var mid = new SKPoint((pa.X + pb.X) / 2, (pa.Y + pb.Y) / 2);
                DrawGlyph(c, v, th, v.GlyphOf(l.ComponentKind), mid, pb - pa, fade(th.Equipment, depth));
                return;
            }
            double val;
            SKColor col = v.LegValues.TryGetValue(l.Id, out val) && !v.LegScale.IsEmpty ? v.LegScale.ColorOf(val) : th.Ink;
            using (var p = Stroke(fade(col, depth), 2.4f * u))
            {
                // a leg off the axes (a straight pipe between two points of different routes) is dashed
                var dir = v.Drawing.P(l.B) - v.Drawing.P(l.A);
                if (dir.AlongAxis(0.01) == null) p.PathEffect = SKPathEffect.CreateDash(new[] { 7f * u, 4f * u }, 0);
                c.DrawLine(pa, pb, p);
            }
        }

        private static void DrawVertex(SKCanvas c, PlantView v, Theme th, PlantVertex x, ScreenPoint s, Func<SKColor, double, SKColor> fade)
        {
            float u = v.Options.UiScale;
            bool sel = v.SelectedVertices.Contains(x.Id), hot = v.Hover.Kind == HitKind.Vertex && v.Hover.Id == x.Id;
            double val;
            bool hasVal = v.VertexValues.TryGetValue(x.Id, out val) && !v.VertexScale.IsEmpty;
            float r = (hasVal ? 6.5f : 2.8f) * u;
            if (sel || hot) using (var h = Fill(th.Accent.WithAlpha(sel ? (byte)120 : (byte)80))) c.DrawCircle(Pt(s), r + 4 * u, h);
            using (var f = Fill(fade(hasVal ? v.VertexScale.ColorOf(val) : th.Ink, s.Depth))) c.DrawCircle(Pt(s), r, f);
            if (hasVal) using (var ring = Stroke(th.Background, 1.2f * u)) c.DrawCircle(Pt(s), r, ring);
        }

        /// <summary>The screen box of an item's symbol: it stands on its point.</summary>
        private static SKRect ItemBox(PlantView v, PlantItem it, ScreenPoint s)
        {
            float u = v.Options.UiScale, w = 14 * u, h = 18 * u;
            var g = v.GlyphOf(it.Kind);
            if (g == ItemGlyph.Tank) { w = 22 * u; h = 22 * u; }
            if (g == ItemGlyph.Nozzle) { w = 12 * u; h = 12 * u; return new SKRect((float)s.X - w / 2, (float)s.Y, (float)s.X + w / 2, (float)s.Y + h); }
            return new SKRect((float)s.X - w / 2, (float)s.Y - h, (float)s.X + w / 2, (float)s.Y);
        }

        private static void DrawItem(SKCanvas c, PlantView v, Theme th, PlantItem it, ScreenPoint s)
        {
            float u = v.Options.UiScale;
            var box = ItemBox(v, it, s);
            var g = v.GlyphOf(it.Kind);
            bool sel = v.SelectedItems.Contains(it.Id), hot = v.Hover.Kind == HitKind.Item && v.Hover.Id == it.Id;
            if (sel || hot)
            {
                var hb = box; hb.Inflate(4 * u, 4 * u);
                using (var h = Fill(th.Accent.WithAlpha(sel ? (byte)110 : (byte)70))) c.DrawRoundRect(hb, 4 * u, 4 * u, h);
            }
            var fire = th.Fire;
            using (var f = Fill(g == ItemGlyph.Tank || g == ItemGlyph.Source ? th.Water : g == ItemGlyph.Sink || g == ItemGlyph.Generic ? th.Equipment : fire))
            using (var o = Stroke(th.Ink, 1.1f * u))
            {
                float cx = box.MidX;
                switch (g)
                {
                    case ItemGlyph.Hydrant:
                    case ItemGlyph.HoseReel:
                        // a post with a cap and two side outlets
                        var body = new SKRect(cx - 3.5f * u, box.Top + 4 * u, cx + 3.5f * u, box.Bottom);
                        c.DrawRect(body, f);
                        c.DrawRoundRect(new SKRect(cx - 4.5f * u, box.Top + 1 * u, cx + 4.5f * u, box.Top + 5 * u), 2 * u, 2 * u, f);
                        c.DrawRect(new SKRect(box.Left, box.Top + 8 * u, box.Right, box.Top + 11 * u), f);
                        if (g == ItemGlyph.HoseReel) using (var ring = Stroke(fire, 1.6f * u)) c.DrawCircle(cx, box.Top + 10 * u, 6 * u, ring);
                        break;
                    case ItemGlyph.Monitor:
                        c.DrawRect(new SKRect(cx - 2 * u, box.Top + 7 * u, cx + 2 * u, box.Bottom), f);
                        c.DrawCircle(cx, box.Top + 7 * u, 4 * u, f);
                        using (var n = Stroke(fire, 2.4f * u)) c.DrawLine(cx, box.Top + 7 * u, box.Right + 2 * u, box.Top + 1 * u, n);
                        break;
                    case ItemGlyph.Nozzle:
                        using (var path = new SKPath())
                        {
                            path.MoveTo(cx, box.Top); path.LineTo(box.Right, box.Bottom); path.LineTo(box.Left, box.Bottom); path.Close();
                            c.DrawPath(path, f);
                        }
                        break;
                    case ItemGlyph.Tank:
                        var tb = new SKRect(box.Left, box.Top + 4 * u, box.Right, box.Bottom);
                        c.DrawRect(tb, f);
                        c.DrawOval(new SKRect(box.Left, box.Top, box.Right, box.Top + 8 * u), f);
                        c.DrawOval(new SKRect(box.Left, box.Top, box.Right, box.Top + 8 * u), o);
                        c.DrawLine(box.Left, box.Top + 4 * u, box.Left, box.Bottom, o);
                        c.DrawLine(box.Right, box.Top + 4 * u, box.Right, box.Bottom, o);
                        break;
                    case ItemGlyph.Source:
                        c.DrawCircle(cx, box.MidY, 6 * u, f);
                        using (var w = Stroke(th.Background, 1.6f * u)) { c.DrawLine(cx - 3 * u, box.MidY, cx + 3 * u, box.MidY, w); c.DrawLine(cx + 1 * u, box.MidY - 2 * u, cx + 3 * u, box.MidY, w); c.DrawLine(cx + 1 * u, box.MidY + 2 * u, cx + 3 * u, box.MidY, w); }
                        break;
                    default:
                        using (var path = new SKPath())
                        {
                            path.MoveTo(box.Left, box.Top + 4 * u); path.LineTo(box.Right, box.Top + 4 * u); path.LineTo(cx, box.Bottom); path.Close();
                            c.DrawPath(path, f); c.DrawPath(path, o);
                        }
                        break;
                }
            }
        }

        /// <summary>A component symbol at a point, turned along the screen direction of its leg.</summary>
        private static void DrawGlyph(SKCanvas c, PlantView v, Theme th, ItemGlyph g, SKPoint at, SKPoint dir, SKColor col)
        {
            float u = v.Options.UiScale, r = 7 * u;
            float len = (float)Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            float ang = len > 1e-3 ? (float)(Math.Atan2(dir.Y, dir.X) * 180 / Math.PI) : 0f;
            c.Save();
            c.Translate(at.X, at.Y);
            c.RotateDegrees(ang);
            using (var f = Fill(th.Background))
            using (var o = Stroke(col, 1.6f * u))
            using (var fc = Fill(col))
            {
                switch (g)
                {
                    case ItemGlyph.Pump:
                        c.DrawCircle(0, 0, r, f); c.DrawCircle(0, 0, r, o);
                        using (var p = new SKPath()) { p.MoveTo(-r * 0.45f, -r * 0.55f); p.LineTo(r * 0.7f, 0); p.LineTo(-r * 0.45f, r * 0.55f); p.Close(); c.DrawPath(p, fc); }
                        break;
                    case ItemGlyph.Compressor:
                        using (var p = new SKPath()) { p.MoveTo(-r, -r); p.LineTo(r, -r * 0.5f); p.LineTo(r, r * 0.5f); p.LineTo(-r, r); p.Close(); c.DrawPath(p, f); c.DrawPath(p, o); }
                        break;
                    case ItemGlyph.Valve:
                    case ItemGlyph.ControlValve:
                    case ItemGlyph.Proportioner:
                        using (var p = new SKPath()) { p.MoveTo(-r, -r * 0.6f); p.LineTo(r, r * 0.6f); p.LineTo(r, -r * 0.6f); p.LineTo(-r, r * 0.6f); p.Close(); c.DrawPath(p, f); c.DrawPath(p, o); }
                        if (g == ItemGlyph.ControlValve) { c.DrawLine(0, 0, 0, -r * 1.3f, o); c.DrawArc(new SKRect(-r * 0.7f, -r * 2f, r * 0.7f, -r * 0.9f), 180, 180, false, o); }
                        if (g == ItemGlyph.Proportioner) c.DrawCircle(0, -r * 1.1f, r * 0.4f, o);
                        break;
                    default:
                        c.DrawRect(new SKRect(-r, -r * 0.7f, r, r * 0.7f), f); c.DrawRect(new SKRect(-r, -r * 0.7f, r, r * 0.7f), o);
                        break;
                }
            }
            c.Restore();
        }

        // ------------------------------------------------------------------ labels

        private static void DrawLabels(SKCanvas c, PlantView v, Theme th, Dictionary<int, ScreenPoint> proj, HashSet<int> stopIds)
        {
            float u = v.Options.UiScale;
            var d = v.Drawing;
            var taken = new List<SKRect>();
            // the symbols themselves are taken first, so that labels go around them; a label does not sit on a line either
            foreach (var it in d.Items) taken.Add(ItemBox(v, it, proj[it.Vertex]));
            var segs = d.Legs.Select(l => Tuple.Create(Pt(proj[l.A]), Pt(proj[l.B]))).ToList();
            float pad = 2 * u;
            Func<SKRect, bool> free = r =>
            {
                if (taken.Any(t => t.IntersectsWith(r))) return false;
                var rr = r; rr.Inflate(pad, pad);
                return !segs.Any(s => Crosses(rr, s.Item1, s.Item2));
            };
            using (var p = Text(th.Muted, 11 * u))
            using (var halo = Text(th.Background, 11 * u))
            {
                halo.Style = SKPaintStyle.Stroke; halo.StrokeWidth = 3 * u; halo.StrokeJoin = SKStrokeJoin.Round;
                Func<string, float, float, bool> place = (text, x, y) =>
                {
                    if (string.IsNullOrEmpty(text)) return false;
                    float w = p.MeasureText(text);
                    // right above, left above, right below, left below, centred above and below
                    float[] dx = { 6 * u, -w - 6 * u, 6 * u, -w - 6 * u, -w / 2, -w / 2 };
                    float[] dy = { -4 * u, -4 * u, 14 * u, 14 * u, -12 * u, 18 * u };
                    for (int k = 0; k < dx.Length; k++)
                    {
                        var r = new SKRect(x + dx[k], y + dy[k] - 10 * u, x + dx[k] + w, y + dy[k] + 3 * u);
                        if (r.Left < 0 || r.Top < 0 || r.Right > v.Camera.Width || r.Bottom > v.Camera.Height) continue;
                        if (!free(r)) continue;
                        taken.Add(r);
                        c.DrawText(text, r.Left, r.Bottom - 3 * u, halo);
                        c.DrawText(text, r.Left, r.Bottom - 3 * u, p);
                        return true;
                    }
                    return false;
                };
                // items first, then pipes (once per run, at its longest leg), then nodes
                foreach (var it in d.Items)
                {
                    var b = ItemBox(v, it, proj[it.Vertex]);
                    place(it.Tag, b.Right - 4 * u, b.MidY + 4 * u);
                }
                foreach (var grp in d.Legs.Where(l => l.Kind == LegKind.Pipe).GroupBy(l => string.IsNullOrEmpty(l.BlockName) ? "#" + l.Id : l.BlockName))
                {
                    var l = grp.OrderByDescending(x => d.Length(x)).First();
                    var a = proj[l.A]; var b = proj[l.B];
                    string text = v.LegLabel != null ? v.LegLabel(l) : (l.Tag + (v.Options.ShowSizes && !string.IsNullOrEmpty(l.Pen.Size) ? " " + l.Pen.Size : "")).Trim();
                    place(text, (float)(a.X + b.X) / 2, (float)(a.Y + b.Y) / 2);
                }
                foreach (var l in d.Legs.Where(x => x.Kind == LegKind.Component))
                {
                    var a = proj[l.A]; var b = proj[l.B];
                    place(l.Tag, (float)(a.X + b.X) / 2 + 6 * u, (float)(a.Y + b.Y) / 2 + 4 * u);
                }
                foreach (var x in d.Vertices.Where(x => stopIds.Contains(x.Id) && !d.ItemsAt(x.Id).Any()))
                    place(x.Tag, (float)proj[x.Id].X, (float)proj[x.Id].Y);
            }
        }

        /// <summary>True when the segment from a to b passes through the rectangle (Liang-Barsky clipping).</summary>
        private static bool Crosses(SKRect r, SKPoint a, SKPoint b)
        {
            double t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
            double[] p = { -dx, dx, -dy, dy };
            double[] q = { a.X - r.Left, r.Right - a.X, a.Y - r.Top, r.Bottom - a.Y };
            for (int i = 0; i < 4; i++)
            {
                if (Math.Abs(p[i]) < 1e-12) { if (q[i] < 0) return false; continue; }
                double t = q[i] / p[i];
                if (p[i] < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            return t0 <= t1;
        }

        // ------------------------------------------------------------------ overlays

        private static void Panel(SKCanvas c, SKRect r, Theme th, float u, byte alpha = 255)
        {
            using (var f = Fill(th.Panel.WithAlpha((byte)(th.Panel.Alpha * alpha / 255))))
            using (var o = Stroke(th.PanelBorder.WithAlpha(alpha), 1f * u))
            {
                c.DrawRoundRect(r, 6 * u, 6 * u, f);
                c.DrawRoundRect(r, 6 * u, 6 * u, o);
            }
        }

        private static void DrawLegend(SKCanvas c, PlantView v, Theme th)
        {
            float u = v.Options.UiScale;
            float y = 12 * u;
            foreach (var s in new[] { v.LegScale, v.VertexScale })
            {
                if (s == null || s.IsEmpty) continue;
                using (var title = Text(th.Ink, 12 * u, true))
                using (var lab = Text(th.Muted, 11 * u))
                {
                    string head = s.Title + (string.IsNullOrEmpty(s.Unit) ? "" : " (" + s.Unit + ")");
                    float row = 18 * u, sw = 14 * u;
                    var labels = Enumerable.Range(0, s.Colors.Length).Select(i => s.Format(s.Edges[i]) + " to " + s.Format(s.Edges[i + 1])).ToList();
                    float w = Math.Max(title.MeasureText(head), labels.Max(t => lab.MeasureText(t)) + sw + 8 * u) + 20 * u;
                    float h = 26 * u + row * s.Colors.Length + 4 * u;
                    var r = new SKRect(12 * u, y, 12 * u + w, y + h);
                    Panel(c, r, th, u);
                    c.DrawText(head, r.Left + 10 * u, r.Top + 18 * u, title);
                    // the strongest band on top
                    for (int i = s.Colors.Length - 1, k = 0; i >= 0; i--, k++)
                    {
                        float ry = r.Top + 26 * u + k * row;
                        using (var f = Fill(s.Colors[i])) c.DrawRoundRect(new SKRect(r.Left + 10 * u, ry + 2 * u, r.Left + 10 * u + sw, ry + row - 4 * u), 2 * u, 2 * u, f);
                        c.DrawText(labels[i], r.Left + 10 * u + sw + 8 * u, ry + row - 6 * u, lab);
                    }
                    y = r.Bottom + 8 * u;
                }
            }
        }

        private static void DrawAxes(SKCanvas c, PlantView v, Theme th)
        {
            float u = v.Options.UiScale;
            var o = new SKPoint(46 * u, (float)v.Camera.Height - 46 * u);
            float len = 30 * u;
            var axes = new[] { Tuple.Create(Axis.X, "E"), Tuple.Create(Axis.Y, "N"), Tuple.Create(Axis.Z, "Z") };
            using (var lab = Text(th.Muted, 11 * u, true))
                foreach (var a in axes)
                {
                    double dx, dy; v.Camera.ScreenVector(V3.Of(a.Item1), out dx, out dy);
                    double l = Math.Sqrt(dx * dx + dy * dy);
                    if (l < 1e-6) { c.DrawCircle(o, 2.5f * u, lab); c.DrawText(a.Item2, o.X + 4 * u, o.Y - 4 * u, lab); continue; }
                    var e = new SKPoint(o.X + (float)(dx / v.Camera.Scale) * len, o.Y + (float)(dy / v.Camera.Scale) * len);
                    using (var p = Stroke(th.Muted, 1.6f * u)) c.DrawLine(o, e, p);
                    float tx = e.X + (float)(dx / l) * 8 * u - lab.MeasureText(a.Item2) / 2, ty = e.Y + (float)(dy / l) * 8 * u + 4 * u;
                    c.DrawText(a.Item2, tx, ty, lab);
                }
        }

        private static void DrawMinimap(SKCanvas c, PlantView v, Theme th)
        {
            float u = v.Options.UiScale;
            float w = 190 * u, h = 130 * u;
            var r = new SKRect((float)v.Camera.Width - w - 12 * u, (float)v.Camera.Height - h - 12 * u, (float)v.Camera.Width - 12 * u, (float)v.Camera.Height - 12 * u);
            if (r.Left < 200 * u || r.Top < 200 * u) { v.MinimapRect = SKRect.Empty; return; }
            v.MinimapRect = r;
            Panel(c, r, th, u);
            var mc = new OrbitCamera { AzimuthDeg = v.Camera.AzimuthDeg, ElevationDeg = v.Camera.ElevationDeg, Width = r.Width, Height = r.Height };
            V3 min, max;
            if (!v.Drawing.Bounds(out min, out max)) return;
            mc.Fit(min, max, 10 * u);
            v.MinimapCamera = mc;
            c.Save();
            c.ClipRect(r);
            c.Translate(r.Left, r.Top);
            using (var p = Stroke(th.Muted, 1f * u))
                foreach (var l in v.Drawing.Legs) c.DrawLine(Pt(mc.Project(v.Drawing.P(l.A))), Pt(mc.Project(v.Drawing.P(l.B))), p);
            // the main view's frame
            var corners = new[] { v.Camera.ViewPlanePoint(0, 0), v.Camera.ViewPlanePoint(v.Camera.Width, 0), v.Camera.ViewPlanePoint(v.Camera.Width, v.Camera.Height), v.Camera.ViewPlanePoint(0, v.Camera.Height) };
            using (var path = new SKPath())
            {
                path.MoveTo(Pt(mc.Project(corners[0])));
                for (int i = 1; i < 4; i++) path.LineTo(Pt(mc.Project(corners[i])));
                path.Close();
                using (var f = Fill(th.Accent.WithAlpha(30))) c.DrawPath(path, f);
                using (var o = Stroke(th.Accent, 1.4f * u)) c.DrawPath(path, o);
            }
            c.Restore();
        }

        private static void DrawCard(SKCanvas c, PlantView v, Theme th)
        {
            float u = v.Options.UiScale;
            float right = (float)v.Camera.Width - 12 * u, top = 12 * u;
            float bs = 24 * u;
            v.CardToggleRect = new SKRect(right - bs, top, right, top + bs);
            if (!v.Options.ShowCard || v.Shortcuts.Count == 0)
            {
                v.CardRect = SKRect.Empty;
                if (!v.Options.ShowCardButton) { v.CardToggleRect = SKRect.Empty; return; }
                Panel(c, v.CardToggleRect, th, u);
                using (var p = Text(th.Ink, 13 * u, true)) { float w = p.MeasureText("?"); c.DrawText("?", v.CardToggleRect.MidX - w / 2, v.CardToggleRect.MidY + 5 * u, p); }
                return;
            }
            byte alpha = v.CardHot ? (byte)90 : (byte)255;
            using (var title = Text(th.Ink.WithAlpha(alpha), 12 * u, true))
            using (var grp = Text(th.Muted.WithAlpha(alpha), 10.5f * u, true))
            using (var key = Text(th.Ink.WithAlpha(alpha), 11 * u, true))
            using (var act = Text(th.Muted.WithAlpha(alpha), 11 * u))
            {
                float kw = v.Shortcuts.SelectMany(g => g.Lines).Max(l => key.MeasureText(l.Keys));
                float aw = v.Shortcuts.SelectMany(g => g.Lines).Max(l => act.MeasureText(l.Action));
                float row = 17 * u;
                int lines = v.Shortcuts.Sum(g => g.Lines.Count);
                float w = Math.Max(kw + aw + 14 * u, title.MeasureText(v.CardTitle) + bs) + 24 * u;
                float h = 30 * u + lines * row + v.Shortcuts.Count * (row + 4 * u) + 4 * u;
                var r = new SKRect(right - w, top, right, top + h);
                v.CardRect = r;
                Panel(c, r, th, u, alpha);
                c.DrawText(v.CardTitle, r.Left + 12 * u, r.Top + 19 * u, title);
                // the close button is the same "?" corner
                using (var x = Stroke(th.Muted.WithAlpha(alpha), 1.4f * u))
                {
                    var t = v.CardToggleRect; float m = 8 * u;
                    c.DrawLine(t.Left + m, t.Top + m, t.Right - m, t.Bottom - m, x);
                    c.DrawLine(t.Right - m, t.Top + m, t.Left + m, t.Bottom - m, x);
                }
                float y = r.Top + 30 * u;
                foreach (var g in v.Shortcuts)
                {
                    y += row;
                    c.DrawText(g.Title.ToUpperInvariant(), r.Left + 12 * u, y - 2 * u, grp);
                    y += 4 * u;
                    foreach (var l in g.Lines)
                    {
                        y += row;
                        c.DrawText(l.Keys, r.Left + 12 * u, y - 4 * u, key);
                        c.DrawText(l.Action, r.Left + 12 * u + kw + 14 * u, y - 4 * u, act);
                    }
                }
            }
        }

        private static void DrawTooltip(SKCanvas c, PlantView v, Theme th)
        {
            if (!v.Hover.IsElement || v.MouseX < 0) return;
            string text = v.Tooltip != null ? v.Tooltip(v.Hover) : TagOf(v, v.Hover);
            if (string.IsNullOrEmpty(text)) return;
            float u = v.Options.UiScale;
            var lines = text.Split('\n');
            using (var p = Text(th.Ink, 11.5f * u))
            using (var b = Text(th.Ink, 11.5f * u, true))
            {
                float w = lines.Select((l, i) => (i == 0 ? b : p).MeasureText(l)).Max() + 16 * u;
                float row = 16 * u, h = lines.Length * row + 10 * u;
                float x = (float)v.MouseX + 16 * u, y = (float)v.MouseY + 18 * u;
                if (x + w > v.Camera.Width) x = (float)v.MouseX - w - 10 * u;
                if (y + h > v.Camera.Height) y = (float)v.MouseY - h - 10 * u;
                var r = new SKRect(x, y, x + w, y + h);
                Panel(c, r, th, u);
                for (int i = 0; i < lines.Length; i++) c.DrawText(lines[i], r.Left + 8 * u, r.Top + 5 * u + (i + 1) * row - 4 * u, i == 0 ? b : p);
            }
        }

        public static string TagOf(PlantView v, Hit h)
        {
            var d = v.Drawing;
            switch (h.Kind)
            {
                case HitKind.Leg: var l = d.Leg(h.Id); return l == null ? "" : l.Tag;
                case HitKind.Vertex: var x = d.Vertex(h.Id); return x == null ? "" : x.Tag;
                case HitKind.Item: var i = d.Item(h.Id); return i == null ? "" : i.Tag;
                default: return "";
            }
        }

        // ------------------------------------------------------------------ hit testing

        /// <summary>What is under a screen point: the overlays first, then items, points and legs, the nearest to the
        /// viewer winning among those within reach.</summary>
        public static Hit HitTest(PlantView v, double sx, double sy)
        {
            var pt = new SKPoint((float)sx, (float)sy);
            if (v.CardToggleRect.Contains(pt)) return new Hit(HitKind.CardToggle, 0);
            if (!v.CardRect.IsEmpty && v.CardRect.Contains(pt)) return new Hit(HitKind.Card, 0);
            if (!v.MinimapRect.IsEmpty && v.MinimapRect.Contains(pt)) return new Hit(HitKind.Minimap, 0);
            var d = v.Drawing; var cam = v.Camera;
            float u = v.Options.UiScale;
            Hit best = Hit.None; double bestDepth = double.MinValue;
            foreach (var it in d.Items)
            {
                var s = cam.Project(d.P(it.Vertex));
                var b = ItemBox(v, it, s); b.Inflate(3 * u, 3 * u);
                if (b.Contains(pt) && s.Depth > bestDepth) { best = new Hit(HitKind.Item, it.Id); bestDepth = s.Depth; }
            }
            if (best.Kind != HitKind.None) return best;
            var stops = NodePoints(d);
            double reach = 7 * u, bestDist = double.MaxValue;
            foreach (var x in d.Vertices)
            {
                if (!stops.Contains(x.Id)) continue;
                var s = cam.Project(x.P);
                double dist = Math.Sqrt((s.X - sx) * (s.X - sx) + (s.Y - sy) * (s.Y - sy));
                if (dist <= reach && (dist < bestDist - 1 || (Math.Abs(dist - bestDist) <= 1 && s.Depth > bestDepth))) { best = new Hit(HitKind.Vertex, x.Id); bestDist = dist; bestDepth = s.Depth; }
            }
            if (best.Kind != HitKind.None) return best;
            reach = 6 * u; bestDist = double.MaxValue;
            foreach (var l in d.Legs)
            {
                var a = cam.Project(d.P(l.A)); var b = cam.Project(d.P(l.B));
                double abx = b.X - a.X, aby = b.Y - a.Y, l2 = abx * abx + aby * aby;
                double t = l2 > 1e-9 ? Math.Max(0, Math.Min(1, ((sx - a.X) * abx + (sy - a.Y) * aby) / l2)) : 0;
                double px = a.X + t * abx - sx, py = a.Y + t * aby - sy, dist = Math.Sqrt(px * px + py * py);
                double depth = a.Depth + t * (b.Depth - a.Depth);
                if (dist <= reach && (dist < bestDist - 1 || (Math.Abs(dist - bestDist) <= 1 && depth > bestDepth))) { best = new Hit(HitKind.Leg, l.Id); bestDist = dist; bestDepth = depth; }
            }
            return best;
        }

        /// <summary>The plant point the minimap shows under a screen point; false outside the minimap.</summary>
        public static bool MinimapPoint(PlantView v, double sx, double sy, out V3 p)
        {
            p = V3.Zero;
            if (v.MinimapRect.IsEmpty || v.MinimapCamera == null || !v.MinimapRect.Contains((float)sx, (float)sy)) return false;
            p = v.MinimapCamera.ViewPlanePoint(sx - v.MinimapRect.Left, sy - v.MinimapRect.Top);
            return true;
        }

        /// <summary>Renders the view into a PNG of the given size (device pixels).</summary>
        public static byte[] RenderPng(PlantView v, int width, int height)
        {
            v.Camera.Width = width; v.Camera.Height = height;
            using (var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul)))
            {
                Render(surface.Canvas, v);
                surface.Canvas.Flush();
                using (var img = surface.Snapshot())
                using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
                    return data.ToArray();
            }
        }
    }
}
