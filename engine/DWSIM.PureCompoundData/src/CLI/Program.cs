using System;
using System.CommandLine;
using System.Threading.Tasks;
using DWSIM.PureCompoundData.CLI.Commands;

namespace DWSIM.PureCompoundData.CLI
{
    internal static class Program
    {
        public static Task<int> Main(string[] args)
        {
            var stdout = Console.Out;
            var stderr = Console.Error;

            var root = new RootCommand("DWSIM Pure-Compound Data - multi-source indexer, estimator, and builder");

            var downloadCmd = new Command("download", "Download the NIST ThermoML bulk archive.");
            var urlOpt = new Option<string?>("--url", () => null, "Archive URL (defaults to NIST 2020-09-30).");
            var destOpt = new Option<string?>("--dest", () => null, "Destination file path.");
            downloadCmd.AddOption(urlOpt);
            downloadCmd.AddOption(destOpt);
            downloadCmd.SetHandler(async (string? url, string? dest) =>
            {
                Environment.ExitCode = await DownloadCommand.RunAsync(url, dest, stdout, stderr);
            }, urlOpt, destOpt);
            root.AddCommand(downloadCmd);

            var ingestCmd = new Command("ingest", "Parse the archive and populate the LiteDB index.");
            var archiveOpt = new Option<string?>("--archive", () => null, "Path to tar.gz archive.");
            var dbOptI = new Option<string?>("--db", () => null, "Path to LiteDB index file.");
            ingestCmd.AddOption(archiveOpt);
            ingestCmd.AddOption(dbOptI);
            ingestCmd.SetHandler((string? archive, string? db) =>
            {
                Environment.ExitCode = IngestCommand.Run(archive, db, stdout, stderr);
            }, archiveOpt, dbOptI);
            root.AddCommand(ingestCmd);

            var searchCmd = new Command("search", "Find pure-compound records.");
            var dbOptS = new Option<string?>("--db", () => null);
            var casOpt = new Option<string?>("--cas", () => null);
            var nameOpt = new Option<string?>("--name", () => null);
            var inchiOpt = new Option<string?>("--inchikey", () => null);
            var catOpt = new Option<string?>("--category", () => null, "e.g. VaporPressure, LiquidDensity.");
            var tMinOpt = new Option<double?>("--tmin", () => null);
            var tMaxOpt = new Option<double?>("--tmax", () => null);
            var fmtOpt = new Option<string?>("--format", () => "table", "table | json");
            var limitOpt = new Option<int>("--limit", () => 50);
            foreach (var o in new Option[] { dbOptS, casOpt, nameOpt, inchiOpt, catOpt, tMinOpt, tMaxOpt, fmtOpt, limitOpt })
                searchCmd.AddOption(o);
            searchCmd.SetHandler(ctx =>
            {
                var p = ctx.ParseResult;
                Environment.ExitCode = SearchCommand.Run(
                    p.GetValueForOption(dbOptS),
                    p.GetValueForOption(casOpt),
                    p.GetValueForOption(nameOpt),
                    p.GetValueForOption(inchiOpt),
                    p.GetValueForOption(catOpt),
                    p.GetValueForOption(tMinOpt),
                    p.GetValueForOption(tMaxOpt),
                    p.GetValueForOption(fmtOpt),
                    p.GetValueForOption(limitOpt),
                    stdout, stderr);
            });
            root.AddCommand(searchCmd);

            var showCmd = new Command("show", "Show a single record by id.");
            var dbOptSh = new Option<string?>("--db", () => null);
            var idOpt = new Option<string>("--id") { IsRequired = true };
            showCmd.AddOption(dbOptSh);
            showCmd.AddOption(idOpt);
            showCmd.SetHandler((string? db, string id) =>
            {
                Environment.ExitCode = ShowCommand.Run(db, id, stdout, stderr);
            }, dbOptSh, idOpt);
            root.AddCommand(showCmd);

            var statsCmd = new Command("stats", "Summary statistics for the index.");
            var dbOptSt = new Option<string?>("--db", () => null);
            statsCmd.AddOption(dbOptSt);
            statsCmd.SetHandler((string? db) =>
            {
                Environment.ExitCode = StatsCommand.Run(db, stdout, stderr);
            }, dbOptSt);
            root.AddCommand(statsCmd);

            var buildCmd = new Command("build", "Assemble a complete ConstantProperties for a CAS (fits + estimates).");
            var dbOptB = new Option<string?>("--db", () => null);
            var casOptB = new Option<string>("--cas") { IsRequired = true };
            var outOpt = new Option<string?>("--out", () => null, "Write JSON to this file instead of stdout.");
            buildCmd.AddOption(dbOptB);
            buildCmd.AddOption(casOptB);
            buildCmd.AddOption(outOpt);
            buildCmd.SetHandler((string? db, string cas, string? outPath) =>
            {
                Environment.ExitCode = BuildCommand.Run(db, cas, outPath, stdout, stderr);
            }, dbOptB, casOptB, outOpt);
            root.AddCommand(buildCmd);

            return root.InvokeAsync(args);
        }
    }
}
