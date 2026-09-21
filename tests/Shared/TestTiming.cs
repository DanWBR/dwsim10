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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: DWSIM.Tests.Shared.TimeEveryTest]

namespace DWSIM.Tests.Shared
{
    /// <summary>
    /// Times every test in the assembly it is applied to. Each test prints its own time as it
    /// finishes, and the run ends with the slowest ones ranked, so a suite that takes twenty
    /// minutes says which tests spent them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class TimeEveryTestAttribute : Attribute, ITestAction
    {
        private static readonly ConcurrentDictionary<string, Stopwatch> Running =
            new ConcurrentDictionary<string, Stopwatch>();

        private static readonly ConcurrentBag<KeyValuePair<string, double>> Finished =
            new ConcurrentBag<KeyValuePair<string, double>>();

        public ActionTargets Targets { get { return ActionTargets.Test; } }

        public void BeforeTest(ITest test)
        {
            Running[test.FullName] = Stopwatch.StartNew();
        }

        public void AfterTest(ITest test)
        {
            Stopwatch watch;
            if (!Running.TryRemove(test.FullName, out watch)) return;

            watch.Stop();
            var seconds = watch.Elapsed.TotalSeconds;
            Finished.Add(new KeyValuePair<string, double>(test.Name, seconds));
            Write(string.Format("[time] {0,8:F1} s  {1}", seconds, test.Name));
        }

        /// <summary>The file the run writes its times to.</summary>
        internal static string LogPath()
        {
            try
            {
                return Path.Combine(TestContext.CurrentContext.WorkDirectory, "test-timings.txt");
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>Starts a run with an empty log, so the file is this run and not every run.</summary>
        internal static void Reset()
        {
            try
            {
                var path = LogPath();
                if (path != "" && File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>The slowest tests first, written when the run ends.</summary>
        internal static void Report(int top)
        {
            var all = Finished.ToList();
            if (all.Count == 0) return;

            Write("");
            Write(string.Format("[time] {0} tests, {1:F0} s in total. Slowest:",
                                all.Count, all.Sum(t => t.Value)));

            foreach (var t in all.OrderByDescending(t => t.Value).Take(top))
                Write(string.Format("[time] {0,8:F1} s  {1}", t.Value, t.Key));

            var log = LogPath();
            if (log != "") TestContext.Progress.WriteLine("[time] written to " + log);
        }

        /// <summary>
        /// To the console, which the runner streams live, and to test-timings.txt in the work
        /// directory, because the console output of a long run is easy to lose.
        /// </summary>
        private static void Write(string line)
        {
            try
            {
                TestContext.Progress.WriteLine(line);
            }
            catch (Exception)
            {
            }

            try
            {
                var path = LogPath();
                if (path != "") File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }
    }
}

/// <summary>
/// Outside a namespace on purpose: NUnit applies a SetUpFixture like this one to the whole
/// assembly, which is where the ranking of the run belongs.
/// </summary>
[SetUpFixture]
public class TestTimingReport
{
    [OneTimeSetUp]
    public void StartTimingLog()
    {
        DWSIM.Tests.Shared.TimeEveryTestAttribute.Reset();
    }

    [OneTimeTearDown]
    public void ReportSlowestTests()
    {
        DWSIM.Tests.Shared.TimeEveryTestAttribute.Report(20);
    }
}
