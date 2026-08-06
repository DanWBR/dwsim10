using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia.Common;

/// <summary>
/// Avalonia equivalents of the Eto.Forms form-creation helpers in
/// DWSIM.ExtensionMethods.Eto/EtoExtensions.cs (class Common).
/// </summary>
public static class Common
{
    public static AvaloniaEditorPanel GetDefaultContainer()
    {
        return new AvaloniaEditorPanel();
    }

    public static Window GetDefaultEditorForm(string title, int width, int height,
        AvaloniaEditorPanel content)
    {
        var sv = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var win = new Window
        {
            Title = title,
            Width = width,
            Height = height + 10,
            Content = sv,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            Icon = IconHelper.GetWindowIcon()
        };
        return win;
    }

    public static Window GetDefaultEditorForm(string title, int width, int height,
        Control content, bool scrollable = true)
    {
        object windowContent = scrollable
            ? new ScrollViewer
            {
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
            : content;

        var win = new Window
        {
            Title = title,
            Width = width,
            Height = height + 10,
            Content = windowContent,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            Icon = IconHelper.GetWindowIcon()
        };
        return win;
    }

    public static Window GetDefaultTabbedForm(string title, int width, int height,
        Control[] contents)
    {
        var tabCtrl = new TabControl();

        foreach (var content in contents)
        {
            var tabTitle = content.Tag as string ?? string.Empty;
            var sv = new ScrollViewer
            {
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            tabCtrl.Items.Add(new TabItem { Header = tabTitle, Content = sv });
        }

        var win = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            Content = tabCtrl,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            Icon = IconHelper.GetWindowIcon()
        };
        return win;
    }

    public static Window CreateDialog(Control content, string title,
        int width = 0, int height = 0)
    {
        var w = new Window
        {
            Title = title,
            Content = content,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon()
        };
        if (width > 0) w.Width = width;
        if (height > 0) w.Height = height;
        return w;
    }

    public static Window CreateDialogWithButtons(Control content, string title,
        Action okClicked, int width = 0, int height = 0)
    {
        var okBtn = new Button
        {
            Content = "OK",
            Width = 80
        };
        okBtn.Classes.Add("dialog");

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        btnPanel.Children.Add(okBtn);

        var root = new DockPanel();
        DockPanel.SetDock(btnPanel, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(btnPanel);
        root.Children.Add(content);

        var w = new Window
        {
            Title = title,
            Content = root,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon()
        };
        if (width > 0) w.Width = width;
        if (height > 0) w.Height = height;

        okBtn.Click += (_, _) => { okClicked(); w.Close(); };

        return w;
    }
}
