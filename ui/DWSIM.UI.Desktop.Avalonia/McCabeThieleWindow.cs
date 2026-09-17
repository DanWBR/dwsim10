using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DWSIM.Automation.DynamicRunner.McCabeThiele;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// McCabe-Thiele diagram of a binary distillation: the equilibrium curve from a property package
/// of the flowsheet, the operating lines, the q-line and the stages stepped off between them,
/// with the minimum reflux, the minimum stages and the feed stage read straight off the
/// construction. A shortcut column of the flowsheet can fill the inputs in.
/// </summary>
public sealed class McCabeThieleWindow : Window
{
    private readonly IFlowsheet _fs;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;
    private readonly McCabeThieleInput _in = new();
    private McCabeThieleResult? _result;
    private CancellationTokenSource? _cts;

    private readonly List<string> _compounds;
    private readonly List<string> _packages;
    private readonly List<(string Tag, string Name)> _columns;
    private ComboBox _lkBox = null!, _hkBox = null!, _ppBox = null!, _modeBox = null!;
    private TextBox _pBox = null!, _xfBox = null!, _qBox = null!, _xdBox = null!, _xbBox = null!, _rBox = null!, _multBox = null!;
    private Button _run = null!, _cancel = null!, _copy = null!, _load = null!, _save = null!;
    private ScrollViewer _left = null!;
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 6, IsVisible = false };
    private readonly StackPanel _summary = new() { Spacing = 2 };
    private readonly XYPlot _plot = new() { MinHeight = 520, Margin = new Thickness(4) };
    private readonly XYPlot _plotTxy = new() { MinHeight = 300, Margin = new Thickness(4) };
    private readonly DataGrid _table = new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserSortColumns = false, MinHeight = 200, MaxHeight = 420, GridLinesVisibility = DataGridGridLinesVisibility.All };

    public sealed class Row
    {
        public string Stage { get; init; } = "";
        public string X { get; init; } = "";
        public string Y { get; init; } = "";
        public string Temperature { get; init; } = "";
        public string Section { get; init; } = "";
    }

    public McCabeThieleWindow(IFlowsheet flowsheet)
    {
        _fs = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;
        _compounds = flowsheet.SelectedCompounds.Keys.OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase).ToList();
        _packages = flowsheet.PropertyPackages.Values.Select(p => p.Tag).ToList();
        _columns = flowsheet.SimulationObjects.Values
            .Where(o => o.GraphicObject != null && o.GraphicObject.ObjectType == ObjectType.ShortcutColumn)
            .Select(o => (o.GraphicObject.Tag, o.Name))
            .OrderBy(c => c.Tag, StringComparer.CurrentCultureIgnoreCase).ToList();

        if (_compounds.Count >= 2) { _in.LightKey = _compounds[0]; _in.HeavyKey = _compounds[1]; }
        if (_packages.Count > 0) _in.PropertyPackageTag = _packages[0];

        Title = "McCabe-Thiele Diagram";
        Width = 1320;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
        AutoLoadCase();
    }

    // ---------------------------------------------------------------- case files

    private static readonly FilePickerFileType CaseType = new("McCabe-Thiele case") { Patterns = new[] { "*" + McCabeThieleInput.FileExtension } };

    private void AutoLoadCase()
    {
        var fp = _fs.FlowsheetOptions?.FilePath;
        if (string.IsNullOrWhiteSpace(fp)) return;
        var path = System.IO.Path.ChangeExtension(fp, McCabeThieleInput.FileExtension);
        if (!System.IO.File.Exists(path)) return;
        try { ApplyLoadedCase(McCabeThieleInput.LoadFromFile(path), System.IO.Path.GetFileName(path)); }
        catch (Exception ex) { _status.Text = "The case file next to the flowsheet could not be loaded: " + ex.Message; }
    }

    private void ApplyLoadedCase(McCabeThieleInput loaded, string fileName)
    {
        _in.CopyFrom(loaded);
        if (!_compounds.Contains(_in.LightKey) && _compounds.Count > 0) _in.LightKey = _compounds[0];
        if (!_compounds.Contains(_in.HeavyKey) && _compounds.Count > 1) _in.HeavyKey = _compounds[1];
        if (!_packages.Contains(_in.PropertyPackageTag) && _packages.Count > 0) _in.PropertyPackageTag = _packages[0];
        _left.Content = BuildInputPanel();
        _status.Text = "Loaded " + fileName + ".";
    }

    private async Task LoadCaseAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Load a McCabe-Thiele case", AllowMultiple = false, FileTypeFilter = new[] { CaseType } });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be read from that location."; return; }
        try { ApplyLoadedCase(McCabeThieleInput.LoadFromFile(path), files[0].Name); }
        catch (Exception ex) { _status.Text = "The case could not be loaded: " + ex.Message; }
    }

    private async Task SaveCaseAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the McCabe-Thiele case",
            SuggestedFileName = (string.IsNullOrWhiteSpace(_fs.FlowsheetOptions?.FilePath) ? "mccabe-thiele" : System.IO.Path.GetFileNameWithoutExtension(_fs.FlowsheetOptions.FilePath)) + McCabeThieleInput.FileExtension,
            DefaultExtension = McCabeThieleInput.FileExtension.TrimStart('.'),
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

        _run = new Button { Content = "Draw", Width = 110, IsDefault = true };
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

    private AvaloniaEditorPanel BuildInputPanel()
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("System");
        p.CreateAndAddDescriptionRow("The diagram is drawn in the mole fraction of the light key, the more volatile of the two. The equilibrium curve comes from bubble-point flashes of the property package at the column pressure, so it shows whatever the model knows, an azeotrope included.");
        if (_columns.Count > 0)
        {
            var names = new List<string> { "(type the inputs below)" };
            names.AddRange(_columns.Select(c => c.Tag));
            p.CreateAndAddDropDownRow("Fill from a shortcut column", names, 0, (dd, _) => { if (dd.SelectedIndex > 0) FillFromColumn(_columns[dd.SelectedIndex - 1].Name); });
        }
        _ppBox = p.CreateAndAddDropDownRow("Property package", _packages, Math.Max(0, _packages.IndexOf(_in.PropertyPackageTag)), (dd, _) => { if (dd.SelectedIndex >= 0) _in.PropertyPackageTag = _packages[dd.SelectedIndex]; });
        _lkBox = p.CreateAndAddDropDownRow("Light key (more volatile)", _compounds, Math.Max(0, _compounds.IndexOf(_in.LightKey)), (dd, _) => { if (dd.SelectedIndex >= 0) _in.LightKey = _compounds[dd.SelectedIndex]; });
        _hkBox = p.CreateAndAddDropDownRow("Heavy key", _compounds, Math.Max(0, _compounds.IndexOf(_in.HeavyKey)), (dd, _) => { if (dd.SelectedIndex >= 0) _in.HeavyKey = _compounds[dd.SelectedIndex]; });
        _pBox = p.CreateAndAddTextBoxRow(_nf, "Column pressure (" + _su.pressure + ")", Show(_su.pressure, _in.Pressure),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.Pressure = cv.ConvertToSI(_su.pressure, v); });

        p.CreateAndAddLabelRow("Feed and products (mole fraction of the light key)");
        _xfBox = p.CreateAndAddTextBoxRow(_nf, "Feed composition xF", _in.FeedComposition, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.FeedComposition = v; });
        _qBox = p.CreateAndAddTextBoxRow(_nf, "Feed quality q", _in.FeedQuality, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.FeedQuality = v; });
        p.CreateAndAddDescriptionRow("q is the liquid fraction the feed adds to the stripping section: 1 for a saturated liquid, 0 for a saturated vapour, between them for a two-phase feed, above 1 for a subcooled liquid, below 0 for a superheated vapour. The q-line through (xF, xF) has slope q/(q - 1).");
        _xdBox = p.CreateAndAddTextBoxRow(_nf, "Distillate composition xD", _in.DistillateComposition, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.DistillateComposition = v; });
        _xbBox = p.CreateAndAddTextBoxRow(_nf, "Bottoms composition xB", _in.BottomsComposition, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.BottomsComposition = v; });

        p.CreateAndAddLabelRow("Reflux");
        _modeBox = p.CreateAndAddDropDownRow("Give the reflux as", new List<string> { "Reflux ratio R", "A multiple of the minimum, R / Rmin" }, _in.RefluxAsMultipleOfMinimum ? 1 : 0,
            (dd, _) => _in.RefluxAsMultipleOfMinimum = dd.SelectedIndex == 1);
        _rBox = p.CreateAndAddTextBoxRow(_nf, "Reflux ratio R = L/D", _in.RefluxRatio, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.RefluxRatio = v; });
        _multBox = p.CreateAndAddTextBoxRow(_nf, "R / Rmin", _in.RefluxMultiplier, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.RefluxMultiplier = v; });
        p.CreateAndAddDescriptionRow("The rectifying line runs from (xD, xD) with slope R/(R + 1). At the minimum reflux it passes through the point where the q-line meets the equilibrium curve, the pinch, and the stages there never end. Design columns run at 1.2 to 1.5 times the minimum.");

        p.CreateAndAddLabelRow("Stages");
        p.CreateAndAddTextBoxRow(_nf, "Murphree vapour efficiency (1 = ideal stages)", _in.MurphreeEfficiency, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.MurphreeEfficiency = v; });
        p.CreateAndAddDescriptionRow("Below 1 the stages are stepped between the operating line and a pseudo-equilibrium curve that covers only that fraction of the distance to the true curve, which is how real trays fall short of equilibrium.");
        p.CreateAndAddTextBoxRow(_nf, "Points on the equilibrium curve", _in.Points, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.Points = (int)v; });
        return p;
    }

    private Control BuildResultsSide(Control leftDock)
    {
        var right = new StackPanel { Spacing = 6, Margin = new Thickness(10, 8, 12, 8) };
        right.Children.Add(new TextBlock { Text = "Results", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13) });
        right.Children.Add(_summary);
        right.Children.Add(_plot);
        right.Children.Add(_plotTxy);
        right.Children.Add(new TextBlock { Text = "Stages", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13), Margin = new Thickness(0, 8, 0, 0) });
        void Col(string header, string path) => _table.Columns.Add(new DataGridTextColumn { Header = header, Binding = new global::Avalonia.Data.Binding(path), Width = DataGridLength.Auto });
        Col("stage", nameof(Row.Stage));
        Col("x (liquid)", nameof(Row.X));
        Col("y (vapour)", nameof(Row.Y));
        Col("T (" + _su.temperature + ")", nameof(Row.Temperature));
        Col("section", nameof(Row.Section));
        right.Children.Add(_table);
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
        _summary.Children.Add(new TextBlock { Text = "Set the system and the compositions, then Draw.", Opacity = 0.7 });
        return root;
    }

    private double Show(string units, double si) => cv.ConvertFromSI(units, si);

    /// <summary>Reads the keys, the pressure, the reflux and the product purities off a shortcut column, and the feed composition off its feed stream.</summary>
    private void FillFromColumn(string name)
    {
        if (!_fs.SimulationObjects.TryGetValue(name, out var obj)) return;
        try
        {
            McCabeThieleStudy.FillFromShortcutColumn(_fs, obj, _in);
            _left.Content = BuildInputPanel();
            _status.Text = "Inputs taken from " + obj.GraphicObject.Tag + ". The feed composition is the light key over light plus heavy key in its feed.";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not read the column: " + ex.Message;
        }
    }

    // ---------------------------------------------------------------- run

    private async Task RunAsync()
    {
        _cts = new CancellationTokenSource();
        _run.IsEnabled = false; _cancel.IsEnabled = true; _copy.IsEnabled = false;
        _progress.IsVisible = true; _progress.Value = 0;
        _status.Text = "Computing the equilibrium curve...";
        var input = _in;
        var token = _cts.Token;
        try
        {
            var result = await Task.Run(() => McCabeThieleStudy.Run(_fs, input,
                frac => Dispatcher.UIThread.Post(() => { _progress.Value = frac; }), token), token);
            _result = result;
            ShowResult(result);
            _status.Text = result.Feasible ? "Done." : "Done; the construction could not be completed, see the warnings.";
            _copy.IsEnabled = true;
        }
        catch (OperationCanceledException) { _status.Text = "Stopped."; }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally
        {
            _run.IsEnabled = true; _cancel.IsEnabled = false; _progress.IsVisible = false;
        }
    }

    private void ShowResult(McCabeThieleResult r)
    {
        var ci = CultureInfo.InvariantCulture;
        _summary.Children.Clear();
        void Line(string text, bool warn = false) => _summary.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = warn ? Brushes.DarkOrange : null });
        Line("Minimum reflux ratio Rmin = " + r.MinimumReflux.ToString("F3", ci) + "   (pinch at x = " + r.PinchX.ToString("F3", ci) + ", y = " + r.PinchY.ToString("F3", ci) + ")");
        Line("Reflux ratio R = " + r.RefluxRatio.ToString("F3", ci) + (r.MinimumReflux > 0 ? "   (R/Rmin = " + (r.RefluxRatio / r.MinimumReflux).ToString("F2", ci) + ")" : ""));
        Line("Minimum stages at total reflux Nmin = " + r.MinimumStages + " (including the reboiler)");
        Line("Theoretical stages N = " + r.TheoreticalStages + " (including the reboiler), feed on stage " + r.FeedStage + " from the top");
        if (!double.IsNaN(r.Azeotrope)) Line("Azeotrope at x = " + r.Azeotrope.ToString("F3", ci), true);
        foreach (var w in r.Warnings) Line(w, true);

        _plot.Clear();
        _plot.PlotTitle = "McCabe-Thiele diagram: " + _in.LightKey + " / " + _in.HeavyKey;
        _plot.XAxisTitle = "x, mole fraction of " + _in.LightKey + " in the liquid";
        _plot.YAxisTitle = "y, mole fraction of " + _in.LightKey + " in the vapour";
        _plot.AddSeries("Equilibrium", r.EquilibriumX, r.EquilibriumY);
        _plot.AddSeries("y = x", new[] { 0.0, 1.0 }, new[] { 0.0, 1.0 }, false, new[] { 3.0, 3.0 });
        _plot.AddSeries("Rectifying line", r.RectifyingX, r.RectifyingY);
        _plot.AddSeries("Stripping line", r.StrippingX, r.StrippingY);
        _plot.AddSeries("q-line", r.QLineX, r.QLineY, false, new[] { 2.0, 2.0 });
        var stairs = _plot.AddSeries("Stages", r.StairX, r.StairY);
        if (stairs != null) stairs.Thickness = 1.2;
        _plot.AddSeries("Pinch", new[] { r.PinchX }, new[] { r.PinchY }, true);
        _plot.InvalidateVisual();

        _plotTxy.Clear();
        _plotTxy.PlotTitle = "T-x-y at " + Show(_su.pressure, _in.Pressure).ToString(_nf, ci) + " " + _su.pressure;
        _plotTxy.XAxisTitle = "x, y";
        _plotTxy.YAxisTitle = "T (" + _su.temperature + ")";
        _plotTxy.AddSeries("Bubble point", r.EquilibriumX, r.EquilibriumT.Select(t => Show(_su.temperature, t)).ToArray());
        _plotTxy.AddSeries("Dew point", r.EquilibriumX, r.DewT.Select(t => Show(_su.temperature, t)).ToArray());
        _plotTxy.AddSeries("Stages", r.Stages.Select(s => s.X).ToArray(), r.Stages.Select(s => Show(_su.temperature, s.Temperature)).ToArray(), true);
        _plotTxy.InvalidateVisual();

        _table.ItemsSource = r.Stages.Select(s => new Row
        {
            Stage = s.Number.ToString(ci),
            X = s.X.ToString("F4", ci),
            Y = s.Y.ToString("F4", ci),
            Temperature = Show(_su.temperature, s.Temperature).ToString(_nf, ci),
            Section = s.Section
        }).ToList();
    }
}
