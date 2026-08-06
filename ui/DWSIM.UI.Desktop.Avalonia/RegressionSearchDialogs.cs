using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DWSIM.PhaseEquilibriumData.Core;
using DWSIM.PhaseEquilibriumData.Index;
using DWSIM.SharedClasses.DataRegression.Reporting;
using DWSIM.Thermodynamics.Databases.KDBLink;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Shared chrome for the two dataset pickers: a status line, a grid, the swap/replace
/// options and the Load/Cancel buttons.
/// </summary>
internal abstract class RegressionSearchDialogBase : Window
{
    protected readonly DataGrid Grid = new() { CanUserSortColumns = false, IsReadOnly = true };
    protected readonly TextBlock StatusLabel = new() { TextWrapping = TextWrapping.Wrap };
    protected readonly CheckBox ChkSwap = new() { Content = "Swap compound order (1 <-> 2)" };
    protected readonly CheckBox ChkReplace = new() { Content = "Replace existing data", IsChecked = true };
    protected readonly Button BtnSelect = new() { Content = "Load Selected", Width = 130, IsEnabled = false };
    protected readonly Button BtnCancel = new() { Content = "Cancel", Width = 90, IsCancel = true };

    protected CancellationTokenSource? Cts;

    /// <summary>The points the user accepted, or null when cancelled.</summary>
    public List<RegressionDataPoint>? SelectedPoints { get; protected set; }

    public bool ReplaceExisting => ChkReplace.IsChecked.GetValueOrDefault();

    protected RegressionSearchDialogBase(string title, int width, int height)
    {
        Title = title;
        Width = width;
        Height = height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        BtnSelect.Classes.Add("dialog");
        BtnCancel.Classes.Add("dialog");

        BtnCancel.Click += (_, _) => Close();
        Grid.SelectionChanged += (_, _) => BtnSelect.IsEnabled = Grid.SelectedIndex >= 0;
        Closed += (_, _) => Cts?.Cancel();
    }

    protected void BuildChrome()
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8)
        };
        buttons.Children.Add(ChkSwap);
        buttons.Children.Add(ChkReplace);
        buttons.Children.Add(BtnSelect);
        buttons.Children.Add(BtnCancel);

        var top = new Border { Child = StatusLabel, Padding = new Thickness(8) };

        var root = new DockPanel();
        DockPanel.SetDock(top, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(buttons);
        root.Children.Add(new Border { Child = Grid, Padding = new Thickness(8, 0) });
        Content = root;
    }

    protected static DataGridTextColumn Col(string header, string path, double width) =>
        new()
        {
            Header = header,
            Binding = new global::Avalonia.Data.Binding(path),
            Width = new DataGridLength(width)
        };
}

/// <summary>
/// Searches the KDB online VLE database for binary datasets matching two compound names.
/// </summary>
internal sealed class KdbSearchDialog : RegressionSearchDialogBase
{
    private readonly string _comp1, _comp2;
    private readonly ObservableCollection<KdbSetRow> _rows = new();
    private KDBVLESearchResult? _searchResult;

    public string? SelectedTUnit { get; private set; }
    public string? SelectedPUnit { get; private set; }

    public KdbSearchDialog(string comp1, string comp2) : base("KDB Binary VLE Search", 900, 520)
    {
        _comp1 = comp1;
        _comp2 = comp2;

        Grid.AutoGenerateColumns = false;
        Grid.Columns.Add(Col("Set ID", "SetID", 70));
        Grid.Columns.Add(Col("Pts", "NumberOfDataPoints", 55));
        Grid.Columns.Add(Col("T min", "TMin", 75));
        Grid.Columns.Add(Col("T max", "TMax", 75));
        Grid.Columns.Add(Col("P min", "PMin", 75));
        Grid.Columns.Add(Col("P max", "PMax", 75));
        Grid.Columns.Add(Col("Title", "Title", 260));
        Grid.Columns.Add(Col("Reference", "Reference", 200));
        Grid.ItemsSource = _rows;

        StatusLabel.Text = $"Searching KDB for {_comp1} + {_comp2}...";
        BuildChrome();

        BtnSelect.Click += async (_, _) => await LoadSelectedAsync();
        Opened += async (_, _) => await SearchAsync();
    }

