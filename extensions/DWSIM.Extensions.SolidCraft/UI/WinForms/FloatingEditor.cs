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
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// The Windows interface: the object opens its own editor form (DisplayEditForm), which this then
    /// floats (DockState.Float) and keeps above the 3D view. The form type comes from the docking
    /// library the application carries, so it is reached by name.
    /// </summary>
    public static partial class EditorHost
    {
        static partial void OpenPlatform(IFlowsheet fs, object flowsheetGui, string id, ref bool done)
        {
            ISimulationObject obj;
            if (!fs.SimulationObjects.TryGetValue(id, out obj) || obj == null) return;
            obj.DisplayEditForm();
            done = true;

            var type = obj.GetType();
            var form = type.GetField("f")?.GetValue(obj) ?? type.GetProperty("f")?.GetValue(obj);
            if (form == null) return;
            try
            {
                var state = form.GetType().GetProperty("DockState");
                if (state != null && state.PropertyType.IsEnum)
                    state.SetValue(form, Enum.Parse(state.PropertyType, "Float"));
                var pane = form.GetType().GetProperty("Pane")?.GetValue(form);
                var floating = pane?.GetType().GetProperty("FloatWindow")?.GetValue(pane) as System.Windows.Forms.Form;
                if (floating != null)
                {
                    floating.TopMost = true;
                    floating.Activate();
                }
            }
            catch (Exception)
            {
                // the editor is open, docked: the user can still float it by hand
            }
        }
    }
}
