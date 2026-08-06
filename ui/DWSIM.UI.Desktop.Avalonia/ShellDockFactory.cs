using System.Collections.Generic;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// The document area of the main window: one document tab per open simulation, which is what
/// the Windows UI gets from its MDI client area. Documents can be dragged out into a floating
/// window, which Dock handles on its own.
/// </summary>
public sealed class ShellDockFactory : Factory
{

    /// <summary>The dock every simulation document is added to.</summary>
    public DocumentDock Documents { get; private set; } = null!;

    public IRootDock Root { get; private set; } = null!;

    public override IRootDock CreateLayout()
    {
        Documents = new DocumentDock
        {
            Id = "Documents",
            Title = "Documents",
            IsCollapsable = false,
            CanCreateDocument = false,
            VisibleDockables = CreateList<IDockable>()
        };

        var root = CreateRootDock();
        root.Id = "Root";
        root.Title = "Root";
        root.IsCollapsable = false;
        root.VisibleDockables = CreateList<IDockable>(Documents);
        root.ActiveDockable = Documents;
        root.DefaultDockable = Documents;

        Root = root;

        return root;
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, System.Func<object?>>();
        DockableLocator = new Dictionary<string, System.Func<IDockable?>>();
        HostWindowLocator = new Dictionary<string, System.Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new Dock.Avalonia.Controls.HostWindow()
        };

        base.InitLayout(layout);
    }

}
