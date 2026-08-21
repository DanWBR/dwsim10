using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// Avalonia flowsheet rendering canvas.
/// Mirrors the rendering approach of FlowsheetSurface_WPF:
///   renders into a WriteableBitmap via SkiaSharp, then blits to Avalonia DrawingContext.
///
/// The bitmap is created at device-pixel resolution (logical size * DPI scale)
/// so the flowsheet is crisp on high-DPI displays. Input coordinates are also
/// passed in device pixels, matching the WPF CanvasControl behavior.
///
/// Engine callbacks:
///   PaintCallback(SKSurface, SKImageInfo)  -- render the flowsheet (fsurface.UpdateSurface)
///   InputPressCallback(x, y)               -- pointer down  (device pixels)
///   InputReleaseCallback()                 -- pointer up
///   InputMoveCallback(x, y)               -- pointer move   (device pixels)
///   WheelCallback(deltaY, x, y, w, h)     -- scroll / zoom  (device pixels)
///
/// No compile-time dependency on the .NET-Framework engine assemblies.
/// Wire callbacks once those projects are ported to .NET 8.
/// </summary>
public class FlowsheetCanvas : Control
{
    private WriteableBitmap? _bitmap;
    private MouseButton _lastPressButton;

    // -------------------------------------------------------------------------
    // Engine callbacks
    // -------------------------------------------------------------------------

    public Action<SKSurface, SKImageInfo>? PaintCallback { get; set; }
    public Action<int, int>? InputPressCallback { get; set; }
    public Action? InputReleaseCallback { get; set; }
    public Action<int, int>? InputMoveCallback { get; set; }
    public Action<double, int, int, int, int>? WheelCallback { get; set; }

    public event EventHandler? InputReleased;

    /// <summary>Fired on double-click. Args: KeyModifiers at the time of the double-click.</summary>
    public event Action<KeyModifiers>? InputDoubleClick;

