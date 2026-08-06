using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Adjust = DWSIM.UnitOperations.SpecialOps.Adjust;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The control panel of an Adjust block: it drives the manipulated variable until the
    /// controlled one reaches the set point, solving the flowsheet at every step, which is what
    /// the Windows control panel does when the block is not left to the flowsheet solver.
    /// </summary>
    public static class AdjustControlPanel
    {

        /// <summary>The convergence methods, in the order the Windows combo lists them.</summary>
        private static readonly List<string> Methods = new List<string>
        {
            "Secant", "Brent", "Newton", "IPOPT"
        };

        private sealed class IterationRow
        {
            public string Iteration { get; set; } = "";
            public string Manipulated { get; set; } = "";
            public string Controlled { get; set; } = "";
            public string SetPoint { get; set; } = "";
            public string Error { get; set; } = "";
        }

        public static void Show(Adjust adjust)
        {
            var flowsheet = adjust.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(
                adjust.GraphicObject.Tag + ": Control Panel", 640, 660, panel);

            if (adjust.ManipulatedObject == null || adjust.ControlledObject == null)
            {
                panel.CreateAndAddDescriptionRow(
                    "Pick the manipulated and the controlled variables before running the adjust.");
                window.Show();
                return;
            }

            var manipulatedUnit = adjust.ManipulatedObject.GetPropertyUnit(
                adjust.ManipulatedObjectData.PropertyName, su);
            var controlledUnit = adjust.ControlledObject.GetPropertyUnit(
                adjust.ControlledObjectData.PropertyName, su);

            SeedLimits(adjust, su, manipulatedUnit);

            panel.CreateAndAddLabelRow("Parameters");

            var method = panel.CreateAndAddDropDownRow("Convergence method", Methods,
                Math.Max(0, adjust.SolvingMethodSelf), (dd, e) => adjust.SolvingMethodSelf = dd.SelectedIndex);

            panel.CreateAndAddTextBoxRow(nf, "Adjust value (" + controlledUnit + ")",
                AdjustEditor.SetPointInDisplayUnit(adjust),
                (tb, e) =>
                {
                    if (UnitOpEditorRows.TryParse(tb.Text, out var v))
                        adjust.AdjustValue = AdjustEditor.SetPointToSI(adjust, v);
                });

            panel.CreateAndAddTextBoxRow(nf, "Tolerance", adjust.Tolerance,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) adjust.Tolerance = v; });

            panel.CreateAndAddTextBoxRow(nf, "Step size", adjust.StepSize,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) adjust.StepSize = v; });

            panel.CreateAndAddTextBoxRow(nf, "Maximum iterations", adjust.MaximumIterations,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) adjust.MaximumIterations = (int)v; });

            panel.CreateAndAddLabelRow("Min / Max Limits (" + manipulatedUnit + ")");

            panel.CreateAndAddTextBoxRow(nf, "Minimum", adjust.MinVal.GetValueOrDefault(),
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) adjust.MinVal = v; });

            panel.CreateAndAddTextBoxRow(nf, "Maximum", adjust.MaxVal.GetValueOrDefault(),
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) adjust.MaxVal = v; });

            panel.CreateAndAddLabelRow("Results");

            var status = panel.CreateAndAddTwoLabelsRow("Status", "Idle");
            var iteration = panel.CreateAndAddTwoLabelsRow("Iteration", "");
            var error = panel.CreateAndAddTwoLabelsRow("Current error", "");

            var rows = new ObservableCollection<IterationRow>();

            var grid = new DataGrid
            {
                ItemsSource = rows,
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Height = 220
            };

            grid.Columns.Add(Column("Iteration", "Iteration", 0.8));
            grid.Columns.Add(Column("MV", "Manipulated", 1.2));
            grid.Columns.Add(Column("CV", "Controlled", 1.2));
            grid.Columns.Add(Column("SP", "SetPoint", 1.2));
            grid.Columns.Add(Column("Error", "Error", 1.2));

            panel.Children.Add(grid);

            Button start = null, stop = null;
            var cancel = false;

            start = panel.CreateAndAddButtonRow("Start Adjust", null, (btn, e) =>
            {
                cancel = false;
                rows.Clear();
                start.IsEnabled = false;
                if (stop != null) stop.IsEnabled = true;
                status.Text = "Adjusting";

                Run(adjust, su, nf, manipulatedUnit, controlledUnit,
                    () => cancel, rows, status, iteration, error,
                    () =>
                    {
                        start.IsEnabled = true;
                        if (stop != null) stop.IsEnabled = false;
                    });
            });

            stop = panel.CreateAndAddButtonRow("Stop", null, (btn, e) => cancel = true);
            stop.IsEnabled = false;

            window.Show();
        }

        /// <summary>
        /// The limits the Windows panel fills in the first time it is opened: a fifth and twice
        /// the current value of the manipulated variable.
        /// </summary>
        private static void SeedLimits(Adjust adjust, IUnitsOfMeasure su, string manipulatedUnit)
        {
            if (adjust.MinVal.HasValue && adjust.MaxVal.HasValue) return;

            try
            {
                var current = Convert.ToDouble(adjust.ManipulatedObject.GetPropertyValue(
                    adjust.ManipulatedObjectData.PropertyName));

                if (!adjust.MinVal.HasValue)
                    adjust.MinVal = cv.ConvertFromSI(manipulatedUnit, current * 0.2);
                if (!adjust.MaxVal.HasValue)
                    adjust.MaxVal = cv.ConvertFromSI(manipulatedUnit, current * 2.0);
            }
            catch (Exception)
            {
            }
        }

        private static void Run(Adjust adjust, IUnitsOfMeasure su, string nf,
                                string manipulatedUnit, string controlledUnit,
                                Func<bool> cancelled,
                                ObservableCollection<IterationRow> rows,
                                TextBlock status, TextBlock iterationLabel, TextBlock errorLabel,
                                Action finished)
        {
            var flowsheet = adjust.GetFlowsheet();

            var maxIterations = adjust.MaximumIterations;
            var tolerance = cv.ConvertToSI(controlledUnit, adjust.Tolerance);

            var minimum = cv.ConvertToSI(manipulatedUnit, adjust.MinVal.GetValueOrDefault());
            var maximum = cv.ConvertToSI(manipulatedUnit, adjust.MaxVal.GetValueOrDefault());

            var start = Convert.ToDouble(adjust.ManipulatedObject.GetPropertyValue(
                adjust.ManipulatedObjectData.PropertyName));

            var count = 0;

            double SetPoint()
            {
                if (!adjust.Referenced) return cv.ConvertFromSI(controlledUnit, adjust.AdjustValue);

                var reference = Convert.ToDouble(flowsheet.SimulationObjects[adjust.ReferencedObjectData.ID]
                    .GetPropertyValue(adjust.ReferencedObjectData.PropertyName, su));

                var unit = flowsheet.SimulationObjects[adjust.ReferencedObjectData.ID]
                    .GetPropertyUnit(adjust.ReferencedObjectData.PropertyName, su);

                var offset = su.GetUnitType(unit) == UnitOfMeasure.temperature
                    ? cv.ConvertFromSI(unit + ".", adjust.AdjustValue)
                    : cv.ConvertFromSI(unit, adjust.AdjustValue);

                return reference + offset;
            }

            Func<double, double> residual = x =>
            {
                if (cancelled()) throw new TaskCanceledException("Adjust cancelled by the user.");

                adjust.ManipulatedObject.SetPropertyValue(adjust.ManipulatedObjectData.PropertyName, x);

                DWSIM.FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(flowsheet,
                    GlobalSettings.Settings.SolverMode);

                var controlled = Convert.ToDouble(adjust.ControlledObject.GetPropertyValue(
                    adjust.ControlledObjectData.PropertyName, su));

                var setPoint = SetPoint();

                var f = cv.ConvertToSI(controlledUnit, controlled) - cv.ConvertToSI(controlledUnit, setPoint);

                var index = count;
                count += 1;

                Dispatcher.UIThread.Post(() =>
                {
                    iterationLabel.Text = (index + 1) + " of " + maxIterations;
                    errorLabel.Text = f.ToString("G6", CultureInfo.CurrentCulture);

                    rows.Add(new IterationRow
                    {
                        Iteration = index.ToString(),
                        Manipulated = cv.ConvertFromSI(manipulatedUnit, x).ToString(nf, CultureInfo.CurrentCulture),
                        Controlled = controlled.ToString(nf, CultureInfo.CurrentCulture),
                        SetPoint = setPoint.ToString(nf, CultureInfo.CurrentCulture),
                        Error = (controlled - setPoint).ToString(nf, CultureInfo.CurrentCulture)
                    });
                });

                return f;
            };

            Task.Factory.StartNew(() =>
            {
                switch (adjust.SolvingMethodSelf)
                {
                    case 1:
                        MathNet.Numerics.RootFinding.Brent.FindRoot(x => residual(x),
                            minimum, maximum, tolerance, maxIterations);
                        break;

                    case 2:
                    {
                        var solver = new DWSIM.MathOps.MathEx.Optimization.NewtonSolver
                        {
                            EnableDamping = false,
                            MaxIterations = maxIterations,
                            Tolerance = tolerance * tolerance
                        };
                        solver.Solve(x => new double[] { residual(x[0]) }, new double[] { start });
                        break;
                    }

                    case 3:
                    {
                        var solver = new DWSIM.MathOps.MathEx.Optimization.IPOPTSolver
                        {
                            MaxIterations = maxIterations,
                            Tolerance = tolerance
                        };
                        solver.Solve(x => Math.Pow(residual(x[0]), 2.0), null,
                            new double[] { start }, new double[] { minimum }, new double[] { maximum });
                        break;
                    }

                    default:
                        MathNet.Numerics.RootFinding.Secant.FindRoot(
                            x => double.IsNaN(x) || double.IsInfinity(x) ? 1.0e20 : residual(x),
                            start, start * 1.01, minimum, maximum, tolerance, maxIterations);
                        break;
                }
            })
            .ContinueWith(task =>
            {
                var failed = task.Exception != null;

                // the flowsheet is left where the last successful step put it, so a failed run
                // is rewound to the value the manipulated variable started from
                if (failed)
                {
                    try
                    {
                        adjust.ManipulatedObject.SetPropertyValue(
                            adjust.ManipulatedObjectData.PropertyName, start);
                        DWSIM.FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(flowsheet,
                            GlobalSettings.Settings.SolverMode);
                    }
                    catch (Exception)
                    {
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    adjust.GraphicObject.Calculated = !failed;

                    status.Text = failed
                        ? "Failed: " + task.Exception.InnerException.Message
                        : "Value adjusted successfully.";

                    flowsheet.UpdateInterface();
                    flowsheet.UpdateOpenEditForms();

                    finished();
                });
            });
        }

        private static DataGridTextColumn Column(string header, string path, double width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path) { Mode = BindingMode.OneWay },
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            };
        }

    }

}
