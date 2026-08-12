using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UI.Desktop.Avalonia.Controls;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using sc = DWSIM.Thermodynamics.ShortcutUtilities;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Phase Envelope utility. Avalonia counterpart of
/// DWSIM.UI.Desktop.Editors.Utilities.PhaseEnvelopeView.
///
/// The engine (DWSIM.Thermodynamics.ShortcutUtilities) does the whole calculation and
/// returns both a formatted report and every curve as raw data; this window only drives
/// it and renders CalculationResults.Data through <see cref="XYPlot"/>.
/// </summary>
public partial class PhaseEnvelopeWindow : Window
{
    private static readonly string[] EnvelopeTypes =
    {
        "Pressure-Temperature",
        "Pressure-Enthalpy",
        "Pressure-Entropy",
        "Temperature-Enthalpy",
        "Temperature-Entropy",
        "Volume-Pressure",
        "Volume-Temperature"
    };

    private readonly IFlowsheet _flowsheet;
    private readonly PhaseEnvelopeOptions _options = new();
    private List<string> _streams = new();

    private ComboBox _cbStream = null!;
    private ComboBox _cbType = null!;
    private readonly XYPlot _plot = new();

    // Full text report, kept for the Copy Report button now that Data is shown as a grid.
    private string _lastReport = "";

    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public PhaseEnvelopeWindow() : this(null!, null) { }

    public PhaseEnvelopeWindow(IFlowsheet flowsheet, string? preselectedStream = null)
    {
        _flowsheet = flowsheet!;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        ChartHost.Child = _plot;
        BuildOptionsPanel(preselectedStream);

        BtnBuild.Click += async (_, _) => await BuildEnvelopeAsync();
        BtnCopyData.Click += async (_, _) => await CopyAsync(_lastReport);
        BtnCopyCsv.Click += async (_, _) => await CopyAsync(_plot.ToDelimitedText());
    }

    // -------------------------------------------------------------------------
    // Setup panel
    // -------------------------------------------------------------------------

    private void BuildOptionsPanel(string? preselectedStream)
    {
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var nf = _flowsheet.FlowsheetOptions.NumberFormat;

        _streams = UtilityHelpers.MaterialStreamTags(_flowsheet);

        var panel = new AvaloniaEditorPanel();

        panel.CreateAndAddLabelRow("Setup");
        panel.CreateAndAddDescriptionRow(
            "The Phase Envelope utility calculates various VLE envelopes for mixtures.");

        _cbStream = panel.CreateAndAddDropDownRow("Material Stream", _streams,
            UtilityHelpers.IndexOfStream(_streams, preselectedStream), null);
        _cbType = panel.CreateAndAddDropDownRow("Envelope Type", EnvelopeTypes.ToList(), 0, null);

        var c1 = new AvaloniaEditorPanel();
        var c2 = new AvaloniaEditorPanel();
        var c3 = new AvaloniaEditorPanel();

        c1.CreateAndAddCheckBoxRow("Quality Line", _options.QualityLine,
            (cb, e) => _options.QualityLine = cb.IsChecked.GetValueOrDefault());
        c1.CreateAndAddDescriptionRow("Includes a Quality Line in the chart (TP only).");
        c1.CreateAndAddTextBoxRow("G6", "Quality Value", _options.QualityValue,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _options.QualityValue = v; });
        c1.CreateAndAddDescriptionRow("Vapor phase mole fraction for the Quality Line, between 0.0 and 1.0.");
        c1.CreateAndAddCheckBoxRow("Stability Curve", _options.StabilityCurve,
            (cb, e) => _options.StabilityCurve = cb.IsChecked.GetValueOrDefault());
        c1.CreateAndAddDescriptionRow("Includes the Stability Curve in the chart (TP only). Works with the PR and SRK Property Packages.");
        c1.CreateAndAddCheckBoxRow("Phase Identification Curve", _options.PhaseIdentificationCurve,
            (cb, e) => _options.PhaseIdentificationCurve = cb.IsChecked.GetValueOrDefault());
        c1.CreateAndAddDescriptionRow("Calculates the PI curve (TP only), where the region above the curve is a liquid-like phase and the region beyond it is a vapor-like phase. Calculated with the PR EOS.");
        c1.CreateAndAddCheckBoxRow("Operation Point", _options.OperatingPoint,
            (cb, e) => _options.OperatingPoint = cb.IsChecked.GetValueOrDefault());
        c1.CreateAndAddDescriptionRow("Includes the operating point in the chart.");
        c1.CreateAndAddCheckBoxRow("Solid-Liquid Equilibrium", _options.SolidLiquidEquilibrium,
            (cb, e) => _options.SolidLiquidEquilibrium = cb.IsChecked.GetValueOrDefault());
        c1.CreateAndAddDescriptionRow("Includes SLE curves (liquidus/solidus) in the chart. Requires compounds with fusion data.");
        c1.CreateAndAddCheckBoxRow("Widom Line", _options.WidomLine,
            (cb, e) => _options.WidomLine = cb.IsChecked.GetValueOrDefault());
        c1.CreateAndAddDescriptionRow("Includes the Widom line (loci of Cp and kT maxima) in the supercritical region (TP only).");

