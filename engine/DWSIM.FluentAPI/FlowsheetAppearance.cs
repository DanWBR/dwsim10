using System;
using DWSIM.Drawing.SkiaSharp;
using DWSIM.Drawing.SkiaSharp.Appearance;
using SkiaSharp;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// The appearance of the flowsheet drawing: its background (a color, a vertical gradient or the interface
    /// theme's), the grid and the layout lock. Everything here is saved with the simulation file and is what the
    /// Flowsheet Appearance window of both interfaces edits.
    /// </summary>
    public sealed partial class Flowsheet
    {
        private GraphicsSurface Surface => Inner.GetSurface() as GraphicsSurface;

        /// <summary>Reads "#RRGGBB" (or "#AARRGGBB", the alpha is dropped).</summary>
        internal static SKColor ParseColor(string color)
        {
            if (color == null || !SKColor.TryParse(color.Trim(), out var c))
                throw new ArgumentException("'" + color + "' is not a color; write it as #RRGGBB.", nameof(color));
            return c.WithAlpha(255);
        }

        /// <summary>A plain background color for the drawing, as "#RRGGBB". Lines and text turn light on a dark color
        /// and dark on a light one.</summary>
        public Flowsheet WithBackground(string color)
        {
            FlowsheetBackground.SetColor(Inner.FlowsheetOptions, ParseColor(color));
            FlowsheetBackground.SetGradientColor(Inner.FlowsheetOptions, null);
            return this;
        }

        /// <summary>A plain background color for the drawing.</summary>
        public Flowsheet WithBackground(byte red, byte green, byte blue)
            => WithBackground(FlowsheetBackground.ToText(new SKColor(red, green, blue)));

        /// <summary>
        /// A vertical gradient from <paramref name="top"/> at the top of the view to <paramref name="bottom"/> at its
        /// bottom, both as "#RRGGBB". The gradient stays put when the view is panned or zoomed. Lines and text follow
        /// the mean of the two ends, so keep both ends light or both dark (see <see cref="FlowsheetBackground.ContrastDiffers"/>).
        /// </summary>
        public Flowsheet WithBackgroundGradient(string top, string bottom)
        {
            FlowsheetBackground.SetColor(Inner.FlowsheetOptions, ParseColor(top));
            FlowsheetBackground.SetGradientColor(Inner.FlowsheetOptions, ParseColor(bottom));
            return this;
        }

        /// <summary>Back to the interface theme's background (white, or dark grey with the dark theme).</summary>
        public Flowsheet WithThemeBackground()
        {
            FlowsheetBackground.SetColor(Inner.FlowsheetOptions, null);
            return this;
        }

        /// <summary>Shows or hides the grid, and snaps moved objects to it or not.</summary>
        public Flowsheet WithGrid(bool show = true, bool snap = false)
        {
            Inner.FlowsheetOptions.FlowsheetDisplayGrid = show;
            Inner.FlowsheetOptions.FlowsheetSnapToGrid = snap;
            var s = Surface;
            if (s != null) { s.ShowGrid = show; s.SnapToGrid = snap; }
            return this;
        }

        /// <summary>
        /// Locks the whole layout: no object can be moved by the mouse, the keyboard, alignment or the automatic
        /// layouts. Panning and zooming are not affected.
        /// </summary>
        public Flowsheet LockLayout(bool locked = true)
        {
            Inner.FlowsheetOptions.FlowsheetLockLayout = locked;
            return this;
        }
    }
}
