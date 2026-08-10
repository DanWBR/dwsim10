using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia;
using DWSIM.Drawing.SkiaSharp;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UI.Desktop.Avalonia.Controls;
using DWSIM.UI.Shared.Avalonia;
using SkiaSharp;
using unvell.ReoGrid;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Per-simulation flowsheet window.
/// Avalonia equivalent of DWSIM.UI.Forms.Forms.Flowsheet (Flowsheet.eto.cs).
///
/// In Phase 2, the canvas uses placeholder callbacks and the object palette lists
/// are populated with static names. Engine wiring (GraphicsSurface, FlowsheetObject)
/// is added when the engine projects are ported to .NET 8.
/// </summary>
public partial class FlowsheetView : UserControl
{
    // -------------------------------------------------------------------------
    // Host hooks — the view lives inside a document tab, so the window it is
    // shown in owns the dialogs, and closing is the host's decision
    // -------------------------------------------------------------------------

    /// <summary>
    /// The window this view is being shown in; child windows are owned by it. A document tab
    /// materialises one layout pass after it is added, so until then the shell window stands in.
    /// </summary>
    public Window HostWindow
    {
        get
        {
            if (TopLevel.GetTopLevel(this) is Window window) return window;

            return Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow!
                : null!;
        }
    }

    /// <summary>
    /// The menu bar of this flowsheet. It is detached from the view and shown by the main
    /// window while this document is the active one.
    /// </summary>
    public Menu FlowsheetMenu => MainMenuBar;

    /// <summary>Opens the setup wizard over this simulation, for the welcome screen.</summary>
    public void ShowSetupWizard()
    {
        if (_flowsheet == null) return;
        new SimulationSetupWizard(_flowsheet).Show(HostWindow);
    }

    /// <summary>Opens the data regression tool over this simulation, for the welcome screen.</summary>
    public void ShowDataRegression(bool loadFromFile)
    {
        if (_flowsheet == null) return;

        var window = new DataRegressionWindow(_flowsheet);
        if (loadFromFile) window.PromptLoadCase();
        window.Show(HostWindow);
    }

    /// <summary>Raised when the simulation name changes, so the host can retitle the tab.</summary>
    public event EventHandler? TitleChanged;

    /// <summary>Raised when the flowsheet asks to be closed, from the File menu.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Raised by File &gt; New, so the host opens another document.</summary>
    public event EventHandler? NewRequested;

    /// <summary>Raised by File &gt; Open, so the host opens another document.</summary>
    public event EventHandler? OpenRequested;

    /// <summary>The host is asked to open one of the recently used files.</summary>
    public event EventHandler<string>? OpenRecentRequested;

    // -------------------------------------------------------------------------
    // Engine callbacks — set by the host once the engine is ready
    // -------------------------------------------------------------------------

    /// <summary>Called every frame: (SKSurface, SKImageInfo) → draw the flowsheet.</summary>
    public Action<SkiaSharp.SKSurface, SkiaSharp.SKImageInfo>? PaintCallback
    {
        get => Canvas.PaintCallback;
        set => Canvas.PaintCallback = value;
    }

    /// <summary>Called on pointer down at (x, y) in device pixels.</summary>
    public Action<int, int>? InputPressCallback
    {
        get => Canvas.InputPressCallback;
        set => Canvas.InputPressCallback = value;
    }

    /// <summary>Called on pointer release.</summary>
    public Action? InputReleaseCallback
    {
        get => Canvas.InputReleaseCallback;
        set => Canvas.InputReleaseCallback = value;
    }

    /// <summary>Called on pointer move at (x, y) in device pixels.</summary>
    public Action<int, int>? InputMoveCallback
    {
        get => Canvas.InputMoveCallback;
        set => Canvas.InputMoveCallback = value;
    }

    /// <summary>Zoom/pan callback: (deltaY, ptrX, ptrY, canvasW, canvasH).</summary>
    public Action<double, int, int, int, int>? WheelCallback
    {
        get => Canvas.WheelCallback;
        set => Canvas.WheelCallback = value;
    }

    // -------------------------------------------------------------------------
    // Phase 3: editor factory — set by the host (.NET Framework bootstrap)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Optional callback set by the host to provide real editor content.
    /// Given an object name, returns an ObjectEditorDescriptor with Avalonia Controls.
    /// If null, CreatePlaceholder() is used (Phase 2 behaviour).
    ///
    /// Usage in the .NET Framework bootstrap:
    ///   flwWin.EditorDescriptorFactory = name => {
    ///       var obj = flowsheet.SimulationObjects[name];
    ///       var panel = new AvaloniaEditorPanel();
    ///       obj.PopulateEditorPanel(panel);          // UO casts panel to AvaloniaEditorPanel
    ///       return new ObjectEditorDescriptor { PropertiesContent = panel, ShowConnections = true, ... };
    ///   };
    /// </summary>
    public Func<string, ObjectEditorDescriptor>? EditorDescriptorFactory { get; set; }

    /// <summary>
    /// Name of the simulation object last clicked on the canvas.
    /// Set by the host's InputReleaseCallback or InputPressCallback.
    /// </summary>
    public string? LastClickedObjectName { get; set; }

    // -------------------------------------------------------------------------
    // Simulation name
    // -------------------------------------------------------------------------

    private string _simulationName = "Untitled";

    public string SimulationName
    {
        get => _simulationName;
        set
        {
            _simulationName = value;
            TitleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // -------------------------------------------------------------------------
    // Extension containers (mirrors Eto FlowsheetForm containers for
    // DWSIM.Support and other extensions to inject panels via reflection)
    // -------------------------------------------------------------------------

    public StackPanel TopContainer => TopContainerPanel;
    public StackPanel BottomContainer => BottomContainerPanel;
    public StackPanel LeftContainer => LeftContainerPanel;
    public StackPanel RightContainer => RightContainerPanel;

    // -------------------------------------------------------------------------
    // Phase 6: engine state (set once a simulation file is loaded)
    // -------------------------------------------------------------------------

    private AvaloniaFlowsheet? _flowsheet;
    private GraphicsSurface? _surface;

    /// <summary>
    /// Public accessor matching the Eto FlowsheetForm.FlowsheetObject property.
    /// Used by extensions (IExtender.SetFlowsheet) and plugins (IUtilityPlugin5.SetFlowsheet).
    /// </summary>
    public DWSIM.Interfaces.IFlowsheet? FlowsheetObject => _flowsheet;

    // Connect mode state
    private bool _connectMode = false;
    private DWSIM.Interfaces.IGraphicObject? _connectSource = null;

    // Copy/paste clipboard
    private (DWSIM.Interfaces.Enums.GraphicObjects.ObjectType type, float x, float y)? _clipboard = null;

    // Backup timer
    private System.Timers.Timer? _backupTimer;

    // -------------------------------------------------------------------------
    // Controls created in code (hosted inside Dock.Avalonia panels)
    // -------------------------------------------------------------------------

    private readonly FlowsheetCanvas Canvas;
    private readonly EditorHolder EditorHolder;
    private readonly StackPanel PaletteStack;
    private readonly ResultsViewerPanel ResultsPanel;
    private readonly MaterialStreamListPanel MaterialStreamsPanel;
    private readonly LogPanel LogList;

    private SpreadsheetPanel? _spreadsheet;
    private readonly ReoGridControl SpreadsheetGrid;
    private readonly DynamicsManagerPanel DynManagerPanel;
    private readonly DynamicsIntegratorPanel IntegratorPanel;

    // Dock factory (to show/hide panels programmatically)
    private FlowsheetDockFactory? _dockFactory;
    private Dock.Model.Core.IDock? _dockLayout;
    private DockControl? _dockControl;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public FlowsheetView()
    {
        // Create controls that will live inside the dock panels
        Canvas = new FlowsheetCanvas();
        EditorHolder = new EditorHolder();

        PaletteStack = new StackPanel { Spacing = 0 };

        ResultsPanel = new ResultsViewerPanel();
        MaterialStreamsPanel = new MaterialStreamListPanel();
        LogList = new LogPanel();
        SpreadsheetGrid = new ReoGridControl();
        DynManagerPanel = new DynamicsManagerPanel();
        IntegratorPanel = new DynamicsIntegratorPanel();
        IntegratorPanel.OnIntegratorStep = () => { Canvas.Refresh(); UpdateResultsPanel(); };
        IntegratorPanel.OnViewResults = WriteIntegratorResultsToSpreadsheet;
        WireCapeOpenSelector();
        WireDataRegressionLauncher();
        WirePropertyPackageConfigurator();

        InitializeComponent();
        SetupDockLayout();
        WireToolbar();
        WireMenus();
        WireMenuIcons();
        WireCanvas();
        WireSubToolbar();
        WireToolbarGroups();
        PopulatePalette();
        SetupBackupTimer();
        // closing is confirmed by the host through ConfirmCloseAsync()

        // the menu bar belongs to the main window, which shows the one of the active
        // flowsheet, so it is taken out of the view's own layout here
        RootPanel.Children.Remove(MainMenuBar);

        EditorHolder.CloseRequested += (_, _) =>
        {
            // Hide the editor tool via dock (instead of IsVisible on a Border)
            if (_dockFactory?.EditorTool != null)
                _dockFactory.EditorTool.IsActive = false;
        };
    }

    // -------------------------------------------------------------------------
    // Dock layout setup
    // -------------------------------------------------------------------------

    private void SetupDockLayout()
    {
        // Build palette content: header + scrollable items
        var paletteContent = new DockPanel();
        var paletteHeader = new TextBlock
        {
            Text = "Objects",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(6, 6, 6, 4)
        };
        DockPanel.SetDock(paletteHeader, global::Avalonia.Controls.Dock.Top);
        paletteContent.Children.Add(paletteHeader);
        paletteContent.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = PaletteStack
        });

        // Create the dock factory with pre-built controls
        // Center document tabs: Flowsheet canvas, Results, Material Streams, Spreadsheet
        // Bottom tool panel: Log (and later Dynamics Integrator)
        _dockFactory = new FlowsheetDockFactory(
            editorContent: EditorHolder,
            canvasContent: Canvas,
            paletteContent: paletteContent,
            logContent: LogList,
            resultsContent: ResultsPanel,
            materialStreamsContent: MaterialStreamsPanel,
            spreadsheetContent: SpreadsheetGrid,
            dynamicsManagerContent: DynManagerPanel,
            integratorContent: IntegratorPanel);

        var layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(layout);
        _dockLayout = layout;

        _dockControl = new DockControl
        {
            Layout = layout,
            Factory = _dockFactory,
            InitializeFactory = false,   // already called InitLayout
            InitializeLayout = false
        };

        MainDockHost.Child = _dockControl;
    }

    // -------------------------------------------------------------------------
    // Canvas events
    // -------------------------------------------------------------------------