        BuildInitializationTab(c2, su, nf, bubble: true);
        BuildInitializationTab(c3, su, nf, bubble: false);

        var tabs = new TabControl { Height = 330, Margin = new Thickness(0, 4, 0, 0) };
        tabs.Items.Add(NewTab("Envelope Options", c1));
        tabs.Items.Add(NewTab("BP Initialization", c2));
        tabs.Items.Add(NewTab("DP Initialization", c3));

        panel.CreateAndAddControlRow(tabs);

        OptionsHost.Content = panel;
    }

    private void BuildInitializationTab(AvaloniaEditorPanel c, IUnitsOfMeasure su, string nf, bool bubble)
    {
        var flashes = new List<string> { "PVF", "TVF" };
        var what = bubble ? "bubble" : "dew";

        c.CreateAndAddCheckBoxRow("Custom Initialization",
            bubble ? _options.BubbleUseCustomParameters : _options.DewUseCustomParameters,
            (cb, e) =>
            {
                var v = cb.IsChecked.GetValueOrDefault();
                if (bubble) _options.BubbleUseCustomParameters = v; else _options.DewUseCustomParameters = v;
            });
        c.CreateAndAddDescriptionRow(
            $"Use the custom initialization options if the generated curve for {what} points has invalid points.");

        var initialFlash = bubble ? _options.BubbleCurveInitialFlash : _options.DewCurveInitialFlash;
        c.CreateAndAddDropDownRow("Initial Flash", flashes, Math.Max(0, flashes.IndexOf(initialFlash)),
            (dd, e) =>
            {
                if (dd.SelectedIndex < 0) return;
                var v = flashes[dd.SelectedIndex];
                if (bubble) _options.BubbleCurveInitialFlash = v; else _options.DewCurveInitialFlash = v;
            });

        c.CreateAndAddTextBoxRow(nf, "Initial Pressure (" + su.pressure + ")",
            cv.ConvertFromSI(su.pressure, bubble ? _options.BubbleCurveInitialPressure : _options.DewCurveInitialPressure),
            (tb, e) =>
            {
                if (!UtilityHelpers.TryVal(tb.Text, out var v)) return;
                var si = cv.ConvertToSI(su.pressure, v);
                if (bubble) _options.BubbleCurveInitialPressure = si; else _options.DewCurveInitialPressure = si;
            });

        c.CreateAndAddTextBoxRow(nf, "Initial Temperature (" + su.temperature + ")",
            cv.ConvertFromSI(su.temperature, bubble ? _options.BubbleCurveInitialTemperature : _options.DewCurveInitialTemperature),
            (tb, e) =>
            {
                if (!UtilityHelpers.TryVal(tb.Text, out var v)) return;
                var si = cv.ConvertToSI(su.temperature, v);
                if (bubble) _options.BubbleCurveInitialTemperature = si; else _options.DewCurveInitialTemperature = si;
            });

        c.CreateAndAddTextBoxRow(nf, "Maximum Temperature (" + su.temperature + ")",
            cv.ConvertFromSI(su.temperature, bubble ? _options.BubbleCurveMaximumTemperature : _options.DewCurveMaximumTemperature),
            (tb, e) =>
            {
                if (!UtilityHelpers.TryVal(tb.Text, out var v)) return;
                var si = cv.ConvertToSI(su.temperature, v);
                if (bubble) _options.BubbleCurveMaximumTemperature = si; else _options.DewCurveMaximumTemperature = si;
            });

        c.CreateAndAddTextBoxRow(nf, "Pressure Step (" + su.deltaP + ")",
            cv.ConvertFromSI(su.deltaP, bubble ? _options.BubbleCurveDeltaP : _options.DewCurveDeltaP),
            (tb, e) =>
            {
                if (!UtilityHelpers.TryVal(tb.Text, out var v)) return;
                var si = cv.ConvertToSI(su.deltaP, v);
                if (bubble) _options.BubbleCurveDeltaP = si; else _options.DewCurveDeltaP = si;
            });

        c.CreateAndAddTextBoxRow(nf, "Temperature Step (" + su.deltaT + ")",
            cv.ConvertFromSI(su.deltaT, bubble ? _options.BubbleCurveDeltaT : _options.DewCurveDeltaT),
            (tb, e) =>
            {
                if (!UtilityHelpers.TryVal(tb.Text, out var v)) return;
                var si = cv.ConvertToSI(su.deltaT, v);
                if (bubble) _options.BubbleCurveDeltaT = si; else _options.DewCurveDeltaT = si;
            });

        c.CreateAndAddTextBoxRow("N0", "Maximum Points",
            bubble ? _options.BubbleCurveMaximumPoints : _options.DewCurveMaximumPoints,
            (tb, e) =>
            {
                if (!UtilityHelpers.TryVal(tb.Text, out var v)) return;
                var n = (int)Math.Round(v);
                if (n <= 0) return;
                if (bubble) _options.BubbleCurveMaximumPoints = n; else _options.DewCurveMaximumPoints = n;
            });
    }

    private static TabItem NewTab(string header, Control content)
    {
        return new TabItem
        {
            Header = header,
            Content = new ScrollViewer
            {
                Content = content,
                HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            }
        };
    }

    // -------------------------------------------------------------------------
    // Calculation
    // -------------------------------------------------------------------------

    private async Task BuildEnvelopeAsync()
    {
        if (_cbStream.SelectedIndex < 0 || _cbType.SelectedIndex < 0)
        {
            StatusLabel.Text = "Select a material stream and an envelope type first.";
            return;
        }

        var tag = _streams[_cbStream.SelectedIndex];
        if (_flowsheet.GetFlowsheetSimulationObject(tag) is not MaterialStream ms)
        {
            StatusLabel.Text = $"'{tag}' is not a material stream.";
            return;
        }

        var calcType = _cbType.SelectedIndex switch
        {
            0 => sc.CalculationType.PhaseEnvelopePT,
            1 => sc.CalculationType.PhaseEnvelopePH,
            2 => sc.CalculationType.PhaseEnvelopePS,
            3 => sc.CalculationType.PhaseEnvelopeTH,
            4 => sc.CalculationType.PhaseEnvelopeTS,
            5 => sc.CalculationType.PhaseEnvelopeVP,
            _ => sc.CalculationType.PhaseEnvelopeVT
        };

        BtnBuild.IsEnabled = false;
        StatusLabel.Text = "Calculating envelope lines...";
        GridData.ItemsSource = null;

        try
        {
            var calc = new sc.Calculation(ms)
            {
                CalcType = calcType,
                PhaseEnvelopeOptions = _options
            };

            var results = await Task.Run(() => calc.Calculate());

            if (results.ExceptionResult != null)
            {
                _plot.Clear();
                GridData.ItemsSource = null;
                _lastReport = results.ExceptionResult.Message;
                StatusLabel.Text = "Calculation failed: " + results.ExceptionResult.Message;
                return;
            }

            _lastReport = results.TextOutput;
            RenderPlot(results, calcType, ms, tag);
            PopulateDataGrid();
            StatusLabel.Text = $"Done. {_plot.Series.Count} curve(s) plotted.";
        }
        catch (Exception ex)
        {
            _plot.Clear();
            GridData.ItemsSource = null;
            _lastReport = ex.ToString();
            StatusLabel.Text = "Calculation failed.";
        }
        finally
        {
            BtnBuild.IsEnabled = true;
        }
    }

    /// <summary>
    /// Rebuilds the chart from CalculationResults.Data. The series pairing per diagram type
    /// mirrors the PlotModels that ShortcutUtilities.Calculate builds for the Windows UI.
    /// </summary>
    private void RenderPlot(sc.CalculationResults results, sc.CalculationType calcType,
        MaterialStream ms, string tag)
    {
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;

        _plot.Clear();
        _plot.PlotSubtitle = $"{tag} / Model: {ms.PropertyPackage?.ComponentName}";

        List<double>? C(string key) => UtilityHelpers.Curve(results, key);

        var props = ms.Phases[0].Properties;
        double opT = cv.ConvertFromSI(su.temperature, props.temperature.GetValueOrDefault());
        double opP = cv.ConvertFromSI(su.pressure, props.pressure.GetValueOrDefault());
        double opH = cv.ConvertFromSI(su.enthalpy, props.enthalpy.GetValueOrDefault());
        double opS = cv.ConvertFromSI(su.entropy, props.entropy.GetValueOrDefault());

        var cp = C("CP");

        switch (calcType)
        {
            case sc.CalculationType.PhaseEnvelopePT:
                _plot.PlotTitle = "Pressure/Temperature diagram";
                _plot.XAxisTitle = $"Temperature ({su.temperature})";
                _plot.YAxisTitle = $"Pressure ({su.pressure})";
                _plot.AddSeries("Bubble Points", C("TB"), C("PB"));
                _plot.AddSeries("Dew Points", C("TD"), C("PD"));
                if (cp != null && cp.Count >= 2)
                    _plot.AddSeries("Critical Point", new[] { cp[0] }, new[] { cp[1] }, scatter: true);
                if (_options.PhaseIdentificationCurve)
                    _plot.AddSeries("Phase Identification Parameter", C("TI"), C("PI"));
                if (_options.QualityLine)
                    _plot.AddSeries($"Quality Curve VF = {_options.QualityValue}", C("TQ"), C("PQ"));
                if (_options.StabilityCurve)
                    _plot.AddSeries("Stability Curve", C("TE"), C("PE"));
                if (_options.OperatingPoint)
                    _plot.AddSeries("Operating Point", new[] { opT }, new[] { opP }, scatter: true);
                _plot.AddSeries("SLE Liquidus", C("TSLE1"), C("PSLE1"));
                _plot.AddSeries("SLE Solidus", C("TSLE2"), C("PSLE2"));
                _plot.AddSeries("Widom Line (Cp)", C("TWidomCp"), C("PWidomCp"), dashes: new double[] { 4, 3 });
                _plot.AddSeries("Widom Line (kT)", C("TWidomBetaT"), C("PWidomBetaT"), dashes: new double[] { 6, 2, 2, 2 });
                _plot.AddSeries("Widom Line (avg)", C("TWidomAvg"), C("PWidomAvg"));
                break;

            case sc.CalculationType.PhaseEnvelopePH:
                _plot.PlotTitle = "Pressure/Enthalpy diagram";
                _plot.XAxisTitle = $"Enthalpy ({su.enthalpy})";
                _plot.YAxisTitle = $"Pressure ({su.pressure})";
                _plot.AddSeries("Bubble Points", C("HB"), C("PB"));
                _plot.AddSeries("Dew Points", C("HD"), C("PD"));
                if (_options.OperatingPoint)
                    _plot.AddSeries("Operating Point", new[] { opH }, new[] { opP }, scatter: true);
                break;

            case sc.CalculationType.PhaseEnvelopePS:
                _plot.PlotTitle = "Pressure/Entropy diagram";
                _plot.XAxisTitle = $"Entropy ({su.entropy})";
                _plot.YAxisTitle = $"Pressure ({su.pressure})";
                _plot.AddSeries("Bubble Points", C("SB"), C("PB"));
                _plot.AddSeries("Dew Points", C("SD"), C("PD"));
                if (_options.OperatingPoint)
                    _plot.AddSeries("Operating Point", new[] { opS }, new[] { opP }, scatter: true);
                break;

            case sc.CalculationType.PhaseEnvelopeTH:
                _plot.PlotTitle = "Temperature/Enthalpy diagram";
                _plot.XAxisTitle = $"Enthalpy ({su.enthalpy})";
                _plot.YAxisTitle = $"Temperature ({su.temperature})";
                _plot.AddSeries("Bubble Points", C("HB"), C("TB"));
                _plot.AddSeries("Dew Points", C("HD"), C("TD"));
                if (_options.OperatingPoint)
                    _plot.AddSeries("Operating Point", new[] { opH }, new[] { opT }, scatter: true);
                break;

            case sc.CalculationType.PhaseEnvelopeTS:
                _plot.PlotTitle = "Temperature/Entropy diagram";
                _plot.XAxisTitle = $"Entropy ({su.entropy})";
                _plot.YAxisTitle = $"Temperature ({su.temperature})";
                _plot.AddSeries("Bubble Points", C("SB"), C("TB"));
                _plot.AddSeries("Dew Points", C("SD"), C("TD"));
                if (_options.OperatingPoint)
                    _plot.AddSeries("Operating Point", new[] { opS }, new[] { opT }, scatter: true);
                break;

            case sc.CalculationType.PhaseEnvelopeVT:
                _plot.PlotTitle = "Volume/Temperature diagram";
                _plot.XAxisTitle = $"Temperature ({su.temperature})";
                _plot.YAxisTitle = $"Volume ({su.molar_volume})";
                _plot.AddSeries("Bubble Points", C("TB"), C("VB"));
                _plot.AddSeries("Dew Points", C("TD"), C("VD"));
                if (cp != null && cp.Count >= 3)
                    _plot.AddSeries("Critical Point", new[] { cp[0] }, new[] { cp[2] }, scatter: true);
                break;

            default: // PhaseEnvelopeVP
                _plot.PlotTitle = "Volume/Pressure diagram";
                _plot.XAxisTitle = $"Pressure ({su.pressure})";
                _plot.YAxisTitle = $"Volume ({su.molar_volume})";
                _plot.AddSeries("Bubble Points", C("PB"), C("VB"));
                _plot.AddSeries("Dew Points", C("PD"), C("VD"));
                if (cp != null && cp.Count >= 3)
                    _plot.AddSeries("Critical Point", new[] { cp[1] }, new[] { cp[2] }, scatter: true);
                break;
        }

        _plot.InvalidateVisual();
    }

    private sealed class PhaseRow
    {
        public string BX { get; set; } = "";
        public string BY { get; set; } = "";
        public string DX { get; set; } = "";
        public string DY { get; set; } = "";
    }

    /// <summary>Fills the Data tab grid with the bubble and dew envelope curves (from the plot),
    /// as in the classic UI, instead of a monospace text dump.</summary>
    private void PopulateDataGrid()
    {
        var nf = _flowsheet.FlowsheetOptions.NumberFormat;
        var bubble = _plot.Series.FirstOrDefault(s => s.Title == "Bubble Points");
        var dew = _plot.Series.FirstOrDefault(s => s.Title == "Dew Points");
        int n = Math.Max(bubble?.Count ?? 0, dew?.Count ?? 0);

        var rows = new List<PhaseRow>();
        for (int i = 0; i < n; i++)
            rows.Add(new PhaseRow
            {
                BX = (bubble != null && i < bubble.Count) ? bubble.X[i].ToString(nf) : "",
                BY = (bubble != null && i < bubble.Count) ? bubble.Y[i].ToString(nf) : "",
                DX = (dew != null && i < dew.Count) ? dew.X[i].ToString(nf) : "",
                DY = (dew != null && i < dew.Count) ? dew.Y[i].ToString(nf) : ""
            });

        GridData.Columns.Clear();
        GridData.Columns.Add(new DataGridTextColumn { Header = $"Bubble {_plot.XAxisTitle}", Binding = new global::Avalonia.Data.Binding("BX"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        GridData.Columns.Add(new DataGridTextColumn { Header = $"Bubble {_plot.YAxisTitle}", Binding = new global::Avalonia.Data.Binding("BY"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        GridData.Columns.Add(new DataGridTextColumn { Header = $"Dew {_plot.XAxisTitle}", Binding = new global::Avalonia.Data.Binding("DX"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        GridData.Columns.Add(new DataGridTextColumn { Header = $"Dew {_plot.YAxisTitle}", Binding = new global::Avalonia.Data.Binding("DY"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        GridData.ItemsSource = rows;
    }

    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var top = GetTopLevel(this);
        if (top?.Clipboard == null) return;
        await top.Clipboard.SetTextAsync(text!);
        StatusLabel.Text = "Copied to the clipboard.";
    }
}
