using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.UI.Desktop.Editors;
using BuiltConstantProperties = DWSIM.PureCompoundData.Builder.BuiltConstantProperties;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Compound import from the local pure-compound database. Avalonia counterpart of the WinForms
/// FormImportCompoundPure; both drive <see cref="PureCompoundImporter"/>, so a compound imported
/// here and there carries the same values and the same provenance comments.
/// </summary>
public sealed class CompoundImportThermoDataWindow : Window
{

    private readonly IFlowsheet _flowsheet;

    private readonly ObservableCollection<PureCompoundImporter.Candidate> _candidates = new();
    private readonly ObservableCollection<PureCompoundImporter.PropertyRow> _properties = new();

    private readonly TextBox _search = new() { Watermark = "CAS number, compound name or InChIKey" };
    private readonly DataGrid _candidateGrid = new()
    {
        AutoGenerateColumns = false,
        CanUserSortColumns = false,
        IsReadOnly = true,
        SelectionMode = DataGridSelectionMode.Single,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        Height = 160
    };
    private readonly DataGrid _propertyGrid = new()
    {
        AutoGenerateColumns = false,
        CanUserSortColumns = false,
        IsReadOnly = true,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
    };
    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    private Button _btnSearch = null!, _btnAdd = null!, _btnExport = null!;
    private BuiltConstantProperties? _built;

    public CompoundImportThermoDataWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;

        Title = "Import Compound from ThermoData";
        Width = 900;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        Content = BuildContent();

