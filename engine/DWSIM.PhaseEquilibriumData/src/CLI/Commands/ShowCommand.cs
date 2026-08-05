using System;
using System.IO;
using DWSIM.PhaseEquilibriumData.Index;
using DWSIM.PhaseEquilibriumData.CLI.Output;

namespace DWSIM.PhaseEquilibriumData.CLI.Commands
{
    internal static class ShowCommand
    {
        public static int Run(string? db, string id, string? format, TextWriter stdout, TextWriter stderr)
        {
            db ??= AppPaths.DefaultDbPath();
            if (!File.Exists(db))
            {
                stderr.WriteLine($"Database not found at {db}. Run 'ingest' first.");
                return ExitCodes.UserError;
            }

            OutputFormat fmt;
            try { fmt = FormatterDispatch.Parse(format); }
            catch (ArgumentException ex) { stderr.WriteLine(ex.Message); return ExitCodes.UserError; }

            try
            {
                using var index = new ThermoMLIndex(db);
                var ds = index.Query.GetById(id);
                if (ds == null) { stderr.WriteLine($"No dataset with id {id}."); return ExitCodes.UserError; }
                FormatterDispatch.WriteDataset(fmt, stdout, ds);
                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                stderr.WriteLine($"Show failed: {ex.Message}");
                return ExitCodes.DataError;
            }
        }
    }
}
