using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// A single curve or point cloud on an <see cref="XYPlot"/>.
/// </summary>
public sealed class PlotSeries
{
    public string Title = "";
    public IReadOnlyList<double> X = Array.Empty<double>();
    public IReadOnlyList<double> Y = Array.Empty<double>();
    public Color Color = Colors.SteelBlue;
    /// <summary>Draw discrete markers instead of a connected line.</summary>
    public bool Scatter;
    public double MarkerSize = 5.0;
    public double Thickness = 1.6;
    /// <summary>Dash pattern in multiples of the stroke thickness. Null means solid.</summary>
    public double[]? Dashes;

    public int Count => Math.Min(X.Count, Y.Count);
}

/// <summary>
/// Immediate-mode XY line/scatter chart drawn straight into the Avalonia DrawingContext.
///
/// The envelope utilities cannot reuse the PlotModel objects that
/// DWSIM.Thermodynamics.ShortcutUtilities builds: those are OxyPlot types from the net472
/// engine build, and OxyPlot.Avalonia 2.1.0 does not expose Plot.Model to code anyway.
/// The engine also returns every curve as raw data in CalculationResults.Data, so the
/// windows feed that dictionary straight into this control instead.
/// </summary>
public sealed class XYPlot : Control
{
    /// <summary>Series colors, in assignment order. Follows the OxyPlot default cycle.</summary>
    public static readonly Color[] Palette =
    {
        Color.FromRgb(0x4E, 0x9A, 0xD8), // blue
        Color.FromRgb(0xC0, 0x39, 0x2B), // red
        Color.FromRgb(0x27, 0xAE, 0x60), // green
        Color.FromRgb(0x8E, 0x44, 0xAD), // purple
        Color.FromRgb(0xE6, 0x7E, 0x22), // orange
        Color.FromRgb(0x16, 0xA0, 0x85), // teal
        Color.FromRgb(0xD3, 0x5F, 0xB7), // magenta
        Color.FromRgb(0x7F, 0x8C, 0x8D), // gray
    };

    public string PlotTitle = "";
    public string PlotSubtitle = "";
    public string XAxisTitle = "";
    public string YAxisTitle = "";

    public readonly List<PlotSeries> Series = new();

    public XYPlot()
    {
        MinHeight = 200;
        ClipToBounds = true;
        SizeChanged += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>Drops every series and resets the titles.</summary>
    public void Clear()
    {
        Series.Clear();
        PlotTitle = PlotSubtitle = XAxisTitle = YAxisTitle = "";
        InvalidateVisual();
    }

    /// <summary>
    /// Adds a series, skipping non-finite points. Returns null when nothing plottable is left,
    /// which lets callers add optional curves without guarding each one.
    /// </summary>
    public PlotSeries? AddSeries(string title, IReadOnlyList<double>? x, IReadOnlyList<double>? y,
        bool scatter = false, double[]? dashes = null)
    {
        if (x == null || y == null) return null;

        int n = Math.Min(x.Count, y.Count);
        var xs = new List<double>(n);
        var ys = new List<double>(n);
        for (int i = 0; i < n; i++)
        {
            if (double.IsNaN(x[i]) || double.IsInfinity(x[i])) continue;
            if (double.IsNaN(y[i]) || double.IsInfinity(y[i])) continue;
            xs.Add(x[i]);
            ys.Add(y[i]);
        }
        if (xs.Count == 0) return null;

        var s = new PlotSeries
        {
            Title = title,
            X = xs,
            Y = ys,
            Color = Palette[Series.Count % Palette.Length],
            Scatter = scatter,
            Dashes = dashes
        };
        Series.Add(s);
        return s;
    }

    /// <summary>Tab-separated dump of every series, for the clipboard.</summary>
    public string ToDelimitedText()
    {
        var sb = new StringBuilder();
        foreach (var s in Series)
        {
            sb.AppendLine(s.Title);
            sb.AppendLine($"{XAxisTitle}\t{YAxisTitle}");
            for (int i = 0; i < s.Count; i++)
            {
                sb.Append(s.X[i].ToString("G6", CultureInfo.InvariantCulture));
                sb.Append('\t');
                sb.AppendLine(s.Y[i].ToString("G6", CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    private bool IsDark => ActualThemeVariant == ThemeVariant.Dark;

    private IBrush PlotBackground => IsDark
        ? new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25))
        : Brushes.White;

    private IBrush Foreground => IsDark ? Brushes.Gainsboro : Brushes.Black;

    private IBrush GridBrush => IsDark
        ? new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x45))
        : new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xD8));

