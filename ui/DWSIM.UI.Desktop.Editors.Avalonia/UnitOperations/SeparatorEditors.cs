using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using ComponentSeparationSpec = DWSIM.UnitOperations.UnitOperations.Auxiliary.ComponentSeparationSpec;
using ComponentSeparator = DWSIM.UnitOperations.UnitOperations.ComponentSeparator;
using Filter = DWSIM.UnitOperations.UnitOperations.Filter;
using OrificePlate = DWSIM.UnitOperations.UnitOperations.OrificePlate;
using ReliefValve = DWSIM.UnitOperations.UnitOperations.ReliefValve;
using SeparationSpec = DWSIM.UnitOperations.UnitOperations.Auxiliary.SeparationSpec;
using SolidsSeparator = DWSIM.UnitOperations.UnitOperations.SolidsSeparator;
using Vessel = DWSIM.UnitOperations.UnitOperations.Vessel;
using Thickness = Avalonia.Thickness;
using StringResources = DWSIM.UI.Shared.Avalonia.StringArrays;
using Valve = DWSIM.UnitOperations.UnitOperations.Valve;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Vessel (separator) editor, as the Windows EditingForm_Vessel lays it out: the flash mode
    /// and its overrides above, and the vessel geometry that the sizing reads below.
    /// </summary>
    public static class VesselEditor
    {

        /// <summary>The flash modes, in the order the Windows combo lists them.</summary>
        private static readonly List<string> CalculationModes = new List<string>
        {
            "Adiabatic", "Legacy", "Heating/Cooling Isothermic", "Heating/Cooling Isobaric"
        };

        /// <summary>
        /// How the outlet pressure is taken from the inlets. The combo does not follow the enum,
        /// which orders them Average, Maximum, Minimum.
        /// </summary>
        private static readonly Vessel.PressureBehavior[] PressureOrder =
        {
            Vessel.PressureBehavior.Minimum,
            Vessel.PressureBehavior.Average,
            Vessel.PressureBehavior.Maximum
        };

        public static Control Build(Vessel vessel)
        {
            return UnitOpEditor.Build(vessel,
                input: panel => BuildParameters(vessel, panel),
                extras: new[] { ("Size", BuildSize(vessel)) });
        }

        private static void BuildParameters(Vessel vessel, AvaloniaEditorPanel panel)
        {
            var nf = vessel.GetFlowsheet().FlowsheetOptions.NumberFormat;

            UnitOpEditorRows.ValueRow temperature = null, pressure = null, duty = null;
            CheckBox overrideT = null, overrideP = null;

            void Apply()
            {
                // only the legacy mode flashes at a temperature and pressure of its own, and only
                // the two heating/cooling modes take a duty
                var legacy = vessel.CalculationMode == Vessel.CalculationModes.Legacy;

                if (overrideT != null) overrideT.IsEnabled = legacy;
                if (overrideP != null) overrideP.IsEnabled = legacy;

                if (temperature != null) temperature.IsEnabled = legacy && vessel.OverrideT;
                if (pressure != null) pressure.IsEnabled = legacy && vessel.OverrideP;

                if (duty != null)
                    duty.IsEnabled = vessel.CalculationMode == Vessel.CalculationModes.HeatingCoolingIsothermic
                                  || vessel.CalculationMode == Vessel.CalculationModes.HeatingCoolingIsobaric;
            }

            panel.CreateAndAddDropDownRow("Calculation Mode", CalculationModes,
                (int)vessel.CalculationMode, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    vessel.CalculationMode = (Vessel.CalculationModes)dd.SelectedIndex;
                    Apply();
                    panel.OnAfterEdit?.Invoke();
                });

            panel.CreateAndAddDropDownRow("Outlet Pressure Calculation",
                new List<string> { "Inlet Minimum", "Inlet Average", "Inlet Maximum" },
                Math.Max(0, Array.IndexOf(PressureOrder, vessel.PressureCalculation)), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    vessel.PressureCalculation = PressureOrder[dd.SelectedIndex];
                });

            overrideT = panel.CreateAndAddCheckBoxRow("Override Sep. Temperature", vessel.OverrideT,
                (cb, e) => { vessel.OverrideT = cb.IsChecked.GetValueOrDefault(); Apply(); });

            temperature = panel.CreateAndAddValueUnitRow(vessel, "Separation Temperature",
                UnitOfMeasure.temperature, vessel.FlashTemperature, v => vessel.FlashTemperature = v);

            overrideP = panel.CreateAndAddCheckBoxRow("Override Sep. Pressure", vessel.OverrideP,
                (cb, e) => { vessel.OverrideP = cb.IsChecked.GetValueOrDefault(); Apply(); });

            pressure = panel.CreateAndAddValueUnitRow(vessel, "Separation Pressure",
                UnitOfMeasure.pressure, vessel.FlashPressure, v => vessel.FlashPressure = v);

            duty = panel.CreateAndAddValueUnitRow(vessel, "Heating/Cooling Amount",
                UnitOfMeasure.heatflow, vessel.HeatingCoolingAmount.GetValueOrDefault(),
                v => vessel.HeatingCoolingAmount = v);

            panel.CreateAndAddDescriptionRow("Leave blank to read from Heat-In stream if heating.");

            Apply();
        }

        private static Control BuildSize(Vessel vessel)
        {
            var panel = new AvaloniaEditorPanel();
            var su = vessel.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem;
            var nf = vessel.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddDropDownRow("Orientation",
                new List<string> { "Vertical", "Horizontal" },
                vessel.SelectedEquipmentType == "Horizontal" ? 1 : 0, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    vessel.SelectedEquipmentType = dd.SelectedIndex == 1 ? "Horizontal" : "Vertical";
                });

            panel.CreateAndAddDropDownRow("Head Type", Vessel.HeadTypes,
                Math.Max(0, Vessel.HeadTypes.IndexOf(vessel.HeadType)), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    vessel.HeadType = Vessel.HeadTypes[dd.SelectedIndex];
                });

            panel.CreateAndAddDropDownRow("Wall Material", Vessel.MaterialTypes,
                Math.Max(0, Vessel.MaterialTypes.IndexOf(vessel.WallMaterial)), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    vessel.WallMaterial = Vessel.MaterialTypes[dd.SelectedIndex];
                });

            panel.CreateAndAddValueUnitRow(vessel, "Wall Thickness", UnitOfMeasure.thickness,
                vessel.WallThickness, v => vessel.WallThickness = v);

            // the diameter and the height come out of the sizing, so they are shown as they stand
            if (vessel.Dimensions != null && vessel.Dimensions.Count >= 2)
            {
                panel.CreateAndAddTwoLabelsRow("Diameter",
                    vessel.Dimensions[0].Value.ToString(nf, CultureInfo.CurrentCulture) + " " +
                    su.GetCurrentUnits(vessel.Dimensions[0].GetUnitsType()));

                panel.CreateAndAddTwoLabelsRow("Height",
                    vessel.Dimensions[1].Value.ToString(nf, CultureInfo.CurrentCulture) + " " +
                    su.GetCurrentUnits(vessel.Dimensions[1].GetUnitsType()));
            }

            return panel;
        }

    }

    /// <summary>
    /// Compound separator editor: which outlet the factors are written for, and one separation
    /// specification per compound, as the Windows grid lists them.
    /// </summary>
    public static class CompoundSeparatorEditor
    {

        private sealed class SpecRow : INotifyPropertyChanged
        {
            private readonly ComponentSeparationSpec _spec;
            private readonly List<string> _types;

            public SpecRow(ComponentSeparationSpec spec, List<string> types)
            {
                _spec = spec;
                _types = types;
            }

            public string Compound { get { return _spec.ComponentID; } }

            public string SpecType
            {
                get
                {
                    var index = (int)_spec.SepSpec;
                    return index >= 0 && index < _types.Count ? _types[index] : "";
                }
                set
                {
                    var index = _types.IndexOf(value);
                    if (index < 0) return;
                    _spec.SepSpec = (SeparationSpec)index;
                    Raise("SpecType");
                }
            }

            public string Value
            {
                get { return _spec.SpecValue.ToString("G6", CultureInfo.CurrentCulture); }
                set
                {
                    if (!UnitOpEditorRows.TryParse(value, out var v)) return;
                    _spec.SpecValue = v;
                    Raise("Value");
                }
            }

            public string Unit
            {
                get { return _spec.SpecUnit; }
                set { _spec.SpecUnit = value; Raise("Unit"); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public static Control Build(ComponentSeparator separator)
        {
            return UnitOpEditor.Build(separator,
                input: panel =>
                {
                    panel.CreateAndAddDropDownRow("Separation Factors specified for",
                        new List<string> { "Outlet Stream 1", "Outlet Stream 2" },
                        separator.SpecifiedStreamIndex, (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            separator.SpecifiedStreamIndex = (byte)dd.SelectedIndex;
                        });
                },
                extras: new[] { ("Separation Factors", BuildSpecs(separator)) });
        }

        private static Control BuildSpecs(ComponentSeparator separator)
        {
            var flowsheet = separator.GetFlowsheet();

            // the dictionary fills in lazily, so every selected compound is seeded a row
            foreach (ICompoundConstantProperties compound in flowsheet.SelectedCompounds.Values)
            {
                if (separator.ComponentSepSpecs.ContainsKey(compound.Name)) continue;

                separator.ComponentSepSpecs.Add(compound.Name,
                    new ComponentSeparationSpec(compound.Name, SeparationSpec.PercentInletMassFlow, 0.0f, "%"));
            }

            var types = StringResources.csepspectype().ToList();
            var units = StringResources.cspecunit().ToList();

            var rows = new ObservableCollection<SpecRow>();

            foreach (var spec in separator.ComponentSepSpecs.Values)
            {
                if (!flowsheet.SelectedCompounds.ContainsKey(spec.ComponentID)) continue;
                rows.Add(new SpecRow(spec, types));
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

            grid.Columns.Add(GridColumns.Text("Compound", "Compound", 1.4, readOnly: true));
            grid.Columns.Add(GridColumns.Combo("Spec Type", "SpecType", types, 1.8));
            grid.Columns.Add(GridColumns.Text("Value", "Value", 1.0));
            grid.Columns.Add(GridColumns.Combo("Unit", "Unit", units, 1.0));

            return grid;
        }

    }

    /// <summary>Solids separator editor: the two separation efficiencies it takes.</summary>
    public static class SolidsSeparatorEditor
    {

        public static Control Build(SolidsSeparator separator)
        {
            return UnitOpEditor.Build(separator,
                input: panel =>
                {
                    var nf = separator.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    panel.CreateAndAddTextBoxRow(nf, "Solids Separation Efficiency (%)",
                        separator.SeparationEfficiency,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) separator.SeparationEfficiency = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Liquids Separation Efficiency (%)",
                        separator.LiquidSeparationEfficiency,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) separator.LiquidSeparationEfficiency = v; });
                },
                propertyPackage: false);
        }

    }

    /// <summary>
    /// Filter editor, as the Windows EditingForm_Filter lays it out: sizing computes the area from
    /// a pressure drop, evaluation computes the pressure drop from the area.
    /// </summary>
    public static class FilterEditor
    {

        public static Control Build(Filter filter)
        {
            return UnitOpEditor.Build(filter,
                input: panel =>
                {
                    var nf = filter.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    UnitOpEditorRows.ValueRow pressureDrop = null, area = null;

                    void Apply()
                    {
                        var sizing = filter.CalcMode == Filter.CalculationMode.Design;

                        // sizing reads the pressure drop and writes the area, evaluation the other way around
                        if (pressureDrop != null) pressureDrop.IsEnabled = sizing;
                        if (area != null) area.IsEnabled = !sizing;
                    }

                    panel.CreateAndAddDropDownRow("Calculation Mode",
                        new List<string> { "Sizing", "Evaluation" },
                        filter.CalcMode == Filter.CalculationMode.Design ? 0 : 1, (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            filter.CalcMode = dd.SelectedIndex == 0
                                ? Filter.CalculationMode.Design
                                : Filter.CalculationMode.Simulation;
                            Apply();
                            panel.OnAfterEdit?.Invoke();
                        });

                    pressureDrop = panel.CreateAndAddValueUnitRow(filter, "Pressure Drop",
                        UnitOfMeasure.deltaP, filter.PressureDrop, v => filter.PressureDrop = v);

                    area = panel.CreateAndAddValueUnitRow(filter, "Total Filter Area",
                        UnitOfMeasure.area, filter.TotalFilterArea, v => filter.TotalFilterArea = v);

                    panel.CreateAndAddTextBoxRow(nf, "Cake Humidity (%)", filter.CakeRelativeHumidity,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) filter.CakeRelativeHumidity = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Submerged Fraction", filter.SubmergedAreaFraction,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) filter.SubmergedAreaFraction = v; });

                    panel.CreateAndAddValueUnitRow(filter, "Cycle Time", UnitOfMeasure.time,
                        filter.FilterCycleTime, v => filter.FilterCycleTime = v);

                    panel.CreateAndAddValueUnitRow(filter, "Cake Resistance", UnitOfMeasure.cakeresistance,
                        filter.SpecificCakeResistance, v => filter.SpecificCakeResistance = v);

                    panel.CreateAndAddValueUnitRow(filter, "Medium Resistance", UnitOfMeasure.mediumresistance,
                        filter.FilterMediumResistance, v => filter.FilterMediumResistance = v);

                    Apply();
                });
        }

    }

    /// <summary>
    /// Orifice plate editor: the two diameters and the tappings, with the pressure drops and the
    /// temperature change the correlation produces.
    /// </summary>
    public static class OrificePlateEditor
    {

        public static Control Build(OrificePlate plate)
        {
            return UnitOpEditor.Build(plate,
                input: panel =>
                {
                    var nf = plate.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    panel.CreateAndAddDropDownRow("Pressure Tappings",
                        new List<string> { "Corner", "Flange", "Radius" },
                        (int)plate.OrifType, (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            plate.OrifType = (OrificePlate.OrificeType)dd.SelectedIndex;
                        });

                    panel.CreateAndAddValueUnitRow(plate, "Orifice Diameter", UnitOfMeasure.diameter,
                        plate.OrificeDiameter, v => plate.OrificeDiameter = v);

                    panel.CreateAndAddValueUnitRow(plate, "Internal Pipe Diameter", UnitOfMeasure.diameter,
                        plate.InternalPipeDiameter, v => plate.InternalPipeDiameter = v);

                    // beta is the ratio of the two diameters, so the calculation writes it
                    panel.CreateAndAddTwoLabelsRow("Orifice Beta (d/D)",
                        plate.Beta.ToString(nf, CultureInfo.CurrentCulture));

                    panel.CreateAndAddTextBoxRow(nf, "Correction Factor", plate.CorrectionFactor,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) plate.CorrectionFactor = v; });
                },
                results: panel =>
                {
                    panel.CreateAndAddResultRow(plate, "Orifice Pressure Drop", UnitOfMeasure.deltaP,
                        plate.OrificePressureDrop);
                    panel.CreateAndAddResultRow(plate, "Overall Pressure Drop", UnitOfMeasure.deltaP,
                        plate.OverallPressureDrop);
                    panel.CreateAndAddResultRow(plate, "Temperature Change", UnitOfMeasure.deltaT,
                        plate.DeltaT);
                });
        }

    }

    /// <summary>
    /// Relief valve editor, as the Windows EditingForm_ReliefValve lays it out: the set point and
    /// the discharge coefficients, plus the opening versus Kv relationship the valve follows.
    /// </summary>
    public static class ReliefValveEditor
    {

        /// <summary>The relationship types, in the order the Windows combo lists them.</summary>
        private static readonly List<string> RelationshipTypes = new List<string>
        {
            "Linear", "Equal Percentage", "Quick Opening", "User-Defined Expression", "Data Table"
        };

        private sealed class PointRow : INotifyPropertyChanged
        {
            private readonly ReliefValve _valve;
            private readonly int _index;

            public PointRow(ReliefValve valve, int index)
            {
                _valve = valve;
                _index = index;
            }

            public string Opening
            {
                get { return Read(_valve.OpeningKvRelDataTableX); }
                set { Write(_valve.OpeningKvRelDataTableX, value, "Opening"); }
            }

            public string Kv
            {
                get { return Read(_valve.OpeningKvRelDataTableY); }
                set { Write(_valve.OpeningKvRelDataTableY, value, "Kv"); }
            }

            private string Read(List<double> values)
            {
                if (values == null || _index >= values.Count) return "";
                return values[_index].ToString("G6", CultureInfo.CurrentCulture);
            }

            private void Write(List<double> values, string text, string property)
            {
                if (values == null || _index >= values.Count) return;
                if (!UnitOpEditorRows.TryParse(text, out var v)) return;
                values[_index] = v;
                Raise(property);
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void Raise(string name)
            {
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public static Control Build(ReliefValve valve)
        {
            return UnitOpEditor.Build(valve,
                input: panel =>
                {
                    var nf = valve.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    panel.CreateAndAddValueUnitRow(valve, "Set-Point Pressure", UnitOfMeasure.pressure,
                        valve.SetPointPressure, v => valve.SetPointPressure = v);

                    panel.CreateAndAddValueUnitRow(valve, "Fully-Opened Pressure", UnitOfMeasure.pressure,
                        valve.FullyOpenedPressure, v => valve.FullyOpenedPressure = v);

                    panel.CreateAndAddTextBoxRow(nf, "Discharge Coefficient", valve.DischargeCoefficient,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) valve.DischargeCoefficient = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Back Pressure Coefficient", valve.BackPressureCoefficient,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) valve.BackPressureCoefficient = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Viscosity Coefficient", valve.ViscosityCoefficient,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) valve.ViscosityCoefficient = v; });

                    var area = panel.CreateAndAddValueUnitRow(valve, "Valve Size (Orifice Area)",
                        UnitOfMeasure.area, valve.OrificeArea, v => valve.OrificeArea = v);

                    // the standard sizes write the area, in square inches, as the Windows list does
                    var sizes = new List<string> { "(select to apply)" };
                    foreach (var size in (List<string>)ReliefValve.StandardOrificeAreas) sizes.Add(size);

                    panel.CreateAndAddDropDownRow("Standard Sizes", sizes, 0, (dd, e) =>
                    {
                        if (dd.SelectedIndex <= 0) return;

                        // "D / 0.11 in² / 0.71 cm²": the square inches sit after the letter
                        var parts = sizes[dd.SelectedIndex].Split('/');
                        if (parts.Length < 2) return;

                        var inches = parts[1].Replace("in²", "").Trim();
                        if (!double.TryParse(inches, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)) return;

                        valve.OrificeArea = value * 0.00064516;

                        var su = valve.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem;
                        area.Value.Text = cv.ConvertFromSI(su.area, valve.OrificeArea)
                                            .ToString(nf, CultureInfo.CurrentCulture);
                    });
                },
                extras: new[] { ("Opening / Kv Relationship", BuildRelationship(valve)) });
        }

        private static Control BuildRelationship(ReliefValve valve)
        {
            var stack = new StackPanel();
            var panel = new AvaloniaEditorPanel();
            var nf = valve.GetFlowsheet().FlowsheetOptions.NumberFormat;

            TextBox expression = null;
            TextBox characteristic = null;
            DataGrid table = null;

            void Apply()
            {
                var type = valve.DefinedOpeningKvRelationShipType;

                if (expression != null)
                    expression.IsEnabled = type == Valve.OpeningKvRelationshipType.UserDefined;
                if (table != null)
                    table.IsEnabled = type == Valve.OpeningKvRelationshipType.DataTable;
                if (characteristic != null)
                    characteristic.IsEnabled = type == Valve.OpeningKvRelationshipType.QuickOpening;
            }

            panel.CreateAndAddDropDownRow("Opening/Kv[Cv] rel. type", RelationshipTypes,
                (int)valve.DefinedOpeningKvRelationShipType, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    valve.DefinedOpeningKvRelationShipType = (Valve.OpeningKvRelationshipType)dd.SelectedIndex;
                    Apply();
                });

            characteristic = panel.CreateAndAddTextBoxRow(nf, "Characteristic Parameter",
                valve.CharacteristicParameter,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) valve.CharacteristicParameter = v; });

            expression = panel.CreateAndAddStringEditorRow("Kv[Cv]/Kv[Cv]max (%) = f(OP(%)) expression",
                valve.PercentOpeningVersusPercentKvExpression,
                (tb, e) => valve.PercentOpeningVersusPercentKvExpression = tb.Text ?? "");

            stack.Children.Add(panel);

            var rows = new ObservableCollection<PointRow>();
            var count = Math.Min(valve.OpeningKvRelDataTableX.Count, valve.OpeningKvRelDataTableY.Count);
            for (int i = 0; i < count; i++) rows.Add(new PointRow(valve, i));

            table = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 200
            };

            table.Columns.Add(GridColumns.Text("Opening (%)", "Opening", 1.0));
            table.Columns.Add(GridColumns.Text("Kv/Kvmax (%)", "Kv", 1.0));

            stack.Children.Add(table);

            Apply();

            return stack;
        }

    }

    /// <summary>The grid columns these editors share.</summary>
    internal static class GridColumns
    {

        internal static DataGridTextColumn Text(string header, string path, double width,
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

        /// <summary>A column of pickers, with the combo living in the cell template itself.</summary>
        internal static DataGridTemplateColumn Combo(string header, string path,
                                                     List<string> items, double width)
        {
            return new DataGridTemplateColumn
            {
                Header = header,
                Width = new DataGridLength(width, DataGridLengthUnitType.Star),
                CellTemplate = new global::Avalonia.Controls.Templates.FuncDataTemplate<object>(
                    (item, scope) =>
                    {
                        var combo = new ComboBox { ItemsSource = items, MinWidth = 80 };
                        combo.Bind(ComboBox.SelectedItemProperty,
                                   new Binding(path) { Mode = BindingMode.TwoWay });
                        return combo;
                    },
                    supportsRecycling: true)
            };
        }

    }

}
