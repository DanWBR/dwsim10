using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Adjust = DWSIM.UnitOperations.SpecialOps.Adjust;
using EnergyRecycle = DWSIM.UnitOperations.SpecialOps.EnergyRecycle;
using Recycle = DWSIM.UnitOperations.SpecialOps.Recycle;
using Spec = DWSIM.UnitOperations.SpecialOps.Spec;
using SpecialOpObjectInfo = DWSIM.UnitOperations.SpecialOps.Helpers.SpecialOpObjectInfo;
using Thickness = Avalonia.Thickness;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The variable a logical block points at: an object, one of its properties and the value it
    /// currently holds, which is the block both the Adjust and the Spec editors repeat.
    /// </summary>
    internal static class VariablePicker
    {

        /// <summary>What the block does with the object it points at, so it can be tagged.</summary>
        internal enum Role
        {
            AdjustManipulated,
            AdjustControlled,
            AdjustReferenced,
            SpecSource,
            SpecTarget,
            InfoSource,
            InfoTarget1,
            InfoTarget2,
            InfoTarget3
        }

        /// <summary>
        /// Adds the object and property rows plus the current value line. <paramref name="writable"/>
        /// narrows the property list to what the block is allowed to write, as the Windows editors
        /// do for the manipulated and target variables. <paramref name="withUnits"/> adds the units
        /// group and unit pickers the controllers keep, which read the value in a unit of their own
        /// rather than in the one of the simulation.
        /// </summary>
        internal static void Add(AvaloniaEditorPanel panel, ISimulationObject block,
                                 SpecialOpObjectInfo info, Role role, string label,
                                 bool writable, bool withUnits = false, Action changed = null)
        {
            var flowsheet = block.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            var objects = flowsheet.SimulationObjects.Values
                .Where(x => x.GraphicObject != null && x.Name != block.Name)
                .OrderBy(x => x.GraphicObject.Tag)
                .ToList();

            if (objects.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("No other objects on the flowsheet yet.");
                return;
            }

            var tags = objects.Select(x => x.GraphicObject.Tag).ToList();
            var kind = writable ? PropertyType.WR : PropertyType.ALL;

            var properties = new List<string>();
            ComboBox propertyPicker = null;
            TextBlock valueLabel = null;

            var selected = objects.FindIndex(x => x.Name == info.ID);
            var objectPicker = panel.CreateAndAddDropDownRow(label + " Object", tags,
                Math.Max(0, selected), null);

            void ReloadProperties()
            {
                properties.Clear();
                var obj = objects.ElementAtOrDefault(objectPicker.SelectedIndex);
                if (obj == null) return;
                properties.AddRange(obj.GetProperties(kind) ?? new string[0]);
            }

            void ShowValue()
            {
                if (valueLabel == null) return;

                var obj = objects.ElementAtOrDefault(objectPicker.SelectedIndex);
                if (obj == null || string.IsNullOrEmpty(info.PropertyName)) { valueLabel.Text = ""; return; }

                try
                {
                    // with a unit of its own the value is read raw and converted into it
                    if (withUnits && !string.IsNullOrEmpty(info.Units))
                    {
                        var raw = Convert.ToDouble(obj.GetPropertyValue(info.PropertyName));
                        valueLabel.Text = cv.ConvertFromSI(info.Units, raw).ToString(nf, CultureInfo.CurrentCulture)
                                          + " " + info.Units;
                        return;
                    }

                    var value = Convert.ToDouble(obj.GetPropertyValue(info.PropertyName, su));
                    valueLabel.Text = value.ToString(nf, CultureInfo.CurrentCulture) + " " +
                                      obj.GetPropertyUnit(info.PropertyName, su);
                }
                catch (Exception)
                {
                    valueLabel.Text = "";
                }
            }

            void StoreProperty()
            {
                var obj = objects.ElementAtOrDefault(objectPicker.SelectedIndex);
                if (obj == null || propertyPicker == null) return;

                var index = propertyPicker.SelectedIndex;
                if (index < 0 || index >= properties.Count) return;

                info.PropertyName = properties[index];

                // the controllers keep the unit the user picked, the other blocks follow the property
                if (!withUnits)
                {
                    info.Units = obj.GetPropertyUnit(info.PropertyName, su);
                    info.UnitsType = su.GetUnitType(info.Units);
                }

                ShowValue();
                if (changed != null) changed();
            }

            ReloadProperties();

            propertyPicker = panel.CreateAndAddDropDownRow(label + " Property", properties.ToList(),
                Math.Max(0, properties.IndexOf(info.PropertyName ?? "")), (dd, e) => StoreProperty());

            if (withUnits) AddUnitPickers(panel, su, info, label, () => ShowValue());

            valueLabel = panel.CreateAndAddTwoLabelsRow("Current Value", "");
            ShowValue();

            objectPicker.SelectionChanged += (s, e) =>
            {
                var obj = objects.ElementAtOrDefault(objectPicker.SelectedIndex);
                if (obj == null) return;

                Detach(flowsheet, info, role);

                info.ID = obj.Name;
                info.Name = obj.GraphicObject.Tag;
                info.ObjectType = obj.GraphicObject.ObjectType.ToString();

                Attach(block, obj, role);

                ReloadProperties();
                // the picker was populated through Items (CreateAndAddDropDownRow), and Avalonia throws
                // if ItemsSource is then assigned on top of that, so refill Items in place instead.
                propertyPicker.Items.Clear();
                foreach (var p in properties) propertyPicker.Items.Add(p);
                propertyPicker.SelectedIndex = Math.Max(0, properties.IndexOf(info.PropertyName ?? ""));

                StoreProperty();
                flowsheet.UpdateInterface();
            };
        }

        /// <summary>
        /// The units group and the unit inside it, which is how the controllers store the unit
        /// they read and write a variable in, independently of the simulation's unit system.
        /// </summary>
        private static void AddUnitPickers(AvaloniaEditorPanel panel, IUnitsOfMeasure su,
                                           SpecialOpObjectInfo info, string label, Action changed)
        {
            var groups = Enum.GetNames(typeof(UnitOfMeasure)).ToList();

            ComboBox unitPicker = null;

            List<string> UnitsOf(UnitOfMeasure type)
            {
                var units = new List<string> { "" };
                try { units.AddRange(su.GetUnitSet(type)); }
                catch (Exception) { }
                return units;
            }

            var groupPicker = panel.CreateAndAddDropDownRow(label + " Units Group", groups,
                Math.Max(0, groups.IndexOf(info.UnitsType.ToString())), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    info.UnitsType = (UnitOfMeasure)Enum.Parse(typeof(UnitOfMeasure), groups[dd.SelectedIndex]);

                    var refilled = UnitsOf(info.UnitsType);
                    if (unitPicker == null) return;
                    unitPicker.Items.Clear();
                    foreach (var u in refilled) unitPicker.Items.Add(u);
                    unitPicker.SelectedIndex = Math.Max(0, refilled.IndexOf(info.Units ?? ""));
                });

            var units0 = UnitsOf(info.UnitsType);

            unitPicker = panel.CreateAndAddDropDownRow(label + " Units", units0,
                Math.Max(0, units0.IndexOf(info.Units ?? "")), (dd, e) =>
                {
                    if (!(dd.SelectedItem is string picked)) return;
                    info.Units = picked;
                    changed();
                });
        }

        /// <summary>Clears the marker the block left on the object it used to point at.</summary>
        private static void Detach(IFlowsheet flowsheet, SpecialOpObjectInfo info, Role role)
        {
            if (string.IsNullOrEmpty(info.ID) || !flowsheet.SimulationObjects.ContainsKey(info.ID)) return;

            var previous = flowsheet.SimulationObjects[info.ID];

            if (role >= Role.InfoSource)
            {
                previous.IsInfoCarrierAttached = false;
                previous.AttachedInfoCarrierId = "";
                previous.InfoCarrierVarType = SpecVarType.None;
            }
            else if (role == Role.SpecSource || role == Role.SpecTarget)
            {
                previous.IsSpecAttached = false;
                previous.AttachedSpecId = "";
                previous.SpecVarType = SpecVarType.None;
            }
            else
            {
                previous.IsAdjustAttached = false;
                previous.AttachedAdjustId = "";
                previous.AdjustVarType = AdjustVarType.None;
            }
        }

        /// <summary>
        /// Marks the object as belonging to this block and points the drawing at it, which is what
        /// draws the dashed line between the block and the object on the flowsheet.
        /// </summary>
        private static void Attach(ISimulationObject block, ISimulationObject target, Role role)
        {
            switch (role)
            {
                case Role.InfoSource:
                case Role.InfoTarget1:
                case Role.InfoTarget2:
                case Role.InfoTarget3:
                    target.IsInfoCarrierAttached = true;
                    target.AttachedInfoCarrierId = block.Name;
                    target.InfoCarrierVarType = role == Role.InfoSource
                        ? SpecVarType.Source
                        : SpecVarType.Target;
                    break;
                case Role.SpecSource:
                case Role.SpecTarget:
                    target.IsSpecAttached = true;
                    target.AttachedSpecId = block.Name;
                    target.SpecVarType = role == Role.SpecSource ? SpecVarType.Source : SpecVarType.Target;
                    break;
                default:
                    target.IsAdjustAttached = true;
                    target.AttachedAdjustId = block.Name;
                    target.AdjustVarType = role == Role.AdjustManipulated
                        ? AdjustVarType.Manipulated
                        : AdjustVarType.Controlled;
                    break;
            }

            var carrier = block as DWSIM.UnitOperations.SpecialOps.InformationCarrier;
            if (carrier != null)
            {
                var drawing = block.GraphicObject as DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.InformationCarrierGraphic;
                var shape = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                var casted = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;

                switch (role)
                {
                    case Role.InfoSource:
                        carrier.SourceObject = casted;
                        if (drawing != null) drawing.ConnectedToSv = shape;
                        break;
                    case Role.InfoTarget1:
                        carrier.TargetObject = casted;
                        if (drawing != null) drawing.ConnectedToTv = shape;
                        break;
                    case Role.InfoTarget2:
                        carrier.TargetObject2 = casted;
                        if (drawing != null) drawing.ConnectedToTv2 = shape;
                        break;
                    case Role.InfoTarget3:
                        carrier.TargetObject3 = casted;
                        if (drawing != null) drawing.ConnectedToTv3 = shape;
                        break;
                }

                return;
            }

            var spec = block as Spec;
            if (spec != null)
            {
                var drawing = block.GraphicObject as DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SpecGraphic;

                if (role == Role.SpecSource)
                {
                    spec.SourceObject = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;
                    if (drawing != null) drawing.ConnectedToSv = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                }
                else
                {
                    spec.TargetObject = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;
                    if (drawing != null) drawing.ConnectedToTv = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                }

                return;
            }

            var pid = block as DWSIM.UnitOperations.SpecialOps.PIDController;
            if (pid != null)
            {
                var drawing = block.GraphicObject as DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PIDControllerGraphic;

                if (role == Role.AdjustManipulated)
                {
                    pid.ManipulatedObject = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;
                    if (drawing != null) drawing.ConnectedToMv = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                }
                else
                {
                    pid.ControlledObject = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;
                    if (drawing != null) drawing.ConnectedToCv = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                }

                return;
            }

            var python = block as DWSIM.UnitOperations.SpecialOps.PythonController;
            if (python != null)
            {
                var drawing = block.GraphicObject as DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PythonControllerGraphic;

                if (role == Role.AdjustManipulated)
                {
                    python.ManipulatedObject = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;
                    if (drawing != null) drawing.ConnectedToMv = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                }
                else
                {
                    python.ControlledObject = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;
                    if (drawing != null) drawing.ConnectedToCv = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                }

                return;
            }

            var adjust = block as Adjust;
            if (adjust == null) return;

            var graphic = block.GraphicObject as DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.AdjustGraphic;

            switch (role)
            {
                case Role.AdjustManipulated:
                    adjust.ManipulatedObject = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;
                    if (graphic != null) graphic.ConnectedToMv = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                    break;
                case Role.AdjustControlled:
                    adjust.ControlledObject = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;
                    if (graphic != null) graphic.ConnectedToCv = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                    break;
                case Role.AdjustReferenced:
                    adjust.ReferenceObject = (DWSIM.SharedClasses.UnitOperations.BaseClass)target;
                    if (graphic != null) graphic.ConnectedToRv = (DWSIM.Drawing.SkiaSharp.GraphicObjects.GraphicObject)target.GraphicObject;
                    break;
            }
        }

    }

    /// <summary>
    /// Adjust editor, as the Windows EditingForm_Adjust lays it out: the three linked variables,
    /// the set point and the tolerance, and the control panel that solves the block on its own.
    /// </summary>
    public static class AdjustEditor
    {

        public static Control Build(Adjust adjust)
        {
            return UnitOpEditor.Build(adjust,
                input: null,
                propertyPackage: false,
                connections: false,
                extras: new[]
                {
                    ("Linked Objects", BuildLinkedObjects(adjust)),
                    ("Parameters", BuildParameters(adjust))
                });
        }

        private static Control BuildLinkedObjects(Adjust adjust)
        {
            var panel = new AvaloniaEditorPanel();

            if (adjust.ManipulatedObjectData == null)
                adjust.ManipulatedObjectData = new SpecialOpObjectInfo();
            if (adjust.ControlledObjectData == null)
                adjust.ControlledObjectData = new SpecialOpObjectInfo();
            if (adjust.ReferencedObjectData == null)
                adjust.ReferencedObjectData = new SpecialOpObjectInfo();

            // the manipulated variable is written by the block, so only writable properties qualify
            VariablePicker.Add(panel, adjust, (SpecialOpObjectInfo)adjust.ManipulatedObjectData,
                VariablePicker.Role.AdjustManipulated, "Manipulated", writable: true);

            VariablePicker.Add(panel, adjust, (SpecialOpObjectInfo)adjust.ControlledObjectData,
                VariablePicker.Role.AdjustControlled, "Controlled", writable: false);

            // with this on, the set point is an offset from the reference variable instead of an
            // absolute value of the controlled one
            panel.CreateAndAddCheckBoxRow("Reference Object", adjust.Referenced,
                (cb, e) => adjust.Referenced = cb.IsChecked.GetValueOrDefault());

            VariablePicker.Add(panel, adjust, (SpecialOpObjectInfo)adjust.ReferencedObjectData,
                VariablePicker.Role.AdjustReferenced, "Reference", writable: false);

            return panel;
        }

        private static Control BuildParameters(Adjust adjust)
        {
            var panel = new AvaloniaEditorPanel();
            var flowsheet = adjust.GetFlowsheet();
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            // the set point is written in the unit of the controlled property, or of the reference
            // property when the adjust is offset from another variable
            panel.CreateAndAddTextBoxRow(nf, "Set-Point/Offset (Controlled Property)",
                SetPointInDisplayUnit(adjust),
                (tb, e) =>
                {
                    if (!UnitOpEditorRows.TryParse(tb.Text, out var v)) return;
                    adjust.AdjustValue = SetPointToSI(adjust, v);
                });

            panel.CreateAndAddTwoLabelsRow("Set-Point Unit", SetPointUnit(adjust));

            panel.CreateAndAddTextBoxRow(nf, "Tolerance (Maximum Error)", adjust.Tolerance,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) adjust.Tolerance = v; });

            Button controlPanel = null;

            panel.CreateAndAddCheckBoxRow("Converge/Solve with Flowsheet Solver",
                adjust.SimultaneousAdjust, (cb, e) =>
                {
                    adjust.SimultaneousAdjust = cb.IsChecked.GetValueOrDefault();
                    // the control panel solves the block on its own, which the global solver replaces
                    if (controlPanel != null) controlPanel.IsEnabled = !adjust.SimultaneousAdjust;
                });

            controlPanel = panel.CreateAndAddButtonRow("Control Panel", null,
                (btn, e) => AdjustControlPanel.Show(adjust));
            controlPanel.IsEnabled = !adjust.SimultaneousAdjust;

            return panel;
        }

        /// <summary>The unit the set point is written in, which the reference variable can change.</summary>
        internal static string SetPointUnit(Adjust adjust)
        {
            var flowsheet = adjust.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;

            var info = adjust.Referenced ? adjust.ReferencedObjectData : adjust.ControlledObjectData;
            if (info == null || string.IsNullOrEmpty(info.ID)) return "";
            if (!flowsheet.SimulationObjects.ContainsKey(info.ID)) return "";

            return flowsheet.SimulationObjects[info.ID].GetPropertyUnit(info.PropertyName, su);
        }

        internal static double SetPointInDisplayUnit(Adjust adjust)
        {
            var unit = SetPointUnit(adjust);
            if (string.IsNullOrEmpty(unit)) return adjust.AdjustValue;

            // an offset from a reference is a temperature difference, not a temperature
            return cv.ConvertFromSI(OffsetUnit(adjust, unit), adjust.AdjustValue);
        }

        internal static double SetPointToSI(Adjust adjust, double value)
        {
            var unit = SetPointUnit(adjust);
            if (string.IsNullOrEmpty(unit)) return value;

            return cv.ConvertToSI(OffsetUnit(adjust, unit), value);
        }

        /// <summary>
        /// A referenced set point is a difference, and the converter spells a temperature
        /// difference with a trailing dot, as the Windows editor does before converting.
        /// </summary>
        private static string OffsetUnit(Adjust adjust, string unit)
        {
            if (!adjust.Referenced) return unit;

            var su = adjust.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem;
            return su.GetUnitType(unit) == UnitOfMeasure.temperature ? unit + "." : unit;
        }

    }

    /// <summary>
    /// Spec editor, as the Windows EditingForm_Spec lays it out: the source and target variables,
    /// when the block runs, and the expression that relates the two.
    /// </summary>
    public static class SpecEditor
    {

        /// <summary>When the block is calculated, in the order the Windows combo lists them.</summary>
        private static readonly List<string> CalculationModes = new List<string>
        {
            "Global Setting",
            "After Source Object",
            "Before Target Object",
            "Before Flowsheet",
            "After Flowsheet",
            "After Object",
            "Before Object"
        };

        public static Control Build(Spec spec)
        {
            return UnitOpEditor.Build(spec,
                input: null,
                propertyPackage: false,
                connections: false,
                extras: new[]
                {
                    ("Connections", BuildVariables(spec)),
                    ("Calculation Parameters", BuildParameters(spec)),
                    ("Expression", BuildExpression(spec))
                });
        }

        private static Control BuildVariables(Spec spec)
        {
            var panel = new AvaloniaEditorPanel();

            if (spec.SourceObjectData == null) spec.SourceObjectData = new SpecialOpObjectInfo();
            if (spec.TargetObjectData == null) spec.TargetObjectData = new SpecialOpObjectInfo();

            VariablePicker.Add(panel, spec, (SpecialOpObjectInfo)spec.SourceObjectData,
                VariablePicker.Role.SpecSource, "Source", writable: false);

            // the target is written by the block, so only writable properties qualify
            VariablePicker.Add(panel, spec, (SpecialOpObjectInfo)spec.TargetObjectData,
                VariablePicker.Role.SpecTarget, "Target", writable: true);

            return panel;
        }

        private static Control BuildParameters(Spec spec)
        {
            var panel = new AvaloniaEditorPanel();
            var flowsheet = spec.GetFlowsheet();

            var objects = flowsheet.SimulationObjects.Values
                .Where(x => x.GraphicObject != null && x.Name != spec.Name)
                .OrderBy(x => x.GraphicObject.Tag)
                .ToList();

            var tags = objects.Select(x => x.GraphicObject.Tag).ToList();
            var selected = objects.FindIndex(x => x.Name == spec.ReferenceObjectID);

            ComboBox reference = null;

            panel.CreateAndAddDropDownRow("Calculation Mode", CalculationModes,
                (int)spec.SpecCalculationMode, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    spec.SpecCalculationMode = (SpecCalcMode2)dd.SelectedIndex;

                    // only the two object-relative modes read the reference
                    if (reference != null) reference.IsEnabled = ReadsReference(spec.SpecCalculationMode);
                });

            reference = panel.CreateAndAddDropDownRow("Reference Object", tags,
                Math.Max(0, selected), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex >= objects.Count) return;
                    spec.ReferenceObjectID = objects[dd.SelectedIndex].Name;
                });

            reference.IsEnabled = ReadsReference(spec.SpecCalculationMode);

            return panel;
        }

        private static bool ReadsReference(SpecCalcMode2 mode)
        {
            return mode == SpecCalcMode2.AfterObject || mode == SpecCalcMode2.BeforeObject;
        }

        private static Control BuildExpression(Spec spec)
        {
            var panel = new AvaloniaEditorPanel();

            panel.CreateAndAddDescriptionRow("Y = Target Variable, X = Source Variable");

            var result = panel.CreateAndAddTwoLabelsRow("Result", "");

            var expression = panel.CreateAndAddStringEditorRow("Expression", spec.Expression ?? "X", null);

            void Evaluate()
            {
                spec.Expression = expression.Text ?? "";

                try
                {
                    var flowsheet = spec.GetFlowsheet();
                    var units = flowsheet.FlowsheetOptions.SelectedUnitSystem;
                    var target = flowsheet.SimulationObjects[spec.TargetObjectData.ID];

                    result.Text = "Y = " + spec.ParseExpression() + " " +
                                  target.GetPropertyUnit(spec.TargetObjectData.PropertyName, units);

                    expression.Foreground = new global::Avalonia.Media.SolidColorBrush(
                        global::Avalonia.Media.Colors.Blue);
                }
                catch (Exception)
                {
                    result.Text = "Error";
                    expression.Foreground = new global::Avalonia.Media.SolidColorBrush(
                        global::Avalonia.Media.Colors.Red);
                }
            }

            expression.TextChanged += (s, e) => Evaluate();
            Evaluate();

            return panel;
        }

    }

    /// <summary>
    /// Recycle editor, as the Windows EditingForm_Recycle lays it out: the convergence settings
    /// above and the per-variable tolerances with their current errors below.
    /// </summary>
    public static class RecycleEditor
    {

        public static Control Build(Recycle recycle)
        {
            return UnitOpEditor.Build(recycle,
                input: panel =>
                {
                    var nf = recycle.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    CheckBox legacy = null;
                    Slider smoothing = null;

                    panel.CreateAndAddTextBoxRow(nf, "Maximum Iterations", recycle.MaximumIterations,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) recycle.MaximumIterations = (int)v; });

                    var broyden = panel.CreateAndAddCheckBoxRow("Global Convergence (Broyden)",
                        recycle.AccelerationMethod == AccelMethod.GlobalBroyden, (cb, e) =>
                        {
                            var on = cb.IsChecked.GetValueOrDefault();
                            recycle.AccelerationMethod = on ? AccelMethod.GlobalBroyden : AccelMethod.None;

                            // Broyden converges the whole loop, so neither of the two applies
                            if (legacy != null) legacy.IsEnabled = !on;
                            if (smoothing != null) smoothing.IsEnabled = !on;
                        });

                    legacy = panel.CreateAndAddCheckBoxRow("Legacy Mode", recycle.LegacyMode, (cb, e) =>
                    {
                        recycle.LegacyMode = cb.IsChecked.GetValueOrDefault();
                        if (smoothing != null) smoothing.IsEnabled = !recycle.LegacyMode;
                    });

                    smoothing = AddSmoothingRow(panel, recycle);

                    var global = recycle.AccelerationMethod == AccelMethod.GlobalBroyden;
                    legacy.IsEnabled = !global;
                    smoothing.IsEnabled = !global && !recycle.LegacyMode;
                },
                results: panel =>
                {
                    panel.CreateAndAddTwoLabelsRow("Converged", recycle.Converged ? "Yes" : "No");
                    panel.CreateAndAddTwoLabelsRow("Iterations Taken", recycle.IterationsTaken.ToString());
                },
                propertyPackage: false,
                extras: new[]
                {
                    ("Convergence Tolerances and Current Errors", BuildTolerances(recycle))
                });
        }

        /// <summary>The 0.1 to 1.0 slider the Windows editor uses for the smoothing factor.</summary>
        private static Slider AddSmoothingRow(AvaloniaEditorPanel panel, Recycle recycle)
        {
            var slider = new Slider
            {
                Minimum = 0.1,
                Maximum = 1.0,
                TickFrequency = 0.1,
                IsSnapToTickEnabled = true,
                Value = recycle.SmoothingFactor,
                MinWidth = 160
            };

            var value = new TextBlock
            {
                Text = recycle.SmoothingFactor.ToString("N1", CultureInfo.CurrentCulture),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            };

            slider.PropertyChanged += (s, e) =>
            {
                if (e.Property != Slider.ValueProperty) return;
                recycle.SmoothingFactor = slider.Value;
                value.Text = slider.Value.ToString("N1", CultureInfo.CurrentCulture);
            };

            var row = new DockPanel();
            DockPanel.SetDock(value, global::Avalonia.Controls.Dock.Right);
            row.Children.Add(value);
            row.Children.Add(slider);

            panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow("Smoothing Factor (0.1-1.0)", row));

            return slider;
        }

        private static Control BuildTolerances(Recycle recycle)
        {
            var panel = new AvaloniaEditorPanel();
            var parameters = recycle.ConvergenceParameters;
            var history = recycle.ConvergenceHistory;

            panel.CreateAndAddValueUnitRow(recycle, "Temperature Tolerance", UnitOfMeasure.deltaT,
                parameters.Temperatura, v => parameters.Temperatura = v);
            panel.CreateAndAddResultRow(recycle, "Temperature Error", UnitOfMeasure.deltaT,
                history.TemperaturaE);

            panel.CreateAndAddValueUnitRow(recycle, "Pressure Tolerance", UnitOfMeasure.deltaP,
                parameters.Pressao, v => parameters.Pressao = v);
            panel.CreateAndAddResultRow(recycle, "Pressure Error", UnitOfMeasure.deltaP,
                history.PressaoE);

            panel.CreateAndAddValueUnitRow(recycle, "Mass Flow Tolerance", UnitOfMeasure.massflow,
                parameters.VazaoMassica, v => parameters.VazaoMassica = v);
            panel.CreateAndAddResultRow(recycle, "Mass Flow Error", UnitOfMeasure.massflow,
                history.VazaoMassicaE);

            return panel;
        }

    }

    /// <summary>
    /// Energy recycle editor: the same shape as the material one, with the single energy flow
    /// tolerance the block converges on.
    /// </summary>
    public static class EnergyRecycleEditor
    {

        public static Control Build(EnergyRecycle recycle)
        {
            return UnitOpEditor.Build(recycle,
                input: panel =>
                {
                    var nf = recycle.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    panel.CreateAndAddTextBoxRow(nf, "Maximum Iterations", recycle.MaximumIterations,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) recycle.MaximumIterations = (int)v; });

                    panel.CreateAndAddCheckBoxRow("Global Convergence (Broyden)",
                        recycle.AccelerationMethod == AccelMethod.GlobalBroyden,
                        (cb, e) => recycle.AccelerationMethod = cb.IsChecked.GetValueOrDefault()
                            ? AccelMethod.GlobalBroyden
                            : AccelMethod.None);
                },
                results: panel =>
                {
                    panel.CreateAndAddTwoLabelsRow("Iterations Taken", recycle.IterationsTaken.ToString());
                },
                propertyPackage: false,
                extras: new[]
                {
                    ("Convergence Tolerances and Current Errors", BuildTolerances(recycle))
                });
        }

        private static Control BuildTolerances(EnergyRecycle recycle)
        {
            var panel = new AvaloniaEditorPanel();

            panel.CreateAndAddValueUnitRow(recycle, "Energy Flow Tolerance", UnitOfMeasure.heatflow,
                recycle.ConvergenceParameters.Energy, v => recycle.ConvergenceParameters.Energy = v);

            panel.CreateAndAddResultRow(recycle, "Energy Flow Error", UnitOfMeasure.heatflow,
                recycle.ConvergenceHistory.EnergyE);

            return panel;
        }

    }

}
