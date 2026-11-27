using System;
using System.Diagnostics;

namespace DWSIM.Drawing.SkiaSharp.PlantDesigner
{
    public enum PointerButton { Left, Middle, Right }

    [Flags]
    public enum KeyMods { None = 0, Shift = 1, Ctrl = 2, Alt = 4 }

    /// <summary>
    /// Turns mouse and keyboard input into camera moves and selection on a plant view. The hosts pass device-pixel
    /// coordinates, the wheel in notches and the key names of their toolkit (WinForms Keys and Avalonia Key spell the
    /// letters, digits and function keys the same way). Right drag orbits, middle drag pans, a left drag that starts on
    /// empty space pans, the wheel zooms at the cursor, a click selects (Ctrl adds) and a double-click activates an
    /// element (the host opens its editor) or fits the drawing.
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

        private PointerButton? _button;
        private double _downX, _downY, _lastX, _lastY;
        private bool _moved, _panning;
        private Hit _downHit;

        /// <summary>A press must move this far, device px, before it counts as a drag.</summary>
        public double DragThreshold = 4;

        public PlantViewController(PlantView view)
        {
            View = view;
            if (View.Shortcuts.Count == 0) View.Shortcuts.AddRange(PlantView.ViewerShortcuts());
        }

        public double Now { get { return _clock.Elapsed.TotalSeconds; } }

        private void Redraw() { var h = Invalidated; if (h != null) h(); }

        public void Resize(double width, double height)
        {
            View.Camera.Width = Math.Max(1, width);
            View.Camera.Height = Math.Max(1, height);
            Redraw();
        }

        public void PointerDown(double x, double y, PointerButton button, KeyMods mods)
        {
            _button = button;
            _downX = _lastX = x; _downY = _lastY = y;
            _moved = false;
            _downHit = PlantRenderer.HitTest(View, x, y);
            _panning = button == PointerButton.Middle || (button == PointerButton.Right && (mods & KeyMods.Shift) != 0)
                || (button == PointerButton.Left && _downHit.Kind == HitKind.None);
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
                if (_panning) View.Camera.Pan(dx, dy);
                else if (_button == PointerButton.Right) View.Camera.Orbit(dx, dy);
                else if (_button == PointerButton.Left && _downHit.Kind == HitKind.Minimap) CenterOnMinimap(x, y);
                Redraw();
                return;
            }
            var h = PlantRenderer.HitTest(View, x, y);
            bool cardHot = h.Kind == HitKind.Card;
            if (!h.Same(View.Hover) || cardHot != View.CardHot || h.IsElement)
            {
                bool changed = !h.Same(View.Hover);
                View.Hover = h.IsElement ? h : Hit.None;
                View.CardHot = cardHot;
                if (changed) { var hc = HoverChanged; if (hc != null) hc(View.Hover); }
                Redraw();
            }
        }

        public void PointerUp(double x, double y, PointerButton button, KeyMods mods)
        {
            var b = _button;
            _button = null;
            if (b != button) return;
            if (_moved) return;
            if (button != PointerButton.Left) return;
            var h = PlantRenderer.HitTest(View, x, y);
            if (h.Kind == HitKind.CardToggle) { View.Options.ShowCard = !View.Options.ShowCard; Redraw(); return; }
            if (h.Kind == HitKind.Card) return;
            if (h.Kind == HitKind.Minimap) { CenterOnMinimap(x, y); Redraw(); return; }
            bool add = (mods & KeyMods.Ctrl) != 0;
            if (h.IsElement) View.Toggle(h, add);
            else if (!add) View.ClearSelection();
            var sc = SelectionChanged; if (sc != null) sc();
            Redraw();
        }

        public void PointerLeave()
        {
            View.MouseX = View.MouseY = -1;
            View.Hover = Hit.None; View.CardHot = false;
            Redraw();
        }

        public void DoubleClick(double x, double y)
        {
            var h = PlantRenderer.HitTest(View, x, y);
            if (h.IsElement) { var a = Activated; if (a != null) a(h); }
            else if (h.Kind == HitKind.None) { View.FitAll(); Redraw(); }
        }

        /// <summary>Zooms by wheel notches (positive zooms in) at the cursor.</summary>
        public void Wheel(double notches, double x, double y)
        {
            View.Camera.ZoomAt(x, y, Math.Pow(1.2, notches));
            Redraw();
        }

        private void CenterOnMinimap(double x, double y)
        {
            V3 p;
            if (PlantRenderer.MinimapPoint(View, x, y, out p)) View.Camera.Target = p;
        }

        /// <summary>Handles a key by its name; returns true when it did something.</summary>
        public bool KeyDown(string key, KeyMods mods)
        {
            var k = (key ?? "").ToUpperInvariant();
            if (k.StartsWith("NUMPAD")) k = "D" + k.Substring(6);
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
            Redraw();
            return true;
        }

        /// <summary>Advances the camera animation; returns true while the host should keep ticking.</summary>
        public bool Tick()
        {
            bool running = View.Camera.Tick(Now);
            if (running || View.Camera.Animating) Redraw();
            return View.Camera.Animating;
        }
    }
}
