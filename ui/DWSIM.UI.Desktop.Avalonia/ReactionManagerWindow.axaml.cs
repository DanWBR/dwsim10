using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using FontWeight   = global::Avalonia.Media.FontWeight;
using Brushes      = global::Avalonia.Media.Brushes;
using TextWrapping = global::Avalonia.Media.TextWrapping;

namespace DWSIM.UI.Desktop.Avalonia;

public partial class ReactionManagerWindow : Window
{
    private readonly IFlowsheet _flowsheet;

    /// <summary>The XAML root, kept so the sub-dialogs can find their owner once it is embedded.</summary>
    private readonly Control? _root;

    public ReactionManagerWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        _root = Content as Control;
        WireEvents();
        RefreshReactionList();
        RefreshReactionSetList();
    }

    /// <summary>
    /// Detaches the manager from its window so another window can host it: the Simulation Settings
    /// window shows it on its Reactions tab, the way the WinForms settings form does.
    /// </summary>
    public static Control CreateEmbeddedContent(IFlowsheet flowsheet)
    {
        var window = new ReactionManagerWindow(flowsheet);
        var content = (Control)window.Content!;
        window.Content = null;
        window.BtnClose.IsVisible = false;
        // the handlers live on the window, so the embedded control has to keep it alive
        content.Tag = window;
        return content;
    }

    /// <summary>The window the sub-dialogs belong to: this one, or the host when embedded.</summary>
    private Window DialogOwner()
    {
        if (_root != null && TopLevel.GetTopLevel(_root) is Window host) return host;
        return this;
    }

    // -------------------------------------------------------------------------
    // Event wiring
    // -------------------------------------------------------------------------

    private void WireEvents()
    {
        BtnClose.Click += (_, _) => Close();

        // Reactions tab
        LbReactions.SelectionChanged += (_, _) => ShowReactionDetail();
        BtnNewReaction.Click    += async (_, _) => await OnNewReaction();
        BtnDeleteReaction.Click += (_, _) => OnDeleteReaction();

        // Reaction Sets tab
        LbReactionSets.SelectionChanged  += (_, _) => RefreshSetReactions();
        BtnNewReactionSet.Click    += async (_, _) => await OnNewReactionSet();
        BtnDeleteReactionSet.Click += (_, _) => OnDeleteReactionSet();
        BtnAddToSet.Click      += (_, _) => OnAddReactionToSet();
        BtnRemoveFromSet.Click += (_, _) => OnRemoveReactionFromSet();
    }

    // -------------------------------------------------------------------------
    // Reaction list helpers
    // -------------------------------------------------------------------------

    private void RefreshReactionList()
    {
        var sel = (LbReactions.SelectedItem as RxnItem)?.ID;
        LbReactions.Items.Clear();
        foreach (var r in _flowsheet.Reactions.Values)
            LbReactions.Items.Add(new RxnItem(r.Name, r.ID, r.ReactionType.ToString()));
        if (sel != null)
        {
            for (int i = 0; i < LbReactions.Items.Count; i++)
                if (((RxnItem)LbReactions.Items[i]!).ID == sel)
                { LbReactions.SelectedIndex = i; break; }
        }
        RefreshAllReactionsForSet();
    }

    private async System.Threading.Tasks.Task OnNewReaction()
    {
        var name = await ShowInputDialogAsync("New Reaction", "Reaction name:", "New Reaction");
        if (string.IsNullOrWhiteSpace(name)) return;

        // Pick type
        var typeStr = await ShowChoiceDialogAsync("Reaction Type", "Select type:",
            new[] { "Conversion", "Equilibrium", "Kinetic", "Heterogeneous Catalytic" });
        if (typeStr == null) return;

        var phase = "Mixture";
        var baseComp = _flowsheet.SelectedCompounds.Keys.FirstOrDefault() ?? "";
        var expression = "0.5";

        IReaction rxn;
        try
        {
            var stoich = new Dictionary<string, double> { { baseComp, -1.0 } };
            rxn = typeStr switch
            {
                "Equilibrium" => _flowsheet.CreateEquilibriumReaction(
                    name, "", stoich, baseComp, phase, "MolarConc", "mol/L", 0.0, ""),
                "Kinetic" => _flowsheet.CreateKineticReaction(
                    name, "", stoich,
                    new Dictionary<string, double> { { baseComp, 1.0 } },
                    new Dictionary<string, double>(),
                    baseComp, phase, "MolarConc", "mol/L", "mol/L/s",
                    1e6, 50000, 0, 0, "", ""),
                "Heterogeneous Catalytic" => _flowsheet.CreateHetCatReaction(
                    name, "", stoich, baseComp, phase, "MolarConc", "mol/L", "mol/g/s", "", "1"),
                _ => _flowsheet.CreateConversionReaction(
                    name, "", stoich, baseComp, phase, expression),
            };
            _flowsheet.AddReaction(rxn);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Error", $"Could not create reaction: {ex.Message}");
            return;
        }

        RefreshReactionList();
        // Select the new one
        for (int i = 0; i < LbReactions.Items.Count; i++)
            if (((RxnItem)LbReactions.Items[i]!).ID == rxn.ID)
            { LbReactions.SelectedIndex = i; break; }
    }

    private void OnDeleteReaction()
    {
        if (LbReactions.SelectedItem is not RxnItem item) return;
        _flowsheet.Reactions.Remove(item.ID);
        // Also remove from any reaction sets
        foreach (var rs in _flowsheet.ReactionSets.Values)
            if (rs.Reactions.ContainsKey(item.ID))
                rs.Reactions.Remove(item.ID);
        ReactionDetailPanel.Children.Clear();
        ReactionDetailPanel.Children.Add(ReactionDetailHint);
        RefreshReactionList();
    }

    // -------------------------------------------------------------------------
    // Reaction detail panel
    // -------------------------------------------------------------------------

    private void ShowReactionDetail()
    {
        ReactionDetailPanel.Children.Clear();

        if (LbReactions.SelectedItem is not RxnItem item ||
            !_flowsheet.Reactions.TryGetValue(item.ID, out var rxn))
        {
            ReactionDetailPanel.Children.Add(new TextBlock
            {
                Text = "Select or create a reaction to view/edit its properties.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Gray
            });
            return;
        }

        AddDetailLabel(rxn.Name);

        // Name
        AddDetailTextRow("Name", rxn.Name, v => { rxn.Name = v; RefreshReactionList(); });
        AddDetailTextRow("Description", rxn.Description ?? "", v => rxn.Description = v);

        AddDetailReadRow("Reaction Type", rxn.ReactionType.ToString());
        AddDetailDropDown("Phase", new[] { "Liquid", "Vapor", "Mixture", "Solid", "Liquid_Solid", "Vapor_Solid" },
            rxn.ReactionPhase.ToString(),
            v => { if (Enum.TryParse<ReactionPhase>(v, out var p)) rxn.ReactionPhase = p; });
        var reactants = rxn.Components.Values.Where(x => x.StoichCoeff < 0).Select(x => x.CompName).ToArray();
        if (reactants.Length > 0)
        {
            AddDetailDropDown("Base Reactant", reactants, rxn.BaseReactant ?? reactants[0],
                v => { rxn.BaseReactant = v; SetBaseReactant(rxn, v); });
        }
        else
        {
            AddDetailReadRow("Base Reactant", "add a reactant first");
        }

        if (rxn.ReactionType == ReactionType.Conversion)
        {
            AddDetailLabel("Conversion");
            AddDetailTextRow("Conversion Expression (T in K)", rxn.Expression ?? "0.5",
                v => rxn.Expression = v);
        }
        else if (rxn.ReactionType == ReactionType.Equilibrium)
        {
            AddDetailLabel("Equilibrium");
            AddDetailDropDown("Keq Option", new[] { "Gibbs Energy", "Expression", "Constant" },
                rxn.KExprType.ToString(),
                v =>
                {
                    rxn.KExprType = v switch { "Expression" => KOpt.Expression, "Constant" => KOpt.Constant, _ => KOpt.Gibbs };
                    ShowReactionDetail(); // rebuild
                });
            if (rxn.KExprType == KOpt.Constant)
                AddDetailTextRow("Constant K Value", rxn.ConstantKeqValue.ToString("G6"),
                    v => { if (double.TryParse(v, out var d)) rxn.ConstantKeqValue = d; });
            else if (rxn.KExprType == KOpt.Expression)
                AddDetailTextRow("ln(Keq) = f(T) Expression", rxn.Expression ?? "",
                    v => rxn.Expression = v);
            AddDetailTextRow("Approach to Equilibrium", rxn.Approach.ToString("G6"),
                v => { if (double.TryParse(v, out var d)) rxn.Approach = d; });
        }
        else if (rxn.ReactionType is ReactionType.Kinetic or ReactionType.Heterogeneous_Catalytic)
        {
            AddDetailLabel("Kinetics");
            AddDetailTextRow("Forward Pre-exp Factor A", rxn.A_Forward.ToString("G6"),
                v => { if (double.TryParse(v, out var d)) rxn.A_Forward = d; });
            AddDetailTextRow("Forward Activation Energy E (J/mol)", rxn.E_Forward.ToString("G6"),
                v => { if (double.TryParse(v, out var d)) rxn.E_Forward = d; });
            AddDetailTextRow("Reverse Pre-exp Factor A", rxn.A_Reverse.ToString("G6"),
                v => { if (double.TryParse(v, out var d)) rxn.A_Reverse = d; });
            AddDetailTextRow("Reverse Activation Energy E (J/mol)", rxn.E_Reverse.ToString("G6"),
                v => { if (double.TryParse(v, out var d)) rxn.E_Reverse = d; });
            if (rxn.ReactionType == ReactionType.Heterogeneous_Catalytic)
            {
                AddDetailTextRow("Rate Numerator Expression", rxn.RateEquationNumerator ?? "",
                    v => rxn.RateEquationNumerator = v);
                AddDetailTextRow("Rate Denominator Expression", rxn.RateEquationDenominator ?? "",
                    v => rxn.RateEquationDenominator = v);
            }
            AddDetailTextRow("Reaction Rate Units", rxn.VelUnit ?? "",
                v => rxn.VelUnit = v);
            AddDetailTextRow("Concentration Units", rxn.ConcUnit ?? "",
                v => rxn.ConcUnit = v);
        }

        AddDetailLabel("Stoichiometry");
        var kinetic = rxn.ReactionType is ReactionType.Kinetic or ReactionType.Heterogeneous_Catalytic;
        foreach (var comp in rxn.Components.Values)
        {
            var c = comp;
            AddDetailReadRow($"  {c.CompName}", c.IsBaseReactant ? "base reactant" : "");
            AddDetailTextRow("    Stoich. Coefficient", c.StoichCoeff.ToString("G6"),
                v => { if (double.TryParse(v, out var d)) { c.StoichCoeff = d; UpdateEquation(rxn); } });
            if (kinetic)
            {
                AddDetailTextRow("    Direct Order", c.DirectOrder.ToString("G6"),
                    v => { if (double.TryParse(v, out var d)) c.DirectOrder = d; });
                AddDetailTextRow("    Reverse Order", c.ReverseOrder.ToString("G6"),
                    v => { if (double.TryParse(v, out var d)) c.ReverseOrder = d; });
            }
        }

        UpdateEquation(rxn);
        AddDetailReadRow("  Equation", rxn.Equation ?? "");
        AddDetailReadRow("  Reaction Heat (kJ/kmol)", rxn.ReactionHeat.ToString("G6"));
        AddDetailReadRow("  Mass Balance", Math.Abs(rxn.StoichBalance) < 0.01
            ? "OK" : rxn.StoichBalance.ToString("G6") + " kg/kmol");

        AddDetailButton("Add Component...", async () => await AddComponentToReaction(rxn));
        AddDetailButton("Remove Selected Component...", async () => await RemoveComponentFromReaction(rxn));
    }

    /// <summary>Marks a single component as the base reactant, which the solvers read.</summary>
    private static void SetBaseReactant(IReaction rxn, string compName)
    {
        foreach (var c in rxn.Components.Values)
            c.IsBaseReactant = c.CompName == compName;
    }

    /// <summary>
    /// Rebuilds the reaction equation, heat of reaction and stoichiometric mass balance from the
    /// current coefficients, the same way the WinForms reaction editors do.
    /// </summary>
    private void UpdateEquation(IReaction rxn)
    {
        double hr = 0, hp = 0, br = 0, bp = 0, brsc = 1.0;

        string Side(bool products)
        {
            var terms = new List<string>();
            foreach (var c in rxn.Components.Values)
            {
                if (products ? c.StoichCoeff <= 0 : c.StoichCoeff >= 0) continue;
                if (!_flowsheet.SelectedCompounds.TryGetValue(c.CompName, out var cp)) continue;

                var coeff = Math.Abs(c.StoichCoeff);
                terms.Add((Math.Abs(coeff - 1.0) < 1e-10 ? "" : coeff.ToString("G4")) + cp.Formula);

                if (products)
                {
                    hp += coeff * cp.IG_Enthalpy_of_Formation_25C * cp.Molar_Weight;
                    bp += coeff * cp.Molar_Weight;
                }
                else
                {
                    hr += coeff * cp.IG_Enthalpy_of_Formation_25C * cp.Molar_Weight;
                    br += coeff * cp.Molar_Weight;
                }
            }
            return string.Join(" + ", terms);
        }

        var equation = Side(false) + " --> " + Side(true);

        foreach (var c in rxn.Components.Values)
        {
            if (!c.IsBaseReactant) continue;
            brsc = Math.Abs(c.StoichCoeff);
            if (brsc == 0.0) brsc = 1.0;
            break;
        }

        rxn.Equation = equation;
        rxn.ReactionHeat = (hp - hr) / brsc;
        rxn.StoichBalance = bp - br;
    }

    private async System.Threading.Tasks.Task AddComponentToReaction(IReaction rxn)
    {
        var compounds = _flowsheet.SelectedCompounds.Keys.ToList();
        if (compounds.Count == 0) { await ShowMessageAsync("Info", "No compounds in flowsheet."); return; }
        var comp = await ShowChoiceDialogAsync("Add Component", "Select compound:", compounds.ToArray());
        if (comp == null) return;
        var coeffStr = await ShowInputDialogAsync("Stoich. Coefficient", $"Coefficient for {comp}\n(negative=reactant, positive=product):", "-1");
        if (string.IsNullOrWhiteSpace(coeffStr)) return;
        if (!double.TryParse(coeffStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var coeff))
        {
            await ShowMessageAsync("Invalid Input", $"'{coeffStr}' is not a valid stoichiometric coefficient.");
            return;
        }
        if (coeff == 0)
        {
            await ShowMessageAsync("Invalid Input", "Stoichiometric coefficient cannot be zero.");
            return;
        }
        if (rxn.Components.ContainsKey(comp))
            rxn.Components[comp].StoichCoeff = coeff;
        else if (_flowsheet.SelectedCompounds.ContainsKey(comp))
            rxn.Components[comp] = CreateStoichBase(comp, coeff);
        ShowReactionDetail();
    }

    private async System.Threading.Tasks.Task RemoveComponentFromReaction(IReaction rxn)
    {
        if (rxn.Components.Count == 0) return;
        var comps = rxn.Components.Keys.ToList();
        var comp = await ShowChoiceDialogAsync("Remove Component", "Select component to remove:", comps.ToArray());
        if (comp == null) return;
        rxn.Components.Remove(comp);
        ShowReactionDetail();
    }

    /// <summary>
    /// Builds a stoichiometry entry the way the engine's own reaction factories do:
    /// New ReactionStoichBase(name, coeff, False, 0, 0).
    /// </summary>
    private static IReactionStoichBase CreateStoichBase(string compName, double coeff) =>
        new global::DWSIM.Thermodynamics.BaseClasses.ReactionStoichBase(compName, coeff, false, 0, 0);

    // -------------------------------------------------------------------------
    // Reaction Sets helpers
    // -------------------------------------------------------------------------

    private void RefreshReactionSetList()
    {
        var sel = (LbReactionSets.SelectedItem as RsItem)?.ID;
        LbReactionSets.Items.Clear();
        foreach (var rs in _flowsheet.ReactionSets.Values)
            LbReactionSets.Items.Add(new RsItem(rs.Name, rs.ID));
        if (sel != null)
        {
            for (int i = 0; i < LbReactionSets.Items.Count; i++)
                if (((RsItem)LbReactionSets.Items[i]!).ID == sel)
                { LbReactionSets.SelectedIndex = i; break; }
        }
    }

    private void RefreshAllReactionsForSet()
    {
        LbAllReactionsForSet.Items.Clear();
        foreach (var r in _flowsheet.Reactions.Values)
            LbAllReactionsForSet.Items.Add(new RxnItem(r.Name, r.ID, r.ReactionType.ToString()));
    }

    private void RefreshSetReactions()
    {
        LbSetReactions.Items.Clear();
        if (LbReactionSets.SelectedItem is not RsItem rsItem) return;
        if (!_flowsheet.ReactionSets.TryGetValue(rsItem.ID, out var rs)) return;
        foreach (var pair in rs.Reactions)
        {
            if (_flowsheet.Reactions.TryGetValue(pair.Key, out var rxn))
                LbSetReactions.Items.Add(new RxnItem(
                    $"{rxn.Name} [rank={pair.Value.Rank}] {(pair.Value.IsActive ? "" : "(disabled)")}",
                    pair.Key, rxn.ReactionType.ToString()));
        }
    }

    private async System.Threading.Tasks.Task OnNewReactionSet()
    {
        var name = await ShowInputDialogAsync("New Reaction Set", "Name:", "New Reaction Set");
        if (string.IsNullOrWhiteSpace(name)) return;
        var rs = _flowsheet.CreateReactionSet(name, "");
        _flowsheet.AddReactionSet(rs);
        RefreshReactionSetList();
    }

    private void OnDeleteReactionSet()
    {
        if (LbReactionSets.SelectedItem is not RsItem item) return;
        _flowsheet.ReactionSets.Remove(item.ID);
        LbSetReactions.Items.Clear();
        RefreshReactionSetList();
    }

    private async void OnAddReactionToSet()
    {
        if (LbReactionSets.SelectedItem is not RsItem rsItem) return;
        if (LbAllReactionsForSet.SelectedItem is not RxnItem rxnItem) return;
        if (!_flowsheet.ReactionSets.TryGetValue(rsItem.ID, out var rs)) return;
        if (rs.Reactions.ContainsKey(rxnItem.ID)) return; // already present

        try
        {
            _flowsheet.AddReactionToSet(rxnItem.ID, rsItem.ID, true, rs.Reactions.Count);
            RefreshSetReactions();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddReactionToSet: {ex.Message}");
            await ShowMessageAsync("Add Reaction Failed", $"Could not add reaction to set: {ex.Message}");
        }
    }

    private void OnRemoveReactionFromSet()
    {
        if (LbReactionSets.SelectedItem is not RsItem rsItem) return;
        if (LbSetReactions.SelectedItem is not RxnItem rxnItem) return;
        if (!_flowsheet.ReactionSets.TryGetValue(rsItem.ID, out var rs)) return;
        rs.Reactions.Remove(rxnItem.ID);
        RefreshSetReactions();
    }

    // -------------------------------------------------------------------------
    // Detail panel builder helpers
    // -------------------------------------------------------------------------

    private void AddDetailLabel(string text)
    {
        ReactionDetailPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 2)
        });
    }

    private void AddDetailReadRow(string label, string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Margin = new Thickness(0, 2) };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var val = new TextBlock { Text = value, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);
        ReactionDetailPanel.Children.Add(grid);
    }

    private void AddDetailTextRow(string label, string current, Action<string> onChanged)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Margin = new Thickness(0, 2) };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var tb = new TextBox { Text = current };
        tb.LostFocus += (_, _) => onChanged(tb.Text ?? "");
        Grid.SetColumn(tb, 1);
        grid.Children.Add(tb);
        ReactionDetailPanel.Children.Add(grid);
    }

    private void AddDetailDropDown(string label, string[] options, string current, Action<string> onChanged)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Margin = new Thickness(0, 2) };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        var cb = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var opt in options) cb.Items.Add(opt);
        var idx = Array.IndexOf(options, current);
        cb.SelectedIndex = idx >= 0 ? idx : 0;
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is string s) onChanged(s); };
        Grid.SetColumn(cb, 1);
        grid.Children.Add(cb);
        ReactionDetailPanel.Children.Add(grid);
    }

    private void AddDetailButton(string label, Func<System.Threading.Tasks.Task> onClick)
    {
        var btn = new Button { Content = label, Margin = new Thickness(0, 4, 0, 0) };
        btn.Classes.Add("panel");
        btn.Click += async (_, _) => await onClick();
        ReactionDetailPanel.Children.Add(btn);
    }

    // -------------------------------------------------------------------------
    // Dialog helpers
    // -------------------------------------------------------------------------

    private async System.Threading.Tasks.Task<string?> ShowInputDialogAsync(
        string title, string prompt, string defaultValue = "")
    {
        string? result = null;
        var tb = new TextBox { Text = defaultValue };
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
            Children =
            {
                new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap },
                tb
            }
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
        await dlg.ShowDialog(DialogOwner());
        return result;
    }

    private async System.Threading.Tasks.Task<string?> ShowChoiceDialogAsync(
        string title, string prompt, string[] choices)
    {
        string? result = null;
        var lb = new ListBox { Height = 160 };
        foreach (var c in choices) lb.Items.Add(c);
        if (choices.Length > 0) lb.SelectedIndex = 0;
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel  = true };
        cancel.Classes.Add("dialog");
        var ok     = new Button { Content = "OK",     Width = 80, IsDefault = true };
        ok.Classes.Add("dialog");

        var btnPanel2 = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 0, 16, 12)
        };
        btnPanel2.Children.Add(cancel);
        btnPanel2.Children.Add(ok);

        var body2 = new DockPanel();
        DockPanel.SetDock(btnPanel2, global::Avalonia.Controls.Dock.Bottom);
        body2.Children.Add(btnPanel2);
        body2.Children.Add(new StackPanel
        {
            Margin  = new Thickness(16, 16, 16, 8),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = prompt },
                lb
            }
        });

        var dlg = new Window
        {
            Title  = title,
            Width  = 340,
            Height = 280,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon(),
            Content = body2
        };
        ok.Click     += (_, _) => { result = lb.SelectedItem as string; dlg.Close(); };
        cancel.Click += (_, _) => dlg.Close();
        lb.DoubleTapped += (_, _) => { result = lb.SelectedItem as string; dlg.Close(); };
        await dlg.ShowDialog(DialogOwner());
        return result;
    }

    private async System.Threading.Tasks.Task ShowMessageAsync(string title, string message)
    {
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
        ok.Classes.Add("dialog");

        var btnPanel3 = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 16, 12)
        };
        btnPanel3.Children.Add(ok);

        var body3 = new DockPanel();
        DockPanel.SetDock(btnPanel3, global::Avalonia.Controls.Dock.Bottom);
        body3.Children.Add(btnPanel3);
        body3.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16, 16, 16, 8)
        });

        var dlg = new Window
        {
            Title  = title,
            Width  = 360,
            Height = 140,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon(),
            Content = body3
        };
        ok.Click += (_, _) => dlg.Close();
        await dlg.ShowDialog(DialogOwner());
    }

    // -------------------------------------------------------------------------
    // List item wrappers
    // -------------------------------------------------------------------------

    private sealed class RxnItem
    {
        public string DisplayName { get; }
        public string ID          { get; }
        public RxnItem(string name, string id, string type) =>
            (DisplayName, ID) = ($"[{type[0]}] {name}", id);
        public override string ToString() => DisplayName;
    }

    private sealed class RsItem
    {
        public string DisplayName { get; }
        public string ID          { get; }
        public RsItem(string name, string id) => (DisplayName, ID) = (name, id);
        public override string ToString() => DisplayName;
    }
}
