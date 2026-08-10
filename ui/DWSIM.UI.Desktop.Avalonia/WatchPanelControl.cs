using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// The watch panel: a running table of chosen object properties, read after every solve, with the
/// writable ones editable in place. Avalonia counterpart of the WinForms WatchPanel, over the same
/// list the flowsheet keeps and saves.
/// </summary>
public sealed class WatchPanelControl : UserControl
{
    private sealed class WatchRow : INotifyPropertyChanged
    {
        private string _value = "";

        public string ObjId { get; init; } = "";
        public string PropId { get; init; } = "";
        public string ObjName { get; init; } = "";
        public string PropLabel { get; init; } = "";
        public bool Editable { get; init; }

        public string Value
        {
            get => _value;
            set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private IFlowsheet? _flowsheet;
    private readonly ObservableCollection<WatchRow> _rows = new();
    private readonly DataGrid _grid = new()
    {
        AutoGenerateColumns = false,
        CanUserSortColumns = false,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
    };
    private bool _refreshing;

    public WatchPanelControl()
    {
        Content = Build();
    }

    public void SetFlowsheet(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        RefreshValues();
    }

    private Control Build()
    {
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Object",
            Binding = new global::Avalonia.Data.Binding("ObjName"),
            IsReadOnly = true,
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Property",
            Binding = new global::Avalonia.Data.Binding("PropLabel"),
            IsReadOnly = true,
            Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
        });
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new global::Avalonia.Data.Binding("Value") { Mode = global::Avalonia.Data.BindingMode.TwoWay },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        _grid.ItemsSource = _rows;
        _grid.CellEditEnded += (_, e) =>
        {
            if (_refreshing || e.Column.DisplayIndex != 2) return;
            if (_grid.SelectedItem is WatchRow row) ApplyEdit(row);
        };

        var btnAdd = Btn("Add...", async () => await AddAsync());
        var btnRemove = Btn("Remove", RemoveSelected);
        var btnRefresh = Btn("Refresh", RefreshValues);

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new global::Avalonia.Thickness(4)
        };
        tools.Children.Add(btnAdd);
        tools.Children.Add(btnRemove);
        tools.Children.Add(btnRefresh);