    /// <summary>Fired when a palette item is dropped onto the canvas. Args: (itemName, deviceX, deviceY).</summary>
    public event Action<string, int, int>? PaletteItemDropped;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public FlowsheetCanvas()
    {
        Focusable = true;
        ClipToBounds = true;

        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerMoved += OnPointerMoved;
        PointerWheelChanged += OnPointerWheelChanged;

        DoubleTapped += OnDoubleTapped;

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    // -------------------------------------------------------------------------
    // DPI scale helper
    // -------------------------------------------------------------------------

    private double GetDpiScale()
    {
        return TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
    }

    /// <summary>The render/DPI scale (RenderScaling); 2.0 at 200% display scaling.</summary>
    public double DpiScale => GetDpiScale();

    /// <summary>Canvas width in DEVICE pixels (Bounds x DpiScale) - the space the drawing surface, its
    /// input coordinates and <c>ZoomAll</c> work in. Use this, not <c>Bounds.Width</c>, for fit/centre math.</summary>
    public int DeviceWidth => (int)(Bounds.Width * GetDpiScale());

    /// <summary>Canvas height in DEVICE pixels (Bounds x DpiScale).</summary>
    public int DeviceHeight => (int)(Bounds.Height * GetDpiScale());

    // -------------------------------------------------------------------------
    // Rendering via WriteableBitmap -> SkiaSharp (at device-pixel resolution)
    // -------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        var logW = (int)Bounds.Width;
        var logH = (int)Bounds.Height;
        if (logW <= 0 || logH <= 0) return;

        var dpiScale = GetDpiScale();
        var devW = (int)(logW * dpiScale * dpiScale);
        var devH = (int)(logH * dpiScale * dpiScale);
        if (devW <= 0 || devH <= 0) return;

        // Create bitmap at device-pixel resolution for crisp rendering
        if (_bitmap == null
            || _bitmap.PixelSize.Width != devW
            || _bitmap.PixelSize.Height != devH)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(devW, devH),
                new Vector(96 * dpiScale, 96 * dpiScale),
                PixelFormats.Bgra8888,
                AlphaFormat.Premul);
        }

        using (var fb = _bitmap.Lock())
        {
            var info = new SKImageInfo(devW, devH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, fb.Address, fb.RowBytes);

            if (PaintCallback != null)
            {
                PaintCallback(surface, info);
            }
            else
            {
                DrawPlaceholder(surface.Canvas, info);
            }

            surface.Canvas.Flush();
        }

        // Blit device-pixel bitmap into logical-pixel bounds (Avalonia scales correctly)
        context.DrawImage(_bitmap, new Rect(0, 0, logW, logH));
    }

    private static void DrawPlaceholder(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.WhiteSmoke);

        using var linePaint = new SKPaint
        {
            Color = new SKColor(210, 210, 210),
            StrokeWidth = 1,
            IsAntialias = false
        };

        const int spacing = 40;
        for (int x = 0; x < info.Width; x += spacing)
            canvas.DrawLine(x, 0, x, info.Height, linePaint);
        for (int y = 0; y < info.Height; y += spacing)
            canvas.DrawLine(0, y, info.Width, y, linePaint);

        using var textPaint = new SKPaint
        {
            Color = new SKColor(180, 180, 180),
            TextSize = 18,
            IsAntialias = true
        };
        canvas.DrawText("Flowsheet Canvas", info.Width / 2f - 90, info.Height / 2f, textPaint);
    }

    // -------------------------------------------------------------------------
    // Input events
    // -------------------------------------------------------------------------

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        UpdateKeyboardState(e.KeyModifiers);
        var (px, py) = DevicePt(e.GetPosition(this));

        // Always perform hit-testing (selects the object under cursor)
        InputPressCallback?.Invoke(px, py);
        InvalidateVisual();

        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            _lastPressButton = MouseButton.Right;
            // Finalize selection so SelectedObject is set for context menu
            InputReleaseCallback?.Invoke();
            // Don't mark handled - PointerReleased needs to bubble for context menu
        }
        else
        {
            _lastPressButton = MouseButton.Left;
            e.Handled = true; // Capture pointer so we get PointerReleased
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        UpdateKeyboardState(e.KeyModifiers);

        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            // Left-click release: finalize selection and notify for editor
            InputReleaseCallback?.Invoke();
            InputReleased?.Invoke(this, EventArgs.Empty);
        }
        // Right-click: InputRelease already called in OnPointerPressed.
        // Context menu handled by FlowsheetWindow via AddHandler(..., handledEventsToo: true).

        e.Handled = true;
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        UpdateKeyboardState(e.KeyModifiers);
        var (px, py) = DevicePt(e.GetPosition(this));
        InputMoveCallback?.Invoke(px, py);
        InvalidateVisual();
    }

    /// <summary>
    /// Receives the modifier-key state (shift, ctrl, alt) on every pointer event. The host
    /// wires this to the engine-side KeyboardState, which engine code reads where it used to
    /// call My.Computer.Keyboard.ShiftKeyDown / CtrlKeyDown. Kept as a hook so this control
    /// stays free of any compile-time dependency on the engine assemblies.
    /// </summary>
    public static Action<bool, bool, bool>? KeyboardStateSink;

    private static void UpdateKeyboardState(global::Avalonia.Input.KeyModifiers mods)
    {
        KeyboardStateSink?.Invoke(
            (mods & global::Avalonia.Input.KeyModifiers.Shift) != 0,
            (mods & global::Avalonia.Input.KeyModifiers.Control) != 0,
            (mods & global::Avalonia.Input.KeyModifiers.Alt) != 0);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        InputDoubleClick?.Invoke(e.KeyModifiers);
        e.Handled = true;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var (px, py) = DevicePt(e.GetPosition(this));
        WheelCallback?.Invoke(e.Delta.Y, px, py, (int)(Bounds.Width * GetDpiScale()), (int)(Bounds.Height * GetDpiScale()));
        e.Handled = true;
        InvalidateVisual();
    }

    // -------------------------------------------------------------------------
    // Drag-and-drop handlers
    // -------------------------------------------------------------------------

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains("PaletteItem")
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("PaletteItem")) return;
        var name = e.Data.Get("PaletteItem") as string;
        if (name == null) return;
        var (px, py) = DevicePt(e.GetPosition(this));
        PaletteItemDropped?.Invoke(name, px, py);
        e.Handled = true;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Convert logical Avalonia point to device pixels (matches WPF CanvasControl).</summary>
    /// <summary>
    /// Size of the canvas in the same device pixels the input callbacks report, which is what
    /// the engine surface expects for its own Size.
    /// </summary>
    public (int Width, int Height) DeviceSize
    {
        get
        {
            var scale = GetDpiScale();
            return ((int)(Bounds.Width * scale), (int)(Bounds.Height * scale));
        }
    }

    private (int x, int y) DevicePt(Point p)
    {
        var scale = GetDpiScale();
        return ((int)(p.X * scale), (int)(p.Y * scale));
    }

    public void Refresh() => InvalidateVisual();
}
