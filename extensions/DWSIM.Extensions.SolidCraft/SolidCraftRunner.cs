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
using System.Text.RegularExpressions;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>Finds SolidCraft and runs its command-line program on a script.</summary>
    public static class SolidCraftRunner
    {
        public class Result
        {
            public bool Success;
            public string Design, Step, Picture, Script;
            public List<string> Skipped = new List<string>();
            public string Error;
        }

        static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        static string Exe(string name) => IsWindows ? name + ".exe" : name;

        /// <summary>
        /// The folder of solidcraft-cli: SOLIDCRAFT_HOME, the usual install folders, then the PATH.
        /// Null when SolidCraft is not installed.
        /// </summary>
        public static string FindInstallFolder()
        {
            var candidates = new List<string>();
            var env = Environment.GetEnvironmentVariable("SOLIDCRAFT_HOME");
            if (!string.IsNullOrWhiteSpace(env)) candidates.Add(env);

            if (IsWindows)
            {
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SolidCraft"));
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SolidCraft"));
            }
            else
            {
                candidates.Add("/Applications/SolidCraft.app/Contents/MacOS");
                candidates.Add("/usr/local/bin");
                candidates.Add("/usr/bin");
                candidates.Add("/opt/solidcraft");
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"));
            }

            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            candidates.AddRange(path.Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)));

            return candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, Exe("solidcraft-cli"))));
        }

        /// <summary>
        /// Runs the script and saves the design, a STEP file and a picture next to it. A command that
        /// fails stops a SolidCraft run, so the failing command is left out and the run is repeated,
        /// at most <paramref name="maxSkips"/> times; the result lists what was left out.
        /// </summary>
        public static Result Run(string folder, List<SolidCraftScript.Command> commands, string outputBase, int maxSkips = 30)
        {
            var cli = Path.Combine(folder, Exe("solidcraft-cli"));
            var res = new Result
            {
                Script = outputBase + ".json",
                Design = outputBase + ".sc",
                Step = outputBase + ".step",
                Picture = outputBase + ".png"
            };

            var cmds = new List<SolidCraftScript.Command>(commands);
            var stepPattern = new Regex(@"step (\d+) \(([^)]*)\):\s*(.*)");

            for (var attempt = 0; ; attempt++)
            {
                File.WriteAllText(res.Script, SolidCraftScript.ToJson(cmds), new UTF8Encoding(false));
                foreach (var f in new[] { res.Design, res.Step }) if (File.Exists(f)) File.Delete(f);

                var exit = Execute(cli, Quote(res.Script) + " --save " + Quote(res.Design) + " --out " + Quote(res.Step) + " --quiet", 600, out var output);
                if (exit == 0 && File.Exists(res.Design))
                {
                    res.Success = true;
                    break;
                }

                var m = stepPattern.Match(output);
                if (!m.Success || attempt >= maxSkips)
                {
                    res.Error = output.Trim();
                    return res;
                }

                var i = int.Parse(m.Groups[1].Value) - 1;
                if (i < 0 || i >= cmds.Count)
                {
                    res.Error = output.Trim();
                    return res;
                }
                var failed = cmds[i];
                res.Skipped.Add(failed.Subject + " (" + failed.Name + "): " + m.Groups[3].Value.Trim());
                if (failed.Subject.StartsWith("line "))
                {
                    // a line is a sketch and a sweep that need each other: leave the whole line out
                    cmds.RemoveAll(c => c.Subject == failed.Subject);
                }
                else
                {
                    cmds.RemoveAt(i);
                    // and the look of the body the failed command would have made
                    if (i < cmds.Count && cmds[i].Name == "appearance.assign" && cmds[i].Subject == failed.Subject) cmds.RemoveAt(i);
                }
            }

            Execute(cli, "snapshot --in " + Quote(res.Design) + " --out " + Quote(res.Picture) + " --width 1600 --height 1000 --view iso", 300, out _, "");
            if (!File.Exists(res.Picture)) res.Picture = null;

            return res;
        }

        /// <summary>Opens the design in the SolidCraft application.</summary>
        public static void OpenInApp(string folder, string design)
        {
            var app = Path.Combine(folder, Exe("solidcraft"));
            if (!File.Exists(app)) return;
            Process.Start(new ProcessStartInfo(app, Quote(design)) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(design) });
        }

        static int Execute(string cli, string args, int timeoutSeconds, out string output, string verb = "run ")
        {
            var psi = new ProcessStartInfo(cli, verb + args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            var sb = new StringBuilder();
            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutSeconds * 1000))
                {
                    try { p.Kill(); } catch { }
                    output = "SolidCraft did not finish in " + timeoutSeconds + " s.";
                    return -1;
                }
                p.WaitForExit();
                output = sb.ToString();
                return p.ExitCode;
            }
        }

        static string Quote(string s) => "\"" + s + "\"";
    }
}
