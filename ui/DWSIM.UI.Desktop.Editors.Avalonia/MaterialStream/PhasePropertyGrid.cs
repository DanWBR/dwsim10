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
    /// Property table of one phase of a material stream: property, value and units, one row each,
    /// in the order the WinForms PopulatePropGrid uses.
    ///
    /// The material stream editor shows one per phase (mixture, vapour, overall liquid, liquid 1,
    /// liquid 2, solid) and they differ only in which phase they read, so the grid takes the phase.
    /// Everything here is read-only: these are solver outputs.
    /// </summary>
    public sealed class PhasePropertyGrid : DataGrid
    {

        public sealed class Row
        {
            public string Property { get; set; } = "";
            public string Value { get; set; } = "";
            public string Units { get; set; } = "";
        }

        /// <summary>A property of the phase: label, value in SI and the unit it is displayed in.</summary>
        private sealed class Definition
        {
            public string Label = "";
            public Func<IPhase, double?> Value = _ => null;
            public Func<IUnitsOfMeasure, string> Units = _ => "";
            /// <summary>Rows the mixture pseudo-phase does not have.</summary>
            public bool SkipForMixture;
        }

        private readonly ObservableCollection<Row> _rows = new();
        private readonly IUnitsOfMeasure _su;
        private readonly string _nf;
        /// <summary>
        /// Avalonia looks the control theme up by the exact type of the control, so a DataGrid
        /// subclass gets no template and renders as an empty rectangle. This points the lookup
        /// back at DataGrid.
        /// </summary>
        protected override System.Type StyleKeyOverride => typeof(DataGrid);


        public PhasePropertyGrid(IUnitsOfMeasure su, string numberFormat)
        {
            _su = su;
            _nf = numberFormat;

            AutoGenerateColumns = false;
            CanUserSortColumns = false;
            IsReadOnly = true;
            ItemsSource = _rows;

            // the fill weights of the WinForms property grids
            AddColumn("Property", nameof(Row.Property), 60);
            AddColumn("Value", nameof(Row.Value), 40);
            AddColumn("Units", nameof(Row.Units), 30);
        }

        private void AddColumn(string header, string path, double width)
        {
            Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            });
        }

        /// <summary>Fills the grid from a phase. Call again after a solve.</summary>
        public void Populate(IPhase? phase)
        {
            _rows.Clear();
            if (phase == null) return;

            var isMixture = phase.Name == "Mixture";

            foreach (var d in Definitions)
            {
                if (isMixture && d.SkipForMixture) continue;

                double? si;
                try { si = d.Value(phase); }
                catch (Exception) { continue; }

                if (!si.HasValue || double.IsNaN(si.Value) || double.IsInfinity(si.Value)) continue;

                var units = d.Units(_su);
                var value = string.IsNullOrEmpty(units) ? si.Value : cv.ConvertFromSI(units, si.Value);

                _rows.Add(new Row
                {
                    Property = d.Label,
                    Value = value.ToString(_nf, CultureInfo.CurrentCulture),
                    Units = units
                });
            }

            // the WinForms grid sorts itself by property name once it is filled
            var sorted = _rows.OrderBy(x => x.Property, StringComparer.CurrentCultureIgnoreCase).ToList();
            _rows.Clear();
            foreach (var row in sorted) _rows.Add(row);
        }

        private static readonly Definition[] Definitions =
        {
            new() { Label = "Volumetric Flow", Units = u => u.volumetricFlow,
                    Value = p => p.Properties.density.GetValueOrDefault() == 0.0
                        ? null
                        : p.Properties.massflow.GetValueOrDefault() / p.Properties.density.GetValueOrDefault() },
            new() { Label = "Mass Flow", Units = u => u.massflow, Value = p => p.Properties.massflow },
            new() { Label = "Molar Flow", Units = u => u.molarflow, Value = p => p.Properties.molarflow },

            new() { Label = "Phase Mole Fraction", Value = p => p.Properties.molarfraction, SkipForMixture = true },
            new() { Label = "Phase Mass Fraction", Value = p => p.Properties.massfraction, SkipForMixture = true },
            new() { Label = "Compressibility Factor", Value = p => p.Properties.compressibilityFactor, SkipForMixture = true },

            new() { Label = "Specific Enthalpy", Units = u => u.enthalpy, Value = p => p.Properties.enthalpy },
            new() { Label = "Molar Enthalpy", Units = u => u.molar_enthalpy, Value = p => p.Properties.molar_enthalpy },
            new() { Label = "Specific Entropy", Units = u => u.entropy, Value = p => p.Properties.entropy },
            new() { Label = "Molar Entropy", Units = u => u.molar_entropy, Value = p => p.Properties.molar_entropy },
            new() { Label = "Internal Energy", Units = u => u.enthalpy, Value = p => p.Properties.internal_energy },
            new() { Label = "Molar Internal Energy", Units = u => u.molar_enthalpy, Value = p => p.Properties.molar_internal_energy },
            new() { Label = "Gibbs Energy", Units = u => u.enthalpy, Value = p => p.Properties.gibbs_free_energy },
            new() { Label = "Molar Gibbs Energy", Units = u => u.molar_enthalpy, Value = p => p.Properties.molar_gibbs_free_energy },
            new() { Label = "Helmholtz Energy", Units = u => u.enthalpy, Value = p => p.Properties.helmholtz_energy },
            new() { Label = "Molar Helmholtz Energy", Units = u => u.molar_enthalpy, Value = p => p.Properties.molar_helmholtz_energy },

            new() { Label = "Molar Weight", Units = u => u.molecularWeight, Value = p => p.Properties.molecularWeight },
            new() { Label = "Density", Units = u => u.density, Value = p => p.Properties.density },
            new() { Label = "Heat Capacity (Cp)", Units = u => u.heatCapacityCp, Value = p => p.Properties.heatCapacityCp },
            new() { Label = "Heat Capacity Ratio (Cp/Cv)",
                    Value = p => p.Properties.heatCapacityCv.GetValueOrDefault() == 0.0
                        ? null
                        : p.Properties.heatCapacityCp.GetValueOrDefault() / p.Properties.heatCapacityCv.GetValueOrDefault() },
            new() { Label = "Ideal Gas Heat Capacity (Cp)", Units = u => u.heatCapacityCp, Value = p => p.Properties.idealGasHeatCapacityCp },
            new() { Label = "Ideal Gas Heat Capacity Ratio", Value = p => p.Properties.idealGasHeatCapacityRatio },
            new() { Label = "Thermal Conductivity", Units = u => u.thermalConductivity, Value = p => p.Properties.thermalConductivity },
            new() { Label = "Isothermal Compressibility", Units = u => u.compressibility, Value = p => p.Properties.isothermal_compressibility },
            new() { Label = "Bulk Modulus", Units = u => u.pressure, Value = p => p.Properties.bulk_modulus },
            new() { Label = "Speed of Sound", Units = u => u.speedOfSound, Value = p => p.Properties.speedOfSound },
            new() { Label = "Joule-Thomson Coefficient", Units = u => u.jouleThomsonCoefficient, Value = p => p.Properties.jouleThomsonCoefficient },
            new() { Label = "Kinematic Viscosity", Units = u => u.cinematic_viscosity, Value = p => p.Properties.kinematic_viscosity },
            new() { Label = "Dynamic Viscosity", Units = u => u.viscosity, Value = p => p.Properties.viscosity },
            new() { Label = "Phase Volumetric Fraction", Value = p => p.Properties.volumetricFraction },

            new() { Label = "Bubble Pressure", Units = u => u.pressure, Value = p => p.Properties.bubblePressure },
            new() { Label = "Dew Pressure", Units = u => u.pressure, Value = p => p.Properties.dewPressure },
            new() { Label = "Bubble Temperature", Units = u => u.temperature, Value = p => p.Properties.bubbleTemperature },
            new() { Label = "Dew Temperature", Units = u => u.temperature, Value = p => p.Properties.dewTemperature },
            new() { Label = "Surface Tension", Units = u => u.surfaceTension, Value = p => p.Properties.surfaceTension }
        };

    }

}