    private async Task SearchAsync()
    {
        Cts = new CancellationTokenSource();
        try
        {
            _searchResult = await Task.Run(() => KDBParser.GetBinaryVLESetIDs(_comp1, _comp2), Cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            StatusLabel.Text = "Search failed: " + (ex.InnerException?.Message ?? ex.Message);
            return;
        }

        foreach (var s in _searchResult.Sets) _rows.Add(new KdbSetRow(s));
        StatusLabel.Text = _rows.Count == 0
            ? "No datasets found for this compound pair."
            : $"{_rows.Count} dataset(s) found. Select one and click Load Selected.";
        if (_rows.Count > 0) Grid.SelectedIndex = 0;
    }

    private async Task LoadSelectedAsync()
    {
        var idx = Grid.SelectedIndex;
        if (idx < 0 || _searchResult == null) return;

        var info = _searchResult.Sets[idx];
        StatusLabel.Text = "Fetching dataset " + info.SetID + "...";
        BtnSelect.IsEnabled = false;

        try
        {
            var ds = await Task.Run(() => KDBParser.GetVLEData(info.SetID));
            SelectedPoints = KdbDatasetLoader.ToRegressionPoints(ds, ChkSwap.IsChecked.GetValueOrDefault());
            SelectedTUnit = ds.Tunits;
            SelectedPUnit = ds.Punits;
            Close();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Fetch failed: " + (ex.InnerException?.Message ?? ex.Message);
            BtnSelect.IsEnabled = true;
        }
    }

    private sealed class KdbSetRow
    {
        public int SetID { get; }
        public int NumberOfDataPoints { get; }
        public double TMin { get; }
        public double TMax { get; }
        public double PMin { get; }
        public double PMax { get; }
        public string Title { get; }
        public string Reference { get; }

        public KdbSetRow(KDBVLESetInfo s)
        {
            SetID = s.SetID;
            NumberOfDataPoints = s.NumberOfDataPoints;
            TMin = s.TMin; TMax = s.TMax;
            PMin = s.PMin; PMax = s.PMax;
            Title = s.Title;
            Reference = s.Reference;
        }
    }
}

/// <summary>
/// Searches the local ThermoML phase-equilibrium database for binary datasets.
/// </summary>
internal sealed class PhaseEqSearchDialog : RegressionSearchDialogBase
{
    private readonly string _comp1, _comp2;
    private readonly EquilibriumType? _typeFilter;
    private readonly string _tUnit, _pUnit;
    private readonly ObservableCollection<DatasetRow> _rows = new();
    private IReadOnlyList<PhaseEquilibriumDataset>? _results;

    public PhaseEqSearchDialog(string comp1, string comp2, EquilibriumType? typeFilter,
        string tUnit, string pUnit) : base("Phase-Equilibrium Database Search", 980, 520)
    {
        _comp1 = comp1;
        _comp2 = comp2;
        _typeFilter = typeFilter;
        _tUnit = tUnit;
        _pUnit = pUnit;

        Grid.AutoGenerateColumns = false;
        Grid.Columns.Add(Col("ID", "Id", 80));
        Grid.Columns.Add(Col("Type", "Type", 120));
        Grid.Columns.Add(Col("Pts", "PointCount", 55));
        Grid.Columns.Add(Col("Constraints", "ConstraintsText", 190));
        Grid.Columns.Add(Col("Compounds", "CompoundsText", 230));
        Grid.Columns.Add(Col("Source", "Source", 180));
        Grid.ItemsSource = _rows;

        StatusLabel.Text = $"Searching the local database for {_comp1} + {_comp2}...";
        BuildChrome();

        BtnSelect.Click += (_, _) => LoadSelected();
        Opened += async (_, _) => await SearchAsync();
    }

