using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using PSV = DWSIM.Thermodynamics.Utilities.PSV;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// API RP 520 orifice sizing for a pressure safety valve on the flowsheet. Avalonia counterpart
/// of the WinForms FrmPsvSize; both drive the engine's PSV sizing class.
/// </summary>
public sealed class PsvSizingWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;

    private ComboBox _valves = null!, _fluid = null!;
    private Button _btnRun = null!;

    private double _kd = 0.85, _kb = 1.0, _kc = 1.0, _overpressure = 10.0;

    private readonly StackPanel _results = new() { Spacing = 2, Margin = new Thickness(8, 4, 8, 4) };
    private readonly TextBlock _status = new() { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    public PsvSizingWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;

        Title = "Pressure Safety Valve Sizing";
        Width = 620;
        Height = 660;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Valve");

        var tags = _flowsheet.SimulationObjects.Values
            .Where(x => x.GraphicObject != null &&
                        x.GraphicObject.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.Valve)
            .Select(x => x.GraphicObject.Tag)
            .OrderBy(x => x)
            .ToList();

        _valves = p.CreateAndAddDropDownRow("Valve", tags, tags.Count > 0 ? 0 : -1, null);
        p.CreateAndAddDescriptionRow("The inlet stream sets the relieving conditions and its pressure is taken as the set pressure; the outlet stream sets the back pressure. Both are absolute pressures. Solve the flowsheet before sizing.");

        p.CreateAndAddLabelRow("Sizing Basis");
        var fluids = new List<string> { "Liquid", "Vapor", "Two-Phase (gas-liquid)" };
        _fluid = p.CreateAndAddDropDownRow("Relieving Fluid", fluids, 1, null);
        p.CreateAndAddTwoLabelsRow("Method", "API RP 520");

        p.CreateAndAddLabelRow("Coefficients");
        p.CreateAndAddTextBoxRow(_nf, "Discharge Coefficient Kd", _kd,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _kd = v; });
        p.CreateAndAddTextBoxRow(_nf, "Back Pressure Correction Kb (Kw for liquid)", _kb,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _kb = v; });
        p.CreateAndAddTextBoxRow(_nf, "Rupture Disk Combination Kc", _kc,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _kc = v; });
        p.CreateAndAddTextBoxRow(_nf, "Overpressure (% of the gauge set pressure)", _overpressure,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _overpressure = v; });
        p.CreateAndAddDescriptionRow("API 520 relieving pressure: P1 = set pressure (gauge) x (1 + overpressure) + 1 atm, absolute.");
        p.CreateAndAddDescriptionRow("The API 520 gas equation assumes an ideal gas with an ideal-gas k = Cp/Cv. The tool passes the real Cp/Cv of the vapour, so near-critical or dense gases (Z far from 1, large Cp/Cv) need a check by the direct integration (HDI) method of API 520 Annex B.");

        _btnRun = new Button
        {
            Content = "Size Orifice",
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

        if (_valves.SelectedIndex < 0) { _status.Text = "Select a valve."; return; }

        var valve = _flowsheet.SimulationObjects.Values
            .FirstOrDefault(x => x.GraphicObject != null &&
                                 x.GraphicObject.Tag == (string)_valves.SelectedItem!);
        if (valve == null) { _status.Text = "Valve not found."; return; }

        MaterialStream inlet, outlet;
        try
        {
            var go = valve.GraphicObject;
            inlet = (MaterialStream)_flowsheet.SimulationObjects[go.InputConnectors[0].AttachedConnector.AttachedFrom.Name];
            outlet = (MaterialStream)_flowsheet.SimulationObjects[go.OutputConnectors[0].AttachedConnector.AttachedTo.Name];
        }
        catch
        {
            _status.Text = "Connect the valve inlet and outlet before sizing it.";
            return;
        }

        double T = inlet.Phases[0].Properties.temperature.GetValueOrDefault();
        double P = inlet.Phases[0].Properties.pressure.GetValueOrDefault();
        double BP = outlet.Phases[0].Properties.pressure.GetValueOrDefault();

        double WT = inlet.Phases[0].Properties.massflow.GetValueOrDefault();

        double visc_l = inlet.Phases[3].Properties.viscosity.GetValueOrDefault();
        double me_l = inlet.Phases[3].Properties.density.GetValueOrDefault();
        double QL = inlet.Phases[3].Properties.volumetric_flow.GetValueOrDefault();

        double WV = inlet.Phases[2].Properties.massflow.GetValueOrDefault();
        double zg = inlet.Phases[2].Properties.compressibilityFactor.GetValueOrDefault();
        double cp = inlet.Phases[2].Properties.heatCapacityCp.GetValueOrDefault();
        double cvv = inlet.Phases[2].Properties.heatCapacityCv.GetValueOrDefault();
        double cpcv = cvv == 0.0 ? 1.0 : cp / cvv;
        double mm_g = inlet.Phases[2].Properties.molecularWeight.GetValueOrDefault();

        // the stream pressures are absolute; P1 = set pressure (gauge) x (1 + overpressure) + 1 atm
        double P1 = PSV.Sizing.RelievingPressure(P, _overpressure);

        double Ao;
        string? warning = null;
        var extra = new List<(string, string)>();

        try
        {
            switch (_fluid.SelectedIndex)
            {
                case 0:
                    Ao = PSV.Sizing.LiquidArea(QL, P1, BP, me_l, visc_l, _kd, _kb, _kc);
                    break;
                case 2:
                    var v = PSV.Sizing.OmegaSpecificVolumes(inlet);
                    var tp = PSV.Sizing.TwoPhaseArea(v[0], v[1], P1, BP, WT, _kd, _kb, _kc);
                    Ao = tp[0];
                    extra.Add(("Omega Parameter", tp[1].ToString("N3")));
                    extra.Add(("Flow Regime", tp[4] > 0.5 ? "Critical" : "Subcritical"));
                    if (!(tp[1] > 0.0))
                        warning = "The mixture does not expand on the isentropic flash to 90 % of the inlet pressure, so the omega method does not apply. Size it as a liquid.";
                    break;
                default:
                    Ao = PSV.Sizing.GasArea(P1, BP, T, WV, zg, mm_g, cpcv, _kd, _kb, _kc);
                    extra.Add(("Compressibility Factor Z", zg.ToString("N3")));
                    extra.Add(("Cp/Cv", cpcv.ToString("N3")));
                    extra.Add(("Flow Regime", BP <= PSV.Sizing.CriticalFlowPressure(P1, cpcv) ? "Critical" : "Subcritical"));
                    warning = PSV.Sizing.IdealGasWarning(zg, cpcv);
                    break;
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Sizing failed: " + (ex.InnerException?.Message ?? ex.Message);
            return;
        }

        if (double.IsNaN(Ao) || double.IsInfinity(Ao) || Ao <= 0)
        {
            _status.Text = warning ?? "The orifice area could not be calculated. Check the relieving conditions and the stream phases.";
            return;
        }

        var orif = PSV.Sizing.StandardOrifice(Ao);

        var p = new AvaloniaEditorPanel();
        p.CreateAndAddLabelRow("Relieving Conditions");
        p.CreateAndAddTwoLabelsRow("Temperature", cv.ConvertFromSI(_su.temperature, T).ToString(_nf) + " " + _su.temperature);
        p.CreateAndAddTwoLabelsRow("Set Pressure (inlet stream)", FormatPressure(P));
        p.CreateAndAddTwoLabelsRow("Relieving Pressure P1", FormatPressure(P1));
        p.CreateAndAddTwoLabelsRow("Back Pressure (outlet stream)", FormatPressure(BP));
        foreach (var (label, value) in extra) p.CreateAndAddTwoLabelsRow(label, value);

        p.CreateAndAddLabelRow("Results");
        p.CreateAndAddTwoLabelsRow("Required Orifice Area", Ao.ToString("N3") + " in2 (" + (Ao * 645.16).ToString("N0") + " mm2)");
        if (orif.Item2 > 0)
        {
            p.CreateAndAddTwoLabelsRow("API Orifice Designation", orif.Item1);
            p.CreateAndAddTwoLabelsRow("API Orifice Area", orif.Item2.ToString("N3") + " in2 (" + (orif.Item2 * 645.16).ToString("N0") + " mm2)");
        }
        else
        {
            p.CreateAndAddTwoLabelsRow("API Orifice Designation", "Larger than T: use several valves");
        }

        _results.Children.Add(p);
        _status.Text = warning ?? "Done.";
    }

    /// <summary>
    /// An absolute pressure in the flowsheet unit, marked absolute, or gauge when the unit is a gauge unit.
    /// </summary>
    private string FormatPressure(double paAbsolute)
    {
        var u = _su.pressure;
        var gauge = u is "barg" or "psig" or "kPag" or "kgf/cm2g";
        return cv.ConvertFromSI(u, paAbsolute).ToString(_nf) + " " + u + (gauge ? " (gauge)" : " (absolute)");
    }
}
