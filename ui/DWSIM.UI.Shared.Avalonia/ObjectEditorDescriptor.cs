using Avalonia.Controls;

namespace DWSIM.UI.Shared.Avalonia;

/// <summary>
/// Describes which tabs to show and what Avalonia Control to place in each tab of an
/// ObjectEditorContainer. Null content causes that tab to show a placeholder label.
///
/// Lives in DWSIM.UI.Shared.Avalonia (netstandard2.0) so both the .NET 8 Avalonia project
/// and .NET 4.7.2 bootstrap code can create/consume instances via EditorDescriptorFactory.
/// </summary>
public sealed class ObjectEditorDescriptor
{
    public bool ShowConnections { get; set; }
    public bool ShowCustomProperties { get; set; }
    public bool ShowDynamics { get; set; }
    public bool ShowAppearance { get; set; }

    public Control? ConnectionsContent { get; set; }
    public Control? PropertiesContent { get; set; }
    public Control? CustomPropertiesContent { get; set; }
    public Control? DynamicsContent { get; set; }
    public Control? ResultsContent { get; set; }
    public Control? AppearanceContent { get; set; }

    /// <summary>
    /// An editor that lays itself out completely, tab strip included. The material stream
    /// editor uses it to reproduce the WinForms form, which does not share the standard
    /// Connections / Properties / Results arrangement. When set, everything above is ignored.
    /// </summary>
    public Control? FullContent { get; set; }
}
