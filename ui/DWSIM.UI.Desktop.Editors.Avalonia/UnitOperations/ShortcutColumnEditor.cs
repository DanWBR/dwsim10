using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Shared.Avalonia;
using ShortcutColumn = DWSIM.UnitOperations.UnitOperations.ShortcutColumn;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Shortcut column editor, as the Windows EditingForm_ShortcutColumn lays it out: the key
    /// compounds and their fractions, the reflux ratio, the condenser type and the two pressures,
    /// with the Fenske-Underwood-Gilliland results below.
    /// </summary>
    public static class ShortcutColumnEditor
    {

        public static Control Build(ShortcutColumn column)
        {
            return UnitOpEditor.Build(column,
                input: panel =>
                {
                    var nf = column.GetFlowsheet().FlowsheetOptions.NumberFormat;
                    var compounds = column.GetFlowsheet().SelectedCompounds.Keys.ToList();

                    panel.CreateAndAddDropDownRow("Light Key Compound (LK)", compounds,
                        Math.Max(0, compounds.IndexOf(column.m_lightkey ?? "")), (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            column.m_lightkey = compounds[dd.SelectedIndex];
                            panel.OnAfterEdit?.Invoke();
                        });

                    panel.CreateAndAddDropDownRow("Heavy Key Compound (HK)", compounds,
                        Math.Max(0, compounds.IndexOf(column.m_heavykey ?? "")), (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            column.m_heavykey = compounds[dd.SelectedIndex];
                            panel.OnAfterEdit?.Invoke();
                        });

                    panel.CreateAndAddTextBoxRow(nf, "LK Mole Fraction in Bottoms",
                        column.m_lightkeymolarfrac,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) column.m_lightkeymolarfrac = v; });

                    panel.CreateAndAddTextBoxRow(nf, "HK Mole Fraction in Distillate",
                        column.m_heavykeymolarfrac,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) column.m_heavykeymolarfrac = v; });

                    panel.CreateAndAddTextBoxRow(nf, "Reflux Ratio", column.m_refluxratio,
                        (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) column.m_refluxratio = v; });

                    panel.CreateAndAddDropDownRow("Condenser Type",
                        new List<string> { "Total", "Partial" }, (int)column.condtype, (dd, e) =>
                        {
                            if (dd.SelectedIndex < 0) return;
                            column.condtype = (ShortcutColumn.CondenserType)dd.SelectedIndex;
                            panel.OnAfterEdit?.Invoke();
                        });

                    panel.CreateAndAddValueUnitRow(column, "Condenser Pressure", UnitOfMeasure.pressure,
                        column.m_condenserpressure, v => column.m_condenserpressure = v);

                    panel.CreateAndAddValueUnitRow(column, "Reboiler Pressure", UnitOfMeasure.pressure,
                        column.m_boilerpressure, v => column.m_boilerpressure = v);

                    panel.CreateAndAddValueUnitRow(column, "Stage/Tray Height", UnitOfMeasure.distance,
                        column.StageHeight, v => column.StageHeight = v);
                },
                results: panel =>
                {
                    // the Windows grid only fills once the column has been calculated
                    if (!column.Calculated)
                    {
                        panel.CreateAndAddDescriptionRow("Solve the flowsheet to see the results.");
                        return;
                    }

                    var nf = column.GetFlowsheet().FlowsheetOptions.NumberFormat;

                    panel.CreateAndAddTwoLabelsRow("Minimum Reflux Ratio", column.m_Rmin.ToString(nf));
                    panel.CreateAndAddTwoLabelsRow("Minimum Number of Stages", column.m_Nmin.ToString(nf));
                    panel.CreateAndAddTwoLabelsRow("Actual Number of Stages", column.m_N.ToString(nf));
                    panel.CreateAndAddTwoLabelsRow("Optimal Feed Stage", column.ofs.ToString(nf));

                    panel.CreateAndAddResultRow(column, "Stripping Liquid", UnitOfMeasure.molarflow, column.L_);
                    panel.CreateAndAddResultRow(column, "Rectify Liquid", UnitOfMeasure.molarflow, column.L);
                    panel.CreateAndAddResultRow(column, "Stripping Vapor", UnitOfMeasure.molarflow, column.V_);
                    panel.CreateAndAddResultRow(column, "Rectify Vapor", UnitOfMeasure.molarflow, column.V);

                    panel.CreateAndAddResultRow(column, "Condenser Duty", UnitOfMeasure.heatflow, column.m_Qc);
                    panel.CreateAndAddResultRow(column, "Reboiler Duty", UnitOfMeasure.heatflow, column.m_Qb);

                    panel.CreateAndAddResultRow(column, "Estimated Height", UnitOfMeasure.distance,
                        column.EstimatedHeight);
                    panel.CreateAndAddResultRow(column, "Estimated Diameter", UnitOfMeasure.distance,
                        column.EstimatedDiameter);
                });
        }

    }

}
