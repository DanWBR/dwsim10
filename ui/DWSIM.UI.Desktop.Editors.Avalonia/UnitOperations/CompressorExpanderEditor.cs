using System;
using System.Collections.Generic;
using Avalonia.Controls;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Compressor = DWSIM.UnitOperations.UnitOperations.Compressor;
using Expander = DWSIM.UnitOperations.UnitOperations.Expander;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Compressor and expander editor, as the Windows EditingForm_ComprExpndr lays them out. The
    /// two share the form; the expander has no energy stream mode and reads its duty as power
    /// generated. Outlet temperature, temperature change and the two coefficients are results the
    /// form shows among the parameters.
    /// </summary>
    public static class CompressorExpanderEditor
    {

        private static readonly string[] Modes =
        {
            "Outlet Pressure",
            "Pressure Increase",
            "Power Required",
            "Energy Stream",
            "Known Head",
            "Performance Curves",
            "Pressure Ratio"
        };

        private static readonly Compressor.CalculationMode[] CompressorOrder =
        {
            Compressor.CalculationMode.OutletPressure,
            Compressor.CalculationMode.Delta_P,
            Compressor.CalculationMode.PowerRequired,
            Compressor.CalculationMode.EnergyStream,
            Compressor.CalculationMode.Head,
            Compressor.CalculationMode.Curves,
            Compressor.CalculationMode.PressureRatio
        };

        private static readonly Expander.CalculationMode[] ExpanderOrder =
        {
            Expander.CalculationMode.OutletPressure,
            Expander.CalculationMode.Delta_P,
            Expander.CalculationMode.PowerGenerated,
            Expander.CalculationMode.OutletPressure,   // the expander has no energy stream mode
            Expander.CalculationMode.Head,
            Expander.CalculationMode.Curves,
            Expander.CalculationMode.PressureRatio
        };

        public static Control Build(Compressor compressor)
        {
            return UnitOpEditor.Build(compressor, panel => Fill(panel, compressor,
                modeIndex: Math.Max(0, Array.IndexOf(CompressorOrder, compressor.CalcMode)),
                setMode: index => compressor.CalcMode = CompressorOrder[index],
                isAdiabatic: () => compressor.ProcessPath == Compressor.ProcessPathType.Adiabatic,
                setProcessPath: adiabatic => compressor.ProcessPath = adiabatic
                    ? Compressor.ProcessPathType.Adiabatic
                    : Compressor.ProcessPathType.Polytropic,
                energyStream: true,
                powerLabel: "Power Required",
                deltaPLabel: "Pressure Increase",
                curves: () => CompressorCurvesEditor.Show(compressor)));
        }

        public static Control Build(Expander expander)
        {
            return UnitOpEditor.Build(expander, panel => Fill(panel, expander,
                modeIndex: Math.Max(0, Array.IndexOf(ExpanderOrder, expander.CalcMode)),
                setMode: index => expander.CalcMode = ExpanderOrder[index],
                isAdiabatic: () => expander.ProcessPath == Expander.ProcessPathType.Adiabatic,
                setProcessPath: adiabatic => expander.ProcessPath = adiabatic
                    ? Expander.ProcessPathType.Adiabatic
                    : Expander.ProcessPathType.Polytropic,
                energyStream: false,
                powerLabel: "Power Generated",
                deltaPLabel: "Pressure Drop",
                curves: () => CompressorCurvesEditor.Show(expander)));
        }

        private static void Fill(AvaloniaEditorPanel panel, ISimulationObject simobj,
                                 int modeIndex, Action<int> setMode,
                                 Func<bool> isAdiabatic, Action<bool> setProcessPath,
                                 bool energyStream, string powerLabel, string deltaPLabel,
                                 Action curves)
        {
            var nf = simobj.GetFlowsheet().FlowsheetOptions.NumberFormat;
            var machine = new Machine(simobj);

            var modes = new List<string>(Modes);
            if (!energyStream) modes[3] = "Energy Stream (not available)";

            UnitOpEditorRows.ValueRow deltaP = null, outletPressure = null, power = null,
                                      adiabaticHead = null, polytropicHead = null;
            TextBox speed = null, ratio = null, adiabaticEfficiency = null, polytropicEfficiency = null;
            Button curvesButton = null;
            var selected = modeIndex;

            void ApplyMode()
            {
                if (deltaP != null) deltaP.IsEnabled = selected == 1;
                if (outletPressure != null) outletPressure.IsEnabled = selected == 0;
                if (power != null) power.IsEnabled = selected == 2;
                if (ratio != null) ratio.IsEnabled = selected == 6;
                if (speed != null) speed.IsEnabled = selected == 5;
                if (curvesButton != null) curvesButton.IsEnabled = selected == 5 && curves != null;

                var head = selected == 4;
                if (adiabaticHead != null) adiabaticHead.IsEnabled = head && isAdiabatic();
                if (polytropicHead != null) polytropicHead.IsEnabled = head && !isAdiabatic();

                if (adiabaticEfficiency != null) adiabaticEfficiency.IsEnabled = isAdiabatic();
                if (polytropicEfficiency != null) polytropicEfficiency.IsEnabled = !isAdiabatic();
            }

            panel.CreateAndAddDropDownRow("Calculation Type", modes, modeIndex, (dd, e) =>
            {
                if (dd.SelectedIndex < 0 || dd.SelectedIndex >= Modes.Length) return;

                if (!energyStream && dd.SelectedIndex == 3)
                {
                    // the expander does not run off an energy stream; the Windows form says so
                    // and falls back to the outlet pressure
                    simobj.GetFlowsheet().ShowMessage(
                        "This calculation mode is not available for expanders.",
                        IFlowsheet.MessageType.Warning);
                    dd.SelectedIndex = 0;
                    return;
                }

                selected = dd.SelectedIndex;
                setMode(selected);
                ApplyMode();
                panel.OnAfterEdit?.Invoke();
            });

            panel.CreateAndAddDropDownRow("Thermodynamic Process",
                new List<string> { "Adiabatic", "Polytropic" }, isAdiabatic() ? 0 : 1, (dd, e) =>
                {
                    setProcessPath(dd.SelectedIndex == 0);
                    ApplyMode();
                    panel.OnAfterEdit?.Invoke();
                });

            curvesButton = panel.CreateAndAddButtonRow("Edit Performance Curves", null,
                (btn, e) => curves?.Invoke());

            speed = panel.CreateAndAddTextBoxRow(nf, "Rotation Speed (rpm)", machine.Speed,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) machine.Speed = v; });

            deltaP = panel.CreateAndAddValueUnitRow(simobj, deltaPLabel, UnitOfMeasure.deltaP,
                machine.DeltaP, v => machine.DeltaP = v);

            outletPressure = panel.CreateAndAddValueUnitRow(simobj, "Outlet Pressure",
                UnitOfMeasure.pressure, machine.OutletPressure, v => machine.OutletPressure = v);

            ratio = panel.CreateAndAddTextBoxRow(nf, "Pressure Ratio", machine.PressureRatio,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) machine.PressureRatio = v; });

            adiabaticEfficiency = panel.CreateAndAddTextBoxRow(nf, "Adiabatic Efficiency (0-100)",
                machine.AdiabaticEfficiency,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) machine.AdiabaticEfficiency = v; });

            polytropicEfficiency = panel.CreateAndAddTextBoxRow(nf, "Polytropic Efficiency (0-100)",
                machine.PolytropicEfficiency,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) machine.PolytropicEfficiency = v; });

            power = panel.CreateAndAddValueUnitRow(simobj, powerLabel, UnitOfMeasure.heatflow,
                machine.DeltaQ, v => machine.DeltaQ = v);

            // results the Windows form keeps among the parameters
            panel.CreateAndAddResultRow(simobj, "Outlet Temperature", UnitOfMeasure.temperature,
                machine.OutletTemperature);
            panel.CreateAndAddResultRow(simobj, "Temperature Change", UnitOfMeasure.deltaT,
                machine.DeltaT);
            panel.CreateAndAddTwoLabelsRow("Adiabatic Coefficient", machine.AdiabaticCoefficient.ToString(nf));
            panel.CreateAndAddTwoLabelsRow("Polytropic Coefficient", machine.PolytropicCoefficient.ToString(nf));

            adiabaticHead = panel.CreateAndAddValueUnitRow(simobj, "Adiabatic Head",
                UnitOfMeasure.distance, machine.AdiabaticHead, v => machine.AdiabaticHead = v);

            polytropicHead = panel.CreateAndAddValueUnitRow(simobj, "Polytropic Head",
                UnitOfMeasure.distance, machine.PolytropicHead, v => machine.PolytropicHead = v);

            ApplyMode();
        }

        /// <summary>
        /// The compressor and the expander carry the same properties under the same names but do
        /// not share a base class that declares them, so the editor reaches them through this.
        /// </summary>
        private sealed class Machine
        {
            private readonly Compressor _compressor;
            private readonly Expander _expander;

            public Machine(ISimulationObject simobj)
            {
                _compressor = simobj as Compressor;
                _expander = simobj as Expander;
            }

            public double Speed
            {
                get { return _compressor != null ? _compressor.Speed : _expander.Speed; }
                set { if (_compressor != null) _compressor.Speed = (int)value; else _expander.Speed = (int)value; }
            }

            public double DeltaP
            {
                get { return _compressor != null ? _compressor.DeltaP : _expander.DeltaP; }
                set { if (_compressor != null) _compressor.DeltaP = value; else _expander.DeltaP = value; }
            }

            public double OutletPressure
            {
                get { return _compressor != null ? _compressor.POut : _expander.POut; }
                set { if (_compressor != null) _compressor.POut = value; else _expander.POut = value; }
            }

            public double PressureRatio
            {
                get { return _compressor != null ? _compressor.PressureRatio : _expander.PressureRatio; }
                set { if (_compressor != null) _compressor.PressureRatio = value; else _expander.PressureRatio = value; }
            }

            public double AdiabaticEfficiency
            {
                get { return _compressor != null ? _compressor.AdiabaticEfficiency : _expander.AdiabaticEfficiency; }
                set { if (_compressor != null) _compressor.AdiabaticEfficiency = value; else _expander.AdiabaticEfficiency = value; }
            }

            public double PolytropicEfficiency
            {
                get { return _compressor != null ? _compressor.PolytropicEfficiency : _expander.PolytropicEfficiency; }
                set { if (_compressor != null) _compressor.PolytropicEfficiency = value; else _expander.PolytropicEfficiency = value; }
            }

            public double DeltaQ
            {
                get { return _compressor != null ? _compressor.DeltaQ : _expander.DeltaQ; }
                set { if (_compressor != null) _compressor.DeltaQ = value; else _expander.DeltaQ = value; }
            }

            public double AdiabaticHead
            {
                get { return _compressor != null ? _compressor.AdiabaticHead : _expander.AdiabaticHead; }
                set { if (_compressor != null) _compressor.AdiabaticHead = value; else _expander.AdiabaticHead = value; }
            }

            public double PolytropicHead
            {
                get { return _compressor != null ? _compressor.PolytropicHead : _expander.PolytropicHead; }
                set { if (_compressor != null) _compressor.PolytropicHead = value; else _expander.PolytropicHead = value; }
            }

            public double OutletTemperature
            {
                get { return _compressor != null ? _compressor.OutletTemperature : _expander.OutletTemperature; }
            }

            public double DeltaT
            {
                get { return _compressor != null ? _compressor.DeltaT : _expander.DeltaT; }
            }

            public double AdiabaticCoefficient
            {
                get { return _compressor != null ? _compressor.AdiabaticCoefficient : _expander.AdiabaticCoefficient; }
            }

            public double PolytropicCoefficient
            {
                get { return _compressor != null ? _compressor.PolytropicCoefficient : _expander.PolytropicCoefficient; }
            }
        }

    }

}