        Opened += async (_, _) => await EnsureDatabaseAsync();
    }

    private Control BuildContent()
    {
        _candidateGrid.ItemsSource = _candidates;
        _candidateGrid.Columns.Add(Col("Identifier", nameof(PureCompoundImporter.Candidate.Identifier), 1.2));
        _candidateGrid.Columns.Add(Col("Name", nameof(PureCompoundImporter.Candidate.Name), 2.0));
        _candidateGrid.Columns.Add(Col("Records", nameof(PureCompoundImporter.Candidate.RecordCount), 0.6));
        _candidateGrid.SelectionChanged += (_, _) => LoadProperties();

        _propertyGrid.ItemsSource = _properties;
        _propertyGrid.Columns.Add(Col("Property", nameof(PureCompoundImporter.PropertyRow.Property), 1.4));
        _propertyGrid.Columns.Add(Col("Origin", nameof(PureCompoundImporter.PropertyRow.Origin), 1.6));
        _propertyGrid.Columns.Add(Col("Value", nameof(PureCompoundImporter.PropertyRow.Value), 1.4));
        _propertyGrid.Columns.Add(Col("Units", nameof(PureCompoundImporter.PropertyRow.Units), 0.6));

        _btnSearch = new Button { Content = "Search", Width = 90, Margin = new Thickness(6, 0, 0, 0) };
        _btnSearch.Classes.Add("panel");
        _btnSearch.Click += (_, _) => Search();

        _search.KeyDown += (_, e) => { if (e.Key == Key.Enter) Search(); };

        var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(_btnSearch, global::Avalonia.Controls.Dock.Right);
        searchRow.Children.Add(_btnSearch);
        searchRow.Children.Add(_search);

        _btnAdd = new Button { Content = "Add to Simulation", IsEnabled = false, Margin = new Thickness(0, 0, 6, 0) };
        _btnAdd.Classes.Add("action");
        _btnAdd.Click += (_, _) => AddToSimulation();

        _btnExport = new Button { Content = "Export to JSON", IsEnabled = false, Margin = new Thickness(0, 0, 6, 0) };
        _btnExport.Classes.Add("panel");
        _btnExport.Click += async (_, _) => await ExportAsync();

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

        var body = new DockPanel();
        DockPanel.SetDock(searchRow, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_candidateGrid, global::Avalonia.Controls.Dock.Top);
        body.Children.Add(searchRow);
        body.Children.Add(_candidateGrid);
        body.Children.Add(new Border
        {
            Margin = new Thickness(0, 8, 0, 0),
            Child = _propertyGrid
        });

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(body);
        return root;
    }

    private static DataGridTextColumn Col(string header, string path, double width) => new()
    {
        Header = header,
        Binding = new global::Avalonia.Data.Binding(path),
        Width = new DataGridLength(width, DataGridLengthUnitType.Star)
    };

    // -------------------------------------------------------------------------
    // Database
    // -------------------------------------------------------------------------

    /// <summary>Offers to fetch the pre-built database the first time it is needed.</summary>
    private async Task EnsureDatabaseAsync()
    {
        if (PureCompoundImporter.DatabaseInstalled)
        {
            _status.Text = "Database: " + PureCompoundImporter.DatabasePath;
            return;
        }

        var download = await ConfirmAsync(
            "Download pure-compound database",
            "The local pure-compound database is not installed." + Environment.NewLine + Environment.NewLine +
            "Download the pre-built database now?" + Environment.NewLine + Environment.NewLine +
            "Destination: " + PureCompoundImporter.DatabasePath);

        if (!download)
        {
            _search.IsEnabled = false;
            _btnSearch.IsEnabled = false;
            _status.Text = "The local database is not installed, so there is nothing to search.";
            return;
        }

        _search.IsEnabled = false;
        _btnSearch.IsEnabled = false;
        _status.Text = "Downloading the database, please wait...";

        // the callback comes off a worker thread
        void Report(long bytes, long total) => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _status.Text = total > 0
                ? $"Downloading the database: {bytes / 1048576.0:N1} of {total / 1048576.0:N1} MB"
                : $"Downloading the database: {bytes / 1048576.0:N1} MB";
        });

        try
        {
            await PureCompoundImporter.DownloadDatabaseAsync(Report, CancellationToken.None);
            _search.IsEnabled = true;
            _btnSearch.IsEnabled = true;
            _status.Text = "Database installed at " + PureCompoundImporter.DatabasePath;
        }
        catch (Exception ex)
        {
            _status.Text = "Download failed: " + ex.Message;
        }
    }

    // -------------------------------------------------------------------------
    // Search and build
    // -------------------------------------------------------------------------

    private async void Search()
    {
        var text = _search.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            _status.Text = "Enter a CAS number, compound name or InChIKey.";
            return;
        }

        _candidates.Clear();
        _properties.Clear();
        _built = null;
        _btnAdd.IsEnabled = false;
        _btnExport.IsEnabled = false;
        _btnSearch.IsEnabled = false;
        _status.Text = "Searching the local index...";

        List<PureCompoundImporter.Candidate> found;
        try
        {
            found = await Task.Run(() => PureCompoundImporter.Search(text));
        }
        catch (Exception ex)
        {
            _status.Text = "Error searching the local index: " + ex.Message;
            _btnSearch.IsEnabled = true;
            return;
        }

        foreach (var candidate in found) _candidates.Add(candidate);

        _btnSearch.IsEnabled = true;

        if (_candidates.Count == 0)
        {
            _status.Text = $"No compounds matched '{text}'.";
            return;
        }

        _status.Text = $"{_candidates.Count} compound(s) found.";
        _candidateGrid.SelectedIndex = 0;
    }

    private async void LoadProperties()
    {
        _properties.Clear();
        _built = null;
        _btnAdd.IsEnabled = false;
        _btnExport.IsEnabled = false;

        if (_candidateGrid.SelectedItem is not PureCompoundImporter.Candidate candidate) return;

        _status.Text = "Collecting the records and estimating the missing properties...";

        BuiltConstantProperties? built;
        try
        {
            built = await Task.Run(() => PureCompoundImporter.Build(candidate));
        }
        catch (Exception ex)
        {
            _status.Text = "Error loading properties: " + ex.Message;
            return;
        }

        if (built == null)
        {
            _status.Text = "The index has no records for this compound.";
            return;
        }

        _built = built;
        foreach (var row in PureCompoundImporter.Describe(built)) _properties.Add(row);

        _btnAdd.IsEnabled = true;
        _btnExport.IsEnabled = true;
        _status.Text = $"{candidate.Name}: {candidate.RecordCount} record(s) in the index.";
    }

    // -------------------------------------------------------------------------
    // Import
    // -------------------------------------------------------------------------

    private void AddToSimulation()
    {
        if (_built == null) return;
        _status.Text = CompoundImportSupport.AddToFlowsheet(_flowsheet,
            PureCompoundImporter.ToConstantProperties(_built));
    }

    private async Task ExportAsync()
    {
        if (_built == null) return;
        var message = await CompoundImportSupport.ExportJsonAsync(this,
            PureCompoundImporter.ToConstantProperties(_built));
        if (!string.IsNullOrEmpty(message)) _status.Text = message;
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var result = false;

        var yes = new Button { Content = "Yes", Width = 90, IsDefault = true };
        yes.Classes.Add("dialog");
        var no = new Button { Content = "No", Width = 90, IsCancel = true, Margin = new Thickness(6, 0, 0, 0) };
        no.Classes.Add("dialog");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        buttons.Children.Add(yes);
        buttons.Children.Add(no);

        var root = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var dialog = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = root
        };
        IconHelper.ApplyWindowIcon(dialog);

        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return result;
    }

}
