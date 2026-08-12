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
/// Binary Envelope utility. Avalonia counterpart of
/// DWSIM.UI.Desktop.Editors.Utilities.BinaryEnvelopeView.
///
/// Builds a throwaway two-compound MaterialStream at the requested T/P, hands it to
/// DWSIM.Thermodynamics.ShortcutUtilities and renders the returned curves.
/// </summary>
public partial class BinaryEnvelopeWindow : Window
{
    private static readonly string[] EnvelopeTypes = { "P-x/y", "T-x/y" };

    private readonly IFlowsheet _flowsheet;
    private readonly XYPlot _plot = new();

    private List<string> _compounds = new();
    private List<string> _packages = new();

    private ComboBox _cbComp1 = null!;
    private ComboBox _cbComp2 = null!;
    private ComboBox _cbType = null!;
    private ComboBox _cbPackage = null!;
    private TextBox _tbT = null!;
    private TextBox _tbP = null!;

    private bool _vle = true, _lle, _sle, _critical, _areas;

    // Full text report, kept for the Copy Report button now that Data is shown as a grid.
    private string _lastReport = "";

    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public BinaryEnvelopeWindow() : this(null!) { }

    public BinaryEnvelopeWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet!;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        ChartHost.Child = _plot;
        BuildOptionsPanel();

