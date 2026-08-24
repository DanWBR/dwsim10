using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia.Controls;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Dock.Avalonia factory that creates the IDE-style layout for FlowsheetWindow:
///   Left   : Editor panel (object properties)
///   Center : Document tabs — Flowsheet canvas, Results, Material Streams, Spreadsheet
///   Right  : Object palette
///   Bottom : Log / Dynamics Integrator
/// </summary>
public sealed class FlowsheetDockFactory : Factory
{
    // Pre-created controls injected by FlowsheetWindow
    private readonly Control _editorContent;
    private readonly Control _canvasContent;
    private readonly Control _paletteContent;
    private readonly Control _logContent;
    private readonly Control _resultsContent;
    private readonly Control _materialStreamsContent;
    private readonly Control _spreadsheetContent;
    private readonly Control _dynamicsManagerContent;
    private readonly Control _integratorContent;
    private readonly Control _watchContent;

    // Exposed so FlowsheetWindow can show/hide panels via the dock API
    /// <summary>Live panel of each dockable id, used when reattaching a restored layout.</summary>
    public Dictionary<string, Control> ContentById { get; private set; } = new();

    public Tool? EditorTool { get; private set; }
    public Tool? PaletteTool { get; private set; }

    /// <summary>The panel a local page is shown on, created the first time one is asked for.</summary>
    public Tool? WebTool { get; private set; }
    public Tool? LogTool { get; private set; }
    public Tool? IntegratorTool { get; private set; }
    public Tool? WatchTool { get; private set; }
    public Document? CanvasDocument { get; private set; }
    public Document? ResultsDocument { get; private set; }
    public Document? MaterialStreamsDocument { get; private set; }
    public Document? SpreadsheetDocument { get; private set; }
    public Document? DynamicsManagerDocument { get; private set; }

    public FlowsheetDockFactory(
        Control editorContent,
        Control canvasContent,
        Control paletteContent,
        Control logContent,
        Control resultsContent,
        Control materialStreamsContent,
        Control spreadsheetContent,
        Control dynamicsManagerContent,
        Control integratorContent,
        Control watchContent)
    {
        _editorContent = editorContent;
        _canvasContent = canvasContent;
        _paletteContent = paletteContent;
        _logContent = logContent;
        _resultsContent = resultsContent;
        _materialStreamsContent = materialStreamsContent;
        _spreadsheetContent = spreadsheetContent;
        _dynamicsManagerContent = dynamicsManagerContent;
        _integratorContent = integratorContent;
        _watchContent = watchContent;

        // content by dockable id, so a layout restored from the simulation file can have the
        // live panels put back into it: the serializer round-trips the tree, not the controls
        ContentById = new Dictionary<string, Control>
        {
            ["Editor"] = editorContent,
            ["Canvas"] = canvasContent,
            ["Palette"] = paletteContent,
            ["Log"] = logContent,
            ["Results"] = resultsContent,
            ["MaterialStreams"] = materialStreamsContent,
            ["Spreadsheet"] = spreadsheetContent,
            ["DynamicsManager"] = dynamicsManagerContent,
            ["Integrator"] = integratorContent,
            ["Watch"] = watchContent
        };
    }

    public override IRootDock CreateLayout()
    {
        // This is a fixed IDE layout: panels are shown and hidden by proportion from the View menu,
        // not by floating or pinning. Every tool keeps CanFloat and CanPin off - pinning (auto-hide)
        // re-hosted the live panel control and left its content blank after re-docking (issue #25), and
        // floating the assistant WebTool made its whole window vanish (the native WebView host does not
        // survive the move into a floating window).

        // --- Left: Editor ---
        EditorTool = new Tool
        {
            Id = "Editor",
            Title = "Editor",
            Content = _editorContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false,
            Proportion = 0.30
        };

        // --- Center: Document tabs ---
        CanvasDocument = new Document
        {
            Id = "Canvas",
            Title = "Flowsheet",
            Content = _canvasContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false
        };

        ResultsDocument = new Document
        {
            Id = "Results",
            Title = "Results",
            Content = _resultsContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false
        };

        MaterialStreamsDocument = new Document
        {
            Id = "MaterialStreams",
            Title = "Material Streams",
            Content = _materialStreamsContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false
        };

        SpreadsheetDocument = new Document
        {
            Id = "Spreadsheet",
            Title = "Spreadsheet",
            Content = _spreadsheetContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false
        };

        DynamicsManagerDocument = new Document
        {
            Id = "DynamicsManager",
            Title = "Dynamics Manager",
            Content = _dynamicsManagerContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false
        };

        // --- Right: Object palette ---
        PaletteTool = new Tool
        {
            Id = "Palette",
            Title = "Objects",
            Content = _paletteContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false,
            Proportion = 0.25
        };

        // --- Bottom: Log + Integrator Controls ---
        LogTool = new Tool
        {
            Id = "Log",
            Title = "Log",
            Content = _logContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false,
            Proportion = 0.20
        };

        IntegratorTool = new Tool
        {
            Id = "Integrator",
            Title = "Integrator Controls",
            Content = _integratorContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false,
            Proportion = 0.20
        };

        WatchTool = new Tool
        {
            Id = "Watch",
            Title = "Watch",
            Content = _watchContent,
            CanClose = false,
            CanPin = false,
            CanFloat = false,
            Proportion = 0.20
        };

        // --- Dock containers ---
        var leftDock = new ToolDock
        {
            Id = "LeftDock",
            Title = "Left",
            Alignment = Alignment.Left,
            Proportion = 0.20,
            VisibleDockables = CreateList<IDockable>(EditorTool),
            ActiveDockable = EditorTool
        };

        var documentDock = new DocumentDock
        {
            Id = "DocumentDock",
            Title = "Documents",
            IsCollapsable = false,
            CanCreateDocument = false,
            VisibleDockables = CreateList<IDockable>(
                CanvasDocument,
                ResultsDocument,
                MaterialStreamsDocument,
                SpreadsheetDocument,
                DynamicsManagerDocument),
            ActiveDockable = CanvasDocument
        };

        var rightDock = new ToolDock
        {
            Id = "RightDock",
            Title = "Right",
            Alignment = Alignment.Right,
            Proportion = 0.15,
            VisibleDockables = CreateList<IDockable>(PaletteTool),
            ActiveDockable = PaletteTool
        };

        var bottomDock = new ToolDock
        {
            Id = "BottomDock",
            Title = "Bottom",
            Alignment = Alignment.Bottom,
            Proportion = 0.30,
            VisibleDockables = CreateList<IDockable>(LogTool, IntegratorTool, WatchTool),
            ActiveDockable = LogTool
        };

        // --- Proportional layout ---
        // Horizontal: [editor | splitter | documents | splitter | palette]
        var horizontalDock = new ProportionalDock
        {
            Id = "HorizontalDock",
            Title = "Horizontal",
            Orientation = Dock.Model.Core.Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftDock,
                new ProportionalDockSplitter(),
                documentDock,
                new ProportionalDockSplitter(),
                rightDock)
        };

