using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Serializer;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Reads and writes the Avalonia panel layout in the simulation file, through the Dock
/// serializer, so the whole dock tree round-trips: split proportions, tab order, which panels
/// were moved to another dock and which ones were left floating.
///
/// It lives in its own element because the WinForms UI already stores a &lt;PanelLayout&gt; in
/// the same file, in the WeifenLuo format, which Dock.Avalonia cannot read. Both sections
/// coexist and each interface reads the one it understands.
///
/// The serializer round-trips the tree, not the live controls: a restored layout comes back with
/// empty panels until <see cref="ReattachContent"/> puts them back by dockable id.
/// </summary>
public static class DockLayoutState
{

    public const string SectionName = "PanelLayoutAvalonia";

    private static readonly DockSerializer Serializer = new(typeof(List<>));

    /// <summary>Writes the current arrangement into the simulation document.</summary>
    public static void Save(XDocument xdoc, IDock? layout)
    {
        var simroot = xdoc.Element("DWSIM_Simulation_Data");
        if (simroot == null || layout == null) return;

        simroot.Element(SectionName)?.Remove();

        var json = Serializer.Serialize(layout);
        if (string.IsNullOrEmpty(json)) return;

        simroot.Add(new XElement(SectionName, new XCData(json)));
    }

    /// <summary>
    /// Rebuilds the saved arrangement, or returns null when the file has no Avalonia layout or
    /// the stored one cannot be read, in which case the caller keeps the default layout.
    /// </summary>
    public static IRootDock? Load(XDocument xdoc)
    {
        var section = xdoc.Element("DWSIM_Simulation_Data")?.Element(SectionName);
        if (section == null) return null;

        var json = section.Value;
        if (string.IsNullOrWhiteSpace(json)) return null;

        return Serializer.Deserialize<Dock.Model.Avalonia.Controls.RootDock>(json);
    }

    /// <summary>
    /// Puts the live panels back into a restored layout. Dockables whose id is not in the map
    /// keep whatever the serializer gave them, which is how a file written by an older layout
    /// still opens.
    /// </summary>
    public static void ReattachContent(IDockable? node, IReadOnlyDictionary<string, Control> contentById)
    {
        if (node == null) return;

        if (!string.IsNullOrEmpty(node.Id) && contentById.TryGetValue(node.Id!, out var content))
        {
            switch (node)
            {
                case Dock.Model.Avalonia.Controls.Tool tool:
                    tool.Content = content;
                    break;
                case Dock.Model.Avalonia.Controls.Document document:
                    document.Content = content;
                    break;
            }
        }

        if (node is IDock dock && dock.VisibleDockables != null)
        {
            foreach (var child in dock.VisibleDockables) ReattachContent(child, contentById);
        }
    }

}
