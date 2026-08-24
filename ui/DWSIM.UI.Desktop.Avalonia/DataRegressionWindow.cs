using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DWSIM.Interfaces;
using DWSIM.SharedClasses.DataRegression.Engine;
using DWSIM.SharedClasses.DataRegression.Models;
using DWSIM.SharedClasses.DataRegression.Reporting;
using DWSIM.UI.Desktop.Avalonia.Controls;
using DWSIM.UI.Shared.Avalonia;
using Newtonsoft.Json;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Data Regression utility. Avalonia counterpart of the Eto FormDataRegression: all of the
/// thermodynamics lives in DWSIM.SharedClasses.DataRegression (RegressionEngine,
/// ChartDataBuilder, RegressionStatsBuilder), so this is UI and wiring only.
///
/// Compounds come from the flowsheet's AvailableCompounds, which Initialize() has already
/// filled from every database, instead of re-loading them here.
/// </summary>
public sealed class DataRegressionWindow : Window
{
    private static readonly string[] DataTypeNames =
    {
        "T-x-y (Txy)", "P-x-y (Pxy)", "T-P-x-y (TPxy)",
        "T-x-x (Txx)", "P-x-x (Pxx)", "T-P-x-x (TPxx)",
        "Solid-Liquid Equilibrium (TTxSE)", "Solid-Solid Equilibrium (TTxSS)"
    };

    private static readonly string[] MethodNames =
    {
        "IPOPT", "Limited Memory BFGS", "Truncated Newton", "Nelder-Mead Simplex Downhill",
        "Particle Swarm", "Particle Swarm Optimization", "Differential Evolution",
        "Local Unimodal Sampling", "Gradient Descent", "Many Optimizing Liaisons", "Mesh"
    };

    private static readonly string[] ObjFuncNames =
    {
        "Least Squares (min T/P)", "Least Squares (min y/x)", "Least Squares (min T/P+y/x)",
        "Weighted Least Squares (min T/P)", "Weighted Least Squares (min y/x)",
        "Weighted Least Squares (min T/P+y/x)", "Chi Square"
    };

    private readonly RegressionEngine _engine = new();
    private readonly IDictionary<string, ICompoundConstantProperties> _compounds;
    private RegressionCase _case = new();

    // ---- setup controls ----
    private readonly TextBox _tbTitle = new();
    private readonly AutoCompleteBox _cbComp1 = new() { FilterMode = AutoCompleteFilterMode.Contains };
    private readonly AutoCompleteBox _cbComp2 = new() { FilterMode = AutoCompleteFilterMode.Contains };
    private readonly ComboBox _cbDataType = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _cbModel = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _cbMethod = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _cbObjFunc = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _cbTUnit = new() { Width = 90 };
    private readonly ComboBox _cbPUnit = new() { Width = 90 };
    private readonly TextBox _tbTolerance = new() { Text = "0.00001" };
    private readonly TextBox _tbMaxIters = new() { Text = "250" };
    private readonly CheckBox _chkIdealVapor = new() { Content = "Ideal Vapor Phase", IsChecked = true };

    private readonly DataGrid _gridParams = new() { Height = 130, CanUserSortColumns = false };
    private readonly DataGrid _gridData = new() { CanUserSortColumns = false };
    private readonly ObservableCollection<ParamRow> _paramRows = new();
    private readonly ObservableCollection<DataRow> _dataRows = new();

    // ---- output ----
    private readonly XYPlot _plot = new();
    private readonly TextBox _tbLog = new()
    {
        IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Consolas,Courier New,monospace"), FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12),
        VerticalContentAlignment = VerticalAlignment.Top
    };
    private readonly TextBox _tbStats = new()
    {
        IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Consolas,Courier New,monospace"), FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12),
        VerticalContentAlignment = VerticalAlignment.Top
    };
    private readonly TextBox _tbBips = new()
    {
        IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Consolas,Courier New,monospace"), FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12),
        VerticalContentAlignment = VerticalAlignment.Top, Text = "(no regression run yet)"
    };

    private readonly Button _btnRun = new() { Content = "Run Regression" };
    private readonly Button _btnOnce = new() { Content = "Calculate Once" };
    private readonly Button _btnCancel = new() { Content = "Cancel", IsEnabled = false };
    private readonly Button _btnExportBips = new() { Content = "Export BIPs...", IsEnabled = false };
    private readonly Button _btnTransfer = new() { Content = "Transfer to Property Package", IsEnabled = false };
    private readonly Button _btnKdb = new() { Content = "Search KDB...", Width = 130 };
    private readonly Button _btnPhaseEq = new() { Content = "Search Local DB...", Width = 150 };
    private readonly TextBlock _status = new() { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Opacity = 0.85, VerticalAlignment = VerticalAlignment.Center };

