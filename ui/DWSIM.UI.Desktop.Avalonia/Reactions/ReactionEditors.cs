using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using RxnBaseClasses = DWSIM.Thermodynamics.BaseClasses;

namespace DWSIM.UI.Desktop.Avalonia.Reactions
{
    /// <summary>
    /// Base of the four per-type reaction editor dialogs, reproducing the classic WinForms editors:
    /// a Components &amp; Stoichiometry grid, an auto-built equation, the reaction heat and mass balance
    /// (balance gates saving), a Balance button that back-solves the base reactant coefficient, a
    /// per-type parameter group, and Name/Description. Values are read into the reaction only on OK,
    /// so Cancel leaves it untouched.
    /// </summary>
    internal abstract class ReactionEditorWindow : Window
    {
        protected readonly IFlowsheet Fs;
        private readonly IReaction _existing;   // null for a new reaction
        private readonly string _suggestedName;

        protected StoichiometryGrid Stoich = null!;

        protected IReaction ExistingReaction => _existing;
        private TextBox _tbEquation = null!, _tbHeat = null!, _tbBalance = null!;
        private TextBox _tbName = null!, _tbDesc = null!;
        private TextBlock _baseCompText = null!;

        public bool Saved { get; private set; }

        protected ReactionEditorWindow(IFlowsheet fs, IReaction existing, string suggestedName)
        {
            Fs = fs;
            _existing = existing;
            _suggestedName = suggestedName;
        }

        protected abstract ReactionType RxType { get; }
        protected virtual bool ShowOrders => false;
        protected virtual bool Reversible => RxType != ReactionType.Conversion;

        /// <summary>Fills the per-type parameter group. Keep control references to read on save.</summary>
        protected abstract void BuildParameters(StackPanel panel);

        /// <summary>Writes the per-type parameters (and reaction basis/phase) into the reaction.</summary>
        protected abstract void ApplyParameters(IReaction rxn);

        /// <summary>Extra per-type validation before saving. Return false + a message to block.</summary>
        protected virtual bool ValidateExtra(out string error) { error = ""; return true; }

        /// <summary>Called after every recompute so a subtype can refresh derived read-outs.</summary>
        protected virtual void OnRecomputed() { }

        // ---- layout ---------------------------------------------------------

        protected void BuildUI(string title)
        {
            Title = title;
            Width = 940;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = IconHelper.GetWindowIcon();

            Stoich = new StoichiometryGrid(ShowOrders) { MinHeight = 220 };
            Stoich.Populate(Fs, _existing);
            Stoich.Edited += Recompute;

            _tbEquation = ReadBox();
            _tbHeat = ReadBox();
            _tbBalance = ReadBox();
            var btnBalance = PanelButton("Balance", Balance);

            var stoichBody = new StackPanel { Spacing = 6 };
            stoichBody.Children.Add(Stoich);
            stoichBody.Children.Add(LabelRow("Equation", _tbEquation));
            stoichBody.Children.Add(LabelRow("Reaction Heat (kJ/kmol, base reactant, 25 °C)", _tbHeat));
            var balRow = LabelRow("Mass Balance", _tbBalance);
            stoichBody.Children.Add(balRow);
            stoichBody.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { btnBalance }
            });

            var paramsPanel = new StackPanel { Spacing = 6 };
            _baseCompText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            BuildParameters(paramsPanel);

            _tbName = new TextBox { Text = _existing?.Name ?? _suggestedName };
            _tbDesc = new TextBox { Text = _existing?.Description ?? "" };
            var idBody = new StackPanel { Spacing = 6 };
            idBody.Children.Add(LabelRow("Name", _tbName));
            idBody.Children.Add(LabelRow("Description", _tbDesc));

