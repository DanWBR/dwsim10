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

    /// <summary>
    /// CAD Tools > Record 3D Flyby Video: a video of the plant in the DWSIM 3D window, flying over
    /// it and closing in on each unit in process order with a card of its main values. It is
    /// saved in the folder of the 3D model next to the simulation (MP4 when ffmpeg is on the PATH,
    /// AVI otherwise).
    /// </summary>
    public class Viewer3DFlybyExtender : IExtender, IExtender3
    {
        IFlowsheet Flowsheet;

        public string ID => "7d2e5b91-4c3a-4f08-9e61-2b8a5c0d7f14";

        public string DisplayText => "Record 3D Flyby Video (DWSIM 3D)";

        public byte[] DisplayImage => PlantModelExtender.Icon();

        public int InsertAtPosition => -1;

        public void SetMainWindow(object mainwindow) { }

        public void SetFlowsheet(IFlowsheet form) => Flowsheet = form;

        public void ReleaseResources() => Flowsheet = null;

        public void Run()
        {
            var fs = Flowsheet;
            if (fs == null) return;
            try
            {
                var avi = FlybyRecorder.Record(fs, null, file => Report(fs, file == null
                    ? "The 3D flyby video could not be recorded."
                    : "The 3D flyby video is ready: " + file, file == null ? IFlowsheet.MessageType.GeneralError : IFlowsheet.MessageType.Information));
                Report(fs, "Recording the 3D flyby video (the DWSIM 3D window closes when it is done): " + avi, IFlowsheet.MessageType.Information);
            }
            catch (System.Exception ex)
            {
                Report(fs, "Could not record the 3D flyby video: " + ex.Message, IFlowsheet.MessageType.GeneralError);
            }
        }

        static void Report(IFlowsheet fs, string text, IFlowsheet.MessageType type)
        {
            try { fs.RunCodeOnUIThread(() => { try { fs.ShowMessage(text, type); } catch (System.Exception) { } }); }
            catch (System.Exception) { }
        }
    }
}