        BtnBuild.Click += async (_, _) => await BuildEnvelopeAsync();
        BtnCopyData.Click += async (_, _) => await CopyAsync(_lastReport);
        BtnCopyCsv.Click += async (_, _) => await CopyAsync(_plot.ToDelimitedText());
    }

    // -------------------------------------------------------------------------
    // Setup panel
    // -------------------------------------------------------------------------

    private void BuildOptionsPanel()
    {
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var nf = _flowsheet.FlowsheetOptions.NumberFormat;

        _compounds = _flowsheet.SelectedCompounds.Values.Select(x => x.Name).OrderBy(x => x).ToList();
        _packages = _flowsheet.PropertyPackages.Values.Select(x => x.Tag).ToList();

        var panel = new AvaloniaEditorPanel();

        panel.CreateAndAddLabelRow("Envelope Setup");
        panel.CreateAndAddDescriptionRow(
            "The Binary Envelope utility calculates temperature and pressure VLE/VLLE envelopes for binary mixtures.");

        _cbComp1 = panel.CreateAndAddDropDownRow("Compound 1", _compounds,
            _compounds.Count > 0 ? 0 : -1, null);
        _cbComp2 = panel.CreateAndAddDropDownRow("Compound 2", _compounds,
            _compounds.Count > 1 ? 1 : -1, null);
        _cbType = panel.CreateAndAddDropDownRow("Envelope Type", EnvelopeTypes.ToList(), 1, null);

        _tbT = panel.CreateAndAddTextBoxRow(nf, "Temperature (" + su.temperature + ")",
            cv.ConvertFromSI(su.temperature, 298.15), null);
        _tbP = panel.CreateAndAddTextBoxRow(nf, "Pressure (" + su.pressure + ")",
            cv.ConvertFromSI(su.pressure, 101325.0), null);
        panel.CreateAndAddDescriptionRow(
            "T-x/y diagrams are built at the pressure above; P-x/y diagrams at the temperature above.");

        panel.CreateAndAddLabelRow("Display Options");

        panel.CreateAndAddCheckBoxRow("VLE", _vle, (cb, e) => _vle = cb.IsChecked.GetValueOrDefault());
        panel.CreateAndAddDescriptionRow("VLE calculation works on all diagram types.");
        panel.CreateAndAddCheckBoxRow("LLE", _lle, (cb, e) => _lle = cb.IsChecked.GetValueOrDefault());
        panel.CreateAndAddDescriptionRow("LLE calculation works on T-x/y and P-x/y diagrams if the selected Property Package is associated with a Flash Algorithm which supports liquid-liquid equilibria.");
        panel.CreateAndAddCheckBoxRow("SLE", _sle, (cb, e) => _sle = cb.IsChecked.GetValueOrDefault());
        panel.CreateAndAddDescriptionRow("SLE calculation works on T-x/y diagrams only.");
        panel.CreateAndAddCheckBoxRow("Critical Line", _critical, (cb, e) => _critical = cb.IsChecked.GetValueOrDefault());
        panel.CreateAndAddDescriptionRow("Critical Line calculation works on T-x/y diagrams only.");
        panel.CreateAndAddCheckBoxRow("Highlight Regions", _areas, (cb, e) => _areas = cb.IsChecked.GetValueOrDefault());
        panel.CreateAndAddDescriptionRow("Asks the engine to compute the phase-region boundaries used to shade the diagram.");

        _cbPackage = panel.CreateAndAddDropDownRow("Property Package", _packages,
            _packages.Count > 0 ? 0 : -1, null);

        panel.CreateAndAddEmptySpace();

        OptionsHost.Content = panel;
    }

    // -------------------------------------------------------------------------
    // Calculation
    // -------------------------------------------------------------------------

    private async Task BuildEnvelopeAsync()
    {
        if (_cbComp1.SelectedIndex < 0 || _cbComp2.SelectedIndex < 0)
        {
            StatusLabel.Text = "Select both compounds first.";
            return;
        }
        if (_cbComp1.SelectedIndex == _cbComp2.SelectedIndex)
        {
            StatusLabel.Text = "Compound 1 and Compound 2 must be different.";
            return;
        }
        if (_cbPackage.SelectedIndex < 0)
        {
            StatusLabel.Text = "The flowsheet has no property package to calculate with.";
            return;
        }

        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var comp1 = _flowsheet.SelectedCompounds[_compounds[_cbComp1.SelectedIndex]];
        var comp2 = _flowsheet.SelectedCompounds[_compounds[_cbComp2.SelectedIndex]];
        var ppTag = _packages[_cbPackage.SelectedIndex];

        var pp = _flowsheet.PropertyPackages.Values.FirstOrDefault(x => x.Tag == ppTag) as PropertyPackage;
        if (pp == null)
        {
            StatusLabel.Text = $"Property package '{ppTag}' could not be resolved.";
            return;
        }

        var calcType = _cbType.SelectedIndex == 0
            ? sc.CalculationType.BinaryEnvelopePxy
            : sc.CalculationType.BinaryEnvelopeTxy;

        // Scratch stream holding only the two selected compounds; never added to the flowsheet.
        var ms = new MaterialStream("", "");
        ms.SetFlowsheet(_flowsheet);
        ms.PropertyPackage = pp;

        foreach (var phase in ms.Phases.Values)
        {
            phase.Compounds.Add(comp1.Name, new DWSIM.Thermodynamics.BaseClasses.Compound(comp1.Name, ""));
            phase.Compounds[comp1.Name].ConstantProperties = comp1;
            phase.Compounds.Add(comp2.Name, new DWSIM.Thermodynamics.BaseClasses.Compound(comp2.Name, ""));
            phase.Compounds[comp2.Name].ConstantProperties = comp2;
        }

        if (UtilityHelpers.TryVal(_tbT.Text, out var tval))
            ms.Phases[0].Properties.temperature = cv.ConvertToSI(su.temperature, tval);
        if (UtilityHelpers.TryVal(_tbP.Text, out var pval))
            ms.Phases[0].Properties.pressure = cv.ConvertToSI(su.pressure, pval);

        BtnBuild.IsEnabled = false;
        StatusLabel.Text = "Calculating envelope lines...";
        GridData.ItemsSource = null;

        try
        {
            var calc = new sc.Calculation(ms)
            {
                CalcType = calcType,
                DisplayEnvelopeAreas = _areas,
                BinaryEnvelopeOptions = new object[] { "", 0, 0, _vle, _lle, _sle, _critical, false }
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
            PopulateDataGrid(results, calcType, comp1.Name);
            RenderPlot(results, calcType, comp1.Name, comp2.Name, pp.ComponentName);
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
    /// Rebuilds the chart from CalculationResults.Data, matching the curve set that
    /// ShortcutUtilities puts in its PlotModel for the Windows UI.
    /// </summary>
    private void RenderPlot(sc.CalculationResults results, sc.CalculationType calcType,
        string comp1, string comp2, string ppName)
    {
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var nf = _flowsheet.FlowsheetOptions.NumberFormat;

        List<double>? C(string key) => UtilityHelpers.Curve(results, key);

        _plot.Clear();

        bool txy = calcType == sc.CalculationType.BinaryEnvelopeTxy;

        _plot.PlotTitle = txy
            ? $"Binary Envelope (Txy) @ {_tbP.Text} {su.pressure}"
            : $"Binary Envelope (Pxy) @ {_tbT.Text} {su.temperature}";
        _plot.PlotSubtitle = $"{comp1} / {comp2} / Model: {ppName}";
        _plot.XAxisTitle = $"Mole Fraction {comp1}";
        _plot.YAxisTitle = txy ? $"Temperature ({su.temperature})" : $"Pressure ({su.pressure})";

        var px = C("px");
        _plot.AddSeries("Bubble Points", px, C("py1"));
        _plot.AddSeries("Dew Points", px, C("py2"));

        var py3 = C("py3");
        _plot.AddSeries("Liquid-Liquid (1)", C("px1l1"), py3);
        _plot.AddSeries("Liquid-Liquid (2)", C("px1l2"), py3);

        // SLE and the critical line are only produced for T-x/y.
        _plot.AddSeries("Solid-Liquid (1)", C("pxs1"), C("pys1"));
        _plot.AddSeries("Solid-Liquid (2)", C("pxs2"), C("pys2"));
        _plot.AddSeries("Critical Line", C("pxc"), C("pyc"));

        _plot.InvalidateVisual();
    }

    private sealed class EnvRow
    {
        public string X { get; set; } = "";
        public string Bubble { get; set; } = "";
        public string Dew { get; set; } = "";
    }

    /// <summary>Fills the Data tab grid with the VLE curve (mole fraction, bubble, dew),
    /// as in the classic UI, instead of a monospace text dump.</summary>
    private void PopulateDataGrid(sc.CalculationResults results, sc.CalculationType calcType, string comp1)
    {
        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var nf = _flowsheet.FlowsheetOptions.NumberFormat;
        bool txy = calcType == sc.CalculationType.BinaryEnvelopeTxy;
        string yunit = txy ? su.temperature : su.pressure;

        var px = UtilityHelpers.Curve(results, "px");
        var py1 = UtilityHelpers.Curve(results, "py1");
        var py2 = UtilityHelpers.Curve(results, "py2");

        var rows = new List<EnvRow>();
        if (px != null)
            for (int i = 0; i < px.Count; i++)
                rows.Add(new EnvRow
                {
                    X = px[i].ToString(nf),
                    Bubble = (py1 != null && i < py1.Count) ? py1[i].ToString(nf) : "",
                    Dew = (py2 != null && i < py2.Count) ? py2[i].ToString(nf) : ""
                });

        GridData.Columns.Clear();
        GridData.Columns.Add(new DataGridTextColumn { Header = $"{comp1} Mole Fraction", Binding = new global::Avalonia.Data.Binding("X"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        GridData.Columns.Add(new DataGridTextColumn { Header = $"Bubble Point ({yunit})", Binding = new global::Avalonia.Data.Binding("Bubble"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        GridData.Columns.Add(new DataGridTextColumn { Header = $"Dew Point ({yunit})", Binding = new global::Avalonia.Data.Binding("Dew"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
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
