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
using System.Diagnostics;
using System.Threading;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// One DWSIM 3D window per flowsheet: the link and the window's process. The session ends when
    /// the window closes, and it closes the window when the flowsheet closes (the Windows form is
    /// disposed, or the cross-platform view is unloaded or holds another flowsheet), or when
    /// DWSIM exits, which drops the connection.
    /// </summary>
    public sealed class Viewer3DSession
    {
        static readonly object sessionsLock = new object();
        static readonly List<Viewer3DSession> sessions = new List<Viewer3DSession>();

        readonly IFlowsheet fs;
        readonly object gui;
        Timer watch;
        bool closed, guiWasLoaded;

        public FlowsheetLink Link { get; private set; }
        public Process Window { get; private set; }

        Viewer3DSession(IFlowsheet flowsheet, object flowsheetGui)
        {
            fs = flowsheet;
            gui = flowsheetGui;
        }

        /// <summary>Opens the 3D window of a flowsheet, or says it is open already. Returns null when it could not start.</summary>
        public static Viewer3DSession Open(IFlowsheet fs, object flowsheetGui = null, bool compatibilityRenderer = false)
        {
            if (fs == null) return null;
            lock (sessionsLock)
            {
                var open = sessions.Find(s => s.fs == fs);
                if (open != null && open.Window != null && !open.Window.HasExited)
                {
                    fs.ShowMessage("The 3D view of this flowsheet is open already.", IFlowsheet.MessageType.Information);
                    return open;
                }
                open?.Close();
            }

            var session = new Viewer3DSession(fs, flowsheetGui);
            try
            {
                session.Link = new FlowsheetLink(fs);
                session.Link.Log += text => Report(fs, text, IFlowsheet.MessageType.Warning);
                session.Link.EditHandler = id => EditorHost.Open(fs, flowsheetGui, id);
                session.Link.Start();
                session.Window = ViewerLauncher.Start(session.Link, compatibilityRenderer);
                session.Window.EnableRaisingEvents = true;
                session.Window.Exited += (s, e) => session.Close();
            }
            catch (Exception ex)
            {
                session.Close();
                Report(fs, "Could not open the 3D view: " + ex.Message, IFlowsheet.MessageType.GeneralError);
                return null;
            }

            session.watch = new Timer(_ => session.Check(), null, 2000, 2000);
            lock (sessionsLock) sessions.Add(session);
            Report(fs, "Opened the 3D view of the flowsheet (DWSIM 3D).", IFlowsheet.MessageType.Information);
            return session;
        }

        /// <summary>The session of a flowsheet, when its window is open.</summary>
        public static Viewer3DSession Of(IFlowsheet fs)
        {
            lock (sessionsLock) return sessions.Find(s => s.fs == fs);
        }

        /// <summary>Closes the window and the link.</summary>
        public void Close()
        {
            lock (this)
            {
                if (closed) return;
                closed = true;
            }
            try { watch?.Dispose(); } catch (Exception) { }
            try { Link?.Stop(); } catch (Exception) { }
            try
            {
                // the window quits by itself when the link says bye; a stuck one is stopped
                if (Window != null && !Window.HasExited && !Window.WaitForExit(3000)) Window.Kill();
            }
            catch (Exception) { }
            lock (sessionsLock) sessions.Remove(this);
        }

        void Check()
        {
            if (closed) return;
            try
            {
                if (Window != null && Window.HasExited) { Close(); return; }
                if (FlowsheetClosed()) Close();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Whether the flowsheet's window closed: a disposed Windows form, or a cross-platform view
        /// that was loaded and is not any more, or that now shows another flowsheet.
        /// </summary>
        bool FlowsheetClosed()
        {
            var disposed = fs.GetType().GetProperty("IsDisposed")?.GetValue(fs) as bool?;
            if (disposed == true) return true;
            if (gui == null) return false;
            var type = gui.GetType();
            var shown = type.GetProperty("FlowsheetObject")?.GetValue(gui);
            if (shown != null && !ReferenceEquals(shown, fs)) return true;
            var loaded = type.GetProperty("IsLoaded")?.GetValue(gui) as bool?;
            if (loaded == true) guiWasLoaded = true;
            return loaded == false && guiWasLoaded;
        }

        static void Report(IFlowsheet fs, string text, IFlowsheet.MessageType type)
        {
            try { fs.RunCodeOnUIThread(() => { try { fs.ShowMessage(text, type); } catch (Exception) { } }); }
            catch (Exception) { }
        }
    }
}
