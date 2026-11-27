using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SkiaSharp;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    /// <summary>The symbol an item or a component is drawn with.</summary>
    public enum ItemGlyph { Generic, Hydrant, HoseReel, Monitor, Nozzle, Tank, Source, Sink, Pump, Valve, ControlValve, Compressor, Proportioner }

    /// <summary>
    /// A discrete colour scale: the value range split into bands, each with one step of a single-hue ramp (light to
    /// dark on a light background, dark to light on a dark one), so that a larger value always reads stronger.
    /// The ramps are ordinal steps of a blue scale validated for colour-vision deficiency and contrast.
    /// </summary>
    public sealed class ColorScale
    {
        public static readonly SKColor[] LightRamp = { SKColor.Parse("#86b6ef"), SKColor.Parse("#5598e7"), SKColor.Parse("#2a78d6"), SKColor.Parse("#1c5cab"), SKColor.Parse("#0d366b") };
        public static readonly SKColor[] DarkRamp = { SKColor.Parse("#184f95"), SKColor.Parse("#3987e5"), SKColor.Parse("#6da7ec"), SKColor.Parse("#9ec5f4"), SKColor.Parse("#cde2fb") };

        public string Title = "", Unit = "";
        /// <summary>The band edges, one more than the colours.</summary>
        public double[] Edges = new double[0];
        public SKColor[] Colors = new SKColor[0];
        public Func<double, string> Format = v => v.ToString("G4", CultureInfo.InvariantCulture);

        /// <summary>Bands with round edges that cover the values.</summary>
        public static ColorScale Bands(string title, string unit, IEnumerable<double> values, bool dark)
        {
            var ramp = dark ? DarkRamp : LightRamp;
            var vals = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
            var s = new ColorScale { Title = title ?? "", Unit = unit ?? "" };
            if (vals.Count == 0) return s;
            double lo = vals.Min(), hi = vals.Max();
            int n = ramp.Length;
            if (hi - lo < 1e-12 * Math.Max(1, Math.Abs(hi)))
            {
                s.Edges = new[] { lo, hi };
                s.Colors = new[] { ramp[n / 2] };
                return s;
            }
            double step = Nice((hi - lo) / n);
            double start = Math.Floor(lo / step) * step;
            // a step that leaves the top value out grows to the next round one
            while (start + n * step < hi - 1e-12 * Math.Abs(hi)) step = Nice(step * 1.01);
            start = Math.Floor(lo / step) * step;
            int bands = Math.Max(1, Math.Min(n, (int)Math.Ceiling((hi - start) / step - 1e-9)));
            s.Edges = Enumerable.Range(0, bands + 1).Select(i => start + i * step).ToArray();
            // fewer bands than steps take steps spread over the ramp, so the strongest stays the strongest
            s.Colors = Enumerable.Range(0, bands).Select(i => ramp[bands == 1 ? n / 2 : (int)Math.Round(i * (n - 1.0) / (bands - 1))]).ToArray();
            int dec = Math.Max(0, Math.Min(6, (int)Math.Ceiling(-Math.Log10(step) + 1e-9)));
            s.Format = v => v.ToString("N" + dec, CultureInfo.CurrentCulture);
            return s;
        }

        private static double Nice(double x)
        {
            double e = Math.Pow(10, Math.Floor(Math.Log10(x)));
            double f = x / e;
            double nf = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
            return nf * e;
        }

        public bool IsEmpty { get { return Colors.Length == 0; } }

        public SKColor ColorOf(double v)
        {
            if (Colors.Length == 0) return SKColors.Gray;
            for (int i = 0; i < Colors.Length - 1; i++) if (v < Edges[i + 1]) return Colors[i];
            return Colors[Colors.Length - 1];
        }
    }

    public sealed class PlantViewOptions
    {
        public bool ShowGrid = true, ShowLabels = true, ShowSizes = false, ShowLegend = true, ShowMinimap = true, ShowAxes = true;
        /// <summary>The shortcuts card is open (false shows only its "?" button).</summary>
        public bool ShowCard = true;
        /// <summary>The "?" button that opens the card shows while the card is closed (pictures for reports leave it out).</summary>
        public bool ShowCardButton = true;
        public bool Dark;
        /// <summary>Device pixels per interface pixel (the display scaling).</summary>
        public float UiScale = 1f;
    }

    /// <summary>The drawing tools' preview: the leg that a click would draw (or the move of a point), and the point a
    /// click would start from.</summary>
    public sealed class PlantPreview
    {
        public bool HasLeg;
        public V3 From, To;
        /// <summary>The length and axis of the leg, written next to it.</summary>
        public string Label = "";
        public bool HasPoint;
        public V3 Point;
        /// <summary>The leg ends on an existing point or pipe (it joins the network there).</summary>
        public bool Joins;

        public void Clear() { HasLeg = false; HasPoint = false; Joins = false; Label = ""; }
    }

    /// <summary>A kind of item or component the designer can place (the host lists them).</summary>
    public sealed class PlantKind
    {
        public string Id = "", Name = "";
        /// <summary>The block goes inside a pipe (a component leg); otherwise it stands on a point.</summary>
        public bool Inline;
        /// <summary>The length a component takes along its pipe, m.</summary>
        public double Length = 1.0;
    }

    public sealed class ShortcutLine
    {
        public string Keys, Action;
        public ShortcutLine(string keys, string action) { Keys = keys; Action = action; }
    }

    public sealed class ShortcutGroup
    {
        public string Title;
        public readonly List<ShortcutLine> Lines = new List<ShortcutLine>();
        public ShortcutGroup(string title, params ShortcutLine[] lines) { Title = title; Lines.AddRange(lines); }
    }

    public enum HitKind { None, Vertex, Leg, Item, Minimap, CardToggle, Card }

    public struct Hit
    {
        public HitKind Kind;
        public int Id;
        public Hit(HitKind k, int id) { Kind = k; Id = id; }
        public bool IsElement { get { return Kind == HitKind.Vertex || Kind == HitKind.Leg || Kind == HitKind.Item; } }
        public static readonly Hit None = new Hit(HitKind.None, 0);
        public bool Same(Hit o) { return Kind == o.Kind && Id == o.Id; }
    }

    /// <summary>
    /// What a plant view shows: the drawing, the camera, the options, the result colours, the selection and the hover.
    /// The renderer draws it and the controller changes it; the hosts (WinForms, Avalonia) only pass the input on.
    /// </summary>
    public sealed class PlantView
    {
        public PlantDrawing Drawing = new PlantDrawing();
        public readonly OrbitCamera Camera = new OrbitCamera();
        public readonly PlantViewOptions Options = new PlantViewOptions();

        /// <summary>Values per pipe leg and the scale that colours them; empty draws the pipes in ink.</summary>
        public readonly Dictionary<int, double> LegValues = new Dictionary<int, double>();
        public ColorScale LegScale = new ColorScale();
        /// <summary>Values per point and the scale that colours the node dots.</summary>
        public readonly Dictionary<int, double> VertexValues = new Dictionary<int, double>();
        public ColorScale VertexScale = new ColorScale();

        public readonly HashSet<int> SelectedLegs = new HashSet<int>(), SelectedVertices = new HashSet<int>(), SelectedItems = new HashSet<int>();
        public Hit Hover = Hit.None;
        public double MouseX = -1, MouseY = -1;
        /// <summary>The mouse is over the shortcuts card (it fades so that the drawing shows through).</summary>
        public bool CardHot;

        /// <summary>The symbol of an item or component kind.</summary>
        public Func<string, ItemGlyph> GlyphOf = DefaultGlyph;
        /// <summary>The label of a pipe leg (shown once per pipe run, at its longest leg).</summary>
        public Func<PlantLeg, string> LegLabel;
        /// <summary>The hover text of an element (several lines); null shows the tag.</summary>
        public Func<Hit, string> Tooltip;
        public string EmptyMessage = "The drawing is empty.";
        public string CardTitle = "Shortcuts";
        public readonly List<ShortcutGroup> Shortcuts = new List<ShortcutGroup>();

        /// <summary>What the drawing tools show under the mouse (the leg being drawn, the point a click would take).</summary>
        public readonly PlantPreview Preview = new PlantPreview();
        /// <summary>The tool state in one line (tool, axis lock, working level, step), shown at the top; empty hides it.</summary>
        public string ToolChip = "";
        /// <summary>The length being typed, shown next to the cursor; empty hides it.</summary>
        public string Typed = "";
        /// <summary>The items and components the designer can place.</summary>
        public readonly List<PlantKind> Kinds = new List<PlantKind>();
        /// <summary>The item the place tool shows under the mouse (its kind); empty for none.</summary>
        public string PreviewKind = "";

        // the screen rectangles of the overlays, from the last render (device pixels)
        internal SKRect CardRect, CardToggleRect, MinimapRect;
        internal OrbitCamera MinimapCamera;

        /// <summary>Where the minimap was drawn by the last render; empty when it was not.</summary>
        public SKRect MinimapBounds { get { return MinimapRect; } }
        /// <summary>Where the shortcuts card (or its button, when it is closed) was drawn by the last render.</summary>
        public SKRect CardBounds { get { return CardRect.IsEmpty ? CardToggleRect : CardRect; } }

        public void ClearSelection() { SelectedLegs.Clear(); SelectedVertices.Clear(); SelectedItems.Clear(); }

        public bool HasSelection { get { return SelectedLegs.Count + SelectedVertices.Count + SelectedItems.Count > 0; } }

        public bool IsSelected(Hit h)
        {
            switch (h.Kind)
            {
                case HitKind.Leg: return SelectedLegs.Contains(h.Id);
                case HitKind.Vertex: return SelectedVertices.Contains(h.Id);
                case HitKind.Item: return SelectedItems.Contains(h.Id);
                default: return false;
            }
        }

        public void Toggle(Hit h, bool add)
        {
            if (!add) ClearSelection();
            HashSet<int> set = h.Kind == HitKind.Leg ? SelectedLegs : h.Kind == HitKind.Vertex ? SelectedVertices : h.Kind == HitKind.Item ? SelectedItems : null;
            if (set == null) return;
            if (add && set.Contains(h.Id)) set.Remove(h.Id); else set.Add(h.Id);
        }

        /// <summary>Fits the whole drawing on the screen.</summary>
        public void FitAll()
        {
            V3 min, max;
            if (Drawing.Bounds(out min, out max)) Camera.Fit(min, max, 60 * Options.UiScale);
        }

        public static ItemGlyph DefaultGlyph(string kind)
        {
            var k = (kind ?? "").ToLowerInvariant();
            if (k.Contains("hydrant")) return ItemGlyph.Hydrant;
            if (k.Contains("hose")) return ItemGlyph.HoseReel;
            if (k.Contains("monitor")) return ItemGlyph.Monitor;
            if (k.Contains("nozzle") || k.Contains("sprinkler") || k.Contains("foamchamber")) return ItemGlyph.Nozzle;
            if (k.Contains("reservoir") || k.Contains("tank")) return ItemGlyph.Tank;
            if (k.Contains("source")) return ItemGlyph.Source;
            if (k.Contains("sink")) return ItemGlyph.Sink;
            if (k.Contains("pump")) return ItemGlyph.Pump;
            if (k.Contains("pressurecontrol") || k.Contains("choke")) return ItemGlyph.ControlValve;
            if (k.Contains("valve")) return ItemGlyph.Valve;
            if (k.Contains("compressor")) return ItemGlyph.Compressor;
            if (k.Contains("proportioner")) return ItemGlyph.Proportioner;
            return ItemGlyph.Generic;
        }

        private static ShortcutGroup CameraGroup()
        {
            return new ShortcutGroup("View",
                new ShortcutLine("Right drag", "orbit"),
                new ShortcutLine("Middle drag", "pan"),
                new ShortcutLine("Wheel", "zoom at the cursor"),
                new ShortcutLine("1 2 3 4 / 5 6 7", "isometric / plan, front, side"),
                new ShortcutLine("Q / E", "turn 90°"),
                new ShortcutLine("F", "fit the drawing"),
                new ShortcutLine("F1", "hide this card"));
        }

        /// <summary>The shortcuts of the designer's pipe tool.</summary>
        public static List<ShortcutGroup> PipeToolShortcuts()
        {
            return new List<ShortcutGroup>
            {
                new ShortcutGroup("Pipe tool",
                    new ShortcutLine("Click", "start, then fix each corner"),
                    new ShortcutLine("Left drag", "pan"),
                    new ShortcutLine("Type a number, Enter", "exact length (m)"),
                    new ShortcutLine("H / V", "horizontal / vertical only"),
                    new ShortcutLine("X / Y / Z", "one axis (again: free)"),
                    new ShortcutLine("PgUp / PgDn", "working level up / down"),
                    new ShortcutLine("Esc, right click, double-click", "end the run"),
                    new ShortcutLine("S", "select tool")),
                new ShortcutGroup("Edit",
                    new ShortcutLine("Ctrl+Z / Ctrl+Y", "undo / redo")),
                CameraGroup(),
            };
        }

        /// <summary>The shortcuts of the designer's place tool.</summary>
        public static List<ShortcutGroup> PlaceToolShortcuts(PlantKind kind)
        {
            var g = kind != null && kind.Inline
                ? new ShortcutGroup("Place: " + kind.Name,
                    new ShortcutLine("Click a pipe", "put the block inside it"),
                    new ShortcutLine("Left drag", "pan"),
                    new ShortcutLine("Esc / S", "back to the select tool"),
                    new ShortcutLine("P", "pipe tool"))
                : new ShortcutGroup("Place: " + (kind != null ? kind.Name : "item"),
                    new ShortcutLine("Click a pipe end", "put the item there"),
                    new ShortcutLine("Click a pipe", "split it and put the item there"),
                    new ShortcutLine("Click the grid", "put the item on a new point"),
                    new ShortcutLine("Left drag", "pan"),
                    new ShortcutLine("Esc / S", "back to the select tool"),
                    new ShortcutLine("P", "pipe tool"));
            return new List<ShortcutGroup> { g, new ShortcutGroup("Edit", new ShortcutLine("Ctrl+Z / Ctrl+Y", "undo / redo")), CameraGroup() };
        }

        /// <summary>The shortcuts of the designer's select tool.</summary>
        public static List<ShortcutGroup> SelectToolShortcuts()
        {
            return new List<ShortcutGroup>
            {
                new ShortcutGroup("Select tool",
                    new ShortcutLine("Click", "select (Ctrl adds)"),
                    new ShortcutLine("Drag a point", "move it along an axis"),
                    new ShortcutLine("H V X Y Z", "lock the move's axis"),
                    new ShortcutLine("Left drag (empty)", "pan"),
                    new ShortcutLine("Del", "delete the selection"),
                    new ShortcutLine("Double-click", "open the block editor"),
                    new ShortcutLine("P", "pipe tool")),
                new ShortcutGroup("Edit",
                    new ShortcutLine("Ctrl+Z / Ctrl+Y", "undo / redo"),
                    new ShortcutLine("Esc", "clear the selection")),
                CameraGroup(),
            };
        }

        /// <summary>The shortcuts of the viewer.</summary>
        public static List<ShortcutGroup> ViewerShortcuts()
        {
            return new List<ShortcutGroup>
            {
                new ShortcutGroup("Mouse",
                    new ShortcutLine("Right drag", "orbit"),
                    new ShortcutLine("Middle drag", "pan"),
                    new ShortcutLine("Left drag (empty)", "pan"),
                    new ShortcutLine("Wheel", "zoom at the cursor"),
                    new ShortcutLine("Click", "select (Ctrl adds)"),
                    new ShortcutLine("Double-click", "open the block editor"),
                    new ShortcutLine("Click the minimap", "go there")),
                new ShortcutGroup("Keyboard",
                    new ShortcutLine("1 2 3 4", "isometric views"),
                    new ShortcutLine("5 / 6 / 7", "plan / front / side"),
                    new ShortcutLine("Q / E", "turn 90°"),
                    new ShortcutLine("F", "fit the drawing"),
                    new ShortcutLine("L / G", "labels / grid"),
                    new ShortcutLine("Esc", "clear the selection"),
                    new ShortcutLine("F1", "hide this card")),
            };
        }
    }
}
