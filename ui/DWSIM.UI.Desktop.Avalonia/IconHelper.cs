using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Centralised icon helpers for menus, context menus, and window title bars.
/// Uses Unicode emoji so no external image assets are required beyond the
/// window .ico file.
/// </summary>
internal static class IconHelper
{
    // Cached WindowIcon instance (loaded once, shared by all windows)
    private static WindowIcon? _windowIcon;

    /// <summary>Get the shared DWSIM window icon.</summary>
    public static WindowIcon? GetWindowIcon()
    {
        if (_windowIcon != null) return _windowIcon;
        try
        {
            // Try loading from embedded Avalonia resource
            var assets = global::Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://DWSIM.UI.Desktop.Avalonia/Assets/dwsim.ico"));
            _windowIcon = new WindowIcon(assets);
        }
        catch
        {
            try
            {
                // Fallback: load from file next to the exe
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
                var path = Path.Combine(dir, "Assets", "dwsim.ico");
                if (File.Exists(path))
                    _windowIcon = new WindowIcon(path);
            }
            catch { /* no icon */ }
        }
        return _windowIcon;
    }

    /// <summary>Apply the DWSIM icon to a window (safe if asset missing).</summary>
    public static void ApplyWindowIcon(Window window)
    {
        var icon = GetWindowIcon();
        if (icon != null) window.Icon = icon;
    }

    /// <summary>Emoji icon size in pixels, multiplied by the UI scaling factor at startup
    /// (see App.ApplyUIScaling). Base value matches the font resources in App.axaml.</summary>
    public static double IconFontSize = 14.0;

    /// <summary>Create a small TextBlock suitable for MenuItem.Icon.</summary>
    public static TextBlock MIcon(string emoji)
    {
        return new TextBlock
        {
            Text = emoji,
            FontSize = IconFontSize,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center
        };
    }

    /// <summary>Set the Icon of a MenuItem to an emoji TextBlock.</summary>
    public static void Set(MenuItem item, string emoji)
    {
        item.Icon = MIcon(emoji);
    }
}
