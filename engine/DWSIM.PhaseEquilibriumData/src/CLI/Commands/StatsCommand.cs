using System;
using System.IO;
using DWSIM.PhaseEquilibriumData.Index;
using DWSIM.PhaseEquilibriumData.CLI.Output;

namespace DWSIM.PhaseEquilibriumData.CLI.Commands
{
    internal static class StatsCommand
    {
        public static int Run(string? db, TextWriter stdout, TextWriter stderr)
        {
            db ??= AppPaths.DefaultDbPath();
            if (!File.Exists(db))
            {
                stderr.WriteLine($"Database not found at {db}. Run 'ingest' first.");
                return ExitCodes.UserError;
            }
            try
            {
                using var index = new ThermoMLIndex(db);
                var s = index.Statistics.Compute();
                TableFormatter.WriteStats(stdout, s);
                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                stderr.WriteLine($"Stats failed: {ex.Message}");
                return ExitCodes.DataError;
            }
        }
    }
}