        // Vertical: [horizontal-row | splitter | bottom-log]
        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Title = "Main",
            Orientation = Dock.Model.Core.Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                horizontalDock,
                new ProportionalDockSplitter(),
                bottomDock)
        };

        var rootDock = (RootDock)CreateRootDock();
        rootDock.Id = "Root";
        rootDock.IsCollapsable = false;
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        rootDock.DefaultDockable = mainLayout;
        rootDock.ActiveDockable = mainLayout;

        return rootDock;
    }

    /// <summary>Adds a panel holding a browser view to the dock on the right and shows it.</summary>
    /// <remarks>
    /// The dock does not hide an inactive tool, it takes its control out of the visual tree, and the
    /// browser control builds its native view once and never again, so a tab switched away and back
    /// leaves it blank. The panel therefore holds a plain host, and a fresh browser is put into it
    /// each time the panel actually becomes visible. The teardown is deferred one turn so the burst
    /// of attach and detach the dock does while laying itself out does not keep rebuilding it.
    /// </remarks>
    public void OpenWebTool(string title, Uri url)
    {
        var host = new Decorator();
        var attached = false;
        var browserFallback = false;

        host.AttachedToVisualTree += (_, _) =>
        {
            attached = true;
            if (host.Child != null || browserFallback) return;
            try
            {
                host.Child = new global::AvaloniaWebView.WebView { Url = url };
            }
            catch (Exception)
            {
                // No usable embedded browser on this machine, e.g. a Linux install without
                // WebKitGTK. This construction is deferred to the attach handler, so the caller's
                // own try/catch never sees it; open the page in the system browser here, once.
                browserFallback = true;
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(url.ToString()) { UseShellExecute = true });
                }
                catch { }
            }
        };

        host.DetachedFromVisualTree += (_, _) =>
        {
            attached = false;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // still gone a turn later: a real switch away, so free the native browser
                if (attached || host.Child == null) return;
                (host.Child as IDisposable)?.Dispose();
                host.Child = null;
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        };

        WebTool = new Tool
        {
            Id = "WebPanel",
            Title = title,
            Content = host,
            CanClose = true,
            CanPin = false,
            CanFloat = false,
            Proportion = 0.30
        };

        ContentById["WebPanel"] = host;

        var right = Find(d => d.Id == "RightDock").OfType<ToolDock>().FirstOrDefault();
        if (right == null) return;

        // the Windows interface gives the assistant a good third of the window
        right.Proportion = 0.30;
        right.VisibleDockables?.Add(WebTool);
        right.ActiveDockable = WebTool;
    }

    /// <summary>Brings the watch panel forward, making sure the bottom dock is open.</summary>
    public void ShowWatch()
    {
        if (WatchTool == null) return;

        var bottom = Find(d => d.Id == "BottomDock").OfType<ToolDock>().FirstOrDefault();
        if (bottom == null) return;

        if (bottom.Proportion <= 0.01) bottom.Proportion = 0.20;
        bottom.ActiveDockable = WatchTool;
    }

    /// <summary>Brings the panel forward when it is already there.</summary>
    public void ShowWebTool()
    {
        if (WebTool == null) return;

        var right = Find(d => d.Id == "RightDock").OfType<ToolDock>().FirstOrDefault();
        if (right == null) return;

        if (right.VisibleDockables?.Contains(WebTool) != true)
            right.VisibleDockables?.Add(WebTool);

        right.ActiveDockable = WebTool;
    }
}
