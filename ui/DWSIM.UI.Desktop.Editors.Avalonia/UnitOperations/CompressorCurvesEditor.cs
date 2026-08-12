using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using DWSIM.UI.Shared.Avalonia;
using Compressor = DWSIM.UnitOperations.UnitOperations.Compressor;
using Expander = DWSIM.UnitOperations.UnitOperations.Expander;
using PumpOps = DWSIM.UnitOperations.UnitOperations.Auxiliary.PumpOps;
using Thickness = Avalonia.Thickness;

namespace DWSIM.UI.Desktop.Editors
{

    /// <summary>
    /// Performance curves of a compressor or an expander. Each keeps one curve set per rotation
    /// speed, so the editor picks the speed first and lets you add or remove speeds; a machine that
    /// carries no curves yet gets a default set built from the unit's own defaults, so the editor
    /// opens on something to edit instead of a dead end, as the Windows form does.
    /// </summary>
    public static class CompressorCurvesEditor
    {

        public static void Show(Compressor compressor)
        {
            ShowCurves(compressor.GraphicObject.Tag, compressor.Curves,
                () => compressor.CreateCurves(), compressor.Speed);
        }

        public static void Show(Expander expander)
        {
            ShowCurves(expander.GraphicObject.Tag, expander.Curves,
                () => expander.CreateCurves(), expander.Speed);
        }

        private static void ShowCurves(string tag,
            Dictionary<int, Dictionary<string, PumpOps.Curve>> curves,
            Func<Dictionary<string, PumpOps.Curve>> createDefault, int defaultSpeed)
        {
            // a machine placed but never configured carries no curves; give it the default set so
            // the editor has something to show instead of a message the user cannot act on
            if (curves.Count == 0) curves[defaultSpeed] = createDefault();

            var panel = AvaloniaCommon.GetDefaultContainer();
            var window = AvaloniaCommon.GetDefaultEditorForm(
                tag + ": Performance Curves", 720, 640, panel);

            var host = new ContentControl { Height = 452, Margin = new Thickness(0, 8, 0, 0) };
            var picker = new ComboBox { MinWidth = 120 };
            var updating = false;

            void ShowSpeed(int rpm)
            {
                host.Content = curves.ContainsKey(rpm) ? BuildCurves(curves[rpm]) : null;
            }

            void Rebuild(int selectSpeed)
            {
                updating = true;
                var speeds = curves.Keys.OrderBy(x => x).ToList();
                picker.ItemsSource = speeds.Select(x => x.ToString()).ToList();
                var idx = speeds.IndexOf(selectSpeed);
                if (idx < 0) idx = 0;
                picker.SelectedIndex = idx;
                ShowSpeed(speeds[idx]);
                updating = false;
            }

            picker.SelectionChanged += (s, e) =>
            {
                if (updating) return;
                var speeds = curves.Keys.OrderBy(x => x).ToList();
                if (picker.SelectedIndex >= 0 && picker.SelectedIndex < speeds.Count)
                    ShowSpeed(speeds[picker.SelectedIndex]);
            };

            var rpmBox = new TextBox { Text = defaultSpeed.ToString(), Width = 90, Margin = new Thickness(8, 0, 4, 0) };

            var add = new Button { Content = "Add Speed" };
            add.Classes.Add("panel");
            add.Click += (s, e) =>
            {
                if (!int.TryParse(rpmBox.Text, out var rpm) || rpm <= 0) return;
                if (!curves.ContainsKey(rpm)) curves[rpm] = createDefault();
                Rebuild(rpm);
            };

            var remove = new Button { Content = "Remove Speed", Margin = new Thickness(6, 0, 0, 0) };
            remove.Classes.Add("panel");
            remove.Click += (s, e) =>
            {
                if (curves.Count <= 1) return;
                var speeds = curves.Keys.OrderBy(x => x).ToList();
                if (picker.SelectedIndex < 0 || picker.SelectedIndex >= speeds.Count) return;
                curves.Remove(speeds[picker.SelectedIndex]);
                Rebuild(curves.Keys.OrderBy(x => x).First());
            };

            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
            header.Children.Add(new TextBlock
            {
                Text = "Rotation Speed (rpm)",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            header.Children.Add(picker);
            header.Children.Add(rpmBox);
            header.Children.Add(add);
            header.Children.Add(remove);

            panel.Children.Add(header);
            panel.Children.Add(host);

            Rebuild(curves.ContainsKey(defaultSpeed) ? defaultSpeed : curves.Keys.OrderBy(x => x).First());

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
