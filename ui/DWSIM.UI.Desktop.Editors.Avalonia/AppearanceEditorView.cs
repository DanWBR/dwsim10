//    Appearance editor for flowsheet graphic objects (cross-platform interface)
//    Copyright 2026 Daniel Wagner Oliveira de Medeiros
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.Drawing.SkiaSharp.Appearance;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;
using SkiaSharp;

namespace DWSIM.UI.Desktop.Editors
{
    /// <summary>
    /// The appearance properties of one or more flowsheet objects, built from the shared
    /// <see cref="AppearanceDescriptors"/> table. Every change is applied at once to each object that
    /// has the property, and <c>changed</c> is called so the host repaints the flowsheet. The fields
    /// show the values of the first object; a field whose value differs among the objects is shown blank.
    /// </summary>
    public sealed class AppearanceEditorView : StackPanel
    {
        private readonly IReadOnlyList<IGraphicObject> _objects;
        private readonly Action _changed;
        private readonly Func<bool> _layoutLocked;

        private readonly CheckBox _showHelp = new() { Content = "Show the explanation under every field" };
        private readonly List<Control> _helpRows = new();
        private readonly List<Control> _positionEditors = new();
        private readonly Dictionary<string, Control> _editors = new();
        private TextBlock _positionNote;
        private bool _loading;

        /// <param name="objects">The objects to edit; the first one decides which fields are shown.</param>
        /// <param name="changed">Called after every change, to repaint the flowsheet.</param>
        /// <param name="layoutLocked">True when the whole flowsheet layout is locked (X and Y read-only).</param>
        public AppearanceEditorView(IEnumerable<IGraphicObject> objects, Action changed = null, Func<bool> layoutLocked = null)
        {
            _objects = objects.Where(o => o != null && !o.IsConnector).Distinct().ToList();
            _changed = changed ?? (() => { });
            _layoutLocked = layoutLocked ?? (() => false);
            Spacing = 2;
            _showHelp.IsCheckedChanged += (_, _) => { foreach (var r in _helpRows) r.IsVisible = _showHelp.IsChecked == true; };
            Rebuild();
        }

        /// <summary>The objects being edited.</summary>
        public IReadOnlyList<IGraphicObject> Objects => _objects;