    public override void Render(DrawingContext ctx)
    {
        var full = new Rect(Bounds.Size);
        ctx.FillRectangle(PlotBackground, full);

        if (Series.Count == 0 || Series.All(s => s.Count == 0))
        {
            DrawText(ctx, "No data. Set up the calculation and press Build.",
                full.Center.X, full.Center.Y - 8, 12, Foreground, TextAlign.Center);
            return;
        }

        var normal = new Typeface(FontFamily.Default);
        var bold = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

        // ---- header (title / subtitle / legend) ------------------------------
        double top = 6;

        if (!string.IsNullOrEmpty(PlotTitle))
        {
            DrawText(ctx, PlotTitle, full.Center.X, top, 14, Foreground, TextAlign.Center, bold);
            top += 20;
        }
        if (!string.IsNullOrEmpty(PlotSubtitle))
        {
            DrawText(ctx, PlotSubtitle, full.Center.X, top, 10, Foreground, TextAlign.Center);
            top += 15;
        }

        top += DrawLegend(ctx, full, top, normal);
        top += 6;

        // ---- axis ranges -----------------------------------------------------
        double xMin = double.MaxValue, xMax = double.MinValue;
        double yMin = double.MaxValue, yMax = double.MinValue;
        foreach (var s in Series)
        {
            for (int i = 0; i < s.Count; i++)
            {
                if (s.X[i] < xMin) xMin = s.X[i];
                if (s.X[i] > xMax) xMax = s.X[i];
                if (s.Y[i] < yMin) yMin = s.Y[i];
                if (s.Y[i] > yMax) yMax = s.Y[i];
            }
        }
        Pad(ref xMin, ref xMax);
        Pad(ref yMin, ref yMax);

        var xTicks = NiceTicks(xMin, xMax, 8);
        var yTicks = NiceTicks(yMin, yMax, 6);

        // Left margin has to fit the widest Y tick label plus the rotated axis title.
        double labelWidth = yTicks
            .Select(t => MeasureWidth(FormatTick(t, yTicks), 10, normal))
            .DefaultIfEmpty(30)
            .Max();

        double left = 12 + (string.IsNullOrEmpty(YAxisTitle) ? 0 : 16) + labelWidth + 8;
        double right = 14;
        double bottom = 22 + (string.IsNullOrEmpty(XAxisTitle) ? 0 : 16);

        var plot = new Rect(left, top,
            Math.Max(1, full.Width - left - right),
            Math.Max(1, full.Height - top - bottom));

        if (plot.Width < 30 || plot.Height < 30) return;

        double sx(double v) => plot.X + (v - xMin) / (xMax - xMin) * plot.Width;
        double sy(double v) => plot.Bottom - (v - yMin) / (yMax - yMin) * plot.Height;

        // ---- grid, ticks, axis labels ---------------------------------------
        var gridPen = new Pen(GridBrush, 1, new DashStyle(new double[] { 3, 3 }, 0));
        var axisPen = new Pen(Foreground, 1);

        foreach (var t in xTicks)
        {
            double x = sx(t);
            ctx.DrawLine(gridPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
            ctx.DrawLine(axisPen, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            DrawText(ctx, FormatTick(t, xTicks), x, plot.Bottom + 5, 10, Foreground, TextAlign.Center);
        }
        foreach (var t in yTicks)
        {
            double y = sy(t);
            ctx.DrawLine(gridPen, new Point(plot.X, y), new Point(plot.Right, y));
            ctx.DrawLine(axisPen, new Point(plot.X - 4, y), new Point(plot.X, y));
            DrawText(ctx, FormatTick(t, yTicks), plot.X - 6, y - 7, 10, Foreground, TextAlign.Right);
        }

        ctx.DrawRectangle(null, axisPen, plot);

        if (!string.IsNullOrEmpty(XAxisTitle))
            DrawText(ctx, XAxisTitle, plot.Center.X, full.Height - 16, 11, Foreground, TextAlign.Center, bold);

        if (!string.IsNullOrEmpty(YAxisTitle))
        {
            using (ctx.PushTransform(
                       Matrix.CreateRotation(-Math.PI / 2) *
                       Matrix.CreateTranslation(14, plot.Center.Y)))
            {
                DrawText(ctx, YAxisTitle, 0, -6, 11, Foreground, TextAlign.Center, bold);
            }
        }

        // ---- series ----------------------------------------------------------
        using (ctx.PushClip(plot))
        {
            foreach (var s in Series)
            {
                var brush = new SolidColorBrush(s.Color);

                if (s.Scatter)
                {
                    double r = s.MarkerSize / 2;
                    for (int i = 0; i < s.Count; i++)
                        ctx.DrawEllipse(brush, null, new Point(sx(s.X[i]), sy(s.Y[i])), r, r);
                    continue;
                }

                if (s.Count == 1)
                {
                    ctx.DrawEllipse(brush, null, new Point(sx(s.X[0]), sy(s.Y[0])), 2.5, 2.5);
                    continue;
                }

                var pen = new Pen(brush, s.Thickness,
                    s.Dashes == null ? null : new DashStyle(s.Dashes, 0),
                    PenLineCap.Round, PenLineJoin.Round);

                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(new Point(sx(s.X[0]), sy(s.Y[0])), false);
                    for (int i = 1; i < s.Count; i++)
                        g.LineTo(new Point(sx(s.X[i]), sy(s.Y[i])));
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, pen, geo);
            }
        }
    }

    /// <summary>Draws the legend under the subtitle. Returns the height it consumed.</summary>
    private double DrawLegend(DrawingContext ctx, Rect full, double top, Typeface face)
    {
        var items = Series.Where(s => !string.IsNullOrEmpty(s.Title) && s.Count > 0).ToList();
        if (items.Count == 0) return 0;

        const double swatch = 14;
        const double gap = 6;
        const double itemGap = 14;
        const double lineHeight = 15;

        // Break the entries into lines that fit the available width.
        var lines = new List<List<(PlotSeries S, double W)>>();
        var current = new List<(PlotSeries, double)>();
        double used = 0;
        double avail = Math.Max(60, full.Width - 20);

        foreach (var s in items)
        {
            double w = swatch + gap + MeasureWidth(s.Title, 10, face);
            if (current.Count > 0 && used + itemGap + w > avail)
            {
                lines.Add(current);
                current = new List<(PlotSeries, double)>();
                used = 0;
            }
            if (current.Count > 0) used += itemGap;
            current.Add((s, w));
            used += w;
        }
        if (current.Count > 0) lines.Add(current);

        double y = top;
        foreach (var line in lines)
        {
            double lineWidth = line.Sum(i => i.W) + itemGap * (line.Count - 1);
            double x = (full.Width - lineWidth) / 2;

            foreach (var (s, w) in line)
            {
                var brush = new SolidColorBrush(s.Color);
                if (s.Scatter)
                {
                    ctx.DrawEllipse(brush, null, new Point(x + swatch / 2, y + 7), 3, 3);
                }
                else
                {
                    var pen = new Pen(brush, 2,
                        s.Dashes == null ? null : new DashStyle(s.Dashes, 0));
                    ctx.DrawLine(pen, new Point(x, y + 7), new Point(x + swatch, y + 7));
                }
                DrawText(ctx, s.Title, x + swatch + gap, y, 10, Foreground, TextAlign.Left, face);
                x += w + itemGap;
            }
            y += lineHeight;
        }

        return y - top;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private enum TextAlign { Left, Center, Right }

    private static void DrawText(DrawingContext ctx, string text, double x, double y,
        double size, IBrush brush, TextAlign align, Typeface? face = null)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            face ?? new Typeface(FontFamily.Default), size, brush);

        double ox = align switch
        {
            TextAlign.Center => x - ft.Width / 2,
            TextAlign.Right => x - ft.Width,
            _ => x
        };
        ctx.DrawText(ft, new Point(ox, y));
    }

