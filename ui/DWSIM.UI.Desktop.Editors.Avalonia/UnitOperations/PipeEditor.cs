using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using Pipe = DWSIM.UnitOperations.UnitOperations.Pipe;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Pipe segment editor, following the General tab of the Windows EditingForm_Pipe, and its
    /// results grid. The hydraulic and thermal profiles are edited by their own controls, which
    /// are not converted yet, so this editor summarises them.
    /// </summary>
    public static class PipeEditor
    {

        private static readonly string[] Modes =
        {
            "Specify Length/Hydraulic Profile (Default)",
            "Specify Outlet Pressure",
            "Specify Outlet Temperature"
        };

        private static readonly string[] FlowPackages =
        {
            "Beggs & Brill",
            "Lockhart & Martinelli",
            "Petalas & Aziz",
            "Weymouth (gas)",
            "Panhandle A (gas)",
            "Panhandle B (gas)"
        };

        private static readonly string[] SlurryViscosity = { "Disabled", "Yoshida et al" };

        public static Control Build(Pipe pipe)
        {
            return UnitOpEditor.Build(pipe,
                input: panel => BuildParameters(pipe, panel),
                results: panel => BuildResults(pipe, panel),
                extras: new[] { ("Profiles", BuildProfileSummary(pipe)) });
        }

        private static void BuildParameters(Pipe pipe, AvaloniaEditorPanel panel)
        {
            var nf = pipe.GetFlowsheet().FlowsheetOptions.NumberFormat;

            UnitOpEditorRows.ValueRow outletT = null, outletP = null;

            void ApplyMode()
            {
                if (outletT != null) outletT.IsEnabled = pipe.Specification == Pipe.Specmode.OutletTemperature;
                if (outletP != null) outletP.IsEnabled = pipe.Specification == Pipe.Specmode.OutletPressure;
            }

            panel.CreateAndAddDropDownRow("Calculation mode", new List<string>(Modes),
                (int)pipe.Specification, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    pipe.Specification = (Pipe.Specmode)dd.SelectedIndex;
                    ApplyMode();
                    panel.OnAfterEdit?.Invoke();
                });

            outletT = panel.CreateAndAddValueUnitRow(pipe, "Outlet temperature (spec)",
                UnitOfMeasure.temperature, pipe.OutletTemperature, v => pipe.OutletTemperature = v);

            outletP = panel.CreateAndAddValueUnitRow(pipe, "Outlet pressure (spec)",
                UnitOfMeasure.pressure, pipe.OutletPressure, v => pipe.OutletPressure = v);

            panel.CreateAndAddDropDownRow("Pressure drop correlation", new List<string>(FlowPackages),
                (int)pipe.SelectedFlowPackage, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    pipe.SelectedFlowPackage = (DWSIM.UnitOperations.UnitOperations.FlowPackage)dd.SelectedIndex;
                    panel.OnAfterEdit?.Invoke();
                });

            // Efficiency factor used only by the Weymouth / Panhandle gas pipeline equations.
            panel.CreateAndAddTextBoxRow(nf, "Pipeline efficiency (gas equations, 0-1)",
                pipe.PipelineEfficiency,
                (tb, e) =>
                {
                    if (UnitOpEditorRows.TryParse(tb.Text, out var v)) pipe.PipelineEfficiency = v;
                });

            panel.CreateAndAddValueUnitRow(pipe, "Temp. error tolerance", UnitOfMeasure.deltaT,
                pipe.TolT, v => pipe.TolT = v);

            panel.CreateAndAddValueUnitRow(pipe, "Pressure error tolerance", UnitOfMeasure.deltaP,
                pipe.TolP, v => pipe.TolP = v);

            panel.CreateAndAddCheckBoxRow("Calculate equilibria along the pipe",
                pipe.CalculateEquilibrium,
                (cb, e) => pipe.CalculateEquilibrium = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddTextBoxRow(nf, "Calculate equilibria at each X sections",
                pipe.CalculateEquilibriumIntervalInSteps,
                (tb, e) =>
                {
                    if (UnitOpEditorRows.TryParse(tb.Text, out var v))
                        pipe.CalculateEquilibriumIntervalInSteps = (int)v;
                });

            panel.CreateAndAddCheckBoxRow("Calculate thermal balance with surroundings",
                pipe.CalculateHeatBalance,
                (cb, e) => pipe.CalculateHeatBalance = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddCheckBoxRow("Include emulsion effect", pipe.IncludeEmulsion,
                (cb, e) => pipe.IncludeEmulsion = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddDropDownRow("Slurry viscosity calculation", new List<string>(SlurryViscosity),
                pipe.SlurryViscosityMode, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    pipe.SlurryViscosityMode = dd.SelectedIndex;
                    panel.OnAfterEdit?.Invoke();
                });

            panel.CreateAndAddCheckBoxRow("Use Global weather conditions", pipe.UseGlobalWeather,
                (cb, e) => pipe.UseGlobalWeather = cb.IsChecked.GetValueOrDefault());

            panel.CreateAndAddDescriptionRow(
                "If checked, DWSIM will use Flowsheet-defined weather conditions for ambient " +
                "temperature, pressure and air (wind) speed.");

            ApplyMode();
        }

        private static void BuildResults(Pipe pipe, AvaloniaEditorPanel panel)
        {
            panel.CreateAndAddResultRow(pipe, "Pressure Difference", UnitOfMeasure.deltaP,
                pipe.DeltaP.GetValueOrDefault());
            panel.CreateAndAddResultRow(pipe, "Temperature Difference", UnitOfMeasure.deltaT,
                pipe.DeltaT.GetValueOrDefault());
            panel.CreateAndAddResultRow(pipe, "Heat Load", UnitOfMeasure.heatflow,
                pipe.DeltaQ.GetValueOrDefault());
        }

        /// <summary>
        /// What the hydraulic profile holds, until its editor is converted. The sections are read
        /// from the file and used by the calculation either way.
        /// </summary>
        private static Control BuildProfileSummary(Pipe pipe)
        {
            var panel = new AvaloniaEditorPanel();

            var count = 0;
            var length = 0.0;

            if (pipe.Profile != null && pipe.Profile.Sections != null)
            {
                foreach (var section in pipe.Profile.Sections.Values)
                {
                    count += 1;
                    length += section.Comprimento * section.Quantidade;
                }
            }

            panel.CreateAndAddTwoLabelsRow("Hydraulic sections", count.ToString());
            panel.CreateAndAddTwoLabelsRow("Total length (m)", length.ToString("G6"));

            panel.CreateAndAddTwoLabelsRow("Thermal profile",
                pipe.ThermalProfile == null ? "-" : pipe.ThermalProfile.TipoPerfil.ToString());

            panel.CreateAndAddDescriptionRow(
                "The hydraulic and thermal profile editors have not been converted yet. Edit them " +
                "in the Windows UI; the profiles stored with the simulation are used as they are.");

            return panel;
        }

    }

}
