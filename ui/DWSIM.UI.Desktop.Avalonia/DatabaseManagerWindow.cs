using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.UI.Shared.Avalonia;
using UserDB = DWSIM.Thermodynamics.Databases.UserDB;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Manages the user compound databases: the list of database files the application knows about, and
/// the compounds inside each. Avalonia counterpart of the WinForms FormDBManager. Editing a compound
/// from its study file is left out here: those files are the legacy binary format the compound
/// creator wrote, which this edition does not read.
/// </summary>
public sealed class DatabaseManagerWindow : Window
{
    private sealed class CompoundRow
    {
        public string Name { get; init; } = "";
        public string CAS { get; init; } = "";
        public string Formula { get; init; } = "";
        public string MW { get; init; } = "";
        public string Id { get; init; } = "";
    }

    private readonly ObservableCollection<CompoundRow> _rows = new();
    private readonly ComboBox _dbSelector = new() { MinWidth = 260 };
    private readonly TextBlock _pathLabel = new() { Opacity = 0.8, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    private List<string> Databases => DWSIM.GlobalSettings.Settings.UserDatabases;

    private string? CurrentPath =>
        _dbSelector.SelectedIndex >= 0 && _dbSelector.SelectedIndex < Databases.Count
            ? Databases[_dbSelector.SelectedIndex]
            : null;

    public DatabaseManagerWindow()
    {
        Title = "User Database Manager";
        Width = 760;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        Content = BuildContent();
        RefreshDatabaseList();
    }

    private Control BuildContent()
    {
        _dbSelector.SelectionChanged += (_, _) => LoadCompounds();

        var btnAdd = MakeButton("Add Existing...", async () => await AddExistingAsync());
        var btnNew = MakeButton("New Database...", async () => await NewDatabaseAsync());
        var btnRemove = MakeButton("Remove from List", RemoveFromList);

        var dbRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        dbRow.Children.Add(new TextBlock { Text = "Database:", VerticalAlignment = VerticalAlignment.Center });
        dbRow.Children.Add(_dbSelector);
        dbRow.Children.Add(btnAdd);
        dbRow.Children.Add(btnNew);
        dbRow.Children.Add(btnRemove);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserSortColumns = true,
            IsReadOnly = true,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };
        void Col(string header, string path, double w, bool star = false)
            => grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new global::Avalonia.Data.Binding(path),
                Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(w)
            });
        Col("Name", "Name", 0, star: true);
        Col("CAS Number", "CAS", 120);
        Col("Formula", "Formula", 120);
        Col("MW", "MW", 80);
        Col("ID", "Id", 70);
        grid.ItemsSource = _rows;
        _grid = grid;

        var btnDelete = MakeButton("Delete Compound", DeleteCompound);
        var btnRefresh = MakeButton("Refresh", LoadCompounds);
        var compTools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new global::Avalonia.Thickness(0, 6, 0, 6) };
        compTools.Children.Add(btnDelete);
        compTools.Children.Add(btnRefresh);

        var btnClose = new Button { Content = "Close", IsCancel = true, Width = 90 };
        btnClose.Classes.Add("dialog");
        btnClose.Click += (_, _) => Close();

        var bottom = new DockPanel();
        DockPanel.SetDock(btnClose, global::Avalonia.Controls.Dock.Right);
        bottom.Children.Add(btnClose);
        bottom.Children.Add(_status);

        var root = new DockPanel { Margin = new global::Avalonia.Thickness(12) };
        DockPanel.SetDock(dbRow, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_pathLabel, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(compTools, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        _pathLabel.Margin = new global::Avalonia.Thickness(0, 6, 0, 0);
        bottom.Margin = new global::Avalonia.Thickness(0, 8, 0, 0);
        root.Children.Add(dbRow);
        root.Children.Add(_pathLabel);
        root.Children.Add(compTools);
        root.Children.Add(bottom);
        root.Children.Add(grid);
        return root;
    }

    private DataGrid _grid = null!;

    private static Button MakeButton(string caption, Action action)
    {
        var b = new Button { Content = caption };
        b.Classes.Add("panel");
        b.Click += (_, _) => action();
        return b;
    }

    private static Button MakeButton(string caption, Func<Task> action)
    {
        var b = new Button { Content = caption };
        b.Classes.Add("panel");
        b.Click += async (_, _) => await action();
        return b;
    }

    // -------------------------------------------------------------------------

    private void RefreshDatabaseList()
    {
        var previous = _dbSelector.SelectedIndex;

        _dbSelector.Items.Clear();
        var live = Databases.Where(File.Exists).ToList();

        // drop entries whose file is gone, so the index lines up with the settings list
        if (live.Count != Databases.Count)
        {
            Databases.Clear();
            Databases.AddRange(live);
        }

        var i = 0;
        foreach (var path in Databases)
        {
            i++;
            _dbSelector.Items.Add($"User {i}: {Path.GetFileName(path)}");
        }

        if (Databases.Count == 0)
        {
            _pathLabel.Text = "No user databases yet. Add an existing one or create a new database.";
            _rows.Clear();
            _status.Text = "";
            return;
        }

        _dbSelector.SelectedIndex = previous >= 0 && previous < Databases.Count ? previous : 0;
        LoadCompounds();
    }

    private void LoadCompounds()
    {
        _rows.Clear();
        var path = CurrentPath;
        if (path == null) { _pathLabel.Text = ""; return; }

        _pathLabel.Text = path;

        try
        {
            var comps = UserDB.ReadComps(path);
            foreach (var cp in comps.OrderBy(c => c.Name))
            {
                _rows.Add(new CompoundRow
                {
                    Name = cp.Name,
                    CAS = cp.CAS_Number,
                    Formula = cp.Formula,
                    MW = cp.Molar_Weight.ToString("N2"),
                    Id = cp.ID.ToString()
                });
            }
            _status.Text = $"{_rows.Count} compound(s) in this database.";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not read this database: " + ex.Message;
        }
    }

    private void DeleteCompound()
    {
        var path = CurrentPath;
        if (path == null || _grid.SelectedItem is not CompoundRow row) { _status.Text = "Select a compound first."; return; }

        try
        {
            UserDB.RemoveCompound(path, row.Id);
            LoadCompounds();
            _status.Text = $"'{row.Name}' removed.";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not remove the compound: " + ex.Message;
        }
    }

    // -------------------------------------------------------------------------

    private async Task AddExistingAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add User Database",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("DWSIM User Database") { Patterns = new[] { "*.xml" } } }
        });

        var path = files?.FirstOrDefault()?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        if (Databases.Any(d => string.Equals(d, path, StringComparison.OrdinalIgnoreCase)))
        {
            _status.Text = "That database is already on the list.";
            return;
        }

        Databases.Add(path);
        Persist();
        RefreshDatabaseList();
        _dbSelector.SelectedIndex = Databases.Count - 1;
        _status.Text = "Database added.";
    }

    private async Task NewDatabaseAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "New User Database",
            SuggestedFileName = "user_database.xml",
            DefaultExtension = "xml",
            FileTypeChoices = new[] { new FilePickerFileType("DWSIM User Database") { Patterns = new[] { "*.xml" } } }
        });

        var path = file?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            UserDB.CreateNew(path, "compounds");
            Databases.Add(path);
            Persist();
            RefreshDatabaseList();
            _dbSelector.SelectedIndex = Databases.Count - 1;
            _status.Text = "Empty database created.";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not create the database: " + ex.Message;
        }
    }

    private void RemoveFromList()
    {
        var idx = _dbSelector.SelectedIndex;
        if (idx < 0 || idx >= Databases.Count) return;

        Databases.RemoveAt(idx);
        Persist();
        RefreshDatabaseList();
        _status.Text = "Database removed from the list. The file on disk was left alone.";
    }

    private static void Persist()
    {
        try { DWSIM.GlobalSettings.Settings.SaveSettings("dwsim_newui.ini"); } catch { }
    }
}
