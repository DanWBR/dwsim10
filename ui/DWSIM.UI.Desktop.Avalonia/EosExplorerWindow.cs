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
using DWSIM.Automation.DynamicRunner.EosExplorer;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Equation of state explorer: the P-V isotherms of a cubic equation for one compound, with the
/// van der Waals loops below Tc and the Maxwell tie lines across them, the compressibility factor
/// against pressure, and the saturation pressure the equation predicts beside the compound's own
/// vapour pressure correlation.
/// </summary>
public sealed class EosExplorerWindow : Window
{
    private readonly IFlowsheet _fs;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;
    private readonly EosExplorerInput _in = new();
    private EosExplorerResult? _result;
    private CancellationTokenSource? _cts;

    private readonly List<string> _compounds;
    private static readonly List<string> Equations = new() { "van der Waals", "Redlich-Kwong", "Soave-Redlich-Kwong", "Peng-Robinson" };
    private ComboBox _compoundBox = null!;
    private TextBox _tempBox = null!;
    private Button _run = null!, _cancel = null!, _copy = null!, _load = null!, _save = null!;
    private ScrollViewer _left = null!;
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 6, IsVisible = false };
    private readonly StackPanel _summary = new() { Spacing = 2 };
    private readonly XYPlot _plotPV = new() { MinHeight = 440, Margin = new Thickness(4) };
    private readonly XYPlot _plotZ = new() { MinHeight = 300, Margin = new Thickness(4) };
    private readonly XYPlot _plotSat = new() { MinHeight = 300, Margin = new Thickness(4) };
    private readonly DataGrid _table = new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserSortColumns = false, MinHeight = 160, MaxHeight = 320, GridLinesVisibility = DataGridGridLinesVisibility.All };

    public sealed class Row
    {
        public string T { get; init; } = "";
        public string Tr { get; init; } = "";
        public string PsatEos { get; init; } = "";
        public string PsatDb { get; init; } = "";
        public string Deviation { get; init; } = "";
        public string VL { get; init; } = "";
        public string VV { get; init; } = "";
        public string ZL { get; init; } = "";
        public string ZV { get; init; } = "";
    }

    public EosExplorerWindow(IFlowsheet flowsheet)
    {
        _fs = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;
        _compounds = flowsheet.SelectedCompounds.Keys.OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (_compounds.Count > 0) _in.CompoundName = _compounds[0];

        Title = "Equation of State Explorer";
        Width = 1320;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
        AutoLoadCase();
        HelpLinks.AttachF1(this, "eos-explorer");
    }

    // ---------------------------------------------------------------- case files

    private static readonly FilePickerFileType CaseType = new("Equation of state explorer case") { Patterns = new[] { "*" + EosExplorerInput.FileExtension } };

    private void AutoLoadCase()
    {
        var fp = _fs.FlowsheetOptions?.FilePath;
        if (string.IsNullOrWhiteSpace(fp)) return;
        var path = System.IO.Path.ChangeExtension(fp, EosExplorerInput.FileExtension);
        if (!System.IO.File.Exists(path)) return;
        try { ApplyLoadedCase(EosExplorerInput.LoadFromFile(path), System.IO.Path.GetFileName(path)); }
        catch (Exception ex) { _status.Text = "The case file next to the flowsheet could not be loaded: " + ex.Message; }
    }

    private void ApplyLoadedCase(EosExplorerInput loaded, string fileName)
    {
        _in.CopyFrom(loaded);
        if (!_compounds.Contains(_in.CompoundName) && _compounds.Count > 0) _in.CompoundName = _compounds[0];
        _left.Content = BuildInputPanel();
        _status.Text = "Loaded " + fileName + ".";
    }

    private async Task LoadCaseAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Load an explorer case", AllowMultiple = false, FileTypeFilter = new[] { CaseType } });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be read from that location."; return; }
        try { ApplyLoadedCase(EosExplorerInput.LoadFromFile(path), files[0].Name); }
        catch (Exception ex) { _status.Text = "The case could not be loaded: " + ex.Message; }
    }

    private async Task SaveCaseAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the explorer case",
            SuggestedFileName = (string.IsNullOrWhiteSpace(_fs.FlowsheetOptions?.FilePath) ? "eos-explorer" : System.IO.Path.GetFileNameWithoutExtension(_fs.FlowsheetOptions.FilePath)) + EosExplorerInput.FileExtension,
            DefaultExtension = EosExplorerInput.FileExtension.TrimStart('.'),
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

        p.CreateAndAddLabelRow("Compound and equation");
        p.CreateAndAddDescriptionRow("The equation is evaluated in its textbook form, P = RT/(V - b) - a(T)/((V + eps b)(V + sigma b)), from the compound's critical temperature, critical pressure and acentric factor. Nothing else of the property package is used, so what you see is the equation itself.");
        _compoundBox = p.CreateAndAddDropDownRow("Compound", _compounds, Math.Max(0, _compounds.IndexOf(_in.CompoundName)), (dd, _) =>
        {
            if (dd.SelectedIndex < 0) return;
            _in.CompoundName = _compounds[dd.SelectedIndex];
            if (string.IsNullOrWhiteSpace(_in.Temperatures)) ShowDefaultTemperatures();
        });
        p.CreateAndAddDropDownRow("Equation of state", Equations, (int)_in.Equation, (dd, _) => { if (dd.SelectedIndex >= 0) _in.Equation = (CubicEos)dd.SelectedIndex; });
        p.CreateAndAddDescriptionRow("van der Waals (1873) has the loop and the critical point but a poor vapour pressure and Zc = 3/8. Redlich-Kwong adds the 1/sqrt(T) attraction. Soave replaced it by alpha(T, omega) fitted to vapour pressures; Peng-Robinson moved the volume terms to fix liquid densities and gives Zc = 0.307.");

        p.CreateAndAddLabelRow("Isotherms");
        _tempBox = p.CreateAndAddStringEditorRow("Temperatures (" + _su.temperature + "), separated by ;", TemperaturesShown(), (tb, _) => ReadTemperatures(tb.Text));
        p.CreateAndAddButtonRow("Use 0.8, 0.9, 1.0 and 1.1 Tc", null, (_, _) => { _in.Temperatures = ""; ShowDefaultTemperatures(); });
        p.CreateAndAddDescriptionRow("Below Tc the isotherm loops: the equation has three volume roots between the liquid and the vapour, and the flat line the Maxwell construction draws across the loop, at equal fugacity of the two roots, is the saturation pressure the equation predicts. At Tc the loop shrinks to an inflection; above it there is one root at every pressure and no condensation.");
        p.CreateAndAddTextBoxRow(_nf, "Largest volume, in critical volumes", _in.VolumeRangeInCriticalVolumes, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.VolumeRangeInCriticalVolumes = v; });
        p.CreateAndAddTextBoxRow(_nf, "Points per isotherm", _in.Points, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.Points = (int)v; });
        return p;
    }

    private string TemperaturesShown()
    {
        var ci = CultureInfo.InvariantCulture;
        if (string.IsNullOrWhiteSpace(_in.Temperatures))
        {
            var tc = CriticalTemperature();
            if (tc <= 0) return "";
            return string.Join("; ", new[] { 0.8, 0.9, 1.0, 1.1 }.Select(f => Show(_su.temperature, f * tc).ToString("F2", ci)));
        }
        return string.Join("; ", _in.TemperatureList(CriticalTemperature()).Select(t => Show(_su.temperature, t).ToString("F2", ci)));
    }

    private void ShowDefaultTemperatures()
    {
        _in.Temperatures = "";
        if (_tempBox != null) _tempBox.Text = TemperaturesShown();
    }

    private void ReadTemperatures(string? text)
    {
        var ci = CultureInfo.InvariantCulture;
        var list = new List<string>();
        foreach (var part in (text ?? "").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (UtilityHelpers.TryVal(part.Trim(), out var v)) list.Add(cv.ConvertToSI(_su.temperature, v).ToString("R", ci));
        }
        _in.Temperatures = string.Join(";", list);
    }

    private double CriticalTemperature()
    {
        return _fs.SelectedCompounds.TryGetValue(_in.CompoundName ?? "", out var cp) ? cp.Critical_Temperature : 0;
    }

    private Control BuildResultsSide(Control leftDock)
    {
        var right = new StackPanel { Spacing = 6, Margin = new Thickness(10, 8, 12, 8) };
        right.Children.Add(new TextBlock { Text = "Results", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13) });
        right.Children.Add(_summary);
        right.Children.Add(_plotPV);
        right.Children.Add(_plotZ);
        right.Children.Add(_plotSat);
        right.Children.Add(new TextBlock { Text = "Saturation by the Maxwell construction", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13), Margin = new Thickness(0, 8, 0, 0) });
        void Col(string header, string path) => _table.Columns.Add(new DataGridTextColumn { Header = header, Binding = new global::Avalonia.Data.Binding(path), Width = DataGridLength.Auto });
        Col("T (" + _su.temperature + ")", nameof(Row.T));
        Col("Tr", nameof(Row.Tr));
        Col("Psat, equation (" + _su.pressure + ")", nameof(Row.PsatEos));
        Col("Psat, database (" + _su.pressure + ")", nameof(Row.PsatDb));
        Col("deviation (%)", nameof(Row.Deviation));
        Col("V liquid (cm3/mol)", nameof(Row.VL));
        Col("V vapour (cm3/mol)", nameof(Row.VV));
        Col("Z liquid", nameof(Row.ZL));
        Col("Z vapour", nameof(Row.ZV));
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
        _summary.Children.Add(new TextBlock { Text = "Pick a compound and an equation, then Draw.", Opacity = 0.7 });
        return root;
    }

    private double Show(string units, double si) => cv.ConvertFromSI(units, si);

    // ---------------------------------------------------------------- run

    private async Task RunAsync()
    {
        ReadTemperatures(_tempBox.Text);
        _cts = new CancellationTokenSource();
        _run.IsEnabled = false; _cancel.IsEnabled = true; _copy.IsEnabled = false;
        _progress.IsVisible = true; _progress.Value = 0;
        _status.Text = "Drawing...";
        var input = _in;
        var token = _cts.Token;
        try
        {
            var result = await Task.Run(() => EosExplorerStudy.Run(_fs, input,
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

    private void ShowResult(EosExplorerResult r)
    {
        var ci = CultureInfo.InvariantCulture;
        _summary.Children.Clear();
        void Line(string text, bool warn = false) => _summary.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = warn ? Brushes.DarkOrange : null });
        Line(r.CompoundName + ", " + EosExplorerStudy.EquationName(r.Equation) + ": Tc = " + Show(_su.temperature, r.Tc).ToString(_nf, ci) + " " + _su.temperature +
             ", Pc = " + Show(_su.pressure, r.Pc).ToString(_nf, ci) + " " + _su.pressure + ", omega = " + r.Omega.ToString("F4", ci));
        Line("b = " + (r.B * 1e6).ToString("F2", ci) + " cm3/mol, a(Tc) = " + r.A.ToString("E4", ci) + " Pa m6/mol2");
        Line("Zc of the equation = " + r.EquationZc.ToString("F4", ci) + (double.IsNaN(r.DatabaseZc) ? "" : ", database Zc = " + r.DatabaseZc.ToString("F4", ci)) +
             "; Vc of the equation = " + (r.EquationVc * 1e6).ToString("F1", ci) + " cm3/mol" + (double.IsNaN(r.DatabaseVc) ? "" : ", database Vc = " + (r.DatabaseVc * 1e6).ToString("F1", ci) + " cm3/mol"));
        Line("Every cubic gives one Zc for every compound; real fluids sit between 0.23 and 0.29, which is why the volume the equation predicts at the critical point is too large.");
        foreach (var w in r.Warnings) Line(w, true);

        // P-V isotherms in reduced coordinates: the loops of every compound look alike that way
        _plotPV.Clear();
        _plotPV.PlotTitle = "P-V isotherms, " + EosExplorerStudy.EquationName(r.Equation);
        _plotPV.XAxisTitle = "V / Vc (Vc of the equation)";
        _plotPV.YAxisTitle = "P / Pc";
        double vc = r.EquationVc;
        foreach (var iso in r.Isotherms)
        {
            var vr = iso.Volume.Select(v => v / vc).ToArray();
            // the loop dives below -Pc on a stiff liquid branch; clip it so the plot keeps its scale
            var pr = iso.Pressure.Select(p => p / r.Pc < -1.0 ? double.NaN : p / r.Pc).ToArray();
            _plotPV.AddSeries("T = " + Show(_su.temperature, iso.Temperature).ToString("F1", ci) + " " + _su.temperature + " (Tr " + iso.ReducedTemperature.ToString("F2", ci) + ")", vr, pr);
        }
        foreach (var iso in r.Isotherms.Where(i => i.Subcritical && !double.IsNaN(i.SaturationPressure)))
        {
            var tie = _plotPV.AddSeries("Maxwell tie line, Tr " + iso.ReducedTemperature.ToString("F2", ci),
                new[] { iso.LiquidVolume / vc, iso.VaporVolume / vc }, new[] { iso.SaturationPressure / r.Pc, iso.SaturationPressure / r.Pc }, false, new[] { 4.0, 2.0 });
            if (tie != null) { tie.Color = Colors.DimGray; tie.Thickness = 1.2; }
        }
        var cp = _plotPV.AddSeries("Critical point", new[] { 1.0 }, new[] { 1.0 }, true);
        if (cp != null) cp.Color = Colors.Black;
        _plotPV.InvalidateVisual();

        _plotZ.Clear();
        _plotZ.PlotTitle = "Compressibility factor";
        _plotZ.XAxisTitle = "P / Pc";
        _plotZ.YAxisTitle = "Z = PV/RT";
        foreach (var iso in r.Isotherms)
            _plotZ.AddSeries("Tr " + iso.ReducedTemperature.ToString("F2", ci), iso.ZPressure.Select(p => p / r.Pc).ToArray(), iso.Z);
        _plotZ.InvalidateVisual();

        _plotSat.Clear();
        _plotSat.PlotTitle = "Saturation pressure: the equation against the database";
        _plotSat.XAxisTitle = "T (" + _su.temperature + ")";
        _plotSat.YAxisTitle = "Psat (" + _su.pressure + ")";
        _plotSat.AddSeries(EosExplorerStudy.EquationName(r.Equation), r.SaturationT.Select(t => Show(_su.temperature, t)).ToArray(), r.SaturationP.Select(p => Show(_su.pressure, p)).ToArray());
        _plotSat.AddSeries("Database correlation", r.SaturationT.Select(t => Show(_su.temperature, t)).ToArray(), r.SaturationPDatabase.Select(p => Show(_su.pressure, p)).ToArray(), false, new[] { 3.0, 3.0 });
        _plotSat.InvalidateVisual();

        _table.ItemsSource = r.Isotherms.Select(iso => new Row
        {
            T = Show(_su.temperature, iso.Temperature).ToString(_nf, ci),
            Tr = iso.ReducedTemperature.ToString("F3", ci),
            PsatEos = iso.Subcritical ? Show(_su.pressure, iso.SaturationPressure).ToString(_nf, ci) : "supercritical",
            PsatDb = iso.Subcritical && !double.IsNaN(iso.DatabaseSaturationPressure) ? Show(_su.pressure, iso.DatabaseSaturationPressure).ToString(_nf, ci) : "",
            Deviation = iso.Subcritical && !double.IsNaN(iso.DatabaseSaturationPressure) ? (100 * (iso.SaturationPressure / iso.DatabaseSaturationPressure - 1)).ToString("F2", ci) : "",
            VL = iso.Subcritical ? (iso.LiquidVolume * 1e6).ToString("F1", ci) : "",
            VV = iso.Subcritical ? (iso.VaporVolume * 1e6).ToString("F1", ci) : "",
            ZL = iso.Subcritical ? iso.LiquidZ.ToString("F4", ci) : "",
            ZV = iso.Subcritical ? iso.VaporZ.ToString("F4", ci) : ""
        }).ToList();
    }
}