    private void WireCanvas()
    {
        // Open editor on left-click release (matches Eto Flowsheet.eto.cs single-click behavior).
        // InputReleased fires after InputReleaseCallback has finalized selection and set LastClickedObjectName.
        Canvas.InputReleased += (_, _) =>
        {
            var name = LastClickedObjectName;
            if (!string.IsNullOrEmpty(name) && _flowsheet != null)
            {
                OpenEditorFor(name!);
            }
        };

        KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                HandleDelete();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _connectMode)
            {
                ExitConnectMode();
                e.Handled = true;
            }
            else if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                HandleCopy();
                e.Handled = true;
            }
            else if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                HandlePaste();
                e.Handled = true;
            }
            else if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Select All
                if (_flowsheet != null && _surface != null)
                {
                    _surface.SelectedObjects.Clear();
                    foreach (var obj in _flowsheet.SimulationObjects.Values)
                        if (obj.GraphicObject != null)
                            _surface.SelectedObjects[obj.Name] = obj.GraphicObject;
                    Canvas.Refresh();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                // F5 = Solve
                await SolveAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.F6)
            {
                BtnCalculatorActive.IsChecked = !BtnCalculatorActive.IsChecked.GetValueOrDefault();
                e.Handled = true;
            }
            else if (e.Key == Key.F7)
            {
                BtnSimultAdjust.IsChecked = !BtnSimultAdjust.IsChecked.GetValueOrDefault();
                e.Handled = true;
            }
            else if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                     e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                // Ctrl+Shift+S = Save As
                await SaveAsAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.N && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+N = New simulation
                await NewSimulationAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+O = Open simulation
                await OpenSimulationAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+Z = Undo
                if (_flowsheet != null)
                {
                    try
            {
                _flowsheet.ProcessUndo();
                Canvas.Refresh();
                UpdateResultsPanel();
                if (DWSIM.GlobalSettings.Settings.UndoRedoRecalculateFlowsheet)
                    _flowsheet.RequestCalculation();
            }
                    catch (Exception ex) { AppendLog($"Undo error: {ex.Message}"); }
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Ctrl+Y = Redo
                if (_flowsheet != null)
                {
                    try
            {
                _flowsheet.ProcessRedo();
                Canvas.Refresh();
                UpdateResultsPanel();
                if (DWSIM.GlobalSettings.Settings.UndoRedoRecalculateFlowsheet)
                    _flowsheet.RequestCalculation();
            }
                    catch (Exception ex) { AppendLog($"Redo error: {ex.Message}"); }
                }
                e.Handled = true;
            }
        };

        // Must use AddHandler with handledEventsToo: true because the canvas's
        // own OnPointerReleased sets e.Handled = true (standard routed-event pattern).
        Canvas.AddHandler(PointerReleasedEvent, (object? _, PointerReleasedEventArgs e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right)
            {
                ShowCanvasContextMenu();
                e.Handled = true;
            }
        }, handledEventsToo: true);

        Canvas.PaletteItemDropped += (name, x, y) =>
        {
            if (_flowsheet == null) return;
            var (ox, oy) = CanvasToObject(x, y);
            AddPaletteObject(name, ox, oy);
        };

        // Double-click with modifiers (matches Eto Flowsheet.eto.cs behavior):
        //   No modifier:   Edit/View properties
        //   Shift:          Edit connections
        //   Alt:            View results
        //   Ctrl:           Debug object
        Canvas.InputDoubleClick += (mods) =>
        {
            var obj = _surface?.SelectedObject;
            if (obj == null || _flowsheet == null) return;
            var simObj = _flowsheet.SimulationObjects.ContainsKey(obj.Name)
                ? _flowsheet.SimulationObjects[obj.Name] : null;
            if (simObj == null) return;

            if (mods.HasFlag(KeyModifiers.Shift))
            {
                // Edit connections
                OpenEditorFor(obj.Name);
                AppendLog($"Edit connections for '{obj.Tag}'.");
            }
            else if (mods.HasFlag(KeyModifiers.Alt))
            {
                // View results - copy properties to log
                var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
                var sb = new StringBuilder();
                sb.AppendLine($"--- Results: {obj.Tag} ({obj.ObjectType}) ---");
                foreach (var prop in simObj.GetProperties(Interfaces.Enums.PropertyType.ALL))
                {
                    try
                    {
                        var val = simObj.GetPropertyValue(prop, su);
                        var unit = simObj.GetPropertyUnit(prop, su);
                        sb.AppendLine($"  {prop} = {val} {unit}");
                    }
                    catch { }
                }
                AppendLog(sb.ToString());
            }
            else if (mods.HasFlag(KeyModifiers.Control))
            {
                // Debug - trigger the debug context menu action
                AppendLog($"Debug mode for '{obj.Tag}' - use right-click > Debug Object.");
            }
            else
            {
                // Default: open editor
                OpenEditorFor(obj.Name);
            }
        };
    }

    private static ObjectType? PaletteNameToObjectType(string name) => name switch
    {
        // Static names (used by fallback palette and context menus)
        "Material Stream"      => ObjectType.MaterialStream,
        "Energy Stream"        => ObjectType.EnergyStream,
        "Information Stream"   => ObjectType.OT_InformationCarrier,
        "Information Carrier"  => ObjectType.OT_InformationCarrier,
        "Mixer"                => ObjectType.NodeIn,
        "Stream Mixer"         => ObjectType.NodeIn,
        "Splitter"             => ObjectType.NodeOut,
        "Stream Splitter"      => ObjectType.NodeOut,
        "Valve"                => ObjectType.Valve,
        "Pump"                 => ObjectType.Pump,
        "Compressor"           => ObjectType.Compressor,
        "Expander"             => ObjectType.Expander,
        "Expander (Turbine)"   => ObjectType.Expander,
        "Heater"               => ObjectType.Heater,
        "Cooler"               => ObjectType.Cooler,
        "Heat Exchanger"       => ObjectType.HeatExchanger,
        "Flash Vessel"         => ObjectType.Vessel,
        "Gas-Liquid Separator" => ObjectType.Vessel,
        "Filter"               => ObjectType.Filter,
        "Pipe Segment"         => ObjectType.Pipe,
        "Short-cut Column"     => ObjectType.ShortcutColumn,
        "Shortcut Column"      => ObjectType.ShortcutColumn,
        "Distillation Column"  => ObjectType.DistillationColumn,
        "Absorption Column"    => ObjectType.AbsorptionColumn,
        "Absorption/Extraction Column" => ObjectType.AbsorptionColumn,
        "Refluxed Absorber"    => ObjectType.RefluxedAbsorber,
        "Reboiled Absorber"    => ObjectType.ReboiledAbsorber,
        "CSTR Reactor"         => ObjectType.RCT_CSTR,
        "Continuous Stirred Tank Reactor (CSTR)" => ObjectType.RCT_CSTR,
        "PFR Reactor"          => ObjectType.RCT_PFR,
        "Plug-Flow Reactor (PFR)" => ObjectType.RCT_PFR,
        "Conversion Reactor"   => ObjectType.RCT_Conversion,
        "Equilibrium Reactor"  => ObjectType.RCT_Equilibrium,
        "Gibbs Reactor"        => ObjectType.RCT_Gibbs,
        "Gibbs Reactor (Reaktoro)" => ObjectType.RCT_Gibbs,
        "Recycle"              => ObjectType.OT_Recycle,
        "Recycle Block"        => ObjectType.OT_Recycle,
        "Energy Recycle"       => ObjectType.OT_EnergyRecycle,
        "Energy Recycle Block" => ObjectType.OT_EnergyRecycle,
        "Adjust"               => ObjectType.OT_Adjust,
        "Controller Block"     => ObjectType.OT_Adjust,
        "Spec"                 => ObjectType.OT_Spec,
        "Specification Block"  => ObjectType.OT_Spec,
        "PID Controller"       => ObjectType.Controller_PID,
        "Switch"               => ObjectType.Switch,
        "Input"                => ObjectType.Input,
        "Input Box"            => ObjectType.Input,
        "Text Block"           => ObjectType.GO_Text,
        "Property Table"       => ObjectType.GO_Table,
        "Chart Object"         => ObjectType.GO_Chart,
        "Spreadsheet Table"    => ObjectType.GO_SpreadsheetTable,
        "Master Table"         => ObjectType.GO_MasterTable,
        "Spreadsheet"          => ObjectType.GO_SpreadsheetTable,
        "Compound Separator"   => ObjectType.ComponentSeparator,
        "Tank"                 => ObjectType.Tank,
        "Orifice Plate"        => ObjectType.OrificePlate,
        "Analog Gauge"         => ObjectType.AnalogGauge,
        "Digital Gauge"        => ObjectType.DigitalGauge,
        "Level Gauge"          => ObjectType.LevelGauge,
        "Wind Turbine"         => ObjectType.WindTurbine,
        "Water Electrolyzer"   => ObjectType.WaterElectrolyzer,
        "Solar Panel"          => ObjectType.SolarPanel,
        "Hydroelectric Turbine" => ObjectType.HydroelectricTurbine,
        _                      => null
    };

    /// <summary>
    /// Adds an object to the flowsheet from the palette.
    /// Tries the engine's string-based AddObject (uses ObjectList) first,
    /// falls back to the static ObjectType mapping.
    /// </summary>
    private void AddPaletteObject(string name, int x, int y)
    {
        if (_flowsheet == null) return;

        // Try engine ObjectList first (works with engine display names)
        if (_flowsheet.AvailableSimulationObjects.ContainsKey(name))
        {
            try
            {
                _flowsheet.AddObject(name, x, y);
                Canvas.Refresh();
                UpdateResultsPanel();
                return;
            }
            catch { }
        }

        // Fallback to ObjectType-based mapping
        var type = PaletteNameToObjectType(name);
        if (type == null) return;
        _flowsheet.AddObject(type.Value, x, y, name);
        Canvas.Refresh();
        UpdateResultsPanel();
    }

    /// <summary>
    /// Opens (or brings to front) the editor for the given object.
    /// Uses EditorDescriptorFactory when set; falls back to CreatePlaceholder.
    /// </summary>
    public void OpenEditorFor(string objectName)
    {
        ObjectEditorContainer editor;
        AvaloniaEditorPanel? editorPanel = null;

        if (EditorDescriptorFactory != null)
        {
            var descriptor = EditorDescriptorFactory(objectName);
            editor = new ObjectEditorContainer(objectName, descriptor);
            editorPanel = descriptor.PropertiesContent as AvaloniaEditorPanel;
        }
        else
        {
            editor = ObjectEditorContainer.CreatePlaceholder(objectName, objectName);
        }

        EditorHolder.OpenEditor(editor);

        // Show the display name (Tag) instead of the internal GUID name
        if (_flowsheet != null && _flowsheet.SimulationObjects.TryGetValue(objectName, out var simObj))
        {
            var tag = simObj.GraphicObject?.Tag ?? objectName;
            EditorHolder.SetDisplayName(tag);
        }
        else if (_surface != null)
        {
            // annotations are not simulation objects, so their tag comes off the surface
            var graphic = _surface.DrawingObjects.FirstOrDefault(o => o.Name == objectName);
            if (graphic != null) EditorHolder.SetDisplayName(graphic.Tag);
        }

        // Arm OnAfterEdit after ALL deferred visual-tree events have settled.
        // When controls enter the tree, Avalonia fires deferred TextChanged /
        // SelectionChanged events via DispatcherOperations. Until ArmAfterEdit()
        // is called, the OnAfterEdit getter returns null, so those deferred events
        // cannot trigger RequestCalculation.
        //
        // Two-level post: the outer post at Background priority runs after all
        // Normal-priority events (where Avalonia queues TextChanged). The inner
        // post ensures we run after any events that were queued during the first
        // Background pass.
        if (editorPanel != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Dispatcher.UIThread.Post(() => editorPanel.ArmAfterEdit(),
                    DispatcherPriority.Background);
            }, DispatcherPriority.Background);
        }
    }

    public void CloseAllEditors()
    {
        EditorHolder.CloseAll();
    }

    /// <summary>
    /// Re-populates the editor panel for the currently selected canvas object.
    /// Invoked when the engine signals UpdateOpenEditForms (e.g. after a solve)
    /// so calculated outputs become visible without the user having to reselect.
    /// </summary>
    private void RefreshSelectedObjectEditor()
    {
        var name = _surface?.SelectedObject?.Name;
        if (!string.IsNullOrEmpty(name)) OpenEditorFor(name!);
    }

    // -------------------------------------------------------------------------
    // Phase 6: simulation file open + engine wiring
    // -------------------------------------------------------------------------

    private async Task OpenSimulationAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this) ?? throw new InvalidOperationException("No top level");

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        SetStatus("Loading simulation...");
        AppendLog($"Opening {Path.GetFileName(path)}");

        try
        {
            await LoadSimulationAsync(path);
            AppendLog("Simulation loaded successfully.");
        }
        catch (Exception ex)
        {
            AppendLog($"Error loading simulation: {ex.Message}", DWSIM.Interfaces.IFlowsheet.MessageType.GeneralError);
            SetStatus("Load failed.");
        }
    }

    private async Task LoadSimulationAsync(string path)
    {
        var fs = new AvaloniaFlowsheet();
        fs.OnLogMessage = (msg, type, eid) => AppendLog(msg, type, eid);
        fs.OnUpdateInterface = () =>
        {
            Canvas.Refresh();
            // open object editors hold live grids; this is the engine's post-solve signal
            DWSIM.UI.Desktop.Editors.MaterialStreamTabbedEditor.RefreshAll();
        };
        fs.OnUpdateOpenEditForms = RefreshSelectedObjectEditor;
        fs.OnCloseOpenEditForms = CloseAllEditors;
        // an extension asking for a page from a local server gets a docked panel here
        fs.OnWebPanelRequested = ShowWebPanel;

        // Wire spreadsheet BEFORE file load: LoadSpreadsheetData callback
        // is invoked during LoadFromXML/LoadZippedXML on the background thread.
        WireSpreadsheet(fs);

        var dlg = new LoadingDialog("Loading Simulation", $"Reading {Path.GetFileName(path)}...");
        dlg.Show(HostWindow);

        try
        {
            await Task.Run(() =>
            {
                fs.Initialize();
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".dwxmz" || ext == ".zip")
                    fs.LoadZippedXML(path);
                else if (ext == ".dwxml" || ext == ".xml")
                    fs.LoadFromXML(System.Xml.Linq.XDocument.Load(path));
            });

            _flowsheet = fs;
            // Without this, Save silently degrades to Save As for every opened file.
            _flowsheet.FilePath = path;
            if (_flowsheet.Options != null) _flowsheet.Options.FilePath = path;
            _surface = (GraphicsSurface)fs.GetSurface();
            _surface.Flowsheet = _flowsheet;

            // the floating property tables and the anchored lists draw nothing for an object
            // type that has no visible properties configured; the WinForms UI seeds them when
            // a flowsheet is created, so do the same here
            DWSIM.UI.Desktop.Editors.FlowsheetObjectTypes.EnsureDefaults(_flowsheet);

            WireEngineSurface(_surface);
            MaterialStreamsPanel.SetFlowsheet(fs);
            ResultsPanel.SetFlowsheet(fs);
            IntegratorPanel.SetFlowsheet(fs);
            DynManagerPanel.SetFlowsheet(fs);
            PopulatePalette(); // re-populate with icons from ObjectList

            SimulationName = fs.Options?.SimulationName
                             ?? Path.GetFileNameWithoutExtension(path);
            SetStatus("Ready");
            _surface.Center((int)(Canvas.Bounds.Width * GlobalSettings.Settings.DpiScale), (int)(Canvas.Bounds.Height * GlobalSettings.Settings.DpiScale));
            _surface.ZoomAll((int)(Canvas.Bounds.Width * GlobalSettings.Settings.DpiScale), (int)(Canvas.Bounds.Height * GlobalSettings.Settings.DpiScale));
            Canvas.Refresh();
            UpdateResultsPanel();
            LoadFlowsheetExtensions();
            LoadFlowsheetPlugins();
        }
        finally
        {
            dlg.Finish();
        }
    }

    // -------------------------------------------------------------------------
    // Script editor
    // -------------------------------------------------------------------------

    private void OpenScriptEditor()
    {
        var editor = new ScriptEditorWindow();

        // the scripts belong to the simulation, so the manager edits them in place and they are
        // written to the file with everything else
        if (_flowsheet != null) editor.SetFlowsheet(_flowsheet);

        editor.RunRequested += (_, script) => RunScript(editor, script, background: false);
        editor.RunAsyncRequested += (_, script) => RunScript(editor, script, background: true);

        editor.StopRequested += (_, _) =>
        {
            DWSIM.GlobalSettings.Settings.CalculatorStopRequested = true;
        };

        editor.Show(HostWindow);
    }

    /// <summary>
    /// Runs a script of the simulation, sending whatever it prints and whatever the flowsheet
    /// reports while it runs to the manager's output pane.
    /// </summary>
    private void RunScript(ScriptEditorWindow editor, DWSIM.Interfaces.IScript script, bool background)
    {
        if (_flowsheet == null)
        {
            editor.AppendOutput("No simulation loaded.");
            editor.NotifyRunCompleted();
            return;
        }

        Task.Run(() =>
        {
            var sb = new StringBuilder();

            // IronPython writes through the flowsheet log, which reports on OnMessage; only
            // Python.NET prints to the console, so that is the one worth redirecting, and
            // redirecting it for both would show every line twice
            var console = script.PythonInterpreter == DWSIM.Interfaces.Enums.Scripts.Interpreter.Python_NET;

            var prevOut = Console.Out;
            using var sw = new StringWriter(sb);
            if (console) Console.SetOut(sw);

            Action<string> prevMsg = _flowsheet.OnMessage!;
            _flowsheet.OnMessage = msg =>
            {
                sb.AppendLine(msg);
                prevMsg?.Invoke(msg);
            };

            try
            {
                if (background) _flowsheet.RunScriptAsync(script.ID);
                else _flowsheet.RunScript(script.ID);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error: {ex.Message}");
            }
            finally
            {
                _flowsheet.OnMessage = prevMsg;
                if (console) Console.SetOut(prevOut);
            }

            var output = sb.ToString();

            Dispatcher.UIThread.Post(() =>
            {
                if (!string.IsNullOrEmpty(output)) editor.AppendOutput(output);
                editor.NotifyRunCompleted();
                AppendLog("Script execution complete.");
            });
        });
    }

    // Public entry points used by MainWindow and command-line bootstrap
    public Task NewAsync()    => NewSimulationAsync();

    /// <summary>Creates the simulation without the setup wizard, for the hosts that open it themselves.</summary>
    public Task NewWithoutWizardAsync() => NewSimulationAsync(showWizard: false);
    public Task LoadAsync(string path) => LoadSimulationAsync(path);

    private async Task NewSimulationAsync(bool showWizard = true)
    {
        var fs = new AvaloniaFlowsheet();
        fs.OnLogMessage = (msg, type, eid) => AppendLog(msg, type, eid);
        fs.OnUpdateInterface = () =>
        {
            Canvas.Refresh();
            // open object editors hold live grids; this is the engine's post-solve signal
            DWSIM.UI.Desktop.Editors.MaterialStreamTabbedEditor.RefreshAll();
        };
        fs.OnUpdateOpenEditForms = RefreshSelectedObjectEditor;
        fs.OnCloseOpenEditForms = CloseAllEditors;
        // an extension asking for a page from a local server gets a docked panel here
        fs.OnWebPanelRequested = ShowWebPanel;

        var dlg = new LoadingDialog("New Simulation", "Loading databases...");
        dlg.Show(HostWindow);

        try
        {
            await Task.Run(() => fs.Initialize());
        }
        finally
        {
            dlg.Finish();
        }

        _flowsheet = fs;
        _surface = (GraphicsSurface)fs.GetSurface();
        _surface.Flowsheet = _flowsheet;

        // the floating property tables and the anchored lists draw nothing for an object
        // type that has no visible properties configured; the WinForms UI seeds them when
        // a flowsheet is created, so do the same here
        DWSIM.UI.Desktop.Editors.FlowsheetObjectTypes.EnsureDefaults(_flowsheet);

        WireEngineSurface(_surface);
        WireSpreadsheet(fs);
        MaterialStreamsPanel.SetFlowsheet(fs);
        ResultsPanel.SetFlowsheet(fs);
        IntegratorPanel.SetFlowsheet(fs);
        DynManagerPanel.SetFlowsheet(fs);
        PopulatePalette(); // re-populate with icons from ObjectList

        ApplyPreferredUnitSystem(fs);

        CloseAllEditors();
        SimulationName = "Untitled";
        SetStatus("Ready");
        Canvas.Refresh();
        UpdateResultsPanel();
        LoadFlowsheetExtensions();
        LoadFlowsheetPlugins();
        AppendLog("New simulation created.");

        if (showWizard)
        {
            var wizard = new SimulationSetupWizard(fs);
            wizard.Show();
        }
    }

    private async Task SaveSimulationAsync(string path)
    {
        if (_flowsheet == null) return;

        SetStatus("Saving...");
        AppendLog($"Saving to {Path.GetFileName(path)}...");

        try
        {
            await Task.Run(() =>
            {
                var xdoc = _flowsheet.SaveToXML();

                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".dwxmz" || ext == ".zip")
                {
                    var xmlfile = Path.ChangeExtension(Path.GetTempFileName(), "xml");
                    try
                    {
                        xdoc.Save(xmlfile);
                        using var zip = new ZipArchive(File.Create(path), ZipArchiveMode.Create);
                        zip.CreateEntryFromFile(xmlfile, Path.GetFileName(xmlfile),
                            CompressionLevel.Optimal);
                    }
                    finally
                    {
                        try { File.Delete(xmlfile); } catch { }
                    }
                }
                else
                {
                    xdoc.Save(path);
                }

                _flowsheet.FilePath = path;
                _flowsheet.Options.FilePath = path;
            });

            SimulationName = Path.GetFileNameWithoutExtension(path);
            AppendLog($"Saved to {Path.GetFileName(path)}.");
            SetStatus("Ready");
            RecentFilesManager.Add(path);
        }
        catch (Exception ex)
        {
            AppendLog($"Save error: {ex.Message}");
            SetStatus("Save failed.");
        }
    }

    private async Task SaveAsAsync()
    {
        if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }

        var topLevel = TopLevel.GetTopLevel(this) ?? throw new InvalidOperationException("No top level");

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save DWSIM Simulation",
            DefaultExtension = "dwxmz",
            SuggestedFileName = SimulationName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("DWSIM Simulation (compressed)") { Patterns = new[] { "*.dwxmz" } },
                new FilePickerFileType("DWSIM Simulation (XML)")        { Patterns = new[] { "*.dwxml" } },
            }
        });

        if (file == null) return;
        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        await SaveSimulationAsync(path);
    }

    private Task SolveAsync() => SolveAsync(customOrder: false);

    private async Task SolveAsync(bool customOrder)
    {
        if (_flowsheet == null)
        {
            AppendLog("No simulation loaded.");
            return;
        }

        SetStatus(customOrder ? "Solving (custom order)..." : "Solving...");
        AppendLog(customOrder ? "Solve started (custom calculation order)." : "Solve started.");
        DWSIM.GlobalSettings.Settings.CalculatorStopRequested = false;

        var dlg = new LoadingDialog("Solving Flowsheet", "Running calculation order. This may take a while for large flowsheets.");
        dlg.Show(HostWindow);

        try
        {
            List<Exception> errors;
            if (customOrder)
                errors = await Task.Run(() => _flowsheet.RequestCalculationAndWait());
            else
                errors = await Task.Run(() => _flowsheet.RequestCalculationAndWait());

            if (errors.Count == 0)
            {
                AppendLog("Solve complete.");
                SetStatus("Ready");
            }
            else
            {
                foreach (var ex in errors)
                    AppendLog($"Error: {ex.Message}", DWSIM.Interfaces.IFlowsheet.MessageType.GeneralError);
                SetStatus("Solve finished with errors.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Solver exception: {ex.Message}", DWSIM.Interfaces.IFlowsheet.MessageType.GeneralError);
            SetStatus("Solver error.");
        }
        finally
        {
            dlg.Finish();
            Canvas.Refresh();
            UpdateResultsPanel();
            SolveRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void WireEngineSurface(GraphicsSurface surface)
    {
        // the toolbar toggles and the state selector belong to the flowsheet being opened
        BtnCalculatorActive.IsChecked = DWSIM.GlobalSettings.Settings.CalculatorActivated;
        BtnSimultAdjust.IsChecked = _flowsheet?.FlowsheetOptions.SimultaneousAdjustSolverEnabled ?? false;
        MenuSimultAdjust.IsChecked = BtnSimultAdjust.IsChecked.GetValueOrDefault();
        MenuCalculatorActive.IsChecked = BtnCalculatorActive.IsChecked.GetValueOrDefault();
        BtnDynamicsMode.IsChecked = _flowsheet?.DynamicMode ?? false;
        RefreshStoredSolutions();

        // the engine places the floating tables against its own idea of the viewport size,
        // which otherwise stays at the 1024x768 default
        void SyncSurfaceSize()
        {
            var size = Canvas.DeviceSize;
            if (size.Width > 0 && size.Height > 0)
                surface.Size = new SkiaSharp.SKSize(size.Width, size.Height);
        }

        Canvas.SizeChanged += (_, _) => SyncSurfaceSize();
        SyncSurfaceSize();

        PaintCallback = (surf, _) =>
        {
            SyncSurfaceSize();
            surface.UpdateSurface(surf);
        };

        InputPressCallback = (x, y) => surface.InputPress(x, y);

        InputReleaseCallback = () =>
        {
            surface.InputRelease();
            LastClickedObjectName = surface.SelectedObject?.Name;
        };

        InputMoveCallback = (x, y) =>
        {
            surface.InputMove(x, y);
            Canvas.Refresh();
        };

        WheelCallback = (delta, px, py, cw, ch) =>
        {
            surface.Zoom = Math.Clamp(surface.Zoom + (float)(delta > 0 ? 0.1 : -0.1), 0.1f, 10f);
            Canvas.Refresh();
        };

        // Wire the Avalonia editor factory so clicking a flowsheet object
        // shows its properties panel populated by the real engine data.
        var factory = new DWSIM.UI.Desktop.Editors.AvaloniaEditorFactory(_flowsheet!);
        // an annotation changes nothing in the process, so editing one only has to repaint
        factory.RedrawRequested = () => Canvas.Refresh();
        EditorDescriptorFactory = factory.CreateDescriptor;
    }

    // -------------------------------------------------------------------------
    // Spreadsheet wiring
    // -------------------------------------------------------------------------

    /// <summary>
    /// Create the SpreadsheetPanel, point SpreadsheetGrid at it, and wire
    /// flowsheet callbacks so the engine can evaluate/read spreadsheet data.
    /// </summary>
    private void WireSpreadsheet(AvaloniaFlowsheet fs)
    {
        _spreadsheet = new SpreadsheetPanel(fs);

        // Replace the Spreadsheet document tab content with the grid and its toolbar. The map by
        // id has to point at it as well, or a layout restored from the file puts the empty
        // placeholder grid back into the tab.
        if (_dockFactory != null)
        {
            var spreadsheet = new SpreadsheetToolbar(_spreadsheet, fs);
            _dockFactory.ContentById["Spreadsheet"] = spreadsheet;
            if (_dockFactory.SpreadsheetDocument != null)
                _dockFactory.SpreadsheetDocument.Content = spreadsheet;
        }

        // Spreadsheet context menu (Import Data, Export Data, Create Chart)
        SetupSpreadsheetContextMenu(fs);

        // Wire engine callbacks for data retrieval
        fs.RetrieveSpreadsheetData = range =>
        {
            try { return _spreadsheet.GetDataFromRange(range); }
            catch { return new System.Collections.Generic.List<string[]>(); }
        };
        fs.RetrieveSpreadsheetFormat = range =>
        {
            try { return _spreadsheet.GetFormatFromRange(range); }
            catch { return new System.Collections.Generic.List<string[]>(); }
        };

        // LoadSpreadsheetData callback - invoked by engine during LoadFromXML/LoadZippedXML.
        // Runs on a background thread, so dispatch UI work to the Avalonia UI thread.
        fs.LoadSpreadsheetData = xdoc =>
        {
            try
            {
                var root = xdoc.Element("DWSIM_Simulation_Data");
                var spreadsheetEl = root?.Element("Spreadsheet");
                if (spreadsheetEl == null) return;

                var rgfEl = spreadsheetEl.Element("RGFData");
                if (rgfEl != null && !string.IsNullOrEmpty(rgfEl.Value))
                {
                    // RGF format: JSON dictionary of worksheet name -> RGF XML JSON
                    var rgfdata = rgfEl.Value.Replace("Calibri", "Arial").Replace("10.25", "10");
                    var sdict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(rgfdata);
                    if (sdict != null && sdict.Count > 0)
                    {
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            _spreadsheet.Grid.RemoveWorksheet(0);
                            _spreadsheet.Loaded = false;
                            foreach (var item in sdict)
                            {
                                try
                                {
                                    var sheet = _spreadsheet.Grid.NewWorksheet(item.Key);
                                    var xmldoc = Newtonsoft.Json.JsonConvert.DeserializeXmlNode(item.Value);
                                    var tmpfile = DWSIM.SharedClasses.Utility.GetTempFileName();
                                    xmldoc!.Save(tmpfile);
                                    sheet.LoadRGF(tmpfile);
                                    System.IO.File.Delete(tmpfile);
                                }
                                catch { }
                            }
                            _spreadsheet.Loaded = true;
                            if (_spreadsheet.Grid.Worksheets.Count > 0)
                                _spreadsheet.Grid.CurrentWorksheet = _spreadsheet.Grid.Worksheets[0];
                        });
                    }
                }
                else
                {
                    // Legacy format: Data1/Data2 pipe-separated strings
                    var data1 = spreadsheetEl.Element("Data1")?.Value ?? "";
                    var data2 = spreadsheetEl.Element("Data2")?.Value ?? "";
                    if (!string.IsNullOrEmpty(data1))
                        _spreadsheet.CopyDT1FromString(data1);
                    if (!string.IsNullOrEmpty(data2))
                        _spreadsheet.CopyDT2FromString(data2);
                    if (!string.IsNullOrEmpty(data1) || !string.IsNullOrEmpty(data2))
                    {
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            _spreadsheet.CopyFromDT();
                            _spreadsheet.EvaluateAll();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Spreadsheet load warning: {ex.Message}", DWSIM.Interfaces.IFlowsheet.MessageType.Warning);
            }
        };

        // SaveSpreadsheetData callback - invoked by engine during SaveToXML
        // the panel layout travels in the simulation file, in its own section
        fs.SaveLayoutData = xdoc =>
        {
            try
            {
                Dispatcher.UIThread.Invoke(() => DockLayoutState.Save(xdoc, _dockLayout));
            }
            catch (Exception ex)
            {
                AppendLog("Could not store the panel layout: " + ex.Message, DWSIM.Interfaces.IFlowsheet.MessageType.Warning);
            }
        };

        fs.LoadLayoutData = xdoc =>
        {
            try
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    var restored = DockLayoutState.Load(xdoc);
                    if (restored == null || _dockFactory == null || _dockControl == null) return;

                    DockLayoutState.ReattachContent(restored, _dockFactory.ContentById);
                    _dockFactory.InitLayout(restored);
                    _dockControl.Layout = restored;
                    _dockLayout = restored;
                });
            }
            catch (Exception ex)
            {
                AppendLog("Could not restore the panel layout, keeping the default one: " + ex.Message, DWSIM.Interfaces.IFlowsheet.MessageType.Warning);
            }
        };

        fs.SaveSpreadsheetData = xdoc =>
        {
            try
            {
                var root = xdoc.Element("DWSIM_Simulation_Data");
                if (root == null) return;

                root.Add(new System.Xml.Linq.XElement("Spreadsheet"));
                root.Element("Spreadsheet")!.Add(new System.Xml.Linq.XElement("RGFData"));

                Dispatcher.UIThread.Invoke(() =>
                {
                    var sdict = new Dictionary<string, string>();
                    foreach (var sheet in _spreadsheet.Grid.Worksheets)
                    {
                        var tmpfile = DWSIM.SharedClasses.Utility.GetTempFileName();
                        sheet.SaveRGF(tmpfile);
                        var xmldoc = new System.Xml.XmlDocument();
                        xmldoc.Load(tmpfile);
                        sdict.Add(sheet.Name, Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmldoc));
                        System.IO.File.Delete(tmpfile);
                    }
                    root.Element("Spreadsheet")!.Element("RGFData")!.Value =
                        Newtonsoft.Json.JsonConvert.SerializeObject(sdict);
                });
            }
            catch (Exception ex)
            {
                AppendLog($"Spreadsheet save warning: {ex.Message}", DWSIM.Interfaces.IFlowsheet.MessageType.Warning);
            }
        };
    }

    // -------------------------------------------------------------------------
    // Spreadsheet context menu
    // -------------------------------------------------------------------------

    private void SetupSpreadsheetContextMenu(AvaloniaFlowsheet fs)
    {
        if (_spreadsheet == null) return;

        var menuImport = new MenuItem { Header = "Import Data (GETPROPVAL)", Icon = IconHelper.MIcon("⬇") }; // down arrow
        var menuExport = new MenuItem { Header = "Export Data (SETPROPVAL)", Icon = IconHelper.MIcon("⬆") }; // up arrow

        menuImport.Click += async (_, _) =>
        {
            var dlg = new PropertySelectorDialog(fs);
            await dlg.ShowDialog(HostWindow);
            if (!dlg.Confirmed || dlg.SelectedObjectId == null || dlg.SelectedPropertyKey == null) return;

            var ws = _spreadsheet.Grid.CurrentWorksheet;
            var cell = ws.Cells[ws.SelectionRange.StartPos];
            var units = dlg.SelectedUnit ?? "";
            cell.Formula = string.Format("GETPROPVAL(\"{0}\";\"{1}\";\"{2}\")",
                dlg.SelectedObjectId, dlg.SelectedPropertyKey, units);
        };

        menuExport.Click += async (_, _) =>
        {
            var dlg = new PropertySelectorDialog(fs);
            await dlg.ShowDialog(HostWindow);
            if (!dlg.Confirmed || dlg.SelectedObjectId == null || dlg.SelectedPropertyKey == null) return;

            var ws = _spreadsheet.Grid.CurrentWorksheet;
            var scell = ws.Cells[ws.SelectionRange.StartPos];
            var currdata = scell.Formula ?? scell.Data?.ToString() ?? "";
            var units = dlg.SelectedUnit ?? "";
            scell.Formula = string.Format("SETPROPVAL(\"{0}\";\"{1}\";\"{2}\";\"{3}\")",
                dlg.SelectedObjectId, dlg.SelectedPropertyKey, currdata, units);
        };

        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(menuImport);
        contextMenu.Items.Add(menuExport);

        _spreadsheet.Grid.ContextMenu = contextMenu;
    }

    // -------------------------------------------------------------------------
    // Log / status helpers
    // -------------------------------------------------------------------------

    public void AppendLog(string message)
    {
        AppendLog(message, DWSIM.Interfaces.IFlowsheet.MessageType.Information);
    }

    /// <summary>The engine reports through here: the kind of message and the exception, if any.</summary>
    public void AppendLog(string message, DWSIM.Interfaces.IFlowsheet.MessageType type,
                          string exceptionId = "")
    {
        if (Dispatcher.UIThread.CheckAccess())
            LogList.Add(message, type, exceptionId);
        else
            Dispatcher.UIThread.Post(() => LogList.Add(message, type, exceptionId));
    }

    public void SetStatus(string text)
    {
        if (Dispatcher.UIThread.CheckAccess())
            StatusLabel.Text = text;
        else
            Dispatcher.UIThread.Post(() => StatusLabel.Text = text);
    }

    public void SetZoom(float zoom) => ZoomLabel.Text = $"Zoom: {zoom * 100:F0}%";

    // -------------------------------------------------------------------------
    // Menu icons
    // -------------------------------------------------------------------------

    private void WireMenuIcons()
    {
        // File
        IconHelper.Set(MenuNew,    "\U0001F4C4"); // page
        IconHelper.Set(MenuOpen,   "\U0001F4C2"); // open folder
        IconHelper.Set(MenuSave,   "\U0001F4BE"); // floppy
        IconHelper.Set(MenuSaveAs, "\U0001F4BE");
        IconHelper.Set(MenuSolve,       "▶");
        IconHelper.Set(MenuSolveCustom, "⏩");
        IconHelper.Set(MenuStop,        "⏹");
        IconHelper.Set(MenuClose,  "✖");      // heavy X

        // Edit
        IconHelper.Set(MenuUndo,   "↩");      // left hook arrow
        IconHelper.Set(MenuRedo,   "↪");      // right hook arrow
        IconHelper.Set(MenuCut,    "✂");      // scissors
        IconHelper.Set(MenuCopy,   "\U0001F4CB");  // clipboard
        IconHelper.Set(MenuPaste,  "\U0001F4CC");  // pushpin
        IconHelper.Set(MenuClone,  "❐");      // copy squares
        IconHelper.Set(MenuSelectAll, "☐");   // ballot box
        IconHelper.Set(MenuInvertSelection, "↔"); // left-right arrow
        IconHelper.Set(MenuDeselectAll, "☒"); // ballot box with X
        IconHelper.Set(MenuDelete, "\U0001F5D1");  // waste basket
        IconHelper.Set(MenuDisconnectAll, "⛓"); // chains
        IconHelper.Set(MenuAddObject, "➕");   // plus
        IconHelper.Set(MenuSimSettings, "⚙"); // gear
        IconHelper.Set(MenuGlobalSettings, "\U0001F527"); // wrench

        // Solver

        // Dynamics
        IconHelper.Set(MenuDynamicsToggle, "⚡");
        IconHelper.Set(MenuDynManager,     "\U0001F4CA"); // chart
        IconHelper.Set(MenuDynIntegrator,  "⏱");     // stopwatch
        IconHelper.Set(MenuDynPIDTuning,   "\U0001F39B"); // control knobs

        // View
        IconHelper.Set(MenuShowEditor,     "\U0001F4DD"); // memo
        IconHelper.Set(MenuShowPalette,    "\U0001F3A8"); // palette
        IconHelper.Set(MenuShowResults,    "\U0001F4C3"); // page with curl
        IconHelper.Set(MenuShowSubToolbar, "\U0001F527"); // wrench
        IconHelper.Set(MenuCloseAllEditors, "✖");
        IconHelper.Set(MenuZoomIn,   "\U0001F50D"); // magnifying glass
        IconHelper.Set(MenuZoomOut,  "\U0001F50D");
        IconHelper.Set(MenuZoomFit,  "⬜");     // white large square
        IconHelper.Set(MenuZoomReset,"1⃣"); // 1 keycap

        // Tools
        IconHelper.Set(MenuSensitivity,    "\U0001F4C8"); // chart increasing
        IconHelper.Set(MenuOptimizer,      "\U0001F3AF"); // target
        IconHelper.Set(MenuPropertyChart,  "\U0001F4CA"); // bar chart
        IconHelper.Set(MenuBalance,        "⚖");     // scales
        IconHelper.Set(MenuInspector,      "\U0001F50E"); // magnifying glass right
        IconHelper.Set(MenuCreateCompound, "\U0001F9EA"); // test tube
        IconHelper.Set(MenuScripts,        "\U0001F4DC"); // scroll
        IconHelper.Set(MenuReactions,      "⚗");     // alembic
        IconHelper.Set(MenuUOExtManager,   "\U0001F50C"); // plug

        // Utilities
        IconHelper.Set(MenuUtilTCP, "\U0001F4D0"); // triangular ruler
        IconHelper.Set(MenuUtilPE,  "\U0001F4C8");
        IconHelper.Set(MenuUtilBE,  "\U0001F4C8");

        // Results
        IconHelper.Set(MenuMarkdownReport, "\U0001F4DD"); // memo

        // Help
        IconHelper.Set(MenuHelpHtml,    "\U0001F4D6"); // open book
        IconHelper.Set(MenuHelpSupport, "❤");     // heart
        IconHelper.Set(MenuHelpBug,     "\U0001F41B"); // bug
        IconHelper.Set(MenuHelpWebsite, "\U0001F310"); // globe
        IconHelper.Set(MenuAbout,       "ℹ");     // info
    }

    // -------------------------------------------------------------------------
    // Toolbar wiring
    // -------------------------------------------------------------------------

    private void WireToolbar()
    {
        BtnSave.Click += (_, _) => MenuSave.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        BtnSimSettings.Click += (_, _) => MenuSimSettings.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        BtnUndo.Click += (_, _) => MenuUndo.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        BtnRedo.Click += (_, _) => MenuRedo.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        BtnZoomIn.Click += (_, _) => ZoomIn();
        BtnZoomOut.Click += (_, _) => ZoomOut();
        BtnZoomFit.Click += (_, _) => ZoomFit();
        BtnZoomReset.Click += (_, _) => ZoomReset();

        BtnDelete.Click += (_, _) => HandleDelete();
        BtnConnect.IsCheckedChanged += (_, _) =>
        {
            _connectMode = BtnConnect.IsChecked.GetValueOrDefault();
            if (_connectMode)
                SetStatus("Connect mode: click the source object");
            else
                ExitConnectMode();
        };

        BtnInspector.IsCheckedChanged += (_, _) =>
        {
            var enabled = BtnInspector.IsChecked.GetValueOrDefault();
            DWSIM.GlobalSettings.Settings.InspectorEnabled = enabled;
            if (enabled)
                DWSIM.GlobalSettings.Settings.EnableParallelProcessing = false;
            AppendLog($"Inspector {(enabled ? "enabled" : "disabled")}.");
        };
    }

    // -------------------------------------------------------------------------
    // Sub-toolbar (grid, snap, alignment, search)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Toolbar buttons that mirror the WinForms groups: the settings shortcuts, the calculator
    /// and dynamic mode toggles, and the flowsheet state store.
    /// </summary>
    private void WireToolbarGroups()
    {
        BtnUnitOpsExt.Click += (_, _) => MenuUOExtManager.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        BtnDynManager.Click += (_, _) => MenuDynManager.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        BtnDynIntegrator.Click += (_, _) => MenuDynIntegrator.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        BtnCalculatorActive.IsChecked = DWSIM.GlobalSettings.Settings.CalculatorActivated;
        MenuCalculatorActive.IsChecked = BtnCalculatorActive.IsChecked.GetValueOrDefault();
        BtnCalculatorActive.IsCheckedChanged += (_, _) =>
        {
            var on = BtnCalculatorActive.IsChecked.GetValueOrDefault();
            DWSIM.GlobalSettings.Settings.CalculatorActivated = on;
            MenuCalculatorActive.IsChecked = on;
            AppendLog("Flowsheet calculator " + (on ? "activated." : "deactivated."));
        };
        MenuCalculatorActive.Click += (_, _) =>
            BtnCalculatorActive.IsChecked = MenuCalculatorActive.IsChecked;

        MenuSolve.Click += async (_, _) => await SolveAsync();
        MenuSolveCustom.Click += async (_, _) => await SolveAsync(customOrder: true);
        MenuStop.Click += (_, _) => BtnStop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        BtnDynamicsMode.IsCheckedChanged += (_, _) =>
        {
            if (_flowsheet == null) return;
            if (_flowsheet.DynamicMode == BtnDynamicsMode.IsChecked.GetValueOrDefault()) return;
            MenuDynamicsToggle.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        };

        BtnStoreSolution.Click += (_, _) =>
        {
            if (_flowsheet == null) return;
            var id = "State " + (_flowsheet.StoredSolutions.Count + 1) + " (" + DateTime.Now.ToString("HH:mm:ss") + ")";
            _flowsheet.StoredSolutions.Add(id, _flowsheet.GetProcessData());
            RefreshStoredSolutions();
            CbStoredSolutions.SelectedItem = id;
            AppendLog("Stored the current flowsheet state as '" + id + "'.");
        };

        BtnLoadSolution.Click += (_, _) =>
        {
            if (_flowsheet == null || CbStoredSolutions.SelectedItem is not string id) return;
            if (!_flowsheet.StoredSolutions.ContainsKey(id)) return;
            _flowsheet.LoadProcessData(_flowsheet.StoredSolutions[id]);
            _flowsheet.UpdateInterface();
            Canvas.Refresh();
            AppendLog("Restored the flowsheet state '" + id + "'.");
        };

        BtnDeleteSolution.Click += (_, _) =>
        {
            if (_flowsheet == null || CbStoredSolutions.SelectedItem is not string id) return;
            _flowsheet.StoredSolutions.Remove(id);
            RefreshStoredSolutions();
            AppendLog("Removed the flowsheet state '" + id + "'.");
        };
    }

    /// <summary>Repopulates the stored state selector from the flowsheet.</summary>
    private void RefreshStoredSolutions()
    {
        var selected = CbStoredSolutions.SelectedItem as string;
        var items = _flowsheet == null ? new List<string>() : _flowsheet.StoredSolutions.Keys.ToList();
        CbStoredSolutions.ItemsSource = items;
        if (selected != null && items.Contains(selected)) CbStoredSolutions.SelectedItem = selected;
        else if (items.Count > 0) CbStoredSolutions.SelectedIndex = 0;
    }

    private void WireSubToolbar()
    {
        ChkDrawGrid.IsCheckedChanged += (_, _) =>
        {
            if (_surface != null)
                _surface.ShowGrid = ChkDrawGrid.IsChecked.GetValueOrDefault();
            Canvas.Refresh();
        };

        ChkSnapToGrid.IsCheckedChanged += (_, _) =>
        {
            if (_surface != null)
                _surface.SnapToGrid = ChkSnapToGrid.IsChecked.GetValueOrDefault();
        };

        ChkMultiSelect.IsCheckedChanged += (_, _) =>
        {
            if (_surface != null)
                _surface.MultiSelectMode = ChkMultiSelect.IsChecked.GetValueOrDefault();
        };

        // Alignment buttons
        BtnAlignLeft.Click   += (_, _) => AlignSelected(GraphicsSurface.AlignDirection.Lefts);
        BtnAlignCenter.Click += (_, _) => AlignSelected(GraphicsSurface.AlignDirection.Centers);
        BtnAlignRight.Click  += (_, _) => AlignSelected(GraphicsSurface.AlignDirection.Rights);
        BtnAlignTop.Click    += (_, _) => AlignSelected(GraphicsSurface.AlignDirection.Tops);
        BtnAlignMiddle.Click += (_, _) => AlignSelected(GraphicsSurface.AlignDirection.Middles);
        BtnAlignBottom.Click += (_, _) => AlignSelected(GraphicsSurface.AlignDirection.Bottoms);
        BtnEqualizeH.Click   += (_, _) => AlignSelected(GraphicsSurface.AlignDirection.EqualizeHorizontal);
        BtnEqualizeV.Click   += (_, _) => AlignSelected(GraphicsSurface.AlignDirection.EqualizeVertical);

        // Search
        TbSearch.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Return) SearchObject(TbSearch.Text);
        };
    }

    private void AlignSelected(GraphicsSurface.AlignDirection direction)
    {
        if (_surface == null) return;
        try
        {
            _surface.AlignSelectedObjects(direction);
            Canvas.Refresh();
        }
        catch (Exception ex) { AppendLog($"Align error: {ex.Message}"); }
    }

    private void SearchObject(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || _surface == null || _flowsheet == null) return;

        foreach (var go in _flowsheet.GraphicObjects.Values)
        {
            if (go.Tag != null && go.Tag.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                var sgo = go as DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject;
                _surface.SelectedObject = sgo;
                // Center view on found object
                if (sgo != null)
                {
                    var cw = (int)Canvas.Bounds.Width;
                    var ch = (int)Canvas.Bounds.Height;
                    if (cw > 0 && ch > 0)
                    {
                        _surface.OffsetAll(
                            -(sgo.X - cw / (2 * _surface.Zoom)),
                            -(sgo.Y - ch / (2 * _surface.Zoom)));
                    }
                }
                Canvas.Refresh();
                AppendLog($"Found '{go.Tag}'.");
                return;
            }
        }
        AppendLog($"Object '{query}' not found.");
    }

    /// <summary>
    /// Puts the new simulation in the system of units the settings ask for. A simulation read
    /// from a file keeps the one it was saved with.
    /// </summary>
    private static void ApplyPreferredUnitSystem(AvaloniaFlowsheet fs)
    {
        var preferred = DWSIM.GlobalSettings.Settings.PreferredSystemOfUnits;
        if (string.IsNullOrEmpty(preferred)) return;

        var system = fs.AvailableSystemsOfUnits.FirstOrDefault(x => x.Name == preferred);
        if (system != null) fs.FlowsheetOptions.SelectedUnitSystem = system;
    }

    // -------------------------------------------------------------------------
    // Backup timer
    // -------------------------------------------------------------------------

    private void SetupBackupTimer()
    {
        if (!DWSIM.GlobalSettings.Settings.EnableBackupCopies) return;
        var interval = DWSIM.GlobalSettings.Settings.BackupInterval;
        if (interval <= 0) interval = 5;

        _backupTimer = new System.Timers.Timer(interval * 60 * 1000);
        _backupTimer.Elapsed += (_, _) => Dispatcher.UIThread.Post(SaveBackupCopy);
        _backupTimer.AutoReset = true;
        _backupTimer.Start();
    }

    private void SaveBackupCopy()
    {
        if (_flowsheet == null) return;
        try
        {
            var backupDir = BackupRecoveryWindow.ResolveBackupFolder();
            Directory.CreateDirectory(backupDir);

            var fname = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}_{SimulationName}.dwxmz";
            var path = Path.Combine(backupDir, fname);

            var xdoc = _flowsheet.SaveToXML();
            var xmlfile = Path.ChangeExtension(Path.GetTempFileName(), "xml");
            try
            {
                xdoc.Save(xmlfile);
                using var zip = new ZipArchive(File.Create(path), ZipArchiveMode.Create);
                zip.CreateEntryFromFile(xmlfile, Path.GetFileName(xmlfile), CompressionLevel.Optimal);
            }
            finally
            {
                try { File.Delete(xmlfile); } catch { }
            }

            System.Diagnostics.Debug.WriteLine($"[Backup] Saved {path}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Backup] Error: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Menu wiring
    // -------------------------------------------------------------------------

    private void WireMenus()
    {
        MenuClose.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        MenuAbout.Click += async (_, _) => await ShowAboutDialogAsync();
        MenuDelete.Click += (_, _) => HandleDelete();

        MenuSelectAll.Click += (_, _) =>
        {
            if (_flowsheet == null || _surface == null) return;
            _surface.SelectedObjects.Clear();
            foreach (var obj in _flowsheet.SimulationObjects.Values)
                if (obj.GraphicObject != null)
                    _surface.SelectedObjects[obj.Name] = obj.GraphicObject;
            Canvas.Refresh();
            AppendLog($"Selected {_surface.SelectedObjects.Count} objects.");
        };

        MenuInvertSelection.Click += (_, _) =>
        {
            if (_flowsheet == null || _surface == null) return;
            var allNames = new HashSet<string>();
            foreach (var obj in _flowsheet.SimulationObjects.Values)
                if (obj.GraphicObject != null) allNames.Add(obj.Name);
            var currentlySelected = new HashSet<string>(_surface.SelectedObjects.Keys);
            _surface.SelectedObjects.Clear();
            foreach (var name in allNames)
            {
                if (!currentlySelected.Contains(name))
                    _surface.SelectedObjects[name] = _flowsheet.SimulationObjects[name].GraphicObject;
            }
            Canvas.Refresh();
        };

        MenuDeselectAll.Click += (_, _) =>
        {
            if (_surface == null) return;
            _surface.SelectedObjects.Clear();
            _surface.SelectedObject = null;
            Canvas.Refresh();
        };

        MenuCut.Click += (_, _) =>
        {
            HandleCopy();
            HandleDelete();
        };

        MenuClone.Click += (_, _) =>
        {
            if (_surface == null || _flowsheet == null) return;
            var obj = _surface.SelectedObject;
            if (obj == null) return;
            var simObj = _flowsheet.SimulationObjects.ContainsKey(obj.Name) ? _flowsheet.SimulationObjects[obj.Name] : null;
            if (simObj == null) return;
            try
            {
                var cloned = (Interfaces.ISimulationObject)simObj.CloneXML();
                cloned.Name = Guid.NewGuid().ToString();
                cloned.GraphicObject.Tag = obj.Tag + " (Clone)";
                cloned.GraphicObject.X += 50;
                cloned.GraphicObject.Y += 50;
                _flowsheet.AddGraphicObject(cloned.GraphicObject);
                _flowsheet.SimulationObjects.Add(cloned.Name, cloned);
                Canvas.Refresh();
                UpdateResultsPanel();
                AppendLog($"Cloned '{obj.Tag}'.");
            }
            catch (Exception ex) { AppendLog($"Clone error: {ex.Message}"); }
        };

        MenuCopy.Click           += (_, _) => HandleCopy();
        MenuPaste.Click          += (_, _) => HandlePaste();
        MenuDisconnectAll.Click  += (_, _) => HandleDisconnectAll();

        // Add Object sub-items
        MenuAddMaterialStream.Click    += (_, _) => AddObjectAtCenter(ObjectType.MaterialStream, "Material Stream");
        MenuAddEnergyStream.Click      += (_, _) => AddObjectAtCenter(ObjectType.EnergyStream, "Energy Stream");
        // annotations have no simulation object behind them and take the other entry point
        MenuAddText.Click              += (_, _) => AddAnnotationAtCenter(ObjectType.GO_Text);
        MenuAddHTMLText.Click          += (_, _) => AddAnnotationAtCenter(ObjectType.GO_HTMLText);
        MenuAddRectangle.Click         += (_, _) => AddAnnotationAtCenter(ObjectType.GO_Rectangle);
        MenuAddButton.Click            += (_, _) => AddAnnotationAtCenter(ObjectType.GO_Button);
        MenuAddImage.Click             += async (_, _) => await AddImageAsync();
        MenuAddTable.Click             += (_, _) => AddAnnotationAtCenter(ObjectType.GO_Table);
        MenuAddMasterTable.Click       += (_, _) => AddAnnotationAtCenter(ObjectType.GO_MasterTable);
        MenuAddSpreadsheetTable.Click  += (_, _) => AddAnnotationAtCenter(ObjectType.GO_SpreadsheetTable);
        MenuAddChart.Click             += (_, _) => AddAnnotationAtCenter(ObjectType.GO_Chart);

        // Global Settings
        MenuGlobalSettings.Click += async (_, _) => await new PreferencesWindow().ShowDialog(HostWindow);

        // Simultaneous Adjust, on the toolbar as in the WinForms UI
        BtnSimultAdjust.IsCheckedChanged += (_, _) =>
        {
            MenuSimultAdjust.IsChecked = BtnSimultAdjust.IsChecked.GetValueOrDefault();
            if (_flowsheet == null) return;
            _flowsheet.FlowsheetOptions.SimultaneousAdjustSolverEnabled =
                BtnSimultAdjust.IsChecked.GetValueOrDefault();
            var state = _flowsheet.FlowsheetOptions.SimultaneousAdjustSolverEnabled ? "enabled" : "disabled";
            AppendLog($"Simultaneous Adjust Solver {state}.");
        };
        MenuSimultAdjust.Click += (_, _) =>
            BtnSimultAdjust.IsChecked = MenuSimultAdjust.IsChecked;

        // View: Close All Editors, Sub-Toolbar
        MenuCloseAllEditors.Click += (_, _) => CloseAllEditors();
        MenuShowSubToolbar.Click += (_, _) =>
            SubToolbarBorder.IsVisible = MenuShowSubToolbar.IsChecked;

        // Utilities
        MenuUtilTCP.Click += (_, _) => OpenUtilityWindow(Interfaces.Enums.FlowsheetUtility.TrueCriticalPoint);
        MenuUtilPE.Click  += (_, _) => OpenUtilityWindow(Interfaces.Enums.FlowsheetUtility.PhaseEnvelope);
        MenuUtilBE.Click  += (_, _) => OpenUtilityWindow(Interfaces.Enums.FlowsheetUtility.PhaseEnvelopeBinary);
        MenuUnitSystems.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new UnitSystemEditorWindow(_flowsheet).Show(HostWindow);
        };
        MenuPetroleumChar.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new PetroleumCharacterizationWindow(_flowsheet).Show(HostWindow);
        };
        MenuDistCurve.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new DistillationCurveWindow(_flowsheet).Show(HostWindow);
        };
        MenuAssayManager.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new AssayManagerWindow(_flowsheet).Show(HostWindow);
        };
        MenuBulkPseudos.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new BulkPseudocompoundsWindow(_flowsheet).Show(HostWindow);
        };
        MenuUtilLLE.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new LLEEnvelopeWindow(_flowsheet).Show(HostWindow);
        };
        MenuPureComp.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new PureCompoundPropertiesWindow(_flowsheet).Show(HostWindow);
        };
        MenuHydrates.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new HydratesWindow(_flowsheet).Show(HostWindow);
        };
        MenuColdFlow.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new ColdFlowPropertiesWindow(_flowsheet).Show(HostWindow);
        };
        MenuSepSizing.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new SeparatorSizingWindow(_flowsheet).Show(HostWindow);
        };
        MenuPsvSizing.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new PsvSizingWindow(_flowsheet).Show(HostWindow);
        };

        MenuReactions.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new ReactionManagerWindow(_flowsheet).Show();
        };

        MenuSensitivity.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new SensitivityAnalysisWindow(_flowsheet).Show();
        };
        MenuOptimizer.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new OptimizerWindow(_flowsheet).Show();
        };
        MenuPropertyChart.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new PropertyChartWindow(_flowsheet).Show();
        };
        MenuBalance.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new BalanceSummaryWindow(_flowsheet).Show();
        };
        MenuInspector.Click += (_, _) =>
        {
            new InspectorReportsWindow().Show();
        };
        MenuCreateCompound.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new CompoundCreatorWindow(_flowsheet).Show();
        };
        MenuBiomassCompound.Click += (_, _) =>
            new BiomassCompoundCreatorWindow(_flowsheet).Show(HostWindow);
        MenuDataRegression.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new DataRegressionWindow(_flowsheet).Show(HostWindow);
        };

        // in a document shell these open another simulation instead of replacing this one
        MenuNew.Click  += async (_, _) =>
        {
            if (NewRequested != null) NewRequested(this, EventArgs.Empty);
            else await NewSimulationAsync();
        };
        MenuOpen.Click += async (_, _) =>
        {
            if (OpenRequested != null) OpenRequested(this, EventArgs.Empty);
            else await OpenSimulationAsync();
        };

        MenuSimSettings.Click += async (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            var dlg = new SimulationSettingsWindow(_flowsheet);
            await dlg.ShowDialog(HostWindow);
        };

        // View > Show/Hide panels: toggle proportion to 0 or restore
        MenuShowEditor.Click += (_, _) => ToggleDockTool(_dockFactory?.EditorTool);
        MenuShowPalette.Click += (_, _) => ToggleDockTool(_dockFactory?.PaletteTool);
        MenuShowResults.Click += (_, _) => ToggleDockTool(_dockFactory?.LogTool);

        MenuZoomIn.Click += (_, _) => ZoomIn();
        MenuZoomOut.Click += (_, _) => ZoomOut();
        MenuZoomFit.Click += (_, _) => ZoomFit();
        MenuZoomReset.Click += (_, _) => ZoomReset();

        MenuScripts.Click += (_, _) => OpenScriptEditor();

        MenuUndo.Click += (_, _) =>
        {
            if (_flowsheet == null) return;
            try
            {
                _flowsheet.ProcessUndo();
                Canvas.Refresh();
                UpdateResultsPanel();
                if (DWSIM.GlobalSettings.Settings.UndoRedoRecalculateFlowsheet)
                    _flowsheet.RequestCalculation();
            }
            catch (Exception ex) { AppendLog($"Undo error: {ex.Message}"); }
        };

        MenuRedo.Click += (_, _) =>
        {
            if (_flowsheet == null) return;
            try
            {
                _flowsheet.ProcessRedo();
                Canvas.Refresh();
                UpdateResultsPanel();
                if (DWSIM.GlobalSettings.Settings.UndoRedoRecalculateFlowsheet)
                    _flowsheet.RequestCalculation();
            }
            catch (Exception ex) { AppendLog($"Redo error: {ex.Message}"); }
        };

        BtnSolve.Click += async (_, _) => await SolveAsync();
        BtnStop.Click += (_, _) =>
        {
            DWSIM.GlobalSettings.Settings.CalculatorStopRequested = true;
            SetStatus("Stop requested.");
            AppendLog("Stop requested.");
        };

        // the list is read when the submenu opens, so it is never stale
        MenuRecent.SubmenuOpened += (_, _) =>
            RecentFilesMenu.Fill(MenuRecent, path => OpenRecentRequested?.Invoke(this, path));

        RecentFilesMenu.Fill(MenuRecent, path => OpenRecentRequested?.Invoke(this, path));

        MenuSave.Click += async (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            var path = _flowsheet.FilePath;
            if (string.IsNullOrEmpty(path))
                await SaveAsAsync();
            else
                await SaveSimulationAsync(path);
        };

        MenuSaveAs.Click += async (_, _) => await SaveAsAsync();

        MenuExportPNG.Click += async (_, _) => await ExportFlowsheetAsync(FlowsheetExportFormat.Png);
        MenuExportSVG.Click += async (_, _) => await ExportFlowsheetAsync(FlowsheetExportFormat.Svg);
        MenuExportPDF.Click += async (_, _) => await ExportFlowsheetAsync(FlowsheetExportFormat.Pdf);

        // Solve with custom calculation order
        BtnSolveForce.Click += async (_, _) => await SolveAsync(customOrder: true);

        // --- Dynamics menu ---
        MenuDynamicsToggle.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            _flowsheet.DynamicMode = !_flowsheet.DynamicMode;
            var state = _flowsheet.DynamicMode ? "enabled" : "disabled";
            AppendLog($"Dynamic mode {state}.");
            Canvas.Refresh();
        };
        MenuDynManager.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            // Activate the Dynamics Manager document tab
            if (_dockFactory?.DynamicsManagerDocument != null)
                _dockFactory.DynamicsManagerDocument.IsActive = true;
        };
        MenuDynIntegrator.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            // Activate the Integrator tool tab in the bottom dock
            if (_dockFactory?.IntegratorTool != null)
                _dockFactory.IntegratorTool.IsActive = true;
        };
        MenuDynPIDTuning.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new PIDTuningWindow(_flowsheet).Show(HostWindow);
        };

        // --- Results menu ---
        MenuMarkdownReport.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            OpenMarkdownReportViewer();
        };

        // --- Help menu ---
        MenuHelpHtml.Click += (_, _) => OpenUrl(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "docs", "dwsim-help", "index.html"));
        MenuHelpSupport.Click += (_, _) => OpenUrl("https://dwsim.org/wiki/index.php?title=Support");
        MenuHelpBug.Click += (_, _) => OpenUrl("https://github.com/DanWBR/dwsim/issues");
        MenuHelpWebsite.Click += (_, _) => OpenUrl("https://dwsim.org");

        // --- UO Extensions Manager ---
        MenuUOExtManager.Click += (_, _) =>
        {
            if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
            new UnitOpExtensionsManagerWindow(_flowsheet).Show(HostWindow);
        };
    }

    // -------------------------------------------------------------------------
    // Events raised to host
    // -------------------------------------------------------------------------

    public event EventHandler? SolveRequested;

    // -------------------------------------------------------------------------
    // Zoom helpers
    // -------------------------------------------------------------------------

    private void ZoomIn()
    {
        if (_surface != null)
        {
            _surface.Zoom = Math.Clamp(_surface.Zoom + 0.1f, 0.1f, 10f);
            SetZoom(_surface.Zoom);
        }
        Canvas.Refresh();
    }

    private void ZoomOut()
    {
        if (_surface != null)
        {
            _surface.Zoom = Math.Clamp(_surface.Zoom - 0.1f, 0.1f, 10f);
            SetZoom(_surface.Zoom);
        }
        Canvas.Refresh();
    }

    private void ZoomFit()
    {
        if (_surface != null)
        {
            _surface.ZoomAll((int)(Canvas.Bounds.Width * GlobalSettings.Settings.DpiScale), (int)(Canvas.Bounds.Height * GlobalSettings.Settings.DpiScale));
            SetZoom(_surface.Zoom);
        }
        Canvas.Refresh();
    }

    private void ZoomReset()
    {
        if (_surface != null)
            _surface.Zoom = 1.0f;
        SetZoom(1.0f);
        Canvas.Refresh();
    }

    // -------------------------------------------------------------------------
    // Results panel population
    // -------------------------------------------------------------------------

    private void UpdateResultsPanel()
    {
        if (_flowsheet == null) return;

        // Results tab - update object list and let user select for per-object reports
        try { ResultsPanel.UpdateList(); } catch { }

        // Spreadsheet tab - recalculate all formulas (custom functions read live data)
        try { _spreadsheet?.EvaluateAll(); } catch { }

        // Material streams tab - rebuild the full property table
        try { MaterialStreamsPanel.UpdateList(); } catch { }
    }

    // -------------------------------------------------------------------------
    // Delete / connect helpers
    // -------------------------------------------------------------------------

    private void HandleCopy()
    {
        if (_surface == null) return;
        var obj = _surface.SelectedObject;
        if (obj == null) return;
        _clipboard = (obj.ObjectType, obj.X + 30, obj.Y + 30);
        AppendLog($"Copied '{obj.Tag}'.");
    }

    private void HandlePaste()
    {
        if (_clipboard == null || _flowsheet == null) return;
        var (type, x, y) = _clipboard.Value;
        _flowsheet.AddObject(type, (int)x, (int)y, type.ToString());
        _clipboard = (_clipboard.Value.type, x + 20, y + 20);
        Canvas.Refresh();
        UpdateResultsPanel();
        AppendLog("Pasted object.");
    }

    private void HandleDisconnectAll()
    {
        if (_surface == null || _flowsheet == null) return;
        var obj = _surface.SelectedObject as DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject;
        if (obj == null) return;

        int count = 0;
        foreach (var other in _flowsheet.GraphicObjects.Values)
        {
            if (other.Name == obj.Name) continue;
            var go = other as DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject;
            if (go == null) continue;
            try { _surface.DisconnectObject(obj, go); count++; } catch { }
            try { _surface.DisconnectObject(go, obj); count++; } catch { }
        }

        Canvas.Refresh();
        AppendLog($"Disconnected all connections from '{obj.Tag}'.");
    }

    private void HandleDelete()
    {
        if (_surface == null || _flowsheet == null) return;
        var obj = _surface.SelectedObject;
        if (obj == null) return;

        var tag  = obj.Tag;
        var name = obj.Name;
        _surface.DeleteSelectedObject(obj);
        _flowsheet.SimulationObjects.Remove(name);
        Canvas.Refresh();
        UpdateResultsPanel();
        AppendLog($"Deleted '{tag}'.");
    }

    private void HandleConnectClick()
    {
        if (_surface == null) return;
        var obj = _surface.SelectedObject;

        if (obj == null)
        {
            ExitConnectMode();
            return;
        }

        if (_connectSource == null)
        {
            _connectSource = obj;
            SetStatus($"Connect: now click the target to connect '{obj.Tag}' to");
        }
        else
        {
            try
            {
                _surface.ConnectObject(
                    (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)_connectSource,
                    (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)obj);
                AppendLog($"Connected '{_connectSource.Tag}' to '{obj.Tag}'.");
                Canvas.Refresh();
            }
            catch (Exception ex)
            {
                AppendLog($"Connect error: {ex.Message}");
            }
            ExitConnectMode();
        }
    }

    private void ExitConnectMode()
    {
        _connectMode   = false;
        _connectSource = null;
        BtnConnect.IsChecked = false;
        SetStatus("Ready");
    }

    /// <summary>
    /// Adds an annotation (table, chart, text, picture, rectangle, button) at the centre of the
    /// view. These have no simulation object behind them, so they do not go through AddObject.
    /// </summary>
    private void AddAnnotationAtCenter(ObjectType type, SkiaSharp.SKImage? image = null)
    {
        if (_flowsheet == null) return;

        var (cx, cy) = ViewCenter();

        var obj = _flowsheet.AddGraphicObject(type, cx, cy, "", image);
        if (obj == null) return;

        Canvas.Refresh();
        AppendLog($"Added '{obj.Tag}'.");
        OpenEditorFor(obj.Name);
    }

    /// <summary>Asks for a picture file and embeds it on the flowsheet.</summary>
    private async Task AddImageAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select an Image",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                },
                FilePickerFileTypes.All
            }
        });

        if (files.Count == 0) return;

        try
        {
            using var stream = await files[0].OpenReadAsync();
            var image = SkiaSharp.SKImage.FromEncodedData(stream);
            if (image == null)
            {
                AppendLog("Could not read that file as an image.");
                return;
            }
            AddAnnotationAtCenter(ObjectType.GO_Image, image);
        }
        catch (Exception ex)
        {
            AppendLog("Could not read that file as an image: " + ex.Message);
        }
    }

    /// <summary>
    /// Converts a point on the canvas into the coordinates an object is placed in.
    /// </summary>
    /// <remarks>
    /// This is the conversion both of the other interfaces make when something is dropped on the
    /// flowsheet: the Windows one divides the client point by the zoom, and the Eto one wrote
    /// <c>e.Location.X * DpiScale / Zoom</c>. The canvas hands out a point that already carries the
    /// display scale, so only the zoom is left to divide by.
    /// </remarks>
    private (int X, int Y) CanvasToObject(double x, double y)
    {
        var zoom = _surface?.Zoom ?? 1.0f;
        if (zoom <= 0.0f) zoom = 1.0f;
        return ((int)(x / zoom), (int)(y / zoom));
    }

    /// <summary>The middle of the visible area, in the coordinates an object is placed in.</summary>
    private (int X, int Y) ViewCenter()
    {
        double cx = Canvas.Bounds.Width / 2;
        double cy = Canvas.Bounds.Height / 2;

        if (_surface != null)
        {
            cx = _surface.Size.Width / 2;
            cy = _surface.Size.Height / 2;
        }

        return CanvasToObject(cx, cy);
    }

    private void AddObjectAtCenter(ObjectType type, string name)
    {
        if (_flowsheet == null) return;
        var (cx, cy) = ViewCenter();
        _flowsheet.AddObject(type, cx, cy, name);
        Canvas.Refresh();
        UpdateResultsPanel();
        AppendLog($"Added '{name}'.");
    }

    // -------------------------------------------------------------------------
    // Context menu
    // -------------------------------------------------------------------------

    private void ShowCanvasContextMenu()
    {
        var obj = _surface?.SelectedObject;
        var ctx = new ContextMenu();

        if (obj != null)
        {
            // ---- Object is selected: show object-specific menu ----
            var header = new MenuItem { Header = obj.Tag ?? obj.Name, IsEnabled = false, FontWeight = FontWeight.SemiBold };
            ctx.Items.Add(header);

            var simObj = _flowsheet?.SimulationObjects.ContainsKey(obj.Name) == true
                ? _flowsheet.SimulationObjects[obj.Name] : null;

            // Toggle Active/Inactive (CheckMenuItem like Eto)
            var toggleActive = new MenuItem { Header = "Toggle Active/Inactive", Icon = IconHelper.MIcon("⏻") };
            toggleActive.Click += (_, _) =>
            {
                obj.Active = !obj.Active;
                if (simObj != null) simObj.GraphicObject.Status = obj.Active
                    ? Interfaces.Enums.GraphicObjects.Status.Idle
                    : Interfaces.Enums.GraphicObjects.Status.Inactive;
                Canvas.Refresh();
                AppendLog($"{obj.Tag}: {(obj.Active ? "activated" : "deactivated")}.");
            };
            ctx.Items.Add(toggleActive);

            // Toggle Show/Hide Label
            var toggleLabel = new MenuItem { Header = "Toggle Show/Hide Label", Icon = IconHelper.MIcon("\U0001F3F7") }; // label
            toggleLabel.Click += (_, _) =>
            {
                obj.DrawLabel = !obj.DrawLabel;
                Canvas.Refresh();
            };
            ctx.Items.Add(toggleLabel);

            ctx.Items.Add(new Separator());

            var openProp = new MenuItem { Header = "Edit/View", Icon = IconHelper.MIcon("\U0001F4DD") }; // memo
            openProp.Click += (_, _) => OpenEditorFor(obj.Name);
            ctx.Items.Add(openProp);

            var appearance = new MenuItem { Header = "Appearance...", Icon = IconHelper.MIcon("\U0001F3A8") }; // palette
            appearance.Click += (_, _) => { if (simObj != null) ShowAppearanceEditor(simObj); };
            ctx.Items.Add(appearance);

            var copyData = new MenuItem { Header = "Copy Data to Clipboard", Icon = IconHelper.MIcon("\U0001F4CB") }; // clipboard
            copyData.Click += async (_, _) =>
            {
                if (simObj == null) return;
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Object: {obj.Tag} ({obj.ObjectType})");
                    sb.AppendLine("---");
                    var su = _flowsheet!.FlowsheetOptions.SelectedUnitSystem;
                    var nf = _flowsheet.FlowsheetOptions.NumberFormat;
                    foreach (var prop in simObj.GetProperties(Interfaces.Enums.PropertyType.ALL))
                    {
                        try
                        {
                            var val = simObj.GetPropertyValue(prop, su);
                            var unit = simObj.GetPropertyUnit(prop, su);
                            sb.AppendLine($"{prop}\t{val}\t{unit}");
                        }
                        catch { }
                    }
                    var clip = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clip != null)
                        await clip.SetTextAsync(sb.ToString());
                    AppendLog("Data copied to clipboard.");
                }
                catch (Exception ex) { AppendLog($"Copy failed: {ex.Message}", DWSIM.Interfaces.IFlowsheet.MessageType.GeneralError); }
            };
            ctx.Items.Add(copyData);

            ctx.Items.Add(new Separator());

            // Calculate
            var calculate = new MenuItem { Header = "Calculate", Icon = IconHelper.MIcon("▶") }; // play
            calculate.Click += async (_, _) =>
            {
                if (_flowsheet == null || simObj == null) return;
                AppendLog($"Calculating {obj.Tag}...");
                try
                {
                    await Task.Run(() => _flowsheet.RequestCalculation3(simObj, false));
                    Canvas.Refresh();
                    UpdateResultsPanel();
                    AppendLog($"{obj.Tag} calculated.");
                }
                catch (Exception ex) { AppendLog($"Calculation error: {ex.Message}"); }
            };
            ctx.Items.Add(calculate);

            // Debug
            var debug = new MenuItem { Header = "Debug Object", Icon = IconHelper.MIcon("\U0001F41B") }; // bug
            debug.Click += async (_, _) =>
            {
                if (simObj == null) return;
                try
                {
                    var report = await Task.Run(() =>
                    {
                        simObj.DebugMode = true;
                        _flowsheet?.RequestCalculation3(simObj, false);
                        var r = simObj.GetDebugReport();
                        simObj.DebugMode = false;
                        return r;
                    });
                    var okBtn = new Button { Content = "OK", IsDefault = true, Width = 80 };
                    okBtn.Classes.Add("dialog");
                    var okPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 8, 0, 0)
                    };
                    okPanel.Children.Add(okBtn);
                    var dlg = new Window
                    {
                        Title = $"Debug Report - {obj.Tag}",
                        Width = 600, Height = 450,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Icon = IconHelper.GetWindowIcon(),
                        Content = new DockPanel
                        {
                            Margin = new Thickness(12),
                            LastChildFill = true,
                            Children =
                            {
                                new TextBox
                                {
                                    Text = report ?? "(no debug data)",
                                    IsReadOnly = true,
                                    AcceptsReturn = true,
                                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                                    FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                                    FontSize = 11
                                }
                            }
                        }
                    };
                    DockPanel.SetDock(okPanel, global::Avalonia.Controls.Dock.Bottom);
                    ((DockPanel)dlg.Content).Children.Insert(0, okPanel);
                    okBtn.Click += (_, _) => dlg.Close();
                    await dlg.ShowDialog(HostWindow);
                }
                catch (Exception ex) { AppendLog($"Debug error: {ex.Message}"); }
            };
            ctx.Items.Add(debug);

            ctx.Items.Add(new Separator());

            // Clone
            var clone = new MenuItem { Header = "Clone", Icon = IconHelper.MIcon("❐") }; // copy
            clone.Click += (_, _) =>
            {
                if (_flowsheet == null || simObj == null) return;
                try
                {
                    var cloned = (Interfaces.ISimulationObject)simObj.CloneXML();
                    cloned.Name = Guid.NewGuid().ToString();
                    cloned.GraphicObject.Tag = obj.Tag + " (Clone)";
                    cloned.GraphicObject.X += 50;
                    cloned.GraphicObject.Y += 50;
                    _flowsheet.AddGraphicObject(cloned.GraphicObject);
                    _flowsheet.SimulationObjects.Add(cloned.Name, cloned);
                    Canvas.Refresh();
                    UpdateResultsPanel();
                    AppendLog($"Cloned '{obj.Tag}'.");
                }
                catch (Exception ex) { AppendLog($"Clone error: {ex.Message}"); }
            };
            ctx.Items.Add(clone);

            var rename = new MenuItem { Header = "Rename...", Icon = IconHelper.MIcon("✏") }; // pencil
            rename.Click += async (_, _) => await RenameSelectedObjectAsync();
            ctx.Items.Add(rename);

            var copy = new MenuItem { Header = "Copy\tCtrl+C", Icon = IconHelper.MIcon("\U0001F4CB") }; // clipboard
            copy.Click += (_, _) => HandleCopy();
            ctx.Items.Add(copy);

            ctx.Items.Add(new Separator());

            var disconnectAll = new MenuItem { Header = "Disconnect All", Icon = IconHelper.MIcon("⛓") }; // chains
            disconnectAll.Click += (_, _) => HandleDisconnectAll();
            ctx.Items.Add(disconnectAll);

            var delete = new MenuItem { Header = "Delete", Icon = IconHelper.MIcon("\U0001F5D1") }; // wastebasket
            delete.Click += (_, _) => HandleDelete();
            ctx.Items.Add(delete);
        }
        else
        {
            // ---- No object selected: show canvas menu ----

            // Add Object submenu - use engine objects when available
            var addObjMenu = new MenuItem { Header = "Add New Object", Icon = IconHelper.MIcon("➕") }; // plus

            if (_flowsheet?.AvailableSimulationObjects != null && _flowsheet.AvailableSimulationObjects.Count > 0)
            {
                // Build from engine ObjectList (like Eto does)
                foreach (var kvp in _flowsheet.AvailableSimulationObjects.OrderBy(x => x.Key))
                {
                    var displayName = kvp.Key;
                    var mi = new MenuItem { Header = displayName };
                    mi.Click += (_, _) =>
                    {
                        var centerX = (int)(Canvas.Bounds.Width / 2);
                        var centerY = (int)(Canvas.Bounds.Height / 2);
                        if (_surface != null)
                        {
                            centerX = (int)(_surface.Size.Width / 2);
                            centerY = (int)(_surface.Size.Height / 2);
                        }
                        AddPaletteObject(displayName, centerX, centerY);
                        Canvas.Refresh();
                        UpdateResultsPanel();
                        AppendLog($"Added '{displayName}'.");
                    };
                    addObjMenu.Items.Add(mi);
                }
            }
            else
            {
                // Static fallback
                var categories = new (string header, string[] items)[]
                {
                    ("Streams", new[] { "Material Stream", "Energy Stream" }),
                    ("Pressure Changers", new[] { "Valve", "Pump", "Compressor", "Expander" }),
                    ("Separators", new[] { "Flash Vessel", "Distillation Column" }),
                    ("Mixers/Splitters", new[] { "Mixer", "Splitter" }),
                    ("Heat Exchangers", new[] { "Heater", "Cooler", "Heat Exchanger" }),
                    ("Reactors", new[] { "Conversion Reactor", "Equilibrium Reactor", "Gibbs Reactor" }),
                    ("Logical", new[] { "Recycle", "Adjust", "Spec" }),
                };
                foreach (var (catHeader, items) in categories)
                {
                    var catMenu = new MenuItem { Header = catHeader };
                    foreach (var itemName in items)
                    {
                        var mi = new MenuItem { Header = itemName };
                        var captured = itemName;
                        mi.Click += (_, _) =>
                        {
                            var type = PaletteNameToObjectType(captured);
                            if (type == null || _flowsheet == null) return;
                            var centerX = (int)(Canvas.Bounds.Width / 2);
                            var centerY = (int)(Canvas.Bounds.Height / 2);
                            _flowsheet.AddObject(type.Value, centerX, centerY, captured);
                            Canvas.Refresh();
                            UpdateResultsPanel();
                            AppendLog($"Added '{captured}'.");
                        };
                        catMenu.Items.Add(mi);
                    }
                    addObjMenu.Items.Add(catMenu);
                }
            }
            ctx.Items.Add(addObjMenu);

            ctx.Items.Add(new Separator());

            var paste = new MenuItem { Header = "Paste\tCtrl+V", IsEnabled = _clipboard != null, Icon = IconHelper.MIcon("\U0001F4CC") }; // pushpin
            paste.Click += (_, _) => HandlePaste();
            ctx.Items.Add(paste);

            ctx.Items.Add(new Separator());

            var zoomAll = new MenuItem { Header = "Zoom All", Icon = IconHelper.MIcon("⬜") }; // fit
            zoomAll.Click += (_, _) => ZoomFit();
            ctx.Items.Add(zoomAll);

            var zoomDefault = new MenuItem { Header = "Default Zoom (100%)", Icon = IconHelper.MIcon("1⃣") }; // 1 keycap
            zoomDefault.Click += (_, _) => ZoomReset();
            ctx.Items.Add(zoomDefault);

            ctx.Items.Add(new Separator());

            // Layout operations
            var autoLayout = new MenuItem { Header = "Perform Auto-Layout", Icon = IconHelper.MIcon("\U0001F4D0") }; // ruler
            autoLayout.Click += (_, _) =>
            {
                try
                {
                    _surface?.AutoArrange();
                    Canvas.Refresh();
                    AppendLog("Auto-layout applied.");
                }
                catch (Exception ex) { AppendLog($"Auto-layout error: {ex.Message}"); }
            };
            ctx.Items.Add(autoLayout);

            var restoreLayout = new MenuItem { Header = "Restore Layout", Icon = IconHelper.MIcon("↩") }; // undo
            restoreLayout.Click += (_, _) =>
            {
                try
                {
                    _surface?.RestoreLayout();
                    Canvas.Refresh();
                    AppendLog("Layout restored.");
                }
                catch (Exception ex) { AppendLog($"Restore layout error: {ex.Message}"); }
            };
            ctx.Items.Add(restoreLayout);
        }

        Canvas.ContextMenu = ctx;
        ctx.Open(Canvas);
    }

    private async Task RenameSelectedObjectAsync()
    {
        var obj = _surface?.SelectedObject;
        if (obj == null) return;
        var newName = await ShowInputDialogAsync("Rename Object", "New name:", obj.Tag);
        if (!string.IsNullOrWhiteSpace(newName) && newName != obj.Tag)
        {
            obj.Tag = newName;
            _flowsheet?.UpdateInterface();
            Canvas.Refresh();
            AppendLog($"Renamed to '{newName}'.");
        }
    }

    // -------------------------------------------------------------------------
    // Dialog helpers
    // -------------------------------------------------------------------------

    private async Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultValue = "")
    {
        string? result = null;
        var tb   = new TextBox { Text = defaultValue };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel  = true };
        cancel.Classes.Add("dialog");
        var ok     = new Button { Content = "OK",     Width = 80, IsDefault = true };
        ok.Classes.Add("dialog");

        var btnPanel = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 16, 12)
        };
        btnPanel.Children.Add(cancel);
        btnPanel.Children.Add(ok);

        var body = new DockPanel();
        DockPanel.SetDock(btnPanel, global::Avalonia.Controls.Dock.Bottom);
        body.Children.Add(btnPanel);
        body.Children.Add(new StackPanel
        {
            Margin  = new Thickness(16, 16, 16, 8),
            Spacing = 10,
            Children = { new TextBlock { Text = prompt }, tb }
        });

        var dlg = new Window
        {
            Title  = title,
            Width  = 360,
            Height = 160,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon(),
            Content = body
        };
        ok.Click     += (_, _) => { result = tb.Text; dlg.Close(); };
        cancel.Click += (_, _) => dlg.Close();
        tb.KeyDown   += (_, e) => { if (e.Key == Key.Return) { result = tb.Text; dlg.Close(); } };
        await dlg.ShowDialog(HostWindow);
        return result;
    }

    private async Task ShowAboutDialogAsync()
    {
        await new AboutWindow().ShowDialog(HostWindow);
    }

    // -------------------------------------------------------------------------
    // Object palette population (ComboBox category selector + ListBox with icons)
    // -------------------------------------------------------------------------

    /// <summary>Category name -> list of (displayName, iconBytes) pairs.</summary>
    private readonly Dictionary<string, List<(string name, byte[]? icon)>> _paletteCategories = new();

    /// <summary>Static fallback categories used when ObjectList is empty (before Initialize).</summary>
    private static readonly (string category, string[] items)[] FallbackCategories =
    {
        ("Streams",           new[] { "Material Stream", "Energy Stream", "Information Stream" }),
        ("Pressure Changers", new[] { "Valve", "Pump", "Compressor", "Expander", "Pipe Segment" }),
        ("Separators",        new[] { "Flash Vessel", "Filter", "Short-cut Column" }),
        ("Mixers/Splitters",  new[] { "Mixer", "Splitter" }),
        ("Heat Exchangers",   new[] { "Heater", "Cooler", "Heat Exchanger" }),
        ("Columns",           new[] { "Distillation Column", "Absorption Column", "Refluxed Absorber", "Reboiled Absorber" }),
        ("Reactors",          new[] { "Conversion Reactor", "Equilibrium Reactor", "Gibbs Reactor", "PFR Reactor", "CSTR Reactor" }),
        ("Solids",            new[] { "Filter" }),
        ("Logical",           new[] { "Recycle", "Energy Recycle", "Adjust", "Spec", "PID Controller", "Switch", "Input" }),
        ("Other",             new[] { "Text Block", "Property Table", "Chart Object", "Spreadsheet Table", "Master Table" }),
    };

    /// <summary>Maps ObjectClass enum to palette category name (matches Eto Flowsheet.eto.cs).</summary>
    private static string ObjectClassToCategory(Interfaces.Enums.SimulationObjectClass cls) => cls switch
    {
        Interfaces.Enums.SimulationObjectClass.Streams        => "Streams",
        Interfaces.Enums.SimulationObjectClass.PressureChangers => "Pressure Changers",
        Interfaces.Enums.SimulationObjectClass.Separators     => "Separators",
        Interfaces.Enums.SimulationObjectClass.MixersSplitters => "Mixers/Splitters",
        Interfaces.Enums.SimulationObjectClass.Exchangers     => "Heat Exchangers",
        Interfaces.Enums.SimulationObjectClass.Columns        => "Columns",
        Interfaces.Enums.SimulationObjectClass.Reactors       => "Reactors",
        Interfaces.Enums.SimulationObjectClass.Solids         => "Solids",
        Interfaces.Enums.SimulationObjectClass.Logical        => "Logical",
        _                                                     => "Other"
    };

    private void PopulatePalette()
    {
        _paletteCategories.Clear();
        PaletteStack.Children.Clear();

        // Build categories from ObjectList if available (after Initialize)
        if (_flowsheet?.AvailableSimulationObjects != null && _flowsheet.AvailableSimulationObjects.Count > 0)
        {
            foreach (var kvp in _flowsheet.AvailableSimulationObjects.OrderBy(x => x.Key))
            {
                var obj = kvp.Value;
                try
                {
                    bool visible = (bool)(obj.GetType().GetProperty("Visible")?.GetValue(obj) ?? true);
                    if (!visible) continue;
                }
                catch { }

                var displayName = obj.GetDisplayName();
                var category = ObjectClassToCategory(obj.ObjectClass);
                byte[]? iconBytes = null;
                try { iconBytes = obj.GetIconBitmapBytes(); } catch { }

                if (!_paletteCategories.ContainsKey(category))
                    _paletteCategories[category] = new List<(string, byte[]?)>();
                _paletteCategories[category].Add((displayName, iconBytes));
            }
        }

        // Fallback to static list if ObjectList is empty
        if (_paletteCategories.Count == 0)
        {
            foreach (var (cat, items) in FallbackCategories)
            {
                _paletteCategories[cat] = items.Select(n => (name: n, icon: (byte[]?)null)).ToList();
            }
        }

        // Build collapsible sections for each category
        foreach (var cat in _paletteCategories.Keys)
        {
            var items = _paletteCategories[cat];

            // --- Category header (clickable toggle) ---
            var arrowText = new TextBlock
            {
                Text = "↓",  // down arrow = expanded
                FontSize = 13,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.SteelBlue,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 4, 0),
                Width = 16
            };
            var headerLabel = new TextBlock
            {
                Text = cat,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            var headerPanel = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromRgb(230, 235, 245)),
                Cursor = new Cursor(StandardCursorType.Hand),
                Height = 30,
            };
            headerPanel.Children.Add(arrowText);
            headerPanel.Children.Add(headerLabel);

            // --- Items grid (2-column wrap of icon+label) ---
            var itemsPanel = new WrapPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                Margin = new Thickness(4, 2, 4, 6)
            };

            foreach (var (name, iconBytes) in items)
            {
                var cell = new StackPanel
                {
                    Orientation = global::Avalonia.Layout.Orientation.Vertical,
                    Width = 90,
                    Margin = new Thickness(2, 4),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = name
                };

                // Icon
                Control iconCtrl;
                if (iconBytes != null && iconBytes.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream(iconBytes);
                        var bmp = new global::Avalonia.Media.Imaging.Bitmap(ms);
                        iconCtrl = new Image
                        {
                            Source = bmp,
                            Width = 40,
                            Height = 40,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                    }
                    catch
                    {
                        iconCtrl = new Border
                        {
                            Width = 40, Height = 40,
                            Background = Brushes.LightGray,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                    }
                }
                else
                {
                    iconCtrl = new Border
                    {
                        Width = 40, Height = 40,
                        Background = Brushes.LightGray,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                }
                cell.Children.Add(iconCtrl);

                // Label
                cell.Children.Add(new TextBlock
                {
                    Text = name,
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MaxWidth = 86
                });

                // Wire double-click to add object
                cell.DoubleTapped += (_, _) =>
                {
                    if (_flowsheet == null) return;
                    var (cx, cy) = ViewCenter();
                    AddPaletteObject(name, cx, cy);
                    AppendLog($"Added '{name}'.");
                };

                // Wire drag-drop from each cell
                WirePaletteCellDrag(cell, name);

                itemsPanel.Children.Add(cell);
            }

            // Toggle expand/collapse on header click
            headerPanel.PointerPressed += (_, _) =>
            {
                bool wasVisible = itemsPanel.IsVisible;
                itemsPanel.IsVisible = !wasVisible;
                arrowText.Text = wasVisible ? "→" : "↓";  // right arrow = collapsed, down = expanded
            };

            PaletteStack.Children.Add(headerPanel);
            PaletteStack.Children.Add(itemsPanel);
        }
    }

    /// <summary>Wires drag-drop from a single palette cell.</summary>
    private static void WirePaletteCellDrag(Control cell, string itemName)
    {
        PointerEventArgs? press = null;
        bool dragging = false;

        cell.AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(cell).Properties.IsLeftButtonPressed)
                press = e;
        }, handledEventsToo: true);

        cell.AddHandler(PointerMovedEvent, async (s, e) =>
        {
            if (press == null || dragging) return;
            if (!e.GetCurrentPoint(cell).Properties.IsLeftButtonPressed) { press = null; return; }

            // Check minimum drag distance to avoid accidental drags
            var startPos = press.GetPosition(cell);
            var curPos = e.GetPosition(cell);
            var dx = Math.Abs(curPos.X - startPos.X);
            var dy = Math.Abs(curPos.Y - startPos.Y);
            if (dx < 4 && dy < 4) return;

            dragging = true;
            try
            {
                var data = new DataObject();
                data.Set("PaletteItem", itemName);
                await DragDrop.DoDragDrop(press, data, DragDropEffects.Copy);
            }
            finally
            {
                press = null;
                dragging = false;
            }
        }, handledEventsToo: true);

        cell.AddHandler(PointerReleasedEvent, (s, e) =>
        {
            press = null;
        }, handledEventsToo: true);
    }

    // -------------------------------------------------------------------------
    // View menu panel toggle helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Stored proportions for dock panels that were hidden via the View menu.
    /// When the user toggles a panel off, we save its Proportion here and set
    /// it to 0 so the panel collapses. Toggling it back on restores the saved value.
    /// </summary>
    private readonly Dictionary<string, double> _savedProportions = new();

    private void ToggleDockTool(Dock.Model.Avalonia.Controls.Tool? tool)
    {
        if (tool == null) return;

        var id = tool.Id ?? tool.Title ?? "";
        if (tool.Proportion > 0.01)
        {
            // Currently visible - hide it
            _savedProportions[id] = tool.Proportion;
            tool.Proportion = 0;
        }
        else
        {
            // Currently hidden - restore it
            tool.Proportion = _savedProportions.TryGetValue(id, out var saved) ? saved : 0.20;
            _savedProportions.Remove(id);
        }
    }

    // -------------------------------------------------------------------------
    // URL / utility helpers
    // -------------------------------------------------------------------------

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    // -------------------------------------------------------------------------
    // Data regression
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers the host launcher behind IFlowsheet.CallDataRegressionUtility, which property
    /// package editors call to regress a binary pair and get the parameters back. The engine
    /// calls it synchronously and expects the result, so the window runs on a nested dispatcher
    /// frame, the same pattern as the CAPE-OPEN picker.
    /// </summary>
    /// <summary>The active window of this app, falling back to the flowsheet window.</summary>
    private Window ActiveOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var active = desktop.Windows.FirstOrDefault(w => w.IsActive);
            if (active != null) return active;
        }
        return HostWindow;
    }

    /// <summary>
    /// The material stream editor asks the host to open the property package editor, which is
    /// what the cog button next to the picker does in the WinForms form.
    /// </summary>
    /// <summary>
    /// The appearance settings of a flowsheet object, reached from the object's context menu as
    /// the WinForms UI does, not from its editor.
    /// </summary>
    private void ShowAppearanceEditor(DWSIM.Interfaces.ISimulationObject simobj)
    {
        var panel = DWSIM.UI.Desktop.Editors.AvaloniaEditorFactory.BuildAppearanceEditor(simobj);

        var close = new Button { Content = "Close", Width = 90, IsCancel = true };
        close.Classes.Add("dialog");

        var bottom = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        bottom.Children.Add(close);

        var root = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = panel });

        var window = new Window
        {
            Title = "Appearance - " + (simobj.GraphicObject?.Tag ?? simobj.Name),
            Width = 460,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = root
        };
        IconHelper.ApplyWindowIcon(window);

        close.Click += (_, _) => window.Close();
        window.Closed += (_, _) => Canvas.Refresh();

        window.Show(HostWindow);
    }

    private void WirePropertyPackageConfigurator()
    {
        DWSIM.UI.Desktop.Editors.MaterialStreamTabbedEditor.ConfigurePropertyPackage = (fs, pp) =>
        {
            var package = pp as DWSIM.Thermodynamics.PropertyPackages.PropertyPackage;
            if (package == null) return;
            new PropertyPackageEditorWindow(fs, package).Show(ActiveOwnerWindow());
        };
    }

    private void WireDataRegressionLauncher()
    {
        DWSIM.FlowsheetBase.FlowsheetBase.DataRegressionLauncher = (comp1, comp2, model) =>
        {
            if (_flowsheet == null) return null!;

            DWSIM.Interfaces.IInteractionParameter? result = null;

            void Show()
            {
                var win = new DataRegressionWindow(_flowsheet, comp1, comp2, model);
                var frame = new DispatcherFrame();
                win.Closed += (_, _) => { result = win.Result; frame.Continue = false; };
                // Own it from whatever window made the call (typically a property package
                // editor), so it does not open behind the caller.
                win.Show(ActiveOwnerWindow());
                Dispatcher.UIThread.PushFrame(frame);
            }

            if (Dispatcher.UIThread.CheckAccess()) Show();
            else Dispatcher.UIThread.Invoke(Show);

            return result!;
        };
    }

    // -------------------------------------------------------------------------
    // CAPE-OPEN
    // -------------------------------------------------------------------------

    /// <summary>
    /// Points the engine's CAPE-OPEN unit-operation picker at the Avalonia one. The engine
    /// calls it synchronously from inside the CapeOpenUO constructor, so the dialog is run on
    /// a nested dispatcher frame rather than awaited.
    /// </summary>
    private void WireCapeOpenSelector()
    {
        if (!OperatingSystem.IsWindows()) return;

        DWSIM.UnitOperations.UnitOperations.CapeOpenUO.SelectorOverride = () =>
        {
            DWSIM.UnitOperations.UnitOperations.Auxiliary.CapeOpen.CapeOpenUnitOpInfo? picked = null;

            void Show()
            {
                var win = new CapeOpenSelectorWindow();
                var frame = new DispatcherFrame();
                win.Closed += (_, _) => { picked = win.Selected; frame.Continue = false; };
                win.Show(HostWindow);
                Dispatcher.UIThread.PushFrame(frame);
            }

            if (Dispatcher.UIThread.CheckAccess()) Show();
            else Dispatcher.UIThread.Invoke(Show);

            return picked!;
        };
    }

    // -------------------------------------------------------------------------
    // Dynamics: integrator results
    // -------------------------------------------------------------------------

    /// <summary>
    /// Dumps the integrator's monitored-variable history into a new spreadsheet worksheet,
    /// one row per stored time step. Mirrors the Eto integrator's View Results button.
    /// </summary>
    private void WriteIntegratorResultsToSpreadsheet()
    {
        if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }
        if (_spreadsheet == null) { AppendLog("The spreadsheet is not available."); return; }

        try
        {
            var dm = _flowsheet.DynamicsManager;
            if (!dm.ScheduleList.ContainsKey(dm.CurrentSchedule))
            { AppendLog("Select a schedule first."); return; }

            var schedule = dm.ScheduleList[dm.CurrentSchedule];
            if (!dm.IntegratorList.ContainsKey(schedule.CurrentIntegrator))
            { AppendLog("The selected schedule has no integrator."); return; }

            var integrator = dm.IntegratorList[schedule.CurrentIntegrator];

            if (integrator.MonitoredVariables.Count == 0)
            { AppendLog("No monitored variables are defined for this integrator."); return; }
            if (integrator.MonitoredVariableValues.Count == 0)
            { AppendLog("No results stored yet. Run the integrator first."); return; }

            var sheet = _spreadsheet.Grid.NewWorksheet("Integrator Results");
            sheet.RowCount = integrator.MonitoredVariableValues.Count + 1;

            sheet.Cells[0, 0].Data = "Time (ms)";
            int col = 1;
            foreach (var v in integrator.MonitoredVariables)
            {
                sheet.Cells[0, col].Data = v.Description +
                    (string.IsNullOrEmpty(v.PropertyUnits) ? "" : " (" + v.PropertyUnits + ")");
                col += 1;
            }

            // The dictionary is keyed by the timestamp's tick count, not by a step index: the
            // integrator stores it as DateTime.Ticks starting from DateTime zero.
            int row = 1;
            foreach (var item in integrator.MonitoredVariableValues)
            {
                sheet.Cells[row, 0].Data = item.Key / (double)TimeSpan.TicksPerMillisecond;
                col = 1;
                foreach (var v in item.Value)
                {
                    sheet.Cells[row, col].Data =
                        double.TryParse(v.PropertyValue, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var d)
                            ? d : (object)v.PropertyValue;
                    col += 1;
                }
                row += 1;
            }

            _spreadsheet.Grid.CurrentWorksheet = sheet;
            if (_dockFactory?.SpreadsheetDocument != null)
                _dockFactory.SpreadsheetDocument.IsActive = true;

            AppendLog($"Integrator results written to the spreadsheet ({row - 1} step(s), {col - 1} variable(s)).");
        }
        catch (Exception ex)
        {
            AppendLog("Could not write the integrator results: " + ex.Message, DWSIM.Interfaces.IFlowsheet.MessageType.GeneralError);
        }
    }

    // -------------------------------------------------------------------------
    // Flowsheet export
    // -------------------------------------------------------------------------

    private enum FlowsheetExportFormat { Png, Svg, Pdf }

    /// <summary>
    /// Renders the flowsheet through GraphicsSurface.UpdateCanvas into a SkiaSharp raster,
    /// SVG or PDF surface. The bounds come from ZoomAll on a throwaway clone of the current
    /// zoom/offset so the export always contains the whole drawing, not just the viewport.
    /// </summary>
    private async Task ExportFlowsheetAsync(FlowsheetExportFormat format)
    {
        if (_flowsheet == null || _surface == null) { AppendLog("No simulation loaded."); return; }

        var (ext, description) = format switch
        {
            FlowsheetExportFormat.Png => (".png", "PNG Image"),
            FlowsheetExportFormat.Svg => (".svg", "SVG Drawing"),
            _ => (".pdf", "PDF Document")
        };

        var file = await HostWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Flowsheet",
            SuggestedFileName = (SimulationName ?? "flowsheet") + ext,
            DefaultExtension = ext.TrimStart('.'),
            FileTypeChoices = new[]
            {
                new FilePickerFileType(description) { Patterns = new[] { "*" + ext } }
            }
        });

        var path = file?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            // Exports exactly what is on screen, at the current zoom and pan. ZoomAll is not
            // used here: on this surface panning moves the objects themselves, so fitting the
            // drawing for the export would alter the simulation.
            int w = Math.Max(64, (int)Canvas.Bounds.Width);
            int h = Math.Max(64, (int)Canvas.Bounds.Height);

            {
                switch (format)
                {
                    case FlowsheetExportFormat.Png:
                    {
                        // 2x for a usable resolution on screens and in documents.
                        const int scale = 2;
                        using var bmp = new SKBitmap(w * scale, h * scale);
                        using (var canvas = new SKCanvas(bmp))
                        {
                            canvas.Clear(SKColors.White);
                            canvas.Scale(scale);
                            _surface.UpdateCanvas(canvas);
                        }
                        using var image = SKImage.FromBitmap(bmp);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var fs = File.Create(path);
                        data.SaveTo(fs);
                        break;
                    }
                    case FlowsheetExportFormat.Svg:
                    {
                        using var stream = new SKFileWStream(path);
                        var writer = new SKXmlStreamWriter(stream);
                        using var canvas = SKSvgCanvas.Create(SKRect.Create(w, h), writer);
                        _surface.UpdateCanvas(canvas);
                        break;
                    }
                    default:
                    {
                        using var stream = new SKFileWStream(path);
                        using var document = SKDocument.CreatePdf(stream);
                        var canvas = document.BeginPage(w, h);
                        _surface.UpdateCanvas(canvas);
                        document.EndPage();
                        document.Close();
                        break;
                    }
                }
            }

            AppendLog($"Flowsheet exported to {path}.");
            SetStatus("Flowsheet exported.");
        }
        catch (Exception ex)
        {
            AppendLog("Export failed: " + ex.Message, DWSIM.Interfaces.IFlowsheet.MessageType.GeneralError);
        }
    }

    private void OpenUtilityWindow(Interfaces.Enums.FlowsheetUtility utility)
    {
        if (_flowsheet == null) { AppendLog("No simulation loaded."); return; }

        // Pre-select the stream picked on the canvas, if there is one. Every utility
        // window also exposes its own picker, so a non-stream selection is not an error.
        var obj = _surface?.SelectedObject;
        var preselected = obj != null && obj.ObjectType == ObjectType.MaterialStream ? obj.Tag : null;

        try
        {
            Window window = utility switch
            {
                Interfaces.Enums.FlowsheetUtility.TrueCriticalPoint
                    => new TrueCriticalPointWindow(_flowsheet, preselected),
                Interfaces.Enums.FlowsheetUtility.PhaseEnvelopeBinary
                    => new BinaryEnvelopeWindow(_flowsheet),
                _ => new PhaseEnvelopeWindow(_flowsheet, preselected)
            };
            window.Show(HostWindow);
        }
        catch (Exception ex) { AppendLog($"Utility error: {ex.Message}"); }
    }

    private void OpenMarkdownReportViewer()
    {
        if (_flowsheet == null) return;
        var viewer = new MarkdownReportWindow(_flowsheet, SimulationName + " - Markdown Report Viewer");
        viewer.Show();
    }

    // -------------------------------------------------------------------------
    // Closing confirmation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asks whether the simulation may be closed and shuts the view down when it may.
    /// The host calls this before removing the document.
    /// </summary>
    public async Task<bool> ConfirmCloseAsync()
    {
        bool? result = null;
        var no  = new Button { Content = "No",  Width = 80, IsDefault = true };
        no.Classes.Add("dialog");
        var yes = new Button { Content = "Yes", Width = 80 };
        yes.Classes.Add("dialog");

        var btnPanel = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 16, 12)
        };
        btnPanel.Children.Add(no);
        btnPanel.Children.Add(yes);

        var body = new DockPanel();
        DockPanel.SetDock(btnPanel, global::Avalonia.Controls.Dock.Bottom);
        body.Children.Add(btnPanel);
        body.Children.Add(new TextBlock
        {
            Text = "Are you sure you want to close this simulation?\nUnsaved changes will be lost.",
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(20, 20, 20, 0)
        });

        var dlg = new Window
        {
            Title = "Close Simulation",
            Width = 380, Height = 160,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon(),
            Content = body
        };
        yes.Click += (_, _) => { result = true;  dlg.Close(); };
        no.Click  += (_, _) => { result = false; dlg.Close(); };
        await dlg.ShowDialog(HostWindow);

        if (result != true) return false;

        _backupTimer?.Stop();
        _backupTimer?.Dispose();

        return true;
    }

    // -------------------------------------------------------------------------
    // Extension / Plugin loading for FlowsheetView
    // (Ported from DWSIM.UI.Desktop.Forms Flowsheet.eto.cs lines 2016-2116)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds the MainWindow instance via the Avalonia application lifetime.
    /// Returns null when running in file-open-direct mode (no MainWindow).
    /// </summary>
    private static MainWindow? FindMainWindow()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow as MainWindow;
        }
        return null;
    }

    /// <summary>
    /// Returns the top-level MenuItem matching the given header text
    /// (the "_" mnemonic prefix is stripped for comparison).
    /// </summary>
    private MenuItem? FindTopLevelMenu(string header)
    {
        return MainMenuBar.Items
            .OfType<MenuItem>()
            .FirstOrDefault(m =>
                (m.Header?.ToString() ?? "").Replace("_", "") == header);
    }

    /// <summary>
    /// Loads FlowsheetView-level extenders (from MainWindow.Extenders).
    /// Mirrors Eto Flowsheet.eto.cs lines 2016-2085.
    /// </summary>
    private void LoadFlowsheetExtensions()
    {
        if (_flowsheet == null) return;

        var mform = FindMainWindow();
        if (mform == null) return;

        foreach (DWSIM.Interfaces.IExtenderCollection extender in mform.Extenders)
        {
            // main-window extensions get a menu item here as well: this is the window that
            // has the menus, and the one that can tell an extension which flowsheet is active
            if (extender.Level != DWSIM.Interfaces.Enums.ExtenderLevel.FlowsheetWindow &&
                extender.Level != DWSIM.Interfaces.Enums.ExtenderLevel.MainWindow)
                continue;

            if (extender.Category == DWSIM.Interfaces.Enums.ExtenderCategory.InitializationScript &&
                extender.Level == DWSIM.Interfaces.Enums.ExtenderLevel.MainWindow)
                continue;

            foreach (var item in extender.Collection)
            {
                if (item is not DWSIM.Interfaces.IExtender ext)
                    continue;

                try
                {
                    // SetFlowsheetGUI may fail if the extension hard-casts to an
                    // Eto-specific type or touches Eto APIs internally. Catch any
                    // exception so the rest of the initialization (SetFlowsheet,
                    // menu item, Run) still proceeds. This also guards against
                    // third-party extensions we don't control.
                    if (ext is DWSIM.Interfaces.IExtender6 ext6)
                    {
                        try { ext6.SetFlowsheetGUI(this); }
                        catch (Exception exGui)
                        {
                            Console.WriteLine($"Extension '{ext.DisplayText}': SetFlowsheetGUI not compatible with Avalonia ({exGui.GetType().Name}); continuing without GUI reference.");
                        }
                    }

                    ext.SetFlowsheet(_flowsheet);

                    if (extender.Category == DWSIM.Interfaces.Enums.ExtenderCategory.InitializationScript)
                    {
                        ext.Run();
                    }
                    else
                    {
                        // Create a menu item for this extension
                        var menuItem = new MenuItem
                        {
                            Header = ext.DisplayText,
                            Icon = IconHelper.MIcon("\U0001FA9B") // swiss knife
                        };
                        menuItem.Click += (_, _) =>
                        {
                            try { ext.Run(); }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error running extension {ext.DisplayText}: {ex}");
                            }
                        };

                        // Add to the appropriate top-level menu based on category
                        MenuItem? targetMenu = extender.Category switch
                        {
                            DWSIM.Interfaces.Enums.ExtenderCategory.File      => FindTopLevelMenu("File"),
                            DWSIM.Interfaces.Enums.ExtenderCategory.Edit      => FindTopLevelMenu("Edit"),
                            DWSIM.Interfaces.Enums.ExtenderCategory.Settings  => FindTopLevelMenu("Edit"),
                            DWSIM.Interfaces.Enums.ExtenderCategory.View      => FindTopLevelMenu("View"),
                            DWSIM.Interfaces.Enums.ExtenderCategory.Tools     => FindTopLevelMenu("Tools"),
                            DWSIM.Interfaces.Enums.ExtenderCategory.Utilities => FindTopLevelMenu("Utilities"),
                            DWSIM.Interfaces.Enums.ExtenderCategory.Dynamics  => FindTopLevelMenu("Dynamics"),
                            DWSIM.Interfaces.Enums.ExtenderCategory.Optimization => FindTopLevelMenu("Tools"),
                            DWSIM.Interfaces.Enums.ExtenderCategory.Results   => FindTopLevelMenu("Results"),
                            DWSIM.Interfaces.Enums.ExtenderCategory.Help      => FindTopLevelMenu("Help"),
                            _ => null
                        };

                        if (targetMenu != null)
                        {
                            targetMenu.Items.Add(menuItem);
                        }

                        // the Windows interface keeps one entry at the right of its menu strip,
                        // the assistant, and leaves everything else on a menu. A main-window
                        // extension filed under Tools is that kind of entry.
                        if (extender.Level == DWSIM.Interfaces.Enums.ExtenderLevel.MainWindow &&
                            extender.Category == DWSIM.Interfaces.Enums.ExtenderCategory.Tools)
                            AddExtensionButton(ext);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading extension {extender.DisplayText}: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Shows a page served on the machine itself on a panel docked to the right of the flowsheet,
    /// where the Windows interface docks the assistant.
    /// </summary>
    public void ShowWebPanel(string title, string url)
    {
        if (_dockFactory == null) return;

        if (_dockFactory.WebTool != null)
        {
            // already open: just bring it forward. The host builds its own browser when shown.
            _dockFactory.WebTool.Title = title;
            _dockFactory.ShowWebTool();
            return;
        }

        try
        {
            _dockFactory.OpenWebTool(title, new Uri(url));
        }
        catch (Exception ex)
        {
            AppendLog($"Could not open '{title}' here: {ex.Message}");
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }

    /// <summary>
    /// Puts an extension on the toolbar, using the icon it publishes. An extension with no icon is
    /// reachable from its menu only.
    /// </summary>
    private void AddExtensionButton(DWSIM.Interfaces.IExtender ext)
    {
        byte[]? png = null;
        try { png = ext.DisplayImage; } catch { }

        // icon and name side by side, as the entry on the Windows menu strip shows them
        var row = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };

        try
        {
            if (png == null || png.Length == 0) throw new InvalidOperationException("no icon");
            using var stream = new MemoryStream(png);
            row.Children.Add(new Image
            {
                Source = new global::Avalonia.Media.Imaging.Bitmap(stream),
                Width = 18,
                Height = 18,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Extension '{ext.DisplayText}': icon not usable ({ex.GetType().Name}).");
        }

        row.Children.Add(new TextBlock
        {
            Text = ext.DisplayText,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        });

        var button = new Button { Content = row };
        button.Classes.Add("toolbar");
        ToolTip.SetTip(button, ext.DisplayText);

        button.Click += (_, _) =>
        {
            try { ext.Run(); }
            catch (Exception ex) { AppendLog($"Error running {ext.DisplayText}: {ex.Message}"); }
        };

        // the menu of a simulation is moved into the main window, so the strip beside it
        // is there and not here
        var strip = FindMainWindow()?.MenuBarExtensions;
        if (strip == null) return;

        strip.Children.Add(button);
        Console.WriteLine($"Menu bar button added for '{ext.DisplayText}'.");
    }

    /// <summary>
    /// Populates the Plugins menu with items from MainWindow.Plugins.
    /// Mirrors Eto Flowsheet.eto.cs lines 2087-2116.
    /// </summary>
    private void LoadFlowsheetPlugins()
    {
        if (_flowsheet == null) return;

        var mform = FindMainWindow();
        if (mform == null) return;

        var pluginItems = new List<MenuItem>();

        foreach (DWSIM.Interfaces.IUtilityPlugin5 iplugin in mform.Plugins)
        {
            var mi = new MenuItem
            {
                Header = iplugin.Name,
                Tag = iplugin.UniqueID,
                Icon = IconHelper.MIcon("\U0001F50C") // electric plug
            };
            mi.Click += (_, _) =>
            {
                try
                {
                    iplugin.SetFlowsheet(_flowsheet);
                    // IUtilityPlugin5.UtilityForm is typed as Object;
                    // in the Eto UI it returns an Eto.Forms.Form.
                    // If a plugin returns an Avalonia Window, show it directly.
                    var form = iplugin.UtilityForm;
                    if (form is Window avWin)
                    {
                        avWin.Show();
                    }
                    else
                    {
                        Console.WriteLine($"Plugin '{iplugin.Name}' returned a non-Avalonia form type; cannot display.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error running plugin {iplugin.Name}: {ex.Message}");
                }
            };
            pluginItems.Add(mi);
        }

        foreach (var pi in pluginItems)
            MenuPlugins.Items.Add(pi);
    }
}
