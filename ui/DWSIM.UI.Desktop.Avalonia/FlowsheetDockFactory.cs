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

    // Exposed so FlowsheetWindow can show/hide panels via the dock API
    /// <summary>Live panel of each dockable id, used when reattaching a restored layout.</summary>
    public Dictionary<string, Control> ContentById { get; private set; } = new();

    public Tool? EditorTool { get; private set; }
    public Tool? PaletteTool { get; private set; }

    /// <summary>The panel a local page is shown on, created the first time one is asked for.</summary>
    public Tool? WebTool { get; private set; }
    public Tool? LogTool { get; private set; }
    public Tool? IntegratorTool { get; private set; }
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
        Control integratorContent)
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
            ["Integrator"] = integratorContent
        };
    }

    public override IRootDock CreateLayout()
    {
        // --- Left: Editor ---
        EditorTool = new Tool
        {
            Id = "Editor",
            Title = "Editor",
            Content = _editorContent,
            CanClose = false,
            CanPin = true,
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
            CanPin = true,
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
            CanPin = true,
            CanFloat = false,
            Proportion = 0.20
        };

        IntegratorTool = new Tool
        {
            Id = "Integrator",
            Title = "Integrator Controls",
            Content = _integratorContent,
            CanClose = false,
            CanPin = true,
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
            Proportion = 0.20,
            VisibleDockables = CreateList<IDockable>(LogTool, IntegratorTool),
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
    public void OpenWebTool(string title, Uri url)
    {
        var view = new global::AvaloniaWebView.WebView { Url = url };

        WebTool = new Tool
        {
            Id = "WebPanel",
            Title = title,
            Content = view,
            CanClose = true,
            CanPin = true,
            CanFloat = true,
            Proportion = 0.30
        };

        ContentById["WebPanel"] = view;

        var right = Find(d => d.Id == "RightDock").OfType<ToolDock>().FirstOrDefault();
        if (right == null) return;

        // the Windows interface gives the assistant a good third of the window
        right.Proportion = 0.30;
        right.VisibleDockables?.Add(WebTool);
        right.ActiveDockable = WebTool;
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
