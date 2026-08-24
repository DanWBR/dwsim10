using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.UI.Desktop.Editors;
using DWSIM.UI.Desktop.Avalonia.Controls;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Constant and temperature-dependent properties of a pure compound. Avalonia counterpart of
/// the WinForms FormPureComp and the Eto CompoundViewer; the curves come from the shared
/// CompoundCurveBuilder.
/// </summary>
public sealed class PureCompoundPropertiesWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;

    private ComboBox _compounds = null!;
    private readonly string? _initial;
    private readonly TabControl _tabs = new();
    private readonly TextBlock _status = new() { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    /// <param name="compound">Compound to show first; the first one of the simulation when null.</param>
    public PureCompoundPropertiesWindow(IFlowsheet flowsheet, string? compound = null)
    {
        _flowsheet = flowsheet;
        _initial = compound;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;

        Title = "Pure Compound Properties";
        Width = 900;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        Content = BuildContent();

        if (_compounds.ItemCount > 0) _compounds.SelectedIndex = 0;
        if (_initial != null) SelectCompound(_initial);
    }

    private Control BuildContent()
    {
        var names = _flowsheet.SelectedCompounds.Keys.OrderBy(x => x).ToList();

        // a compound picked from the settings grid may not be in the simulation yet
        if (_initial != null && !names.Contains(_initial) &&
            _flowsheet.AvailableCompounds.ContainsKey(_initial))
        {
            names.Add(_initial);
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
        }

        _compounds = new ComboBox { ItemsSource = names, Width = 320 };
        _compounds.SelectionChanged += (_, _) => ShowCompound();

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(new TextBlock { Text = "Compound", VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(_compounds);

        var btnClose = new Button { Content = "Close", Width = 90, IsCancel = true };
        btnClose.Classes.Add("dialog");
        btnClose.Click += (_, _) => Close();

        var bottom = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(btnClose, global::Avalonia.Controls.Dock.Right);
        bottom.Children.Add(btnClose);
        bottom.Children.Add(_status);

        var root = new DockPanel();
        DockPanel.SetDock(header, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(bottom);
        root.Children.Add(_tabs);
        return root;
    }

    private void ShowCompound()
    {
        _tabs.Items.Clear();

        if (_compounds.SelectedItem is not string name) return;
        if (!_flowsheet.SelectedCompounds.TryGetValue(name, out var icp) &&
            !_flowsheet.AvailableCompounds.TryGetValue(name, out icp)) return;

        var cp = (ConstantProperties)icp;

        _tabs.Items.Add(new TabItem
        {
            Header = "Constant",
            Content = new ScrollViewer { Content = BuildConstantPanel(cp), Padding = new Thickness(8) }
        });
        _tabs.Items.Add(new TabItem
        {
            Header = "Molecular",
            Content = new ScrollViewer { Content = BuildMolecularPanel(cp), Padding = new Thickness(8) }
        });

        try
        {
            var builder = new CompoundCurveBuilder(_flowsheet, cp);
            if (builder.HasCurves)
            {
                var curves = builder.Build();
                AddCurveTab("Liquid", curves, new[]
                {
                    "Liquid Heat Capacity", "Vapor Pressure", "Heat of Vaporization",
                    "Liquid Density", "Liquid Viscosity", "Liquid Thermal Conductivity", "Surface Tension"
                });
                AddCurveTab("Vapor", curves, new[]
                {
                    "Ideal Gas Heat Capacity", "Vapor Viscosity", "Vapor Thermal Conductivity"
                });
                AddCurveTab("Solid", curves, new[] { "Solid Heat Capacity", "Solid Density" });
                _status.Text = $"{curves.Count(c => c.HasData)} property curve(s) available.";
            }
            else
            {
                _status.Text = "Ions, salts and black oil pseudocompounds have no property curves.";
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Could not build the property curves: " + ex.Message;
        }

        _tabs.SelectedIndex = 0;
    }

    private void AddCurveTab(string header, List<CompoundCurve> curves, string[] titles)
    {
        var inner = new TabControl();

        foreach (var title in titles)
        {
            var curve = curves.FirstOrDefault(x => x.Title == title);
            if (curve == null || !curve.HasData) continue;

            var plot = new XYPlot
            {
                PlotTitle = curve.Title,
                XAxisTitle = curve.XTitle,
                YAxisTitle = curve.YTitle,
                Margin = new Thickness(4)
            };
            plot.AddSeries(curve.Title, curve.X.ToArray(), curve.Y.ToArray());

            inner.Items.Add(new TabItem { Header = ShortName(title), Content = plot });
        }

        if (inner.ItemCount == 0) return;

        inner.SelectedIndex = 0;
        _tabs.Items.Add(new TabItem { Header = header, Content = inner });
    }

    /// <summary>Drops the phase prefix, which is already the outer tab header.</summary>
    private static string ShortName(string title)
    {
        foreach (var prefix in new[] { "Liquid ", "Vapor ", "Solid " })
            if (title.StartsWith(prefix)) return title.Substring(prefix.Length);
        return title;
    }

    private Control BuildConstantPanel(ConstantProperties c)
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Identification");
        p.CreateAndAddTwoLabelsRow("Database", c.OriginalDB ?? "");
        p.CreateAndAddTwoLabelsRow("ID", c.ID.ToString());
        p.CreateAndAddTwoLabelsRow("CAS Number", c.CAS_Number ?? "");

        p.CreateAndAddLabelRow("Critical and Basic Properties");
        Val(p, "Molar Weight (" + _su.molecularWeight + ")", c.Molar_Weight);
        Conv(p, "Critical Temperature", _su.temperature, c.Critical_Temperature);
        Conv(p, "Critical Pressure", _su.pressure, c.Critical_Pressure);
        Conv(p, "Critical Volume", _su.molar_volume, c.Critical_Volume);
        Val(p, "Critical Compressibility", c.Critical_Compressibility);
        Val(p, "Acentric Factor", c.Acentric_Factor);
        Conv(p, "Normal Boiling Point", _su.temperature, c.Normal_Boiling_Point);
        Conv(p, "Temperature of Fusion", _su.temperature, c.TemperatureOfFusion);
        Val(p, "Enthalpy of Fusion at Tf (kJ/mol)", c.EnthalpyOfFusionAtTf);

        p.CreateAndAddLabelRow("Formation Properties");
        Conv(p, "IG Enthalpy of Formation @ 25 C", _su.enthalpy, c.IG_Enthalpy_of_Formation_25C);
        Conv(p, "IG Gibbs Energy of Formation @ 25 C", _su.enthalpy, c.IG_Gibbs_Energy_of_Formation_25C);

        p.CreateAndAddLabelRow("Model Parameters");
        Val(p, "Chao-Seader Acentric Factor", c.Chao_Seader_Acentricity);
        Val(p, "Chao-Seader Solubility Parameter", c.Chao_Seader_Solubility_Parameter);
        Val(p, "Chao-Seader Liquid Molar Volume (mL/mol)", c.Chao_Seader_Liquid_Molar_Volume);
        Val(p, "Rackett Compressibility", c.Z_Rackett);
        Val(p, "Peng-Robinson Volume Translation Coefficient", c.PR_Volume_Translation_Coefficient);
        Val(p, "SRK Volume Translation Coefficient", c.SRK_Volume_Translation_Coefficient);
        Val(p, "UNIQUAC R", c.UNIQUAC_R);
        Val(p, "UNIQUAC Q", c.UNIQUAC_Q);

        p.CreateAndAddLabelRow("Electrolytes");
        p.CreateAndAddTwoLabelsRow("Charge", c.Charge.ToString("#;-#;0"));
        p.CreateAndAddTwoLabelsRow("Hydration Number", c.HydrationNumber.ToString());
        p.CreateAndAddTwoLabelsRow("Positive Ion", c.PositiveIon ?? "");
        p.CreateAndAddTwoLabelsRow("Negative Ion", c.NegativeIon ?? "");
        Conv(p, "Temperature of Solid Density Ts", _su.temperature, c.SolidTs);
        Conv(p, "Solid Density at Ts", _su.density, c.SolidDensityAtTs);
        Val(p, "Del Gf (kJ/mol)", c.Electrolyte_DelGF);
        Val(p, "Del Hf (kJ/mol)", c.Electrolyte_DelHF);
        Val(p, "Cp0 (kJ/[mol.K])", c.Electrolyte_Cp0);
        Val(p, "Standard State Molar Volume (cm3/mol)", c.StandardStateMolarVolume);

        p.CreateAndAddLabelRow("Black Oil");
        Val(p, "Gas Specific Gravity", c.BO_SGG);
        Val(p, "Oil Specific Gravity", c.BO_SGO);
        Conv(p, "Gas-Oil Ratio", _su.gor, c.BO_GOR);
        Val(p, "BSW", c.BO_BSW);
        Conv(p, "Oil Viscosity 1", _su.cinematic_viscosity, c.BO_OilVisc1);
        Conv(p, "Oil Viscosity Temperature 1", _su.temperature, c.BO_OilViscTemp1);
        Conv(p, "Oil Viscosity 2", _su.cinematic_viscosity, c.BO_OilVisc2);
        Conv(p, "Oil Viscosity Temperature 2", _su.temperature, c.BO_OilViscTemp2);
        Val(p, "Paraffins", c.BO_PNA_P);
        Val(p, "Naphthenes", c.BO_PNA_N);
        Val(p, "Aromatics", c.BO_PNA_A);

        return p;
    }

    private Control BuildMolecularPanel(ConstantProperties c)
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Molecular Properties");
        p.CreateAndAddTwoLabelsRow("Formula", c.Formula ?? "");
        p.CreateAndAddTwoLabelsRow("SMILES String", c.SMILES ?? "");
        p.CreateAndAddTwoLabelsRow("InChI String", c.InChI ?? "");

        p.CreateAndAddLabelRow("Group Contribution");
        p.CreateAndAddDescriptionRow("UNIFAC: " + Groups(c.UNIFACGroups,
            id => new Thermodynamics.PropertyPackages.Auxiliary.Unifac().ID2Group(id)));
        p.CreateAndAddDescriptionRow("MODFAC-Do: " + Groups(c.MODFACGroups,
            id => new Thermodynamics.PropertyPackages.Auxiliary.Modfac().ID2Group(id)));
        p.CreateAndAddDescriptionRow("MODFAC-NIST: " + Groups(c.NISTMODFACGroups,
            id => new Thermodynamics.PropertyPackages.Auxiliary.NISTMFAC().ID2Group(id)));

        return p;
    }

    private static string Groups(System.Collections.SortedList? groups, Func<int, string> nameOf)
    {
        if (groups == null || groups.Count == 0) return "not defined";

        var terms = new List<string>();
        foreach (System.Collections.DictionaryEntry kvp in groups)
        {
            try { terms.Add(nameOf(int.Parse(Convert.ToString(kvp.Key)!)) + " " + kvp.Value); }
            catch (Exception) { }
        }
        return terms.Count > 0 ? string.Join(", ", terms) : "not defined";
    }

    private void Val(AvaloniaEditorPanel p, string label, double value)
        => p.CreateAndAddTwoLabelsRow(label, value.ToString(_nf));

    private void Conv(AvaloniaEditorPanel p, string label, string units, double siValue)
        => p.CreateAndAddTwoLabelsRow(label + " (" + units + ")",
            cv.ConvertFromSI(units, siValue).ToString(_nf));

    /// <summary>Brings a compound to the front, when the window lists it.</summary>
    public void SelectCompound(string name)
    {
        var index = (_compounds.ItemsSource as List<string>)?.IndexOf(name) ?? -1;
        if (index >= 0) _compounds.SelectedIndex = index;
    }
}
