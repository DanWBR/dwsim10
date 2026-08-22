using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Bar chart of a single property across every object of a chosen type in the flowsheet.
/// Hand-rolled on top of Canvas + Rectangle/TextBlock to avoid OxyPlot.Avalonia's
/// XAML-only API surface (which doesn't expose Plot.Model in the 2.1.0 build).
/// </summary>
public partial class PropertyChartWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private List<ISimulationObject> _currentObjects = new();
    private List<string> _currentProps = new();
    private List<(string Tag, double Value)> _lastRows = new();
    private string _lastUnit = string.Empty;

    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public PropertyChartWindow() : this(null!) { }

    public PropertyChartWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet!;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        PopulateObjectTypes();
        CbObjectType.SelectionChanged += (_, _) => OnObjectTypeChanged();
        BtnPlot.Click += (_, _) => RebuildPlot();
        BtnCopyCsv.Click += async (_, _) =>
        {
            if (_lastRows.Count == 0) return;
            var csv = BuildCsv();
            var top = GetTopLevel(this);
            if (top?.Clipboard != null) await top.Clipboard.SetTextAsync(csv);
            StatusLabel.Text = $"Copied {_lastRows.Count} row(s) to clipboard.";
        };

        PlotSurface.SizeChanged += (_, _) => Render();

        if (CbObjectType.Items.Count > 0) CbObjectType.SelectedIndex = 0;
    }

    private void PopulateObjectTypes()
    {
        var types = _flowsheet.SimulationObjects.Values
            .Select(o => o.GraphicObject?.ObjectType.ToString() ?? "(unknown)")
            .Where(t => t != "(unknown)")
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        CbObjectType.Items.Clear();
        foreach (var t in types) CbObjectType.Items.Add(t);
    }

    private void OnObjectTypeChanged()
    {
        if (CbObjectType.SelectedItem is not string typeName) return;

        _currentObjects = _flowsheet.SimulationObjects.Values
            .Where(o => o.GraphicObject != null
                        && o.GraphicObject.ObjectType.ToString() == typeName)
            .OrderBy(o => o.GraphicObject.Tag)
            .ToList();

        _currentProps = _currentObjects.Count == 0
            ? new List<string>()
            : _currentObjects[0].GetProperties(PropertyType.ALL).OrderBy(p => p).ToList();

        CbProperty.Items.Clear();
        foreach (var p in _currentProps) CbProperty.Items.Add(p);
        if (CbProperty.Items.Count > 0) CbProperty.SelectedIndex = 0;

        StatusLabel.Text = $"{_currentObjects.Count} object(s) of type {typeName}. {_currentProps.Count} property(ies) available.";
    }

    private void RebuildPlot()
    {
        if (_currentObjects.Count == 0 || CbProperty.SelectedItem is not string prop)
        {
            StatusLabel.Text = "Nothing to plot.";
            return;
        }

        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var rows = new List<(string Tag, double Value)>();
        foreach (var o in _currentObjects)
        {
            try
            {
                var raw = o.GetPropertyValue(prop, su);
                if (raw is double d) rows.Add((o.GraphicObject.Tag, d));
                else if (raw is float f) rows.Add((o.GraphicObject.Tag, f));
                else if (raw is int i) rows.Add((o.GraphicObject.Tag, i));
                else if (raw != null && double.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    rows.Add((o.GraphicObject.Tag, v));
            }
            catch { /* skip rows that don't expose the property */ }
        }

        _lastRows = rows;
        _lastUnit = TryGetUnit(prop, _flowsheet);
        Render();
        StatusLabel.Text = $"Plotted {rows.Count} value(s).";
    }

    private void Render()
    {
        PlotSurface.Children.Clear();
        if (_lastRows.Count == 0) return;

        double w = PlotSurface.Bounds.Width;
        double h = PlotSurface.Bounds.Height;
        if (w < 80 || h < 80) return;

        const double leftPad = 70;
        const double rightPad = 20;
        const double topPad = 20;
        const double bottomPad = 60;

        double plotW = w - leftPad - rightPad;
        double plotH = h - topPad - bottomPad;
        if (plotW <= 0 || plotH <= 0) return;

        double maxVal = _lastRows.Max(r => r.Value);
        double minVal = _lastRows.Min(r => r.Value);
        if (maxVal == minVal) { maxVal = minVal + 1; minVal -= 0; }
        if (minVal > 0) minVal = 0; // start bars from zero unless negatives exist

        double range = maxVal - minVal;
        if (range <= 0) range = 1;

        // Axes
        AddLine(leftPad, topPad, leftPad, topPad + plotH, Brushes.Black);
        AddLine(leftPad, topPad + plotH, leftPad + plotW, topPad + plotH, Brushes.Black);

        // Y axis ticks (5)
        for (int t = 0; t <= 5; t++)
        {
            double frac = t / 5.0;
            double v = minVal + frac * range;
            double y = topPad + plotH - frac * plotH;
            AddLine(leftPad - 4, y, leftPad, y, Brushes.Black);
            AddText(v.ToString("G4", CultureInfo.InvariantCulture), 4, y - 7, 60, TextAlignment.Right, fontSize: 10);
        }

        // Y-axis label
        var unitLabel = string.IsNullOrEmpty(_lastUnit)
            ? (CbProperty.SelectedItem?.ToString() ?? "")
            : $"{CbProperty.SelectedItem} ({_lastUnit})";
        var yLabel = new TextBlock
        {
            Text = unitLabel,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            FontWeight = FontWeight.Bold,
            Width = h - bottomPad - topPad
        };
        Canvas.SetLeft(yLabel, -h / 2 + topPad);
        Canvas.SetTop(yLabel, topPad - 4);
        yLabel.RenderTransform = new RotateTransform(-90);
        // (skipping rotated label fitting for simplicity; place a horizontal one instead)
        AddText(unitLabel, leftPad, 2, plotW, TextAlignment.Center, fontSize: 11, bold: true);

        // Bars
        int n = _lastRows.Count;
        double barSlot = plotW / n;
        double barWidth = Math.Max(8, Math.Min(60, barSlot * 0.6));

        for (int i = 0; i < n; i++)
        {
            var (tag, value) = _lastRows[i];
            double frac = (value - minVal) / range;
            double barH = frac * plotH;
            double cx = leftPad + (i + 0.5) * barSlot;
            double left = cx - barWidth / 2;
            double top = topPad + plotH - barH;

            var rect = new Rectangle
            {
                Width = barWidth,
                Height = barH,
                Fill = new SolidColorBrush(Color.FromRgb(70, 130, 180)),
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            PlotSurface.Children.Add(rect);

            // Value label above bar
            AddText(value.ToString("G4", CultureInfo.InvariantCulture),
                cx - 30, top - 14, 60, TextAlignment.Center, fontSize: 9);

            // X-axis tag (rotated 30 deg)
            var tagLabel = new TextBlock
            {
                Text = tag ?? "",
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(10),
                TextWrapping = TextWrapping.NoWrap
            };
            Canvas.SetLeft(tagLabel, cx - 8);
            Canvas.SetTop(tagLabel, topPad + plotH + 6);
            tagLabel.RenderTransform = new RotateTransform(30);
            tagLabel.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
            PlotSurface.Children.Add(tagLabel);
        }
    }

    private void AddLine(double x1, double y1, double x2, double y2, IBrush brush)
    {
        var line = new Line
        {
            StartPoint = new Point(x1, y1),
            EndPoint = new Point(x2, y2),
            Stroke = brush,
            StrokeThickness = 1
        };
        PlotSurface.Children.Add(line);
    }

    private void AddText(string text, double x, double y, double width, TextAlignment align,
        double fontSize = 11, bool bold = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            Width = width,
            TextAlignment = align
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        PlotSurface.Children.Add(tb);
    }

    private static string TryGetUnit(string prop, IFlowsheet fs)
    {
        var su = fs.FlowsheetOptions.SelectedUnitSystem;
        var lower = prop.ToLowerInvariant();
        if (lower.Contains("temperature")) return su.temperature;
        if (lower.Contains("pressure"))    return su.pressure;
        if (lower.Contains("mass flow"))   return su.massflow;
        if (lower.Contains("molar flow"))  return su.molarflow;
        if (lower.Contains("volumetric"))  return su.volumetricFlow;
        if (lower.Contains("heat"))        return su.heatflow;
        if (lower.Contains("area"))        return su.area;
        if (lower.Contains("volume"))      return su.volume;
        if (lower.Contains("length"))      return su.distance;
        return string.Empty;
    }

    private string BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tag,{CbProperty.SelectedItem}");
        foreach (var (tag, value) in _lastRows)
            sb.AppendLine($"{tag},{value.ToString("G6", CultureInfo.InvariantCulture)}");
        return sb.ToString();
    }
}
