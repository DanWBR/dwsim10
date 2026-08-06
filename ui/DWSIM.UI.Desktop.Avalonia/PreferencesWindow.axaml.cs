using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using DWSIM.UI.Shared.Avalonia;
using S = DWSIM.GlobalSettings.Settings;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// The general settings of the application, grouped as the Windows settings form groups them:
/// the solver, the flowsheet, the compound datasets, the backups, Python and the rest, plus what
/// this interface has of its own.
/// </summary>
public partial class PreferencesWindow : Window
{

    private static readonly string[] Cultures = { "en", "pt-BR", "de", "es", "ru", "zh-CN", "fr" };

    private static readonly string[] FontSizes =
        { "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16" };

    /// <summary>
    /// The systems of units a new simulation can start in. They are the ones the engine adds to
    /// every flowsheet, plus whatever the user has defined.
    /// </summary>
    private static readonly string[] UnitSystems =
        { "SI", "SI (Engineering)", "CGS", "ENG", "C1", "C2", "C3", "C4", "C5" };

    // solver
    private NumericUpDown _timeout = null!;
    private CheckBox _parallel = null!;
    private NumericUpDown _threads = null!;
    private CheckBox _simd = null!;
    private CheckBox _breakOnException = null!;
    private CheckBox _inspector = null!;
    private CheckBox _clearInspector = null!;

    // flowsheet
    private ComboBox _unitSystem = null!;
    private CheckBox _antiAlias = null!;
    private CheckBox _editOnSelect = null!;
    private CheckBox _solverOnEdit = null!;
    private CheckBox _fixedInputSize = null!;
    private ComboBox _editorFontSize = null!;
    private ComboBox _reportFontSize = null!;
    private CheckBox _undoRedoRecalc = null!;

    // compounds
    private ListBox _datasets = null!;
    private ListBox _interactions = null!;
    private CheckBox _replaceComps = null!;

    // backups
    private TextBox _configDir = null!;
    private CheckBox _enableBackup = null!;
    private NumericUpDown _backupInterval = null!;
    private TextBox _backupFolder = null!;

    // python
    private TextBox _pythonPath = null!;

    // other
    private CheckBox _checkUpdates = null!;
    private CheckBox _hideSolidPhaseCO = null!;
    private CheckBox _loadExtensions = null!;
    private NumericUpDown _debugLevel = null!;

    // interface
    private NumericUpDown _scaling = null!;
    private ComboBox _theme = null!;
    private ComboBox _culture = null!;

    public PreferencesWindow()
    {
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);

        Tabs.Items.Add(Tab("Solver", BuildSolver()));
        Tabs.Items.Add(Tab("Flowsheet", BuildFlowsheet()));
        Tabs.Items.Add(Tab("User Compounds", BuildCompounds()));
        Tabs.Items.Add(Tab("Backups", BuildBackups()));
        Tabs.Items.Add(Tab("Python", BuildPython()));
        Tabs.Items.Add(Tab("Other", BuildOther()));
        Tabs.Items.Add(Tab("Interface", BuildInterface()));

