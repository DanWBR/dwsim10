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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft.Viewer3D
{
    /// <summary>
    /// Records a flyby of the plant as a video: a DWSIM 3D window of its own, in Godot's Movie
    /// Maker mode (every frame drawn at a fixed rate, with the sound, nothing played aloud), flies
    /// over the plant, closes in on each unit in process order with a card of its main values,
    /// and quits; the AVI it writes becomes an MP4 when ffmpeg is on the PATH.
    /// </summary>
    public static class FlybyRecorder
    {
        /// <summary>The size of the video: the size the window starts with (project.godot).</summary>
        public const int Width = 1920, Height = 1080;

        public class Options
        {
            public int Fps = 30;

            /// <summary>
            /// Seconds the camera stays on each unit; null takes the window's own setting (the
            /// "Flyby stop" of its panel, which the user chooses and the window keeps).
            /// </summary>
            public double? Hold;

            /// <summary>The folder of the video; null for the folder of the 3D model next to the simulation.</summary>
            public string Folder;
        }

        /// <summary>
        /// Starts the recording and returns the AVI it writes; <paramref name="done"/> gets the
        /// final file (the MP4 when the conversion worked) once the window has finished, or null
        /// when it failed.
        /// </summary>
        public static string Record(IFlowsheet fs, Options options = null, Action<string> done = null, string executable = null)
        {
            options = options ?? new Options();
            var folder = options.Folder ?? PlantModelExtender.OutputFolder(fs);
            Directory.CreateDirectory(folder);
            var avi = Path.Combine(folder, PlantModelExtender.ModelName(fs) + " - flyby.avi");
            if (File.Exists(avi)) File.Delete(avi);

            var link = new FlowsheetLink(fs);
            link.Start();
            var godot = "--write-movie \"" + avi + "\" --fixed-fps " + options.Fps;
            var window = "--flyby-record" + (options.Hold.HasValue ? " --flyby-hold=" + options.Hold.Value.ToString("0.###", CultureInfo.InvariantCulture) : "");
            Process p;
            try { p = ViewerLauncher.Start(link, false, executable, godot, window); }
            catch (Exception)
            {
                link.Stop();
                throw;
            }

            var watcher = new Thread(() =>
            {
                string result = null;
                try
                {
                    p.WaitForExit(3600 * 1000);
                    if (!p.HasExited) p.Kill();
                    if (File.Exists(avi) && new FileInfo(avi).Length > 0)
                    {
                        result = avi;
                        var mp4 = ToMp4(avi);
                        if (mp4 != null)
                        {
                            try { File.Delete(avi); } catch (Exception) { }
                            result = mp4;
                        }
                    }
                }
                catch (Exception)
                {
                    result = null;
                }
                finally
                {
                    link.Stop();
                }
                done?.Invoke(result);
            }) { IsBackground = true, Name = "DWSIM 3D flyby" };
            watcher.Start();
            return avi;
        }

        /// <summary>ffmpeg on the PATH, or null.</summary>
        public static string FindFfmpeg()
        {
            var names = Environment.OSVersion.Platform == PlatformID.Win32NT ? new[] { "ffmpeg.exe" } : new[] { "ffmpeg" };
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (var n in names)
                {
                    string candidate;
                    try { candidate = Path.Combine(dir.Trim().Trim('"'), n); }
                    catch (ArgumentException) { continue; }
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return null;
        }

        /// <summary>The AVI as an H.264/AAC MP4 next to it, or null when ffmpeg is missing or failed.</summary>
        public static string ToMp4(string avi)
        {
            var ffmpeg = FindFfmpeg();
            if (ffmpeg == null) return null;
            var mp4 = Path.ChangeExtension(avi, ".mp4");
            var info = new ProcessStartInfo(ffmpeg,
                "-y -v error -i \"" + avi + "\" -c:v libx264 -pix_fmt yuv420p -crf 23 -maxrate 6M -bufsize 12M -preset slow -movflags +faststart -c:a aac -b:a 160k \"" + mp4 + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = Process.Start(info))
            {
                if (p == null || !p.WaitForExit(30 * 60 * 1000)) return null;
                return p.ExitCode == 0 && File.Exists(mp4) ? mp4 : null;
            }
        }
    }
}
