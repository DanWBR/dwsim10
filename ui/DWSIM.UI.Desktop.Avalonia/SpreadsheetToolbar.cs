using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;
using DWSIM.UI.Desktop.Avalonia.Controls;
using DWSIM.UI.Shared.Avalonia;
using unvell.ReoGrid;
using unvell.ReoGrid.DataFormat;
using ReoGridFile = unvell.ReoGrid.IO.FileFormat;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// The chrome around the spreadsheet, as the Windows editor carries it: the address and formula
/// bar, the worksheets, what the cells look like, and the commands that tie the sheet to the
/// flowsheet.
/// </summary>
public sealed class SpreadsheetToolbar : DockPanel
{

    private readonly SpreadsheetPanel _panel;
    private readonly IFlowsheet _flowsheet;

    private readonly TextBox _address;
    private readonly TextBox _formula;
    private readonly ComboBox _sheets;

    /// <summary>Keeps writing the cell back while the bar is being filled from it.</summary>
    private bool _reading;

    /// <summary>
    /// The cell the bar is editing, set as soon as the text is touched. The write goes there and
    /// not to whatever is selected when the box loses the focus, which by then is another cell.
    /// </summary>
    private CellPosition? _editing;

    public SpreadsheetToolbar(SpreadsheetPanel panel, IFlowsheet flowsheet)
    {
        _panel = panel;
        _flowsheet = flowsheet;

        var commands = Row();

        _sheets = new ComboBox
        {
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            MinWidth = 120,
            VerticalAlignment = VerticalAlignment.Center
        };
        _sheets.SelectionChanged += (_, _) => SelectSheet();

        commands.Children.Add(Label("Sheet"));
        commands.Children.Add(_sheets);
        commands.Children.Add(Button("Add", (_, _) => AddSheet()));
        commands.Children.Add(Button("Rename...", async (_, _) => await RenameSheetAsync()));
        commands.Children.Add(Button("Remove", (_, _) => RemoveSheet()));

        commands.Children.Add(Separator());

        commands.Children.Add(Button("Recalculate", (_, _) => _panel.EvaluateAll()));
        commands.Children.Add(Button("Import Data...", async (_, _) => await ImportAsync()));
        commands.Children.Add(Button("Export Data...", async (_, _) => await ExportAsync()));
        commands.Children.Add(Button("Chart from Selection", (_, _) => ChartFromSelection()));

        commands.Children.Add(Separator());

        commands.Children.Add(Button("Open...", async (_, _) => await OpenAsync()));
        commands.Children.Add(Button("Save As...", async (_, _) => await SaveAsync()));

        var format = Row();

        format.Children.Add(Button("B", (_, _) => SetBold(), bold: true));
        format.Children.Add(Button("I", (_, _) => SetItalic(), italic: true));
        format.Children.Add(Button("Left", (_, _) => SetAlign(ReoGridHorAlign.Left)));
        format.Children.Add(Button("Center", (_, _) => SetAlign(ReoGridHorAlign.Center)));
        format.Children.Add(Button("Right", (_, _) => SetAlign(ReoGridHorAlign.Right)));

        format.Children.Add(Separator());

        format.Children.Add(Label("Decimals"));

        var decimals = new ComboBox
        {
            ItemsSource = new[] { "General", "0", "1", "2", "3", "4", "6", "8" },
            SelectedIndex = 0,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            MinWidth = 90,
            VerticalAlignment = VerticalAlignment.Center
        };
        decimals.SelectionChanged += (_, _) => SetNumberFormat(decimals.SelectedIndex);
        format.Children.Add(decimals);

        format.Children.Add(Button("Percent", (_, _) => SetFormat(CellDataFormatFlag.Percent)));
        format.Children.Add(Button("Text", (_, _) => SetFormat(CellDataFormatFlag.Text)));

        format.Children.Add(Separator());

        format.Children.Add(Button("Merge", (_, _) => Merge(true)));
        format.Children.Add(Button("Unmerge", (_, _) => Merge(false)));

        // the address and the formula of the selected cell, the way every spreadsheet shows them
        var bar = Row();

        _address = new TextBox
        {
            Width = 90,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            IsReadOnly = true,
            VerticalAlignment = VerticalAlignment.Center
        };

        _formula = new TextBox
        {
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            MinWidth = 400,
            VerticalAlignment = VerticalAlignment.Center,
            Watermark = "Value or formula of the selected cell"
        };

        _formula.TextChanged += (_, _) =>
        {
            if (_reading || _editing != null) return;

            var sheet = Sheet;
            if (sheet != null) _editing = sheet.SelectionRange.StartPos;
        };

        _formula.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) WriteCell();
            if (e.Key == Key.Escape) { _editing = null; ReadCell(); }
        };

        _formula.LostFocus += (_, _) => WriteCell();

        bar.Children.Add(_address);
        bar.Children.Add(_formula);
        bar.Children[1].SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);

        var top = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Vertical };
        top.Children.Add(commands);
        top.Children.Add(format);
        top.Children.Add(bar);

        SetDock(top, global::Avalonia.Controls.Dock.Top);
        Children.Add(top);
        Children.Add(_panel.Grid);

        _panel.Grid.CurrentWorksheetChanged += (_, _) => { ReloadSheets(); ReadCell(); };
        _panel.Grid.WorksheetInserted += (_, _) => ReloadSheets();
        _panel.Grid.WorksheetRemoved += (_, _) => ReloadSheets();

        HookSelection();

        ReloadSheets();
    }

    // -------------------------------------------------------------------------
    // Building blocks
    // -------------------------------------------------------------------------

    private static StackPanel Row()
    {
        return new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(4, 2)
        };
    }

    private static TextBlock Label(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 2, 0)
        };
    }

    private static Control Separator()
    {
        return new Border
        {
            Width = 1,
            Margin = new Thickness(6, 2),
            Background = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128))
        };
    }

    private static Button Button(string text,
                                 EventHandler<global::Avalonia.Interactivity.RoutedEventArgs> handler,
                                 bool bold = false, bool italic = false)
    {
        var button = new global::Avalonia.Controls.Button
        {
            Content = text,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
            Padding = new Thickness(8, 3),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("panel");
        button.Click += handler;
        return button;
    }

    /// <summary>
    /// The worksheet the bar works on. Loading a simulation empties the book before filling it
    /// again, so there are moments when there is none.
    /// </summary>
    private Worksheet? Sheet
    {
        get { return _panel.Grid.Worksheets.Count == 0 ? null : _panel.Grid.CurrentWorksheet; }
    }

    // -------------------------------------------------------------------------
    // Address and formula bar
    // -------------------------------------------------------------------------

    private void HookSelection()
    {
        foreach (var sheet in _panel.Grid.Worksheets) Hook(sheet);

        _panel.Grid.WorksheetInserted += (_, e) => Hook(e.Worksheet);
        _panel.Grid.WorksheetCreated += (_, e) => Hook(e.Worksheet);
    }

    private void Hook(Worksheet sheet)
    {
        sheet.SelectionRangeChanged += (_, _) => ReadCell();
        sheet.CellDataChanged += (_, _) => ReadCell();
    }

    private void ReadCell()
    {
        try
        {
            _reading = true;

            var sheet = Sheet;
            if (sheet == null) { _formula.Text = ""; _address.Text = ""; return; }

            var pos = sheet.SelectionRange.StartPos;

            _address.Text = pos.ToAddress();
            _editing = null;

            var cell = sheet.GetCell(pos);
            if (cell == null) { _formula.Text = ""; return; }

            _formula.Text = string.IsNullOrEmpty(cell.Formula)
                ? cell.Data?.ToString() ?? ""
                : "=" + cell.Formula;
        }
        catch (Exception) { }
        finally { _reading = false; }
    }

    private void WriteCell()
    {
        if (_reading || _editing == null) return;

        var position = _editing.Value;
        _editing = null;

        try
        {
            var sheet = Sheet;
            if (sheet == null) return;

            var cell = sheet.CreateAndGetCell(position);
            var text = _formula.Text ?? "";

            if (text.StartsWith("="))
            {
                cell.Formula = text.Substring(1);
                sheet.Recalculate();
            }
            else
            {
                cell.Formula = null;
                cell.Data = text;
            }
        }
        catch (Exception ex)
        {
            _flowsheet.ShowMessage("Could not write the cell: " + ex.Message,
                IFlowsheet.MessageType.GeneralError);
        }
    }

    // -------------------------------------------------------------------------
    // Worksheets
    // -------------------------------------------------------------------------

    private void ReloadSheets()
    {
        var names = _panel.Grid.Worksheets.Select(x => x.Name).ToList();
        var current = Sheet;

        _reading = true;
        _sheets.ItemsSource = names;
        _sheets.SelectedIndex = current == null ? -1 : names.IndexOf(current.Name);
        _reading = false;
    }

    private void SelectSheet()
    {
        if (_reading) return;
        if (_sheets.SelectedItem is not string name) return;

        var sheet = _panel.Grid.Worksheets.FirstOrDefault(x => x.Name == name);
        if (sheet != null) _panel.Grid.CurrentWorksheet = sheet;
    }

    private void AddSheet()
    {
        var index = _panel.Grid.Worksheets.Count + 1;
        var name = "Sheet" + index;

        while (_panel.Grid.Worksheets.Any(x => x.Name == name))
        {
            index++;
            name = "Sheet" + index;
        }

        var sheet = _panel.Grid.NewWorksheet(name);
        _panel.Grid.CurrentWorksheet = sheet;

        ReloadSheets();
    }

    private async System.Threading.Tasks.Task RenameSheetAsync()
    {
        var sheet = Sheet;
        if (sheet == null) return;

        var name = await TextPrompt.AskAsync(TopLevel.GetTopLevel(this) as Window,
            "Rename Worksheet", "Name", sheet.Name);

        if (string.IsNullOrWhiteSpace(name)) return;

        if (_panel.Grid.Worksheets.Any(x => x.Name == name))
        {
            _flowsheet.ShowMessage("There is already a worksheet with that name.",
                IFlowsheet.MessageType.Warning);
            return;
        }

        sheet.Name = name;
        ReloadSheets();
    }

    private void RemoveSheet()
    {
        if (_panel.Grid.Worksheets.Count < 2)
        {
            _flowsheet.ShowMessage("The spreadsheet needs at least one worksheet.",
                IFlowsheet.MessageType.Warning);
            return;
        }

        _panel.Grid.RemoveWorksheet(_panel.Grid.CurrentWorksheet);
        ReloadSheets();
    }

    // -------------------------------------------------------------------------
    // Cell appearance
    // -------------------------------------------------------------------------

    private void SetBold()
    {
        var sheet = Sheet;
        if (sheet == null) return;

        var bold = !sheet.Cells[sheet.SelectionRange.StartPos].Style.Bold;

        sheet.SetRangeStyles(sheet.SelectionRange, new WorksheetRangeStyle
        {
            Flag = PlainStyleFlag.FontStyleBold,
            Bold = bold
        });
    }

    private void SetItalic()
    {
        var sheet = Sheet;
        if (sheet == null) return;

        var italic = !sheet.Cells[sheet.SelectionRange.StartPos].Style.Italic;

        sheet.SetRangeStyles(sheet.SelectionRange, new WorksheetRangeStyle
        {
            Flag = PlainStyleFlag.FontStyleItalic,
            Italic = italic
        });
    }

    private void SetAlign(ReoGridHorAlign align)
    {
        var sheet = Sheet;
        if (sheet == null) return;

        sheet.SetRangeStyles(sheet.SelectionRange, new WorksheetRangeStyle
        {
            Flag = PlainStyleFlag.HorizontalAlign,
            HAlign = align
        });
    }

    private void SetNumberFormat(int index)
    {
        if (_reading) return;

        var sheet = Sheet;
        if (sheet == null) return;

        if (index <= 0)
        {
            sheet.SetRangeDataFormat(sheet.SelectionRange, CellDataFormatFlag.General, null);
            return;
        }

        int[] places = { 0, 0, 1, 2, 3, 4, 6, 8 };

        sheet.SetRangeDataFormat(sheet.SelectionRange, CellDataFormatFlag.Number,
            new NumberDataFormatter.NumberFormatArgs
            {
                DecimalPlaces = (short)places[index],
                UseSeparator = false,
                NegativeStyle = NumberDataFormatter.NumberNegativeStyle.Minus
            });
    }

    private void SetFormat(CellDataFormatFlag format)
    {
        var sheet = Sheet;
        if (sheet == null) return;

        sheet.SetRangeDataFormat(sheet.SelectionRange, format, null);
    }

    private void Merge(bool merge)
    {
        var sheet = Sheet;
        if (sheet == null) return;

        try
        {
            if (merge) sheet.MergeRange(sheet.SelectionRange);
            else sheet.UnmergeRange(sheet.SelectionRange);
        }
        catch (Exception ex)
        {
            _flowsheet.ShowMessage(ex.Message, IFlowsheet.MessageType.GeneralError);
        }
    }

    // -------------------------------------------------------------------------
    // Flowsheet data
    // -------------------------------------------------------------------------

    private async System.Threading.Tasks.Task ImportAsync()
    {
        var dialog = new PropertySelectorDialog(_flowsheet);
        await dialog.ShowDialog((TopLevel.GetTopLevel(this) as Window)!);

        if (!dialog.Confirmed || dialog.SelectedObjectId == null || dialog.SelectedPropertyKey == null) return;

        var sheet = Sheet;
        if (sheet == null) return;

        var cell = sheet.CreateAndGetCell(sheet.SelectionRange.StartPos);
        // If no unit was picked, use the 2-argument form so GETPROPVAL returns the
        // value in the property's own unit instead of a conversion from "" (which
        // would throw and show INVALID ARGS / ERROR).
        var unit = dialog.SelectedUnit ?? "";
        cell.Formula = string.IsNullOrEmpty(unit)
            ? string.Format("GETPROPVAL(\"{0}\",\"{1}\")", dialog.SelectedObjectId, dialog.SelectedPropertyKey)
            : string.Format("GETPROPVAL(\"{0}\",\"{1}\",\"{2}\")", dialog.SelectedObjectId, dialog.SelectedPropertyKey, unit);

        sheet.Recalculate();
        ReadCell();
    }

    private async System.Threading.Tasks.Task ExportAsync()
    {
        var dialog = new PropertySelectorDialog(_flowsheet);
        await dialog.ShowDialog((TopLevel.GetTopLevel(this) as Window)!);

        if (!dialog.Confirmed || dialog.SelectedObjectId == null || dialog.SelectedPropertyKey == null) return;

        var sheet = Sheet;
        if (sheet == null) return;

        var cell = sheet.CreateAndGetCell(sheet.SelectionRange.StartPos);

        // what the cell already holds becomes the value written to the property
        var current = string.IsNullOrEmpty(cell.Formula) ? cell.Data?.ToString() ?? "" : cell.Formula;

        var unit = dialog.SelectedUnit ?? "";
        cell.Formula = string.IsNullOrEmpty(unit)
            ? string.Format("SETPROPVAL(\"{0}\",\"{1}\",\"{2}\")", dialog.SelectedObjectId, dialog.SelectedPropertyKey, current)
            : string.Format("SETPROPVAL(\"{0}\",\"{1}\",\"{2}\",\"{3}\")", dialog.SelectedObjectId, dialog.SelectedPropertyKey, current, unit);

        sheet.Recalculate();
        ReadCell();
    }

    /// <summary>
    /// Plots the selected range: the first column is x, the others are curves, and a row of text
    /// on top names them.
    /// </summary>
    private void ChartFromSelection()
    {
        var sheet = Sheet;
        if (sheet == null) return;

        var range = sheet.SelectionRange;

        if (range.Cols < 2 || range.Rows < 2)
        {
            _flowsheet.ShowMessage("Select at least two columns and two rows to plot.",
                IFlowsheet.MessageType.Warning);
            return;
        }

        var headers = !double.TryParse(Text(range.Row, range.Col), NumberStyles.Any,
            CultureInfo.CurrentCulture, out _);

        var first = headers ? range.Row + 1 : range.Row;

        var x = new List<double>();
        for (int row = first; row <= range.EndRow; row++)
        {
            if (double.TryParse(Text(row, range.Col), NumberStyles.Any, CultureInfo.CurrentCulture, out var v))
                x.Add(v);
        }

        var plot = new XYPlot
        {
            PlotTitle = "Chart from " + sheet.Name,
            XAxisTitle = headers ? Text(range.Row, range.Col) : ""
        };

        for (int col = range.Col + 1; col <= range.EndCol; col++)
        {
            var y = new List<double>();
            for (int row = first; row <= range.EndRow; row++)
            {
                if (double.TryParse(Text(row, col), NumberStyles.Any, CultureInfo.CurrentCulture, out var v))
                    y.Add(v);
            }

            var title = headers ? Text(range.Row, col) : "Series " + (col - range.Col);
            plot.AddSeries(title, x, y);
        }

        if (plot.Series.Count == 1) plot.YAxisTitle = plot.Series[0].Title;

        new Window
        {
            Title = "Chart from Selection",
            Width = 800,
            Height = 550,
            Icon = IconHelper.GetWindowIcon(),
            Content = new Border { Padding = new Thickness(8), Child = plot }
        }.Show();
    }

    private string Text(int row, int col)
    {
        var cell = Sheet?.GetCell(row, col);
        return cell?.Data?.ToString() ?? "";
    }

    // -------------------------------------------------------------------------
    // Files
    // -------------------------------------------------------------------------

    private async System.Threading.Tasks.Task OpenAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Spreadsheet",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Excel Workbook") { Patterns = new[] { "*.xlsx" } }
            }
        });

        var path = files?.FirstOrDefault()?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            _panel.Loaded = false;
            using (var stream = File.OpenRead(path)) _panel.Grid.Load(stream, ReoGridFile.Excel2007);
            _panel.Loaded = true;

            ReloadSheets();
            _flowsheet.ShowMessage("Spreadsheet loaded from " + Path.GetFileName(path) + ".",
                IFlowsheet.MessageType.Information);
        }
        catch (Exception ex)
        {
            _panel.Loaded = true;
            _flowsheet.ShowMessage("Could not load the spreadsheet: " + ex.Message,
                IFlowsheet.MessageType.GeneralError);
        }
    }

    private async System.Threading.Tasks.Task SaveAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Spreadsheet",
            SuggestedFileName = "spreadsheet.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Excel Workbook") { Patterns = new[] { "*.xlsx" } }
            }
        });

        var path = file?.Path?.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            using (var stream = File.Create(path)) _panel.Grid.Save(stream, ReoGridFile.Excel2007);

            _flowsheet.ShowMessage("Spreadsheet saved to " + Path.GetFileName(path) + ".",
                IFlowsheet.MessageType.Information);
        }
        catch (Exception ex)
        {
            _flowsheet.ShowMessage("Could not save the spreadsheet: " + ex.Message,
                IFlowsheet.MessageType.GeneralError);
        }
    }

}

/// <summary>A one-line prompt, for the few places that need a name and nothing else.</summary>
internal static class TextPrompt
{

    public static async System.Threading.Tasks.Task<string?> AskAsync(Window? owner, string title,
                                                                     string label, string current)
    {
        var box = new TextBox { Text = current, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12), Margin = new Thickness(0, 4) };

        var ok = new global::Avalonia.Controls.Button { Content = "OK", MinWidth = 80, IsDefault = true };
        var cancel = new global::Avalonia.Controls.Button { Content = "Cancel", MinWidth = 80, IsCancel = true };

        var buttons = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var stack = new StackPanel { Margin = new Thickness(12), Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = label, FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(12) });
        stack.Children.Add(box);
        stack.Children.Add(buttons);

        var window = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = IconHelper.GetWindowIcon(),
            Content = stack
        };

        string? result = null;

        ok.Click += (_, _) => { result = box.Text; window.Close(); };
        cancel.Click += (_, _) => window.Close();

        if (owner != null) await window.ShowDialog(owner);
        else window.Show();

        return result;
    }

}
