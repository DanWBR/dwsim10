using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Avalonia port of the Eto PropertySelector dialog.
/// Shows three columns: objects, properties, units.
/// Used by the spreadsheet context menu for Import/Export Data.
/// </summary>
public sealed class PropertySelectorDialog : Window
{
    private readonly ListBox _objectList;
    private readonly ListBox _propertyList;
    private readonly ListBox _unitList;

    private readonly IFlowsheet _flowsheet;
    private readonly Dictionary<string, ISimulationObject> _objDict;

    /// <summary>Selected object ID (key in SimulationObjects).</summary>
    public string? SelectedObjectId { get; private set; }

    /// <summary>Selected property key.</summary>
    public string? SelectedPropertyKey { get; private set; }

    /// <summary>Selected unit string (may be empty).</summary>
    public string? SelectedUnit { get; private set; }

    /// <summary>True if user clicked OK.</summary>
    public bool Confirmed { get; private set; }

    public PropertySelectorDialog(IFlowsheet flowsheet, Dictionary<string, ISimulationObject>? objList = null)
    {
        _flowsheet = flowsheet;
        _objDict = objList ?? flowsheet.SimulationObjects
            .Where(kv => kv.Value.GraphicObject != null)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        Title = "Select Property";
        Width = 700;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = true;
        IconHelper.ApplyWindowIcon(this);

        _objectList = new ListBox { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        _propertyList = new ListBox { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };
        _unitList = new ListBox { FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11) };

        // Populate objects
        foreach (var kv in _objDict.OrderBy(kv => kv.Value.GraphicObject?.Tag ?? kv.Key))
        {
            _objectList.Items.Add(new ListBoxItem
            {
                Content = kv.Value.GraphicObject?.Tag ?? kv.Key,
                Tag = kv.Key
            });
        }

        _objectList.SelectionChanged += OnObjectSelected;
        _propertyList.SelectionChanged += OnPropertySelected;

        // Layout: three columns with labels
        var col1 = new DockPanel { Margin = new Thickness(4) };
        var lbl1 = new TextBlock { Text = "Object", FontWeight = FontWeight.SemiBold, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Margin = new Thickness(2) };
        DockPanel.SetDock(lbl1, global::Avalonia.Controls.Dock.Top);
        col1.Children.Add(lbl1);
        col1.Children.Add(_objectList);

        var col2 = new DockPanel { Margin = new Thickness(4) };
        var lbl2 = new TextBlock { Text = "Property", FontWeight = FontWeight.SemiBold, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Margin = new Thickness(2) };
        DockPanel.SetDock(lbl2, global::Avalonia.Controls.Dock.Top);
        col2.Children.Add(lbl2);
        col2.Children.Add(_propertyList);

        var col3 = new DockPanel { Margin = new Thickness(4) };
        var lbl3 = new TextBlock { Text = "Units", FontWeight = FontWeight.SemiBold, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11), Margin = new Thickness(2) };
        DockPanel.SetDock(lbl3, global::Avalonia.Controls.Dock.Top);
        col3.Children.Add(lbl3);
        col3.Children.Add(_unitList);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,Auto"),
            Margin = new Thickness(4)
        };
        Grid.SetColumn(col1, 0);
        Grid.SetColumn(col2, 1);
        Grid.SetColumn(col3, 2);
        grid.Children.Add(col1);
        grid.Children.Add(col2);
        grid.Children.Add(col3);

        // Buttons
        var btnCancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        btnCancel.Classes.Add("dialog");
        var btnOk = new Button { Content = "OK", Width = 80, IsDefault = true };
        btnOk.Classes.Add("dialog");

        btnOk.Click += (_, _) =>
        {
            Confirmed = true;
            Close();
        };
        btnCancel.Click += (_, _) =>
        {
            Confirmed = false;
            Close();
        };

        var buttonPanel = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(4)
        };
        buttonPanel.Children.Add(btnCancel);
        buttonPanel.Children.Add(btnOk);

        var mainPanel = new DockPanel();
        DockPanel.SetDock(buttonPanel, global::Avalonia.Controls.Dock.Bottom);
        mainPanel.Children.Add(buttonPanel);
        mainPanel.Children.Add(grid);

        Content = mainPanel;
    }

    private void OnObjectSelected(object? sender, SelectionChangedEventArgs e)
    {
        _propertyList.Items.Clear();
        _unitList.Items.Clear();

        if (_objectList.SelectedItem is not ListBoxItem item) return;
        var key = item.Tag as string;
        if (key == null || !_objDict.ContainsKey(key)) return;

        SelectedObjectId = key;
        var obj = _objDict[key];
        var props = obj.GetProperties(Interfaces.Enums.PropertyType.ALL);
        foreach (var prop in props)
        {
            _propertyList.Items.Add(new ListBoxItem
            {
                Content = _flowsheet.GetTranslatedString(prop),
                Tag = prop
            });
        }
    }

    private void OnPropertySelected(object? sender, SelectionChangedEventArgs e)
    {
        _unitList.Items.Clear();
        SelectedUnit = "";

        if (_objectList.SelectedItem is not ListBoxItem objItem) return;
        if (_propertyList.SelectedItem is not ListBoxItem propItem) return;

        var objKey = objItem.Tag as string;
        var propKey = propItem.Tag as string;
        if (objKey == null || propKey == null) return;
        if (!_objDict.ContainsKey(objKey)) return;

        SelectedPropertyKey = propKey;
        var obj = _objDict[objKey];

        try
        {
            var unit = obj.GetPropertyUnit(propKey);
            if (!string.IsNullOrEmpty(unit))
            {
                _unitList.Items.Add(new ListBoxItem { Content = unit, Tag = unit });
                _unitList.SelectedIndex = 0;
                SelectedUnit = unit;
            }
        }
        catch { }

        _unitList.SelectionChanged += (_, _) =>
        {
            if (_unitList.SelectedItem is ListBoxItem ui)
                SelectedUnit = ui.Tag as string ?? "";
        };
    }
}
