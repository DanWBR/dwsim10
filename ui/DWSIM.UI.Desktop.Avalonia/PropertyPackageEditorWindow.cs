using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.PropertyPackages.Auxiliary;
using f = DWSIM.Interfaces.Enums.FlashSetting;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Tabbed editor window for a single property package.
/// Mirrors the Eto.Forms PP editor: Interaction Parameters, Equilibrium Calculations,
/// Property Calculations, Electrolyte Settings (conditional), Advanced Settings.
/// </summary>
public class PropertyPackageEditorWindow : Window
{
    private readonly IFlowsheet _flowsheet;
    private readonly PropertyPackage _pp;

    // PP types that support binary interaction parameter editing
    private static readonly string[] SupportedIPTypes =
    {
        "NRTL", "UNIQUAC",
        "Peng-Robinson (PR)", "Soave-Redlich-Kwong (SRK)",
        "Lee-Kesler-Plöcker",
        "Peng-Robinson 1978 (PR78)", "Peng-Robinson / Lee-Kesler (PR/LK)",
        "Peng-Robinson-Stryjek-Vera 2 (PRSV2-M)",
        "Peng-Robinson-Stryjek-Vera 2 (PRSV2-VL)",
        "Wilson"
    };

    public PropertyPackageEditorWindow(IFlowsheet flowsheet, PropertyPackage pp)
    {
        _flowsheet = flowsheet;
        _pp = pp;

        Title = $"Edit '{pp.Tag}' ({pp.ComponentName})";
        Width = 820;
        Height = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        IconHelper.ApplyWindowIcon(this);

        BuildUI();
    }

    // =========================================================================
    // Main UI builder
    // =========================================================================

    private void BuildUI()
    {
        var tabs = new TabControl();

        if (SupportedIPTypes.Contains(_pp.ComponentName))
        {
            _ipTab = new TabItem { Header = "Interaction Parameters", Content = BuildIPTab() };
            tabs.Items.Add(_ipTab);
        }

        tabs.Items.Add(new TabItem { Header = "Equilibrium Calculations", Content = BuildFlashTab() });
        tabs.Items.Add(new TabItem { Header = "Property Calculations", Content = BuildPropertyCalcTab() });

        if (_pp is ElectrolyteBasePropertyPackage)
            tabs.Items.Add(new TabItem { Header = "Electrolyte Settings", Content = BuildElectrolyteTab() });

        tabs.Items.Add(new TabItem { Header = "Advanced Settings", Content = BuildAdvancedTab() });

        // Dialog layout
        var root = new DockPanel { Margin = new Thickness(8) };

        var btnClose = new Button { Content = "Close", Width = 80 };
        btnClose.Classes.Add("dialog");
        btnClose.Click += (_, _) => Close();

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };
        btnPanel.Children.Add(btnClose);
        DockPanel.SetDock(btnPanel, global::Avalonia.Controls.Dock.Bottom);

        root.Children.Add(btnPanel);
        root.Children.Add(tabs);

