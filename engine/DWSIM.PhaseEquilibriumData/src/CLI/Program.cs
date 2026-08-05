using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using DWSIM.PhaseEquilibriumData.CLI.Commands;

namespace DWSIM.PhaseEquilibriumData.CLI
{
    internal static class Program
    {
        public static Task<int> Main(string[] args)
        {
            var stdout = Console.Out;
            var stderr = Console.Error;

            var root = new RootCommand("DWSIM Phase Equilibrium Data - ThermoML indexer CLI");

            // download
            var downloadCmd = new Command("download", "Download the NIST ThermoML bulk archive.");
            var urlOpt = new Option<string?>("--url", () => null, "Archive URL.");
            var destOpt = new Option<string?>("--dest", () => null, "Destination path.");
            downloadCmd.AddOption(urlOpt);
            downloadCmd.AddOption(destOpt);
            downloadCmd.SetHandler(async (string? url, string? dest) =>
            {
                Environment.ExitCode = await DownloadCommand.RunAsync(url, dest, stdout, stderr);
            }, urlOpt, destOpt);
            root.AddCommand(downloadCmd);

            // ingest
            var ingestCmd = new Command("ingest", "Parse the archive and populate the index.");
            var archiveOpt = new Option<string?>("--archive", () => null, "Path to tar.gz archive.");
            var dbOptI = new Option<string?>("--db", () => null, "Path to LiteDB index file.");
            ingestCmd.AddOption(archiveOpt);
            ingestCmd.AddOption(dbOptI);
            ingestCmd.SetHandler((string? archive, string? db) =>
            {
                Environment.ExitCode = IngestCommand.Run(archive, db, stdout, stderr);
            }, archiveOpt, dbOptI);
            root.AddCommand(ingestCmd);

            // search
            var searchCmd = new Command("search", "Find binary datasets by CAS pair.");
            var dbOptS = new Option<string?>("--db", () => null);
            var cas1Opt = new Option<string>("--cas1") { IsRequired = true };
            var cas2Opt = new Option<string>("--cas2") { IsRequired = true };
            var typeOpt = new Option<string?>("--type", () => null);
            var tMinOpt = new Option<double?>("--tmin", () => null);
            var tMaxOpt = new Option<double?>("--tmax", () => null);
            var pMinOpt = new Option<double?>("--pmin", () => null);
            var pMaxOpt = new Option<double?>("--pmax", () => null);
            var fmtOpt = new Option<string?>("--format", () => "table");
            var limitOpt = new Option<int>("--limit", () => 50);
            searchCmd.AddOption(dbOptS);
            searchCmd.AddOption(cas1Opt);
            searchCmd.AddOption(cas2Opt);
            searchCmd.AddOption(typeOpt);
            searchCmd.AddOption(tMinOpt);
            searchCmd.AddOption(tMaxOpt);
            searchCmd.AddOption(pMinOpt);
            searchCmd.AddOption(pMaxOpt);
            searchCmd.AddOption(fmtOpt);
            searchCmd.AddOption(limitOpt);
            searchCmd.SetHandler(ctx =>
            {
                var p = ctx.ParseResult;
                Environment.ExitCode = SearchCommand.Run(
                    p.GetValueForOption(dbOptS),
                    p.GetValueForOption(cas1Opt)!,
                    p.GetValueForOption(cas2Opt)!,
                    p.GetValueForOption(typeOpt),
                    p.GetValueForOption(tMinOpt),
                    p.GetValueForOption(tMaxOpt),
                    p.GetValueForOption(pMinOpt),
                    p.GetValueForOption(pMaxOpt),
                    p.GetValueForOption(fmtOpt),
                    p.GetValueForOption(limitOpt),
                    stdout, stderr);
            });
            root.AddCommand(searchCmd);

            // show
            var showCmd = new Command("show", "Show a single dataset by id.");
            var dbOptSh = new Option<string?>("--db", () => null);
            var idOpt = new Option<string>("--id") { IsRequired = true };
            var fmtOptSh = new Option<string?>("--format", () => "table");
            showCmd.AddOption(dbOptSh);
            showCmd.AddOption(idOpt);
            showCmd.AddOption(fmtOptSh);
            showCmd.SetHandler((string? db, string id, string? fmt) =>
            {
                Environment.ExitCode = ShowCommand.Run(db, id, fmt, stdout, stderr);
            }, dbOptSh, idOpt, fmtOptSh);
            root.AddCommand(showCmd);

            // stats
            var statsCmd = new Command("stats", "Summary statistics for the index.");
            var dbOptSt = new Option<string?>("--db", () => null);
            statsCmd.AddOption(dbOptSt);
            statsCmd.SetHandler((string? db) =>
            {
                Environment.ExitCode = StatsCommand.Run(db, stdout, stderr);
            }, dbOptSt);
            root.AddCommand(statsCmd);

            return root.InvokeAsync(args);
        }
    }
}
