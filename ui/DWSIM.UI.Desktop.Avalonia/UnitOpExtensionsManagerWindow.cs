using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.UnitOperations.UnitOperations;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Attaches and detaches IUnitOperationExtension instances on each unit operation of the
/// flowsheet. Avalonia counterpart of the Eto UnitOperationExtensionManager.
/// </summary>
public sealed class UnitOpExtensionsManagerWindow : Window
{
    private sealed class UnitOpRow
    {
        public string Tag { get; init; } = "";
        public string TypeName { get; init; } = "";
        public UnitOpBaseClass UnitOp { get; init; } = null!;
    }

    private sealed class ExtRow
    {
        public bool Attached { get; set; }
        public string Name { get; init; } = "";
        public IUnitOperationExtension Extension { get; init; } = null!;
    }

    private readonly IFlowsheet _flowsheet;

    private readonly DataGrid _gridUnitOps = new() { CanUserSortColumns = false, IsReadOnly = true };
    private readonly StackPanel _extPanel = new() { Spacing = 2 };
    private readonly ObservableCollection<UnitOpRow> _unitOps = new();
    private readonly List<ExtRow> _extensions = new();

    private readonly TextBlock _lblName = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _lblDesc = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _lblAuthor = new() { FontSize = 11, Opacity = 0.85 };
    private readonly TextBlock _lblWebsite = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85 };

    public UnitOpExtensionsManagerWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;

        Title = "Unit Operation Extensions Manager";
        Width = 900;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        Content = BuildContent();
        PopulateUnitOperations();
    }

    private Control BuildContent()
    {
        _gridUnitOps.AutoGenerateColumns = false;
        _gridUnitOps.Columns.Add(new DataGridTextColumn
        {
            Header = "Tag",
            Binding = new global::Avalonia.Data.Binding("Tag"),
            Width = new DataGridLength(150)
        });
        _gridUnitOps.Columns.Add(new DataGridTextColumn
        {
            Header = "Type",
            Binding = new global::Avalonia.Data.Binding("TypeName"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        _gridUnitOps.ItemsSource = _unitOps;
        _gridUnitOps.SelectionChanged += (_, _) => OnUnitOpSelected();

        var left = new DockPanel { Width = 340, Margin = new Thickness(8) };
        var leftHeader = new TextBlock { Text = "Unit Operations", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(leftHeader, global::Avalonia.Controls.Dock.Top);
        left.Children.Add(leftHeader);
        left.Children.Add(_gridUnitOps);

        var details = new StackPanel { Spacing = 3, Margin = new Thickness(0, 8, 0, 0) };
        details.Children.Add(new TextBlock { Text = "Selected Extension", FontWeight = FontWeight.SemiBold });
        details.Children.Add(_lblName);
        details.Children.Add(_lblDesc);
        details.Children.Add(_lblAuthor);
        details.Children.Add(_lblWebsite);

        var right = new DockPanel { Margin = new Thickness(0, 8, 8, 8) };
        var rightHeader = new TextBlock { Text = "Available Extensions", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(rightHeader, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(details, global::Avalonia.Controls.Dock.Bottom);
        right.Children.Add(rightHeader);
        right.Children.Add(details);
        right.Children.Add(new ScrollViewer { Content = _extPanel });

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        body.Children.Add(left);
        body.Children.Add(right);

        var btnClose = new Button { Content = "Close", Width = 80, IsCancel = true };
        btnClose.Classes.Add("dialog");
        btnClose.Click += (_, _) => Close();

        var bottom = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8)
        };
        bottom.Children.Add(_status);
        bottom.Children.Add(btnClose);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(body);
        return root;
    }

    private void PopulateUnitOperations()
    {
        foreach (var obj in _flowsheet.SimulationObjects.Values
                     .OfType<UnitOpBaseClass>()
                     .Where(x => x.GraphicObject != null)
                     .OrderBy(x => x.GraphicObject.Tag))
        {
            _unitOps.Add(new UnitOpRow
            {
                Tag = obj.GraphicObject.Tag,
                TypeName = obj.GetDisplayName(),
                UnitOp = obj
            });
        }

        if (DWSIM.FlowsheetBase.FlowsheetBase.AvailableUnitOperationExtensions.Count == 0)
            _status.Text = "No unit operation extensions are installed.";
        else if (_unitOps.Count == 0)
            _status.Text = "This flowsheet has no unit operations.";
        else
            _gridUnitOps.SelectedIndex = 0;
    }

    private void OnUnitOpSelected()
    {
        _extPanel.Children.Clear();
        _extensions.Clear();
        ShowExtensionDetails(null);

        if (_gridUnitOps.SelectedItem is not UnitOpRow row) return;
        var uo = row.UnitOp;

        foreach (var kvp in DWSIM.FlowsheetBase.FlowsheetBase.AvailableUnitOperationExtensions)
        {
            var attached = uo.AttachedExtensions != null &&
                           uo.AttachedExtensions.Any(x => x.Name == kvp.Key);

            var er = new ExtRow { Attached = attached, Name = kvp.Key, Extension = kvp.Value };
            _extensions.Add(er);

            var cb = new CheckBox { Content = kvp.Key, IsChecked = attached };
            cb.IsCheckedChanged += (_, _) =>
            {
                er.Attached = cb.IsChecked.GetValueOrDefault();
                ApplyExtensions(uo);
            };
            // Selecting the row shows the metadata without toggling the checkbox.
            cb.GotFocus += (_, _) => ShowExtensionDetails(er);
            cb.PointerEntered += (_, _) => ShowExtensionDetails(er);

            _extPanel.Children.Add(cb);
        }

        if (_extensions.Count == 0)
            _extPanel.Children.Add(new TextBlock
            {
                Text = "No unit operation extensions are installed.", FontSize = 11, Opacity = 0.8
            });
        else
            ShowExtensionDetails(_extensions[0]);
    }

    private void ShowExtensionDetails(ExtRow? er)
    {
        _lblName.Text = er?.Extension.Name ?? "-";
        _lblDesc.Text = er?.Extension.Description ?? "";
        _lblAuthor.Text = er == null ? "" : "Author: " + (er.Extension.Author ?? "-");
        _lblWebsite.Text = er == null ? "" : "Website: " + (er.Extension.Website ?? "-");
    }

    /// <summary>
    /// Rebuilds AttachedExtensions from the check state, keeping the instances that are already
    /// attached so their state survives a toggle of some other extension.
    /// </summary>
    private void ApplyExtensions(UnitOpBaseClass uo)
    {
        uo.AttachedExtensions ??= new List<IUnitOperationExtension>();

        var updated = new List<IUnitOperationExtension>();
        foreach (var er in _extensions.Where(x => x.Attached))
        {
            var existing = uo.AttachedExtensions.FirstOrDefault(x => x.Name == er.Name);
            updated.Add(existing ?? er.Extension.NewInstance());
        }
        uo.AttachedExtensions = updated;

        _status.Text = $"{updated.Count} extension(s) attached to {uo.GraphicObject.Tag}.";
    }
}
