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
    /// CAD Tools > 3D View: opens the flowsheet in the DWSIM 3D window, which shows the plant model
    /// and follows every change of the flowsheet. It needs neither SolidCraft nor CADCraft.
    /// </summary>
    public class Viewer3DExtender : IExtender, IExtender3, IExtender6
    {
        IFlowsheet Flowsheet;
        object FlowsheetGui;

        public string ID => "3c8f1d27-6a4e-4b9c-a2d5-7e1f0b9c4d83";

        public string DisplayText => "3D View (DWSIM 3D)";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void SetFlowsheetGUI(object FlowsheetGUI) => FlowsheetGui = FlowsheetGUI;

        public void ReleaseResources()
        {
            Flowsheet = null;
            FlowsheetGui = null;
        }

        public void Run()
        {
            var fs = Flowsheet;
            if (fs == null) return;
            // the GUI object belongs to the flowsheet only when it shows it (the cross-platform view)
            var gui = FlowsheetGui;
            var shown = gui?.GetType().GetProperty("FlowsheetObject")?.GetValue(gui);
            Viewer3DSession.Open(fs, shown == null || ReferenceEquals(shown, fs) ? gui : null);
        }
    }
}