        Content = root;
    }

    // =========================================================================
    // Tab 1: Interaction Parameters
    // =========================================================================

    private TabItem? _ipTab;

    /// <summary>Rebuilds the interaction-parameter tab from the current values.</summary>
    private void RefreshIPTab()
    {
        if (_ipTab != null) _ipTab.Content = BuildIPTab();
    }

    private ScrollViewer BuildIPTab()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        var comps = _flowsheet.SelectedCompounds.Values.Select(x => x.Name).ToList();

        switch (_pp.ComponentName)
        {
            case "NRTL":
                BuildNRTL(panel, comps);
                break;
            case "UNIQUAC":
                BuildUNIQUAC(panel, comps);
                break;
            case "Peng-Robinson (PR)":
                BuildPR(panel, comps, ((PengRobinsonPropertyPackage)_pp).m_pr.InteractionParameters);
                break;
            case "Soave-Redlich-Kwong (SRK)":
                BuildPR(panel, comps, ((SRKPropertyPackage)_pp).m_pr.InteractionParameters);
                break;
            case "Peng-Robinson 1978 (PR78)":
                BuildPR(panel, comps, ((PengRobinson1978PropertyPackage)_pp).m_pr.InteractionParameters);
                break;
            case "Peng-Robinson / Lee-Kesler (PR/LK)":
                BuildPR(panel, comps, ((PengRobinsonLKPropertyPackage)_pp).m_pr.InteractionParameters);
                break;
            case "Lee-Kesler-Plöcker":
                BuildLKP(panel, comps);
                break;
            case "Peng-Robinson-Stryjek-Vera 2 (PRSV2-M)":
                BuildPRSV2(panel, comps, (PRSV2PropertyPackage)_pp);
                break;
            case "Peng-Robinson-Stryjek-Vera 2 (PRSV2-VL)":
                BuildPRSV2VL(panel, comps, (PRSV2VLPropertyPackage)_pp);
                break;
            case "Wilson":
                BuildWilson(panel, comps);
                break;
        }

        return new ScrollViewer { Content = panel };
    }

    // --- NRTL ---
    private void BuildNRTL(StackPanel panel, List<string> comps)
    {
        var ppn = (NRTLPropertyPackage)_pp;
        var ip = ppn.m_uni.InteractionParameters;

        EnsureIPKeys(comps, ip);

        foreach (var c1 in comps)
            foreach (var c2 in comps)
            {
                if (c1 == c2 || !ip[c1].ContainsKey(c2)) continue;
                var d = ip[c1][c2];
                panel.Children.Add(MakeBinaryHeader(c1, c2, "NRTL", p =>
                {
                    if (p.TryGetValue("A12", out var a12)) d.A12 = ToDouble(a12);
                    if (p.TryGetValue("A21", out var a21)) d.A21 = ToDouble(a21);
                    if (p.TryGetValue("B12", out var b12)) d.B12 = ToDouble(b12);
                    if (p.TryGetValue("B21", out var b21)) d.B21 = ToDouble(b21);
                    if (p.TryGetValue("C12", out var c12)) d.C12 = ToDouble(c12);
                    if (p.TryGetValue("C21", out var c21)) d.C21 = ToDouble(c21);
                    if (p.TryGetValue("alpha12", out var al)) d.alpha12 = ToDouble(al);
                }));
                panel.Children.Add(MakeTextBoxRow("A12", d.A12, v => d.A12 = v));
                panel.Children.Add(MakeTextBoxRow("A21", d.A21, v => d.A21 = v));
                panel.Children.Add(MakeTextBoxRow("B12", d.B12, v => d.B12 = v));
                panel.Children.Add(MakeTextBoxRow("B21", d.B21, v => d.B21 = v));
                panel.Children.Add(MakeTextBoxRow("C12", d.C12, v => d.C12 = v));
                panel.Children.Add(MakeTextBoxRow("C21", d.C21, v => d.C21 = v));
                panel.Children.Add(MakeTextBoxRow("alpha12", d.alpha12, v => d.alpha12 = v));
            }
    }

    // --- UNIQUAC ---
    private void BuildUNIQUAC(StackPanel panel, List<string> comps)
    {
        var ppu = (UNIQUACPropertyPackage)_pp;
        var ip = ppu.m_uni.InteractionParameters;

        EnsureIPKeysUNIQUAC(comps, ip);

        foreach (var c1 in comps)
            foreach (var c2 in comps)
            {
                if (c1 == c2 || !ip[c1].ContainsKey(c2)) continue;
                var d = ip[c1][c2];
                panel.Children.Add(MakeBinaryHeader(c1, c2, "UNIQUAC", p =>
                {
                    if (p.TryGetValue("A12", out var a12)) d.A12 = ToDouble(a12);
                    if (p.TryGetValue("A21", out var a21)) d.A21 = ToDouble(a21);
                    if (p.TryGetValue("B12", out var b12)) d.B12 = ToDouble(b12);
                    if (p.TryGetValue("B21", out var b21)) d.B21 = ToDouble(b21);
                    if (p.TryGetValue("C12", out var c12)) d.C12 = ToDouble(c12);
                    if (p.TryGetValue("C21", out var c21)) d.C21 = ToDouble(c21);
                }));
                panel.Children.Add(MakeTextBoxRow("A12", d.A12, v => d.A12 = v));
                panel.Children.Add(MakeTextBoxRow("A21", d.A21, v => d.A21 = v));
                panel.Children.Add(MakeTextBoxRow("B12", d.B12, v => d.B12 = v));
                panel.Children.Add(MakeTextBoxRow("B21", d.B21, v => d.B21 = v));
                panel.Children.Add(MakeTextBoxRow("C12", d.C12, v => d.C12 = v));
                panel.Children.Add(MakeTextBoxRow("C21", d.C21, v => d.C21 = v));
            }
    }

    // --- PR / SRK / PR78 / PR-LK (kij only) ---
    // A compound x compound matrix of kij (row i / column j), like the classic UI grid.
    private void BuildPR(StackPanel panel, List<string> comps,
        Dictionary<string, Dictionary<string, PR_IPData>> ipc)
    {
        EnsureIPKeysPR(comps, ipc);

        panel.Children.Add(new TextBlock
        {
            Text = "Binary interaction parameters (kij), row i / column j:",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        foreach (var _ in comps) grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(92)));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        foreach (var _ in comps) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Column headers
        for (int j = 0; j < comps.Count; j++)
        {
            var h = new TextBlock
            {
                Text = comps[j],
                FontWeight = FontWeight.SemiBold,
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                Margin = new Thickness(4, 2),
                MaxWidth = 88,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ToolTip.SetTip(h, comps[j]);
            Grid.SetRow(h, 0);
            Grid.SetColumn(h, j + 1);
            grid.Children.Add(h);
        }

        for (int i = 0; i < comps.Count; i++)
        {
            var rh = new TextBlock
            {
                Text = comps[i],
                FontWeight = FontWeight.SemiBold,
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                Margin = new Thickness(4, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(rh, i + 1);
            Grid.SetColumn(rh, 0);
            grid.Children.Add(rh);

            for (int j = 0; j < comps.Count; j++)
            {
                var c1 = comps[i];
                var c2 = comps[j];
                Control cell;
                if (c1 == c2 || !ipc[c1].ContainsKey(c2))
                {
                    cell = new TextBox { IsEnabled = false, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Margin = new Thickness(1) };
                }
                else
                {
                    var d = ipc[c1][c2];
                    var tb = new TextBox { Text = d.kij.ToString("N4"), FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Margin = new Thickness(1) };
                    tb.LostFocus += (_, _) =>
                    {
                        if (double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var v))
                        {
                            tb.Foreground = Brushes.Black;
                            d.kij = v;
                        }
                        else
                        {
                            tb.Foreground = Brushes.Red;
                        }
                    };
                    cell = tb;
                }
                Grid.SetRow(cell, i + 1);
                Grid.SetColumn(cell, j + 1);
                grid.Children.Add(cell);
            }
        }

        panel.Children.Add(new ScrollViewer
        {
            Content = grid,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });
    }

    // --- LKP ---
    private void BuildLKP(StackPanel panel, List<string> comps)
    {
        var ppl = (LKPPropertyPackage)_pp;
        var ipl = ppl.m_lk.InteractionParameters;

        foreach (var c1 in comps)
        {
            if (!ipl.ContainsKey(c1)) ipl.Add(c1, new Dictionary<string, LKP_IPData>());
            foreach (var c2 in comps)
            {
                if (c1 != c2 && !ipl[c1].ContainsKey(c2))
                    if (ipl.ContainsKey(c2) && !ipl[c2].ContainsKey(c1))
                        ipl[c1].Add(c2, new LKP_IPData());
            }
        }

        foreach (var c1 in comps)
            foreach (var c2 in comps)
            {
                if (c1 == c2 || !ipl[c1].ContainsKey(c2)) continue;
                var d = ipl[c1][c2];
                panel.Children.Add(MakeTextBoxRow($"{c1} / {c2}  kij", d.kij, v => d.kij = v));
            }
    }

    // --- PRSV2-M ---
    private void BuildPRSV2(StackPanel panel, List<string> comps, PRSV2PropertyPackage pppv2)
    {
        var ipp = pppv2.m_pr.InteractionParameters;

        panel.Children.Add(MakeHeader("Binary Interaction Parameters (kij / kji)"));
        foreach (var c1 in comps)
        {
            var k1 = c1.ToLower();
            if (!ipp.ContainsKey(k1)) ipp.Add(k1, new Dictionary<string, PRSV2_IPData>());
            foreach (var c2 in comps)
            {
                var k2 = c2.ToLower();
                if (c1 != c2 && !ipp[k1].ContainsKey(k2))
                    if (!(ipp.ContainsKey(k2) && ipp[k2].ContainsKey(k1)))
                        ipp[k1].Add(k2, new PRSV2_IPData { id1 = c1, id2 = c2 });
            }
        }
        foreach (var c1 in comps)
        {
            var k1 = c1.ToLower();
            foreach (var c2 in comps)
            {
                var k2 = c2.ToLower();
                if (c1 != c2 && ipp.ContainsKey(k1) && ipp[k1].ContainsKey(k2))
                {
                    var d = ipp[k1][k2];
                    panel.Children.Add(MakeTextBoxRow($"{c1}/{c2} kij", d.kij, v => d.kij = v));
                    panel.Children.Add(MakeTextBoxRow($"{c1}/{c2} kji", d.kji, v => d.kji = v));
                }
            }
        }

        panel.Children.Add(MakeHeader("Pure-Compound PRSV2 Parameters (kappa1, kappa2, kappa3)"));
        foreach (var c1 in comps)
        {
            var k1 = c1.ToLower();
            if (!pppv2.m_pr._data.ContainsKey(k1))
                pppv2.m_pr._data.Add(k1, new PRSV2Param { compound = c1 });
            var par = pppv2.m_pr._data[k1];
            panel.Children.Add(MakeTextBoxRow($"{c1} kappa1", par.kappa1, v => par.kappa1 = v));
            panel.Children.Add(MakeTextBoxRow($"{c1} kappa2", par.kappa2, v => par.kappa2 = v));
            panel.Children.Add(MakeTextBoxRow($"{c1} kappa3", par.kappa3, v => par.kappa3 = v));
        }
    }

    // --- PRSV2-VL ---
    private void BuildPRSV2VL(StackPanel panel, List<string> comps, PRSV2VLPropertyPackage pppv2)
    {
        var ipp = pppv2.m_pr.InteractionParameters;

        panel.Children.Add(MakeHeader("Binary Interaction Parameters (kij / kji)"));
        foreach (var c1 in comps)
        {
            var k1 = c1.ToLower();
            if (!ipp.ContainsKey(k1)) ipp.Add(k1, new Dictionary<string, PRSV2_IPData>());
            foreach (var c2 in comps)
            {
                var k2 = c2.ToLower();
                if (c1 != c2 && !ipp[k1].ContainsKey(k2))
                    if (!(ipp.ContainsKey(k2) && ipp[k2].ContainsKey(k1)))
                        ipp[k1].Add(k2, new PRSV2_IPData { id1 = c1, id2 = c2 });
            }
        }
        foreach (var c1 in comps)
        {
            var k1 = c1.ToLower();
            foreach (var c2 in comps)
            {
                var k2 = c2.ToLower();
                if (c1 != c2 && ipp.ContainsKey(k1) && ipp[k1].ContainsKey(k2))
                {
                    var d = ipp[k1][k2];
                    panel.Children.Add(MakeTextBoxRow($"{c1}/{c2} kij", d.kij, v => d.kij = v));
                    panel.Children.Add(MakeTextBoxRow($"{c1}/{c2} kji", d.kji, v => d.kji = v));
                }
            }
        }

        panel.Children.Add(MakeHeader("Pure-Compound PRSV2 Parameters (kappa1, kappa2, kappa3)"));
        foreach (var c1 in comps)
        {
            var k1 = c1.ToLower();
            if (!pppv2.m_pr._data.ContainsKey(k1))
                pppv2.m_pr._data.Add(k1, new PRSV2Param { compound = c1 });
            var par = pppv2.m_pr._data[k1];
            panel.Children.Add(MakeTextBoxRow($"{c1} kappa1", par.kappa1, v => par.kappa1 = v));
            panel.Children.Add(MakeTextBoxRow($"{c1} kappa2", par.kappa2, v => par.kappa2 = v));
            panel.Children.Add(MakeTextBoxRow($"{c1} kappa3", par.kappa3, v => par.kappa3 = v));
        }
    }

    // --- Wilson ---
    private void BuildWilson(StackPanel panel, List<string> comps)
    {
        var ppw = (DWSIM.Thermodynamics.WilsonPropertyPackage)_pp;
        var bips = ppw.WilsonM.BIPs;
        var casmap = _flowsheet.SelectedCompounds.Values.ToDictionary(x => x.Name, x => x.CAS_Number);

        foreach (var c1 in comps)
        {
            var cas1 = casmap[c1];
            if (!bips.ContainsKey(cas1)) bips.Add(cas1, new Dictionary<string, double[]>());
            foreach (var c2 in comps)
            {
                if (c1 == c2) continue;
                var cas2 = casmap[c2];
                if (!bips[cas1].ContainsKey(cas2))
                    if (!(bips.ContainsKey(cas2) && bips[cas2].ContainsKey(cas1)))
                        bips[cas1].Add(cas2, new double[] { 0.0, 0.0 });
            }
        }

        foreach (var c1 in comps)
        {
            var cas1 = casmap[c1];
            foreach (var c2 in comps)
            {
                if (c1 == c2) continue;
                var cas2 = casmap[c2];
                if (bips.ContainsKey(cas1) && bips[cas1].ContainsKey(cas2))
                {
                    var arr = bips[cas1][cas2];
                    panel.Children.Add(MakeTextBoxRow($"{c1}/{c2} A12 (cal/mol)", arr[0], v => arr[0] = v));
                    panel.Children.Add(MakeTextBoxRow($"{c1}/{c2} A21 (cal/mol)", arr[1], v => arr[1] = v));
                }
            }
        }
    }

    // --- IP key initialization helpers ---

    private static void EnsureIPKeys(List<string> comps, Dictionary<string, Dictionary<string, NRTL_IPData>> ip)
    {
        foreach (var c1 in comps)
        {
            if (!ip.ContainsKey(c1)) ip.Add(c1, new Dictionary<string, NRTL_IPData>());
            foreach (var c2 in comps)
            {
                if (c1 != c2 && !ip[c1].ContainsKey(c2))
                    if (ip.ContainsKey(c2) && !ip[c2].ContainsKey(c1))
                        ip[c1].Add(c2, new NRTL_IPData());
            }
        }
    }

    private static void EnsureIPKeysUNIQUAC(List<string> comps, Dictionary<string, Dictionary<string, UNIQUAC_IPData>> ip)
    {
        foreach (var c1 in comps)
        {
            if (!ip.ContainsKey(c1)) ip.Add(c1, new Dictionary<string, UNIQUAC_IPData>());
            foreach (var c2 in comps)
            {
                if (c1 != c2 && !ip[c1].ContainsKey(c2))
                    if (ip.ContainsKey(c2) && !ip[c2].ContainsKey(c1))
                        ip[c1].Add(c2, new UNIQUAC_IPData());
            }
        }
    }

    private static void EnsureIPKeysPR(List<string> comps, Dictionary<string, Dictionary<string, PR_IPData>> ip)
    {
        foreach (var c1 in comps)
        {
            if (!ip.ContainsKey(c1)) ip.Add(c1, new Dictionary<string, PR_IPData>());
            foreach (var c2 in comps)
            {
                if (c1 != c2 && !ip[c1].ContainsKey(c2))
                    if (ip.ContainsKey(c2) && !ip[c2].ContainsKey(c1))
                        ip[c1].Add(c2, new PR_IPData());
            }
        }
    }

    // =========================================================================
    // Tab 2: Equilibrium Calculations (Flash Settings)
    // =========================================================================

    private ScrollViewer BuildFlashTab()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        var s = _pp.FlashSettings;
        var ci = CultureInfo.InvariantCulture;

        panel.Children.Add(MakeHeader("General"));

        var eqTypes = new[] { "Default", "VLE", "VLLE", "SVLE", "SVLLE", "No Flash" };
        panel.Children.Add(MakeDropDownRow("Phase Equilibria Calculation Type", eqTypes,
            s[f.ForceEquilibriumCalculationType],
            idx => s[f.ForceEquilibriumCalculationType] = eqTypes[idx]));

        var fsModes = new[] { "Rigorous VLE", "Ideal VLE", "No Flash", "Nothing (Throw Error)" };
        panel.Children.Add(MakeDropDownRow("Fail-Safe Procedure", fsModes,
            int.Parse(s[f.FailSafeCalculationMode]),
            idx => s[f.FailSafeCalculationMode] = idx.ToString()));

        var methods = new[] { "Nested Loops", "Inside-Out", "Gibbs Minimization" };
        panel.Children.Add(MakeDropDownRow("Numerical Method", methods,
            (int)_pp.FlashCalculationApproach,
            idx => _pp.FlashCalculationApproach = (PropertyPackage.FlashCalculationApproachType)idx));

        panel.Children.Add(MakeCheckBox("Identify Phases After Equilibrium Calculation",
            bool.Parse(s[f.UsePhaseIdentificationAlgorithm]),
            v => s[f.UsePhaseIdentificationAlgorithm] = v.ToString()));

        panel.Children.Add(MakeCheckBox("Calculate Saturation Conditions (Bubble/Dew Points)",
            bool.Parse(s[f.CalculateBubbleAndDewPoints]),
            v => s[f.CalculateBubbleAndDewPoints] = v.ToString()));

        panel.Children.Add(MakeCheckBox("Validate Equilibrium Calculation Results",
            bool.Parse(s[f.ValidateEquilibriumCalc]),
            v => s[f.ValidateEquilibriumCalc] = v.ToString()));

        panel.Children.Add(MakeCheckBox("Display Warning for Missing Compound Parameters",
            _pp.DisplayMissingCompoundPropertiesWarning,
            v => _pp.DisplayMissingCompoundPropertiesWarning = v));

        // Nested Loops Options
        panel.Children.Add(MakeHeader("Nested Loops Options"));

        panel.Children.Add(MakeCheckBox("Handle Solids",
            bool.Parse(s[f.HandleSolidsInDefaultEqCalcMode]),
            v => s[f.HandleSolidsInDefaultEqCalcMode] = v.ToString()));

        panel.Children.Add(MakeCheckBox("Immiscible Water",
            bool.Parse(s[f.ImmiscibleWaterOption]),
            v => s[f.ImmiscibleWaterOption] = v.ToString()));

        panel.Children.Add(MakeCheckBox("PH/PS Flash Fast Mode",
            bool.Parse(s[f.NL_FastMode]),
            v => s[f.NL_FastMode] = v.ToString()));

        panel.Children.Add(MakeCheckBox("PH Flash - Interpolate Temperature on Oscillating Cases",
            bool.Parse(s[f.PHFlash_Use_Interpolated_Result_In_Oscillating_Temperature_Cases]),
            v => s[f.PHFlash_Use_Interpolated_Result_In_Oscillating_Temperature_Cases] = v.ToString()));

        panel.Children.Add(MakeCheckBox("PV Flash - Try Ideal Calculation on Failure",
            bool.Parse(s[f.PVFlash_TryIdealCalcOnFailure]),
            v => s[f.PVFlash_TryIdealCalcOnFailure] = v.ToString()));

        // Convergence Parameters
        panel.Children.Add(MakeHeader("Convergence Parameters"));

        panel.Children.Add(MakeTextBoxRow("PV Flash - Temp Delta for K-value Derivative",
            double.Parse(s[f.PVFlash_TemperatureDerivativeEpsilon], ci),
            v => s[f.PVFlash_TemperatureDerivativeEpsilon] = v.ToString(ci)));

        panel.Children.Add(MakeTextBoxRow("PV Flash - Max Temp Update Delta (K)",
            double.Parse(s[f.PVFlash_MaximumTemperatureChange], ci),
            v => s[f.PVFlash_MaximumTemperatureChange] = v.ToString(ci)));

        panel.Children.Add(MakeTextBoxRow("PV Flash - Fixed Damping Factor (0-2)",
            double.Parse(s[f.PVFlash_FixedDampingFactor], ci),
            v => s[f.PVFlash_FixedDampingFactor] = v.ToString(ci)));

        panel.Children.Add(MakeTextBoxRow("PH/PS Flash - Max Temp Update Delta (K)",
            double.Parse(s[f.PHFlash_MaximumTemperatureChange], ci),
            v => s[f.PHFlash_MaximumTemperatureChange] = v.ToString(ci)));

        panel.Children.Add(MakeTextBoxRow("PT Flash - Fixed Damping Factor (0-2)",
            double.Parse(s[f.PTFlash_DampingFactor], ci),
            v => s[f.PTFlash_DampingFactor] = v.ToString(ci)));

        // Convergence Tolerances
        panel.Children.Add(MakeHeader("Convergence Error Tolerances"));

        panel.Children.Add(MakeTextBoxRow("PT/PV - Internal loop tolerance",
            double.Parse(s[f.PTFlash_Internal_Loop_Tolerance], ci),
            v => s[f.PTFlash_Internal_Loop_Tolerance] = v.ToString(ci)));

        panel.Children.Add(MakeTextBoxRow("PT/PV - External loop tolerance",
            double.Parse(s[f.PTFlash_External_Loop_Tolerance], ci),
            v => s[f.PTFlash_External_Loop_Tolerance] = v.ToString(ci)));

        panel.Children.Add(MakeTextBoxRow("PH/PS - Internal loop tolerance",
            double.Parse(s[f.PHFlash_Internal_Loop_Tolerance], ci),
            v => s[f.PHFlash_Internal_Loop_Tolerance] = v.ToString(ci)));

        panel.Children.Add(MakeTextBoxRow("PH/PS - External loop tolerance",
            double.Parse(s[f.PHFlash_External_Loop_Tolerance], ci),
            v => s[f.PHFlash_External_Loop_Tolerance] = v.ToString(ci)));

        panel.Children.Add(MakeIntTextBoxRow("PT/PV - Max internal iterations",
            int.Parse(s[f.PTFlash_Maximum_Number_Of_Internal_Iterations], ci),
            v => s[f.PTFlash_Maximum_Number_Of_Internal_Iterations] = v.ToString(ci)));

        panel.Children.Add(MakeIntTextBoxRow("PT/PV - Max external iterations",
            int.Parse(s[f.PTFlash_Maximum_Number_Of_External_Iterations], ci),
            v => s[f.PTFlash_Maximum_Number_Of_External_Iterations] = v.ToString(ci)));

        panel.Children.Add(MakeIntTextBoxRow("PH/PS - Max internal iterations",
            int.Parse(s[f.PHFlash_Maximum_Number_Of_Internal_Iterations], ci),
            v => s[f.PHFlash_Maximum_Number_Of_Internal_Iterations] = v.ToString(ci)));

        panel.Children.Add(MakeIntTextBoxRow("PH/PS - Max external iterations",
            int.Parse(s[f.PHFlash_Maximum_Number_Of_External_Iterations], ci),
            v => s[f.PHFlash_Maximum_Number_Of_External_Iterations] = v.ToString(ci)));

        return new ScrollViewer { Content = panel };
    }

    // =========================================================================
    // Tab 3: Property Calculations
    // =========================================================================

    private ScrollViewer BuildPropertyCalcTab()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(8) };

        // Liquid Phase Density
        panel.Children.Add(MakeHeader("Liquid Phase Density"));

        var densityModes = Enum.GetNames(typeof(PropertyPackage.LiquidDensityCalcMode));
        panel.Children.Add(MakeDropDownRow("Calculation Method", densityModes,
            (int)_pp.LiquidDensityCalculationMode_Subcritical,
            idx =>
            {
                _pp.LiquidDensityCalculationMode_Subcritical = (PropertyPackage.LiquidDensityCalcMode)idx;
                _pp.LiquidDensityCalculationMode_Supercritical = (PropertyPackage.LiquidDensityCalcMode)idx;
            }));

        panel.Children.Add(MakeCheckBox("Correct Experimental Data for Pressure",
            _pp.LiquidDensity_CorrectExpDataForPressure,
            v => _pp.LiquidDensity_CorrectExpDataForPressure = v));

        var cbPeneloux = MakeCheckBox("Use Peneloux Volume Translation (PR/SRK EOS only)",
            _pp.LiquidDensity_UsePenelouxVolumeTranslation,
            v => _pp.LiquidDensity_UsePenelouxVolumeTranslation = v);
        cbPeneloux.IsEnabled = _pp is PengRobinsonPropertyPackage || _pp is SRKPropertyPackage;
        panel.Children.Add(cbPeneloux);

        // Liquid Phase Viscosity
        panel.Children.Add(MakeHeader("Liquid Phase Viscosity"));

        var viscModes = Enum.GetNames(typeof(PropertyPackage.LiquidViscosityCalcMode));
        panel.Children.Add(MakeDropDownRow("Calculation Method", viscModes,
            (int)_pp.LiquidViscosityCalculationMode_Subcritical,
            idx =>
            {
                _pp.LiquidViscosityCalculationMode_Subcritical = (PropertyPackage.LiquidViscosityCalcMode)idx;
                _pp.LiquidViscosityCalculationMode_Supercritical = (PropertyPackage.LiquidViscosityCalcMode)idx;
            }));

        var viscMixRules = Enum.GetNames(typeof(PropertyPackage.LiquidViscosityMixRule));
        panel.Children.Add(MakeDropDownRow("Mixing Rule", viscMixRules,
            (int)_pp.LiquidViscosity_MixingRule,
            idx => _pp.LiquidViscosity_MixingRule = (PropertyPackage.LiquidViscosityMixRule)idx));

        panel.Children.Add(MakeCheckBox("Correct Experimental Data for Pressure",
            _pp.LiquidViscosity_CorrectExpDataForPressure,
            v => _pp.LiquidViscosity_CorrectExpDataForPressure = v));

        // Fugacity
        panel.Children.Add(MakeHeader("Fugacity Calculation"));

        var fugModes = Enum.GetNames(typeof(PropertyPackage.VaporPhaseFugacityCalcMode));
        var ddFug = MakeDropDownRow("Vapor Phase Fugacity", fugModes,
            (int)_pp.VaporPhaseFugacityCalculationMode,
            idx => _pp.VaporPhaseFugacityCalculationMode = (PropertyPackage.VaporPhaseFugacityCalcMode)idx);
        ddFug.IsEnabled = _pp is ActivityCoefficientPropertyPackage;
        panel.Children.Add(ddFug);

        var cbPoynting = MakeCheckBox("Liquid Phase: Use Poynting Correction Factor",
            _pp.LiquidFugacity_UsePoyntingCorrectionFactor,
            v => _pp.LiquidFugacity_UsePoyntingCorrectionFactor = v);
        cbPoynting.IsEnabled = _pp is ActivityCoefficientPropertyPackage;
        panel.Children.Add(cbPoynting);

        // Enthalpy / Entropy
        panel.Children.Add(MakeHeader("Enthalpy, Entropy, Cp and Cv"));

        var hhModes = Enum.GetNames(typeof(PropertyPackage.EnthalpyEntropyCpCvCalcMode));
        var ddHH = MakeDropDownRow("Calculation Method (AC models)", hhModes,
            (int)_pp.EnthalpyEntropyCpCvCalculationMode,
            idx => _pp.EnthalpyEntropyCpCvCalculationMode = (PropertyPackage.EnthalpyEntropyCpCvCalcMode)idx);
        ddHH.IsEnabled = _pp is ActivityCoefficientPropertyPackage;
        panel.Children.Add(ddHH);

        var eosModes = Enum.GetNames(typeof(PropertyPackage.LiquidEnthalpyEntropyCpCvCalcMode_EOS));
        var ddEOS = MakeDropDownRow("Calculation Method (EOS models)", eosModes,
            (int)_pp.LiquidEnthalpyEntropyCpCvCalculationMode_EOS,
            idx => _pp.LiquidEnthalpyEntropyCpCvCalculationMode_EOS = (PropertyPackage.LiquidEnthalpyEntropyCpCvCalcMode_EOS)idx);
        ddEOS.IsEnabled = _pp.PackageType == PackageType.EOS;
        panel.Children.Add(ddEOS);

        // Other
        panel.Children.Add(MakeHeader("Other"));

        var cbIgnoreIP = MakeCheckBox("Ignore Missing UNIQUAC/NRTL Interaction Parameters",
            _pp.ActivityCoefficientModels_IgnoreMissingInteractionParameters,
            v => _pp.ActivityCoefficientModels_IgnoreMissingInteractionParameters = v);
        cbIgnoreIP.IsEnabled = _pp is UNIQUACPropertyPackage || _pp is NRTLPropertyPackage;
        panel.Children.Add(cbIgnoreIP);

        var cbAutoEstIP = MakeCheckBox("Auto Estimate Missing UNIQUAC/NRTL Interaction Parameters",
            _pp.AutoEstimateMissingNRTLUNIQUACParameters,
            v => _pp.AutoEstimateMissingNRTLUNIQUACParameters = v);
        cbAutoEstIP.IsEnabled = _pp is UNIQUACPropertyPackage || _pp is NRTLPropertyPackage;
        panel.Children.Add(cbAutoEstIP);

        var cbSalinity = MakeCheckBox("Ignore Maximum Salinity Limit (Seawater only)",
            _pp.IgnoreSalinityLimit,
            v => _pp.IgnoreSalinityLimit = v);
        cbSalinity.IsEnabled = _pp is SeawaterPropertyPackage;
        panel.Children.Add(cbSalinity);

        var cbVFrac = MakeCheckBox("Ignore Vapor Fraction Bounds (Sour Water only)",
            _pp.IgnoreVaporFractionLimit,
            v => _pp.IgnoreVaporFractionLimit = v);
        cbVFrac.IsEnabled = _pp is SourWaterPropertyPackage;
        panel.Children.Add(cbVFrac);

        return new ScrollViewer { Content = panel };
    }

    // =========================================================================
    // Tab 4: Electrolyte Settings (conditional)
    // =========================================================================

    private ScrollViewer BuildElectrolyteTab()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        var epp = (ElectrolyteBasePropertyPackage)_pp;

        panel.Children.Add(MakeHeader("Electrolyte Solver Settings"));

        panel.Children.Add(MakeIntTextBoxRow("Max. Iterations", epp.MaxIterations,
            v => epp.MaxIterations = v));

        panel.Children.Add(MakeTextBoxRow("Convergence Tolerance", epp.Tolerance,
            v => epp.Tolerance = v));

        panel.Children.Add(MakeHeader("Reaction Set"));

        var rsNames = _flowsheet.ReactionSets.Values.Select(x => x.Name).ToList();
        var rsIds = _flowsheet.ReactionSets.Values.Select(x => x.ID).ToList();
        var currentIdx = rsIds.IndexOf(epp.ReactionSet);
        if (currentIdx < 0) currentIdx = 0;

        if (rsNames.Count > 0)
        {
            panel.Children.Add(MakeDropDownRow("Equilibrium/Kinetic Reaction Set", rsNames.ToArray(),
                currentIdx,
                idx => epp.ReactionSet = rsIds[idx]));
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text = "No reaction sets defined. Add reactions via Simulation Settings first.",
                FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4)
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = "These parameters control the isothermal-flash solver used by the electrolyte property package.",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(10),
            Foreground = new SolidColorBrush(Color.Parse("#777")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });

        return new ScrollViewer { Content = panel };
    }

    // =========================================================================
    // Tab 5: Advanced Settings (Forced Solids)
    // =========================================================================

    private ScrollViewer BuildAdvancedTab()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(8) };

        panel.Children.Add(MakeHeader("Forced Solids"));
        panel.Children.Add(new TextBlock
        {
            Text = "Select the compounds which will be forcedly put into the solid phase.\n" +
                   "This setting works only with the Nested Loops SVLE (Eutetic) Flash Algorithm.",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(10),
            Foreground = new SolidColorBrush(Color.Parse("#777")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (var comp in _flowsheet.SelectedCompounds.Values)
        {
            var name = comp.Name;
            panel.Children.Add(MakeCheckBox(name,
                _pp.ForcedSolids.Contains(name),
                v =>
                {
                    if (v) { if (!_pp.ForcedSolids.Contains(name)) _pp.ForcedSolids.Add(name); }
                    else { _pp.ForcedSolids.Remove(name); }
                }));
        }

        AddPropertyOverrides(panel);

        return new ScrollViewer { Content = panel };
    }

    /// <summary>
    /// The property override editor: a Python script that replaces a calculated phase property.
    /// The script must set a variable named 'propval' with the value to use.
    /// </summary>
    private void AddPropertyOverrides(StackPanel panel)
    {
        panel.Children.Add(MakeHeader("Override Phase Properties"));
        panel.Children.Add(new TextBlock
        {
            Text = "Write a Python script to override a calculated phase property. Select the " +
                   "Phase/Property pair and write the script, which must set a variable named " +
                   "'propval' with the override value.\n\n" +
                   "Available variables: 'flowsheet' (the flowsheet), 'this' (the property package), " +
                   "'matstr' (the associated material stream), 'phase' (the current phase), " +
                   "'currval' (the current value), 'T' (K) and 'P' (Pa) of the material stream.",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(10),
            Foreground = new SolidColorBrush(Color.Parse("#777")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var phases = new[] { "Mixture", "Vapor", "OverallLiquid", "Liquid1", "Liquid2", "Solid" };

        var keys = new List<string> { "" };
        foreach (var phase in phases)
        {
            foreach (var property in typeof(DWSIM.Thermodynamics.BaseClasses.PhaseProperties).GetProperties())
            {
                keys.Add(phase + "/" + property.Name);
            }
        }

        var editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            Height = 260,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(0, 4, 0, 4)
        };

        var selector = new ComboBox { ItemsSource = keys, SelectedIndex = 0, Width = 320 };

        selector.SelectionChanged += (_, _) =>
        {
            var key = selector.SelectedItem as string ?? "";

            editor.Text = _pp.PropertyOverrides.ContainsKey(key) ? _pp.PropertyOverrides[key] : "";
        };

        panel.Children.Add(new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 2),
            Children =
            {
                new TextBlock
                {
                    Text = "Phase / Property",
                    Width = 120,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11)
                },
                selector
            }
        });

        panel.Children.Add(editor);

        var save = new Button { Content = "Save Script", Margin = new Thickness(0, 0, 6, 0) };
        var clear = new Button { Content = "Clear Script" };

        save.Click += (_, _) =>
        {
            var key = selector.SelectedItem as string ?? "";

            if (key == "") return;

            _pp.PropertyOverrides[key] = editor.Text ?? "";
        };

        clear.Click += (_, _) =>
        {
            var key = selector.SelectedItem as string ?? "";

            editor.Text = "";
            _pp.PropertyOverrides.Remove(key);
        };

        panel.Children.Add(new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Children = { save, clear }
        });
    }

    // =========================================================================
    // UI Helper methods
    // =========================================================================

    /// <summary>
    /// Header for one binary pair, with a Regress button that runs the data regression utility
    /// for that pair and writes the returned parameters back through <paramref name="apply"/>.
    /// </summary>
    private Control MakeBinaryHeader(string c1, string c2, string model,
        Action<Dictionary<string, object>> apply)
    {
        var header = MakeHeader($"{c1} / {c2}");
        header.VerticalAlignment = VerticalAlignment.Center;
        header.Margin = new Thickness(0, 10, 8, 4);

        var btn = new Button
        {
            Content = "Regress...",
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        btn.Classes.Add("panel");
        ToolTip.SetTip(btn, $"Regress the {model} parameters for {c1} + {c2} from experimental data.");

        btn.Click += (_, _) =>
        {
            // Goes through IFlowsheet.CallDataRegressionUtility, which the Avalonia host points
            // at its own regression window; the call blocks until the user transfers or cancels.
            var ip = _flowsheet.CallDataRegressionUtility(c1, c2, model);
            if (ip?.Parameters == null || ip.Parameters.Count == 0) return;

            apply(ip.Parameters);

            // Rebuild the tab so the text boxes show the regressed values.
            RefreshIPTab();
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        row.Children.Add(header);
        row.Children.Add(btn);
        return row;
    }

    private static double ToDouble(object value)
    {
        try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
        catch { return 0.0; }
    }

    private static TextBlock MakeHeader(string text) => new()
    {
        Text = text,
        FontWeight = FontWeight.Bold,
        FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12),
        Margin = new Thickness(0, 10, 0, 4)
    };

    private static StackPanel MakeTextBoxRow(string label, double value, Action<double> setter)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 1) };

        var lbl = new TextBlock
        {
            Text = label,
            Width = 350,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11)
        };

        var tb = new TextBox { Text = value.ToString("N4"), Width = 160, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        tb.LostFocus += (_, _) =>
        {
            if (double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var v))
            {
                tb.Foreground = Brushes.Black;
                setter(v);
            }
            else
            {
                tb.Foreground = Brushes.Red;
            }
        };

        row.Children.Add(lbl);
        row.Children.Add(tb);
        return row;
    }

    private static StackPanel MakeIntTextBoxRow(string label, int value, Action<int> setter)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 1) };

        var lbl = new TextBlock
        {
            Text = label,
            Width = 350,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11)
        };

        var tb = new TextBox { Text = value.ToString(), Width = 160, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        tb.LostFocus += (_, _) =>
        {
            if (int.TryParse(tb.Text, out var v))
            {
                tb.Foreground = Brushes.Black;
                setter(v);
            }
            else
            {
                tb.Foreground = Brushes.Red;
            }
        };

        row.Children.Add(lbl);
        row.Children.Add(tb);
        return row;
    }

    private static StackPanel MakeDropDownRow(string label, string[] items, int selectedIndex, Action<int> setter)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2) };

        var lbl = new TextBlock
        {
            Text = label,
            Width = 350,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11)
        };

        var cb = new ComboBox { Width = 220, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        foreach (var item in items) cb.Items.Add(item);
        if (selectedIndex >= 0 && selectedIndex < cb.Items.Count) cb.SelectedIndex = selectedIndex;
        cb.SelectionChanged += (_, _) => { if (cb.SelectedIndex >= 0) setter(cb.SelectedIndex); };

        row.Children.Add(lbl);
        row.Children.Add(cb);
        return row;
    }

    /// <summary>DropDown where selectedIndex is determined by matching a string value.</summary>
    private static StackPanel MakeDropDownRow(string label, string[] items, string selectedValue, Action<int> setter)
    {
        int idx = Array.IndexOf(items, selectedValue);
        if (idx < 0) idx = 0;
        return MakeDropDownRow(label, items, idx, setter);
    }

    private static CheckBox MakeCheckBox(string text, bool isChecked, Action<bool> setter)
    {
        var cb = new CheckBox
        {
            Content = text,
            IsChecked = isChecked,
            Margin = new Thickness(0, 2),
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11)
        };
        cb.IsCheckedChanged += (_, _) => setter(cb.IsChecked.GetValueOrDefault());
        return cb;
    }
}