            var left = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 8, 0) };
            left.Children.Add(GroupBox("Components and Stoichiometry", stoichBody));

            var right = new StackPanel { Spacing = 10 };
            right.Children.Add(GroupBox("Parameters", paramsPanel));
            right.Children.Add(GroupBox("Identification", idBody));

            var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*"), Margin = new Thickness(12) };
            global::Avalonia.Controls.Grid.SetColumn(left, 0);
            global::Avalonia.Controls.Grid.SetColumn(right, 1);
            columns.Children.Add(left);
            columns.Children.Add(right);

            var ok = new Button { Content = "OK", Width = 90, IsDefault = true };
            ok.Classes.Add("dialog");
            ok.Click += (_, _) => OnOk();
            var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true };
            cancel.Classes.Add("dialog");
            cancel.Click += (_, _) => Close();
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new Thickness(0, 0, 16, 12),
                Children = { cancel, ok }
            };

            var body = new DockPanel();
            DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
            body.Children.Add(buttons);
            body.Children.Add(new ScrollViewer { Content = columns });
            Content = body;

            Recompute();
        }

        protected Control BaseComponentRow() => LabelRow("Base Component", _baseCompText);

        // ---- recompute / balance -------------------------------------------

        private void Recompute()
        {
            double hr = 0, hp = 0, gr = 0, gp = 0, br = 0, bp = 0, brsc = 1.0;
            var reactants = new List<string>();
            var products = new List<string>();
            var inerts = new List<string>();

            foreach (var row in Stoich.IncludedRows)
            {
                if (!Fs.SelectedCompounds.TryGetValue(row.Compound, out var cp)) continue;
                var coeff = row.CoefficientValue;
                var acoeff = Math.Abs(coeff);
                var term = (Math.Abs(acoeff - 1.0) < 1e-10 ? "" : acoeff.ToString("G4", CultureInfo.CurrentCulture)) + cp.Formula;

                if (coeff < 0)
                {
                    reactants.Add(term);
                    hr += acoeff * cp.IG_Enthalpy_of_Formation_25C * cp.Molar_Weight;
                    gr += acoeff * cp.IG_Gibbs_Energy_of_Formation_25C * cp.Molar_Weight;
                    br += acoeff * cp.Molar_Weight;
                }
                else if (coeff > 0)
                {
                    products.Add(term);
                    hp += acoeff * cp.IG_Enthalpy_of_Formation_25C * cp.Molar_Weight;
                    gp += acoeff * cp.IG_Gibbs_Energy_of_Formation_25C * cp.Molar_Weight;
                    bp += acoeff * cp.Molar_Weight;
                }
                else
                {
                    inerts.Add(cp.Formula);
                }
            }

            var baseRow = Stoich.BaseRow;
            if (baseRow != null)
            {
                brsc = Math.Abs(baseRow.CoefficientValue);
                if (brsc == 0.0) brsc = 1.0;
            }

            var arrow = Reversible ? " <--> " : " --> ";
            var eq = string.Join(" + ", reactants) + arrow + string.Join(" + ", products);
            if (RxType == ReactionType.Heterogeneous_Catalytic && inerts.Count > 0)
                eq += "  (N: " + string.Join(", ", inerts) + ")";

            _tbEquation.Text = eq;
            _tbHeat.Text = ((hp - hr) / brsc).ToString("G6", CultureInfo.CurrentCulture);
            var balance = bp - br;
            _tbBalance.Text = Math.Abs(balance) < 1e-3 ? "OK" : balance.ToString("G6", CultureInfo.CurrentCulture) + " kg/kmol";

            _baseCompText.Text = baseRow?.Compound ?? "(none)";
            OnRecomputed();
        }

        private void Balance()
        {
            var baseRow = Stoich.BaseRow;
            if (baseRow == null || !Fs.SelectedCompounds.TryGetValue(baseRow.Compound, out var cpBase) || cpBase.Molar_Weight <= 0)
                return;

            double others = 0;
            foreach (var row in Stoich.IncludedRows)
            {
                if (ReferenceEquals(row, baseRow)) continue;
                if (Fs.SelectedCompounds.TryGetValue(row.Compound, out var cp))
                    others += row.CoefficientValue * cp.Molar_Weight;
            }
            var coeff = -others / cpBase.Molar_Weight;
            baseRow.Coefficient = coeff.ToString("G6", CultureInfo.CurrentCulture);
            Recompute();
        }

        // ---- save -----------------------------------------------------------

        private async void OnOk()
        {
            var baseRow = Stoich.BaseRow;
            if (baseRow == null)
            {
                await Msg("Cannot Save", "Mark one included reactant as the Base component.");
                return;
            }
            if (baseRow.CoefficientValue >= 0)
            {
                await Msg("Cannot Save", "The base component must be a reactant (a negative stoichiometric coefficient).");
                return;
            }
            if (_tbBalance.Text != "OK")
            {
                await Msg("Cannot Save", "The stoichiometry is not mass-balanced. Use Balance, or adjust the coefficients, so the mass balance reads OK.");
                return;
            }
            if (!Stoich.IncludedRows.Any(r => r.CoefficientValue > 0))
            {
                await Msg("Cannot Save", "Add at least one product (a positive stoichiometric coefficient).");
                return;
            }
            if (string.IsNullOrWhiteSpace(_tbName.Text))
            {
                await Msg("Cannot Save", "Enter a name for the reaction.");
                return;
            }
            if (!ValidateExtra(out var error))
            {
                await Msg("Cannot Save", error);
                return;
            }

            Fs.RegisterSnapshot(SnapshotType.ReactionSubsystem);

            var rxn = _existing ?? new RxnBaseClasses.Reaction();
            if (_existing == null)
            {
                rxn.ID = Guid.NewGuid().ToString();
                rxn.ReactionType = RxType;
            }
            rxn.Name = _tbName.Text!.Replace(":", "_");
            rxn.Description = _tbDesc.Text ?? "";
            rxn.BaseReactant = baseRow.Compound;

            rxn.Components.Clear();
            foreach (var row in Stoich.IncludedRows)
                rxn.Components[row.Compound] = new RxnBaseClasses.ReactionStoichBase(
                    row.Compound, row.CoefficientValue, row.IsBase, row.DirectOrderValue, row.ReverseOrderValue);

            ApplyParameters(rxn);
            WriteEquationHeatBalance(rxn);

            if (_existing == null)
            {
                Fs.AddReaction(rxn);
                if (Fs.ReactionSets.ContainsKey("DefaultSet"))
                    Fs.AddReactionToSet(rxn.ID, "DefaultSet", true, 0);
            }

            Saved = true;
            Close();
        }

        private void WriteEquationHeatBalance(IReaction rxn)
        {
            double hr = 0, hp = 0, gr = 0, gp = 0, br = 0, bp = 0, brsc = 1.0;
            foreach (var c in rxn.Components.Values)
            {
                if (!Fs.SelectedCompounds.TryGetValue(c.CompName, out var cp)) continue;
                var a = Math.Abs(c.StoichCoeff);
                if (c.StoichCoeff < 0)
                { hr += a * cp.IG_Enthalpy_of_Formation_25C * cp.Molar_Weight; gr += a * cp.IG_Gibbs_Energy_of_Formation_25C * cp.Molar_Weight; br += a * cp.Molar_Weight; }
                else if (c.StoichCoeff > 0)
                { hp += a * cp.IG_Enthalpy_of_Formation_25C * cp.Molar_Weight; gp += a * cp.IG_Gibbs_Energy_of_Formation_25C * cp.Molar_Weight; bp += a * cp.Molar_Weight; }
                if (c.IsBaseReactant) { brsc = a == 0.0 ? 1.0 : a; }
            }
            rxn.ReactionType = RxType;
            rxn.ReactionHeat = (hp - hr) / brsc;
            rxn.ReactionGibbsEnergy = (gp - gr) / brsc;
            rxn.StoichBalance = bp - br;
            rxn.Equation = _tbEquation.Text;
        }

        // ---- shared control helpers ----------------------------------------

        protected static TextBox ReadBox() => new() { IsReadOnly = true, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };

        protected static Control LabelRow(string label, Control control)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 1), ColumnSpacing = 8 };
            var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, MinWidth = 160, TextWrapping = TextWrapping.Wrap };
            global::Avalonia.Controls.Grid.SetColumn(control, 1);
            g.Children.Add(lbl);
            g.Children.Add(control);
            return g;
        }

        protected static Control GroupBox(string header, Control content)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = header, FontWeight = FontWeight.SemiBold, Margin = new Thickness(2, 2, 0, 6) });
            stack.Children.Add(content);
            var border = new Border { Child = stack, Padding = new Thickness(8), BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 0, 4) };
            return border;
        }

        protected static ComboBox Combo(IEnumerable<string> items, int selected)
        {
            var cb = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var it in items) cb.Items.Add(it);
            if (cb.Items.Count > 0) cb.SelectedIndex = Math.Max(0, Math.Min(selected, cb.Items.Count - 1));
            return cb;
        }

        protected static TextBox Text(string value) => new() { Text = value };

        protected static Button PanelButton(string label, Action onClick)
        {
            var b = new Button { Content = label, Width = 110 };
            b.Classes.Add("panel");
            b.Click += (_, _) => onClick();
            return b;
        }

        protected async System.Threading.Tasks.Task Msg(string title, string message)
        {
            var ok = new Button { Content = "OK", Width = 80, IsDefault = true };
            ok.Classes.Add("dialog");
            var dlg = new Window
            {
                Title = title,
                Width = 380,
                Height = 160,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Icon = IconHelper.GetWindowIcon()
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

        // ---- shared option lists (English, matching the WinForms editors) ---

        protected static readonly string[] PhaseItems =
            { "Liquid", "Vapor", "Mixture", "Solid", "Liquid + Solid", "Vapor + Solid" };   // index == (int)ReactionPhase

        protected static readonly string[] BasisItems =
            { "Activity", "Fugacity", "Molar Concentration", "Mass Concentration", "Molar Fraction", "Mass Fraction", "Partial Pressure" }; // index == (int)ReactionBasis

        protected string[] EnergyUnits() => Fs.FlowsheetOptions.SelectedUnitSystem.GetUnitSet(UnitOfMeasure.molar_enthalpy).ToArray();
        protected string[] BasisUnits(ReactionBasis basis)
        {
            var su = Fs.FlowsheetOptions.SelectedUnitSystem;
            return basis switch
            {
                ReactionBasis.MolarConc => su.GetUnitSet(UnitOfMeasure.molar_conc).ToArray(),
                ReactionBasis.MassConc => su.GetUnitSet(UnitOfMeasure.mass_conc).ToArray(),
                ReactionBasis.PartialPress => su.GetUnitSet(UnitOfMeasure.pressure).ToArray(),
                _ => new[] { "" }
            };
        }

        protected static readonly string[] ConcUnitsMolar = { "kmol/m3", "mol/m3", "mol/L", "mol/cm3", "mol/mL", "lbmol/ft3" };
        protected static readonly string[] ConcUnitsMass = { "kg/m3", "g/L", "g/cm3", "g/mL", "lbm/ft3" };
        protected static readonly string[] ConcUnitsPressure = { "Pa", "atm", "kgf/cm2", "kgf/cm2g", "lbf/ft2", "kPa", "kPag", "bar", "barg", "ftH2O", "inH2O", "inHg", "mbar", "mH2O", "mmH2O", "mmHg", "MPa", "psi", "psig" };
        protected static readonly string[] ConcUnitsAll = { "kmol/m3", "mol/m3", "mol/L", "mol/cm3", "mol/mL", "lbmol/ft3", "kg/m3", "g/L", "g/cm3", "g/mL", "lbm/ft3" };
        protected static readonly string[] VelUnitsHomogeneous = { "kmol/[m3.s]", "kmol/[m3.min.]", "kmol/[m3.h]", "mol/[m3.s]", "mol/[m3.min.]", "mol/[m3.h]", "mol/[L.s]", "mol/[L.min.]", "mol/[L.h]", "mol/[cm3.s]", "mol/[cm3.min.]", "mol/[cm3.h]", "lbmol/[ft3.h]" };
        protected static readonly string[] VelUnitsCatalytic = { "kmol/[kg.s]", "kmol/[kg.min.]", "kmol/[kg.h]", "mol/[kg.s]", "mol/[kg.min.]", "mol/[kg.h]", "lbmol/[lbm.h]" };

        protected static string[] ConcUnitsFor(ReactionBasis basis) => basis switch
        {
            ReactionBasis.MolarConc => ConcUnitsMolar,
            ReactionBasis.MassConc => ConcUnitsMass,
            ReactionBasis.PartialPress => ConcUnitsPressure,
            _ => new[] { "" }
        };

        protected static int IndexOfOr0(string[] items, string value)
        {
            var i = Array.IndexOf(items, value);
            return i >= 0 ? i : 0;
        }

        /// <summary>Culture-tolerant parse, falling back to a default (matches the number-entry fix).</summary>
        protected static double ParseD(string s, double fallback)
            => !string.IsNullOrWhiteSpace(s) && s.IsValidDoubleFlexible() ? s.ToDoubleFromCurrent() : fallback;

        /// <summary>The flowsheet's Python script titles, with a leading blank (as the WinForms combo shows).</summary>
        protected string[] ScriptTitles()
        {
            var list = new List<string> { "" };
            try { list.AddRange(Fs.Scripts.Values.Select(s => s.Title)); } catch { }
            return list.ToArray();
        }
    }

    // =====================================================================
    // Conversion
    // =====================================================================
    internal sealed class ConversionReactionEditor : ReactionEditorWindow
    {
        private ComboBox _phase = null!;
        private TextBox _expr = null!;

        public ConversionReactionEditor(IFlowsheet fs, IReaction existing, string name) : base(fs, existing, name)
            => BuildUI(existing == null ? "Add Conversion Reaction" : "Edit Conversion Reaction");

        protected override ReactionType RxType => ReactionType.Conversion;

        protected override void BuildParameters(StackPanel panel)
        {
            var ex = ExistingReaction;
            _phase = Combo(PhaseItems, (int)(ex?.ReactionPhase ?? ReactionPhase.Mixture));
            _expr = Text(ex?.Expression ?? "0.5");
            panel.Children.Add(LabelRow("Phase", _phase));
            panel.Children.Add(BaseComponentRow());
            panel.Children.Add(LabelRow("Conversion [%, f(T)] = (T in K)", _expr));
            panel.Children.Add(new TextBlock { Text = "Use '.' as the decimal separator in the conversion expression.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        }

        protected override void ApplyParameters(IReaction rxn)
        {
            rxn.ReactionPhase = (ReactionPhase)_phase.SelectedIndex;
            rxn.Expression = _expr.Text ?? "";
        }
    }

    // =====================================================================
    // Equilibrium
    // =====================================================================
    internal sealed class EquilibriumReactionEditor : ReactionEditorWindow
    {
        private ComboBox _phase = null!, _basis = null!, _units = null!;
        private TextBox _tmin = null!, _tmax = null!, _approach = null!;
        private RadioButton _rbGibbs = null!, _rbExpr = null!, _rbConst = null!;
        private TextBox _tbDelG = null!, _tbExpr = null!, _tbConst = null!;

        public EquilibriumReactionEditor(IFlowsheet fs, IReaction existing, string name) : base(fs, existing, name)
            => BuildUI(existing == null ? "Add Equilibrium Reaction" : "Edit Equilibrium Reaction");

        protected override ReactionType RxType => ReactionType.Equilibrium;

        protected override void BuildParameters(StackPanel panel)
        {
            var ex = ExistingReaction;
            _phase = Combo(new[] { "Vapor", "Liquid" }, (ex != null && ex.ReactionPhase == ReactionPhase.Liquid) ? 1 : 0);
            _basis = Combo(BasisItems, (int)(ex?.ReactionBasis ?? ReactionBasis.Fugacity));
            _units = Combo(BasisUnits(ex?.ReactionBasis ?? ReactionBasis.Fugacity), IndexOfOr0(BasisUnits(ex?.ReactionBasis ?? ReactionBasis.Fugacity), ex?.EquilibriumReactionBasisUnits ?? ""));
            _basis.SelectionChanged += (_, _) =>
            {
                var b = (ReactionBasis)_basis.SelectedIndex;
                var units = BasisUnits(b);
                _units.Items.Clear();
                foreach (var u in units) _units.Items.Add(u);
                _units.SelectedIndex = units.Length > 0 ? 0 : -1;
                _units.IsEnabled = b is ReactionBasis.MolarConc or ReactionBasis.MassConc or ReactionBasis.PartialPress;
            };
            _units.IsEnabled = (ex?.ReactionBasis ?? ReactionBasis.Fugacity) is ReactionBasis.MolarConc or ReactionBasis.MassConc or ReactionBasis.PartialPress;

            _tmin = Text((ex?.Tmin ?? 298.15).ToString("G6", CultureInfo.CurrentCulture));
            _tmax = Text((ex?.Tmax ?? 2000.0).ToString("G6", CultureInfo.CurrentCulture));
            _approach = Text((ex?.Approach ?? 1.0).ToString("G6", CultureInfo.CurrentCulture));

            panel.Children.Add(LabelRow("Phase", _phase));
            panel.Children.Add(LabelRow("Basis", _basis));
            panel.Children.Add(LabelRow("Units", _units));
            panel.Children.Add(LabelRow("Tmin (K)", _tmin));
            panel.Children.Add(LabelRow("Tmax (K)", _tmax));
            panel.Children.Add(LabelRow("Temperature Approach (K)", _approach));
            panel.Children.Add(BaseComponentRow());

            _rbGibbs = new RadioButton { Content = "Calculate from Gibbs free energy", GroupName = "keq" };
            _rbExpr = new RadioButton { Content = "Function of temperature: ln Keq = f(T)", GroupName = "keq" };
            _rbConst = new RadioButton { Content = "Constant", GroupName = "keq" };
            _tbDelG = ReadBox();
            _tbExpr = Text(ex != null && ex.KExprType == KOpt.Expression ? (ex.Expression ?? "") : "");
            _tbConst = Text((ex?.ConstantKeqValue ?? 1.0).ToString("G6", CultureInfo.CurrentCulture));

            var keq = new StackPanel { Spacing = 4 };
            keq.Children.Add(_rbGibbs);
            keq.Children.Add(LabelRow("  ΔG_R (kJ/kmol, base reactant, 25 °C)", _tbDelG));
            keq.Children.Add(_rbExpr);
            keq.Children.Add(LabelRow("  ln Keq = f(T)  (T in K)", _tbExpr));
            keq.Children.Add(_rbConst);
            keq.Children.Add(LabelRow("  Constant Keq", _tbConst));
            panel.Children.Add(GroupBox("Equilibrium Constant (Keq)", keq));

            var kopt = ex?.KExprType ?? KOpt.Gibbs;
            _rbGibbs.IsChecked = kopt == KOpt.Gibbs;
            _rbExpr.IsChecked = kopt == KOpt.Expression;
            _rbConst.IsChecked = kopt == KOpt.Constant;
        }

        protected override void OnRecomputed()
        {
            if (_tbDelG == null) return;
            // ΔG_R read-out mirrors the reaction Gibbs energy the WinForms Gibbs option shows
            double gr = 0, gp = 0, brsc = 1.0;
            foreach (var row in Stoich.IncludedRows)
            {
                if (!Fs.SelectedCompounds.TryGetValue(row.Compound, out var cp)) continue;
                var a = Math.Abs(row.CoefficientValue);
                if (row.CoefficientValue < 0) gr += a * cp.IG_Gibbs_Energy_of_Formation_25C * cp.Molar_Weight;
                else if (row.CoefficientValue > 0) gp += a * cp.IG_Gibbs_Energy_of_Formation_25C * cp.Molar_Weight;
                if (row.IsBase) brsc = a == 0.0 ? 1.0 : a;
            }
            _tbDelG.Text = ((gp - gr) / brsc).ToString("G6", CultureInfo.CurrentCulture);
        }

        protected override void ApplyParameters(IReaction rxn)
        {
            rxn.ReactionPhase = _phase.SelectedIndex == 1 ? ReactionPhase.Liquid : ReactionPhase.Vapor;
            rxn.ReactionBasis = (ReactionBasis)_basis.SelectedIndex;
            rxn.EquilibriumReactionBasisUnits = _units.SelectedItem as string ?? "";
            rxn.Tmin = ParseD(_tmin.Text, 298.15);
            rxn.Tmax = ParseD(_tmax.Text, 2000.0);
            rxn.Approach = ParseD(_approach.Text, 1.0);
            rxn.KExprType = _rbExpr.IsChecked == true ? KOpt.Expression : _rbConst.IsChecked == true ? KOpt.Constant : KOpt.Gibbs;
            if (rxn.KExprType == KOpt.Expression) rxn.Expression = _tbExpr.Text ?? "";
            if (rxn.KExprType == KOpt.Constant) rxn.ConstantKeqValue = ParseD(_tbConst.Text, 1.0);
        }
    }

    // =====================================================================
    // Kinetic
    // =====================================================================
    internal sealed class KineticReactionEditor : ReactionEditorWindow
    {
        private ComboBox _phase = null!, _basis = null!, _conc = null!, _vel = null!;
        private TextBox _tmin = null!, _tmax = null!;
        private RadioButton _rbBasic = null!, _rbAdvanced = null!;
        private ComboBox _script = null!;
        private RadioButton _fwdArr = null!, _fwdUD = null!, _revArr = null!, _revUD = null!;
        private TextBox _fwdA = null!, _fwdE = null!, _fwdExpr = null!, _revA = null!, _revE = null!, _revExpr = null!;
        private ComboBox _fwdEUnits = null!, _revEUnits = null!;

        public KineticReactionEditor(IFlowsheet fs, IReaction existing, string name) : base(fs, existing, name)
            => BuildUI(existing == null ? "Add Kinetic Reaction" : "Edit Kinetic Reaction");

        protected override ReactionType RxType => ReactionType.Kinetic;
        protected override bool ShowOrders => true;

        protected override void BuildParameters(StackPanel panel)
        {
            var ex = ExistingReaction;
            _rbBasic = new RadioButton { Content = "Basic (expression)", GroupName = "kmode", IsChecked = ex == null || ex.ReactionKinetics == ReactionKinetics.Expression };
            _rbAdvanced = new RadioButton { Content = "Advanced (Python script)", GroupName = "kmode", IsChecked = ex != null && ex.ReactionKinetics == ReactionKinetics.PythonScript };
            _script = Combo(ScriptTitles(), IndexOfOr0(ScriptTitles(), ex?.ScriptTitle ?? ""));
            panel.Children.Add(_rbBasic);
            panel.Children.Add(_rbAdvanced);
            panel.Children.Add(LabelRow("Python Script", _script));

            _phase = Combo(PhaseItems, (int)(ex?.ReactionPhase ?? ReactionPhase.Mixture));
            _basis = Combo(BasisItems, (int)(ex?.ReactionBasis ?? ReactionBasis.MolarConc));
            _conc = Combo(ConcUnitsFor(ex?.ReactionBasis ?? ReactionBasis.MolarConc), IndexOfOr0(ConcUnitsFor(ex?.ReactionBasis ?? ReactionBasis.MolarConc), ex?.ConcUnit ?? ""));
            _vel = Combo(VelUnitsHomogeneous, IndexOfOr0(VelUnitsHomogeneous, ex?.VelUnit ?? ""));
            _basis.SelectionChanged += (_, _) => RepopulateConc(_conc, (ReactionBasis)_basis.SelectedIndex);
            _tmin = Text((ex?.Tmin ?? 0.0).ToString("G6", CultureInfo.CurrentCulture));
            _tmax = Text((ex?.Tmax ?? 2000.0).ToString("G6", CultureInfo.CurrentCulture));

            panel.Children.Add(LabelRow("Phase", _phase));
            panel.Children.Add(LabelRow("Basis", _basis));
            panel.Children.Add(LabelRow("Amount (Basis) Unit", _conc));
            panel.Children.Add(LabelRow("Rate Unit", _vel));
            panel.Children.Add(BaseComponentRow());
            panel.Children.Add(LabelRow("Tmin (K)", _tmin));
            panel.Children.Add(LabelRow("Tmax (K)", _tmax));

            _fwdArr = new RadioButton { Content = "Arrhenius", GroupName = "fwd" };
            _fwdUD = new RadioButton { Content = "User-defined: f(T[K])", GroupName = "fwd" };
            _fwdA = Text((ex?.A_Forward ?? 0.0).ToString("G6", CultureInfo.CurrentCulture));
            _fwdE = Text((ex?.E_Forward ?? 0.0).ToString("G6", CultureInfo.CurrentCulture));
            _fwdEUnits = Combo(EnergyUnits(), IndexOfOr0(EnergyUnits(), ex?.E_Forward_Unit ?? "J/mol"));
            _fwdExpr = Text(ex?.ReactionKinFwdExpression ?? "");
            var fwd = new StackPanel { Spacing = 4 };
            fwd.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _fwdArr, _fwdUD } });
            fwd.Children.Add(LabelRow("A", _fwdA));
            fwd.Children.Add(LabelRow("E", _fwdE));
            fwd.Children.Add(LabelRow("E Unit", _fwdEUnits));
            fwd.Children.Add(LabelRow("f(T) expression", _fwdExpr));
            panel.Children.Add(GroupBox("Forward Reaction", fwd));

            _revArr = new RadioButton { Content = "Arrhenius", GroupName = "rev" };
            _revUD = new RadioButton { Content = "User-defined: f(T[K])", GroupName = "rev" };
            _revA = Text((ex?.A_Reverse ?? 0.0).ToString("G6", CultureInfo.CurrentCulture));
            _revE = Text((ex?.E_Reverse ?? 0.0).ToString("G6", CultureInfo.CurrentCulture));
            _revEUnits = Combo(EnergyUnits(), IndexOfOr0(EnergyUnits(), ex?.E_Reverse_Unit ?? "J/mol"));
            _revExpr = Text(ex?.ReactionKinRevExpression ?? "");
            var rev = new StackPanel { Spacing = 4 };
            rev.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _revArr, _revUD } });
            rev.Children.Add(LabelRow("A'", _revA));
            rev.Children.Add(LabelRow("E'", _revE));
            rev.Children.Add(LabelRow("E' Unit", _revEUnits));
            rev.Children.Add(LabelRow("f(T) expression", _revExpr));
            panel.Children.Add(GroupBox("Reverse Reaction", rev));

            _fwdArr.IsChecked = ex == null || ex.ReactionKinFwdType == ReactionKineticType.Arrhenius;
            _fwdUD.IsChecked = ex != null && ex.ReactionKinFwdType == ReactionKineticType.UserDefined;
            _revArr.IsChecked = ex == null || ex.ReactionKinRevType == ReactionKineticType.Arrhenius;
            _revUD.IsChecked = ex != null && ex.ReactionKinRevType == ReactionKineticType.UserDefined;
        }

        private void RepopulateConc(ComboBox cb, ReactionBasis basis)
        {
            var units = ConcUnitsFor(basis);
            cb.Items.Clear();
            foreach (var u in units) cb.Items.Add(u);
            cb.SelectedIndex = units.Length > 0 ? 0 : -1;
        }

        protected override void ApplyParameters(IReaction rxn)
        {
            rxn.ReactionKinetics = _rbAdvanced.IsChecked == true ? ReactionKinetics.PythonScript : ReactionKinetics.Expression;
            rxn.ScriptTitle = _script.SelectedItem as string ?? "";
            rxn.ReactionPhase = (ReactionPhase)_phase.SelectedIndex;
            rxn.ReactionBasis = (ReactionBasis)_basis.SelectedIndex;
            rxn.ConcUnit = _conc.SelectedItem as string ?? "";
            rxn.VelUnit = _vel.SelectedItem as string ?? "";
            rxn.Tmin = ParseD(_tmin.Text, 0.0);
            rxn.Tmax = ParseD(_tmax.Text, 2000.0);
            rxn.ReactionKinFwdType = _fwdUD.IsChecked == true ? ReactionKineticType.UserDefined : ReactionKineticType.Arrhenius;
            rxn.A_Forward = ParseD(_fwdA.Text, 0.0);
            rxn.E_Forward = ParseD(_fwdE.Text, 0.0);
            rxn.E_Forward_Unit = _fwdEUnits.SelectedItem as string ?? "J/mol";
            rxn.ReactionKinFwdExpression = _fwdExpr.Text ?? "";
            rxn.ReactionKinRevType = _revUD.IsChecked == true ? ReactionKineticType.UserDefined : ReactionKineticType.Arrhenius;
            rxn.A_Reverse = ParseD(_revA.Text, 0.0);
            rxn.E_Reverse = ParseD(_revE.Text, 0.0);
            rxn.E_Reverse_Unit = _revEUnits.SelectedItem as string ?? "J/mol";
            rxn.ReactionKinRevExpression = _revExpr.Text ?? "";
        }
    }

    // =====================================================================
    // Heterogeneous Catalytic
    // =====================================================================
    internal sealed class HeterogeneousReactionEditor : ReactionEditorWindow
    {
        private ComboBox _phase = null!, _basis = null!, _conc = null!, _vel = null!;
        private TextBox _tmin = null!, _tmax = null!, _num = null!, _den = null!;
        private RadioButton _rbBasic = null!, _rbAdvanced = null!;
        private ComboBox _script = null!;

        public HeterogeneousReactionEditor(IFlowsheet fs, IReaction existing, string name) : base(fs, existing, name)
            => BuildUI(existing == null ? "Add Heterogeneous Catalytic Reaction" : "Edit Heterogeneous Catalytic Reaction");

        protected override ReactionType RxType => ReactionType.Heterogeneous_Catalytic;

        protected override void BuildParameters(StackPanel panel)
        {
            var ex = ExistingReaction;
            _rbBasic = new RadioButton { Content = "Basic (expression)", GroupName = "hmode", IsChecked = ex == null || ex.ReactionKinetics == ReactionKinetics.Expression };
            _rbAdvanced = new RadioButton { Content = "Advanced (Python script)", GroupName = "hmode", IsChecked = ex != null && ex.ReactionKinetics == ReactionKinetics.PythonScript };
            _script = Combo(ScriptTitles(), IndexOfOr0(ScriptTitles(), ex?.ScriptTitle ?? ""));
            panel.Children.Add(_rbBasic);
            panel.Children.Add(_rbAdvanced);
            panel.Children.Add(LabelRow("Python Script", _script));

            _phase = Combo(PhaseItems, (int)(ex?.ReactionPhase ?? ReactionPhase.Mixture));
            _basis = Combo(BasisItems, (int)(ex?.ReactionBasis ?? ReactionBasis.MolarConc));
            _conc = Combo(ConcUnitsFor(ex?.ReactionBasis ?? ReactionBasis.MolarConc), IndexOfOr0(ConcUnitsFor(ex?.ReactionBasis ?? ReactionBasis.MolarConc), ex?.ConcUnit ?? ""));
            _vel = Combo(VelUnitsCatalytic, IndexOfOr0(VelUnitsCatalytic, ex?.VelUnit ?? ""));
            _basis.SelectionChanged += (_, _) =>
            {
                var units = ConcUnitsFor((ReactionBasis)_basis.SelectedIndex);
                _conc.Items.Clear();
                foreach (var u in units) _conc.Items.Add(u);
                _conc.SelectedIndex = units.Length > 0 ? 0 : -1;
            };
            _tmin = Text((ex?.Tmin ?? 0.0).ToString("G6", CultureInfo.CurrentCulture));
            _tmax = Text((ex?.Tmax ?? 2000.0).ToString("G6", CultureInfo.CurrentCulture));
            _num = Text(ex?.RateEquationNumerator ?? "");
            _den = Text(ex?.RateEquationDenominator ?? "1");

            panel.Children.Add(LabelRow("Phase", _phase));
            panel.Children.Add(LabelRow("Basis", _basis));
            panel.Children.Add(LabelRow("Amount (Basis) Unit", _conc));
            panel.Children.Add(BaseComponentRow());
            panel.Children.Add(LabelRow("Tmin (K)", _tmin));
            panel.Children.Add(LabelRow("Tmax (K)", _tmax));

            var rate = new StackPanel { Spacing = 4 };
            rate.Children.Add(LabelRow("Numerator", _num));
            rate.Children.Add(LabelRow("Denominator", _den));
            rate.Children.Add(LabelRow("Rate Unit", _vel));
            rate.Children.Add(new TextBlock { Text = "Variables: T, R1..Rn (reactants), P1..Pn (products), r. Use '.' as the decimal separator.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(GroupBox("Rate Expression (Langmuir-Hinshelwood)", rate));
        }

        protected override void ApplyParameters(IReaction rxn)
        {
            rxn.ReactionKinetics = _rbAdvanced.IsChecked == true ? ReactionKinetics.PythonScript : ReactionKinetics.Expression;
            rxn.ScriptTitle = _script.SelectedItem as string ?? "";
            rxn.ReactionPhase = (ReactionPhase)_phase.SelectedIndex;
            rxn.ReactionBasis = (ReactionBasis)_basis.SelectedIndex;
            rxn.ConcUnit = _conc.SelectedItem as string ?? "";
            rxn.VelUnit = _vel.SelectedItem as string ?? "";
            rxn.Tmin = ParseD(_tmin.Text, 0.0);
            rxn.Tmax = ParseD(_tmax.Text, 2000.0);
            rxn.RateEquationNumerator = _num.Text ?? "";
            rxn.RateEquationDenominator = _den.Text ?? "1";
        }
    }
}