    private readonly StringBuilder _log = new();
    private bool _running;

    // ---- callback mode (launched from a property package editor) ----
    private readonly bool _callbackMode;

    /// <summary>
    /// Set when the user clicks Transfer. The launcher registered on
    /// FlowsheetBase.DataRegressionLauncher reads it once the window closes.
    /// </summary>
    public IInteractionParameter? Result { get; private set; }

    public DataRegressionWindow(IFlowsheet flowsheet)
        : this(flowsheet, null, null, null) { }

    /// <summary>
    /// Callback-mode constructor: prefills the compound pair and model, and closes on Transfer
    /// so the caller can pick up <see cref="Result"/>.
    /// </summary>
    public DataRegressionWindow(IFlowsheet flowsheet, string? comp1, string? comp2, string? model)
    {
        _compounds = flowsheet.AvailableCompounds;
        _callbackMode = !string.IsNullOrEmpty(comp1) || !string.IsNullOrEmpty(comp2) || !string.IsNullOrEmpty(model);

        Title = "Data Regression";
        Width = 1280;
        Height = 780;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        foreach (var b in new[] { _btnRun, _btnOnce, _btnTransfer }) b.Classes.Add("action");
        foreach (var b in new[] { _btnCancel, _btnExportBips, _btnKdb, _btnPhaseEq }) b.Classes.Add("panel");

        Content = BuildContent();
        WireEngine();
        WireEvents();

        if (!string.IsNullOrEmpty(comp1)) _case.comp1 = comp1;
        if (!string.IsNullOrEmpty(comp2)) _case.comp2 = comp2;
        if (!string.IsNullOrEmpty(model) && ModelRegistry.Contains(model)) _case.model = model;

        LoadFromCase();

        if (_callbackMode)
        {
            Title = "Data Regression - Transfer to Property Package";
            _status.Text = "Regress the parameters, then press Transfer to send them back to the property package.";
        }
    }

    // -------------------------------------------------------------------------
    // Layout
    // -------------------------------------------------------------------------

