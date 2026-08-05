using System;
using System.IO;
using DWSIM.PhaseEquilibriumData.Core;
using DWSIM.PhaseEquilibriumData.Index;
using DWSIM.PhaseEquilibriumData.CLI.Output;

namespace DWSIM.PhaseEquilibriumData.CLI.Commands
{
    internal static class SearchCommand
    {
        public static int Run(
            string? db,
            string cas1,
            string cas2,
            string? type,
            double? tmin,
            double? tmax,
            double? pmin,
            double? pmax,
            string? format,
            int limit,
            TextWriter stdout,
            TextWriter stderr)
        {
            db ??= AppPaths.DefaultDbPath();
            if (!File.Exists(db))
            {
                stderr.WriteLine($"Database not found at {db}. Run 'ingest' first.");
                return ExitCodes.UserError;
            }

            EquilibriumType? typeFilter = null;
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (!Enum.TryParse<EquilibriumType>(type, ignoreCase: true, out var t))
                {
                    stderr.WriteLine($"Unknown equilibrium type '{type}'.");
                    return ExitCodes.UserError;
                }
                typeFilter = t;
            }

            (double, double)? tRange = tmin.HasValue || tmax.HasValue ? (tmin ?? double.MinValue, tmax ?? double.MaxValue) : ((double, double)?)null;
            (double, double)? pRange = pmin.HasValue || pmax.HasValue ? (pmin ?? double.MinValue, pmax ?? double.MaxValue) : ((double, double)?)null;

            OutputFormat fmt;
            try { fmt = FormatterDispatch.Parse(format); }
            catch (ArgumentException ex) { stderr.WriteLine(ex.Message); return ExitCodes.UserError; }

            try
            {
                using var index = new ThermoMLIndex(db);
                var results = index.Query.SearchBinary(cas1, cas2, typeFilter, tRange, pRange, limit);
                FormatterDispatch.WriteDatasets(fmt, stdout, results);
                if (fmt == OutputFormat.Table) stdout.WriteLine($"\n{results.Count} result(s).");
                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                stderr.WriteLine($"Query failed: {ex.Message}");
                return ExitCodes.DataError;
            }
        }
    }
}
