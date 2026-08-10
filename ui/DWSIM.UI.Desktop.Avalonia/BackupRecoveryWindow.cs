using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Offers back the backup copies the application saved while a simulation was open. It is shown at
/// startup when the previous run did not close cleanly, the same role the WinForms FormRecoverFiles
/// filled: the user ticks the ones to reopen, or clears them all.
/// </summary>
public sealed class BackupRecoveryWindow : Window
{
    private sealed class BackupRow
    {
        public bool Recover { get; set; } = true;
        public string FileName { get; init; } = "";
        public string Date { get; init; } = "";
        public string Size { get; init; } = "";
        public string Path { get; init; } = "";
    }

    private readonly ObservableCollection<BackupRow> _rows = new();
    private readonly Action<string> _onRecover;

    /// <summary>
    /// Where the backup copies are written, matching what the flowsheet's backup timer uses: the
    /// folder from the settings, or a default under the user's documents.
    /// </summary>
    public static string ResolveBackupFolder()
    {
        var dir = DWSIM.GlobalSettings.Settings.BackupFolder;
        if (string.IsNullOrEmpty(dir))
        {
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DWSIM Application Data", "Backup");
        }
        return dir;
    }

    /// <summary>The backup copies present on disk, newest first.</summary>
    public static string[] FindBackups()
    {
        try
        {
            var dir = ResolveBackupFolder();
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.EnumerateFiles(dir, "backup_*.dwxmz")
                .OrderByDescending(File.GetLastWriteTime)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public BackupRecoveryWindow(Action<string> onRecover)
    {
        _onRecover = onRecover;

        Title = "Recover Backup Copies";
        Width = 640;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        Content = BuildContent();
        Populate();
    }

    private Control BuildContent()
    {
        var header = new TextBlock
        {
            Text = "The previous session did not close normally. These backup copies were found; " +
                   "tick the ones to reopen.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new global::Avalonia.Thickness(0, 0, 0, 8)
        };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            IsReadOnly = false,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };
        grid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = "Recover",
            Binding = new global::Avalonia.Data.Binding("Recover") { Mode = global::Avalonia.Data.BindingMode.TwoWay },
            Width = new DataGridLength(90)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "File",
            Binding = new global::Avalonia.Data.Binding("FileName"),
            IsReadOnly = true,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Saved",
            Binding = new global::Avalonia.Data.Binding("Date"),
            IsReadOnly = true,
            Width = new DataGridLength(150)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Size",
            Binding = new global::Avalonia.Data.Binding("Size"),
            IsReadOnly = true,
            Width = new DataGridLength(80)
        });
        grid.ItemsSource = _rows;

        var btnRecover = new Button { Content = "Recover Selected", IsDefault = true };
        btnRecover.Classes.Add("action");
        btnRecover.Click += (_, _) => RecoverSelected();

        var btnDelete = new Button { Content = "Delete All Backups" };
        btnDelete.Classes.Add("panel");
        btnDelete.Click += (_, _) => DeleteAll();

        var btnClose = new Button { Content = "Close", IsCancel = true, Width = 90 };
        btnClose.Classes.Add("dialog");
        btnClose.Click += (_, _) => Close();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        buttons.Children.Add(btnDelete);
        buttons.Children.Add(btnRecover);
        buttons.Children.Add(btnClose);

        var root = new DockPanel { Margin = new global::Avalonia.Thickness(12) };
        DockPanel.SetDock(header, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        buttons.Margin = new global::Avalonia.Thickness(0, 8, 0, 0);
        root.Children.Add(header);
        root.Children.Add(buttons);
        root.Children.Add(grid);
        return root;
    }

    private void Populate()
    {
        _rows.Clear();
        foreach (var path in FindBackups())
        {
            var info = new FileInfo(path);
            _rows.Add(new BackupRow
            {
                Recover = true,
                FileName = info.Name,
                Date = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Size = $"{info.Length / 1024.0:N0} KB",
                Path = path
            });
        }
    }

    private void RecoverSelected()
    {
        var picked = _rows.Where(r => r.Recover).Select(r => r.Path).ToList();
        Close();
        foreach (var path in picked)
        {
            try { _onRecover(path); } catch { }
        }
    }

    private void DeleteAll()
    {
        foreach (var r in _rows.ToList())
        {
            try { File.Delete(r.Path); } catch { }
        }
        _rows.Clear();
        Close();
    }
}
