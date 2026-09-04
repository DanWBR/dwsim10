using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using AnalogGauge = DWSIM.UnitOperations.UnitOperations.AnalogGauge;
using DigitalGauge = DWSIM.UnitOperations.UnitOperations.DigitalGauge;
using InfoCarrier = DWSIM.UnitOperations.SpecialOps.InformationCarrier;
using Input = DWSIM.UnitOperations.UnitOperations.Input;
using LevelGauge = DWSIM.UnitOperations.UnitOperations.LevelGauge;
using SpecialOpObjectInfo = DWSIM.UnitOperations.SpecialOps.Helpers.SpecialOpObjectInfo;
using Switch = DWSIM.UnitOperations.UnitOperations.Switch;
using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The property an indicator, a switch or an input points at: the object, the property, the
    /// units group and the unit it is read in, plus the value it currently holds. The four fields
    /// live directly on the object rather than in a record, so they are reached through delegates.
    /// </summary>
    internal sealed class MonitoredProperty
    {
        public Func<string> GetObjectID;
        public Action<string> SetObjectID;
        public Func<string> GetProperty;
        public Action<string> SetProperty;
        public Func<UnitOfMeasure> GetUnitsType;
        public Action<UnitOfMeasure> SetUnitsType;
        public Func<string> GetUnits;
        public Action<string> SetUnits;

        internal static MonitoredProperty Of(IIndicator indicator)
        {
            return new MonitoredProperty
            {
                GetObjectID = () => indicator.SelectedObjectID,
                SetObjectID = v => indicator.SelectedObjectID = v,
                GetProperty = () => indicator.SelectedProperty,
                SetProperty = v => indicator.SelectedProperty = v,
                GetUnitsType = () => indicator.SelectedPropertyType,
                SetUnitsType = v => indicator.SelectedPropertyType = v,
                GetUnits = () => indicator.SelectedPropertyUnits,
                SetUnits = v => indicator.SelectedPropertyUnits = v
            };
        }

        internal static MonitoredProperty Of(ISwitch sw)
        {
            return new MonitoredProperty
            {
                GetObjectID = () => sw.SelectedObjectID,
                SetObjectID = v => sw.SelectedObjectID = v,
                GetProperty = () => sw.SelectedProperty,
                SetProperty = v => sw.SelectedProperty = v,
                GetUnitsType = () => sw.SelectedPropertyType,
                SetUnitsType = v => sw.SelectedPropertyType = v,
                GetUnits = () => sw.SelectedPropertyUnits,
                SetUnits = v => sw.SelectedPropertyUnits = v
            };
        }

        internal static MonitoredProperty Of(IInput input)
        {
            return new MonitoredProperty
            {
                GetObjectID = () => input.SelectedObjectID,
                SetObjectID = v => input.SelectedObjectID = v,
                GetProperty = () => input.SelectedProperty,
                SetProperty = v => input.SelectedProperty = v,
                GetUnitsType = () => input.SelectedPropertyType,
                SetUnitsType = v => input.SelectedPropertyType = v,
                GetUnits = () => input.SelectedPropertyUnits,
                SetUnits = v => input.SelectedPropertyUnits = v
            };
        }
    }

    internal static class MonitoredPropertyRows
    {

        /// <summary>
        /// Adds the object, property, units group, unit and current value rows.
        /// <paramref name="writable"/> narrows the property list to what the object may write,
        /// which is what a switch and an input do.
        /// </summary>
        internal static void Add(AvaloniaEditorPanel panel, ISimulationObject owner,
                                 MonitoredProperty target, bool writable)
        {
            var flowsheet = owner.GetFlowsheet();
            var su = flowsheet.FlowsheetOptions.SelectedUnitSystem;
            var nf = flowsheet.FlowsheetOptions.NumberFormat;

            var objects = flowsheet.SimulationObjects.Values
                .Where(x => x.GraphicObject != null && x.Name != owner.Name)
                .OrderBy(x => x.GraphicObject.Tag)
                .ToList();

            if (objects.Count == 0)
            {
                panel.CreateAndAddDescriptionRow("No other objects on the flowsheet yet.");
                return;
            }

            var kind = writable ? PropertyType.WR : PropertyType.ALL;

            var properties = new List<string>();
            ComboBox propertyPicker = null, unitPicker = null;
            TextBlock valueLabel = null;

            var selected = objects.FindIndex(x => x.Name == target.GetObjectID());

            var objectPicker = panel.CreateAndAddDropDownRow("Object",
                objects.Select(x => x.GraphicObject.Tag).ToList(), Math.Max(0, selected), null);

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
                var property = target.GetProperty();

                if (obj == null || string.IsNullOrEmpty(property)) { valueLabel.Text = ""; return; }

                try
                {
                    var raw = Convert.ToDouble(obj.GetPropertyValue(property));
                    var unit = target.GetUnits();

                    if (string.IsNullOrEmpty(unit))
                    {
                        valueLabel.Text = Convert.ToDouble(obj.GetPropertyValue(property, su))
                                                 .ToString(nf, CultureInfo.CurrentCulture) + " " +
                                          obj.GetPropertyUnit(property, su);
                        return;
                    }

                    valueLabel.Text = cv.ConvertFromSI(unit, raw).ToString(nf, CultureInfo.CurrentCulture)
                                      + " " + unit;
                }
                catch (Exception)
                {
                    valueLabel.Text = "";
                }
            }

            List<string> UnitsOf(UnitOfMeasure type)
            {
                var units = new List<string> { "" };
                try { units.AddRange(su.GetUnitSet(type)); }
                catch (Exception) { }
                return units;
            }

            ReloadProperties();

            propertyPicker = panel.CreateAndAddDropDownRow("Property", properties.ToList(),
                Math.Max(0, properties.IndexOf(target.GetProperty() ?? "")), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex >= properties.Count) return;
                    target.SetProperty(properties[dd.SelectedIndex]);
                    ShowValue();
                });

            var groups = Enum.GetNames(typeof(UnitOfMeasure)).ToList();

            panel.CreateAndAddDropDownRow("Property Type", groups,
                Math.Max(0, groups.IndexOf(target.GetUnitsType().ToString())), (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    target.SetUnitsType((UnitOfMeasure)Enum.Parse(typeof(UnitOfMeasure), groups[dd.SelectedIndex]));

                    var refilled = UnitsOf(target.GetUnitsType());
                    if (unitPicker == null) return;
                    unitPicker.SetOptions(refilled);
                    unitPicker.SelectedIndex = Math.Max(0, refilled.IndexOf(target.GetUnits() ?? ""));
                });

            var units0 = UnitsOf(target.GetUnitsType());

            unitPicker = panel.CreateAndAddDropDownRow("Property Units", units0,
                Math.Max(0, units0.IndexOf(target.GetUnits() ?? "")), (dd, e) =>
                {
                    if (!(dd.SelectedItem is string picked)) return;
                    target.SetUnits(picked);
                    ShowValue();
                });

            valueLabel = panel.CreateAndAddTwoLabelsRow("Current Value", "");
            ShowValue();

            objectPicker.SelectionChanged += (s, e) =>
            {
                var obj = objects.ElementAtOrDefault(objectPicker.SelectedIndex);
                if (obj == null) return;

                target.SetObjectID(obj.Name);

                ReloadProperties();
                propertyPicker.SetOptions(properties);
                propertyPicker.SelectedIndex = Math.Max(0, properties.IndexOf(target.GetProperty() ?? ""));

                ShowValue();
                flowsheet.UpdateInterface();
            };
        }

        /// <summary>The four alarm levels the gauges share, each with its own switch.</summary>
        internal static Control BuildAlarms(ISimulationObject owner, IIndicator indicator)
        {
            var panel = new AvaloniaEditorPanel();
            var nf = owner.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddCheckBoxRow("Show Alarm Indicators", indicator.ShowAlarms,
                (cb, e) => indicator.ShowAlarms = cb.IsChecked.GetValueOrDefault());

            void Level(string label, Func<bool> enabled, Action<bool> setEnabled,
                       Func<double> value, Action<double> setValue)
            {
                panel.CreateAndAddCheckBoxRow(label, enabled(),
                    (cb, e) => setEnabled(cb.IsChecked.GetValueOrDefault()));

                panel.CreateAndAddTextBoxRow(nf, label + " Value", value(),
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) setValue(v); });
            }

            Level("Very Low", () => indicator.VeryLowAlarmEnabled, v => indicator.VeryLowAlarmEnabled = v,
                  () => indicator.VeryLowAlarmValue, v => indicator.VeryLowAlarmValue = v);

            Level("Low", () => indicator.LowAlarmEnabled, v => indicator.LowAlarmEnabled = v,
                  () => indicator.LowAlarmValue, v => indicator.LowAlarmValue = v);

            Level("High", () => indicator.HighAlarmEnabled, v => indicator.HighAlarmEnabled = v,
                  () => indicator.HighAlarmValue, v => indicator.HighAlarmValue = v);

            Level("Very High", () => indicator.VeryHighAlarmEnabled, v => indicator.VeryHighAlarmEnabled = v,
                  () => indicator.VeryHighAlarmValue, v => indicator.VeryHighAlarmValue = v);

            return panel;
        }

    }

    /// <summary>
    /// Analog and level gauge editors: the monitored property, the scale it is drawn on and the
    /// alarms, as the Windows gauge editors lay them out.
    /// </summary>
    public static class GaugeEditors
    {

        public static Control Build(AnalogGauge gauge)
        {
            return Scaled(gauge, gauge);
        }

        public static Control Build(LevelGauge gauge)
        {
            return Scaled(gauge, gauge);
        }

        /// <summary>A gauge that draws its value between a minimum and a maximum.</summary>
        private static Control Scaled(ISimulationObject owner, IIndicator indicator)
        {
            return UnitOpEditor.Build(owner,
                input: panel =>
                {
                    var nf = owner.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    MonitoredPropertyRows.Add(panel, owner, MonitoredProperty.Of(indicator),
                        writable: false);

                    panel.CreateAndAddTextBoxRow(nf, "Minimum Value", indicator.MinimumValue,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) indicator.MinimumValue = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Maximum Value", indicator.MaximumValue,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) indicator.MaximumValue = v; });

                    panel.CreateAndAddCheckBoxRow("Display as Percentage", indicator.DisplayInPercent,
                        (cb, e) => indicator.DisplayInPercent = cb.IsChecked.GetValueOrDefault());
                },
                propertyPackage: false,
                connections: false,
                extras: new[] { ("Alarms", MonitoredPropertyRows.BuildAlarms(owner, indicator)) });
        }

        public static Control Build(DigitalGauge gauge)
        {
            return UnitOpEditor.Build(gauge,
                input: panel =>
                {
                    var nf = gauge.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    MonitoredPropertyRows.Add(panel, gauge, MonitoredProperty.Of(gauge), writable: false);

                    // a digital gauge is sized by how many digits it prints, not by a scale
                    panel.CreateAndAddTextBoxRow(nf, "Integral Digits", gauge.IntegralDigits,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) gauge.IntegralDigits = (int)v; });

                    panel.CreateAndAddTextBoxRow(nf, "Decimal Digits", gauge.DecimalDigits,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) gauge.DecimalDigits = (int)v; });

                    panel.CreateAndAddCheckBoxRow("Display as Percentage", gauge.DisplayInPercent,
                        (cb, e) => gauge.DisplayInPercent = cb.IsChecked.GetValueOrDefault());
                },
                propertyPackage: false,
                connections: false,
                extras: new[] { ("Alarms", MonitoredPropertyRows.BuildAlarms(gauge, gauge)) });
        }

    }

    /// <summary>Switch editor: the property it writes and the two values it toggles between.</summary>
    public static class SwitchEditor
    {

        public static Control Build(Switch sw)
        {
            return UnitOpEditor.Build(sw,
                input: panel =>
                {
                    var nf = sw.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    MonitoredPropertyRows.Add(panel, sw, MonitoredProperty.Of(sw), writable: true);

                    panel.CreateAndAddTextBoxRow(nf, "Value when On", sw.OnValue,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) sw.OnValue = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Value when Off", sw.OffValue,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) sw.OffValue = v; });

                    panel.CreateAndAddCheckBoxRow("Switch is On", sw.IsOn,
                        (cb, e) => sw.IsOn = cb.IsChecked.GetValueOrDefault());
                },
                propertyPackage: false,
                connections: false);
        }

    }

    /// <summary>Input editor: the property the value typed on the flowsheet is written to.</summary>
    public static class InputEditor
    {

        public static Control Build(Input input)
        {
            return UnitOpEditor.Build(input,
                input: panel => MonitoredPropertyRows.Add(panel, input, MonitoredProperty.Of(input),
                    writable: true),
                propertyPackage: false,
                connections: false);
        }

    }

    /// <summary>
    /// Information carrier editor: the source variable and the up to three targets it copies the
    /// value to, with when the copy happens.
    /// </summary>
    public static class InfoCarrierEditor
    {

        /// <summary>When the value is carried across, in the order the Windows combo lists them.</summary>
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

        public static Control Build(InfoCarrier carrier)
        {
            return UnitOpEditor.Build(carrier,
                input: panel =>
                {
                    panel.CreateAndAddDropDownRow("Calculation Mode", CalculationModes,
                        Math.Min((int)carrier.CalculationMode, CalculationModes.Count - 1), (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            carrier.CalculationMode = (SpecCalcMode2)dd.SelectedIndex;
                        });
                },
                propertyPackage: false,
                connections: false,
                extras: new[] { ("Connections", BuildVariables(carrier)) });
        }

        private static Control BuildVariables(InfoCarrier carrier)
        {
            var panel = new AvaloniaEditorPanel();

            if (carrier.SourceObjectData == null) carrier.SourceObjectData = new SpecialOpObjectInfo();
            if (carrier.TargetObjectData == null) carrier.TargetObjectData = new SpecialOpObjectInfo();
            if (carrier.TargetObjectData2 == null) carrier.TargetObjectData2 = new SpecialOpObjectInfo();
            if (carrier.TargetObjectData3 == null) carrier.TargetObjectData3 = new SpecialOpObjectInfo();

            VariablePicker.Add(panel, carrier, (SpecialOpObjectInfo)carrier.SourceObjectData,
                VariablePicker.Role.InfoSource, "Source", writable: false);

            // the value is written to each target, so only writable properties qualify
            VariablePicker.Add(panel, carrier, (SpecialOpObjectInfo)carrier.TargetObjectData,
                VariablePicker.Role.InfoTarget1, "Target 1", writable: true);

            VariablePicker.Add(panel, carrier, (SpecialOpObjectInfo)carrier.TargetObjectData2,
                VariablePicker.Role.InfoTarget2, "Target 2", writable: true);

            VariablePicker.Add(panel, carrier, (SpecialOpObjectInfo)carrier.TargetObjectData3,
                VariablePicker.Role.InfoTarget3, "Target 3", writable: true);

            return panel;
        }

    }

}
