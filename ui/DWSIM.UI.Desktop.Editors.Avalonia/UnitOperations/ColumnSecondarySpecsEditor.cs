using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;
using DWSIM.Thermodynamics.Utilities.PetroleumProperties;
using Column = DWSIM.UnitOperations.UnitOperations.Column;
using ColumnSecondarySpec = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.ColumnSecondarySpec;
using StreamInformation = DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.StreamInformation;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{
    /// <summary>
    /// Secondary specifications of a distillation column: a property of a product (a distillation
    /// curve point, the flash point, a density, or any stream property) met by moving the condenser or
    /// reboiler specification or a side draw rate.
    /// </summary>
    public static class ColumnSecondarySpecsEditor
    {
        private static readonly double[] StandardPercents = { 0, 5, 10, 30, 50, 70, 90, 95, 100 };

        /// <summary>The properties offered: every curve at the standard points, then the product properties.</summary>
        public static List<string> PropertyNames()
        {
            var list = new List<string>();
            for (int c = 0; c < DistillationCurvesUtility.CurvePrefixes.Length; c++)
                foreach (var p in StandardPercents) list.Add(DistillationCurvesUtility.PointName(c, p));
            list.AddRange(DistillationCurvesUtility.TemperatureProperties);
            list.AddRange(DistillationCurvesUtility.OtherProperties);
            return list;
        }

        /// <summary>The flowsheet unit a property's target is shown in, and the one its tolerance is shown in.</summary>
        private static (string Value, string Tolerance) UnitsOf(IUnitsOfMeasure su, string property)
        {
            switch (DistillationCurvesUtility.UnitKind(property))
            {
                case "temperature": return (su.temperature, su.deltaT);
                case "density": return (su.density, su.density);
                case "cinematic_viscosity": return (su.cinematic_viscosity, su.cinematic_viscosity);
                case "pressure": return (su.pressure, su.deltaP);
                case "molecularWeight": return (su.molecularWeight, su.molecularWeight);
                default: return ("", "");
            }
        }

        private static double FromSI(string unit, double v) => string.IsNullOrEmpty(unit) ? v : cv.ConvertFromSI(unit, v);
        private static double ToSI(string unit, double v) => string.IsNullOrEmpty(unit) ? v : cv.ConvertToSI(unit, v);

        public static Control Build(Column column)
        {
            var host = new StackPanel();
            Populate(host, column);
            return new ScrollViewer { Content = host };
        }

        private static void Populate(StackPanel host, Column column)
        {
            host.Children.Clear();
            var panel = new AvaloniaEditorPanel().AutoSolveOnEdit(column);
            host.Children.Add(panel);

            var fs = column.GetFlowsheet();
            var su = fs.FlowsheetOptions.SelectedUnitSystem;
            var nf = fs.FlowsheetOptions.NumberFormat;

            panel.CreateAndAddLabelRow("Secondary Specifications");
            panel.CreateAndAddDescriptionRow(
                "Each specification sets a property of a product (a distillation curve point, the flash point, a density, or any stream property) " +
                "and moves one free variable of the column to reach it: the value of the condenser or reboiler specification, or a side draw rate. " +
                "The column solves as usual inside an outer loop, so the specifications above keep their type and only their value moves. " +
                "Each variable can serve one specification.");

            var products = column.MaterialStreams.Values
                .Where(s => s.StreamBehavior == StreamInformation.Behavior.Distillate || s.StreamBehavior == StreamInformation.Behavior.OverheadVapor ||
                            s.StreamBehavior == StreamInformation.Behavior.BottomsLiquid || s.StreamBehavior == StreamInformation.Behavior.Sidedraw)
                .Where(s => !string.IsNullOrEmpty(s.StreamID) && fs.SimulationObjects.ContainsKey(s.StreamID))
                .ToList();
            var sideDraws = products.Where(s => s.StreamBehavior == StreamInformation.Behavior.Sidedraw).ToList();
            string Tag(string id) => fs.SimulationObjects.TryGetValue(id, out var o) ? o.GraphicObject.Tag : id;

            var variables = new List<(ColumnSecondarySpec.ManipulatedVariableType Type, string Stream, string Label)>
            {
                (ColumnSecondarySpec.ManipulatedVariableType.CondenserSpecValue, "", "Condenser specification value"),
                (ColumnSecondarySpec.ManipulatedVariableType.ReboilerSpecValue, "", "Reboiler specification value"),
            };
            foreach (var s in sideDraws)
                variables.Add((ColumnSecondarySpec.ManipulatedVariableType.SideDrawRate, s.StreamID, "Side draw rate: " + Tag(s.StreamID) + " (mol/s)"));

            var names = PropertyNames();

            int index = 0;
            foreach (var spec in column.SecondarySpecs.ToList())
            {
                index++;
                var s = spec;
                panel.CreateAndAddLabelRow("Specification " + index + (string.IsNullOrEmpty(s.Name) ? "" : ": " + s.Name));
                panel.CreateAndAddCheckBoxRow("Active", s.Enabled, (cb, _) => s.Enabled = cb.IsChecked == true);
                panel.CreateAndAddStringEditorRow("Name", s.Name, (tb, _) => s.Name = tb.Text ?? "");

                var productTags = products.Select(p => Tag(p.StreamID)).ToList();
                panel.CreateAndAddDropDownRow("Product", productTags, products.FindIndex(p => p.StreamID == s.ProductStreamID),
                    (dd, _) => { if (dd.SelectedIndex >= 0) s.ProductStreamID = products[dd.SelectedIndex].StreamID; });

                var propList = names.Contains(s.PropertyName) || string.IsNullOrEmpty(s.PropertyName) ? names : names.Concat(new[] { s.PropertyName }).ToList();
                TextBox targetBox = null, tolBox = null;
                panel.CreateAndAddDropDownRow("Property", propList, propList.IndexOf(s.PropertyName),
                    (dd, _) =>
                    {
                        if (dd.SelectedIndex < 0) return;
                        s.PropertyName = propList[dd.SelectedIndex];
                        Populate(host, column);
                    });
                panel.CreateAndAddDescriptionRow("Curve points and product properties come from the distillation curves calculation (API Technical Data Book). Any other material stream property can be given by its ID in the Property ID row.");
                panel.CreateAndAddStringEditorRow("Property ID", s.PropertyName, (tb, _) => { s.PropertyName = tb.Text ?? ""; Populate(host, column); });

                var (vu, tu) = UnitsOf(su, s.PropertyName);
                targetBox = panel.CreateAndAddTextBoxRow(nf, "Target" + (vu != "" ? " (" + vu + ")" : ""), FromSI(vu, s.TargetValue),
                    (tb, _) => { if (TryParse(tb.Text, out var v)) s.TargetValue = ToSI(vu, v); });
                tolBox = panel.CreateAndAddTextBoxRow(nf, "Tolerance" + (tu != "" ? " (" + tu + ")" : "") + ", 0 = 0.1 % of the target", FromSI(tu, s.Tolerance),
                    (tb, _) => { if (TryParse(tb.Text, out var v)) s.Tolerance = Math.Max(0, ToSI(tu, v)); });

                panel.CreateAndAddDropDownRow("Variable moved", variables.Select(v => v.Label).ToList(),
                    variables.FindIndex(v => v.Type == s.ManipulatedVariable && (v.Type != ColumnSecondarySpec.ManipulatedVariableType.SideDrawRate || v.Stream == s.ManipulatedStreamID)),
                    (dd, _) =>
                    {
                        if (dd.SelectedIndex < 0) return;
                        s.ManipulatedVariable = variables[dd.SelectedIndex].Type;
                        s.ManipulatedStreamID = variables[dd.SelectedIndex].Stream;
                    });
                panel.CreateAndAddTextBoxRow(nf, "Lower limit of the variable", s.MinValue, (tb, _) => { if (TryParse(tb.Text, out var v)) s.MinValue = v; });
                panel.CreateAndAddTextBoxRow(nf, "Upper limit of the variable", s.MaxValue, (tb, _) => { if (TryParse(tb.Text, out var v)) s.MaxValue = v; });
                panel.CreateAndAddDescriptionRow("The limits are in the units of the variable (the specification's own units, or mol/s for a side draw). Equal limits leave the variable free.");

                if (!double.IsNaN(s.CalculatedValue))
                {
                    panel.CreateAndAddTwoLabelsRow("Calculated", FromSI(vu, s.CalculatedValue).ToString(nf) + (vu != "" ? " " + vu : "") + (s.Converged ? "" : " (not met)"));
                    if (!double.IsNaN(s.ManipulatedValue))
                        panel.CreateAndAddTwoLabelsRow("Variable", s.ManipulatedValue.ToString(nf));
                }

                panel.CreateAndAddButtonRow("Remove specification " + index, null, (_, _) =>
                {
                    column.SecondarySpecs.Remove(s);
                    Populate(host, column);
                });
            }

            panel.CreateAndAddButtonRow("Add specification", null, (_, _) =>
            {
                var first = products.FirstOrDefault();
                column.SecondarySpecs.Add(new ColumnSecondarySpec
                {
                    Name = "",
                    ProductStreamID = first?.StreamID ?? "",
                    PropertyName = "D86 95%",
                    TargetValue = 573.15,
                    Enabled = false,
                    ManipulatedVariable = ColumnSecondarySpec.ManipulatedVariableType.CondenserSpecValue
                });
                Populate(host, column);
            });

            if (column.SecondarySpecsLog.Count > 0)
            {
                panel.CreateAndAddLabelRow("Last Solution");
                panel.CreateAndAddDescriptionRow(string.Join(Environment.NewLine, column.SecondarySpecsLog.Where(l => !l.StartsWith("  "))));
            }
        }

        private static bool TryParse(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
