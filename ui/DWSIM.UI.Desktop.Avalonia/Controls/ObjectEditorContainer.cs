using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia.Controls;

/// <summary>
/// Avalonia equivalent of ObjectEditorContainer (EditorContainer.cs).
/// Wraps a standard TabControl with DWSIM editor tabs for a simulation object:
///   - Connections, Properties, Custom Properties, Dynamics, Results, Appearance.
///
/// Uses composition (ContentControl wrapping a TabControl) instead of
/// inheritance to avoid Avalonia 11 style/template resolution issues that
/// cause a TabControl subclass created in code to render with zero size.
/// </summary>
public class ObjectEditorContainer : ContentControl
{
    /// <summary>Display name shown on the host EditorHolder tab.</summary>
    public string ObjectName { get; }

    /// <summary>Raised when the user closes this editor via the close button.</summary>
    public event EventHandler? EditorCloseRequested;

    internal void RaiseCloseRequested() =>
        EditorCloseRequested?.Invoke(this, EventArgs.Empty);

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public ObjectEditorContainer(string objectName, ObjectEditorDescriptor descriptor)
    {
        ObjectName = objectName;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        BuildTabs(descriptor);
    }

    // -------------------------------------------------------------------------
    // Tab building
    // -------------------------------------------------------------------------

    private void BuildTabs(ObjectEditorDescriptor d)
    {
        // an editor that brings its own layout replaces the standard tabs entirely
        if (d.FullContent != null)
        {
            Content = d.FullContent;
            return;
        }

        var tc = new TabControl();

        if (d.ShowConnections)
            tc.Items.Add(MakeTab("Connections", d.ConnectionsContent ?? MakePlaceholder("Connections editor")));

        tc.Items.Add(MakeTab("Properties", d.PropertiesContent ?? MakePlaceholder("Properties editor")));

        if (d.ShowCustomProperties)
            tc.Items.Add(MakeTab("Custom Properties", d.CustomPropertiesContent ?? MakePlaceholder("Custom properties")));

        if (d.ShowDynamics)
            tc.Items.Add(MakeTab("Dynamics", d.DynamicsContent ?? MakePlaceholder("Dynamics parameters")));

        tc.Items.Add(MakeTab("Results", d.ResultsContent ?? MakePlaceholder("Results")));

        if (d.ShowAppearance)
            tc.Items.Add(MakeTab("Appearance", d.AppearanceContent ?? MakePlaceholder("Appearance settings")));

        if (d.ShowUtilities && d.UtilitiesContent != null)
            tc.Items.Add(MakeTab("Utilities", d.UtilitiesContent));

        if (tc.Items.Count > 0) tc.SelectedIndex = 0;

        Content = tc;
    }

    private static TabItem MakeTab(string header, Control content)
    {
        var sv = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        return new TabItem
        {
            Header = header,
            Content = sv
        };
    }

    private static Control MakePlaceholder(string label)
    {
        return new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            Margin = new Thickness(5)
        };
    }

    // -------------------------------------------------------------------------
    // Convenience factory for Phase 2 (placeholder content)
    // -------------------------------------------------------------------------

    public static ObjectEditorContainer CreatePlaceholder(string objectName, string objectTypeName)
    {
        var d = new ObjectEditorDescriptor
        {
            ShowConnections = true,
            ShowCustomProperties = true,
            ShowDynamics = true,
            ShowAppearance = true,
            PropertiesContent = BuildPropertiesPlaceholder(objectName, objectTypeName)
        };
        return new ObjectEditorContainer(objectName, d);
    }

    private static Control BuildPropertiesPlaceholder(string name, string typeName)
    {
        var panel = new AvaloniaEditorPanel();
        panel.CreateAndAddLabelRow(typeName);
        panel.CreateAndAddTwoLabelsRow("Object name", name);
        panel.CreateAndAddDescriptionRow(
            "Full editor will be available in Phase 3. " +
            "Implement PopulateEditorPanel with AvaloniaEditorPanel as the container.");
        return panel;
    }
}

// -------------------------------------------------------------------------
// EditorHolder — hosts the current ObjectEditorContainer
// -------------------------------------------------------------------------

/// <summary>
/// The panel on the left side of FlowsheetWindow that shows the editor for
/// the currently selected simulation object.
///
/// Uses a Grid(Auto,*) layout (header bar + content) instead of a nested
/// TabControl to avoid Avalonia 11 layout issues with nested TabControls.
/// The ObjectEditorContainer itself is a TabControl with the standard
/// Connections / Properties / Results / Appearance tabs.
/// </summary>
public class EditorHolder : Grid
{
    private string? _currentName;
    private readonly TextBlock _headerLabel;
    private ObjectEditorContainer? _currentEditor;

    /// <summary>Raised when the user clicks the close button.</summary>
    public event EventHandler? CloseRequested;

    public EditorHolder()
    {
        RowDefinitions = new RowDefinitions("Auto,*");

        // --- Row 0: Header bar (object name + close button) ---
        _headerLabel = new TextBlock
        {
            Text = "",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(6, 0, 0, 0)
        };

        var closeBtn = new Button
        {
            Content = "✕",
            Width = 20,
            Height = 20,
            Padding = new Thickness(0),
            FontSize = DWSIM.UI.Shared.Avalonia.UiScale.Font(10),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0)
        };
        closeBtn.Classes.Add("compact");
        closeBtn.Click += (_, _) =>
        {
            CloseAll();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 2, 0, 2)
        };
        Grid.SetColumn(_headerLabel, 0);
        Grid.SetColumn(closeBtn, 1);
        headerGrid.Children.Add(_headerLabel);
        headerGrid.Children.Add(closeBtn);

        var headerBar = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            Child = headerGrid
        };
        SetRow(headerBar, 0);
        Children.Add(headerBar);

        // Row 1 is left empty; OpenEditor places the ObjectEditorContainer
        // directly into the Grid so there is no intermediate container that
        // could swallow the TabControl's size.
    }

    public void OpenEditor(ObjectEditorContainer editor)
    {
        // Remove the previous editor from the Grid, if any
        if (_currentEditor != null)
            Children.Remove(_currentEditor);

        _currentName = editor.ObjectName;
        _headerLabel.Text = editor.ObjectName;

        // Place the TabControl directly into row 1
        SetRow(editor, 1);
        Children.Add(editor);
        _currentEditor = editor;
    }

    /// <summary>
    /// Sets the display text in the header bar (object Tag / display name).
    /// </summary>
    public void SetDisplayName(string displayName)
    {
        _headerLabel.Text = displayName;
    }

    public void CloseEditor(string objectName)
    {
        if (_currentName != objectName) return;
        CloseAll();
    }

    public void CloseAll()
    {
        _currentName = null;
        _headerLabel.Text = "";
        if (_currentEditor != null)
        {
            Children.Remove(_currentEditor);
            _currentEditor = null;
        }
    }
}
