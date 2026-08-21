using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using System.Threading.Tasks;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.BaseClasses;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// DWSIM launcher / welcome screen.
/// Equivalent of DWSIM.UI.Forms.Forms.MainForm (MainForm.eto.cs).
/// </summary>
public partial class MainWindow : Window
{
    public List<ConstantProperties> UserCompounds { get; } = new();

    public List<IUtilityPlugin5> Plugins { get; } = new();

    public List<IExtenderCollection> Extenders { get; } = new();

    /// <summary>
    /// Bottom container for extension panels (licensing, support).
    /// Mirrors Eto MainForm.BottomContainer so DWSIM.Support.dll can access it via reflection.
    /// </summary>
    public StackPanel BottomContainer => BottomPane;

    public MainWindow()
    {
        GlobalSettings.Settings.OldUI = false;
        GlobalSettings.Settings.DpiScale = GetTopLevel(this)?.RenderScaling ?? 1.0;

        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);

        InitializeSupport();

        // the settings can keep the extensions out of the way, as the Windows launcher does
        if (GlobalSettings.Settings.LoadExtensionsAndPlugins)
        {
            LoadPlugins();
            LoadExtenders();
        }

        SetupDocuments();

        WireButtons();
        WireMenus();
        WireMenuIcons();
        LoadRecentFiles();
        LoadSamples();
        LoadFosseeFlowsheets();
        LoadCaseLibraryFlowsheets();
        // read the marker before laying it down again: if it survived, the last run did not close
        // normally, and the backup copies are worth offering back
        _previousRunCrashed = File.Exists(SessionLockPath);
        WriteSessionLock();

