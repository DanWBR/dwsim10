using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DWSIM.Automation.DynamicRunner.LiveSliders;
using DWSIM.Automation.DynamicRunner.Scenarios;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Live sliders: a specification of the flowsheet bound to a slider, a few results watched while
/// it moves, every position solved as it comes (positions that arrive while a solve runs are
/// coalesced into the next one). The first watched result is traced against the first slider.
/// </summary>
public sealed class LiveSlidersWindow : Window
{
    private sealed class SliderRow
    {
        public SliderDefinition Def = new();
        public string Label = "";
        public string Unit = "";
        public double Initial;
        public Slider Control = null!;
        public TextBlock ValueText = null!;
    }

    private sealed class WatchRow
    {
        public WatchDefinition Def = new();
        public string Label = "";
        public string Unit = "";
        public TextBlock ValueText = null!;
    }

    private readonly IFlowsheet _fs;
    private readonly string _nf;
    private readonly List<SliderRow> _sliders = new();
    private readonly List<WatchRow> _watches = new();
    private readonly List<LiveSliderSample> _history = new();
    private bool _solving, _pending, _building;
    private double[]? _pendingValues;

    private readonly StackPanel _sliderPanel = new() { Spacing = 6 };
    private readonly StackPanel _watchPanel = new() { Spacing = 4 };
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly XYPlot _plot = new() { MinHeight = 420, Margin = new Thickness(4) };
    private readonly CheckBox _liveBox = new() { Content = "Solve while dragging", IsChecked = true };
    private ComboBox _specObject = null!, _specProperty = null!, _resObject = null!, _resProperty = null!;
    private List<ISimulationObject> _specObjects = new(), _resObjects = new();
    private List<PropertyChoice> _specChoices = new(), _resChoices = new();

    private static readonly FilePickerFileType CaseType = new("Live slider case") { Patterns = new[] { "*" + LiveSliderInput.FileExtension } };

    public LiveSlidersWindow(IFlowsheet flowsheet)
    {
        _fs = flowsheet;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;
        Title = "Live Sliders";
        Width = 1240;
        Height = 820;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
        AutoLoadCase();
    }

    // ---------------------------------------------------------------- layout

