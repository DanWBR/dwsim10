using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DWSIM.PhaseEquilibriumData.Core;
using DWSIM.PhaseEquilibriumData.Index;
using DWSIM.PhaseEquilibriumData.Sources.ThermoML;
using ShellProgressBar;

namespace DWSIM.PhaseEquilibriumData.CLI.Commands
{
    internal static class IngestCommand
    {
        public static int Run(string? archive, string? db, TextWriter stdout, TextWriter stderr)
        {
            archive ??= AppPaths.DefaultArchivePath();
            db ??= AppPaths.DefaultDbPath();

            if (!File.Exists(archive))
            {
                stderr.WriteLine($"Archive not found: {archive}");
                return ExitCodes.UserError;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(db)!);
                using var index = new ThermoMLIndex(db);

                var warnCount = 0;
                var source = new ThermoMLLocalSource(archive, warn: _ => warnCount++);

                var pbarOpts = new ProgressBarOptions
                {
                    ProgressCharacter = '#',
                    CollapseWhenFinished = true
                };
                using var pbar = new ProgressBar(100, "Ingesting…", pbarOpts);
                var lastTick = DateTime.UtcNow;
                long seen = 0;

                IEnumerable<PhaseEquilibriumDataset> Stream()
                {
                    foreach (var ds in source.EnumerateDatasets())
                    {
                        seen++;
                        if ((DateTime.UtcNow - lastTick).TotalMilliseconds > 500)
                        {
                            pbar.Message = $"Ingesting… seen {seen:N0}";
                            lastTick = DateTime.UtcNow;
                        }
                        yield return ds;
                    }
                }

                var result = index.Ingestor.Ingest(Stream());
                pbar.Tick(100);
                stdout.WriteLine($"Done. Seen {result.Seen:N0}, inserted {result.Inserted:N0}, skipped {result.Skipped:N0}, parser warnings {warnCount:N0}.");
                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                stderr.WriteLine($"Ingest failed: {ex.Message}");
                return ExitCodes.DataError;
            }
        }
    }
}