        Closing += OnMainWindowClosing;
        Opened += OnMainWindowOpened;
    }

    private bool _previousRunCrashed;

    private static string SessionLockPath =>
        Path.Combine(DWSIM.GlobalSettings.Settings.GetConfigFileDir(), "session.lock");

    private static void WriteSessionLock()
    {
        try { File.WriteAllText(SessionLockPath, DateTime.Now.ToString("o")); } catch { }
    }

    private async void OnMainWindowOpened(object? sender, EventArgs e)
    {
        var splash = new SplashWindow();
        splash.Show(this);

        if (_previousRunCrashed && BackupRecoveryWindow.FindBackups().Length > 0)
            await new BackupRecoveryWindow(OpenFlowsheetFile).ShowDialog(this);

        _ = CheckForUpdatesAsync();
    }

    /// <summary>Startup update check, as the WinForms UI did: if enabled, ask dwsim.org whether a
    /// newer version exists and, if so, offer to open the downloads page.</summary>
    private async Task CheckForUpdatesAsync()
    {
        if (!DWSIM.GlobalSettings.Settings.CheckForUpdates) return;
        try
        {
            var (available, whatsnew) = await Task.Run(() =>
            {
                DWSIM.GlobalSettings.Settings.CurrentRunningVersion =
                    Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
                bool av = DWSIM.SharedClasses.UpdateCheck.CheckForUpdates();
                return (av, av ? DWSIM.SharedClasses.UpdateCheck.GetWhatsNew() : "");
            });

            if (!available) return;

            var body = new TextBlock
            {
                Text = "A newer version of DWSIM is available." +
                       (string.IsNullOrWhiteSpace(whatsnew) ? "" : "\n\n" + whatsnew),
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(14)
            };

            var later = new Button { Content = "Later", IsCancel = true, Width = 90, Margin = new Thickness(6) };
            var download = new Button { Content = "Open Downloads", IsDefault = true, Width = 150, Margin = new Thickness(6) };
            download.Classes.Add("dialog");

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8)
            };
            buttons.Children.Add(later);
            buttons.Children.Add(download);

            var root = new DockPanel();
            DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(new ScrollViewer { Content = body });

            var dlg = new Window
            {
                Title = "Update Available",
                Width = 520,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = root
            };
            IconHelper.ApplyWindowIcon(dlg);
            later.Click += (_, _) => dlg.Close();
            download.Click += (_, _) => { OpenUrl("https://dwsim.org/downloads"); dlg.Close(); };
            await dlg.ShowDialog(this);
        }
        catch { }
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try { DWSIM.GlobalSettings.Settings.SaveSettings("dwsim_newui.ini"); } catch { }
        // a clean close clears the marker, so the next run does not offer recovery
        try { File.Delete(SessionLockPath); } catch { }
    }

    private void LoadRecentFiles()
    {
        RecentFilesList.Items.Clear();
        foreach (var path in RecentFilesManager.Load())
            RecentFilesList.Items.Add(path);
    }

    private void LoadSamples()
    {
        SamplesList.Items.Clear();
        try
        {
            var samplesDir = FindSamplesDirectory();
            if (samplesDir != null)
            {
                var files = Directory.EnumerateFiles(samplesDir, "*.dwxm*")
                    .OrderBy(x => Path.GetFileNameWithoutExtension(x))
                    .ToList();
                foreach (var file in files)
                    SamplesList.Items.Add(new SampleItem(
                        Path.GetFileNameWithoutExtension(file), file));
            }
            else
            {
                SamplesList.Items.Add("(No samples directory found)");
            }
        }
        catch
        {
            SamplesList.Items.Add("(Could not load samples)");
        }
    }

    /// <summary>
    /// Searches for the samples directory in multiple locations:
    /// 1. Next to the running executable (installed layout)
    /// 2. DWSIM install directory (AppData\Local\DWSIM)
    /// 3. Repository PlatformFiles\Common\samples (development layout)
    /// </summary>
    private static string? FindSamplesDirectory()
    {
        var candidates = new System.Collections.Generic.List<string>();

        // 1. Standard: next to the executable
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        candidates.Add(Path.Combine(baseDir, "samples"));

        // 2. Installed DWSIM location (Windows)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            candidates.Add(Path.Combine(localAppData, "DWSIM", "samples"));

        // 3. Development layout: repo\PlatformFiles\Common\samples
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        candidates.Add(Path.Combine(repoRoot, "PlatformFiles", "Common", "samples"));

        foreach (var dir in candidates)
        {
            if (Directory.Exists(dir))
                return dir;
        }

        return null;
    }

    private void WireButtons()
    {
        // Process Modeling
        LnkNew.Click += (_, _) => OpenNewFlowsheet();
        LnkNewWizard.Click += async (_, _) => await OpenNewWithWizardAsync();
        LnkLoadFile.Click += async (_, _) => await OpenFileDialogAsync();

        // Compound creation / data regression. Both tools work over a flowsheet, so a new
        // simulation is opened for them, which is what the Windows screen does with its MDI child
        LnkNewRegression.Click += (_, _) => OpenRegression(loadFromFile: false);
        LnkLoadRegression.Click += (_, _) => OpenRegression(loadFromFile: true);
        LnkNewCompound.Click += (_, _) => new CompoundCreatorWindow().Show(this);
        LnkNewSolid.Click += (_, _) => new BiomassCompoundCreatorWindow().Show(this);
        LnkNewBlackOil.Click += (_, _) => new BlackOilCompoundCreatorWindow().Show(this);
        LnkNewCompoundWiz.Click += (_, _) => new CompoundCreatorWindow().Show(this);
        LnkDatabaseManager.Click += async (_, _) => await new DatabaseManagerWindow().ShowDialog(this);

        // Documentation
        LnkGuideHtml.Click += (_, _) => OpenUserGuide();
        LnkGuidePdf.Click += (_, _) => OpenUserGuidePdf();
        LnkTutorials.Click += (_, _) => OpenUrl("https://dwsim.org/tutorials/");
        LnkLearning.Click += (_, _) => OpenUrl("https://dwsim.org/wiki/index.php?title=Category:Tutorials");
        LnkPublications.Click += (_, _) => OpenUrl("https://dwsim.org/wiki/index.php?title=Literature");
        LnkApiDocs.Click += (_, _) => OpenUrl("https://dwsim.org/api_help/html/R_Project_DWSIM_Class_Library_Documentation.htm");

        // Support
        BtnSponsorGitHub.Click += (_, _) => OpenUrl("https://github.com/sponsors/DanWBR");
        BtnSponsorPatreon.Click += (_, _) => OpenUrl("https://www.patreon.com/join/dwsim?");
        BtnSponsorCoffee.Click += (_, _) => OpenUrl("https://www.buymeacoffee.com/dwsim");

        // FOSSEE
        BtnFosseeProject.Click += (_, _) => OpenUrl("https://dwsim.fossee.in/flowsheeting-project");
        BtnFosseeSite.Click += (_, _) => OpenUrl("https://fossee.in/");
        FosseeList.DoubleTapped += (_, _) => OpenFosseeFlowsheet();

        // Case Library
        BtnCaseLibraryRepo.Click += (_, _) => OpenUrl("https://github.com/DanWBR/dwsim-case-library");
        BtnCaseLibraryContribute.Click += (_, _) => OpenUrl("https://github.com/DanWBR/dwsim-case-library/blob/main/CONTRIBUTING.md");
        CaseLibraryList.DoubleTapped += (_, _) => OpenCaseLibraryFlowsheet();

        BtnSettings.Click += async (_, _) => await new PreferencesWindow().ShowDialog(this);
        BtnAbout.Click += (_, _) => ShowAbout();
    }

    private async System.Threading.Tasks.Task OpenNewWithWizardAsync()
    {
        var view = AddDocument("Untitled");
        await view.NewWithoutWizardAsync();
        view.ShowSetupWizard();
    }

    private async void OpenRegression(bool loadFromFile)
    {
        var view = AddDocument("Untitled");
        await view.NewWithoutWizardAsync();
        view.ShowDataRegression(loadFromFile);
    }

    // -------------------------------------------------------------------------
    // FOSSEE flowsheets
    // -------------------------------------------------------------------------

    private sealed class FosseeItem
    {
        public FosseeItem(DWSIM.SharedClasses.FOSSEEFlowsheet flowsheet)
        {
            Flowsheet = flowsheet;
        }

        public DWSIM.SharedClasses.FOSSEEFlowsheet Flowsheet { get; }

        public override string ToString()
        {
            return $"{Flowsheet.Title} - {Flowsheet.ProposerName} ({Flowsheet.Institution})";
        }
    }

    /// <summary>
    /// Reads the flowsheet index off the FOSSEE site. It is a web request, so it runs in the
    /// background and the list fills in when it lands.
    /// </summary>
    private async void LoadFosseeFlowsheets()
    {
        FosseeList.Items.Clear();
        FosseeList.Items.Add("Loading flowsheets from dwsim.fossee.in...");

        try
        {
            var list = await System.Threading.Tasks.Task.Run(
                () => DWSIM.SharedClasses.FOSSEEFlowsheets.GetFOSSEEFlowsheets());

            FosseeList.Items.Clear();

            if (list == null || list.Count == 0)
            {
                FosseeList.Items.Add("(No flowsheets returned)");
                return;
            }

            foreach (var flowsheet in list.OrderBy(x => x.Title))
                FosseeList.Items.Add(new FosseeItem(flowsheet));
        }
        catch (Exception ex)
        {
            FosseeList.Items.Clear();
            FosseeList.Items.Add("(Could not reach the FOSSEE site: " + ex.Message + ")");
        }
    }

    /// <summary>
    /// Downloads the selected flowsheet and opens it as a document, after asking, which is what
    /// the Windows welcome screen does.
    /// </summary>
    private async void OpenFosseeFlowsheet()
    {
        if (FosseeList.SelectedItem is not FosseeItem item) return;

        var info = item.Flowsheet;

        var message = $"Title: {info.Title}\nAuthor: {info.ProposerName}\nInstitution: {info.Institution}\n\n" +
                      "Download and open this flowsheet?";

        if (!await ConfirmAsync("Open FOSSEE Flowsheet", message)) return;

        try
        {
            var path = await System.Threading.Tasks.Task.Run(() =>
            {
                // the index only carries the page address; the download link lives on the page
                var details = string.IsNullOrEmpty(info.DownloadLink)
                    ? DWSIM.SharedClasses.FOSSEEFlowsheets.GetFOSSEEFlowsheetInfo(info.Address)
                    : info;

                var temp = DWSIM.SharedClasses.FOSSEEFlowsheets.DownloadFlowsheet(details.DownloadLink, null);

                // the download has no extension, and the loader picks the reader from it
                var target = Path.Combine(Path.GetTempPath(),
                    MakeFileName(info.Title) + (IsZip(temp) ? ".dwxmz" : ".dwxml"));

                File.Copy(temp, target, true);
                try { File.Delete(temp); } catch { }

                return target;
            });

            OpenFlowsheetFile(path);
        }
        catch (Exception ex)
        {
            await ConfirmAsync("Error", "Could not download the flowsheet: " + ex.Message, okOnly: true);
        }
    }

    private static bool IsZip(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return stream.ReadByte() == 'P' && stream.ReadByte() == 'K';
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Case Library flowsheets (github.com/DanWBR/dwsim-case-library)
    // -------------------------------------------------------------------------

    private sealed class CaseLibraryItem
    {
        public CaseLibraryItem(DWSIM.SharedClasses.CaseLibraryFlowsheet flowsheet)
        {
            Flowsheet = flowsheet;
        }

        public DWSIM.SharedClasses.CaseLibraryFlowsheet Flowsheet { get; }

        public override string ToString() => Flowsheet.DisplayName;
    }

    /// <summary>
    /// Reads the case index off the GitHub repository. It is a web request, so it runs in the
    /// background and the list fills in when it lands.
    /// </summary>
    private async void LoadCaseLibraryFlowsheets()
    {
        CaseLibraryList.Items.Clear();
        CaseLibraryList.Items.Add("Loading cases from the DWSIM Case Library...");

        try
        {
            var list = await System.Threading.Tasks.Task.Run(
                () => DWSIM.SharedClasses.CaseLibraryFlowsheets.GetCaseLibraryFlowsheets());

            CaseLibraryList.Items.Clear();

            if (list == null || list.Count == 0)
            {
                CaseLibraryList.Items.Add("(No cases returned)");
                return;
            }

            foreach (var flowsheet in list)
                CaseLibraryList.Items.Add(new CaseLibraryItem(flowsheet));
        }
        catch (Exception ex)
        {
            CaseLibraryList.Items.Clear();
            CaseLibraryList.Items.Add("(Could not reach the Case Library: " + ex.Message + ")");
        }
    }

    /// <summary>
    /// Downloads the selected case and opens it as a document, after asking.
    /// </summary>
    private async void OpenCaseLibraryFlowsheet()
    {
        if (CaseLibraryList.SelectedItem is not CaseLibraryItem item) return;

        var info = item.Flowsheet;

        var message = $"Title: {info.Title}\nCategory: {info.Category}\n\n" +
                      "Download and open this case?";

        if (!await ConfirmAsync("Open Case Library Flowsheet", message)) return;

        try
        {
            var path = await System.Threading.Tasks.Task.Run(
                () => DWSIM.SharedClasses.CaseLibraryFlowsheets.DownloadFlowsheet(info.DownloadUrl, null));

            OpenFlowsheetFile(path);
        }
        catch (Exception ex)
        {
            await ConfirmAsync("Error", "Could not download the case: " + ex.Message, okOnly: true);
        }
    }

    private static string MakeFileName(string title)
    {
        var name = new string(title.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(name) ? "fossee_flowsheet" : name.Trim();
    }

    private async System.Threading.Tasks.Task<bool> ConfirmAsync(string title, string message,
                                                                 bool okOnly = false)
    {
        var result = false;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 16, 12)
        };

        var dlg = new Window
        {
            Title = title,
            Width = 460,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon()
        };

        if (!okOnly)
        {
            var no = new Button { Content = "No", Width = 80, IsCancel = true };
            no.Classes.Add("dialog");
            no.Click += (_, _) => dlg.Close();
            buttons.Children.Add(no);
        }

        var ok = new Button { Content = okOnly ? "OK" : "Yes", Width = 80, IsDefault = true };
        ok.Classes.Add("dialog");
        ok.Click += (_, _) => { result = true; dlg.Close(); };
        buttons.Children.Add(ok);

        var body = new DockPanel();
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        body.Children.Add(buttons);
        body.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(20, 20, 20, 0)
        });

        dlg.Content = body;

        await dlg.ShowDialog(this);

        return result;
    }

    private void OpenUserGuidePdf()
    {
        // The distribution ships docs/User_Guide.pdf (case matters on Linux/macOS).
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "User_Guide.pdf");
        if (File.Exists(path)) OpenUrl(path); else OpenUserGuide();
    }

    private void WireMenus()
    {
        MenuNew.Click   += (_, _) => OpenNewFlowsheet();

        // the list is read when the submenu opens, so it is never stale
        MenuRecent.SubmenuOpened += (_, _) => RecentFilesMenu.Fill(MenuRecent, OpenFlowsheetFile);
        RecentFilesMenu.Fill(MenuRecent, OpenFlowsheetFile);
        MenuOpen.Click  += async (_, _) => await OpenFileDialogAsync();
        MenuExit.Click  += (_, _) => Close();
        MenuAbout.Click += (_, _) => ShowAbout();
        MenuPrefs.Click += async (_, _) => await new PreferencesWindow().ShowDialog(this);
        MenuUserGuide.Click += (_, _) => OpenUserGuide();
        MenuHelpSupport.Click += (_, _) => OpenUrl("https://dwsim.org/wiki/index.php?title=Support");
        MenuHelpBug.Click += (_, _) => OpenUrl("https://github.com/DanWBR/dwsim10/issues");
        MenuHelpWebsite.Click += (_, _) => OpenUrl("https://dwsim.org");

        RecentFilesList.DoubleTapped += (_, _) =>
        {
            if (RecentFilesList.SelectedItem is string path)
                OpenFlowsheetFile(path);
        };

        SamplesList.DoubleTapped += (_, _) =>
        {
            if (SamplesList.SelectedItem is SampleItem si)
                OpenFlowsheetFile(si.FilePath);
        };
    }

    private void WireMenuIcons()
    {
        IconHelper.Set(MenuNew,         "\U0001F4C4"); // page
        IconHelper.Set(MenuOpen,        "\U0001F4C2"); // open folder
        IconHelper.Set(MenuExit,        "✖");          // heavy X
        IconHelper.Set(MenuPrefs,       "⚙");          // gear
        IconHelper.Set(MenuUserGuide,   "\U0001F4D6"); // open book
        IconHelper.Set(MenuHelpSupport, "❤");          // heart
        IconHelper.Set(MenuHelpBug,     "\U0001F41B"); // bug
        IconHelper.Set(MenuHelpWebsite, "\U0001F310"); // globe
        IconHelper.Set(MenuAbout,       "ℹ");          // info
    }

    private static void OpenUserGuide()
    {
        // Try local docs first, then online
        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "dwsim-help", "index.html");
        if (File.Exists(localPath))
            OpenUrl(localPath);
        else
            OpenUrl("https://dwsim.org/");
    }

    private static void OpenUrl(string url)
    {
        // ShellExecute (UseShellExecute=true) is the obvious choice, but on a machine whose default
        // browser/protocol registration is broken it throws "no application found" and pops the shell's
        // own error dialog. The per-OS openers resolve the default handler more reliably, so try them
        // first (both for web URLs and local file paths) and fall back to ShellExecute.
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + url + "\"") { UseShellExecute = false });
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("open", url) { UseShellExecute = false });
            else
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
        }
        catch
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                // Browser launch can fail on locked-down machines; fail silently rather than crash.
            }
        }
    }

    // -------------------------------------------------------------------------
    // Documents: one tab per open simulation, in place of the MDI client area
    // -------------------------------------------------------------------------

    private readonly ShellDockFactory _shell = new();

    private readonly Dictionary<Dock.Model.Avalonia.Controls.Document, FlowsheetView> _documents = new();

    /// <summary>
    /// A simulation document. Closing is answered by the simulation itself, and the answer only
    /// arrives after the dialog is dismissed, so the first attempt is refused and the close is
    /// asked again once confirmed. Both the tab's close button and File &gt; Close land here.
    /// </summary>
    private sealed class FlowsheetDocument : Dock.Model.Avalonia.Controls.Document
    {
        private bool _confirmed;

        public FlowsheetView View { get; set; } = null!;

        public override bool OnClose()
        {
            if (_confirmed) return true;

            _ = ConfirmAsync();

            return false;
        }

        private async System.Threading.Tasks.Task ConfirmAsync()
        {
            if (!await View.ConfirmCloseAsync()) return;

            _confirmed = true;
            Factory?.CloseDockable(this);
        }
    }

    /// <summary>The simulation the menu and the window title currently follow.</summary>
    public FlowsheetView? ActiveFlowsheet { get; private set; }

    private void SetupDocuments()
    {
        var layout = _shell.CreateLayout();
        _shell.InitLayout(layout);

        DocumentsHost.Factory = _shell;
        DocumentsHost.Layout = layout;

        _shell.ActiveDockableChanged += (_, e) =>
        {
            if (e.Dockable is Dock.Model.Avalonia.Controls.Document doc &&
                _documents.TryGetValue(doc, out var view))
                SetActiveFlowsheet(view);
        };

        // the document itself runs the confirmation; the welcome screen comes back once the
        // last one is gone
        _shell.DockableClosed += (_, e) =>
        {
            if (e.Dockable is not Dock.Model.Avalonia.Controls.Document doc) return;
            if (!_documents.Remove(doc)) return;

            global::Avalonia.Threading.Dispatcher.UIThread.Post(ShowWelcomeIfEmpty);
        };

        // Under Semi the stock document close X renders no glyph (its Path fill binding resolves
        // null), so give every document tab strip our own close template as its strips appear.
        DocumentsHost.LayoutUpdated += (_, _) => ApplyDocumentCloseTemplate();
    }

    private global::Avalonia.Controls.Templates.IDataTemplate? _documentCloseTemplate;

    /// <summary>A close button we fully control: a filled X coloured by the tab foreground (white
    /// on the accent-selected tab), falling back to grey if the ancestor lookup does not resolve.</summary>
    private global::Avalonia.Controls.Templates.IDataTemplate BuildDocumentCloseTemplate()
        => new global::Avalonia.Controls.Templates.FuncDataTemplate<Dock.Model.Core.IDockable>((item, _) =>
        {
            var glyph = new global::Avalonia.Controls.Shapes.Path
            {
                Width = 9,
                Height = 9,
                Stretch = Stretch.Uniform,
                Data = Geometry.Parse("M0,1 L1,0 L4.5,3.5 L8,0 L9,1 L5.5,4.5 L9,8 L8,9 L4.5,5.5 L1,9 L0,8 L3.5,4.5 Z"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            glyph.Bind(global::Avalonia.Controls.Shapes.Path.FillProperty,
                new global::Avalonia.Data.Binding("Foreground")
                {
                    RelativeSource = new global::Avalonia.Data.RelativeSource(
                        global::Avalonia.Data.RelativeSourceMode.FindAncestor)
                    { AncestorType = typeof(Dock.Avalonia.Controls.DocumentTabStripItem) },
                    FallbackValue = Brushes.Gray,
                    TargetNullValue = Brushes.Gray
                });

            var button = new Button
            {
                Content = glyph,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2),
                Width = 18,
                Height = 18,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand),
                // Only closable documents get an X (the inner Flowsheet/Results/... tabs are not).
                IsVisible = item.CanClose
            };
            button.Click += (_, _) => _shell.CloseDockable(item);
            return button;
        }, supportsRecycling: false);

    private void ApplyDocumentCloseTemplate()
    {
        _documentCloseTemplate ??= BuildDocumentCloseTemplate();
        foreach (var strip in DocumentsHost.GetVisualDescendants().OfType<Dock.Avalonia.Controls.DocumentTabStrip>())
            if (!ReferenceEquals(strip.CloseTemplate, _documentCloseTemplate))
                strip.CloseTemplate = _documentCloseTemplate;
    }

    private FlowsheetView AddDocument(string title)
    {
        var view = new FlowsheetView { SimulationName = title };

        var doc = new FlowsheetDocument
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Content = view,
            View = view,
            CanClose = true,
            CanFloat = true
        };

        _documents[doc] = view;

        view.TitleChanged += (_, _) => doc.Title = view.SimulationName;
        view.CloseRequested += (_, _) => _shell.CloseDockable(doc);

        // File > New and File > Open open another document instead of replacing this one
        view.NewRequested += (_, _) => OpenNewFlowsheet();
        view.OpenRequested += async (_, _) => await OpenFileDialogAsync();
        view.OpenRecentRequested += (_, path) => OpenFlowsheetFile(path);

        // the close button on the tab reaches the factory through the dockable itself, so a
        // document built by hand has to be told which factory owns it
        doc.Factory = _shell;
        doc.Owner = _shell.Documents;

        _shell.AddDockable(_shell.Documents, doc);
        _shell.SetActiveDockable(doc);
        _shell.SetFocusedDockable(_shell.Root, doc);

        WelcomeHost.IsVisible = false;
        DocumentsHost.IsVisible = true;

        SetActiveFlowsheet(view);

        return view;
    }

    /// <summary>
    /// Puts the menu of the active simulation in the main window's menu bar, which is what the
    /// Windows MDI parent does with its child menus.
    /// </summary>
    private void SetActiveFlowsheet(FlowsheetView? view)
    {
        ActiveFlowsheet = view;

        MenuHost.Content = view != null ? view.FlowsheetMenu : BaseMenu;
        Title = view != null ? $"DWSIM - {view.SimulationName}" : "DWSIM";

        // the extension buttons follow the active flowsheet: show its set, and none on the welcome
        // screen, so opening and closing simulations does not leave stale buttons on the strip
        if (MenuBarExtensions != null)
        {
            MenuBarExtensions.Children.Clear();
            if (view != null)
                foreach (var button in view.ExtensionButtons)
                    MenuBarExtensions.Children.Add(button);
        }
    }

    /// <summary>
    /// Re-applies the given view's extension buttons to the menu-bar strip. LoadFlowsheetExtensions
    /// populates the buttons after the flowsheet has loaded, which is after SetActiveFlowsheet
    /// already built the strip from the then-empty list; this puts them on once they exist.
    /// </summary>
    public void RefreshExtensionButtons(FlowsheetView view)
    {
        if (ActiveFlowsheet != view || MenuBarExtensions == null) return;
        MenuBarExtensions.Children.Clear();
        foreach (var button in view.ExtensionButtons)
            MenuBarExtensions.Children.Add(button);
    }

    private void ShowWelcomeIfEmpty()
    {
        if (_documents.Count > 0) return;

        DocumentsHost.IsVisible = false;
        WelcomeHost.IsVisible = true;

        SetActiveFlowsheet(null);
    }

    // -------------------------------------------------------------------------
    // Actions
    // -------------------------------------------------------------------------

    private async void OpenNewFlowsheet()
    {
        var view = AddDocument("Untitled");
        await view.NewWithoutWizardAsync();
    }

    private async System.Threading.Tasks.Task OpenFileDialogAsync()
    {
        var top = TopLevel.GetTopLevel(this)!;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open DWSIM Simulation",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("DWSIM Simulation")
                {
                    Patterns = new[] { "*.dwxmz", "*.dwxml", "*.xml" }
                },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
            OpenFlowsheetFile(files[0].Path.LocalPath);
    }

    public async void OpenFlowsheetFile(string path)
    {
        var view = AddDocument(System.IO.Path.GetFileNameWithoutExtension(path));

        try
        {
            await view.LoadAsync(path);
            RecentFilesManager.Add(path);
            LoadRecentFiles();
        }
        catch { }
    }

    private async void ShowAbout()
    {
        await new AboutWindow().ShowDialog(this);
    }

    // -------------------------------------------------------------------------
    // Recent files management
    // -------------------------------------------------------------------------

    public void AddRecentFile(string path)
    {
        if (!RecentFilesList.Items.Contains(path))
            RecentFilesList.Items.Insert(0, path);
    }

    // -------------------------------------------------------------------------
    // Extension / Plugin loading
    // (Ported from DWSIM.UI.Desktop.Forms MainForm.eto.cs)
    // -------------------------------------------------------------------------

    private void InitializeSupport()
    {
        Assembly? sa = null;
        // Probe the app's own directory first (flat self-contained bundle, the macOS/Linux layout),
        // then the parent directory (the nested Windows Plus layout).
        var appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        foreach (var candidate in new[]
                 {
                     Path.Combine(appDir, "DWSIM.Support.dll"),
                     Path.Combine(Path.GetDirectoryName(appDir)!, "DWSIM.Support.dll")
                 })
        {
            try
            {
                if (File.Exists(candidate))
                {
                    // LoadFrom (default ALC), NOT LoadFile (isolated ALC): the Plus extensions load via
                    // Assembly.LoadFrom into the default context and resolve DWSIM.Support there. LoadFile
                    // here would put the app's DWSIM.Support - the one that runs the licence check and
                    // populates Initialization.Details - in a SEPARATE instance, so the extensions saw a
                    // second copy with an empty licence state (GetAccessLevel()=0) and their Plus gate fired
                    // even for valid subscribers, while the membership panel (this instance) showed the tier.
                    sa = Assembly.LoadFrom(candidate);
                    break;
                }
            }
            catch { }
        }

        if (sa != null)
        {
            try
            {
                var tp = sa.CreateInstance("DWSIM.Support.Initialization");
                tp?.GetType().GetMethod("Initialize")?.Invoke(tp, new object[] { this });
                Console.WriteLine("Support Initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error initializing Support: " + ex.Message);
            }
        }
    }

    private void LoadPlugins()
    {
        var pluginAssemblies = LoadPluginAssemblies();
        var pluginList = GetPlugins(pluginAssemblies);
        foreach (var ip in pluginList)
        {
            Console.WriteLine($"Loaded plugin library {ip.Name}");
            Plugins.Add(ip);
        }

        // Discover IUnitOperationExtension types in plugin + extender assemblies
        var uoExtAssemblies = new List<Assembly>(pluginAssemblies);
        try { uoExtAssemblies.AddRange(LoadExtenderDLLs()); } catch { }

        foreach (var asm in uoExtAssemblies)
        {
            try
            {
                foreach (var t in asm.GetExportedTypes())
                {
                    try
                    {
                        if (t.GetInterfaces().Contains(typeof(IUnitOperationExtension))
                            && !t.IsAbstract && !t.IsInterface)
                        {
                            var uoext = Activator.CreateInstance(t) as IUnitOperationExtension;
                            if (uoext != null &&
                                !DWSIM.FlowsheetBase.FlowsheetBase.AvailableUnitOperationExtensions.ContainsKey(uoext.Name))
                            {
                                DWSIM.FlowsheetBase.FlowsheetBase.AvailableUnitOperationExtensions.Add(uoext.Name, uoext);
                                Console.WriteLine($"Loaded unit operation extension: {uoext.Name}");
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    private List<Assembly> LoadPluginAssemblies()
    {
        var pluginAssemblyList = new List<Assembly>();

        // 1. plugins/ next to the executable
        var basePlugins = GetExtensionDirectory("plugins");
        if (Directory.Exists(basePlugins))
        {
            foreach (var fi in new DirectoryInfo(basePlugins).GetFiles("*.*", SearchOption.TopDirectoryOnly))
            {
                if (fi.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    fi.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    try { pluginAssemblyList.Add(Assembly.LoadFile(fi.FullName)); }
                    catch (Exception ex) { Console.WriteLine($"Error loading {fi.FullName}: {ex.Message}"); }
                }
            }
        }

        // 2. plugins/ in the user config directory
        var configPlugins = Path.Combine(GlobalSettings.Settings.GetConfigFileDir(), "plugins");
        if (Directory.Exists(configPlugins))
        {
            foreach (var fi in new DirectoryInfo(configPlugins).GetFiles("*.*", SearchOption.TopDirectoryOnly))
            {
                if (fi.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    fi.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    try { pluginAssemblyList.Add(Assembly.LoadFile(fi.FullName)); }
                    catch (Exception ex) { Console.WriteLine($"Error loading {fi.FullName}: {ex.Message}"); }
                }
            }
        }

        return pluginAssemblyList;
    }

    private static List<IUtilityPlugin5> GetPlugins(List<Assembly> assemblies)
    {
        var availableTypes = new List<Type>();
        foreach (var asm in assemblies)
        {
            try
            {
                availableTypes.AddRange(asm.GetExportedTypes());
                Console.WriteLine("Loaded plugin dll: " + asm);
            }
            catch (Exception ex) { Console.WriteLine("Error loading plugin dll: " + ex.Message); }
        }

        var pluginTypes = availableTypes.FindAll(t => t.GetInterfaces().Contains(typeof(IUtilityPlugin5)));
        var result = new List<IUtilityPlugin5>();
        foreach (var t in pluginTypes)
        {
            try
            {
                if (Activator.CreateInstance(t) is IUtilityPlugin5 p)
                    result.Add(p);
            }
            catch { }
        }
        return result;
    }

    /// <summary>
    /// Where an extension folder lives: beside the executable, which is where the Windows
    /// application keeps extenders, ppacks, unitops and plugins, and where the engine looks first.
    /// The folder one level up is kept as a second place to look, as the engine does.
    /// </summary>
    private static string GetExtensionDirectory(string name)
    {
        var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        var candidates = new[]
        {
            Path.Combine(exeDir, name),
            Path.Combine(Directory.GetParent(exeDir)?.FullName ?? exeDir, name),
            Path.Combine(GlobalSettings.Settings.GetConfigFileDir(), name)
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    /// <summary>
    /// Where the extensions are read from. DWSIM_EXTENDERS_DIR points it somewhere else, which is
    /// how an extension built for the Windows edition can be tried here without being copied over.
    /// </summary>
    private static string GetExtendersDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable("DWSIM_EXTENDERS_DIR");
        if (!string.IsNullOrEmpty(overridden)) return overridden;

        return GetExtensionDirectory("extenders");
    }

    private List<Assembly> LoadExtenderDLLs()
    {
        var extenderDlls = new List<Assembly>();

        var dir = GetExtendersDirectory();
        Console.WriteLine($"Loading extensions from {dir}");

        if (Directory.Exists(dir))
        {
            foreach (var fi in new DirectoryInfo(dir).GetFiles("*Extensions*.dll"))
            {
                try { extenderDlls.Add(Assembly.LoadFrom(fi.FullName)); }
                catch (Exception ex) { Console.WriteLine($"Error loading extender {fi.FullName}: {ex.Message}"); }
            }
        }
        return extenderDlls;
    }

    private static List<IExtenderCollection> GetExtenders(List<Assembly> assemblies)
    {
        var availableTypes = new List<Type>();
        foreach (var asm in assemblies)
        {
            try
            {
                availableTypes.AddRange(asm.GetExportedTypes());
                Console.WriteLine("Loaded extension dll: " + asm);
            }
            catch (Exception ex) { Console.WriteLine("Error loading extension dll: " + ex.Message); }
        }

        var extTypes = availableTypes.FindAll(t => t.GetInterfaces().Contains(typeof(IExtenderCollection)));
        var result = new List<IExtenderCollection>();
        foreach (var t in extTypes)
        {
            try
            {
                if (Activator.CreateInstance(t) is IExtenderCollection ec)
                    result.Add(ec);
            }
            catch { }
        }
        return result;
    }

    private void LoadExtenders()
    {
        List<IExtenderCollection> extList;
        try { extList = GetExtenders(LoadExtenderDLLs()); }
        catch { return; }

        foreach (var extender in extList)
        {
            Extenders.Add(extender);

            // only an initialization script runs on its own; everything else becomes a menu item
            // on the simulation window, which is where the menus and the active flowsheet are
            if (extender.Level == Interfaces.Enums.ExtenderLevel.MainWindow &&
                extender.Category == Interfaces.Enums.ExtenderCategory.InitializationScript)
            {
                foreach (var item in extender.Collection)
                {
                    try
                    {
                        if (item is IExtender6 ext6) ext6.SetFlowsheetGUI(this);
                        item.SetMainWindow(this);
                        item.Run();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error running extension {extender.DisplayText}: {ex}");
                    }
                }
            }

        }


    }



    // -------------------------------------------------------------------------
    // Helper types
    // -------------------------------------------------------------------------

    private sealed class SampleItem
    {
        public string DisplayName { get; }
        public string FilePath { get; }
        public SampleItem(string displayName, string filePath)
        {
            DisplayName = displayName;
            FilePath = filePath;
        }
        public override string ToString() => DisplayName;
    }
}
