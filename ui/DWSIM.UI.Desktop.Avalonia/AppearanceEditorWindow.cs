//    Edit Appearance window (cross-platform interface)
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
using DWSIM.Drawing.SkiaSharp;
using DWSIM.Drawing.SkiaSharp.Appearance;
using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums;
using DWSIM.UI.Desktop.Editors;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// Edits the appearance of the selected flowsheet objects (colors, line, fill, text, size,
/// orientation, position and the position lock). Every change shows on the flowsheet at once.
/// The whole session is one undo step: the layout is recorded when the window opens.
/// </summary>
public sealed class AppearanceEditorWindow : Window
{
    private readonly AppearanceEditorView _view;

    public AppearanceEditorWindow(IFlowsheet flowsheet, GraphicsSurface? surface, IEnumerable<IGraphicObject> objects, Action? redraw)
    {
        var list = objects.Where(o => o != null && !o.IsConnector).Distinct().ToList();

        try { flowsheet?.RegisterSnapshot(SnapshotType.ObjectLayout); } catch { }

        Title = list.Count == 1 ? "Edit Appearance - " + (list[0].Tag ?? list[0].Name) : $"Edit Appearance - {list.Count} objects";
        Width = 470;
        Height = 640;
        MinWidth = 380;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        IconHelper.ApplyWindowIcon(this);

        _view = new AppearanceEditorView(list, redraw, () => surface?.LockLayout ?? false);

        var intro = new TextBlock
        {
            Text = "Changes show on the flowsheet as you make them. Undo (Ctrl+Z) after closing this window reverts all of them at once.",
            TextWrapping = TextWrapping.Wrap, FontSize = UiScale.Font(11), Opacity = 0.7, Margin = new Thickness(0, 0, 0, 6)
        };

        var reset = new Button { Content = "Reset to defaults", Margin = new Thickness(0, 0, 8, 0) };
        reset.Classes.Add("dialog");
        ToolTip.SetTip(reset, "Put back the colors, line, fill, font and orientation a new object of the same type has. Size, position, text and the position lock are kept.");
        reset.Click += (_, _) =>
        {
            foreach (var o in _view.Objects) AppearanceDescriptors.ResetToDefaults(o);
            _view.Rebuild();
            redraw?.Invoke();
        };

        var close = new Button { Content = "Close", Width = 90, IsCancel = true };
        close.Classes.Add("dialog");
        close.Click += (_, _) => Close();

        var bottom = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        bottom.Children.Add(reset);
        bottom.Children.Add(close);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(intro, global::Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(bottom, global::Avalonia.Controls.Dock.Bottom);
        root.Children.Add(intro);
        root.Children.Add(bottom);
        root.Children.Add(new ScrollViewer { Content = _view, Padding = new Thickness(0, 0, 12, 0) });

        Content = root;

        // the position changes when the view is panned or zoomed while the window is open
        Activated += (_, _) => _view.RefreshPosition();
        Closed += (_, _) => redraw?.Invoke();
    }
}
