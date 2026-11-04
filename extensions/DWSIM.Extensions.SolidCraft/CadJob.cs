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
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// A CAD tool running in the background: the step it is on, and the SolidCraft and CADCraft
    /// processes it started, so a cancel can stop them. The exports reach the job of their thread
    /// through <see cref="Current"/>; outside a job (tests, scripts) the calls do nothing.
    /// </summary>
    public sealed class CadJob
    {
        static readonly AsyncLocal<CadJob> current = new AsyncLocal<CadJob>();

        /// <summary>The job the calling code runs in; null outside one.</summary>
        public static CadJob Current => current.Value;

        readonly CancellationTokenSource cancel = new CancellationTokenSource();
        readonly List<Process> processes = new List<Process>();
        volatile string step = "Preparing...", stage;
        volatile string outcome;
        volatile bool finished, failed;
        DateTime started = DateTime.Now, ended;

        /// <summary>What the user chose in the tool's dialog.</summary>
        public readonly CadToolRequest Request;

        /// <summary>The window title (the tool's).</summary>
        public readonly string Title;

        /// <summary>The folder the results go to.</summary>
        public readonly string OutputFolder;

        public CadJob(CadToolRequest request)
        {
            Request = request;
            Title = request.Title;
            try { OutputFolder = request.OutputIsFile ? Path.GetDirectoryName(request.Output) : request.Output; }
            catch (Exception) { OutputFolder = null; }
        }

        public string Step => stage == null || Cancelled ? step : stage + "\n" + step;

        /// <summary>What happened, once the job is over.</summary>
        public string Outcome => outcome;

        public bool Finished => finished;

        public bool Failed => failed;

        public bool Cancelled => cancel.IsCancellationRequested;

        public TimeSpan Elapsed => (finished ? ended : DateTime.Now) - started;

        /// <summary>"42 s", "3 min 05 s", "1 h 02 min".</summary>
        public static string Duration(TimeSpan t)
            => t.TotalHours >= 1 ? string.Format(CultureInfo.InvariantCulture, "{0} h {1:00} min", (int)t.TotalHours, t.Minutes)
             : t.TotalMinutes >= 1 ? string.Format(CultureInfo.InvariantCulture, "{0} min {1:00} s", (int)t.TotalMinutes, t.Seconds)
             : string.Format(CultureInfo.InvariantCulture, "{0} s", (int)t.TotalSeconds);

        /// <summary>A long error (a program's output) cut to what a window can show.</summary>
        public static string Shorten(string text, int length = 500)
        {
            text = (text ?? "").Trim();
            return text.Length <= length ? text : text.Substring(0, length).TrimEnd() + "... (the flowsheet messages have all of it)";
        }

        /// <summary>
        /// Runs <paramref name="work"/> on a background thread; it returns null when it made what it
        /// should, otherwise the reason it did not. The flowsheet messages keep the details.
        /// </summary>
        public void Start(IFlowsheet fs, string what, Func<string> work)
        {
            started = DateTime.Now;
            Task.Run(() =>
            {
                current.Value = this;
                try
                {
                    var error = work();
                    failed = error != null;
                    outcome = error == null ? "Finished in " + Duration(DateTime.Now - started) + ". The flowsheet messages list what was saved." : error;
                }
                catch (OperationCanceledException)
                {
                    outcome = "Cancelled after " + Duration(DateTime.Now - started) + ".";
                    fs.ShowMessage(what + ": cancelled.", IFlowsheet.MessageType.Warning);
                }
                catch (Exception ex)
                {
                    failed = true;
                    outcome = ex.Message;
                    fs.ShowMessage(what + ": " + ex.Message, IFlowsheet.MessageType.GeneralError);
                }
                finally
                {
                    ended = DateTime.Now;
                    finished = true;
                    current.Value = null;
                }
            });
        }

        /// <summary>Stops the job: its programs are closed and it ends at the next step.</summary>
        public void Cancel()
        {
            if (finished || cancel.IsCancellationRequested) return;
            step = "Cancelling...";
            cancel.Cancel();
            lock (processes)
                foreach (var p in processes) Kill(p);
        }

        /// <summary>Shows the step the job of this thread is on.</summary>
        public static void Report(string text)
        {
            var job = Current;
            if (job != null && !job.Cancelled) job.step = text;
        }

        /// <summary>Shows, over the step, the part of a longer job it belongs to ("Part 2 of 6: P&amp;ID"); null clears it.</summary>
        public static void Stage(string text)
        {
            var job = Current;
            if (job == null) return;
            job.stage = text;
            if (text != null && !job.Cancelled) job.step = "Preparing...";
        }

        /// <summary>Ends the job of this thread when it was cancelled.</summary>
        public static void Check() => Current?.cancel.Token.ThrowIfCancellationRequested();

        /// <summary>Keeps a program the job started, to close it on a cancel; closes it at once when the job was cancelled already.</summary>
        internal static void Watch(Process p)
        {
            var job = Current;
            if (job == null || p == null) return;
            lock (job.processes)
            {
                if (job.Cancelled)
                {
                    Kill(p);
                    throw new OperationCanceledException();
                }
                job.processes.Add(p);
            }
        }

        internal static void Forget(Process p)
        {
            var job = Current;
            if (job == null || p == null) return;
            lock (job.processes) job.processes.Remove(p);
        }

        /// <summary>Closes a program and whatever it started.</summary>
        static void Kill(Process p)
        {
            try
            {
                if (p.HasExited) return;
#if NET5_0_OR_GREATER
                p.Kill(true);
#else
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var tk = Process.Start(new ProcessStartInfo("taskkill", "/T /F /PID " + p.Id) { UseShellExecute = false, CreateNoWindow = true }))
                        tk?.WaitForExit(5000);
                }
                if (!p.HasExited) p.Kill();
#endif
            }
            catch (Exception) { }
        }

        /// <summary>Opens the output folder in the file manager.</summary>
        public void OpenFolder()
        {
            if (!string.IsNullOrWhiteSpace(OutputFolder) && Directory.Exists(OutputFolder)) Open(OutputFolder);
        }

        /// <summary>Opens a file or a folder with the program the system has for it.</summary>
        public static void Open(string path)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else
                    Process.Start(new ProcessStartInfo(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open", "\"" + path + "\"") { UseShellExecute = false });
            }
            catch (Exception) { }
        }
    }
}
