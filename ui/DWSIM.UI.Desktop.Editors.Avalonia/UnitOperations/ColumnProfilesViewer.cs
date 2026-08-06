using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;
using Column = DWSIM.UnitOperations.UnitOperations.Column;
using Thickness = Avalonia.Thickness;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Stage-by-stage profiles of a solved column: temperature, pressure and the vapour and
    /// liquid flows, plus the compositions of each phase, which is what the Windows results
    /// window plots and tabulates.
    /// </summary>
    public static class ColumnProfilesViewer
    {

        private sealed class ProfileRow
        {
            public string Stage { get; set; } = "";
            public string Temperature { get; set; } = "";
            public string Pressure { get; set; } = "";
            public string LiquidFlow { get; set; } = "";
            public string VaporFlow { get; set; } = "";
        }

        public static void Show(Column column)
        {
            var flowsheet = column.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(
                column.GraphicObject.Tag + ": Profiles", 820, 640, panel);

            if (column.Stages == null || column.Stages.Count == 0 || Compositions(column, vapor: false) == null)
            {
                panel.CreateAndAddDescriptionRow("Solve the column to see its profiles.");
                window.Show();
                return;
            }

            var tabs = new TabControl { Height = 520 };
            tabs.Items.Add(new TabItem { Header = "Profile", Content = BuildProfileGrid(column, su, nf) });
            tabs.Items.Add(new TabItem { Header = "Liquid Composition", Content = BuildCompositionGrid(column, nf, vapor: false) });
            tabs.Items.Add(new TabItem { Header = "Vapor Composition", Content = BuildCompositionGrid(column, nf, vapor: true) });

            panel.Children.Add(tabs);
            window.Show();
        }

        private static Control BuildProfileGrid(Column column, IUnitsOfMeasure su, string nf)
        {
            var rows = new ObservableCollection<ProfileRow>();

            for (int i = 0; i < column.Stages.Count; i++)
            {
                rows.Add(new ProfileRow
                {
                    Stage = column.Stages[i].Name,
                    Temperature = Value(column.Tf.Length > 0 ? column.Tf : column.T0, i, su.temperature, nf),
                    Pressure = Value(column.P0, i, su.pressure, nf),
                    LiquidFlow = Value(column.Lf.Length > 0 ? column.Lf : column.L0, i, su.molarflow, nf),
                    VaporFlow = Value(column.Vf.Length > 0 ? column.Vf : column.V0, i, su.molarflow, nf)
                });
            }

            var grid = Grid();
            grid.ItemsSource = rows;
            grid.Columns.Add(Column("Stage", "Stage", 1.4));
            grid.Columns.Add(Column("Temperature (" + su.temperature + ")", "Temperature", 1.0));
            grid.Columns.Add(Column("Pressure (" + su.pressure + ")", "Pressure", 1.0));
            grid.Columns.Add(Column("Liquid Flow (" + su.molarflow + ")", "LiquidFlow", 1.0));
            grid.Columns.Add(Column("Vapor Flow (" + su.molarflow + ")", "VaporFlow", 1.0));
            return grid;
        }

        /// <summary>
        /// The converged compositions if the column has been solved, the initial estimates
        /// otherwise, and null when neither has been filled in yet.
        /// </summary>
        private static System.Collections.ArrayList Compositions(Column column, bool vapor)
        {
            var final = vapor ? column.yf : column.xf;
            if (final != null && final.Count > 0) return final;

            var initial = vapor ? column.y0 : column.x0;
            if (initial != null && initial.Count > 0) return initial;

            return null;
        }

        /// <summary>One row per stage, one column per compound.</summary>
        private static Control BuildCompositionGrid(Column column, string nf, bool vapor)
        {
            var compounds = column.GetFlowsheet().SelectedCompounds.Keys.ToList();
            var source = Compositions(column, vapor);

            var rows = new List<Dictionary<string, string>>();

            for (int i = 0; i < column.Stages.Count; i++)
            {
                var row = new Dictionary<string, string> { { "Stage", column.Stages[i].Name } };
                var stage = source != null && i < source.Count ? source[i] as double[] : null;

                for (int j = 0; j < compounds.Count; j++)
                    row[compounds[j]] = stage != null && j < stage.Length
                        ? stage[j].ToString(nf, CultureInfo.CurrentCulture)
                        : "";

                rows.Add(row);
            }

            var grid = Grid();
            grid.ItemsSource = rows;
            grid.Columns.Add(Column("Stage", "[Stage]", 1.4));
            foreach (var compound in compounds)
                grid.Columns.Add(Column(compound, "[" + compound + "]", 1.0));

            return grid;
        }

        private static DataGrid Grid()
        {
            return new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
            };
        }

        private static DataGridTextColumn Column(string header, string path, double width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            };
        }

        private static string Value(double[] values, int index, string unit, string nf)
        {
            if (values == null || index >= values.Length) return "";
            return cv.ConvertFromSI(unit, values[index]).ToString(nf, CultureInfo.CurrentCulture);
        }

    }

}
