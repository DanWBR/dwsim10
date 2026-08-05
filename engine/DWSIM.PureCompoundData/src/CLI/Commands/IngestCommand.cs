using System;
using System.Collections.Generic;
using System.IO;
using DWSIM.PureCompoundData.Index;
using DWSIM.PureCompoundData.Sources;

namespace DWSIM.PureCompoundData.CLI.Commands
{
    internal static class IngestCommand
    {
        public static int Run(string? archive, string? dbPath, TextWriter stdout, TextWriter stderr)
        {
            archive ??= AppPaths.DefaultArchivePath();
            dbPath ??= AppPaths.DefaultDbPath();

            if (!File.Exists(archive))
            {
                stderr.WriteLine($"Archive not found: {archive}");
                stderr.WriteLine("Run 'dwsim-purecompound download' first.");
                return ExitCodes.UserError;
            }

            stdout.WriteLine($"Ingesting {archive}");
            stdout.WriteLine($"       -> {dbPath}");

            var src = new ThermoMLPureSource(archive, warn: msg => stderr.WriteLine($"warn: {msg}"));

            using var index = new PureCompoundIndex(dbPath);
            const int batchSize = 5000;
            var batch = new List<Core.PureCompoundRecord>(batchSize);
            int total = 0, seen = 0, inserted = 0, skipped = 0;
            foreach (var rec in src.EnumerateRecords())
            {
                batch.Add(rec);
                total++;
                if (batch.Count >= batchSize)
                {
                    var res = index.Ingestor.Ingest(batch);
                    seen += res.Seen; inserted += res.Inserted; skipped += res.Skipped;
                    batch.Clear();
                    stdout.WriteLine($"  progress: {total} records streamed, {inserted} inserted.");
                }
            }
            if (batch.Count > 0)
            {
                var res = index.Ingestor.Ingest(batch);
                seen += res.Seen; inserted += res.Inserted; skipped += res.Skipped;
            }

            stdout.WriteLine($"Done. Seen={seen} Inserted={inserted} Skipped={skipped}.");
            return ExitCodes.Success;
        }
    }
}
