using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using S = DWSIM.GlobalSettings.Settings;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Avalonia port of DWSIM's Inspector window. Drives the engine-side static class
/// DWSIM.Inspector.Host (and InspectorItem) through reflection so we don't need a
/// hard-coded reference to the net472 DWSIM.Inspector.dll at compile time. The
/// AssemblyResolve handler in Program.cs loads the DLL on demand.
/// </summary>
public partial class InspectorReportsWindow : Window
{
    private Type? _hostType;
    private Type? _itemType;
    private MethodInfo? _getHtmlMethod;

    private record Node(object Item, string Display, List<Node> Children);

    private readonly List<Node> _flat = new();

    private string _html = "";

    public InspectorReportsWindow()
    {
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        ResolveTypes();

        CbEnable.IsChecked = S.InspectorEnabled;
        CbEnable.IsCheckedChanged += (_, _) => S.InspectorEnabled = CbEnable.IsChecked.GetValueOrDefault();

        BtnClear.Click += (_, _) =>
        {
            ClearHostItems();
            RefreshList();
        };

        BtnRefresh.Click += (_, _) => RefreshList();

        LbItems.SelectionChanged += (_, _) => ShowSelected();

        BtnOpenReport.Click += (_, _) => OpenInBrowser();

        RefreshList();
    }

    private void ResolveTypes()
    {
        try
        {
            // The Inspector assembly might not be in the load context yet; force a load.
            Assembly inspector = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "DWSIM.Inspector")
                ?? Assembly.Load("DWSIM.Inspector");

            _hostType = inspector.GetType("Inspector.Host") ?? inspector.GetType("DWSIM.Inspector.Host");
            _itemType = inspector.GetType("Inspector.InspectorItem") ?? inspector.GetType("DWSIM.Inspector.InspectorItem");
            _getHtmlMethod = _itemType?.GetMethod("GetHTML");
        }
        catch
        {
            // Inspector DLL not available — UI still renders but lists nothing.
        }
    }

    private IEnumerable<object> GetHostItems()
    {
        if (_hostType == null) yield break;
        var prop = _hostType.GetField("Items", BindingFlags.Public | BindingFlags.Static);
        if (prop?.GetValue(null) is not IEnumerable raw) yield break;
        foreach (var o in raw) yield return o;
    }

    private void ClearHostItems()
    {
        if (_hostType == null) return;
        var field = _hostType.GetField("Items", BindingFlags.Public | BindingFlags.Static);
        if (field?.GetValue(null) is IList list) list.Clear();
    }

    private void RefreshList()
    {
        _flat.Clear();
        LbItems.Items.Clear();

        if (_hostType == null || _itemType == null)
        {
            LblCount.Text = "Inspector module not available.";
            return;
        }

        foreach (var item in GetHostItems())
            Walk(item, depth: 0);

        foreach (var n in _flat) LbItems.Items.Add(n.Display);
        LblCount.Text = $"{_flat.Count} item(s).";
    }

    private void Walk(object item, int depth)
    {
        var nameProp = _itemType!.GetProperty("Name");
        var descProp = _itemType.GetProperty("Description");
        var childrenProp = _itemType.GetProperty("Items");

        var name = nameProp?.GetValue(item)?.ToString() ?? "(unnamed)";
        var desc = descProp?.GetValue(item)?.ToString() ?? "";
        var prefix = new string(' ', depth * 2);
        var display = $"{prefix}{name}" + (string.IsNullOrEmpty(desc) ? "" : $"  --  {desc}");

        _flat.Add(new Node(item, display, new List<Node>()));

        if (childrenProp?.GetValue(item) is IEnumerable children)
            foreach (var child in children)
                Walk(child, depth + 1);
    }

    private void ShowSelected()
    {
        if (LbItems.SelectedIndex < 0 || LbItems.SelectedIndex >= _flat.Count) return;
        if (_getHtmlMethod == null) return;

        var item = _flat[LbItems.SelectedIndex].Item;
        try
        {
            _html = _getHtmlMethod.Invoke(item, null) as string ?? "";
            TbReport.Text = _html;
            BtnOpenReport.IsEnabled = _html.Length > 0;
        }
        catch (Exception ex)
        {
            LblCount.Text = $"Error reading item: {ex.Message}";
        }
    }

    /// <summary>
    /// Writes the report to a temporary file and hands it to the system browser, which renders
    /// the MathJax the report is written against.
    /// </summary>
    private async void OpenInBrowser()
    {
        if (_html.Length == 0) return;

        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"dwsim_inspector_{Guid.NewGuid():N}.html");
            await File.WriteAllTextAsync(path, _html);

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LblCount.Text = $"Could not open the report: {ex.Message}";
        }
    }
}
