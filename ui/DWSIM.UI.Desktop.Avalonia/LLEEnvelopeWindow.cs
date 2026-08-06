using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Thermodynamics.Utilities.LLE;
using DWSIM.UI.Shared.Avalonia;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Ternary liquid-liquid equilibrium diagram at fixed temperature and pressure. Avalonia
/// counterpart of the WinForms FormLLEDiagram; both drive the engine's TernaryLLETracer.
/// </summary>
public sealed class LLEEnvelopeWindow : Window
{
    private sealed class TieRow
    {
        public double L1X1 { get; init; }
        public double L1X2 { get; init; }
        public double L2X1 { get; init; }
        public double L2X2 { get; init; }
    }

    private readonly IFlowsheet _flowsheet;
    private readonly IUnitsOfMeasure _su;
    private readonly string _nf;

    private ComboBox _c1 = null!, _c2 = null!, _c3 = null!, _pp = null!;
    private double _t = 298.15, _p = 101325.0;
    private Button _btnRun = null!;

    private readonly TernaryPlot _plot = new() { Margin = new Thickness(4) };
    private readonly ObservableCollection<TieRow> _rows = new();
    private readonly DataGrid _grid = new() { CanUserSortColumns = false, IsReadOnly = true, AutoGenerateColumns = false, Height = 170 };
    private readonly TextBlock _status = new() { FontSize = 11, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };

    public LLEEnvelopeWindow(IFlowsheet flowsheet)
    {
        _flowsheet = flowsheet;
        _su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
        _nf = flowsheet.FlowsheetOptions.NumberFormat;

        _t = 298.15;
        _p = 101325.0;

        Title = "LLE Envelope (Ternary Diagram)";
        Width = 980;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        var names = _flowsheet.SelectedCompounds.Keys.OrderBy(x => x).ToList();
        var packages = _flowsheet.PropertyPackages.Values.Select(x => x.Tag).ToList();

        var p = new AvaloniaEditorPanel { Width = 330 };

        p.CreateAndAddLabelRow("Compounds");
        _c1 = p.CreateAndAddDropDownRow("Compound 1", names, names.Count > 0 ? 0 : -1, null);
        _c2 = p.CreateAndAddDropDownRow("Compound 2", names, names.Count > 1 ? 1 : -1, null);
        _c3 = p.CreateAndAddDropDownRow("Compound 3", names, names.Count > 2 ? 2 : -1, null);
        p.CreateAndAddDescriptionRow("Compound 1 is the lower right corner, compound 2 the apex and compound 3 the lower left corner of the triangle.");

        p.CreateAndAddLabelRow("Conditions");
        _pp = p.CreateAndAddDropDownRow("Property Package", packages, packages.Count > 0 ? 0 : -1, null);
        p.CreateAndAddTextBoxRow(_nf, "Temperature (" + _su.temperature + ")",
            cv.ConvertFromSI(_su.temperature, _t),
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _t = cv.ConvertToSI(_su.temperature, v); });
        p.CreateAndAddTextBoxRow(_nf, "Pressure (" + _su.pressure + ")",
            cv.ConvertFromSI(_su.pressure, _p),
            (tb, e) => { if (UtilityHelpers.TryVal(tb.Text, out var v)) _p = cv.ConvertToSI(_su.pressure, v); });
        p.CreateAndAddDescriptionRow("The property package must predict a liquid phase split, otherwise no tie lines are found.");

        _grid.Columns.Add(Col("[1] x1", "L1X1"));
        _grid.Columns.Add(Col("[1] x2", "L1X2"));
        _grid.Columns.Add(Col("[2] x1", "L2X1"));
        _grid.Columns.Add(Col("[2] x2", "L2X2"));
        _grid.ItemsSource = _rows;

        _btnRun = new Button
        {
            Content = "Calculate Diagram",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(8)
        };
        _btnRun.Classes.Add("action");
        _btnRun.Click += async (_, _) => await CalculateAsync();

        var side = new DockPanel();
        DockPanel.SetDock(_btnRun, global::Avalonia.Controls.Dock.Bottom);
        side.Children.Add(_btnRun);
        side.Children.Add(new ScrollViewer { Content = p, Padding = new Thickness(8) });

        var right = new DockPanel();
        DockPanel.SetDock(_grid, global::Avalonia.Controls.Dock.Bottom);
        right.Children.Add(_grid);
        right.Children.Add(_plot);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(side, 0);
        Grid.SetColumn(right, 1);
        body.Children.Add(side);
        body.Children.Add(right);

        var btnClose = new Button { Content = "Close", Width = 90, IsCancel = true };
        btnClose.Classes.Add("dialog");
        btnClose.Click += (_, _) => Close();

        var bottom = new DockPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(btnClose, global::Avalonia.Controls.Dock.Right);
        bottom.Children.Add(btnClose);
        bottom.Children.Add(_status);

