using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using DWSIM.Interfaces;

using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Per-compound property table of one phase: one row per compound and one value, chosen from
    /// the same six the WinForms UpdateCompPropBasis offers.
    ///
    /// The material stream editor shows one per phase (vapour, liquid 1, liquid 2, solid), all
    /// driven by a single selector, so the grid exposes <see cref="Property"/> for the host to
    /// set on every instance at once.
    /// </summary>
    public sealed class CompoundPropertyGrid : DataGrid
    {

        /// <summary>The per-compound properties the grid can show, in the WinForms order.</summary>
        public enum PropertyKind
        {
            FugacityCoefficient = 0,
            LogFugacityCoefficient = 1,
            ActivityCoefficient = 2,
            PartialPressure = 3,
            PartialVolume = 4,
            DiffusionCoefficient = 5
        }

        public static readonly string[] PropertyNames =
        {
            "Fugacity Coefficients",
            "Log Fugacity Coefficients",
            "Activity Coefficients",
            "Partial Pressures",
            "Partial Volumes",
            "Infinite Dilution Diffusion Coefficients"
        };

        public sealed class Row
        {
            public string Compound { get; set; } = "";
            public string Value { get; set; } = "";
        }

        private readonly ObservableCollection<Row> _rows = new ObservableCollection<Row>();
        private readonly IUnitsOfMeasure _su;
        private readonly string _nf;
        private readonly DataGridTextColumn _valueColumn;

        private IPhase _phase;
        private PropertyKind _property = PropertyKind.FugacityCoefficient;
        /// <summary>
        /// Avalonia looks the control theme up by the exact type of the control, so a DataGrid
        /// subclass gets no template and renders as an empty rectangle. This points the lookup
        /// back at DataGrid.
        /// </summary>
        protected override System.Type StyleKeyOverride => typeof(DataGrid);


        public CompoundPropertyGrid(IUnitsOfMeasure su, string numberFormat)
        {
            _su = su;
            _nf = numberFormat;

            AutoGenerateColumns = false;
            CanUserSortColumns = false;
            IsReadOnly = true;
            ItemsSource = _rows;

            Columns.Add(new DataGridTextColumn
            {
                Header = "Compound",
                Binding = new Binding(nameof(Row.Compound)) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(60, DataGridLengthUnitType.Star)
            });

            _valueColumn = new DataGridTextColumn
            {
                Header = PropertyNames[0],
                Binding = new Binding(nameof(Row.Value)) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(40, DataGridLengthUnitType.Star)
            };
            Columns.Add(_valueColumn);
        }

        /// <summary>Display unit of the property being shown, empty when dimensionless.</summary>
        public string CurrentUnits { get { return UnitsOf(_property); } }

        /// <summary>Which property is shown. Setting it refills the value column.</summary>
        public PropertyKind Property
        {
            get { return _property; }
            set
            {
                _property = value;
                Populate(_phase);
            }
        }

        /// <summary>Fills the grid from a phase. Call again after a solve.</summary>
        public void Populate(IPhase phase)
        {
            _phase = phase;
            _rows.Clear();

            var units = UnitsOf(_property);
            _valueColumn.Header = string.IsNullOrEmpty(units) ? "Property" : "Property (" + units + ")";

            if (phase?.Compounds == null) return;

            foreach (var compound in phase.Compounds.Values.OrderBy(x => x.Name))
            {
                double? si;
                try { si = ValueOf(compound, _property); }
                catch (Exception) { continue; }

                if (!si.HasValue || double.IsNaN(si.Value) || double.IsInfinity(si.Value)) continue;

                var value = string.IsNullOrEmpty(units) ? si.Value : cv.ConvertFromSI(units, si.Value);

                _rows.Add(new Row
                {
                    Compound = compound.Name,
                    Value = value.ToString(_nf, CultureInfo.CurrentCulture)
                });
            }
        }

        private static double? ValueOf(ICompound c, PropertyKind kind)
        {
            switch (kind)
            {
                case PropertyKind.FugacityCoefficient:
                    return c.FugacityCoeff;
                case PropertyKind.LogFugacityCoefficient:
                    var phi = c.FugacityCoeff.GetValueOrDefault();
                    return phi > 0.0 ? Math.Log(phi) : (double?)null;
                case PropertyKind.ActivityCoefficient:
                    return c.ActivityCoeff;
                case PropertyKind.PartialPressure:
                    return c.PartialPressure;
                case PropertyKind.PartialVolume:
                    return c.PartialVolume;
                case PropertyKind.DiffusionCoefficient:
                    return c.DiffusionCoefficient;
                default:
                    return null;
            }
        }

        /// <summary>Display units of each property, empty for the dimensionless ones.</summary>
        private string UnitsOf(PropertyKind kind)
        {
            switch (kind)
            {
                case PropertyKind.PartialPressure: return _su.pressure;
                case PropertyKind.PartialVolume: return _su.molar_volume;
                case PropertyKind.DiffusionCoefficient: return _su.diffusivity;
                default: return "";
            }
        }

    }

}
