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
using Newtonsoft.Json.Linq;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>Finds SolidCraft and runs its command-line program on a script.</summary>
    public static class SolidCraftRunner
    {
        public class Result
        {
            public bool Success;
            public string Design, Step, Picture, Script, Animation;
            public List<string> Skipped = new List<string>();
            public string Error, AnimationError;
        }

        static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        static string Exe(string name) => IsWindows ? name + ".exe" : name;

        static readonly Regex StepPattern = new Regex(@"step (\d+) \(([^)]*)\):\s*(.*)");

        /// <summary>
        /// The folder of solidcraft-cli: the one set in the settings, SOLIDCRAFT_HOME, the usual
        /// install folders, then the PATH. Null when SolidCraft is not installed.
        /// </summary>
        public static string FindInstallFolder()
        {
            var candidates = new List<string>();
            var configured = GlobalSettings.Settings.SolidCraftPath;
            if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured.Trim());
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
        /// Builds the design and its STEP file from the model script, then adds the labels and the
        /// flow animation (<paramref name="extras"/>) in a second run on that design, so they stay
        /// out of the STEP file and a failure there never costs the model, and takes a picture.
        /// </summary>
        public static Result Run(string folder, List<SolidCraftScript.Command> model, List<SolidCraftScript.Command> extras, string outputBase)
        {
            var cli = Path.Combine(folder, Exe("solidcraft-cli"));
            var res = new Result
            {
                Script = outputBase + ".json",
                Design = outputBase + ".sc",
                Step = outputBase + ".step",
                Picture = outputBase + ".png"
            };

            foreach (var f in new[] { res.Design, res.Step }) if (File.Exists(f)) File.Delete(f);
            res.Error = RunStage(cli, model, res.Script, null, res.Design, res.Step, null, res.Skipped);
            res.Success = res.Error == null && File.Exists(res.Design);
            if (!res.Success) return res;

            if (extras != null) extras = WithoutFailedLines(extras, res.Skipped);
            if (extras != null && extras.Count > 0)
            {
                var gif = outputBase + " - flow.gif";
                var animated = outputBase + ".flow.sc";
                if (File.Exists(gif)) File.Delete(gif);
                if (File.Exists(animated)) File.Delete(animated);
                res.AnimationError = RunStage(cli, extras, outputBase + " - labels and flow.json", res.Design, animated, null, FlowAnimation.Prepare, res.Skipped);
                if (res.AnimationError == null && File.Exists(animated))
                {
                    File.Delete(res.Design);
                    File.Move(animated, res.Design);
                    if (File.Exists(gif)) res.Animation = gif;
                }
            }

            Execute(cli, "snapshot --in " + Quote(res.Design) + " --out " + Quote(res.Picture) + " --width 1600 --height 1000 --view iso", 300, out _);
            if (!File.Exists(res.Picture)) res.Picture = null;

            return res;
        }

        /// <summary>
        /// Runs a script, optionally on an existing design. A command that fails stops a SolidCraft
        /// run, so the failing command is left out and the run repeated (a line, a label or a flow
        /// band is left out whole); returns null on success, else the error.
        /// </summary>
        static string RunStage(string cli, List<SolidCraftScript.Command> commands, string script, string input, string save, string step,
            Action<List<SolidCraftScript.Command>> prepare, List<string> skipped, int maxSkips = 40)
        {
            var cmds = new List<SolidCraftScript.Command>(commands);

            for (var attempt = 0; ; attempt++)
            {
                prepare?.Invoke(cmds);
                File.WriteAllText(script, SolidCraftScript.ToJson(FlowAnimation.Clean(cmds)), new UTF8Encoding(false));

                var args = "run " + Quote(script) + (input != null ? " --in " + Quote(input) : "") + " --save " + Quote(save)
                    + (step != null ? " --out " + Quote(step) : "") + " --quiet";
                var exit = Execute(cli, args, 900, out var output);
                if (exit == 0 && File.Exists(save)) return null;

                var m = StepPattern.Match(output);
                var i = m.Success ? int.Parse(m.Groups[1].Value) - 1 : -1;
                if (i < 0 || i >= cmds.Count || attempt >= maxSkips) return output.Trim();

                var failed = cmds[i];
                skipped.Add(failed.Subject + " (" + failed.Name + "): " + m.Groups[3].Value.Trim());
                if (failed.Subject.StartsWith("line ") || failed.Subject.StartsWith("label ") || failed.Subject.StartsWith("flow "))
                {
                    // the commands of a line, a label or a band need each other: leave all of them
                    // out, and their sketches out of the command that hides the sketches
                    var gone = cmds.Where(c => c.Subject == failed.Subject).ToList();
                    cmds.RemoveAll(c => c.Subject == failed.Subject);
                    ForgetSketches(cmds, gone);
                }
                else
                {
                    cmds.RemoveAt(i);
                    // and the look and material of the body the failed command would have made
                    while (i < cmds.Count && (cmds[i].Name == "appearance.assign" || cmds[i].Name == "material.assign") && cmds[i].Subject == failed.Subject)
                        cmds.RemoveAt(i);
                }
            }
        }

        /// <summary>Takes the sketches the removed commands would have made out of the browser.visibility lists.</summary>
        static void ForgetSketches(List<SolidCraftScript.Command> cmds, List<SolidCraftScript.Command> removed)
        {
            var names = new HashSet<string>(removed.Where(c => c.Name == "sketch.create").Select(c => (string)c.Params["name"]));
            if (names.Count == 0) return;
            foreach (var c in cmds.Where(c => c.Name == "browser.visibility"))
            {
                if (!(c.Params["items"] is JArray items)) continue;
                foreach (var item in items.Where(i => names.Contains((string)i)).ToList()) item.Remove();
            }
        }

        /// <summary>Leaves out the labels and flow bands of the lines the model run had to leave out.</summary>
        static List<SolidCraftScript.Command> WithoutFailedLines(List<SolidCraftScript.Command> extras, List<string> skipped)
        {
            var tags = skipped.Where(s => s.StartsWith("line ")).Select(s => s.Substring(5, s.IndexOf(" (", StringComparison.Ordinal) - 5)).ToList();
            if (tags.Count == 0) return extras;
            var kept = extras.Where(c => !tags.Any(t => c.Subject == "label " + t || c.Subject.StartsWith("flow " + t + " "))).ToList();
            ForgetSketches(kept, extras.Except(kept).ToList());
            return kept;
        }

        /// <summary>
        /// Runs read-only commands on a design and returns their results in order; null when the
        /// design cannot be read. The design itself is not changed.
        /// </summary>
        public static JArray Query(string folder, string design, JArray commands)
        {
            var cli = Path.Combine(folder, Exe("solidcraft-cli"));
            var script = Path.Combine(Path.GetTempPath(), "dwsim-solidcraft-query-" + Guid.NewGuid().ToString("N") + ".json");
            var output = Path.ChangeExtension(script, ".out.json");
            try
            {
                File.WriteAllText(script, new JObject { ["commands"] = commands }.ToString(), new UTF8Encoding(false));
                var exit = Execute(cli, "run " + Quote(script) + " --in " + Quote(design) + " --results --json-out " + Quote(output), 300, out _);
                if (exit != 0 || !File.Exists(output)) return null;
                return JToken.Parse(File.ReadAllText(output)) as JArray;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                try { File.Delete(script); } catch { }
                try { File.Delete(output); } catch { }
            }
        }

        /// <summary>Opens the design in the SolidCraft application.</summary>
        public static void OpenInApp(string folder, string design)
        {
            var app = Path.Combine(folder, Exe("solidcraft"));
            if (!File.Exists(app)) return;
            // through the shell, so the application inherits none of DWSIM's handles (its console
            // output among them) and lives on its own after DWSIM closes
            Process.Start(new ProcessStartInfo(app, Quote(design)) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(design) });
        }

        static int Execute(string cli, string args, int timeoutSeconds, out string output)
        {
            var psi = new ProcessStartInfo(cli, args)
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