        var root = new DockPanel();
        DockPanel.SetDock(tools, global::Avalonia.Controls.Dock.Top);
        root.Children.Add(tools);
        root.Children.Add(_grid);
        return root;
    }

    private static Button Btn(string caption, Action action)
    {
        var b = new Button { Content = caption };
        b.Classes.Add("panel");
        b.Click += (_, _) => action();
        return b;
    }

    private static Button Btn(string caption, Func<Task> action)
    {
        var b = new Button { Content = caption };
        b.Classes.Add("panel");
        b.Click += async (_, _) => await action();
        return b;
    }

    // -------------------------------------------------------------------------

    /// <summary>Reads every watched property again, dropping any whose object is gone.</summary>
    public void RefreshValues()
    {
        if (_flowsheet == null) return;

        _refreshing = true;

        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        var nf = _flowsheet.FlowsheetOptions.NumberFormat;

        // clear out watches whose object no longer exists, keeping the saved list honest
        _flowsheet.WatchItems.RemoveAll(w => !_flowsheet.SimulationObjects.ContainsKey(w.ObjID));

        _rows.Clear();
        foreach (var w in _flowsheet.WatchItems)
        {
            var obj = _flowsheet.SimulationObjects[w.ObjID];
            _rows.Add(new WatchRow
            {
                ObjId = w.ObjID,
                PropId = w.PropID,
                ObjName = obj.GraphicObject?.Tag ?? obj.Name,
                PropLabel = _flowsheet.GetTranslatedString(w.PropID) + " (" + obj.GetPropertyUnit(w.PropID, su) + ")",
                Editable = !w.IsReadOnly,
                Value = Format(obj.GetPropertyValue(w.PropID, su), nf)
            });
        }

        _refreshing = false;
    }

    private static string Format(object? value, string nf)
    {
        if (value == null) return "";
        if (value is double d) return d.ToString(nf, CultureInfo.InvariantCulture);
        if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var dd))
            return dd.ToString(nf, CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    private void ApplyEdit(WatchRow row)
    {
        if (_flowsheet == null) return;

        if (!row.Editable || !_flowsheet.SimulationObjects.ContainsKey(row.ObjId))
        {
            RefreshValues();
            return;
        }

        if (!double.TryParse(row.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
        {
            RefreshValues();
            return;
        }

        var su = _flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _flowsheet.SimulationObjects[row.ObjId].SetPropertyValue(row.PropId, v, su);
        _flowsheet.RequestCalculationAndWait();
        RefreshValues();
    }

    private void RemoveSelected()
    {
        if (_flowsheet == null || _grid.SelectedItem is not WatchRow row) return;

        var w = _flowsheet.WatchItems.FirstOrDefault(x => x.ObjID == row.ObjId && x.PropID == row.PropId);
        if (w != null) _flowsheet.WatchItems.Remove(w);
        RefreshValues();
    }

    private async Task AddAsync()
    {
        if (_flowsheet == null) return;

        var owner = this.FindAncestorOfType<Window>();
        var dlg = new WatchAddDialog(_flowsheet);
        var ok = owner != null ? await dlg.ShowDialog<bool>(owner) : false;
        if (!ok || dlg.PickedObjId == null || dlg.PickedPropId == null) return;

        // one watch per object/property pair
        if (_flowsheet.WatchItems.Any(x => x.ObjID == dlg.PickedObjId && x.PropID == dlg.PickedPropId)) return;

        _flowsheet.WatchItems.Add(new DWSIM.SharedClasses.Extras.WatchItem(dlg.PickedObjId, dlg.PickedPropId, !dlg.PickedEditable));
        RefreshValues();
    }

    // -------------------------------------------------------------------------

    /// <summary>Picks an object and one of its properties for a new watch.</summary>
    private sealed class WatchAddDialog : Window
    {
        private readonly IFlowsheet _flowsheet;
        private readonly ComboBox _objBox = new() { MinWidth = 260 };
        private readonly ComboBox _propBox = new() { MinWidth = 260 };
        private List<string> _objIds = new();
        private List<string> _propIds = new();

        public string? PickedObjId { get; private set; }
        public string? PickedPropId { get; private set; }
        public bool PickedEditable { get; private set; }

        public WatchAddDialog(IFlowsheet flowsheet)
        {
            _flowsheet = flowsheet;

            Title = "Add Watch";
            Width = 420;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            IconHelper.ApplyWindowIcon(this);

            Content = Build();
            FillObjects();
        }

        private Control Build()
        {
            _objBox.SelectionChanged += (_, _) => FillProperties();

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                Margin = new global::Avalonia.Thickness(12)
            };
            void Row(int r, string label, Control c)
            {
                var t = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new global::Avalonia.Thickness(0, 4, 8, 4) };
                Grid.SetRow(t, r); Grid.SetColumn(t, 0);
                Grid.SetRow(c, r); Grid.SetColumn(c, 1);
                grid.Children.Add(t); grid.Children.Add(c);
            }
            Row(0, "Object:", _objBox);
            Row(1, "Property:", _propBox);

            var btnOk = new Button { Content = "Add", IsDefault = true, Width = 80 };
            btnOk.Classes.Add("dialog");
            btnOk.Click += (_, _) => Commit();

            var btnCancel = new Button { Content = "Cancel", IsCancel = true, Width = 80 };
            btnCancel.Classes.Add("dialog");
            btnCancel.Click += (_, _) => Close(false);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Margin = new global::Avalonia.Thickness(12, 0, 12, 12)
            };
            buttons.Children.Add(btnOk);
            buttons.Children.Add(btnCancel);

            var root = new DockPanel();
            DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(grid);
            return root;
        }

        private void FillObjects()
        {
            var objs = _flowsheet.SimulationObjects.Values
                .Where(o => o.GraphicObject != null)
                .OrderBy(o => o.GraphicObject!.Tag)
                .ToList();

            _objIds = objs.Select(o => o.Name).ToList();
            _objBox.ItemsSource = objs.Select(o => o.GraphicObject!.Tag + "  (" + o.GetDisplayName() + ")").ToList();
            if (_objIds.Count > 0) _objBox.SelectedIndex = 0;
        }

        private void FillProperties()
        {
            _propBox.ItemsSource = null;
            _propIds = new List<string>();
            if (_objBox.SelectedIndex < 0 || _objBox.SelectedIndex >= _objIds.Count) return;

            var obj = _flowsheet.SimulationObjects[_objIds[_objBox.SelectedIndex]];
            _propIds = obj.GetProperties(PropertyType.ALL).ToList();
            _propBox.ItemsSource = _propIds.Select(p => _flowsheet.GetTranslatedString(p)).ToList();
            if (_propIds.Count > 0) _propBox.SelectedIndex = 0;
        }

        private void Commit()
        {
            if (_objBox.SelectedIndex < 0 || _objBox.SelectedIndex >= _objIds.Count) { Close(false); return; }
            if (_propBox.SelectedIndex < 0 || _propBox.SelectedIndex >= _propIds.Count) { Close(false); return; }

            PickedObjId = _objIds[_objBox.SelectedIndex];
            PickedPropId = _propIds[_propBox.SelectedIndex];

            var obj = _flowsheet.SimulationObjects[PickedObjId];
            PickedEditable = obj.GetProperties(PropertyType.WR).Contains(PickedPropId);

            Close(true);
        }
    }
}
