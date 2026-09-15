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
using DWSIM.Automation.DynamicRunner.Depressurization;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Vessel depressurization (blowdown) study. The composition comes from a material stream of the
/// flowsheet; the vessel, the blowdown orifice and the heat model are described here, and the
/// engine runs a private dynamic flowsheet with the rigorous vessel model. Results: pressure,
/// content and wall temperatures and the released flow against time, plus the extremes a relief
/// or a material selection needs. Avalonia counterpart of the WinForms FrmDepressurization.
/// </summary>
public sealed class DepressurizationWindow : Window
{
    private readonly IFlowsheet _fs;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;
    private readonly DepressurizationInput _in = new();
    private DepressurizationResult? _result;
    private CancellationTokenSource? _cts;

    private readonly List<(string Tag, string Name)> _streams;
    private ComboBox _streamBox = null!;
    private TextBox _pBox = null!, _tBox = null!;
    private Button _run = null!, _cancel = null!, _export = null!;
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 6, IsVisible = false };
    private readonly StackPanel _summary = new() { Spacing = 2 };
    private readonly XYPlot _plotP = new() { MinHeight = 220, Margin = new Thickness(4) };
    private readonly XYPlot _plotT = new() { MinHeight = 220, Margin = new Thickness(4) };
    private readonly XYPlot _plotW = new() { MinHeight = 200, Margin = new Thickness(4) };
    private readonly DataGrid _table = new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserSortColumns = false, MinHeight = 260, MaxHeight = 420, GridLinesVisibility = DataGridGridLinesVisibility.All };

    /// <summary>One row of the results table, already in the flowsheet units.</summary>
    public sealed class Row
    {
        public string Time { get; init; } = "";
        public string Pressure { get; init; } = "";
        public string Temperature { get; init; } = "";
        public string WettedWall { get; init; } = "";
        public string DryWall { get; init; } = "";
        public string MassFlow { get; init; } = "";
        public string Released { get; init; } = "";
        public string Level { get; init; } = "";
        public string FireHeat { get; init; } = "";
        public string Opening { get; init; } = "";
    }

    private static readonly List<string> Heads = new() { "Ellipsoidal (2:1)", "Hemispherical", "Torispherical (ASME F&D)", "Torispherical (Standard F&D)", "Torispherical (80:10 F&D)", "Flat" };
    private static readonly List<string> Materials = new() { "Carbon Steel", "Stainless Steel", "Steel", "Cast Iron", "Commercial Copper" };

    public DepressurizationWindow(IFlowsheet flowsheet)
    {
        _fs = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;
        _streams = flowsheet.SimulationObjects.Values
            .Where(o => o.GraphicObject != null && o.GraphicObject.ObjectType == ObjectType.MaterialStream)
            .Select(o => (o.GraphicObject.Tag, o.Name))
            .OrderBy(s => s.Tag, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Title = "Vessel Depressurization";
        Width = 1280;
        Height = 860;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
        if (_streams.Count > 0) _streamBox.SelectedIndex = 0;
    }

    // ---------------------------------------------------------------- layout

    private Control BuildContent()
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Fluid");
        p.CreateAndAddDescriptionRow("The vessel is filled with the composition of the stream you pick, flashed at the initial pressure and temperature below. The stream's own flow does not matter.");
        _streamBox = p.CreateAndAddDropDownRow("Source stream", _streams.Select(s => s.Tag).ToList(), -1, (dd, _) => OnStreamChanged());
        _pBox = p.CreateAndAddTextBoxRow(_nf, "Initial pressure (" + _su.pressure + ")", Show(_su.pressure, _in.InitialPressure),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.InitialPressure = cv.ConvertToSI(_su.pressure, v); });
        _tBox = p.CreateAndAddTextBoxRow(_nf, "Initial temperature (" + _su.temperature + ")", Show(_su.temperature, _in.InitialTemperature),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.InitialTemperature = cv.ConvertToSI(_su.temperature, v); });
        p.CreateAndAddTextBoxRow(_nf, "Initial liquid level (volume fraction, 0 to 1)", _in.InitialLiquidVolumeFraction,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.InitialLiquidVolumeFraction = v; });
        p.CreateAndAddDescriptionRow("With a two-phase flash the liquid fills this share of the volume and the vapour the rest. A single-phase fluid fills the whole vessel.");

        p.CreateAndAddLabelRow("Vessel");
        p.CreateAndAddDropDownRow("Orientation", new List<string> { "Vertical", "Horizontal" }, _in.Horizontal ? 1 : 0, (dd, _) => _in.Horizontal = dd.SelectedIndex == 1);
        p.CreateAndAddTextBoxRow(_nf, "Diameter (" + _su.distance + ")", Show(_su.distance, _in.Diameter),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.Diameter = cv.ConvertToSI(_su.distance, v); });
        p.CreateAndAddTextBoxRow(_nf, "Length, tangent to tangent (" + _su.distance + ")", Show(_su.distance, _in.Length),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.Length = cv.ConvertToSI(_su.distance, v); });
        p.CreateAndAddDropDownRow("Head type", Heads, Math.Max(0, Heads.IndexOf(_in.HeadType)), (dd, _) => { if (dd.SelectedIndex >= 0) _in.HeadType = Heads[dd.SelectedIndex]; });
        p.CreateAndAddTextBoxRow(_nf, "Wall thickness (" + _su.thickness + ")", Show(_su.thickness, _in.WallThickness),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.WallThickness = cv.ConvertToSI(_su.thickness, v); });
        p.CreateAndAddDropDownRow("Wall material", Materials, Math.Max(0, Materials.IndexOf(_in.WallMaterial)), (dd, _) => { if (dd.SelectedIndex >= 0) _in.WallMaterial = Materials[dd.SelectedIndex]; });

        p.CreateAndAddLabelRow("Blowdown valve or restriction orifice");
        p.CreateAndAddDescriptionRow("A sharp-edged orifice is described by its bore and discharge coefficient (0.6 to 0.65 for a thin plate). A valve can be given by its Cv instead; leave Cv at zero to use the orifice. Flow through it follows the ISA choked-flow forms.");
        p.CreateAndAddTextBoxRow(_nf, "Orifice bore (" + _su.distance + ")", Show(_su.distance, _in.OrificeDiameter),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.OrificeDiameter = cv.ConvertToSI(_su.distance, v); });
        p.CreateAndAddTextBoxRow(_nf, "Discharge coefficient", _in.DischargeCoefficient,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.DischargeCoefficient = v; });
        p.CreateAndAddTextBoxRow(_nf, "Valve Cv (0 = use the orifice)", _in.FlowCoefficientCv,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.FlowCoefficientCv = v; });
        p.CreateAndAddTextBoxRow(_nf, "Back pressure at the outlet (" + _su.pressure + ")", Show(_su.pressure, _in.BackPressure),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.BackPressure = cv.ConvertToSI(_su.pressure, v); });
        p.CreateAndAddTextBoxRow(_nf, "Valve opening time (s, 0 = instant)", _in.ValveOpeningTime,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.ValveOpeningTime = v; });

        p.CreateAndAddLabelRow("Heat");
        p.CreateAndAddDropDownRow("Case", new List<string> { "Adiabatic (cold blowdown)", "Fire (API 521)", "Isothermal" }, (int)_in.Mode,
            (dd, _) => { if (dd.SelectedIndex >= 0) _in.Mode = (DepressurizationMode)dd.SelectedIndex; });
        p.CreateAndAddDescriptionRow("Adiabatic: the content cools as it expands and exchanges heat with the metal and the ambient; the answer is the lowest fluid and wall temperature. Fire: API 521 pool-fire heat on the wetted wall, plus a flux you give on the dry wall; the answer is the relief flow and the hottest metal. Isothermal: the content keeps its temperature.");
        p.CreateAndAddTextBoxRow(_nf, "Ambient temperature (" + _su.temperature + ")", Show(_su.temperature, _in.AmbientTemperature),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.AmbientTemperature = cv.ConvertToSI(_su.temperature, v); });
        p.CreateAndAddCheckBoxRow("Include the wall (metal thermal mass, wall-to-fluid and ambient heat transfer)", _in.IncludeWallHeatTransfer,
            (cb, _) => _in.IncludeWallHeatTransfer = cb.IsChecked == true);
        p.CreateAndAddTextBoxRow(_nf, "Wall-to-fluid heat transfer factor (1 = natural convection as estimated)", _in.InternalHeatTransferFactor,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) _in.InternalHeatTransferFactor = v; });
        p.CreateAndAddDescriptionRow("The film coefficient between the metal and the content is estimated by a natural-convection correlation. Against the Imperial College nitrogen blowdown data the correlation as is gives a gas minimum 15 to 20 K warmer than measured; a factor of 0.5 to 0.7 reproduces the measured gas curve. Use 0.5 for a conservative lowest fluid temperature.");
        p.CreateAndAddTextBoxRow(_nf, "Fire: environment factor F", _in.FireEnvironmentFactor,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.FireEnvironmentFactor = v; });
        p.CreateAndAddCheckBoxRow("Fire: adequate drainage and prompt firefighting (C = 43200; otherwise 70900)", _in.FireAdequateDrainage,
            (cb, _) => _in.FireAdequateDrainage = cb.IsChecked == true);
        p.CreateAndAddTextBoxRow(_nf, "Fire: heat flux on the dry wall (W/m2)", _in.FireDryWallHeatFlux,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.FireDryWallHeatFlux = v; });
        p.CreateAndAddTextBoxRow(_nf, "Fire: vessel bottom elevation above grade (" + _su.distance + ")", Show(_su.distance, _in.VesselBottomElevation),
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.VesselBottomElevation = cv.ConvertToSI(_su.distance, v); });

        p.CreateAndAddLabelRow("Integration");
        p.CreateAndAddTextBoxRow(_nf, "Time step (s)", _in.TimeStep, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.TimeStep = v; });
        p.CreateAndAddTextBoxRow(_nf, "Duration (s)", _in.Duration, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.Duration = v; });
        p.CreateAndAddTextBoxRow(_nf, "Stop at pressure (" + _su.pressure + ", 0 = run to the end)", 0.0,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _in.StopAtPressure = v > 0 ? cv.ConvertToSI(_su.pressure, v) : 0.0; });
        p.CreateAndAddDescriptionRow("API 521 asks for the pressure to fall to the lower of 50 % of the design pressure or 6.9 barg within 15 minutes; enter that pressure to read the time straight off the results.");

        var left = new ScrollViewer { Content = p, Padding = new Thickness(10, 8, 10, 8), AllowAutoHide = false };

        _run = new Button { Content = "Run", Width = 110, IsDefault = true };
        _run.Classes.Add("dialog");
        _run.Click += async (_, _) => await RunAsync();
        _cancel = new Button { Content = "Stop", Width = 90, IsEnabled = false };
        _cancel.Classes.Add("dialog");
        _cancel.Click += (_, _) => _cts?.Cancel();
        _export = new Button { Content = "Export CSV...", Width = 130, IsEnabled = false };
        _export.Classes.Add("dialog");
        _export.Click += async (_, _) => await ExportAsync();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 6, 12, 8), Children = { _run, _cancel, _export } };

        var leftDock = new DockPanel();
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        leftDock.Children.Add(buttons);
        leftDock.Children.Add(left);

        _plotP.PlotTitle = "Vessel pressure";
        _plotP.XAxisTitle = "time (s)";
        _plotP.YAxisTitle = _su.pressure;
        _plotT.PlotTitle = "Temperatures";
        _plotT.XAxisTitle = "time (s)";
        _plotT.YAxisTitle = _su.temperature;
        _plotW.PlotTitle = "Released flow";
        _plotW.XAxisTitle = "time (s)";
        _plotW.YAxisTitle = _su.massflow;

        var right = new StackPanel { Spacing = 6, Margin = new Thickness(10, 8, 12, 8) };
        right.Children.Add(new TextBlock { Text = "Results", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13) });
        right.Children.Add(_summary);
        right.Children.Add(_plotP);
        right.Children.Add(_plotT);
        right.Children.Add(_plotW);
        right.Children.Add(new TextBlock { Text = "Data", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13), Margin = new Thickness(0, 8, 0, 0) });
        void Col(string header, string path) => _table.Columns.Add(new DataGridTextColumn { Header = header, Binding = new global::Avalonia.Data.Binding(path), Width = DataGridLength.Auto });
        Col("time (s)", nameof(Row.Time));
        Col("pressure (" + _su.pressure + ")", nameof(Row.Pressure));
        Col("content T (" + _su.temperature + ")", nameof(Row.Temperature));
        Col("wetted wall T (" + _su.temperature + ")", nameof(Row.WettedWall));
        Col("dry wall T (" + _su.temperature + ")", nameof(Row.DryWall));
        Col("mass flow (" + _su.massflow + ")", nameof(Row.MassFlow));
        Col("released (" + _su.mass + ")", nameof(Row.Released));
        Col("liquid level (" + _su.distance + ")", nameof(Row.Level));
        Col("fire heat (" + _su.heatflow + ")", nameof(Row.FireHeat));
        Col("opening (%)", nameof(Row.Opening));
        right.Children.Add(_table);
        var rightScroll = new ScrollViewer { Content = right, AllowAutoHide = false };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("470,Auto,*") };
        Grid.SetColumn(leftDock, 0);
        var rule = new Border { Width = 1, Background = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), Margin = new Thickness(2, 8, 2, 8) };
        Grid.SetColumn(rule, 1);
        Grid.SetColumn(rightScroll, 2);
        grid.Children.Add(leftDock);
        grid.Children.Add(rule);
        grid.Children.Add(rightScroll);

        var bottom = new StackPanel { Margin = new Thickness(12, 0, 12, 8), Spacing = 4 };
        bottom.Children.Add(_progress);
        bottom.Children.Add(_status);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(grid);
        SetSummaryPlaceholder();
        return root;
    }

    private double Show(string units, double si) => cv.ConvertFromSI(units, si);

    private void OnStreamChanged()
    {
        if (_streamBox.SelectedIndex < 0) return;
        _in.SourceStreamName = _streams[_streamBox.SelectedIndex].Name;
        if (_fs.SimulationObjects.TryGetValue(_in.SourceStreamName, out var o) && o is IMaterialStream ms)
        {
            var p = ms.GetPressure();
            var t = ms.GetTemperature();
            if (p > 0 && t > 0)
            {
                _in.InitialPressure = p;
                _in.InitialTemperature = t;
                _pBox.Text = Show(_su.pressure, p).ToString(_nf, CultureInfo.CurrentCulture);
                _tBox.Text = Show(_su.temperature, t).ToString(_nf, CultureInfo.CurrentCulture);
            }
        }
    }

    // ---------------------------------------------------------------- run

    private async Task RunAsync()
    {
        if (_streamBox.SelectedIndex < 0) { _status.Text = "Pick a source stream."; return; }
        _in.SourceStreamName = _streams[_streamBox.SelectedIndex].Name;
        if (_in.InitialPressure <= _in.BackPressure) { _status.Text = "The initial pressure must be above the back pressure."; return; }

        _cts = new CancellationTokenSource();
        _run.IsEnabled = false; _cancel.IsEnabled = true; _export.IsEnabled = false;
        _progress.IsVisible = true; _progress.Value = 0;
        _status.Text = "Running...";
        var input = _in;
        var token = _cts.Token;
        try
        {
            var result = await Task.Run(() => DepressurizationStudy.Run(_fs, input,
                frac => Dispatcher.UIThread.Post(() => { _progress.Value = frac; _status.Text = $"Running... {frac * 100:F0} %"; }),
                token), token);
            _result = result;
            ShowResult(result);
            _status.Text = result.Aborted ? "Stopped by the user; showing what was computed." :
                $"Done: {result.Points.Count - 1} steps in {result.Elapsed.TotalSeconds:F1} s." + (result.Warnings.Count > 0 ? " " + string.Join(" ", result.Warnings) : "");
            _export.IsEnabled = true;
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Stopped.";
        }
        catch (Exception ex)
        {
            _status.Text = "The study failed: " + ex.Message;
        }
        finally
        {
            _run.IsEnabled = true; _cancel.IsEnabled = false;
            _progress.IsVisible = false;
            _cts.Dispose(); _cts = null;
        }
    }

    private void SetSummaryPlaceholder()
    {
        _summary.Children.Clear();
        _summary.Children.Add(new TextBlock { Text = "Set up the case on the left and press Run.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
    }

    private void ShowResult(DepressurizationResult r)
    {
        _summary.Children.Clear();
        var p = new AvaloniaEditorPanel();
        string T(double k) => Show(_su.temperature, k).ToString(_nf, CultureInfo.CurrentCulture) + " " + _su.temperature;
        string P(double pa) => Show(_su.pressure, pa).ToString(_nf, CultureInfo.CurrentCulture) + " " + _su.pressure;
        string Time(double? s) => s.HasValue ? $"{s.Value:F0} s ({s.Value / 60.0:F1} min)" : "not reached";

        p.CreateAndAddTwoLabelsRow("Vessel volume", Show(_su.volume, r.VesselVolume).ToString(_nf, CultureInfo.CurrentCulture) + " " + _su.volume);
        p.CreateAndAddTwoLabelsRow("Wetted area at the start", Show(_su.area, r.WettedAreaAtStart).ToString(_nf, CultureInfo.CurrentCulture) + " " + _su.area);
        p.CreateAndAddTwoLabelsRow("Initial inventory", Show(_su.mass, r.InitialMass).ToString(_nf, CultureInfo.CurrentCulture) + " " + _su.mass);
        p.CreateAndAddTwoLabelsRow("Final pressure", P(r.FinalPressure) + " at " + T(r.FinalTemperature));
        p.CreateAndAddTwoLabelsRow("Time to halve the pressure", Time(r.TimeToHalfPressure));
        if (_in.StopAtPressure > 0) p.CreateAndAddTwoLabelsRow("Time to reach " + P(_in.StopAtPressure), Time(r.TimeToStopPressure));
        p.CreateAndAddTwoLabelsRow("Peak released flow", Show(_su.massflow, r.PeakMassFlow).ToString(_nf, CultureInfo.CurrentCulture) + " " + _su.massflow);
        p.CreateAndAddTwoLabelsRow("Mass released", Show(_su.mass, r.TotalMassReleased).ToString(_nf, CultureInfo.CurrentCulture) + " " + _su.mass);
        p.CreateAndAddTwoLabelsRow("Lowest content temperature", T(r.MinimumFluidTemperature));
        p.CreateAndAddTwoLabelsRow("Lowest wetted wall temperature", T(r.MinimumWettedWallTemperature));
        p.CreateAndAddTwoLabelsRow("Lowest dry wall temperature", T(r.MinimumDryWallTemperature));
        if (_in.Mode == DepressurizationMode.Fire) p.CreateAndAddTwoLabelsRow("Highest dry wall temperature", T(r.MaximumDryWallTemperature));
        p.CreateAndAddDescriptionRow(_in.Mode == DepressurizationMode.Fire
            ? "The peak released flow is the relief load of this case; the highest dry wall temperature is the metal check (API 521 uses the wall reaching about 590 C as the rupture criterion for carbon steel)."
            : "The lowest content and wall temperatures are what the material of the vessel and of the downstream piping must tolerate (MDMT); the wall lags the gas because the metal has thermal mass.");
        _summary.Children.Add(p);

        var t = r.Points.Select(x => x.Time).ToArray();
        _plotP.Clear();
        _plotP.AddSeries("Pressure", t, r.Points.Select(x => Show(_su.pressure, x.Pressure)).ToArray());
        _plotP.InvalidateVisual();

        _plotT.Clear();
        _plotT.AddSeries("Content", t, r.Points.Select(x => Show(_su.temperature, x.Temperature)).ToArray());
        var wet = _plotT.AddSeries("Wetted wall", t, r.Points.Select(x => Show(_su.temperature, x.WettedWallTemperature)).ToArray(), false, new[] { 4.0, 3.0 });
        if (wet != null) wet.Color = Colors.DarkOrange;
        var dry = _plotT.AddSeries("Dry wall", t, r.Points.Select(x => Show(_su.temperature, x.DryWallTemperature)).ToArray(), false, new[] { 2.0, 2.0 });
        if (dry != null) dry.Color = Colors.Firebrick;
        _plotT.InvalidateVisual();

        _plotW.Clear();
        _plotW.AddSeries("Mass flow", t, r.Points.Select(x => Show(_su.massflow, x.MassFlow)).ToArray());
        _plotW.InvalidateVisual();

        var ci = CultureInfo.CurrentCulture;
        _table.ItemsSource = r.Points.Select(x => new Row
        {
            Time = x.Time.ToString(_nf, ci),
            Pressure = Show(_su.pressure, x.Pressure).ToString(_nf, ci),
            Temperature = Show(_su.temperature, x.Temperature).ToString(_nf, ci),
            WettedWall = Show(_su.temperature, x.WettedWallTemperature).ToString(_nf, ci),
            DryWall = Show(_su.temperature, x.DryWallTemperature).ToString(_nf, ci),
            MassFlow = Show(_su.massflow, x.MassFlow).ToString(_nf, ci),
            Released = Show(_su.mass, x.CumulativeMass).ToString(_nf, ci),
            Level = Show(_su.distance, x.LiquidLevel).ToString(_nf, ci),
            FireHeat = Show(_su.heatflow, x.FireHeat).ToString(_nf, ci),
            Opening = x.ValveOpening.ToString("N0", ci)
        }).ToList();
    }

    // ---------------------------------------------------------------- export

    private async Task ExportAsync()
    {
        if (_result == null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export the depressurization results",
            SuggestedFileName = "depressurization.csv",
            FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
        });
        if (file == null) return;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", "time (s)", "pressure (" + _su.pressure + ")", "content T (" + _su.temperature + ")",
            "wetted wall T (" + _su.temperature + ")", "dry wall T (" + _su.temperature + ")", "mass flow (" + _su.massflow + ")",
            "released (" + _su.mass + ")", "liquid level (" + _su.distance + ")", "liquid volume fraction", "fire heat (" + _su.heatflow + ")", "valve opening (%)", "vapour fraction out"));
        var ci = CultureInfo.InvariantCulture;
        foreach (var x in _result.Points)
            sb.AppendLine(string.Join(";",
                x.Time.ToString("G", ci), Show(_su.pressure, x.Pressure).ToString("G6", ci), Show(_su.temperature, x.Temperature).ToString("G6", ci),
                Show(_su.temperature, x.WettedWallTemperature).ToString("G6", ci), Show(_su.temperature, x.DryWallTemperature).ToString("G6", ci),
                Show(_su.massflow, x.MassFlow).ToString("G6", ci), Show(_su.mass, x.CumulativeMass).ToString("G6", ci),
                Show(_su.distance, x.LiquidLevel).ToString("G6", ci), x.LiquidVolumeFraction.ToString("G4", ci),
                Show(_su.heatflow, x.FireHeat).ToString("G6", ci), x.ValveOpening.ToString("G4", ci), x.VapourFractionOut.ToString("G4", ci)));
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new System.IO.StreamWriter(stream);
        await writer.WriteAsync(sb.ToString());
        _status.Text = "Exported to " + file.Name + ".";
    }
}
