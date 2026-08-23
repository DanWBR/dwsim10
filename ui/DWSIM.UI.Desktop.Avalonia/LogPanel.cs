using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using ExceptionList = DWSIM.SharedClasses.ExceptionProcessing.ExceptionList;
using ExceptionParser = DWSIM.SharedClasses.ExceptionProcessing.ExceptionParser;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// What the simulation has to say, as the Windows log does it: one row per message, with its
/// kind, the time it arrived and the text, and a button that opens the exception behind a
/// failure when there is one.
/// </summary>
public sealed class LogPanel : DockPanel
{

    /// <summary>One message.</summary>
    private sealed class LogRow
    {
        public string Icon { get; set; } = "";
        public int Index { get; set; }
        public string Date { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Message { get; set; } = "";
        public IBrush Color { get; set; } = Brushes.Gray;

        /// <summary>The exception the message came from, if the engine recorded one.</summary>
        public string ExceptionId { get; set; } = "";

        public bool HasDetails
        {
            get { return !string.IsNullOrEmpty(ExceptionId) && ExceptionList.Exceptions.ContainsKey(ExceptionId); }
        }
    }

    /// <summary>Older messages are dropped past this, so a long run cannot eat the memory.</summary>
    private const int MaxRows = 500;

    private readonly DataGrid _grid;
    private readonly ObservableCollection<LogRow> _rows = new();

    private int _count;

    public LogPanel()
    {
        // A slim vertical column of actions down the right side, beside the table, so they never take
        // a whole horizontal row (the same actions are also on the grid's right-click menu).
        var toolbar = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Vertical,
            Spacing = 4,
            Margin = new Thickness(4, 2),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top
        };

        var clearBtn = ToolButton("Clear List", (_, _) => Clear());
        var copyBtn = ToolButton("Copy Information", async (_, _) => await CopySelectedAsync());
        clearBtn.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        copyBtn.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        toolbar.Children.Add(clearBtn);
        toolbar.Children.Add(copyBtn);

        SetDock(toolbar, global::Avalonia.Controls.Dock.Right);
        Children.Add(toolbar);

