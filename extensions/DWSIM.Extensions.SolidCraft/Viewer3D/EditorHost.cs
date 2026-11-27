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

using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// Opens the editor of an object, asked from the DWSIM 3D window, in a floating window of DWSIM
    /// that stays above the 3D view. Each interface brings its own way: the cross-platform one builds
    /// the editor it docks and puts it in a window of its own (UI/Avalonia/FloatingEditor.cs), the
    /// Windows one opens the object's editor form and floats it (UI/WinForms/FloatingEditor.cs).
    /// </summary>
    public static partial class EditorHost
    {
        /// <summary>Opens the editor; false when this interface cannot show it.</summary>
        public static bool Open(IFlowsheet fs, object flowsheetGui, string id)
        {
            var done = false;
            OpenPlatform(fs, flowsheetGui, id, ref done);
            return done;
        }

        static partial void OpenPlatform(IFlowsheet fs, object flowsheetGui, string id, ref bool done);
    }
}
