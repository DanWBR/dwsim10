using System.IO;
using DWSIM.PureCompoundData.Core;
using DWSIM.PureCompoundData.Index;

namespace DWSIM.PureCompoundData.CLI.Commands
{
    internal static class ShowCommand
    {
        public static int Run(string? dbPath, string id, TextWriter stdout, TextWriter stderr)
        {
            dbPath ??= AppPaths.DefaultDbPath();
            if (!File.Exists(dbPath))
            {
                stderr.WriteLine($"Index not found: {dbPath}.");
                return ExitCodes.UserError;
            }

            using var index = new PureCompoundIndex(dbPath);
            var r = index.Query.GetById(id);
            if (r == null)
            {
                stderr.WriteLine($"No record with id {id}.");
                return ExitCodes.DataError;
            }
            stdout.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(r, CoreJson.Options));
            return ExitCodes.Success;
        }
    }
}
