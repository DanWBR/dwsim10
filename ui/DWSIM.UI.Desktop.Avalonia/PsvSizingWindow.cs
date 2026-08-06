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
    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

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
        p.CreateAndAddDescriptionRow("The inlet stream sets the relieving conditions and the outlet stream sets the back pressure. Solve the flowsheet before sizing.");

        p.CreateAndAddLabelRow("Sizing Basis");
        var fluids = new List<string> { "Liquid", "Vapor", "Two-Phase (gas-liquid)" };
        _fluid = p.CreateAndAddDropDownRow("Relieving Fluid", fluids, 1, null);
        p.CreateAndAddTwoLabelsRow("Method", "API RP 520");

        p.CreateAndAddLabelRow("Coefficients");
        p.CreateAndAddTextBoxRow(_nf, "Discharge Coefficient Kd", _kd,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _kd = v; });
        p.CreateAndAddTextBoxRow(_nf, "Back Pressure Correction Kb", _kb,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _kb = v; });
        p.CreateAndAddTextBoxRow(_nf, "Rupture Disk Combination Kc", _kc,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _kc = v; });
        p.CreateAndAddTextBoxRow(_nf, "Overpressure (%)", _overpressure,
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _overpressure = v; });

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

        double me_m = inlet.Phases[0].Properties.density.GetValueOrDefault();
        double QT = inlet.Phases[0].Properties.volumetric_flow.GetValueOrDefault();

        double visc_l = inlet.Phases[3].Properties.viscosity.GetValueOrDefault();
        double me_l = inlet.Phases[3].Properties.density.GetValueOrDefault();
        double QL = inlet.Phases[3].Properties.volumetric_flow.GetValueOrDefault();

        double WV = inlet.Phases[2].Properties.massflow.GetValueOrDefault();
        double me_v = inlet.Phases[2].Properties.density.GetValueOrDefault();
        double zg = inlet.Phases[2].Properties.compressibilityFactor.GetValueOrDefault();
        double cp = inlet.Phases[2].Properties.heatCapacityCp.GetValueOrDefault();
        double cvv = inlet.Phases[2].Properties.heatCapacityCv.GetValueOrDefault();
        double cpcv = cvv == 0.0 ? 1.0 : cp / cvv;
        double mm_g = inlet.Phases[2].Properties.molecularWeight.GetValueOrDefault();
        double xm_g = inlet.Phases[2].Properties.massfraction.GetValueOrDefault();

        // the API correlations take pressures in kgf/cm2 absolute
        double Prel = (P * 1.033 / 101325) * (1 + _overpressure / 100);
        double Pback = BP * 1.033 / 101325;

        var sz = new PSV.Sizing();
        double Ao;

        try
        {
            switch (_fluid.SelectedIndex)
            {
                case 0:
                    Ao = Convert.ToDouble(sz.PSV_LCC_D(QL * 24 * 3600, Prel, Pback, me_l, visc_l, _kd, _kc));
                    break;
                case 2:
                    var rho90 = DensityAtReducedPressure(inlet);
                    var tmp2 = (object[])sz.PSV_GL_D23_D(xm_g, me_v, me_m, rho90,
                        Prel - 1.033, Pback - 1.033, QT * 24 * 3600, _kd, _kb, _kc);
                    Ao = Convert.ToDouble(tmp2[0]);
                    break;
                default:
                    Ao = Convert.ToDouble(sz.PSV_G_D(Prel, Pback, T, WV * 3600, zg, mm_g, cpcv, _kd, _kb, _kc));
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
            _status.Text = "The orifice area could not be calculated. Check the relieving conditions and the stream phases.";
            return;
        }

        var orif = (object[])sz.ORIF_API(Ao);

        var p = new AvaloniaEditorPanel();
        p.CreateAndAddLabelRow("Relieving Conditions");
        p.CreateAndAddTwoLabelsRow("Temperature", cv.ConvertFromSI(_su.temperature, T).ToString(_nf) + " " + _su.temperature);
        p.CreateAndAddTwoLabelsRow("Set Pressure", cv.ConvertFromSI(_su.pressure, P).ToString(_nf) + " " + _su.pressure);
        p.CreateAndAddTwoLabelsRow("Back Pressure", cv.ConvertFromSI(_su.pressure, BP).ToString(_nf) + " " + _su.pressure);

        p.CreateAndAddLabelRow("Results");
        p.CreateAndAddTwoLabelsRow("Required Orifice Area", Ao.ToString("N2") + " cm2");
        p.CreateAndAddTwoLabelsRow("API Orifice Designation", Convert.ToString(orif[1]));
        p.CreateAndAddTwoLabelsRow("API Orifice Area", Convert.ToDouble(orif[2]).ToString("N2") + " cm2");

        _results.Children.Add(p);
        _status.Text = "Done.";
    }

    /// <summary>
    /// Two-phase sizing needs the mixture density at 90 % of the relieving pressure, from a
    /// flash on a copy of the inlet stream.
    /// </summary>
    private static double DensityAtReducedPressure(MaterialStream inlet)
    {
        var clone = (MaterialStream)inlet.Clone();
        clone.SetFlowsheet(inlet.GetFlowsheet());
        clone.PropertyPackage = inlet.PropertyPackage;
        clone.Phases[0].Properties.pressure = inlet.Phases[0].Properties.pressure.GetValueOrDefault() * 0.9;
        clone.PropertyPackage.CurrentMaterialStream = clone;
        clone.Calculate();
        return clone.Phases[0].Properties.density.GetValueOrDefault();
    }
}
