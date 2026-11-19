using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Tells the AI assistant what the user last worked on in this flowsheet: the panel the pointer or the
/// focus went to, the object editor and its tab, or a tool window of the flowsheet. The assistant's
/// own panel never counts, so a click in it to type a question leaves the answer on the task before.
/// </summary>
public partial class FlowsheetView
{
    private DispatcherTimer? _uiContextTimer;

    // the last panel of this view the pointer or the focus went to, and when
    private string? _uiArea;
    private WeakReference<Visual>? _uiAreaSource;
    private DateTime _uiAreaAt;

    // the last tool window of this flowsheet that became active, and when
    private WeakReference<Window>? _uiWindow;
    private DateTime _uiWindowAt;
    private Window? _uiPreviousActive;

    private Dictionary<string, object>? _uiContextSnapshot;

    private void WireUIContextTracking()
    {
        AddHandler(PointerPressedEvent, (_, e) => OnUIContextInput(e), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, (_, e) => OnUIContextInput(e), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Answers from any thread; while the UI thread is busy, the last answer is returned.</summary>
    internal Dictionary<string, object>? GetUIContext()
    {
        if (Dispatcher.UIThread.CheckAccess()) return BuildUIContext();
        try
        {
            var task = Dispatcher.UIThread.InvokeAsync(BuildUIContext).GetTask();
            if (task.Wait(1500)) return task.Result;
        }
        catch
        {
        }
        return _uiContextSnapshot;
    }

    private void OnUIContextInput(RoutedEventArgs e)
    {
        if (e.Source is not Visual source) return;
        var area = AreaOf(source);
        // the assistant's panel, a menu or a toolbar: the task before stays the answer
        if (area == null) return;
        _uiArea = area;
        _uiAreaSource = new WeakReference<Visual>(source);
        _uiAreaAt = DateTime.UtcNow;
    }

    private string? AreaOf(Visual source)
    {
        Visual? web = null;
        if (_dockFactory != null && _dockFactory.ContentById.TryGetValue("WebPanel", out var webContent)) web = webContent;

        for (Visual? v = source; v != null && !ReferenceEquals(v, this); v = v.GetVisualParent())
        {
            if (web != null && ReferenceEquals(v, web)) return null;
            if (ReferenceEquals(v, EditorHolder)) return "editor";
            if (ReferenceEquals(v, Canvas) || ReferenceEquals(v, _canvasHost)) return "flowsheet";
            if (_dockFactory == null) continue;
            foreach (var entry in _dockFactory.ContentById)
            {
                if (ReferenceEquals(v, entry.Value)) return entry.Key == "Canvas" ? "flowsheet" : entry.Key;
            }
        }
        return null;
    }

    /// <summary>
    /// Records a tool window of this flowsheet when it becomes active. The main window is left to the
    /// pointer tracking, because a click in the assistant's browser panel doesn't reach Avalonia.
    /// </summary>
    private void SampleUIContextWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime life) return;
        var active = life.Windows.FirstOrDefault(w => w.IsActive);
        if (active == null || ReferenceEquals(active, _uiPreviousActive)) return;
        _uiPreviousActive = active;

        var host = TopLevel.GetTopLevel(this) as Window;
        if (ReferenceEquals(active, host) || !BelongsToThisFlowsheet(active, host)) return;
        if (active is IUIContextSource source && SafeDescribe(source) == null) return;

        _uiWindow = new WeakReference<Window>(active);
        _uiWindowAt = DateTime.UtcNow;
    }

    private bool BelongsToThisFlowsheet(Window window, Window? host)
    {
        if (host == null) return false;
        if (ReferenceEquals(window.Owner, host)) return IsEffectivelyVisible;
        // an unowned window goes with the flowsheet shown in the main window
        return window.Owner == null && IsEffectivelyVisible;
    }

    private Dictionary<string, object> BuildUIContext()
    {
        if (_uiContextTimer == null)
        {
            _uiContextTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _uiContextTimer.Tick += (_, _) => SampleUIContextWindow();
            _uiContextTimer.Start();
            DetachedFromVisualTree += (_, _) => _uiContextTimer?.Stop();
            AttachedToVisualTree += (_, _) => _uiContextTimer?.Start();
        }
        SampleUIContextWindow();

        var ctx = new Dictionary<string, object> { ["host"] = "crossplatform" };

        var selected = new List<string>();
        if (_surface != null) selected.AddRange(_surface.SelectedObjects.Keys.Take(20));
        ctx["selected"] = selected;

        Window? window = null;
        if (_uiWindow != null && _uiWindow.TryGetTarget(out var w) && w.IsVisible && _uiWindowAt > _uiAreaAt) window = w;

        if (window != null)
        {
            ctx["area"] = "tool";
            ctx["window"] = DescribeWindow(window);
        }
        else if (_uiArea == "editor" && EditorHolder.CurrentObjectName is { } name)
        {
            ctx["area"] = "editor";
            ctx["editor"] = new Dictionary<string, object> { ["object"] = name, ["tab"] = EditorTab() };
        }
        else
        {
            ctx["area"] = _uiArea == null || _uiArea == "editor" ? "flowsheet" : _uiArea;
        }

        _uiContextSnapshot = ctx;
        return ctx;
    }

    private static Dictionary<string, object> DescribeWindow(Window window)
    {
        var info = window is IUIContextSource source ? SafeDescribe(source) : null;
        var result = info != null ? new Dictionary<string, object>(info) : new Dictionary<string, object>();
        if (!result.ContainsKey("id")) result["id"] = window.GetType().Name;
        if (!result.ContainsKey("title")) result["title"] = window.Title ?? "";
        return result;
    }

    private static Dictionary<string, object>? SafeDescribe(IUIContextSource source)
    {
        try { return source.DescribeUIContext(); }
        catch { return null; }
    }

    /// <summary>The tab the user last clicked in the editor, or the one it shows.</summary>
    private string EditorTab()
    {
        var parts = new List<string>();
        if (_uiAreaSource != null && _uiAreaSource.TryGetTarget(out var source))
        {
            var inEditor = false;
            for (Visual? v = source; v != null; v = v.GetVisualParent())
            {
                if (ReferenceEquals(v, EditorHolder)) { inEditor = true; break; }
                if (v is TabItem item && item.Header is string header) parts.Insert(0, header);
            }
            // a click in an editor that has since been replaced says nothing about the one shown now
            if (!inEditor) parts.Clear();
        }
        if (parts.Count == 0 && EditorHolder.CurrentEditor is { } editor)
        {
            // the logical tree, because the editor's panel is out of the visual tree while the dock hides it
            var tabs = editor.Content as TabControl ?? editor.GetLogicalDescendants().OfType<TabControl>().FirstOrDefault();
            if (tabs?.SelectedItem is TabItem { Header: string header }) parts.Add(header);
        }
        return string.Join(" > ", parts);
    }
}