        var root = new DockPanel();
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(body);
        return root;
    }

    private static DataGridTextColumn Col(string header, string path) => new()
    {
        Header = header,
        Binding = new global::Avalonia.Data.Binding(path) { StringFormat = "{0:G5}" },
        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
    };

    private async Task CalculateAsync()
    {
        _rows.Clear();
        _plot.TieLines = null;

        if (_c1.SelectedIndex < 0 || _c2.SelectedIndex < 0 || _c3.SelectedIndex < 0)
        {
            _status.Text = "Select three compounds.";
            return;
        }

        var n1 = (string)_c1.SelectedItem!;
        var n2 = (string)_c2.SelectedItem!;
        var n3 = (string)_c3.SelectedItem!;

        if (n1 == n2 || n1 == n3 || n2 == n3)
        {
            _status.Text = "Select three different compounds.";
            return;
        }

        var pp = _flowsheet.PropertyPackages.Values.ElementAtOrDefault(Math.Max(0, _pp.SelectedIndex)) as PropertyPackage;
        if (pp == null) { _status.Text = "Select a property package."; return; }

        _btnRun.IsEnabled = false;
        _status.Text = "Tracing the miscibility gap, please wait...";

        List<TieLine> ties;
        var errors = new List<string>();
        try
        {
            ties = await Task.Run(() =>
                new TernaryLLETracer(_flowsheet, pp, n1, n2, n3, _t, _p)
                    .Trace(m => errors.Add(m)));
        }
        catch (Exception ex)
        {
            _status.Text = ex.InnerException?.Message ?? ex.Message;
            _btnRun.IsEnabled = true;
            return;
        }

        foreach (var t in ties)
            _rows.Add(new TieRow { L1X1 = t.X11, L1X2 = t.X12, L2X1 = t.X21, L2X2 = t.X22 });

        _plot.Corner1 = n1;
        _plot.Corner2 = n2;
        _plot.Corner3 = n3;
        _plot.TieLines = ties;
        _plot.InvalidateVisual();

        _btnRun.IsEnabled = true;
        _status.Text = ties.Count == 0
            ? "No liquid phase split found: the system is miscible at these conditions."
            : $"{ties.Count} tie line(s) traced." + (errors.Count > 0 ? " " + errors[0] : "");
    }
}

/// <summary>
/// Ternary composition triangle with the binodal curve and its tie lines. Compound 1 sits at
/// the lower right corner, compound 2 at the apex and compound 3 at the lower left.
/// </summary>
public sealed class TernaryPlot : Control
{
    public List<TieLine>? TieLines;
    public string Corner1 = "1", Corner2 = "2", Corner3 = "3";

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w < 60 || h < 60) return;

        const double margin = 34;
        var size = Math.Min(w - 2 * margin, h - 2 * margin);
        var left = margin + (w - size - 2 * margin) / 2;
        var bottom = h - margin;

        var fg = ActualThemeVariant == global::Avalonia.Styling.ThemeVariant.Dark
            ? Colors.WhiteSmoke : Colors.Black;
        var axis = new Pen(new SolidColorBrush(fg), 1.2);
        var tie = new Pen(new SolidColorBrush(Color.FromRgb(70, 130, 180)), 1.2);
        var binodal = new Pen(new SolidColorBrush(Color.FromRgb(196, 78, 62)), 2.0);

        Point T(double x1, double x2) => new(left + (x1 + x2 / 2) * size, bottom - x2 * size);

        // triangle
        var a = T(0, 0);
        var b = T(1, 0);
        var c = T(0, 1);
        context.DrawLine(axis, a, b);
        context.DrawLine(axis, b, c);
        context.DrawLine(axis, c, a);

        // gridlines every 20 %
        var grid = new Pen(new SolidColorBrush(Color.FromArgb(60, fg.R, fg.G, fg.B)), 0.8);
        for (int i = 1; i < 5; i++)
        {
            var f = i / 5.0;
            // the three families: constant x3, constant x1, constant x2
            context.DrawLine(grid, T(f, 0), T(0, f));
            context.DrawLine(grid, T(f, 0), T(f, 1 - f));
            context.DrawLine(grid, T(0, f), T(1 - f, f));
        }

        Label(context, fg, Corner3, a.X - 4, a.Y + 4, false);
        Label(context, fg, Corner1, b.X - 30, b.Y + 4, false);
        Label(context, fg, Corner2, c.X - 20, c.Y - 20, false);

        if (TieLines == null || TieLines.Count == 0) return;

        foreach (var t in TieLines)
            context.DrawLine(tie, T(t.X11, t.X12), T(t.X21, t.X22));

        // binodal: one branch through each end of the tie lines
        DrawBranch(context, binodal, TieLines.Select(t => T(t.X11, t.X12)).ToList());
        DrawBranch(context, binodal, TieLines.Select(t => T(t.X21, t.X22)).ToList());
    }

    private static void DrawBranch(DrawingContext context, Pen pen, List<Point> pts)
    {
        for (int i = 1; i < pts.Count; i++)
            context.DrawLine(pen, pts[i - 1], pts[i]);
    }

    private void Label(DrawingContext context, Color color, string text, double x, double y, bool centered)
    {
        var ft = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, Typeface.Default, 12, new SolidColorBrush(color));
        context.DrawText(ft, new Point(centered ? x - ft.Width / 2 : x, y));
    }
}
