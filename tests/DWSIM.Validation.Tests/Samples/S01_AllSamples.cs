using DWSIM.Automation.FluentAPI;
using DWSIM.Validation.Tests.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DWSIM.Validation.Tests.Samples
{
    /// <summary>S01 - Suite-wide smoke test: loads every <c>.dwxml</c> / <c>.dwxmz</c> in the
    /// <c>samples/</c> directory next to the test executable, attempts to solve, and reports
    /// per-file status plus aggregate counts.
    /// Many shipped samples rely on Plus features (Cantera, Reaktoro, dynamic mode, custom scripts,
    /// Excel UOs) that are not available in this headless harness - those are expected to fail with
    /// a recoverable message rather than crash the suite.</summary>
    internal static class S01_AllSamples
    {
        public static void Run()
        {
            var baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var samplesDir = Path.Combine(baseDir, "samples");
            if (!Directory.Exists(samplesDir))
            {
                Console.WriteLine($"  [SKIP] Samples directory not found: {samplesDir}");
                return;
            }

            var files = Directory.GetFiles(samplesDir)
                .Where(f => f.EndsWith(".dwxml", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".dwxmz", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int loaded = 0, loadFail = 0, solved = 0, solveFail = 0;
            double totalSec = 0.0;

            Console.WriteLine($"  Scanning {files.Count} flowsheet sample(s) in {samplesDir}");
            Console.WriteLine();
            Console.WriteLine($"  {"File",-78} {"Status",-12} {"Time",10}  Detail");
            Console.WriteLine($"  {new string('-', 78)} {new string('-', 12)} {new string('-', 10)}  {new string('-', 50)}");

            foreach (var path in files)
            {
                var name = Path.GetFileName(path);
                if (name.Length > 76) name = name.Substring(0, 73) + "...";

                Flowsheet fs = null;
                var sw = Stopwatch.StartNew();
                string status, detail = "";
                double dt;

                try
                {
                    fs = Flowsheet.Load(path);
                    sw.Stop();
                    dt = sw.Elapsed.TotalSeconds;
                    loaded++;

                    sw.Restart();
                    System.Collections.Generic.IReadOnlyList<Exception> errs;
                    try { errs = fs.TrySolve(); }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        solveFail++;
                        dt += sw.Elapsed.TotalSeconds;
                        totalSec += dt;
                        status = "SOLVE-EX";
                        detail = ex.GetType().Name + ": " + Truncate(ex.Message, 80);
                        Console.WriteLine($"  {name,-78} {status,-12} {dt,9:F2}s  {detail}");
                        continue;
                    }
                    sw.Stop();
                    dt += sw.Elapsed.TotalSeconds;
                    totalSec += dt;

                    if (errs.Count == 0)
                    {
                        solved++;
                        status = "OK";
                        detail = $"{fs.Inner.SimulationObjects.Count} objects";
                    }
                    else
                    {
                        solveFail++;
                        status = "SOLVE-FAIL";
                        detail = $"{errs.Count} solver errors; first: " + Truncate(errs[0].Message, 60);
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    dt = sw.Elapsed.TotalSeconds;
                    totalSec += dt;
                    loadFail++;
                    status = "LOAD-FAIL";
                    detail = ex.GetType().Name + ": " + Truncate(ex.Message, 80);
                }

                Console.WriteLine($"  {name,-78} {status,-12} {dt,9:F2}s  {detail}");
            }

            Console.WriteLine();
            Console.WriteLine($"  Totals: {files.Count} sample(s)  |  Loaded: {loaded}  |  Solved: {solved}  "
                            + $"|  Load-fail: {loadFail}  |  Solve-fail: {solveFail}  |  Wall: {totalSec:F1}s");

            // Thresholds: many samples are Plus-only / dynamic / external-script. Loading should mostly
            // work; full solve is harder. Calibrated so the routine passes on a clean free-edition headless build.
            double loadRate = files.Count > 0 ? (double)loaded / files.Count : 0;
            double solveRate = files.Count > 0 ? (double)solved / files.Count : 0;

            new ResultTable("S01 - Sample suite load+solve report")
                .RowInRange("Samples discovered > 20", 20, 1000, files.Count, "")
                .RowInRange("Load success rate >= 60 %", 0.60, 1.0, loadRate, "")
                .RowInRange("Solve success rate >= 20 %", 0.20, 1.0, solveRate, "")
                .PrintAndThrowIfFailed();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace('\n', ' ').Replace('\r', ' ');
            return s.Length <= max ? s : s.Substring(0, max - 3) + "...";
        }
    }
}