        _grid = new DataGrid
        {
            ItemsSource = _rows,
            AutoGenerateColumns = false,
            CanUserSortColumns = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            SelectionMode = DataGridSelectionMode.Single,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11)
        };

        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "",
            Width = new DataGridLength(34),
            CellTemplate = new FuncDataTemplate<LogRow>((row, _) =>
            {
                if (row == null) return new TextBlock();
                return new TextBlock
                {
                    Text = row.Icon,
                    Foreground = row.Color,
                    FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(13),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }, supportsRecycling: false)
        });

        _grid.Columns.Add(Text("#", "Index", 50));
        _grid.Columns.Add(Text("Date", "Date", 150));
        _grid.Columns.Add(Text("Type", "Kind", 110));

        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Message",
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            CellTemplate = new FuncDataTemplate<LogRow>((row, _) =>
            {
                if (row == null) return new TextBlock();
                return new TextBlock
                {
                    Text = row.Message,
                    Foreground = row.Color,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap,
                    Margin = new Thickness(4, 0)
                };
            }, supportsRecycling: false)
        });

        // only the rows that carry an exception get the button, as the Windows log has it
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "",
            Width = new DataGridLength(90),
            CellTemplate = new FuncDataTemplate<LogRow>((row, _) =>
            {
                if (row == null || !row.HasDetails) return new TextBlock();

                var button = new Button
                {
                    Content = "Details",
                    FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
                    Padding = new Thickness(6, 1),
                    Margin = new Thickness(2, 1),
                    VerticalAlignment = VerticalAlignment.Center
                };
                button.Classes.Add("panel");
                button.Click += (_, _) => ShowDetails(row);
                return button;
            }, supportsRecycling: false)
        });

        var menu = new ContextMenu();

        var clear = new MenuItem { Header = "Clear List" };
        clear.Click += (_, _) => Clear();

        var copy = new MenuItem { Header = "Copy Information" };
        copy.Click += async (_, _) => await CopySelectedAsync();

        menu.Items.Add(clear);
        menu.Items.Add(copy);
        _grid.ContextMenu = menu;

        Children.Add(_grid);
    }

    private static DataGridTextColumn Text(string header, string path, double width)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path),
            IsReadOnly = true,
            Width = new DataGridLength(width)
        };
    }

    private static Button ToolButton(string text,
                                     EventHandler<global::Avalonia.Interactivity.RoutedEventArgs> handler)
    {
        var button = new Button
        {
            Content = text,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            Padding = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        button.Classes.Add("panel");
        button.Click += handler;
        return button;
    }

    // -------------------------------------------------------------------------
    // Messages
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds a message. Newest first, which is the order the Windows log sorts itself into.
    /// </summary>
    public void Add(string message, IFlowsheet.MessageType type, string exceptionId = "")
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        string icon, kind;
        IBrush color;

        switch (type)
        {
            case IFlowsheet.MessageType.Warning:
                icon = "⚠"; kind = "Warning";
                color = new SolidColorBrush(Color.FromRgb(200, 110, 0));
                break;
            case IFlowsheet.MessageType.GeneralError:
                icon = "✖"; kind = "Error";
                color = new SolidColorBrush(Color.FromRgb(200, 30, 30));
                break;
            case IFlowsheet.MessageType.Tip:
                icon = "\U0001F4A1"; kind = "Tip";
                color = new SolidColorBrush(Color.FromRgb(40, 90, 200));
                break;
            default:
                icon = "ℹ"; kind = "Message";
                color = new SolidColorBrush(Color.FromRgb(40, 90, 200));
                break;
        }

        _count++;

        // the grid holds one line per message; the rest of a multi-line text is in the details
        var first = message.Replace("\r\n", "\n").Split('\n')[0];

        _rows.Insert(0, new LogRow
        {
            Icon = icon,
            Index = _count,
            Date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
            Kind = kind,
            Message = first,
            Color = color,
            ExceptionId = exceptionId ?? ""
        });

        while (_rows.Count > MaxRows) _rows.RemoveAt(_rows.Count - 1);

        if (_grid.SelectedIndex != 0) _grid.SelectedIndex = 0;
    }

    public void Clear()
    {
        _rows.Clear();
        _count = 0;
    }

    /// <summary>The whole log as text, for the callers that write it to a file.</summary>
    public string GetText()
    {
        var sb = new StringBuilder();

        foreach (var row in _rows.Reverse())
            sb.AppendLine("[" + row.Date + "] " + row.Kind + ": " + row.Message);

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Details
    // -------------------------------------------------------------------------

    private void ShowDetails(LogRow row)
    {
        if (!ExceptionList.Exceptions.TryGetValue(row.ExceptionId, out var exception)) return;

        var parsed = ExceptionParser.ParseException(exception);

        var window = new EventDescriptionWindow(parsed);

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner != null) window.Show(owner);
        else window.Show();
    }

    private async System.Threading.Tasks.Task CopySelectedAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        if (_grid.SelectedItem is not LogRow row)
        {
            await clipboard.SetTextAsync(GetText());
            return;
        }

        // the whole exception when there is one, as the Windows log copies it
        if (row.HasDetails && ExceptionList.Exceptions.TryGetValue(row.ExceptionId, out var exception))
        {
            await clipboard.SetTextAsync(exception.ToString());
            return;
        }

        await clipboard.SetTextAsync("[" + row.Date + "] " + row.Kind + ": " + row.Message);
    }

}

/// <summary>
/// What went wrong, read out of the exception: what happened, where, and what to do about it.
/// </summary>
public sealed class EventDescriptionWindow : Window
{

    public EventDescriptionWindow(DWSIM.SharedClasses.ExceptionProcessing.ProcessedException parsed)
    {
        Title = "Event Description";
        Width = 760;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Icon = IconHelper.GetWindowIcon();

        var stack = new StackPanel { Margin = new Thickness(10), Spacing = 8 };

        stack.Children.Add(Field("Event Type", "Error (Exception)", 1));
        stack.Children.Add(Field("Description",
            parsed.DetailedDescription + Environment.NewLine + Environment.NewLine +
            parsed.ExceptionObject?.ToString(), 12));
        stack.Children.Add(Field("Location",
            "Code Location: " + parsed.CodeLocation + Environment.NewLine + Environment.NewLine +
            "Calling Method: " + parsed.CallingMethod + Environment.NewLine + Environment.NewLine +
            parsed.CodeLocationDetails, 6));
        stack.Children.Add(Field("What to do", parsed.UserAction, 3));

        var copy = new Button { Content = "Copy Exception", MinWidth = 120 };
        copy.Click += async (_, _) =>
        {
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard != null && parsed.ExceptionObject != null)
                await clipboard.SetTextAsync(parsed.ExceptionObject.ToString());
        };

        var close = new Button { Content = "Close", MinWidth = 100, IsCancel = true };
        close.Click += (_, _) => Close();

        var buttons = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(copy);
        buttons.Children.Add(close);

        stack.Children.Add(buttons);

        Content = new ScrollViewer { Content = stack };
    }

    private static Control Field(string label, string? text, int lines)
    {
        var panel = new StackPanel { Spacing = 2 };

        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12)
        });

        panel.Children.Add(new TextBox
        {
            Text = text ?? "",
            IsReadOnly = true,
            AcceptsReturn = lines > 1,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            MinHeight = 22 * lines
        });

        return panel;
    }

}
