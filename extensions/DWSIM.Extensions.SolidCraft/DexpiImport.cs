// Copyright 2026 Daniel Wagner O. de Medeiros
//
// This file is part of DWSIM.
//
// DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DWSIM. If not, see
// <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// Reads a DEXPI model (DEXPI 2.0 and 2.0.1 XML, Proteus XML) as a drawing reading, through
    /// dwsim-dexpi: a separate program (tools/dwsim-dexpi, it uses pyDEXPI) that the extension
    /// only runs. A process model gives a PFD reading with its stream data, a plant model a
    /// P&amp;ID reading. No AI Assistant is needed.
    /// </summary>
    public static class DexpiImport
    {
        public const string Program = "dwsim-dexpi";
        const string Script = "dwsim_dexpi.py";

        /// <summary>The file types read as DEXPI models.</summary>
        public static readonly string[] Extensions = { ".xml" };

        public static bool IsDexpi(string file) =>
            Extensions.Any(x => (file ?? "").Trim().EndsWith(x, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The converter: the frozen program or its Python script, in DWSIM_DEXPI_HOME, in
        /// extenders/dexpi or tools/dwsim-dexpi under DWSIM's folder, then on the PATH. Null when
        /// it is not there.
        /// </summary>
        public static string FindConverter()
        {
            var folders = new List<string>();
            var env = Environment.GetEnvironmentVariable("DWSIM_DEXPI_HOME");
            if (!string.IsNullOrWhiteSpace(env)) folders.Add(env.Trim());
            var here = Path.GetDirectoryName(typeof(DexpiImport).Assembly.Location) ?? "";
            foreach (var root in new[] { here, AppDomain.CurrentDomain.BaseDirectory }.Where(r => !string.IsNullOrEmpty(r)).Distinct())
            {
                folders.Add(Path.Combine(root, "dexpi"));
                folders.Add(Path.Combine(root, "extenders", "dexpi"));
                folders.Add(Path.Combine(root, "tools", Program));
            }
            folders.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)));
            foreach (var f in folders)
                foreach (var name in new[] { CADCraftSession.Exe(Program), Script })
                    try { if (File.Exists(Path.Combine(f, name))) return Path.Combine(f, name); }
                    catch (ArgumentException) { }
            return null;
        }

        /// <summary>
        /// The Python that runs the script, the first of these that is 3.12 or newer (pyDEXPI needs
        /// it): DWSIM_DEXPI_PYTHON, the ones installed for the user or the computer, the one on
        /// the PATH, the one of DWSIM's settings. The settings' Python comes last: it is the one
        /// DWSIM's scripts run on, often an older distribution. Null when there is none.
        /// </summary>
        static string Python()
        {
            var name = CADCraftSession.Exe("python");
            var candidates = new List<string>();
            var env = Environment.GetEnvironmentVariable("DWSIM_DEXPI_PYTHON");
            if (!string.IsNullOrWhiteSpace(env)) candidates.Add(env.Trim());
            if (CADCraftSession.IsWindows)
            {
                var roots = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python"),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                };
                foreach (var root in roots.Where(Directory.Exists))
                    try
                    {
                        candidates.AddRange(Directory.GetDirectories(root, "Python3*")
                            .OrderByDescending(d => { int v; return int.TryParse(Path.GetFileName(d).Substring(7), out v) ? v : 0; })
                            .Select(d => Path.Combine(d, name)).Where(File.Exists));
                    }
                    catch (Exception) { }
                candidates.Add("python");
            }
            else
            {
                candidates.Add("python3");
                candidates.Add("python");
            }
            var configured = (GlobalSettings.Settings.PythonPath ?? "").Trim();
            if (configured.Length > 0)
                candidates.AddRange(new[] { Path.Combine(configured, name), Path.Combine(configured, "bin", "python3") }.Where(File.Exists));
            return candidates.FirstOrDefault(Suits);
        }

        /// <summary>Whether a Python starts and is 3.12 or newer.</summary>
        static bool Suits(string python)
        {
            try
            {
                using (var p = new Process { StartInfo = Start(python, "-c \"import sys; sys.exit(0 if sys.version_info >= (3, 12) else 3)\"") })
                {
                    p.Start();
                    if (!p.WaitForExit(15000)) { try { p.Kill(); } catch (Exception) { } return false; }
                    return p.ExitCode == 0;
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// A hidden process with its output redirected, without the PYTHONHOME that DWSIM sets for
        /// the Python of its settings (another Python would look for its library there).
        /// </summary>
        static ProcessStartInfo Start(string program, string args)
        {
            var psi = new ProcessStartInfo(program, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables.Remove("PYTHONHOME");
            return psi;
        }

        /// <summary>The reading of a DEXPI file, as the JSON <see cref="DrawingReading.FromJson"/> takes.</summary>
        public static string Read(string file, CancellationToken ct = default, Action<string> step = null)
        {
            var converter = FindConverter();
            if (converter == null)
                throw new InvalidOperationException("DEXPI files are read by " + Program + ", a separate program that was not found. " +
                    "Install it (tools/" + Program + " of DWSIM's sources, with pyDEXPI) and set DWSIM_DEXPI_HOME to its folder.");
            var script = converter.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
            var output = Path.Combine(Path.GetTempPath(), "dwsim-dexpi-" + Guid.NewGuid().ToString("N") + ".json");
            var args = (script ? Quote(converter) + " " : "") + Quote(file) + " -o " + Quote(output);
            var python = script ? Python() : null;
            if (script && python == null)
                throw new InvalidOperationException(Script + " needs Python 3.12 or newer, and none was found. " +
                    "Install it, or set DWSIM_DEXPI_PYTHON to its python executable.");
            var psi = Start(script ? python : converter, args);
            step?.Invoke("Reading the DEXPI model with " + Program);
            var log = new StringBuilder();
            try
            {
                using (var p = new Process { StartInfo = psi })
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
                    try { p.Start(); }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException((script ? "Python could not be started to run " + Script + " (set its folder in DWSIM's Python settings)" :
                            Program + " could not be started") + ": " + ex.Message, ex);
                    }
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    var watch = Stopwatch.StartNew();
                    while (!p.WaitForExit(200))
                        if (ct.IsCancellationRequested || watch.Elapsed.TotalSeconds > TimeoutSeconds)
                        {
                            try { p.Kill(); } catch (Exception) { }
                            ct.ThrowIfCancellationRequested();
                            throw new TimeoutException(Program + " did not finish in " + TimeoutSeconds + " s.");
                        }
                    p.WaitForExit();
                    string text;
                    lock (log) text = log.ToString().Trim();
                    if (p.ExitCode != 0 || !File.Exists(output))
                        throw new InvalidOperationException("The DEXPI model could not be read: " + LastLines(text, 3));
                    var summary = text.Split('\n').FirstOrDefault(l => l.Trim().Length > 0);
                    if (summary != null) step?.Invoke(summary.Trim());
                    return File.ReadAllText(output, Encoding.UTF8);
                }
            }
            finally
            {
                try { if (File.Exists(output)) File.Delete(output); } catch (Exception) { }
            }
        }

        const int TimeoutSeconds = 300;

        static string Quote(string s) => "\"" + s + "\"";

        static string LastLines(string text, int n)
        {
            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            return lines.Count == 0 ? "no message" : string.Join(" ", lines.Skip(Math.Max(0, lines.Count - n)));
        }
    }
}
