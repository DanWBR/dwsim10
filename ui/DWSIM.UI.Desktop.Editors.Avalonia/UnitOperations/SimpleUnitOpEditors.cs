using System;
using System.Collections.Generic;
using Avalonia.Controls;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Mixer = DWSIM.UnitOperations.UnitOperations.Mixer;
using Splitter = DWSIM.UnitOperations.UnitOperations.Splitter;
using Tank = DWSIM.UnitOperations.UnitOperations.Tank;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Mixer editor: the outlet pressure rule, which is all the Windows form offers.
    /// </summary>
    public static class MixerEditor
    {

        private static readonly Mixer.PressureBehavior[] Order =
        {
            Mixer.PressureBehavior.Minimum,
            Mixer.PressureBehavior.Average,
            Mixer.PressureBehavior.Maximum
        };

        public static Control Build(Mixer mixer)
        {
            return UnitOpEditor.Build(mixer, panel =>
            {
                panel.CreateAndAddDropDownRow("Pressure Calculation",
                    new List<string> { "Inlet Minimum", "Inlet Average", "Inlet Maximum" },
                    Math.Max(0, Array.IndexOf(Order, mixer.PressureCalculation)), (dd, e) =>
                    {
                        if (dd.SelectedIndex < 0 || dd.SelectedIndex >= Order.Length) return;
                        mixer.PressureCalculation = Order[dd.SelectedIndex];
                        panel.OnAfterEdit?.Invoke();
                    });
            });
        }

    }

    /// <summary>
    /// Splitter editor: split ratios or flow specs for the outlet streams, as the Windows form
    /// switches between them. The rows follow the number of outlets actually connected, which is
    /// what the Windows form enables on load.
    /// </summary>
    public static class SplitterEditor
    {

        private static readonly Splitter.OpMode[] Order =
        {
            Splitter.OpMode.SplitRatios,
            Splitter.OpMode.StreamMassFlowSpec,
            Splitter.OpMode.StreamMoleFlowSpec,
            Splitter.OpMode.StreamVolumetricFlowSpec
        };

        public static Control Build(Splitter splitter)
        {
            return UnitOpEditor.Build(splitter, panel =>
            {
                var nf = splitter.GetFlowsheet().FlowsheetOptions.NumberFormat;

                var outlets = 0;
                foreach (var connector in splitter.GraphicObject.OutputConnectors)
                    if (connector.IsAttached) outlets += 1;

                TextBox ratio1 = null, ratio2 = null;
                UnitOpEditorRows.ValueRow spec1 = null, spec2 = null;

                void ApplyMode()
                {
                    var ratios = splitter.OperationMode == Splitter.OpMode.SplitRatios;

                    if (ratio1 != null) ratio1.IsEnabled = ratios && outlets >= 2;
                    if (ratio2 != null) ratio2.IsEnabled = ratios && outlets >= 3;
                    if (spec1 != null) spec1.IsEnabled = !ratios && outlets >= 2;
                    if (spec2 != null) spec2.IsEnabled = !ratios && outlets >= 3;
                }

                panel.CreateAndAddDropDownRow("Calculation Type",
                    new List<string>
                    {
                        "Stream Split Ratios",
                        "Stream Mass Flow Specs",
                        "Stream Mole Flow Specs",
                        "Stream Volumetric Flow Specs"
                    },
                    Math.Max(0, Array.IndexOf(Order, splitter.OperationMode)), (dd, e) =>
                    {
                        if (dd.SelectedIndex < 0 || dd.SelectedIndex >= Order.Length) return;
                        splitter.OperationMode = Order[dd.SelectedIndex];
                        ApplyMode();
                        panel.OnAfterEdit?.Invoke();
                    });

                ratio1 = panel.CreateAndAddTextBoxRow(nf, "Stream 1 Split Ratio", RatioAt(splitter, 0),
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) SetRatio(splitter, 0, v); });

                ratio2 = panel.CreateAndAddTextBoxRow(nf, "Stream 2 Split Ratio", RatioAt(splitter, 1),
                    (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) SetRatio(splitter, 1, v); });

                var measure = FlowMeasure(splitter.OperationMode);

                spec1 = panel.CreateAndAddValueUnitRow(splitter, "Stream 1 Flow Spec", measure,
                    splitter.StreamFlowSpec, v => splitter.StreamFlowSpec = v);

                spec2 = panel.CreateAndAddValueUnitRow(splitter, "Stream 2 Flow Spec", measure,
                    splitter.Stream2FlowSpec, v => splitter.Stream2FlowSpec = v);

                if (outlets <= 1)
                    panel.CreateAndAddDescriptionRow("Connect the outlet streams to set their split.");

                ApplyMode();
            });
        }

        /// <summary>The flow spec is read in the unit of the mode picked.</summary>
        private static UnitOfMeasure FlowMeasure(Splitter.OpMode mode)
        {
            switch (mode)
            {
                case Splitter.OpMode.StreamMoleFlowSpec: return UnitOfMeasure.molarflow;
                case Splitter.OpMode.StreamVolumetricFlowSpec: return UnitOfMeasure.volumetricFlow;
                default: return UnitOfMeasure.massflow;
            }
        }

        private static double RatioAt(Splitter splitter, int index)
        {
            try { return Convert.ToDouble(splitter.Ratios[index]); }
            catch (Exception) { return 0.0; }
        }

        private static void SetRatio(Splitter splitter, int index, double value)
        {
            try { splitter.Ratios[index] = value; }
            catch (Exception) { }
        }

    }

    /// <summary>Tank editor: the volume and the residence time of the Windows form.</summary>
    public static class TankEditor
    {

        public static Control Build(Tank tank)
        {
            return UnitOpEditor.Build(tank, panel =>
            {
                panel.CreateAndAddValueUnitRow(tank, "Tank Volume", UnitOfMeasure.volume,
                    tank.Volume, v => tank.Volume = v);

                panel.CreateAndAddValueUnitRow(tank, "Fluid Residence Time", UnitOfMeasure.time,
                    tank.ResidenceTime, v => tank.ResidenceTime = v);
            });
        }

    }

}