    private Control BuildContent()
    {
        _specObjects = LiveSliderStudy.ObjectsWithSpecifications(_fs);
        _resObjects = LiveSliderStudy.ObjectsWithResults(_fs);

        var left = new StackPanel { Spacing = 6, Margin = new Thickness(10, 8, 10, 8) };
        left.Children.Add(new TextBlock { Text = "Sliders", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13) });
        left.Children.Add(new TextBlock { Opacity = 0.75, TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(11), Text = "Only the specifications an object's calculation mode reads are offered; results cannot be dragged. Pick an object, a specification, and add it." });
        _specObject = new ComboBox { Width = 200, ItemsSource = _specObjects.Select(o => o.GraphicObject.Tag).ToList() };
        _specProperty = new ComboBox { Width = 260 };
        _specObject.SelectionChanged += (_, _) => FillSpecProperties();
        var addSlider = new Button { Content = "Add slider", Width = 100 };
        addSlider.Classes.Add("dialog");
        addSlider.Click += (_, _) => AddSliderFromChoice();
        left.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _specObject, _specProperty, addSlider } });
        left.Children.Add(_sliderPanel);

        left.Children.Add(new TextBlock { Text = "Watched results", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13), Margin = new Thickness(0, 10, 0, 0) });
        _resObject = new ComboBox { Width = 200, ItemsSource = _resObjects.Select(o => o.GraphicObject.Tag).ToList() };
        _resProperty = new ComboBox { Width = 260 };
        _resObject.SelectionChanged += (_, _) => FillResultProperties();
        var addWatch = new Button { Content = "Watch", Width = 100 };
        addWatch.Classes.Add("dialog");
        addWatch.Click += (_, _) => AddWatchFromChoice();
        left.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _resObject, _resProperty, addWatch } });
        left.Children.Add(_watchPanel);

        var reset = new Button { Content = "Reset sliders", Width = 120 };
        reset.Classes.Add("dialog");
        reset.Click += (_, _) => { foreach (var s in _sliders) s.Control.Value = s.Initial; Queue(); };
        var solveNow = new Button { Content = "Solve now", Width = 100 };
        solveNow.Classes.Add("dialog");
        solveNow.Click += (_, _) => Queue(true);
        var clear = new Button { Content = "Clear trace", Width = 100 };
        clear.Classes.Add("dialog");
        clear.Click += (_, _) => { _history.Clear(); DrawTrace(); };
        left.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 10, 0, 0), Children = { _liveBox, solveNow, reset, clear } });
        if (_specObjects.Count > 0) _specObject.SelectedIndex = 0;
        if (_resObjects.Count > 0) _resObject.SelectedIndex = 0;

        var load = new Button { Content = "Load case...", Width = 120 };
        load.Classes.Add("dialog");
        load.Click += async (_, _) => await LoadCaseAsync();
        var save = new Button { Content = "Save case...", Width = 120 };
        save.Classes.Add("dialog");
        save.Click += async (_, _) => await SaveCaseAsync();
        var topButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 8, 12, 4), Children = { load, save } };
        var leftDock = new DockPanel();
        DockPanel.SetDock(topButtons, global::Avalonia.Controls.Dock.Top);
        leftDock.Children.Add(topButtons);
        leftDock.Children.Add(new ScrollViewer { Content = left, AllowAutoHide = false });

        var right = new StackPanel { Spacing = 6, Margin = new Thickness(10, 8, 12, 8) };
        right.Children.Add(new TextBlock { Text = "Trace: first watched result against the first slider", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13) });
        right.Children.Add(_plot);
        right.Children.Add(new TextBlock { Opacity = 0.75, TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(11), Text = "Every solved position adds a point. Drag the slider across its range to draw the response curve; a jump in the trace is a phase change, a flat stretch a saturated specification, a gap a position that did not solve." });
        var rightScroll = new ScrollViewer { Content = right, AllowAutoHide = false };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("600,6,*") };
        grid.ColumnDefinitions[0].MinWidth = 380;
        grid.ColumnDefinitions[2].MinWidth = 300;
        Grid.SetColumn(leftDock, 0);
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, Background = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), Margin = new Thickness(0, 8, 0, 8) };
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(rightScroll, 2);
        grid.Children.Add(leftDock);
        grid.Children.Add(splitter);
        grid.Children.Add(rightScroll);

        var bottom = new StackPanel { Margin = new Thickness(12, 0, 12, 8), Children = { _status } };
        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(grid);
        _status.Text = _specObjects.Count == 0 ? "Solve the flowsheet first: no object offers a specification yet." : "Add a slider and a watched result.";
        return root;
    }

    private void FillSpecProperties()
    {
        int i = _specObject.SelectedIndex;
        _specChoices = i >= 0 && i < _specObjects.Count ? LiveSliderStudy.Specifications(_fs, _specObjects[i]) : new List<PropertyChoice>();
        _specProperty.ItemsSource = _specChoices.Select(c => c.Label).ToList();
        if (_specChoices.Count > 0) _specProperty.SelectedIndex = 0;
    }

    private void FillResultProperties()
    {
        int i = _resObject.SelectedIndex;
        _resChoices = i >= 0 && i < _resObjects.Count ? LiveSliderStudy.Results(_fs, _resObjects[i]) : new List<PropertyChoice>();
        _resProperty.ItemsSource = _resChoices.Select(c => c.Label).ToList();
        if (_resChoices.Count > 0) _resProperty.SelectedIndex = 0;
    }

    private void AddSliderFromChoice()
    {
        int i = _specProperty.SelectedIndex;
        if (i < 0 || i >= _specChoices.Count) return;
        var c = _specChoices[i];
        double lo, hi;
        LiveSliderStudy.DefaultRange(c.Value, c.Unit, out lo, out hi);
        AddSlider(new SliderDefinition { ObjectName = c.ObjectName, Property = c.Property, Min = lo, Max = hi });
    }

    private void AddSlider(SliderDefinition def)
    {
        if (_sliders.Any(s => s.Def.ObjectName == def.ObjectName && s.Def.Property == def.Property)) { _status.Text = "That slider is already there."; return; }
        ISimulationObject obj;
        if (!_fs.SimulationObjects.TryGetValue(def.ObjectName, out obj)) return;
        var row = new SliderRow { Def = def, Unit = LiveSliderStudy.GetUnit(_fs, def.ObjectName, def.Property) };
        row.Label = obj.GraphicObject.Tag + ": " + LiveSliderStudy.GetName(_fs, def.Property) + (string.IsNullOrEmpty(row.Unit) ? "" : " (" + row.Unit + ")");
        double current = LiveSliderStudy.GetValue(_fs, def.ObjectName, def.Property);
        if (double.IsNaN(current)) current = def.Min;
        row.Initial = current;
        if (current < def.Min) def.Min = current;
        if (current > def.Max) def.Max = current;

        var minBox = new TextBox { Width = 80, Text = def.Min.ToString(_nf, CultureInfo.CurrentCulture) };
        var maxBox = new TextBox { Width = 80, Text = def.Max.ToString(_nf, CultureInfo.CurrentCulture) };
        row.Control = new Slider { Minimum = def.Min, Maximum = def.Max, Value = current, Width = 260, TickFrequency = (def.Max - def.Min) / 100.0, IsSnapToTickEnabled = false };
        row.ValueText = new TextBlock { Text = current.ToString(_nf, CultureInfo.CurrentCulture), Width = 90, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold };
        row.Control.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty || _building) return;
            row.ValueText.Text = row.Control.Value.ToString(_nf, CultureInfo.CurrentCulture);
            if (_liveBox.IsChecked == true) Queue();
        };
        row.Control.PointerReleased += (_, _) => { if (_liveBox.IsChecked != true) Queue(); };
        minBox.LostFocus += (_, _) => { if (UtilityHelpers.TryVal(minBox.Text, out var v)) { row.Def.Min = v; row.Control.Minimum = Math.Min(v, row.Control.Maximum - 1e-12); } };
        maxBox.LostFocus += (_, _) => { if (UtilityHelpers.TryVal(maxBox.Text, out var v)) { row.Def.Max = v; row.Control.Maximum = Math.Max(v, row.Control.Minimum + 1e-12); } };
        var remove = new Button { Content = "x", Width = 28 };
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = row.Label, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { minBox, row.Control, maxBox, row.ValueText, remove } });
        remove.Click += (_, _) => { _sliders.Remove(row); _sliderPanel.Children.Remove(panel); _history.Clear(); DrawTrace(); };
        _sliders.Add(row);
        _sliderPanel.Children.Add(panel);
        _history.Clear();
        DrawTrace();
    }

    private void AddWatchFromChoice()
    {
        int i = _resProperty.SelectedIndex;
        if (i < 0 || i >= _resChoices.Count) return;
        var c = _resChoices[i];
        AddWatch(new WatchDefinition { ObjectName = c.ObjectName, Property = c.Property });
    }

    private void AddWatch(WatchDefinition def)
    {
        if (_watches.Any(w => w.Def.ObjectName == def.ObjectName && w.Def.Property == def.Property)) return;
        ISimulationObject obj;
        if (!_fs.SimulationObjects.TryGetValue(def.ObjectName, out obj)) return;
        var row = new WatchRow { Def = def, Unit = LiveSliderStudy.GetUnit(_fs, def.ObjectName, def.Property) };
        row.Label = obj.GraphicObject.Tag + ": " + LiveSliderStudy.GetName(_fs, def.Property);
        double current = LiveSliderStudy.GetValue(_fs, def.ObjectName, def.Property);
        row.ValueText = new TextBlock { Text = Fmt(current) + " " + row.Unit, FontSize = UiScale.Font(16), FontWeight = FontWeight.SemiBold, Width = 200, VerticalAlignment = VerticalAlignment.Center };
        var remove = new Button { Content = "x", Width = 28 };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = row.Label, Width = 300, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }, row.ValueText, remove } };
        remove.Click += (_, _) => { _watches.Remove(row); _watchPanel.Children.Remove(panel); _history.Clear(); DrawTrace(); };
        _watches.Add(row);
        _watchPanel.Children.Add(panel);
        _history.Clear();
        DrawTrace();
    }

    private string Fmt(double v) => double.IsNaN(v) ? "-" : v.ToString(_nf, CultureInfo.CurrentCulture);

    // ---------------------------------------------------------------- solving

    private void Queue(bool force = false)
    {
        if (_sliders.Count == 0 && !force) return;
        _pendingValues = _sliders.Select(s => s.Control.Value).ToArray();
        if (_solving) { _pending = true; return; }
        _ = SolveLoopAsync();
    }

    private async Task SolveLoopAsync()
    {
        _solving = true;
        try
        {
            do
            {
                _pending = false;
                var values = _pendingValues ?? new double[0];
                var sliders = _sliders.Select(s => s.Def).ToList();
                var watches = _watches.Select(w => w.Def).ToList();
                _status.Text = "Solving...";
                LiveSliderSample sample;
                try { sample = await Task.Run(() => LiveSliderStudy.Apply(_fs, sliders, values, watches)); }
                catch (Exception ex) { sample = new LiveSliderSample { Inputs = values, Outputs = new double[watches.Count], Error = ex.Message }; }
                for (int i = 0; i < _watches.Count && i < sample.Outputs.Length; i++)
                {
                    _watches[i].ValueText.Text = Fmt(sample.Outputs[i]) + " " + _watches[i].Unit;
                    _watches[i].ValueText.Foreground = sample.Solved ? null : Brushes.DarkOrange;
                }
                _status.Text = sample.Solved ? "Solved in " + sample.Seconds.ToString("F2", CultureInfo.InvariantCulture) + " s." : "Did not solve: " + sample.Error;
                if (sample.Solved) { _history.Add(sample); DrawTrace(); }
            } while (_pending);
        }
        finally { _solving = false; }
    }

    private void DrawTrace()
    {
        _plot.Clear();
        if (_sliders.Count > 0) _plot.XAxisTitle = _sliders[0].Label;
        if (_watches.Count > 0) _plot.YAxisTitle = _watches[0].Label + (string.IsNullOrEmpty(_watches[0].Unit) ? "" : " (" + _watches[0].Unit + ")");
        _plot.PlotTitle = _history.Count == 0 ? "Move a slider to start the trace" : _history.Count + " solved position(s)";
        if (_sliders.Count > 0 && _watches.Count > 0 && _history.Count > 0)
        {
            var pts = _history.Where(h => h.Inputs.Length > 0 && h.Outputs.Length > 0 && !double.IsNaN(h.Outputs[0])).OrderBy(h => h.Inputs[0]).ToList();
            _plot.AddSeries("trace", pts.Select(h => h.Inputs[0]).ToArray(), pts.Select(h => h.Outputs[0]).ToArray());
            _plot.AddSeries("positions", pts.Select(h => h.Inputs[0]).ToArray(), pts.Select(h => h.Outputs[0]).ToArray(), true);
            var last = _history[_history.Count - 1];
            if (!double.IsNaN(last.Outputs[0])) _plot.AddSeries("now", new[] { last.Inputs[0] }, new[] { last.Outputs[0] }, true);
        }
        _plot.InvalidateVisual();
    }

    // ---------------------------------------------------------------- case files

    private void AutoLoadCase()
    {
        var fp = _fs.FlowsheetOptions?.FilePath;
        if (string.IsNullOrWhiteSpace(fp)) return;
        var path = System.IO.Path.ChangeExtension(fp, LiveSliderInput.FileExtension);
        if (!System.IO.File.Exists(path)) return;
        try { ApplyCase(LiveSliderInput.LoadFromFile(path)); _status.Text = "Loaded " + System.IO.Path.GetFileName(path) + "."; }
        catch (Exception ex) { _status.Text = "The case file next to the flowsheet could not be loaded: " + ex.Message; }
    }

    private void ApplyCase(LiveSliderInput input)
    {
        _building = true;
        try
        {
            _sliders.Clear(); _sliderPanel.Children.Clear();
            _watches.Clear(); _watchPanel.Children.Clear();
            foreach (var s in input.Sliders()) AddSlider(s);
            foreach (var w in input.Watches()) AddWatch(w);
            _liveBox.IsChecked = input.SolveWhileDragging;
        }
        finally { _building = false; }
    }

    private async Task LoadCaseAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Load a live slider case", AllowMultiple = false, FileTypeFilter = new[] { CaseType } });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be read from that location."; return; }
        try { ApplyCase(LiveSliderInput.LoadFromFile(path)); _status.Text = "Loaded " + files[0].Name + "."; }
        catch (Exception ex) { _status.Text = "The case could not be loaded: " + ex.Message; }
    }

    private async Task SaveCaseAsync()
    {
        var input = new LiveSliderInput { SolveWhileDragging = _liveBox.IsChecked == true };
        input.SetSliders(_sliders.Select(s => s.Def));
        input.SetWatches(_watches.Select(w => w.Def));
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the live slider case",
            SuggestedFileName = (string.IsNullOrWhiteSpace(_fs.FlowsheetOptions?.FilePath) ? "sliders" : System.IO.Path.GetFileNameWithoutExtension(_fs.FlowsheetOptions.FilePath)) + LiveSliderInput.FileExtension,
            DefaultExtension = LiveSliderInput.FileExtension.TrimStart('.'),
            FileTypeChoices = new[] { CaseType }
        });
        if (file == null) return;
        var path = file.TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be written to that location."; return; }
        try { input.SaveToFile(path); _status.Text = "Saved " + file.Name + "."; }
        catch (Exception ex) { _status.Text = "The case could not be saved: " + ex.Message; }
    }
}
