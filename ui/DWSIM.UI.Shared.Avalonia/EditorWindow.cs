using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>An editor that is on screen, so whoever opened it can close it again.</summary>
public interface IPresentedEditor
{
    void Close();

    /// <summary>Completes when it closes, whether the caller closed it or the user did.</summary>
    Task Closed { get; }
}

/// <summary>
/// A secondary editor form, as the object editors open for a curve, a section or a set of results.
/// </summary>
/// <remarks>
/// On a desktop this is a real <see cref="Window"/>. Android and iOS run Avalonia with a single
/// view and no windowing at all — constructing a <see cref="Window"/> there throws — so a host on
/// those platforms installs <see cref="Presenter"/> and the same content is shown in its own
/// overlay instead. The window is built lazily for that reason: on a phone one is never built.
///
/// Callers only ever show, show modally, or close one of these, so both cases fit behind the same
/// three members and no editor has to know which platform it is running on.
/// </remarks>
public sealed class EditorWindow
{
    /// <summary>
    /// Set by a single-view host to take over presentation. The arguments are the title, the width
    /// and height the editor asked for, its content, and whether it was opened as a dialog.
    /// </summary>
    public static Func<string, int, int, Control, bool, IPresentedEditor>? Presenter;

    private readonly string _title;
    private readonly int _width;
    private readonly int _height;
    private readonly Control _content;
    private readonly bool _canResize;

    private Window? _window;
    private IPresentedEditor? _presented;

    internal EditorWindow(string title, int width, int height, Control content, bool canResize = true)
    {
        _title = title;
        _width = width;
        _height = height;
        _content = content;
        _canResize = canResize;
    }

    public void Show()
    {
        if (Presenter is not null)
        {
            _presented = Presenter(_title, _width, _height, _content, false);
            return;
        }
        Native().Show();
    }

    /// <summary>
    /// Shows it modally. The owner is whatever the caller has at hand - a window, another form, or
    /// nothing; only a desktop window can actually own a dialog. The task completes when it closes.
    /// </summary>
    public Task ShowDialog(object? owner = null)
    {
        if (Presenter is not null)
        {
            _presented = Presenter(_title, _width, _height, _content, true);
            return _presented.Closed;
        }

        var host = owner as Window ?? (owner is Control c ? TopLevel.GetTopLevel(c) as Window : null);
        return Native().ShowDialog(host ?? Native());
    }

    public void Close()
    {
        _presented?.Close();
        _window?.Close();
    }

    private Window Native()
    {
        return _window ??= new Window
        {
            Title = _title,
            Width = _width,
            Height = _height,
            Content = _content,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = _canResize,
        };
    }
}
