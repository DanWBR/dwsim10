using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.Automation.DynamicRunner.Scenarios;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Scenario comparison: two snapshots of the solved flowsheet (taken now, or loaded from a
/// .dwsnap file saved earlier) side by side, the changed specifications first and every result
/// ordered by how much it moved. Solve, capture A, change something, solve, capture B.
/// </summary>
public sealed class ScenarioComparisonWindow : Window
{
    private readonly IFlowsheet _fs;
    private ScenarioSnapshot? _a, _b;
    private ScenarioComparisonResult? _result;
    private readonly TextBlock _labelA = new() { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.85 };
    private readonly TextBlock _labelB = new() { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.85 };
    private readonly TextBox _nameA = new() { Width = 130, Text = "A", Watermark = "label" };
    private readonly TextBox _nameB = new() { Width = 130, Text = "B", Watermark = "label" };
    private readonly CheckBox _onlyChanged = new() { Content = "Only what changed", IsChecked = true };
    private readonly TextBox _filter = new() { Width = 200, Watermark = "filter by object or property" };
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _summary = new() { Spacing = 4, Margin = new Thickness(12, 4, 12, 4) };
    private readonly DataGrid _table = new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserSortColumns = true, GridLinesVisibility = DataGridGridLinesVisibility.All };
    private Button _copy = null!;

    public sealed class Row
    {
        public string Object { get; init; } = "";
        public string Property { get; init; } = "";
        public string Kind { get; init; } = "";
        public string A { get; init; } = "";
        public string B { get; init; } = "";
        public string Change { get; init; } = "";
        public string Percent { get; init; } = "";
        public string Unit { get; init; } = "";
    }

    private static readonly FilePickerFileType SnapType = new("Scenario snapshot") { Patterns = new[] { "*" + ScenarioSnapshot.FileExtension } };

    public ScenarioComparisonWindow(IFlowsheet flowsheet)
    {
        _fs = flowsheet;
        Title = "Scenario Comparison";
        Width = 1200;
        Height = 820;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        Control ScenarioBar(string which, TextBox name, TextBlock label, Action capture, Func<Task> load, Func<Task> save)
        {
            var btnCapture = new Button { Content = "Capture " + which + " now", Width = 140 };
            btnCapture.Classes.Add("dialog");
            btnCapture.Click += (_, _) => capture();
            var btnLoad = new Button { Content = "Load...", Width = 90 };
            btnLoad.Classes.Add("dialog");
            btnLoad.Click += async (_, _) => await load();
            var btnSave = new Button { Content = "Save...", Width = 90 };
            btnSave.Classes.Add("dialog");
            btnSave.Click += async (_, _) => await save();
            return new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 4, 12, 2),
                Children = { new TextBlock { Text = "Scenario " + which, Width = 80, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center }, name, btnCapture, btnLoad, btnSave, label }
            };
        }
        var barA = ScenarioBar("A", _nameA, _labelA, () => Capture(true), () => LoadAsync(true), () => SaveAsync(true));
        var barB = ScenarioBar("B", _nameB, _labelB, () => Capture(false), () => LoadAsync(false), () => SaveAsync(false));

        _copy = new Button { Content = "Copy report", Width = 130, IsEnabled = false };
        _copy.Classes.Add("dialog");
        _copy.Click += async (_, _) =>
        {
            var top = GetTopLevel(this);
            if (top?.Clipboard != null && _result != null) { await top.Clipboard.SetTextAsync(_result.TextReport); _status.Text = "Report copied."; }
        };
        _onlyChanged.IsCheckedChanged += (_, _) => FillTable();
        _filter.TextChanged += (_, _) => FillTable();
        var options = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(12, 6, 12, 4),
            Children = { _onlyChanged, _filter, _copy }
        };
        var hint = new TextBlock
        {
            Margin = new Thickness(12, 0, 12, 4), Opacity = 0.75, TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(11),
            Text = "Solve the flowsheet and capture A. Change a specification, solve again and capture B. The table lists the changed specifications first (marked spec), then every result by how much it moved, in the flowsheet's units. Snapshots can be saved and compared with a later session."
        };
        var top = new StackPanel { Children = { hint, barA, barB, options } };

        void Col(string header, string path) => _table.Columns.Add(new DataGridTextColumn { Header = header, Binding = new global::Avalonia.Data.Binding(path), Width = DataGridLength.Auto });
        Col("object", nameof(Row.Object));
        Col("property", nameof(Row.Property));
        Col("", nameof(Row.Kind));
        Col("A", nameof(Row.A));
        Col("B", nameof(Row.B));
        Col("change", nameof(Row.Change));
        Col("%", nameof(Row.Percent));
        Col("unit", nameof(Row.Unit));

        var body = new DockPanel();
        DockPanel.SetDock(_summary, global::Avalonia.Controls.Dock.Top);
        body.Children.Add(_summary);
        body.Children.Add(_table);

        var bottom = new StackPanel { Margin = new Thickness(12, 0, 12, 8), Children = { _status } };
        var root = new DockPanel();
        DockPanel.SetDock(top, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(bottom);
        root.Children.Add(body);
        UpdateLabels();
        return root;
    }

    private void UpdateLabels()
    {
        _labelA.Text = _a == null ? "not captured" : _a.Values.Count + " values, " + _a.Taken.ToString("g", CultureInfo.CurrentCulture);
        _labelB.Text = _b == null ? "not captured" : _b.Values.Count + " values, " + _b.Taken.ToString("g", CultureInfo.CurrentCulture);
    }

    private void Capture(bool isA)
    {
        try
        {
            var snap = ScenarioComparison.Snapshot(_fs, (isA ? _nameA.Text : _nameB.Text) ?? (isA ? "A" : "B"));
            if (isA) _a = snap; else _b = snap;
            _status.Text = "Captured " + snap.Values.Count + " values into " + (isA ? "A" : "B") + ".";
            UpdateLabels();
            Compare();
        }
        catch (Exception ex) { _status.Text = "Capture failed: " + ex.Message; }
    }

    private async Task LoadAsync(bool isA)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Load a scenario snapshot", AllowMultiple = false, FileTypeFilter = new[] { SnapType } });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be read from that location."; return; }
        try
        {
            var snap = ScenarioSnapshot.LoadFromFile(path);
            if (isA) { _a = snap; _nameA.Text = snap.Label; } else { _b = snap; _nameB.Text = snap.Label; }
            _status.Text = "Loaded " + files[0].Name + " (" + snap.Values.Count + " values).";
            UpdateLabels();
            Compare();
        }
        catch (Exception ex) { _status.Text = "The snapshot could not be loaded: " + ex.Message; }
    }

    private async Task SaveAsync(bool isA)
    {
        var snap = isA ? _a : _b;
        if (snap == null) { _status.Text = "Capture " + (isA ? "A" : "B") + " first."; return; }
        snap.Label = (isA ? _nameA.Text : _nameB.Text) ?? snap.Label;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the scenario snapshot",
            SuggestedFileName = (string.IsNullOrWhiteSpace(_fs.FlowsheetOptions?.FilePath) ? "scenario" : System.IO.Path.GetFileNameWithoutExtension(_fs.FlowsheetOptions.FilePath)) + "-" + (isA ? "A" : "B") + ScenarioSnapshot.FileExtension,
            DefaultExtension = ScenarioSnapshot.FileExtension.TrimStart('.'),
            FileTypeChoices = new[] { SnapType }
        });
        if (file == null) return;
        var path = file.TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be written to that location."; return; }
        try { snap.SaveToFile(path); _status.Text = "Saved " + file.Name + "."; }
        catch (Exception ex) { _status.Text = "The snapshot could not be saved: " + ex.Message; }
    }

    private void Compare()
    {
        _summary.Children.Clear();
        _result = null;
        _copy.IsEnabled = false;
        if (_a == null || _b == null) { FillTable(); return; }
        _a.Label = _nameA.Text ?? "A"; _b.Label = _nameB.Text ?? "B";
        try
        {
            _result = ScenarioComparison.Compare(_a, _b);
            foreach (var s in _result.Summary) _summary.Children.Add(new TextBlock { Text = s, TextWrapping = TextWrapping.Wrap });
            _copy.IsEnabled = true;
        }
        catch (Exception ex) { _status.Text = "Comparison failed: " + ex.Message; }
        FillTable();
    }

    private void FillTable()
    {
        if (_result == null) { _table.ItemsSource = new List<Row>(); return; }
        var ci = CultureInfo.InvariantCulture;
        bool only = _onlyChanged.IsChecked == true;
        string f = (_filter.Text ?? "").Trim();
        string Fmt(double v, string t) => double.IsNaN(v) ? t : v.ToString("G6", ci);
        _table.ItemsSource = _result.Differences
            .Where(d => !only || d.Changed)
            .Where(d => f.Length == 0 || d.ObjectTag.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0 || d.Name.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0)
            .Select(d => new Row
            {
                Object = d.ObjectTag,
                Property = d.Name,
                Kind = d.IsInput ? "spec" : "",
                A = Fmt(d.A, d.TextA),
                B = Fmt(d.B, d.TextB),
                Change = double.IsNaN(d.Delta) ? "" : d.Delta.ToString("G5", ci),
                Percent = double.IsNaN(d.Percent) ? "" : d.Percent.ToString("+0.00;-0.00", ci),
                Unit = d.Unit
            }).ToList();
    }
}
