using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// A block of calculated results laid out as one table with three columns: Property, Value
/// (right-aligned, tabular digits, selectable) and Unit.
///
/// Editors do not build it directly: <see cref="AvaloniaEditorExtensions.CreateAndAddResultRow(AvaloniaEditorPanel, string, string, string)"/>
/// appends a row to the table at the bottom of the panel, or starts a new table when the last
/// row of the panel is something else (a section label, a description, a chart). Consecutive
/// result rows therefore always share one table, and every table of a panel shares the widths
/// of its Value and Unit columns through a shared size scope on the panel, so the values line
/// up from one section to the next. The header row is drawn on the first table of the panel only.
/// </summary>
public sealed class ResultsTable : Border
{
    private const string ValueGroup = "DwsimResultsValue";
    private const string UnitGroup = "DwsimResultsUnit";

    // Low-alpha greys, as the column rules of the property editor: they read on both the light
    // and the dark theme without a resource of their own.
    private static readonly IBrush OutlineBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
    private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.FromArgb(36, 128, 128, 128));
    private static readonly IBrush StripeBrush = new SolidColorBrush(Color.FromArgb(16, 128, 128, 128));

    private static readonly Thickness CellMargin = new(8, 3, 8, 3);

    // the row height of a Windows Forms DataGridView at 100 % scaling
    private const double RowHeight = 24;

    private readonly Grid _grid;
    private readonly FontFeatureCollection _tabularDigits = new() { FontFeature.Parse("tnum") };
    private int _dataRows;

    public ResultsTable(bool header)
    {
        BorderBrush = OutlineBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(3);
        Margin = new Thickness(0, 1, 0, 1);
        ClipToBounds = true;

        _grid = new Grid();
        _grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 90, SharedSizeGroup = ValueGroup });
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 70, SharedSizeGroup = UnitGroup });
        Child = _grid;

        if (header) AddHeader();
    }

    /// <summary>The number of result rows, the header left out.</summary>
    public int RowCount => _dataRows;

    /// <summary>
    /// The table a new result row of <paramref name="panel"/> goes into: the one at the bottom of
    /// the panel, or a new one when the panel ends with something else.
    /// </summary>
    public static ResultsTable For(AvaloniaEditorPanel panel)
    {
        if (panel.Children.Count > 0 && panel.Children[panel.Children.Count - 1] is ResultsTable last)
            return last;

        var first = !panel.Children.OfType<ResultsTable>().Any();
        Grid.SetIsSharedSizeScope(panel, true);

        var table = new ResultsTable(header: first);
        panel.Children.Add(table);
        return table;
    }

    /// <summary>Adds a row and returns the value cell, so a caller can refresh or colour it.</summary>
    public TextBlock AddRow(string property, string value, string unit)
    {
        var row = _grid.RowDefinitions.Count;
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto) { MinHeight = RowHeight });

        if (_dataRows % 2 == 1)
        {
            var stripe = new Border { Background = StripeBrush };
            Grid.SetRow(stripe, row);
            Grid.SetColumnSpan(stripe, 3);
            _grid.Children.Add(stripe);
        }
        _dataRows++;

        var name = new TextBlock
        {
            Text = property,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = CellMargin
        };

        var number = new SelectableTextBlock
        {
            Text = value,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = CellMargin
        };
        TextElement.SetFontFeatures(number, _tabularDigits);
        // the theme gives the selectable block a font size of its own; follow the panel instead
        number[!TextBlock.FontSizeProperty] = this[!TextElement.FontSizeProperty];

        var units = new TextBlock
        {
            Text = unit ?? "",
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.8,
            Margin = CellMargin
        };

        Place(name, row, 0);
        Place(number, row, 1);
        Place(units, row, 2);

        return number;
    }

    private void AddHeader()
    {
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto) { MinHeight = RowHeight });

        var band = new Border
        {
            Background = HeaderBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        Grid.SetColumnSpan(band, 3);
        _grid.Children.Add(band);

        Place(HeaderCell("Property", TextAlignment.Left), 0, 0);
        Place(HeaderCell("Value", TextAlignment.Right), 0, 1);
        Place(HeaderCell("Unit", TextAlignment.Left), 0, 2);
    }

    private static TextBlock HeaderCell(string text, TextAlignment alignment) => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        TextAlignment = alignment,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = CellMargin
    };

    private void Place(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        _grid.Children.Add(control);
    }
}
