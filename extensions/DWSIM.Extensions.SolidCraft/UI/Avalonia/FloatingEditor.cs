//    Copyright 2026 Daniel Wagner O. de Medeiros
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
using Avalonia.Controls;
using Avalonia.Threading;
using DWSIM.Interfaces;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// The cross-platform interface: the flowsheet view hands out the factory of its editors, so the
    /// floating window holds the same editor the view docks (the same tabs, the same live edits), in
    /// a window of its own that stays above the 3D view. One window per object: asking again brings
    /// it forward.
    /// </summary>
    public static partial class EditorHost
    {
        static readonly Dictionary<string, Window> windows = new Dictionary<string, Window>();

        static partial void OpenPlatform(IFlowsheet fs, object flowsheetGui, string id, ref bool done)
        {
            if (windows.TryGetValue(id, out var open))
            {
                open.Activate();
                done = true;
                return;
            }
            var factory = flowsheetGui?.GetType().GetProperty("EditorDescriptorFactory")?.GetValue(flowsheetGui) as Func<string, ObjectEditorDescriptor>;
            if (factory == null) return;
            var descriptor = factory(id);
            if (descriptor == null) return;

            // the view's own container lays the descriptor out in tabs; its assembly is the view's
            Control content = null;
            var container = flowsheetGui.GetType().Assembly.GetType("DWSIM.UI.Desktop.Avalonia.Controls.ObjectEditorContainer");
            if (container != null)
                try { content = Activator.CreateInstance(container, id, descriptor) as Control; }
                catch (Exception) { content = null; }
            content = content ?? descriptor.FullContent ?? descriptor.PropertiesContent;
            if (content == null) return;

            string tag = id;
            if (fs.SimulationObjects.TryGetValue(id, out var obj) && obj.GraphicObject != null) tag = obj.GraphicObject.Tag;
            var window = new Window
            {
                Title = tag + " - DWSIM",
                Width = 560,
                Height = 760,
                Content = content,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            window.Closed += (s, e) => windows.Remove(id);
            windows[id] = window;
            window.Show();
            window.Activate();

            // as the docked editor does: edits recalculate only once the controls have settled
            if (descriptor.PropertiesContent is AvaloniaEditorPanel panel)
                Dispatcher.UIThread.Post(() => Dispatcher.UIThread.Post(panel.ArmAfterEdit, DispatcherPriority.Background), DispatcherPriority.Background);
            done = true;
        }
    }
}
