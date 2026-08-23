using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// Avalonia equivalents of DWSIM.UI.Shared.Common form-creation helpers.
/// Targets netstandard2.0 — usable from both .NET Framework 4.7.2 UO projects
/// and from the .NET 8 Avalonia project.
///
/// VB.NET UOs reference this via:
///   Imports DWSIM.UI.Shared.Avalonia
///   Dim container = AvaloniaCommon.GetDefaultContainer()
/// </summary>
public static class AvaloniaCommon
{
    public static AvaloniaEditorPanel GetDefaultContainer()
        => new AvaloniaEditorPanel();

    public static EditorWindow GetDefaultEditorForm(string title, int width, int height,
        AvaloniaEditorPanel content)
    {
        var sv = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        return new EditorWindow(title, width, height + 10, sv);
    }

    public static EditorWindow GetDefaultEditorForm(string title, int width, int height,
        Control content, bool scrollable = true)
    {
        Control windowContent = scrollable
            ? new ScrollViewer
            {
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
            : content;

        return new EditorWindow(title, width, height + 10, windowContent);
    }

    public static EditorWindow GetDefaultTabbedForm(string title, int width, int height,
        AvaloniaEditorPanel[] contents)
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

        return new EditorWindow(title, width, height, tabCtrl);
    }

    public static Window CreateDialog(Control content, string title,
        int width = 0, int height = 0)
    {
        var w = new Window
        {
            Title = title,
            Content = content,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (width > 0) w.Width = width;
        if (height > 0) w.Height = height;
        return w;
    }

    public static Window CreateDialogWithButtons(Control content, string title,
        System.Action okClicked, int width = 0, int height = 0)
    {
        var okBtn = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = 80
        };

        var root = new DockPanel();
        DockPanel.SetDock(okBtn, Dock.Bottom);
        root.Children.Add(okBtn);
        root.Children.Add(content);

        var w = new Window
        {
            Title = title,
            Content = root,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (width > 0) w.Width = width;
        if (height > 0) w.Height = height;

        okBtn.Click += (_, _) => { okClicked(); w.Close(); };

        return w;
    }
}
