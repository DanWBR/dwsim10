using System;
using System.Collections.Generic;
using Avalonia.Controls;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using HydroelectricTurbine = DWSIM.UnitOperations.UnitOperations.HydroelectricTurbine;
using SolarPanel = DWSIM.UnitOperations.UnitOperations.SolarPanel;
using WaterElectrolyzer = DWSIM.UnitOperations.UnitOperations.WaterElectrolyzer;
using WindTurbine = DWSIM.UnitOperations.UnitOperations.WindTurbine;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// The clean energy unit operations, which all share the same shape: a handful of parameters
    /// and the power they generate, as their Windows editors lay them out.
    /// </summary>
    public static class CleanEnergyEditors
    {

        public static Control Build(SolarPanel panel_)
        {
            return UnitOpEditor.Build(panel_,
                input: panel =>
                {
                    var nf = panel_.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    UnitOpEditorRows.ValueRow irradiation = null;

                    // the global weather comes from the flowsheet, so the local value is then read-only
                    var global = panel.CreateAndAddCheckBoxRow("Use Global Weather Conditions",
                        !panel_.UseUserDefinedWeather, (cb, e) =>
                        {
                            panel_.UseUserDefinedWeather = !cb.IsChecked.GetValueOrDefault();
                            if (irradiation != null) irradiation.IsEnabled = panel_.UseUserDefinedWeather;
                        });

                    irradiation = panel.CreateAndAddValueUnitRow(panel_, "Solar Irradiation (kW/m2)",
                        UnitOfMeasure.none, panel_.SolarIrradiation_kW_m2,
                        v => panel_.SolarIrradiation_kW_m2 = v);

                    irradiation.IsEnabled = panel_.UseUserDefinedWeather;

                    panel.CreateAndAddValueUnitRow(panel_, "Panel Area", UnitOfMeasure.area,
                        panel_.PanelArea, v => panel_.PanelArea = v);

                    panel.CreateAndAddTextBoxRow(nf, "Panel Efficiency (0-100%)", panel_.PanelEfficiency,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) panel_.PanelEfficiency = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Number of Panels", panel_.NumberOfPanels,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) panel_.NumberOfPanels = (int)v; });
                },
                results: panel => panel.CreateAndAddResultRow(panel_, "Generated Power",
                    UnitOfMeasure.heatflow, panel_.GeneratedPower),
                propertyPackage: false);
        }

        public static Control Build(WindTurbine turbine)
        {
            return UnitOpEditor.Build(turbine,
                input: panel =>
                {
                    var nf = turbine.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    UnitOpEditorRows.ValueRow speed = null, pressure = null, temperature = null;
                    TextBox humidity = null;

                    void Apply()
                    {
                        var local = turbine.UseUserDefinedWeather;
                        if (speed != null) speed.IsEnabled = local;
                        if (pressure != null) pressure.IsEnabled = local;
                        if (temperature != null) temperature.IsEnabled = local;
                        if (humidity != null) humidity.IsEnabled = local;
                    }

                    panel.CreateAndAddCheckBoxRow("Use Global Weather Conditions",
                        !turbine.UseUserDefinedWeather, (cb, e) =>
                        {
                            turbine.UseUserDefinedWeather = !cb.IsChecked.GetValueOrDefault();
                            Apply();
                        });

                    speed = panel.CreateAndAddValueUnitRow(turbine, "Wind Speed", UnitOfMeasure.velocity,
                        turbine.UserDefinedWindSpeed, v => turbine.UserDefinedWindSpeed = v);

                    pressure = panel.CreateAndAddValueUnitRow(turbine, "Atmospheric Pressure",
                        UnitOfMeasure.pressure, turbine.UserDefinedAirPressure,
                        v => turbine.UserDefinedAirPressure = v);

                    temperature = panel.CreateAndAddValueUnitRow(turbine, "Atmospheric Temperature",
                        UnitOfMeasure.temperature, turbine.UserDefinedAirTemperature,
                        v => turbine.UserDefinedAirTemperature = v);

                    humidity = panel.CreateAndAddTextBoxRow(nf, "Relative Humidity (%)",
                        turbine.UserDefinedRelativeHumidity,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) turbine.UserDefinedRelativeHumidity = v; });

                    panel.CreateAndAddValueUnitRow(turbine, "Rotor Diameter", UnitOfMeasure.distance,
                        turbine.RotorDiameter, v => turbine.RotorDiameter = v);

                    panel.CreateAndAddTextBoxRow(nf, "Turbine Efficiency (0-100%)", turbine.Efficiency,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) turbine.Efficiency = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Number of Units", turbine.NumberOfTurbines,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) turbine.NumberOfTurbines = (int)v; });

                    Apply();
                },
                results: panel =>
                {
                    panel.CreateAndAddResultRow(turbine, "Generated Power", UnitOfMeasure.heatflow,
                        turbine.GeneratedPower);
                    panel.CreateAndAddResultRow(turbine, "Maximum Theoretical Power",
                        UnitOfMeasure.heatflow, turbine.MaximumTheoreticalPower);
                    panel.CreateAndAddResultRow(turbine, "Calculated Air Density", UnitOfMeasure.density,
                        turbine.AirDensity);
                },
                propertyPackage: false);
        }

        public static Control Build(HydroelectricTurbine turbine)
        {
            return UnitOpEditor.Build(turbine,
                input: panel =>
                {
                    var nf = turbine.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    panel.CreateAndAddValueUnitRow(turbine, "Static Head", UnitOfMeasure.distance,
                        turbine.StaticHead, v => turbine.StaticHead = v);

                    panel.CreateAndAddValueUnitRow(turbine, "Inlet Velocity", UnitOfMeasure.velocity,
                        turbine.InletVelocity, v => turbine.InletVelocity = v);

                    panel.CreateAndAddValueUnitRow(turbine, "Outlet Velocity", UnitOfMeasure.velocity,
                        turbine.OutletVelocity, v => turbine.OutletVelocity = v);

                    panel.CreateAndAddTextBoxRow(nf, "Efficiency (%)", turbine.Efficiency,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) turbine.Efficiency = v; });
                },
                results: panel =>
                {
                    panel.CreateAndAddResultRow(turbine, "Total Head", UnitOfMeasure.distance,
                        turbine.TotalHead);
                    panel.CreateAndAddResultRow(turbine, "Generated Power", UnitOfMeasure.heatflow,
                        turbine.GeneratedPower);
                },
                propertyPackage: false);
        }

        public static Control Build(WaterElectrolyzer electrolyzer)
        {
            return UnitOpEditor.Build(electrolyzer,
                input: panel =>
                {
                    var nf = electrolyzer.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    panel.CreateAndAddTextBoxRow(nf, "Total Voltage (V)", electrolyzer.Voltage,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) electrolyzer.Voltage = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Number of Cells", electrolyzer.NumberOfCells,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) electrolyzer.NumberOfCells = (int)v; });

                    panel.CreateAndAddTextBoxRow(nf, "User-defined Efficiency", electrolyzer.InputEfficiency,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) electrolyzer.InputEfficiency = v; });
                },
                results: panel =>
                {
                    var nf = electrolyzer.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    panel.CreateAndAddTwoLabelsRow("Cell Voltage", electrolyzer.CellVoltage.ToString(nf) + " V");
                    panel.CreateAndAddTwoLabelsRow("Reversible Voltage", electrolyzer.ReversibleVoltage.ToString(nf) + " V");
                    panel.CreateAndAddTwoLabelsRow("Thermoneutral Voltage", electrolyzer.ThermoNeutralVoltage.ToString(nf) + " V");
                    panel.CreateAndAddTwoLabelsRow("Current", electrolyzer.Current.ToString(nf) + " A");

                    panel.CreateAndAddResultRow(electrolyzer, "Electron Transfer", UnitOfMeasure.molarflow,
                        electrolyzer.ElectronTransfer);
                    panel.CreateAndAddResultRow(electrolyzer, "Waste Heat", UnitOfMeasure.heatflow,
                        electrolyzer.WasteHeat);

                    panel.CreateAndAddTwoLabelsRow("Calculated Efficiency", electrolyzer.Efficiency.ToString(nf));
                },
                propertyPackage: false);
        }

    }

}
