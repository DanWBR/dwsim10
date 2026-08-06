using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using DWSIM.UI.Shared.Avalonia;
using Thickness = Avalonia.Thickness;
using PumpOps = DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The performance curves of a pump: impeller data and the head, power, efficiency and NPSHr
    /// curves, each a table of points with its own units, as the Windows curve editor shows them.
    /// </summary>
    public static class PerformanceCurvesEditor
    {

        private sealed class Point : INotifyPropertyChanged
        {
            private double _x, _y;

            public Point(double x, double y) { _x = x; _y = y; }

            public string X
            {
                get { return _x.ToString("G6", CultureInfo.CurrentCulture); }
                set { if (UnitOpEditorRows.TryParse(value, out var v)) { _x = v; Changed(); } }
            }

            public string Y
            {
                get { return _y.ToString("G6", CultureInfo.CurrentCulture); }
                set { if (UnitOpEditorRows.TryParse(value, out var v)) { _y = v; Changed(); } }
            }

            public double XValue { get { return _x; } }
            public double YValue { get { return _y; } }

            public event Action Edited;
            public event PropertyChangedEventHandler PropertyChanged;

            private void Changed()
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("X"));
                if (Edited != null) Edited();
            }
        }

        public static void Show(DWSIM.Interfaces.ISimulationObject owner, PumpOps.CurveSet set, string title)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(title, 720, 620, panel);

            var nf = owner.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddLabelRow("Impeller");
            panel.CreateAndAddTextBoxRow(nf, "Diameter (" + set.ImpellerDiameterUnit + ")",
                set.ImpellerDiameter,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) set.ImpellerDiameter = v; });
            panel.CreateAndAddTextBoxRow(nf, "Speed (rpm)", set.ImpellerSpeed,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) set.ImpellerSpeed = v; });

            var tabs = new TabControl { Height = 420, Margin = new Thickness(0, 8, 0, 0) };
            tabs.Items.Add(CurveTab("Head", set.CurveHead));
            tabs.Items.Add(CurveTab("Power", set.CurvePower));
            tabs.Items.Add(CurveTab("Efficiency", set.CurveEfficiency));
            tabs.Items.Add(CurveTab("NPSHr", set.CurveNPSHr));

            panel.Children.Add(tabs);

            window.Show();
        }

        /// <summary>One curve as a table of points; the compressor editor reuses it.</summary>
        internal static TabItem CurveTab(string header, PumpOps.Curve curve)
        {
            var points = new ObservableCollection<Point>();

            Action writeBack = () =>
            {
                curve.X = points.Select(p => p.XValue).ToList();
                curve.Y = points.Select(p => p.YValue).ToList();
            };

            for (int i = 0; i < curve.X.Count; i++)
            {
                var point = new Point(curve.X[i], i < curve.Y.Count ? curve.Y[i] : 0.0);
                point.Edited += () => writeBack();
                points.Add(point);
            }

            var grid = new DataGrid
            {
                ItemsSource = points,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "X (" + curve.xunit + ")",
                Binding = new Binding("X") { Mode = BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Y (" + curve.yunit + ")",
                Binding = new Binding("Y") { Mode = BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            grid.CellEditEnded += (s, e) => writeBack();

            var enabled = new CheckBox { Content = "Enabled", IsChecked = curve.Enabled, Margin = new Thickness(0, 0, 12, 0) };
            enabled.IsCheckedChanged += (s, e) => curve.Enabled = enabled.IsChecked.GetValueOrDefault();

            var xunit = new TextBox { Text = curve.xunit, Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            xunit.TextChanged += (s, e) =>
            {
                curve.xunit = xunit.Text;
                grid.Columns[0].Header = "X (" + curve.xunit + ")";
            };

            var yunit = new TextBox { Text = curve.yunit, Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            yunit.TextChanged += (s, e) =>
            {
                curve.yunit = yunit.Text;
                grid.Columns[1].Header = "Y (" + curve.yunit + ")";
            };

            var add = new Button { Content = "Add Point", Margin = new Thickness(0, 0, 6, 0) };
            add.Classes.Add("panel");
            add.Click += (s, e) =>
            {
                var point = new Point(0.0, 0.0);
                point.Edited += () => writeBack();
                points.Add(point);
                writeBack();
            };

            var remove = new Button { Content = "Remove Point" };
            remove.Classes.Add("panel");
            remove.Click += (s, e) =>
            {
                var selected = grid.SelectedItem as Point;
                if (selected == null) return;
                points.Remove(selected);
                writeBack();
            };

            var header1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
            header1.Children.Add(enabled);
            header1.Children.Add(new TextBlock { Text = "X unit", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            header1.Children.Add(xunit);
            header1.Children.Add(new TextBlock { Text = "Y unit", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            header1.Children.Add(yunit);
            header1.Children.Add(add);
            header1.Children.Add(remove);

            var host = new DockPanel();
            DockPanel.SetDock(header1, global::Avalonia.Controls.Dock.Top);
            host.Children.Add(header1);
            host.Children.Add(grid);

            return new TabItem { Header = header, Content = host };
        }

    }

}
