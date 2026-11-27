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
using System.IO;
using System.Runtime.InteropServices;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// Finds and starts the DWSIM 3D window, a Godot program shipped in the viewer3d folder next to
    /// DWSIM (one build per operating system and processor). The window gets the port and the token
    /// of a <see cref="FlowsheetLink"/> on its command line and connects back.
    /// </summary>
    public static class ViewerLauncher
    {
        /// <summary>An environment variable that points at another build of the window (a development one).</summary>
        public const string PathVariable = "DWSIM_VIEWER3D";

        public const string FolderName = "viewer3d";

        /// <summary>The window's executable, or null when this installation has none.</summary>
        public static string FindExecutable()
        {
            foreach (var candidate in Candidates())
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate)) return candidate;
            return null;
        }

        /// <summary>The places the window is looked for, in order.</summary>
        public static IEnumerable<string> Candidates()
        {
            var custom = Environment.GetEnvironmentVariable(PathVariable);
            if (!string.IsNullOrWhiteSpace(custom)) yield return custom.Trim().Trim('"');

            var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                yield return Path.Combine(root, "DWSIM3D.exe");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                yield return Path.Combine(root, "DWSIM 3D.app", "Contents", "MacOS", "DWSIM 3D");
                // inside a bundle the application folder is Contents/MacOS; the window may sit in Resources
                yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", FolderName, "DWSIM 3D.app", "Contents", "MacOS", "DWSIM 3D");
            }
            else
            {
                var arm = RuntimeInformation.OSArchitecture == Architecture.Arm64;
                yield return Path.Combine(root, arm ? "DWSIM3D.arm64" : "DWSIM3D.x86_64");
            }
        }

        /// <summary>
        /// The command line of the window: Godot's own options first, then, after "--", the ones the
        /// window reads (port, token and the title).
        /// </summary>
        public static string Arguments(FlowsheetLink link, bool compatibilityRenderer = false)
        {
            var godot = compatibilityRenderer ? "--rendering-method gl_compatibility " : "";
            return godot + "-- --dwsim-port=" + link.Port + " --dwsim-token=" + link.Token;
        }

        /// <summary>
        /// Starts the window for a link that is listening. The compatibility renderer (OpenGL 3.3)
        /// is for computers without Vulkan, Direct3D 12 or Metal (old graphics, virtual machines).
        /// </summary>
        public static Process Start(FlowsheetLink link, bool compatibilityRenderer = false, string executable = null)
        {
            if (link == null) throw new ArgumentNullException(nameof(link));
            if (link.Port == 0) throw new InvalidOperationException("the link must be started before the window");
            var exe = executable ?? FindExecutable();
            if (exe == null)
                throw new FileNotFoundException("This installation of DWSIM does not include the 3D window (" + FolderName + " folder).");
            var info = new ProcessStartInfo(exe, Arguments(link, compatibilityRenderer))
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(exe)) ?? ""
            };
            return Process.Start(info);
        }
    }
}
