using System;
using System.Collections.Generic;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Cooler = DWSIM.UnitOperations.UnitOperations.Cooler;
using Heater = DWSIM.UnitOperations.UnitOperations.Heater;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Heater and cooler editor, as the Windows EditingForm_HeaterCooler lays it out. The two
    /// share the form: the cooler has the same parameters minus the energy stream calculation
    /// mode, and reads its duty as heat removed.
    /// </summary>
    public static class HeaterCoolerEditor
    {

        private static readonly string[] HeaterModes =
        {
            "Heat Added/Removed",
            "Temperature Change",
            "Outlet Temperature",
            "Outlet Vapor Mole Fraction",
            "Energy Stream"
        };

        /// <summary>
        /// The calculation modes in the order the Windows combo lists them, which is not the
        /// order of the enum.
        /// </summary>
        private static readonly Heater.CalculationMode[] HeaterModeOrder =
        {
            Heater.CalculationMode.HeatAdded,
            Heater.CalculationMode.TemperatureChange,
            Heater.CalculationMode.OutletTemperature,
            Heater.CalculationMode.OutletVaporFraction,
            Heater.CalculationMode.EnergyStream
        };

        private static readonly Cooler.CalculationMode[] CoolerModeOrder =
        {
            Cooler.CalculationMode.HeatRemoved,
            Cooler.CalculationMode.TemperatureChange,
            Cooler.CalculationMode.OutletTemperature,
            Cooler.CalculationMode.OutletVaporFraction
        };

        public static Control Build(Heater heater)
        {
            return UnitOpEditor.Build(heater, panel => Fill(panel, heater,
                modes: new List<string>(HeaterModes),
                modeIndex: Math.Max(0, Array.IndexOf(HeaterModeOrder, heater.CalcMode)),
                setMode: index => heater.CalcMode = HeaterModeOrder[index],
                efficiency: () => heater.Eficiencia.GetValueOrDefault(), setEfficiency: v => heater.Eficiencia = v,
                duty: () => heater.DeltaQ.GetValueOrDefault(), setDuty: v => heater.DeltaQ = v,
                outletT: () => heater.OutletTemperature.GetValueOrDefault(), setOutletT: v => heater.OutletTemperature = v,
                deltaT: () => heater.DeltaT.GetValueOrDefault(), setDeltaT: v => heater.DeltaT = v,
                vaporFraction: () => heater.OutletVaporFraction.GetValueOrDefault(), setVaporFraction: v => heater.OutletVaporFraction = v,
                deltaP: () => heater.DeltaP.GetValueOrDefault(), setDeltaP: v => heater.DeltaP = v));
        }

        public static Control Build(Cooler cooler)
        {
            // the cooler has no energy stream mode
            var modes = new List<string>(HeaterModes);
            modes.RemoveAt(modes.Count - 1);

            return UnitOpEditor.Build(cooler, panel => Fill(panel, cooler,
                modes: modes,
                modeIndex: Math.Max(0, Array.IndexOf(CoolerModeOrder, cooler.CalcMode)),
                setMode: index => cooler.CalcMode = CoolerModeOrder[index],
                efficiency: () => cooler.Eficiencia.GetValueOrDefault(), setEfficiency: v => cooler.Eficiencia = v,
                duty: () => cooler.DeltaQ.GetValueOrDefault(), setDuty: v => cooler.DeltaQ = v,
                outletT: () => cooler.OutletTemperature.GetValueOrDefault(), setOutletT: v => cooler.OutletTemperature = v,
                deltaT: () => cooler.DeltaT.GetValueOrDefault(), setDeltaT: v => cooler.DeltaT = v,
                vaporFraction: () => cooler.OutletVaporFraction.GetValueOrDefault(), setVaporFraction: v => cooler.OutletVaporFraction = v,
                deltaP: () => cooler.DeltaP.GetValueOrDefault(), setDeltaP: v => cooler.DeltaP = v));
        }

        private static void Fill(AvaloniaEditorPanel panel, ISimulationObject simobj,
            List<string> modes, int modeIndex, Action<int> setMode,
            Func<double> efficiency, Action<double> setEfficiency,
            Func<double> duty, Action<double> setDuty,
            Func<double> outletT, Action<double> setOutletT,
            Func<double> deltaT, Action<double> setDeltaT,
            Func<double> vaporFraction, Action<double> setVaporFraction,
            Func<double> deltaP, Action<double> setDeltaP)
        {
            var nf = simobj.GetFlowsheet().FlowsheetOptions.NumberFormat;

            UnitOpEditorRows.ValueRow dutyRow = null, outletTRow = null, deltaTRow = null;
            TextBox vaporFractionBox = null;
            var selected = modeIndex;

            // every mode but the energy stream one reads a single specification; the rest of the
            // fields are results the calculation writes back, so the Windows form greys them out
            void ApplyMode()
            {
                if (dutyRow != null) dutyRow.IsEnabled = selected == 0;
                if (deltaTRow != null) deltaTRow.IsEnabled = selected == 1;
                if (outletTRow != null) outletTRow.IsEnabled = selected == 2;
                if (vaporFractionBox != null) vaporFractionBox.IsEnabled = selected == 3;
            }

            panel.CreateAndAddDropDownRow("Calculation Type", modes, modeIndex, (dd, e) =>
            {
                if (dd.SelectedIndex < 0 || dd.SelectedIndex >= modes.Count) return;
                selected = dd.SelectedIndex;
                setMode(selected);
                ApplyMode();
                panel.OnAfterEdit?.Invoke();
            });

            panel.CreateAndAddTextBoxRow(nf, "Efficiency (0-100%)", efficiency(),
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) setEfficiency(v); });

            dutyRow = panel.CreateAndAddValueUnitRow(simobj, "Heating/Cooling", UnitOfMeasure.heatflow,
                duty(), setDuty);

            outletTRow = panel.CreateAndAddValueUnitRow(simobj, "Outlet Temperature",
                UnitOfMeasure.temperature, outletT(), setOutletT);

            deltaTRow = panel.CreateAndAddValueUnitRow(simobj, "Temperature Change",
                UnitOfMeasure.deltaT, deltaT(), setDeltaT);

            vaporFractionBox = panel.CreateAndAddTextBoxRow(nf, "Outlet Vapor Fraction", vaporFraction(),
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) setVaporFraction(v); });

            panel.CreateAndAddValueUnitRow(simobj, "Pressure Drop", UnitOfMeasure.deltaP,
                deltaP(), setDeltaP);

            ApplyMode();
        }

    }

}
