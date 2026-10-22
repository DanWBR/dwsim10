using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// A form an extension opens on its own, as the techno-economic and life-cycle tools or the
/// equipment catalogues do.
/// </summary>
/// <remarks>
/// These were windows. Android and iOS run Avalonia with a single view and no windowing, and
/// constructing a window there throws, so this is a control instead: on a desktop <see cref="Show"/>
/// puts it in a real window, and on a single-view host it goes to <see cref="EditorWindow.Presenter"/>
/// and lands in whatever that host draws its forms in.
///
/// It carries the window members these forms actually use — <see cref="Title"/>, <see cref="Icon"/>,
/// <see cref="CanResize"/>, <see cref="WindowStartupLocation"/>, Show with an owner, ShowDialog with a
/// result — so a form changes its base class and nothing else.
/// </remarks>
public class PlusWindow : UserControl
{
    private Window? _window;
    private IPresentedEditor? _presented;
    private object? _result;
    private string _title = "";

    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? "";
            if (_window is not null) _window.Title = _title;
        }
    }

    /// <summary>The desktop window's icon. Ignored on a single-view host.</summary>
    public WindowIcon? Icon { get; set; }

    public bool CanResize { get; set; } = true;

    public WindowStartupLocation WindowStartupLocation { get; set; } = WindowStartupLocation.CenterOwner;

    /// <summary>Raised once the form is closed, whichever way it was shown.</summary>
    public event EventHandler? Closed;

    /// <summary>
    /// Raised before a desktop window closes, with the chance to cancel (a form that asks whether to
    /// save its edits). A single-view host closes without asking.
    /// </summary>
    public event EventHandler<WindowClosingEventArgs>? Closing;

    /// <summary>
    /// Where to put the window on screen, in physical pixels; when set the startup location is
    /// manual. A faceplate opened beside the pointer uses it. Ignored on a single-view host.
    /// </summary>
    public PixelPoint? Position { get; set; }

    /// <summary>
    /// Lets the desktop window take the size of its content (a faceplate, a small dialog) instead of
    /// the Width and Height set on the form. Ignored on a single-view host.
    /// </summary>
    public SizeToContent SizeToContent { get; set; } = SizeToContent.Manual;

    /// <summary>The desktop window behind this form once shown; null before that and on a single-view host.</summary>
    public Window? NativeWindow => _window;

    public void Show() => Show(null);

    /// <summary>Shows it over an owner — a window, another form or a control — where the desktop can.</summary>
    public void Show(object? owner)
    {
        if (EditorWindow.Presenter is not null)
        {
            Present(modal: false);
            return;
        }
        var host = OwnerWindow(owner);
        if (host is null) Native().Show(); else Native().Show(host);
    }

    /// <summary>
    /// Shows it modally. The owner is whatever the caller has at hand — another form, a control, or
    /// nothing; only a desktop window can actually own a dialog.
    /// </summary>
    public Task ShowDialog(object? owner = null)
    {
        if (EditorWindow.Presenter is not null)
        {
            Present(modal: true);
            return _presented!.Closed;
        }

        var host = OwnerWindow(owner);
        return host is null ? Native().ShowDialog(Native()) : Native().ShowDialog(host);
    }

    /// <summary>Shows it modally and returns what <see cref="Close(object?)"/> was given.</summary>
    public async Task<T> ShowDialog<T>(object? owner = null)
    {
        await ShowDialog(owner);
        return _result is T value ? value : default!;
    }

    public void Activate() => _window?.Activate();

    public void Close() => Close(null);

    public void Close(object? result)
    {
        _result = result;
        if (_presented is not null) { _presented.Close(); return; }
        _window?.Close(result);
    }

    /// <summary>The window behind an owner, whatever kind of thing the caller passed.</summary>
    private static Window? OwnerWindow(object? owner) => owner switch
    {
        Window w => w,
        PlusWindow pw => pw._window,
        Control c => TopLevel.GetTopLevel(c) as Window,
        _ => null,
    };

    private void Present(bool modal)
    {
        // The size is a preference here, not a frame: the host fits the form to the screen.
        var width = double.IsNaN(Width) ? 800 : Width;
        var height = double.IsNaN(Height) ? 600 : Height;
        Width = double.NaN;
        Height = double.NaN;
        ClearValue(MinWidthProperty);
        ClearValue(MinHeightProperty);

        _presented = EditorWindow.Presenter!(Title, (int)width, (int)height, this, modal);
        _presented.Closed.ContinueWith(_ => Closed?.Invoke(this, EventArgs.Empty),
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private Window Native()
    {
        if (_window is not null) return _window;

        // The form sized itself; hand that to the window and let the control fill it.
        var width = Width;
        var height = Height;
        var minWidth = MinWidth;
        var minHeight = MinHeight;
        Width = double.NaN;
        Height = double.NaN;
        ClearValue(MinWidthProperty);
        ClearValue(MinHeightProperty);

        _window = new Window
        {
            Title = Title,
            MinWidth = minWidth,
            MinHeight = minHeight,
            Content = this,
            CanResize = CanResize,
            WindowStartupLocation = WindowStartupLocation,
        };
        if (!double.IsNaN(width)) _window.Width = width;
        if (!double.IsNaN(height)) _window.Height = height;
        _window.SizeToContent = SizeToContent;
        if (Icon is not null) _window.Icon = Icon;
        if (Position is PixelPoint p)
        {
            _window.WindowStartupLocation = WindowStartupLocation.Manual;
            _window.Position = p;
        }
        _window.Closing += (s, e) => Closing?.Invoke(this, e);
        _window.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
        return _window;
    }
}
