using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Shows a picture in its own window: fitted to the window, at its actual size or zoomed, with
/// Ctrl + mouse wheel and the buttons. When the picture is larger than the window, dragging moves it.
/// Double-click switches between fitted and actual size. Avalonia counterpart of FormImageViewer.
/// </summary>
public sealed class ImageViewerWindow : Window
{
    private static readonly double[] Steps = { 0.1, 0.25, 0.33, 0.5, 0.67, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 };

    private readonly byte[] _png;
    private readonly Bitmap _bitmap;
    private readonly Image _image;
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _lblZoom = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };

    private bool _fit = true;
    private double _zoom = 1.0;
    private Point? _dragFrom;

    /// <summary>Parameterless constructor required by the Avalonia previewer.</summary>
    public ImageViewerWindow() : this(Array.Empty<byte>(), "Image") { }

    /// <summary>Opens the viewer on the PNG bytes <paramref name="png"/>.</summary>
    public ImageViewerWindow(byte[] png, string title)
    {
        _png = png;
        Title = string.IsNullOrWhiteSpace(title) ? "Image" : title;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        using (var ms = new MemoryStream(png)) _bitmap = png.Length > 0 ? new Bitmap(ms) : null!;

        _image = new Image
        {
            Source = _bitmap,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.HighQuality);

        _scroll = new ScrollViewer
        {
            Content = _image,
            Background = Brushes.White,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        var btnFit = new Button { Content = "Fit to Window" };
        var btnActual = new Button { Content = "Actual Size" };
        var btnIn = new Button { Content = "Zoom In" };
        var btnOut = new Button { Content = "Zoom Out" };
        var btnSave = new Button { Content = "Save Image..." };
        btnFit.Click += (_, _) => ShowFitted();
        btnActual.Click += (_, _) => ShowZoomed(1.0);
        btnIn.Click += (_, _) => ZoomBy(1);
        btnOut.Click += (_, _) => ZoomBy(-1);
        btnSave.Click += async (_, _) => await SaveAsync();

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8),
            Children = { btnFit, btnActual, btnIn, btnOut, btnSave, _lblZoom },
        };
        DockPanel.SetDock(toolbar, global::Avalonia.Controls.Dock.Top);
        Content = new DockPanel { Children = { toolbar, _scroll } };

        _scroll.SizeChanged += (_, _) => { if (_fit) ShowFitted(); };
        // the scroll viewer takes the wheel for itself, so the zoom listens on the way down
        _scroll.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        _image.PointerPressed += OnPointerPressed;
        _image.PointerMoved += OnPointerMoved;
        _image.PointerReleased += (_, _) => _dragFrom = null;
        _image.DoubleTapped += (_, _) => { if (_fit) ShowZoomed(1.0); else ShowFitted(); };
        KeyDown += OnKeyDown;
        Closed += (_, _) => { _image.Source = null; _bitmap?.Dispose(); };
    }

    private Size ImageSize => _bitmap?.Size ?? new Size(1, 1);

    private void ShowFitted()
    {
        _fit = true;
        var view = _scroll.Bounds.Size;
        if (view.Width <= 0 || view.Height <= 0) return;
        // a picture smaller than the window is shown at its own size, so it isn't blurred by stretching
        var scale = Math.Min(1.0, Math.Min(view.Width / ImageSize.Width, view.Height / ImageSize.Height));
        _zoom = scale;
        _scroll.HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        _scroll.VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        SetImageSize(scale);
        _image.Cursor = Cursor.Default;
        _lblZoom.Text = $"{scale * 100:0} % (fitted)";
    }

    private void ShowZoomed(double zoom)
    {
        _fit = false;
        _zoom = Math.Max(Steps[0], Math.Min(Steps[^1], zoom));
        _scroll.HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        _scroll.VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        SetImageSize(_zoom);
        _image.Cursor = new Cursor(StandardCursorType.SizeAll);
        _lblZoom.Text = $"{_zoom * 100:0} %";
    }

    private void SetImageSize(double scale)
    {
        _image.Width = Math.Max(1, ImageSize.Width * scale);
        _image.Height = Math.Max(1, ImageSize.Height * scale);
    }

    private void ZoomBy(int direction)
    {
        var current = _zoom;
        var next = direction > 0
            ? Steps.FirstOrDefault(s => s > current + 0.001, Steps[^1])
            : Steps.LastOrDefault(s => s < current - 0.001, Steps[0]);
        ShowZoomed(next);
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0) return;
        ZoomBy(Math.Sign(e.Delta.Y));
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_fit || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragFrom = e.GetPosition(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragFrom is not { } from) return;
        var cur = e.GetPosition(this);
        _dragFrom = cur;
        _scroll.Offset = new Vector(_scroll.Offset.X - (cur.X - from.X), _scroll.Offset.Y - (cur.Y - from.Y));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.Add or Key.OemPlus: ZoomBy(1); break;
            case Key.Subtract or Key.OemMinus: ZoomBy(-1); break;
            default: return;
        }
        e.Handled = true;
    }

    private async Task SaveAsync()
    {
        var name = string.Join("_", Title!.Split(Path.GetInvalidFileNameChars()));
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Image",
            SuggestedFileName = name + ".png",
            DefaultExtension = "png",
            FileTypeChoices = new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } },
        });
        if (file == null) return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(_png);
    }
}