    private Control BuildContent()
    {
        var compNames = _compounds.Keys.OrderBy(x => x).ToList();
        _cbComp1.ItemsSource = compNames;
        _cbComp2.ItemsSource = compNames;

        foreach (var s in DataTypeNames) _cbDataType.Items.Add(s);
        foreach (var m in ModelRegistry.AllModels()) _cbModel.Items.Add(m.Name);
        foreach (var s in MethodNames) _cbMethod.Items.Add(s);
        foreach (var s in ObjFuncNames) _cbObjFunc.Items.Add(s);
        foreach (var s in new[] { "C", "K", "F", "R" }) _cbTUnit.Items.Add(s);
        foreach (var s in new[] { "Pa", "kPa", "bar", "atm", "psi", "mbar", "MPa" }) _cbPUnit.Items.Add(s);

        BuildParamGrid();
        BuildDataGrid();

        // ---- left: setup ----
        var left = new StackPanel { Spacing = 6, Width = 340, Margin = new Thickness(8) };
        void Add(string label, Control c)
        {
            left.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 0) });
            left.Children.Add(c);
        }

        left.Children.Add(new TextBlock { Text = "Case", FontWeight = FontWeight.SemiBold });
        Add("Title:", _tbTitle);
        Add("Compound 1:", _cbComp1);
        Add("Compound 2:", _cbComp2);
        Add("Data Type:", _cbDataType);
        Add("Model:", _cbModel);

        left.Children.Add(new TextBlock { Text = "Regression", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
        Add("Method:", _cbMethod);
        Add("Objective Function:", _cbObjFunc);

        var units = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Margin = new Thickness(0, 4, 0, 0) };
        var tu = new StackPanel { Spacing = 2 };
        tu.Children.Add(new TextBlock { Text = "T unit:" });
        tu.Children.Add(_cbTUnit);
        var pu = new StackPanel { Spacing = 2 };
        pu.Children.Add(new TextBlock { Text = "P unit:" });
        pu.Children.Add(_cbPUnit);
        Grid.SetColumn(tu, 0); Grid.SetColumn(pu, 2);
        units.Children.Add(tu); units.Children.Add(pu);
        left.Children.Add(units);

        var lims = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Margin = new Thickness(0, 4, 0, 0) };
        var tol = new StackPanel { Spacing = 2 };
        tol.Children.Add(new TextBlock { Text = "Tolerance:" });
        tol.Children.Add(_tbTolerance);
        var its = new StackPanel { Spacing = 2 };
        its.Children.Add(new TextBlock { Text = "Max Iterations:" });
        its.Children.Add(_tbMaxIters);
        Grid.SetColumn(tol, 0); Grid.SetColumn(its, 2);
        lims.Children.Add(tol); lims.Children.Add(its);
        left.Children.Add(lims);

        left.Children.Add(_chkIdealVapor);

        left.Children.Add(new TextBlock { Text = "Parameters", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
        left.Children.Add(_gridParams);

        var actions = new StackPanel { Spacing = 4, Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(_btnRun);
        actions.Children.Add(_btnOnce);
        actions.Children.Add(_btnCancel);
        left.Children.Add(actions);

        // ---- middle: experimental data ----
        var btnAddRow = new Button { Content = "Add Row", Width = 90 };
        var btnDelRow = new Button { Content = "Remove", Width = 90 };
        var btnPaste = new Button { Content = "Paste (TSV/CSV)", Width = 140 };
        var btnClear = new Button { Content = "Clear", Width = 80 };
        foreach (var b in new[] { btnAddRow, btnDelRow, btnPaste, btnClear }) b.Classes.Add("panel");

        btnAddRow.Click += (_, _) => _dataRows.Add(new DataRow());
        btnDelRow.Click += (_, _) => { if (_gridData.SelectedItem is DataRow r) _dataRows.Remove(r); };
        btnClear.Click += (_, _) => _dataRows.Clear();
        btnPaste.Click += async (_, _) => await PasteDataAsync();

        var dataButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 6) };
        foreach (var b in new Control[] { btnAddRow, btnDelRow, btnPaste, btnClear }) dataButtons.Children.Add(b);

        var searchButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 6) };
        searchButtons.Children.Add(_btnKdb);
        searchButtons.Children.Add(_btnPhaseEq);

        var middle = new DockPanel { Margin = new Thickness(0, 8, 8, 8), Width = 440 };
        var mHeader = new TextBlock { Text = "Experimental Data", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(mHeader, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(searchButtons, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(dataButtons, global::Avalonia.Controls.Dock.Top);
        middle.Children.Add(mHeader);
        middle.Children.Add(searchButtons);
        middle.Children.Add(dataButtons);
        middle.Children.Add(_gridData);

        // ---- right: results ----
        var bipPanel = new DockPanel();
        var bipButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        bipButtons.Children.Add(_btnExportBips);
        bipButtons.Children.Add(_btnTransfer);
        DockPanel.SetDock(bipButtons, global::Avalonia.Controls.Dock.Bottom);
        bipPanel.Children.Add(bipButtons);
        bipPanel.Children.Add(_tbBips);

        var tabs = new TabControl { Margin = new Thickness(0, 8, 8, 8) };
        tabs.Items.Add(new TabItem { Header = "Chart", Content = new Border { Padding = new Thickness(2), Child = _plot } });
        tabs.Items.Add(new TabItem { Header = "Log", Content = _tbLog });
        tabs.Items.Add(new TabItem { Header = "Statistics", Content = _tbStats });
        tabs.Items.Add(new TabItem { Header = "Parameters (BIPs)", Content = bipPanel });

        // ---- toolbar ----
        var btnLoad = new Button { Content = "Load Case...", Width = 110 };
        var btnSave = new Button { Content = "Save Case...", Width = 110 };
        foreach (var b in new[] { btnLoad, btnSave }) b.Classes.Add("panel");
        btnLoad.Click += async (_, _) => await LoadCaseAsync();
        btnSave.Click += async (_, _) => await SaveCaseAsync();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(8, 8, 8, 0), VerticalAlignment = VerticalAlignment.Center
        };
        toolbar.Children.Add(btnLoad);
        toolbar.Children.Add(btnSave);
        toolbar.Children.Add(_status);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*") };
        var leftScroll = new ScrollViewer { Content = left };
        Grid.SetColumn(leftScroll, 0);
        Grid.SetColumn(middle, 1);
        Grid.SetColumn(tabs, 2);
        body.Children.Add(leftScroll);
        body.Children.Add(middle);
        body.Children.Add(tabs);

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, global::Avalonia.Controls.Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(body);
        return root;
    }

    private void BuildParamGrid()
    {
        _gridParams.ItemsSource = _paramRows;
        _gridParams.AutoGenerateColumns = false;
        _gridParams.Columns.Add(new DataGridTextColumn { Header = "Parameter", Binding = new global::Avalonia.Data.Binding("Label"), IsReadOnly = true, Width = new DataGridLength(90) });
        _gridParams.Columns.Add(new DataGridTextColumn { Header = "Lower", Binding = new global::Avalonia.Data.Binding("Lower") });
        _gridParams.Columns.Add(new DataGridTextColumn { Header = "Initial", Binding = new global::Avalonia.Data.Binding("Initial") });
        _gridParams.Columns.Add(new DataGridTextColumn { Header = "Upper", Binding = new global::Avalonia.Data.Binding("Upper") });
        _gridParams.Columns.Add(new DataGridCheckBoxColumn { Header = "Fixed", Binding = new global::Avalonia.Data.Binding("Fixed"), Width = new DataGridLength(55) });
    }

    private void BuildDataGrid()
    {
        _gridData.ItemsSource = _dataRows;
        _gridData.AutoGenerateColumns = false;
        _gridData.Columns.Add(new DataGridCheckBoxColumn { Header = "Use", Binding = new global::Avalonia.Data.Binding("Use"), Width = new DataGridLength(45) });
        foreach (var name in new[] { "T", "P", "X1", "X2", "Y1", "TL", "TS" })
        {
            _gridData.Columns.Add(new DataGridTextColumn
            {
                Header = name,
                Binding = new global::Avalonia.Data.Binding(name),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }
    }

    // -------------------------------------------------------------------------
    // Engine wiring
    // -------------------------------------------------------------------------

    private void WireEngine()
    {
        _engine.LogLine += (_, args) => AppendLog(args.Message);
        _engine.IterationCompleted += (_, args) => AppendLog(args.IsException
            ? $"Iteration #{args.Iteration}, Exception: {args.ExceptionMessage}\n"
            : $"Iteration #{args.Iteration}, Function Value = {args.FunctionValue:E} {args.ParameterText}\n");
        _engine.ObjectiveEvaluated += (_, _) => Dispatcher.UIThread.Post(RefreshOutputs);
    }

    private void WireEvents()
    {
        _cbModel.SelectionChanged += (_, _) => SyncParamGridFromModel();
        _btnRun.Click += async (_, _) => await RunAsync(once: false);
        _btnOnce.Click += async (_, _) => await RunAsync(once: true);
        _btnCancel.Click += (_, _) => { _engine.Cancel(); _status.Text = "Cancelling..."; };
        _btnExportBips.Click += async (_, _) => await ExportBipsAsync();
        _btnTransfer.Click += (_, _) => TransferResult();
        _btnKdb.Click += async (_, _) => await SearchKdbAsync();
        _btnPhaseEq.Click += async (_, _) => await SearchPhaseEqAsync();
    }

    // -------------------------------------------------------------------------
    // Online / local dataset search
    // -------------------------------------------------------------------------

    private bool RequireBothCompounds()
    {
        if (!string.IsNullOrWhiteSpace(_cbComp1.Text) && !string.IsNullOrWhiteSpace(_cbComp2.Text)) return true;
        _status.Text = "Pick both compounds first.";
        return false;
    }

    private async Task SearchKdbAsync()
    {
        if (!RequireBothCompounds()) return;

        var dlg = new KdbSearchDialog(_cbComp1.Text!, _cbComp2.Text!);
        await dlg.ShowDialog(this);
        if (dlg.SelectedPoints == null) return;

        if (!string.IsNullOrEmpty(dlg.SelectedTUnit)) SelectString(_cbTUnit, dlg.SelectedTUnit!);
        if (!string.IsNullOrEmpty(dlg.SelectedPUnit)) SelectString(_cbPUnit, dlg.SelectedPUnit!);
        AppendDataPoints(dlg.SelectedPoints, dlg.ReplaceExisting);
    }

    private async Task SearchPhaseEqAsync()
    {
        if (!RequireBothCompounds()) return;

        if (!DWSIM.PhaseEquilibriumData.Index.PhaseEqBundle.IsInstalled())
        {
            var download = await ConfirmAsync("Download phase-equilibrium database",
                "The local phase-equilibrium database is not installed.\n\n" +
                "Download the pre-built database (about 94 MB compressed, about 800 MB installed) now?\n\n" +
                "Destination: " + DWSIM.PhaseEquilibriumData.Index.PhaseEqBundle.DefaultDbPath());
            if (!download) return;

            var dl = new PhaseEqDownloadDialog();
            await dl.ShowDialog(this);
            if (!dl.Succeeded) { _status.Text = "The database was not installed."; return; }
        }

        var typeFilter = PhaseEqDatasetLoader.TypeFilterFor((DataType)Math.Max(0, _cbDataType.SelectedIndex));
        var dlg = new PhaseEqSearchDialog(_cbComp1.Text!, _cbComp2.Text!, typeFilter,
            _cbTUnit.SelectedItem as string ?? "C", _cbPUnit.SelectedItem as string ?? "bar");
        await dlg.ShowDialog(this);
        if (dlg.SelectedPoints == null) return;

        AppendDataPoints(dlg.SelectedPoints, dlg.ReplaceExisting);
    }

    private void AppendDataPoints(List<RegressionDataPoint> points, bool replace)
    {
        if (replace) _dataRows.Clear();
        foreach (var p in points)
        {
            _dataRows.Add(new DataRow
            {
                Use = p.Use, T = p.T, P = p.P,
                X1 = p.X1, X2 = p.X2, Y1 = p.Y1,
                TL = p.TL, TS = p.TS
            });
        }
        _status.Text = $"{points.Count} data point(s) loaded.";
    }

    /// <summary>Minimal yes/no prompt; Avalonia has no built-in message box.</summary>
    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var result = false;
        var yes = new Button { Content = "Yes", Width = 80 };
        var no = new Button { Content = "No", Width = 80, IsCancel = true, IsDefault = true };
        yes.Classes.Add("dialog");
        no.Classes.Add("dialog");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(14)
        };
        buttons.Children.Add(yes);
        buttons.Children.Add(no);

        var body = new DockPanel();
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        body.Children.Add(buttons);
        body.Children.Add(new TextBlock
        {
            Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 16, 16, 0)
        });

        var dlg = new Window
        {
            Title = title, Width = 520, Height = 230, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon(), Content = body
        };
        yes.Click += (_, _) => { result = true; dlg.Close(); };
        no.Click += (_, _) => { result = false; dlg.Close(); };
        await dlg.ShowDialog(this);
        return result;
    }

    // -------------------------------------------------------------------------
    // Transfer
    // -------------------------------------------------------------------------

    /// <summary>
    /// Packs the regressed parameters into an IInteractionParameter for the caller. In callback
    /// mode the window closes so the launcher can read Result.
    /// </summary>
    private void TransferResult()
    {
        var p = _engine.RegressedParameters;
        if (p == null || p.Count == 0) return;

        var ip = new DWSIM.Thermodynamics.BaseClasses.InteractionParameter
        {
            Comp1 = _case.comp1 ?? "",
            Comp2 = _case.comp2 ?? "",
            Model = _case.model ?? ""
        };
        foreach (var kv in p) ip.Parameters[kv.Key] = kv.Value;

        Result = ip;
        _status.Text = "Parameters transferred.";
        if (_callbackMode) Close();
    }

    // -------------------------------------------------------------------------
    // Case <-> UI
    // -------------------------------------------------------------------------

    private void LoadFromCase()
    {
        _tbTitle.Text = _case.title ?? "";
        _cbComp1.Text = _case.comp1 ?? "";
        _cbComp2.Text = _case.comp2 ?? "";
        _cbDataType.SelectedIndex = (int)_case.datatype;
        SelectString(_cbModel, _case.model);
        SelectString(_cbMethod, _case.method);
        SelectString(_cbObjFunc, _case.objfunction);
        SelectString(_cbTUnit, _case.tunit);
        SelectString(_cbPUnit, _case.punit);
        _tbTolerance.Text = _case.tolerance.ToString("G", CultureInfo.InvariantCulture);
        _tbMaxIters.Text = _case.maxits.ToString("G", CultureInfo.InvariantCulture);
        _chkIdealVapor.IsChecked = _case.idealvapormodel;

        SyncParamGridFromModel();

        // Bounds and initial values saved with the case win over the model defaults.
        for (int i = 0; i < _paramRows.Count && i < 3; i++)
        {
            _paramRows[i].Lower = CaseAccessor.GetLowerBound(_case, i);
            _paramRows[i].Initial = CaseAccessor.GetInitial(_case, i);
            _paramRows[i].Upper = CaseAccessor.GetUpperBound(_case, i);
            _paramRows[i].Fixed = CaseAccessor.GetFixed(_case, i);
        }
        _gridParams.ItemsSource = null;
        _gridParams.ItemsSource = _paramRows;

        _dataRows.Clear();
        for (int i = 0; i < _case.checkp.Count; i++)
        {
            _dataRows.Add(new DataRow
            {
                Use = _case.checkp[i] is bool b && b,
                T = SafeAt(_case.tp, i),
                P = SafeAt(_case.pp, i),
                X1 = SafeAt(_case.x1p, i),
                X2 = SafeAt(_case.x2p, i),
                Y1 = SafeAt(_case.yp, i),
                TL = SafeAt(_case.tl, i),
                TS = SafeAt(_case.ts, i)
            });
        }
    }

    private void SyncParamGridFromModel()
    {
        _paramRows.Clear();
        var def = ModelRegistry.GetDefinition(_cbModel.SelectedItem as string ?? "");
        if (def == null) return;
        foreach (var r in def.DefaultRows)
            _paramRows.Add(new ParamRow { Label = r.Label, Lower = r.LowerBound, Initial = r.InitialValue, Upper = r.UpperBound, Fixed = r.Fixed });
    }

    private void StoreCase()
    {
        _case.title = _tbTitle.Text ?? "";
        _case.comp1 = _cbComp1.Text ?? "";
        _case.comp2 = _cbComp2.Text ?? "";
        _case.datatype = (DataType)Math.Max(0, _cbDataType.SelectedIndex);
        _case.model = _cbModel.SelectedItem as string ?? "";
        _case.method = _cbMethod.SelectedItem as string ?? "";
        _case.objfunction = _cbObjFunc.SelectedItem as string ?? "";
        _case.tunit = _cbTUnit.SelectedItem as string ?? "C";
        _case.punit = _cbPUnit.SelectedItem as string ?? "bar";
        if (UtilityHelpers.TryVal(_tbTolerance.Text, out var tol)) _case.tolerance = tol;
        if (UtilityHelpers.TryVal(_tbMaxIters.Text, out var its)) _case.maxits = its;
        _case.idealvapormodel = _chkIdealVapor.IsChecked.GetValueOrDefault(true);

        for (int i = 0; i < _paramRows.Count && i < 3; i++)
        {
            CaseAccessor.SetLowerBound(_case, i, _paramRows[i].Lower);
            CaseAccessor.SetInitial(_case, i, _paramRows[i].Initial);
            CaseAccessor.SetUpperBound(_case, i, _paramRows[i].Upper);
            CaseAccessor.SetFixed(_case, i, _paramRows[i].Fixed);
        }

        _case.checkp.Clear(); _case.tp.Clear(); _case.pp.Clear();
        _case.x1p.Clear(); _case.x2p.Clear(); _case.yp.Clear();
        _case.tl.Clear(); _case.ts.Clear();
        foreach (var r in _dataRows)
        {
            _case.checkp.Add(r.Use);
            _case.tp.Add(r.T); _case.pp.Add(r.P);
            _case.x1p.Add(r.X1); _case.x2p.Add(r.X2);
            _case.yp.Add(r.Y1);
            _case.tl.Add(r.TL); _case.ts.Add(r.TS);
        }
    }

    private static double SafeAt(System.Collections.ArrayList list, int i)
        => list != null && i < list.Count && list[i] != null ? Convert.ToDouble(list[i]) : 0.0;

    private static void SelectString(ComboBox cb, string value)
    {
        if (string.IsNullOrEmpty(value)) { if (cb.SelectedIndex < 0 && cb.Items.Count > 0) cb.SelectedIndex = 0; return; }
        var idx = cb.Items.IndexOf(value);
        cb.SelectedIndex = idx >= 0 ? idx : (cb.Items.Count > 0 ? 0 : -1);
    }

    // -------------------------------------------------------------------------
    // Run
    // -------------------------------------------------------------------------

    private async Task RunAsync(bool once)
    {
        if (_running) return;

        try
        {
            StoreCase();

            if (string.IsNullOrWhiteSpace(_case.comp1) || string.IsNullOrWhiteSpace(_case.comp2))
            { _status.Text = "Pick both compounds first."; return; }
            if (!_compounds.ContainsKey(_case.comp1) || !_compounds.ContainsKey(_case.comp2))
            { _status.Text = "One of the compounds is not in the database."; return; }
            if (_dataRows.Count == 0)
            { _status.Text = "Add experimental data points first."; return; }

            var def = ModelRegistry.GetDefinition(_case.model);
            if (def == null) { _status.Text = "Pick a model first."; return; }

            _running = true;
            SetActionsEnabled(false);
            _log.Clear();
            _tbLog.Text = "";
            _status.Text = once ? "Evaluating..." : "Running regression...";

            var initval = CaseAccessor.GetInitialVector(_case, def.ParameterCount);
            _engine.ResetCancel();
            _engine.Output = true;
            _engine.TDep.Enabled = false;

            if (once)
                await Task.Run(() => _engine.EvaluateOnce(_case, initval));
            else
                await Task.Run(() => _engine.Run(_case, initval, _compounds));

            RefreshOutputs();
            _status.Text = "Done.";
        }
        catch (Exception ex)
        {
            var baseex = ex;
            while (baseex.InnerException != null) baseex = baseex.InnerException;
            AppendLog("Error: " + baseex.Message + "\n");
            _status.Text = "Failed.";
        }
        finally
        {
            _running = false;
            SetActionsEnabled(true);
        }
    }

    private void SetActionsEnabled(bool enabled)
    {
        _btnRun.IsEnabled = enabled;
        _btnOnce.IsEnabled = enabled;
        _btnCancel.IsEnabled = !enabled;
    }

    // -------------------------------------------------------------------------
    // Outputs
    // -------------------------------------------------------------------------

    private void RefreshOutputs()
    {
        try { RefreshChart(); } catch { /* the engine already logged the underlying issue */ }
        try { RefreshStats(); } catch { }
        try { RefreshBips(); } catch { }
    }

    /// <summary>
    /// Renders the RegressionChartData series through XYPlot. The payload carries up to five
    /// y-series with their own x-arrays and a CurveStyle each; PointsOnly becomes a scatter,
    /// the dashed styles get a dash pattern.
    /// </summary>
    private void RefreshChart()
    {
        var data = ChartDataBuilder.Build(_case, _tbTitle.Text ?? "");
        if (data == null) return;

        _plot.Clear();
        _plot.PlotTitle = data.Title ?? "";
        _plot.PlotSubtitle = $"{_case.comp1} / {_case.comp2} - {_case.model}";
        _plot.XAxisTitle = data.XAxisTitle ?? "";
        _plot.YAxisTitle = data.YAxisTitle ?? "";

        var xs = new[] { data.Px, data.Px, data.Px2, data.Px3, data.Px4 };
        var ys = new[] { data.Py1, data.Py2, data.Py3, data.Py4, data.Py5 };
        var titles = new[] { data.Y1Title, data.Y2Title, data.Y3Title, data.Y4Title, data.Y5Title };

        for (int i = 0; i < ys.Length; i++)
        {
            var y = ToDoubles(ys[i]);
            if (y.Count == 0) continue;
            var x = ToDoubles(xs[i]);
            if (x.Count == 0) x = ToDoubles(data.Px);
            if (x.Count == 0) continue;

            var style = i < data.CurveStyles.Count ? data.CurveStyles[i] : CurveStyle.PointsAndLine;
            var scatter = style == CurveStyle.PointsOnly;
            var dashes = style is CurveStyle.DashedLine or CurveStyle.DashedLineWithPoints
                ? new double[] { 4, 3 }
                : null;

            _plot.AddSeries(string.IsNullOrEmpty(titles[i]) ? $"Series {i + 1}" : titles[i],
                x, y, scatter, dashes);
        }

        _plot.InvalidateVisual();
    }

    private static List<double> ToDoubles(System.Collections.ArrayList? list)
    {
        var result = new List<double>();
        if (list == null) return result;
        foreach (var item in list)
        {
            if (item == null) continue;
            try { result.Add(Convert.ToDouble(item, CultureInfo.InvariantCulture)); } catch { }
        }
        return result;
    }

    private void RefreshStats()
    {
        var rows = RegressionStatsBuilder.Build(_case);
        if (rows == null || rows.Count == 0) { _tbStats.Text = "(no results yet)"; return; }

        var cols = StatsColumns.VisibleFor(_case.datatype);
        var sb = new StringBuilder();
        sb.Append("#".PadRight(5));
        foreach (var c in cols) sb.Append(StatsColumns.HeaderFor(c).PadRight(18));
        sb.AppendLine();
        sb.AppendLine(new string('-', 5 + cols.Count * 18));

        int n = 1;
        foreach (var row in rows)
        {
            sb.Append(n.ToString().PadRight(5));
            foreach (var c in cols)
            {
                var v = StatsColumns.ValueOf(row, c);
                sb.Append((double.IsNaN(v) ? "-" : v.ToString("G6", CultureInfo.InvariantCulture)).PadRight(18));
            }
            sb.AppendLine();
            n += 1;
        }
        _tbStats.Text = sb.ToString();
    }

    private void RefreshBips()
    {
        var p = _engine.RegressedParameters;
        if (p == null || p.Count == 0)
        {
            _tbBips.Text = "(no regression run yet)";
            _btnExportBips.IsEnabled = false;
            _btnTransfer.IsEnabled = false;
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Model: " + (_case.model ?? "(unknown)"));
        sb.AppendLine("Compounds: " + (_case.comp1 ?? "?") + " + " + (_case.comp2 ?? "?"));
        sb.AppendLine();
        foreach (var kv in p) sb.AppendLine($"{kv.Key,-12} = {kv.Value:G6}");
        _tbBips.Text = sb.ToString();
        _btnExportBips.IsEnabled = true;
        _btnTransfer.IsEnabled = true;
    }

    private void AppendLog(string text)
    {
        lock (_log) _log.Append(text);
        Dispatcher.UIThread.Post(() => { lock (_log) _tbLog.Text = _log.ToString(); });
    }

    // -------------------------------------------------------------------------
    // Files and clipboard
    // -------------------------------------------------------------------------

    /// <summary>Opens the load dialog straight away, for the welcome screen's load link.</summary>
    public void PromptLoadCase()
    {
        Opened += async (_, _) => await LoadCaseAsync();
    }

    private async Task LoadCaseAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Regression Case",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Regression Case") { Patterns = new[] { "*.dwrsd", "*.json" } } }
        });
        var path = files?.FirstOrDefault()?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var loaded = JsonConvert.DeserializeObject<RegressionCase>(File.ReadAllText(path));
            if (loaded == null) { _status.Text = "The file does not contain a regression case."; return; }
            _case = loaded;
            LoadFromCase();
            RefreshOutputs();
            _status.Text = "Case loaded from " + Path.GetFileName(path) + ".";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not load the case: " + ex.Message;
        }
    }

    private async Task SaveCaseAsync()
    {
        StoreCase();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Regression Case",
            SuggestedFileName = (string.IsNullOrWhiteSpace(_case.title) ? "regression" : _case.title) + ".dwrsd",
            DefaultExtension = "dwrsd",
            FileTypeChoices = new[] { new FilePickerFileType("Regression Case") { Patterns = new[] { "*.dwrsd" } } }
        });
        var path = file?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(_case, Formatting.Indented));
            _status.Text = "Case saved to " + Path.GetFileName(path) + ".";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not save the case: " + ex.Message;
        }
    }

    /// <summary>
    /// Pastes tabular data from the clipboard. Columns follow the grid order
    /// (T, P, X1, X2, Y1, TL, TS); missing trailing columns stay at zero.
    /// </summary>
    private async Task PasteDataAsync()
    {
        var top = GetTopLevel(this);
        if (top?.Clipboard == null) return;
        var text = await top.Clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) { _status.Text = "The clipboard has no text."; return; }

        int added = 0;
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(new[] { '\t', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var values = new double[7];
            int k = 0;
            foreach (var part in parts)
            {
                if (k >= 7) break;
                if (!UtilityHelpers.TryVal(part.Trim(), out values[k])) { k = -1; break; }
                k += 1;
            }
            if (k <= 0) continue; // header or unparseable line

            _dataRows.Add(new DataRow
            {
                Use = true,
                T = values[0], P = values[1], X1 = values[2],
                X2 = values[3], Y1 = values[4], TL = values[5], TS = values[6]
            });
            added += 1;
        }
        _status.Text = added > 0 ? $"{added} data point(s) pasted." : "No numeric rows found in the clipboard.";
    }

    private async Task ExportBipsAsync()
    {
        var p = _engine.RegressedParameters;
        if (p == null || p.Count == 0) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Interaction Parameters",
            SuggestedFileName = $"{_case.comp1}_{_case.comp2}_{_case.model}.json",
            DefaultExtension = "json",
            FileTypeChoices = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } }
        });
        var path = file?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var payload = new
            {
                Model = _case.model,
                Comp1 = _case.comp1,
                Comp2 = _case.comp2,
                Parameters = p
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(payload, Formatting.Indented));
            _status.Text = "Parameters exported to " + Path.GetFileName(path) + ".";
        }
        catch (Exception ex)
        {
            _status.Text = "Could not export the parameters: " + ex.Message;
        }
    }

    // -------------------------------------------------------------------------

    private sealed class ParamRow
    {
        public string Label { get; set; } = "";
        public double Lower { get; set; }
        public double Initial { get; set; }
        public double Upper { get; set; }
        public bool Fixed { get; set; }
    }

    private sealed class DataRow
    {
        public bool Use { get; set; } = true;
        public double T { get; set; }
        public double P { get; set; }
        public double X1 { get; set; }
        public double X2 { get; set; }
        public double Y1 { get; set; }
        public double TL { get; set; }
        public double TS { get; set; }
    }
}
