using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// Extension methods for AvaloniaEditorPanel that mirror the Eto.Forms CreateAndAdd* API
/// defined in DWSIM.ExtensionMethods.Eto/EtoExtensions.cs.
///
/// PopulateEditorPanel implementations can be migrated by changing the container cast from
/// DynamicLayout to AvaloniaEditorPanel; the CreateAndAdd* call sites remain identical.
/// </summary>
public static class AvaloniaEditorExtensions
{
    private static int ControlWidth => 160;

    // -------------------------------------------------------------------------
    // Section headers
    // -------------------------------------------------------------------------

    public static TextBlock CreateAndAddLabelRow(this AvaloniaEditorPanel panel, string text)
    {
        panel.Children.Add(new Border { Height = 3 });

        var lbl = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 0)
        };
        panel.Children.Add(lbl);

        panel.Children.Add(new Separator { Margin = new Thickness(0, 1, 0, 3) });

        return lbl;
    }

    public static TextBlock CreateAndAddLabelRow2(this AvaloniaEditorPanel panel, string text)
    {
        var lbl = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2)
        };
        panel.Children.Add(lbl);
        return lbl;
    }

    public static TextBlock CreateAndAddLabelRow3(this AvaloniaEditorPanel panel, string text)
    {
        var lbl = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(11),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 3)
        };
        panel.Children.Add(lbl);
        return lbl;
    }

    public static TextBlock CreateAndAddDescriptionRow(this AvaloniaEditorPanel panel, string text)
    {
        var lbl = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(10),
            Foreground = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            Margin = new Thickness(0, 1, 0, 3)
        };
        panel.Children.Add(lbl);
        return lbl;
    }

    public static void CreateAndAddEmptySpace(this AvaloniaEditorPanel panel)
    {
        panel.Children.Add(new Border { Height = 3 });
    }

    // -------------------------------------------------------------------------
    // TextBox rows
    // -------------------------------------------------------------------------

    public static TextBox CreateAndAddTextBoxRow(this AvaloniaEditorPanel panel,
        string numberformat, string label, double currval,
        Action<TextBox, EventArgs>? command, Action? keypress = null)
    {
        var tb = new TextBox
        {
            Text = currval.ToString(numberformat, CultureInfo.InvariantCulture),
            Width = ControlWidth,
            TextAlignment = TextAlignment.Right
        };

        WireUnitContextMenu(tb, label);

        if (command != null) tb.TextChanged += (s, e) => { command((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };
        if (keypress != null) tb.KeyDown += (s, e) =>
        {
            if (e.Key == global::Avalonia.Input.Key.Enter) keypress();
        };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, tb));
        return tb;
    }

    public static TextBox CreateAndAddTextBoxRow2(this AvaloniaEditorPanel panel,
        string numberformat, string label, double currval,
        Action<TextBox, EventArgs>? command)
    {
        return panel.CreateAndAddTextBoxRow(numberformat, label, currval, command);
    }

    public static TextBox CreateAndAddStringEditorRow(this AvaloniaEditorPanel panel,
        string label, string currval,
        Action<TextBox, EventArgs>? command, Action? keypress = null)
    {
        var tb = new TextBox { Text = currval, Width = ControlWidth };

        if (command != null) tb.TextChanged += (s, e) => { command((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };
        if (keypress != null) tb.KeyDown += (s, e) =>
        {
            if (e.Key == global::Avalonia.Input.Key.Enter) keypress();
        };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, tb));
        return tb;
    }

    public static TextBox CreateAndAddStringEditorRow(this AvaloniaEditorPanel panel,
        string label, string currval,
        Action<TextBox, EventArgs>? command, int tbwidth, Action? keypress = null)
    {
        var tb = new TextBox { Text = currval, Width = tbwidth };

        if (command != null) tb.TextChanged += (s, e) => { command((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };
        if (keypress != null) tb.KeyDown += (s, e) =>
        {
            if (e.Key == global::Avalonia.Input.Key.Enter) keypress();
        };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, tb));
        return tb;
    }

    public static TextBox CreateAndAddStringEditorRow2(this AvaloniaEditorPanel panel,
        string label, string placeholder, string currval,
        Action<TextBox, EventArgs>? command)
    {
        var tb = new TextBox { Text = currval, Watermark = placeholder, Width = ControlWidth };

        if (command != null) tb.TextChanged += (s, e) => { command((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, tb));
        return tb;
    }

    public static TextBox CreateAndAddFullTextBoxRow(this AvaloniaEditorPanel panel,
        string text, Action<TextBox, EventArgs>? command)
    {
        var tb = new TextBox { Text = text, HorizontalAlignment = HorizontalAlignment.Stretch };

        if (command != null) tb.TextChanged += (s, e) => { command((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(string.Empty, tb));
        return tb;
    }

    // -------------------------------------------------------------------------
    // Two-textbox rows
    // -------------------------------------------------------------------------

    public static TextBox CreateAndAddDoubleTextBoxRow(this AvaloniaEditorPanel panel,
        string numberformat, string label, string currval1, double currval2,
        Action<TextBox, EventArgs>? command, Action<TextBox, EventArgs>? command2)
    {
        var tb1 = new TextBox { Text = currval1, Width = 100 };
        var tb2 = new TextBox
        {
            Text = currval2.ToString(numberformat, CultureInfo.InvariantCulture),
            Width = 100
        };

        if (command != null) tb1.TextChanged += (s, e) => { command((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };
        if (command2 != null) tb2.TextChanged += (s, e) => { command2((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,4,Auto"),
            Margin = new Thickness(0, 1, 0, 1)
        };
        var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(tb1, 2);
        Grid.SetColumn(tb2, 4);
        row.Children.Add(lbl);
        row.Children.Add(tb1);
        row.Children.Add(tb2);

        panel.Children.Add(row);
        return tb2;
    }

    public static TextBox[] CreateAndAddDoubleTextBoxRow2(this AvaloniaEditorPanel panel,
        string numberformat, string label, double currval1, double currval2,
        Action<TextBox, EventArgs>? command, Action<TextBox, EventArgs>? command2)
    {
        var tb1 = new TextBox { Text = currval1.ToString(numberformat, CultureInfo.InvariantCulture), Width = 100 };
        var tb2 = new TextBox { Text = currval2.ToString(numberformat, CultureInfo.InvariantCulture), Width = 100 };

        if (command != null) tb1.TextChanged += (s, e) => { command((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };
        if (command2 != null) tb2.TextChanged += (s, e) => { command2((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,4,Auto"),
            Margin = new Thickness(0, 1, 0, 1)
        };
        var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(tb1, 2);
        Grid.SetColumn(tb2, 4);
        row.Children.Add(lbl);
        row.Children.Add(tb1);
        row.Children.Add(tb2);

        panel.Children.Add(row);
        return new[] { tb1, tb2 };
    }

    // -------------------------------------------------------------------------
    // Numeric editor rows
    // -------------------------------------------------------------------------

    public static NumericUpDown CreateAndAddNumericEditorRow(this AvaloniaEditorPanel panel,
        string label, double currval, double minval, double maxval, int decimalplaces,
        Action<NumericUpDown, EventArgs>? command)
    {
        var ed = new NumericUpDown
        {
            Value = (decimal)currval,
            Minimum = (decimal)minval,
            Maximum = (decimal)maxval,
            FormatString = decimalplaces > 0 ? "F" + decimalplaces : "F0",
            Width = ControlWidth
        };

        if (command != null) ed.ValueChanged += (s, e) => { command((NumericUpDown)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, ed));
        return ed;
    }

    public static TextBox CreateAndAddNumericEditorRow2(this AvaloniaEditorPanel panel,
        string label, double currval, double minval, double maxval, int decimalplaces,
        Action<TextBox, EventArgs>? command)
    {
        var tb = new TextBox
        {
            Text = currval.ToString("F" + decimalplaces, CultureInfo.InvariantCulture),
            Width = ControlWidth,
            TextAlignment = TextAlignment.Right
        };

        if (command != null)
        {
            tb.TextChanged += (s, e) =>
            {
                if (double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
                    && v >= minval && v <= maxval)
                {
                    tb.Foreground = Brushes.Blue;
                    command((TextBox)s!, e);
                    panel.OnAfterEdit?.Invoke();
                }
                else
                {
                    tb.Foreground = Brushes.Red;
                }
            };
        }

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, tb));
        return tb;
    }

    // -------------------------------------------------------------------------
    // TextArea rows
    // -------------------------------------------------------------------------

    public static TextBox CreateAndAddMultilineTextBoxRow(this AvaloniaEditorPanel panel,
        string text, bool readOnly, bool autoSized, Action<TextBox, EventArgs>? command)
    {
        var ta = new TextBox
        {
            Text = text,
            IsReadOnly = readOnly,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = autoSized ? double.NaN : 120
        };

        if (command != null) ta.TextChanged += (s, e) => { command((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeFullRow(ta));
        return ta;
    }

    public static TextBox CreateAndAddMultilineMonoSpaceTextBoxRow(this AvaloniaEditorPanel panel,
        string text, int height, bool readOnly, Action<TextBox, EventArgs>? command)
    {
        var ta = new TextBox
        {
            Text = text,
            IsReadOnly = readOnly,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = height
        };

        if (command != null) ta.TextChanged += (s, e) => { command((TextBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeFullRow(ta));
        return ta;
    }

    // -------------------------------------------------------------------------
    // DropDown / ComboBox rows
    // -------------------------------------------------------------------------

    public static ComboBox CreateAndAddDropDownRow(this AvaloniaEditorPanel panel,
        string label, List<string> options, int position,
        Action<ComboBox, EventArgs>? command, Action? keypress = null)
    {
        var cb = new ComboBox { Width = ControlWidth };
        foreach (var item in options) cb.Items.Add(item);
        if (options.Count > 0 && position >= 0 && position < options.Count)
            cb.SelectedIndex = position;

        if (command != null) cb.SelectionChanged += (s, e) => { command((ComboBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, cb));
        return cb;
    }

    public static ComboBox CreateAndAddDropDownRow(this AvaloniaEditorPanel panel,
        string label, List<string> options, string selectedItem,
        Action<ComboBox, EventArgs>? command, Action? keypress = null)
    {
        var cb = new ComboBox { Width = ControlWidth };
        foreach (var item in options) cb.Items.Add(item);
        var idx = options.IndexOf(selectedItem);
        if (idx >= 0) cb.SelectedIndex = idx;

        if (command != null) cb.SelectionChanged += (s, e) => { command((ComboBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, cb));
        return cb;
    }

    public static ComboBox CreateAndAddDropDownRow(this AvaloniaEditorPanel panel,
        string label, List<string> options, int position,
        Action<ComboBox, EventArgs>? command, int ddwidth, Action? keypress = null)
    {
        var cb = new ComboBox { Width = ddwidth };
        foreach (var item in options) cb.Items.Add(item);
        if (options.Count > 0 && position >= 0 && position < options.Count)
            cb.SelectedIndex = position;

        if (command != null) cb.SelectionChanged += (s, e) => { command((ComboBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, cb));
        return cb;
    }

    public static AutoCompleteBox CreateAndAddEditableDropDownRow(this AvaloniaEditorPanel panel,
        string label, List<string> options, int position,
        Action<AutoCompleteBox, EventArgs>? command, Action? keypress = null)
    {
        var cb = new AutoCompleteBox
        {
            Width = ControlWidth,
            ItemsSource = options,
            FilterMode = AutoCompleteFilterMode.Contains
        };
        if (options.Count > 0 && position >= 0 && position < options.Count)
            cb.Text = options[position];

        if (command != null) cb.TextChanged += (s, e) => { command((AutoCompleteBox)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, cb));
        return cb;
    }

    // -------------------------------------------------------------------------
    // CheckBox row
    // -------------------------------------------------------------------------

    public static CheckBox CreateAndAddCheckBoxRow(this AvaloniaEditorPanel panel,
        string text, bool value,
        Action<CheckBox, EventArgs>? command, Action? keypress = null)
    {
        var cb = new CheckBox { Content = text, IsChecked = value };

        if (command != null) cb.IsCheckedChanged += (s, e) => { command((CheckBox)s!, e); panel.OnAfterEdit?.Invoke(); };
        if (keypress != null) cb.IsCheckedChanged += (_, _) => keypress();

        panel.Children.Add(AvaloniaEditorPanel.MakeFullRow(cb));
        panel.Children.Add(new Border { Height = 1 });
        return cb;
    }

    // -------------------------------------------------------------------------
    // Button rows
    // -------------------------------------------------------------------------

    public static Button CreateAndAddButtonRow(this AvaloniaEditorPanel panel,
        string buttonLabel, string? imageResId, Action<Button, EventArgs>? command)
    {
        var btn = new Button
        {
            Content = buttonLabel,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        btn.Classes.Add("panel");

        if (command != null) btn.Click += (s, e) => command((Button)s!, e);

        panel.Children.Add(AvaloniaEditorPanel.MakeFullRow(btn));
        return btn;
    }

    public static Button CreateAndAddLabelAndButtonRow(this AvaloniaEditorPanel panel,
        string label, string buttonLabel, string? imageResId,
        Action<Button, EventArgs>? command)
    {
        var btn = new Button { Content = buttonLabel, Width = ControlWidth };
        btn.Classes.Add("panel");

        if (command != null) btn.Click += (s, e) => command((Button)s!, e);

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, btn));
        return btn;
    }

    public static Button CreateAndAddBoldLabelAndButtonRow(this AvaloniaEditorPanel panel,
        string label, string buttonLabel, string? imageResId,
        Action<Button, EventArgs>? command)
    {
        var btn = new Button { Content = buttonLabel, Width = ControlWidth };
        btn.Classes.Add("panel");

        if (command != null) btn.Click += (s, e) => command((Button)s!, e);

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, btn, boldLabel: true));
        return btn;
    }

    public static void CreateAndAddLabelAndControlRow(this AvaloniaEditorPanel panel,
        string label, Control control)
    {
        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, control));
    }

    public static Control CreateAndAddControlRow(this AvaloniaEditorPanel panel, Control control)
    {
        panel.Children.Add(AvaloniaEditorPanel.MakeFullRow(control));
        return control;
    }

    // Two-button rows

    public static (Button, Button) CreateAndAddTwoButtonsRow(this AvaloniaEditorPanel panel,
        string buttonLabel, string? imageResId,
        string buttonLabel2, string? imageResId2,
        Action<Button, EventArgs>? command, Action<Button, EventArgs>? command2)
    {
        var btn = new Button { Content = buttonLabel, Width = 100 };
        btn.Classes.Add("panel");
        var btn2 = new Button { Content = buttonLabel2, Width = 100 };
        btn2.Classes.Add("panel");

        if (command != null) btn.Click += (s, e) => command((Button)s!, e);
        if (command2 != null) btn2.Click += (s, e) => command2((Button)s!, e);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 4 };
        row.Children.Add(btn);
        row.Children.Add(btn2);
        panel.Children.Add(row);
        return (btn, btn2);
    }

    public static (Button, Button) CreateAndAddLabelAndTwoButtonsRow(this AvaloniaEditorPanel panel,
        string label,
        string buttonLabel, string? imageResId,
        string buttonLabel2, string? imageResId2,
        Action<Button, EventArgs>? command, Action<Button, EventArgs>? command2)
    {
        var btn = new Button { Content = buttonLabel, Width = 100 };
        btn.Classes.Add("panel");
        var btn2 = new Button { Content = buttonLabel2, Width = 100 };
        btn2.Classes.Add("panel");

        if (command != null) btn.Click += (s, e) => command((Button)s!, e);
        if (command2 != null) btn2.Click += (s, e) => command2((Button)s!, e);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        btnPanel.Children.Add(btn);
        btnPanel.Children.Add(btn2);

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, btnPanel));
        return (btn, btn2);
    }

    // TextBox + button rows

    public static TextBox CreateAndAddLabelAndTextBoxAndButtonRow(this AvaloniaEditorPanel panel,
        string label, string textboxValue, string buttonLabel, string? imageResId,
        Action<TextBox, EventArgs>? txteditcommand, Action<Button, EventArgs>? command)
    {
        var tb = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        tb.Text = textboxValue;
        var btn = new Button { Content = buttonLabel, Width = 80 };
        btn.Classes.Add("panel");

        if (txteditcommand != null) tb.TextChanged += (s, e) => txteditcommand((TextBox)s!, e);
        if (command != null) btn.Click += (s, e) => command((Button)s!, e);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,4,Auto"),
            Margin = new Thickness(0, 1, 0, 1)
        };
        var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(tb, 1);
        Grid.SetColumn(btn, 3);
        row.Children.Add(lbl);
        row.Children.Add(tb);
        row.Children.Add(btn);

        panel.Children.Add(row);
        return tb;
    }

    public static TextBox CreateAndAddLabelAndTextBoxAndButtonRow2(this AvaloniaEditorPanel panel,
        string label, string textboxValue, string buttonLabel, string? imageResId,
        Action<TextBox, EventArgs>? txteditcommand, Action<Button, EventArgs>? command)
        => panel.CreateAndAddLabelAndTextBoxAndButtonRow(label, textboxValue, buttonLabel, imageResId, txteditcommand, command);

    public static (TextBox, Button, Button) CreateAndAddTextBoxAndTwoButtonsRow(this AvaloniaEditorPanel panel,
        string tbText,
        string buttonLabel, string? imageResId,
        string buttonLabel2, string? imageResId2,
        Action<TextBox, EventArgs>? command0,
        Action<Button, EventArgs>? command, Action<Button, EventArgs>? command2)
    {
        var tb = new TextBox { Width = 250 };
        tb.Text = tbText;
        var btn = new Button { Content = buttonLabel, Width = 100 };
        btn.Classes.Add("panel");
        var btn2 = new Button { Content = buttonLabel2, Width = 100 };
        btn2.Classes.Add("panel");

        if (command0 != null) tb.TextChanged += (s, e) => command0((TextBox)s!, e);
        if (command != null) btn.Click += (s, e) => command((Button)s!, e);
        if (command2 != null) btn2.Click += (s, e) => command2((Button)s!, e);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(tb);
        row.Children.Add(btn);
        row.Children.Add(btn2);
        panel.Children.Add(row);
        return (tb, btn, btn2);
    }

    public static (TextBox, Button, Button, Button) CreateAndAddTextBoxAndThreeButtonsRow(this AvaloniaEditorPanel panel,
        string tbText,
        string bl1, string? ir1, string bl2, string? ir2, string bl3, string? ir3,
        Action<TextBox, EventArgs>? command0,
        Action<Button, EventArgs>? c1, Action<Button, EventArgs>? c2, Action<Button, EventArgs>? c3)
    {
        var tb = new TextBox { Width = 300 };
        tb.Text = tbText;
        var b1 = new Button { Content = bl1, Width = 100 };
        b1.Classes.Add("panel");
        var b2 = new Button { Content = bl2, Width = 100 };
        b2.Classes.Add("panel");
        var b3 = new Button { Content = bl3, Width = 100 };
        b3.Classes.Add("panel");

        if (command0 != null) tb.TextChanged += (s, e) => command0((TextBox)s!, e);
        if (c1 != null) b1.Click += (s, e) => c1((Button)s!, e);
        if (c2 != null) b2.Click += (s, e) => c2((Button)s!, e);
        if (c3 != null) b3.Click += (s, e) => c3((Button)s!, e);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(tb);
        row.Children.Add(b1);
        row.Children.Add(b2);
        row.Children.Add(b3);
        panel.Children.Add(row);
        return (tb, b1, b2, b3);
    }

    public static (TextBox, Button, Button, Button, Button) CreateAndAddTextBoxAndFourButtonsRow(this AvaloniaEditorPanel panel,
        string tbText,
        string bl1, string? ir1, string bl2, string? ir2, string bl3, string? ir3, string bl4, string? ir4,
        Action<TextBox, EventArgs>? command0,
        Action<Button, EventArgs>? c1, Action<Button, EventArgs>? c2, Action<Button, EventArgs>? c3, Action<Button, EventArgs>? c4)
    {
        var tb = new TextBox { Width = 300 };
        tb.Text = tbText;
        var b1 = new Button { Content = bl1, Width = 100 };
        b1.Classes.Add("panel");
        var b2 = new Button { Content = bl2, Width = 100 };
        b2.Classes.Add("panel");
        var b3 = new Button { Content = bl3, Width = 100 };
        b3.Classes.Add("panel");
        var b4 = new Button { Content = bl4, Width = 100 };
        b4.Classes.Add("panel");

        if (command0 != null) tb.TextChanged += (s, e) => command0((TextBox)s!, e);
        if (c1 != null) b1.Click += (s, e) => c1((Button)s!, e);
        if (c2 != null) b2.Click += (s, e) => c2((Button)s!, e);
        if (c3 != null) b3.Click += (s, e) => c3((Button)s!, e);
        if (c4 != null) b4.Click += (s, e) => c4((Button)s!, e);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(tb);
        row.Children.Add(b1);
        row.Children.Add(b2);
        row.Children.Add(b3);
        row.Children.Add(b4);
        panel.Children.Add(row);
        return (tb, b1, b2, b3, b4);
    }

    // -------------------------------------------------------------------------
    // Two / three label rows
    // -------------------------------------------------------------------------

    public static TextBlock CreateAndAddTwoLabelsRow(this AvaloniaEditorPanel panel,
        string text1, string text2)
    {
        var lbl2 = new TextBlock { Text = text2, Width = ControlWidth, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(text1, lbl2, boldLabel: true));
        return lbl2;
    }

    public static TextBlock CreateAndAddTwoLabelsRow2(this AvaloniaEditorPanel panel,
        string text1, string text2)
    {
        var lbl2 = new TextBlock { Text = text2, Width = 350, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(text1, lbl2, boldLabel: true));
        return lbl2;
    }

    public static TextBlock CreateAndAddThreeLabelsRow(this AvaloniaEditorPanel panel,
        string text1, string text2, string text3)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,4,Auto"),
            Margin = new Thickness(0, 1, 0, 1)
        };
        var l1 = new TextBlock { Text = text1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        var l2 = new TextBlock { Text = text2, Width = ControlWidth, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var l3 = new TextBlock { Text = text3, Width = ControlWidth, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(l1, 0);
        Grid.SetColumn(l2, 2);
        Grid.SetColumn(l3, 4);
        row.Children.Add(l1);
        row.Children.Add(l2);
        row.Children.Add(l3);
        panel.Children.Add(row);
        return l2;
    }

    // -------------------------------------------------------------------------
    // ListBox row
    // -------------------------------------------------------------------------

    public static ListBox CreateAndAddListBoxRow(this AvaloniaEditorPanel panel,
        int height, string[] items, Action<ListBox, EventArgs>? command)
    {
        var lb = new ListBox { Height = height, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in items) lb.Items.Add(item);

        if (command != null) lb.SelectionChanged += (s, e) => command((ListBox)s!, e);

        panel.Children.Add(AvaloniaEditorPanel.MakeFullRow(lb));
        return lb;
    }

    // -------------------------------------------------------------------------
    // ColorPicker row
    // -------------------------------------------------------------------------

    public static ColorPicker CreateAndAddColorPickerRow(this AvaloniaEditorPanel panel,
        string label, global::Avalonia.Media.Color currval,
        Action<ColorPicker, EventArgs>? command)
    {
        var cp = new ColorPicker
        {
            Color = currval,
            Width = ControlWidth
        };

        if (command != null) cp.ColorChanged += (s, e) => { command((ColorPicker)s!, e); panel.OnAfterEdit?.Invoke(); };

        panel.Children.Add(AvaloniaEditorPanel.MakeLabelControlRow(label, cp));
        return cp;
    }

    /// <summary>
    /// Legacy overload kept for callers that still pass a TextBox-style command.
    /// Bridges the new ColorPicker into the old TextBox-based API by writing the hex
    /// representation into a synthetic TextBox before invoking the command.
    /// </summary>
    public static ColorPicker CreateAndAddColorPickerRow(this AvaloniaEditorPanel panel,
        string label, global::Avalonia.Media.Color currval,
        Action<TextBox, EventArgs>? command)
    {
        return panel.CreateAndAddColorPickerRow(label, currval, (cp, e) =>
        {
            if (command == null) return;
            var bridge = new TextBox { Text = cp.Color.ToString() };
            command(bridge, e);
        });
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void WireUnitContextMenu(TextBox tb, string label)
    {
        // Skip if the host hasn't registered the conversion delegates yet — Avalonia's default
        // TextBox context menu (Cut/Copy/Paste) remains and the user loses nothing.
        if (!UnitConversionRegistry.IsConfigured) return;

        var unit = ExtractUnit(label);
        if (string.IsNullOrEmpty(unit)) return;

        var alternatives = UnitConversionRegistry.GetAlternatives!(unit);
        if (alternatives == null || alternatives.Count <= 1) return;

        var menu = new ContextMenu();
        var header = new MenuItem
        {
            Header = $"Convert from [{unit}]",
            IsEnabled = false
        };
        menu.Items.Add(header);
        menu.Items.Add(new Separator());

        foreach (var alt in alternatives)
        {
            if (alt == unit) continue;
            var altCopy = alt; // capture
            var item = new MenuItem { Header = $"Copy as [{alt}]" };
            item.Click += (_, _) =>
            {
                if (!double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var current)) return;
                try
                {
                    var converted = UnitConversionRegistry.Convert!(unit, altCopy, current);
                    var formatted = converted.ToString("G6", CultureInfo.InvariantCulture);
                    var topLevel = TopLevel.GetTopLevel(tb);
                    topLevel?.Clipboard?.SetTextAsync($"{formatted} {altCopy}");
                }
                catch { /* conversion failure: swallow to avoid disrupting edits */ }
            };
            menu.Items.Add(item);
        }

        tb.ContextMenu = menu;
    }

    /// <summary>
    /// Pulls the unit symbol out of a label like "Pressure (Pa)" or "Heat Flow (kW)".
    /// Returns an empty string when no parenthesized fragment is present.
    /// </summary>
    private static string ExtractUnit(string label)
    {
        if (string.IsNullOrEmpty(label)) return string.Empty;
        int open = label.LastIndexOf('(');
        int close = label.LastIndexOf(')');
        if (open < 0 || close <= open) return string.Empty;
        return label.Substring(open + 1, close - open - 1).Trim();
    }
}
