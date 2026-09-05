using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.Thermodynamics.Streams;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Splits a polymer into pseudo-components along a molar-mass distribution (Schulz-Zimm or log-normal), so
/// a polydisperse polymer can be modelled with the equation of state as a mixture of cuts of the same
/// chemistry and different molar mass. The cuts share the base compound's CAS, so PC-SAFT reuses its
/// parameters and only the molar mass (segment number) differs. Mn and Mw are reproduced exactly.
/// </summary>
public class PolymerCharacterizationWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private string _baseName;
    private double _mn = 50000.0;
    private double _pdi = 2.0;
    private int _ncuts = 4;
    private DWSIM.Thermodynamics.Polymers.PolymerDistribution _dist = DWSIM.Thermodynamics.Polymers.PolymerDistribution.SchulzZimm;

    private readonly DataGrid _grid = new();
    private readonly ObservableCollection<CutRow> _rows = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private Button _btnAdd;
    private List<ConstantProperties> _cuts = new();

    private sealed class CutRow
    {
        public string Name { get; set; } = "";
        public double M { get; set; }
        public double Z { get; set; }
        public double W { get; set; }
    }

    public PolymerCharacterizationWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        Title = "Polymer Characterization";
        Width = 760;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        _grid.IsReadOnly = true;
        _grid.AutoGenerateColumns = false;
        void AddCol(string header, string path, string fmt, double star)
        {
            _grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new global::Avalonia.Data.Binding(path) { StringFormat = fmt },
                Width = new DataGridLength(star, DataGridLengthUnitType.Star)
            });
        }
        AddCol("Pseudo-component", "Name", "{0}", 2.2);
        AddCol("Molar mass (g/mol)", "M", "{0:N0}", 1.2);
        AddCol("Mole fraction", "Z", "{0:N4}", 1.0);
        AddCol("Mass fraction", "W", "{0:N4}", 1.0);
        _grid.ItemsSource = _rows;

        // base polymer choices: available compounds whose formula reads as a repeat unit "(...)n"
        var polymers = _flowsheet.AvailableCompounds.Values
            .Where(c => !string.IsNullOrEmpty(c.Formula) && c.Formula.TrimEnd().EndsWith("n"))
            .Select(c => c.Name).OrderBy(c => c).ToList();
        if (polymers.Count == 0)
            polymers = _flowsheet.SelectedCompounds.Values.Select(c => c.Name).OrderBy(c => c).ToList();
        _baseName = polymers.FirstOrDefault() ?? "";

        var p = new AvaloniaEditorPanel { Width = 340 };
        p.CreateAndAddLabelRow("Polymer Characterization");
        p.CreateAndAddDescriptionRow("Cuts a polymer into pseudo-components along a molar-mass distribution (Schulz-Zimm or log-normal) so a polydisperse polymer can be modelled as a mixture of cuts of the same chemistry. A log-normal target needs enough cuts to reach the polydispersity (two cuts reach at most Mw/Mn = 2).");

        p.CreateAndAddDropDownRow("Base polymer", polymers, 0,
            (dd, e) => { if (dd.SelectedIndex >= 0 && dd.SelectedIndex < polymers.Count) _baseName = polymers[dd.SelectedIndex]; });

        p.CreateAndAddLabelRow("Distribution");
        var distNames = new List<string> { "Schulz-Zimm (Gamma)", "Log-normal" };
        p.CreateAndAddDropDownRow("Type", distNames, 0, (dd, e) =>
        {
            _dist = dd.SelectedIndex == 1
                ? DWSIM.Thermodynamics.Polymers.PolymerDistribution.LogNormal
                : DWSIM.Thermodynamics.Polymers.PolymerDistribution.SchulzZimm;
        });
        p.CreateAndAddTextBoxRow("N0", "Number-average Mn (g/mol)", _mn,
            (tb, e) => { if (double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var v)) _mn = v; });
        p.CreateAndAddTextBoxRow("N4", "Polydispersity Mw / Mn", _pdi,
            (tb, e) => { if (double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var v)) _pdi = v; });
        p.CreateAndAddNumericEditorRow("Number of cuts", _ncuts, 1, 12, 0,
            (nud, e) => { _ncuts = (int)(nud.Value ?? 4m); });

        p.CreateAndAddLabelRow("Actions");
        p.CreateAndAddButtonRow("Preview Cuts", null, (_, _) => Preview());
        _btnAdd = p.CreateAndAddButtonRow("Add Cuts to Simulation", null, (_, _) => AddToFlowsheet());
        _btnAdd.IsEnabled = false;

        p.CreateAndAddDescriptionRow("Each cut shares the base polymer CAS, so the equation of state reuses its parameters and only the molar mass (segment number) differs. The cut mole fractions are the polymer's relative distribution; set the feed with them once the cuts are added.");

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_grid, 0);
        var side = new ScrollViewer { Content = p, Padding = new Thickness(8) };
        Grid.SetColumn(side, 1);
        body.Children.Add(_grid);
        body.Children.Add(side);

        var btnClose = new Button { Content = "Close", Width = 90, IsCancel = true };
        btnClose.Classes.Add("dialog");
        btnClose.Click += (_, _) => Close();

        var bottom = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(btnClose, global::Avalonia.Controls.Dock.Right);
        bottom.Children.Add(btnClose);
        bottom.Children.Add(_status);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(body);
        return root;
    }

    private void Preview()
    {
        _rows.Clear();
        _cuts.Clear();
        _btnAdd.IsEnabled = false;

        if (string.IsNullOrEmpty(_baseName) || !_flowsheet.AvailableCompounds.ContainsKey(_baseName))
        {
            _status.Text = "Select a base polymer.";
            return;
        }
        if (_mn <= 0 || _pdi <= 1.0)
        {
            _status.Text = "Mn must be positive and the polydispersity greater than one.";
            return;
        }

        var basecp = _flowsheet.AvailableCompounds[_baseName] as ConstantProperties;
        if (basecp == null) { _status.Text = "The base compound is not editable."; return; }

        try
        {
            double[] z = null;
            var cuts = DWSIM.Thermodynamics.Polymers.PolymerCharacterization.BuildCuts(basecp, _mn, _pdi, _ncuts, _dist, ref z);
            double totMass = 0.0, m1 = 0.0, m2 = 0.0;
            for (int i = 0; i < cuts.Count; i++) { totMass += z[i] * cuts[i].Molar_Weight; m1 += z[i] * cuts[i].Molar_Weight; m2 += z[i] * cuts[i].Molar_Weight * cuts[i].Molar_Weight; }
            for (int i = 0; i < cuts.Count; i++)
                _rows.Add(new CutRow { Name = cuts[i].Name, M = cuts[i].Molar_Weight, Z = z[i], W = z[i] * cuts[i].Molar_Weight / totMass });
            _cuts = cuts;
            _btnAdd.IsEnabled = true;
            double mwCut = m2 / m1;
            string note = mwCut < _mn * _pdi * 0.999 ? "  (add cuts to reach the target Mw)" : "";
            _status.Text = $"{cuts.Count} cuts  |  Mn = {m1:N0}, Mw = {mwCut:N0} g/mol (target Mw = {_mn * _pdi:N0}){note}";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not characterize: " + ex.Message;
        }
    }

    private void AddToFlowsheet()
    {
        if (_cuts.Count == 0) return;
        try
        {
            foreach (var cp in _cuts)
            {
                if (!_flowsheet.AvailableCompounds.ContainsKey(cp.Name))
                    _flowsheet.AvailableCompounds.Add(cp.Name, cp);
                if (!_flowsheet.SelectedCompounds.ContainsKey(cp.Name))
                    _flowsheet.SelectedCompounds.Add(cp.Name, _flowsheet.AvailableCompounds[cp.Name]);

                foreach (MaterialStream obj in _flowsheet.SimulationObjects.Values
                             .Where(x => x.GraphicObject != null && x.GraphicObject.ObjectType == ObjectType.MaterialStream))
                    foreach (var phase in obj.Phases.Values)
                    {
                        if (phase.Compounds.ContainsKey(cp.Name)) continue;
                        phase.Compounds.Add(cp.Name, new Compound(cp.Name, ""));
                        phase.Compounds[cp.Name].ConstantProperties = _flowsheet.SelectedCompounds[cp.Name];
                    }
            }

            _flowsheet.UpdateInterface();
            _status.Text = $"{_cuts.Count} cut(s) added to the simulation.";
            _flowsheet.ShowMessage(_status.Text, IFlowsheet.MessageType.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "Could not add cuts: " + ex.Message;
        }
    }
}
