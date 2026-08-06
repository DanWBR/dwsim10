using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using DWSIM.UI.Shared.Avalonia;
using Compressor = DWSIM.UnitOperations.UnitOperations.Compressor;
using PumpOps = DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps;
using Thickness = Avalonia.Thickness;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Performance curves of a compressor. Unlike the pump, which carries a single curve set,
    /// the compressor keeps one set per rotation speed, so the editor picks the speed first.
    /// </summary>
    public static class CompressorCurvesEditor
    {

        public static void Show(Compressor compressor)
        {
            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(
                compressor.GraphicObject.Tag + ": Performance Curves", 720, 640, panel);

            var speeds = compressor.Curves.Keys.OrderBy(x => x).ToList();

            if (speeds.Count == 0)
            {
                panel.CreateAndAddDescriptionRow(
                    "This compressor has no performance curves yet. Curves are read from the " +
                    "curve database or from a simulation that already carries them.");
                window.Show();
                return;
            }

            var host = new ContentControl();

            var picker = panel.CreateAndAddDropDownRow("Rotation Speed (rpm)",
                speeds.Select(x => x.ToString()).ToList(), 0,
                (dd, e) =>
                {
                    if (dd.SelectedIndex < 0 || dd.SelectedIndex >= speeds.Count) return;
                    host.Content = BuildCurves(compressor.Curves[speeds[dd.SelectedIndex]]);
                });

            host.Content = BuildCurves(compressor.Curves[speeds[0]]);
            host.Margin = new Thickness(0, 8, 0, 0);
            host.Height = 460;
            panel.Children.Add(host);

            window.Show();
        }

        /// <summary>One tab per curve of the set, each a table of points.</summary>
        private static Control BuildCurves(Dictionary<string, PumpOps.Curve> curves)
        {
            var tabs = new TabControl();

            foreach (var item in curves)
            {
                var curve = item.Value;
                var header = string.IsNullOrEmpty(curve.Name) ? item.Key : curve.Name;
                tabs.Items.Add(PerformanceCurvesEditor.CurveTab(header, curve));
            }

            return tabs;
        }

    }

}
