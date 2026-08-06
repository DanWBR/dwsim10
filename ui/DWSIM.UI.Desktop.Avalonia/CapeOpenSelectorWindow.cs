using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using COInfo = DWSIM.UnitOperations.UnitOperations.Auxiliary.CapeOpen.CapeOpenUnitOpInfo;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Picks a CAPE-OPEN unit operation from the ones registered on this machine.
///
/// CAPE-OPEN is COM, so this is Windows-only by construction: the registry scan comes from
/// CapeOpenUO.SearchRegisteredUnitOperations, which touches nothing but the registry.
/// On other platforms the window says so and lists nothing.
/// </summary>
public sealed class CapeOpenSelectorWindow : Window
{
    private readonly ListBox _list = new();
    private readonly TextBlock _name = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _version = new() { FontSize = 11, Opacity = 0.85 };
    private readonly TextBlock _capeVersion = new() { FontSize = 11, Opacity = 0.85 };
    private readonly TextBlock _vendor = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _description = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80 };
    private readonly TextBox _about = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 100 };
    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85, VerticalAlignment = VerticalAlignment.Center };

    private readonly Button _ok = new() { Content = "OK", Width = 90, IsEnabled = false };
    private readonly Button _cancel = new() { Content = "Cancel", Width = 90, IsCancel = true };

    private List<COInfo> _items = new();

    /// <summary>The unit operation the user accepted, or null when cancelled.</summary>
    public COInfo? Selected { get; private set; }

    public CapeOpenSelectorWindow()
    {
        Title = "Select a CAPE-OPEN Unit Operation";
        Width = 760;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        _ok.Classes.Add("dialog");
        _cancel.Classes.Add("dialog");

        Content = BuildContent();

        _list.SelectionChanged += (_, _) => ShowDetails();
        _ok.Click += (_, _) =>
        {
            var idx = _list.SelectedIndex;
            if (idx >= 0 && idx < _items.Count) Selected = _items[idx];
            Close();
        };
        _cancel.Click += (_, _) => { Selected = null; Close(); };

        Opened += async (_, _) => await LoadAsync();
    }

    private Control BuildContent()
    {
        var details = new StackPanel { Spacing = 4, Margin = new Thickness(8, 0, 0, 0) };
        details.Children.Add(_name);
        details.Children.Add(_version);
        details.Children.Add(_capeVersion);
        details.Children.Add(_vendor);
        details.Children.Add(new TextBlock { Text = "Description", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        details.Children.Add(_description);
        details.Children.Add(new TextBlock { Text = "About", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        details.Children.Add(_about);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("280,*"), Margin = new Thickness(8) };
        Grid.SetColumn(_list, 0);
        var scroll = new ScrollViewer { Content = details };
        Grid.SetColumn(scroll, 1);
        grid.Children.Add(_list);
        grid.Children.Add(scroll);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8)
        };
        buttons.Children.Add(_ok);
        buttons.Children.Add(_cancel);

        var bottom = new DockPanel { Margin = new Thickness(0) };
        DockPanel.SetDock(buttons, global::Avalonia.Controls.Dock.Right);
        bottom.Children.Add(buttons);
        bottom.Children.Add(new Border { Child = _status, Margin = new Thickness(12, 0, 0, 0) });

        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(grid);
        return root;
    }

    private async Task LoadAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            _status.Text = "CAPE-OPEN is a Windows COM standard and is not available on this platform.";
            return;
        }

        _status.Text = "Scanning the registry for CAPE-OPEN unit operations...";
        try
        {
            // The scan walks every CLSID subkey, so it takes a couple of seconds.
            _items = await Task.Run(() =>
                DWSIM.UnitOperations.UnitOperations.CapeOpenUO.SearchRegisteredUnitOperations(false)
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .OrderBy(x => x.Name)
                    .ToList());
        }
        catch (Exception ex)
        {
            _status.Text = "Registry scan failed: " + ex.Message;
            return;
        }

        foreach (var item in _items) _list.Items.Add(item.Name);

        if (_items.Count == 0)
        {
            _status.Text = "No CAPE-OPEN unit operations are registered on this machine.";
            return;
        }

        _status.Text = $"{_items.Count} unit operation(s) found.";
        _list.SelectedIndex = 0;
    }

    private void ShowDetails()
    {
        var idx = _list.SelectedIndex;
        _ok.IsEnabled = idx >= 0 && idx < _items.Count;
        if (!_ok.IsEnabled) return;

        var uo = _items[idx];
        _name.Text = uo.Name;
        _version.Text = "Version: " + (string.IsNullOrEmpty(uo.Version) ? "-" : uo.Version);
        _capeVersion.Text = "CAPE-OPEN version: " + (string.IsNullOrEmpty(uo.CapeVersion) ? "-" : uo.CapeVersion);
        _vendor.Text = "Vendor: " + (string.IsNullOrEmpty(uo.VendorURL) ? "-" : uo.VendorURL);
        _description.Text = uo.Description ?? "";
        _about.Text = uo.AboutInfo ?? "";
    }
}
