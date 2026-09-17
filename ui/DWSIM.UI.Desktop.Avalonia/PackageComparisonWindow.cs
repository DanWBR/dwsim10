using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DWSIM.Automation.DynamicRunner.PackageComparison;
using DWSIM.Interfaces;
using DWSIM.PhaseEquilibriumData.Core;
using DWSIM.PhaseEquilibriumData.Index;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Property package comparator: the T-x-y or P-x-y diagram of one binary drawn with every
/// property package of the flowsheet, measured points laid over them (from the local
/// phase-equilibrium database or typed in) and each package scored by its deviation, so the
/// choice of model is made on evidence.
/// </summary>
public sealed class PackageComparisonWindow : Window
{
    private readonly IFlowsheet _fs;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;
    private readonly PackageComparisonInput _in = new();
    private PackageComparisonResult? _result;
    private CancellationTokenSource? _cts;

    private readonly List<string> _compounds;
    private readonly List<string> _packages;
    private readonly HashSet<string> _selected = new();
    private Button _run = null!, _cancel = null!, _copy = null!, _load = null!, _save = null!;
    private ScrollViewer _left = null!;
    private TextBox _dataBox = null!;
    private TextBlock _dataLabel = null!;
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 6, IsVisible = false };
    private readonly StackPanel _summary = new() { Spacing = 4 };
    private readonly XYPlot _plot = new() { MinHeight = 520, Margin = new Thickness(4) };
    private readonly XYPlot _plotDev = new() { MinHeight = 300, Margin = new Thickness(4) };
    private readonly DataGrid _table = new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserSortColumns = false, MinHeight = 120, MaxHeight = 320, GridLinesVisibility = DataGridGridLinesVisibility.All };

    public sealed class Row
    {
        public string Package { get; init; } = "";
        public string Model { get; init; } = "";
        public string Aad { get; init; } = "";
        public string Max { get; init; } = "";
        public string AadY { get; init; } = "";
        public string Points { get; init; } = "";
        public string Azeotrope { get; init; } = "";
    }

    public PackageComparisonWindow(IFlowsheet flowsheet)
    {
        _fs = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;
        _compounds = flowsheet.SelectedCompounds.Keys.OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase).ToList();
        _packages = flowsheet.PropertyPackages.Values.Select(p => p.Tag).ToList();
        foreach (var p in _packages) _selected.Add(p);
        if (_compounds.Count >= 2) { _in.Compound1 = _compounds[0]; _in.Compound2 = _compounds[1]; }

        Title = "Property Package Comparison";
        Width = 1320;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
        AutoLoadCase();
    }

    // ---------------------------------------------------------------- case files

    private static readonly FilePickerFileType CaseType = new("Property package comparison case") { Patterns = new[] { "*" + PackageComparisonInput.FileExtension } };

    private void AutoLoadCase()
    {
        var fp = _fs.FlowsheetOptions?.FilePath;
        if (string.IsNullOrWhiteSpace(fp)) return;
        var path = System.IO.Path.ChangeExtension(fp, PackageComparisonInput.FileExtension);
        if (!System.IO.File.Exists(path)) return;
        try { ApplyLoadedCase(PackageComparisonInput.LoadFromFile(path), System.IO.Path.GetFileName(path)); }
        catch (Exception ex) { _status.Text = "The case file next to the flowsheet could not be loaded: " + ex.Message; }
    }

    private void ApplyLoadedCase(PackageComparisonInput loaded, string fileName)
    {
        _in.CopyFrom(loaded);
        if (!_compounds.Contains(_in.Compound1) && _compounds.Count > 0) _in.Compound1 = _compounds[0];
        if (!_compounds.Contains(_in.Compound2) && _compounds.Count > 1) _in.Compound2 = _compounds[1];
        var wanted = _in.PackageList();
        _selected.Clear();
        foreach (var p in _packages) if (wanted.Count == 0 || wanted.Contains(p)) _selected.Add(p);
        _left.Content = BuildInputPanel();
        _status.Text = "Loaded " + fileName + ".";
    }

    private async Task LoadCaseAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Load a comparison case", AllowMultiple = false, FileTypeFilter = new[] { CaseType } });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be read from that location."; return; }
        try { ApplyLoadedCase(PackageComparisonInput.LoadFromFile(path), files[0].Name); }
        catch (Exception ex) { _status.Text = "The case could not be loaded: " + ex.Message; }
    }

    private async Task SaveCaseAsync()
    {
        if (!ReadData()) return;
        _in.SetPackages(_packages.Where(_selected.Contains));
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the comparison case",
            SuggestedFileName = (string.IsNullOrWhiteSpace(_fs.FlowsheetOptions?.FilePath) ? "package-comparison" : System.IO.Path.GetFileNameWithoutExtension(_fs.FlowsheetOptions.FilePath)) + PackageComparisonInput.FileExtension,
            DefaultExtension = PackageComparisonInput.FileExtension.TrimStart('.'),
            FileTypeChoices = new[] { CaseType }
        });
        if (file == null) return;
        var path = file.TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be written to that location."; return; }
        try { _in.SaveToFile(path); _status.Text = "Saved " + file.Name + "."; }
        catch (Exception ex) { _status.Text = "The case could not be saved: " + ex.Message; }
    }

    // ---------------------------------------------------------------- layout

    private Control BuildContent()
    {
        _left = new ScrollViewer { Content = BuildInputPanel(), Padding = new Thickness(10, 8, 10, 8), AllowAutoHide = false };

        _run = new Button { Content = "Compare", Width = 110, IsDefault = true };
        _run.Classes.Add("dialog");
        _run.Click += async (_, _) => await RunAsync();
        _cancel = new Button { Content = "Stop", Width = 90, IsEnabled = false };
        _cancel.Classes.Add("dialog");
        _cancel.Click += (_, _) => _cts?.Cancel();
        _copy = new Button { Content = "Copy report", Width = 130, IsEnabled = false };
        _copy.Classes.Add("dialog");
        _copy.Click += async (_, _) =>
        {
            var top = GetTopLevel(this);
            if (top?.Clipboard != null && _result != null) { await top.Clipboard.SetTextAsync(_result.TextReport); _status.Text = "Report copied."; }
        };
        _load = new Button { Content = "Load case...", Width = 120 };
        _load.Classes.Add("dialog");
        _load.Click += async (_, _) => await LoadCaseAsync();
        _save = new Button { Content = "Save case...", Width = 120 };
        _save.Classes.Add("dialog");
        _save.Click += async (_, _) => await SaveCaseAsync();
        var topButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 8, 12, 4), Children = { _load, _save } };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 6, 12, 8), Children = { _run, _cancel, _copy } };

        var leftDock = new DockPanel();
        DockPanel.SetDock(topButtons, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        leftDock.Children.Add(topButtons);
        leftDock.Children.Add(buttons);
        leftDock.Children.Add(_left);
        return BuildResultsSide(leftDock);
    }

    private string TUnit => _su.temperature;
    private string PUnit => _su.pressure;

    private AvaloniaEditorPanel BuildInputPanel()
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Binary pair and diagram");
        p.CreateAndAddDescriptionRow("Every selected property package draws the same diagram from its own bubble and dew points. Where the curves part, the model matters; where measured points sit, the data say which model is right.");
        p.CreateAndAddDropDownRow("Compound 1 (x axis)", _compounds, Math.Max(0, _compounds.IndexOf(_in.Compound1)), (dd, _) => { if (dd.SelectedIndex >= 0) _in.Compound1 = _compounds[dd.SelectedIndex]; });
        p.CreateAndAddDropDownRow("Compound 2", _compounds, Math.Max(0, _compounds.IndexOf(_in.Compound2)), (dd, _) => { if (dd.SelectedIndex >= 0) _in.Compound2 = _compounds[dd.SelectedIndex]; });
        p.CreateAndAddDropDownRow("Diagram", new List<string> { "T-x-y at a fixed pressure", "P-x-y at a fixed temperature" }, (int)_in.Kind, (dd, _) => { _in.Kind = (DiagramKind)Math.Max(0, dd.SelectedIndex); _left.Content = BuildInputPanel(); });
        if (_in.Kind == DiagramKind.Txy)
            p.CreateAndAddTextBoxRow(_nf, "Pressure (" + PUnit + ")", cv.ConvertFromSI(PUnit, _in.Pressure), (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.Pressure = cv.ConvertToSI(PUnit, v); });
        else
            p.CreateAndAddTextBoxRow(_nf, "Temperature (" + TUnit + ")", cv.ConvertFromSI(TUnit, _in.Temperature), (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.Temperature = cv.ConvertToSI(TUnit, v); });
        p.CreateAndAddTextBoxRow(_nf, "Points per curve", _in.Points, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.Points = (int)v; });

        p.CreateAndAddLabelRow("Property packages");
        if (_packages.Count == 0) p.CreateAndAddDescriptionRow("The simulation has no property package; add some (Peng-Robinson, NRTL, UNIFAC...) and open this tool again.");
        foreach (var tag in _packages)
        {
            var t = tag;
            var model = _fs.PropertyPackages.Values.First(pp => pp.Tag == t);
            var name = model is DWSIM.Thermodynamics.PropertyPackages.PropertyPackage cp && !string.IsNullOrEmpty(cp.ComponentName) ? cp.ComponentName : model.GetType().Name;
            p.CreateAndAddCheckBoxRow(t + "  (" + name + ")", _selected.Contains(t), (cb, _) => { if (cb.IsChecked == true) _selected.Add(t); else _selected.Remove(t); });
        }
        p.CreateAndAddDescriptionRow("Add packages to the simulation to widen the comparison: an equation of state, an activity coefficient model with fitted parameters, a group-contribution one, Raoult's law as the ideal reference.");

        p.CreateAndAddLabelRow("Measured data");
        _dataLabel = p.CreateAndAddDescriptionRow(DataSummary());
        p.CreateAndAddTwoButtonsRow("From the database...", null, "Clear", null,
            async (_, _) => await LoadFromDatabaseAsync(),
            (_, _) => { _in.DataText = ""; _in.DatasetLabel = ""; _dataBox.Text = ""; _dataLabel.Text = DataSummary(); });
        p.CreateAndAddDescriptionRow("Or type one point per line: x1, y1, T (" + TUnit + "), P (" + PUnit + "). Leave y1 empty when it was not measured.");
        _dataBox = p.CreateAndAddMultilineMonoSpaceTextBoxRow(DataToText(), 160, false, (tb, _) => { });
        return p;
    }

    private string DataSummary()
    {
        int n = _in.Data().Count;
        return n == 0 ? "No measured points loaded. The curves are still drawn and compared with each other." : n + " point(s)" + (string.IsNullOrEmpty(_in.DatasetLabel) ? "" : " from " + _in.DatasetLabel) + ".";
    }

    private string DataToText()
    {
        var ci = CultureInfo.InvariantCulture;
        return string.Join(Environment.NewLine, _in.Data().Select(pt =>
            pt.X1.ToString("G6", ci) + ", " + (double.IsNaN(pt.Y1) ? "" : pt.Y1.ToString("G6", ci)) + ", " +
            cv.ConvertFromSI(TUnit, pt.T).ToString("G6", ci) + ", " + cv.ConvertFromSI(PUnit, pt.P).ToString("G6", ci)));
    }

    /// <summary>Parses the typed points (flowsheet units) into the input, in SI.</summary>
    private bool ReadData()
    {
        var text = _dataBox?.Text ?? "";
        var points = new List<ExperimentalPoint>();
        int lineNo = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNo++;
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(new[] { ',', ';', '\t' });
            if (parts.Length < 3) { _status.Text = "Line " + lineNo + " of the data needs x1, y1, T and P."; return false; }
            double x, y = double.NaN, t = double.NaN, pr = double.NaN;
            if (!UtilityHelpers.TryVal(parts[0], out x)) { _status.Text = "Line " + lineNo + ": x1 is not a number."; return false; }
            if (parts[1].Trim().Length > 0 && !UtilityHelpers.TryVal(parts[1], out y)) { _status.Text = "Line " + lineNo + ": y1 is not a number."; return false; }
            if (parts.Length > 2 && parts[2].Trim().Length > 0 && UtilityHelpers.TryVal(parts[2], out var tv)) t = cv.ConvertToSI(TUnit, tv);
            if (parts.Length > 3 && parts[3].Trim().Length > 0 && UtilityHelpers.TryVal(parts[3], out var pv)) pr = cv.ConvertToSI(PUnit, pv);
            points.Add(new ExperimentalPoint { X1 = x, Y1 = y, T = t, P = pr });
        }
        _in.SetData(points);
        return true;
    }

    private async Task LoadFromDatabaseAsync()
    {
        if (string.IsNullOrEmpty(_in.Compound1) || string.IsNullOrEmpty(_in.Compound2)) { _status.Text = "Choose the two compounds first."; return; }
        if (!PhaseEqBundle.IsInstalled())
        {
            _status.Text = "The local phase-equilibrium database is not installed; download it from the Data Regression tool (Utilities menu), about 94 MB.";
            return;
        }
        var filter = _in.Kind == DiagramKind.Txy ? EquilibriumType.VLE_Isobaric : EquilibriumType.VLE_Isothermal;
        var dlg = new PhaseEqSearchDialog(_in.Compound1, _in.Compound2, filter, "K", "Pa");
        await dlg.ShowDialog(this);
        if (dlg.SelectedPoints == null) return;
        var points = dlg.SelectedPoints.Where(pt => pt.Use).Select(pt => new ExperimentalPoint { X1 = pt.X1, Y1 = pt.Y1 > 0 ? pt.Y1 : double.NaN, T = pt.T, P = pt.P }).ToList();
        _in.SetData(points);
        _in.DatasetLabel = "the phase-equilibrium database (NIST ThermoML)";
        // take the fixed condition off the data when it is consistent
        if (_in.Kind == DiagramKind.Txy && points.Count > 0 && points.All(pt => pt.P > 0)) _in.Pressure = points.Average(pt => pt.P);
        if (_in.Kind == DiagramKind.Pxy && points.Count > 0 && points.All(pt => pt.T > 0)) _in.Temperature = points.Average(pt => pt.T);
        _left.Content = BuildInputPanel();
        _status.Text = points.Count + " point(s) loaded; the fixed " + (_in.Kind == DiagramKind.Txy ? "pressure" : "temperature") + " was set from the dataset.";
    }

    private Control BuildResultsSide(Control leftDock)
    {
        var right = new StackPanel { Spacing = 6, Margin = new Thickness(10, 8, 12, 8) };
        right.Children.Add(new TextBlock { Text = "Results", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13) });
        right.Children.Add(_summary);
        void Col(string header, string path) => _table.Columns.Add(new DataGridTextColumn { Header = header, Binding = new global::Avalonia.Data.Binding(path), Width = DataGridLength.Auto });
        Col("package", nameof(Row.Package));
        Col("model", nameof(Row.Model));
        Col("mean deviation", nameof(Row.Aad));
        Col("largest", nameof(Row.Max));
        Col("mean dev. y1", nameof(Row.AadY));
        Col("points", nameof(Row.Points));
        Col("azeotrope", nameof(Row.Azeotrope));
        right.Children.Add(_table);
        right.Children.Add(_plot);
        right.Children.Add(_plotDev);
        var rightScroll = new ScrollViewer { Content = right, AllowAutoHide = false };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("470,6,*") };
        grid.ColumnDefinitions[0].MinWidth = 320;
        grid.ColumnDefinitions[2].MinWidth = 320;
        Grid.SetColumn(leftDock, 0);
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, Background = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), Margin = new Thickness(0, 8, 0, 8) };
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(rightScroll, 2);
        grid.Children.Add(leftDock);
        grid.Children.Add(splitter);
        grid.Children.Add(rightScroll);

        var bottom = new StackPanel { Margin = new Thickness(12, 0, 12, 8), Spacing = 4 };
        bottom.Children.Add(_progress);
        bottom.Children.Add(_status);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(grid);
        _summary.Children.Add(new TextBlock { Text = "Pick the pair, the packages and (if you have them) the measurements, then Compare.", Opacity = 0.7 });
        return root;
    }

    // ---------------------------------------------------------------- run

    private async Task RunAsync()
    {
        if (!ReadData()) return;
        _in.SetPackages(_packages.Where(_selected.Contains));
        if (_selected.Count == 0) { _status.Text = "Select at least one property package."; return; }
        _cts = new CancellationTokenSource();
        _run.IsEnabled = false; _cancel.IsEnabled = true; _copy.IsEnabled = false;
        _progress.IsVisible = true; _progress.Value = 0;
        _status.Text = "Tracing the diagrams...";
        var input = _in;
        var token = _cts.Token;
        try
        {
            var result = await Task.Run(() => PackageComparisonStudy.Run(_fs, input,
                frac => Dispatcher.UIThread.Post(() => { _progress.Value = frac; }), token), token);
            _result = result;
            ShowResult(result);
            _status.Text = "Done.";
            _copy.IsEnabled = true;
        }
        catch (OperationCanceledException) { _status.Text = "Stopped."; }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally
        {
            _run.IsEnabled = true; _cancel.IsEnabled = false; _progress.IsVisible = false;
        }
    }

    private void ShowResult(PackageComparisonResult r)
    {
        var ci = CultureInfo.InvariantCulture;
        bool txy = r.Kind == DiagramKind.Txy;
        string unitB = txy ? "K" : "%";
        _summary.Children.Clear();
        foreach (var v in r.Verdict) _summary.Children.Add(new TextBlock { Text = v, TextWrapping = TextWrapping.Wrap });
        foreach (var w in r.Warnings) _summary.Children.Add(new TextBlock { Text = w, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkOrange });

        _table.ItemsSource = r.Curves.Select(c => new Row
        {
            Package = c.Tag,
            Model = c.ModelName,
            Aad = c.Failed ? "failed" : double.IsNaN(c.AadBubble) ? "" : c.AadBubble.ToString("F3", ci) + " " + unitB,
            Max = double.IsNaN(c.MaxBubble) ? "" : c.MaxBubble.ToString("F3", ci) + " " + unitB,
            AadY = double.IsNaN(c.AadY) ? "" : c.AadY.ToString("F4", ci),
            Points = c.PointsCompared.ToString(ci),
            Azeotrope = double.IsNaN(c.Azeotrope) ? "" : "x1 = " + c.Azeotrope.ToString("F3", ci)
        }).ToList();

        string yUnit = txy ? TUnit : PUnit;
        Func<double, double> show = v => cv.ConvertFromSI(yUnit, v);
        _plot.Clear();
        _plot.PlotTitle = (txy ? "T-x-y at " + cv.ConvertFromSI(PUnit, r.Pressure).ToString(_nf, ci) + " " + PUnit : "P-x-y at " + cv.ConvertFromSI(TUnit, r.Temperature).ToString(_nf, ci) + " " + TUnit) + ": " + r.Compound1 + " (1) / " + r.Compound2 + " (2)";
        _plot.XAxisTitle = "x1, y1 (mole fraction of " + r.Compound1 + ")";
        _plot.YAxisTitle = (txy ? "T (" : "P (") + yUnit + ")";
        foreach (var c in r.Curves.Where(c => !c.Failed))
        {
            _plot.AddSeries(c.Tag + " bubble", c.X, c.Bubble.Select(show).ToArray());
            _plot.AddSeries(c.Tag + " dew", c.X, c.Dew.Select(show).ToArray(), false, new[] { 3.0, 3.0 });
        }
        var withB = r.Data.Where(pt => (txy ? pt.T : pt.P) > 0).ToList();
        if (withB.Count > 0)
        {
            _plot.AddSeries("measured (x1)", withB.Select(pt => pt.X1).ToArray(), withB.Select(pt => show(txy ? pt.T : pt.P)).ToArray(), true);
            var withY = withB.Where(pt => !double.IsNaN(pt.Y1)).ToList();
            if (withY.Count > 0) _plot.AddSeries("measured (y1)", withY.Select(pt => pt.Y1).ToArray(), withY.Select(pt => show(txy ? pt.T : pt.P)).ToArray(), true);
        }
        _plot.InvalidateVisual();

        _plotDev.Clear();
        _plotDev.PlotTitle = "Deviation from the measurements, bubble " + (txy ? "temperature" : "pressure");
        _plotDev.XAxisTitle = "x1";
        _plotDev.YAxisTitle = txy ? "model - measured (K)" : "model - measured (%)";
        bool any = false;
        foreach (var c in r.Curves.Where(c => !c.Failed && c.DeviationX.Length > 0))
        {
            _plotDev.AddSeries(c.Tag, c.DeviationX, c.DeviationBubble, true);
            any = true;
        }
        if (any) _plotDev.AddSeries("zero", new[] { 0.0, 1.0 }, new[] { 0.0, 0.0 }, false, new[] { 3.0, 3.0 });
        _plotDev.IsVisible = any;
        _plotDev.InvalidateVisual();
    }
}
