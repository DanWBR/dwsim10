using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using MPCController = DWSIM.UnitOperations.SpecialOps.MPCController;
using MPCVariable = DWSIM.UnitOperations.SpecialOps.MPCVariable;
using PIDController = DWSIM.UnitOperations.SpecialOps.PIDController;
using PythonController = DWSIM.UnitOperations.SpecialOps.PythonController;
using SpecialOpObjectInfo = DWSIM.UnitOperations.SpecialOps.Helpers.SpecialOpObjectInfo;
using StepResponseModel = DWSIM.UnitOperations.SpecialOps.StepResponseModel;
using Thickness = Avalonia.Thickness;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// PID controller editor, as the Windows EditingForm_PIDController lays it out: the two linked
    /// variables above, then the gains and the output limits, with the advanced form, cascade and
    /// feedforward settings on a page of their own.
    /// </summary>
    public static class PIDControllerEditor
    {

        /// <summary>The three PID forms, in the order the Windows combo lists them.</summary>
        private static readonly List<string> Forms = new List<string>
        {
            "Parallel", "ISA (Standard)", "Series"
        };

        public static Control Build(PIDController pid)
        {
            return UnitOpEditor.Build(pid,
                input: null,
                propertyPackage: false,
                connections: false,
                extras: new[]
                {
                    ("Linked Objects", BuildLinkedObjects(pid)),
                    ("Parameters", BuildParameters(pid))
                });
        }

        private static Control BuildLinkedObjects(PIDController pid)
        {
            var panel = new AvaloniaEditorPanel();

            if (pid.ManipulatedObjectData == null)
                pid.ManipulatedObjectData = new SpecialOpObjectInfo();
            if (pid.ControlledObjectData == null)
                pid.ControlledObjectData = new SpecialOpObjectInfo();

            VariablePicker.Add(panel, pid, (SpecialOpObjectInfo)pid.ManipulatedObjectData,
                VariablePicker.Role.AdjustManipulated, "Manipulated", writable: true, withUnits: true);

            VariablePicker.Add(panel, pid, (SpecialOpObjectInfo)pid.ControlledObjectData,
                VariablePicker.Role.AdjustControlled, "Controlled", writable: false, withUnits: true);

            return panel;
        }

        private static Control BuildParameters(PIDController pid)
        {
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "General", Content = BuildGeneral(pid) });
            tabs.Items.Add(new TabItem { Header = "Advanced", Content = BuildAdvanced(pid) });
            return tabs;
        }

        private static Control BuildGeneral(PIDController pid)
        {
            var panel = new AvaloniaEditorPanel();
            var nf = pid.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddCheckBoxRow("Controller Active", pid.Active,
                (cb, e) => pid.Active = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddCheckBoxRow("Reverse Acting", pid.ReverseActing,
                (cb, e) => pid.ReverseActing = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddTextBoxRow(nf, "Set-Point", pid.AdjustValue,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.AdjustValue = v; });

            TextBox kp = null, ki = null, kd = null;

            kp = panel.CreateAndAddTextBoxRow(nf, "Kp", pid.Kp,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.Kp = v; });
            ki = panel.CreateAndAddTextBoxRow(nf, "Ki", pid.Ki,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.Ki = v; });
            kd = panel.CreateAndAddTextBoxRow(nf, "Kd", pid.Kd,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.Kd = v; });

            panel.CreateAndAddButtonRow("Estimate Parameters", null, (btn, e) =>
            {
                pid.EstimateParameters();
                kp.Text = pid.Kp.ToString(nf, CultureInfo.CurrentCulture);
                ki.Text = pid.Ki.ToString(nf, CultureInfo.CurrentCulture);
                kd.Text = pid.Kd.ToString(nf, CultureInfo.CurrentCulture);
            });

            panel.CreateAndAddTextBoxRow(nf, "Offset (Bias)", pid.Offset,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.Offset = v; });

            panel.CreateAndAddTextBoxRow(nf, "Wind-Up Guard", pid.WindupGuard,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.WindupGuard = v; });

            panel.CreateAndAddTextBoxRow(nf, "Min. Ouput (Absolute)", pid.OutputMin,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.OutputMin = v; });

            panel.CreateAndAddTextBoxRow(nf, "Max Output (Absolute)", pid.OutputMax,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.OutputMax = v; });

            // the three terms and the absolute output only mean something while the controller runs
            panel.CreateAndAddLabelRow("Output");

            if (pid.Active)
            {
                panel.CreateAndAddTwoLabelsRow("P", pid.PTerm.ToString(nf, CultureInfo.CurrentCulture));
                panel.CreateAndAddTwoLabelsRow("I", (pid.Ki * pid.ITerm).ToString(nf, CultureInfo.CurrentCulture));
                panel.CreateAndAddTwoLabelsRow("D", (pid.Kd * pid.DTerm).ToString(nf, CultureInfo.CurrentCulture));

                var factor = pid.ReverseActing ? 1.0 + pid.Output : 1.0 - pid.Output;
                panel.CreateAndAddTwoLabelsRow("Abs. Output",
                    (factor * pid.BaseSP.GetValueOrDefault()).ToString(nf, CultureInfo.CurrentCulture));
            }
            else
            {
                panel.CreateAndAddDescriptionRow("The controller is off, so it has no output yet.");
            }

            return panel;
        }

        private static Control BuildAdvanced(PIDController pid)
        {
            var panel = new AvaloniaEditorPanel();
            var flowsheet = pid.GetFlowsheet();
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            panel.CreateAndAddDropDownRow("PID Form", Forms, pid.PIDForm,
                (dd, e) => { if (dd.SelectedIndex >= 0) pid.PIDForm = dd.SelectedIndex; });

            panel.CreateAndAddTextBoxRow(nf, "Derivative Filter Coeff.", pid.DerivativeFilterCoefficient,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.DerivativeFilterCoefficient = v; });

            panel.CreateAndAddCheckBoxRow("Use Derivative on PV", pid.UseDerivativeOnPV,
                (cb, e) => pid.UseDerivativeOnPV = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddTextBoxRow(nf, "SP Weight P (beta)", pid.SetpointWeightP,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.SetpointWeightP = v; });

            panel.CreateAndAddTextBoxRow(nf, "SP Weight D (gamma)", pid.SetpointWeightD,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.SetpointWeightD = v; });

            panel.CreateAndAddTextBoxRow(nf, "Execution Order", pid.ExecutionOrder,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.ExecutionOrder = (int)v; });

            // the master of a cascade is another controller, and an empty pick breaks the link
            var masters = flowsheet.SimulationObjects.Values
                .Where(x => x is PIDController && x.Name != pid.Name)
                .OrderBy(x => x.GraphicObject.Tag)
                .ToList();

            var masterTags = new List<string> { "" };
            masterTags.AddRange(masters.Select(x => x.GraphicObject.Tag));

            var selected = masters.FindIndex(x => x.Name == pid.CascadeMasterID);

            panel.CreateAndAddDropDownRow("Cascade Master", masterTags,
                selected < 0 ? 0 : selected + 1, (dd, e) =>
                {
                    if (dd.SelectedIndex <= 0) { pid.CascadeMasterID = ""; return; }
                    pid.CascadeMasterID = masters[dd.SelectedIndex - 1].Name;
                });

            panel.CreateAndAddTextBoxRow(nf, "Feedforward Gain", pid.FeedforwardGain,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.FeedforwardGain = v; });

            panel.CreateAndAddTextBoxRow(nf, "FF Lead Time (s)", pid.FeedforwardLeadTime,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.FeedforwardLeadTime = v; });

            panel.CreateAndAddTextBoxRow(nf, "FF Lag Time (s)", pid.FeedforwardLagTime,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pid.FeedforwardLagTime = v; });

            return panel;
        }

    }

    /// <summary>
    /// Python controller editor: the same two linked variables as the PID, with a script that
    /// computes the output instead of the gains.
    /// </summary>
    public static class PythonControllerEditor
    {

        public static Control Build(PythonController controller)
        {
            return UnitOpEditor.Build(controller,
                input: null,
                propertyPackage: false,
                connections: false,
                extras: new[]
                {
                    ("Linked Objects", BuildLinkedObjects(controller)),
                    ("Parameters", BuildParameters(controller))
                });
        }

        private static Control BuildLinkedObjects(PythonController controller)
        {
            var panel = new AvaloniaEditorPanel();

            if (controller.ManipulatedObjectData == null)
                controller.ManipulatedObjectData = new SpecialOpObjectInfo();
            if (controller.ControlledObjectData == null)
                controller.ControlledObjectData = new SpecialOpObjectInfo();

            VariablePicker.Add(panel, controller, (SpecialOpObjectInfo)controller.ManipulatedObjectData,
                VariablePicker.Role.AdjustManipulated, "Manipulated", writable: true, withUnits: true);

            VariablePicker.Add(panel, controller, (SpecialOpObjectInfo)controller.ControlledObjectData,
                VariablePicker.Role.AdjustControlled, "Controlled", writable: false, withUnits: true);

            return panel;
        }

        private static Control BuildParameters(PythonController controller)
        {
            var panel = new AvaloniaEditorPanel();
            var nf = controller.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddCheckBoxRow("Controller Active", controller.Active,
                (cb, e) => controller.Active = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddTextBoxRow(nf, "Set-Point", controller.AdjustValue,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) controller.AdjustValue = v; });

            panel.CreateAndAddLabelRow("Script");
            panel.CreateAndAddDescriptionRow(
                "Reads PV, SP and MV and writes the new value of the manipulated variable to MV.");

            panel.CreateAndAddMultilineMonoSpaceTextBoxRow(controller.PythonScript ?? "", 320, false,
                (tb, e) => controller.PythonScript = tb.Text ?? "");

            return panel;
        }

    }

    /// <summary>
    /// MPC controller editor, as the Windows EditingForm_MPCController lays it out: the horizons
    /// and weights, then the controlled and manipulated variables and the step response models
    /// that relate them, each in a grid of its own.
    /// </summary>
    public static class MPCControllerEditor
    {

        private sealed class VariableRow
        {
            public string Name { get; set; } = "";
            public string Object { get; set; } = "";
            public string Property { get; set; } = "";
            public string Units { get; set; } = "";
            public string Minimum { get; set; } = "";
            public string Maximum { get; set; } = "";
            public string Weight { get; set; } = "";
        }

        private sealed class ModelRow
        {
            public string ControlledVariable { get; set; } = "";
            public string ManipulatedVariable { get; set; } = "";
            public string Gain { get; set; } = "";
            public string TimeConstant { get; set; } = "";
            public string DeadTime { get; set; } = "";
            public string Coefficients { get; set; } = "";
        }

        public static Control Build(MPCController mpc)
        {
            return UnitOpEditor.Build(mpc,
                input: panel =>
                {
                    var nf = mpc.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    panel.CreateAndAddCheckBoxRow("Controller Active", mpc.Active,
                        (cb, e) => mpc.Active = cb.IsChecked.GetValueOrDefault());

                    panel.CreateAndAddTextBoxRow(nf, "Prediction Horizon", mpc.PredictionHorizon,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) mpc.PredictionHorizon = (int)v; });

                    panel.CreateAndAddTextBoxRow(nf, "Control Horizon", mpc.ControlHorizon,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) mpc.ControlHorizon = (int)v; });

                    panel.CreateAndAddTextBoxRow(nf, "Sample Time", mpc.SampleTime,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) mpc.SampleTime = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Move Suppression Weight", mpc.MoveSuppressionWeight,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) mpc.MoveSuppressionWeight = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Execution Order", mpc.ExecutionOrder,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) mpc.ExecutionOrder = (int)v; });
                },
                propertyPackage: false,
                connections: false,
                extras: new[]
                {
                    ("Controlled Variables", BuildVariables(mpc, controlled: true)),
                    ("Manipulated Variables", BuildVariables(mpc, controlled: false)),
                    ("Step Response Models", BuildModels(mpc))
                });
        }

        private static Control BuildVariables(MPCController mpc, bool controlled)
        {
            var stack = new StackPanel();
            var rows = new ObservableCollection<VariableRow>();

            var variables = controlled ? mpc.ControlledVariables : mpc.ManipulatedVariables;

            void Refresh()
            {
                rows.Clear();
                foreach (var variable in variables) rows.Add(RowOf(mpc, variable, controlled));
            }

            Refresh();

            var grid = Grid();
            grid.ItemsSource = rows;
            grid.SelectionMode = DataGridSelectionMode.Single;
            grid.Height = 160;

            grid.Columns.Add(Column("Name", "Name", 1.2));
            grid.Columns.Add(Column("Object", "Object", 1.2));
            grid.Columns.Add(Column("Property", "Property", 1.6));
            grid.Columns.Add(Column("Units", "Units", 0.8));
            grid.Columns.Add(Column("Minimum", "Minimum", 1.0));
            grid.Columns.Add(Column("Maximum", "Maximum", 1.0));
            if (controlled) grid.Columns.Add(Column("Weight", "Weight", 0.8));

            stack.Children.Add(grid);

            var actions = new AvaloniaEditorPanel();

            actions.CreateAndAddButtonRow("Add Variable", null, (btn, e) =>
                MPCVariableDialog.Show(mpc, controlled, variable =>
                {
                    variables.Add(variable);
                    Refresh();
                }));

            actions.CreateAndAddButtonRow("Remove Selected Variable", null, (btn, e) =>
            {
                var index = grid.SelectedIndex;
                if (index < 0 || index >= variables.Count) return;
                variables.RemoveAt(index);
                Refresh();
            });

            stack.Children.Add(actions);

            return stack;
        }

        private static VariableRow RowOf(MPCController mpc, MPCVariable variable, bool controlled)
        {
            var flowsheet = mpc.GetFlowsheet();

            var tag = "";
            if (!string.IsNullOrEmpty(variable.ObjectID) &&
                flowsheet.SimulationObjects.ContainsKey(variable.ObjectID))
                tag = flowsheet.SimulationObjects[variable.ObjectID].GraphicObject.Tag;

            return new VariableRow
            {
                Name = variable.Name,
                Object = tag,
                Property = variable.PropertyName,
                Units = variable.Units,
                Minimum = variable.MinValue == double.MinValue ? "" : variable.MinValue.ToString("G6", CultureInfo.CurrentCulture),
                Maximum = variable.MaxValue == double.MaxValue ? "" : variable.MaxValue.ToString("G6", CultureInfo.CurrentCulture),
                Weight = controlled ? variable.Weight.ToString("G6", CultureInfo.CurrentCulture) : ""
            };
        }

        private static Control BuildModels(MPCController mpc)
        {
            var stack = new StackPanel();
            var rows = new ObservableCollection<ModelRow>();

            void Refresh()
            {
                rows.Clear();
                foreach (var model in mpc.StepResponseModels)
                    rows.Add(new ModelRow
                    {
                        ControlledVariable = NameOf(mpc.ControlledVariables, model.CVIndex),
                        ManipulatedVariable = NameOf(mpc.ManipulatedVariables, model.MVIndex),
                        Gain = model.Gain.ToString("G6", CultureInfo.CurrentCulture),
                        TimeConstant = model.TimeConstant.ToString("G6", CultureInfo.CurrentCulture),
                        DeadTime = model.DeadTime.ToString("G6", CultureInfo.CurrentCulture),
                        Coefficients = model.StepCoefficients.Count + " pts"
                    });
            }

            Refresh();

            var grid = Grid();
            grid.ItemsSource = rows;
            grid.SelectionMode = DataGridSelectionMode.Single;
            grid.Height = 160;

            grid.Columns.Add(Column("Controlled Variable", "ControlledVariable", 1.4));
            grid.Columns.Add(Column("Manipulated Variable", "ManipulatedVariable", 1.4));
            grid.Columns.Add(Column("Gain", "Gain", 1.0));
            grid.Columns.Add(Column("Time Constant", "TimeConstant", 1.0));
            grid.Columns.Add(Column("Dead Time", "DeadTime", 1.0));
            grid.Columns.Add(Column("Coefficients", "Coefficients", 1.0));

            stack.Children.Add(grid);

            var actions = new AvaloniaEditorPanel();

            actions.CreateAndAddButtonRow("Add Model", null, (btn, e) =>
                StepResponseModelDialog.Show(mpc, model =>
                {
                    mpc.StepResponseModels.Add(model);
                    Refresh();
                }));

            actions.CreateAndAddButtonRow("Remove Selected Model", null, (btn, e) =>
            {
                var index = grid.SelectedIndex;
                if (index < 0 || index >= mpc.StepResponseModels.Count) return;
                mpc.StepResponseModels.RemoveAt(index);
                Refresh();
            });

            actions.CreateAndAddButtonRow("Regenerate Models", null, (btn, e) =>
            {
                mpc.InitializeModels();
                Refresh();
            });

            stack.Children.Add(actions);

            return stack;
        }

        private static string NameOf(List<MPCVariable> variables, int index)
        {
            if (index < 0 || index >= variables.Count) return index.ToString();
            return variables[index].Name;
        }

        private static DataGrid Grid()
        {
            return new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserSortColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
            };
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

    /// <summary>
    /// The dialog that adds a controlled or manipulated variable to an MPC controller, as the
    /// Windows AddMPCVariableDialog does.
    /// </summary>
    internal static class MPCVariableDialog
    {

        internal static void Show(MPCController mpc, bool controlled, Action<MPCVariable> accepted)
        {
            var flowsheet = mpc.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;

            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(
                controlled ? "Add Controlled Variable" : "Add Manipulated Variable", 520, 460, panel);

            var objects = flowsheet.SimulationObjects.Values
                .Where(x => x.GraphicObject != null && x.Name != mpc.Name)
                .OrderBy(x => x.GraphicObject.Tag)
                .ToList();

            if (objects.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("No other objects on the flowsheet yet.");
                window.Show();
                return;
            }

            var variable = new MPCVariable();
            var properties = new List<string>();
            ComboBox propertyPicker = null;

            // a controlled variable is read, a manipulated one is written
            var kind = controlled ? PropertyType.ALL : PropertyType.WR;

            var objectPicker = panel.CreateAndAddDropDownRow("Object",
                objects.Select(x => x.GraphicObject.Tag).ToList(), 0, null);

            void ReloadProperties()
            {
                properties.Clear();
                var obj = objects.ElementAtOrDefault(objectPicker.SelectedIndex);
                if (obj == null) return;
                properties.AddRange(obj.GetProperties(kind) ?? new string[0]);
            }

            ReloadProperties();

            propertyPicker = panel.CreateAndAddDropDownRow("Property", properties.ToList(), 0, null);

            objectPicker.SelectionChanged += (s, e) =>
            {
                ReloadProperties();
                propertyPicker.ItemsSource = properties.ToList();
                propertyPicker.SelectedIndex = properties.Count > 0 ? 0 : -1;
            };

            var name = panel.CreateAndAddStringEditorRow("Name", "", null);

            var minimum = panel.CreateAndAddStringEditorRow("Minimum", "", null);
            var maximum = panel.CreateAndAddStringEditorRow("Maximum", "", null);

            TextBox weight = null;
            if (controlled) weight = panel.CreateAndAddStringEditorRow("Weight", "1", null);

            panel.CreateAndAddDescriptionRow("Leave a limit empty to leave the variable unbounded.");

            panel.CreateAndAddButtonRow("Add", null, (btn, e) =>
            {
                var obj = objects.ElementAtOrDefault(objectPicker.SelectedIndex);
                if (obj == null || propertyPicker.SelectedIndex < 0) return;

                variable.ObjectID = obj.Name;
                variable.PropertyName = properties[propertyPicker.SelectedIndex];
                variable.Units = obj.GetPropertyUnit(variable.PropertyName, su);
                variable.UnitsType = su.GetUnitType(variable.Units);
                variable.Name = string.IsNullOrWhiteSpace(name.Text)
                    ? obj.GraphicObject.Tag + " / " + variable.PropertyName
                    : name.Text;

                variable.MinValue = UnitOpEditorRows.TryParse(minimum.Text, out var min) ? min : double.MinValue;
                variable.MaxValue = UnitOpEditorRows.TryParse(maximum.Text, out var max) ? max : double.MaxValue;

                if (weight != null && UnitOpEditorRows.TryParse(weight.Text, out var w)) variable.Weight = w;

                accepted(variable);
                window.Close();
            });

            window.Show();
        }

    }

    /// <summary>
    /// The dialog that adds a step response model to an MPC controller, relating one controlled
    /// variable to one manipulated variable through a first-order model with dead time.
    /// </summary>
    internal static class StepResponseModelDialog
    {

        internal static void Show(MPCController mpc, Action<StepResponseModel> accepted)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm("Add Step Response Model", 520, 380, panel);

            if (mpc.ControlledVariables.Count == 0 || mpc.ManipulatedVariables.Count == 0)
            {
                panel.CreateAndAddDescriptionRow(
                    "Add at least one controlled and one manipulated variable first.");
                window.Show();
                return;
            }

            var nf = mpc.GetFlowsheet().FlowsheetOptions.NumberFormat;

            var controlled = panel.CreateAndAddDropDownRow("Controlled Variable",
                mpc.ControlledVariables.Select(x => x.Name).ToList(), 0, null);

            var manipulated = panel.CreateAndAddDropDownRow("Manipulated Variable",
                mpc.ManipulatedVariables.Select(x => x.Name).ToList(), 0, null);

            var model = new StepResponseModel();

            var gain = panel.CreateAndAddTextBoxRow(nf, "Gain", model.Gain, null);
            var timeConstant = panel.CreateAndAddTextBoxRow(nf, "Time Constant", model.TimeConstant, null);
            var deadTime = panel.CreateAndAddTextBoxRow(nf, "Dead Time", model.DeadTime, null);

            panel.CreateAndAddButtonRow("Add", null, (btn, e) =>
            {
                model.CVIndex = Math.Max(0, controlled.SelectedIndex);
                model.MVIndex = Math.Max(0, manipulated.SelectedIndex);

                if (UnitOpEditorRows.TryParse(gain.Text, out var g)) model.Gain = g;
                if (UnitOpEditorRows.TryParse(timeConstant.Text, out var t)) model.TimeConstant = t;
                if (UnitOpEditorRows.TryParse(deadTime.Text, out var d)) model.DeadTime = d;

                accepted(model);
                window.Close();
            });

            window.Show();
        }

    }

}
