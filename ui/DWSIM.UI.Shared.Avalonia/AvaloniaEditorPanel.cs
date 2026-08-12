using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// Avalonia equivalent of Eto.Forms.DynamicLayout for DWSIM unit-operation editors.
/// Targets netstandard2.0 so both .NET Framework 4.7.2 and .NET 8 projects can reference it.
///
/// PopulateEditorPanel implementations receive this as the container object:
///   If TypeOf container Is AvaloniaEditorPanel Then
///       Dim panel = DirectCast(container, AvaloniaEditorPanel)
///       panel.CreateAndAddTextBoxRow(...)
///   End If
/// </summary>
public class AvaloniaEditorPanel : StackPanel
{
    public const int DefaultControlWidth = 230;
    public const double RowSpacing = 6.0;

    private Action? _onAfterEdit;
    private bool _armed;

    /// <summary>
    /// Invoked by CreateAndAdd* helpers after the user's per-control callback runs.
    /// Editors set this once (typically to RequestCalculation + UpdateInterface for simobj)
    /// instead of duplicating the call in every lambda.
    ///
    /// The getter returns null until <see cref="ArmAfterEdit"/> is called, so all
    /// <c>panel.OnAfterEdit?.Invoke()</c> calls are no-ops during initial population
    /// and visual-tree attachment (when Avalonia fires deferred TextChanged/SelectionChanged).
    /// </summary>
    public Action? OnAfterEdit
    {
        get => _armed ? _onAfterEdit : null;
        set => _onAfterEdit = value;
    }

    /// <summary>
    /// Enables the OnAfterEdit callback. Call this AFTER the panel has entered the
    /// visual tree and all deferred layout/property-change events have settled.
    /// Before this call, OnAfterEdit always returns null regardless of what was set.
    /// </summary>
    public void ArmAfterEdit() => _armed = true;

    public AvaloniaEditorPanel()
    {
        Orientation = Orientation.Vertical;
        Spacing = RowSpacing;
        Margin = new Thickness(2);
        TextElement.SetFontSize(this, 11);

        // Style class used by App.axaml selectors (StackPanel.editorPanel TextBox, etc.)
        // to enforce compact control sizing inside editor panels.
        Classes.Add("editorPanel");
    }

    /// <summary>Creates a two-column row: label on the left, control fixed-width on the right.</summary>
    public static Grid MakeLabelControlRow(string label, Control control, bool boldLabel = false)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 1, 0, 1)
        };

        var lbl = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = boldLabel ? FontWeight.Bold : FontWeight.Normal,
            Margin = new Thickness(0, 0, 6, 0),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);

        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    /// <summary>Creates a full-width single-column row.</summary>
    public static Grid MakeFullRow(Control control)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        grid.Children.Add(control);
        return grid;
    }
}
