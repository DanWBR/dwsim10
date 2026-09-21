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
using PumpUO = DWSIM.UnitOperations.UnitOperations.Pump;

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

        /// <summary>
        /// The curve editor of a pump. A pump measured at one speed has a single set and covers other
        /// speeds with the affinity laws; a variable-frequency pump carries the manufacturer's set for
        /// each speed, and the picker at the top moves between them.
        /// </summary>
        public static void Show(DWSIM.Interfaces.ISimulationObject owner, PumpUO pump, string title)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(title, 720, 680, panel);

            var nf = owner.GetFlowsheet().FlowsheetOptions.NumberFormat;

            var host = new ContentControl { Height = 452, Margin = new Thickness(0, 8, 0, 0) };
            var picker = new ComboBox { MinWidth = 160 };
            var updating = false;

            // the measured sets, ordered by speed: the reference set (the one the pump has always
            // had) and whatever else the user added
            Func<List<KeyValuePair<double, PumpOps.CurveSet>>> sets = () =>
            {
                var all = new List<KeyValuePair<double, PumpOps.CurveSet>>
                {
                    new KeyValuePair<double, PumpOps.CurveSet>(pump.PumpCurveSet.ImpellerSpeed, pump.PumpCurveSet)
                };
                all.AddRange(pump.CurveSets.Select(kvp => new KeyValuePair<double, PumpOps.CurveSet>(kvp.Key, kvp.Value)));
                return all.OrderBy(kvp => kvp.Key).ToList();
            };

            Func<double, string> label = rpm =>
            {
                var reference = pump.PumpCurveSet.ImpellerSpeed;
                if (reference <= 0.0 || pump.NominalFrequency <= 0.0) return rpm.ToString("G6", CultureInfo.CurrentCulture) + " rpm";
                var hz = pump.NominalFrequency * rpm / reference;
                return string.Format(CultureInfo.CurrentCulture, "{0:G6} rpm ({1:G4} Hz)", rpm, hz);
            };

            Action<double> rebuild = null;

            rebuild = selected =>
            {
                updating = true;
                var current = sets();
                picker.ItemsSource = current.Select(kvp => label(kvp.Key)).ToList();
                var idx = current.FindIndex(kvp => kvp.Key == selected);
                if (idx < 0) idx = 0;
                picker.SelectedIndex = idx;
                host.Content = BuildSet(nf, current[idx].Value, pump, rebuild);
                updating = false;
            };

            picker.SelectionChanged += (s, e) =>
            {
                if (updating) return;
                var current = sets();
                if (picker.SelectedIndex >= 0 && picker.SelectedIndex < current.Count)
                    host.Content = BuildSet(nf, current[picker.SelectedIndex].Value, pump, rebuild);
            };

            var rpmBox = new TextBox { Text = "1750", Width = 90, Margin = new Thickness(8, 0, 4, 0) };

            var add = new Button { Content = "Add Speed" };
            add.Classes.Add("panel");
            add.Click += (s, e) =>
            {
                if (!int.TryParse(rpmBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var rpm) || rpm <= 0) return;
                if (Math.Abs(pump.PumpCurveSet.ImpellerSpeed - rpm) < 1e-9) return;
                if (!pump.CurveSets.ContainsKey(rpm))
                {
                    pump.CurveSets[rpm] = new PumpOps.CurveSet
                    {
                        ImpellerSpeed = rpm,
                        ImpellerDiameter = pump.PumpCurveSet.ImpellerDiameter,
                        ImpellerDiameterUnit = pump.PumpCurveSet.ImpellerDiameterUnit,
                        Name = rpm.ToString(CultureInfo.InvariantCulture) + " rpm"
                    };
                }
                rebuild(rpm);
            };

            var remove = new Button { Content = "Remove Speed", Margin = new Thickness(6, 0, 0, 0) };
            remove.Classes.Add("panel");
            remove.Click += (s, e) =>
            {
                var current = sets();
                if (current.Count <= 1) return;
                if (picker.SelectedIndex < 0 || picker.SelectedIndex >= current.Count) return;

                var victim = current[picker.SelectedIndex];

                if (ReferenceEquals(victim.Value, pump.PumpCurveSet))
                {
                    // the reference set is the one the rest of the pump reads, so the next set takes
                    // its place instead of leaving the pump without one
                    var promoted = pump.CurveSets.OrderBy(kvp => kvp.Key).First();
                    pump.CurveSets.Remove(promoted.Key);
                    pump.PumpCurveSet = promoted.Value;
                }
                else
                {
                    var key = pump.CurveSets.First(kvp => ReferenceEquals(kvp.Value, victim.Value)).Key;
                    pump.CurveSets.Remove(key);
                }

                rebuild(sets().First().Key);
            };

            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
            header.Children.Add(new TextBlock
            {
                Text = "Measured at",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            header.Children.Add(picker);
            header.Children.Add(rpmBox);
            header.Children.Add(add);
            header.Children.Add(remove);

            panel.Children.Add(header);

            panel.CreateAndAddTextBoxRow(nf, "Supply Frequency at the Reference Speed (Hz)", pump.NominalFrequency,
                (tb, e) =>
                {
                    if (!UnitOpEditorRows.TryParse(tb.Text, out var v)) return;
                    pump.NominalFrequency = v;
                    rebuild(sets()[Math.Max(0, picker.SelectedIndex)].Key);
                });

            panel.Children.Add(host);

            rebuild(pump.PumpCurveSet.ImpellerSpeed);

            window.Show();
        }

        /// <summary>The impeller data and the four curves of one measured set.</summary>
        private static Control BuildSet(string nf, PumpOps.CurveSet set, PumpUO pump, Action<double> rebuild)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            stack.Children.Add(LabeledBox("Impeller Diameter (" + set.ImpellerDiameterUnit + ")",
                set.ImpellerDiameter.ToString("G6", CultureInfo.CurrentCulture),
                text => { if (UnitOpEditorRows.TryParse(text, out var v)) set.ImpellerDiameter = v; }));

            stack.Children.Add(LabeledBox("Speed the Curves Were Measured at (rpm)",
                set.ImpellerSpeed.ToString("G6", CultureInfo.CurrentCulture),
                text =>
                {
                    if (!UnitOpEditorRows.TryParse(text, out var v) || v <= 0.0) return;
                    var previous = set.ImpellerSpeed;
                    if (Math.Abs(previous - v) < 1e-9) return;
                    set.ImpellerSpeed = v;

                    // an added set is keyed by its speed, so retyping the speed re-keys it
                    if (!ReferenceEquals(set, pump.PumpCurveSet))
                    {
                        var entry = pump.CurveSets.FirstOrDefault(kvp => ReferenceEquals(kvp.Value, set));
                        pump.CurveSets.Remove(entry.Key);
                        pump.CurveSets[(int)Math.Round(v)] = set;
                    }

                    rebuild(v);
                }));

            var tabs = new TabControl { Height = 380, Margin = new Thickness(0, 8, 0, 0) };
            tabs.Items.Add(CurveTab("Head", set.CurveHead));
            tabs.Items.Add(CurveTab("Power", set.CurvePower));
            tabs.Items.Add(CurveTab("Efficiency", set.CurveEfficiency));
            tabs.Items.Add(CurveTab("NPSHr", set.CurveNPSHr));

            stack.Children.Add(tabs);

            return stack;
        }

        /// <summary>A label and a text box on one line, for the rows inside the set panel.</summary>
        private static Control LabeledBox(string label, string value, Action<string> onChanged)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2, 4, 2) };

            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 330,
                VerticalAlignment = VerticalAlignment.Center
            });

            var box = new TextBox { Text = value, Width = 120 };
            box.LostFocus += (s, e) => onChanged(box.Text);

            row.Children.Add(box);

            return row;
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