    private async Task SearchAsync()
    {
        if (!PhaseEqBundle.IsInstalled())
        {
            StatusLabel.Text = "The local phase-equilibrium database is not installed at " +
                               PhaseEqBundle.DefaultDbPath() + ".";
            return;
        }

        Cts = new CancellationTokenSource();
        try
        {
            _results = await Task.Run<IReadOnlyList<PhaseEquilibriumDataset>>(() =>
            {
                using var index = new ThermoMLIndex(PhaseEqBundle.DefaultDbPath());
                return index.Query.SearchBinaryByNames(_comp1, _comp2, _typeFilter, null, null, 500);
            }, Cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            StatusLabel.Text = "Search failed: " + (ex.InnerException?.Message ?? ex.Message);
            return;
        }

        foreach (var ds in _results) _rows.Add(new DatasetRow(ds));
        StatusLabel.Text = _rows.Count == 0
            ? "No datasets found for these compounds."
            : $"{_rows.Count} dataset(s) found.";
        if (_rows.Count > 0) Grid.SelectedIndex = 0;
    }

    private void LoadSelected()
    {
        var idx = Grid.SelectedIndex;
        if (idx < 0 || _results == null) return;
        SelectedPoints = PhaseEqDatasetLoader.ToRegressionPoints(
            _results[idx], _tUnit, _pUnit, ChkSwap.IsChecked.GetValueOrDefault());
        Close();
    }

    private sealed class DatasetRow
    {
        public string Id { get; }
        public string Type { get; }
        public int PointCount { get; }
        public string ConstraintsText { get; }
        public string CompoundsText { get; }
        public string Source { get; }

        public DatasetRow(PhaseEquilibriumDataset ds)
        {
            Id = ds.Id;
            Type = ds.EquilibriumType.ToString();
            PointCount = ds.Points.Count;
            ConstraintsText = string.Join(", ",
                ds.Constraints.Select(c => $"{c.Kind}={c.Value:G4} {c.Unit}".Trim()));
            CompoundsText = string.Join(" + ", ds.Compounds.Select(c => c.CommonName));
            Source = ds.SourceProvider ?? "";
        }
    }
}

/// <summary>
/// Downloads and installs the phase-equilibrium LiteDB bundle, reporting progress.
/// </summary>
internal sealed class PhaseEqDownloadDialog : Window
{
    public bool Succeeded { get; private set; }

    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Height = 18 };
    private readonly TextBlock _statusLabel = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _bytesLabel = new() { Text = "-", FontSize = 11 };
    private readonly Button _btnCancel = new() { Content = "Cancel", Width = 90 };

    private CancellationTokenSource? _cts;
    private bool _finishing;

    public PhaseEqDownloadDialog()
    {
        Title = "Downloading Phase-Equilibrium Database";
        Width = 560;
        Height = 230;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        _btnCancel.Classes.Add("dialog");

        _statusLabel.Text = "Downloading the bundle from " + PhaseEqBundle.DefaultBundleUrl + "...";

        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(14) };
        panel.Children.Add(_statusLabel);
        panel.Children.Add(_progress);
        panel.Children.Add(_bytesLabel);
        panel.Children.Add(new TextBlock
        {
            Text = "Destination: " + PhaseEqBundle.DefaultDbPath(),
            FontSize = 11, Opacity = 0.8, TextWrapping = TextWrapping.Wrap
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(14, 0, 14, 14)
        };
        buttons.Children.Add(_btnCancel);

        var root = new DockPanel();
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(panel);
        Content = root;

        _btnCancel.Click += (_, _) =>
        {
            _btnCancel.IsEnabled = false;
            _statusLabel.Text = "Cancelling...";
            _cts?.Cancel();
        };
        Closed += (_, _) => _cts?.Cancel();
        Opened += async (_, _) => await DownloadAsync();
    }

    private async Task DownloadAsync()
    {
        _cts = new CancellationTokenSource();
        // Progress<T> captures the current SynchronizationContext, so the callbacks already
        // come back on the UI thread.
        var progress = new Progress<(long Bytes, long Total)>(ReportProgress);

        try
        {
            await PhaseEqBundle.DownloadAndInstallAsync(null, null, progress, _cts.Token);
            if (_finishing) return;
            _finishing = true;
            Succeeded = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Download failed: " + (ex.InnerException?.Message ?? ex.Message);
            _progress.IsIndeterminate = false;
            _progress.Value = 0;
            _btnCancel.Content = "Close";
            _btnCancel.IsEnabled = true;
        }
    }

    private void ReportProgress((long Bytes, long Total) report)
    {
        if (report.Total > 0)
        {
            _progress.IsIndeterminate = false;
            _progress.Maximum = 1000;
            _progress.Value = 1000.0 * report.Bytes / report.Total;
            _bytesLabel.Text = $"{FormatBytes(report.Bytes)} / {FormatBytes(report.Total)} " +
                               $"({100.0 * report.Bytes / report.Total:F1} %)";
        }
        else
        {
            _progress.IsIndeterminate = true;
            _bytesLabel.Text = $"{FormatBytes(report.Bytes)} downloaded (total size unknown)";
        }
    }

    private static string FormatBytes(long n)
    {
        if (n < 1024) return n + " B";
        if (n < 1024 * 1024) return (n / 1024.0).ToString("F1") + " KB";
        if (n < 1024L * 1024 * 1024) return (n / (1024.0 * 1024.0)).ToString("F2") + " MB";
        return (n / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
    }
}
