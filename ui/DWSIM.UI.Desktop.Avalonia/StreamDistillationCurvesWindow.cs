using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.Streams;
using DWSIM.Thermodynamics.Utilities.PetroleumProperties;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Distillation curves (TBP, ASTM D86, D1160, D2887 and EFV) and petroleum product properties of a
/// material stream. Opened from the Utilities menu it works on any stream and can attach the result
/// to it; opened from an attached utility it edits that utility's settings.
/// </summary>
public sealed class StreamDistillationCurvesWindow : Window
{
    private readonly IFlowsheet _fs;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;
    private readonly DistillationCurvesUtility? _utility;
    private readonly DistillationCurvesUtility _settings;
    private DistillationCurveResults? _result;

    private readonly List<(string Tag, string Name)> _streams;
    private ComboBox _streamBox = null!;
    private Button _run = null!, _copy = null!, _attach = null!;
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly XYPlot _plot = new() { MinHeight = 300, Margin = new Thickness(4) };
    private readonly StackPanel _properties = new() { Spacing = 2 };
    private readonly TextBlock _messages = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly DataGrid _table = new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserSortColumns = false, MinHeight = 200, GridLinesVisibility = DataGridGridLinesVisibility.All };

    private static readonly string[] CurveNames = { "TBP", "ASTM D86", "ASTM D1160", "ASTM D2887", "EFV" };
    private static readonly List<string> Bases = new() { "Liquid volume", "Mass", "Mole" };
    private static readonly List<string> EfvMethods = new() { "Flash with the property package", "Riazi-Daubert correlation (from D86)" };

    /// <summary>One row of the curves table, already in the flowsheet units.</summary>
    public sealed class Row
    {
        public string Percent { get; init; } = "";
        public string TBP { get; init; } = "";
        public string D86 { get; init; } = "";
        public string D1160 { get; init; } = "";
        public string D2887 { get; init; } = "";
        public string EFV { get; init; } = "";
    }

    public StreamDistillationCurvesWindow(IFlowsheet flowsheet, string? preselectedTag = null, DistillationCurvesUtility? utility = null)
    {
        _fs = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;
        _utility = utility;
        _settings = new DistillationCurvesUtility();
        if (utility != null)
            foreach (var key in new[] { DistillationCurvesUtility.KeyLiquidOnly, DistillationCurvesUtility.KeyTBPBasis, DistillationCurvesUtility.KeyPercents,
                                        DistillationCurvesUtility.KeyD1160Pressure, DistillationCurvesUtility.KeyEFVPressure, DistillationCurvesUtility.KeyEFVMethod })
                _settings.SetPropertyValue(key, utility.GetPropertyValue(key));

        _streams = flowsheet.SimulationObjects.Values
            .Where(o => o.GraphicObject != null && o.GraphicObject.ObjectType == ObjectType.MaterialStream)
            .Select(o => (o.GraphicObject.Tag, o.Name))
            .OrderBy(s => s.Tag, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Title = utility != null ? "Distillation Curves - " + utility.Name : "Distillation Curves";
        Width = 1240;
        Height = 820;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();

        var tag = utility?.AttachedTo?.GraphicObject?.Tag ?? preselectedTag;
        if (_streams.Count > 0)
            _streamBox.SelectedIndex = Math.Max(0, _streams.FindIndex(s => s.Tag == tag));
        if (utility?.LastResults != null) ShowResults(utility.LastResults);
    }

    private string Setting(string key) => Convert.ToString(_settings.GetPropertyValue(key), CultureInfo.InvariantCulture) ?? "";

    private double SettingValue(string key) => Convert.ToDouble(_settings.GetPropertyValue(key), CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- layout

    private Control BuildContent()
    {
        var left = new ScrollViewer { Content = BuildInputPanel(), Padding = new Thickness(10, 8, 10, 8), AllowAutoHide = false };

        _run = new Button { Content = "Calculate", Width = 110, IsDefault = true };
        _run.Classes.Add("dialog");
        _run.Click += async (_, _) => await RunAsync();
        _copy = new Button { Content = "Copy data", Width = 110, IsEnabled = false };
        _copy.Classes.Add("dialog");
        _copy.Click += async (_, _) => await CopyAsync();
        _attach = new Button { Content = _utility != null ? "Save to the utility" : "Attach to the stream", Width = 160 };
        _attach.Classes.Add("dialog");
        _attach.Click += (_, _) => Attach();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 6, 12, 8), Children = { _run, _copy, _attach } };

        var leftDock = new DockPanel();
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        leftDock.Children.Add(buttons);
        leftDock.Children.Add(left);

        _plot.PlotTitle = "Distillation curves";
        _plot.XAxisTitle = "percent distilled";
        _plot.YAxisTitle = _su.temperature;

        void Col(string header, string path) => _table.Columns.Add(new DataGridTextColumn { Header = header, Binding = new global::Avalonia.Data.Binding(path), Width = DataGridLength.Auto });
        Col("% distilled", nameof(Row.Percent));
        Col("TBP (" + _su.temperature + ")", nameof(Row.TBP));
        Col("D86 (" + _su.temperature + ")", nameof(Row.D86));
        Col("D1160 (" + _su.temperature + ")", nameof(Row.D1160));
        Col("D2887 (" + _su.temperature + ")", nameof(Row.D2887));
        Col("EFV (" + _su.temperature + ")", nameof(Row.EFV));

        var right = new StackPanel { Spacing = 6, Margin = new Thickness(10, 8, 12, 8) };
        right.Children.Add(new TextBlock { Text = "Curves", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13) });
        right.Children.Add(_plot);
        right.Children.Add(_table);
        right.Children.Add(new TextBlock { Text = "Product properties", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13), Margin = new Thickness(0, 8, 0, 0) });
        right.Children.Add(_properties);
        right.Children.Add(_messages);
        var rightScroll = new ScrollViewer { Content = right, AllowAutoHide = false };

        // inputs and results side by side, the split draggable
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("480,6,*") };
        grid.ColumnDefinitions[0].MinWidth = 300;
        grid.ColumnDefinitions[2].MinWidth = 320;
        Grid.SetColumn(leftDock, 0);
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, Background = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), Margin = new Thickness(0, 8, 0, 8) };
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(rightScroll, 2);
        grid.Children.Add(leftDock);
        grid.Children.Add(splitter);
        grid.Children.Add(rightScroll);

        var bottom = new StackPanel { Margin = new Thickness(12, 0, 12, 8), Spacing = 4 };
        bottom.Children.Add(_status);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(grid);
        return root;
    }

    private AvaloniaEditorPanel BuildInputPanel()
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Stream");
        _streamBox = p.CreateAndAddDropDownRow("Material stream", _streams.Select(s => s.Tag).ToList(), -1, null);
        _streamBox.IsEnabled = _utility == null;
        p.CreateAndAddCheckBoxRow("Use the liquid phase only", Convert.ToBoolean(_settings.GetPropertyValue(DistillationCurvesUtility.KeyLiquidOnly)),
            (cb, _) => _settings.SetPropertyValue(DistillationCurvesUtility.KeyLiquidOnly, cb.IsChecked == true));
        p.CreateAndAddDescriptionRow("The TBP curve comes from the compounds of the stream: each one occupies its share of the stream and boils at its normal boiling point. Pseudocomponents cut from an assay return the assay's curve.");

        p.CreateAndAddLabelRow("Curves");
        p.CreateAndAddDropDownRow("TBP basis", Bases, (int)SettingValue(DistillationCurvesUtility.KeyTBPBasis),
            (dd, _) => { if (dd.SelectedIndex >= 0) _settings.SetPropertyValue(DistillationCurvesUtility.KeyTBPBasis, dd.SelectedIndex); });
        p.CreateAndAddStringEditorRow("Percents distilled", Setting(DistillationCurvesUtility.KeyPercents),
            (tb, _) => _settings.SetPropertyValue(DistillationCurvesUtility.KeyPercents, tb.Text ?? ""));
        p.CreateAndAddDescriptionRow("Separated by commas, from 0 (initial boiling point) to 100 (final boiling point). The D86, D1160 and EFV curves are on liquid volume and the D2887 curve on mass, whatever the TBP basis.");
        p.CreateAndAddTextBoxRow(_nf, "D1160 pressure (" + _su.pressure + ")", cv.ConvertFromSI(_su.pressure, SettingValue(DistillationCurvesUtility.KeyD1160Pressure)),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) _settings.SetPropertyValue(DistillationCurvesUtility.KeyD1160Pressure, cv.ConvertToSI(_su.pressure, v)); });
        p.CreateAndAddDescriptionRow("The D1160 conversion (API figure 3A2.1) works at 10 mmHg; other pressures are reached with the Maxwell-Bonnell relation.");
        p.CreateAndAddDropDownRow("EFV", EfvMethods, (int)SettingValue(DistillationCurvesUtility.KeyEFVMethod),
            (dd, _) => { if (dd.SelectedIndex >= 0) _settings.SetPropertyValue(DistillationCurvesUtility.KeyEFVMethod, dd.SelectedIndex); });
        p.CreateAndAddTextBoxRow(_nf, "EFV pressure (" + _su.pressure + ")", cv.ConvertFromSI(_su.pressure, SettingValue(DistillationCurvesUtility.KeyEFVPressure)),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) _settings.SetPropertyValue(DistillationCurvesUtility.KeyEFVPressure, cv.ConvertToSI(_su.pressure, v)); });
        p.CreateAndAddDescriptionRow("By flash, each EFV point is the temperature where the vapour is that share of the stream's standard liquid volume.");

        p.CreateAndAddLabelRow("Methods");
        p.CreateAndAddDescriptionRow("D86: API procedure 3A1.1. D2887: API procedure 3A3.1. D1160: API figure 3A2.1 and procedure 5A1.19. Flash point: API 2B7.1 from the D86 10 % point. Pour, cloud, freezing, aniline and smoke points, refraction and cetane index: API chapter 2. Calculated cetane index: ASTM D4737. Densities and viscosities: the stream's property package.");

        if (_utility == null)
        {
            p.CreateAndAddLabelRow("Attaching");
            p.CreateAndAddDescriptionRow("Attached to the stream, the curves follow the flowsheet and every point becomes a stream property (\"DistillationCurves1: D86 95%\") that an Adjust, a specification, the spreadsheet or a column secondary specification can read.");
        }
        return p;
    }

    // ---------------------------------------------------------------- calculation

    private MaterialStream? SelectedStream()
    {
        if (_streamBox.SelectedIndex < 0) return null;
        return _fs.SimulationObjects.TryGetValue(_streams[_streamBox.SelectedIndex].Name, out var o) ? o as MaterialStream : null;
    }

    private async Task RunAsync()
    {
        var ms = SelectedStream();
        if (ms == null) { _status.Text = "Select a material stream."; return; }

        _run.IsEnabled = false;
        _status.Text = "Calculating, please wait...";
        try
        {
            var settings = _settings.GetSettings();
            var res = await Task.Run(() => DistillationCurves.Calculate(ms, settings));
            ShowResults(res);
            _status.Text = "Done.";
        }
        catch (Exception ex)
        {
            _status.Text = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            _run.IsEnabled = true;
        }
    }

    private string T(double si) => cv.ConvertFromSI(_su.temperature, si).ToString(_nf);

    private void ShowResults(DistillationCurveResults res)
    {
        _result = res;
        _copy.IsEnabled = true;

        _plot.Clear();
        _plot.YAxisTitle = _su.temperature;
        var types = new[] { DistillationCurveType.TBP, DistillationCurveType.ASTM_D86, DistillationCurveType.ASTM_D1160, DistillationCurveType.ASTM_D2887, DistillationCurveType.EFV };
        foreach (var type in types)
        {
            if (!res.Curves.TryGetValue(type, out var c)) continue;
            var name = CurveNames[(int)type];
            if (type == DistillationCurveType.ASTM_D1160) name += " @ " + cv.ConvertFromSI(_su.pressure, c.Pressure).ToString("G4") + " " + _su.pressure;
            if (type == DistillationCurveType.EFV) name += " @ " + cv.ConvertFromSI(_su.pressure, c.Pressure).ToString("G4") + " " + _su.pressure;
            _plot.AddSeries(name, c.Percents, c.Temperatures.Select(t => cv.ConvertFromSI(_su.temperature, t)).ToArray());
        }
        _plot.InvalidateVisual();

        var tbp = res.Curves[DistillationCurveType.TBP];
        var rows = new List<Row>();
        for (int i = 0; i < tbp.Percents.Length; i++)
        {
            rows.Add(new Row
            {
                Percent = tbp.Percents[i].ToString("0.##", CultureInfo.CurrentCulture),
                TBP = T(tbp.Temperatures[i]),
                D86 = T(res.Curves[DistillationCurveType.ASTM_D86].Temperatures[i]),
                D1160 = T(res.Curves[DistillationCurveType.ASTM_D1160].Temperatures[i]),
                D2887 = T(res.Curves[DistillationCurveType.ASTM_D2887].Temperatures[i]),
                EFV = T(res.Curves[DistillationCurveType.EFV].Temperatures[i]),
            });
        }
        _table.ItemsSource = rows;

        _properties.Children.Clear();
        var panel = new AvaloniaEditorPanel();
        foreach (var (label, value, unit) in PropertyRows(res.Properties))
            ResultsTable.For(panel).AddRow(label, value, unit);
        _properties.Children.Add(panel);

        _messages.Text = string.Join(Environment.NewLine, res.Messages);
    }

    private IEnumerable<(string Label, string Value, string Unit)> PropertyRows(PetroleumProductProperties pr)
    {
        (string, string, string) Conv(string label, string unit, double si) => (label, cv.ConvertFromSI(unit, si).ToString(_nf), unit);
        (string, string, string) Plain(string label, double v, string unit = "") => (label, v.ToString(_nf), unit);

        yield return Conv("Molecular weight", _su.molecularWeight, pr.MolecularWeight);
        yield return Plain("Specific gravity 60F/60F", pr.SpecificGravity);
        yield return Plain("API gravity", pr.API);
        yield return Plain("Watson K", pr.WatsonK);
        yield return Conv("Mean average boiling point", _su.temperature, pr.MeanAverageBoilingPoint);
        yield return Conv("Density @ 15 C", _su.density, pr.Density15C);
        yield return Conv("Density @ 20 C", _su.density, pr.Density20C);
        yield return Conv("Kinematic viscosity @ 40 C", _su.cinematic_viscosity, pr.KinematicViscosity40C);
        yield return Conv("Kinematic viscosity @ 100 C", _su.cinematic_viscosity, pr.KinematicViscosity100C);
        yield return Conv("Flash point, closed cup (D93)", _su.temperature, pr.FlashPointClosedCup);
        yield return Conv("Flash point, open cup (D92)", _su.temperature, pr.FlashPointOpenCup);
        yield return Conv("Pour point", _su.temperature, pr.PourPoint);
        yield return Conv("Cloud point", _su.temperature, pr.CloudPoint);
        yield return Conv("Freezing point", _su.temperature, pr.FreezingPoint);
        yield return Conv("Aniline point", _su.temperature, pr.AnilinePoint);
        yield return Plain("Smoke point", pr.SmokePoint * 1000, "mm");
        yield return Plain("Refraction index", pr.RefractionIndex);
        yield return Plain("Cetane index (API 2B13.1)", pr.CetaneIndex);
        yield return Plain("Calculated cetane index (D4737)", pr.CetaneIndexD4737);
        yield return Conv("True vapor pressure @ 100 F", _su.pressure, pr.TrueVaporPressure);
        yield return Conv("Reid vapor pressure", _su.pressure, pr.ReidVaporPressure);
        yield return Plain("Sulfur", pr.SulfurWtPct, "wt %");
    }

    private async Task CopyAsync()
    {
        if (_result == null) return;
        var sb = new StringBuilder();
        var tbp = _result.Curves[DistillationCurveType.TBP];
        sb.AppendLine("% distilled\tTBP\tD86\tD1160\tD2887\tEFV\t(" + _su.temperature + ")");
        for (int i = 0; i < tbp.Percents.Length; i++)
        {
            sb.Append(tbp.Percents[i].ToString("0.##", CultureInfo.CurrentCulture));
            foreach (var type in new[] { DistillationCurveType.TBP, DistillationCurveType.ASTM_D86, DistillationCurveType.ASTM_D1160, DistillationCurveType.ASTM_D2887, DistillationCurveType.EFV })
                sb.Append('\t').Append(T(_result.Curves[type].Temperatures[i]));
            sb.AppendLine();
        }
        sb.AppendLine();
        foreach (var (label, value, unit) in PropertyRows(_result.Properties))
            sb.AppendLine(label + "\t" + value + "\t" + unit);
        var clip = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clip != null) await clip.SetTextAsync(sb.ToString());
        _status.Text = "Copied to the clipboard.";
    }

    private void Attach()
    {
        var ms = SelectedStream();
        if (ms == null) { _status.Text = "Select a material stream."; return; }

        var utility = _utility;
        if (utility == null)
        {
            utility = new DistillationCurvesUtility
            {
                ID = new Random().Next(1, int.MaxValue),
                Name = "DistillationCurves" + (ms.AttachedUtilities.Count(x => x.GetUtilityType() == DWSIM.Interfaces.Enums.FlowsheetUtility.DistillationCurves) + 1),
                AttachedTo = ms
            };
            ms.AttachedUtilities.Add(utility);
        }
        foreach (var key in new[] { DistillationCurvesUtility.KeyLiquidOnly, DistillationCurvesUtility.KeyTBPBasis, DistillationCurvesUtility.KeyPercents,
                                    DistillationCurvesUtility.KeyD1160Pressure, DistillationCurvesUtility.KeyEFVPressure, DistillationCurvesUtility.KeyEFVMethod })
            utility.SetPropertyValue(key, _settings.GetPropertyValue(key));

        try
        {
            utility.Update();
            if (utility.LastResults != null) ShowResults(utility.LastResults);
            _status.Text = _utility == null
                ? "Attached to " + ms.GraphicObject.Tag + " as " + utility.Name + ". Its points are now stream properties."
                : "Saved to " + utility.Name + ".";
        }
        catch (Exception ex)
        {
            _status.Text = "Attached, but the update failed: " + (ex.InnerException?.Message ?? ex.Message);
        }
    }
}
