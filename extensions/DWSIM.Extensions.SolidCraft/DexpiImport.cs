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
    /// dwsim-dexpi: a separate program (github.com/DanWBR/dwsim-dexpi, it uses pyDEXPI) that the
    /// user installs and the extension only runs. A process model gives a PFD reading with its stream data, a plant model a
    /// P&amp;ID reading. No AI Assistant is needed.
    /// </summary>
    public static class DexpiImport
    {
        public const string Program = "dwsim-dexpi";
        const string Script = "dwsim_dexpi.py";

        /// <summary>Where dwsim-dexpi is downloaded from.</summary>
        public const string DownloadUrl = "https://github.com/DanWBR/dwsim-dexpi/releases/latest";

        /// <summary>The folder under DWSIM's where the program is looked for first.</summary>
        public static string InstallFolder => Path.Combine(Path.GetDirectoryName(typeof(DexpiImport).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory, "dexpi");

        /// <summary>What to tell the user when the program is not installed.</summary>
        public static string MissingText =>
            "DEXPI models are read by " + Program + ", a separate free program that is not installed. Download it from " + DownloadUrl +
            ", unpack it into " + InstallFolder + " (or set DWSIM_DEXPI_HOME to its folder) and read the file again.";

        /// <summary>
        /// The note the import shows in place of the AI Assistant's when the files chosen are DEXPI
        /// models: who reads them, or how to get it when it is missing.
        /// </summary>
        public static string ReaderNote =>
            FindConverter() != null ? "DEXPI models are read by " + Program + ", without the AI Assistant." : MissingText;

        /// <summary>Opens the download page in the browser.</summary>
        public static void OpenDownloadPage()
        {
            try { Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true }); }
            catch (Exception) { }
        }

        /// <summary>The file types read as DEXPI models.</summary>
        public static readonly string[] Extensions = { ".xml" };

        /// <summary>
        /// The files named in the import's file box: one, or several DEXPI files separated by
        /// semicolons (the sheets of a P&amp;ID, and a process model of the same plant).
        /// </summary>
        public static string[] Files(string text) =>
            (text ?? "").Split(';').Select(f => f.Trim().Trim('"')).Where(f => f.Length > 0).ToArray();

        /// <summary>Whether every file named exists.</summary>
        public static bool Exist(string text)
        {
            var files = Files(text);
            return files.Length > 0 && files.All(File.Exists);
        }

        public static bool IsDexpi(string file)
        {
            var files = Files(file);
            return files.Length > 0 && files.All(f => Extensions.Any(x => f.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// The converter: the program or its Python script, in DWSIM_DEXPI_HOME, in extenders/dexpi
        /// under DWSIM's folder, then on the PATH; in each folder, also in the dwsim-dexpi folder the
        /// download unpacks to. Null when it is not there.
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
            }
            folders.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)));
            foreach (var f in folders)
                foreach (var name in new[] { CADCraftSession.Exe(Program), Path.Combine(Program, CADCraftSession.Exe(Program)), Script })
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
                throw new InvalidOperationException(MissingText);
            var script = converter.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
            var output = Path.Combine(Path.GetTempPath(), "dwsim-dexpi-" + Guid.NewGuid().ToString("N") + ".json");
            var args = (script ? Quote(converter) + " " : "") + string.Join(" ", Files(file).Select(Quote)) + " -o " + Quote(output);
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
