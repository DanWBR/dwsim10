using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using DWSIM.ExtensionMethods;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Avalonia.Reactions
{
    /// <summary>
    /// The Components &amp; Stoichiometry grid shared by every reaction editor, mirroring the
    /// DataGridView at the top of the classic WinForms reaction forms: one row per selected
    /// compound, with read-only Name / Molar Weight / ΔHf columns, an Include checkbox, a mutually
    /// exclusive Base-reactant checkbox, an editable stoichiometric coefficient, and (for kinetic
    /// reactions) Direct/Reverse reaction-order columns.
    /// </summary>
    public sealed class StoichiometryGrid : DataGrid
    {
        public sealed class Row : INotifyPropertyChanged
        {
            private bool _include;
            private bool _isBase;
            private string _coeff;
            private string _directOrder;
            private string _reverseOrder;

            public Row(ICompoundConstantProperties cp)
            {
                Compound = cp.Name;
                Formula = cp.Formula;
                MolarWeight = cp.Molar_Weight;
                Hf = cp.IG_Enthalpy_of_Formation_25C;
                _coeff = "0";
                _directOrder = "0";
                _reverseOrder = "0";
            }

            public string Compound { get; }
            public string Formula { get; }
            public double MolarWeight { get; }
            public double Hf { get; }

            public string MolarWeightText => MolarWeight.ToString("G6", CultureInfo.CurrentCulture);
            public string HfText => Hf.ToString("G6", CultureInfo.CurrentCulture);

            public bool Include
            {
                get => _include;
                set { _include = value; Raise(nameof(Include)); }
            }

            public bool IsBase
            {
                get => _isBase;
                set { _isBase = value; Raise(nameof(IsBase)); }
            }

            /// <summary>Negative = reactant, positive = product, zero = inert (as in WinForms).</summary>
            public string Coefficient
            {
                get => _coeff;
                set { _coeff = value; Raise(nameof(Coefficient)); }
            }

            public string DirectOrder
            {
                get => _directOrder;
                set { _directOrder = value; Raise(nameof(DirectOrder)); }
            }

            public string ReverseOrder
            {
                get => _reverseOrder;
                set { _reverseOrder = value; Raise(nameof(ReverseOrder)); }
            }

            public double CoefficientValue => _coeff.IsValidDoubleFlexible() ? _coeff.ToDoubleFromCurrent() : 0.0;
            public double DirectOrderValue => _directOrder.IsValidDoubleFlexible() ? _directOrder.ToDoubleFromCurrent() : 0.0;
            public double ReverseOrderValue => _reverseOrder.IsValidDoubleFlexible() ? _reverseOrder.ToDoubleFromCurrent() : 0.0;

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private readonly ObservableCollection<Row> _rows = new();
        private bool _suppressExclusivity;

        /// <summary>
        /// Avalonia looks the control theme up by the exact type, so a DataGrid subclass renders as
        /// an empty rectangle without this. Points the lookup back at DataGrid (as CompoundGrid does).
        /// </summary>
        protected override System.Type StyleKeyOverride => typeof(DataGrid);

        /// <summary>Raised after any edit (coefficient, include, base, order) so the host can
        /// recompute the equation, reaction heat and mass balance.</summary>
        public event Action Edited;

        public StoichiometryGrid(bool showOrders)
        {
            AutoGenerateColumns = false;
            CanUserSortColumns = false;
            CanUserResizeColumns = true;
            HeadersVisibility = DataGridHeadersVisibility.Column;
            GridLinesVisibility = DataGridGridLinesVisibility.All;
            ItemsSource = _rows;

            Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(Row.Compound)) { Mode = BindingMode.OneWay },
                IsReadOnly = true,
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            Columns.Add(new DataGridTextColumn
            {
                Header = "Molar Weight",
                Binding = new Binding(nameof(Row.MolarWeightText)) { Mode = BindingMode.OneWay },
                IsReadOnly = true,
                Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
            });
            Columns.Add(new DataGridTextColumn
            {
                Header = "ΔHf (kJ/kg)",
                Binding = new Binding(nameof(Row.HfText)) { Mode = BindingMode.OneWay },
                IsReadOnly = true,
                Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
            });
            // A DataGridCheckBoxColumn only reacts once the cell is in edit mode, so it costs two
            // clicks and reads as disabled; a CheckBox inside a template column takes the first click.
            Columns.Add(CheckBoxColumn("Include", nameof(Row.Include)));
            Columns.Add(CheckBoxColumn("Base", nameof(Row.IsBase)));
            Columns.Add(new DataGridTextColumn
            {
                Header = "Stoich. Coeff.",
                Binding = new Binding(nameof(Row.Coefficient)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });
            if (showOrders)
            {
                Columns.Add(new DataGridTextColumn
                {
                    Header = "Direct Order",
                    Binding = new Binding(nameof(Row.DirectOrder)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                    Width = new DataGridLength(1.1, DataGridLengthUnitType.Star)
                });
                Columns.Add(new DataGridTextColumn
                {
                    Header = "Reverse Order",
                    Binding = new Binding(nameof(Row.ReverseOrder)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                    Width = new DataGridLength(1.1, DataGridLengthUnitType.Star)
                });
            }

            CellEditEnded += (_, _) => Edited?.Invoke();
        }

        private static DataGridTemplateColumn CheckBoxColumn(string header, string property) => new()
        {
            Header = header,
            Width = new DataGridLength(0.8, DataGridLengthUnitType.Star),
            CellTemplate = new FuncDataTemplate<Row>((_, _) =>
            {
                var cb = new CheckBox { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                cb.Bind(CheckBox.IsCheckedProperty, new Binding(property) { Mode = BindingMode.TwoWay });
                return cb;
            })
        };

        /// <summary>Fills one row per selected compound, then applies an existing reaction's state.</summary>
        public void Populate(IFlowsheet flowsheet, IReaction existing)
        {
            _rows.Clear();
            foreach (var cp in flowsheet.SelectedCompounds.Values)
            {
                var row = new Row(cp);
                if (existing != null && existing.Components.TryGetValue(cp.Name, out var sb))
                {
                    row.Include = true;
                    row.IsBase = sb.IsBaseReactant;
                    row.Coefficient = sb.StoichCoeff.ToString("G6", CultureInfo.CurrentCulture);
                    row.DirectOrder = sb.DirectOrder.ToString("G6", CultureInfo.CurrentCulture);
                    row.ReverseOrder = sb.ReverseOrder.ToString("G6", CultureInfo.CurrentCulture);
                }
                row.PropertyChanged += OnRowChanged;
                _rows.Add(row);
            }
        }

        private void OnRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Row.IsBase) && !_suppressExclusivity && sender is Row changed && changed.IsBase)
            {
                // the base reactant is unique across rows, as the WinForms editors enforce
                _suppressExclusivity = true;
                foreach (var r in _rows)
                    if (!ReferenceEquals(r, changed) && r.IsBase) r.IsBase = false;
                _suppressExclusivity = false;
            }
            Edited?.Invoke();
        }

        /// <summary>The compound rows the user marked as taking part in the reaction.</summary>
        public IEnumerable<Row> IncludedRows => _rows.Where(r => r.Include);

        /// <summary>The row marked as base reactant, or null.</summary>
        public Row BaseRow => _rows.FirstOrDefault(r => r.Include && r.IsBase);

        public IReadOnlyList<Row> Rows => _rows;
    }
}