        BtnOK.Click += OnOK;
        BtnCancel.Click += (_, _) => Close();
    }

    private static TabItem Tab(string header, Control content)
    {
        return new TabItem
        {
            Header = header,
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = content
            }
        };
    }

    private static AvaloniaEditorPanel Page()
    {
        return new AvaloniaEditorPanel { Margin = new Thickness(14, 10) };
    }

    // -------------------------------------------------------------------------
    // Solver
    // -------------------------------------------------------------------------

    private Control BuildSolver()
    {
        var page = Page();

        page.CreateAndAddLabelRow("Solution Inspector");

        _inspector = page.CreateAndAddCheckBoxRow("Enable Inspector Reports", S.InspectorEnabled, null);
        page.CreateAndAddDescriptionRow(
            "Enabling Inspector Reports will create model description and performance reports " +
            "on-the-fly as the calculations are requested by the flowsheet solver. Use the " +
            "Solution Inspector tool to view these reports.");

        _clearInspector = page.CreateAndAddCheckBoxRow(
            "Clear Previous Reports on new Flowsheet Calculation Request",
            S.ClearInspectorHistoryOnNewCalculationRequest, null);
        page.CreateAndAddDescriptionRow(
            "This will erase all previously stored reports when a new flowsheet calculation " +
            "request is made.");

        page.CreateAndAddLabelRow("Solver Options");

        _timeout = page.CreateAndAddNumericEditorRow("Solver Timeout (seconds)",
            S.SolverTimeoutSeconds, 10, 86400, 0, null);
        page.CreateAndAddDescriptionRow("The solver's maximum calculation (waiting) time.");

        _parallel = page.CreateAndAddCheckBoxRow("Enable CPU Parallel Processing",
            S.EnableParallelProcessing, null);
        page.CreateAndAddDescriptionRow(
            "Enables utilization of all CPU cores during flowsheet calculations. The Inspector " +
            "is turned off while this is on.");

        _threads = page.CreateAndAddNumericEditorRow("Max Parallel Threads",
            S.MaxDegreeOfParallelism > 0 ? S.MaxDegreeOfParallelism : Environment.ProcessorCount,
            1, 256, 0, null);

        _simd = page.CreateAndAddCheckBoxRow("Enable SIMD Extensions", S.UseSIMDExtensions, null);
        page.CreateAndAddDescriptionRow(
            "Enables utilization of special CPU instructions for accelerated math calculations.");

        _breakOnException = page.CreateAndAddCheckBoxRow("Break on Exception",
            S.SolverBreakOnException, null);
        page.CreateAndAddDescriptionRow(
            "If activated, the solver will not calculate the rest of the flowsheet when an error " +
            "occurs during the calculation of an intermediate block.");

        // the two cannot be on at the same time, as in the Windows form
        _inspector.IsCheckedChanged += (_, _) =>
        {
            if (_inspector.IsChecked == true) _parallel.IsChecked = false;
        };
        _parallel.IsCheckedChanged += (_, _) =>
        {
            if (_parallel.IsChecked == true) _inspector.IsChecked = false;
        };

        return page;
    }

    // -------------------------------------------------------------------------
    // Flowsheet
    // -------------------------------------------------------------------------

    private Control BuildFlowsheet()
    {
        var page = Page();

        page.CreateAndAddLabelRow("Systems of Units");

        var systems = UnitSystems.ToList();
        var current = Math.Max(0, systems.IndexOf(S.PreferredSystemOfUnits ?? "SI"));

        _unitSystem = page.CreateAndAddDropDownRow("Default System of Units", systems, current, null);
        page.CreateAndAddDescriptionRow("The system of units a new simulation starts in.");

        page.CreateAndAddLabelRow("Renderer");

        _antiAlias = page.CreateAndAddCheckBoxRow("Enable Anti-Aliasing", S.DrawingAntiAlias, null);
        page.CreateAndAddDescriptionRow("Sets anti-aliasing (edge smoothing) for the flowsheet.");

        page.CreateAndAddLabelRow("Flowsheet Object Editors");

        _editOnSelect = page.CreateAndAddCheckBoxRow("View/Open Object Editor After Selection",
            S.EditOnSelect, null);
        page.CreateAndAddDescriptionRow("Opens the object editor after selection on the flowsheet.");

        _solverOnEdit = page.CreateAndAddCheckBoxRow("Call Solver on Editor Property Update",
            S.CallSolverOnEditorPropertyChanged, null);
        page.CreateAndAddDescriptionRow(
            "Requests a flowsheet calculation after an object property is changed on the editor.");

        _fixedInputSize = page.CreateAndAddCheckBoxRow("Fix Size of Input Controls",
            S.EditorTextBoxFixedSize, null);

        var sizes = FontSizes.ToList();

        _editorFontSize = page.CreateAndAddDropDownRow("Font Size (Editor Labels/Descriptions)",
            sizes, Math.Max(0, sizes.IndexOf(S.EditorFontSize > 0 ? S.EditorFontSize.ToString() : "10")), null);

        _reportFontSize = page.CreateAndAddDropDownRow("Font Size (Text Reports)",
            sizes, Math.Max(0, sizes.IndexOf(S.ResultsReportFontSize.ToString())), null);

        page.CreateAndAddLabelRow("Undo/Redo");

        _undoRedoRecalc = page.CreateAndAddCheckBoxRow("Recalculate the Flowsheet after Undo/Redo",
            S.UndoRedoRecalculateFlowsheet, null);

        return page;
    }

    // -------------------------------------------------------------------------
    // User compounds
    // -------------------------------------------------------------------------

    private Control BuildCompounds()
    {
        var page = Page();

        page.CreateAndAddLabelRow("User Compound Datasets");
        page.CreateAndAddDescriptionRow(
            "Compound datasets are read when the application starts. XML datasets and single " +
            "compound JSON files are both accepted.");

        _datasets = new ListBox { Height = 150, FontSize = 11 };
        foreach (var db in S.UserDatabases) _datasets.Items.Add(db);
        page.CreateAndAddControlRow(_datasets);

        page.CreateAndAddTwoButtonsRow("Add Dataset...", null, "Remove Selected", null,
            async (btn, e) => await AddAsync(false), (btn, e) => Remove(false));

        _replaceComps = page.CreateAndAddCheckBoxRow(
            "Replace Compounds with the Same Name", S.ReplaceComps, null);
        page.CreateAndAddDescriptionRow(
            "When on, a user compound replaces the one already loaded under the same name.");

        page.CreateAndAddLabelRow("User Interaction Parameter Datasets");

        _interactions = new ListBox { Height = 150, FontSize = 11 };
        foreach (var db in S.UserInteractionsDatabases) _interactions.Items.Add(db);
        page.CreateAndAddControlRow(_interactions);

        page.CreateAndAddTwoButtonsRow("Add Dataset...", null, "Remove Selected", null,
            async (btn, e) => await AddAsync(true), (btn, e) => Remove(true));

        return page;
    }

    private async System.Threading.Tasks.Task AddAsync(bool interactions)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var types = interactions
            ? new[] { new FilePickerFileType("Interaction Parameter Files") { Patterns = new[] { "*.xml" } } }
            : new[] { new FilePickerFileType("Compound Datasets") { Patterns = new[] { "*.xml", "*.json" } } };

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File",
            AllowMultiple = false,
            FileTypeFilter = types
        });

        var path = files?.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        if (interactions)
        {
            if (S.UserInteractionsDatabases.Contains(path)) return;
            S.UserInteractionsDatabases.Add(path);
            _interactions.Items.Add(path);
        }
        else
        {
            if (S.UserDatabases.Contains(path)) return;
            S.UserDatabases.Add(path);
            _datasets.Items.Add(path);
        }
    }

    private void Remove(bool interactions)
    {
        var list = interactions ? _interactions : _datasets;
        if (list.SelectedItem is not string path) return;

        if (interactions) S.UserInteractionsDatabases.Remove(path);
        else S.UserDatabases.Remove(path);

        list.Items.Remove(path);
    }

    // -------------------------------------------------------------------------
    // Backups
    // -------------------------------------------------------------------------

    private Control BuildBackups()
    {
        var page = Page();

        page.CreateAndAddLabelRow("Configuration Directory");

        _configDir = page.CreateAndAddStringEditorRow("Directory", S.GetConfigFileDir(), null);
        _configDir.IsReadOnly = true;

        page.CreateAndAddButtonRow("Open Configuration Directory", null, (btn, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = S.GetConfigFileDir(),
                    UseShellExecute = true
                });
            }
            catch (Exception) { }
        });

        page.CreateAndAddLabelRow("Backup Copies");

        _enableBackup = page.CreateAndAddCheckBoxRow("Enable Backup Copies", S.EnableBackupCopies, null);

        _backupInterval = page.CreateAndAddNumericEditorRow("Backup Interval (minutes)",
            S.BackupInterval, 1, 120, 0, null);

        _backupFolder = page.CreateAndAddStringEditorRow("Backup Folder", BackupFolder(), null);
        _backupFolder.IsReadOnly = true;

        page.CreateAndAddButtonRow("Browse for the Backup Folder...", null,
            async (btn, e) => await BrowseBackupAsync());

        page.CreateAndAddButtonRow("Purge Backup Folder", null, (btn, e) => PurgeBackupFolder());

        page.CreateAndAddDescriptionRow(
            "Backup copies are written while a simulation is open. Purging deletes every file in " +
            "the folder above.");

        return page;
    }

    /// <summary>The folder the backups go to: what the user picked, or the default one.</summary>
    private static string BackupFolder()
    {
        if (!string.IsNullOrEmpty(S.BackupFolder)) return S.BackupFolder;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DWSIM Application Data", "Backup");
    }

    private async System.Threading.Tasks.Task BrowseBackupAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Backup Folder",
            AllowMultiple = false
        });

        var path = folders?.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        S.BackupFolder = path;
        _backupFolder.Text = path;
    }

    private void PurgeBackupFolder()
    {
        try
        {
            var folder = BackupFolder();
            if (!Directory.Exists(folder)) return;

            foreach (var file in Directory.GetFiles(folder))
            {
                try { File.Delete(file); } catch (Exception) { }
            }
        }
        catch (Exception) { }
    }

    // -------------------------------------------------------------------------
    // Python
    // -------------------------------------------------------------------------

    private Control BuildPython()
    {
        var page = Page();

        page.CreateAndAddLabelRow("Python.NET Interpreter Settings");
        page.CreateAndAddDescriptionRow(
            "Set the path of the Python 3.x dynamic library to enable integration with DWSIM. " +
            "Restart DWSIM for the change to take effect.");

        _pythonPath = page.CreateAndAddStringEditorRow("Python Library Path", S.PythonPath ?? "", null);

        page.CreateAndAddButtonRow("Browse for the Python Library...", null,
            async (btn, e) => await BrowsePythonAsync());

        page.CreateAndAddButtonRow("Get WinPython", null, (btn, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://winpython.github.io/",
                    UseShellExecute = true
                });
            }
            catch (Exception) { }
        });

        return page;
    }

    private async System.Threading.Tasks.Task BrowsePythonAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Python Library",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Python Libraries") { Patterns = new[] { "*.dll", "*.so", "*.dylib" } },
                FilePickerFileTypes.All
            }
        });

        var path = files?.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path)) _pythonPath.Text = path;
    }

    // -------------------------------------------------------------------------
    // Other
    // -------------------------------------------------------------------------

    private Control BuildOther()
    {
        var page = Page();

        page.CreateAndAddLabelRow("Updates");

        _checkUpdates = page.CreateAndAddCheckBoxRow("Check for Updates", S.CheckForUpdates, null);

        page.CreateAndAddLabelRow("CAPE-OPEN");

        _hideSolidPhaseCO = page.CreateAndAddCheckBoxRow("Hide the Solid Phase from CAPE-OPEN Components",
            S.HideSolidPhaseFromCAPEOPENComponents, null);

        page.CreateAndAddLabelRow("Extensions");

        _loadExtensions = page.CreateAndAddCheckBoxRow("Load Extensions and Plugins at Startup",
            S.LoadExtensionsAndPlugins, null);
        page.CreateAndAddDescriptionRow("Takes effect the next time the application starts.");

        page.CreateAndAddLabelRow("Debug Mode");

        _debugLevel = page.CreateAndAddNumericEditorRow("Debug Level (0 = off)", S.DebugLevel, 0, 3, 0, null);

        return page;
    }

    // -------------------------------------------------------------------------
    // Interface
    // -------------------------------------------------------------------------

    private Control BuildInterface()
    {
        var page = Page();

        page.CreateAndAddLabelRow("Scaling");

        _scaling = page.CreateAndAddNumericEditorRow("Scaling Factor",
            S.UIScalingFactor > 0 ? S.UIScalingFactor : 1.0, 0.2, 3.0, 2, null);
        page.CreateAndAddDescriptionRow(
            "Sets the scaling factor for controls. Takes effect the next time the application starts.");

        page.CreateAndAddLabelRow("Appearance");

        var themes = new List<string> { "Light", "Dark", "System default" };

        var currentTheme = 2;
        var variant = global::Avalonia.Application.Current?.RequestedThemeVariant;
        if (variant == ThemeVariant.Light) currentTheme = 0;
        else if (variant == ThemeVariant.Dark) currentTheme = 1;

        _theme = page.CreateAndAddDropDownRow("Theme", themes, currentTheme, null);
        page.CreateAndAddDescriptionRow("The theme is applied as soon as the settings are accepted.");

        var cultures = Cultures.ToList();
        _culture = page.CreateAndAddDropDownRow("Culture / Locale", cultures,
            Math.Max(0, cultures.IndexOf(S.CurrentCulture ?? "en")), null);

        return page;
    }

    // -------------------------------------------------------------------------
    // Accept
    // -------------------------------------------------------------------------

    private void OnOK(object? sender, RoutedEventArgs e)
    {
        // solver
        S.InspectorEnabled = _inspector.IsChecked.GetValueOrDefault();
        S.ClearInspectorHistoryOnNewCalculationRequest = _clearInspector.IsChecked.GetValueOrDefault();
        S.SolverTimeoutSeconds = (int)(_timeout.Value ?? 3600);
        S.EnableParallelProcessing = _parallel.IsChecked.GetValueOrDefault();
        S.MaxDegreeOfParallelism = (int)(_threads.Value ?? Environment.ProcessorCount);
        S.UseSIMDExtensions = _simd.IsChecked.GetValueOrDefault();
        S.SolverBreakOnException = _breakOnException.IsChecked.GetValueOrDefault();

        // flowsheet
        if (_unitSystem.SelectedItem is string units) S.PreferredSystemOfUnits = units;
        S.DrawingAntiAlias = _antiAlias.IsChecked.GetValueOrDefault();
        S.EditOnSelect = _editOnSelect.IsChecked.GetValueOrDefault();
        S.CallSolverOnEditorPropertyChanged = _solverOnEdit.IsChecked.GetValueOrDefault();
        S.EditorTextBoxFixedSize = _fixedInputSize.IsChecked.GetValueOrDefault();

        if (_editorFontSize.SelectedItem is string editorSize && int.TryParse(editorSize, out var editorValue))
            S.EditorFontSize = editorValue;
        if (_reportFontSize.SelectedItem is string reportSize && int.TryParse(reportSize, out var reportValue))
            S.ResultsReportFontSize = reportValue;

        S.UndoRedoRecalculateFlowsheet = _undoRedoRecalc.IsChecked.GetValueOrDefault();

        // compounds
        S.ReplaceComps = _replaceComps.IsChecked.GetValueOrDefault();

        // backups
        S.EnableBackupCopies = _enableBackup.IsChecked.GetValueOrDefault();
        S.BackupInterval = (int)(_backupInterval.Value ?? 5);

        // python
        S.PythonPath = _pythonPath.Text ?? "";

        // other
        S.CheckForUpdates = _checkUpdates.IsChecked.GetValueOrDefault();
        S.HideSolidPhaseFromCAPEOPENComponents = _hideSolidPhaseCO.IsChecked.GetValueOrDefault();
        S.LoadExtensionsAndPlugins = _loadExtensions.IsChecked.GetValueOrDefault();
        S.DebugLevel = (int)(_debugLevel.Value ?? 0);

        // interface
        S.UIScalingFactor = (double)(_scaling.Value ?? 1.0m);

        ApplyTheme();

        if (_culture.SelectedItem is string culture)
        {
            S.CurrentCulture = culture;
            S.CultureInfo = culture;
        }

        try { S.SaveSettings("dwsim_newui.ini"); }
        catch (Exception) { }

        Close();
    }

    private void ApplyTheme()
    {
        var app = global::Avalonia.Application.Current;
        if (app == null) return;

        switch (_theme.SelectedIndex)
        {
            case 0:
                app.RequestedThemeVariant = ThemeVariant.Light;
                S.DarkMode = false;
                break;
            case 1:
                app.RequestedThemeVariant = ThemeVariant.Dark;
                S.DarkMode = true;
                break;
            default:
                app.RequestedThemeVariant = ThemeVariant.Default;
                break;
        }
    }

}
