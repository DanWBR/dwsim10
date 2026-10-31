using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Reorders a list of names with Move Up and Move Down, as the classic FormCustomCalcOrder does.
/// <c>ShowDialog&lt;List&lt;string&gt;&gt;</c> returns the new order on OK and null on Cancel.
/// </summary>
public sealed class ItemOrderWindow : Window
{
    private readonly ObservableCollection<string> _items;
    private readonly ListBox _list;

    public ItemOrderWindow(string title, string header, IEnumerable<string> items)
    {
        Title = title;
        Width = 380;
        Height = 480;
        MinWidth = 300;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _items = new ObservableCollection<string>(items);
        _list = new ListBox { ItemsSource = _items };
        if (_items.Count > 0) _list.SelectedIndex = 0;

        var btnUp = new Button { Content = "Move Up", Width = 100 };
        var btnDown = new Button { Content = "Move Down", Width = 100 };
        btnUp.Click += (_, _) => Move(-1);
        btnDown.Click += (_, _) => Move(+1);
        var order = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        order.Children.Add(btnUp);
        order.Children.Add(btnDown);

        var btnOK = new Button { Content = "OK", Width = 90, IsDefault = true };
        var btnCancel = new Button { Content = "Cancel", Width = 90, IsCancel = true };
        btnOK.Click += (_, _) => Close(_items.ToList());
        btnCancel.Click += (_, _) => Close(null);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        actions.Children.Add(btnOK);
        actions.Children.Add(btnCancel);

        var root = new DockPanel { Margin = new Thickness(12) };
        var heading = new TextBlock { Text = header, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(heading, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(actions, global::Avalonia.Controls.Dock.Bottom);
        DockPanel.SetDock(order, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(heading);
        root.Children.Add(actions);
        root.Children.Add(order);
        root.Children.Add(_list);

        Content = root;
    }

    private void Move(int direction)
    {
        var i = _list.SelectedIndex;
        var j = i + direction;
        if (i < 0 || j < 0 || j >= _items.Count) return;
        _items.Move(i, j);
        _list.SelectedIndex = j;
    }
}
