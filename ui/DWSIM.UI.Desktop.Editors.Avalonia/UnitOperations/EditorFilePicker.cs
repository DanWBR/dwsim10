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
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;

namespace DWSIM.UI.Desktop.Editors
{
    /// <summary>
    /// The file dialogs the editors open. They go through the storage provider of the window that
    /// hosts the editor, which is the only picker the cross-platform app has: the shared
    /// <c>FilePickerService</c> is only given a factory by the Windows Forms host, so anything
    /// asking it for a picker here used to fail silently (issue #81).
    /// </summary>
    internal static class EditorFilePicker
    {
        /// <summary>
        /// Opens a file dialog owned by the window hosting <paramref name="owner"/> and calls
        /// <paramref name="onPicked"/> with the local path of the chosen file. Nothing is called
        /// when the user cancels. The dialog is asynchronous, so the caller returns before the
        /// user has chosen.
        /// </summary>
        internal static async void Open(Control owner, string title, string[] patterns,
            Action<string> onPicked, IFlowsheet flowsheet = null)
        {
            try
            {
                var top = TopLevel.GetTopLevel(owner);
                if (top == null || top.StorageProvider == null)
                {
                    Report(flowsheet, "The file dialog is not available on this window.");
                    return;
                }

                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false
                };

                if (patterns != null && patterns.Length > 0)
                {
                    options.FileTypeFilter = new List<FilePickerFileType>
                    {
                        new FilePickerFileType("Supported Files") { Patterns = patterns.ToList() }
                    };
                }

                var files = await top.StorageProvider.OpenFilePickerAsync(options);
                if (files == null || files.Count == 0) return;

                var path = files[0].Path == null ? null : files[0].Path.LocalPath;
                if (!string.IsNullOrEmpty(path)) onPicked(path);
            }
            catch (Exception ex)
            {
                Report(flowsheet, "The file dialog could not be opened: " + ex.Message);
            }
        }

        private static void Report(IFlowsheet flowsheet, string message)
        {
            try
            {
                flowsheet?.ShowMessage(message, IFlowsheet.MessageType.GeneralError);
            }
            catch
            {
                // the log is a courtesy; never let it take the editor down
            }
        }
    }
}
