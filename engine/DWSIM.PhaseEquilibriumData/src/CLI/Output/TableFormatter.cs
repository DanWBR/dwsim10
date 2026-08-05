using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.CLI.Output
{
    internal static class TableFormatter
    {
        public static void WriteDatasets(TextWriter writer, IReadOnlyList<PhaseEquilibriumDataset> datasets)
        {
            if (datasets.Count == 0)
            {
                writer.WriteLine("(no results)");
                return;
            }
            writer.WriteLine($"{"Id",-18} {"Type",-16} {"Method",-24} {"Points",6} {"DOI",-40}");
            writer.WriteLine(new string('-', 106));
            foreach (var d in datasets)
            {
                writer.WriteLine(
                    $"{Trim(d.Id, 16),-18} {d.EquilibriumType,-16} {d.Method,-24} {d.Points.Count,6} {Trim(d.Citation.Doi ?? "-", 38),-40}");
            }
        }

        public static void WriteDataset(TextWriter writer, PhaseEquilibriumDataset d)
        {
            writer.WriteLine($"Id:     {d.Id}");
            writer.WriteLine($"Type:   {d.EquilibriumType}");
            writer.WriteLine($"Method: {d.Method}");
            writer.WriteLine($"DOI:    {d.Citation.Doi ?? "(none)"}");
            writer.WriteLine($"Title:  {d.Citation.Title ?? "(none)"}");
            writer.WriteLine($"Components: {string.Join(", ", d.Compounds.Select(c => $"{c.CommonName} [{c.CasNumber}]"))}");
            writer.WriteLine($"Variables: {string.Join(", ", d.VariableNames)}");
            writer.WriteLine($"Points: {d.Points.Count}");
            foreach (var p in d.Points)
                writer.WriteLine("  " + string.Join(", ",
                    p.Values.Select(kv => $"{kv.Key}={kv.Value.ToString("G6", CultureInfo.InvariantCulture)}")));
        }

        public static void WriteStats(TextWriter writer, Index.StatsReport s)
        {
            writer.WriteLine($"Total datasets:    {s.TotalDatasets}");
            writer.WriteLine($"Unique compounds:  {s.UniqueCompounds}");
            writer.WriteLine($"DB file size:      {s.DbFileSizeBytes:N0} bytes");
            writer.WriteLine("Breakdown by equilibrium type:");
            foreach (var kv in s.Breakdown.OrderByDescending(k => k.Value))
                writer.WriteLine($"  {kv.Key,-20} {kv.Value}");
        }

        private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
