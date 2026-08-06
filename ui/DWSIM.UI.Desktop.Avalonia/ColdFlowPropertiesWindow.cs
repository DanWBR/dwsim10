using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.Streams;
using DWSIM.Thermodynamics.Utilities.PetroleumProperties;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Cold flow properties of a petroleum stream. Avalonia counterpart of the WinForms
/// FrmColdProperties; both drive the engine's ColdFlowProperties.
/// </summary>
public sealed class ColdFlowPropertiesWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;

    private ComboBox _streams = null!;
    private Button _btnRun = null!;

    private readonly StackPanel _results = new() { Spacing = 2, Margin = new Thickness(8, 4, 8, 4) };
    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    public ColdFlowPropertiesWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;

        Title = "Petroleum Cold Flow Properties";
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
        p.CreateAndAddDescriptionRow("Flash point, pour point, cloud point, freezing point, refraction index and cetane index from the API correlations (procedures 2B5.1, 2B7.1, 2B8.1, 2B11.1, 2B12.1 and 2B13.1). The calculation runs on a copy of the stream.");

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

        if (_streams.SelectedIndex < 0) { _status.Text = "Select a material stream."; return; }

        var obj = _flowsheet.SimulationObjects.Values
            .FirstOrDefault(x => x.GraphicObject != null &&
                                 x.GraphicObject.Tag == (string)_streams.SelectedItem!) as MaterialStream;
        if (obj == null) { _status.Text = "Stream not found."; return; }

        _btnRun.IsEnabled = false;
        _status.Text = "Calculating, please wait...";

        ColdFlowResults res;
        try
        {
            res = await Task.Run(() =>
            {
                var clone = (MaterialStream)obj.Clone();
                clone.SetFlowsheet(obj.GetFlowsheet());
                return ColdFlowProperties.Calculate(clone);
            });
        }
        catch (Exception ex)
        {
            _status.Text = ex.InnerException?.Message ?? ex.Message;
            _btnRun.IsEnabled = true;
            return;
        }

        ShowResults(res);
        _btnRun.IsEnabled = true;
        _status.Text = "Done.";
    }

    private void ShowResults(ColdFlowResults res)
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Vapor Pressure");
        Row(p, "True Vapor Pressure @ 100 F", res.TrueVaporPressure, _su.pressure);
        Row(p, "Reid Vapor Pressure", res.ReidVaporPressure, _su.pressure);

        p.CreateAndAddLabelRow("Viscosity");
        Row(p, "Dynamic Viscosity @ 100 F", res.Viscosity37C, _su.viscosity);
        Row(p, "Dynamic Viscosity @ 210 F", res.Viscosity98C, _su.viscosity);

        p.CreateAndAddLabelRow("Cold Flow Properties");
        Row(p, "Flash Point", res.FlashPoint, _su.temperature);
        Row(p, "Pour Point", res.PourPoint, _su.temperature);
        Row(p, "Cloud Point", res.CloudPoint, _su.temperature);
        Row(p, "Freezing Point", res.FreezingPoint, _su.temperature);

        p.CreateAndAddLabelRow("Indexes");
        p.CreateAndAddTwoLabelsRow("Refraction Index", res.RefractionIndex.ToString(_nf));
        p.CreateAndAddTwoLabelsRow("Cetane Index", res.CetaneIndex.ToString(_nf));

        p.CreateAndAddLabelRow("Bulk Properties Used");
        Row(p, "Mean Average Boiling Point", res.MeanAverageBoilingPoint, _su.temperature);
        p.CreateAndAddTwoLabelsRow("Specific Gravity", res.SpecificGravity.ToString(_nf));
        p.CreateAndAddTwoLabelsRow("Watson K", res.WatsonK.ToString(_nf));
        p.CreateAndAddTwoLabelsRow("API Gravity", res.API.ToString(_nf));

        _results.Children.Add(p);
    }

    private void Row(AvaloniaEditorPanel p, string label, double siValue, string units)
    {
        p.CreateAndAddTwoLabelsRow(label,
            cv.ConvertFromSI(units, siValue).ToString(_nf) + " " + units);
    }
}
