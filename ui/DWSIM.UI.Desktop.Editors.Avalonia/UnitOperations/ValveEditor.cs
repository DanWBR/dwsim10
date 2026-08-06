using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Thickness = Avalonia.Thickness;
using Valve = DWSIM.UnitOperations.UnitOperations.Valve;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Valve editor, as the Windows EditingForm_Valve lays it out: the calculation type, the
    /// pressure specs, the flow coefficient and the opening/Kv relationship with its data table.
    /// </summary>
    public static class ValveEditor
    {

        private static readonly string[] Modes =
        {
            "Outlet Pressure",
            "Pressure Drop",
            "Liquid Service Kv/Cv (Deprecated)",
            "Gas Service Kv/Cv (Deprecated)",
            "Steam Service Kv/Cv",
            "General Service Kv/Cv (IEC 60534)"
        };

        private static readonly Valve.CalculationMode[] ModeOrder =
        {
            Valve.CalculationMode.OutletPressure,
            Valve.CalculationMode.DeltaP,
            Valve.CalculationMode.Kv_Liquid,
            Valve.CalculationMode.Kv_Gas,
            Valve.CalculationMode.Kv_Steam,
            Valve.CalculationMode.Kv_General
        };

        private static readonly string[] RelationshipTypes =
        {
            "Linear",
            "Equal Percentage",
            "Quick Opening",
            "User-Defined Expression",
            "Data Table"
        };

        public static Control Build(Valve valve)
        {
            return UnitOpEditor.Build(valve, panel =>
            {
                var nf = valve.GetFlowsheet().FlowsheetOptions.NumberFormat;

                UnitOpEditorRows.ValueRow pressureDrop = null, outletPressure = null;
                TextBox kv = null, opening = null, expression = null, characteristic = null;
                ComboBox coefficient = null, relationship = null;
                Control table = null;

                bool IsKvMode()
                {
                    var mode = valve.CalcMode;
                    return mode != Valve.CalculationMode.OutletPressure && mode != Valve.CalculationMode.DeltaP;
                }

                void ApplyMode()
                {
                    if (outletPressure != null)
                        outletPressure.IsEnabled = valve.CalcMode == Valve.CalculationMode.OutletPressure;
                    if (pressureDrop != null)
                        pressureDrop.IsEnabled = valve.CalcMode == Valve.CalculationMode.DeltaP;

                    var kvmode = IsKvMode();
                    if (kv != null) kv.IsEnabled = kvmode;
                    if (coefficient != null) coefficient.IsEnabled = kvmode;

                    // the relationship block only lives while the relationship is enabled
                    var related = valve.EnableOpeningKvRelationship;
                    if (opening != null) opening.IsEnabled = kvmode && related;
                    if (relationship != null) relationship.IsEnabled = related;

                    var type = valve.DefinedOpeningKvRelationShipType;
                    if (expression != null)
                        expression.IsEnabled = related && type == Valve.OpeningKvRelationshipType.UserDefined;
                    if (characteristic != null)
                        characteristic.IsEnabled = related && type == Valve.OpeningKvRelationshipType.QuickOpening;
                    if (table != null)
                        table.IsEnabled = related && type == Valve.OpeningKvRelationshipType.DataTable;
                }

                panel.CreateAndAddDropDownRow("Calculation Type", new List<string>(Modes),
                    Math.Max(0, Array.IndexOf(ModeOrder, valve.CalcMode)), (dd, e) =>
                    {
                        if (dd.SelectedIndex < 0 || dd.SelectedIndex >= ModeOrder.Length) return;
                        valve.CalcMode = ModeOrder[dd.SelectedIndex];
                        ApplyMode();
                        panel.OnAfterEdit?.Invoke();
                    });

                pressureDrop = panel.CreateAndAddValueUnitRow(valve, "Pressure Drop",
                    UnitOfMeasure.deltaP, valve.DeltaP.GetValueOrDefault(), v => valve.DeltaP = v);

                outletPressure = panel.CreateAndAddValueUnitRow(valve, "Outlet Pressure",
                    UnitOfMeasure.pressure, valve.OutletPressure.GetValueOrDefault(),
                    v => valve.OutletPressure = v);

                coefficient = panel.CreateAndAddDropDownRow("Flow Coefficient Type",
                    new List<string> { "Kv", "Cv" }, (int)valve.FlowCoefficient, (dd, e) =>
                    {
                        valve.FlowCoefficient = dd.SelectedIndex == 1
                            ? Valve.FlowCoefficientType.Cv
                            : Valve.FlowCoefficientType.Kv;
                        panel.OnAfterEdit?.Invoke();
                    });

                kv = panel.CreateAndAddTextBoxRow(nf, "Kv[Cv](max) (IEC 60534)", valve.Kv,
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) valve.Kv = v; });

                panel.CreateAndAddButtonRow("Calculate Kv from the current stream", null, (btn, e) =>
                {
                    valve.CalculateKv();
                    valve.GetFlowsheet().UpdateOpenEditForms();
                });

                panel.CreateAndAddCheckBoxRow(
                    "Use Opening (%) versus Kv[Cv]/Kv[Cv]max (%) relationship",
                    valve.EnableOpeningKvRelationship, (cb, e) =>
                    {
                        valve.EnableOpeningKvRelationship = cb.IsChecked.GetValueOrDefault();
                        ApplyMode();
                        panel.OnAfterEdit?.Invoke();
                    });

                opening = panel.CreateAndAddTextBoxRow(nf, "Valve Opening (%)", valve.OpeningPct,
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) valve.OpeningPct = v; });

                relationship = panel.CreateAndAddDropDownRow("Opening/Kv[Cv] rel. type",
                    new List<string>(RelationshipTypes),
                    (int)valve.DefinedOpeningKvRelationShipType, (dd, e) =>
                    {
                        valve.DefinedOpeningKvRelationShipType = (Valve.OpeningKvRelationshipType)dd.SelectedIndex;
                        ApplyMode();
                        panel.OnAfterEdit?.Invoke();
                    });

                expression = panel.CreateAndAddStringEditorRow("Kv[Cv]/Kv[Cv]max (%) = f(OP(%))",
                    valve.PercentOpeningVersusPercentKvExpression,
                    (tb, e) => valve.PercentOpeningVersusPercentKvExpression = tb.Text);

                characteristic = panel.CreateAndAddTextBoxRow(nf, "Characteristic Parameter",
                    valve.CharacteristicParameter,
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) valve.CharacteristicParameter = v; });

                table = BuildDataTable(valve);
                panel.Children.Add(UnitOpEditor.Group("Data Table", table));

                ApplyMode();
            });
        }

        // ---------------------------------------------------------------------
        // Opening versus Kv data table
        // ---------------------------------------------------------------------

        private sealed class TableRow : INotifyPropertyChanged
        {
            private double _opening, _kv;

            public TableRow(double opening, double kv) { _opening = opening; _kv = kv; }

            public string Opening
            {
                get { return _opening.ToString("G6", CultureInfo.CurrentCulture); }
                set { if (UnitOpEditorRows.TryParse(value, out var v)) { _opening = v; Raise(); } }
            }

            public string Kv
            {
                get { return _kv.ToString("G6", CultureInfo.CurrentCulture); }
                set { if (UnitOpEditorRows.TryParse(value, out var v)) { _kv = v; Raise(); } }
            }

            public double OpeningValue { get { return _opening; } }
            public double KvValue { get { return _kv; } }

            public event Action Edited;
            public event PropertyChangedEventHandler PropertyChanged;

            private void Raise()
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Opening"));
                if (Edited != null) Edited();
            }
        }

        private static Control BuildDataTable(Valve valve)
        {
            var rows = new ObservableCollection<TableRow>();

            Action writeBack = () =>
            {
                valve.OpeningKvRelDataTableX = rows.Select(r => r.OpeningValue).ToList();
                valve.OpeningKvRelDataTableY = rows.Select(r => r.KvValue).ToList();
            };

            var x = valve.OpeningKvRelDataTableX ?? new List<double>();
            var y = valve.OpeningKvRelDataTableY ?? new List<double>();

            for (int i = 0; i < x.Count; i++)
            {
                var row = new TableRow(x[i], i < y.Count ? y[i] : 0.0);
                row.Edited += () => writeBack();
                rows.Add(row);
            }

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 180
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Opening (%)",
                Binding = new Binding("Opening") { Mode = BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Kv/Kvmax (%)",
                Binding = new Binding("Kv") { Mode = BindingMode.TwoWay },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            grid.CellEditEnded += (s, e) => writeBack();

            var add = new Button { Content = "Add Row", Margin = new Thickness(0, 0, 6, 0) };
            add.Classes.Add("panel");
            add.Click += (s, e) =>
            {
                var row = new TableRow(0.0, 0.0);
                row.Edited += () => writeBack();
                rows.Add(row);
                writeBack();
            };

            var remove = new Button { Content = "Remove Row" };
            remove.Classes.Add("panel");
            remove.Click += (s, e) =>
            {
                var selected = grid.SelectedItem as TableRow;
                if (selected == null) return;
                rows.Remove(selected);
                writeBack();
            };

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };
            actions.Children.Add(add);
            actions.Children.Add(remove);

            var host = new DockPanel();
            DockPanel.SetDock(actions, global::Avalonia.Controls.Dock.Bottom);
            host.Children.Add(actions);
            host.Children.Add(grid);
            return host;
        }

    }

}
