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
    /// The stages of a rigorous column: name, efficiency and the tray geometry the sizing reads,
    /// as the Windows stages editor lists them, with the same bulk setters below the grid.
    /// </summary>
    public static class ColumnStagesEditor
    {

        private sealed class StageRow : INotifyPropertyChanged
        {
            private readonly Stage _stage;
            private readonly IUnitsOfMeasure _su;
            private readonly string _nf;

            public StageRow(int index, Stage stage, IUnitsOfMeasure su, string nf)
            {
                Stage = index.ToString();
                _stage = stage;
                _su = su;
                _nf = nf;
            }

            public string Stage { get; private set; }

            public string Name
            {
                get { return _stage.Name; }
                set { _stage.Name = value; Raise("Name"); }
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
            var flowsheet = column.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            var rows = new ObservableCollection<StageRow>();

            if (column.Stages != null)
            {
                var index = 0;
                foreach (var stage in column.Stages)
                {
                    rows.Add(new StageRow(index, stage, su, nf));
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
            grid.Columns.Add(Column("Efficiency", "Efficiency", 1.0));
            grid.Columns.Add(Column("Total Hole Area (" + su.area + ")", "HoleArea", 1.2));
            grid.Columns.Add(Column("Downcomer Length (" + su.distance + ")", "DowncomerLength", 1.2));
            grid.Columns.Add(Column("Downcomer Height (" + su.distance + ")", "DowncomerHeight", 1.2));

            var host = new DockPanel();
            DockPanel.SetDock(grid, global::Avalonia.Controls.Dock.Top);
            host.Children.Add(grid);
            host.Children.Add(BuildBulkSetters(column, su, rows));

            return host;
        }

        private static DataGridTextColumn Column(string header, string path, double width, bool readOnly = false)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path) { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay },
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
