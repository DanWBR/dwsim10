using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.Databases.ChemeoLink;
using ConstantProperties = DWSIM.Thermodynamics.BaseClasses.ConstantProperties;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Compound import from the Cheméo online database. Avalonia counterpart of the WinForms
/// FormImportCompoundOnline: search by name, pick one of the matches, then download and review
/// what the database holds for it before adding it to the simulation.
/// </summary>
public sealed class CompoundImportOnlineWindow : Window
{

    private readonly IFlowsheet _flowsheet;

    private readonly TextBox _search = new() { Watermark = "Compound name, formula or CAS number" };
    private readonly ListBox _matches = new() { Height = 150 };
    private readonly DataGrid _checklist = CompoundImportSupport.BuildChecklistGrid();
    private readonly ObservableCollection<CompoundImportSupport.ChecklistRow> _rows = new();
    private readonly TextBlock _status = new() { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    private Button _btnSearch = null!, _btnFetch = null!, _btnAdd = null!, _btnExport = null!;

    private List<string[]> _found = new();
    private ConstantProperties? _compound;

    public CompoundImportOnlineWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;

        Title = "Import Compound from Online Database";
        Width = 820;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        Content = BuildContent();
    }

    private Control BuildContent()
    {
        _checklist.ItemsSource = _rows;

        _btnSearch = new Button { Content = "Search", Width = 90, Margin = new Thickness(6, 0, 0, 0) };
        _btnSearch.Classes.Add("panel");
        _btnSearch.Click += async (_, _) => await SearchAsync();

        _search.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await SearchAsync(); };

        var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(_btnSearch, global::Avalonia.Controls.Dock.Right);
        searchRow.Children.Add(_btnSearch);
        searchRow.Children.Add(_search);

        _btnFetch = new Button { Content = "Get Compound Data", IsEnabled = false, Margin = new Thickness(0, 8, 0, 8) };
        _btnFetch.Classes.Add("panel");
        _btnFetch.Click += async (_, _) => await FetchAsync();

        _matches.SelectionChanged += (_, _) => _btnFetch.IsEnabled = _matches.SelectedIndex >= 0;
        _matches.DoubleTapped += async (_, _) => await FetchAsync();

        _btnAdd = new Button { Content = "Add to Simulation", IsEnabled = false, Margin = new Thickness(0, 0, 6, 0) };
        _btnAdd.Classes.Add("action");
        _btnAdd.Click += (_, _) =>
        {
            if (_compound != null) _status.Text = CompoundImportSupport.AddToFlowsheet(_flowsheet, _compound);
        };

        _btnExport = new Button { Content = "Export to JSON", IsEnabled = false, Margin = new Thickness(0, 0, 6, 0) };
        _btnExport.Classes.Add("panel");
        _btnExport.Click += async (_, _) =>
        {
            var message = await CompoundImportSupport.ExportJsonAsync(this, _compound!);
            if (!string.IsNullOrEmpty(message)) _status.Text = message;
        };

        var btnClose = new Button { Content = "Close", Width = 90, IsCancel = true };
        btnClose.Classes.Add("dialog");
        btnClose.Click += (_, _) => Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(_btnAdd);
        buttons.Children.Add(_btnExport);
        buttons.Children.Add(btnClose);

        var bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Right);
        bottom.Children.Add(buttons);
        bottom.Children.Add(_status);

        var link = new TextBlock
        {
            Text = "Data provided by Cheméo (https://www.chemeo.com/)",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Opacity = 0.75,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var body = new DockPanel();
        DockPanel.SetDock(link, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(searchRow, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_matches, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_btnFetch, global::Avalonia.Controls.Dock.Top);
        body.Children.Add(link);
        body.Children.Add(searchRow);
        body.Children.Add(_matches);
        body.Children.Add(_btnFetch);
        body.Children.Add(_checklist);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(body);
        return root;
    }

    private async Task SearchAsync()
    {
        var text = _search.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            _status.Text = "Enter the name of the compound to look for.";
            return;
        }

        _matches.Items.Clear();
        _rows.Clear();
        _compound = null;
        _btnFetch.IsEnabled = false;
        _btnAdd.IsEnabled = false;
        _btnExport.IsEnabled = false;
        _btnSearch.IsEnabled = false;
        _status.Text = "Searching Cheméo, please wait...";

        try
        {
            _found = await ChemeoParser.GetCompoundIDs(text, false);
        }
        catch (Exception ex)
        {
            _status.Text = "Error getting data from Cheméo: " + (ex.InnerException?.Message ?? ex.Message);
            _btnSearch.IsEnabled = true;
            return;
        }

        _btnSearch.IsEnabled = true;

        foreach (var item in _found) _matches.Items.Add(item[1]);

        _status.Text = _found.Count == 0
            ? "Could not find matching compounds for the given search text."
            : $"{_found.Count} compound(s) found. Select one and get its data.";
    }

    private async Task FetchAsync()
    {
        var index = _matches.SelectedIndex;
        if (index < 0 || index >= _found.Count) return;

        _rows.Clear();
        _compound = null;
        _btnAdd.IsEnabled = false;
        _btnExport.IsEnabled = false;
        _btnFetch.IsEnabled = false;
        _status.Text = "Downloading the compound data, please wait...";

        var id = _found[index][0];

        try
        {
            _compound = await Task.Run(() => ChemeoParser.GetCompoundData(id));
        }
        catch (Exception ex)
        {
            _status.Text = "Error getting data from Cheméo: " + (ex.InnerException?.Message ?? ex.Message);
            _btnFetch.IsEnabled = true;
            return;
        }

        _btnFetch.IsEnabled = true;

        if (_compound == null)
        {
            _status.Text = "Could not find data for this compound.";
            return;
        }

        foreach (var row in CompoundImportSupport.Checklist(_compound)) _rows.Add(row);

        _btnAdd.IsEnabled = true;
        _btnExport.IsEnabled = true;
        _status.Text = $"'{_compound.Name}' downloaded. Review the data and add it to the simulation.";
    }

}
