using System.Collections.Generic;
using DWSIM.UI.Shared.Avalonia;
using HeatExchanger = DWSIM.UnitOperations.UnitOperations.HeatExchanger;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Shell and tube geometry of a heat exchanger, which the rating and fouling factor modes
    /// read. The values are kept in the units the engine stores them in, which is what the
    /// Windows properties form edits as well: millimetres for diameters and spacing, metres for
    /// the tube length.
    /// </summary>
    public static class ShellAndTubeEditor
    {

        private static readonly List<string> Fluids = new List<string> { "Cold Fluid", "Hot Fluid" };

        private static readonly List<string> BaffleTypes = new List<string>
        {
            "Single", "Double", "Triple", "Grid (NTIW)"
        };

        private static readonly List<string> BaffleOrientations = new List<string> { "Horizontal", "Vertical" };

        private static readonly List<string> TubeLayouts = new List<string>
        {
            "Triangular (30°)", "Rotated Square (45°)", "Square (90°)", "Rotated Triangular (60°)"
        };

        public static void Show(HeatExchanger hx)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(
                hx.GraphicObject.Tag + ": Shell and Tube Properties", 620, 700, panel);

            var st = hx.STProperties;
            var nf = hx.GetFlowsheet().FlowsheetOptions.NumberFormat;

            panel.CreateAndAddLabelRow("Shell");
            panel.CreateAndAddDropDownRow("Shell Fluid", Fluids, st.Shell_Fluid,
                (dd, e) => st.Shell_Fluid = dd.SelectedIndex);
            Number(panel, nf, "Number of Shells in Series", st.Shell_NumberOfShellsInSeries,
                v => st.Shell_NumberOfShellsInSeries = (int)v);
            Number(panel, nf, "Number of Shell Passes", st.Shell_NumberOfPasses,
                v => st.Shell_NumberOfPasses = (int)v);
            Number(panel, nf, "Shell Internal Diameter (mm)", st.Shell_Di, v => st.Shell_Di = v);
            Number(panel, nf, "Shell Fouling Factor (K.m2/W)", st.Shell_Fouling, v => st.Shell_Fouling = v);
            Number(panel, nf, "Shell Roughness (mm)", st.Shell_Roughness, v => st.Shell_Roughness = v);

            panel.CreateAndAddLabelRow("Baffles");
            panel.CreateAndAddDropDownRow("Baffle Type", BaffleTypes, st.Shell_BaffleType,
                (dd, e) => st.Shell_BaffleType = dd.SelectedIndex);
            panel.CreateAndAddDropDownRow("Baffle Orientation", BaffleOrientations, st.Shell_BaffleOrientation,
                (dd, e) => st.Shell_BaffleOrientation = dd.SelectedIndex);
            Number(panel, nf, "Baffle Cut (%)", st.Shell_BaffleCut, v => st.Shell_BaffleCut = v);
            Number(panel, nf, "Baffle Spacing (mm)", st.Shell_BaffleSpacing, v => st.Shell_BaffleSpacing = v);

            panel.CreateAndAddLabelRow("Tubes");
            panel.CreateAndAddDropDownRow("Tube Fluid", Fluids, st.Tube_Fluid,
                (dd, e) => st.Tube_Fluid = dd.SelectedIndex);
            Number(panel, nf, "Tubes per Shell", st.Tube_NumberPerShell, v => st.Tube_NumberPerShell = (int)v);
            Number(panel, nf, "Tube Passes per Shell", st.Tube_PassesPerShell, v => st.Tube_PassesPerShell = (int)v);
            Number(panel, nf, "Tube Internal Diameter (mm)", st.Tube_Di, v => st.Tube_Di = v);
            Number(panel, nf, "Tube External Diameter (mm)", st.Tube_De, v => st.Tube_De = v);
            Number(panel, nf, "Tube Length (m)", st.Tube_Length, v => st.Tube_Length = v);
            Number(panel, nf, "Tube Pitch (mm)", st.Tube_Pitch, v => st.Tube_Pitch = v);
            panel.CreateAndAddDropDownRow("Tube Layout", TubeLayouts, st.Tube_Layout,
                (dd, e) => st.Tube_Layout = dd.SelectedIndex);
            Number(panel, nf, "Tube Fouling Factor (K.m2/W)", st.Tube_Fouling, v => st.Tube_Fouling = v);
            Number(panel, nf, "Tube Roughness (mm)", st.Tube_Roughness, v => st.Tube_Roughness = v);
            Number(panel, nf, "Tube Thermal Conductivity (W/[m.K])", st.Tube_ThermalConductivity,
                v => st.Tube_ThermalConductivity = v);
            Number(panel, nf, "Scaling Friction Correction Factor", st.Tube_Scaling_FricCorrFactor,
                v => st.Tube_Scaling_FricCorrFactor = v);

            window.Show();
        }

        private static void Number(AvaloniaEditorPanel panel, string nf, string label,
                                   double value, System.Action<double> commit)
        {
            panel.CreateAndAddTextBoxRow(nf, label, value,
                (tb, e) => { if (UnitOpEditorRows.TryParse(tb.Text, out var v)) commit(v); });
        }

    }

}
