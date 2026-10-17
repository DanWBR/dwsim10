using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Column = DWSIM.UnitOperations.UnitOperations.Column;
using Stage = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.Stage;
using Thickness = Avalonia.Thickness;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The stages of a rigorous column: name, pressure, efficiency and the tray geometry the sizing
    /// reads, as the Windows stages editor lists them, with the same bulk setters below the grid.
    /// </summary>
    public static class ColumnStagesEditor
    {

        private sealed class StageRow : INotifyPropertyChanged
        {
            private readonly Stage _stage;
            private readonly IUnitsOfMeasure _su;
            private readonly string _nf;
            private readonly int _index;
            private readonly Action<int, double> _setPressure;

            public StageRow(int index, Stage stage, IUnitsOfMeasure su, string nf, Action<int, double> setPressure)
            {
                Stage = index.ToString();
                _index = index;
                _stage = stage;
                _su = su;
                _nf = nf;
                _setPressure = setPressure;
            }

            public string Stage { get; private set; }

            public string Name
            {
                get { return _stage.Name; }
                set { _stage.Name = value; Raise("Name"); }
            }

            public string Pressure
            {
                get { return cv.ConvertFromSI(_su.pressure, _stage.P).ToString(_nf, CultureInfo.CurrentCulture); }
                set
                {
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                    _setPressure(_index, cv.ConvertToSI(_su.pressure, v));
                }
            }

            public string Efficiency
            {
                get { return _stage.Efficiency.ToString(_nf, CultureInfo.CurrentCulture); }
                set { if (UnitOpEditorRows.TryParse(value, out var v)) { _stage.Efficiency = v; Raise("Efficiency"); } }
            }

            public string HoleArea
            {
                get { return cv.ConvertFromSI(_su.area, _stage.TotalHoleArea).ToString(_nf, CultureInfo.CurrentCulture); }
                set
                {
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                    _stage.TotalHoleArea = cv.ConvertToSI(_su.area, v);
                    Raise("HoleArea");
                }
            }

            public string DowncomerLength
            {
                get { return cv.ConvertFromSI(_su.distance, _stage.DowncomerLength).ToString(_nf, CultureInfo.CurrentCulture); }
                set
                {
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                    _stage.DowncomerLength = cv.ConvertToSI(_su.distance, v);
                    Raise("DowncomerLength");
                }
            }

            public string DowncomerHeight
            {
                get { return cv.ConvertFromSI(_su.distance, _stage.DowncomerHeight).ToString(_nf, CultureInfo.CurrentCulture); }
                set
                {
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                    _stage.DowncomerHeight = cv.ConvertToSI(_su.distance, v);
                    Raise("DowncomerHeight");
                }
            }

            public void Refresh()
            {
                Raise("Pressure");
                Raise("Efficiency");
                Raise("HoleArea");
                Raise("DowncomerLength");
                Raise("DowncomerHeight");
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public static Control Build(Column column)
        {
            return Build(column, null, out _);
        }

        /// <summary>
        /// The stages grid. <paramref name="pressuresChanged"/> runs after a stage pressure is typed, so
        /// the general tab can show the new top pressure and drop; <paramref name="refresh"/> redraws the
        /// grid after the general tab changes them.
        /// </summary>
        public static Control Build(Column column, Action pressuresChanged, out Action refresh)
        {
            var flowsheet = column.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            var rows = new ObservableCollection<StageRow>();

            // The top stage moves the profile as the top pressure does in the general tab. Any other
            // stage makes the profile a custom one: the column pressure drop is cleared, or the solver
            // would recompute the stages from the top and the typed value would be lost.
            void SetPressure(int index, double p)
            {
                if (column.Stages == null || index < 0 || index >= column.Stages.Count) return;
                flowsheet.RegisterSnapshot(SnapshotType.ObjectData, column);
                if (index == 0)
                {
                    column.SetTopStagePressure(p);
                }
                else
                {
                    column.ColumnPressureDrop = double.NaN;
                    column.Stages[index].P = p;
                }
                foreach (var row in rows) row.Refresh();
                pressuresChanged?.Invoke();
            }

            if (column.Stages != null)
            {
                var index = 0;
                foreach (var stage in column.Stages)
                {
                    rows.Add(new StageRow(index, stage, su, nf, SetPressure));
                    index += 1;
                }
            }

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 300
            };

            grid.Columns.Add(Column("Stage", "Stage", 0.6, readOnly: true));
            grid.Columns.Add(Column("Name", "Name", 1.4));
            grid.Columns.Add(Column("Pressure (" + su.pressure + ")", "Pressure", 1.1));
            grid.Columns.Add(Column("Efficiency", "Efficiency", 1.0));
            grid.Columns.Add(Column("Total Hole Area (" + su.area + ")", "HoleArea", 1.2));
            grid.Columns.Add(Column("Downcomer Length (" + su.distance + ")", "DowncomerLength", 1.2));
            grid.Columns.Add(Column("Downcomer Height (" + su.distance + ")", "DowncomerHeight", 1.2));

            var note = new TextBlock
            {
                Text = "Stage pressures follow the top pressure and the column pressure drop. Typing the pressure of a stage below the top makes the profile a custom one and clears the column pressure drop.",
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.75,
                Margin = new Thickness(4, 6, 4, 2)
            };

            var host = new DockPanel();
            DockPanel.SetDock(grid, global::Avalonia.Controls.Dock.Top);
            DockPanel.SetDock(note, global::Avalonia.Controls.Dock.Top);
            host.Children.Add(grid);
            host.Children.Add(note);
            host.Children.Add(BuildBulkSetters(column, su, rows));

            refresh = () => { foreach (var row in rows) row.Refresh(); };

            return host;
        }

        private static DataGridTextColumn Column(string header, string path, double width, bool readOnly = false)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path) { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                IsReadOnly = readOnly,
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            };
        }

        /// <summary>The "set for all stages" fields the Windows editor keeps under the grid.</summary>
        private static Control BuildBulkSetters(Column column, IUnitsOfMeasure su,
                                                ObservableCollection<StageRow> rows)
        {
            var panel = new AvaloniaEditorPanel();
            var nf = column.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddLabelRow("Set for all stages");

            Bulk(panel, nf, "Set Efficiency", "", v =>
            {
                foreach (var stage in column.Stages) stage.Efficiency = v;
            }, rows);

            Bulk(panel, nf, "Set Total Hole Area", su.area, v =>
            {
                foreach (var stage in column.Stages) stage.TotalHoleArea = cv.ConvertToSI(su.area, v);
            }, rows);

            Bulk(panel, nf, "Set Downcomer Length", su.distance, v =>
            {
                foreach (var stage in column.Stages) stage.DowncomerLength = cv.ConvertToSI(su.distance, v);
            }, rows);

            Bulk(panel, nf, "Set Downcomer Height", su.distance, v =>
            {
                foreach (var stage in column.Stages) stage.DowncomerHeight = cv.ConvertToSI(su.distance, v);
            }, rows);

            return panel;
        }

        private static void Bulk(AvaloniaEditorPanel panel, string nf, string label, string unit,
                                 Action<double> apply, ObservableCollection<StageRow> rows)
        {
            var caption = string.IsNullOrEmpty(unit) ? label : label + " (" + unit + ")";

            var value = new TextBox { Width = 120, TextAlignment = global::Avalonia.Media.TextAlignment.Right };

            var button = new Button { Content = "Set", Margin = new Thickness(6, 0, 0, 0) };
            button.Classes.Add("panel");
            button.Click += (s, e) =>
            {
                if (!UnitOpEditorRows.TryParse(value.Text, out var v)) return;
                apply(v);
                foreach (var row in rows) row.Refresh();
            };

            var row2 = new StackPanel { Orientation = Orientation.Horizontal };
            row2.Children.Add(value);
            row2.Children.Add(button);

            panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(caption, row2));
        }

    }

}