        /// <summary>Reads every value again from the objects and rebuilds the fields.</summary>
        public void Rebuild()
        {
            _loading = true;
            try
            {
                Children.Clear();
                _helpRows.Clear();
                _positionEditors.Clear();
                _editors.Clear();
                _positionNote = null;

                if (_objects.Count == 0)
                {
                    Children.Add(Description("Nothing selected."));
                    return;
                }

                var descriptors = AppearanceDescriptors.ForObject(_objects[0]);
                if (descriptors.Count == 0)
                {
                    Children.Add(Description("This object has no appearance settings."));
                    return;
                }

                if (_objects.Count > 1)
                    Children.Add(Description($"{_objects.Count} objects selected. A change applies to every selected object that has the property; a blank field means their values differ."));

                Children.Add(_showHelp);

                foreach (var group in descriptors.GroupBy(d => d.Group).OrderBy(g => g.Key))
                {
                    var header = new TextBlock
                    {
                        Text = AppearanceDescriptors.GroupDisplayName(group.Key),
                        FontWeight = FontWeight.SemiBold,
                        FontSize = UiScale.Font(12),
                        Margin = new Thickness(0, 10, 0, 4)
                    };
                    Children.Add(header);
                    foreach (var d in group) AddRow(d);
                    if (group.Key == AppearanceGroup.Position)
                    {
                        _positionNote = Description("");
                        _positionNote.Margin = new Thickness(0, 2, 0, 4);
                        Children.Add(_positionNote);
                    }
                }

                foreach (var r in _helpRows) r.IsVisible = _showHelp.IsChecked == true;
                UpdatePositionState();
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>Reads X and Y again, which change when the flowsheet view is panned or zoomed.</summary>
        public void RefreshPosition()
        {
            _loading = true;
            try
            {
                foreach (var key in new[] { "X", "Y" })
                {
                    if (!_editors.TryGetValue(key, out var ed) || ed is not NumericUpDown nud) continue;
                    var d = AppearanceDescriptors.Find(key);
                    var common = d.GetCommonValue(_objects);
                    nud.Value = common == null ? null : (decimal?)Convert.ToDecimal(common);
                }
            }
            finally
            {
                _loading = false;
            }
        }

        private IEnumerable<IGraphicObject> Targets(AppearancePropertyDescriptor d) => _objects.Where(d.AppliesTo);

        private void Apply(AppearancePropertyDescriptor d, object value)
        {
            if (_loading || value == null) return;
            if (d.IsPosition && _layoutLocked()) return;
            // controls may report their initial value again once they are shown: that is not an edit
            if (Targets(d).All(o => SameValue(d, d.GetValue(o), value))) return;
            foreach (var o in Targets(d)) d.SetValue(o, value);

            // a line color only shows with custom colors on, and the descriptor turns them on
            if (d.Key == "LineColor" && _editors.TryGetValue("OverrideColors", out var ov) && ov is CheckBox ovc)
            {
                _loading = true;
                try { ovc.IsThreeState = false; ovc.IsChecked = true; }
                finally { _loading = false; }
            }
            if (d.Key == "PositionLocked") UpdatePositionState();
            _changed();
        }

        private void AddRow(AppearancePropertyDescriptor d)
        {
            var common = d.GetCommonValue(_objects);
            var value = common ?? d.GetValue(_objects[0]);
            var mixed = common == null && Targets(d).Count() > 1;
            Control row;
            Control editor;

            switch (d.Kind)
            {
                case AppearancePropertyKind.Boolean:
                {
                    var cb = new CheckBox { Content = d.DisplayName, IsThreeState = mixed, IsChecked = mixed ? null : Convert.ToBoolean(value) };
                    cb.IsCheckedChanged += (_, _) =>
                    {
                        if (_loading || cb.IsChecked == null) return;
                        cb.IsThreeState = false;
                        Apply(d, cb.IsChecked == true);
                    };
                    editor = cb;
                    row = AvaloniaEditorPanel.MakeFullRow(cb);
                    break;
                }
                case AppearancePropertyKind.Color:
                {
                    var sk = value is SKColor c ? c : SKColors.Black;
                    var cp = new ColorPicker { Color = Color.FromArgb(sk.Alpha, sk.Red, sk.Green, sk.Blue), Width = 160 };
                    cp.ColorChanged += (_, _) =>
                    {
                        var a = cp.Color;
                        Apply(d, new SKColor(a.R, a.G, a.B, a.A));
                    };
                    editor = cp;
                    row = AvaloniaEditorPanel.MakeLabelControlRow(Label(d, mixed), cp);
                    break;
                }
                case AppearancePropertyKind.Double:
                case AppearancePropertyKind.Integer:
                {
                    var nud = new NumericUpDown
                    {
                        Width = 160,
                        FormatString = d.DecimalPlaces > 0 ? "F" + d.DecimalPlaces : "F0",
                        Increment = 1,
                        Value = mixed ? null : (decimal?)Convert.ToDecimal(value)
                    };
                    if (d.Maximum > d.Minimum)
                    {
                        nud.Minimum = (decimal)d.Minimum;
                        nud.Maximum = (decimal)d.Maximum;
                    }
                    nud.ValueChanged += (_, _) =>
                    {
                        if (nud.Value == null) return;
                        Apply(d, (double)nud.Value.Value);
                    };
                    editor = nud;
                    if (d.Presets.Length > 0)
                    {
                        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                        line.Children.Add(nud);
                        foreach (var p in d.Presets)
                        {
                            var preset = p;
                            var b = new Button { Content = preset.ToString("0"), MinWidth = 36, Padding = new Thickness(4, 2) };
                            ToolTip.SetTip(b, $"Set to {preset:0} degrees");
                            b.Click += (_, _) => nud.Value = (decimal)preset;
                            line.Children.Add(b);
                        }
                        row = AvaloniaEditorPanel.MakeLabelControlRow(Label(d, mixed), line);
                    }
                    else
                    {
                        row = AvaloniaEditorPanel.MakeLabelControlRow(Label(d, mixed), nud);
                    }
                    break;
                }
                case AppearancePropertyKind.Enum:
                {
                    var names = d.Options.Select(o => o.Value).ToList();
                    var idx = mixed ? -1 : d.Options.FindIndex(o => Equals(o.Key, value));
                    var combo = new ComboBox { Width = 160 };
                    foreach (var n in names) combo.Items.Add(n);
                    if (idx >= 0) combo.SelectedIndex = idx;
                    combo.SelectionChanged += (_, _) =>
                    {
                        if (combo.SelectedIndex < 0 || combo.SelectedIndex >= d.Options.Count) return;
                        Apply(d, d.Options[combo.SelectedIndex].Key);
                    };
                    editor = combo;
                    row = AvaloniaEditorPanel.MakeLabelControlRow(Label(d, mixed), combo);
                    break;
                }
                default:
                {
                    // Avalonia raises TextChanged for the initial text through the dispatcher: only a text
                    // that differs from the committed one is an edit
                    var committed = mixed ? "" : Convert.ToString(value) ?? "";
                    var tb = new TextBox
                    {
                        Text = committed,
                        AcceptsReturn = d.Multiline,
                        TextWrapping = d.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                        MinHeight = d.Multiline ? 60 : 0,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    tb.TextChanged += (_, _) =>
                    {
                        var text = tb.Text ?? "";
                        if (text == committed) return;
                        committed = text;
                        Apply(d, text);
                    };
                    editor = tb;
                    var stack = new StackPanel { Spacing = 2 };
                    stack.Children.Add(new TextBlock { Text = Label(d, mixed) });
                    stack.Children.Add(tb);
                    row = stack;
                    break;
                }
            }

            ToolTip.SetTip(row, d.Help);
            _editors[d.Key] = editor;
            if (d.IsPosition) _positionEditors.Add(editor);
            Children.Add(row);

            if (!string.IsNullOrWhiteSpace(d.Help))
            {
                var help = Description(d.Help);
                help.Margin = new Thickness(12, 0, 0, 6);
                help.IsVisible = false;
                _helpRows.Add(help);
                Children.Add(help);
            }
        }

        /// <summary>X and Y are read-only while the layout or every edited object is locked.</summary>
        private void UpdatePositionState()
        {
            if (_positionEditors.Count == 0) return;
            var targets = _objects.ToList();
            var layout = _layoutLocked();
            var lockedCount = targets.Count(o => o.PositionLocked);
            var allLocked = layout || lockedCount == targets.Count;
            foreach (var e in _positionEditors) e.IsEnabled = !allLocked;
            if (_positionNote == null) return;
            if (layout)
                _positionNote.Text = "The flowsheet layout is locked (Lock Layout on the flowsheet toolbar), so X and Y cannot be changed.";
            else if (allLocked)
                _positionNote.Text = "The position is locked. Clear Lock position to move the object.";
            else if (lockedCount > 0)
                _positionNote.Text = "Some of the selected objects are locked; X and Y change only the others.";
            else
                _positionNote.Text = "";
            _positionNote.IsVisible = !string.IsNullOrEmpty(_positionNote.Text);
        }

        private static bool SameValue(AppearancePropertyDescriptor d, object current, object value)
        {
            if (current == null) return false;
            switch (d.Kind)
            {
                case AppearancePropertyKind.Double:
                case AppearancePropertyKind.Integer:
                    var a = AppearanceDescriptors.ParseNumber(current);
                    var b = AppearanceDescriptors.ParseNumber(value);
                    return a.HasValue && b.HasValue && Math.Abs(a.Value - b.Value) < 1e-9;
                default:
                    return Equals(current, value);
            }
        }

        private static string Label(AppearancePropertyDescriptor d, bool mixed) => mixed ? d.DisplayName + " (values differ)" : d.DisplayName;

        private static TextBlock Description(string text) => new()
        {
            Text = text, TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(11), Opacity = 0.7
        };
    }
}
