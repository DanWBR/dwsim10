using System;
using System.Collections.Generic;
using Avalonia.Controls;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Pump = DWSIM.UnitOperations.UnitOperations.Pump;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Pump editor, as the Windows EditingForm_Pump lays it out. Outlet temperature, temperature
    /// change and NPSH are results the form shows among the parameters, so they are read-only
    /// here too.
    /// </summary>
    public static class PumpEditor
    {

        private static readonly string[] Modes =
        {
            "Pressure Increase",
            "Outlet Pressure",
            "Power Required",
            "Energy Stream",
            "Performance Curves"
        };

        private static readonly Pump.CalculationMode[] ModeOrder =
        {
            Pump.CalculationMode.Delta_P,
            Pump.CalculationMode.OutletPressure,
            Pump.CalculationMode.Power,
            Pump.CalculationMode.EnergyStream,
            Pump.CalculationMode.Curves
        };

        public static Control Build(Pump pump)
        {
            return UnitOpEditor.Build(pump, panel =>
            {
                var nf = pump.GetFlowsheet().FlowsheetOptions.NumberFormat;

                UnitOpEditorRows.ValueRow pressureIncrease = null, outletPressure = null, power = null;
                TextBox efficiency = null, speed = null;
                Button curves = null;

                void ApplyMode()
                {
                    var mode = pump.CalcMode;
                    if (pressureIncrease != null) pressureIncrease.IsEnabled = mode == Pump.CalculationMode.Delta_P;
                    if (outletPressure != null) outletPressure.IsEnabled = mode == Pump.CalculationMode.OutletPressure;
                    if (power != null) power.IsEnabled = mode == Pump.CalculationMode.Power;
                    if (efficiency != null) efficiency.IsEnabled = mode != Pump.CalculationMode.Curves;
                    if (curves != null) curves.IsEnabled = mode == Pump.CalculationMode.Curves;
                    if (speed != null) speed.IsEnabled = mode == Pump.CalculationMode.Curves;
                }

                panel.CreateAndAddDropDownRow("Calculation Type", new List<string>(Modes),
                    Math.Max(0, Array.IndexOf(ModeOrder, pump.CalcMode)), (dd, e) =>
                    {
                        if (dd.SelectedIndex < 0 || dd.SelectedIndex >= ModeOrder.Length) return;
                        pump.CalcMode = ModeOrder[dd.SelectedIndex];
                        ApplyMode();
                        panel.OnAfterEdit?.Invoke();
                    });

                pressureIncrease = panel.CreateAndAddValueUnitRow(pump, "Pressure Increase",
                    UnitOfMeasure.deltaP, pump.DeltaP.GetValueOrDefault(), v => pump.DeltaP = v);

                outletPressure = panel.CreateAndAddValueUnitRow(pump, "Outlet Pressure",
                    UnitOfMeasure.pressure, pump.Pout, v => pump.Pout = v);

                efficiency = panel.CreateAndAddTextBoxRow(nf, "Efficiency (0-100%)",
                    pump.Eficiencia.GetValueOrDefault(),
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pump.Eficiencia = v; });

                // results the Windows form keeps among the parameters
                panel.CreateAndAddResultRow(pump, "Outlet Temperature", UnitOfMeasure.temperature,
                    pump.OutletTemperature);
                panel.CreateAndAddResultRow(pump, "Temperature Change", UnitOfMeasure.deltaT,
                    pump.DeltaT.GetValueOrDefault());

                power = panel.CreateAndAddValueUnitRow(pump, "Power Required",
                    UnitOfMeasure.heatflow, pump.DeltaQ.GetValueOrDefault(), v => pump.DeltaQ = v);

                panel.CreateAndAddResultRow(pump, "NPSH Available", UnitOfMeasure.distance,
                    pump.NPSH.GetValueOrDefault());

                curves = panel.CreateAndAddButtonRow("Edit Performance Curves", null,
                    (btn, e) => PerformanceCurvesEditor.Show(pump, pump.PumpCurveSet,
                        pump.GraphicObject.Tag + ": Performance Curves"));

                speed = panel.CreateAndAddTextBoxRow(nf, "Operating Speed (rpm)",
                    pump.EffectiveSpeed,
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pump.OperatingSpeed = v; });

                ApplyMode();
            });
        }

    }

}
