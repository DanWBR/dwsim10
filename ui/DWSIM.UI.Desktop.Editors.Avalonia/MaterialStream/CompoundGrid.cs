using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using DWSIM.Interfaces;
using CompoundAmounts = DWSIM.Thermodynamics.Streams.CompoundAmounts;
using MaterialStream = DWSIM.Thermodynamics.Streams.MaterialStream;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Compound amounts of one phase, on the basis the editor selects: the two column
    /// Compound / Amount table of the WinForms material stream editor.
    ///
    /// Editing writes to the rows only; <see cref="Amounts"/> is what the editor hands to
    /// CompoundAmounts.Apply when the user accepts the changes, which is how the WinForms
    /// editor commits as well.
    /// </summary>
    public sealed class CompoundGrid : DataGrid
    {

        public sealed class Row : INotifyPropertyChanged
        {
            private double _amount;

            public Row(string compound, double amount, string format)
            {
                Compound = compound;
                _amount = amount;
                Format = format;
            }

            public string Compound { get; private set; }
            public string Format { get; set; }

            public double Value { get { return _amount; } }

            /// <summary>The amount as shown and typed, in the number format of the flowsheet.</summary>
            public string Amount
            {
                get { return _amount.ToString(Format, CultureInfo.CurrentCulture); }
                set
                {
                    double parsed;
                    if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed) ||
                        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                    {
                        _amount = parsed;
                    }
                    Raise(nameof(Amount));
                }
            }

            /// <summary>Sets the amount without going through the text.</summary>
            public void Set(double amount)
            {
                _amount = amount;
                Raise(nameof(Amount));
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private readonly ObservableCollection<Row> _rows = new ObservableCollection<Row>();
        private readonly IUnitsOfMeasure _su;
        private readonly string _nf;

        private MaterialStream _stream;
        private IPhase _phase;
        private CompoundAmounts.Basis _basis = CompoundAmounts.Basis.MoleFractions;
        private bool _percentage;

        /// <summary>
        /// Avalonia looks the control theme up by the exact type of the control, so a DataGrid
        /// subclass gets no template and renders as an empty rectangle. This points the lookup
        /// back at DataGrid.
        /// </summary>
        protected override System.Type StyleKeyOverride { get { return typeof(DataGrid); } }

        public CompoundGrid(IUnitsOfMeasure su, string numberFormat, bool editable)
        {
            _su = su;
            _nf = numberFormat;

            AutoGenerateColumns = false;
            CanUserSortColumns = false;
            IsReadOnly = !editable;
            ItemsSource = _rows;

            Columns.Add(new DataGridTextColumn
            {
                Header = "Compound",
                Binding = new Binding(nameof(Row.Compound)) { Mode = BindingMode.OneWay },
                IsReadOnly = true,
                Width = new DataGridLength(60, DataGridLengthUnitType.Star)
            });

            Columns.Add(new DataGridTextColumn
            {
                Header = "Amount",
                Binding = new Binding(nameof(Row.Amount))
                {
                    Mode = editable ? BindingMode.TwoWay : BindingMode.OneWay
                },
                IsReadOnly = !editable,
                Width = new DataGridLength(40, DataGridLengthUnitType.Star)
            });

            CellEditEnded += (s, e) => { if (Edited != null) Edited(); };
        }

        /// <summary>Raised after the user commits a cell, or one of the actions runs.</summary>
        public event Action Edited;

        /// <summary>The basis the amounts are shown on. Setting it refills the grid.</summary>
        public CompoundAmounts.Basis Basis
        {
            get { return _basis; }
            set
            {
                _basis = value;
                Populate(_stream, _phase);
            }
        }

        /// <summary>Shows the fraction bases as percentages, as the WinForms checkbox does.</summary>
        public bool ShowAsPercentage
        {
            get { return _percentage; }
            set
            {
                _percentage = value;
                Populate(_stream, _phase);
            }
        }

        /// <summary>The unit of the current basis, empty for the dimensionless ones.</summary>
        public string Units
        {
            get { return CompoundAmounts.Units(_basis, _su); }
        }

        /// <summary>What the user typed, ready for CompoundAmounts.Apply.</summary>
        public Dictionary<string, double> Amounts
        {
            get { return _rows.ToDictionary(x => x.Compound, x => x.Value); }
        }

        /// <summary>Sum of the amount column, which the editors show as the total.</summary>
        public double Total
        {
            get { return _rows.Sum(x => x.Value); }
        }

        /// <summary>Fills the grid from a phase. Call again after a solve.</summary>
        public void Populate(MaterialStream stream, IPhase phase)
        {
            _stream = stream;
            _phase = phase;
            _rows.Clear();

            if (stream == null || phase == null || phase.Compounds == null) return;

            Dictionary<string, double> amounts;
            try { amounts = CompoundAmounts.Read(stream, phase, _basis, _su, _percentage); }
            catch (Exception) { return; }

            foreach (var item in amounts) _rows.Add(new Row(item.Key, item.Value, _nf));
        }

        // ---------------------------------------------------------------------
        // The actions of the input tab, which work on the column, not the stream
        // ---------------------------------------------------------------------

        public void Normalize()
        {
            var total = Total;
            if (total == 0.0) return;
            foreach (var row in _rows) row.Set(row.Value / total);
            if (Edited != null) Edited();
        }

        public void Equalize()
        {
            if (_rows.Count == 0) return;
            foreach (var row in _rows) row.Set(1.0 / _rows.Count);
            if (Edited != null) Edited();
        }

        public void Erase()
        {
            foreach (var row in _rows) row.Set(0.0);
            if (Edited != null) Edited();
        }

        /// <summary>Fills the selected compound so that the column adds up to one.</summary>
        public void Complete()
        {
            var selected = SelectedItem as Row;
            if (selected == null) return;

            var others = _rows.Where(x => x != selected).Sum(x => x.Value);
            selected.Set(Math.Max(0.0, 1.0 - others));
            if (Edited != null) Edited();
        }

    }

}
