using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.Streams;
using DWSIM.Thermodynamics.Utilities.Hydrates;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Natural gas hydrate formation conditions of a material stream. Avalonia counterpart of the
/// WinForms FormHYD; both drive the engine's HydrateCalculator.
/// </summary>
public sealed class HydratesWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;

    private ComboBox _streams = null!, _models = null!;
    private CheckBox _vaporOnly = null!;
    private Button _btnRun = null!;

    private readonly StackPanel _results = new() { Spacing = 2, Margin = new Thickness(8, 4, 8, 4) };
    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    public HydratesWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;

        Title = "Natural Gas Hydrates";
        Width = 620;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Stream");
        var tags = UtilityHelpers.MaterialStreamTags(_flowsheet);
        _streams = p.CreateAndAddDropDownRow("Material Stream", tags, tags.Count > 0 ? 0 : -1, null);
        p.CreateAndAddDescriptionRow("The stream must contain water. Hydrate formation is evaluated at the stream temperature and pressure.");

        p.CreateAndAddLabelRow("Model");
        var models = new List<string>
        {
            "van der Waals - Platteeuw",
            "Klauda - Sandler",
            "Chen - Guo",
            "Klauda - Sandler (modified)"
        };
        _models = p.CreateAndAddDropDownRow("Thermodynamic Model", models, 0, null);
        _vaporOnly = p.CreateAndAddCheckBoxRow("Vapor-hydrate equilibrium only (no free water phase)", false, null);

        _btnRun = new Button
        {
            Content = "Calculate",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(8)
        };
        _btnRun.Classes.Add("action");
        _btnRun.Click += async (_, _) => await CalculateAsync();

        var bottom = new StackPanel { Margin = new Thickness(8, 0, 8, 8), Spacing = 4 };
        bottom.Children.Add(_status);

        var top = new StackPanel();
        top.Children.Add(p);
        top.Children.Add(_results);

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_btnRun, global::Avalonia.Controls.Dock.Bottom);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(_btnRun);
        dock.Children.Add(new ScrollViewer { Content = top, Padding = new Thickness(8) });
        return dock;
    }

    private async Task CalculateAsync()
    {
        _results.Children.Clear();

        var idx = _streams.SelectedIndex;
        if (idx < 0) { _status.Text = "Select a material stream."; return; }

        var obj = _flowsheet.SimulationObjects.Values
            .FirstOrDefault(x => x.GraphicObject != null &&
                                 x.GraphicObject.Tag == (string)_streams.SelectedItem!) as MaterialStream;
        if (obj == null) { _status.Text = "Stream not found."; return; }

        var model = (HydrateModel)Math.Max(0, _models.SelectedIndex);
        var vaporOnly = _vaporOnly.IsChecked.GetValueOrDefault();

        _btnRun.IsEnabled = false;
        _status.Text = "Calculating, please wait...";

        HydrateResults res;
        try
        {
            res = await Task.Run(() => HydrateCalculator.Calculate(obj, model, vaporOnly));
        }
        catch (Exception ex)
        {
            _status.Text = ex.InnerException?.Message ?? ex.Message;
            _btnRun.IsEnabled = true;
            return;
        }

        ShowResults(res, vaporOnly);
        _btnRun.IsEnabled = true;
        _status.Text = res.FormsHydrate
            ? "Hydrates form at the stream conditions."
            : "No hydrates form at the stream conditions.";
    }

    private void ShowResults(HydrateResults res, bool vaporOnly)
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Stream Conditions");
        p.CreateAndAddTwoLabelsRow("Temperature",
            Fmt(cv.ConvertFromSI(_su.temperature, res.StreamTemperature)) + " " + _su.temperature);
        p.CreateAndAddTwoLabelsRow("Pressure",
            Fmt(cv.ConvertFromSI(_su.pressure, res.StreamPressure)) + " " + _su.pressure);

        p.CreateAndAddLabelRow("Formation Conditions");

        // above 600 atm the models have no meaningful answer
        p.CreateAndAddTwoLabelsRow("Formation Pressure at Stream T",
            res.FormationPressure > 600 * 101325
                ? "not determined"
                : Fmt(cv.ConvertFromSI(_su.pressure, res.FormationPressure)) + " " + _su.pressure);
        p.CreateAndAddTwoLabelsRow("Structure at Stream T", res.StructureAtStreamTemperature);
        p.CreateAndAddTwoLabelsRow("Phases at Stream T",
            DescribePhases(vaporOnly, res.StructureAtStreamTemperature,
                res.StreamTemperature, res.DetailsAtStreamTemperature));

        p.CreateAndAddTwoLabelsRow("Formation Temperature at Stream P",
            res.FormationTemperature < 0
                ? "not determined"
                : Fmt(cv.ConvertFromSI(_su.temperature, res.FormationTemperature)) + " " + _su.temperature);
        p.CreateAndAddTwoLabelsRow("Structure at Stream P", res.StructureAtStreamPressure);
        p.CreateAndAddTwoLabelsRow("Phases at Stream P",
            DescribePhases(vaporOnly, res.StructureAtStreamPressure,
                res.FormationTemperature, res.DetailsAtStreamPressure));

        p.CreateAndAddLabelRow("Verdict");
        p.CreateAndAddTwoLabelsRow("Hydrates Form", res.FormsHydrate ? "Yes" : "No");
        p.CreateAndAddTwoLabelsRow("Structure", res.FormsHydrate ? res.FormedStructure : "-");

        AddPhaseComposition(p, "Hydrate Phase Composition (at stream T)", res.DetailsAtStreamTemperature);

        _results.Children.Add(p);
    }

    /// <summary>
    /// Reads the ice / liquid water transition temperature from the model detail output and
    /// names the phases in equilibrium with the hydrate.
    /// </summary>
    private static string DescribePhases(bool vaporOnly, string structure, double t, object[]? details)
    {
        if (vaporOnly) return $"vapor and hydrate ({structure})";
        if (details == null || details.Length == 0) return $"hydrate ({structure})";

        var t0 = Convert.ToDouble(details[0]);
        if (Math.Abs(t - t0) < 0.1) return $"ice, liquid water, gas and hydrate ({structure})";
        if (t < t0) return $"ice, gas and hydrate ({structure})";
        return $"liquid water, gas and hydrate ({structure})";
    }

    /// <summary>Cage occupancy and hydrate composition, when the model reports them.</summary>
    private void AddPhaseComposition(AvaloniaEditorPanel p, string title, object[]? details)
    {
        if (details == null || details.Length < 7) return;

        var names = _flowsheet.SelectedCompounds.Keys.ToList();
        var vh = details[6] as Array;
        if (vh == null) return;

        p.CreateAndAddLabelRow(title);
        for (int i = 0; i < Math.Min(names.Count, vh.Length); i++)
        {
            var x = Convert.ToDouble(vh.GetValue(i));
            if (x <= 0) continue;
            p.CreateAndAddTwoLabelsRow(names[i], x.ToString("G6"));
        }
    }

    private string Fmt(double v) => v.ToString(_nf);
}
