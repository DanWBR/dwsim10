using System;
using System.Collections.Generic;
using Avalonia.Controls;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using HeatExchanger = DWSIM.UnitOperations.UnitOperations.HeatExchanger;
using HeatExchangerCalcMode = DWSIM.UnitOperations.UnitOperations.HeatExchangerCalcMode;
using SpecifiedTemperature = DWSIM.UnitOperations.UnitOperations.SpecifiedTemperature;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Heat exchanger editor, as the Windows EditingForm_HeatExchanger lays it out: the
    /// calculation type decides which of the specs is live, and the results group shows what the
    /// last calculation produced, including the shell and tube figures in the rating modes.
    /// </summary>
    public static class HeatExchangerEditor
    {

        /// <summary>The combo index is the enum value here, as in the Windows form.</summary>
        private static readonly string[] Modes =
        {
            "Calculate Hot Fluid Outlet Temperature",
            "Calculate Cold Fluid Outlet Temperature",
            "Calculate Outlet Temperatures",
            "Calculate Outlet Temperatures (UA)",
            "Calculate Area",
            "Shell and Tubes Exchanger Rating",
            "Shell and Tubes Exchanger Fouling Factor Calculation",
            "Pinch Point",
            "Specify Heat Transfer Efficiency",
            "Specify Outlet Molar Vapor Fraction (Stream 1)",
            "Specify Outlet Molar Vapor Fraction (Stream 2)"
        };

        public static Control Build(HeatExchanger hx)
        {
            return UnitOpEditor.Build(hx,
                input: panel => BuildParameters(hx, panel),
                results: panel => BuildResults(hx, panel),
                propertyPackage: false);
        }

        private static void BuildParameters(HeatExchanger hx, AvaloniaEditorPanel panel)
        {
            var nf = hx.GetFlowsheet().FlowsheetOptions.NumberFormat;

            UnitOpEditorRows.ValueRow coldPDrop = null, hotPDrop = null, coldOutT = null, hotOutT = null,
                                      overallU = null, area = null, heat = null, mita = null;
            TextBox efficiency = null, ovf1 = null, ovf2 = null;
            CheckBox pinchAtOutlets = null, calcProfile = null;
            Button shellAndTube = null;

            void ApplyMode()
            {
                var mode = hx.CalculationMode;

                // the Windows handler resets everything, then re-enables per mode
                if (coldOutT != null) coldOutT.IsEnabled = true;
                if (hotOutT != null) hotOutT.IsEnabled = true;
                if (overallU != null) overallU.IsEnabled = true;
                if (area != null) area.IsEnabled = true;
                if (heat != null) heat.IsEnabled = true;
                if (mita != null) mita.IsEnabled = false;
                if (efficiency != null) efficiency.IsEnabled = false;
                if (ovf1 != null) ovf1.IsEnabled = false;
                if (ovf2 != null) ovf2.IsEnabled = false;
                if (pinchAtOutlets != null) pinchAtOutlets.IsEnabled = false;
                if (shellAndTube != null) shellAndTube.IsEnabled = false;
                if (calcProfile != null) calcProfile.IsEnabled = true;

                var pressureDrops = true;

                switch (mode)
                {
                    case HeatExchangerCalcMode.CalcTempHotOut:
                        hotOutT.IsEnabled = false; heat.IsEnabled = false; overallU.IsEnabled = false;
                        break;
                    case HeatExchangerCalcMode.CalcTempColdOut:
                        coldOutT.IsEnabled = false; heat.IsEnabled = false; overallU.IsEnabled = false;
                        break;
                    case HeatExchangerCalcMode.CalcBothTemp:
                        coldOutT.IsEnabled = false; hotOutT.IsEnabled = false; overallU.IsEnabled = false;
                        break;
                    case HeatExchangerCalcMode.CalcBothTemp_UA:
                        coldOutT.IsEnabled = false; hotOutT.IsEnabled = false; heat.IsEnabled = false;
                        break;
                    case HeatExchangerCalcMode.CalcArea:
                        area.IsEnabled = false; heat.IsEnabled = false;
                        break;
                    case HeatExchangerCalcMode.ShellandTube_Rating:
                        coldOutT.IsEnabled = false; hotOutT.IsEnabled = false;
                        overallU.IsEnabled = false; area.IsEnabled = false; heat.IsEnabled = false;
                        shellAndTube.IsEnabled = true;
                        pressureDrops = false;
                        break;
                    case HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor:
                        overallU.IsEnabled = false; area.IsEnabled = false; heat.IsEnabled = false;
                        shellAndTube.IsEnabled = true;
                        pressureDrops = false;
                        break;
                    case HeatExchangerCalcMode.PinchPoint:
                        coldOutT.IsEnabled = false; hotOutT.IsEnabled = false;
                        area.IsEnabled = false; heat.IsEnabled = false;
                        mita.IsEnabled = true;
                        pinchAtOutlets.IsEnabled = true;
                        calcProfile.IsEnabled = false;
                        break;
                    case HeatExchangerCalcMode.ThermalEfficiency:
                        coldOutT.IsEnabled = false; hotOutT.IsEnabled = false;
                        area.IsEnabled = false; heat.IsEnabled = false;
                        efficiency.IsEnabled = true;
                        break;
                    case HeatExchangerCalcMode.OutletVaporFraction1:
                        coldOutT.IsEnabled = false; hotOutT.IsEnabled = false;
                        heat.IsEnabled = false; overallU.IsEnabled = false;
                        ovf1.IsEnabled = true;
                        break;
                    case HeatExchangerCalcMode.OutletVaporFraction2:
                        coldOutT.IsEnabled = false; hotOutT.IsEnabled = false;
                        heat.IsEnabled = false; overallU.IsEnabled = false;
                        ovf2.IsEnabled = true;
                        break;
                }

                if (coldPDrop != null) coldPDrop.IsEnabled = pressureDrops;
                if (hotPDrop != null) hotPDrop.IsEnabled = pressureDrops;
            }

            panel.CreateAndAddDropDownRow("Calculation Type", new List<string>(Modes),
                (int)hx.CalculationMode, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    hx.CalculationMode = (HeatExchangerCalcMode)dd.SelectedIndex;
                    ApplyMode();
                    panel.OnAfterEdit?.Invoke();
                });

            panel.CreateAndAddDropDownRow("Flow Direction",
                new List<string> { "Counter Current", "Co Current" }, (int)hx.FlowDir, (dd, e) =>
                {
                    if (dd.SelectedIndex < 0) return;
                    hx.FlowDir = (DWSIM.UnitOperations.UnitOperations.FlowDirection)dd.SelectedIndex;
                    panel.OnAfterEdit?.Invoke();
                });

            shellAndTube = panel.CreateAndAddButtonRow("Edit Shell and Tube Heat Exchanger Properties",
                null, (btn, e) => ShellAndTubeEditor.Show(hx));

            coldPDrop = panel.CreateAndAddValueUnitRow(hx, "Cold Fluid Pressure Drop",
                UnitOfMeasure.deltaP, hx.ColdSidePressureDrop, v => hx.ColdSidePressureDrop = v);

            hotPDrop = panel.CreateAndAddValueUnitRow(hx, "Hot Fluid Pressure Drop",
                UnitOfMeasure.deltaP, hx.HotSidePressureDrop, v => hx.HotSidePressureDrop = v);

            coldOutT = panel.CreateAndAddValueUnitRow(hx, "Cold Fluid Outlet Temperature",
                UnitOfMeasure.temperature, hx.ColdSideOutletTemperature, v =>
                {
                    hx.DefinedTemperature = SpecifiedTemperature.Cold_Fluid;
                    hx.ColdSideOutletTemperature = v;
                });

            hotOutT = panel.CreateAndAddValueUnitRow(hx, "Hot Fluid Outlet Temperature",
                UnitOfMeasure.temperature, hx.HotSideOutletTemperature, v =>
                {
                    hx.DefinedTemperature = SpecifiedTemperature.Hot_Fluid;
                    hx.HotSideOutletTemperature = v;
                });

            overallU = panel.CreateAndAddValueUnitRow(hx, "Global Heat Transfer Coefficient",
                UnitOfMeasure.heat_transf_coeff, hx.OverallCoefficient.GetValueOrDefault(), v => hx.OverallCoefficient = v);

            area = panel.CreateAndAddValueUnitRow(hx, "Heat Exchange Area",
                UnitOfMeasure.area, hx.Area.GetValueOrDefault(), v => hx.Area = v);

            heat = panel.CreateAndAddValueUnitRow(hx, "Heat Exchanged",
                UnitOfMeasure.heatflow, hx.Q.GetValueOrDefault(), v => hx.Q = v);

            mita = panel.CreateAndAddValueUnitRow(hx, "Min Temperature Difference",
                UnitOfMeasure.deltaT, hx.MITA, v => hx.MITA = v);

            panel.CreateAndAddValueUnitRow(hx, "Heat Loss", UnitOfMeasure.heatflow,
                hx.HeatLoss, v => hx.HeatLoss = v);

            efficiency = panel.CreateAndAddTextBoxRow(nf, "Heat Transfer Efficiency (%)",
                hx.ThermalEfficiency,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) hx.ThermalEfficiency = v; });

            ovf1 = panel.CreateAndAddTextBoxRow(nf, "Outlet Vap. Mol. Frac. (Stream 1)",
                hx.OutletVaporFraction1,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) hx.OutletVaporFraction1 = v; });

            ovf2 = panel.CreateAndAddTextBoxRow(nf, "Outlet Vap. Mol. Frac. (Stream 2)",
                hx.OutletVaporFraction2,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) hx.OutletVaporFraction2 = v; });

            panel.CreateAndAddCheckBoxRow("Ignore LMTD Error", hx.IgnoreLMTDError,
                (cb, e) => hx.IgnoreLMTDError = cb.IsChecked.GetValueOrDefault());

            pinchAtOutlets = panel.CreateAndAddCheckBoxRow("Force Pinch Point Location to Outlets",
                hx.PinchPointAtOutlets, (cb, e) => hx.PinchPointAtOutlets = cb.IsChecked.GetValueOrDefault());

            calcProfile = panel.CreateAndAddCheckBoxRow("Calculate Heat Exchange Profile",
                hx.CalculateHeatExchangeProfile,
                (cb, e) => hx.CalculateHeatExchangeProfile = cb.IsChecked.GetValueOrDefault());

            ApplyMode();
        }

        /// <summary>
        /// What the Windows results grid lists, only once the exchanger has been calculated. The
        /// shell and tube figures only make sense in the two rating modes.
        /// </summary>
        private static void BuildResults(HeatExchanger hx, AvaloniaEditorPanel panel)
        {
            if (!hx.Calculated)
            {
                panel.CreateAndAddDescriptionRow("Solve the flowsheet to see the results.");
                return;
            }

            var nf = hx.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddResultRow(hx, "Maximum Heat Exchange", UnitOfMeasure.heatflow, hx.MaxHeatExchange);
            panel.CreateAndAddTwoLabelsRow("Thermal Efficiency (%)", hx.ThermalEfficiency.ToString(nf));
            panel.CreateAndAddResultRow(hx, "Log Mean Temperature Difference (LMTD)", UnitOfMeasure.deltaT, hx.LMTD);
            panel.CreateAndAddTwoLabelsRow("LMTD Correction Factor (Shell and Tube)", hx.LMTD_F.ToString(nf));

            var rating = hx.CalculationMode == HeatExchangerCalcMode.ShellandTube_Rating ||
                         hx.CalculationMode == HeatExchangerCalcMode.ShellandTube_CalcFoulingFactor;

            if (!rating) return;

            panel.CreateAndAddTwoLabelsRow("Shell-side Reynolds Number", hx.STProperties.ReS.ToString(nf));
            panel.CreateAndAddTwoLabelsRow("Tube-side Reynolds Number", hx.STProperties.ReT.ToString(nf));
            panel.CreateAndAddResultRow(hx, "Shell-side Resistance", UnitOfMeasure.foulingfactor, hx.STProperties.Fs);
            panel.CreateAndAddResultRow(hx, "Tube-side Resistance", UnitOfMeasure.foulingfactor, hx.STProperties.Ft);
            panel.CreateAndAddResultRow(hx, "Pipe Wall Resistance", UnitOfMeasure.foulingfactor, hx.STProperties.Fc);
            panel.CreateAndAddResultRow(hx, "Fouling Resistance", UnitOfMeasure.foulingfactor, hx.STProperties.Ff);
        }

    }

}
