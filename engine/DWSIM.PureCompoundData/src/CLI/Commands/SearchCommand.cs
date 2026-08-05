using System;
using System.IO;
using System.Linq;
using DWSIM.PureCompoundData.Core;
using DWSIM.PureCompoundData.Index;

namespace DWSIM.PureCompoundData.CLI.Commands
{
    internal static class SearchCommand
    {
        public static int Run(string? dbPath, string? cas, string? name, string? inchiKey,
            string? category, double? tmin, double? tmax, string? format, int limit,
            TextWriter stdout, TextWriter stderr)
        {
            dbPath ??= AppPaths.DefaultDbPath();
            if (!File.Exists(dbPath))
            {
                stderr.WriteLine($"Index not found: {dbPath}. Run 'ingest' first.");
                return ExitCodes.UserError;
            }

            using var index = new PureCompoundIndex(dbPath);
            var q = index.CreateQuery().Take(limit);

            if (!string.IsNullOrWhiteSpace(cas)) q = q.ForCompound(cas!);
            else if (!string.IsNullOrWhiteSpace(inchiKey)) q = q.ForCompoundByInChIKey(inchiKey!);
            else if (!string.IsNullOrWhiteSpace(name)) q = q.ForCompoundByName(name!);
            else
            {
                stderr.WriteLine("One of --cas, --name, or --inchikey is required.");
                return ExitCodes.UserError;
            }

            if (!string.IsNullOrWhiteSpace(category) &&
                Enum.TryParse<PropertyCategory>(category, ignoreCase: true, out var cat))
                q = q.OfCategory(cat);
            if (tmin.HasValue && tmax.HasValue) q = q.InTemperatureRangeK(tmin.Value, tmax.Value);

            var results = q.Execute();

            if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            {
                stdout.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(results, CoreJson.Options));
            }
            else
            {
                stdout.WriteLine($"{results.Count} record(s).");
                stdout.WriteLine($"{"Id",-14}  {"Category",-22}  {"Prop",-8}  {"Pts",4}  {"T range (K)",-14}  {"Source"}");
                foreach (var r in results)
                {
                    var idShort = r.Id.Length > 12 ? r.Id.Substring(0, 12) + ".." : r.Id;
                    string range = r.TMin.HasValue && r.TMax.HasValue
                        ? $"{r.TMin:F1}-{r.TMax:F1}" : "-";
                    stdout.WriteLine($"{idShort,-14}  {r.Category,-22}  {r.Property,-8}  {r.Points?.Count ?? 0,4}  {range,-14}  {r.SourceProvider}");
                }
            }
            return ExitCodes.Success;
        }
    }
}
