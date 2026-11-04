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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// A headless CADCraft drawing session (cadcraft-cli mcp, a separate program the user installs):
    /// commands go one by one, so one that fails is reported and the drawing goes on without it.
    /// </summary>
    public sealed class CADCraftSession : IDisposable
    {
        static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        internal static string Exe(string name) => IsWindows ? name + ".exe" : name;

        /// <summary>
        /// The folder of cadcraft-cli: the one chosen in a CAD Tools dialog, CADCRAFT_HOME, the usual
        /// install folders (CADCraft comes on its own or in the ArtCraft suite, next to SolidCraft),
        /// then the PATH. Null when CADCraft is not installed.
        /// </summary>
        public static string FindInstallFolder()
        {
            var candidates = new List<string>();
            var configured = ExportOptions.Load().CADCraftPath;
            if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured.Trim());
            var env = Environment.GetEnvironmentVariable("CADCRAFT_HOME");
            if (!string.IsNullOrWhiteSpace(env)) candidates.Add(env);
            var solidcraft = GlobalSettings.Settings.SolidCraftPath;
            if (!string.IsNullOrWhiteSpace(solidcraft))
            {
                var parent = Path.GetDirectoryName(solidcraft.Trim().TrimEnd('\\', '/'));
                if (parent != null) candidates.Add(Path.Combine(parent, "CADCraft"));
            }

            if (IsWindows)
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                candidates.Add(Path.Combine(pf, "ArtCraft", "CADCraft"));
                candidates.Add(Path.Combine(pf, "CADCraft"));
                candidates.Add(Path.Combine(local, "Programs", "ArtCraft", "CADCraft"));
                candidates.Add(Path.Combine(local, "Programs", "CADCraft"));
            }
            else
            {
                candidates.Add("/Applications/CADCraft.app/Contents/MacOS");
                candidates.Add("/usr/local/bin");
                candidates.Add("/usr/bin");
                candidates.Add("/opt/cadcraft");
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"));
            }

            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            candidates.AddRange(path.Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)));

            return candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, Exe("cadcraft-cli"))));
        }

        /// <summary>Opens a drawing in the CADCraft application, on its own.</summary>
        public static void OpenInApp(string folder, string drawing)
        {
            var app = Path.Combine(folder, Exe("cadcraft"));
            if (!File.Exists(app)) return;
            Process.Start(new ProcessStartInfo(app, "\"" + drawing + "\"") { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(drawing) });
        }

        readonly Process process;
        int id;

        /// <summary>The commands that failed, with the reason.</summary>
        public readonly List<string> Failed = new List<string>();

        public CADCraftSession(string folder)
        {
            var psi = new ProcessStartInfo(Path.Combine(folder, Exe("cadcraft-cli")), "mcp")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetTempPath()
            };
            process = Process.Start(psi);
            process.ErrorDataReceived += (s, e) => { };
            process.BeginErrorReadLine();
            Call("initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "DWSIM", ["version"] = "1" }
            }, 30);
            Send(new JObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });
        }

        void Send(JObject msg)
        {
            // UTF-8 without a byte order mark, whatever the console code page
            var bytes = new UTF8Encoding(false).GetBytes(msg.ToString(Newtonsoft.Json.Formatting.None) + "\n");
            process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
            process.StandardInput.BaseStream.Flush();
        }

        JObject Call(string method, JObject prms, int timeoutSeconds)
        {
            id++;
            Send(new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = prms });
            while (true)
            {
                var read = Task.Run(() => process.StandardOutput.ReadLine());
                if (!read.Wait(timeoutSeconds * 1000) || read.Result == null) throw new TimeoutException("CADCraft did not answer");
                var answer = JObject.Parse(read.Result);
                if (answer["id"]?.Value<int>() == id) return answer;
            }
        }

        /// <summary>Calls one tool; null (and the reason in <see cref="Failed"/>) when it failed.</summary>
        public string Tool(string name, JObject args, string what, int timeoutSeconds = 120)
        {
            var r = Call("tools/call", new JObject { ["name"] = name, ["arguments"] = args }, timeoutSeconds);
            var text = string.Join("\n", (r["result"]?["content"] as JArray ?? new JArray()).Select(c => c["text"]?.ToString() ?? ""));
            if (r["error"] != null || r["result"]?["isError"]?.Value<bool>() == true)
            {
                Failed.Add(what + ": " + (r["error"]?["message"]?.ToString() ?? text).Trim());
                return null;
            }
            return text;
        }

        /// <summary>Runs one engine command; null when it failed.</summary>
        public string Execute(string command, JObject prms, int timeoutSeconds = 120)
            => Tool("execute", new JObject { ["command"] = command, ["params"] = prms }, command, timeoutSeconds);

        public void Dispose()
        {
            try { process.StandardInput.Close(); } catch { }
            try { if (!process.WaitForExit(5000)) process.Kill(); } catch { }
            process.Dispose();
        }
    }
}
