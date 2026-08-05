using System.IO;
using DWSIM.PureCompoundData.Builder;
using DWSIM.PureCompoundData.Core;
using DWSIM.PureCompoundData.Index;
using Newtonsoft.Json;

namespace DWSIM.PureCompoundData.CLI.Commands
{
    internal static class BuildCommand
    {
        public static int Run(string? dbPath, string cas, string? outPath, TextWriter stdout, TextWriter stderr)
        {
            dbPath ??= AppPaths.DefaultDbPath();
            if (!File.Exists(dbPath))
            {
                stderr.WriteLine($"Index not found: {dbPath}.");
                return ExitCodes.UserError;
            }

            using var index = new PureCompoundIndex(dbPath);
            var records = index.Query.GetAllForCompound(cas);
            if (records.Count == 0)
            {
                stderr.WriteLine($"No records for CAS {cas}.");
                return ExitCodes.DataError;
            }

            var built = new ConstantPropertiesBuilder().Build(records);
            var json = JsonConvert.SerializeObject(built, Formatting.Indented, CoreJson.Options);

            if (!string.IsNullOrWhiteSpace(outPath))
            {
                File.WriteAllText(outPath!, json);
                stdout.WriteLine($"Wrote {outPath} ({records.Count} source record(s) merged).");
            }
            else
            {
                stdout.WriteLine(json);
            }
            return ExitCodes.Success;
        }
    }
}
