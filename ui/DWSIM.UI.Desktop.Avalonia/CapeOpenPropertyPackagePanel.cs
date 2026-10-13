using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.SharedClasses;
using DWSIM.Thermodynamics.PropertyPackages;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// The CAPE-OPEN tab of the property package editor: picks the Property Package Manager (1.1) or
/// Thermo System (1.0) registered on this machine, then one of its property packages, and maps the
/// flowsheet compounds and phases onto the package's, as the WinForms configuration window does.
///
/// The registry scan runs on a worker thread. Everything that touches the CAPE-OPEN objects runs on
/// the UI thread, which is an STA thread on Windows (Program.Main is [STAThread]).
/// </summary>
public sealed class CapeOpenPropertyPackagePanel : UserControl
{
    private readonly IFlowsheet _flowsheet;
    private readonly CAPEOPENPropertyPackage _pp;

    private readonly ComboBox _servers = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 320 };
    private readonly ComboBox _packages = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 320 };
    private readonly Button _editServer = new() { Content = "Edit manager settings", Margin = new Thickness(6, 0, 0, 0) };
    private readonly Button _editPackage = new() { Content = "Edit package settings", Margin = new Thickness(6, 0, 0, 0) };
    private readonly TextBlock _serverInfo = new() { TextWrapping = TextWrapping.Wrap, Opacity = 0.85, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    private readonly StackPanel _compoundRows = new() { Spacing = 2 };
    private readonly StackPanel _phaseRows = new() { Spacing = 2 };

    private List<CapeOpenObjInfo> _serverList = new();
    private bool _loading = true;

    public CapeOpenPropertyPackagePanel(IFlowsheet flowsheet, CAPEOPENPropertyPackage pp)
    {
        _flowsheet = flowsheet;
        _pp = pp;

        Content = Build();

        _servers.SelectionChanged += (_, _) => OnServerChanged();
        _packages.SelectionChanged += (_, _) => OnPackageChanged();
        _editServer.Click += (_, _) => OnEditServer();
        _editPackage.Click += (_, _) => OnEditPackage();

        AttachedToVisualTree += async (_, _) =>
        {
            if (_serverList.Count == 0 && _loading) await LoadAsync();
        };
    }

    private IEnumerable<ICompoundConstantProperties> FlowsheetCompounds =>
        _flowsheet.SelectedCompounds.Values.Cast<ICompoundConstantProperties>();

    private Control Build()
    {
        var root = new StackPanel { Spacing = 6, Margin = new Thickness(8) };

        root.Children.Add(new TextBlock
        {
            Text = "Select the CAPE-OPEN Property Package Manager (CAPE-OPEN 1.1) or Thermo System (CAPE-OPEN 1.0), " +
                   "then the property package it should deliver. The flowsheet compounds and phases are matched " +
                   "to the package's below.",
            TextWrapping = TextWrapping.Wrap
        });

        root.Children.Add(Row("Manager / Thermo System", _servers, _editServer));
        root.Children.Add(_serverInfo);
        root.Children.Add(Row("Property Package", _packages, _editPackage));
        root.Children.Add(_status);

        root.Children.Add(Heading("Compound Mappings (DWSIM compound → package compound)"));
        root.Children.Add(_compoundRows);
        root.Children.Add(Heading("Phase Mappings (DWSIM phase → package phase)"));
        root.Children.Add(_phaseRows);

        _editServer.Classes.Add("panel");
        _editPackage.Classes.Add("panel");

        return new ScrollViewer { Content = root };
    }

    private static TextBlock Heading(string text) =>
        new() { Text = text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 2) };

    private static Control Row(string label, Control field, Control? button)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*,Auto") };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(text);
        Grid.SetColumn(field, 1);
        grid.Children.Add(field);
        if (button != null)
        {
            Grid.SetColumn(button, 2);
            grid.Children.Add(button);
        }
        return grid;
    }

    private static string Caption(CapeOpenObjInfo info) =>
        info.Name + (string.Equals(info.ImplementedCategory, CAPEOPENPropertyPackage.ThermoSystemCategory10,
            StringComparison.OrdinalIgnoreCase) ? "  (CAPE-OPEN 1.0 Thermo System)" : "  (CAPE-OPEN 1.1 Property Package Manager)");

    private static bool Same(CapeOpenObjInfo a, CapeOpenObjInfo b) =>
        string.Equals(a.TypeName, b.TypeName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.ImplementedCategory, b.ImplementedCategory, StringComparison.OrdinalIgnoreCase);

    private async Task LoadAsync()
    {
        _loading = true;
        _servers.IsEnabled = _packages.IsEnabled = _editServer.IsEnabled = _editPackage.IsEnabled = false;

        if (!OperatingSystem.IsWindows())
        {
            _status.Text = "CAPE-OPEN property packages are Windows COM components. They can be selected and used only " +
                           "when DWSIM runs on Windows.";
            return;
        }

        _status.Text = "Looking for the CAPE-OPEN Property Package Managers and Thermo Systems registered on this computer...";
        try
        {
            _serverList = await Task.Run(() => CAPEOPENPropertyPackage.SearchRegisteredServers());
        }
        catch (Exception ex)
        {
            _status.Text = "The registry scan failed: " + ex.Message;
            return;
        }

        // the server this package already uses, even when it is no longer registered
        var current = _pp._selts;
        if (current != null && !_serverList.Any(x => Same(x, current))) _serverList.Insert(0, current);

        _servers.Items.Clear();
        foreach (var s in _serverList) _servers.Items.Add(Caption(s));
        _servers.IsEnabled = true;

        if (current != null)
        {
            _servers.SelectedIndex = _serverList.FindIndex(x => Same(x, current));
            ShowServerInfo(current);
            if (_pp._pptpl != null) ListPackages(_pp._ppname);
        }

        if (_pp.HasPropertyPackage)
        {
            TryRun(() => _pp.UpdateMappings(FlowsheetCompounds));
        }

        UpdateState();
        RebuildMappings();

        _loading = false;

        if (_serverList.Count == 0)
            _status.Text = "No CAPE-OPEN Property Package Manager or Thermo System is registered on this computer. " +
                           "Install one (COCO's TEA, for example) and open this window again.";
        else if (current == null)
            _status.Text = "Select a Property Package Manager or Thermo System (" + _serverList.Count + " found on this computer).";
        else if (!_pp.HasPropertyPackage)
            _status.Text = "Select a property package.";
        else if (!_status.Text!.StartsWith("Error"))
            _status.Text = "Using property package '" + _pp._ppname + "' from '" + current.Name + "'.";
    }

    private void ShowServerInfo(CapeOpenObjInfo info)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(info.Version)) parts.Add("Version " + info.Version);
        if (!string.IsNullOrEmpty(info.VendorURL)) parts.Add(info.VendorURL);
        if (!string.IsNullOrEmpty(info.Description)) parts.Add(info.Description);
        _serverInfo.Text = string.Join("  |  ", parts);
    }

    private void ListPackages(string select)
    {
        _packages.Items.Clear();
        List<string> names = new();
        if (!TryRun(() => names = _pp.GetAvailablePropertyPackages())) return;
        foreach (var n in names) _packages.Items.Add(n);
        if (!string.IsNullOrEmpty(select))
        {
            if (!names.Contains(select)) _packages.Items.Add(select);
            _packages.SelectedItem = select;
        }
        _packages.IsEnabled = true;
    }

    private void OnServerChanged()
    {
        if (_loading) return;
        var idx = _servers.SelectedIndex;
        if (idx < 0 || idx >= _serverList.Count) return;

        var info = _serverList[idx];
        ShowServerInfo(info);

        _loading = true;
        try
        {
            if (TryRun(() => _pp.SelectServer(info)))
            {
                ListPackages("");
                _status.Text = _packages.Items.Count == 0
                    ? "'" + info.Name + "' offers no property packages. Use 'Edit manager settings' to create one."
                    : "Select one of the " + _packages.Items.Count + " property package(s) offered by '" + info.Name + "'.";
            }
            else
            {
                _packages.Items.Clear();
            }
        }
        finally
        {
            _loading = false;
        }

        UpdateState();
        RebuildMappings();
    }

    private void OnPackageChanged()
    {
        if (_loading) return;
        if (_packages.SelectedItem is not string name) return;

        if (TryRun(() => _pp.SelectPropertyPackage(name, FlowsheetCompounds)))
            _status.Text = "Property package '" + name + "' selected.";

        UpdateState();
        RebuildMappings();
    }

    private void OnEditServer()
    {
        bool editable = false;
        if (!TryRun(() => editable = _pp.EditServer())) return;
        if (!editable)
        {
            _status.Text = "This Property Package Manager has no settings window of its own.";
            return;
        }
        // a manager can add or remove packages in its own editor
        _loading = true;
        try { ListPackages(_pp._ppname); } finally { _loading = false; }
    }

    private void OnEditPackage()
    {
        bool editable = false;
        if (!TryRun(() => editable = _pp.EditPropertyPackage())) return;
        if (!editable)
        {
            _status.Text = "This property package has no settings window of its own.";
            return;
        }
        // the package's compounds may have changed in its own editor
        TryRun(() => _pp.UpdateMappings(FlowsheetCompounds));
        RebuildMappings();
    }

    private void UpdateState()
    {
        _editServer.IsEnabled = _pp._pptpl != null;
        _editPackage.IsEnabled = _pp.HasPropertyPackage;
    }

    private void RebuildMappings()
    {
        _compoundRows.Children.Clear();
        _phaseRows.Children.Clear();

        if (!_pp.HasPropertyPackage)
        {
            _compoundRows.Children.Add(new TextBlock { Text = "Select a property package first.", Opacity = 0.7 });
            return;
        }

        List<string> ids = new(), labels = new();
        TryRun(() =>
        {
            ids = _pp.GetPackageCompoundIds();
            labels = _pp.GetPackagePhaseLabels();
        });

        foreach (var key in _pp._mappings.Keys.ToList())
        {
            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var id in ids) combo.Items.Add(id);
            var current = _pp._mappings[key] ?? "";
            if (current != "" && !ids.Contains(current)) combo.Items.Add(current);
            if (current != "") combo.SelectedItem = current;
            var compound = key;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is string s) _pp._mappings[compound] = s;
            };
            _compoundRows.Children.Add(Row(compound, combo, null));
        }

        var options = new List<string> { "", "Disabled" };
        options.AddRange(labels);

        if (_pp._phasemappings == null) _pp.CreatePhaseMappings();
        foreach (var kvp in _pp._phasemappings.ToList())
        {
            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var o in options) combo.Items.Add(o);
            var current = kvp.Value.PhaseLabel ?? "";
            if (!options.Contains(current)) combo.Items.Add(current);
            combo.SelectedItem = current;
            var info = kvp.Value;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is string s) info.PhaseLabel = s;
            };
            _phaseRows.Children.Add(Row(kvp.Key, combo, null));
        }
    }

    /// <summary>Runs a CAPE-OPEN call and shows its error on the status line instead of throwing.</summary>
    private bool TryRun(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            _status.Text = "Error: " + ex.Message + (ex.InnerException != null && ex.InnerException.Message != ex.Message ? " " + ex.InnerException.Message : "");
            return false;
        }
    }
}
