using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Keeps every window inside the screen it opens on. The sizes the windows ask for suit a large
/// display at 100%; on a smaller one, or at a high scaling factor, a window can come up taller
/// than the desktop, and then its title bar sits above the top edge where it cannot be grabbed.
/// </summary>
internal static class WindowFit
{

    /// <summary>Margin left around the window, in device independent pixels.</summary>
    private const double Margin = 24;

    /// <summary>
    /// Applies the fit to every window of the application as it becomes visible. Opening is a
    /// plain event and cannot be handled for the whole class, so the visibility of the window is
    /// what is watched; the fit itself waits for the layout, which is when there is a size and a
    /// position to correct.
    /// </summary>
    public static void Install()
    {
        Window.IsVisibleProperty.Changed.AddClassHandler<Window>((window, args) =>
        {
            if (args.NewValue is not bool visible || !visible) return;

            Dispatcher.UIThread.Post(() => Apply(window), DispatcherPriority.Loaded);
        });
    }

    public static void Apply(Window window)
    {
        try
        {
            if (window.WindowState != WindowState.Normal) return;

            var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
            if (screen == null) return;

            var scale = screen.Scaling <= 0 ? 1.0 : screen.Scaling;
            var area = screen.WorkingArea;

            // the working area is in physical pixels, the window size is not
            var maxWidth = area.Width / scale - Margin;
            var maxHeight = area.Height / scale - Margin;

            var width = window.Bounds.Width > 0 ? window.Bounds.Width : window.Width;
            var height = window.Bounds.Height > 0 ? window.Bounds.Height : window.Height;

            if (double.IsNaN(width) || width <= 0) width = maxWidth;
            if (double.IsNaN(height) || height <= 0) height = maxHeight;

            var fitted = false;

            if (width > maxWidth) { window.Width = maxWidth; width = maxWidth; fitted = true; }
            if (height > maxHeight) { window.Height = maxHeight; height = maxHeight; fitted = true; }

            // and then it has to sit somewhere the title bar can be reached
            var physicalWidth = (int)Math.Round(width * scale);
            var physicalHeight = (int)Math.Round(height * scale);

            var position = window.Position;

            // a window that had to be shrunk is centred again, the others only move if they stick out
            var wanted = fitted
                ? new PixelPoint(area.X + (area.Width - physicalWidth) / 2,
                                 area.Y + (area.Height - physicalHeight) / 2)
                : position;

            var x = Math.Max(area.X, Math.Min(wanted.X, area.X + area.Width - physicalWidth));
            var y = Math.Max(area.Y, Math.Min(wanted.Y, area.Y + area.Height - physicalHeight));

            if (x != position.X || y != position.Y)
                window.Position = new PixelPoint(x, y);
        }
        catch (Exception)
        {
            // a window that cannot be measured is left where it is
        }
    }

}
