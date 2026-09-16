using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.Automation.DynamicRunner.ColumnInternals;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Column internals rating: sieve, valve and bubble-cap trays and random and structured packings rated
/// stage by stage on a solved rigorous column (flooding, pressure drop, weeping, entrainment, downcomer
/// backup; holdup, HETP and bed height for packings), with sizing for a target fraction of flood and an
/// automatic iteration with the column solver. Same layout as the depressurization tool: inputs on the
/// left with the case buttons on top, results on the right. Opened from the Utilities menu on any column,
/// or on the utility attached to a column, whose case it keeps.
/// </summary>
public sealed class ColumnInternalsWindow : Window
{
    private readonly IFlowsheet _fs;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;
    private readonly ColumnInternalsInput _in = new();
    private ColumnInternalsResult? _result;
    private readonly List<string> _columns;
    private int _selected = -1;
    private readonly ColumnInternalsUtility? _utility;

    private ComboBox _columnBox = null!;
    private Button _run = null!, _iterate = null!, _export = null!, _load = null!, _save = null!, _applyP = null!, _applyE = null!, _applyN = null!;
    private ScrollViewer _left = null!;
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _summary = new() { Spacing = 2 };
    private readonly XYPlot _plotFlood = new() { MinHeight = 220, Margin = new Thickness(4) };
    private readonly XYPlot _plotDp = new() { MinHeight = 220, Margin = new Thickness(4) };
    private readonly DataGrid _table = new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserSortColumns = false, MinHeight = 260, MaxHeight = 460, GridLinesVisibility = DataGridGridLinesVisibility.All };

    private static readonly List<string> TypeNames = new() { "Sieve tray", "Valve tray", "Bubble-cap tray", "Random packing", "Structured packing" };
    private static readonly List<string> FloodModels = new() { "Fair (1961)", "Kister and Haas (1990)" };
    private static readonly List<string> PackingModels = new() { "Robbins + Kister-Gill", "Billet and Schultes", "Rocha, Bravo and Fair (structured)" };
    private static readonly List<string> HetpModels = new() { "Onda (random packings)", "Billet and Schultes", "Rule of thumb", "Rocha, Bravo and Fair (structured)" };
    private static readonly List<string> ValveLegNames = new() { "Three legs", "Four legs", "Caged (no legs)" };
    private static readonly List<string> CapMethods = new() { "Bolles (1956)", "Modified Dauphine" };
    private static readonly List<string> ValveModels = new() { "Klein (1982) with Fair or Kister-Haas", "Glitsch Ballast Tray Design Manual (Bulletin 4900)" };
    private const string UserPacking = "User-defined...";

    /// <summary>One row of the results table, already in the flowsheet units.</summary>
    public sealed class Row
    {
        public string Stage { get; init; } = "";
        public string Section { get; init; } = "";
        public string Flv { get; init; } = "";
        public string Velocity { get; init; } = "";
        public string Flood { get; init; } = "";
        public string Dp { get; init; } = "";
        public string Head { get; init; } = "";
        public string Weep { get; init; } = "";
        public string Regime { get; init; } = "";
        public string Backup { get; init; } = "";
        public string Residence { get; init; } = "";
        public string Entrainment { get; init; } = "";
        public string Efficiency { get; init; } = "";
        public string Holdup { get; init; } = "";
        public string Hetp { get; init; } = "";
        public string Wetting { get; init; } = "";
        public string Notes { get; init; } = "";
    }

    public ColumnInternalsWindow(IFlowsheet flowsheet) : this(flowsheet, null) { }

    /// <summary>On a utility attached to a column the tool works on that column only and keeps its case in the utility.</summary>
    public ColumnInternalsWindow(IFlowsheet flowsheet, ColumnInternalsUtility? utility)
    {
        _fs = flowsheet;
        _utility = utility;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;
        _columns = ColumnInternalsStudy.ColumnNames(flowsheet);
        if (utility?.AttachedTo?.GraphicObject != null)
        {
            var tag = utility.AttachedTo.GraphicObject.Tag;
            _columns = _columns.Where(c => c == tag).ToList();
            if (_columns.Count == 0) _columns.Add(tag);
        }

        Title = utility != null ? "Column Internals: " + utility.Name + " on " + _columns[0] : "Column Internals";
        Width = 1320;
        Height = 880;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        if (utility != null)
        {
            _in.CopyFrom(utility.GetInput());
            _in.ColumnName = _columns[0];
            _selected = _in.Sections.Count > 0 ? 0 : -1;
        }
        Content = BuildContent();
        if (_columns.Count > 0 && string.IsNullOrEmpty(_in.ColumnName)) SelectColumn(_columns[0], true);
        if (utility != null)
        {
            if (_in.Sections.Count == 0) { SelectColumn(_columns[0], true); Rebuild(); }
            if (utility.LastResult != null) { _result = utility.LastResult; ShowResult(utility.LastResult); _export.IsEnabled = true; _applyP.IsEnabled = true; _applyE.IsEnabled = true; }
            Closing += (_, _) => StoreInUtility();
        }
        else AutoLoadCase();
    }

    /// <summary>Hands the case (and the last rating) to the attached utility so the simulation keeps them.</summary>
    private void StoreInUtility()
    {
        if (_utility == null) return;
        try { _utility.SetInput(_in); if (_result != null) _utility.Store(_result); } catch { }
    }

    // ---------------------------------------------------------------- case files next to the flowsheet

    private void AutoLoadCase()
    {
        var kept = ColumnInternalsStudy.FindColumn(_fs, _in.ColumnName);
        if (kept != null && ColumnInternalsStudy.LoadCaseFromColumn(kept) != null) return;   // the column's own case is already loaded
        var path = CaseFilePathOfFlowsheet();
        if (path == null) return;
        try { ApplyLoadedCase(ColumnInternalsInput.LoadFromFile(path), System.IO.Path.GetFileName(path)); }
        catch (Exception ex) { _status.Text = "The case file next to the flowsheet could not be loaded: " + ex.Message; }
    }

    private string? CaseFilePathOfFlowsheet()
    {
        var fp = _fs.FlowsheetOptions?.FilePath;
        if (string.IsNullOrWhiteSpace(fp)) return null;
        var path = System.IO.Path.ChangeExtension(fp, ColumnInternalsInput.FileExtension);
        return System.IO.File.Exists(path) ? path : null;
    }

    private void ApplyLoadedCase(ColumnInternalsInput loaded, string fileName)
    {
        _in.CopyFrom(loaded);
        var missing = !_columns.Contains(_in.ColumnName);
        if (missing) _in.ColumnName = _columns.Count > 0 ? _columns[0] : "";
        _selected = _in.Sections.Count > 0 ? 0 : -1;
        Rebuild();
        _status.Text = "Loaded " + fileName + (missing && loaded.ColumnName != "" ? " (the column '" + loaded.ColumnName + "' is not in this flowsheet; pick one)." : ".");
    }

    // ---------------------------------------------------------------- layout

    private Control BuildContent()
    {
        _left = new ScrollViewer { Content = BuildInputPanel(), Padding = new Thickness(10, 8, 22, 8), AllowAutoHide = false, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        _run = new Button { Content = "Rate", Width = 110, IsDefault = true };
        _run.Classes.Add("dialog");
        _run.Click += async (_, _) => await RunAsync(false);
        _iterate = new Button { Content = "Rate and iterate with the solver" };
        _iterate.Classes.Add("dialog");
        _iterate.Click += async (_, _) => await RunAsync(true);
        _export = new Button { Content = "Export CSV...", Width = 130, IsEnabled = false };
        _export.Classes.Add("dialog");
        _export.Click += async (_, _) => await ExportAsync();
        _load = new Button { Content = "Load case...", Width = 120 };
        _load.Classes.Add("dialog");
        _load.Click += async (_, _) => await LoadCaseAsync();
        _save = new Button { Content = "Save case...", Width = 120 };
        _save.Classes.Add("dialog");
        _save.Click += async (_, _) => await SaveCaseAsync();
        var topButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 8, 12, 4), Children = { _load, _save } };
        _applyP = new Button { Content = "Pressures to column", IsEnabled = false };
        _applyP.Classes.Add("dialog");
        _applyP.Click += (_, _) => ApplyToColumn(true, false);
        _applyE = new Button { Content = "Efficiencies to column", IsEnabled = false };
        _applyE.Classes.Add("dialog");
        _applyE.Click += (_, _) => ApplyToColumn(false, true);
        _applyN = new Button { Content = "Stages to column", IsEnabled = false };
        _applyN.Classes.Add("dialog");
        _applyN.Click += (_, _) => ApplyStagesToColumn();
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 6, 12, 8) };
        foreach (var b in new[] { _run, _export, _applyP, _applyE, _applyN, _iterate }) { b.Margin = new Thickness(0, 0, 8, 4); buttons.Children.Add(b); }

        var leftDock = new DockPanel();
        DockPanel.SetDock(topButtons, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
        leftDock.Children.Add(topButtons);
        leftDock.Children.Add(buttons);
        leftDock.Children.Add(_left);
        return BuildResultsSide(leftDock);
    }

    private void Rebuild() { _left.Content = BuildInputPanel(); }

    private void SelectColumn(string name, bool resetSections)
    {
        _in.ColumnName = name;
        if (!resetSections && _in.Sections.Count > 0) return;
        _in.Sections.Clear();
        var col = ColumnInternalsStudy.FindColumn(_fs, name);
        // the case the column carries comes first
        var kept = col != null ? ColumnInternalsStudy.LoadCaseFromColumn(col) : null;
        if (kept != null)
        {
            _in.CopyFrom(kept);
            _in.ColumnName = name;
            _selected = _in.Sections.Count > 0 ? 0 : -1;
            _status.Text = "Loaded the case saved in " + name + ".";
            return;
        }
        int n = col != null ? ColumnInternalsStudy.StageCount(col) : 0;
        if (n >= 3)
        {
            bool distillation = col!.GraphicObject.ObjectType == Interfaces.Enums.GraphicObjects.ObjectType.DistillationColumn;
            _in.Sections.Add(new InternalsSection { Name = "Trays", FromStage = distillation ? 2 : 1, ToStage = distillation ? n - 1 : n, Type = InternalType.SieveTray });
        }
        _selected = _in.Sections.Count > 0 ? 0 : -1;
    }

    private static string SectionLabel(InternalsSection s)
    {
        return s.Name + "  (stages " + s.FromStage + " to " + s.ToStage + ", " + TypeNames[(int)s.Type].ToLowerInvariant() + ")";
    }

    /// <summary>The input rows, bound to the current case; rebuilt when the sections change or a case is loaded.</summary>
    private AvaloniaEditorPanel BuildInputPanel()
    {
        var p = new AvaloniaEditorPanel();

        p.CreateAndAddLabelRow("Column");
        p.CreateAndAddDescriptionRow("The rating reads the vapour and liquid flows and properties of every stage from the last solution of the column. Solve the flowsheet first.");
        _columnBox = p.CreateAndAddDropDownRow("Column", _columns, Math.Max(-1, _columns.IndexOf(_in.ColumnName)), (dd, _) =>
        {
            if (dd.SelectedIndex < 0 || _columns[dd.SelectedIndex] == _in.ColumnName) return;
            SelectColumn(_columns[dd.SelectedIndex], true);
            Rebuild();
        });
        _columnBox.IsEnabled = _utility == null;
        var col = ColumnInternalsStudy.FindColumn(_fs, _in.ColumnName);
        int nStages = col != null ? ColumnInternalsStudy.StageCount(col) : 0;
        if (col != null) p.CreateAndAddDescriptionRow(nStages + " stages. Stage 1 is the top stage; on a distillation column the condenser and the reboiler are stages 1 and " + nStages + ".");

        p.CreateAndAddLabelRow("Design targets");
        p.CreateAndAddTextBoxRow(_nf, "Target fraction of flood, trays", _in.TargetFloodFractionTrays,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0 && v < 1) _in.TargetFloodFractionTrays = v; });
        p.CreateAndAddTextBoxRow(_nf, "Target fraction of flood, packings", _in.TargetFloodFractionPackings,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0 && v < 1) _in.TargetFloodFractionPackings = v; });
        p.CreateAndAddTextBoxRow(_nf, "Minimum downcomer residence time (s)", _in.MinDowncomerResidenceTime,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) _in.MinDowncomerResidenceTime = v; });
        p.CreateAndAddTextBoxRow(_nf, "Turndown for the weeping check (min / design vapour)", _in.Turndown,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0 && v <= 1) _in.Turndown = v; });
        p.CreateAndAddDescriptionRow("A section with diameter 0 is sized so that its worst stage sits at the target fraction of flood; a section with a diameter is rated as it is.");
        p.CreateAndAddLabelRow("Iteration with the solver");
        p.CreateAndAddDescriptionRow("Rate and iterate writes the rated pressures and O'Connell efficiencies into the column, solves the flowsheet and rates again until the column pressure drop settles.");
        p.CreateAndAddTextBoxRow(_nf, "Passes at most", _in.MaxIterations,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v >= 1) _in.MaxIterations = (int)Math.Round(v); });
        p.CreateAndAddTextBoxRow(_nf, "Pressure drop change that stops it (fraction)", _in.IterationTolerance,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) _in.IterationTolerance = v; });
        p.CreateAndAddCheckBoxRow("Write the stage pressures", _in.IteratePressures, (cb, _) => _in.IteratePressures = cb.IsChecked ?? true);
        p.CreateAndAddCheckBoxRow("Write the stage efficiencies", _in.IterateEfficiencies, (cb, _) => _in.IterateEfficiencies = cb.IsChecked ?? true);
        p.CreateAndAddCheckBoxRow("Re-stage the packed beds that have a bed height (stages = bed height / HETP)", _in.IterateStages, (cb, _) => _in.IterateStages = cb.IsChecked ?? true);

        p.CreateAndAddLabelRow("Sections");
        p.CreateAndAddDescriptionRow("Split the column into ranges of stages, each with one kind of internal.");
        var items = _in.Sections.Select(SectionLabel).ToArray();
        var list = p.CreateAndAddListBoxRow(Math.Max(60, Math.Min(140, 24 * Math.Max(1, items.Length) + 12)), items, (lb, _) =>
        {
            if (lb.SelectedIndex >= 0 && lb.SelectedIndex != _selected) { _selected = lb.SelectedIndex; Rebuild(); }
        });
        if (_selected >= 0 && _selected < items.Length) list.SelectedIndex = _selected;
        var (add, remove) = p.CreateAndAddTwoButtonsRow("Add", null, "Remove", null,
            (_, _) =>
            {
                var last = _in.Sections.LastOrDefault();
                var s = new InternalsSection { Name = "Section " + (_in.Sections.Count + 1) };
                if (last != null) { s.FromStage = Math.Min(last.ToStage + 1, Math.Max(1, nStages)); s.ToStage = Math.Max(s.FromStage, nStages > 0 ? nStages - 1 : s.FromStage); s.Type = last.Type; s.Diameter = last.Diameter; }
                _in.Sections.Add(s); _selected = _in.Sections.Count - 1; Rebuild();
            },
            (_, _) =>
            {
                if (_selected < 0 || _selected >= _in.Sections.Count) return;
                _in.Sections.RemoveAt(_selected); _selected = Math.Min(_selected, _in.Sections.Count - 1); Rebuild();
            });
        remove.IsEnabled = _selected >= 0;
        add.Width = 120; remove.Width = 120;

        if (_selected >= 0 && _selected < _in.Sections.Count) BuildSectionRows(p, _in.Sections[_selected], nStages);
        // room under the last row so the bottom buttons do not cover it
        p.CreateAndAddEmptySpace();
        p.CreateAndAddEmptySpace();
        p.Children.Add(new Border { Height = 24 });
        return p;
    }

    private void BuildSectionRows(AvaloniaEditorPanel p, InternalsSection s, int nStages)
    {
        p.CreateAndAddLabelRow("Section: " + s.Name);
        p.CreateAndAddStringEditorRow("Name", s.Name, (tb, _) => { s.Name = tb.Text ?? ""; });
        p.CreateAndAddNumericEditorRow("First stage", s.FromStage, 1, Math.Max(1, nStages), 0, (nud, _) => { s.FromStage = (int)(nud.Value ?? s.FromStage); if (s.ToStage < s.FromStage) s.ToStage = s.FromStage; });
        p.CreateAndAddNumericEditorRow("Last stage", s.ToStage, 1, Math.Max(1, nStages), 0, (nud, _) => { s.ToStage = (int)(nud.Value ?? s.ToStage); if (s.ToStage < s.FromStage) s.FromStage = s.ToStage; });
        p.CreateAndAddDropDownRow("Internal", TypeNames, (int)s.Type, (dd, _) =>
        {
            if (dd.SelectedIndex < 0 || dd.SelectedIndex == (int)s.Type) return;
            s.Type = (InternalType)dd.SelectedIndex;
            if (!s.IsTray && string.IsNullOrEmpty(s.PackingName) && s.CustomPacking == null)
                s.PackingName = s.Type == InternalType.StructuredPacking ? "Mellapak Sheet metal 250Y" : "Pall rings Metal 50 mm";
            if (s.Type == InternalType.StructuredPacking && s.PackingModel == PackingModel.RobbinsKisterGill && s.HetpModel == HetpModel.Onda) { s.PackingModel = PackingModel.RochaBravoFair; s.HetpModel = HetpModel.RochaBravoFair; }
            if (s.Type == InternalType.RandomPacking && s.PackingModel == PackingModel.RochaBravoFair) { s.PackingModel = PackingModel.RobbinsKisterGill; s.HetpModel = HetpModel.Onda; }
            Rebuild();
        });
        p.CreateAndAddTextBoxRow(_nf, "Column diameter (" + _su.distance + ", 0 = size)", s.Diameter > 0 ? Show(_su.distance, s.Diameter) : 0.0,
            (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) s.Diameter = v > 0 ? cv.ConvertToSI(_su.distance, v) : 0; });

        if (s.IsTray)
        {
            p.CreateAndAddLabelRow("Tray geometry");
            p.CreateAndAddTextBoxRow(_nf, "Tray spacing (" + _su.distance + ")", Show(_su.distance, s.TraySpacing),
                (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.TraySpacing = cv.ConvertToSI(_su.distance, v); });
            p.CreateAndAddTextBoxRow(_nf, "Downcomer area fraction (single pass)", s.DowncomerAreaFraction,
                (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0 && v < 0.5) s.DowncomerAreaFraction = v; });
            p.CreateAndAddTextBoxRow(_nf, "Weir height (" + _su.distance + ")", Show(_su.distance, s.WeirHeight),
                (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.WeirHeight = cv.ConvertToSI(_su.distance, v); });
            p.CreateAndAddTextBoxRow(_nf, "Downcomer clearance (" + _su.distance + ", 0 = weir minus 10 mm)", s.DowncomerClearance > 0 ? Show(_su.distance, s.DowncomerClearance) : 0.0,
                (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) s.DowncomerClearance = v > 0 ? cv.ConvertToSI(_su.distance, v) : 0; });
            if (s.Type == InternalType.SieveTray)
            {
                p.CreateAndAddTextBoxRow(_nf, "Hole diameter (" + _su.distance + ")", Show(_su.distance, s.HoleDiameter),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.HoleDiameter = cv.ConvertToSI(_su.distance, v); });
                p.CreateAndAddTextBoxRow(_nf, "Hole area fraction of the active area", s.HoleAreaFraction,
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0 && v < 0.5) s.HoleAreaFraction = v; });
            }
            p.CreateAndAddTextBoxRow(_nf, (s.Type == InternalType.ValveTray ? "Deck" : "Plate") + " thickness (" + _su.distance + ")", Show(_su.distance, s.PlateThickness),
                (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.PlateThickness = cv.ConvertToSI(_su.distance, v); });
            if (s.Type == InternalType.SieveTray)
                p.CreateAndAddDescriptionRow("Usual values: spacing 0.45 to 0.6 m, downcomer 12 %, weir 40 to 50 mm (6 to 12 mm in vacuum), holes 5 mm, hole area 10 %, plate 5 mm carbon steel or 3 mm stainless.");
            if (s.Type == InternalType.ValveTray)
            {
                p.CreateAndAddLabelRow("Valves");
                p.CreateAndAddDropDownRow("Valve tray procedure", ValveModels, (int)s.ValveModel, (dd, _) => { if (dd.SelectedIndex >= 0) { s.ValveModel = (ValveTrayModel)dd.SelectedIndex; Rebuild(); } });
                p.CreateAndAddDescriptionRow(s.ValveModel == ValveTrayModel.Klein
                    ? "Klein's closed and open balance points from the valve weight for the dry pressure drop; flooding by the correlation chosen below."
                    : "The Glitsch manual: per cent of flood at constant V/L from the CAF chart and the system factor, downcomer design velocity, dry drop through NU/78.5 ft2 of holes (V-1 flat or V-4 venturi units), total drop with 0.4 (h_w + h_ow), backup and the leakage point. Single-pass trays.");
                p.CreateAndAddTextBoxRow(_nf, "Valves per m2 of active area", s.ValvesPerArea,
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.ValvesPerArea = v; });
                p.CreateAndAddTextBoxRow(_nf, "Deck hole diameter under the valve (" + _su.distance + ")", Show(_su.distance, s.ValveHoleDiameter),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.ValveHoleDiameter = cv.ConvertToSI(_su.distance, v); });
                p.CreateAndAddTextBoxRow(_nf, "Valve thickness (" + _su.distance + ")", Show(_su.distance, s.ValveThickness),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.ValveThickness = cv.ConvertToSI(_su.distance, v); });
                p.CreateAndAddTextBoxRow(_nf, "Valve metal density (" + _su.density + ")", Show(_su.density, s.ValveDensity),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.ValveDensity = cv.ConvertToSI(_su.density, v); });
                p.CreateAndAddDropDownRow("Valve legs", ValveLegNames, (int)s.ValveLegs, (dd, _) => { if (dd.SelectedIndex >= 0) s.ValveLegs = (ValveLegs)dd.SelectedIndex; });
                p.CreateAndAddCheckBoxRow("Venturi (contoured) orifice", s.ValveVenturi, (cb, _) => s.ValveVenturi = cb.IsChecked ?? false);
                p.CreateAndAddDescriptionRow("Usual values: 130 to 170 valves per m2 (12 to 16 per ft2), 1.5 in holes, 16 gauge (1.5 mm) carbon steel valves with four legs, 12 gauge (2.6 mm) deck. The closed and open balance points come from the valve weight; between them the dry drop is flat.");
            }
            if (s.Type == InternalType.BubbleCapTray)
            {
                p.CreateAndAddLabelRow("Caps (Bolles)");
                p.CreateAndAddTextBoxRow(_nf, "Cap inside diameter (" + _su.distance + ")", Show(_su.distance, s.CapDiameter),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.CapDiameter = cv.ConvertToSI(_su.distance, v); });
                p.CreateAndAddTextBoxRow(_nf, "Riser inside diameter (" + _su.distance + ")", Show(_su.distance, s.RiserDiameter),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.RiserDiameter = cv.ConvertToSI(_su.distance, v); });
                p.CreateAndAddTextBoxRow(_nf, "Cap pitch over the cap outside diameter", s.CapPitchRatio,
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 1) s.CapPitchRatio = v; });
                p.CreateAndAddTextBoxRow(_nf, "Slots per cap", s.SlotsPerCap,
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v >= 1) s.SlotsPerCap = (int)Math.Round(v); });
                p.CreateAndAddTextBoxRow(_nf, "Slot width (" + _su.distance + ")", Show(_su.distance, s.SlotWidth),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.SlotWidth = cv.ConvertToSI(_su.distance, v); });
                p.CreateAndAddTextBoxRow(_nf, "Slot height (" + _su.distance + ")", Show(_su.distance, s.SlotHeight),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.SlotHeight = cv.ConvertToSI(_su.distance, v); });
                p.CreateAndAddTextBoxRow(_nf, "Slot top width over base width (1 rectangular, 0 triangular)", s.SlotTopWidthRatio,
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v >= 0 && v <= 1) s.SlotTopWidthRatio = v; });
                p.CreateAndAddDropDownRow("Cap pressure drop", CapMethods, (int)s.CapMethod, (dd, _) => { if (dd.SelectedIndex >= 0) { s.CapMethod = (BubbleCapMethod)dd.SelectedIndex; Rebuild(); } });
                if (s.CapMethod == BubbleCapMethod.Dauphine)
                {
                    p.CreateAndAddTextBoxRow(_nf, "Riser height above the tray (" + _su.distance + ")", Show(_su.distance, s.RiserHeight),
                        (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.RiserHeight = cv.ConvertToSI(_su.distance, v); });
                    p.CreateAndAddTextBoxRow(_nf, "Cap inside height above the tray (" + _su.distance + ")", Show(_su.distance, s.CapInsideHeight),
                        (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.CapInsideHeight = cv.ConvertToSI(_su.distance, v); });
                    p.CreateAndAddDescriptionRow("Dauphine: riser, reversal and dry slot drops corrected for the wet cap (Ludwig eqs. 8-232 to 8-237); the cap must not exceed the riser plus reversal plus slot height, or the vapour blows under the shroud ring.");
                }
                p.CreateAndAddTextBoxRow(_nf, "Static slot seal, weir top above the slots (" + _su.distance + ")", Show(_su.distance, s.StaticSeal),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v >= 0) s.StaticSeal = cv.ConvertToSI(_su.distance, v); });
                p.CreateAndAddTextBoxRow(_nf, "Skirt clearance (" + _su.distance + ")", Show(_su.distance, s.SkirtClearance),
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v >= 0) s.SkirtClearance = cv.ConvertToSI(_su.distance, v); });
                p.CreateAndAddTextBoxRow(_nf, "Liquid gradient across the tray (" + _su.distance + ", 0 = Davies estimate)", s.LiquidGradient > 0 ? Show(_su.distance, s.LiquidGradient) : 0.0,
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) s.LiquidGradient = v > 0 ? cv.ConvertToSI(_su.distance, v) : 0; });
                p.CreateAndAddDescriptionRow("A 4 in cap has about a 98 mm inside diameter, a 68 mm riser, 40 to 50 slots 3 mm wide and 38 mm tall on a 140 mm pitch. The static seal is usually 13 to 38 mm (0.5 to 1.5 in). Bolles keeps the gradient below half the cap drop.");
            }
            p.CreateAndAddLabelRow("Capacity");
            if (s.Type == InternalType.BubbleCapTray)
                p.CreateAndAddDescriptionRow("Fair's chart on the net area, with the surface tension correction; it was drawn for bubble-cap and sieve trays.");
            else if (s.Type == InternalType.ValveTray && s.ValveModel == ValveTrayModel.Glitsch)
                p.CreateAndAddDescriptionRow("Glitsch: the vapour capacity factor CAF_0 of Bulletin 4900 Figure 5 for the tray spacing and vapour density, times the system factor below.");
            else
            {
                p.CreateAndAddDropDownRow("Entrainment flooding correlation", FloodModels, (int)s.FloodModel, (dd, _) => { if (dd.SelectedIndex >= 0) { s.FloodModel = (TrayFloodModel)dd.SelectedIndex; Rebuild(); } });
                p.CreateAndAddDescriptionRow(s.FloodModel == TrayFloodModel.Fair
                    ? "Fair's chart on the net area, with the surface tension correction; the industry standard and the one Towler and Sinnott and ChemSep use."
                    : "Kister and Haas: the correlation Kister recommends for sieve and valve trays (hole area 6 to 20 %, spacing above 14 in, non-foaming systems).");
            }
            if (s.Type == InternalType.ValveTray && s.ValveModel == ValveTrayModel.Klein && s.FloodModel == TrayFloodModel.KisterHaas)
                p.CreateAndAddTextBoxRow(_nf, "Open valve area fraction of the active area", s.ValveOpenAreaFraction,
                    (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) s.ValveOpenAreaFraction = v; });
            p.CreateAndAddTextBoxRow(_nf, "System (foaming) factor on flooding", s.SystemFactor,
                (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0 && v <= 1) s.SystemFactor = v; });
            p.CreateAndAddDescriptionRow("1 for non-foaming systems; 0.9 for light foaming (crude, absorbers), 0.85 for moderate (amine, glycol regenerators), 0.73 for heavy foaming (amine and glycol absorbers), 0.6 for stable foam.");
        }
        else
        {
            p.CreateAndAddLabelRow("Packing");
            var names = PackingCatalogue.All.Where(x => x.Structured == (s.Type == InternalType.StructuredPacking)).Select(x => x.DisplayName).ToList();
            names.Add(UserPacking);
            int idx = s.CustomPacking != null ? names.Count - 1 : names.IndexOf(s.PackingName);
            p.CreateAndAddDropDownRow("Packing", names, Math.Max(0, idx), (dd, _) =>
            {
                if (dd.SelectedIndex < 0) return;
                if (dd.SelectedIndex == names.Count - 1)
                {
                    if (s.CustomPacking == null)
                    {
                        var basis = s.ResolvePacking();
                        s.CustomPacking = basis != null ? basis.Clone() : new PackingData { Name = "My packing", Material = "Metal", Structured = s.Type == InternalType.StructuredPacking, a = 150, Epsilon = 0.95, Fp = 80, NominalSize = 0.05 };
                        s.CustomPacking.Name = "My packing"; s.CustomPacking.Source = "User";
                    }
                    s.PackingName = "";
                }
                else { s.PackingName = names[dd.SelectedIndex]; s.CustomPacking = null; }
                Rebuild();
            });
            var pk = s.ResolvePacking();
            if (s.CustomPacking != null)
            {
                var c = s.CustomPacking;
                p.CreateAndAddStringEditorRow("Name", c.Name, (tb, _) => c.Name = tb.Text ?? "");
                p.CreateAndAddDropDownRow("Material", new List<string> { "Metal", "Ceramic", "Plastic", "Carbon" }, Math.Max(0, new List<string> { "Metal", "Ceramic", "Plastic", "Carbon" }.IndexOf(c.Material)), (dd, _) => { if (dd.SelectedIndex >= 0) c.Material = new[] { "Metal", "Ceramic", "Plastic", "Carbon" }[dd.SelectedIndex]; });
                p.CreateAndAddTextBoxRow(_nf, "Nominal size (mm)", c.NominalSize * 1000, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) c.NominalSize = v / 1000.0; });
                p.CreateAndAddTextBoxRow(_nf, "Specific area a (m2/m3)", c.a, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) c.a = v; });
                p.CreateAndAddTextBoxRow(_nf, "Void fraction", c.Epsilon, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0 && v < 1) c.Epsilon = v; });
                p.CreateAndAddTextBoxRow(_nf, "Packing factor Fp (1/m)", c.Fp, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) c.Fp = v; });
                p.CreateAndAddTextBoxRow(_nf, "Fpd for Robbins (1/m, 0 = use Fp)", double.IsNaN(c.Fpd) ? 0 : c.Fpd, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) c.Fpd = v > 0 ? v : double.NaN; });
                if (c.Structured)
                {
                    p.CreateAndAddTextBoxRow(_nf, "Corrugation side S (mm, 0 = estimate from a and the void fraction)", double.IsNaN(c.CorrugationSide) ? 0 : c.CorrugationSide * 1000, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) c.CorrugationSide = v > 0 ? v / 1000.0 : double.NaN; });
                    p.CreateAndAddTextBoxRow(_nf, "Corrugation angle from the horizontal (deg)", c.CorrugationAngle, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 10 && v < 80) c.CorrugationAngle = v; });
                    p.CreateAndAddTextBoxRow(_nf, "Surface enhancement factor F_SE (Rocha-Bravo-Fair)", c.SurfaceEnhancement, (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v) && v > 0) c.SurfaceEnhancement = v; });
                }
                p.CreateAndAddDescriptionRow("Billet and Schultes constants (leave 0 when unknown; the Robbins route and the rules of thumb do not need them).");
                void BC(string label, Func<double> get, Action<double> set) => p.CreateAndAddTextBoxRow(_nf, label, double.IsNaN(get()) ? 0 : get(), (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) set(v > 0 ? v : double.NaN); });
                BC("C_h (holdup)", () => c.Ch, v => c.Ch = v);
                BC("C_p (pressure drop)", () => c.Cp, v => c.Cp = v);
                BC("C_s (loading)", () => c.Cs, v => c.Cs = v);
                BC("C_Fl (flooding)", () => c.CFl, v => c.CFl = v);
                BC("C_L (liquid mass transfer)", () => c.CL, v => c.CL = v);
                BC("C_V (vapour mass transfer)", () => c.CV, v => c.CV = v);
            }
            else if (pk != null)
            {
                string F(double v, string fmt) => double.IsNaN(v) ? "n/a" : v.ToString(fmt, CultureInfo.CurrentCulture);
                p.CreateAndAddTwoLabelsRow("Packing factor Fp", F(pk.Fp, "0") + " 1/m" + (double.IsNaN(pk.Fpd) ? "" : ", Fpd " + F(pk.Fpd, "0") + " 1/m"));
                p.CreateAndAddTwoLabelsRow("Area, void fraction", F(pk.a, "0") + " m2/m3, " + F(pk.Epsilon, "0.000"));
                if (pk.Structured) p.CreateAndAddTwoLabelsRow("Corrugation side, angle", F(pk.EffectiveCorrugationSide * 1000, "0.0") + " mm" + (double.IsNaN(pk.CorrugationSide) ? " (estimated)" : "") + ", " + F(pk.CorrugationAngle, "0") + " deg");
                p.CreateAndAddTwoLabelsRow("Billet-Schultes constants", pk.HasBilletHydraulics ? "C_h " + F(pk.Ch, "0.000") + ", C_p " + F(pk.Cp, "0.000") + ", C_s " + F(pk.Cs, "0.000") + ", C_Fl " + F(pk.CFl, "0.000") + (pk.HasBilletMassTransfer ? ", C_L " + F(pk.CL, "0.000") + ", C_V " + F(pk.CV, "0.000") : ", no mass transfer constants") : "not available");
                p.CreateAndAddTwoLabelsRow("Source", pk.Source);
            }
            p.CreateAndAddTextBoxRow(_nf, "Bed height (" + _su.distance + ", 0 = stages times HETP)", s.BedHeight > 0 ? Show(_su.distance, s.BedHeight) : 0.0,
                (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) s.BedHeight = v > 0 ? cv.ConvertToSI(_su.distance, v) : 0; });
            p.CreateAndAddLabelRow("Models");
            p.CreateAndAddDropDownRow("Capacity and pressure drop", PackingModels, (int)s.PackingModel, (dd, _) => { if (dd.SelectedIndex >= 0) { s.PackingModel = (PackingModel)dd.SelectedIndex; Rebuild(); } });
            p.CreateAndAddDescriptionRow(s.PackingModel == PackingModel.RobbinsKisterGill
                ? "Robbins (1991) pressure drop with the flood point at the Kister and Gill pressure drop 0.115 Fp^0.7 in H2O/ft; needs only the packing factor."
                : s.PackingModel == PackingModel.BilletSchultes
                    ? "Billet and Schultes (1999): loading and flooding velocities, holdup and pressure drop from the packing constants; the preferred route when the constants exist."
                    : "Rocha, Bravo and Fair (1993): holdup and pressure drop of corrugated sheet packings from the channel geometry, flooding where the pressure drop reaches the Kister and Gill value; structured packings only.");
            p.CreateAndAddDropDownRow("HETP", HetpModels, (int)s.HetpModel, (dd, _) => { if (dd.SelectedIndex >= 0) { s.HetpModel = (HetpModel)dd.SelectedIndex; Rebuild(); } });
            p.CreateAndAddDescriptionRow("Kister's advice: measured HETP data first, rules of thumb next (1.5 times the packing size for Pall-type rings; 100/a + 4 in for structured packings), mass transfer models last. The rule-of-thumb value is always shown beside the model. Rocha, Bravo and Fair (1996) is the mass transfer model for corrugated sheets.");
            p.CreateAndAddTextBoxRow(_nf, "Liquid diffusivity (m2/s, 0 = estimate)", s.LiquidDiffusivity,
                (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) s.LiquidDiffusivity = Math.Max(0, v); });
            p.CreateAndAddTextBoxRow(_nf, "Vapour diffusivity (m2/s, 0 = estimate)", s.VapourDiffusivity,
                (tb, _) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) s.VapourDiffusivity = Math.Max(0, v); });
            p.CreateAndAddDescriptionRow("Typical values: 1e-9 m2/s for liquids, 1e-5 m2/s for gases at 1 atm. The estimates scale these with temperature, pressure and liquid viscosity.");
        }
    }

    private Control BuildResultsSide(Control leftDock)
    {
        _plotFlood.PlotTitle = "Fraction of flood";
        _plotFlood.XAxisTitle = "stage";
        _plotFlood.YAxisTitle = "u / u_flood";
        _plotDp.PlotTitle = "Pressure drop";
        _plotDp.XAxisTitle = "stage";
        _plotDp.YAxisTitle = _su.deltaP + " per tray or per stage of bed";

        var right = new StackPanel { Spacing = 6, Margin = new Thickness(10, 8, 24, 8) };
        right.Children.Add(new TextBlock { Text = "Results", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13) });
        right.Children.Add(_summary);
        right.Children.Add(_plotFlood);
        right.Children.Add(_plotDp);
        right.Children.Add(new TextBlock { Text = "Stage by stage", FontWeight = FontWeight.SemiBold, FontSize = UiScale.Font(13), Margin = new Thickness(0, 8, 0, 0) });
        void Col(string header, string path) => _table.Columns.Add(new DataGridTextColumn { Header = header, Binding = new global::Avalonia.Data.Binding(path), Width = DataGridLength.Auto });
        Col("stage", nameof(Row.Stage));
        Col("section", nameof(Row.Section));
        Col("F_LV", nameof(Row.Flv));
        Col("u (" + _su.velocity + ")", nameof(Row.Velocity));
        Col("flood %", nameof(Row.Flood));
        Col("dP (" + _su.deltaP + ")", nameof(Row.Dp));
        Col("h_t (mm liq)", nameof(Row.Head));
        Col("u_h/u_weep", nameof(Row.Weep));
        Col("valves / slots", nameof(Row.Regime));
        Col("backup (mm)", nameof(Row.Backup));
        Col("t_dc (s)", nameof(Row.Residence));
        Col("entrainment", nameof(Row.Entrainment));
        Col("E_O'Connell", nameof(Row.Efficiency));
        Col("holdup", nameof(Row.Holdup));
        Col("HETP (" + _su.distance + ")", nameof(Row.Hetp));
        Col("u_L/u_L,min", nameof(Row.Wetting));
        Col("notes", nameof(Row.Notes));
        right.Children.Add(_table);
        var rightScroll = new ScrollViewer { Content = right, AllowAutoHide = false, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("470,6,*") };
        grid.ColumnDefinitions[0].MinWidth = 320;
        grid.ColumnDefinitions[2].MinWidth = 320;
        Grid.SetColumn(leftDock, 0);
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, Background = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), Margin = new Thickness(0, 8, 0, 8) };
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(rightScroll, 2);
        grid.Children.Add(leftDock);
        grid.Children.Add(splitter);
        grid.Children.Add(rightScroll);

        var bottom = new StackPanel { Margin = new Thickness(12, 0, 12, 8), Spacing = 4 };
        bottom.Children.Add(_status);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(grid);
        SetSummaryPlaceholder();
        return root;
    }

    private double Show(string units, double si) => cv.ConvertFromSI(units, si);
    private string N(double v) => double.IsNaN(v) ? "" : v.ToString(_nf, CultureInfo.CurrentCulture);
    private string N(double v, string fmt) => double.IsNaN(v) ? "" : v.ToString(fmt, CultureInfo.CurrentCulture);

    // ---------------------------------------------------------------- run

    private async Task RunAsync(bool iterate)
    {
        if (string.IsNullOrEmpty(_in.ColumnName)) { _status.Text = "Pick a column."; return; }
        if (_in.Sections.Count == 0) { _status.Text = "Add at least one section."; return; }
        var col = ColumnInternalsStudy.FindColumn(_fs, _in.ColumnName);
        if (col == null) { _status.Text = "The column is not on the flowsheet."; return; }
        if (!col.Calculated) { _status.Text = "The column has not been solved; solve the flowsheet first."; return; }

        _run.IsEnabled = false; _iterate.IsEnabled = false; _export.IsEnabled = false;
        _status.Text = iterate ? "Rating and solving..." : "Rating...";
        var input = _in.Clone();
        try
        {
            var result = iterate
                ? await Task.Run(() => ColumnInternalsStudy.RunIterating(_fs, input, line => Dispatcher.UIThread.Post(() => _status.Text = line)))
                : await Task.Run(() => ColumnInternalsStudy.Run(_fs, input));
            _result = result;
            if (iterate && result.Input != null) { _in.CopyFrom(result.Input); Rebuild(); }
            ColumnInternalsStudy.StoreCaseInColumn(col, _in);
            ShowResult(result);
            var warn = result.Sections.Sum(s => s.Warnings.Count + s.Stages.Sum(r => r.Warnings.Count));
            var head = iterate ? (result.Converged ? "Settled after " + result.Iterations + " pass(es). " : "Not settled after " + result.Iterations + " pass(es); see the summary. ") : "Done. ";
            _status.Text = head + (warn == 0 ? "No warnings." : warn + " warning(s); see the notes column and the summary.");
            _export.IsEnabled = true;
            _applyP.IsEnabled = !iterate;
            _applyE.IsEnabled = !iterate && result.Sections.Any(sec => sec.Stages.Any(r => !double.IsNaN(r.OConnellEfficiency)));
            _applyN.IsEnabled = !iterate && ColumnInternalsStudy.HasRestageableSection(_in);
            if (iterate) { try { _fs.UpdateOpenEditForms(); } catch { } }
            StoreInUtility();
        }
        catch (Exception ex)
        {
            _status.Text = "The rating failed: " + ex.Message;
        }
        finally { _run.IsEnabled = true; _iterate.IsEnabled = true; }
    }

    private void ApplyToColumn(bool pressures, bool efficiencies)
    {
        if (_result == null) return;
        var col = ColumnInternalsStudy.FindColumn(_fs, _result.ColumnName);
        if (col == null) { _status.Text = "The column is not on the flowsheet."; return; }
        try
        {
            _status.Text = ColumnInternalsStudy.ApplyToColumn(col, _result, pressures, efficiencies);
            ColumnInternalsStudy.StoreCaseInColumn(col, _in);
            _applyP.IsEnabled = false; _applyE.IsEnabled = false;
            try { _fs.UpdateOpenEditForms(); } catch { }
        }
        catch (Exception ex) { _status.Text = "Could not write to the column: " + ex.Message; }
    }

    private void ApplyStagesToColumn()
    {
        if (_result == null) return;
        var col = ColumnInternalsStudy.FindColumn(_fs, _result.ColumnName);
        if (col == null) { _status.Text = "The column is not on the flowsheet."; return; }
        try
        {
            _status.Text = ColumnInternalsStudy.ApplyStagesToColumn(col, _result, _in);
            ColumnInternalsStudy.StoreCaseInColumn(col, _in);
            _applyP.IsEnabled = false; _applyE.IsEnabled = false; _applyN.IsEnabled = false;
            Rebuild();
            try { _fs.UpdateOpenEditForms(); } catch { }
        }
        catch (Exception ex) { _status.Text = "Could not re-stage the column: " + ex.Message; }
    }

    private void SetSummaryPlaceholder()
    {
        _summary.Children.Clear();
        _summary.Children.Add(new TextBlock { Text = "Set up the sections on the left and press Rate.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
    }

    private void ShowResult(ColumnInternalsResult res)
    {
        _summary.Children.Clear();
        var p = new AvaloniaEditorPanel();
        string D(double m) => N(Show(_su.distance, m)) + " " + _su.distance;
        string DP(double pa) => N(Show(_su.deltaP, pa)) + " " + _su.deltaP;
        foreach (var sr in res.Sections)
        {
            p.CreateAndAddLabelRow(sr.Section.Name + " (stages " + sr.Section.FromStage + " to " + sr.Section.ToStage + ", " + TypeNames[(int)sr.Section.Type].ToLowerInvariant() + ")");
            if (sr.Stages.Count == 0) { foreach (var w in sr.Warnings) p.CreateAndAddDescriptionRow(w); continue; }
            p.CreateAndAddTwoLabelsRow("Diameter", D(sr.Diameter) + (sr.Section.Diameter > 0 ? " (given; " + D(sr.RequiredDiameter) + " would put the worst stage at the target flood)" : " (sized for the target flood)"));
            p.CreateAndAddTwoLabelsRow("Highest fraction of flood", N(sr.MaxFloodFraction, "0.00") + " on stage " + sr.LimitingStage);
            p.CreateAndAddTwoLabelsRow("Pressure drop over the section", DP(sr.TotalPressureDrop));
            if (!sr.Section.IsTray)
            {
                p.CreateAndAddTwoLabelsRow("Average HETP", D(sr.AverageHETP));
                p.CreateAndAddTwoLabelsRow("Bed height", D(sr.BedHeight) + (sr.Section.BedHeight > 0 ? " (given)" : " (stages times HETP)"));
            }
            else
            {
                p.CreateAndAddTwoLabelsRow("Tray stack height", D(sr.Stages.Count * sr.Section.TraySpacing));
            }
            foreach (var w in sr.Warnings) p.CreateAndAddDescriptionRow(w);
            var stageWarnings = sr.Stages.Where(r => r.Warnings.Count > 0).Select(r => "stage " + r.Stage + ": " + string.Join(" ", r.Warnings)).ToList();
            if (stageWarnings.Count > 0) p.CreateAndAddDescriptionRow(string.Join(" | ", stageWarnings.Take(6)) + (stageWarnings.Count > 6 ? " | ..." : ""));
        }
        p.CreateAndAddTwoLabelsRow("Whole column", "pressure drop " + DP(res.TotalPressureDrop) + ", internals height " + D(res.TotalHeight));
        if (res.Log.Count > 0)
        {
            p.CreateAndAddLabelRow("Iteration with the solver");
            foreach (var l in res.Log) p.CreateAndAddDescriptionRow(l);
        }
        _summary.Children.Add(p);

        _plotFlood.Clear();
        _plotDp.Clear();
        foreach (var sr in res.Sections)
        {
            var rated = sr.Stages.Where(r => !double.IsNaN(r.FloodFraction)).ToList();
            if (rated.Count == 0) continue;
            _plotFlood.AddSeries(sr.Section.Name, rated.Select(r => (double)r.Stage).ToArray(), rated.Select(r => r.FloodFraction).ToArray(), rated.Count == 1);
            _plotDp.AddSeries(sr.Section.Name, rated.Select(r => (double)r.Stage).ToArray(), rated.Select(r => Show(_su.deltaP, r.PressureDropTotal)).ToArray(), rated.Count == 1);
        }
        _plotFlood.InvalidateVisual();
        _plotDp.InvalidateVisual();

        var rows = new List<Row>();
        foreach (var sr in res.Sections)
            foreach (var r in sr.Stages)
                rows.Add(new Row
                {
                    Stage = r.Stage.ToString(CultureInfo.CurrentCulture),
                    Section = sr.Section.Name,
                    Flv = N(r.FlowParameter, "0.000"),
                    Velocity = N(Show(_su.velocity, r.NetVelocity)),
                    Flood = N(r.FloodFraction * 100, "0"),
                    Dp = N(Show(_su.deltaP, r.PressureDropTotal)),
                    Head = N(r.TotalHead, "0"),
                    Weep = N(r.WeepRatio, "0.00"),
                    Regime = r.Regime,
                    Backup = N(r.DowncomerBackup, "0"),
                    Residence = N(r.DowncomerResidenceTime, "0.0"),
                    Entrainment = N(r.Entrainment, "0.000"),
                    Efficiency = N(r.OConnellEfficiency, "0.00"),
                    Holdup = N(r.LiquidHoldup, "0.000"),
                    Hetp = N(Show(_su.distance, r.HETP)),
                    Wetting = N(r.WettingRatio, "0.0"),
                    Notes = string.Join(" ", r.Warnings)
                });
        _table.ItemsSource = rows;
    }

    // ---------------------------------------------------------------- case files

    private static readonly FilePickerFileType CaseType = new("Column internals case") { Patterns = new[] { "*" + ColumnInternalsInput.FileExtension } };

    private async Task LoadCaseAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Load a column internals case", AllowMultiple = false, FileTypeFilter = new[] { CaseType } });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be read from that location."; return; }
        try { ApplyLoadedCase(ColumnInternalsInput.LoadFromFile(path), files[0].Name); }
        catch (Exception ex) { _status.Text = "The case could not be loaded: " + ex.Message; }
    }

    private async Task SaveCaseAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save the column internals case",
            SuggestedFileName = (string.IsNullOrWhiteSpace(_fs.FlowsheetOptions?.FilePath) ? "internals" : System.IO.Path.GetFileNameWithoutExtension(_fs.FlowsheetOptions.FilePath)) + ColumnInternalsInput.FileExtension,
            DefaultExtension = ColumnInternalsInput.FileExtension.TrimStart('.'),
            FileTypeChoices = new[] { CaseType }
        });
        if (file == null) return;
        var path = file.TryGetLocalPath();
        if (path == null) { _status.Text = "The file could not be written to that location."; return; }
        try { _in.SaveToFile(path); _status.Text = "Saved " + file.Name + "."; }
        catch (Exception ex) { _status.Text = "The case could not be saved: " + ex.Message; }
    }

    private async Task ExportAsync()
    {
        if (_result == null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export the column internals rating",
            SuggestedFileName = "column_internals.csv",
            FileTypeChoices = new[] { new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } } }
        });
        if (file == null) return;
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", "stage", "section", "internal", "diameter (" + _su.distance + ")", "F_LV", "velocity (" + _su.velocity + ")", "flooding velocity (" + _su.velocity + ")",
            "fraction of flood", "pressure drop (" + _su.deltaP + ")", "total head (mm liquid)", "dry drop (mm liquid)", "weir crest (mm)", "hole velocity (m/s)", "weep point velocity (m/s)",
            "valves / slots", "closed balance velocity (m/s)", "open balance velocity (m/s)", "cap drop (mm)", "slot opening (mm)", "slot load", "liquid gradient (mm)", "dynamic seal (mm)",
            "downcomer backup (mm)", "backup limit (mm)", "downcomer residence (s)", "entrainment", "efficiency factor", "O'Connell efficiency", "holdup (m3/m3)", "loading velocity (m/s)",
            "HETP (" + _su.distance + ")", "HETP rule of thumb (" + _su.distance + ")", "H_G (m)", "H_L (m)", "H_OG (m)", "wetting ratio", "notes"));
        string G(double v) => double.IsNaN(v) ? "" : v.ToString("G6", ci);
        foreach (var sr in _result.Sections)
            foreach (var r in sr.Stages)
                sb.AppendLine(string.Join(";", r.Stage.ToString(ci), sr.Section.Name, TypeNames[(int)sr.Section.Type], G(Show(_su.distance, r.Diameter)), G(r.FlowParameter),
                    G(Show(_su.velocity, r.NetVelocity)), G(Show(_su.velocity, r.FloodingVelocity)), G(r.FloodFraction), G(Show(_su.deltaP, r.PressureDropTotal)), G(r.TotalHead), G(r.DryPressureDrop),
                    G(r.WeirCrest), G(r.HoleVelocity), G(r.WeepPointVelocity), r.Regime, G(r.ClosedBalanceVelocity), G(r.OpenBalanceVelocity), G(r.CapPressureDrop), G(r.SlotOpening), G(r.SlotLoad), G(r.LiquidGradient), G(r.DynamicSeal),
                    G(r.DowncomerBackup), G(r.DowncomerBackupLimit), G(r.DowncomerResidenceTime), G(r.Entrainment),
                    G(r.EntrainmentEfficiencyFactor), G(r.OConnellEfficiency), G(r.LiquidHoldup), G(r.LoadingVelocity), G(Show(_su.distance, r.HETP)), G(Show(_su.distance, r.HETPRuleOfThumb)), G(r.HG), G(r.HL), G(r.HOG),
                    G(r.WettingRatio), string.Join(" ", r.Warnings).Replace(';', ',')));
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new System.IO.StreamWriter(stream);
        await writer.WriteAsync(sb.ToString());
        _status.Text = "Exported to " + file.Name + ".";
    }
}
