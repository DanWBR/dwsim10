using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Units = DWSIM.SharedClasses.SystemsOfUnits.Units;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Systems of Units editor. Edits the entries of IFlowsheet.AvailableSystemsOfUnits, which
/// SaveToXML/LoadFromXML persist with the simulation, and can set the active one.
/// Avalonia counterpart of DWSIM.UI.Desktop.Editors.UnitSetEditorView plus the unit-set
/// management that lives in the Eto Simulation Settings page.
/// </summary>
public partial class UnitSystemEditorWindow : Window
{
    private static readonly string[] BuiltIn = { "SI", "CGS", "ENG" };

    private readonly IFlowsheet _flowsheet;
    private IUnitsOfMeasure? _current;
    private bool _loading;

    // Parameterless ctor required by Avalonia's XAML compiler (designer-only).
    public UnitSystemEditorWindow() : this(null!) { }

    public UnitSystemEditorWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet!;
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        if (flowsheet == null) return;

        WireEvents();
        RefreshSystemList(_flowsheet.FlowsheetOptions.SelectedUnitSystem?.Name);
    }

    private List<IUnitsOfMeasure> Systems => _flowsheet.AvailableSystemsOfUnits;

    private static bool IsBuiltIn(IUnitsOfMeasure u) => BuiltIn.Contains(u.Name);

    // -------------------------------------------------------------------------

    private void WireEvents()
    {
        BtnClose.Click += (_, _) => Close();

        CbSystem.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            var idx = CbSystem.SelectedIndex;
            if (idx >= 0 && idx < Systems.Count) LoadSystem(Systems[idx]);
        };

        BtnNew.Click += (_, _) =>
        {
            var s = new DWSIM.SharedClasses.SystemsOfUnits.SI { Name = UniqueName("New Unit Set") };
            Systems.Add(s);
            RefreshSystemList(s.Name);
            TbStatus.Text = "New unit set created.";
        };

        BtnClone.Click += (_, _) =>
        {
            if (_current == null) return;
            var copy = new DWSIM.SharedClasses.SystemsOfUnits.SI { Name = UniqueName(_current.Name + " (copy)") };
            CopyUnits(_current, copy);
            Systems.Add(copy);
            RefreshSystemList(copy.Name);
            TbStatus.Text = "Unit set duplicated.";
        };

        BtnDelete.Click += (_, _) =>
        {
            if (_current == null) return;
            if (IsBuiltIn(_current)) { TbStatus.Text = "Built-in unit sets cannot be deleted."; return; }
            var wasActive = _flowsheet.FlowsheetOptions.SelectedUnitSystem == _current;
            Systems.Remove(_current);
            if (wasActive)
                _flowsheet.FlowsheetOptions.SelectedUnitSystem =
                    Systems.FirstOrDefault(x => x.Name == "SI") ?? Systems.FirstOrDefault();
            _current = null;
            RefreshSystemList(_flowsheet.FlowsheetOptions.SelectedUnitSystem?.Name);
            TbStatus.Text = "Unit set removed.";
        };

        TbName.TextChanged += (_, _) =>
        {
            if (_loading || _current == null || IsBuiltIn(_current)) return;
            _current.Name = TbName.Text ?? "";
            var idx = CbSystem.SelectedIndex;
            if (idx >= 0 && idx < CbSystem.Items.Count)
            {
                _loading = true;
                CbSystem.Items[idx] = _current.Name;
                CbSystem.SelectedIndex = idx;
                _loading = false;
            }
        };

        ChkActive.IsCheckedChanged += (_, _) =>
        {
            if (_loading || _current == null) return;
            if (ChkActive.IsChecked.GetValueOrDefault())
            {
                _flowsheet.FlowsheetOptions.SelectedUnitSystem = _current;
                TbStatus.Text = $"'{_current.Name}' is now the active unit system.";
            }
        };
    }

    private string UniqueName(string baseName)
    {
        var name = baseName;
        int i = 2;
        while (Systems.Any(x => x.Name == name)) name = $"{baseName} {i++}";
        return name;
    }

    private void RefreshSystemList(string? select)
    {
        _loading = true;
        CbSystem.Items.Clear();
        foreach (var s in Systems) CbSystem.Items.Add(s.Name);
        _loading = false;

        var idx = select == null ? 0 : Systems.FindIndex(x => x.Name == select);
        CbSystem.SelectedIndex = Systems.Count == 0 ? -1 : Math.Max(0, idx);
    }

    // -------------------------------------------------------------------------

    private void LoadSystem(IUnitsOfMeasure u)
    {
        _current = u;
        var readOnly = IsBuiltIn(u);

        _loading = true;
        TbName.Text = u.Name;
        TbName.IsEnabled = !readOnly;
        ChkActive.IsChecked = _flowsheet.FlowsheetOptions.SelectedUnitSystem == u;
        _loading = false;

        var panel = new AvaloniaEditorPanel();
        panel.CreateAndAddLabelRow("Units");
        if (readOnly)
            panel.CreateAndAddDescriptionRow("This is a built-in set and cannot be edited. Use Duplicate to create an editable copy.");

        // One picker per UnitOfMeasure. The enum member names match the property names on
        // Units one-to-one, so the rows are built by reflection instead of by hand.
        foreach (var measure in Enum.GetValues(typeof(UnitOfMeasure)).Cast<UnitOfMeasure>()
                     .OrderBy(m => m.ToString()))
        {
            if (measure == UnitOfMeasure.none) continue;

            var prop = typeof(Units).GetProperty(measure.ToString(),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || prop.PropertyType != typeof(string) || !prop.CanWrite) continue;

            string[] options;
            try { options = u.GetUnitSet(measure)?.ToArray() ?? Array.Empty<string>(); }
            catch { continue; }
            if (options.Length == 0) continue;

            var currentValue = prop.GetValue(u) as string ?? "";
            var target = u;
            var p = prop;

            var dd = panel.CreateAndAddDropDownRow(Caption(measure), options.ToList(),
                Math.Max(0, Array.IndexOf(options, currentValue)),
                (cb, e) =>
                {
                    if (cb.SelectedIndex < 0 || cb.SelectedIndex >= options.Length) return;
                    p.SetValue(target, options[cb.SelectedIndex]);
                    TbStatus.Text = "Unit changed; re-open editors to see the new unit.";
                });
            dd.IsEnabled = !readOnly;
        }

        UnitsHost.Content = panel;
    }

    /// <summary>Turns an enum member name into a readable label.</summary>
    private static string Caption(UnitOfMeasure measure)
    {
        var raw = measure.ToString().Replace('_', ' ');
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]) && raw[i - 1] != ' ')
                sb.Append(' ');
            sb.Append(i == 0 ? char.ToUpperInvariant(raw[i]) : raw[i]);
        }
        return sb.ToString();
    }

    /// <summary>Copies every unit string from one set to another.</summary>
    private static void CopyUnits(IUnitsOfMeasure from, IUnitsOfMeasure to)
    {
        foreach (var prop in typeof(Units).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.PropertyType != typeof(string) || !prop.CanRead || !prop.CanWrite) continue;
            if (prop.Name == "Name") continue;
            try { prop.SetValue(to, prop.GetValue(from)); } catch { }
        }
    }
}
