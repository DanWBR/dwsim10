using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Automation.DynamicRunner.Insight;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// "Why this result?": for the chosen solved object, the balances and equilibrium relations it
/// satisfied with the flowsheet's numbers in them, the tables and the textbook diagram of the
/// case (Rachford-Rice function, heating curve, T-Q diagram, isenthalpic and isentropic paths,
/// Levenspiel plot). The words and pictures come from UnitInsightStudy.
/// </summary>
public sealed class ExplainResultWindow : Window
{
    private readonly IFlowsheet _fs;
    private readonly List<ISimulationObject> _objects;
    private InsightResult? _result;
    private ComboBox _objectBox = null!;
    private Button _run = null!, _copy = null!;
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _body = new() { Spacing = 6, Margin = new Thickness(12, 8, 14, 8) };

    public ExplainResultWindow(IFlowsheet flowsheet, string? objectName = null)
    {
        _fs = flowsheet;
        _objects = flowsheet.SimulationObjects.Values
            .Where(o => o.GraphicObject != null && UnitInsightStudy.Supports(o))
            .OrderBy(o => o.GraphicObject.Tag, StringComparer.CurrentCultureIgnoreCase).ToList();

        Title = "Explain Result";
        Width = 1100;
        Height = 860;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();

        int idx = objectName == null ? -1 : _objects.FindIndex(o => o.Name == objectName);
        if (idx >= 0) { _objectBox.SelectedIndex = idx; _ = RunAsync(); }
        else if (_objects.Count > 0) _objectBox.SelectedIndex = 0;
    }

    private Control BuildContent()
    {
        _objectBox = new ComboBox { Width = 320, ItemsSource = _objects.Select(o => o.GraphicObject.Tag + "  (" + Kind(o) + ")").ToList() };
        _run = new Button { Content = "Explain", Width = 110, IsDefault = true };
        _run.Classes.Add("dialog");
        _run.Click += async (_, _) => await RunAsync();
        _copy = new Button { Content = "Copy report", Width = 130, IsEnabled = false };
        _copy.Classes.Add("dialog");
        _copy.Click += async (_, _) =>
        {
            var top = GetTopLevel(this);
            if (top?.Clipboard != null && _result != null) { await top.Clipboard.SetTextAsync(_result.TextReport); _status.Text = "Report copied."; }
        };
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 10, 12, 6),
            Children = { new TextBlock { Text = "Object", VerticalAlignment = VerticalAlignment.Center }, _objectBox, _run, _copy }
        };
        var hint = new TextBlock
        {
            Margin = new Thickness(12, 0, 12, 4), Opacity = 0.75, TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(11),
            Text = "Streams, separators, heaters and coolers, heat exchangers, pumps, compressors and expanders, valves, mixers and splitters, reactors, shortcut and rigorous columns. The object must be solved."
        };
        var top = new StackPanel { Children = { bar, hint } };

        var bottom = new StackPanel { Margin = new Thickness(12, 0, 12, 8), Children = { _status } };
        var root = new DockPanel();
        DockPanel.SetDock(top, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = _body, AllowAutoHide = false });
        _body.Children.Add(new TextBlock { Text = _objects.Count == 0 ? "Nothing on the flowsheet can be explained yet." : "Pick an object and press Explain.", Opacity = 0.7 });
        return root;
    }

    private static string Kind(ISimulationObject o) => o.GraphicObject.ObjectType.ToString().Replace("RCT_", "").Replace("NodeIn", "Mixer").Replace("NodeOut", "Splitter");

    private async Task RunAsync()
    {
        int idx = _objectBox.SelectedIndex;
        if (idx < 0 || idx >= _objects.Count) return;
        var obj = _objects[idx];
        _run.IsEnabled = false; _copy.IsEnabled = false;
        _status.Text = "Explaining " + obj.GraphicObject.Tag + "...";
        try
        {
            var r = await Task.Run(() => UnitInsightStudy.Explain(_fs, obj));
            _result = r;
            Show(r);
            _status.Text = r.Warnings.Count == 0 ? "Done." : "Done, with warnings.";
            _copy.IsEnabled = true;
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally { _run.IsEnabled = true; }
    }

    private void Show(InsightResult r)
    {
        _body.Children.Clear();
        _body.Children.Add(new TextBlock { Text = r.Title, FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(15), TextWrapping = TextWrapping.Wrap });
        foreach (var line in r.Lines)
        {
            if (line.StartsWith("## "))
                _body.Children.Add(new TextBlock { Text = line.Substring(3), FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13), Margin = new Thickness(0, 10, 0, 0) });
            else if (line.StartsWith("    "))
                _body.Children.Add(new TextBlock { Text = line.Trim(), FontFamily = new FontFamily("Consolas,Menlo,DejaVu Sans Mono,monospace"), Margin = new Thickness(16, 2, 0, 2), TextWrapping = TextWrapping.Wrap });
            else
                _body.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap });
        }
        foreach (var t in r.Tables) _body.Children.Add(BuildTable(t));
        foreach (var ch in r.Charts)
        {
            var plot = new XYPlot { MinHeight = 380, Margin = new Thickness(0, 6, 0, 6) };
            plot.PlotTitle = ch.Title;
            plot.XAxisTitle = ch.XTitle;
            plot.YAxisTitle = ch.YTitle;
            foreach (var s in ch.Series) plot.AddSeries(s.Title, s.X, s.Y, s.Scatter, s.Dashed ? new[] { 3.0, 3.0 } : null);
            plot.InvalidateVisual();
            _body.Children.Add(plot);
        }
        foreach (var w in r.Warnings)
            _body.Children.Add(new TextBlock { Text = w, Foreground = Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap });
    }

    private static Control BuildTable(InsightTable t)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 4) };
        for (int c = 0; c < t.Columns.Count; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var border = new SolidColorBrush(Color.FromArgb(90, 128, 128, 128));
        void Cell(string text, int row, int col, bool head)
        {
            var b = new Border
            {
                BorderBrush = border, BorderThickness = new Thickness(0, 0, 1, 1), Padding = new Thickness(8, 3, 8, 3),
                Child = new TextBlock { Text = text, FontWeight = head ? FontWeight.SemiBold : FontWeight.Normal, HorizontalAlignment = col == 0 || head ? HorizontalAlignment.Left : HorizontalAlignment.Right }
            };
            Grid.SetRow(b, row); Grid.SetColumn(b, col);
            grid.Children.Add(b);
        }
        for (int c = 0; c < t.Columns.Count; c++) Cell(t.Columns[c], 0, c, true);
        for (int rIdx = 0; rIdx < t.Rows.Count; rIdx++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < t.Columns.Count; c++) Cell(c < t.Rows[rIdx].Length ? t.Rows[rIdx][c] : "", rIdx + 1, c, false);
        }
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(new TextBlock { Text = t.Title, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(new Border { BorderBrush = border, BorderThickness = new Thickness(1, 1, 0, 0), Child = grid, HorizontalAlignment = HorizontalAlignment.Left });
        return panel;
    }
}
