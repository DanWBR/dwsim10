using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DWSIM.Interfaces;
using DWSIM.SharedClasses.SystemsOfUnits;
using DWSIM.Thermodynamics.BaseClasses;
using DWSIM.Thermodynamics.CompoundEditing;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Edits the properties of a compound that is loaded in the simulation. The window works on a
/// clone; OK copies the clone INTO the live compound (same object, so every stream sees it), Cancel
/// drops it. The compound can be linked to a JSON file to reload from and save to, with a
/// side-by-side diff. Every field carries plain-language help, and each temperature-dependent
/// block has an explainer: which formula the equation number stands for, which coefficients it
/// uses, that T is in K, and which unit the raw equation must return for this compound's database.
/// Replaces the read-only PureCompoundPropertiesWindow (open a compound that is only in the
/// available list and the window is read-only).
/// </summary>
public sealed class CompoundPropertyEditorWindow : Window
{
    private readonly IFlowsheet _fs;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;
    private readonly string? _initial;

    private ICompoundConstantProperties? _live;
    private ConstantProperties? _clone;
    private bool _readOnly;
    private bool _dirty;

    private ComboBox _compounds = null!;
    private readonly TextBlock _status = new() { FontSize = UiScale.Font(11), Opacity = 0.85, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly ScrollViewer _centre = new() { Padding = new Thickness(8) };
    private readonly DataGrid _checklist = CompoundImportSupport.BuildChecklistGrid();
    private readonly StackPanel _warnings = new() { Spacing = 4 };
    private readonly TextBlock _warningsHeader = new() { FontWeight = FontWeight.SemiBold };
    private readonly CheckBox _showHelp = new() { Content = "Show the explanation under every field" };
    private readonly List<Control> _helpRows = new();
    private readonly HashSet<string> _invalid = new();

    // explainer + preview
    private ComboBox _blocks = null!;
    private readonly TextBlock _explainer = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _tryT = new() { Width = 100, TextAlignment = TextAlignment.Right };
    private readonly TextBlock _tryResult = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _tryLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private XYPlot _plot = new() { Margin = new Thickness(4), MinHeight = 260 };
    private readonly Dictionary<string, List<TextBox>> _coefficientBoxes = new();
    private readonly Dictionary<string, ComboBox> _equationCombos = new();

    // json link bar
    private readonly TextBlock _linkPath = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.9 };
    private Button _btnReload = null!, _btnSave = null!, _btnDiff = null!, _btnUnlink = null!, _btnLink = null!, _btnOk = null!;

    /// <summary>True after OK applied the edits to the simulation.</summary>
    public bool Saved { get; private set; }

    /// <param name="compound">Compound to open first; the first one in the simulation when null.</param>
    public CompoundPropertyEditorWindow(IFlowsheet flowsheet, string? compound = null)
    {
        _fs = flowsheet;
        _initial = compound;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;

        Title = "Compound Properties";
        Width = 1240;
        Height = 820;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        Content = BuildContent();

        var names = (_compounds.ItemsSource as List<string>) ?? new List<string>();
        var idx = _initial != null ? names.IndexOf(_initial) : -1;
        if (idx < 0 && names.Count > 0) idx = 0;
        if (idx >= 0) _compounds.SelectedIndex = idx;
    }

    // =========================================================================
    // layout
    // =========================================================================

