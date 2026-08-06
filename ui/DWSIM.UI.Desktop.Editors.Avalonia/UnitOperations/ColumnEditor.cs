using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using AbsorptionColumn = DWSIM.UnitOperations.UnitOperations.AbsorptionColumn;
using Column = DWSIM.UnitOperations.UnitOperations.Column;
using ColumnSpec = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSpec;
using DistillationColumn = DWSIM.UnitOperations.UnitOperations.DistillationColumn;
using Thickness = Avalonia.Thickness;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Rigorous column editor, following the notebook of the Windows EditingForm_Column:
    /// General, Specifications (condenser and reboiler), Stages, Connections, Estimates and
    /// Results. Absorbers lose the two specification tabs, as they have neither end.
    /// </summary>
    public static class ColumnEditor
    {

        /// <summary>The spec types in the order the Windows combos list them.</summary>
        private static readonly string[] SpecTypes =
        {
            "Heat Load",
            "Product Molar Flow",
            "Compound Molar flow in Product Stream",
            "Product Mass Flow",
            "Compound Mass Flow in Product Stream",
            "Compound Fraction in Product Stream",
            "Compound Recovery",
            "Reflux Ratio",
            "Temperature",
            "Feed Recovery"
        };

        /// <summary>Molar flow units, in the order the Windows spec combos list them.</summary>
        private static readonly string[] MolarFlowUnits =
        {
            "mol/s", "lbmol/h", "mol/h", "mol/d", "kmol/s", "kmol/h", "kmol/d",
            "m3/d @ BR", "m3/d @ NC", "m3/d @ CNTP", "m3/d @ SC", "m3/d @ 0 C, 1 atm",
            "m3/d @ 15.56 C, 1 atm", "m3/d @ 20 C, 1 atm", "ft3/d @ 60 F, 14.7 psia",
            "ft3/d @ 0 C, 1 atm"
        };

        private static readonly string[] MassFlowUnits =
        {
            "g/s", "lbm/h", "kg/s", "kg/h", "kg/d", "kg/min", "lb/min", "lb/s"
        };

        /// <summary>The units each specification type accepts, as the Windows combos are refilled.</summary>
        private static List<string> SpecUnits(ColumnSpec.SpecType type)
        {
            switch (type)
            {
                case ColumnSpec.SpecType.Component_Fraction:
                    return new List<string> { "Molar", "Mass" };
                case ColumnSpec.SpecType.Component_Mass_Flow_Rate:
                case ColumnSpec.SpecType.Product_Mass_Flow_Rate:
                    return new List<string>(MassFlowUnits);
                case ColumnSpec.SpecType.Component_Molar_Flow_Rate:
                case ColumnSpec.SpecType.Product_Molar_Flow_Rate:
                    return new List<string>(MolarFlowUnits);
                case ColumnSpec.SpecType.Component_Recovery:
                    return new List<string> { "% M/M", "% W/W" };
                case ColumnSpec.SpecType.Heat_Duty:
                    return new List<string> { "kW", "kcal/h", "BTU/h", "BTU/s", "cal/s", "HP", "kJ/h", "kJ/d", "MW", "W" };
                case ColumnSpec.SpecType.Temperature:
                    return new List<string> { "K", "R", "C", "F" };
                case ColumnSpec.SpecType.Feed_Recovery:
                    return new List<string> { "%" };
                default:
                    return new List<string> { "" };
            }
        }

        public static Control Build(Column column)
        {
            return UnitOpEditor.Build(column,
                input: panel =>
                {
                    var tabs = new TabControl { Margin = new Thickness(0, 4, 0, 0) };

                    tabs.Items.Add(new TabItem { Header = "General", Content = BuildGeneral(column) });

                    if (column is DistillationColumn distillation)
                    {
                        var specs = new TabControl();
                        specs.Items.Add(new TabItem { Header = "Condenser", Content = BuildCondenser(distillation) });
                        specs.Items.Add(new TabItem { Header = "Reboiler", Content = BuildReboiler(distillation) });
                        tabs.Items.Add(new TabItem { Header = "Specifications", Content = specs });
                    }

                    tabs.Items.Add(new TabItem { Header = "Stages", Content = ColumnStagesEditor.Build(column) });
                    tabs.Items.Add(new TabItem { Header = "Connections", Content = BuildConnections(column) });
                    tabs.Items.Add(new TabItem { Header = "Estimates", Content = BuildEstimates(column) });

                    panel.Children.Add(tabs);
                },
                results: panel => BuildResults(column, panel),
                propertyPackage: false,
                connections: false);
        }

        // ---------------------------------------------------------------------
        // General
        // ---------------------------------------------------------------------

        private static Control BuildGeneral(Column column)
        {
            var panel = new AvaloniaEditorPanel();
            var flowsheet = column.GetFlowsheet();
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            if (column is AbsorptionColumn absorber)
            {
                panel.CreateAndAddDropDownRow("Absorber Operating Mode",
                    new List<string> { "Absorber", "Liquid-Liquid Extractor" },
                    (int)absorber.OperationMode, (dd, e) =>
                    {
                        if (dd.SelectedIndex < 0) return;
                        absorber.OperationMode = (AbsorptionColumn.OpMode)dd.SelectedIndex;
                        panel.OnAfterEdit?.Invoke();
                    });
            }

            panel.CreateAndAddTextBoxRow(nf, "Number of Stages", column.NumberOfStages, (tb, e) =>
            {
                if (!UnitOpEditorRows.TryParse(tb.Text, out var v)) return;
                var stages = (int)v;
                if (stages < 3 || stages == column.NumberOfStages) return;
                column.SetNumberOfStages(stages);
                flowsheet.UpdateOpenEditForms();
            });

            if (column.Stages != null && column.Stages.Count > 0)
            {
                panel.CreateAndAddValueUnitRow(column, "Condenser/Top Pressure", UnitOfMeasure.pressure,
                    column.Stages[0].P, v => column.Stages[0].P = v);
            }

            panel.CreateAndAddValueUnitRow(column, "Column Pressure Drop", UnitOfMeasure.deltaP,
                double.IsNaN(column.ColumnPressureDrop) ? 0.0 : column.ColumnPressureDrop,
                v => column.ColumnPressureDrop = v);

            panel.CreateAndAddValueUnitRow(column, "Tray Spacing (Sizing)", UnitOfMeasure.distance,
                column.TraySpacing, v => column.TraySpacing = v);

            panel.CreateAndAddTextBoxRow(nf, "Maximum Number of Iterations", column.MaxIterations,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) column.MaxIterations = (int)v; });

            // the Windows form writes one box into both tolerances
            panel.CreateAndAddTextBoxRow(nf, "Convergence Tolerance", column.ExternalLoopTolerance,
                (tb, e) =>
                {
                    if (!UnitOpEditorRows.TryParse(tb.Text, out var v)) return;
                    column.ExternalLoopTolerance = v;
                    column.InternalLoopTolerance = v;
                });

            AddPropertyPackageRow(column, panel);

            var solvers = SolverNames(column);
            panel.CreateAndAddDropDownRow("Steady-State Column Solver", solvers,
                Math.Max(0, solvers.IndexOf(column.SolvingMethodName ?? "")), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    column.SolvingMethodName = solvers[dd.SelectedIndex];
                    panel.OnAfterEdit?.Invoke();
                });

            var providers = ProviderNames();
            panel.CreateAndAddDropDownRow("Steady-State Initial Estimates Provider", providers,
                Math.Max(0, providers.IndexOf(column.InitialEstimatesProvider ?? "")), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    column.InitialEstimatesProvider = providers[dd.SelectedIndex];
                });

            panel.CreateAndAddCheckBoxRow("Generate Convergence Report",
                column.CreateSolverConvergengeReport,
                (cb, e) => column.CreateSolverConvergengeReport = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddButtonRow("Test Convergence with Current Settings", null, (btn, e) =>
            {
                try
                {
                    column.TestConvergence();
                    flowsheet.ShowMessage("The column converged with the current settings.",
                        IFlowsheet.MessageType.Information);
                }
                catch (Exception ex)
                {
                    flowsheet.ShowMessage("The column did not converge: " + ex.Message,
                        IFlowsheet.MessageType.GeneralError);
                }
            });

            panel.CreateAndAddDescriptionRow(
                "Tests current solver settings without updating the product streams.");

            if (column is DistillationColumn dc)
            {
                panel.CreateAndAddButtonRow("Convert to Complex Dynamic Column", null, (btn, e) =>
                {
                    try
                    {
                        dc.ConvertToComplex();
                        flowsheet.UpdateInterface();
                        flowsheet.UpdateOpenEditForms();
                    }
                    catch (Exception ex)
                    {
                        flowsheet.ShowMessage("Could not convert the column: " + ex.Message,
                            IFlowsheet.MessageType.GeneralError);
                    }
                });
            }

            return panel;
        }

        private static void AddPropertyPackageRow(Column column, AvaloniaEditorPanel panel)
        {
            var flowsheet = column.GetFlowsheet();
            var packages = flowsheet.PropertyPackages.Values.ToList();
            if (packages.Count == 0) return;

            var names = packages.Select(x => x.Tag).ToList();
            var selected = names.IndexOf(column.PropertyPackage == null ? "" : column.PropertyPackage.Tag);

            panel.CreateAndAddDropDownRow("Property Package", names, selected, (dd, e) =>
            {
                if (dd.SelectedIndex < 0 || dd.SelectedIndex >= packages.Count) return;
                column.PropertyPackage = packages[dd.SelectedIndex];
            });
        }

        /// <summary>The solvers the Windows form offers, per column type, plus the external ones.</summary>
        private static List<string> SolverNames(Column column)
        {
            var names = column is AbsorptionColumn
                ? new List<string>
                {
                    "Burningham-Otto (Sum Rates)",
                    "Napthali-Sandholm (Simultaneous Correction)"
                }
                : new List<string>
                {
                    "Wang-Henke (Bubble Point)",
                    "Napthali-Sandholm (Simultaneous Correction)",
                    "Modified Wang-Henke (Bubble Point)"
                };

            try { names.AddRange(Column.ExternalColumnSolvers.Keys); }
            catch (Exception) { }

            return names;
        }

        private static List<string> ProviderNames()
        {
            var names = new List<string>
            {
                "Internal (Default)",
                "Internal 2 (Experimental)",
                "Internal 3 (Robust)"
            };

            try { names.AddRange(Column.ExternalInitialEstimatesProviders.Keys); }
            catch (Exception) { }

            return names;
        }

        // ---------------------------------------------------------------------
        // Condenser and reboiler specifications
        // ---------------------------------------------------------------------

        private static Control BuildCondenser(DistillationColumn column)
        {
            var panel = new AvaloniaEditorPanel();

            UnitOpEditorRows.ValueRow vaporFlow = null, subcooling = null;
            ComboBox condenserType = null, compound = null;
            TextBox specValue = null;

            void Apply()
            {
                var live = !column.ReboiledAbsorber;

                if (condenserType != null) condenserType.IsEnabled = live;
                if (compound != null) compound.IsEnabled = live && NeedsCompound(column.Specs["C"].SType);
                if (specValue != null) specValue.IsEnabled = live;

                if (vaporFlow != null)
                    vaporFlow.IsEnabled = live && column.CondenserType == Column.condtype.Partial_Condenser;
                if (subcooling != null)
                    subcooling.IsEnabled = live && column.CondenserType == Column.condtype.Total_Condenser;
            }

            panel.CreateAndAddCheckBoxRow("No Condenser (Reboiled Absorber)", column.ReboiledAbsorber,
                (cb, e) => { column.ReboiledAbsorber = cb.IsChecked.GetValueOrDefault(); Apply(); });

            condenserType = panel.CreateAndAddDropDownRow("Condenser Type",
                new List<string> { "Total", "Partial", "Full Reflux" },
                (int)column.CondenserType, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    column.CondenserType = (Column.condtype)dd.SelectedIndex;
                    Apply();
                    panel.OnAfterEdit?.Invoke();
                });

            panel.CreateAndAddValueUnitRow(column, "Condenser Pressure Drop", UnitOfMeasure.deltaP,
                column.CondenserDeltaP, v => column.CondenserDeltaP = v);

            AddSpecRows(column, panel, "C", ref compound, ref specValue, Apply);

            vaporFlow = AddStoredUnitRow(column, panel, "Vapor Product Flow Rate",
                new List<string>(MolarFlowUnits),
                () => column.VaporFlowRateUnit, u => column.VaporFlowRateUnit = u,
                () => column.VaporFlowRate, v => column.VaporFlowRate = v);

            subcooling = panel.CreateAndAddValueUnitRow(column, "Total Condenser Subcooling",
                UnitOfMeasure.deltaT, column.TotalCondenserSubcoolingDeltaT,
                v => column.TotalCondenserSubcoolingDeltaT = v);

            Apply();
            return panel;
        }

        private static Control BuildReboiler(DistillationColumn column)
        {
            var panel = new AvaloniaEditorPanel();

            ComboBox compound = null;
            TextBox specValue = null;

            void Apply()
            {
                var live = !column.RefluxedAbsorber;
                if (compound != null) compound.IsEnabled = live && NeedsCompound(column.Specs["R"].SType);
                if (specValue != null) specValue.IsEnabled = live;
            }

            panel.CreateAndAddCheckBoxRow("No Reboiler (Refluxed Absorber)", column.RefluxedAbsorber,
                (cb, e) => { column.RefluxedAbsorber = cb.IsChecked.GetValueOrDefault(); Apply(); });

            AddSpecRows(column, panel, "R", ref compound, ref specValue, Apply, reboiler: true);

            Apply();
            return panel;
        }

        /// <summary>
        /// The specification block both ends share: type, compound and the value with the unit
        /// it is written in. The value is stored as typed, in the picked unit, which is what the
        /// solver reads from the spec.
        /// </summary>
        private static void AddSpecRows(Column column, AvaloniaEditorPanel panel, string key,
                                        ref ComboBox compound, ref TextBox specValue,
                                        Action apply, bool reboiler = false)
        {
            var nf = column.GetFlowsheet().FlowsheetOptions.NumberFormat;
            var spec = column.Specs[key];

            var types = new List<string>(SpecTypes);
            if (reboiler) types[(int)ColumnSpec.SpecType.Stream_Ratio] = "Boil-Up Ratio";

            var units = SpecUnits(spec.SType);
            var unitPicker = new ComboBox
            {
                ItemsSource = units,
                SelectedIndex = Math.Max(0, units.IndexOf(spec.SpecUnit ?? "")),
                MinWidth = 90,
                Margin = new Thickness(4, 0, 0, 0)
            };

            unitPicker.SelectionChanged += (s, e) =>
            {
                if (unitPicker.SelectedItem is string picked) spec.SpecUnit = picked;
            };

            panel.CreateAndAddDropDownRow("Specification", types, (int)spec.SType, (dd, e) =>
            {
                if (dd.SelectedIndex < 0) return;
                spec.SType = (ColumnSpec.SpecType)dd.SelectedIndex;

                // the unit list belongs to the specification type, so it is refilled with it
                var refilled = SpecUnits(spec.SType);
                unitPicker.ItemsSource = refilled;
                unitPicker.SelectedIndex = 0;
                spec.SpecUnit = refilled[0];

                apply();
                panel.OnAfterEdit?.Invoke();
            });

            var compounds = column.GetFlowsheet().SelectedCompounds.Keys.ToList();
            compound = panel.CreateAndAddDropDownRow("Compound", compounds,
                Math.Max(0, compounds.IndexOf(spec.ComponentID ?? "")), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    spec.ComponentID = compounds[dd.SelectedIndex];
                });

            specValue = new TextBox
            {
                Text = spec.SpecValue.ToString(nf, CultureInfo.CurrentCulture),
                TextAlignment = global::Avalonia.Media.TextAlignment.Right,
                MinWidth = 110
            };

            var box = specValue;
            void Commit()
            {
                if (!UnitOpEditorRows.TryParse(box.Text, out var v)) return;
                spec.SpecValue = v;
                panel.OnAfterEdit?.Invoke();
            }

            specValue.TextChanged += (s, e) => box.Foreground = new global::Avalonia.Media.SolidColorBrush(
                UnitOpEditorRows.TryParse(box.Text, out _)
                    ? global::Avalonia.Media.Colors.Blue
                    : global::Avalonia.Media.Colors.Red);

            specValue.KeyDown += (s, e) =>
            {
                if (e.Key != global::Avalonia.Input.Key.Enter) return;
                Commit();
                e.Handled = true;
            };

            specValue.LostFocus += (s, e) => Commit();

            var row = new DockPanel();
            DockPanel.SetDock(unitPicker, global::Avalonia.Controls.Dock.Right);
            row.Children.Add(unitPicker);
            row.Children.Add(specValue);

            panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow("Specification Value", row));
        }

        /// <summary>
        /// A value whose display unit lives on the object rather than on the unit system, which is
        /// how the condenser vapor flow rate is stored. The value itself is kept in SI.
        /// </summary>
        private static UnitOpEditorRows.ValueRow AddStoredUnitRow(Column column,
                                                                  AvaloniaEditorPanel panel,
                                                                  string label,
                                                                  List<string> units,
                                                                  Func<string> getUnit,
                                                                  Action<string> setUnit,
                                                                  Func<double> getValue,
                                                                  Action<double> setValue)
        {
            var nf = column.GetFlowsheet().FlowsheetOptions.NumberFormat;
            var unit = getUnit();
            if (string.IsNullOrEmpty(unit)) unit = units[0];

            var value = new TextBox
            {
                Text = cv.ConvertFromSI(unit, getValue()).ToString(nf, CultureInfo.CurrentCulture),
                TextAlignment = global::Avalonia.Media.TextAlignment.Right,
                MinWidth = 110
            };

            var picker = new ComboBox
            {
                ItemsSource = units,
                SelectedIndex = Math.Max(0, units.IndexOf(unit)),
                MinWidth = 90,
                Margin = new Thickness(4, 0, 0, 0)
            };

            void Commit()
            {
                if (!UnitOpEditorRows.TryParse(value.Text, out var typed)) return;
                setValue(cv.ConvertToSI(picker.SelectedItem as string ?? unit, typed));
                panel.OnAfterEdit?.Invoke();
            }

            value.TextChanged += (s, e) => value.Foreground = new global::Avalonia.Media.SolidColorBrush(
                UnitOpEditorRows.TryParse(value.Text, out _)
                    ? global::Avalonia.Media.Colors.Blue
                    : global::Avalonia.Media.Colors.Red);

            value.KeyDown += (s, e) =>
            {
                if (e.Key != global::Avalonia.Input.Key.Enter) return;
                Commit();
                e.Handled = true;
            };

            value.LostFocus += (s, e) => Commit();

            // picking another unit re-reads the stored value in it, so the number stays the same quantity
            picker.SelectionChanged += (s, e) =>
            {
                if (!(picker.SelectedItem is string picked)) return;
                setUnit(picked);
                value.Text = cv.ConvertFromSI(picked, getValue()).ToString(nf, CultureInfo.CurrentCulture);
            };

            var row = new DockPanel();
            DockPanel.SetDock(picker, global::Avalonia.Controls.Dock.Right);
            row.Children.Add(picker);
            row.Children.Add(value);

            panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, row));
            return new UnitOpEditorRows.ValueRow { Value = value, Unit = picker };
        }

        /// <summary>The spec types that read a compound, which is when the picker is live.</summary>
        private static bool NeedsCompound(ColumnSpec.SpecType type)
        {
            return type == ColumnSpec.SpecType.Component_Molar_Flow_Rate
                || type == ColumnSpec.SpecType.Component_Mass_Flow_Rate
                || type == ColumnSpec.SpecType.Component_Fraction
                || type == ColumnSpec.SpecType.Component_Recovery;
        }

        // ---------------------------------------------------------------------
        // Connections, estimates and results
        // ---------------------------------------------------------------------

        private static Control BuildConnections(Column column)
        {
            return ColumnConnectionsEditor.Build(column);
        }

        private static Control BuildEstimates(Column column)
        {
            return ColumnEstimatesEditor.Build(column);
        }

        private static void BuildResults(Column column, AvaloniaEditorPanel panel)
        {
            var nf = column.GetFlowsheet().FlowsheetOptions.NumberFormat;

            if (column is DistillationColumn distillation)
            {
                panel.CreateAndAddResultRow(column, "Condenser Duty", UnitOfMeasure.heatflow,
                    distillation.CondenserDuty);
                panel.CreateAndAddResultRow(column, "Reboiler Duty", UnitOfMeasure.heatflow,
                    distillation.ReboilerDuty);

                panel.CreateAndAddTwoLabelsRow("Condenser Specification Value",
                    column.Specs["C"].SpecValue.ToString(nf) + " " + column.Specs["C"].SpecUnit);
                panel.CreateAndAddTwoLabelsRow("Condenser Specification Calculated Value",
                    column.Specs["C"].CalculatedValue.ToString(nf) + " " + column.Specs["C"].SpecUnit);
                panel.CreateAndAddTwoLabelsRow("Reboiler Specification Value",
                    column.Specs["R"].SpecValue.ToString(nf) + " " + column.Specs["R"].SpecUnit);
                panel.CreateAndAddTwoLabelsRow("Reboiler Specification Calculated Value",
                    column.Specs["R"].CalculatedValue.ToString(nf) + " " + column.Specs["R"].SpecUnit);
            }

            panel.CreateAndAddTwoLabelsRow("Iterations Taken", column.ic.ToString());
            panel.CreateAndAddResultRow(column, "Estimated Height", UnitOfMeasure.diameter,
                column.EstimatedHeight);
            panel.CreateAndAddResultRow(column, "Estimated Diameter", UnitOfMeasure.diameter,
                column.EstimatedDiameter);

            panel.CreateAndAddButtonRow("View Temperature, Pressure and Composition Profiles", null,
                (btn, e) => ColumnProfilesViewer.Show(column));

            var properties = panel.CreateAndAddButtonRow("View Properties Profile", null,
                (btn, e) => ShowReport(column, "Properties Profile", column.ColumnPropertiesProfile));
            properties.IsEnabled = column.Calculated;

            var convergence = panel.CreateAndAddButtonRow("View Convergence Report", null,
                (btn, e) => ShowReport(column, "Convergence Report", column.ColumnSolverConvergenceReport));
            convergence.IsEnabled = !string.IsNullOrEmpty(column.ColumnSolverConvergenceReport);
        }

        private static void ShowReport(Column column, string title, string text)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(
                column.GraphicObject.Tag + ": " + title, 800, 600, panel);

            panel.CreateAndAddMultilineMonoSpaceTextBoxRow(text ?? "(empty)", 520, true, null);
            window.Show();
        }

    }

}
