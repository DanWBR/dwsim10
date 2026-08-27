using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Thickness = Avalonia.Thickness;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The frame every unit operation editor of the Windows UI is built on, stacked from top to
    /// bottom:
    ///
    ///   General Info            tag, status, what the object is linked to, active
    ///   Connections             one row per connector
    ///   Calculation Parameters  the property package and whatever the object takes as input
    ///   Results                 what the calculation produced
    ///   Notes                   the annotation
    ///
    /// An editor that needs more than that (curves, matrices, a notebook of its own) adds its
    /// own groups; everything else fills in the two panels it is handed.
    /// </summary>
    public static class UnitOpEditor
    {

        /// <summary>Builds the editor of an object from the sections it fills in.</summary>
        /// <param name="input">Fills the calculation parameters, after the property package.</param>
        /// <param name="results">Fills the results; the group is left out when nothing is added.</param>
        /// <param name="propertyPackage">Shows the property package picker with the parameters.</param>
        /// <param name="extras">Groups appended after the results, for the editors that need them.</param>
        /// <param name="connections">Shows the connections group; off for the objects that
        /// connect their streams from a page of their own, as the column does.</param>
        public static Control Build(ISimulationObject simobj,
                                    Action<AvaloniaEditorPanel> input,
                                    Action<AvaloniaEditorPanel> results = null,
                                    bool propertyPackage = true,
                                    IEnumerable<(string Header, Control Content)> extras = null,
                                    bool connections = true)
        {
            var stack = new StackPanel { Margin = new Thickness(6) };

            stack.Children.Add(Group("General Info", BuildIdentification(simobj)));
            if (connections)
                stack.Children.Add(Group("Connections", AvaloniaTabBuilders.BuildConnections(simobj)));

            var parameters = new AvaloniaEditorPanel();
            if (propertyPackage) AddPropertyPackageRow(simobj, parameters);
            input?.Invoke(parameters);
            if (parameters.Children.Count > 0)
                stack.Children.Add(Group("Calculation Parameters", parameters));

            if (results != null)
            {
                var panel = new AvaloniaEditorPanel();
                results(panel);
                if (panel.Children.Count > 0) stack.Children.Add(Group("Results", panel));
            }

            if (extras != null)
            {
                foreach (var extra in extras)
                {
                    if (extra.Content == null) continue;
                    stack.Children.Add(Group(extra.Header, extra.Content));
                }
            }

            stack.Children.Add(Group("Notes", BuildAnnotation(simobj)));

            // the panels commit as they are edited; the host arms this after the editor is in
            // the tree so the events Avalonia raises while building do not solve the flowsheet
            return new ScrollViewer { Content = stack };
        }

        /// <summary>A group box, as the Windows editors frame each section.</summary>
        public static Control Group(string header, Control content)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = header,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(2, 2, 0, 6)
            });
            stack.Children.Add(content);

            var border = new Border { Child = stack, Margin = new Thickness(0, 0, 0, 8) };
            border.Classes.Add("group");
            return border;
        }

        // ---------------------------------------------------------------------
        // General Info
        // ---------------------------------------------------------------------

        private static Control BuildIdentification(ISimulationObject simobj)
        {
            var flowsheet = simobj.GetFlowsheet();
            var panel = new AvaloniaEditorPanel();

            var tag = panel.CreateAndAddStringEditorRow("Object", simobj.GraphicObject.Tag, null);
            tag.LostFocus += (s, e) =>
            {
                if (tag.Text == simobj.GraphicObject.Tag) return;
                flowsheet.RegisterSnapshot(SnapshotType.ObjectLayout);
                simobj.GraphicObject.Tag = tag.Text;
                flowsheet.UpdateInterface();
            };
            tag.KeyDown += (s, e) =>
            {
                if (e.Key != global::Avalonia.Input.Key.Enter) return;
                flowsheet.RegisterSnapshot(SnapshotType.ObjectLayout);
                simobj.GraphicObject.Tag = tag.Text;
                flowsheet.UpdateInterface();
                flowsheet.UpdateOpenEditForms();
            };

            var status = StatusOf(simobj);
            var statusRow = panel.CreateAndAddTwoLabelsRow("Status", status.Text);
            statusRow.Foreground = new SolidColorBrush(status.Color);

            // Only when something is actually attached: a row reading "Linked to  -" is a line of
            // height spent saying nothing, and most objects have no logical block on them.
            var linkedTo = LinkedTo(simobj);
            if (!string.IsNullOrEmpty(linkedTo))
                panel.CreateAndAddTwoLabelsRow("Linked to", linkedTo);

            panel.CreateAndAddCheckBoxRow("Active", simobj.GraphicObject.Active, (cb, e) =>
            {
                simobj.GraphicObject.Active = cb.IsChecked.GetValueOrDefault();
                flowsheet.UpdateInterface();
                flowsheet.UpdateOpenEditForms();
            });

            return panel;
        }

        /// <summary>The status line of the Windows editors, with the colour they paint it.</summary>
        private static (string Text, Color Color) StatusOf(ISimulationObject simobj)
        {
            if (simobj.Calculated)
                return ("Calculated (" + simobj.LastUpdated + ")", Colors.Blue);

            if (!simobj.GraphicObject.Active)
                return ("Inactive", Colors.Gray);

            if (!string.IsNullOrEmpty(simobj.ErrorMessage))
                return (simobj.ErrorMessage, Colors.Red);

            return ("Not calculated", Colors.Black);
        }

        /// <summary>The spec or adjust block driving this object, as the Windows editors show.</summary>
        private static string LinkedTo(ISimulationObject simobj)
        {
            var flowsheet = simobj.GetFlowsheet();

            try
            {
                if (simobj.IsSpecAttached && flowsheet.SimulationObjects.ContainsKey(simobj.AttachedSpecId))
                    return flowsheet.SimulationObjects[simobj.AttachedSpecId].GraphicObject.Tag;

                if (simobj.IsAdjustAttached && flowsheet.SimulationObjects.ContainsKey(simobj.AttachedAdjustId))
                    return flowsheet.SimulationObjects[simobj.AttachedAdjustId].GraphicObject.Tag;
            }
            catch (Exception) { }

            return "";
        }

        private static void AddPropertyPackageRow(ISimulationObject simobj, AvaloniaEditorPanel panel)
        {
            var flowsheet = simobj.GetFlowsheet();
            var packages = flowsheet.PropertyPackages.Values.ToList();
            if (packages.Count == 0) return;

            var names = packages.Select(x => x.Tag).ToList();
            var selected = names.IndexOf(simobj.PropertyPackage == null ? "" : simobj.PropertyPackage.Tag);

            panel.CreateAndAddDropDownRow("Property Package", names, selected, (dd, e) =>
            {
                if (dd.SelectedIndex < 0 || dd.SelectedIndex >= packages.Count) return;
                simobj.PropertyPackage = packages[dd.SelectedIndex];
            });
        }

        // ---------------------------------------------------------------------
        // Notes
        // ---------------------------------------------------------------------

        private static Control BuildAnnotation(ISimulationObject simobj)
        {
            var text = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 110,
                Text = simobj.Annotation == null ? "" : simobj.Annotation.ToString()
            };

            text.TextChanged += (s, e) => simobj.Annotation = text.Text;
            return text;
        }

    }

    /// <summary>
    /// The value row of the Windows editors: the number on the left, the unit picker on the
    /// right. Picking another unit means "the number I typed is in this unit", so the value is
    /// converted into the unit system of the simulation and committed, which is what the
    /// Windows editors do on the unit combo.
    /// </summary>
    public static class UnitOpEditorRows
    {

        /// <summary>A value row and its unit picker, so a calculation mode can switch it off.</summary>
        public sealed class ValueRow
        {
            public TextBox Value;
            public ComboBox Unit;

            public bool IsEnabled
            {
                set
                {
                    if (Value != null) Value.IsEnabled = value;
                    if (Unit != null) Unit.IsEnabled = value;
                }
            }
        }

        public static ValueRow CreateAndAddValueUnitRow(this AvaloniaEditorPanel panel,
                                                       ISimulationObject simobj,
                                                       string label,
                                                       UnitOfMeasure measure,
                                                       double siValue,
                                                       Action<double> commit,
                                                       bool enabled = true)
        {
            var flowsheet = simobj.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            var unit = UnitOf(su, measure);
            var units = Units(su, measure);

            var value = new TextBox
            {
                Text = cv.ConvertFromSI(unit, siValue).ToString(nf, CultureInfo.CurrentCulture),
                TextAlignment = TextAlignment.Right,
                IsEnabled = enabled,
                MinWidth = 110
            };

            var picker = new ComboBox
            {
                ItemsSource = units,
                SelectedIndex = Math.Max(0, units.IndexOf(unit)),
                IsEnabled = enabled,
                MinWidth = 90,
                Margin = new Thickness(4, 0, 0, 0)
            };

            void Commit()
            {
                if (!TryParse(value.Text, out var typed)) return;
                var from = picker.SelectedItem as string ?? unit;

                flowsheet.RegisterSnapshot(SnapshotType.ObjectData, simobj);
                commit(cv.ConvertToSI(from, typed));

                // back to the unit of the simulation, showing what was stored
                value.Text = cv.ConvertFromSI(unit, cv.ConvertToSI(from, typed)).ToString(nf, CultureInfo.CurrentCulture);
                picker.SelectedIndex = Math.Max(0, units.IndexOf(unit));

                panel.OnAfterEdit?.Invoke();
            }

            // blue while the text parses, red while it does not, as the Windows editors colour it
            value.TextChanged += (s, e) => value.Foreground =
                new SolidColorBrush(TryParse(value.Text, out _) ? Colors.Blue : Colors.Red);

            value.KeyDown += (s, e) =>
            {
                if (e.Key != global::Avalonia.Input.Key.Enter) return;
                Commit();
                e.Handled = true;
            };

            value.LostFocus += (s, e) => Commit();
            picker.SelectionChanged += (s, e) => { if (picker.IsFocused) Commit(); };

            var row = new DockPanel();
            DockPanel.SetDock(picker, global::Avalonia.Controls.Dock.Right);
            row.Children.Add(picker);
            row.Children.Add(value);

            panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, row));
            return new ValueRow { Value = value, Unit = picker };
        }

        /// <summary>Read-only result row, with the unit spelled out as the Windows editors do.</summary>
        public static void CreateAndAddResultRow(this AvaloniaEditorPanel panel,
                                                 ISimulationObject simobj,
                                                 string label,
                                                 UnitOfMeasure measure,
                                                 double? siValue)
        {
            var flowsheet = simobj.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;
            var unit = UnitOf(su, measure);

            var text = siValue.HasValue && !double.IsNaN(siValue.Value)
                ? cv.ConvertFromSI(unit, siValue.Value).ToString(nf, CultureInfo.CurrentCulture)
                : "";

            panel.CreateAndAddTwoLabelsRow(label + (string.IsNullOrEmpty(unit) ? "" : " (" + unit + ")"), text);
        }

        public static string UnitOf(IUnitsOfMeasure su, UnitOfMeasure measure)
        {
            try { return su.GetCurrentUnits(measure) ?? ""; }
            catch (Exception) { return ""; }
        }

        public static List<string> Units(IUnitsOfMeasure su, UnitOfMeasure measure)
        {
            try { return su.GetUnitSet(measure) ?? new List<string>(); }
            catch (Exception) { return new List<string>(); }
        }

        public static bool TryParse(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

    }

}
