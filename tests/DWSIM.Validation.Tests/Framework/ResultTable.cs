using System;
using System.Collections.Generic;
using System.Text;

namespace DWSIM.Validation.Tests.Framework
{
    /// <summary>
    /// Accumulates expected/calculated comparisons for a single test case, prints a
    /// formatted table on completion and throws if any row failed its tolerance.
    /// Tolerance is interpreted as relative (fraction of expected) when |expected| &gt; eps,
    /// absolute otherwise.
    /// </summary>
    internal sealed class ResultTable
    {
        private readonly string _title;
        private readonly List<RowEntry> _rows = new List<RowEntry>();
        private const double Eps = 1e-12;

        public ResultTable(string title) { _title = title; }

        public ResultTable Row(string name, double expected, double actual, double relTol, string unit = "")
        {
            _rows.Add(new RowEntry { Name = name, Expected = expected, Actual = actual, RelTol = relTol, Unit = unit, IsRange = false });
            return this;
        }

        public ResultTable RowInRange(string name, double low, double high, double actual, string unit = "")
        {
            _rows.Add(new RowEntry { Name = name, Expected = (low + high) / 2.0, Actual = actual, RelTol = 0, Unit = unit, IsRange = true, Low = low, High = high });
            return this;
        }

        public void PrintAndThrowIfFailed()
        {
            Console.WriteLine(_title);
            const string header = "{0,-32} {1,14} {2,14} {3,10} {4,10} {5,-6} {6,6}";
            Console.WriteLine(string.Format(header, "Case", "Expected", "Computed", "Err %", "Tol %", "Unit", "Stat"));
            int failures = 0;
            var failMsg = new StringBuilder();
            foreach (var r in _rows)
            {
                bool pass;
                double errPct;
                if (r.IsRange)
                {
                    pass = r.Actual >= r.Low && r.Actual <= r.High;
                    errPct = 0;
                    Console.WriteLine(string.Format("{0,-32} {1,14} {2,14:0.######} {3,10} {4,10} {5,-6} {6,6}",
                        r.Name, $"[{r.Low:0.####},{r.High:0.####}]", r.Actual, "-", "-", r.Unit, pass ? "PASS" : "FAIL"));
                }
                else
                {
                    double absErr = r.Actual - r.Expected;
                    double denom = Math.Abs(r.Expected) > Eps ? Math.Abs(r.Expected) : 1.0;
                    errPct = absErr / denom * 100.0;
                    pass = Math.Abs(errPct) <= r.RelTol * 100.0;
                    Console.WriteLine(string.Format(header,
                        r.Name,
                        r.Expected.ToString("0.######"),
                        r.Actual.ToString("0.######"),
                        errPct.ToString("+0.00;-0.00;0.00"),
                        (r.RelTol * 100.0).ToString("0.00"),
                        r.Unit,
                        pass ? "PASS" : "FAIL"));
                }
                if (!pass)
                {
                    failures++;
                    failMsg.AppendLine($"  {r.Name}: expected {r.Expected}, got {r.Actual} (tol {r.RelTol * 100:0.00}%, err {errPct:+0.00;-0.00}%)");
                }
            }
            if (failures > 0)
                throw new Exception($"{failures} row(s) failed in '{_title}':\n{failMsg}");
        }

        private sealed class RowEntry
        {
            public string Name;
            public double Expected;
            public double Actual;
            public double RelTol;
            public string Unit;
            public bool IsRange;
            public double Low, High;
        }
    }
}
