using System.IO;
using System.Linq;
using DWSIM.PureCompoundData.Index;

namespace DWSIM.PureCompoundData.CLI.Commands
{
    internal static class StatsCommand
    {
        public static int Run(string? dbPath, TextWriter stdout, TextWriter stderr)
        {
            dbPath ??= AppPaths.DefaultDbPath();
            if (!File.Exists(dbPath))
            {
                stderr.WriteLine($"Index not found: {dbPath}.");
                return ExitCodes.UserError;
            }

            using var index = new PureCompoundIndex(dbPath);
            var s = index.Statistics.Compute();

            stdout.WriteLine($"Database      : {dbPath}");
            stdout.WriteLine($"File size     : {s.DbFileSizeBytes / (1024 * 1024)} MiB");
            stdout.WriteLine($"Records       : {s.TotalRecords}");
            stdout.WriteLine($"Compounds     : {s.UniqueCompounds}");
            stdout.WriteLine();
            stdout.WriteLine("By category:");
            foreach (var kv in s.CategoryBreakdown.OrderByDescending(x => x.Value))
                stdout.WriteLine($"  {kv.Key,-28} {kv.Value}");
            stdout.WriteLine();
            stdout.WriteLine("By provider:");
            foreach (var kv in s.ProviderBreakdown.OrderByDescending(x => x.Value))
                stdout.WriteLine($"  {kv.Key,-28} {kv.Value}");
            return ExitCodes.Success;
        }
    }
}
