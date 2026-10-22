using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// A tool window written the way a <see cref="Window"/> subclass is - <c>Title</c>, <c>Width</c>,
/// <c>Height</c>, <c>Content</c>, <c>Show(owner)</c>, <c>ShowDialog</c>, <c>Close()</c>, <c>Closed</c> -
/// that also opens where there are no windows.
/// </summary>
/// <remarks>
/// On a desktop the dialog is the content of a real <see cref="Window"/>, built when it is shown and
/// given the title, size and placement set here. Android and iOS run Avalonia with a single view, where
/// constructing a <see cref="Window"/> throws; there the host's <see cref="EditorWindow.Presenter"/>
/// shows the same control as a card. A subclass derives from this instead of <see cref="Window"/> and
/// keeps its code as it was.
/// </remarks>
public class EditorDialog : ContentControl
{
    private Window? _window;
    private IPresentedEditor? _presented;
    private object? _result;
    private bool _closedRaised;

    /// <summary>Every subclass draws as the ContentControl it is (Avalonia looks themes up by exact type).</summary>
    protected override Type StyleKeyOverride => typeof(ContentControl);

    private string? _title;

    public string? Title
    {
        get => _title;
        set
        {
            _title = value;
            if (_window != null) _window.Title = value ?? "";
        }
    }
    public WindowStartupLocation WindowStartupLocation { get; set; } = WindowStartupLocation.CenterOwner;
    public bool CanResize { get; set; } = true;
    public SizeToContent SizeToContent { get; set; }
    public WindowIcon? Icon { get; set; }
    public bool Topmost { get; set; }

    public event EventHandler? Opened;
    public event EventHandler? Closed;

    /// <summary>
    /// Raised before it closes. Cancelling keeps a desktop window open; a card has already gone by the
    /// time it is raised, so there it only announces the close.
    /// </summary>
    public event EventHandler<CancelEventArgs>? Closing;

    /// <summary>The desktop window it is shown in, or null on a single-view host and before it is shown.</summary>
    public Window? HostWindow => _window;

    public bool IsShown => _window != null || _presented != null;

    /// <summary>What the dialog was shown over: a window, another dialog, a control, or nothing.</summary>
    public object? Owner { get; private set; }

    /// <summary>The storage provider of whatever top level the dialog is shown in.</summary>
    public IStorageProvider StorageProvider => TopLevel.GetTopLevel(this)!.StorageProvider;

    public void Show() => Show(null);

    public void Show(object? owner)
    {
        if (IsShown) return;
        Owner = owner;
        if (EditorWindow.Presenter is not null)
        {
            Present(false);
            return;
        }
        var window = BuildWindow();
        var host = OwnerWindow(owner);
        if (host != null) window.Show(host); else window.Show();
    }

    public Task ShowDialog(object? owner = null) => ShowDialog<object?>(owner);

    public async Task<T> ShowDialog<T>(object? owner = null)
    {
        Owner = owner;
        if (EditorWindow.Presenter is not null)
        {
            Present(true);
            await _presented!.Closed;
            return _result is T value ? value : default!;
        }
        var window = BuildWindow();
        var host = OwnerWindow(owner);
        if (host == null)
        {
            // a desktop dialog needs an owner; without one it is shown modeless and awaited
            var done = new TaskCompletionSource<bool>();
            window.Closed += (_, _) => done.TrySetResult(true);
            window.Show();
            await done.Task;
            return _result is T value ? value : default!;
        }
        return await window.ShowDialog<T>(host);
    }

    public void Close() => Close(null);

    public void Close(object? result)
    {
        _result = result;
        if (_window != null) _window.Close(result);
        else _presented?.Close();
    }

    public void Activate() => _window?.Activate();

    private Window BuildWindow()
    {
        var window = new Window
        {
            Title = Title ?? "",
            WindowStartupLocation = WindowStartupLocation,
            CanResize = CanResize,
            SizeToContent = SizeToContent,
            Topmost = Topmost,
        };
        if (Icon != null) window.Icon = Icon;

        // The size was set on this control as if it were the window: it belongs to the window.
        MoveSize(WidthProperty, Window.WidthProperty, window);
        MoveSize(HeightProperty, Window.HeightProperty, window);
        MoveSize(MinWidthProperty, Window.MinWidthProperty, window);
        MoveSize(MinHeightProperty, Window.MinHeightProperty, window);

        window.Content = this;
        window.Opened += (_, _) => Opened?.Invoke(this, EventArgs.Empty);
        window.Closing += (_, e) =>
        {
            var args = new CancelEventArgs();
            Closing?.Invoke(this, args);
            e.Cancel = args.Cancel;
        };
        window.Closed += (_, _) =>
        {
            window.Content = null;
            _window = null;
            Closed?.Invoke(this, EventArgs.Empty);
        };
        _window = window;
        return window;
    }

    private void MoveSize(AvaloniaProperty from, AvaloniaProperty to, Window window)
    {
        if (!IsSet(from)) return;
        window.SetValue(to, GetValue(from));
        ClearValue(from);
    }

    private static Window? OwnerWindow(object? owner) => owner switch
    {
        Window w => w,
        EditorDialog d => d.HostWindow ?? TopLevel.GetTopLevel(d) as Window,
        Visual v => TopLevel.GetTopLevel(v) as Window,
        _ => null,
    };

    private void Present(bool modal)
    {
        // The size asked for is a preference for the card, which is capped to the screen; the content
        // fills whatever the card ends up being.
        var width = double.IsNaN(Width) ? 800 : Width;
        var height = double.IsNaN(Height) ? 600 : Height;
        ClearValue(WidthProperty);
        ClearValue(HeightProperty);
        ClearValue(MinWidthProperty);
        ClearValue(MinHeightProperty);

        _closedRaised = false;
        _presented = EditorWindow.Presenter!(Title ?? "", (int)width, (int)height, this, modal);
        _ = RaiseClosedWhenDone(_presented);
        Dispatcher.UIThread.Post(() => Opened?.Invoke(this, EventArgs.Empty));
    }

    private async Task RaiseClosedWhenDone(IPresentedEditor shown)
    {
        await shown.Closed;
        if (_closedRaised) return;
        _closedRaised = true;
        Dispatcher.UIThread.Post(() =>
        {
            _presented = null;
            Closing?.Invoke(this, new CancelEventArgs());
            Closed?.Invoke(this, EventArgs.Empty);
        });
    }
}