    private Control BuildContent()
    {
        var names = _fs.SelectedCompounds.Keys.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (_initial != null && !names.Contains(_initial) && _fs.AvailableCompounds.ContainsKey(_initial))
        {
            names.Add(_initial);
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
        }

        _compounds = new ComboBox { ItemsSource = names, Width = 300 };
        _compounds.SelectionChanged += async (_, _) => await SwitchCompoundAsync();

        // one row, every control centred on the same horizontal axis
        var header = new Grid { Margin = new Thickness(12, 8, 12, 4), ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*") };
        var lblCompound = new TextBlock { Text = "Compound", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        _compounds.VerticalAlignment = VerticalAlignment.Center;
        _compounds.VerticalContentAlignment = VerticalAlignment.Center;
        _compounds.Margin = new Thickness(0, 0, 12, 0);
        _showHelp.VerticalAlignment = VerticalAlignment.Center;
        _showHelp.VerticalContentAlignment = VerticalAlignment.Center;
        _showHelp.Margin = new Thickness(0, 0, 16, 0);
        Grid.SetColumn(lblCompound, 0);
        Grid.SetColumn(_compounds, 1);
        Grid.SetColumn(_showHelp, 2);
        Grid.SetColumn(_status, 3);
        header.Children.Add(lblCompound);
        header.Children.Add(_compounds);
        header.Children.Add(_showHelp);
        header.Children.Add(_status);
        _showHelp.IsCheckedChanged += (_, _) => { foreach (var r in _helpRows) r.IsVisible = _showHelp.IsChecked == true; };

        // ---- left rail: what is available, what to look at
        var rail = new StackPanel { Spacing = 10, Margin = new Thickness(12, 10, 10, 10) };
        _checklist.Height = 300;
        rail.Children.Add(Group("Data available",
            Stack(Description("A quick view of which properties this compound carries. Missing ones are estimated by DWSIM from the basic constants."), _checklist)));
        var warnBody = new StackPanel { Spacing = 4 };
        warnBody.Children.Add(Description("Checks that run on every edit. They never block anything; they tell you what DWSIM will do with the data as it is."));
        warnBody.Children.Add(_warningsHeader);
        warnBody.Children.Add(_warnings);
        var btnRecheck = PanelButton("Run the checks again", () => RefreshWarnings());
        btnRecheck.HorizontalAlignment = HorizontalAlignment.Left;
        btnRecheck.Margin = new Thickness(0, 6, 0, 0);
        ToolTip.SetTip(btnRecheck, "The checks already run after every edit; use this if you want to be sure the list above is current.");
        warnBody.Children.Add(btnRecheck);
        rail.Children.Add(Group("Warnings", warnBody));

        // ---- right column: the explainer and the preview
        _blocks = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        _blocks.ItemsSource = CompoundPropertyDescriptors.Blocks.Select(b => b.DisplayName).ToList();
        _blocks.SelectionChanged += (_, _) => RefreshExplainer();
        var tryRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        tryRow.Children.Add(_tryLabel);
        tryRow.Children.Add(_tryT);
        var tryBtn = new Button { Content = "Compute" };
        tryBtn.Classes.Add("panel");
        tryBtn.Click += (_, _) => TryIt();
        _tryT.KeyDown += (_, e) => { if (e.Key == global::Avalonia.Input.Key.Enter) { TryIt(); e.Handled = true; } };
        tryRow.Children.Add(tryBtn);
        var right = new StackPanel { Spacing = 10, Margin = new Thickness(10, 10, 12, 10) };
        right.Children.Add(Group("Temperature-dependent property",
            Stack(Description("Pick a property to see what its equation number means, which coefficients it uses and in which unit the equation must return its value."), _blocks, _explainer)));
        right.Children.Add(Group("Try it",
            Stack(Description("Type a temperature and see the value the equation gives, both in the raw equation unit and converted to the simulation's unit. Compare it with a value you know."), tryRow, _tryResult)));
        right.Children.Add(Group("Preview", _plot));

        // three columns with a thin rule between them, so the rail, the editor and the explainer read as separate panes
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("280,Auto,*,Auto,440") };
        // AllowAutoHide = false: the scrollbar takes its own column instead of floating over the text
        var railScroll = new ScrollViewer { Content = rail, AllowAutoHide = false };
        Grid.SetColumn(railScroll, 0);
        var rule1 = ColumnRule();
        Grid.SetColumn(rule1, 1);
        _centre.Padding = new Thickness(14, 10, 14, 10);
        _centre.AllowAutoHide = false;
        Grid.SetColumn(_centre, 2);
        var rule2 = ColumnRule();
        Grid.SetColumn(rule2, 3);
        var rightScroll = new ScrollViewer { Content = right, AllowAutoHide = false };
        Grid.SetColumn(rightScroll, 4);
        grid.Children.Add(railScroll);
        grid.Children.Add(rule1);
        grid.Children.Add(_centre);
        grid.Children.Add(rule2);
        grid.Children.Add(rightScroll);

        // ---- bottom: json link bar and buttons
        _btnLink = PanelButton("Link...", async () => await LinkAsync());
        _btnReload = PanelButton("Reload from file", async () => await ReloadFromFileAsync(true));
        _btnSave = PanelButton("Save to file", async () => await SaveToFileAsync());
        _btnDiff = PanelButton("Show diff", async () => await ReloadFromFileAsync(false));
        _btnUnlink = PanelButton("Unlink", () => { if (_clone != null) _clone.LinkedJsonFile = ""; MarkDirty(); RefreshLinkBar(); });
        var linkButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _btnLink, _btnReload, _btnSave, _btnDiff, _btnUnlink } };
        var linkBar = new DockPanel { Margin = new Thickness(10, 4, 10, 4) };
        var linkCaption = new TextBlock { Text = "Linked JSON file:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), FontWeight = FontWeight.SemiBold };
        DockPanel.SetDock(linkCaption, global::Avalonia.Controls.Dock.Left);
        DockPanel.SetDock(linkButtons, global::Avalonia.Controls.Dock.Right);
        linkBar.Children.Add(linkCaption);
        linkBar.Children.Add(linkButtons);
        linkBar.Children.Add(_linkPath);
        ToolTip.SetTip(linkBar, "Link this compound to a .json file. Reload brings the file's values into the editor (you still press OK to apply them to the simulation); Save writes what you see here to the file; Show diff lists what differs between the file and the editor.");

        _btnOk = new Button { Content = "OK", Width = 90, IsDefault = true };
        _btnOk.Classes.Add("dialog");
        _btnOk.Click += async (_, _) => await OnOkAsync();
        var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true };
        cancel.Classes.Add("dialog");
        cancel.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 6, 12, 12), Children = { cancel, _btnOk } };

        var bottom = new StackPanel();
        bottom.Children.Add(new Border { Height = 1, Background = RuleBrush, Margin = new Thickness(0, 4, 0, 6) });
        bottom.Children.Add(new Border { Classes = { "group" }, Child = linkBar, Margin = new Thickness(12, 0, 12, 0) });
        bottom.Children.Add(buttons);

        var root = new DockPanel();
        DockPanel.SetDock(header, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(bottom);
        root.Children.Add(grid);
        return root;
    }

    // =========================================================================
    // compound selection and the clone
    // =========================================================================

    private async System.Threading.Tasks.Task SwitchCompoundAsync()
    {
        if (_compounds.SelectedItem is not string name) return;

        if (_dirty && _live != null && _clone != null && !_readOnly)
        {
            if (await ConfirmAsync("Apply changes?", $"Apply the changes made to '{_live.Name}' to the simulation before switching? Choosing No discards them."))
                Apply();
        }

        _live = CompoundEditor.FindLive(_fs, name);
        _readOnly = _live == null;
        if (_live == null && _fs.AvailableCompounds.TryGetValue(name, out var available)) _live = available;
        if (_live == null) return;

        _clone = CompoundEditor.BeginEdit(_live);
        _dirty = false;
        _invalid.Clear();
        _btnOk.IsVisible = !_readOnly;
        _btnLink.IsEnabled = !_readOnly;
        _btnReload.IsEnabled = !_readOnly;

        RebuildPanel();
        RefreshAll();
        if (_blocks.SelectedIndex < 0) _blocks.SelectedIndex = 0; else RefreshExplainer();
        SetStatus(_readOnly
            ? "This compound is not in the simulation, so it is shown read-only. Add it to the simulation to edit it."
            : "Original (no changes yet). Values are applied to the simulation only when you press OK.");
    }

    private void MarkDirty()
    {
        _dirty = true;
        if (!_readOnly) SetStatus("Modified (not applied yet). Press OK to apply to the simulation, Cancel to discard.");
    }

    private void SetStatus(string text) => _status.Text = text;

    /// <summary>Everything that depends on the clone's values: checklist, warnings, explainer, preview, link bar.</summary>
    private void RefreshAll()
    {
        RefreshChecklist();
        RefreshWarnings();
        RefreshLinkBar();
        RefreshExplainer();
    }

    private void AfterEdit()
    {
        MarkDirty();
        // coalesce the burst of edits into one refresh
        Dispatcher.UIThread.Post(RefreshAll, DispatcherPriority.Background);
    }

    // =========================================================================
    // the descriptor-driven panel
    // =========================================================================

    private void RebuildPanel()
    {
        _helpRows.Clear();
        _coefficientBoxes.Clear();
        _equationCombos.Clear();
        if (_clone == null) { _centre.Content = null; return; }

        var content = new StackPanel { Spacing = 6 };
        var advanced = new StackPanel { Spacing = 6 };

        foreach (var g in CompoundPropertyDescriptors.Groups)
        {
            var rows = CompoundPropertyDescriptors.ScalarsInGroup(g.Group);
            if (rows.Count == 0) continue;
            var panel = new AvaloniaEditorPanel();
            panel.CreateAndAddDescriptionRow(g.Intro);
            foreach (var d in rows) AddRow(panel, d);
            (g.IsAdvanced ? advanced : content).Children.Add(Group(g.DisplayName, panel));

            // the temperature-dependent blocks sit right after the formation data
            if (g.Group == CompoundPropertyGroup.Formation)
            {
                var blocksPanel = new StackPanel { Spacing = 6 };
                blocksPanel.Children.Add(new TextBlock
                {
                    Text = "Each property below is a formula of temperature identified by an equation number, with up to five coefficients A to E. T is always in K. The unit the formula must return depends on the property and on the compound's source database; the explainer on the right spells it out for the selected property.",
                    TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(11), Opacity = 0.7, Margin = new Thickness(0, 0, 0, 4)
                });
                foreach (var b in CompoundPropertyDescriptors.Blocks) blocksPanel.Children.Add(BuildBlock(b));
                content.Children.Add(Group("Temperature-dependent properties", blocksPanel));
            }
        }

        if (advanced.Children.Count > 0)
            content.Children.Add(new Expander { Header = "Advanced (electrolytes, black oil, petroleum fractions)", Content = advanced, IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch });

        _centre.Content = content;
        foreach (var r in _helpRows) r.IsVisible = _showHelp.IsChecked == true;
    }

    private void AddRow(AvaloniaEditorPanel panel, CompoundPropertyDescriptor d)
    {
        var label = d.DisplayName;
        var unit = d.DisplayUnit(_su);
        if (!string.IsNullOrEmpty(unit) && d.IsNumeric) label += " (" + unit + ")";

        Control? row = null;
        switch (d.Kind)
        {
            case CompoundPropertyKind.Text:
                if (d.IsReadOnly || _readOnly)
                {
                    row = panel.CreateAndAddTwoLabelsRow(label, Convert.ToString(d.GetValue(_clone!)) ?? "");
                }
                else if (d.Key == "Comments")
                {
                    panel.CreateAndAddLabelRow2(label);
                    row = panel.CreateAndAddMultilineTextBoxRow(Convert.ToString(d.GetValue(_clone!)) ?? "", false, false,
                        (tb, _) => { d.SetValue(_clone!, tb.Text ?? ""); MarkDirty(); });
                }
                else
                {
                    row = panel.CreateAndAddStringEditorRow(label, Convert.ToString(d.GetValue(_clone!)) ?? "",
                        (tb, _) => { d.SetValue(_clone!, tb.Text ?? ""); AfterEdit(); if (d.Key == "Formula") RebuildPanel(); }, 260);
                }
                break;

            case CompoundPropertyKind.Boolean:
                var cb = panel.CreateAndAddCheckBoxRow(label, Convert.ToBoolean(d.GetValue(_clone!)),
                    (c, _) => { d.SetValue(_clone!, c.IsChecked == true); AfterEdit(); });
                cb.IsEnabled = !d.IsReadOnly && !_readOnly;
                row = cb;
                break;

            case CompoundPropertyKind.Elements:
            case CompoundPropertyKind.Groups:
                row = panel.CreateAndAddTwoLabelsRow2(label, GroupsText(d));
                break;

            case CompoundPropertyKind.Tabular:
                row = panel.CreateAndAddTwoLabelsRow(label, CompoundDiff.FormatValue(d.Key, d.GetValue(_clone!), _su, _nf));
                break;

            default:
                row = NumericRow(panel, d, label);
                break;
        }

        if (row != null) ToolTip.SetTip(row, d.Help);
        AddHelpRow(panel, d.Help);
    }

    /// <summary>A numeric field that commits on Enter or focus loss, turns red while it does not parse, and blocks OK while red.</summary>
    private TextBox NumericRow(AvaloniaEditorPanel panel, CompoundPropertyDescriptor d, string label)
    {
        var tb = new TextBox { Width = 160, TextAlignment = TextAlignment.Right, Text = DisplayText(d), IsEnabled = !d.IsReadOnly && !_readOnly };
        var committed = tb.Text ?? "";

        bool Parses(string text, out double v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(text)) return d.Kind == CompoundPropertyKind.NullableDouble;
            return TryParse(text, out v);
        }

        void Commit()
        {
            var text = tb.Text ?? "";
            if (text == committed) return;
            if (!Parses(text, out var v))
            {
                _invalid.Add(d.Key);
                tb.Foreground = Brushes.Red;
                SetStatus($"'{text}' is not a number. Fix the value shown in red before pressing OK.");
                return;
            }
            _invalid.Remove(d.Key);
            tb.ClearValue(TemplatedControl.ForegroundProperty);
            if (string.IsNullOrWhiteSpace(text)) d.SetValue(_clone!, null);
            else d.SetFromDisplay(_clone!, _su, v);
            committed = DisplayText(d);
            tb.Text = committed;
            AfterEdit();
            RefreshCoefficientStates();
        }

        tb.TextChanged += (_, _) =>
        {
            var pending = (tb.Text ?? "") != committed;
            if (!pending) { tb.ClearValue(TemplatedControl.ForegroundProperty); return; }
            tb.Foreground = Parses(tb.Text ?? "", out _) ? Brushes.Blue : Brushes.Red;
            ToolTip.SetTip(tb, "Press Enter to apply this value.");
        };
        tb.KeyDown += (_, e) =>
        {
            if (e.Key == global::Avalonia.Input.Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == global::Avalonia.Input.Key.Escape) { tb.Text = committed; tb.ClearValue(TemplatedControl.ForegroundProperty); e.Handled = true; }
        };
        tb.LostFocus += (_, _) => Commit();

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, tb));
        return tb;
    }

    private string DisplayText(CompoundPropertyDescriptor d)
    {
        var v = d.GetDisplayValue(_clone!, _su);
        if (v == null) return "";
        if (v is double x) return x.ToString(_nf, CultureInfo.CurrentCulture);
        return Convert.ToString(v, CultureInfo.CurrentCulture) ?? "";
    }

    private void AddHelpRow(AvaloniaEditorPanel panel, string help)
    {
        if (string.IsNullOrWhiteSpace(help)) return;
        var lbl = panel.CreateAndAddDescriptionRow(help);
        lbl.Margin = new Thickness(12, 0, 0, 6);
        lbl.IsVisible = false;
        _helpRows.Add(lbl);
    }

    private string GroupsText(CompoundPropertyDescriptor d)
    {
        var list = d.GetValue(_clone!) as System.Collections.SortedList;
        if (list == null || list.Count == 0) return "(none)";
        Func<int, string>? nameOf = d.Key switch
        {
            "UNIFACGroups" => id => new Thermodynamics.PropertyPackages.Auxiliary.Unifac().ID2Group(id),
            "MODFACGroups" => id => new Thermodynamics.PropertyPackages.Auxiliary.Modfac().ID2Group(id),
            "NISTMODFACGroups" => id => new Thermodynamics.PropertyPackages.Auxiliary.NISTMFAC().ID2Group(id),
            _ => null
        };
        var terms = new List<string>();
        foreach (System.Collections.DictionaryEntry kv in list)
        {
            var key = Convert.ToString(kv.Key) ?? "";
            var count = CompoundDiff.ToDouble(kv.Value);
            if (nameOf != null)
            {
                try { key = nameOf(int.Parse(key)); } catch (Exception) { }
            }
            terms.Add(key + " " + count.ToString("G5", CultureInfo.CurrentCulture));
        }
        return string.Join(", ", terms);
    }

    // ---- temperature-dependent blocks ---------------------------------------

    private Control BuildBlock(TemperatureDependentBlock b)
    {
        var panel = new AvaloniaEditorPanel();
        panel.CreateAndAddDescriptionRow(b.Help);

        // the equation number, with the formula text next to it
        var options = CompoundEquationCatalog.All().Select(i => i.ListText).ToList();
        var current = b.Equation(_clone!);
        var info = CompoundEquationCatalog.Describe(current);
        var selected = info.ListText;
        if (info.IsExpression) options.Add(selected);
        var combo = panel.CreateAndAddDropDownRow("Equation", options, Math.Max(0, options.IndexOf(selected)), null, 360);
        combo.IsEnabled = !_readOnly;
        _equationCombos[b.Key] = combo;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0) return;
            var all = CompoundEquationCatalog.All();
            var id = combo.SelectedIndex < all.Count ? all[combo.SelectedIndex].Id : b.Equation(_clone!);
            CompoundPropertyDescriptors.ByKey(b.EquationKey).SetValue(_clone!, id);
            SelectBlock(b);
            AfterEdit();
            RefreshCoefficientStates();
        };
        ToolTip.SetTip(combo, "The number identifies the formula DWSIM evaluates. The list shows each formula; the explainer on the right describes the selected one in words.");

        var expr = panel.CreateAndAddStringEditorRow("Custom expression (optional)", info.IsExpression ? current : "",
            (tb, _) =>
            {
                var text = (tb.Text ?? "").Trim();
                if (text == "") return;
                string msg = "";
                if (!CompoundEquationCatalog.TryValidateExpression(text, ref msg))
                {
                    SetStatus("The expression cannot be evaluated: " + msg);
                    tb.Foreground = Brushes.Red;
                    return;
                }
                tb.ClearValue(TemplatedControl.ForegroundProperty);
                CompoundPropertyDescriptors.ByKey(b.EquationKey).SetValue(_clone!, text);
                var listText = CompoundEquationCatalog.Describe(text).ListText;
                var opts = CompoundEquationCatalog.All().Select(i => i.ListText).ToList();
                opts.Add(listText);
                combo.SetOptions(opts);
                combo.SelectedIndex = opts.Count - 1;
                SelectBlock(b);
                AfterEdit();
            }, 360);
        expr.IsEnabled = !_readOnly;
        ToolTip.SetTip(expr, "Instead of a number you can type your own formula, for example \"y = A + B * T\" or \"ln(P) = A - B / (T + C) where T in C and P in bar\". Leave it empty to use the equation number above.");

        var boxes = new List<TextBox>();
        foreach (var k in b.CoefficientKeys)
        {
            var d = CompoundPropertyDescriptors.ByKey(k);
            boxes.Add(NumericRow(panel, d, "Coefficient " + k.Substring(k.Length - 1)));
        }
        _coefficientBoxes[b.Key] = boxes;

        if (b.TminKey != null) NumericRow(panel, CompoundPropertyDescriptors.ByKey(b.TminKey), "Valid from (" + _su.temperature + ")");
        if (b.TmaxKey != null) NumericRow(panel, CompoundPropertyDescriptors.ByKey(b.TmaxKey), "Valid to (" + _su.temperature + ")");

        var show = panel.CreateAndAddButtonRow("Explain and preview this property", null, (_, _) => SelectBlock(b));
        ToolTip.SetTip(show, "Shows this property in the explainer and the preview on the right.");

        var group = Group(b.DisplayName, panel);
        group.GotFocus += (_, _) => SelectBlock(b);
        return group;
    }

    private void SelectBlock(TemperatureDependentBlock b)
    {
        var idx = CompoundPropertyDescriptors.Blocks.IndexOf(b);
        if (idx >= 0 && _blocks.SelectedIndex != idx) _blocks.SelectedIndex = idx; else RefreshExplainer();
    }

    private TemperatureDependentBlock? CurrentBlock()
    {
        var idx = _blocks.SelectedIndex;
        if (idx < 0 || idx >= CompoundPropertyDescriptors.Blocks.Count) return null;
        return CompoundPropertyDescriptors.Blocks[idx];
    }

    /// <summary>Greys the coefficient boxes the selected formula does not read.</summary>
    private void RefreshCoefficientStates()
    {
        if (_clone == null) return;
        foreach (var b in CompoundPropertyDescriptors.Blocks)
        {
            if (!_coefficientBoxes.TryGetValue(b.Key, out var boxes)) continue;
            var info = CompoundEquationCatalog.Describe(b.Equation(_clone));
            for (int i = 0; i < boxes.Count; i++)
            {
                var used = info.IsNotDefined ? (b.Key == "EnthalpyOfVaporization" && i == 0) : info.IsExpression || info.UsesCoefficient((char)('A' + i));
                boxes[i].Opacity = used ? 1.0 : 0.45;
                ToolTip.SetTip(boxes[i], used ? null : "This formula does not use this coefficient; the value is kept but ignored.");
            }
        }
    }

    // =========================================================================
    // explainer, try-it and preview
    // =========================================================================

    private void RefreshExplainer()
    {
        var b = CurrentBlock();
        if (b == null || _clone == null) { _explainer.Text = ""; return; }

        // the same words the Windows editor shows, from the shared engine explainer
        _explainer.Text = CompoundExplainer.ExplainBlock(b, _clone, _su, _nf);
        _tryLabel.Text = "T (" + _su.temperature + ")";
        if (string.IsNullOrWhiteSpace(_tryT.Text))
            _tryT.Text = CompoundExplainer.SuggestedTemperature(b, _clone, _su).ToString(_nf, CultureInfo.CurrentCulture);
        TryIt();
        RefreshPreview();
    }

    private void TryIt()
    {
        var b = CurrentBlock();
        if (b == null || _clone == null) return;
        if (!TryParse(_tryT.Text ?? "", out var tDisplay)) { _tryResult.Text = "Type a temperature first."; return; }
        _tryResult.Text = CompoundExplainer.TryIt(b, _clone, _su, tDisplay, _nf);
    }

    private void RefreshPreview()
    {
        var b = CurrentBlock();
        if (b == null || _clone == null) return;
        try
        {
            var r = b.DefaultRange(_clone);
            var pts = b.Sample(_clone, r.Tmin, r.Tmax, 51);
            var yUnit = b.DisplayUnit(_su);
            var xs = pts.Select(p => Converter.ConvertFromSI(_su.temperature, p.T)).ToArray();
            var ys = pts.Select(p => Converter.ConvertFromSI(yUnit, p.Y)).ToArray();
            _plot.Clear();
            _plot.PlotTitle = b.DisplayName;
            _plot.XAxisTitle = "Temperature (" + _su.temperature + ")";
            _plot.YAxisTitle = b.DisplayName + " (" + yUnit + ")";
            if (xs.Length > 1) _plot.AddSeries(b.DisplayName, xs, ys);
            _plot.InvalidateVisual();
        }
        catch (Exception)
        {
            _plot.Clear();
        }
    }

    // =========================================================================
    // rail: checklist and warnings
    // =========================================================================

    private void RefreshChecklist()
    {
        if (_clone == null) return;
        try { _checklist.ItemsSource = CompoundImportSupport.Checklist(_clone); } catch (Exception) { }
    }

    private void RefreshWarnings()
    {
        _warnings.Children.Clear();
        if (_clone == null) return;
        var issues = CompoundValidator.Validate(_clone).OrderByDescending(i => i.Severity).ToList();
        var nWarn = issues.Count(i => i.Severity == CompoundIssueSeverity.Warning);
        _warningsHeader.Text = issues.Count == 0 ? "No warnings." : $"{nWarn} warning(s), {issues.Count - nWarn} note(s).";
        foreach (var i in issues)
        {
            var tb = new TextBlock
            {
                Text = (i.Severity == CompoundIssueSeverity.Warning ? "Warning: " : "Note: ") + i.Message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = UiScale.Font(11),
                Opacity = i.Severity == CompoundIssueSeverity.Warning ? 1.0 : 0.75
            };
            _warnings.Children.Add(tb);
        }
    }

    // =========================================================================
    // json link
    // =========================================================================

    private void RefreshLinkBar()
    {
        var linked = _clone?.LinkedJsonFile ?? "";
        _linkPath.Text = linked == "" ? "(not linked)" : linked;
        ToolTip.SetTip(_linkPath, linked == "" ? null : linked);
        var has = linked != "";
        _btnReload.IsEnabled = has && !_readOnly;
        _btnDiff.IsEnabled = has;
        _btnUnlink.IsEnabled = has && !_readOnly;
    }

    private async System.Threading.Tasks.Task LinkAsync()
    {
        if (_clone == null) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Link to a compound JSON file",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("JSON compound files") { Patterns = new[] { "*.json" } } }
        });
        var file = files.FirstOrDefault();
        if (file == null) return;
        _clone.LinkedJsonFile = file.Path.LocalPath;
        MarkDirty();
        RefreshLinkBar();
        SetStatus("Linked to " + file.Path.LocalPath + ". Use Reload to bring its values into the editor, or Save to write the editor's values to it.");
    }

    private async System.Threading.Tasks.Task ReloadFromFileAsync(bool offerReloadAll)
    {
        if (_clone == null) return;
        var path = CompoundEditor.ResolveLinkedJson(_fs, _clone);
        if (path == null)
        {
            await MsgAsync("No file", "The linked file was not found. Use Link... to choose the JSON file first.");
            return;
        }
        ConstantProperties fromFile;
        try { fromFile = CompoundEditor.LoadJson(path); }
        catch (Exception ex) { await MsgAsync("Cannot read the file", ex.Message); return; }

        if (!string.Equals(fromFile.Name, _clone.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (!await ConfirmAsync("Different compound", $"The file contains '{fromFile.Name}', but the editor shows '{_clone.Name}'. Compare them anyway?")) return;
        }

        var diff = CompoundDiff.Compare(_clone, fromFile);
        if (!diff.Any(e => e.Differs))
        {
            await MsgAsync("No differences", "The file and the editor hold the same values.");
            return;
        }

        var dlg = new CompoundDiffWindow(_clone, fromFile, _su, _nf, "In the editor", "In the file", _readOnly ? false : true, offerReloadAll);
        await dlg.ShowDialog(this);
        if (dlg.Applied)
        {
            _clone.LinkedJsonFile = path;
            MarkDirty();
            RebuildPanel();
            RefreshAll();
            SetStatus("Values taken from the file. They are applied to the simulation only when you press OK.");
        }
    }

    private async System.Threading.Tasks.Task SaveToFileAsync()
    {
        if (_clone == null) return;
        var path = _clone.LinkedJsonFile;
        if (string.IsNullOrWhiteSpace(path))
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save compound to JSON",
                SuggestedFileName = _clone.Name + ".json",
                FileTypeChoices = new[] { new FilePickerFileType("JSON compound files") { Patterns = new[] { "*.json" } } }
            });
            if (file == null) return;
            path = file.Path.LocalPath;
        }
        try
        {
            CompoundEditor.SaveToJson(_clone, path);
            if (!_readOnly) MarkDirty();
            RefreshLinkBar();
            SetStatus("Saved to " + path + ". The file holds what the editor shows now.");
        }
        catch (Exception ex)
        {
            await MsgAsync("Cannot save", ex.Message);
        }
    }

    // =========================================================================
    // OK / apply
    // =========================================================================

    private async System.Threading.Tasks.Task OnOkAsync()
    {
        if (_readOnly) { Close(); return; }
        if (_invalid.Count > 0)
        {
            await MsgAsync("Cannot apply", "Fix the values shown in red first: they are not numbers.");
            return;
        }
        try
        {
            Apply();
            Saved = true;
            Close();
        }
        catch (Exception ex)
        {
            await MsgAsync("Cannot apply", ex.Message);
        }
    }

    private void Apply()
    {
        if (_live == null || _clone == null || _readOnly) return;
        CompoundEditor.ApplyInPlace(_fs, _live.Name, _clone);
        Saved = true;
        _dirty = false;
        try { _fs.UpdateInterface(); } catch (Exception) { }
    }

    // =========================================================================
    // small helpers
    // =========================================================================

    private static readonly IBrush RuleBrush = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128));

    /// <summary>A vertical hairline between two panes; the low-alpha grey reads on both themes.</summary>
    private static Control ColumnRule() => new Border { Width = 1, Background = RuleBrush, Margin = new Thickness(2, 8, 2, 8) };

    private static Control Group(string header, Control content)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = header, FontWeight = FontWeight.SemiBold, Margin = new Thickness(2, 2, 0, 6) });
        stack.Children.Add(content);
        var border = new Border { Child = stack, Margin = new Thickness(0, 0, 0, 8) };
        border.Classes.Add("group");
        return border;
    }

    private static StackPanel Stack(params Control[] children)
    {
        var s = new StackPanel { Spacing = 6 };
        foreach (var c in children) s.Children.Add(c);
        return s;
    }

    private static TextBlock Description(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(11), Opacity = 0.7
    };

    private static Button PanelButton(string label, Action onClick)
    {
        var b = new Button { Content = label };
        b.Classes.Add("panel");
        b.Click += (_, _) => onClick();
        return b;
    }

    private static Button PanelButton(string label, Func<System.Threading.Tasks.Task> onClick)
    {
        var b = new Button { Content = label };
        b.Classes.Add("panel");
        b.Click += async (_, _) => await onClick();
        return b;
    }

    internal static bool TryParse(string text, out double value)
    {
        value = 0.0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        const NumberStyles ns = NumberStyles.Float;
        if (double.TryParse(t, ns, CultureInfo.CurrentCulture, out value)) return true;
        if (double.TryParse(t, ns, CultureInfo.InvariantCulture, out value)) return true;
        var swapped = t.Replace(",", ".");
        if (swapped.IndexOf('.') == swapped.LastIndexOf('.') && double.TryParse(swapped, ns, CultureInfo.InvariantCulture, out value)) return true;
        return false;
    }

    private async System.Threading.Tasks.Task MsgAsync(string title, string message)
    {
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Classes.Add("dialog");
        var dlg = new Window
        {
            Title = title, Width = 420, Height = 180, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Icon = IconHelper.GetWindowIcon()
        };
        var body = new DockPanel();
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 16, 12), Children = { ok } };
        DockPanel.SetDock(bp, global::Avalonia.Controls.Dock.Bottom);
        body.Children.Add(bp);
        body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16) });
        dlg.Content = body;
        ok.Click += (_, _) => dlg.Close();
        await dlg.ShowDialog(this);
    }

    private async System.Threading.Tasks.Task<bool> ConfirmAsync(string title, string message)
    {
        var result = false;
        var yes = new Button { Content = "Yes", Width = 80, IsDefault = true };
        yes.Classes.Add("dialog");
        var no = new Button { Content = "No", Width = 80, IsCancel = true };
        no.Classes.Add("dialog");
        var dlg = new Window
        {
            Title = title, Width = 440, Height = 180, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Icon = IconHelper.GetWindowIcon()
        };
        var body = new DockPanel();
        var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 0, 16, 12), Children = { no, yes } };
        DockPanel.SetDock(bp, global::Avalonia.Controls.Dock.Bottom);
        body.Children.Add(bp);
        body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16) });
        dlg.Content = body;
        yes.Click += (_, _) => { result = true; dlg.Close(); };
        no.Click += (_, _) => dlg.Close();
        await dlg.ShowDialog(this);
        return result;
    }
}
