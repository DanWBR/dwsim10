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
using DWSIM.UI.Shared.Avalonia;
using Column = DWSIM.UnitOperations.UnitOperations.Column;
using Parameter = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.Parameter;
using Thickness = Avalonia.Thickness;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The initial estimates of a rigorous column, as the Windows estimates editor holds them:
    /// which sets the solver is allowed to use, the stage temperatures and flows, and the liquid
    /// and vapor compositions, all editable and all readable back from the last solution.
    /// </summary>
    public static class ColumnEstimatesEditor
    {

        /// <summary>Temperature and the two molar flows of one stage.</summary>
        private sealed class FlowRow : INotifyPropertyChanged
        {
            private readonly Column _column;
            private readonly int _index;
            private readonly IUnitsOfMeasure _su;
            private readonly string _nf;

            public FlowRow(Column column, int index, IUnitsOfMeasure su, string nf)
            {
                _column = column;
                _index = index;
                _su = su;
                _nf = nf;
                Stage = column.Stages[index].Name;
            }

            public string Stage { get; private set; }

            public string Temperature
            {
                get { return Read(_column.InitialEstimates.StageTemps, _su.temperature); }
                set { Write(_column.InitialEstimates.StageTemps, _su.temperature, value, "Temperature"); }
            }

            public string VaporFlow
            {
                get { return Read(_column.InitialEstimates.VapMolarFlows, _su.molarflow); }
                set { Write(_column.InitialEstimates.VapMolarFlows, _su.molarflow, value, "VaporFlow"); }
            }

            public string LiquidFlow
            {
                get { return Read(_column.InitialEstimates.LiqMolarFlows, _su.molarflow); }
                set { Write(_column.InitialEstimates.LiqMolarFlows, _su.molarflow, value, "LiquidFlow"); }
            }

            private string Read(List<Parameter> values, string unit)
            {
                if (values == null || _index >= values.Count) return "";
                return cv.ConvertFromSI(unit, values[_index].Value).ToString(_nf, CultureInfo.CurrentCulture);
            }

            private void Write(List<Parameter> values, string unit,
                               string text, string property)
            {
                if (values == null || _index >= values.Count) return;
                if (!UnitOpEditorRows.TryParse(text, out var v)) return;
                values[_index].Value = cv.ConvertToSI(unit, v);
                Raise(property);
            }

            public void Refresh()
            {
                Raise("Temperature");
                Raise("VaporFlow");
                Raise("LiquidFlow");
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// One stage of a composition grid. The compounds are reached by name, so the grid can
        /// bind a column per compound without knowing them at compile time.
        /// </summary>
        private sealed class CompositionRow : INotifyPropertyChanged
        {
            private readonly Column _column;
            private readonly int _index;
            private readonly bool _vapor;
            private readonly string _nf;

            public CompositionRow(Column column, int index, bool vapor, string nf)
            {
                _column = column;
                _index = index;
                _vapor = vapor;
                _nf = nf;
            }

            private List<Dictionary<string, Parameter>> Source
            {
                get
                {
                    return _vapor
                        ? _column.InitialEstimates.VapCompositions
                        : _column.InitialEstimates.LiqCompositions;
                }
            }

            public string this[string key]
            {
                get
                {
                    if (key == "Stage") return _column.Stages[_index].Name;

                    var source = Source;
                    if (source == null || _index >= source.Count) return "";
                    if (!source[_index].ContainsKey(key)) return "";

                    return source[_index][key].Value.ToString(_nf, CultureInfo.CurrentCulture);
                }
                set
                {
                    if (key == "Stage") return;

                    var source = Source;
                    if (source == null || _index >= source.Count) return;
                    if (!source[_index].ContainsKey(key)) return;
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;

                    source[_index][key].Value = v;
                    Raise("Item[]");
                }
            }

            public void Refresh()
            {
                Raise("Item[]");
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

            var stack = new StackPanel();

            var options = new AvaloniaEditorPanel();
            options.CreateAndAddDescriptionRow(
                "Check the boxes for the data sets that you want to use as initial estimates to " +
                "solve the column.");

            options.CreateAndAddCheckBoxRow("Temperatures", column.UseTemperatureEstimates,
                (cb, e) => column.UseTemperatureEstimates = cb.IsChecked.GetValueOrDefault());
            options.CreateAndAddCheckBoxRow("Vapor Flows", column.UseVaporFlowEstimates,
                (cb, e) => column.UseVaporFlowEstimates = cb.IsChecked.GetValueOrDefault());
            options.CreateAndAddCheckBoxRow("Liquid Flows", column.UseLiquidFlowEstimates,
                (cb, e) => column.UseLiquidFlowEstimates = cb.IsChecked.GetValueOrDefault());
            options.CreateAndAddCheckBoxRow("Compositions", column.UseCompositionEstimates,
                (cb, e) => column.UseCompositionEstimates = cb.IsChecked.GetValueOrDefault());
            options.CreateAndAddCheckBoxRow("Update Estimates After Solving",
                column.AutoUpdateInitialEstimates,
                (cb, e) => column.AutoUpdateInitialEstimates = cb.IsChecked.GetValueOrDefault());

            stack.Children.Add(options);

            if (column.Stages == null || column.Stages.Count == 0)
            {
                stack.Children.Add(new TextBlock { Text = "The column has no stages yet." });
                return stack;
            }

            // the estimates are rebuilt whenever they do not line up with the current compounds,
            // which is what the Windows editor does before it fills its grids
            EnsureEstimates(column, flowsheet);

            var compounds = flowsheet.SelectedCompounds.Keys.ToList();

            var flowRows = new ObservableCollection<FlowRow>();
            var liquidRows = new ObservableCollection<CompositionRow>();
            var vaporRows = new ObservableCollection<CompositionRow>();

            for (int i = 0; i < column.Stages.Count; i++)
            {
                flowRows.Add(new FlowRow(column, i, su, nf));
                liquidRows.Add(new CompositionRow(column, i, vapor: false, nf: nf));
                vaporRows.Add(new CompositionRow(column, i, vapor: true, nf: nf));
            }

            var tabs = new TabControl { Height = 340 };

            var flows = Grid();
            flows.ItemsSource = flowRows;
            flows.Columns.Add(TextColumn("Stage", "Stage", 1.4, readOnly: true));
            flows.Columns.Add(TextColumn("Temperature (" + su.temperature + ")", "Temperature", 1.2));
            flows.Columns.Add(TextColumn("Vapor Flow (" + su.molarflow + ")", "VaporFlow", 1.2));
            flows.Columns.Add(TextColumn("Liquid Flow (" + su.molarflow + ")", "LiquidFlow", 1.2));

            tabs.Items.Add(new TabItem { Header = "Temperatures and Flows", Content = flows });
            tabs.Items.Add(new TabItem
            {
                Header = "Liquid Compositions",
                Content = CompositionGrid(liquidRows, compounds)
            });
            tabs.Items.Add(new TabItem
            {
                Header = "Vapor Compositions",
                Content = CompositionGrid(vaporRows, compounds)
            });

            stack.Children.Add(tabs);

            var actions = new AvaloniaEditorPanel();
            actions.CreateAndAddButtonRow("Read Estimates from the Last Solution", null, (btn, e) =>
            {
                if (column.Tf == null || column.Tf.Length == 0)
                {
                    flowsheet.ShowMessage("There is no solution to read from. Solve the column first.",
                        IFlowsheet.MessageType.Warning);
                    return;
                }

                ReadFromSolution(column, compounds);

                foreach (var row in flowRows) row.Refresh();
                foreach (var row in liquidRows) row.Refresh();
                foreach (var row in vaporRows) row.Refresh();
            });

            stack.Children.Add(actions);

            return stack;
        }

        /// <summary>Rebuilds the estimates when they do not match the stages or the compounds.</summary>
        private static void EnsureEstimates(Column column, IFlowsheet flowsheet)
        {
            var ie = column.InitialEstimates;

            var stale = ie == null
                        || ie.StageTemps.Count != column.Stages.Count
                        || ie.LiqCompositions.Count != column.Stages.Count
                        || ie.LiqCompositions.Count == 0
                        || ie.LiqCompositions[0].Count != flowsheet.SelectedCompounds.Count;

            if (stale) column.InitialEstimates = column.RebuildEstimates();
        }

        /// <summary>Copies the converged profiles into the estimates, as the Windows button does.</summary>
        private static void ReadFromSolution(Column column, List<string> compounds)
        {
            var ie = column.InitialEstimates;

            for (int i = 0; i < column.Stages.Count; i++)
            {
                if (i < column.Tf.Length) ie.StageTemps[i].Value = column.Tf[i];
                if (i < column.Vf.Length) ie.VapMolarFlows[i].Value = column.Vf[i];
                if (i < column.Lf.Length) ie.LiqMolarFlows[i].Value = column.Lf[i];

                var x = i < column.xf.Count ? column.xf[i] as double[] : null;
                var y = i < column.yf.Count ? column.yf[i] as double[] : null;

                for (int j = 0; j < compounds.Count; j++)
                {
                    if (x != null && j < x.Length && ie.LiqCompositions[i].ContainsKey(compounds[j]))
                        ie.LiqCompositions[i][compounds[j]].Value = x[j];
                    if (y != null && j < y.Length && ie.VapCompositions[i].ContainsKey(compounds[j]))
                        ie.VapCompositions[i][compounds[j]].Value = y[j];
                }
            }
        }

        private static Control CompositionGrid(ObservableCollection<CompositionRow> rows,
                                               List<string> compounds)
        {
            var grid = Grid();
            grid.ItemsSource = rows;
            grid.Columns.Add(TextColumn("Stage", "[Stage]", 1.4, readOnly: true));

            foreach (var compound in compounds)
                grid.Columns.Add(TextColumn(compound, "[" + compound + "]", 1.0));

            return grid;
        }

        private static DataGrid Grid()
        {
            return new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
            };
        }

        private static DataGridTextColumn TextColumn(string header, string path, double width,
                                                     bool readOnly = false)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path) { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay },
                IsReadOnly = readOnly,
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            };
        }

    }

}
