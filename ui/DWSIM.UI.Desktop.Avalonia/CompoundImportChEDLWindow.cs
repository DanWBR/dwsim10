using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.Databases.ChEDLThermoLink;
using ConstantProperties = DWSIM.Thermodynamics.BaseClasses.ConstantProperties;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Compound import from the ChEDL Thermo/Chemicals Python libraries. Avalonia counterpart of
/// the WinForms FormImportCompoundFromThermo: the search resolves the compound identity, the
/// second step downloads its data. Needs a working Python environment, which the parser sets up.
/// </summary>
public sealed class CompoundImportChEDLWindow : Window
{

    private readonly IFlowsheet _flowsheet;
    private readonly ChEDLThermoParser _parser = new();

    private readonly TextBox _search = new() { Watermark = "Compound name, formula or CAS number" };
    private readonly TextBox _match = new() { IsReadOnly = true };
    private readonly TextBox _importAs = new();
    private readonly DataGrid _checklist = CompoundImportSupport.BuildChecklistGrid();
    private readonly ObservableCollection<CompoundImportSupport.ChecklistRow> _rows = new();
    private readonly TextBlock _status = new() { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    private Button _btnSearch = null!, _btnFetch = null!, _btnAdd = null!, _btnExport = null!;
    private ConstantProperties? _compound;

    public CompoundImportChEDLWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;

        Title = "Import Compound from ChEDL Thermo/Chemicals";
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

        var matchRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var matchLabel = new TextBlock { Text = "Query match", VerticalAlignment = VerticalAlignment.Center };
        var asLabel = new TextBlock
        {
            Text = "Import as",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 8, 0)
        };
        Grid.SetColumn(matchLabel, 0);
        Grid.SetColumn(_match, 1);
        Grid.SetColumn(asLabel, 2);
        Grid.SetColumn(_importAs, 3);
        _match.Margin = new Thickness(8, 0, 0, 0);
        matchRow.Children.Add(matchLabel);
        matchRow.Children.Add(_match);
        matchRow.Children.Add(asLabel);
        matchRow.Children.Add(_importAs);

        _btnAdd = new Button { Content = "Add to Simulation", IsEnabled = false, Margin = new Thickness(0, 0, 6, 0) };
        _btnAdd.Classes.Add("action");
        _btnAdd.Click += (_, _) => AddToSimulation();

        _btnExport = new Button { Content = "Export to JSON", IsEnabled = false, Margin = new Thickness(0, 0, 6, 0) };
        _btnExport.Classes.Add("panel");
        _btnExport.Click += async (_, _) =>
        {
            if (_compound == null) return;
            _compound.Name = NameToImport();
            var message = await CompoundImportSupport.ExportJsonAsync(this, _compound);
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
            Text = "Data from the ChEDL Thermo Python library (https://github.com/CalebBell/thermo). " +
                   "The first search initializes the Python environment and may take a while.",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var body = new DockPanel();
        DockPanel.SetDock(link, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(searchRow, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(matchRow, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_btnFetch, global::Avalonia.Controls.Dock.Top);
        body.Children.Add(link);
        body.Children.Add(searchRow);
        body.Children.Add(matchRow);
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

        _match.Text = "";
        _importAs.Text = "";
        _rows.Clear();
        _compound = null;
        _btnFetch.IsEnabled = false;
        _btnAdd.IsEnabled = false;
        _btnExport.IsEnabled = false;
        _btnSearch.IsEnabled = false;
        _status.Text = "Looking the compound up, please wait...";

        try
        {
            var result = await Task.Run(() => _parser.SearchCompound(text));
            _match.Text = result.Count > 0 ? result[0] : "";
        }
        catch (Exception ex)
        {
            _status.Text = ex.InnerException?.Message ?? ex.Message;
            _btnSearch.IsEnabled = true;
            return;
        }

        _btnSearch.IsEnabled = true;

        if (string.IsNullOrEmpty(_match.Text))
        {
            _status.Text = "No compound matched the search text.";
            return;
        }

        _btnFetch.IsEnabled = true;
        _status.Text = "Compound found. Get its data to review what the library holds for it.";
    }

    private async Task FetchAsync()
    {
        var name = _match.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        _rows.Clear();
        _compound = null;
        _btnAdd.IsEnabled = false;
        _btnExport.IsEnabled = false;
        _btnFetch.IsEnabled = false;
        _status.Text = "Downloading the compound data, please wait...";

        try
        {
            _compound = await Task.Run(() => _parser.GetCompoundData(name));
        }
        catch (Exception ex)
        {
            _status.Text = ex.InnerException?.Message ?? ex.Message;
            _btnFetch.IsEnabled = true;
            return;
        }

        _btnFetch.IsEnabled = true;

        if (_compound == null)
        {
            _status.Text = "The library has no data for this compound.";
            return;
        }

        _importAs.Text = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ToTitleCase(_compound.Name ?? "");

        foreach (var row in CompoundImportSupport.Checklist(_compound)) _rows.Add(row);

        _btnAdd.IsEnabled = true;
        _btnExport.IsEnabled = true;
        _status.Text = $"'{_compound.Name}' downloaded. Review the data and add it to the simulation.";
    }

    private string NameToImport()
    {
        var name = _importAs.Text?.Trim();
        return string.IsNullOrEmpty(name) ? _compound?.Name ?? "" : name;
    }

    private void AddToSimulation()
    {
        if (_compound == null) return;
        _compound.Name = NameToImport();
        _status.Text = CompoundImportSupport.AddToFlowsheet(_flowsheet, _compound);
    }

}
