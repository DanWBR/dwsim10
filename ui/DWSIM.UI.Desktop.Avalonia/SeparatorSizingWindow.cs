using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.Utilities.Sizing;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Souders-Brown sizing of a gas-liquid separator already on the flowsheet. Avalonia counterpart
/// of the WinForms FrmDAVP; both drive the engine's SeparatorSizing.
/// </summary>
public sealed class SeparatorSizingWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;

    private ComboBox _vessels = null!, _orientation = null!;
    private Button _btnRun = null!;

    private readonly SeparatorSizingInput _input = new();
    private readonly StackPanel _results = new() { Spacing = 2, Margin = new Thickness(8, 4, 8, 4) };
    private readonly TextBlock _feed = new() { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    public SeparatorSizingWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;

        Title = "Gas-Liquid Separator Sizing";
        Width = 620;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Separator");

        var tags = _flowsheet.SimulationObjects.Values
            .Where(x => x.GraphicObject != null && x.GraphicObject.ObjectType == ObjectType.Vessel)
            .Select(x => x.GraphicObject.Tag)
            .OrderBy(x => x)
            .ToList();

        _vessels = p.CreateAndAddDropDownRow("Separator", tags, tags.Count > 0 ? 0 : -1, null);
        p.CreateAndAddDescriptionRow("The vessel must be connected and solved: the inlet and the two outlet streams supply the densities and flows.");
        p.CreateAndAddControlRow(_feed);

        _orientation = p.CreateAndAddDropDownRow("Orientation", new List<string> { "Vertical", "Horizontal" }, 0, null);

        p.CreateAndAddLabelRow("Design Parameters");
        p.CreateAndAddDescriptionRow("Vessel: the diameter is the larger of what the gas velocity needs (Souders-Brown) and what holds the liquid for the residence time at the aspect ratio below, with one diameter of vapour space above the liquid. Vertical: height = L/D x diameter. Horizontal: length = L/D x diameter, liquid level found where gas area and liquid holdup agree.");
        p.CreateAndAddTextBoxRow(_nf, "Length / Diameter Ratio", _input.LengthToDiameter,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _input.LengthToDiameter = v; });
        p.CreateAndAddTextBoxRow(_nf, "Souders-Brown K (m/s)", _input.KFactor,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _input.KFactor = v; });
        p.CreateAndAddTextBoxRow(_nf, "Gas Velocity (% of terminal)", _input.GasVelocityPercent,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _input.GasVelocityPercent = v; });
        p.CreateAndAddDescriptionRow("Terminal (settling) velocity = K x sqrt((liquid density - gas density) / gas density). The design gas velocity is this percentage of it; a lower percentage gives a wider vessel.");
        p.CreateAndAddTextBoxRow(_nf, "Nozzle Constant", _input.NozzleConstant,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _input.NozzleConstant = v; });
        p.CreateAndAddDescriptionRow("Sizes the inlet and gas outlet nozzles only (not the vessel): maximum nozzle velocity in m/s = constant / sqrt(stream density in kg/m3), the usual rho.v^2 momentum criterion. 100 means rho.v^2 = 10000 kg/(m.s2); a lower constant gives bigger nozzles.");
        p.CreateAndAddTextBoxRow(_nf, "Max. Liquid Nozzle Velocity (m/s)", _input.MaxLiquidVelocity,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _input.MaxLiquidVelocity = v; });
        p.CreateAndAddDescriptionRow("Sizes the liquid outlet nozzle only.");
        p.CreateAndAddTextBoxRow(_nf, "Liquid Residence Time (min)", _input.ResidenceTime,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _input.ResidenceTime = v; });
        p.CreateAndAddTextBoxRow(_nf, "Surge Factor", _input.SurgeFactor,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _input.SurgeFactor = v; });
        p.CreateAndAddDescriptionRow("Multiplies both the gas and the liquid flow before sizing (design margin). Values typed in the boxes are applied when you press Enter or leave the box.");

        _btnRun = new Button
        {
            Content = "Size Separator",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(8)
        };
        _btnRun.Classes.Add("action");
        _btnRun.Click += (_, _) => Calculate();

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

    private void Calculate()
    {
        _results.Children.Clear();

        if (_vessels.SelectedIndex < 0) { _status.Text = "Select a separator."; return; }

        var vessel = _flowsheet.SimulationObjects.Values
            .FirstOrDefault(x => x.GraphicObject != null &&
                                 x.GraphicObject.Tag == (string)_vessels.SelectedItem!);
        if (vessel == null) { _status.Text = "Separator not found."; return; }

        if (!SeparatorSizing.ReadStreams(_flowsheet, vessel, _input))
        {
            _status.Text = "Connect the separator inlet, the gas outlet and the liquid outlet before sizing it.";
            return;
        }

        if (_input.VaporDensity <= 0 || _input.LiquidDensity <= 0)
        {
            _status.Text = "Solve the flowsheet first: the outlet stream densities are zero.";
            return;
        }

        _feed.Text =
            $"Liquid: {Conv(_su.volumetricFlow, _input.LiquidVolumetricFlow)} {_su.volumetricFlow}, " +
            $"{Conv(_su.density, _input.LiquidDensity)} {_su.density}\n" +
            $"Gas: {Conv(_su.volumetricFlow, _input.VaporVolumetricFlow)} {_su.volumetricFlow}, " +
            $"{Conv(_su.density, _input.VaporDensity)} {_su.density}";

        SeparatorSizingResults res;
        try
        {
            res = _orientation.SelectedIndex == 1
                ? SeparatorSizing.SizeHorizontal(_input)
                : SeparatorSizing.SizeVertical(_input);
        }
        catch (Exception ex)
        {
            _status.Text = "Sizing failed: " + ex.Message;
            return;
        }

        var p = new AvaloniaEditorPanel();
        p.CreateAndAddLabelRow("Vessel");
        p.CreateAndAddTwoLabelsRow("Minimum Diameter", res.Diameter.ToString("N1") + " mm");
        p.CreateAndAddTwoLabelsRow(_orientation.SelectedIndex == 1 ? "Minimum Length" : "Minimum Height",
            res.Length.ToString("N1") + " mm");
        if (_orientation.SelectedIndex != 1)
            p.CreateAndAddDescriptionRow(res.DiameterSetByLiquid
                ? "The liquid holdup set the diameter: the gas alone would need a narrower vessel, but the liquid would not fit at this L/D. A shorter residence time or a higher L/D gives a narrower vessel."
                : "The gas velocity set the diameter; the liquid holdup fits within the height at this L/D.");

        p.CreateAndAddLabelRow("Nozzles");
        p.CreateAndAddTwoLabelsRow("Inlet", res.InletNozzle.ToString("N2") + " in");
        p.CreateAndAddTwoLabelsRow("Gas Outlet", res.GasNozzle.ToString("N2") + " in");
        p.CreateAndAddTwoLabelsRow("Liquid Outlet", res.LiquidNozzle.ToString("N2") + " in");

        _results.Children.Add(p);
        _status.Text = "Done.";
    }

    private string Conv(string units, double si) => cv.ConvertFromSI(units, si).ToString(_nf);
}