    private static double MeasureWidth(string text, double size, Typeface face)
    {
        return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            face, size, Brushes.Black).Width;
    }

    /// <summary>Widens a degenerate range and adds 3% headroom on both ends.</summary>
    private static void Pad(ref double min, ref double max)
    {
        if (double.IsInfinity(min) || double.IsInfinity(max) || min > max)
        {
            min = 0;
            max = 1;
            return;
        }
        if (Math.Abs(max - min) < 1e-12)
        {
            double d = Math.Abs(max) > 1e-12 ? Math.Abs(max) * 0.05 : 0.5;
            min -= d;
            max += d;
            return;
        }
        double pad = (max - min) * 0.03;
        min -= pad;
        max += pad;
    }

    /// <summary>Tick positions on a 1/2/5 x 10^n ladder, covering [min, max].</summary>
    private static List<double> NiceTicks(double min, double max, int target)
    {
        var ticks = new List<double>();
        double range = max - min;
        if (range <= 0 || double.IsNaN(range)) return ticks;

        double raw = range / Math.Max(1, target);
        double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double norm = raw / mag;
        double step = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
        step *= mag;

        double first = Math.Ceiling(min / step) * step;
        for (double t = first; t <= max + step * 1e-6; t += step)
        {
            // Snap away the accumulated floating-point drift so labels stay clean.
            double v = Math.Round(t / step) * step;
            if (v == 0) v = 0; // kill negative zero, which formats as "-0.00"
            if (v >= min && v <= max) ticks.Add(v);
        }
        return ticks;
    }

    /// <summary>Picks a shared decimal count from the tick spacing, then formats one tick.</summary>
    private static string FormatTick(double value, List<double> ticks)
    {
        if (Math.Abs(value) >= 1e5 || (value != 0 && Math.Abs(value) < 1e-3))
            return value.ToString("G4", CultureInfo.CurrentCulture);

        double step = ticks.Count > 1 ? Math.Abs(ticks[1] - ticks[0]) : Math.Abs(value);
        int decimals = step <= 0 ? 2 : Math.Max(0, Math.Min(6, (int)Math.Ceiling(-Math.Log10(step)) + 1));
        return value.ToString("F" + decimals, CultureInfo.CurrentCulture);
    }
}
